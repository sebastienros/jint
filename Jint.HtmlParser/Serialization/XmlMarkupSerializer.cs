namespace Jint.HtmlParser.Serialization;

// DOM Parsing §5.2.1, https://w3c.github.io/DOM-Parsing/#xml-serialization.
// One invocation owns its traversal, namespace bindings and unpublished output.
internal static class XmlMarkupSerializer
{
    internal static string Serialize(Node node, bool requireWellFormed = false,
        SerializationLimits? limits = null, Action<SerializationStage>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new Operation(node, requireWellFormed, limits, checkpoint, cancellationToken).Run(children: false);
    }

    internal static string Serialize(Attr attribute, bool requireWellFormed = false,
        SerializationLimits? limits = null, Action<SerializationStage>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        // Attr's XML serialization is empty. It still checks entry and final cancellation.
        _ = requireWellFormed;
        var writer = new SerializationWriter(new SerializationWork(cancellationToken, checkpoint), limits);
        return writer.Materialize();
    }

    internal static string SerializeChildren(Node parent, bool requireWellFormed = false,
        SerializationLimits? limits = null, Action<SerializationStage>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent is not Element and not Document and not DocumentFragment)
        {
            throw new ArgumentException("XML children require an Element, Document or DocumentFragment.", nameof(parent));
        }

        return new Operation(parent, requireWellFormed, limits, checkpoint, cancellationToken).Run(children: true);
    }

    private sealed class Operation
    {
        private struct Frame(Node node, Document document, ulong documentStamp, string? inheritedNamespace, int boundary)
        {
            internal readonly Node Node = node;
            internal readonly Document Document = document;
            internal readonly ulong DocumentStamp = documentStamp;
            internal readonly string? InheritedNamespace = inheritedNamespace;
            internal string? ChildNamespace = inheritedNamespace;
            internal readonly int Boundary = boundary;
            internal Node? NextChild;
            internal string? ClosingName;
            internal bool Entered;
        }

        private readonly Node _root;
        private readonly Document _rootDocument;
        private readonly bool _wellFormed;
        private readonly SerializationWork _work;
        private readonly SerializationWriter _writer;
        private readonly XmlNamespaceScope _scope;
        private readonly ulong _rootStamp;
        // Every owner reached is verified again at the end; the root and active owners are also
        // cached in fields so the per-node checks never hash.
        private readonly Dictionary<Document, ulong> _stamps = new();
        // Per-element scratch sets, cleared at each element instead of allocated.
        private readonly HashSet<string> _localReserved = new(StringComparer.Ordinal);
        private readonly HashSet<string> _localDeclared = new(StringComparer.Ordinal);
        private HashSet<(string?, string)>? _expandedNames;
        private Frame[] _frames = new Frame[16];
        private int _depth;
        private Document? _activeDocument;
        private ulong _activeStamp;
        private Node? _activeNode;

        internal Operation(Node root, bool wellFormed, SerializationLimits? limits,
            Action<SerializationStage>? checkpoint, CancellationToken cancellationToken)
        {
            _root = root;
            _rootDocument = root as Document ?? root.OwnerDocument ??
                throw new ArgumentException("A native owner document is required.", nameof(root));
            _wellFormed = wellFormed;
            var initialStamp = _rootDocument.MutationStamp;
            if (initialStamp == ulong.MaxValue) throw Invalidated();
            _rootStamp = initialStamp;
            _stamps.Add(_rootDocument, initialStamp);
            _work = new SerializationWork(cancellationToken, stage =>
            {
                CheckActive();
                checkpoint?.Invoke(stage);
                CheckActive();
            });
            _writer = new SerializationWriter(_work, limits);
            _scope = new XmlNamespaceScope(_work);
            CheckActive();
        }

        internal string Run(bool children)
        {
            if (children)
            {
                // A synthetic fragment isolates each top-level child from the parent's
                // ancestors and siblings without creating or moving a native node.
                var container = EffectiveContainer(_root);
                Push(container, null);
            }
            else
            {
                Push(_root, null);
            }

            while (_depth != 0)
            {
                _work.Charge(1, SerializationStage.Scan);
                ref var frame = ref _frames[_depth - 1];
                _activeDocument = frame.Document;
                _activeStamp = frame.DocumentStamp;
                _activeNode = frame.Node;
                CheckActive();
                if (!frame.Entered)
                {
                    frame.Entered = true;
                    Enter(ref frame, children && _depth == 1);
                }

                if (frame.NextChild is { } child)
                {
                    frame.NextChild = child.NextSibling;
                    Push(child, frame.ChildNamespace);
                    continue;
                }

                if (frame.ClosingName is { } closingName)
                {
                    _writer.Append("</");
                    _writer.Append(closingName);
                    _writer.Append('>');
                }

                _scope.Restore(frame.Boundary);
                frame = default;
                _depth--;
                _work.Charge(1, SerializationStage.Scan);
            }

            // Materialization is still private until every captured owner is checked.
            var result = _writer.Materialize();
            foreach (var (document, stamp) in _stamps)
            {
                _work.Charge(1, SerializationStage.Final);
                if (document.MutationStamp != stamp || stamp == ulong.MaxValue)
                {
                    throw Invalidated();
                }
            }

            CheckActive();
            return result;
        }

        private void Push(Node node, string? inheritedNamespace)
        {
            _work.Poll(SerializationStage.Scan);
            var owner = node as Document ?? node.OwnerDocument ?? throw Invalidated();
            var stamp = Capture(owner);
            if (!ReferenceEquals(node as Document ?? node.OwnerDocument, owner)) throw Invalidated();
            if (_depth == _frames.Length) Array.Resize(ref _frames, _frames.Length * 2);
            _frames[_depth++] = new Frame(node, owner, stamp, inheritedNamespace, _scope.Boundary);
            _work.Charge(1, SerializationStage.Scan);
        }

        private void Enter(ref Frame frame, bool childrenRoot)
        {
            CheckFrame(in frame);
            if (childrenRoot)
            {
                frame.NextChild = frame.Node.FirstChild;
                return;
            }

            switch (frame.Node)
            {
                case Element element:
                    WriteElement(ref frame, element);
                    break;
                case Document document:
                    if (_wellFormed)
                    {
                        var foundElement = false;
                        for (var child = document.FirstChild; child is not null; child = child.NextSibling)
                        {
                            _work.Charge(1, SerializationStage.Scan);
                            if (child is not Element) continue;
                            foundElement = true;
                            break;
                        }

                        if (!foundElement) Invalid("An XML document has no document element.");
                    }

                    frame.NextChild = document.FirstChild;
                    break;
                case DocumentFragment:
                    frame.NextChild = frame.Node.FirstChild;
                    break;
                case Text text:
                    XmlScalarSerializer.WriteText(text, _writer, _wellFormed);
                    break;
                case CDataSection cdata:
                    XmlScalarSerializer.WriteCData(cdata, _writer, _wellFormed);
                    break;
                case Comment comment:
                    XmlScalarSerializer.WriteComment(comment, _writer, _wellFormed);
                    break;
                case ProcessingInstruction instruction:
                    XmlScalarSerializer.WriteProcessingInstruction(instruction, _writer, _wellFormed);
                    break;
                case DocumentType doctype:
                    XmlScalarSerializer.WriteDocumentType(doctype, _writer, _wellFormed);
                    break;
                default:
                    throw new ArgumentException("The native node kind cannot be XML serialized.", nameof(frame));
            }
        }

        private void WriteElement(ref Frame frame, Element element)
        {
            if (element.TemplateContent is { } templateContents) Capture(templateContents.OwnerDocument!);
            ValidateName(element.LocalName);
            _work.Poll(SerializationStage.Scan);
            var attributes = element.AttributeSpan;
            var localReserved = _localReserved;
            var localDeclared = _localDeclared;
            localReserved.Clear();
            localDeclared.Clear();
            _work.Poll(SerializationStage.Scan);
            string? localDefault = null;
            foreach (var attribute in attributes)
            {
                _work.Charge(1 + attribute.LocalName.Length + attribute.Value.Length, SerializationStage.Scan);
                if (attribute.NamespaceUri != Namespaces.Xmlns) continue;
                if (attribute.Prefix is null)
                {
                    localDefault = attribute.Value;
                    continue;
                }

                var prefix = attribute.LocalName;
                _work.Charge(prefix.Length + attribute.Value.Length, SerializationStage.Scan);
                localReserved.Add(prefix);
                if (attribute.Value is Namespaces.Xml or "" || prefix is "xml" or "xmlns") continue;
                if (_scope.Effective(prefix) != attribute.Value) localDeclared.Add(prefix);
                _scope.Bind(prefix, attribute.Value);
            }

            var ns = element.NamespaceUri;
            var inherited = frame.InheritedNamespace;
            var prefixToWrite = (string?) null;
            var emitDefault = false;
            var ignoreDefault = false;
            var generatedPrefix = false;
            if (ns == Namespaces.Xml)
            {
                prefixToWrite = "xml";
                ignoreDefault = ns == inherited;
            }
            else if (ns == inherited)
            {
                ignoreDefault = localDefault is not null;
            }
            else
            {
                var ownPrefix = element.Prefix;
                if (ownPrefix == "xmlns" && _wellFormed) Invalid("An XML element cannot use the xmlns prefix.");
                prefixToWrite = ns is null ? null : _scope.Preferred(ns, ownPrefix);
                if (prefixToWrite is null && ownPrefix is not null && ns is not null)
                {
                    prefixToWrite = localReserved.Contains(ownPrefix) || ownPrefix == "xml"
                        ? _scope.Generate(localReserved)
                        : ownPrefix;
                    generatedPrefix = true;
                    _scope.Bind(prefixToWrite, ns);
                }
                else if (prefixToWrite is null && (localDefault is null || EmptyAsNull(localDefault) != ns))
                {
                    emitDefault = true;
                    ignoreDefault = true;
                }
            }

            var qualifiedName = prefixToWrite is null ? element.LocalName : string.Concat(prefixToWrite, ":", element.LocalName);
            _work.Charge(qualifiedName.Length, SerializationStage.Scan);
            _writer.Append('<');
            _writer.Append(qualifiedName);
            if (generatedPrefix)
            {
                WriteDeclaration(prefixToWrite!, ns!);
            }

            if (emitDefault)
            {
                WriteDeclaration(null, ns ?? string.Empty);
            }

            if (prefixToWrite is not null) localReserved.Add(prefixToWrite);

            frame.ChildNamespace = prefixToWrite is null ? ns :
                localDefault is not null && localDefault != Namespaces.Xml ? EmptyAsNull(localDefault) : inherited;
            if (ignoreDefault) frame.ChildNamespace = prefixToWrite is null ? ns : inherited;
            WriteAttributes(attributes, localReserved, localDeclared, ignoreDefault);

            var container = EffectiveContainer(element);
            if (container.FirstChild is null)
            {
                if (ns != Namespaces.Html)
                {
                    _writer.Append("/>");
                    return;
                }

                if (IsHtmlVoid(element.LocalName))
                {
                    _writer.Append(" />");
                    return;
                }
            }

            _writer.Append('>');
            frame.ClosingName = qualifiedName;
            frame.NextChild = container.FirstChild;
        }

        private void WriteAttributes(ReadOnlySpan<Attr> attributes, HashSet<string> localReserved,
            HashSet<string> localDeclared, bool ignoreDefault)
        {
            _work.Poll(SerializationStage.Scan);
            HashSet<(string?, string)>? expandedNames = null;
            if (_wellFormed)
            {
                expandedNames = _expandedNames ??= [];
                expandedNames.Clear();
            }
            _work.Poll(SerializationStage.Scan);
            foreach (var attribute in attributes)
            {
                var ns = attribute.NamespaceUri;
                var local = attribute.LocalName;
                var value = attribute.Value;
                _work.Charge(1 + (ns?.Length ?? 0) + local.Length + value.Length, SerializationStage.Scan);
                if (expandedNames is not null && !expandedNames.Add((ns, local))) Invalid("Duplicate expanded XML attribute name.");
                if (ns == Namespaces.Xmlns)
                {
                    if (value == Namespaces.Xml || attribute.Prefix is null && ignoreDefault) continue;
                    if (attribute.Prefix is not null)
                    {
                        if (_wellFormed && (value.Length == 0 || value == Namespaces.Xmlns))
                            Invalid("Invalid XML namespace declaration.");
                        if (value.Length != 0 && !localDeclared.Contains(local)) continue;
                        var effective = _scope.Effective(local);
                        if (value.Length != 0 && effective != value) continue;
                    }
                    else if (_wellFormed && value == Namespaces.Xmlns)
                    {
                        Invalid("The XMLNS URI cannot be a default namespace.");
                    }

                    WriteAttribute(attribute.Prefix is null ? "xmlns" : string.Concat("xmlns:", local), value);
                    continue;
                }

                ValidateName(local);
                if (_wellFormed && ns is null && local == "xmlns") Invalid("An unnamespaced xmlns attribute is invalid.");
                string? prefix = null;
                if (ns is not null)
                {
                    prefix = ns == Namespaces.Xml ? "xml" : _scope.Preferred(ns, attribute.Prefix);
                    if (prefix is null)
                    {
                        var own = attribute.Prefix;
                        prefix = own is not null && !localReserved.Contains(own) && own != "xml"
                            ? own : _scope.Generate(localReserved);
                        _scope.Bind(prefix, ns);
                        localReserved.Add(prefix);
                        WriteDeclaration(prefix, ns);
                    }

                    localReserved.Add(prefix);
                }

                WriteAttribute(prefix is null ? local : string.Concat(prefix, ":", local), value);
            }
        }

        private void WriteDeclaration(string? prefix, string uri)
        {
            _writer.Append(prefix is null ? " xmlns=\"" : " xmlns:");
            if (prefix is not null)
            {
                _writer.Append(prefix);
                _writer.Append("=\"");
            }

            XmlScalarSerializer.WriteAttributeValue(uri, _writer, _wellFormed);
            _writer.Append('"');
        }

        private void WriteAttribute(string name, string value)
        {
            _work.Charge(name.Length, SerializationStage.Scan);
            _writer.Append(' ');
            _writer.Append(name);
            _writer.Append("=\"");
            XmlScalarSerializer.WriteAttributeValue(value, _writer, _wellFormed);
            _writer.Append('"');
        }

        private void ValidateName(string name)
        {
            if (!_wellFormed) return;
            if (name.Length == 0) Invalid("An XML name is empty.");
            var first = true;
            for (var index = 0; index < name.Length;)
            {
                var character = name[index++];
                _work.Charge(1, SerializationStage.Scan);
                int scalar = character;
                if (char.IsHighSurrogate(character) && index < name.Length && char.IsLowSurrogate(name[index]))
                {
                    scalar = char.ConvertToUtf32(character, name[index++]);
                    _work.Charge(1, SerializationStage.Scan);
                }
                else if (char.IsSurrogate(character)) Invalid("An XML name contains invalid UTF-16.");
                var valid = IsNameStart(scalar) || !first && scalar is ('-' or '.' or >= '0' and <= '9' or
                    0xB7 or >= 0x0300 and <= 0x036F or >= 0x203F and <= 0x2040);
                if (scalar == ':' || !valid)
                    Invalid("An XML local name does not match Name without colon.");
                first = false;
            }
        }

        private static bool IsNameStart(int scalar)
            => scalar is '_' or >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                >= 0xC0 and <= 0xD6 or >= 0xD8 and <= 0xF6 or >= 0xF8 and <= 0x2FF or
                >= 0x370 and <= 0x37D or >= 0x37F and <= 0x1FFF or >= 0x200C and <= 0x200D or
                >= 0x2070 and <= 0x218F or >= 0x2C00 and <= 0x2FEF or >= 0x3001 and <= 0xD7FF or
                >= 0xF900 and <= 0xFDCF or >= 0xFDF0 and <= 0xFFFD or >= 0x10000 and <= 0xEFFFF;

        private static Node EffectiveContainer(Node node) => node is Element { TemplateContent: { } contents } ? contents : node;
        private static string? EmptyAsNull(string value) => value.Length == 0 ? null : value;
        private static bool IsHtmlVoid(string name) => name is "area" or "base" or "basefont" or "bgsound" or
            "br" or "col" or "embed" or "frame" or "hr" or "img" or "input" or "keygen" or "link" or
            "menuitem" or "meta" or "param" or "source" or "track" or "wbr";

        private ulong Capture(Document document)
        {
            var observed = document.MutationStamp;
            if (observed == ulong.MaxValue) throw Invalidated();
            if (ReferenceEquals(document, _activeDocument))
            {
                if (_activeStamp != observed) throw Invalidated();
            }
            else if (_stamps.TryGetValue(document, out var prior))
            {
                if (prior != observed) throw Invalidated();
            }
            else
            {
                _stamps.Add(document, observed);
            }
            _work.Poll(SerializationStage.Scan);
            if (document.MutationStamp != observed) throw Invalidated();
            return observed;
        }

        private void CheckActive()
        {
            if (_rootDocument.MutationStamp != _rootStamp ||
                !ReferenceEquals(_root as Document ?? _root.OwnerDocument, _rootDocument)) throw Invalidated();
            if (_activeDocument is { } active && active.MutationStamp != _activeStamp)
                throw Invalidated();
            if (_activeNode is { } node &&
                !ReferenceEquals(node as Document ?? node.OwnerDocument, _activeDocument)) throw Invalidated();
        }

        private void CheckFrame(in Frame frame)
        {
            CheckActive();
            if (!ReferenceEquals(frame.Node as Document ?? frame.Node.OwnerDocument, frame.Document)) throw Invalidated();
        }

        private static InvalidOperationException Invalidated()
            => new("The native XML serialization was invalidated by mutation.");

        private static void Invalid(string message) => throw new DomException("InvalidStateError", message);
    }
}

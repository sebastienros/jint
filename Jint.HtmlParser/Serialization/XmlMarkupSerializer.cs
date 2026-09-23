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
        private sealed class Frame(Node node, Document document, string? inheritedNamespace, int boundary)
        {
            internal Node Node = node;
            internal Document Document = document;
            internal string? InheritedNamespace = inheritedNamespace;
            internal string? ChildNamespace = inheritedNamespace;
            internal int Boundary = boundary;
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
        private readonly Dictionary<Document, ulong> _stamps = new();
        private readonly Stack<Frame> _stack = new();
        private Document? _activeDocument;
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

            while (_stack.Count != 0)
            {
                _work.Charge(1, SerializationStage.Scan);
                var frame = _stack.Peek();
                _activeDocument = frame.Document;
                _activeNode = frame.Node;
                CheckActive();
                if (!frame.Entered)
                {
                    frame.Entered = true;
                    Enter(frame, children && _stack.Count == 1);
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
                _stack.Pop();
                _work.Charge(1, SerializationStage.Scan);
            }

            // Materialization is still private until every captured owner is checked.
            var result = _writer.Materialize();
            foreach (var pair in _stamps)
            {
                _work.Charge(1, SerializationStage.Final);
                if (pair.Key.MutationStamp != pair.Value || pair.Value == ulong.MaxValue)
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
            Capture(owner);
            if (!ReferenceEquals(node as Document ?? node.OwnerDocument, owner)) throw Invalidated();
            _stack.Push(new Frame(node, owner, inheritedNamespace, _scope.Boundary));
            _work.Charge(1, SerializationStage.Scan);
        }

        private void Enter(Frame frame, bool childrenRoot)
        {
            CheckFrame(frame);
            if (childrenRoot)
            {
                frame.NextChild = frame.Node.FirstChild;
                return;
            }

            switch (frame.Node)
            {
                case Element element:
                    WriteElement(frame, element);
                    break;
                case Document or DocumentFragment:
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

        private void WriteElement(Frame frame, Element element)
        {
            if (element.TemplateContent is { } templateContents) Capture(templateContents.OwnerDocument!);
            ValidateName(element.LocalName);
            _work.Poll(SerializationStage.Scan);
            var attributes = new List<Attr>(element.AttributeCount);
            var localReserved = new HashSet<string>(element.AttributeCount, StringComparer.Ordinal);
            var localDeclared = new HashSet<string>(element.AttributeCount, StringComparer.Ordinal);
            _work.Poll(SerializationStage.Scan);
            string? localDefault = null;
            foreach (var attribute in element.Attributes)
            {
                _work.Charge(1 + attribute.LocalName.Length + attribute.Value.Length, SerializationStage.Scan);
                attributes.Add(attribute);
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
                    prefixToWrite = localReserved.Contains(ownPrefix) || _scope.Effective(ownPrefix) is not null
                        ? _scope.Generate(ns, localReserved)
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

        private void WriteAttributes(List<Attr> attributes, HashSet<string> localReserved,
            HashSet<string> localDeclared, bool ignoreDefault)
        {
            _work.Poll(SerializationStage.Scan);
            HashSet<(string?, string)>? expandedNames = _wellFormed ? new(attributes.Count) : null;
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
                        prefix = own is not null && !localReserved.Contains(own) && _scope.Effective(own) is null
                            ? own : _scope.Generate(ns, localReserved);
                        _scope.Bind(prefix, ns);
                        localReserved.Add(prefix);
                        WriteDeclaration(prefix, ns);
                    }
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

        private void Capture(Document document)
        {
            var observed = document.MutationStamp;
            if (observed == ulong.MaxValue) throw Invalidated();
            if (_stamps.TryGetValue(document, out var prior))
            {
                if (prior != observed) throw Invalidated();
            }
            else
            {
                _stamps.Add(document, observed);
            }
            _work.Poll(SerializationStage.Scan);
            if (document.MutationStamp != observed) throw Invalidated();
        }

        private void CheckActive()
        {
            if (_stamps.TryGetValue(_rootDocument, out var rootStamp) &&
                (_rootDocument.MutationStamp != rootStamp ||
                 !ReferenceEquals(_root as Document ?? _root.OwnerDocument, _rootDocument))) throw Invalidated();
            if (_activeDocument is { } active && _stamps.TryGetValue(active, out var stamp) && active.MutationStamp != stamp)
                throw Invalidated();
            if (_activeNode is { } node &&
                !ReferenceEquals(node as Document ?? node.OwnerDocument, _activeDocument)) throw Invalidated();
        }

        private void CheckFrame(Frame frame)
        {
            CheckActive();
            if (!ReferenceEquals(frame.Node as Document ?? frame.Node.OwnerDocument, frame.Document)) throw Invalidated();
        }

        private static InvalidOperationException Invalidated()
            => new("The native XML serialization was invalidated by mutation.");

        private static void Invalid(string message) => throw new DomException("InvalidStateError", message);
    }
}

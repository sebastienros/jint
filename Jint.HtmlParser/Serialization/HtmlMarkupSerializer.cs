namespace Jint.HtmlParser.Serialization;

// HTML Standard §13.3, https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments.
// The native tree is read in place; this operation owns its stack, shadow selection and unpublished output.
internal static class HtmlMarkupSerializer
{
    private const string XLinkNamespace = "http://www.w3.org/1999/xlink";
    internal static string Serialize(Node node, HtmlSerializationOptions? options = null,
        SerializationLimits? limits = null, Action<SerializationStage>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new Operation(node, options ?? HtmlSerializationOptions.Default, limits, checkpoint,
            cancellationToken).Run(children: false);
    }

    internal static string SerializeChildren(Node parent, HtmlSerializationOptions? options = null,
        SerializationLimits? limits = null, Action<SerializationStage>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent is not Element and not Document and not DocumentFragment)
            throw new ArgumentException("HTML children require an Element, Document or DocumentFragment.", nameof(parent));
        return new Operation(parent, options ?? HtmlSerializationOptions.Default, limits, checkpoint,
            cancellationToken).Run(children: true);
    }

    private sealed class Operation
    {
        private struct Frame(Node node, Document owner, ulong ownerStamp, bool scripting, bool suppressTag, bool shadowWrapper)
        {
            internal readonly Node Node = node;
            internal readonly Document Owner = owner;
            internal readonly ulong OwnerStamp = ownerStamp;
            internal readonly bool Scripting = scripting;
            internal readonly bool SuppressTag = suppressTag;
            internal readonly bool ShadowWrapper = shadowWrapper;
            internal bool Entered;
            internal ShadowRoot? PendingShadow;
            internal DocumentFragment? PendingContent;
            internal Node? NextChild;
            internal string? ClosingName;
        }

        private readonly Node _root;
        private readonly Document _rootOwner;
        private readonly ulong _rootStamp;
        private readonly CancellationToken _cancellationToken;
        private readonly HtmlSerializationOptions _options;
        private readonly SerializationWork _work;
        private readonly SerializationWriter _writer;
        // Every owner reached is verified again at the end; the root and active owners are also
        // cached in fields so the per-node checks never hash.
        private readonly Dictionary<Document, ulong> _stamps = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<ShadowRoot> _selectedRoots = new(ReferenceEqualityComparer.Instance);
        private Frame[] _frames = new Frame[16];
        private int _depth;
        private Document? _activeOwner;
        private ulong _activeStamp;
        private Node? _activeNode;

        internal Operation(Node root, HtmlSerializationOptions options, SerializationLimits? limits,
            Action<SerializationStage>? checkpoint, CancellationToken cancellationToken)
        {
            _root = root;
            _rootOwner = OwnerOf(root) ?? throw new ArgumentException("A native owner document is required.", nameof(root));
            _cancellationToken = cancellationToken;
            _options = options;
            var stamp = _rootOwner.MutationStamp;
            if (stamp == ulong.MaxValue) throw Invalidated();
            _rootStamp = stamp;
            _stamps.Add(_rootOwner, stamp);
            _work = new SerializationWork(cancellationToken, stage =>
            {
                CheckActive();
                checkpoint?.Invoke(stage);
                CheckActive();
            });
            _writer = new SerializationWriter(_work, limits);
            BuildSelection();
        }

        internal string Run(bool children)
        {
            Push(_root, _options.ScriptingEnabled, children);
            while (_depth != 0)
            {
                _work.Charge(1, SerializationStage.HtmlTraversal);
                ref var frame = ref _frames[_depth - 1];
                _activeNode = frame.Node;
                _activeOwner = frame.Owner;
                _activeStamp = frame.OwnerStamp;
                CheckFrame(in frame);
                if (!frame.Entered)
                {
                    frame.Entered = true;
                    Enter(ref frame);
                }

                // Push may grow the frame array, so the current frame is updated before it.
                if (frame.PendingShadow is { } shadow)
                {
                    frame.PendingShadow = null;
                    var scripting = frame.Scripting;
                    WriteShadowStart(shadow);
                    Push(shadow, scripting, suppressTag: true, shadowWrapper: true);
                    continue;
                }

                if (frame.PendingContent is { } content)
                {
                    frame.PendingContent = null;
                    Push(content, frame.Scripting && ReferenceEquals(content.OwnerDocument, frame.Owner),
                        suppressTag: true);
                    continue;
                }

                if (frame.NextChild is { } child)
                {
                    frame.NextChild = child.NextSibling;
                    Push(child, frame.Scripting);
                    continue;
                }

                if (frame.ClosingName is { } name)
                {
                    _writer.Append("</");
                    WriteName(name);
                    _writer.Append('>');
                }
                if (frame.ShadowWrapper) _writer.Append("</template>");
                frame = default;
                _depth--;
                _work.Charge(1, SerializationStage.HtmlTraversal);
            }

            var result = _writer.Materialize();
            _work.Poll(SerializationStage.Final);
            // Charge the whole verification pass before reading stamps. A checkpoint
            // during a per-owner check could mutate an owner already verified.
            _work.Charge(_stamps.Count, SerializationStage.Final);
            var checkedCount = 0;
            foreach (var (owner, stamp) in _stamps)
            {
                if ((checkedCount++ & 255) == 0) _cancellationToken.ThrowIfCancellationRequested();
                if (stamp == ulong.MaxValue || owner.MutationStamp != stamp) throw Invalidated();
            }
            CheckActive();
            return result;
        }

        private void BuildSelection()
        {
            _work.Poll(SerializationStage.HtmlOptions);
            foreach (var root in _options.ShadowRoots)
            {
                _work.Charge(1, SerializationStage.HtmlOptions);
                _selectedRoots.Add(root);
                _work.Poll(SerializationStage.HtmlOptions);
            }
            _work.Poll(SerializationStage.HtmlOptions);
        }

        private void Push(Node node, bool scripting, bool suppressTag = false, bool shadowWrapper = false)
        {
            _work.Poll(SerializationStage.HtmlTraversal);
            var owner = OwnerOf(node) ?? throw Invalidated();
            var stamp = Capture(owner);
            if (!ReferenceEquals(OwnerOf(node), owner)) throw Invalidated();
            if (_depth == _frames.Length) Array.Resize(ref _frames, _frames.Length * 2);
            _frames[_depth++] = new Frame(node, owner, stamp, scripting, suppressTag, shadowWrapper);
            _work.Charge(1, SerializationStage.HtmlTraversal);
        }

        // Run has just checked the frame; nothing runs between that check and entry.
        private void Enter(ref Frame frame)
        {
            switch (frame.Node)
            {
                case Element element:
                    WriteElement(ref frame, element);
                    break;
                case Document or DocumentFragment:
                    frame.NextChild = frame.Node.FirstChild;
                    break;
                case Text text:
                    HtmlScalarSerializer.WriteText(text,
                        text.ParentNode is Element textParent && IsRawText(textParent, frame.Scripting), _writer);
                    break;
                case CDataSection cdata:
                    WriteCharacterData(cdata.Data, cdata.ParentNode, frame.Scripting);
                    break;
                case Comment comment:
                    _writer.Append("<!--");
                    HtmlScalarSerializer.WriteLiteral(comment.Data, _writer, SerializationStage.HtmlEscape);
                    _writer.Append("-->");
                    break;
                case ProcessingInstruction instruction:
                    _writer.Append("<?");
                    WriteName(instruction.Target);
                    _writer.Append(' ');
                    HtmlScalarSerializer.WriteLiteral(instruction.Data, _writer, SerializationStage.HtmlEscape);
                    _writer.Append("?>");
                    break;
                case DocumentType doctype:
                    _writer.Append("<!DOCTYPE ");
                    WriteName(doctype.Name);
                    _writer.Append('>');
                    break;
                default:
                    throw new ArgumentException("The native node kind cannot be HTML serialized.", nameof(frame));
            }
        }

        private void WriteElement(ref Frame frame, Element element)
        {
            var name = ElementName(element);
            if (!frame.SuppressTag)
            {
                _writer.Append('<');
                WriteName(name);
                WriteAttributes(element);
                _writer.Append('>');
                frame.ClosingName = name;
            }

            if (IsVoid(element))
            {
                frame.ClosingName = null;
                return;
            }

            if (element.AttachedShadowRoot is { } shadow &&
                (_selectedRoots.Contains(shadow) || _options.SerializableShadowRoots && shadow.Serializable))
            {
                _work.Charge(1, SerializationStage.HtmlShadow);
                frame.PendingShadow = shadow;
            }

            if (element.TemplateContent is { } content)
            {
                Capture(content.OwnerDocument ?? throw Invalidated());
                frame.PendingContent = content;
            }
            else
            {
                frame.NextChild = element.FirstChild;
            }
        }

        private void WriteAttributes(Element element)
        {
            var attributes = element.AttributeSpan;
            if (element.IsValue is not null)
            {
                var hasIs = false;
                foreach (var attribute in attributes)
                {
                    _work.Charge(1 + attribute.LocalName.Length, SerializationStage.HtmlName);
                    if (attribute.NamespaceUri is null && attribute.LocalName == "is") hasIs = true;
                }
                if (!hasIs)
                {
                    _writer.Append(" is=\"");
                    HtmlScalarSerializer.WriteEscaped(element.IsValue, attribute: true, _writer);
                    _writer.Append('"');
                }
            }

            foreach (var attribute in attributes)
            {
                _work.Charge(1, SerializationStage.HtmlTraversal);
                _writer.Append(' ');
                WriteAttributeName(attribute);
                _writer.Append("=\"");
                HtmlScalarSerializer.WriteEscaped(attribute.ValueSpan, attribute: true, _writer);
                _writer.Append('"');
            }
        }

        private void WriteAttributeName(Attr attribute)
        {
            var ns = attribute.NamespaceUri;
            if (ns == Namespaces.Xml) _writer.Append("xml:");
            else if (ns == Namespaces.Xmlns && attribute.LocalName != "xmlns") _writer.Append("xmlns:");
            else if (ns == XLinkNamespace) _writer.Append("xlink:");
            else if (ns is not null && ns != Namespaces.Xmlns && attribute.Prefix is { } prefix)
            {
                WriteName(prefix);
                _writer.Append(':');
            }
            WriteName(attribute.LocalName);
        }

        private void WriteCharacterData(string data, Node? parent, bool scripting)
        {
            if (parent is Element element && IsRawText(element, scripting))
                HtmlScalarSerializer.WriteLiteral(data, _writer, SerializationStage.HtmlEscape);
            else
                HtmlScalarSerializer.WriteEscaped(data, attribute: false, _writer);
        }

        private void WriteShadowStart(ShadowRoot shadow)
        {
            _work.Poll(SerializationStage.HtmlShadow);
            _writer.Append("<template shadowrootmode=\"");
            _writer.Append(shadow.Mode == ShadowRootMode.Open ? "open" : "closed");
            _writer.Append('"');
            if (shadow.DelegatesFocus) _writer.Append(" shadowrootdelegatesfocus=\"\"");
            if (shadow.Serializable) _writer.Append(" shadowrootserializable=\"\"");
            if (shadow.SlotAssignment == SlotAssignmentMode.Manual)
                _writer.Append(" shadowrootslotassignment=\"manual\"");
            if (shadow.Clonable) _writer.Append(" shadowrootclonable=\"\"");
            var documentRegistry = shadow.OwnerDocument!.CustomElementRegistry;
            var shadowRegistry = shadow.CustomElementRegistry;
            if (!(documentRegistry is null && shadowRegistry is null) &&
                !(documentRegistry is { IsScoped: false } && shadowRegistry is { IsScoped: false }))
                _writer.Append(" shadowrootcustomelementregistry=\"\"");
            _writer.Append('>');
            _work.Poll(SerializationStage.HtmlShadow);
        }

        private void WriteName(string name)
            => HtmlScalarSerializer.WriteLiteral(name, _writer, SerializationStage.HtmlName);

        private string ElementName(Element element)
        {
            if (element.NamespaceUri is Namespaces.Html or Namespaces.Svg or Namespaces.MathMl || element.Prefix is null)
                return element.LocalName;
            _work.Charge(element.Prefix.Length + element.LocalName.Length, SerializationStage.HtmlName);
            _work.Poll(SerializationStage.HtmlName);
            var name = string.Concat(element.Prefix, ":", element.LocalName);
            _work.Poll(SerializationStage.HtmlName);
            return name;
        }

        // HTML §13.3 includes obsolete serialization-only void names; menuitem is not one.
        private static bool IsVoid(Element element) => element.NamespaceUri == Namespaces.Html &&
            element.LocalName is "area" or "base" or "br" or "col" or "embed" or "hr" or "img" or
                "input" or "link" or "meta" or "source" or "track" or "wbr" or "basefont" or
                "bgsound" or "frame" or "keygen" or "param";

        private static bool IsRawText(Element element, bool scripting) => element.NamespaceUri == Namespaces.Html &&
            (element.LocalName is "style" or "script" or "xmp" or "iframe" or "noembed" or
                "noframes" or "plaintext" || scripting && element.LocalName == "noscript");

        private ulong Capture(Document document)
        {
            var observed = document.MutationStamp;
            if (observed == ulong.MaxValue) throw Invalidated();
            if (ReferenceEquals(document, _activeOwner))
            {
                if (_activeStamp != observed) throw Invalidated();
            }
            else if (_stamps.TryGetValue(document, out var prior))
            {
                if (prior != observed) throw Invalidated();
            }
            else _stamps.Add(document, observed);
            _work.Poll(SerializationStage.HtmlTraversal);
            if (document.MutationStamp != observed) throw Invalidated();
            return observed;
        }

        private void CheckActive()
        {
            if (_rootOwner.MutationStamp != _rootStamp ||
                !ReferenceEquals(OwnerOf(_root), _rootOwner)) throw Invalidated();
            if (_activeOwner is { } owner && owner.MutationStamp != _activeStamp) throw Invalidated();
            if (_activeNode is { } node && !ReferenceEquals(OwnerOf(node), _activeOwner)) throw Invalidated();
        }

        private void CheckFrame(in Frame frame)
        {
            CheckActive();
            if (!ReferenceEquals(OwnerOf(frame.Node), frame.Owner)) throw Invalidated();
        }

        private static Document? OwnerOf(Node node) => node as Document ?? node.OwnerDocument;
        private static InvalidOperationException Invalidated()
            => new("The native HTML serialization was invalidated by mutation.");
    }
}

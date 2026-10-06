namespace Jint.HtmlParser.Sanitization;

/// <summary>
/// HTML §8.6.4's <i>sanitize</i> over a native tree: removes the nodes and attributes a
/// <see cref="SanitizerConfiguration"/> does not allow, descending into template contents and shadow roots.
/// </summary>
/// <remarks>
/// <para>
/// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#sanitize. The inner steps are
/// recursive in the specification; here they run on an explicit stack, so a deeply nested input costs
/// heap rather than CLR stack. Each child's next sibling is taken before the child is handled, which is
/// what makes removing it, or replacing it with its already sanitized children, safe to do in the walk.
/// </para>
/// <para>
/// This runs no script and reads no host state; the caller supplies the bounded-work checkpoint.
/// </para>
/// </remarks>
internal static class HtmlSanitizer
{
    private const int CheckpointInterval = 256;

    /// <summary>
    /// Sanitizes <paramref name="node"/>'s descendants. When <paramref name="safe"/> is
    /// <see langword="true"/> the configuration is first copied and has <i>remove unsafe</i> applied to it,
    /// so a <c>Sanitizer</c> a page holds is never modified by being used.
    /// </summary>
    internal static void Sanitize(Node node, SanitizerConfiguration configuration, bool safe,
        Action<int>? checkpoint = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(configuration);
        if (safe)
        {
            configuration = configuration.Clone();
            configuration.RemoveUnsafe();
        }

        if (configuration.AllowsEverything)
        {
            return;
        }

        new Walk(configuration, checkpoint, cancellationToken).Run(node);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/dynamic-markup-insertion.html#contains-a-javascript-url: the
    /// basic URL parser's scheme state, which is all that decides the answer.
    /// </summary>
    /// <remarks>
    /// The parser strips leading and trailing C0 controls and spaces and every ASCII tab or newline, then
    /// reads an ASCII alpha followed by alphanumerics, <c>+</c>, <c>-</c> or <c>.</c> up to a colon. A
    /// <c>javascript:</c> URL whose host would fail to parse (<c>javascript://a b</c>) is still treated as
    /// one — such a URL navigates nowhere, and removing it is the conservative answer.
    /// </remarks>
    internal static bool ContainsJavascriptUrl(string value)
    {
        const string Scheme = "javascript";
        var start = 0;
        var end = value.Length;
        while (start < end && value[start] <= ' ') start++;
        while (end > start && value[end - 1] <= ' ') end--;

        var matched = 0;
        for (var i = start; i < end; i++)
        {
            var c = value[i];
            if (c is '\t' or '\n' or '\r') continue;
            if (c == ':') return matched == Scheme.Length;
            if (matched == Scheme.Length) return false;
            if ((c | 0x20) != Scheme[matched]) return false;
            matched++;
        }

        return false;
    }

    private sealed class Walk(SanitizerConfiguration configuration, Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        private readonly Dictionary<SanitizerName, SanitizerElementRule>? _elements = configuration.Elements?.ToDictionary(static e => e.Name);
        private readonly HashSet<SanitizerName>? _removeElements = configuration.RemoveElements is null ? null : [.. configuration.RemoveElements];
        private readonly HashSet<SanitizerName>? _replaceWithChildren = configuration.ReplaceWithChildrenElements is null ? null : [.. configuration.ReplaceWithChildrenElements];
        private readonly HashSet<SanitizerName>? _attributes = configuration.Attributes is null ? null : [.. configuration.Attributes];
        private readonly HashSet<SanitizerName>? _removeAttributes = configuration.RemoveAttributes is null ? null : [.. configuration.RemoveAttributes];
        private readonly HashSet<string>? _processingInstructions = configuration.ProcessingInstructions is null ? null : new(configuration.ProcessingInstructions, StringComparer.Ordinal);
        private readonly HashSet<string>? _removeProcessingInstructions = configuration.RemoveProcessingInstructions is null ? null : new(configuration.RemoveProcessingInstructions, StringComparer.Ordinal);
        private readonly bool _comments = configuration.Comments == true;
        private readonly bool _dataAttributes = configuration.DataAttributes == true;
        private readonly bool _javascriptUrls = configuration.JavascriptUrls == true;
        private readonly Stack<Frame> _frames = new();
        private int _pending;

        private struct Frame(Node parent, Element? replaceWhenDone)
        {
            internal readonly Node Parent = parent;
            internal readonly Element? ReplaceWhenDone = replaceWhenDone;
            internal Node? Next = parent.FirstChild;
        }

        internal void Run(Node root)
        {
            Check();
            _frames.Push(new Frame(root, null));
            while (_frames.Count > 0)
            {
                Step();
                var top = _frames.Pop();
                if (top.Next is not { } child)
                {
                    if (top.ReplaceWhenDone is { } replaced) ReplaceWithChildren(replaced);
                    continue;
                }

                top.Next = child.NextSibling;
                _frames.Push(top);
                Visit(top.Parent, child);
            }

            Check();
        }

        private void Visit(Node parent, Node child)
        {
            switch (child.NodeType)
            {
                case NodeType.Comment:
                    if (!_comments) parent.RemoveChild(child);
                    return;
                case NodeType.ProcessingInstruction:
                    var target = ((ProcessingInstruction) child).Target;
                    if (_processingInstructions is not null ? !_processingInstructions.Contains(target) : _removeProcessingInstructions!.Contains(target))
                    {
                        parent.RemoveChild(child);
                    }
                    return;
                case NodeType.Element:
                    VisitElement(parent, (Element) child);
                    return;
                default:
                    // Text, CDATA and doctype nodes are kept as they are.
                    return;
            }
        }

        private void VisitElement(Node parent, Element element)
        {
            var name = new SanitizerName(element.LocalName, element.NamespaceUri);
            if (_replaceWithChildren is not null && _replaceWithChildren.Contains(name))
            {
                // Sanitized first, then replaced by its children when this frame completes.
                _frames.Push(new Frame(element, element));
                return;
            }

            SanitizerElementRule? local = null;
            if (_elements is not null)
            {
                if (!_elements.TryGetValue(name, out local))
                {
                    parent.RemoveChild(element);
                    return;
                }
            }
            else if (_removeElements!.Contains(name))
            {
                parent.RemoveChild(element);
                return;
            }

            FilterAttributes(element, name, local);

            // The children are pushed first so that the template contents and the shadow root, pushed after,
            // are walked before them — the specification's order, though nothing here can observe it.
            _frames.Push(new Frame(element, null));
            if (element.AttachedShadowRoot is { } shadow) _frames.Push(new Frame(shadow, null));
            if (name.Namespace == Namespaces.Html && name.Name == "template" && element.TemplateContent is { } content)
            {
                _frames.Push(new Frame(content, null));
            }
        }

        private void FilterAttributes(Element element, SanitizerName elementName, SanitizerElementRule? local)
        {
            List<Attr>? doomed = null;
            foreach (var attribute in element.AttributeSpan)
            {
                Step();
                if (!IsAllowed(elementName, attribute, local))
                {
                    (doomed ??= []).Add(attribute);
                }
            }

            if (doomed is null) return;
            foreach (var attribute in doomed)
            {
                element.RemoveAttributeNode(attribute);
            }
        }

        private bool IsAllowed(SanitizerName elementName, Attr attribute, SanitizerElementRule? local)
        {
            var name = new SanitizerName(attribute.LocalName, attribute.NamespaceUri);
            if (local?.RemoveAttributes is { } localRemove && localRemove.Contains(name)) return false;
            if (_attributes is not null)
            {
                if (!_attributes.Contains(name) && !(local?.Attributes?.Contains(name) ?? false)
                    && !(_dataAttributes && name.Namespace is null && name.Name.StartsWith("data-", StringComparison.Ordinal)))
                {
                    return false;
                }
            }
            else
            {
                if (local?.Attributes is { } localAllow && !localAllow.Contains(name)) return false;
                if (_removeAttributes!.Contains(name)) return false;
            }

            if (_javascriptUrls) return true;
            if (SanitizerBuiltins.IsNavigatingUrlAttribute(elementName.Namespace, elementName.Name, name.Namespace, name.Name)
                && ContainsJavascriptUrl(attribute.Value))
            {
                return false;
            }

            if (elementName.Namespace == Namespaces.MathMl && name.Name == "href"
                && name.Namespace is null or SanitizerBuiltins.XLinkNamespace && ContainsJavascriptUrl(attribute.Value))
            {
                return false;
            }

            return !(SanitizerBuiltins.IsAnimatingUrlAttribute(elementName.Namespace, elementName.Name, name.Namespace, name.Name)
                && attribute.Value is "href" or "xlink:href");
        }

        private void ReplaceWithChildren(Element element)
        {
            var parent = element.ParentNode!;
            var fragment = (parent.OwnerDocument ?? (Document) parent).CreateDocumentFragment();
            while (element.FirstChild is { } child)
            {
                Step();
                fragment.AppendChild(child);
            }

            parent.ReplaceChild(fragment, element);
        }

        private void Step()
        {
            if (++_pending == CheckpointInterval) Check();
        }

        private void Check()
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkpoint?.Invoke(_pending);
            cancellationToken.ThrowIfCancellationRequested();
            _pending = 0;
        }
    }
}

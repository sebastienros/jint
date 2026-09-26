using System.Text;
using System.Xml;
using System.Xml.XPath;

namespace Jint.HtmlParser;

internal enum XPathWorkStage
{
    Other,
    AncestorScan,
    DescendantScan,
    AttributeScan,
    AttributeIndex,
    NamespaceScan,
    NamespaceIndex,
    OrderIndex,
    NameAtomization,
    IdIndex,
    CompilationScan,
    CompilationLookahead,
    ResultMaterialization,
    ResultPublication
}

internal sealed class XPathReadSession
{
    private readonly Document _document;
    private readonly ulong _stamp;
    private readonly CancellationToken _token;
    private readonly Action<XPathWorkStage, int>? _checkpoint;
    private int _work;
    private readonly Dictionary<Element, Attr[]> _attributes = new();
    private readonly Dictionary<Attr, int> _attributeOrder = new();
    private readonly Dictionary<Element, Scope> _scopes = new();
    private readonly Dictionary<(Element, string), XPathNamespaceBinding> _bindings = new();
    private readonly Dictionary<Element, XPathNamespaceBinding[]> _allNamespaces = new();
    private readonly Dictionary<(Element, string), int> _namespaceOrder = new();
    private readonly Dictionary<Node, string> _textValues = new();
    private Dictionary<Node, int>? _order;
    private Dictionary<string, Element>? _ids;

    internal XPathReadSession(Node context, Action<XPathWorkStage, int>? checkpoint, CancellationToken token)
    {
        _token = token;
        _checkpoint = checkpoint;
        _document = context as Document ?? context.OwnerDocument ?? throw new ArgumentException("A native owner document is required.", nameof(context));
        _stamp = _document.MutationStamp;
        if (_stamp == ulong.MaxValue)
        {
            throw new InvalidOperationException("A saturated mutation stamp cannot prove XPath view freshness.");
        }

        _token.ThrowIfCancellationRequested();
        TreeRoot = Top(context);
        RootIdentity = TreeRoot;
        NameTable = new NameTable();
        Check();
    }

    internal XPathReadSession(Attr context, Action<XPathWorkStage, int>? checkpoint, CancellationToken token)
    {
        _token = token;
        _checkpoint = checkpoint;
        _document = context.OwnerDocument;
        _stamp = _document.MutationStamp;
        if (_stamp == ulong.MaxValue)
        {
            throw new InvalidOperationException("A saturated mutation stamp cannot prove XPath view freshness.");
        }

        DetachedAttributeRoot = context;
        RootIdentity = context;
        NameTable = new NameTable();
        Check();
    }

    internal Node? TreeRoot { get; }
    internal Attr? DetachedAttributeRoot { get; }
    internal object RootIdentity { get; }
    internal XmlNameTable NameTable { get; }

    private Node Top(Node node)
    {
        while (node.ParentNode is { } parent)
        {
            node = parent;
            _work++;
            if ((_work & 255) == 0)
            {
                _checkpoint?.Invoke(XPathWorkStage.AncestorScan, _work);
                _token.ThrowIfCancellationRequested();
                if (_document.MutationStamp != _stamp)
                {
                    throw new InvalidOperationException("The native XPath view was invalidated by mutation.");
                }
            }
        }

        _token.ThrowIfCancellationRequested();
        if (_document.MutationStamp != _stamp)
        {
            throw new InvalidOperationException("The native XPath view was invalidated by mutation.");
        }

        return node;
    }

    internal void Check()
    {
        _token.ThrowIfCancellationRequested();
        if (_document.MutationStamp != _stamp || _stamp == ulong.MaxValue ||
            (TreeRoot is { } root &&
             (!ReferenceEquals(root as Document ?? root.OwnerDocument, _document) || root.ParentNode is not null)) ||
            (DetachedAttributeRoot is { } attribute &&
             (!ReferenceEquals(attribute.OwnerDocument, _document) || attribute.OwnerElement is not null)))
        {
            throw new InvalidOperationException("The native XPath view was invalidated by mutation.");
        }
    }

    internal void Work(int units = 1, XPathWorkStage stage = XPathWorkStage.Other)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        var old = _work;
        _work = unchecked(_work + units);
        if ((_work >> 8) != (old >> 8) || _work < old)
        {
            _checkpoint?.Invoke(stage, _work);
            Check();
        }
    }

    internal void CopyWork(string value)
    {
        Check();
        Work(value.Length);
        Check();
    }

    internal void PublishResult()
    {
        Check();
        _checkpoint?.Invoke(XPathWorkStage.ResultPublication, _work);
        Check();
    }

    internal string Atom(string value)
    {
        Check();
        Work(value.Length, XPathWorkStage.NameAtomization);
        Check();
        var atom = NameTable.Add(value);
        Check();
        return atom;
    }

    internal Node Representative(Node node)
    {
        if (!IsText(node)) return node;
        var candidate = node;
        for (var previous = node.PreviousSibling; previous is not null; previous = previous.PreviousSibling)
        {
            Work();
            if (previous is DocumentType) continue;
            if (!IsText(previous)) break;
            if (Data(previous).Length != 0) candidate = previous;
        }

        return candidate;
    }

    internal static bool IsText(Node node) => node is Text or CDataSection;
    internal static string Data(Node node) => node switch
    {
        Text text => text.Data,
        CDataSection cdata => cdata.Data,
        Comment comment => comment.Data,
        ProcessingInstruction pi => pi.Data,
        _ => ""
    };

    internal Node? First(Node parent)
    {
        for (var child = parent.FirstChild; child is not null; child = child.NextSibling)
        {
            Work();
            if (Visible(child)) return child;
        }

        return null;
    }

    internal Node? Next(Node node)
    {
        var skipText = IsText(node);
        for (var next = node.NextSibling; next is not null; next = next.NextSibling)
        {
            Work();
            if (next is DocumentType || IsText(next) && Data(next).Length == 0) continue;
            if (skipText && IsText(next)) continue;
            return next;
        }

        return null;
    }

    internal Node? Previous(Node node)
    {
        for (var previous = node.PreviousSibling; previous is not null; previous = previous.PreviousSibling)
        {
            Work();
            if (!Visible(previous)) continue;
            var representative = Representative(previous);
            if (!ReferenceEquals(representative, node)) return representative;
        }

        return null;
    }

    internal static bool Visible(Node node) => node is not DocumentType && (!IsText(node) || Data(node).Length != 0);

    internal Attr[] Attributes(Element element)
    {
        Check();
        if (_attributes.TryGetValue(element, out var found)) return found;
        var list = new List<Attr>(element.AttributeCount);
        foreach (var attr in element.Attributes)
        {
            Work(1 + attr.LocalName.Length + attr.Value.Length, XPathWorkStage.AttributeScan);
            if (attr.NamespaceUri != Jint.HtmlParser.Namespaces.Xmlns) list.Add(attr);
        }

        Check();
        found = list.ToArray();
        Work(found.Length);
        Check();
        for (var i = 0; i < found.Length; i++)
        {
            Work(1, XPathWorkStage.AttributeIndex);
            _attributeOrder.Add(found[i], i);
        }

        Check();
        _attributes.Add(element, found);
        return found;
    }

    internal int AttributeOrder(Attr attribute)
    {
        Check();
        Attributes(attribute.OwnerElement!);
        return _attributeOrder[attribute];
    }

    internal string TextValue(Node representative)
    {
        Check();
        if (_textValues.TryGetValue(representative, out var value)) return value;
        var builder = new StringBuilder();
        for (var member = representative; member is not null; member = member.NextSibling)
        {
            Work();
            if (member is DocumentType) continue;
            if (!IsText(member)) break;
            var data = Data(member);
            Work(data.Length);
            Check();
            builder.Append(data);
            Check();
        }

        Check();
        value = builder.ToString();
        Work(value.Length);
        Check();
        _textValues.Add(representative, value);
        return value;
    }

    internal string DescendantValue(Node root)
    {
        Check();
        var builder = new StringBuilder();
        var current = root.FirstChild;
        while (current is not null)
        {
            Work(1, XPathWorkStage.DescendantScan);
            if (IsText(current))
            {
                var data = Data(current);
                Work(data.Length, XPathWorkStage.DescendantScan);
                Check();
                builder.Append(data);
                Check();
            }

            if (current.FirstChild is { } child)
            {
                current = child;
                continue;
            }

            while (current is not null && !ReferenceEquals(current, root) && current.NextSibling is null)
            {
                Work(1, XPathWorkStage.DescendantScan);
                current = current.ParentNode;
            }

            current = ReferenceEquals(current, root) ? null : current?.NextSibling;
        }

        Check();
        var value = builder.ToString();
        Work(value.Length);
        Check();
        return value;
    }

    internal Scope ScopeOf(Element element)
    {
        Check();
        if (_scopes.TryGetValue(element, out var known)) return known;
        var path = new Stack<Element>();
        for (Node? current = element; current is Element ancestor; current = current.ParentNode)
        {
            Work(1, XPathWorkStage.NamespaceScan);
            if (_scopes.ContainsKey(ancestor)) break;
            path.Push(ancestor);
        }

        while (path.TryPop(out var next))
        {
            Work(1, XPathWorkStage.NamespaceScan);
            var parent = next.ParentNode as Element;
            var parentMap = parent is null ? null : _scopes[parent].Map;
            Dictionary<string, string>? map = null;
            List<string>? local = null;
            void Bind(string prefix, string uri, bool localBinding = false)
            {
                var source = map ?? parentMap;
                if (source is not null && source.TryGetValue(prefix, out var previous) && previous == uri)
                {
                    if (localBinding) (local ??= []).Add(prefix);
                    return;
                }

                if (map is null)
                {
                    Check();
                    Work(source?.Count ?? 0);
                    Check();
                    map = source is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(source, StringComparer.Ordinal);
                    Check();
                }

                map[prefix] = uri;
                if (localBinding) (local ??= []).Add(prefix);
            }

            if (parentMap is null) Bind("xml", Jint.HtmlParser.Namespaces.Xml);
            var declaredXml = false;
            foreach (var attr in next.Attributes)
            {
                Work(1 + attr.LocalName.Length + attr.Value.Length, XPathWorkStage.NamespaceScan);
                if (attr.NamespaceUri != Jint.HtmlParser.Namespaces.Xmlns) continue;
                var prefix = attr.Prefix == "xmlns" ? attr.LocalName : "";
                if (prefix == "xmlns") continue;
                if (prefix == "xml") declaredXml = true;
                Bind(prefix, attr.Value, localBinding: true);
            }

            // XML's reserved prefix is fixed even if a manually assembled DOM has a bad declaration.
            if (declaredXml)
            {
                Bind("xml", Jint.HtmlParser.Namespaces.Xml, localBinding: true);
            }

            if (next.Prefix is { } ownPrefix)
            {
                Bind(ownPrefix, next.NamespaceUri ?? "", localBinding: true);
            }
            else
            {
                Bind("", next.NamespaceUri ?? "", localBinding: true);
            }

            var effective = map ?? parentMap!;
            Work(local?.Count ?? 0, XPathWorkStage.NamespaceIndex);
            Check();
            var localPrefixes = local?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
            Check();
            _scopes.Add(next, new Scope(effective, localPrefixes));
        }

        Check();
        return _scopes[element];
    }

    internal XPathNamespaceBinding[] Namespaces(Element element, XPathNamespaceScope scope)
    {
        Check();
        if (scope == XPathNamespaceScope.All && _allNamespaces.TryGetValue(element, out var cached)) return cached;
        var info = ScopeOf(element);
        Work(info.Map.Count);
        Check();
        var prefixes = scope == XPathNamespaceScope.Local ? info.Local : info.Map.Keys.ToArray();
        var results = new List<XPathNamespaceBinding>(prefixes.Length);
        foreach (var prefix in prefixes)
        {
            Work(prefix.Length + 1, XPathWorkStage.NamespaceScan);
            if (scope == XPathNamespaceScope.ExcludeXml && prefix == "xml") continue;
            if (info.Map.TryGetValue(prefix, out var uri) && uri.Length != 0)
            {
                var key = (element, prefix);
                if (!_bindings.TryGetValue(key, out var binding))
                {
                    binding = new XPathNamespaceBinding(element, prefix, uri);
                    _bindings.Add(key, binding);
                }

                results.Add(binding);
            }
        }

        Work(results.Count);
        Check();
        results.Sort((a, b) => StringComparer.Ordinal.Compare(a.Prefix, b.Prefix));
        Check();
        var answer = results.ToArray();
        Work(answer.Length);
        Check();
        if (scope == XPathNamespaceScope.All)
        {
            for (var i = 0; i < answer.Length; i++)
            {
                Work(1 + answer[i].Prefix.Length, XPathWorkStage.NamespaceIndex);
                _namespaceOrder.Add((element, answer[i].Prefix), i);
            }

            Check();
            _allNamespaces.Add(element, answer);
        }

        return answer;
    }

    internal int NamespaceOrder(XPathNamespaceBinding binding)
    {
        Check();
        Namespaces(binding.OwnerElement, XPathNamespaceScope.All);
        return _namespaceOrder[(binding.OwnerElement, binding.Prefix)];
    }

    internal XPathNamespaceBinding? BindingFor(Element owner, string prefix, string uri)
    {
        Check();
        Namespaces(owner, XPathNamespaceScope.All);
        return _bindings.TryGetValue((owner, prefix), out var binding) && binding.NamespaceUri == uri ? binding : null;
    }

    // XPath 1.0 §4.1: ID lookup uses DTD-typed attributes in ordinary tree order.
    internal Element? FindId(string id)
    {
        Check();
        if (id.Length == 0 || TreeRoot is null) return null;
        Work(id.Length, XPathWorkStage.IdIndex);
        Check();
        if (_ids is null)
        {
            Check();
            var index = new Dictionary<string, Element>(StringComparer.Ordinal);
            Check();
            var current = TreeRoot;
            while (current is not null)
            {
                Work(1, XPathWorkStage.IdIndex);
                if (current is Element element)
                {
                    foreach (var attribute in element.Attributes)
                    {
                        // Count every native attribute, including untyped and XMLNS attributes.
                        Work(1 + attribute.LocalName.Length, XPathWorkStage.IdIndex);
                        var value = attribute.Value;
                        Work(value.Length, XPathWorkStage.IdIndex);
                        if (attribute.IsDtdId && value.Length != 0)
                        {
                            Check();
                            index.TryAdd(value, element);
                            Check();
                        }
                    }
                }

                if (current.FirstChild is { } child)
                {
                    current = child;
                    continue;
                }

                while (!ReferenceEquals(current, TreeRoot) && current.NextSibling is null)
                {
                    Work(1, XPathWorkStage.IdIndex);
                    current = current.ParentNode!;
                }

                current = ReferenceEquals(current, TreeRoot) ? null : current.NextSibling;
            }

            Check();
            _ids = index;
        }

        Check();
        var found = _ids.TryGetValue(id, out var result) ? result : null;
        Check();
        return found;
    }

    internal int OrderOf(Node node)
    {
        Check();
        if (_order is null)
        {
            var index = new Dictionary<Node, int>();
            var stack = new Stack<Node>();
            stack.Push(TreeRoot!);
            while (stack.TryPop(out var current))
            {
                Work(1, XPathWorkStage.OrderIndex);
                if (Visible(current)) index.Add(current, index.Count);
                for (var child = current.LastChild; child is not null; child = child.PreviousSibling)
                {
                    Work(1, XPathWorkStage.OrderIndex);
                    if (Visible(child)) stack.Push(child);
                }
            }

            Check();
            _order = index;
        }

        return _order[node];
    }

    internal sealed class Scope(Dictionary<string, string> map, string[] local)
    {
        internal Dictionary<string, string> Map { get; } = map;
        internal string[] Local { get; } = local;
    }
}

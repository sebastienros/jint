using Jint.HtmlParser.Css.Selectors;

namespace Jint.HtmlParser;

// DOM §4.2.5: the first descendant in tree order with the given nonempty ID.
// https://dom.spec.whatwg.org/#dom-nonelementparentnode-getelementbyid
// A scope acquires an index only on demand. Parser nodes gain no eager bookkeeping.
// Weak values prevent a warm scope from retaining removed descendants after invalidation.
internal sealed class NativeIdIndex
{
    private Dictionary<string, WeakReference<Element>>? _first;
    // The IDs more than one descendant carries, which only a walk can list in tree order.
    private HashSet<string>? _shared;
    private WeakReference<Document>? _document;
    private ulong _revision;

    internal static Element? Find(Node root, string id, ref SelectorMatchWork work)
    {
        work.Check();
        for (var i = 0; i < id.Length; i++) work.Step();
        if (id.Length == 0) return null;
        var index = Current(root, ref work);
        work.Step();
        var result = index._first!.TryGetValue(id, out var weak) && weak.TryGetTarget(out var elementResult)
            ? elementResult : null;
        work.Check();
        return result;
    }

    /// <summary>
    /// Whether the descendants of <paramref name="root"/> carrying <paramref name="id"/> can be listed from the
    /// index — none, or exactly one, in <paramref name="element"/> — rather than by a walk, which only an ID more
    /// than one of them carries needs: <c>querySelectorAll('#' + id)</c> is how axe-core checks an ID is unique.
    /// </summary>
    internal static bool TryFindOnly(Node root, string id, ref SelectorMatchWork work, out Element? element)
    {
        element = null;
        work.Check();
        for (var i = 0; i < id.Length; i++) work.Step();
        if (id.Length == 0) return true;
        var index = Current(root, ref work);
        work.Step();
        if (index._shared!.Contains(id)) return false;
        element = index._first!.TryGetValue(id, out var weak) && weak.TryGetTarget(out var found) ? found : null;
        work.Check();
        return true;
    }

    // The root's index, rebuilt when the document's ID revision has moved since it was built.
    private static NativeIdIndex Current(Node root, ref SelectorMatchWork work)
    {
        var document = root as Document ?? root.OwnerDocument!;
        var revision = document.IdIndexRevision;
        var index = root.IdIndex;
        if (index._first is null || index._document is null ||
            !index._document.TryGetTarget(out var previous) || !ReferenceEquals(previous, document) ||
            revision == ulong.MaxValue || index._revision != revision)
        {
            var first = new Dictionary<string, WeakReference<Element>>(StringComparer.Ordinal);
            var shared = new HashSet<string>(StringComparer.Ordinal);
            for (var node = root.FirstChild; node is not null;)
            {
                work.Step();
                if (node is Element element)
                {
                    foreach (var attribute in element.AttributeSpan)
                    {
                        work.Step();
                        if (attribute.NamespaceUri is not null || attribute.LocalName != "id") continue;
                        var value = attribute.Value;
                        for (var i = 0; i < value.Length; i++) work.Step();
                        if (value.Length != 0 && !first.TryAdd(value, new WeakReference<Element>(element))) shared.Add(value);
                        break;
                    }
                }
                work.Step();
                if (node.FirstChild is { } child) { node = child; continue; }
                while (!ReferenceEquals(node, root) && node.NextSibling is null)
                {
                    work.Step();
                    node = node.ParentNode!;
                }
                work.Step();
                node = ReferenceEquals(node, root) ? null : node.NextSibling;
            }
            // A callback can mutate or adopt during construction. Never publish its stale work.
            work.Check();
            index._first = first;
            index._shared = shared;
            index._document = new WeakReference<Document>(document);
            index._revision = revision;
        }
        return index;
    }
}

using Jint.HtmlParser;

namespace Jint.Browser.Layout;

// Native measurement data only: never retains a cascade, engine, work callback or token.
// Widths, stretched heights and placement are query-owned because they can depend on siblings.
internal sealed class SizeMeasurements
{
    internal readonly Dictionary<Element, IReadOnlyList<Element>> Children = new(ReferenceEqualityComparer.Instance);
    internal readonly Dictionary<Element, bool> Rendered = new(ReferenceEqualityComparer.Instance);
    internal readonly Dictionary<Element, int> Rows = new(ReferenceEqualityComparer.Instance);

    internal void InvalidateChildren(Element parent, Action checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        checkpoint();
        // Only used when selector dependencies prove that a child-list edit cannot change
        // existing siblings' rendering. Flex sizes and positions are never retained.
        Children.Remove(parent);
        var work = 0;
        for (Element? ancestor = parent; ancestor is not null; ancestor = ancestor.ParentNode as Element)
        {
            if ((++work & 255) == 0) { token.ThrowIfCancellationRequested(); checkpoint(); }
            Rows.Remove(ancestor);
        }
        token.ThrowIfCancellationRequested();
    }

    internal void Invalidate(Element root, Action checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        checkpoint();
        var pending = new Stack<Element>();
        var visited = new HashSet<Element>(ReferenceEqualityComparer.Instance);
        pending.Push(root);
        var work = 0;
        while (pending.TryPop(out var element))
        {
            if ((++work & 255) == 0) { token.ThrowIfCancellationRequested(); checkpoint(); }
            if (!visited.Add(element)) continue;
            // Follow BOTH snapshots and current links: removals/reparenting must evict detached
            // measurements too, without keeping a second document tree.
            if (Children.Remove(element, out var previous))
                foreach (var child in previous) pending.Push(child);
            for (var child = element.FirstChild; child is not null; child = child.NextSibling)
            {
                if ((++work & 255) == 0) { token.ThrowIfCancellationRequested(); checkpoint(); }
                if (child is Element next) pending.Push(next);
            }
            Rendered.Remove(element);
            Rows.Remove(element);
        }
        for (var ancestor = root.ParentNode as Element; ancestor is not null; ancestor = ancestor.ParentNode as Element)
        {
            if ((++work & 255) == 0) { token.ThrowIfCancellationRequested(); checkpoint(); }
            Rows.Remove(ancestor);
        }
        token.ThrowIfCancellationRequested();
    }
}

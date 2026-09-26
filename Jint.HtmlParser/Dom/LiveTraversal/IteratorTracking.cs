namespace Jint.HtmlParser;

internal static class IteratorTracking
{
    internal static void Register(DomNodeIterator iterator)
    {
        var document = LiveTraversalTracking.DocumentOf(iterator.Root);
        Sweep(document);
        iterator.TrackingDocument = document;
        var slots = document.IteratorSlots ??= [];
        iterator.TrackingIndex = slots.Count;
        slots.Add(new(iterator));
        var roots = iterator.Root.Node is { } node ? node.RootIterators ??= [] : iterator.Root.Attribute!.RootIterators ??= [];
        roots.RemoveAll(static slot => !slot.TryGetTarget(out _));
        roots.Add(new(iterator));
    }
    private static void RemoveSlot(Document document, int index)
    {
        var slots = document.IteratorSlots!;
        if (slots[index].TryGetTarget(out var removed)) removed.TrackingIndex = -1;
        var last = slots.Count - 1;
        if (index != last)
        {
            slots[index] = slots[last];
            if (slots[index].TryGetTarget(out var moved)) moved.TrackingIndex = index;
        }
        slots.RemoveAt(last);
        if (slots.Count == 0) document.IteratorSlots = null;
    }
    private static void Sweep(Document document)
    {
        for (var scanned = 0; scanned < 8 && document.IteratorSlots is { Count: > 0 } slots; scanned++)
        {
            var index = document.IteratorSweepCursor % slots.Count;
            if (!slots[index].TryGetTarget(out _)) RemoveSlot(document, index);
            else document.IteratorSweepCursor = index + 1;
        }
    }
    internal static void Remove(Node removed, Document document)
    {
        if (document.IteratorSlots is not { } slots) return;
        foreach (var slot in slots)
            if (slot.TryGetTarget(out var iterator) && ReferenceEquals(LiveTraversalTracking.DocumentOf(iterator.Root), document)) iterator.PreRemove(removed);
        Sweep(document);
    }
    internal static void Rehome(List<WeakReference<DomNodeIterator>>? roots, Document document)
    {
        if (roots is null) return;
        roots.RemoveAll(static slot => !slot.TryGetTarget(out _));
        foreach (var slot in roots)
        {
            if (!slot.TryGetTarget(out var iterator) || ReferenceEquals(iterator.TrackingDocument, document)) continue;
            RemoveSlot(iterator.TrackingDocument, iterator.TrackingIndex);
            iterator.TrackingDocument = document;
            var slots = document.IteratorSlots ??= [];
            iterator.TrackingIndex = slots.Count;
            slots.Add(slot);
        }
    }
}

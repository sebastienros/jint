namespace Jint.HtmlParser;

// DOM §4.2.3 and §4.10: repair live points inside the actual native mutation.
// Documents retain only weak bucket keys; nodes own lazy buckets, ranges own handles.
internal sealed class EndpointBucket
{
    internal readonly WeakReference<object> Owner;
    internal Document Document;
    internal int Index = -1;
    internal int EntrySweepCursor;
    internal readonly List<EndpointHandle> Entries = [];
    internal EndpointBucket(DomNodeIdentity owner, Document document)
    {
        Owner = new(owner.Node as object ?? owner.Attribute!);
        Document = document;
    }
}

internal sealed class EndpointHandle(DomRange range, bool start, EndpointBucket bucket)
{
    internal readonly WeakReference<DomRange> Range = new(range);
    internal readonly bool Start = start;
    internal readonly EndpointBucket Bucket = bucket;
    internal int Index = -1;
}

internal static class LiveTraversalTracking
{
    internal static Document DocumentOf(DomNodeIdentity identity)
        => identity.Node as Document ?? identity.Node?.OwnerDocument ?? identity.Attribute!.OwnerDocument;

    internal static EndpointHandle Register(DomRange range, bool start, DomNodeIdentity identity,
        Action<int>? workCheckpoint = null)
    {
        var work = new RegistrationWork(workCheckpoint);
        Sweep(DocumentOf(identity), ref work);
        var bucket = identity.Node?.RangeEndpoints ?? identity.Attribute?.RangeEndpoints;
        if (bucket is null)
        {
            var document = DocumentOf(identity);
            bucket = new(identity, document);
            if (identity.Node is { } node) node.RangeEndpoints = bucket;
            else identity.Attribute!.RangeEndpoints = bucket;
            Index(bucket, document);
        }
        PruneEntries(bucket, ref work);
        var handle = new EndpointHandle(range, start, bucket);
        handle.Index = bucket.Entries.Count;
        bucket.Entries.Add(handle);
        work.Step();
        return handle;
    }

    internal static void Unregister(EndpointHandle? handle)
    {
        if (handle is null) return;
        var bucket = handle.Bucket;
        if (handle.Index >= 0) RemoveEntry(bucket, handle.Index);
        var work = new RegistrationWork(null);
        PruneEntries(bucket, ref work);
        ReleaseEmptyBucket(bucket);
    }

    private static void RemoveEntry(EndpointBucket bucket, int index)
    {
        var entries = bucket.Entries;
        entries[index].Index = -1;
        var last = entries.Count - 1;
        if (index != last)
        {
            entries[index] = entries[last];
            entries[index].Index = index;
        }
        entries.RemoveAt(last);
    }

    private static void PruneEntries(EndpointBucket bucket, ref RegistrationWork work)
    {
        var budget = Math.Min(8, bucket.Entries.Count);
        for (var scanned = 0; scanned < budget && bucket.Entries.Count != 0; scanned++)
        {
            work.Step();
            var index = bucket.EntrySweepCursor % bucket.Entries.Count;
            if (!bucket.Entries[index].Range.TryGetTarget(out _)) RemoveEntry(bucket, index);
            else bucket.EntrySweepCursor = index + 1;
        }
    }

    private static void ReleaseEmptyBucket(EndpointBucket bucket)
    {
        if (bucket.Entries.Count != 0) return;
        if (bucket.Owner.TryGetTarget(out var owner))
        {
            if (owner is Node node) node.RangeEndpoints = null;
            else ((Attr) owner).RangeEndpoints = null;
        }
        RemoveIndex(bucket);
    }

    private static void Index(EndpointBucket bucket, Document document)
    {
        var index = document.RangeBuckets ??= [];
        bucket.Index = index.Count;
        index.Add(new(bucket));
    }

    private static void RemoveIndex(EndpointBucket bucket)
    {
        if (bucket.Index < 0) return;
        RemoveSlot(bucket.Document, bucket.Index);
        bucket.Index = -1;
    }

    private static void RemoveSlot(Document document, int index)
    {
        var slots = document.RangeBuckets!;
        if (slots[index].TryGetTarget(out var removed)) removed.Index = -1;
        var last = slots.Count - 1;
        if (index != last)
        {
            slots[index] = slots[last];
            if (slots[index].TryGetTarget(out var moved)) moved.Index = index;
        }
        slots.RemoveAt(last);
        if (slots.Count == 0) document.RangeBuckets = null;
    }

    private static void Sweep(Document document)
    {
        var work = new RegistrationWork(null);
        Sweep(document, ref work);
    }

    private static void Sweep(Document document, ref RegistrationWork work)
    {
        var budget = Math.Min(8, document.RangeBuckets?.Count ?? 0);
        for (var scanned = 0; scanned < budget && document.RangeBuckets is { Count: > 0 } slots; scanned++)
        {
            work.Step();
            var index = document.RangeSweepCursor % slots.Count;
            if (!slots[index].TryGetTarget(out var bucket)) { RemoveSlot(document, index); continue; }
            PruneEntries(bucket, ref work);
            if (bucket.Entries.Count == 0) ReleaseEmptyBucket(bucket);
            else document.RangeSweepCursor = index + 1;
        }
    }

    internal static void Rehome(EndpointBucket? bucket, Document document)
    {
        if (bucket is null || ReferenceEquals(bucket.Document, document)) return;
        RemoveIndex(bucket);
        bucket.Document = document;
        Index(bucket, document);
    }

    internal static uint IndexOf(Node node)
    {
        uint index = 0;
        for (var sibling = node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling) index++;
        return index;
    }

    internal static bool Contains(Node ancestor, Node node)
    {
        for (Node? current = node; current is not null; current = current.ParentNode)
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    internal static void Insert(Node parent, Node? before, uint count)
    {
        if (parent.RangeEndpoints is not { } bucket || before is null || count == 0) return;
        var index = IndexOf(before);
        foreach (var entry in bucket.Entries.ToArray())
        {
            if (!entry.Range.TryGetTarget(out var range)) continue;
            var point = entry.Start ? range.Start : range.End;
            if (point.Offset > index) range.Repair(entry.Start, point with { Offset = point.Offset + count });
        }
    }

    internal static void ReplaceData(Node node, uint offset, uint count, uint length)
    {
        if (node.RangeEndpoints is not { } bucket) return;
        foreach (var entry in bucket.Entries.ToArray())
        {
            if (!entry.Range.TryGetTarget(out var range)) continue;
            var point = entry.Start ? range.Start : range.End;
            var next = point.Offset > offset + count
                ? point with { Offset = point.Offset - count + length }
                : point.Offset > offset ? point with { Offset = offset } : point;
            range.Repair(entry.Start, next);
        }
    }

    internal static void Remove(Node node, Node parent, uint? knownIndex = null)
    {
        var document = parent as Document ?? parent.OwnerDocument!;
        if (document.RangeBuckets is not { } buckets) return;
        var index = knownIndex ?? IndexOf(node);
        // Snapshot handles once before endpoint moves rewire the sparse index.
        List<EndpointHandle>? affected = null;
        foreach (var slot in buckets)
        {
            if (!slot.TryGetTarget(out var bucket) || !bucket.Owner.TryGetTarget(out var owner) || owner is not Node container) continue;
            if (!ReferenceEquals(container, parent) && !Contains(node, container)) continue;
            foreach (var entry in bucket.Entries)
                if (entry.Range.TryGetTarget(out _)) (affected ??= []).Add(entry);
        }
        if (affected is not null)
        {
            foreach (var entry in affected)
            {
                if (!entry.Range.TryGetTarget(out var range)) continue;
                var point = entry.Start ? range.Start : range.End;
                var next = ReferenceEquals(point.Container.Node, parent)
                    ? point.Offset > index ? point with { Offset = point.Offset - 1 } : point
                    : new BoundaryPoint(new(parent), index);
                range.Repair(entry.Start, next);
            }
        }
        Sweep(document);
    }

    internal static void Adjust(EndpointBucket bucket, Func<BoundaryPoint, BoundaryPoint> adjustment)
    {
        foreach (var entry in bucket.Entries.ToArray())
        {
            if (entry.Range.TryGetTarget(out var range))
            {
                var point = entry.Start ? range.Start : range.End;
                range.Repair(entry.Start, adjustment(point));
            }
        }
    }
}

// Invocation-local counting for deterministic registration-complexity regressions.
internal struct RegistrationWork(Action<int>? checkpoint)
{
    private int _steps;
    internal void Step() => checkpoint?.Invoke(++_steps);
}

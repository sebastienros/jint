namespace Jint.HtmlParser;

// DOM §4.2.3 and §4.10: repair live points inside the actual native mutation.
// Documents retain only weak bucket keys; nodes own lazy buckets, ranges own handles.
internal sealed class EndpointBucket
{
    internal readonly WeakReference<object> Owner;
    internal Document Document;
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
}

internal static class LiveTraversalTracking
{
    internal static Document DocumentOf(DomNodeIdentity identity)
        => identity.Node as Document ?? identity.Node?.OwnerDocument ?? identity.Attribute!.OwnerDocument;

    internal static EndpointHandle Register(DomRange range, bool start, DomNodeIdentity identity)
    {
        var bucket = identity.Node?.RangeEndpoints ?? identity.Attribute?.RangeEndpoints;
        if (bucket is null)
        {
            var document = DocumentOf(identity);
            bucket = new(identity, document);
            if (identity.Node is { } node) node.RangeEndpoints = bucket;
            else identity.Attribute!.RangeEndpoints = bucket;
            Index(bucket, document);
        }
        bucket.Entries.RemoveAll(static entry => !entry.Range.TryGetTarget(out _));
        var handle = new EndpointHandle(range, start, bucket);
        bucket.Entries.Add(handle);
        return handle;
    }

    internal static void Unregister(EndpointHandle? handle)
    {
        if (handle is null) return;
        var bucket = handle.Bucket;
        bucket.Entries.Remove(handle);
        bucket.Entries.RemoveAll(static entry => !entry.Range.TryGetTarget(out _));
        if (bucket.Entries.Count != 0) return;
        if (bucket.Owner.TryGetTarget(out var owner))
        {
            if (owner is Node node) node.RangeEndpoints = null;
            else ((Attr) owner).RangeEndpoints = null;
        }
        bucket.Document.RangeBuckets?.RemoveAll(slot => !slot.TryGetTarget(out var target) || ReferenceEquals(target, bucket));
    }

    private static void Index(EndpointBucket bucket, Document document)
    {
        var index = document.RangeBuckets ??= [];
        index.RemoveAll(static slot => !slot.TryGetTarget(out var target) || !target.Owner.TryGetTarget(out _) ||
            target.Entries.TrueForAll(static entry => !entry.Range.TryGetTarget(out _)));
        index.Add(new(bucket));
    }

    internal static void Rehome(EndpointBucket? bucket, Document document)
    {
        if (bucket is null || ReferenceEquals(bucket.Document, document)) return;
        bucket.Document.RangeBuckets?.RemoveAll(slot => !slot.TryGetTarget(out var target) || ReferenceEquals(target, bucket));
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
        Adjust(bucket, point => point.Offset > index ? point with { Offset = point.Offset + count } : point);
    }

    internal static void ReplaceData(Node node, uint offset, uint count, uint length)
    {
        if (node.RangeEndpoints is not { } bucket) return;
        Adjust(bucket, point => point.Offset > offset + count
            ? point with { Offset = point.Offset - count + length }
            : point.Offset > offset ? point with { Offset = offset } : point);
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
        buckets.RemoveAll(static slot => !slot.TryGetTarget(out var target) || !target.Owner.TryGetTarget(out _));
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

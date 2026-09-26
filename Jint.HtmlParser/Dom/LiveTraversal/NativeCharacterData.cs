using System.Text;

namespace Jint.HtmlParser;

// DOM §4.10–4.11: replace data, split a Text node, and normalize exclusive Text.
// https://dom.spec.whatwg.org/#concept-cd-replace
// https://dom.spec.whatwg.org/#concept-text-split
// https://dom.spec.whatwg.org/#dom-node-normalize
internal static class NativeCharacterData
{
    internal static uint GetLength(Node node)
    {
        RequireData(node);
        return BoundaryOrder.GetLength(new(node));
    }
    private static void RequireData(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node is not Text and not CDataSection and not Comment and not ProcessingInstruction)
            throw new ArgumentException("A character data node is required.", nameof(node));
    }
    private static string Data(Node node) => node switch
    {
        Text text => text.Data,
        Comment comment => comment.Data,
        CDataSection cdata => cdata.Data,
        ProcessingInstruction pi => pi.Data,
        _ => throw new ArgumentException("A character data node is required.", nameof(node))
    };
    internal static string SubstringData(Node node, uint offset, uint count)
    {
        var length = GetLength(node);
        if (offset > length) throw DomRange.Error("IndexSizeError");
        return Data(node).Substring((int) offset, (int) Math.Min(count, length - offset));
    }
    internal static void ReplaceData(Node node, uint offset, uint count, string data)
    {
        var length = GetLength(node);
        ArgumentNullException.ThrowIfNull(data);
        if (offset > length) throw DomRange.Error("IndexSizeError");
        count = Math.Min(count, length - offset);
        var old = Data(node);
        var replacement = string.Concat(old.AsSpan(0, (int) offset), data.AsSpan(), old.AsSpan((int) (offset + count)));
        var insertedLength = (uint) data.Length;
        switch (node)
        {
            case Text text: text.ReplaceDataCore(replacement, offset, count, insertedLength); break;
            case Comment comment: comment.ReplaceDataCore(replacement, offset, count, insertedLength); break;
            case CDataSection cdata: cdata.ReplaceDataCore(replacement, offset, count, insertedLength); break;
            case ProcessingInstruction pi: pi.ReplaceDataCore(replacement, offset, count, insertedLength); break;
        }
    }
    internal static Text SplitText(Node node, uint offset)
    {
        using var rangeMutation = new RangeMutationScope(node?.OwnerDocument ?? throw new ArgumentNullException(nameof(node)));
        ArgumentNullException.ThrowIfNull(node);
        if (node is not Text and not CDataSection) throw new ArgumentException("A Text or CDATA node is required.", nameof(node));
        var length = GetLength(node);
        if (offset > length) throw DomRange.Error("IndexSizeError");
        var newNode = node.OwnerDocument!.CreateTextNode(SubstringData(node, offset, length - offset));
        if (node.ParentNode is { } parent)
        {
            var index = LiveTraversalTracking.IndexOf(node);
            parent.InsertBefore(newNode, node.NextSibling);
            if (node.RangeEndpoints is { } bucket)
                LiveTraversalTracking.Adjust(bucket, point => point.Offset > offset ? new(new(newNode), point.Offset - offset) : point);
            if (parent.RangeEndpoints is { } parentBucket)
                LiveTraversalTracking.Adjust(parentBucket, point => point.Offset == index + 1 ? point with { Offset = point.Offset + 1 } : point);
        }
        ReplaceData(node, offset, length - offset, string.Empty);
        return newNode;
    }
    internal static void Normalize(Node root) => Normalize(root, null);

    // Invocation-local instrumentation counts child links, run members and frame
    // transitions. It is never retained and cannot interrupt atomic bookkeeping.
    internal static void Normalize(Node root, Action<int>? workCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var rangeMutation = new RangeMutationScope(root as Document ?? root.OwnerDocument!);
        var frames = new Stack<NormalizationFrame>();
        frames.Push(new(root.FirstChild, 0));
        var steps = 0;
        while (frames.TryPop(out var frame))
        {
            workCheckpoint?.Invoke(++steps);
            if (frame.NextChild is not { } node) continue;
            var next = node.NextSibling;
            workCheckpoint?.Invoke(++steps);
            if (node is not Text text)
            {
                frames.Push(new(next, frame.Index + 1));
                if (node.FirstChild is { } child) frames.Push(new(child, 0));
                continue;
            }
            if (text.DataLength == 0)
            {
                text.RemoveForNormalization(frame.Index);
                frames.Push(new(next, frame.Index));
                continue;
            }
            if (next is not Text)
            {
                frames.Push(new(next, frame.Index + 1));
                continue;
            }
            var oldLength = (uint) text.DataLength;
            var combined = new StringBuilder();
            var merged = new List<(Text Node, uint Offset)>();
            var length = oldLength;
            while (next is Text sibling)
            {
                workCheckpoint?.Invoke(++steps);
                merged.Add((sibling, length));
                combined.Append(sibling.Data);
                length += (uint) sibling.DataLength;
                next = sibling.NextSibling;
            }
            ReplaceData(text, oldLength, 0, combined.ToString());
            foreach (var (sibling, offset) in merged)
            {
                workCheckpoint?.Invoke(++steps);
                if (sibling.RangeEndpoints is { } bucket)
                    LiveTraversalTracking.Adjust(bucket, point => new(new(text), point.Offset + offset));
                if (text.ParentNode!.RangeEndpoints is { } parentBucket)
                    LiveTraversalTracking.Adjust(parentBucket, point => point.Offset == frame.Index + 1 ? new(new(text), offset) : point);
                sibling.RemoveForNormalization(frame.Index + 1);
            }
            // Only the surviving Text consumes an index. Removed siblings never
            // make the next run recount an already visited parent prefix.
            frames.Push(new(next, frame.Index + 1));
        }
        workCheckpoint?.Invoke(steps);
    }

    private readonly record struct NormalizationFrame(Node? NextChild, uint Index);
}

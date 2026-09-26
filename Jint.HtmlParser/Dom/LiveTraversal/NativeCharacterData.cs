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
    internal static void Normalize(Node root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var work = new TraversalWork(default);
        var node = root.FirstChild;
        while (node is not null)
        {
            if (node is not Text text) { node = NativeTraversal.Next(node, root, ref work); continue; }
            var next = NativeTraversal.Next(node, root, ref work);
            if (text.DataLength == 0)
            {
                text.ParentNode!.RemoveChild(text);
                node = next;
                continue;
            }
            if (text.NextSibling is not Text) { node = next; continue; }
            var index = LiveTraversalTracking.IndexOf(text);
            var oldLength = (uint) text.DataLength;
            var combined = new StringBuilder();
            var merged = new List<(Text Node, uint Offset)>();
            var length = oldLength;
            for (var sibling = text.NextSibling as Text; sibling is not null; sibling = sibling.NextSibling as Text)
            {
                merged.Add((sibling, length));
                combined.Append(sibling.Data);
                length += (uint) sibling.DataLength;
            }
            ReplaceData(text, oldLength, 0, combined.ToString());
            foreach (var (sibling, offset) in merged)
            {
                if (sibling.RangeEndpoints is { } bucket)
                    LiveTraversalTracking.Adjust(bucket, point => new(new(text), point.Offset + offset));
                if (text.ParentNode!.RangeEndpoints is { } parentBucket)
                    LiveTraversalTracking.Adjust(parentBucket, point => point.Offset == index + 1 ? new(new(text), offset) : point);
                sibling.RemoveForNormalization(index + 1);
            }
            node = NativeTraversal.Next(text, root, ref work);
        }
    }
}

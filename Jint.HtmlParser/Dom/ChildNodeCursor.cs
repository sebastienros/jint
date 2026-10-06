using System.Runtime.CompilerServices;

namespace Jint.HtmlParser;

// DOM §4.2.10: a lazy live child-list cursor, repaired at the native link boundaries.
// No field on Node, no snapshot of membership, and no retained removed child.
internal sealed class ChildNodeCursor
{
    private static readonly ConditionalWeakTable<Node, ChildNodeCursor> Cursors = new();
    private static volatile bool _active;
    private Node? _node;
    private uint _index;

    internal static ChildNodeCursor Of(Node parent)
    {
        _active = true;
        return Cursors.GetValue(parent, static _ => new());
    }

    internal Node? ReadItem(Node parent, uint index, Action<int>? checkpoint, CancellationToken token)
    {
        var work = new TraversalWork(new(parent), default, checkpoint, token);
        if (index >= (uint) parent.ChildCount) return null;
        var end = (uint) parent.ChildCount - 1;
        var position = index <= end - index ? 0 : end;
        var current = position == 0 ? parent.FirstChild! : parent.LastChild!;
        if (_node is not null && Distance(_index, index) <= Distance(position, index))
        {
            current = _node;
            position = _index;
        }
        while (position != index)
        {
            work.Step();
            if (position < index) { current = current.NextSibling!; position++; }
            else { current = current.PreviousSibling!; position--; }
        }
        work.Check();
        _node = current;
        _index = index;
        return current;
    }

    private static uint Distance(uint first, uint second) => first >= second ? first - second : second - first;

    // Resolve edits at either end or next to the cursor without walking siblings.
    // A distant interior edit drops only this parent's cursor; the next read chooses
    // the nearer endpoint. Mutations in descendants or other parents do nothing.
    private uint? IndexOf(Node parent, Node node)
    {
        if (ReferenceEquals(node, _node)) return _index;
        if (ReferenceEquals(node, parent.FirstChild)) return 0;
        if (ReferenceEquals(node, parent.LastChild)) return (uint) parent.ChildCount - 1;
        if (ReferenceEquals(node.NextSibling, _node)) return _index - 1;
        if (ReferenceEquals(node.PreviousSibling, _node)) return _index + 1;
        return null;
    }

    internal static void Removing(Node parent, Node node)
    {
        if (!_active || !Cursors.TryGetValue(parent, out var cursor) || cursor._node is null) return;
        if (cursor.IndexOf(parent, node) is not { } index) { cursor._node = null; return; }
        if (ReferenceEquals(node, cursor._node))
        {
            // The successor takes the removed node's index, or the predecessor
            // takes index - 1. Repair before unlinking clears both sibling links.
            cursor._node = node.NextSibling ?? node.PreviousSibling;
            if (node.NextSibling is null && index != 0) cursor._index--;
        }
        else if (index < cursor._index) cursor._index--;
    }

    internal static void Inserting(Node parent, Node? reference)
    {
        if (!_active || !Cursors.TryGetValue(parent, out var cursor) || cursor._node is null || reference is null) return;
        if (cursor.IndexOf(parent, reference) is not { } index) { cursor._node = null; return; }
        if (index <= cursor._index) cursor._index++;
    }
}

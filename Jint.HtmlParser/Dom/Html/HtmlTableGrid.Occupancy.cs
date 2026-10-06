namespace Jint.HtmlParser;

internal sealed partial class HtmlTableGrid
{
    // Sparse range-chmax tree of exclusive row expiries. Its coordinate domain
    // is the full nonnegative long range, so no table-specific fixed-size grid
    // or arbitrary maximum column count is needed. An operation visits at most
    // 63 levels and O(log U) nodes, regardless of colspan or rowspan area.
    private sealed class Occupancy(Work work)
    {
        private Node _root = new();

        internal void Clear() => _root = new Node();

        internal void Cover(long start, long end, long expiry)
        {
            var stack = new Stack<UpdateFrame>();
            stack.Push(new UpdateFrame(_root, 0, long.MaxValue, false));
            while (stack.Count != 0)
            {
                work.Step();
                var frame = stack.Pop();
                if (frame.Exit)
                {
                    frame.Node.Minimum = Math.Min(frame.Node.Left!.Minimum, frame.Node.Right!.Minimum);
                    continue;
                }

                if (end <= frame.Low || start >= frame.High) continue;
                if (start <= frame.Low && frame.High <= end)
                {
                    Apply(frame.Node, expiry);
                    continue;
                }

                PushFloor(frame.Node);
                var middle = frame.Low + (frame.High - frame.Low) / 2;
                stack.Push(new UpdateFrame(frame.Node, frame.Low, frame.High, true));
                if (end > middle)
                    stack.Push(new UpdateFrame(frame.Node.Right!, middle, frame.High, false));
                if (start < middle)
                    stack.Push(new UpdateFrame(frame.Node.Left!, frame.Low, middle, false));
            }
        }

        internal long FindFirstFree(long start, long row)
        {
            var stack = new Stack<SearchFrame>();
            stack.Push(new SearchFrame(_root, 0, long.MaxValue, 0));
            while (stack.Count != 0)
            {
                work.Step();
                var frame = stack.Pop();
                if (frame.High <= start) continue;
                var minimum = Math.Max(frame.InheritedFloor, frame.Node?.Minimum ?? 0);
                if (minimum > row) continue;
                if (frame.Node is null || frame.High - frame.Low == 1)
                {
                    return Math.Max(start, frame.Low);
                }

                var floor = Math.Max(frame.InheritedFloor, frame.Node.Floor);
                var middle = frame.Low + (frame.High - frame.Low) / 2;
                stack.Push(new SearchFrame(frame.Node.Right, middle, frame.High, floor));
                stack.Push(new SearchFrame(frame.Node.Left, frame.Low, middle, floor));
            }

            // Reaching this requires more participating cells than a long can
            // represent at one column per cell; checked coordinates reject it.
            throw new OverflowException("Table column coordinate exceeds Int64.MaxValue.");
        }

        private static void Apply(Node node, long expiry)
        {
            if (node.Floor < expiry) node.Floor = expiry;
            if (node.Minimum < expiry) node.Minimum = expiry;
        }

        private static void PushFloor(Node node)
        {
            node.Left ??= new Node();
            node.Right ??= new Node();
            if (node.Floor == 0) return;
            Apply(node.Left, node.Floor);
            Apply(node.Right, node.Floor);
            node.Floor = 0;
        }

        private sealed class Node
        {
            internal Node? Left;
            internal Node? Right;
            internal long Floor;
            internal long Minimum;
        }

        private readonly record struct UpdateFrame(Node Node, long Low, long High, bool Exit);
        private readonly record struct SearchFrame(Node? Node, long Low, long High, long InheritedFloor);
    }
}

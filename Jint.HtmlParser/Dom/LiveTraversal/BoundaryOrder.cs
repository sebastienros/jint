namespace Jint.HtmlParser;

// DOM Standard §5.2, boundary point position and root algorithms.
// https://dom.spec.whatwg.org/#concept-range-bp-position
internal static class BoundaryOrder
{
    internal static uint GetLength(DomNodeIdentity identity)
    {
        if (!identity.IsValid)
        {
            throw new ArgumentException("A node or attribute identity is required.", nameof(identity));
        }

        return identity.Node switch
        {
            Text text => (uint) text.DataLength,
            CDataSection cdata => (uint) cdata.Data.Length,
            Comment comment => (uint) comment.Data.Length,
            ProcessingInstruction instruction => (uint) instruction.Data.Length,
            DocumentType => 0,
            { } node => (uint) node.ChildCount,
            _ => 0 // An Attr has no children, regardless of its value or owner.
        };
    }

    internal static DomNodeIdentity GetRoot(DomNodeIdentity node, CancellationToken cancellationToken)
        => GetRoot(node, null, cancellationToken);

    internal static DomNodeIdentity GetRoot(DomNodeIdentity node, Action<int>? workCheckpoint,
        CancellationToken cancellationToken)
    {
        RequireValid(node, nameof(node));
        var work = new WorkCounter(workCheckpoint, cancellationToken);
        work.Check();
        if (node.Attribute is not null)
        {
            work.Check();
            return node;
        }

        var current = node.Node!;
        while (true)
        {
            var parent = current.ParentNode;
            work.Step(); // Include the final ascent that discovers a root.
            if (parent is null)
            {
                work.Check();
                return new DomNodeIdentity(current);
            }

            current = parent;
        }
    }

    internal static int Compare(BoundaryPoint left, BoundaryPoint right, CancellationToken cancellationToken)
        => Compare(left, right, null, cancellationToken);

    internal static int Compare(BoundaryPoint left, BoundaryPoint right, Action<int>? workCheckpoint,
        CancellationToken cancellationToken)
    {
        RequireValid(left.Container, nameof(left));
        RequireValid(right.Container, nameof(right));
        var work = new WorkCounter(workCheckpoint, cancellationToken);
        work.Check();

        // Validate left then right before comparing roots. A point can retain an
        // out-of-bounds offset after a later mutation; comparison must reject it.
        ValidatePoint(left, ref work);
        ValidatePoint(right, ref work);

        if (left.Container.Equals(right.Container))
        {
            work.Check();
            return left.Offset.CompareTo(right.Offset);
        }

        // An Attr is its own singleton tree. No owner-element link participates.
        if (left.Container.Attribute is not null || right.Container.Attribute is not null)
        {
            work.Check();
            throw WrongDocument();
        }

        var leftPath = BuildPath(left.Container.Node!, ref work);
        var rightPath = BuildPath(right.Container.Node!, ref work);
        if (!ReferenceEquals(leftPath[^1], rightPath[^1]))
        {
            work.Check();
            throw WrongDocument();
        }

        var leftIndex = leftPath.Count - 1;
        var rightIndex = rightPath.Count - 1;
        while (leftIndex >= 0 && rightIndex >= 0 &&
               ReferenceEquals(leftPath[leftIndex], rightPath[rightIndex]))
        {
            leftIndex--;
            rightIndex--;
            work.Step();
        }

        int result;
        if (leftIndex < 0)
        {
            // Left's container is an ancestor. Its offset is at or before the
            // branch containing right exactly when it precedes right.
            var branchIndex = IndexOfChild(leftPath[0], rightPath[rightIndex], ref work);
            result = left.Offset <= branchIndex ? -1 : 1;
        }
        else if (rightIndex < 0)
        {
            var branchIndex = IndexOfChild(rightPath[0], leftPath[leftIndex], ref work);
            result = branchIndex < right.Offset ? -1 : 1;
        }
        else
        {
            // Both divergent nodes have the same parent. Scan once from that
            // parent's first child, without repeated ancestor-of-ancestor walks.
            var parent = leftPath[leftIndex + 1];
            result = BeforeInSiblings(parent, leftPath[leftIndex], rightPath[rightIndex], ref work)
                ? -1 : 1;
        }

        work.Check();
        return result;
    }

    private static void ValidatePoint(BoundaryPoint point, ref WorkCounter work)
    {
        if (point.Container.Node is DocumentType)
        {
            throw new DomException("InvalidNodeTypeError", "A document type cannot contain a boundary point.");
        }

        work.Check(); // Text.Data can materialize parser-owned storage.
        var length = GetLength(point.Container);
        work.Check();
        if (point.Offset > length)
        {
            throw new DomException("IndexSizeError", "The boundary offset exceeds the container length.");
        }
    }

    private static List<Node> BuildPath(Node node, ref WorkCounter work)
    {
        var path = new List<Node>();
        for (Node? current = node; current is not null; current = current.ParentNode)
        {
            path.Add(current);
            work.Step();
        }

        work.Step(); // Poll the final ascent to null as well.
        return path;
    }

    private static uint IndexOfChild(Node parent, Node child, ref WorkCounter work)
    {
        uint index = 0;
        for (var current = parent.FirstChild; current is not null; current = current.NextSibling)
        {
            work.Step();
            if (ReferenceEquals(current, child))
            {
                return index;
            }

            index++;
        }

        throw new InvalidOperationException("The tree changed during boundary comparison.");
    }

    private static bool BeforeInSiblings(Node parent, Node left, Node right, ref WorkCounter work)
    {
        for (var current = parent.FirstChild; current is not null; current = current.NextSibling)
        {
            work.Step();
            if (ReferenceEquals(current, left))
            {
                return true;
            }

            if (ReferenceEquals(current, right))
            {
                return false;
            }
        }

        throw new InvalidOperationException("The tree changed during boundary comparison.");
    }

    private static void RequireValid(DomNodeIdentity identity, string parameter)
    {
        if (!identity.IsValid)
        {
            throw new ArgumentException("A node or attribute identity is required.", parameter);
        }
    }

    private static DomException WrongDocument()
        => new("WrongDocumentError", "Boundary points are in different trees.");

    private struct WorkCounter(Action<int>? checkpoint, CancellationToken cancellationToken)
    {
        private int _steps;

        internal void Step()
        {
            _steps++;
            if ((_steps & 255) == 0)
            {
                Check();
            }
        }

        internal void Check()
        {
            checkpoint?.Invoke(_steps);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}

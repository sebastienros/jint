using System.Text;

namespace Jint.HtmlParser;

// DOM Standard §5.5. Ordinary-tree endpoints are authoritative native state.
// https://dom.spec.whatwg.org/#interface-range
internal sealed partial class DomRange
{
    private EndpointHandle? _startHandle;
    private EndpointHandle? _endHandle;
    internal DomRange(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Start = End = new(new(document), 0);
        _startHandle = LiveTraversalTracking.Register(this, true, Start.Container);
        _endHandle = LiveTraversalTracking.Register(this, false, End.Container);
    }
    internal BoundaryPoint Start { get; private set; }
    internal BoundaryPoint End { get; private set; }
    internal bool Collapsed => Start.Equals(End);

    internal void Repair(bool start, BoundaryPoint point)
    {
        var old = start ? Start : End;
        if (old.Equals(point)) return;
        if (!old.Container.Equals(point.Container))
        {
            LiveTraversalTracking.Unregister(start ? _startHandle : _endHandle);
            var handle = LiveTraversalTracking.Register(this, start, point.Container);
            if (start) _startHandle = handle;
            else _endHandle = handle;
        }
        if (start) Start = point;
        else End = point;
    }

    internal void SetStart(DomNodeIdentity node, uint offset) => Set(true, new(node, offset));
    internal void SetEnd(DomNodeIdentity node, uint offset) => Set(false, new(node, offset));
    private void Set(bool start, BoundaryPoint point)
    {
        Validate(point);
        var opposite = start ? End : Start;
        var different = !BoundaryOrder.GetRoot(point.Container, default).Equals(BoundaryOrder.GetRoot(opposite.Container, default));
        if (different || (start ? BoundaryOrder.Compare(point, opposite, default) > 0 : BoundaryOrder.Compare(point, opposite, default) < 0))
            Repair(!start, point);
        Repair(start, point);
    }
    internal static void Validate(BoundaryPoint point)
    {
        if (!point.Container.IsValid) throw new ArgumentException("A valid identity is required.", nameof(point));
        if (point.Container.Node is DocumentType) throw Error("InvalidNodeTypeError");
        if (point.Offset > BoundaryOrder.GetLength(point.Container)) throw Error("IndexSizeError");
    }
    internal static DomException Error(string name) => new(name, name);
    private static BoundaryPoint Beside(DomNodeIdentity identity, bool after)
    {
        if (!identity.IsValid) throw new ArgumentException("A valid identity is required.", nameof(identity));
        if (identity.Node?.ParentNode is not { } parent) throw Error("InvalidNodeTypeError");
        return new(new(parent), LiveTraversalTracking.IndexOf(identity.Node) + (after ? 1u : 0u));
    }
    internal void SetStartBefore(DomNodeIdentity node) => Set(true, Beside(node, false));
    internal void SetStartAfter(DomNodeIdentity node) => Set(true, Beside(node, true));
    internal void SetEndBefore(DomNodeIdentity node) => Set(false, Beside(node, false));
    internal void SetEndAfter(DomNodeIdentity node) => Set(false, Beside(node, true));
    internal void Collapse(bool toStart = false) => Repair(toStart ? false : true, toStart ? Start : End);
    internal void SelectNode(DomNodeIdentity node)
    {
        var start = Beside(node, false);
        Repair(true, start);
        Repair(false, start with { Offset = start.Offset + 1 });
    }
    internal void SelectNodeContents(DomNodeIdentity node)
    {
        var point = new BoundaryPoint(node, 0);
        Validate(point);
        Repair(true, point);
        Repair(false, point with { Offset = BoundaryOrder.GetLength(node) });
    }
    internal DomRange CloneRange()
    {
        var clone = new DomRange(LiveTraversalTracking.DocumentOf(Start.Container));
        clone.Repair(true, Start);
        clone.Repair(false, End);
        return clone;
    }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "DOM Range.detach is an instance no-op.")]
    internal void Detach() { }
    internal DomNodeIdentity GetCommonAncestor(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Start.Container.Equals(End.Container)) return Start.Container;
        var ancestors = new HashSet<Node>();
        var work = new TraversalWork(cancellationToken);
        for (var node = Start.Container.Node; node is not null; node = node.ParentNode) { ancestors.Add(node); work.Step(); }
        for (var node = End.Container.Node; node is not null; node = node.ParentNode)
        {
            work.Step();
            if (ancestors.Contains(node)) { work.Check(); return new(node); }
        }
        throw Error("WrongDocumentError");
    }
    internal int CompareBoundaryPoints(ushort how, DomRange source, CancellationToken cancellationToken)
    {
        if (how > 3) throw Error("NotSupportedError");
        ArgumentNullException.ThrowIfNull(source);
        var (left, right) = how switch
        {
            0 => (Start, source.Start),
            1 => (End, source.Start),
            2 => (End, source.End),
            _ => (Start, source.End)
        };
        return BoundaryOrder.Compare(left, right, cancellationToken);
    }
    internal int ComparePoint(DomNodeIdentity node, uint offset, CancellationToken cancellationToken)
    {
        if (!BoundaryOrder.GetRoot(node, cancellationToken).Equals(BoundaryOrder.GetRoot(Start.Container, cancellationToken))) throw Error("WrongDocumentError");
        var point = new BoundaryPoint(node, offset);
        Validate(point);
        if (BoundaryOrder.Compare(point, Start, cancellationToken) < 0) return -1;
        return BoundaryOrder.Compare(point, End, cancellationToken) > 0 ? 1 : 0;
    }
    internal bool IsPointInRange(DomNodeIdentity node, uint offset, CancellationToken cancellationToken)
    {
        if (!BoundaryOrder.GetRoot(node, cancellationToken).Equals(BoundaryOrder.GetRoot(Start.Container, cancellationToken))) return false;
        return ComparePoint(node, offset, cancellationToken) == 0;
    }
    internal bool IntersectsNode(DomNodeIdentity node, CancellationToken cancellationToken)
    {
        if (!BoundaryOrder.GetRoot(node, cancellationToken).Equals(BoundaryOrder.GetRoot(Start.Container, cancellationToken))) return false;
        if (node.Node?.ParentNode is null) return true;
        var work = new TraversalWork(cancellationToken);
        uint index = 0;
        for (var sibling = node.Node.PreviousSibling; sibling is not null; sibling = sibling.PreviousSibling) { index++; work.Step(); }
        work.Check();
        var before = new BoundaryPoint(new(node.Node.ParentNode), index);
        return BoundaryOrder.Compare(before with { Offset = before.Offset + 1 }, Start, cancellationToken) > 0 &&
               BoundaryOrder.Compare(before, End, cancellationToken) < 0;
    }
    internal string GetText(CancellationToken cancellationToken)
    {
        var work = new TraversalWork(cancellationToken);
        if (Collapsed) { work.Check(); return string.Empty; }
        var root = GetCommonAncestor(cancellationToken).Node;
        if (root is null) { work.Check(); return string.Empty; }
        var builder = new StringBuilder();
        var first = BoundaryNode(Start, root, ref work);
        var stop = BoundaryNode(End, root, ref work);
        // CharacterData's boundary node is included, but its ending slice is bounded.
        for (var node = first; node is not null; node = NativeTraversal.Next(node, root, ref work))
        {
            work.Step();
            var isEnd = ReferenceEquals(node, End.Container.Node);
            if (ReferenceEquals(node, stop) && (End.Container.Node is not { } endNode || !IsData(endNode))) break;
            if (node is Text or CDataSection)
            {
                var from = ReferenceEquals(node, Start.Container.Node) ? Start.Offset : 0;
                var to = isEnd ? End.Offset : BoundaryOrder.GetLength(new(node));
                for (var i = from; i < to; i++)
                {
                    builder.Append(node is Text text ? text.DataAt((int) i) : ((CDataSection) node).Data[(int) i]);
                    work.Step();
                }
            }
            if (isEnd && IsData(node)) break;
            if (ReferenceEquals(node, stop)) break;
        }
        work.Check();
        var result = builder.ToString();
        work.Check();
        return result;
    }
    internal static bool IsData(Node node) => node is Text or CDataSection or Comment or ProcessingInstruction;
    private static Node? BoundaryNode(BoundaryPoint point, Node root, ref TraversalWork work)
    {
        var node = point.Container.Node;
        if (node is null || IsData(node)) return node;
        var child = node.FirstChild;
        for (uint i = 0; i < point.Offset; i++) { child = child!.NextSibling; work.Step(); }
        return child ?? NativeTraversal.Following(node, root, ref work);
    }

}

internal struct TraversalWork
{
    private readonly CancellationToken _token;
    private int _count;
    internal TraversalWork(CancellationToken token) { _token = token; _count = 0; Check(); }
    internal void Step() { if ((++_count & 255) == 0) Check(); }
    internal readonly void Check() => _token.ThrowIfCancellationRequested();
}
internal static class NativeTraversal
{
    internal static Node? Next(Node node, Node root, ref TraversalWork work)
    {
        if (node.FirstChild is { } child) { work.Step(); return child; }
        return Following(node, root, ref work);
    }
    internal static Node? Following(Node node, Node root, ref TraversalWork work)
    {
        while (!ReferenceEquals(node, root))
        {
            work.Step();
            if (node.NextSibling is { } next) return next;
            if (node.ParentNode is not { } parent) return null;
            node = parent;
        }
        work.Step();
        return null;
    }
}

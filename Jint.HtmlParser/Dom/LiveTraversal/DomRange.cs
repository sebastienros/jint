using System.Text;

namespace Jint.HtmlParser;

// DOM Standard §5.5. Ordinary-tree endpoints are authoritative native state.
// https://dom.spec.whatwg.org/#interface-range
/// <summary>A live ordinary-tree range whose endpoints follow native DOM mutations.</summary>
/// <remarks>Create ranges through Document.CreateRange. Native objects require serialized host access. Geometry and Selection belong to Browser.</remarks>
public sealed partial class DomRange
{
    private EndpointHandle? _startHandle;
    private EndpointHandle? _endHandle;
    internal DomRange(Document document) : this(document, null) { }

    internal DomRange(Document document, Action<int>? registrationCheckpoint)
    {
        ArgumentNullException.ThrowIfNull(document);
        Start = End = new(new(document), 0);
        _startHandle = LiveTraversalTracking.Register(this, true, Start.Container, registrationCheckpoint);
        _endHandle = LiveTraversalTracking.Register(this, false, End.Container, registrationCheckpoint);
    }
    /// <summary>Gets the start boundary point.</summary>
    public BoundaryPoint Start { get; private set; }
    /// <summary>Gets the end boundary point.</summary>
    public BoundaryPoint End { get; private set; }
    /// <summary>Gets whether both endpoints are the same boundary point.</summary>
    public bool Collapsed => Start.Equals(End);

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
        Changed();
    }

    /// <summary>Sets the start boundary, collapsing the end if the point is later or in a different tree.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError for a doctype, or IndexSizeError for an excessive offset.</exception>
    public void SetStart(DomNodeIdentity node, uint offset) => Set(true, new(node, offset));
    /// <summary>Sets the end boundary, collapsing the start if the point is earlier or in a different tree.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError for a doctype, or IndexSizeError for an excessive offset.</exception>
    public void SetEnd(DomNodeIdentity node, uint offset) => Set(false, new(node, offset));
    private void Set(bool start, BoundaryPoint point)
    {
        using var change = Changing();
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
    /// <summary>Sets the start immediately before a node in its ordinary parent.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError when the identity has no ordinary parent.</exception>
    public void SetStartBefore(DomNodeIdentity node) => Set(true, Beside(node, false));
    /// <summary>Sets the start immediately after a node in its ordinary parent.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError when the identity has no ordinary parent.</exception>
    public void SetStartAfter(DomNodeIdentity node) => Set(true, Beside(node, true));
    /// <summary>Sets the end immediately before a node in its ordinary parent.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError when the identity has no ordinary parent.</exception>
    public void SetEndBefore(DomNodeIdentity node) => Set(false, Beside(node, false));
    /// <summary>Sets the end immediately after a node in its ordinary parent.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError when the identity has no ordinary parent.</exception>
    public void SetEndAfter(DomNodeIdentity node) => Set(false, Beside(node, true));
    /// <summary>Collapses to the end by default, or to the start when toStart is true.</summary>
    public void Collapse(bool toStart = false)
    {
        using var change = Changing();
        Repair(toStart ? false : true, toStart ? Start : End);
    }
    /// <summary>Selects a node in its ordinary parent, without entering hosted template or shadow contents.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError when the identity has no ordinary parent.</exception>
    public void SelectNode(DomNodeIdentity node)
    {
        using var change = Changing();
        var start = Beside(node, false);
        Repair(true, start);
        Repair(false, start with { Offset = start.Offset + 1 });
    }
    /// <summary>Selects the full character data or ordinary children of an identity.</summary>
    /// <exception cref="DomException">InvalidNodeTypeError for a doctype, or IndexSizeError for an excessive offset.</exception>
    public void SelectNodeContents(DomNodeIdentity node)
    {
        using var change = Changing();
        var point = new BoundaryPoint(node, 0);
        Validate(point);
        Repair(true, point);
        Repair(false, point with { Offset = BoundaryOrder.GetLength(node) });
    }
    /// <summary>Creates an independently live range with the same endpoint identities and offsets.</summary>
    public DomRange CloneRange()
    {
        var clone = new DomRange(LiveTraversalTracking.DocumentOf(Start.Container));
        clone.Repair(true, Start);
        clone.Repair(false, End);
        return clone;
    }
    /// <summary>Performs the DOM compatibility no-op; the object remains usable and live.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "DOM Range.detach is an instance no-op.")]
    public void Detach() { }
    /// <summary>Gets the nearest ordinary-tree inclusive ancestor of both endpoints.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public DomNodeIdentity GetCommonAncestor(CancellationToken cancellationToken = default) => GetCommonAncestor(null, cancellationToken);

    internal DomNodeIdentity GetCommonAncestor(Action<int>? workCheckpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Start.Container.Equals(End.Container)) return Start.Container;
        var ancestors = new HashSet<Node>();
        var work = new TraversalWork(workCheckpoint, cancellationToken);
        for (var node = Start.Container.Node; node is not null; node = node.ParentNode) { ancestors.Add(node); work.Step(); }
        for (var node = End.Container.Node; node is not null; node = node.ParentNode)
        {
            work.Step();
            if (ancestors.Contains(node)) { work.Check(); return new(node); }
        }
        throw Error("WrongDocumentError");
    }
    /// <summary>Compares selected boundary points using DOM selectors 0, 1, 2, or 3; returns -1, 0, or 1.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public int CompareBoundaryPoints(ushort how, DomRange source, CancellationToken cancellationToken = default)
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
    /// <summary>Returns -1 before the range, 0 inside it, or 1 after it; a different root throws WrongDocumentError.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public int ComparePoint(DomNodeIdentity node, uint offset, CancellationToken cancellationToken = default)
    {
        if (!BoundaryOrder.GetRoot(node, cancellationToken).Equals(BoundaryOrder.GetRoot(Start.Container, cancellationToken))) throw Error("WrongDocumentError");
        var point = new BoundaryPoint(node, offset);
        Validate(point);
        if (BoundaryOrder.Compare(point, Start, cancellationToken) < 0) return -1;
        return BoundaryOrder.Compare(point, End, cancellationToken) > 0 ? 1 : 0;
    }
    /// <summary>Tests whether a valid point is within the range, returning false for a different root.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public bool IsPointInRange(DomNodeIdentity node, uint offset, CancellationToken cancellationToken = default)
    {
        if (!BoundaryOrder.GetRoot(node, cancellationToken).Equals(BoundaryOrder.GetRoot(Start.Container, cancellationToken))) return false;
        return ComparePoint(node, offset, cancellationToken) == 0;
    }
    /// <summary>Tests ordinary-tree intersection; a matching parentless root intersects even a collapsed range.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public bool IntersectsNode(DomNodeIdentity node, CancellationToken cancellationToken = default)
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
    /// <summary>Copies selected Text and CDATA data in tree order, excluding Comment and processing-instruction data.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public string GetText(CancellationToken cancellationToken = default) => GetText(null, cancellationToken);

    internal string GetText(Action<int>? workCheckpoint, CancellationToken cancellationToken)
    {
        var work = new TraversalWork(workCheckpoint, cancellationToken);
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
    private readonly Action<int>? _checkpoint;
    private int _count;
    internal TraversalWork(CancellationToken token) : this(null, token) { }
    internal TraversalWork(Action<int>? checkpoint, CancellationToken token) { _token = token; _checkpoint = checkpoint; _count = 0; Check(); }
    internal void Step() { if ((++_count & 255) == 0) Check(); }
    internal readonly void Check() { _checkpoint?.Invoke(_count); _token.ThrowIfCancellationRequested(); }
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

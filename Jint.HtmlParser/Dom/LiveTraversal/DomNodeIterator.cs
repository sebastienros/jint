namespace Jint.HtmlParser;

// Current DOM §6.1 candidate-reference traversal and pre-removal pointer repair.
// https://dom.spec.whatwg.org/#interface-nodeiterator
/// <summary>Traverses an ordinary tree using a live reference and an independently repaired in-flight candidate.</summary>
/// <remarks>Filters are invocation-local and synchronous. They may mutate the tree; recursive filtering throws InvalidStateError. Cancellation is polled around callbacks and at most every 256 traversal steps.</remarks>
public sealed class DomNodeIterator
{
    private readonly record struct Pointer(DomNodeIdentity Node, bool Before);
    private sealed class Candidate(Pointer pointer) { internal Pointer Pointer = pointer; }
    private Pointer _reference;
    private Candidate? _candidate;
    private bool _active;
    /// <summary>Creates a traverser rooted at the given native identity.</summary>
    public DomNodeIterator(DomNodeIdentity root, uint whatToShow) : this(root, whatToShow, null) { }

    internal DomNodeIterator(DomNodeIdentity root, uint whatToShow, Action<int>? registrationCheckpoint)
    {
        if (!root.IsValid) throw new ArgumentException("A valid identity is required.", nameof(root));
        Root = root; WhatToShow = whatToShow; _reference = new(root, true);
        IteratorTracking.Register(this, registrationCheckpoint);
    }
    /// <summary>Gets the original traversal root identity, including a valid singleton attribute root.</summary>
    public DomNodeIdentity Root { get; }
    /// <summary>Gets the complete unsigned DOM node-type mask; excluded nodes do not invoke the filter.</summary>
    public uint WhatToShow { get; }
    /// <summary>Gets the live iterator reference identity.</summary>
    public DomNodeIdentity Reference => _reference.Node;
    /// <summary>Gets whether the iterator pointer is before its reference identity.</summary>
    public bool PointerBeforeReference => _reference.Before;
    internal int TrackingIndex = -1;
    internal Document TrackingDocument = null!;
    /// <summary>Moves forward in tree order and returns an accepted identity, or null.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public DomNodeIdentity? Next(TraversalFilter? filter, CancellationToken cancellationToken = default) => Traverse(true, filter, cancellationToken);
    /// <summary>Moves backward in tree order and returns an accepted identity, or null.</summary>
    /// <exception cref="OperationCanceledException">The supplied cancellation token is canceled.</exception>
    public DomNodeIdentity? Previous(TraversalFilter? filter, CancellationToken cancellationToken = default) => Traverse(false, filter, cancellationToken);
    /// <summary>Performs the DOM compatibility no-op; the object remains usable and live.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "DOM NodeIterator.detach is an instance no-op.")]
    public void Detach() { }
    private DomNodeIdentity? Traverse(bool next, TraversalFilter? filter, CancellationToken token)
    {
        var work = new TraversalWork(token);
        var outer = _candidate;
        var invocation = new Candidate(_reference);
        _candidate = invocation;
        try
        {
            while (true)
            {
                work.Step();
                var pointer = invocation.Pointer;
                if (next != pointer.Before)
                {
                    var adjacent = next ? Following(pointer.Node, ref work) : Preceding(pointer.Node, ref work);
                    if (adjacent is null) { work.Check(); return null; }
                    pointer = new(adjacent.Value, !next);
                }
                else pointer = pointer with { Before = !next };
                invocation.Pointer = pointer;
                var node = pointer.Node;
                if (NativeFiltering.Filter(node, WhatToShow, filter, ref _active, ref work) != 1) continue;
                _reference = invocation.Pointer;
                work.Check();
                return node;
            }
        }
        finally { _candidate = outer; }
    }
    private DomNodeIdentity? Following(DomNodeIdentity identity, ref TraversalWork work)
    {
        if (identity.Node is not { } node || Root.Node is not { } root) return null;
        var next = NativeTraversal.Next(node, root, ref work);
        return next is null ? null : new DomNodeIdentity(next);
    }
    private DomNodeIdentity? Preceding(DomNodeIdentity identity, ref TraversalWork work)
    {
        if (identity.Equals(Root) || identity.Node is not { } node) return null;
        if (node.PreviousSibling is not { } previous) return node.ParentNode is { } parent ? new(parent) : null;
        while (previous.LastChild is { } child) { previous = child; work.Step(); }
        return new(previous);
    }
    internal void PreRemove(Node removed)
    {
        _reference = Adjust(_reference, removed);
        if (_candidate is { } candidate) candidate.Pointer = Adjust(candidate.Pointer, removed);
    }
    private Pointer Adjust(Pointer pointer, Node removed)
    {
        if (Root.Node is not { } root || pointer.Node.Node is not { } node ||
            !LiveTraversalTracking.Contains(removed, node) || LiveTraversalTracking.Contains(removed, root)) return pointer;
        if (pointer.Before)
        {
            var work = new TraversalWork(default);
            if (NativeTraversal.Following(removed, root, ref work) is { } next) return new(new(next), true);
        }
        var previous = removed.PreviousSibling;
        if (previous is null) return new(new(removed.ParentNode!), false);
        while (previous.LastChild is { } child) previous = child;
        return new(new(previous), false);
    }
}

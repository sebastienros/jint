namespace Jint.HtmlParser;

// Current DOM §6.1 candidate-reference traversal and pre-removal pointer repair.
// https://dom.spec.whatwg.org/#interface-nodeiterator
internal sealed class DomNodeIterator
{
    private readonly record struct Pointer(DomNodeIdentity Node, bool Before);
    private sealed class Candidate(Pointer pointer) { internal Pointer Pointer = pointer; }
    private Pointer _reference;
    private Candidate? _candidate;
    private bool _active;
    internal DomNodeIterator(DomNodeIdentity root, uint whatToShow)
    {
        if (!root.IsValid) throw new ArgumentException("A valid identity is required.", nameof(root));
        Root = root; WhatToShow = whatToShow; _reference = new(root, true);
        IteratorTracking.Register(this);
    }
    internal DomNodeIdentity Root { get; }
    internal uint WhatToShow { get; }
    internal DomNodeIdentity Reference => _reference.Node;
    internal bool PointerBeforeReference => _reference.Before;
    internal int TrackingIndex = -1;
    internal Document TrackingDocument = null!;
    internal DomNodeIdentity? Next(TraversalFilter? filter, CancellationToken cancellationToken) => Traverse(true, filter, cancellationToken);
    internal DomNodeIdentity? Previous(TraversalFilter? filter, CancellationToken cancellationToken) => Traverse(false, filter, cancellationToken);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "DOM NodeIterator.detach is an instance no-op.")]
    internal void Detach() { }
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

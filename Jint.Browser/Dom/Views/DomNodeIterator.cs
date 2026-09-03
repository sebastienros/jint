using AngleSharp.Dom;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-nodeiterator">DOM §6.1's <c>NodeIterator</c></a>, whose
/// traversal is written here because the standard's is about a second position — the candidate reference —
/// that AngleSharp's iterator does not have.
/// </summary>
/// <remarks>
/// <para>
/// <b>What was wrong.</b> DOM's <a href="https://dom.spec.whatwg.org/#concept-nodeiterator-traverse">traverse</a>
/// moves a <i>candidate</i> reference and only promotes it to the reference once a node is accepted, and its
/// <a href="https://dom.spec.whatwg.org/#nodeiterator-pre-removing-steps">pre-remove steps</a> adjust
/// <b>both</b> positions when a node is removed. AngleSharp's <c>NodeIterator.Next</c> keeps the in-flight
/// position in a local, so the pre-remove steps adjust the field and the local overwrites it on the way out:
/// remove a node from inside the filter that is looking at it and the iterator is left pointing at the
/// detached node, from which no further traversal is possible. That is the case a live iterator exists for
/// and the one an ordinary page hits, because the filter is the only place a page can mutate the tree
/// mid-traversal.
/// </para>
/// <para>
/// <b>Why two AngleSharp iterators rather than two fields.</b> The pre-remove steps have to run at the
/// instant of the removal, on <i>every</i> path that removes a node — <c>removeChild</c>, <c>replaceChild</c>,
/// <c>innerHTML</c>, the parser — and AngleSharp's hook for that (<c>IPreRemove</c>, and the
/// <c>Document.AttachReference</c> that registers one) is internal to that assembly. What <i>is</i> reachable
/// is that an <c>INodeIterator</c> <b>is</b> such a hook: it is a node pointer that AngleSharp adjusts
/// correctly. So each of the standard's two positions is one, driven one step at a time with a filter that
/// accepts everything, and the page's filter runs <b>outside</b> the AngleSharp call — which is precisely
/// what stops the local from overwriting the adjustment. It costs one extra attached reference per iterator.
/// </para>
/// <para>
/// <b>Nothing generated moves.</b> <c>DomTypeMap</c> keys on the CLR interface, so this class gets the
/// generated <c>NodeIterator</c> shape and every projected member reaches it unchanged; the same seam
/// <c>DomTreeWalker</c> takes.
/// </para>
/// </remarks>
internal sealed class DomNodeIterator : INodeIterator
{
    /// <summary>
    /// A bound on <see cref="SyncTo"/>'s walk. Each step moves one position in a finite collection and the
    /// loop only ever steps towards its target, so this cannot be reached; it is here because the collection
    /// is live and a removal between two steps is a tree this code did not choose.
    /// </summary>
    private const int SyncLimit = 1_000_000;

    private readonly INode _root;
    private readonly FilterSettings _settings;
    private readonly NodeFilter? _filter;

    /// <summary>https://dom.spec.whatwg.org/#nodeiterator-reference.</summary>
    private readonly INodeIterator _reference;

    /// <summary>https://dom.spec.whatwg.org/#nodeiterator-candidate-reference.</summary>
    /// <remarks>
    /// The standard's is null between traversals and this one is not; it is simply re-synced to the reference
    /// at the top of every traverse, which is step 1 either way. What matters is that it is a real node
    /// pointer while a filter is running, because that is when it has to be adjusted.
    /// </remarks>
    private readonly INodeIterator _candidate;

    /// <summary>https://dom.spec.whatwg.org/#concept-traversal-active.</summary>
    private bool _active;

    internal DomNodeIterator(IDocument document, INode root, FilterSettings settings, NodeFilter? filter)
    {
        _root = root;
        _settings = settings;
        _filter = filter;

        // FilterSettings.All and no filter, so that one call is one position: whatToShow and the page's
        // filter are this class's to apply, outside AngleSharp's loop.
        _reference = document.CreateNodeIterator(root, FilterSettings.All);
        _candidate = document.CreateNodeIterator(root, FilterSettings.All);
    }

    /// <inheritdoc />
    public INode Root => _root;

    /// <inheritdoc />
    public INode Reference => _reference.Reference;

    /// <inheritdoc />
    public bool IsBeforeReference => _reference.IsBeforeReference;

    /// <inheritdoc />
    public FilterSettings Settings => _settings;

    /// <inheritdoc />
    public NodeFilter Filter => _filter ?? (static _ => FilterResult.Accept);

    /// <inheritdoc />
    public INode? Next() => Traverse(forward: true);

    /// <inheritdoc />
    public INode? Previous() => Traverse(forward: false);

    /// <summary>https://dom.spec.whatwg.org/#concept-nodeiterator-traverse.</summary>
    /// <remarks>
    /// The loop terminates because every iteration moves the candidate one position forward (or backward) in
    /// a finite collection, or breaks: a step that has nowhere to go answers <see langword="null"/> and
    /// leaves the pointer where it was, which is the standard's "if there is no such node, then break".
    /// </remarks>
    private INode? Traverse(bool forward)
    {
        // Step 1. After an accepted traversal the two are already at the same position, so this is free in
        // the case that happens; it costs a step per node examined only after a traversal that accepted none.
        SyncTo(_candidate, _reference);

        while (true)
        {
            var stepped = forward ? _candidate.Next() : _candidate.Previous();

            if (stepped is null)
            {
                // Steps 3.1/3.2's "return null" — the reference and its pointer stay exactly where the
                // pre-remove steps left them, which is why the reference is a pointer of its own.
                return null;
            }

            var node = _candidate.Reference;

            // Filtering may run the page's filter, which may remove nodes; both pointers are adjusted by
            // AngleSharp while it does, and neither this method nor the standard's re-reads them until now.
            if (FilterNode(node) != FilterResult.Accept)
            {
                continue;
            }

            // Steps 3.4.1-3.4.3: the reference becomes the candidate — the *adjusted* candidate, which is
            // what makes a removal during filtering leave the iterator on a node that is still in the tree —
            // and the value answered is the node that was filtered.
            SyncTo(_reference, _candidate);
            return node;
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-node-filter.</summary>
    /// <remarks>
    /// Step 3's bit arithmetic is written out because AngleSharp's own <c>FilterSettings.Accepts</c> is
    /// internal to that assembly. Step 1's active flag is what makes a filter that traverses its own iterator
    /// an <c>InvalidStateError</c> instead of unbounded recursion on the page's thread.
    /// </remarks>
    private FilterResult FilterNode(INode node)
    {
        if (_active)
        {
            throw new DomException(DomError.InvalidState);
        }

        var bit = (int) node.NodeType - 1;

        if ((uint) bit >= 32 || (((ulong) _settings >> bit) & 1) == 0)
        {
            return FilterResult.Skip;
        }

        if (_filter is null)
        {
            return FilterResult.Accept;
        }

        _active = true;

        try
        {
            return _filter(node);
        }
        finally
        {
            _active = false;
        }
    }

    /// <summary>
    /// Moves <paramref name="from"/> to the position <paramref name="to"/> holds, which is the standard's
    /// assignment of one node pointer to another.
    /// </summary>
    /// <remarks>
    /// A pointer can only be moved by stepping it, so the assignment is a walk. It is one step or none in
    /// every case a page produces: after an accepted traversal the two are equal, and after a rejected one
    /// they are as far apart as the traversal walked.
    /// </remarks>
    private static void SyncTo(INodeIterator from, INodeIterator to)
    {
        var target = to.Reference;
        var before = to.IsBeforeReference;

        for (var guard = 0; !ReferenceEquals(from.Reference, target); guard++)
        {
            // A descendant of the current node is Following it and an ancestor is Preceding it, which is the
            // pre-order the iterator collection is in.
            var stepped = (from.Reference.CompareDocumentPosition(target) & DocumentPositions.Following) != 0
                ? from.Next()
                : from.Previous();

            if (stepped is null || guard >= SyncLimit)
            {
                // The target left the collection between the two pointers being read, which no page can
                // arrange: both are adjusted by the same removal. Leaving the pointer where it is keeps it on
                // a node in the tree, which is the property the rest of this class needs.
                return;
            }
        }

        if (from.IsBeforeReference != before)
        {
            // A step in the direction of the flag flips it without moving the node, which is the one place
            // AngleSharp's iterator does exactly what the standard's pointer assignment needs.
            if (before)
            {
                from.Previous();
            }
            else
            {
                from.Next();
            }
        }
    }
}

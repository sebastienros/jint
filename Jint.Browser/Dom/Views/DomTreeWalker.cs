using AngleSharp.Dom;

namespace Jint.Browser.Dom.Views;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-treewalker">DOM §6.1</a>'s <c>TreeWalker</c>, implemented
/// against <c>AngleSharp.Dom.ITreeWalker</c> so that the generated <c>TreeWalker</c> shape projects it
/// without knowing which implementation it holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one algorithm in this package that is not AngleSharp's, and the reason is a denial of
/// service rather than a conformance gap.</b> AngleSharp's own <c>TreeWalker.ToPrevious</c> never assigns its
/// <c>sibling</c> variable inside the loop that reads it and never moves to the parent, so
/// <c>previousNode()</c> spins forever the moment the previous sibling is not accepted outright — a filter
/// answering <c>FILTER_REJECT</c> or <c>FILTER_SKIP</c>, or a <c>currentNode</c> pointed outside the root next
/// to a node <c>whatToShow</c> excludes. Nothing can bound that from outside: a node excluded by
/// <c>whatToShow</c> is <c>FILTER_SKIP</c> <em>without the page's filter being called</em>, so the loop never
/// re-enters the engine and no constraint, cancellation token or page budget is ever consulted. One line of
/// page script wedged the thread that owns the page. It is recorded in
/// <a href="../AGENTS.md">the divergence table</a> and reported upstream; carrying only the one broken method
/// here would leave the package with two traversals and a termination argument for neither, so all seven are
/// the standard's.
/// </para>
/// <para>
/// <b>Every loop below says why it makes progress</b>, because that is the property the four hanging
/// documents were about. The measure is the node's position in the tree's pre-order: forward for
/// <see cref="ToNext"/>, <see cref="ToNextSibling"/> and the <c>"first"</c> half of
/// <see cref="TraverseChildren"/>, backward for <see cref="ToPrevious"/>, <see cref="ToParent"/> and the
/// mirrors. A tree is finite, so a strictly monotone walk over it ends; what the standard's algorithms add is
/// that every branch either moves the measure or returns.
/// </para>
/// <para>
/// A filter that mutates the tree it is walking can still make a walk run long, exactly as it can in a
/// browser — the standard's algorithms read the tree as they go, deliberately, so a walk sees the tree it is
/// in rather than a snapshot. That is bounded by the same thing an ordinary <c>while (true)</c> in page
/// script is, which is <c>BrowserOptions.MaxTaskDuration</c>: it works there because the mutation is script,
/// and it is why it could not work here.
/// </para>
/// </remarks>
internal sealed class DomTreeWalker : ITreeWalker
{
    /// <summary>What a walker created with no filter reports from <see cref="Filter"/>.</summary>
    /// <remarks>
    /// The <em>algorithm</em> reads <see cref="_filter"/> and answers <c>FILTER_ACCEPT</c> for a null one
    /// (DOM §6's filter, step 4) rather than calling this, so a walker with no filter costs no delegate hop
    /// per node. This exists because <c>ITreeWalker.Filter</c> is not nullable; what script reads is the
    /// value the page passed, which <c>DomViewMembers.Filter</c> keeps.
    /// </remarks>
    private static readonly NodeFilter _acceptAll = static _ => FilterResult.Accept;

    private readonly NodeFilter? _filter;

    /// <summary>
    /// <a href="https://dom.spec.whatwg.org/#concept-traversal-active">DOM §6</a>'s <i>is active</i> flag.
    /// </summary>
    private bool _isActive;

    internal DomTreeWalker(INode root, FilterSettings settings, NodeFilter? filter)
    {
        Root = root;
        Settings = settings;
        _filter = filter;
        Current = root;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-root.</summary>
    public INode Root { get; }

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-whattoshow.</summary>
    public FilterSettings Settings { get; }

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-filter.</summary>
    public NodeFilter Filter => _filter ?? _acceptAll;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-treewalker-currentnode — a plain attribute, and the setter's steps
    /// are "set this's current to the given value" with <b>no root check at all</b>. A walker may be pointed
    /// anywhere, and it is each method's own root test that stops the walk afterwards.
    /// </summary>
    public INode Current { get; set; }

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-parentnode.</summary>
    public INode? ToParent()
    {
        var node = Current;

        // Progress: every iteration replaces node with its own parent, so the walk climbs one finite
        // ancestor path and ends at the root, or at a node with no parent when current is outside the root.
        while (node != Root)
        {
            var parent = node.Parent;

            if (parent is null)
            {
                return null;
            }

            node = parent;

            if (Filtering(node) == FilterResult.Accept)
            {
                Current = node;
                return node;
            }
        }

        return null;
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-firstchild.</summary>
    public INode? ToFirst() => TraverseChildren(first: true);

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-lastchild.</summary>
    public INode? ToLast() => TraverseChildren(first: false);

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-nextsibling.</summary>
    public INode? ToNextSibling() => TraverseSiblings(next: true);

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-previoussibling.</summary>
    public INode? ToPreviousSibling() => TraverseSiblings(next: false);

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-nextnode.</summary>
    public INode? ToNext()
    {
        var node = Current;
        var result = FilterResult.Accept;

        // Progress: node moves strictly forward in pre-order on every pass — into its first child, or to the
        // next sibling of itself or of an ancestor — and the walk returns null the moment neither exists or
        // the climb reaches the root. FILTER_REJECT is the only thing that suppresses the descent, which is
        // what makes it mean "this node and its subtree" where FILTER_SKIP means "not this node, but do look
        // at its children".
        while (true)
        {
            while (result != FilterResult.Reject && node.HasChildNodes)
            {
                node = node.FirstChild!;
                result = Filtering(node);

                if (result == FilterResult.Accept)
                {
                    Current = node;
                    return node;
                }
            }

            INode? sibling = null;

            for (var temporary = node; temporary is not null; temporary = temporary.Parent)
            {
                if (temporary == Root)
                {
                    return null;
                }

                sibling = temporary.NextSibling;

                if (sibling is not null)
                {
                    break;
                }
            }

            // The climb ran out of parents without meeting the root, which is what a currentNode outside the
            // root looks like from here. Returning is the whole difference between a walk and a loop.
            if (sibling is null)
            {
                return null;
            }

            node = sibling;
            result = Filtering(node);

            if (result == FilterResult.Accept)
            {
                Current = node;
                return node;
            }
        }
    }

    /// <summary>https://dom.spec.whatwg.org/#dom-treewalker-previousnode.</summary>
    public INode? ToPrevious()
    {
        var node = Current;

        // Progress: every pass moves node strictly backward in pre-order. The previous sibling's whole
        // subtree precedes node, so descending into it after the sibling step still lands before where node
        // started; and the climb to a parent is a step back too. The walk ends at the root, or at a node with
        // no parent when current is outside the root.
        while (node != Root)
        {
            var sibling = node.PreviousSibling;

            while (sibling is not null)
            {
                node = sibling;
                var result = Filtering(node);

                // The deepest last descendant that is not rejected — which is the node immediately before
                // the one we came from, in reverse document order.
                while (result != FilterResult.Reject && node.HasChildNodes)
                {
                    node = node.LastChild!;
                    result = Filtering(node);
                }

                if (result == FilterResult.Accept)
                {
                    Current = node;
                    return node;
                }

                // AngleSharp's own loop is missing exactly this line, and the one below it: without them the
                // walk re-reads the same sibling forever.
                sibling = node.PreviousSibling;
            }

            if (node == Root || node.Parent is null)
            {
                return null;
            }

            node = node.Parent;

            if (Filtering(node) == FilterResult.Accept)
            {
                Current = node;
                return node;
            }
        }

        return null;
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-traverse-children.</summary>
    private INode? TraverseChildren(bool first)
    {
        var node = first ? Current.FirstChild : Current.LastChild;

        // Progress: this is one pre-order walk of the current node's subtree, in the direction `first`
        // chooses. A descent goes strictly deeper, a sibling step strictly further along, and the climb that
        // follows an exhausted sibling list returns as soon as it reaches the subtree's own boundary — the
        // root, the node the walk started from, or no parent at all. So no node is entered more than twice
        // and the subtree is finite.
        while (node is not null)
        {
            var result = Filtering(node);

            if (result == FilterResult.Accept)
            {
                Current = node;
                return node;
            }

            if (result == FilterResult.Skip)
            {
                // FILTER_SKIP passes over the node and keeps its children; FILTER_REJECT falls through to
                // the sibling walk below, which is what drops the subtree with it.
                var child = first ? node.FirstChild : node.LastChild;

                if (child is not null)
                {
                    node = child;
                    continue;
                }
            }

            while (node is not null)
            {
                var sibling = first ? node.NextSibling : node.PreviousSibling;

                if (sibling is not null)
                {
                    node = sibling;
                    break;
                }

                var parent = node.Parent;

                if (parent is null || parent == Root || parent == Current)
                {
                    return null;
                }

                node = parent;
            }
        }

        return null;
    }

    /// <summary>https://dom.spec.whatwg.org/#concept-traverse-siblings.</summary>
    private INode? TraverseSiblings(bool next)
    {
        var node = Current;

        if (node == Root)
        {
            return null;
        }

        // Progress: the inner loop moves node strictly along the traversal direction — a sibling, or the
        // first/last child of one — and when it runs out, the outer step climbs to the parent and takes
        // *its* sibling, which is past everything the subtree just covered. Either way the position moves,
        // and both the root test and the null-parent test end the walk.
        while (true)
        {
            var sibling = next ? node.NextSibling : node.PreviousSibling;

            while (sibling is not null)
            {
                node = sibling;
                var result = Filtering(node);

                if (result == FilterResult.Accept)
                {
                    Current = node;
                    return node;
                }

                sibling = next ? node.FirstChild : node.LastChild;

                if (result == FilterResult.Reject || sibling is null)
                {
                    sibling = next ? node.NextSibling : node.PreviousSibling;
                }
            }

            var parent = node.Parent;

            if (parent is null || parent == Root)
            {
                return null;
            }

            node = parent;

            // An accepted ancestor is the end of the sibling walk rather than its answer: the standard
            // returns null here, because the node the walker would report is one it has already passed.
            if (Filtering(node) == FilterResult.Accept)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// <a href="https://dom.spec.whatwg.org/#concept-node-filter">DOM §6</a>'s filter algorithm, whole.
    /// </summary>
    /// <remarks>
    /// The <c>whatToShow</c> test is step 3's own bit arithmetic rather than AngleSharp's
    /// <c>FilterSettings.Accepts</c>, which is internal to that assembly: the <i>n</i>th bit, where <i>n</i>
    /// is the node type minus one. Step 1's re-entrancy refusal is what stops a filter that walks its own
    /// walker from recursing through this traversal once per node it is handed — unbounded CLR recursion on
    /// the page's own thread, and the same denial of service in a second shape.
    /// </remarks>
    private FilterResult Filtering(INode node)
    {
        if (_isActive)
        {
            // Translated to an InvalidStateError DOMException by DomFailures.Guard, which wraps every
            // generated member body — including the seven this class answers.
            throw new DomException(DomError.InvalidState);
        }

        if (((ulong) Settings & (1UL << ((int) node.NodeType - 1))) == 0)
        {
            return FilterResult.Skip;
        }

        if (_filter is null)
        {
            return FilterResult.Accept;
        }

        _isActive = true;

        try
        {
            return _filter(node);
        }
        finally
        {
            // A filter that throws leaves the walker usable and current where it was, which is what
            // dom/traversal/TreeWalker-acceptNode-filter.html asserts after each of its four throwing shapes.
            _isActive = false;
        }
    }
}

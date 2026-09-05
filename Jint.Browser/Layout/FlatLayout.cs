using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom.Views;

namespace Jint.Browser.Layout;

/// <summary>
/// The flat renderer: one deterministic box per rendered element, computed from tree order alone.
/// <para>
/// Design doc §8, "Input without layout".
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>One model answers both sides.</b> <c>Element.getBoundingClientRect</c>,
/// <c>document.elementFromPoint</c>, <c>DOM.getBoxModel</c>, <c>DOM.getNodeForLocation</c> and
/// <c>Input.dispatchMouseEvent(x, y)</c> are all this class, so a client that reads a box, clicks its centre
/// and asks what was hit gets one consistent story rather than three approximations that disagree.
/// </para>
/// <para>
/// <b>The rows.</b> Every rendered element gets an ordinal <c>i</c> in tree order and owns the row
/// <c>[i·R, (i+1)·R)</c> with <c>R = <see cref="RowHeight"/></c>. Its box starts at that row and is as tall
/// as its whole subtree — <c>R × (1 + rendered descendants)</c> — and as wide as the viewport. Boxes
/// therefore nest exactly as the tree does and never straddle, and the deepest box containing a point is
/// always the owner of the row the point is in, because a descendant's rows all come after its ancestor's
/// first one. So the centre of a leaf hits the leaf, the centre of a container hits a descendant — which is
/// what a browser does — and the click bubbles back up through the container.
/// </para>
/// <para>
/// <b>It is recomputed per query and never cached.</b> A cache would need an invalidation signal, and the
/// only one available is an AngleSharp <c>MutationObserver</c> over the whole document — which would make
/// every DOM mutation on every page pay for mutation records whether or not anything ever asks for a box.
/// One walk shares a cascade so each element's selectors are matched once, not again for every descendant.
/// The scope ends with the query, so mutations, focus and media changes need no invalidation machinery.
/// </para>
/// </remarks>
internal sealed class FlatLayout
{
    /// <summary>The height of one row, which is the height of a leaf element's box.</summary>
    /// <remarks>
    /// A line height, and the one number the whole model is built from. It is deliberately not
    /// configurable: a client that computes a coordinate from a box this model gave it gets the same answer
    /// whatever it is, and a host that could change it would change every recorded coordinate with it.
    /// </remarks>
    internal const double RowHeight = 16;

    private readonly List<IElement> _elements = [];
    private readonly List<int> _depths = [];

    private FlatLayout(double viewportWidth, double viewportHeight, double scrollY)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        ScrollY = scrollY;
    }

    /// <summary>The width every box is given, which is the viewport's.</summary>
    internal double ViewportWidth { get; }

    /// <summary>The height of the window the boxes are seen through.</summary>
    internal double ViewportHeight { get; }

    /// <summary>How far the page is scrolled, which every viewport-relative answer subtracts.</summary>
    internal double ScrollY { get; }

    /// <summary>How many elements are rendered, which is also how many rows the document has.</summary>
    internal int Count => _elements.Count;

    /// <summary>The height of the whole document, which is one row per rendered element.</summary>
    internal double ContentHeight => _elements.Count * RowHeight;

    /// <summary>The largest <c>scrollY</c> the document admits, which is zero for a document that fits.</summary>
    internal double MaxScrollY => Math.Max(0, ContentHeight - ViewportHeight);

    /// <summary>Lays <paramref name="document"/> out, or answers an empty layout when there is none.</summary>
    /// <param name="document">The document to walk, or <see langword="null"/>.</param>
    /// <param name="visibility">
    /// What <c>hidden</c> means, which is R7's — the <c>hidden</c> content attribute and the cascade's
    /// <c>display</c> and <c>visibility</c>. The instance is the page's, because its cascade probe latches.
    /// </param>
    /// <param name="viewportWidth">The viewport width, which is every box's width.</param>
    /// <param name="viewportHeight">The viewport height, which bounds a hit test.</param>
    /// <param name="scrollY">How far the page is scrolled.</param>
    internal static FlatLayout Of(
        IDocument? document,
        ElementVisibility visibility,
        double viewportWidth,
        double viewportHeight,
        double scrollY)
    {
        var layout = new FlatLayout(viewportWidth, viewportHeight, scrollY);
        var cascade = visibility.CreateTraversal(document);

        if (document?.DocumentElement is { } root && IsRendered(root, visibility, cascade))
        {
            layout.Walk(root, visibility, cascade);
        }

        return layout;
    }

    /// <summary>
    /// Whether <paramref name="element"/> has a box of its own, ignoring its ancestors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rendered set is HTML's, minus what a rendering would have needed and this has not: the metadata
    /// content a browser never draws (<c>&lt;head&gt;</c> and everything in it, plus a
    /// <c>&lt;script&gt;</c>, <c>&lt;style&gt;</c>, <c>&lt;template&gt;</c> or <c>&lt;noscript&gt;</c>
    /// wherever it sits), and whatever R7's <see cref="ElementVisibility"/> calls not rendered — the
    /// <c>hidden</c> content attribute, <c>display: none</c> and <c>visibility: hidden|collapse</c> from the
    /// cascade. <c>aria-hidden</c> deliberately does <b>not</b> remove a box: it removes a node from the
    /// accessibility tree and changes nothing about the rendering, which is why the question asked here is
    /// <c>RenderingReasonFor</c> and not <c>ReasonFor</c>.
    /// </para>
    /// <para>
    /// <b>One simplification, stated rather than hidden: an excluded element takes its subtree with it.</b>
    /// That is right for <c>display: none</c> and for the metadata elements, and wrong for
    /// <c>visibility: hidden</c>, which CSS inherits and a <c>visibility: visible</c> descendant escapes. A
    /// model whose boxes are rows cannot give a descendant a row inside a parent that has none, so the
    /// choice is between this and giving the descendant a box that does not nest; the nesting is what the
    /// hit test depends on.
    /// </para>
    /// </remarks>
    internal static bool IsRendered(IElement element, ElementVisibility visibility, CssCascade.Traversal? cascade = null)
    {
        if (element is IHtmlHeadElement)
        {
            return false;
        }

        switch (element.LocalName)
        {
            case "script":
            case "style":
            case "template":
            case "noscript":
            case "title":
            case "meta":
            case "link":
                return false;
            default:
                break;
        }

        return visibility.RenderingReasonFor(element, cascade) == AxIgnoredReason.None;
    }

    /// <summary>The ordinal of <paramref name="element"/>, or <c>-1</c> when it is not rendered.</summary>
    internal int OrdinalOf(IElement element)
    {
        for (var i = 0; i < _elements.Count; i++)
        {
            if (ReferenceEquals(_elements[i], element))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The element that owns row <paramref name="ordinal"/>, or <see langword="null"/>.</summary>
    internal IElement? At(int ordinal)
        => (uint) ordinal < (uint) _elements.Count ? _elements[ordinal] : null;

    /// <summary>How many rendered elements are inside the one at <paramref name="ordinal"/>.</summary>
    /// <remarks>
    /// The subtree of a pre-order walk is the run that follows it at a greater depth, so this is a scan of
    /// that run rather than a second array built for every layout.
    /// </remarks>
    internal int DescendantsOf(int ordinal)
    {
        var depth = _depths[ordinal];
        var count = 0;

        for (var i = ordinal + 1; i < _depths.Count && _depths[i] > depth; i++)
        {
            count++;
        }

        return count;
    }

    /// <summary>The box of <paramref name="element"/> in document coordinates, or <see langword="null"/>.</summary>
    internal FlatBox? DocumentBoxOf(IElement element)
    {
        var ordinal = OrdinalOf(element);
        return ordinal < 0 ? null : DocumentBoxAt(ordinal);
    }

    /// <summary>The box of row <paramref name="ordinal"/> in document coordinates.</summary>
    internal FlatBox DocumentBoxAt(int ordinal) => new(
        0,
        ordinal * RowHeight,
        ViewportWidth,
        RowHeight * (1 + DescendantsOf(ordinal)));

    /// <summary>The box of <paramref name="element"/> relative to the viewport, or <see langword="null"/>.</summary>
    internal FlatBox? ClientBoxOf(IElement element)
        => DocumentBoxOf(element) is { } box ? box with { Y = box.Y - ScrollY } : null;

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-document-elementfrompoint — the topmost box at
    /// <paramref name="x"/>, <paramref name="y"/> in viewport coordinates, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The deepest box containing a point is always the owner of the row the point falls in, so a hit test
    /// is one division and one lookup rather than a walk. A point outside the viewport hits nothing, which
    /// is what CSSOM View says.
    /// </remarks>
    internal IElement? ElementFromPoint(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || y < 0 || x >= ViewportWidth || y >= ViewportHeight)
        {
            return null;
        }

        var row = (y + ScrollY) / RowHeight;
        return row >= 0 && row < _elements.Count ? _elements[(int) row] : null;
    }

    private void Walk(IElement root, ElementVisibility visibility, CssCascade.Traversal? cascade)
    {
        var stack = new Stack<(IElement Element, int Depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            var (element, depth) = stack.Pop();
            _elements.Add(element);
            _depths.Add(depth);

            var children = element.Children;
            for (var i = children.Length - 1; i >= 0; i--)
            {
                var child = children[i];
                if (IsRendered(child, visibility, cascade))
                {
                    stack.Push((child, depth + 1));
                }
            }
        }
    }
}

/// <summary>One element's rectangle, in whichever coordinate space the caller asked for.</summary>
/// <param name="X">The left edge, which is always zero: nothing here lays out horizontally.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width, which is the viewport's.</param>
/// <param name="Height">The height, which is one row per element of the subtree.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct FlatBox(double X, double Y, double Width, double Height)
{
    /// <summary>The rectangle a hidden element answers, which is the origin with no extent.</summary>
    internal static FlatBox Empty { get; }

    /// <summary>The right edge.</summary>
    internal double Right => X + Width;

    /// <summary>The bottom edge.</summary>
    internal double Bottom => Y + Height;

    /// <summary>The horizontal centre, which is where a client clicks.</summary>
    internal double CenterX => X + (Width / 2);

    /// <summary>The vertical centre.</summary>
    internal double CenterY => Y + (Height / 2);

    /// <summary>The four corners, clockwise from the top left — the protocol's <c>Quad</c>.</summary>
    internal double[] ToQuad() => [X, Y, Right, Y, Right, Bottom, X, Bottom];
}

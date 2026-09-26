using Jint.HtmlParser;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom.Views;

namespace Jint.Browser.Layout;

/// <summary>
/// The flat renderer: deterministic boxes from tree order, with horizontal single-line flex rows.
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
/// <b>Single-line flex rows partition their width.</b> Stacking their children at viewport width makes a
/// trailing icon own the centre of a toolbar. <see cref="FlexRow"/> distributes the available width using
/// computed bases, growth and shrinkage; the row's height is its tallest child plus its own row. The same
/// rectangles answer hit tests, offsets and resize measurements. Other formatting remains the flat model:
/// no text measurement, wrapping, gaps, margins, min/max sizing, main-axis justification, ordering or positioned layout.
/// </para>
/// <para>
/// <b>PageLayout owns reuse and invalidation.</b> This factory still builds a fresh layout. The page may
/// share its size query and cascade until a Browser-owned mutation, environment change or lifecycle boundary.
/// Unclassified native writers and construction keep query-local measurements. No layout mutation observer
/// is installed, so pages which never read geometry allocate no layout observer records.
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

    private readonly List<Element> _elements = [];
    private readonly List<int> _depths = [];
    private List<FlatBox>? _boxes;

    private FlatLayout(double viewportWidth, double viewportHeight, double scrollY, Action? checkpoint, CancellationToken token)
    {
        _checkpoint = checkpoint;
        _token = token;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        ScrollY = scrollY;
    }

    private readonly Action? _checkpoint;
    private readonly CancellationToken _token;
    private int _work;
    private void Step()
    {
        if ((++_work & 255) == 0)
        {
            _token.ThrowIfCancellationRequested();
            _checkpoint?.Invoke();
        }
    }

    /// <summary>The width available to the root box.</summary>
    internal double ViewportWidth { get; }

    /// <summary>The height of the window the boxes are seen through.</summary>
    internal double ViewportHeight { get; }

    /// <summary>How far the page is scrolled, which every viewport-relative answer subtracts.</summary>
    internal double ScrollY { get; }

    /// <summary>How many elements are rendered, including children sharing a flex row.</summary>
    internal int Count => _elements.Count;

    /// <summary>The height of the whole document, sharing vertical space inside flex rows.</summary>
    internal double ContentHeight => _boxes is { Count: > 0 } ? _boxes[0].Height : _elements.Count * RowHeight;

    /// <summary>The largest <c>scrollY</c> the document admits, which is zero for a document that fits.</summary>
    internal double MaxScrollY => Math.Max(0, ContentHeight - ViewportHeight);

    /// <summary>Lays <paramref name="document"/> out, or answers an empty layout when there is none.</summary>
    /// <param name="document">The document to walk, or <see langword="null"/>.</param>
    /// <param name="visibility">
    /// What <c>hidden</c> means, which is R7's — the <c>hidden</c> content attribute and the cascade's
    /// <c>display</c> and <c>visibility</c>. The instance is the page's, because its cascade probe latches.
    /// </param>
    /// <param name="viewportWidth">The viewport width, partitioned between children of flex rows.</param>
    /// <param name="viewportHeight">The viewport height, which bounds a hit test.</param>
    /// <param name="scrollY">How far the page is scrolled.</param>
    /// <param name="sizes">Optional measurements from the page's current layout revision.</param>
    /// <param name="checkpoint">Checks the page's execution constraints during traversal.</param>
    /// <param name="token">Cancels this layout read.</param>
    internal static FlatLayout Of(
        Document? document,
        ElementVisibility visibility,
        double viewportWidth,
        double viewportHeight,
        double scrollY,
        SizeQuery? sizes = null,
        Action? checkpoint = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke();
        var layout = new FlatLayout(viewportWidth, viewportHeight, scrollY, checkpoint, token);
        var cascade = sizes is null ? visibility.CreateTraversal(document) : sizes.Cascade;

        if (document?.DocumentElement is { } root && IsRendered(root, visibility, cascade))
        {
            if (layout.Walk(root, visibility, cascade))
            {
                layout.Arrange(sizes ?? new SizeQuery(document, visibility, viewportWidth, cascade, checkpoint, token));
            }
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
    internal static bool IsRendered(Element element, ElementVisibility visibility, CssCascade.Traversal? cascade = null)
    {
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "head" })
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
    internal int OrdinalOf(Element element)
    {
        for (var i = 0; i < _elements.Count; i++)
        {
            Step();
            if (ReferenceEquals(_elements[i], element))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The element that owns row <paramref name="ordinal"/>, or <see langword="null"/>.</summary>
    internal Element? At(int ordinal)
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
    internal FlatBox? DocumentBoxOf(Element element)
    {
        var ordinal = OrdinalOf(element);
        return ordinal < 0 ? null : DocumentBoxAt(ordinal);
    }

    /// <summary>The box of row <paramref name="ordinal"/> in document coordinates.</summary>
    internal FlatBox DocumentBoxAt(int ordinal) => _boxes is not null ? _boxes[ordinal] : new(
        0,
        ordinal * RowHeight,
        ViewportWidth,
        RowHeight * (1 + DescendantsOf(ordinal)));

    /// <summary>The box of <paramref name="element"/> relative to the viewport, or <see langword="null"/>.</summary>
    internal FlatBox? ClientBoxOf(Element element)
        => DocumentBoxOf(element) is { } box ? box with { Y = box.Y - ScrollY } : null;

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-document-elementfrompoint — the topmost box at
    /// <paramref name="x"/>, <paramref name="y"/> in viewport coordinates, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Without flex rows, the deepest box containing a point is the owner of its row: one division and
    /// one lookup. Flex rows require checking both coordinates against their assigned rectangles, in
    /// reverse tree order so descendants win. A point outside the viewport hits nothing, which
    /// is what CSSOM View says.
    /// </remarks>
    internal Element? ElementFromPoint(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || x < 0 || y < 0 || x >= ViewportWidth || y >= ViewportHeight)
        {
            return null;
        }

        if (_boxes is not null)
        {
            var documentY = y + ScrollY;
            for (var i = _boxes.Count - 1; i >= 0; i--)
            {
                var box = _boxes[i];
                if (x >= box.X && x < box.Right && documentY >= box.Y && documentY < box.Bottom)
                {
                    return _elements[i];
                }
            }

            return null;
        }

        var row = (y + ScrollY) / RowHeight;
        return row >= 0 && row < _elements.Count ? _elements[(int) row] : null;
    }

    private void Arrange(SizeQuery sizes)
    {
        _boxes = new List<FlatBox>(_elements.Count);
        foreach (var element in _elements)
        {
            _boxes.Add(sizes.Place(element));
        }
    }

    private static List<Element> NativeChildren(Element parent, Action? checkpoint, CancellationToken token)
    {
        var children = new List<Element>();
        var work = 0;
        for (var child = parent.FirstChild; child is not null; child = child.NextSibling)
        {
            if ((++work & 255) == 0) { token.ThrowIfCancellationRequested(); checkpoint?.Invoke(); }
            if (child is Element element) children.Add(element);
        }
        token.ThrowIfCancellationRequested();
        checkpoint?.Invoke();
        return children;
    }

    private bool Walk(Element root, ElementVisibility visibility, CssCascade.Traversal? cascade)
    {
        var hasFlexRows = false;
        var stack = new Stack<(Element Element, int Depth)>();
        stack.Push((root, 0));

        while (stack.Count > 0)
        {
            Step();
            var (element, depth) = stack.Pop();
            _elements.Add(element);
            _depths.Add(depth);
            hasFlexRows = hasFlexRows || FlexRow.IsHorizontal(element, cascade);

            for (var child = element.LastChild; child is not null; child = child.PreviousSibling)
            {
                Step();
                if (child is Element candidate && IsRendered(candidate, visibility, cascade))
                    stack.Push((candidate, depth + 1));
            }
        }

        return hasFlexRows;
    }

    /// <summary>
    /// A synchronous size-only query: ancestors decide visibility and width; flex siblings also determine
    /// distributed widths and stretched heights. Unrelated document branches need no rows.
    /// PageLayout may retain measurements and the cascade while its complete invalidation identity is valid.
    /// Native mutation scopes, parsing and unclassified callbacks always get independent queries.
    /// </summary>
    internal sealed class SizeQuery(
        Document? document,
        ElementVisibility visibility,
        double viewportWidth,
        CssCascade.Traversal? cascade, Action? checkpoint = null, CancellationToken token = default)
    {
        private int _work;
        private readonly Dictionary<Element, IReadOnlyList<Element>> _children = new(ReferenceEqualityComparer.Instance);
        private IReadOnlyList<Element> Children(Element element)
        {
            if (!_children.TryGetValue(element, out var children))
            {
                children = NativeChildren(element, checkpoint, token);
                _children.Add(element, children);
            }
            return children;
        }
        private void Step()
        {
            if ((++_work & 255) == 0) { token.ThrowIfCancellationRequested(); checkpoint?.Invoke(); }
        }
        internal CssCascade.Traversal? Cascade => cascade;

        private readonly Dictionary<Element, bool> _rendered = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, int> _rows = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, double> _widths = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, double> _heights = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, FlatBox> _sizes = new(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<Element, FlatBox> _positions = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, (int Next, double Offset)> _childrenPlaced = new(ReferenceEqualityComparer.Instance);

        // The same placement serves a whole layout and a single rectangle. A rectangle needs its
        // ancestors and preceding siblings, but never the positions of unrelated descendants.
        internal FlatBox Place(Element target)
        {
            if (!HasBox(target))
            {
                return FlatBox.Empty;
            }

            var ancestors = new Stack<Element>();
            for (var element = target; !_positions.ContainsKey(element); element = (element.ParentNode as Element)!)
            {
                Step();
                if (ReferenceEquals(element, document?.DocumentElement))
                {
                    _positions.Add(element, FlatBox.Empty);
                    break;
                }

                ancestors.Push(element);
            }

            while (ancestors.TryPop(out var element))
            {
                Step();
                if (_positions.ContainsKey(element))
                {
                    continue;
                }

                var parent = (element.ParentNode as Element)!;
                var parentBox = _positions[parent];
                var horizontal = FlexRow.IsHorizontal(parent, cascade);
                var reverse = horizontal && FlexRow.IsReversed(parent, cascade);
                var (next, offset) = _childrenPlaced.TryGetValue(parent, out var placed)
                    ? placed : (0, horizontal ? (reverse ? WidthOf(parent) : 0) : RowHeight);
                var children = Children(parent);
                for (; next < children.Count; next++)
                {
                    Step();
                    var child = children[next];
                    if (!HasBox(child))
                    {
                        continue;
                    }

                    if (!horizontal && ReferenceEquals(child, element))
                    {
                        // Its height affects the next sibling, not its own vertical origin. Leave
                        // this index pending so a later sibling query measures it when needed.
                        _positions[child] = new FlatBox(parentBox.X, parentBox.Y + offset, 0, 0);
                        break;
                    }

                    var width = horizontal ? WidthOf(child) : 0;
                    if (horizontal && reverse)
                    {
                        offset -= width;
                    }

                    var cross = 0d;
                    if (horizontal && FlexRow.Alignment(child, parent, cascade) is "center" or "flex-end" or "end")
                    {
                        var space = HeightOf(parent) - RowHeight - HeightOf(child);
                        cross = FlexRow.Alignment(child, parent, cascade) == "center" ? space / 2 : space;
                    }

                    _positions[child] = new FlatBox(
                        parentBox.X + (horizontal ? offset : 0),
                        parentBox.Y + (horizontal ? RowHeight + cross : offset), 0, 0);
                    offset += horizontal ? (reverse ? 0 : width) : HeightOf(child);
                    if (ReferenceEquals(child, element))
                    {
                        next++;
                        break;
                    }
                }

                _childrenPlaced[parent] = (next, offset);
            }

            var position = _positions[target];
            var measured = Measure(target);
            return measured with { X = position.X, Y = position.Y };
        }

        internal FlatBox Measure(Element target)
        {
            if (!_sizes.TryGetValue(target, out var size))
            {
                size = HasBox(target) ? new FlatBox(0, 0, WidthOf(target), HeightOf(target)) : FlatBox.Empty;
                _sizes.Add(target, size);
            }

            return size;
        }

        internal bool TryGetSize(Element target, out FlatBox size) => _sizes.TryGetValue(target, out size);

        internal double Width(Element target) => HasBox(target) ? WidthOf(target) : 0;

        internal bool HasBox(Element target)
        {
            var ancestors = new Stack<Element>();
            var rendered = false;
            for (Element? element = target; element is not null; element = (element.ParentNode as Element))
            {
                Step();
                if (_rendered.TryGetValue(element, out rendered))
                {
                    break;
                }

                ancestors.Push(element);
                if (ReferenceEquals(element, document?.DocumentElement))
                {
                    rendered = true;
                    break;
                }
            }

            while (ancestors.TryPop(out var ancestor))
            {
                Step();
                rendered = rendered && IsRendered(ancestor, visibility, cascade);
                _rendered.Add(ancestor, rendered);
            }

            return rendered;
        }

        private double WidthOf(Element target)
        {
            var ancestors = new Stack<Element>();
            for (var element = target; !_widths.ContainsKey(element); element = (element.ParentNode as Element)!)
            {
                Step();
                if (ReferenceEquals(element, document?.DocumentElement))
                {
                    _widths.Add(element, viewportWidth);
                    break;
                }

                ancestors.Push(element);
            }

            while (ancestors.TryPop(out var element))
            {
                Step();
                if (_widths.ContainsKey(element))
                {
                    continue;
                }

                var parent = (element.ParentNode as Element)!;
                var width = _widths[parent];
                if (FlexRow.IsHorizontal(parent, cascade))
                {
                    var children = Children(parent).Where(HasBox).ToArray();
                    var widths = FlexRow.Widths(children, width, cascade, Step, token);
                    for (var i = 0; i < children.Length; i++)
                    {
                        Step();
                        _widths.Add(children[i], widths[i]);
                    }
                }
                else
                {
                    _widths.Add(element, width);
                }
            }

            return _widths[target];
        }

        private double HeightOf(Element target)
        {
            var ancestors = new Stack<Element>();
            var element = target;
            while (!_heights.ContainsKey(element))
            {
                Step();
                if ((element.ParentNode as Element) is not { } parent
                    || !FlexRow.IsHorizontal(parent, cascade)
                    || FlexRow.Alignment(element, parent, cascade) != "stretch")
                {
                    _heights.Add(element, CountRows(element) * RowHeight);
                    break;
                }

                ancestors.Push(element);
                element = parent;
            }

            while (ancestors.TryPop(out element))
            {
                Step();
                _heights.Add(element, _heights[(element.ParentNode as Element)!] - RowHeight);
            }

            return _heights[target];
        }

        // A scroll clamp only needs proof that the document reaches the viewport's bottom.
        // Stop at that bound; partial counts must never enter the exact-size cache.
        internal double HeightUpTo(Element target, double limit)
        {
            var required = (int) Math.Min(int.MaxValue, Math.Ceiling(limit / RowHeight));
            var pending = new Stack<(Element Element, int Next, int Rows, int Limit, bool Horizontal)>();
            var element = target;
            var bound = required;
            var result = 0;
            while (true)
            {
                Step();
                if (_rows.TryGetValue(element, out var known))
                {
                    result = Math.Min(known, bound);
                }
                else if (!HasBox(element))
                {
                    result = 0;
                }
                else
                {
                    pending.Push((element, 0, 1, bound, FlexRow.IsHorizontal(element, cascade)));
                }

                while (pending.TryPop(out var frame))
                {
                    Step();
                    var rows = frame.Next == 0 ? frame.Rows
                        : frame.Horizontal ? Math.Max(frame.Rows, result + 1) : frame.Rows + result;
                    if (rows >= frame.Limit || frame.Next == Children(frame.Element).Count)
                    {
                        result = Math.Min(rows, frame.Limit);
                        if (rows < frame.Limit)
                        {
                            _rows.TryAdd(frame.Element, rows);
                        }
                        continue;
                    }

                    element = Children(frame.Element)[frame.Next];
                    bound = frame.Horizontal ? frame.Limit - 1 : frame.Limit - rows;
                    pending.Push((frame.Element, frame.Next + 1, rows, frame.Limit, frame.Horizontal));
                    break;
                }

                if (pending.Count == 0)
                {
                    return result * RowHeight;
                }
            }
        }

        private int CountRows(Element target)
        {
            var pending = new Stack<(Element Element, bool Visited)>();
            pending.Push((target, false));
            while (pending.TryPop(out var item))
            {
                Step();
                var (element, visited) = item;
                if (_rows.ContainsKey(element))
                {
                    continue;
                }

                if (!visited)
                {
                    if (!_rendered.TryGetValue(element, out var rendered))
                    {
                        rendered = IsRendered(element, visibility, cascade);
                        _rendered.Add(element, rendered);
                    }

                    if (!rendered)
                    {
                        _rows.Add(element, 0);
                        continue;
                    }

                    pending.Push((element, true));
                    foreach (var child in Children(element))
                    {
                        Step();
                        pending.Push((child, false));
                    }
                }
                else
                {
                    var rows = 0;
                    var horizontal = FlexRow.IsHorizontal(element, cascade);
                    foreach (var child in Children(element))
                    {
                        Step();
                        rows = horizontal ? Math.Max(rows, _rows[child]) : rows + _rows[child];
                    }

                    _rows.Add(element, rows + 1);
                }
            }

            return _rows[target];
        }
    }
}

/// <summary>One element's rectangle, in whichever coordinate space the caller asked for.</summary>
/// <param name="X">The left edge, including a flex item's horizontal offset.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The containing width, partitioned between flex items.</param>
/// <param name="Height">The synthetic subtree height, sharing rows between flex items.</param>
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

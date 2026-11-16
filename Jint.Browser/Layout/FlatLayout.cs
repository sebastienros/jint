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
    private readonly List<bool> _invisible = [];
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
        if (IsMetadata(element))
        {
            return false;
        }

        // A visibility: hidden box is still laid out, as CSS 2 §11.2 says, so hiding an editor's caret moves
        // nothing; it is only not hit (IsInvisible). One out of flow takes no space in a browser at all, so an
        // overlay that hides itself keeps no box. One hidden with its parent keeps its rows, as the parent does:
        // a caret positioned in a hidden layer appears and disappears with it without moving anything.
        return visibility.RenderingReasonFor(element, cascade) switch
        {
            AxIgnoredReason.None => true,
            AxIgnoredReason.NotVisible => !IsOutOfFlow(element, cascade)
                || element.ParentNode is Element parent && visibility.RenderingReasonFor(parent, cascade) == AxIgnoredReason.NotVisible,
            _ => false,
        };
    }

    /// <summary>
    /// Whether <paramref name="element"/>'s box is <c>visibility: hidden</c> or <c>collapse</c>: laid out, but
    /// never the target of a hit test, which goes to the visible box under it instead.
    /// </summary>
    internal static bool IsInvisible(Element element, ElementVisibility visibility, CssCascade.Traversal? cascade)
        => visibility.RenderingReasonFor(element, cascade) == AxIgnoredReason.NotVisible;

    /// <summary>Whether <paramref name="element"/> is metadata content that a rendering never draws.</summary>
    internal static bool IsMetadata(Element element)
    {
        if (element is { NamespaceUri: Namespaces.Html, LocalName: "head" })
        {
            return true;
        }

        return element.LocalName is "script" or "style" or "template" or "noscript" or "title" or "meta" or "link";
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
                if (!_invisible[i] && x >= box.X && x < box.Right && documentY >= box.Y && documentY < box.Bottom)
                {
                    return _elements[i];
                }
            }

            return null;
        }

        var row = (y + ScrollY) / RowHeight;
        return row >= 0 && row < _elements.Count ? _elements[(int) row] : null;
    }

    // A point read owns the same measurements as a complete layout, but only places branches
    // crossing its vertical coordinate. It must not be consulted again after dispatching a listener.
    internal readonly struct PointQuery(SizeQuery sizes, double width, double height, double scrollY)
    {
        internal FlatBox? ClientBoxOf(Element element)
        {
            if (!sizes.HasBox(element)) return null;
            var box = sizes.Place(element);
            return box with { Y = box.Y - scrollY };
        }

        internal Element? ElementFromPoint(double x, double y)
            => double.IsNaN(x) || double.IsNaN(y) || x < 0 || y < 0 || x >= width || y >= height
                ? null : sizes.ElementFromPoint(x, y + scrollY);
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

    /// <summary>
    /// Whether a block-level box says how wide it is — its width, max-width or min-width — which can make it
    /// narrower than its parent (SizeQuery.BlockWidth), so the row division cannot answer for it.
    /// </summary>
    private static bool HasAuthoredWidth(Element element, CssCascade.Traversal? cascade)
        => cascade?.LayoutOf(element) is { } style
            && CssCascade.ValueOf(style, "display") is "block" or "flow-root" or "list-item" or "flex" or "grid"
                or "inline-block" or "inline-flex" or "inline-grid"
            && (CssCascade.ValueOf(style, "width") is not (null or "" or "auto")
                || CssCascade.ValueOf(style, "max-width") is not (null or "" or "none")
                || CssCascade.ValueOf(style, "min-width") is not (null or "" or "auto" or "0" or "0px"));

    /// <summary>Whether <paramref name="element"/> is absolutely positioned, out of its parent's flow.</summary>
    private static bool IsOutOfFlow(Element element, CssCascade.Traversal? cascade)
        => cascade?.LayoutOf(element) is { } style
            && CssCascade.ValueOf(style, "position") is "absolute" or "fixed";

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
            var invisible = IsInvisible(element, visibility, cascade);
            _invisible.Add(invisible);
            // A positioned box can be narrower than its parent, which the row division cannot answer either.
            // So can an invisible one, which the row division would hit, and a column whose height or growth
            // gives a box more rows than its descendants.
            hasFlexRows = hasFlexRows || invisible || FlexRow.IsHorizontal(element, cascade) || IsOutOfFlow(element, cascade)
                || FlexRow.IsColumn(element, cascade) || HasAuthoredWidth(element, cascade);

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
    /// PageLayout may retain native measurement data across classified writes, never its old cascade or guards.
    /// Native mutation scopes, parsing and unclassified callbacks always get independent queries.
    /// </summary>
    internal sealed class SizeQuery(
        Document? document,
        ElementVisibility visibility,
        double viewportWidth,
        CssCascade.Traversal? cascade, SizeMeasurements? measurements, PageLayoutDiagnostics? diagnostics,
        Action? checkpoint = null, CancellationToken token = default)
    {
        internal SizeQuery(Document? document, ElementVisibility visibility, double viewportWidth,
            CssCascade.Traversal? cascade, Action? checkpoint = null, CancellationToken token = default)
            : this(document, visibility, viewportWidth, cascade, null, null, checkpoint, token) { }

        private int _work;
        internal SizeMeasurements Measurements { get; } = measurements ?? new();
        private Dictionary<Element, IReadOnlyList<Element>> _children => Measurements.Children;
        private readonly HashSet<Element>? _retainedRows = diagnostics is null || measurements is null
            ? null : new(measurements.Rows.Keys, ReferenceEqualityComparer.Instance);
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

        private Dictionary<Element, bool> _rendered => Measurements.Rendered;
        private Dictionary<Element, int> _rows => Measurements.Rows;
        private readonly Dictionary<Element, double> _widths = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, double> _heights = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, FlatBox> _sizes = new(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<Element, FlatBox> _positions = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Element, (int Next, double Offset)> _childrenPlaced = new(ReferenceEqualityComparer.Instance);

        private bool TryRows(Element element, out int rows)
        {
            if (!_rows.TryGetValue(element, out rows)) return false;
            if (_retainedRows?.Remove(element) == true) diagnostics!.RetainedRowUsed();
            return true;
        }

        /// <summary>CSSOM View's topmost box, with the complete layout's reverse tree-order precedence.</summary>
        internal Element? ElementFromPoint(double x, double y)
        {
            token.ThrowIfCancellationRequested();
            checkpoint?.Invoke();
            if (document?.DocumentElement is not { } root) return null;
            var pending = new Stack<(Element Element, bool Visited)>();
            pending.Push((root, false));
            while (pending.TryPop(out var frame))
            {
                Step();
                if (!HasBox(frame.Element)) continue;
                var box = Place(frame.Element);
                // Every descendant fits vertically inside its ancestor in the flat model.
                // Horizontal overflow is possible for nonshrinking flex items: never prune on X.
                if (y < box.Y || y >= box.Bottom) continue;
                if (frame.Visited)
                {
                    if (x >= box.X && x < box.Right && !IsInvisible(frame.Element, visibility, cascade))
                    {
                        token.ThrowIfCancellationRequested();
                        checkpoint?.Invoke();
                        return frame.Element;
                    }
                    continue;
                }
                pending.Push((frame.Element, true));
                foreach (var child in Children(frame.Element))
                {
                    Step();
                    pending.Push((child, false));
                }
            }
            token.ThrowIfCancellationRequested();
            checkpoint?.Invoke();
            return null;
        }

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
            return Overlay(target, measured with { X = position.X, Y = position.Y });
        }

        /// <summary>
        /// An absolutely positioned box with a pixel height of its own covers that height, or with its top and
        /// bottom set its parent's height, as far as its parent's box goes, or less than its row when nothing is
        /// inside it: a layer of an editor — Monaco's lines — is as tall as the editor, so a press
        /// below the last line is still on it. It is out of flow, so its rows, and every other box, stay as
        /// they are: its parent does not grow with it, which would feed back into an editor sizing its
        /// layers from its container.
        /// </summary>
        private FlatBox Overlay(Element target, FlatBox box)
        {
            if (cascade?.LayoutOf(target) is not { } style
                || CssCascade.ValueOf(style, "position") != "absolute"
                || target.ParentNode is not Element parent
                || !HasBox(parent))
            {
                return box;
            }

            // CSS 2 §10.6.4: with both its top and its bottom set and an auto height, it spans its containing
            // block — a canvas laid over its host with inset: 0.
            var height = CssCascade.ValueOf(style, "height");
            double pixels;
            if (height is null or "" or "auto")
            {
                if (CssCascade.ValueOf(style, "top") is null or "" or "auto" || CssCascade.ValueOf(style, "bottom") is null or "" or "auto")
                {
                    return box;
                }

                pixels = double.PositiveInfinity;
            }
            else if (!height.EndsWith("px", StringComparison.Ordinal) || FlexRow.Length(height, 0) is not { } length)
            {
                return box;
            }
            else
            {
                pixels = length;
            }

            // Shorter than its row only without boxes inside it, which have to stay inside it.
            if (pixels < box.Height)
            {
                return Children(target).Any(HasBox) ? box : box with { Height = pixels };
            }

            var room = Place(parent).Bottom - box.Y;
            return room > box.Height ? box with { Height = Math.Min(pixels, room) } : box;
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

        // A positioned box's own height can differ from its rows (Overlay), which needs its place.
        internal double Height(Element target)
            => !HasBox(target) ? 0 : IsOutOfFlow(target, cascade) ? Place(target).Height : HeightOf(target);

        // CSS2 §10.1: ordinary inline/contents ancestors do not establish the normal-flow
        // containing block. Its available width is the same synthetic (including flex-assigned)
        // width used by rectangles, never an authored width or a viewport fallback.
        internal double? ContainingInlineWidth(Element target)
        {
            token.ThrowIfCancellationRequested();
            checkpoint?.Invoke();
            if (!HasBox(target)) return null;
            if (ReferenceEquals(target, document?.DocumentElement)) return viewportWidth;
            for (var parent = target.ParentNode as Element; parent is not null; parent = parent.ParentNode as Element)
            {
                Step();
                var display = cascade?.Of(parent).GetPropertyValue("display");
                if (display is "inline" or "contents") continue;
                if (display is null or "none" || parent.NamespaceUri != Namespaces.Html) return null;
                return HasBox(parent) ? WidthOf(parent) : null;
            }
            return null;
        }

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
                    var widths = FlexRow.Widths(children, width, cascade, child => ContentBasis(child, width, 0), Step, token);
                    for (var i = 0; i < children.Length; i++)
                    {
                        Step();
                        _widths.Add(children[i], widths[i]);
                    }
                }
                else
                {
                    _widths.Add(element, PositionedWidth(element, width) ?? GridItemWidth(element, parent, width) ?? BlockWidth(element, width) ?? width);
                }
            }

            return _widths[target];
        }

        /// <summary>
        /// The width of an absolutely positioned box, which does not fill its containing block
        /// (https://www.w3.org/TR/CSS2/visudet.html#abs-non-replaced-width): its own width, or, when that is
        /// <c>auto</c> and its left or right offset is too, the shrink-to-fit width — its content's preferred
        /// width bounded by the available width. <see langword="null"/> for any other box, which is as wide as
        /// its parent. A positioned box's offsets are not applied: it stays at its parent's left edge.
        /// </summary>
        private double? PositionedWidth(Element element, double available)
        {
            if (!IsOutOfFlow(element, cascade) || cascade!.LayoutOf(element) is not { } style)
            {
                return null;
            }

            var authored = FlexRow.Length(CssCascade.ValueOf(style, "width"), available);
            if (authored is null)
            {
                if (CssCascade.ValueOf(style, "left") is not (null or "" or "auto")
                    && CssCascade.ValueOf(style, "right") is not (null or "" or "auto"))
                {
                    return null;
                }

                authored = Math.Min(available, PreferredWidth(element, available, 0));
            }

            var width = authored.Value;
            if (FlexRow.Length(CssCascade.ValueOf(style, "max-width"), available) is { } maximum) width = Math.Min(width, maximum);
            if (FlexRow.Length(CssCascade.ValueOf(style, "min-width"), available) is { } minimum) width = Math.Max(width, minimum);
            return width;
        }

        /// <summary>
        /// The width of a grid item (https://drafts.csswg.org/css-grid-2/#layout-algorithm, for column tracks
        /// it can size without content): the tracks it spans and the gaps between them, from the first column —
        /// items are not placed side by side, each keeps its row, but a dashboard widget is as wide as its cells.
        /// <see langword="null"/> outside a grid, or for a track list that needs content to size.
        /// </summary>
        private double? GridItemWidth(Element element, Element parent, double available)
        {
            if (cascade?.LayoutOf(parent) is not { } container
                || !Dom.Views.ResolvedValueText.IsGridContainer(container)
                || cascade.LayoutOf(element) is not { } style)
            {
                return null;
            }

            // Absolute lengths, as getComputedStyle reads them: a gap of 1rem is 16px.
            var viewport = ViewportOf();
            var columns = Dom.Views.ResolvedLengths.Resolve("grid-template-columns", container.GetNormalizedProperty("grid-template-columns"), container, viewport);
            var columnGap = Dom.Views.ResolvedLengths.Resolve("column-gap", container.GetNormalizedProperty("column-gap"), container, viewport);
            if (Dom.Views.ResolvedValueText.ColumnTracks(columns, available, columnGap, out var gap) is not { } tracks)
            {
                return null;
            }

            // A shorthand with a var() in it leaves its longhands pending; the shorthand has the substituted value.
            var span = Math.Max(Span(CssCascade.ValueOf(style, "grid-column-start")), Span(CssCascade.ValueOf(style, "grid-column-end")));
            if (span == 1 && CssCascade.ValueOf(style, "grid-column") is { } shorthand)
            {
                span = Span(shorthand.Split('/')[0].Trim());
            }

            span = Math.Min(span, tracks.Length);
            var width = gap * (span - 1);
            for (var i = 0; i < span; i++)
            {
                width += tracks[i];
            }

            return Math.Min(width, available);

            // "span 2" spans two tracks; a line number or auto spans one.
            static int Span(string? value)
                => value is not null && value.StartsWith("span ", StringComparison.Ordinal)
                    && int.TryParse(value.AsSpan(5).Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
                    && count > 0 ? count : 1;
        }

        /// <summary>
        /// The width of a block-level or inline-block box in normal flow
        /// (https://www.w3.org/TR/CSS2/visudet.html#blockwidth): its own width, else its containing block's, clamped
        /// by its min-width and max-width — a limited-width form row is a quarter of the form. Its horizontal margins
        /// are not applied: it stays at its parent's left edge. <see langword="null"/> for a box that fills its
        /// parent, and for an inline box, which width does not apply to.
        /// </summary>
        private double? BlockWidth(Element element, double available)
        {
            if (cascade?.LayoutOf(element) is not { } style
                || CssCascade.ValueOf(style, "display") is not ("block" or "flow-root" or "list-item" or "flex" or "grid"
                    or "inline-block" or "inline-flex" or "inline-grid"))
            {
                return null;
            }

            var viewport = ViewportOf();
            double? Resolved(string name)
                => FlexRow.Length(ResolvedLengths.Resolve(name, style.GetNormalizedProperty(name), style, viewport), available);

            var authored = Resolved("width");
            var maximum = Resolved("max-width");
            var minimum = Resolved("min-width");
            if (authored is null && maximum is null && minimum is null)
            {
                return null;
            }

            var width = authored ?? available;
            if (maximum is { } max) width = Math.Min(width, max);
            if (minimum is { } min) width = Math.Max(width, min);
            return width;
        }

        /// <summary>
        /// The preferred (max-content) width of a box's content without text measurement: its own authored
        /// width, a horizontal flex row's items side by side, else its widest in-flow child; one row for text.
        /// </summary>
        private double PreferredWidth(Element element, double available, int depth)
        {
            var style = cascade?.LayoutOf(element);
            if (style is not null && FlexRow.Length(CssCascade.ValueOf(style, "width"), available) is { } authored)
            {
                return authored;
            }

            if (depth >= 8)
            {
                return RowHeight;
            }

            if (FlexRow.IsHorizontal(element, cascade))
            {
                return ContentBasis(element, available, depth);
            }

            var widest = 0d;
            foreach (var child in Children(element))
            {
                Step();

                // An absolutely positioned child is out of flow, and does not widen its parent.
                if (HasBox(child) && !IsOutOfFlow(child, cascade))
                {
                    widest = Math.Max(widest, PreferredWidth(child, available, depth + 1));
                }
            }

            return widest > 0 ? widest : RowHeight;
        }

        /// <summary>
        /// The content size a flex item with an automatic basis starts from (css-flexbox §9.2 step 3.E, max-content
        /// without text measurement): a horizontal flex row is as wide as its rendered items' base sizes together,
        /// so a tab strip or a toolbar holds its tabs; anything else is one row.
        /// </summary>
        private double ContentBasis(Element element, double available, int depth)
        {
            if (depth >= 8 || !FlexRow.IsHorizontal(element, cascade))
            {
                return RowHeight;
            }

            var sum = 0d;
            foreach (var child in Children(element))
            {
                Step();
                if (HasBox(child))
                {
                    sum += FlexRow.BaseSize(child, available, cascade, nested => ContentBasis(nested, available, depth + 1));
                }
            }

            return Math.Max(RowHeight, sum);
        }

        private double HeightOf(Element target)
        {
            var ancestors = new Stack<Element>();
            var element = target;
            while (!_heights.ContainsKey(element))
            {
                Step();
                if ((element.ParentNode as Element) is not { } parent || !SizedByParent(element, parent))
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
                var parent = (element.ParentNode as Element)!;
                _heights.Add(element, FlexRow.IsHorizontal(parent, cascade)
                    ? _heights[parent] - RowHeight
                    : Grown(element, parent, _heights[parent]));
            }

            return _heights[target];
        }

        // A horizontal flex row stretches its items to its height; a column grows the items that flex-grow.
        private bool SizedByParent(Element element, Element parent)
            => FlexRow.IsHorizontal(parent, cascade)
                ? FlexRow.Alignment(element, parent, cascade) == "stretch"
                : FlexRow.IsColumn(parent, cascade) && FlexRow.Grow(element, cascade) > 0;

        /// <summary>
        /// https://drafts.csswg.org/css-flexbox/#resolve-flexible-lengths, along a column: the height of a
        /// container left over by its items' rows is shared between the items that grow — a designer's canvas
        /// fills the column a full-height layout gives it. An item is never shorter than its rows.
        /// </summary>
        private double Grown(Element element, Element parent, double parentHeight)
        {
            var own = CountRows(element) * RowHeight;
            var content = 0d;
            var growth = 0d;
            foreach (var child in Children(parent))
            {
                Step();
                if (HasBox(child))
                {
                    content += CountRows(child) * RowHeight;
                    growth += FlexRow.Grow(child, cascade);
                }
            }

            var free = parentHeight - RowHeight - content;
            return free > 0 && growth > 0 ? own + (free * FlexRow.Grow(element, cascade) / Math.Max(1, growth)) : own;
        }

        /// <summary>
        /// The rows a column flex container's own pixel height covers — <c>height: calc(100vh - 5rem)</c> on a
        /// full-height layout — which its growing items then share. Only a column: its items' heights come from
        /// it, while a block sized from its content, an editor's, would feed back into the content sizing itself.
        /// </summary>
        private int DefiniteRows(Element element)
        {
            if (!FlexRow.IsColumn(element, cascade) || cascade?.LayoutOf(element) is not { } style)
            {
                return 0;
            }

            var height = ResolvedLengths.Resolve("height", style.GetNormalizedProperty("height"), style, ViewportOf());
            return height.EndsWith("px", StringComparison.Ordinal) && FlexRow.Length(height, 0) is { } pixels
                ? (int) Math.Min(int.MaxValue / 2, Math.Ceiling(pixels / RowHeight))
                : 0;
        }

        /// <summary>The page's viewport, which vw, vh and rem lengths resolve against.</summary>
        private Viewport ViewportOf()
            => document is not null && Styling.NativeCssStyleSheets.RealmOf(document) is { } host
                && Runtime.PageRuntime.FindBrowsingContext(host.Engine, document) is { } runtime
                    ? runtime.Viewport
                    : Viewport.Default with { Width = (int) viewportWidth };

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
                if (TryRows(element, out var known))
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
                    if (frame.Next == Children(frame.Element).Count)
                    {
                        rows = Math.Max(rows, DefiniteRows(frame.Element));
                    }

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
                if (TryRows(element, out _))
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

                    _rows.Add(element, Math.Max(rows + 1, DefiniteRows(element)));
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

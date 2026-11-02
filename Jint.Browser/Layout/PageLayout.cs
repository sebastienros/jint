using Jint.HtmlParser;
using Jint.Browser.Accessibility;
using Jint.Browser.Dom.Views;
using Jint.Browser.Events;
using Jint.Browser.Runtime;
using Jint.Runtime;

namespace Jint.Browser.Layout;

/// <summary>
/// One document's layout state: how far it is scrolled, and the boxes that follow from that.
/// </summary>
/// <remarks>
/// <para>
/// <b>The scroll is virtual.</b> Alongside the document's invalidated measurements, scrolling is a
/// number the page keeps and every viewport-relative answer subtracts. <c>window.scrollTo</c>,
/// <c>scrollBy</c>, <c>element.scrollIntoView</c>, <c>DOM.scrollIntoViewIfNeeded</c> and Playwright's own
/// scroll path all set it; <c>window.scrollY</c>, <c>pageYOffset</c> and
/// <c>document.scrollingElement.scrollTop</c> read it. That is what lets a client whose click path insists
/// on "scroll it into view, then check the box is inside the viewport" — Playwright's does — succeed on a
/// document taller than its window.
/// </para>
/// <para>
/// <b>Horizontal scrolling does not exist.</b> Flex rows partition the containing width, but horizontal
/// overflow still has no scroll range; <c>scrollX</c> and <c>pageXOffset</c> are zero and stay zero.
/// </para>
/// <para>
/// <b>One <c>scroll</c> event per turn.</b> A change queues one job on the engine's own queue and further
/// changes before it runs join it, which is what a browser's "run the scroll steps once per rendering
/// update" amounts to here. It is fired at the document, and bubbles, so a <c>window.onscroll</c> listener
/// hears it through the document's own event path.
/// </para>
/// <para>
/// It belongs to the page runtime, so a navigation starts a document at the top with no bookkeeping.
/// </para>
/// </remarks>
internal sealed partial class PageLayout
{
    internal PageLayoutDiagnostics? Diagnostics { get; set; }
    private readonly PageRuntime _runtime;
    private readonly Action _scrollJob;

    private double _scrollY;
    private bool _scrollScheduled;

    internal PageLayout(PageRuntime runtime)
    {
        _runtime = runtime;
        _scrollJob = FireScroll;
    }

    /// <summary>
    /// What "not rendered" means, kept for the page because the cascade probe inside it latches.
    /// </summary>
    internal ElementVisibility Visibility { get; } = new(useComputedStyle: true);

    /// <summary>How far the page is scrolled down, in CSS pixels.</summary>
    internal double ScrollY => _scrollY;

    /// <summary>Shares current measurements where all writers are tracked; otherwise starts a fresh query.</summary>
    internal FlatLayout.SizeQuery MeasureSizes()
    {
        Diagnostics?.SizeQueryRequested();
        if (!CanReuse())
        {
            return CreateUnretainedSizes();
        }

        return _sizes ??= CreateSizes();
    }

    // Measurements that cannot be retained are walked afresh on every read. The styles they consult need not
    // be: the document's shared traversal validates itself against every native input, exactly as
    // getComputedStyle relies on, so consecutive reads stop re-matching every element that precedes the target.
    private FlatLayout.SizeQuery CreateUnretainedSizes()
    {
        var document = _runtime.Document;
        var traversal = document is not null && Visibility.CascadeAvailable ? CssCascade.Traversal.Current(document) : null;
        return new(document, Visibility, _runtime.Viewport.Width, traversal, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);
    }

    private FlatLayout.SizeQuery CreateSizes()
    {
        var traversal = _runtime.Document is { } document && Visibility.CascadeAvailable
            ? CssCascade.Traversal.Current(document) : null;
        return traversal?.ReadContext is { } context ? RetainedSizes(context)
            : new(_runtime.Document, Visibility, _runtime.Viewport.Width, traversal, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);
    }

    /// <summary>A single rectangle using the same placement and scroll clamp as a complete layout.</summary>
    internal FlatBox? ClientBoxOf(Element element)
    {
        var sizes = MeasureSizes();
        FlatBox? box = sizes.HasBox(element) ? sizes.Place(element) : null;
        // Zero is already inside every scroll range. Only a positive offset can need clamping
        // after a document shrinks. A placed box also proves that the document reaches its bottom:
        // the flat model nests every box inside the root, including horizontally arranged children.
        // Otherwise reuse the rows measured for placement while establishing the remaining extent.
        if (_scrollY > 0 && (box is null || box.Value.Bottom < _scrollY + _runtime.Viewport.Height))
        {
            var height = _runtime.Document?.DocumentElement is { } root
                ? sizes.HeightUpTo(root, _scrollY + _runtime.Viewport.Height) : 0;
            _scrollY = Math.Min(_scrollY, Math.Max(0, height - _runtime.Viewport.Height));
        }
        return box is { } placed ? placed with { Y = placed.Y - _scrollY } : null;
    }

    /// <summary>Fresh point hit testing without constructing unrelated document boxes.</summary>
    internal FlatLayout.PointQuery ReadPoint()
    {
        Diagnostics?.PointQueryRequested();
        var sizes = MeasureSizes();
        if (_scrollY > 0) _scrollY = Math.Min(_scrollY, ScrollMaximum(sizes, _scrollY));
        var viewport = _runtime.Viewport;
        return new(sizes, viewport.Width, viewport.Height, _scrollY);
    }

    /// <summary>The complete layout, including boxes not needed by a single hit test.</summary>
    internal FlatLayout Current()
    {
        var reuse = CanReuse();
        if (reuse && _layout is { } cached && cached.ScrollY == _scrollY)
        {
            return cached;
        }

        var viewport = _runtime.Viewport;
        var sizes = MeasureSizes();
        Diagnostics?.FullLayoutBuilt();
        var layout = FlatLayout.Of(_runtime.Document, Visibility, viewport.Width, viewport.Height, _scrollY, sizes, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);

        // A document that shrank under a scrolled page leaves the offset past its end, so the clamp is read
        // here rather than only written in ScrollTo: what a box answers must agree with what scrollY reads.
        var clamped = Math.Min(_scrollY, layout.MaxScrollY);
        if (clamped == _scrollY)
        {
            if (reuse)
            {
                _layout = layout;
            }
            return layout;
        }

        _scrollY = clamped;
        Diagnostics?.FullLayoutBuilt();
        layout = FlatLayout.Of(_runtime.Document, Visibility, viewport.Width, viewport.Height, clamped, sizes, _runtime.Engine.Constraints.Check, _runtime.Dom.CancellationToken);
        if (reuse)
        {
            _layout = layout;
        }
        return layout;
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-window-scroll — scrolls to <paramref name="y"/>, clamped.
    /// </summary>
    internal void ScrollTo(double y)
        => ScrollTo(y, ScrollMaximum(MeasureSizes(), y));

    private double ScrollMaximum(FlatLayout.SizeQuery sizes, double requested)
    {
        if (double.IsNaN(requested) || requested <= 0) return 0;
        // A bounded height is sufficient: either it proves the requested offset fits, or it is
        // the exact shorter extent. No unrelated element positions or hit-test boxes are needed.
        var viewport = _runtime.Viewport.Height;
        var height = _runtime.Document?.DocumentElement is { } root
            ? sizes.HeightUpTo(root, requested + viewport) : 0;
        return Math.Max(0, height - viewport);
    }

    private void ScrollTo(double y, double maximum)
    {
        var target = double.IsNaN(y) ? 0 : Math.Clamp(y, 0, maximum);
        if (target == _scrollY)
        {
            return;
        }

        _scrollY = target;
        ScheduleScrollEvent();
    }

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-window-scrollby — moves by <paramref name="delta"/>.</summary>
    internal void ScrollBy(double delta) => ScrollTo(double.IsNaN(delta) ? _scrollY : _scrollY + delta);

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-element-scrollintoview — brings
    /// <paramref name="element"/>'s bounding box into the viewport.
    /// </summary>
    /// <param name="element">The element to reveal.</param>
    /// <param name="block">
    /// The <c>block</c> alignment: <c>start</c>, <c>center</c>, <c>end</c> or <c>nearest</c>. Anything else
    /// is <c>start</c>, which is what the enumeration's own default is for the argument-less call.
    /// </param>
    /// <remarks>
    /// The alignment uses the same bounding box as client rectangles and hit testing. Revealing only the
    /// first row can leave every descendant outside the viewport, so a client clicks the container's empty
    /// row instead of its contents. With <c>nearest</c>, a box spanning both viewport edges stays put.
    /// </remarks>
    internal void ScrollIntoView(Element element, string block)
    {
        var sizes = MeasureSizes();
        _scrollY = Math.Min(_scrollY, ScrollMaximum(sizes, _scrollY));
        if (!sizes.HasBox(element))
        {
            return;
        }

        var box = sizes.Place(element);
        var top = box.Y;
        var bottom = box.Bottom;
        var height = _runtime.Viewport.Height;

        switch (block)
        {
            case "center":
                ScrollTo(box.CenterY - (height / 2), ScrollMaximum(sizes, box.CenterY - (height / 2)));
                return;

            case "end":
                ScrollTo(bottom - height, ScrollMaximum(sizes, bottom - height));
                return;

            case "nearest":
                if (top < _scrollY && bottom > _scrollY + height)
                {
                    return;
                }

                if ((top < _scrollY && box.Height <= height)
                    || (bottom > _scrollY + height && box.Height > height))
                {
                    ScrollTo(top, ScrollMaximum(sizes, top));
                }
                else if ((bottom > _scrollY + height && box.Height <= height)
                    || (top < _scrollY && box.Height > height))
                {
                    ScrollTo(bottom - height, ScrollMaximum(sizes, bottom - height));
                }

                return;

            default:
                ScrollTo(top, ScrollMaximum(sizes, top));
                return;
        }
    }

    private void ScheduleScrollEvent()
    {
        if (_scrollScheduled)
        {
            return;
        }

        _scrollScheduled = true;
        _runtime.Engine.AddToEventLoop(_scrollJob, EventLoopJobKind.Task);
    }

    private void FireScroll()
    {
        _scrollScheduled = false;

        if (_runtime.DocumentWrapper is { } document)
        {
            // https://drafts.csswg.org/cssom-view/#scrolling-events — fired at the document and bubbling, so
            // a window listener is on its path; DomNodeObject.GetParent is what puts the window there.
            ActivationBehaviors.Fire(document, "scroll", bubbles: true, composed: false);
        }
    }
}

// Opt-in per-page counters for source regression tests, with no callbacks or ambient collector.
internal sealed class PageLayoutDiagnostics
{
    internal long PointQueries { get; private set; }
    internal long FullLayouts { get; private set; }
    internal long SizeQueryRequests { get; private set; }
    internal long RetainedQueries { get; private set; }
    internal long RetainedRowHits { get; private set; }
    internal Dictionary<string, long> MeasurementDiscardReasons { get; } = new(StringComparer.Ordinal);
    internal void SizeQueryRequested() => SizeQueryRequests++;
    internal void PointQueryRequested() => PointQueries++;
    internal void FullLayoutBuilt() => FullLayouts++;
    internal void MeasurementsRetained() => RetainedQueries++;
    internal void RetainedRowUsed() => RetainedRowHits++;
    internal void MeasurementsDiscarded(string reason)
        => MeasurementDiscardReasons[reason] = MeasurementDiscardReasons.GetValueOrDefault(reason) + 1;
}

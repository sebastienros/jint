using AngleSharp.Dom;
using Jint.Browser.Accessibility;
using Jint.Browser.Events;
using Jint.Browser.Runtime;

namespace Jint.Browser.Layout;

/// <summary>
/// One document's layout state: how far it is scrolled, and the boxes that follow from that.
/// </summary>
/// <remarks>
/// <para>
/// <b>The scroll is virtual and it is the only state here.</b> There is nothing to paint, so scrolling is a
/// number the page keeps and every viewport-relative answer subtracts. <c>window.scrollTo</c>,
/// <c>scrollBy</c>, <c>element.scrollIntoView</c>, <c>DOM.scrollIntoViewIfNeeded</c> and Playwright's own
/// scroll path all set it; <c>window.scrollY</c>, <c>pageYOffset</c> and
/// <c>document.scrollingElement.scrollTop</c> read it. That is what lets a client whose click path insists
/// on "scroll it into view, then check the box is inside the viewport" — Playwright's does — succeed on a
/// document taller than its window.
/// </para>
/// <para>
/// <b>Horizontal scrolling does not exist.</b> Every box is exactly as wide as the viewport, so there is
/// never anything to the side; <c>scrollX</c> and <c>pageXOffset</c> are zero and stay zero.
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
internal sealed class PageLayout
{
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
    /// <remarks>
    /// <see cref="ElementVisibility"/> asks AngleSharp.Css once and stays on the inline-style path if the
    /// service is not registered, which is a decision it can only make once per document rather than once
    /// per query.
    /// </remarks>
    internal ElementVisibility Visibility { get; } = new(useComputedStyle: true);

    /// <summary>How far the page is scrolled down, in CSS pixels.</summary>
    internal double ScrollY => _scrollY;

    /// <summary>The layout of the document as it stands, with the current viewport and scroll offset.</summary>
    internal FlatLayout Current()
    {
        var viewport = _runtime.Viewport;
        var layout = FlatLayout.Of(_runtime.Document, Visibility, viewport.Width, viewport.Height, _scrollY);

        // A document that shrank under a scrolled page leaves the offset past its end, so the clamp is read
        // here rather than only written in ScrollTo: what a box answers must agree with what scrollY reads.
        var clamped = Math.Min(_scrollY, layout.MaxScrollY);
        if (clamped == _scrollY)
        {
            return layout;
        }

        _scrollY = clamped;
        return FlatLayout.Of(_runtime.Document, Visibility, viewport.Width, viewport.Height, clamped);
    }

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#dom-window-scroll — scrolls to <paramref name="y"/>, clamped.
    /// </summary>
    internal void ScrollTo(double y)
    {
        var target = double.IsNaN(y) ? 0 : Math.Clamp(y, 0, Current().MaxScrollY);
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
    /// <paramref name="element"/>'s first row into the viewport.
    /// </summary>
    /// <param name="element">The element to reveal.</param>
    /// <param name="block">
    /// The <c>block</c> alignment: <c>start</c>, <c>center</c>, <c>end</c> or <c>nearest</c>. Anything else
    /// is <c>start</c>, which is what the enumeration's own default is for the argument-less call.
    /// </param>
    /// <remarks>
    /// <b>Only the element's first row is aligned, never its whole box.</b> A container's box spans its
    /// subtree, so aligning the box of a page's <c>&lt;body&gt;</c> to the centre of the window would scroll
    /// past everything in it; the first row is the element itself, which is the part a client asked to see.
    /// <c>nearest</c> scrolls only when the row is outside the viewport, which is what makes it the
    /// alignment a client's "scroll into view if needed" wants.
    /// </remarks>
    internal void ScrollIntoView(IElement element, string block)
    {
        var layout = Current();
        if (layout.DocumentBoxOf(element) is not { } box)
        {
            return;
        }

        var top = box.Y;
        var bottom = top + FlatLayout.RowHeight;
        var height = layout.ViewportHeight;

        switch (block)
        {
            case "center":
                ScrollTo(top - ((height - FlatLayout.RowHeight) / 2));
                return;

            case "end":
                ScrollTo(bottom - height);
                return;

            case "nearest":
                if (top < _scrollY)
                {
                    ScrollTo(top);
                }
                else if (bottom > _scrollY + height)
                {
                    ScrollTo(bottom - height);
                }

                return;

            default:
                ScrollTo(top);
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
        _runtime.Engine.AddToEventLoop(_scrollJob);
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

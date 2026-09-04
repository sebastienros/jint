using AngleSharp.Html.Dom;
using Jint.Browser.Events;

namespace Jint.Browser.Runtime;

/// <summary>
/// What an activation behaviour's default action becomes once there is a page behind it: a navigation, a form
/// submission, a file chooser the host is asked to answer.
/// </summary>
/// <remarks>
/// <para>
/// The events bridge decides <i>that</i> a link was followed and <i>which</i> form a submit button submits —
/// the half HTML specifies as an element's activation behaviour — and stops there, because everything after
/// it is a navigation and navigation is the runtime's. This class is the join. Without it the events bridge
/// still works and records what it was asked for (<see cref="BrowserActivationHost.Recording"/>), which is
/// what a binding-only engine with no page gets.
/// </para>
/// <para>
/// It is installed per page rather than per process because every seam it reaches — the page's navigation
/// queue, its error recorder — belongs to one page, and because a page that has been closed must stop
/// navigating rather than keep a static host alive.
/// </para>
/// </remarks>
internal sealed class PageActivationHost : BrowserActivationHost
{
    private readonly PageRuntime _runtime;

    internal PageActivationHost(PageRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/links.html#following-hyperlinks-2. A same-document fragment
    /// navigation goes through the same seam as any other: the page's navigation queue is what decides that a
    /// URL differing only in its fragment is a fragment navigation, fires <c>hashchange</c> and pushes a
    /// history entry without a fetch.
    /// </summary>
    /// <remarks>
    /// <c>target</c> is honoured only as far as this version can: there is no seam that opens a second page,
    /// so <c>_blank</c>, a frame name and <c>_top</c> all load here and the page is told rather than left to
    /// wonder — the same sentence a targeted form submission gets.
    /// </remarks>
    internal override void FollowHyperlink(BrowserEventRealm realm, IHtmlElement source, string url, string target)
    {
        if (url.Length == 0)
        {
            return;
        }

        if (target.Length != 0 && !string.Equals(target, "_self", StringComparison.OrdinalIgnoreCase))
        {
            _runtime.Recorder.Add(
                PageErrorKind.ReportedError,
                "A link targeting '" + target + "' was followed in the same page: this version opens no second "
                + "page, so _blank, a frame name and _top all load here.",
                source.LocalName);
        }

        _runtime.Page.RequestNavigation(
            url,
            replace: false,
            engine: _runtime.Engine,
            reason: PageNavigationReason.AnchorClick);
    }

    /// <summary>
    /// The submission's lower half — https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm
    /// from the entry list on. The <c>submit</c> event has already fired and survived by the time this runs.
    /// </summary>
    internal override void SubmitForm(BrowserEventRealm realm, IHtmlFormElement form, IHtmlElement? submitter)
        => FormSubmitter.Submit(_runtime, form, submitter);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#show-the-picker,-if-applicable for
    /// <c>&lt;input type=file&gt;</c>. A headless host has no picker, so the click is recorded as something a
    /// page asked for and nobody answered; the protocol's own file-chooser interception (campaign item C5)
    /// replaces the body without moving the seam.
    /// </summary>
    internal override void OpenFileChooser(BrowserEventRealm realm, IHtmlInputElement input)
        => _runtime.Recorder.Add(
            PageErrorKind.ReportedError,
            "A file chooser was opened by clicking an <input type=file>, and this version has no file chooser "
            + "to open; the file list is unchanged.",
            input.Id is { Length: > 0 } id ? id : input.Name ?? "input");
}

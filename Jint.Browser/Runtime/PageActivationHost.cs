using Jint.HtmlParser;
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
    internal override void FollowHyperlink(BrowserEventRealm realm, Element source, string url, string target)
    {
        if (url.Length == 0)
        {
            return;
        }

        target = WindowOpen.Target(_runtime, source, source.GetAttribute("target"));
        var (noopener, noreferrer) = WindowOpen.Relationship(source, target);
        if (WindowOpen.NavigateTarget(_runtime, target, noopener, WindowOpen.Navigation(_runtime, url, noreferrer))) return;

        _runtime.Page.RequestNavigation(
            url,
            replace: false,
            engine: _runtime.Engine,
            reason: PageNavigationReason.AnchorClick,
            sourceElement: _runtime.Dom.ExistingNavigation is null ? null : _runtime.Dom.WrapNode(source),
            downloadRequest: source.GetAttribute("download"),
            userInitiated: realm.ActivationIsUserInitiated,
            referrer: noreferrer ? "" : null);
    }

    /// <summary>
    /// The submission's lower half — https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm
    /// from the entry list on. The <c>submit</c> event has already fired and survived by the time this runs.
    /// </summary>
    internal override void SubmitForm(BrowserEventRealm realm, Element form, Element? submitter)
        => FormSubmitter.Submit(_runtime, form, submitter);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#show-the-picker,-if-applicable for
    /// <c>&lt;input type=file&gt;</c>. A headless host has no picker, so the click is recorded as something a
    /// page asked for and nobody answered; the protocol's own file-chooser interception (campaign item C5)
    /// replaces the body without moving the seam.
    /// </summary>
    internal override void OpenFileChooser(BrowserEventRealm realm, Element input)
        => _runtime.Recorder.Add(
            PageErrorKind.ReportedError,
            "A file chooser was opened by clicking an <input type=file>, and this version has no file chooser "
            + "to open; the file list is unchanged.",
            input.GetAttribute("id") is { Length: > 0 } id ? id : input.GetAttribute("name") ?? "input");
}

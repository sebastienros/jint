using AngleSharp.Html.Dom;

namespace Jint.Browser.Events;

/// <summary>
/// Where an activation behaviour's default action goes when it leaves the DOM — a hyperlink to follow, a form
/// to submit, a file chooser to open.
/// </summary>
/// <remarks>
/// <para>
/// The events bridge decides <i>that</i> a link was followed and <i>which</i> form a submit button submits,
/// which is the half HTML specifies as an element's activation behaviour. What happens next is a navigation,
/// and navigation belongs to the page runtime (campaign item R5): it owns the fetch, the history entry, the
/// engine replacement and the load events. This class is the join, so neither half has to know the other's
/// internals.
/// </para>
/// <para>
/// The default is <see cref="Recording"/>, which carries nothing out and writes down what was asked for. That
/// is what makes a submit button testable — and correct — before the navigation layer exists: a page whose
/// form submits does not silently do nothing, it records a submission the host can read.
/// </para>
/// </remarks>
internal abstract class BrowserActivationHost
{
    /// <summary>The host that records rather than acts, and the default for an engine with no navigation layer.</summary>
    internal static readonly BrowserActivationHost Recording = new RecordingHost();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/links.html#following-hyperlinks-2 — <i>follow the hyperlink</i>
    /// for an <c>&lt;a href&gt;</c> or an <c>&lt;area href&gt;</c> whose click was not canceled.
    /// </summary>
    /// <param name="realm">The engine the link belongs to.</param>
    /// <param name="source">The element carrying the <c>href</c>.</param>
    /// <param name="url">The <c>href</c>, already resolved against the document's base URL by AngleSharp.</param>
    /// <param name="target">The <c>target</c> attribute's value, or the empty string.</param>
    internal abstract void FollowHyperlink(BrowserEventRealm realm, IHtmlElement source, string url, string target);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#concept-form-submit — submit a
    /// form, from a submit button's activation behaviour or from <c>form.requestSubmit()</c>. The
    /// <c>submit</c> event has already been fired and was not canceled by the time this is reached.
    /// </summary>
    /// <param name="realm">The engine the form belongs to.</param>
    /// <param name="form">The form to submit.</param>
    /// <param name="submitter">The button that started it, or <see langword="null"/> for the form itself.</param>
    internal abstract void SubmitForm(BrowserEventRealm realm, IHtmlFormElement form, IHtmlElement? submitter);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/input.html#show-the-picker,-if-applicable for
    /// <c>&lt;input type=file&gt;</c>: the point a headless host answers a file chooser, which is what CDP's
    /// <c>Page.setInterceptFileChooserDialog</c> needs (campaign item C5).
    /// </summary>
    /// <param name="realm">The engine the input belongs to.</param>
    /// <param name="input">The file input whose picker was asked for.</param>
    internal abstract void OpenFileChooser(BrowserEventRealm realm, IHtmlInputElement input);

    private sealed class RecordingHost : BrowserActivationHost
    {
        internal override void FollowHyperlink(BrowserEventRealm realm, IHtmlElement source, string url, string target)
            => realm.Record(new PendingActivation(PendingActivationKind.Navigation, url, target));

        internal override void SubmitForm(BrowserEventRealm realm, IHtmlFormElement form, IHtmlElement? submitter)
            => realm.Record(new PendingActivation(
                PendingActivationKind.FormSubmission,
                form.Action ?? "",
                submitter is null ? "" : NameOf(submitter)));

        internal override void OpenFileChooser(BrowserEventRealm realm, IHtmlInputElement input)
            => realm.Record(new PendingActivation(PendingActivationKind.FileChooser, input.Name ?? "", input.Id ?? ""));

        private static string NameOf(IHtmlElement submitter)
            => submitter.Id is { Length: > 0 } id ? id : submitter.GetAttribute("name") ?? submitter.LocalName;
    }
}

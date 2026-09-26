using Jint.HtmlParser;
using Jint.HtmlParser.Css.Syntax;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// The upper half of HTML's form submission and form reset: the constraint validation that can refuse a
/// submission, the <c>submit</c> and <c>reset</c> events, and the hand-off of whatever survives them.
/// <para>
/// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The split with <see cref="Runtime.FormSubmitter"/> is the one HTML itself draws. Everything up to and
/// including "if the event was canceled, return" is here, because it is events; everything after it — the
/// entry list with its <c>formdata</c> event, the encoding, the request, the navigation — is the runtime's,
/// reached through <see cref="BrowserActivationHost.SubmitForm"/>. That is also why the two halves can be
/// tested apart, and why a binding-only engine with no page still fires a correct <c>submit</c> event and
/// records that a submission was asked for.
/// </para>
/// <para>
/// <b>Order matters and is the specification's.</b> Interactive validation is step 4.5 and the <c>submit</c>
/// event is step 4.7, so a form whose constraints fail never fires <c>submit</c> at all —
/// <c>form.submit()</c> skips both, which is the whole of what distinguishes it from
/// <c>form.requestSubmit()</c> and from a submit button.
/// </para>
/// </remarks>
internal static class FormSubmission
{
    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#concept-form-submit — validate,
    /// fire the <c>submit</c> event and, if nothing refused it, hand the form to the runtime.
    /// </summary>
    /// <param name="realm">The binding realm the form belongs to.</param>
    /// <param name="form">The form owner, or <see langword="null"/> when the button has none — in which case
    /// nothing happens at all, which is what a submit button outside a form does.</param>
    /// <param name="submitter">The button that started it, or <see langword="null"/> for the form itself.</param>
    internal static void Submit(DomRealm realm, Element? form, Element? submitter)
    {
        if (form is null || IsConstructingEntryList(realm, form))
        {
            return;
        }

        if (!Validate(realm, form, submitter))
        {
            return;
        }

        var target = realm.WrapNode(form);
        var eventRealm = BrowserEventRealm.Of(realm.Engine, target.DomRealm.OwningRealm);

        var submitEvent = eventRealm.CreateTrusted(
            BrowserEventInterfaces.SubmitEvent,
            new JsSubmitEvent(
                realm.Engine,
                JsString.Create("submit"),
                new EventInit(Bubbles: true, Cancelable: true, Composed: false),
                eventRealm.TimeStamp,
                submitter is null ? JsValue.Null : realm.WrapNode(submitter)));

        if (!target.DispatchEvent(submitEvent))
        {
            return;
        }

        SubmitWithoutEvent(realm, form, submitter);
    }

    /// <summary>
    /// The lower half on its own: <c>form.submit()</c> submits without validating and without firing
    /// <c>submit</c> at all.
    /// </summary>
    internal static void SubmitWithoutEvent(DomRealm realm, Element form, Element? submitter)
    {
        var eventRealm = BrowserEventRealm.Of(realm.Engine);
        eventRealm.ActivationHost.SubmitForm(eventRealm, form, submitter);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#dom-form-requestsubmit — submit
    /// as if <paramref name="submitterValue"/> had been clicked, validating first that it really is a submit
    /// button of this form.
    /// </summary>
    internal static void RequestSubmit(DomRealm realm, Element form, JsValue submitterValue)
    {
        if (submitterValue.IsNullOrUndefined())
        {
            Submit(realm, form, submitter: null);
            return;
        }

        if (submitterValue is not DomNodeObject { Node: Element candidate } || !IsSubmitButton(candidate))
        {
            Throw.TypeError(realm.OwningRealm, "Failed to execute 'requestSubmit' on 'HTMLFormElement': The specified element is not a submit button.");
            return;
        }

        if (!ReferenceEquals(HtmlFormOwner.Of(candidate), form))
        {
            // A NotFoundError DOMException, which is what the standard says and what a browser raises; the
            // wrong-kind refusal above is the TypeError, and the two are different on purpose.
            var exception = realm.OwningRealm.Intrinsics.DomException.CreateException(
                DomExceptionNames.NotFound,
                "Failed to execute 'requestSubmit' on 'HTMLFormElement': The specified element is not owned by this form element.");

            var location = realm.Engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(realm.Engine, exception, in location);
            return;
        }

        Submit(realm, form, candidate);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#concept-form-reset — fire the
    /// cancelable <c>reset</c> event and, if it survives, run the reset algorithm on every control.
    /// </summary>
    internal static void Reset(DomRealm realm, Element? form)
    {
        using var mutation = realm.MutateLayout();
        if (form is null)
        {
            return;
        }

        var target = realm.WrapNode(form);
        var events = target.DomRealm.OwningRealm.Intrinsics.Event;

        var resetEvent = events.CreateTrustedEvent(
            JsString.Create("reset"),
            new EventInit(Bubbles: true, Cancelable: true, Composed: false));

        if (target.DispatchEvent(resetEvent))
        {
            BrowserFormReset.Reset(realm, form);
            Dom.Files.FileTransferRealm.Of(realm.Engine).ResetForm(form);
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#interactively-validate-the-constraints:
    /// collect the controls that do not satisfy their constraints, fire <c>invalid</c> at each, and refuse the
    /// submission when there was one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>novalidate</c> on the form and <c>formnovalidate</c> on the submitter both skip the whole step, which
    /// is the half a page's behaviour most often depends on.
    /// </para>
    /// <para>
    /// The <c>invalid</c> events are fired at every failing control before the refusal, not instead of it: the
    /// algorithm's step is "fire an event named <c>invalid</c> at each", and a page listens to them to render
    /// its own messages. They are cancelable, and cancelling one changes nothing — HTML uses the canceled flag
    /// only to decide whether the user agent reports the problem itself, and there is nothing here to report
    /// with.
    /// </para>
    /// </remarks>
    private static bool Validate(DomRealm realm, Element form, Element? submitter)
    {
        if (form.HasContentAttribute("novalidate") || submitter?.HasContentAttribute("formnovalidate") == true)
        {
            return true;
        }

        List<Element>? invalid = null;

        // https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#statically-validate-the-constraints:
        // "let controls be a list of all the submittable elements whose form owner is form, in tree order" —
        // form ownership, so a control associated into this form by its `form` attribute is validated here and
        // one associated away from it is not. Reading `form.elements` instead would validate whatever
        // AngleSharp's own ownership rule put in it, and then submit a different set.
        foreach (var element in HtmlFormOwner.ControlsOf(form, realm.NativeReadCheckpoint, realm.CancellationToken))
        {
            if (!BrowserControlValidation.WillValidate(realm, element)
                || BrowserControlValidation.Read(realm, element).IsValid)
            {
                continue;
            }

            (invalid ??= []).Add(element);
        }

        if (invalid is null)
        {
            return true;
        }

        foreach (var control in invalid)
        {
            var ev = realm.Engine._mainRealm.Intrinsics.Event.CreateTrustedEvent(
                JsString.Create("invalid"),
                new EventInit(Bubbles: false, Cancelable: true, Composed: false));

            realm.WrapNode(control).DispatchEvent(ev);
        }

        return false;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-the-form-data-set
    /// step 1's flag, read as the submission algorithm's own step 1: a <c>formdata</c> listener that submits
    /// the same form again must not recurse. The runtime owns the flag because it owns the entry list.
    /// </summary>
    private static bool IsConstructingEntryList(DomRealm realm, Element form)
        => PageRuntime.Find(realm.Engine)?.SubmittingForms.Contains(form) == true;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#concept-submit-button — a button or input whose type
    /// makes it submit its form.
    /// </summary>
    internal static bool IsSubmitButton(Element element)
        => element.NamespaceUri == Namespaces.Html && element.LocalName switch
        {
            "button" => IsSubmitButtonType(element),
            "input" => EventDom.InputType(element) is "submit" or "image",
            _ => false,
        };

    // HTML's missing/invalid button type is Auto, including its command and select-child exclusions.
    private static bool IsSubmitButtonType(Element button)
    {
        var type = button.GetAttributeNodeNS(null, "type")?.Value ?? string.Empty;
        if (CssAscii.EqualsIgnoreCase(type, "submit")) return true;
        if (CssAscii.EqualsIgnoreCase(type, "reset") || CssAscii.EqualsIgnoreCase(type, "button")) return false;
        return !button.HasContentAttribute("command") && !button.HasContentAttribute("commandfor")
            && button.ParentNode is not Element { NamespaceUri: Namespaces.Html, LocalName: "select" };
    }

}

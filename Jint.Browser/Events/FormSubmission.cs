using AngleSharp.Dom;
using AngleSharp.Html.Dom;
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
    internal static void Submit(DomRealm realm, IHtmlFormElement? form, IHtmlElement? submitter)
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
        var eventRealm = BrowserEventRealm.Of(realm.Engine);

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
    internal static void SubmitWithoutEvent(DomRealm realm, IHtmlFormElement form, IHtmlElement? submitter)
    {
        var eventRealm = BrowserEventRealm.Of(realm.Engine);
        eventRealm.ActivationHost.SubmitForm(eventRealm, form, submitter);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#dom-form-requestsubmit — submit
    /// as if <paramref name="submitterValue"/> had been clicked, validating first that it really is a submit
    /// button of this form.
    /// </summary>
    internal static void RequestSubmit(DomRealm realm, IHtmlFormElement form, JsValue submitterValue)
    {
        if (submitterValue.IsNullOrUndefined())
        {
            Submit(realm, form, submitter: null);
            return;
        }

        if (submitterValue is not DomNodeObject { Node: IHtmlElement candidate } || !IsSubmitButton(candidate))
        {
            Throw.TypeError(realm.PrincipalRealm, "Failed to execute 'requestSubmit' on 'HTMLFormElement': The specified element is not a submit button.");
            return;
        }

        if (!ReferenceEquals(FormOwnerOf(candidate), form))
        {
            // A NotFoundError DOMException, which is what the standard says and what a browser raises; the
            // wrong-kind refusal above is the TypeError, and the two are different on purpose.
            var exception = realm.PrincipalRealm.Intrinsics.DomException.CreateException(
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
    /// <remarks>
    /// The default action is AngleSharp's <c>IHtmlFormElement.Reset()</c>, which is the reset algorithm and
    /// nothing else: it restores every control's value and checkedness to its default and fires nothing, so it
    /// is exactly the half this one is missing.
    /// </remarks>
    internal static void Reset(DomRealm realm, IHtmlFormElement? form)
    {
        if (form is null)
        {
            return;
        }

        var target = realm.WrapNode(form);
        var events = realm.Engine._mainRealm.Intrinsics.Event;

        var resetEvent = events.CreateTrustedEvent(
            JsString.Create("reset"),
            new EventInit(Bubbles: true, Cancelable: true, Composed: false));

        if (target.DispatchEvent(resetEvent))
        {
            form.Reset();
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
    private static bool Validate(DomRealm realm, IHtmlFormElement form, IHtmlElement? submitter)
    {
        if (form.HasAttribute("novalidate") || submitter?.HasAttribute("formnovalidate") == true)
        {
            return true;
        }

        List<IElement>? invalid = null;

        foreach (var element in form.Elements)
        {
            if (element is not IValidation validation)
            {
                continue;
            }

            try
            {
                // https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#candidate-for-constraint-validation:
                // a button, a disabled or readonly control, a control inside a disabled fieldset and an
                // output are all barred from constraint validation, and `willValidate` is the one member that
                // answers all of those at once. Without it every `<button type=button>` in the form would be
                // examined, which is not what "the form's constraints" means.
                if (!validation.WillValidate || validation.Validity.IsValid)
                {
                    continue;
                }
            }
            catch (Exception)
            {
                // A validity model that cannot answer is not a reason to refuse a submission the page asked
                // for; AngleSharp's raises for a control whose type it does not fully model.
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
    private static bool IsConstructingEntryList(DomRealm realm, IHtmlFormElement form)
        => PageRuntime.Find(realm.Engine)?.SubmittingForms.Contains(form) == true;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/forms.html#concept-submit-button — a button or input whose type
    /// makes it submit its form.
    /// </summary>
    internal static bool IsSubmitButton(IHtmlElement element) => element switch
    {
        IHtmlButtonElement button => string.Equals(button.Type, "submit", StringComparison.Ordinal),
        IHtmlInputElement input => input.Type is "submit" or "image",
        _ => false,
    };

    private static IHtmlFormElement? FormOwnerOf(IHtmlElement element) => element switch
    {
        IHtmlButtonElement button => button.Form,
        IHtmlInputElement input => input.Form,
        _ => null,
    };
}

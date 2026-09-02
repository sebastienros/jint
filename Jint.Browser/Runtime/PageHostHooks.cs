using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.DomException;

namespace Jint.Browser.Runtime;

/// <summary>
/// The runtime's half of the DOM binding: the members whose behaviour is a navigation, and so exist only
/// once a page does.
/// </summary>
/// <remarks>
/// <para>
/// Today that is form submission. <c>form.submit()</c> is not generated because AngleSharp's
/// <c>IHtmlFormElement.Submit()</c> returns a <c>Task</c> — a recorded generator skip — and
/// <c>form.requestSubmit()</c> is not generated because AngleSharp has no such member at all. Both would be
/// wrong even if they were: AngleSharp's own submission builds its own entry list, dispatches into its own
/// event bus and navigates through <c>IBrowsingContext</c> on the calling thread, so a page whose
/// <c>submit</c> listener called <c>preventDefault()</c> would be navigated anyway.
/// </para>
/// <para>
/// They are own properties of the form wrapper rather than members of <c>HTMLFormElement.prototype</c>,
/// which is what keeps that prototype shaped and its inline caching intact. The visible consequence is that
/// <c>form.hasOwnProperty('submit')</c> answers <see langword="true"/> where a browser answers
/// <see langword="false"/>, and that deleting <c>form.submit</c> reveals nothing underneath.
/// </para>
/// </remarks>
internal sealed class PageHostHooks : DomHostHooks
{
    internal static readonly PageHostHooks Instance = new();

    /// <inheritdoc />
    internal override void WrapperCreated(DomRealm realm, object target, ObjectInstance wrapper)
    {
        if (target is not IHtmlFormElement)
        {
            return;
        }

        var engine = realm.Engine;

        // https://html.spec.whatwg.org/multipage/forms.html#dom-form-submit: submit() skips the submit event
        // and the interactive validation entirely, which is exactly what distinguishes it from a button.
        wrapper.DefineOwnPropertyUnchecked(
            "submit",
            new PropertyDescriptor(
                new ClrFunction(engine, "submit", static (thisObject, _) => Submit(thisObject, "submit", fireEvent: false, submitter: null)),
                PropertyFlag.NonEnumerable));

        // https://html.spec.whatwg.org/multipage/forms.html#dom-form-requestsubmit: the one that behaves like
        // a button — the submit event fires and a listener can stop it.
        wrapper.DefineOwnPropertyUnchecked(
            "requestSubmit",
            new PropertyDescriptor(
                new ClrFunction(engine, "requestSubmit", static (thisObject, arguments) =>
                {
                    var submitter = Submitter(thisObject, arguments.At(0));
                    return Submit(thisObject, "requestSubmit", fireEvent: true, submitter);
                }, 1),
                PropertyFlag.NonEnumerable));
    }

    private static JsValue Submit(JsValue thisObject, string member, bool fireEvent, IElement? submitter)
    {
        var runtime = PageRuntime.Of(thisObject, member);

        if (thisObject is not IDomWrapper { DomTarget: IHtmlFormElement form })
        {
            Throw.TypeError(runtime.Engine.Realm, "Failed to execute '" + member + "' on 'HTMLFormElement': Illegal invocation");
            return JsValue.Undefined;
        }

        FormSubmitter.Submit(runtime, form, submitter, fireEvent);
        return JsValue.Undefined;
    }

    /// <summary>
    /// <c>requestSubmit</c>'s optional argument: a submit button of this very form, or nothing.
    /// </summary>
    /// <remarks>
    /// Both refusals are the standard's: an element that is not a submit button is a <c>TypeError</c>, and a
    /// submit button belonging to another form is a <c>NotFoundError</c>.
    /// </remarks>
    private static IElement? Submitter(JsValue thisObject, JsValue argument)
    {
        if (argument.IsUndefined() || argument.IsNull())
        {
            return null;
        }

        var runtime = PageRuntime.Of(thisObject, "requestSubmit");
        var realm = runtime.Engine._mainRealm;

        if (argument is not IDomWrapper { DomTarget: IElement element } || !IsSubmitButton(element))
        {
            Throw.TypeError(realm, "Failed to execute 'requestSubmit' on 'HTMLFormElement': The specified element is not a submit button.");
            return null;
        }

        if (thisObject is IDomWrapper { DomTarget: IHtmlFormElement form }
            && !ReferenceEquals(OwningForm(element), form))
        {
            var exception = realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.NotFound,
                "Failed to execute 'requestSubmit' on 'HTMLFormElement': The specified element is not owned by this form element.");

            var location = runtime.Engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(runtime.Engine, exception, in location);
        }

        return element;
    }

    private static bool IsSubmitButton(IElement element)
        => element switch
        {
            IHtmlButtonElement button => string.Equals(button.Type, "submit", StringComparison.OrdinalIgnoreCase),
            IHtmlInputElement input => (input.GetAttribute("type") ?? "").Equals("submit", StringComparison.OrdinalIgnoreCase)
                || (input.GetAttribute("type") ?? "").Equals("image", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

    private static IHtmlFormElement? OwningForm(IElement element)
        => element switch
        {
            IHtmlButtonElement button => button.Form,
            IHtmlInputElement input => input.Form,
            _ => null,
        };
}

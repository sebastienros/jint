using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Files;

namespace Jint.Browser.Runtime;

/// <summary>
/// https://xhr.spec.whatwg.org/#dom-formdata — the browser half of the FormData constructor.
/// </summary>
internal static class FormDataConstruction
{
    internal static void Install(PageRuntime runtime)
    {
        runtime.Engine._mainRealm.Intrinsics.FormData.ConstructFromForm =
            (form, submitter, newTarget) => Construct(runtime, form, submitter, newTarget);
    }

    private static JsFormData Construct(PageRuntime runtime, JsValue formValue, JsValue submitterValue, JsValue newTarget)
    {
        var realm = runtime.Engine._mainRealm;
        IHtmlFormElement? form = null;
        if (!formValue.IsUndefined())
        {
            if (formValue is not DomNodeObject { Node: IHtmlFormElement candidate })
            {
                Throw.TypeError(realm, "FormData: form must be an HTMLFormElement");
                return null!;
            }

            form = candidate;
        }

        IHtmlElement? submitter = null;
        if (!submitterValue.IsNullOrUndefined())
        {
            if (submitterValue is not DomNodeObject { Node: IHtmlElement candidate })
            {
                Throw.TypeError(realm, "FormData: submitter must be an HTMLElement");
                return null!;
            }

            submitter = candidate;
        }

        // WebIDL converts both arguments before creating the instance, even if the form is absent.
        // Reading newTarget.prototype can run script; it precedes the constructor's entry-list steps.
        var instance = realm.Intrinsics.FormData.CreateInstance(newTarget);
        if (form is null)
        {
            return instance;
        }

        if (submitter is not null)
        {
            if (!FormSubmission.IsSubmitButton(submitter))
            {
                Throw.TypeError(realm, "FormData: submitter must be a submit button");
            }

            if (!ReferenceEquals(FormSubmission.FormOwnerOf(submitter), form))
            {
                var exception = realm.Intrinsics.DomException.CreateException(
                    DomExceptionNames.NotFound, "The submitter is not owned by this form");
                var location = runtime.Engine._lastSyntaxElement?.Location ?? default;
                Throw.JavaScriptException(runtime.Engine, exception, in location);
                return null!;
            }
        }

        var entries = FormSubmitter.ConstructEntryList(runtime, form, submitter);
        if (entries is null)
        {
            var exception = realm.Intrinsics.DomException.CreateException(
                DomExceptionNames.InvalidState, "The form's entry list is already being constructed");
            var location = runtime.Engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(runtime.Engine, exception, in location);
            return null!;
        }

        instance.Entries.AddRange(entries);
        return instance;
    }
}

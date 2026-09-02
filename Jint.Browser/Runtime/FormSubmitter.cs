using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// HTML's form submission algorithm, ending in a navigation the page runs.
/// <para>
/// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#form-submission-algorithm
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>AngleSharp's own submission is deliberately not used.</b> <c>IHtmlFormElement.Submit()</c> builds its
/// own entry list, dispatches into its own event bus — which holds nothing a script registered — and then
/// navigates through <c>IBrowsingContext</c> on whatever thread it was called from
/// (<see href="https://github.com/AngleSharp/AngleSharp/issues/1309">AngleSharp#1309</see>). A page whose
/// <c>submit</c> listener called <c>preventDefault()</c> would be navigated anyway, and a page's DOM would
/// be reached from a thread that is not its loop's. Everything below runs on the page loop and ends by
/// asking the page to navigate.
/// </para>
/// <para>
/// <b>A button click is not here.</b> This is the algorithm a submission runs once something has decided to
/// submit; what decides is either a script (<c>form.submit()</c>, <c>form.requestSubmit()</c>) or an
/// activation behaviour, and activation behaviour is the input model's (campaign item R2). That is why the
/// submitter is a parameter rather than something this works out.
/// </para>
/// </remarks>
internal static class FormSubmitter
{
    /// <summary>
    /// The lower half of the submission algorithm: the entry list with its <c>formdata</c> event, the
    /// encoding, and the navigation the result becomes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The upper half — interactive validation, the <c>submit</c> event and its cancelation — is
    /// <see cref="Events.FormSubmission"/>, and this is only ever reached once that half has let the
    /// submission through. <c>form.submit()</c> reaches it having skipped both, which is the whole of what
    /// distinguishes it from <c>form.requestSubmit()</c> and from a submit button.
    /// </para>
    /// <para>
    /// The reentrancy guard is HTML's <i>constructing entry list</i> flag, and the events half reads the same
    /// one before it fires anything, so a <c>formdata</c> listener that submits its own form again is stopped
    /// before a second <c>submit</c> event rather than after it.
    /// </para>
    /// </remarks>
    /// <param name="runtime">The page runtime the form belongs to.</param>
    /// <param name="form">The form to submit.</param>
    /// <param name="submitter">The element that caused the submission, or <see langword="null"/>.</param>
    internal static void Submit(PageRuntime runtime, IHtmlFormElement form, IElement? submitter)
    {
        if (runtime.SubmittingForms.Contains(form))
        {
            return;
        }

        Navigate(runtime, form, submitter);
    }

    private static void Navigate(PageRuntime runtime, IHtmlFormElement form, IElement? submitter)
    {
        var action = Attribute(submitter, "formaction") ?? form.GetAttribute("action");
        if (string.IsNullOrEmpty(action))
        {
            action = runtime.DocumentUrl;
        }

        var target = PageUrl.Parse(action!, runtime.DocumentUrl);
        if (target is null)
        {
            runtime.Recorder.Add(new PageError(
                PageErrorKind.ReportedError,
                "A form was submitted to '" + action + "', which is not a URL.",
                "form"));
            return;
        }

        var method = (Attribute(submitter, "formmethod") ?? form.GetAttribute("method") ?? "get").ToLowerInvariant();
        var enctype = Normalize(Attribute(submitter, "formenctype") ?? form.GetAttribute("enctype"));

        if (string.Equals(method, "dialog", StringComparison.Ordinal))
        {
            runtime.Recorder.Add(new PageError(
                PageErrorKind.ReportedError,
                "A form was submitted with method='dialog', and <dialog> is not implemented in this version.",
                "form"));
            return;
        }

        var entries = ConstructEntryList(runtime, form, submitter);

        // target=_blank opens a new page in a browser; there is no page-opening seam in this version, so
        // every target loads here and the page is told rather than left wondering.
        var frameTarget = Attribute(submitter, "formtarget") ?? form.GetAttribute("target");
        if (!string.IsNullOrEmpty(frameTarget)
            && !string.Equals(frameTarget, "_self", StringComparison.OrdinalIgnoreCase))
        {
            runtime.Recorder.Add(new PageError(
                PageErrorKind.ReportedError,
                "A form targeting '" + frameTarget + "' was submitted into the same page: this version opens no "
                + "second page, so _blank, a frame name and _top all load here.",
                "form"));
        }

        if (string.Equals(method, "get", StringComparison.Ordinal))
        {
            SubmitAsGet(runtime, target, entries);
            return;
        }

        SubmitAsPost(runtime, target, entries, enctype);
    }

    /// <summary>
    /// The mutate-action-URL step: the entry list replaces the URL's query, and the request is a plain
    /// <c>GET</c>. A <c>GET</c> submission cannot carry a file, so a file entry contributes its name — which
    /// is what the URL-encoded serializer does with one.
    /// </summary>
    private static void SubmitAsGet(PageRuntime runtime, UrlRecord target, List<FormDataEntry> entries)
    {
        target.Query = FormUrlEncoded.Serialize(UrlEncodedPairs(entries));
        runtime.Page.RequestNavigation(target.Serialize(), replace: false);
    }

    private static void SubmitAsPost(PageRuntime runtime, UrlRecord target, List<FormDataEntry> entries, string enctype)
    {
        byte[] body;
        string contentType;

        if (string.Equals(enctype, "multipart/form-data", StringComparison.Ordinal))
        {
            var boundary = MultipartBoundary();
            body = MultipartFormData.Serialize(entries, boundary);
            contentType = "multipart/form-data; boundary=" + boundary;
        }
        else if (string.Equals(enctype, "text/plain", StringComparison.Ordinal))
        {
            body = Encoding.UTF8.GetBytes(PlainText(entries));
            contentType = "text/plain;charset=UTF-8";
        }
        else
        {
            body = Encoding.UTF8.GetBytes(FormUrlEncoded.Serialize(UrlEncodedPairs(entries)));
            contentType = "application/x-www-form-urlencoded;charset=UTF-8";
        }

        runtime.Page.RequestFormPost(target.Serialize(), body, contentType);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-form-data-set,
    /// including the <c>formdata</c> event a script may amend the result in.
    /// </summary>
    internal static List<FormDataEntry> ConstructEntryList(PageRuntime runtime, IHtmlFormElement form, IElement? submitter)
    {
        var entries = new List<FormDataEntry>();
        runtime.SubmittingForms.Add(form);

        try
        {
            foreach (var element in form.Elements)
            {
                Append(runtime, entries, element, submitter);
            }

            return FireFormData(runtime, form, entries);
        }
        finally
        {
            runtime.SubmittingForms.Remove(form);
        }
    }

    private static void Append(PageRuntime runtime, List<FormDataEntry> entries, IHtmlElement element, IElement? submitter)
    {
        // Step 5.1: a control inside a datalist is a suggestion, not a submission.
        if (element.Closest("datalist") is not null)
        {
            return;
        }

        if (element is IHtmlInputElement or IHtmlButtonElement or IHtmlSelectElement or IHtmlTextAreaElement)
        {
            if (IsDisabled(element))
            {
                return;
            }
        }
        else
        {
            // Output, fieldset, object and the rest of form.elements are not submittable.
            return;
        }

        // Step 5.5: a button contributes only when it is the submitter.
        if (element is IHtmlButtonElement && !ReferenceEquals(element, submitter))
        {
            return;
        }

        var name = element.GetAttribute("name");

        if (element is IHtmlInputElement input)
        {
            var type = (input.GetAttribute("type") ?? "text").ToLowerInvariant();

            switch (type)
            {
                case "submit" or "reset" or "button":
                    if (!ReferenceEquals(element, submitter))
                    {
                        return;
                    }

                    break;

                case "checkbox" or "radio":
                    if (!input.IsChecked)
                    {
                        return;
                    }

                    break;

                case "image":
                    // The coordinate pair an image button submits needs a click position, which is the input
                    // model's (campaign item R2); until then an image button contributes nothing.
                    return;

                case "file":
                    if (string.IsNullOrEmpty(name))
                    {
                        return;
                    }

                    // "If there are no selected files, then append an entry with an empty File object" —
                    // which is what makes a server see the field at all.
                    entries.Add(new FormDataEntry(name!, EmptyFile(runtime)));
                    return;
            }

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            // A checkbox with no value attribute submits "on".
            var value = type is "checkbox" or "radio"
                ? input.GetAttribute("value") ?? "on"
                : input.Value ?? "";

            entries.Add(new FormDataEntry(name!, JsString.Create(value)));
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (element is IHtmlSelectElement select)
        {
            foreach (var option in select.Options)
            {
                if (option.IsSelected && !option.IsDisabled)
                {
                    entries.Add(new FormDataEntry(name!, JsString.Create(option.Value ?? "")));
                }
            }

            return;
        }

        if (element is IHtmlTextAreaElement textArea)
        {
            // The API value with newlines normalized to CRLF, which is what the algorithm asks for.
            var text = (textArea.Value ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
            entries.Add(new FormDataEntry(name!, JsString.Create(text)));
            return;
        }

        if (element is IHtmlButtonElement button)
        {
            entries.Add(new FormDataEntry(name!, JsString.Create(button.Value ?? "")));
        }
    }

    /// <summary>
    /// Step 6: a <c>formdata</c> event carrying a <c>FormData</c> over the entries, which a listener may
    /// add to, delete from or rewrite before the request is built.
    /// </summary>
    private static List<FormDataEntry> FireFormData(PageRuntime runtime, IHtmlFormElement form, List<FormDataEntry> entries)
    {
        if (runtime.Dom.WrapNode(form) is not { } target)
        {
            return entries;
        }

        var engine = runtime.Engine;
        var formData = new JsFormData(engine)
        {
            _prototype = engine._mainRealm.Intrinsics.FormData.PrototypeObject,
        };

        formData.Entries.AddRange(entries);

        var ev = PageEvents.Create(runtime, "formdata", bubbles: true, cancelable: false);
        PageEvents.Member(ev, "formData", formData);
        PageEvents.Dispatch(runtime, target, ev);

        // The list the listeners left behind, which is the one the request is built from.
        return [.. formData.Entries];
    }

    private static bool IsDisabled(IElement element)
    {
        if (element.HasAttribute("disabled"))
        {
            return true;
        }

        // A control inside a disabled fieldset is disabled too, unless it is inside that fieldset's first
        // legend — https://html.spec.whatwg.org/multipage/form-elements.html#concept-fieldset-disabled.
        for (var parent = element.ParentElement; parent is not null; parent = parent.ParentElement)
        {
            if (parent is not IHtmlFieldSetElement fieldSet || !fieldSet.HasAttribute("disabled"))
            {
                continue;
            }

            var legend = fieldSet.QuerySelector("legend");
            if (legend is null || !legend.Contains(element))
            {
                return true;
            }
        }

        return false;
    }

    private static JsFile EmptyFile(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        return new JsFile(engine, ReadOnlyMemory<byte>.Empty, "application/octet-stream", "", 0)
        {
            _prototype = engine._mainRealm.Intrinsics.File.PrototypeObject,
        };
    }

    private static List<FormUrlEncodedEntry> UrlEncodedPairs(List<FormDataEntry> entries)
    {
        var pairs = new List<FormUrlEncodedEntry>(entries.Count);

        foreach (var entry in entries)
        {
            pairs.Add(new FormUrlEncodedEntry(entry.Name, Text(entry.Value)));
        }

        return pairs;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#text/plain-encoding-algorithm.
    /// </summary>
    private static string PlainText(List<FormDataEntry> entries)
    {
        var builder = new StringBuilder();

        foreach (var entry in entries)
        {
            builder.Append(entry.Name).Append('=').Append(Text(entry.Value)).Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>A file in a text encoding contributes its name, which is what the standard says.</summary>
    private static string Text(JsValue value)
        => value is JsFile file ? file.Name : value.ToString();

    private static string Normalize(string? enctype)
    {
        var value = enctype?.Trim().ToLowerInvariant();
        return value switch
        {
            "multipart/form-data" => "multipart/form-data",
            "text/plain" => "text/plain",
            _ => "application/x-www-form-urlencoded",
        };
    }

    private static string? Attribute(IElement? submitter, string name)
    {
        var value = submitter?.GetAttribute(name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// A boundary no body can contain: the Fetch Standard's own shape, dashes and 32 random digits.
    /// </summary>
    private static string MultipartBoundary()
    {
        var builder = new StringBuilder("----JintBrowserFormBoundary", 27 + 32);
        var bytes = new byte[16];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}

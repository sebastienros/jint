using System.Text;
using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Url.Parsing;

namespace Jint.Browser.Runtime;

/// <summary>
/// The lower half of HTML's form submission algorithm: the entry list, the encoding, and the navigation the
/// page runs.
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
/// <b>Neither the decision nor the events are here.</b> This is what a submission runs once something has
/// decided to submit and the <c>submit</c> event has survived; what decides is a script
/// (<c>form.submit()</c>, <c>form.requestSubmit()</c>) or a submit button's activation behaviour, and the
/// validation and the event are <see cref="Events.FormSubmission"/>. That is why the submitter is a
/// parameter rather than something this works out.
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
    internal static void Submit(PageRuntime runtime, Element form, Element? submitter)
    {
        if (runtime.SubmittingForms.Contains(form))
        {
            return;
        }

        Navigate(runtime, form, submitter);
    }

    private static void Navigate(PageRuntime runtime, Element form, Element? submitter)
    {
        var work = new DomReadWork(runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken);
        work.Check();
        string? Attribute(Element? element, string name) => element is null ? null : work.Attribute(element, name);
        var action = Attribute(submitter, "formaction") ?? Attribute(form, "action");
        if (string.IsNullOrEmpty(action))
        {
            action = runtime.DocumentUrl;
        }

        var target = PageUrl.Parse(action!, DomDocumentState.BaseUri(form.OwnerDocument!,
            runtime.Engine.Constraints.Check, runtime.Dom.CancellationToken));
        work.Check();
        if (target is null)
        {
            runtime.Recorder.Add(
                PageErrorKind.ReportedError,
                "A form was submitted to '" + action + "', which is not a URL.",
                "form");
            return;
        }

        var rawMethod = Attribute(submitter, "formmethod") ?? Attribute(form, "method");
        var method = work.EqualAsciiIgnoreCase(rawMethod, "post") ? "post"
            : work.EqualAsciiIgnoreCase(rawMethod, "dialog") ? "dialog" : "get";
        var enctype = Normalize(Attribute(submitter, "formenctype") ?? Attribute(form, "enctype"));

        if (string.Equals(method, "dialog", StringComparison.Ordinal))
        {
            runtime.Recorder.Add(
                PageErrorKind.ReportedError,
                "A form was submitted with method='dialog', and <dialog> is not implemented in this version.",
                "form");
            return;
        }

        var entries = ConstructEntryList(runtime, form, submitter);
        if (entries is null)
        {
            return;
        }

        // target=_blank opens a new page in a browser; there is no page-opening seam in this version, so
        // every target loads here and the page is told rather than left wondering.
        var frameTarget = Attribute(submitter, "formtarget") ?? Attribute(form, "target");
        if (!string.IsNullOrEmpty(frameTarget)
            && !string.Equals(frameTarget, "_self", StringComparison.OrdinalIgnoreCase))
        {
            runtime.Recorder.Add(
                PageErrorKind.ReportedError,
                "A form targeting '" + frameTarget + "' was submitted into the same page: this version opens no "
                + "second page, so _blank, a frame name and _top all load here.",
                "form");
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
        runtime.Page.RequestNavigation(
            target.Serialize(),
            replace: false,
            reason: PageNavigationReason.FormSubmissionGet);
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
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#constructing-the-form-data-set,
    /// including the <c>formdata</c> event a script may amend the result in.
    /// </summary>
    internal static List<FormDataEntry>? ConstructEntryList(PageRuntime runtime, Element form, Element? submitter)
    {
        if (!runtime.SubmittingForms.Add(form))
        {
            return null;
        }

        var entries = new List<FormDataEntry>();

        try
        {
            // HTML's entry-list inventory is submittable controls, not form.elements: the latter excludes
            // image inputs and decides ownership by AngleSharp's rule rather than the standard's. The walk is
            // over the form's whole tree in tree order, so a control outside the form that the `form`
            // attribute associated with it contributes, and one inside it that points elsewhere does not.
            foreach (var element in HtmlFormOwner.ControlsOf(form, runtime.Dom.NativeReadCheckpoint, runtime.Dom.CancellationToken, runtime.CustomElementsIfCreated))
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

    private static void Append(PageRuntime runtime, List<FormDataEntry> entries, Element element, Element? submitter)
    {
        var realm = runtime.Dom;
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        for (var parent = element.ParentNode; parent is not null; parent = parent.ParentNode)
        {
            work.Step();
            if (parent is Element { NamespaceUri: Namespaces.Html, LocalName: "datalist" }) return;
        }
        if (element is not { NamespaceUri: Namespaces.Html, LocalName: "input" or "button" or "select" or "textarea" }
            || HtmlDisabledness.GetState(element, realm.NativeReadCheckpoint, realm.CancellationToken) == HtmlDisabledState.Disabled)
            return;
        if (element.LocalName == "button" && !ReferenceEquals(element, submitter)) return;
        var rawName = work.Attribute(element, "name");
        var name = rawName is null ? null : UrlCharacters.ToScalarValueString(rawName);
        if (element.LocalName == "input")
        {
            var type = HtmlInputTypes.Parse(work.Attribute(element, "type"));
            switch (type)
            {
                case HtmlInputType.Submit or HtmlInputType.Reset or HtmlInputType.Button:
                    if (!ReferenceEquals(element, submitter)) return;
                    break;
                case HtmlInputType.Checkbox or HtmlInputType.Radio:
                    if (!element.GetHtmlState()!.CheckedState!.Checked) return;
                    break;
                case HtmlInputType.Image:
                    if (ReferenceEquals(element, submitter))
                    {
                        var prefix = string.IsNullOrEmpty(name) ? "" : name + ".";
                        var (x, y) = Events.BrowserEventRealm.Of(runtime.Engine).SelectedImageCoordinate(element);
                        entries.Add(new FormDataEntry(prefix + "x", JsString.Create(x.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                        entries.Add(new FormDataEntry(prefix + "y", JsString.Create(y.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                    }
                    return;
                case HtmlInputType.File:
                    if (string.IsNullOrEmpty(name)) return;
                    var files = Dom.Files.FileTransferRealm.Of(runtime.Engine).InputFiles(element, false);
                    if (files is { Length: > 0 })
                    {
                        foreach (var file in files.Files)
                        {
                            work.Step();
                            entries.Add(new FormDataEntry(name, file));
                        }
                    }
                    else entries.Add(new FormDataEntry(name, EmptyFile(runtime)));
                    return;
            }
            if (string.IsNullOrEmpty(name)) return;
            var value = element.GetHtmlState()!.InputValue!.GetValue(realm.CancellationToken);
            if (type == HtmlInputType.Hidden && work.EqualAsciiIgnoreCase(name, "_charset_")) value = "UTF-8";
            entries.Add(StringEntry(name, value));
            AppendDirection(realm, entries, element, work);
            return;
        }
        if (string.IsNullOrEmpty(name)) return;
        if (element.LocalName == "select")
        {
            var options = element.GetHtmlState()!.Select!.SelectedOptions.Snapshot(realm.CancellationToken);
            work.Check();
            foreach (var option in options)
            {
                work.Step();
                if (!HtmlDisabledness.IsOptionDisabled(option, realm.NativeReadCheckpoint, realm.CancellationToken))
                    entries.Add(StringEntry(name, option.GetHtmlState()!.Option!.GetValue(realm.CancellationToken)));
            }
            return;
        }
        if (element.LocalName == "textarea")
        {
            entries.Add(StringEntry(name, DomTextAreaMembers.Value(realm, element)));
            AppendDirection(realm, entries, element, work);
            return;
        }
        entries.Add(StringEntry(name, work.Attribute(element, "value") ?? ""));
    }

    private static void AppendDirection(DomRealm realm, List<FormDataEntry> entries, Element element, DomReadWork work)
    {
        // HTML entry-list construction step 5.11: dirname follows the control's value.
        if (HtmlDirectionality.IsAutoDirectionalityControl(element, realm.NativeReadCheckpoint, realm.CancellationToken)
            && work.Attribute(element, "dirname") is { Length: > 0 } name)
            entries.Add(StringEntry(UrlCharacters.ToScalarValueString(name),
                HtmlDirectionality.Of(element, realm.NativeReadCheckpoint, realm.CancellationToken)));
    }

    /// <summary>
    /// Step 6: a <c>formdata</c> event carrying a <c>FormData</c> over the entries, which a listener may
    /// add to, delete from or rewrite before the request is built.
    /// </summary>
    private static List<FormDataEntry> FireFormData(PageRuntime runtime, Element form, List<FormDataEntry> entries)
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

    private static JsFile EmptyFile(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        return new JsFile(engine, ReadOnlyMemory<byte>.Empty, "application/octet-stream", "", 0)
        {
            _prototype = engine._mainRealm.Intrinsics.File.PrototypeObject,
        };
    }

    private static FormDataEntry StringEntry(string name, string value)
        => new(name, JsString.Create(UrlCharacters.ToScalarValueString(value)));

    private static List<FormUrlEncodedEntry> UrlEncodedPairs(List<FormDataEntry> entries)
    {
        var pairs = new List<FormUrlEncodedEntry>(entries.Count);

        foreach (var entry in entries)
        {
            pairs.Add(new FormUrlEncodedEntry(NormalizeNewlines(entry.Name), NormalizeNewlines(Text(entry.Value))));
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
            builder.Append(NormalizeNewlines(entry.Name)).Append('=').Append(NormalizeNewlines(Text(entry.Value))).Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#converting-an-entry-list-to-a-list-of-name-value-pairs
    /// Normalize CR and LF only, including file names in these text encodings. The multipart serializer
    /// owns its separate normalization rules, which preserve file bytes and do not normalize file names.
    /// </summary>
    private static string NormalizeNewlines(string value)
        => value.AsSpan().IndexOfAny('\r', '\n') < 0
            ? value
            : value.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", "\r\n", StringComparison.Ordinal);

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

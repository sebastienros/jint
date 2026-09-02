using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;

namespace Jint.Browser.Runtime;

/// <summary>
/// Parses a document with AngleSharp on the page loop, runs the inline scripts the parse reaches, and fires
/// the load lifecycle through Jint's own dispatcher.
/// </summary>
/// <remarks>
/// <para>
/// <b>The parse is driven synchronously and the scripting hook proves it.</b> AngleSharp's parse is an
/// asynchronous method, but nothing in this configuration is asynchronous — the source is in memory, no
/// resource loader is registered, no event loop is registered so its task queue runs inline, and the
/// scripting hook answers with a completed task — so every <c>await</c> inside it continues on the calling
/// thread and the whole parse, script execution included, happens on this one. That is checked rather than
/// believed: the scripting service records the thread it was called on, and a mismatch is reported as a page
/// error naming the fallback the design specifies.
/// </para>
/// <para>
/// <b>The lifecycle events are Jint's, not AngleSharp's.</b> AngleSharp fires its own <c>DOMContentLoaded</c>
/// and <c>load</c> into its own listener lists, which hold nothing a script registered, so they are invisible.
/// What a page sees is dispatched here, through the tree-aware dispatcher, so that a listener on the window
/// is on a bubbling event's path.
/// </para>
/// </remarks>
internal static class PageDocument
{
    /// <summary>Opens <paramref name="html"/> as <paramref name="url"/> and runs its inline scripts.</summary>
    internal static PageLoad Load(PageRuntime runtime, string html, string url, int loopThreadId)
    {
        var scripting = new PageScriptingService(runtime, html, url, loopThreadId);

        // WithCss registers the declaration factory `element.style` and the computed-style cascade read
        // through. There is deliberately no WithDefaultLoader: nothing here may reach the network, which is
        // also what makes AngleSharp's own navigation inert — its handler declines every protocol when no
        // requester is registered, so a script assigning location.host cannot start a fetch behind the loop.
        var context = BrowsingContext.New(Configuration.Default.WithCss().With(scripting));
        IDocument document;

        try
        {
            document = context.OpenAsync(response => response.Content(html).Address(url)).GetAwaiter().GetResult();
        }
        catch
        {
            // The context is this method's until a PageLoad owns it, so a parse that never produced one takes
            // its context with it rather than leaving it to the collector.
            (context as IDisposable)?.Dispose();
            throw;
        }

        runtime.Document ??= document;

        if (scripting.Hopped)
        {
            runtime.Recorder.Add(new PageError(
                PageErrorKind.ScriptError,
                "AngleSharp resumed the parse on another thread, so an inline script ran off the page loop. "
                + "The parse has to be driven synchronously instead: see Jint.Browser/AGENTS.md, 'the parser hop'.",
                url));
        }

        var unsupported = Survey(document);
        FireLifecycle(runtime);

        return new PageLoad(document, context, unsupported, scripting.ScriptsRun);
    }

    /// <summary>
    /// Names every <c>&lt;script&gt;</c> the parse did not run, so that a page doing nothing is explained.
    /// </summary>
    private static List<string> Survey(IDocument document)
    {
        var unsupported = new List<string>();

        foreach (var element in document.QuerySelectorAll("script"))
        {
            if (element is not IHtmlScriptElement script)
            {
                continue;
            }

            var reason = ReasonNotRun(script);
            if (reason is null)
            {
                continue;
            }

            unsupported.Add(reason);
        }

        return unsupported;
    }

    private static string? ReasonNotRun(IHtmlScriptElement script)
    {
        var type = script.Type;

        if (!string.IsNullOrEmpty(type) && !AngleSharp.Io.MimeTypeNames.IsJavaScript(type))
        {
            return string.Equals(type, "module", StringComparison.OrdinalIgnoreCase)
                ? "module script not run: " + Describe(script) + " (modules arrive with the parser driver)"
                : null;
        }

        if (!string.IsNullOrEmpty(script.Source))
        {
            return "external script not run: " + script.Source + " (a page reaches no network in this version)";
        }

        return null;
    }

    private static string Describe(IHtmlScriptElement script)
        => string.IsNullOrEmpty(script.Source) ? "inline" : script.Source!;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/parsing.html#the-end — <c>readystatechange</c> at the document,
    /// <c>DOMContentLoaded</c> bubbling from it, then <c>load</c> at the window.
    /// </summary>
    /// <remarks>
    /// <c>document.readyState</c> already reads <c>"complete"</c> at all three, because AngleSharp advances it
    /// during the parse and its setter is not reachable from outside the assembly. The separate
    /// <c>"interactive"</c> step becomes observable when the parser driver owns the parse.
    /// </remarks>
    private static void FireLifecycle(PageRuntime runtime)
    {
        var engine = runtime.Engine;
        var events = engine._mainRealm.Intrinsics.Event;
        var document = runtime.DocumentWrapper;
        var window = engine._webApi?.GlobalEventTarget;

        if (document is null || window is null)
        {
            return;
        }

        Dispatch(runtime, document, events.CreateTrustedEvent(JsString.Create("readystatechange")));
        Dispatch(runtime, document, events.CreateTrustedEvent(JsString.Create("DOMContentLoaded"), new EventInit(Bubbles: true, Cancelable: false, Composed: false)));
        Dispatch(runtime, window, events.CreateTrustedEvent(JsString.Create("load")));
    }

    private static void Dispatch(PageRuntime runtime, JsEventTarget target, JsEvent ev)
    {
        try
        {
            target.DispatchEvent(ev);
        }
        catch (JavaScriptException exception)
        {
            // Only reachable when the page has no diagnostics sink, since the sink is what makes the engine
            // report a listener's exception and carry on. A page still survives its listeners either way.
            runtime.Recorder.Add(new PageError(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                ev.TypeName));
        }
    }
}

/// <summary>What one parse produced: the document, the context that owns it, and what it could not run.</summary>
internal sealed record PageLoad(
    IDocument Document,
    IBrowsingContext Context,
    IReadOnlyList<string> UnsupportedScripts,
    int ScriptsRun);

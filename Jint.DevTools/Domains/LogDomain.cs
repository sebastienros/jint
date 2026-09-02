using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Log;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Runtime;

namespace Jint.DevTools.Domains;

/// <summary>
/// The <c>Log</c> domain: what went wrong, as the one entry stream a client watches for failures.
/// </summary>
/// <remarks>
/// <para>
/// Every recorded automation client sends <c>Log.enable</c> while connecting — Puppeteer, PuppeteerSharp and
/// Playwright all do — and then waits for <c>Log.entryAdded</c>. On a page it carries network failures,
/// deprecations and violations; on an engine target the only thing there is to report is script that failed,
/// so that is what it reports: an exception that escaped the pump, and a promise rejected with nothing to
/// handle it.
/// </para>
/// <para>
/// <b>There is no store, and <c>enable</c> replays nothing.</b> The protocol says the command sends the
/// entries collected so far; an engine target collects none, because the console journal is the history that
/// exists and it is the <c>Console</c> and <c>Runtime</c> domains that replay it. A client enabling after a
/// failure hears about the next one.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Log/"/>.
/// </para>
/// </remarks>
internal sealed class LogDomain : LogDomainBase, ITargetObserver
{
    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>Clears a store this target does not keep, which is the success a client expects.</summary>
    protected override ValueTask<EmptyResult> ClearAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers the success a client expects and reports no violations.
    /// </summary>
    /// <remarks>
    /// A violation is a rendering or a blocking-call budget — a long task, a forced reflow, a slow event
    /// handler — measured by a renderer this target does not have. Refusing would read to a client as a
    /// broken target; reporting nothing is the truth.
    /// </remarks>
    protected override ValueTask<EmptyResult> StartViolationsReportAsync(StartViolationsReportRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc cref="StartViolationsReportAsync"/>
    protected override ValueTask<EmptyResult> StopViolationsReportAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The text is the error's message and stack, rendered under the engine's own result limits: reading a
    /// <c>stack</c> a script defined as an accessor runs that script, and a client watching a log stream
    /// must not be what makes a page's code run unbounded.
    /// </remarks>
    void ITargetObserver.ExceptionThrown(JavaScriptException exception)
    {
        var location = exception.Location;
        var limits = (exception.Error as Native.Object.ObjectInstance)?.Engine.Options.ResultLimits;

        var text = limits is null
            ? exception.GetJavaScriptErrorString()
            : exception.GetJavaScriptErrorString(limits);

        Report(text, location.SourceFile, location.Start.Line);
    }

    /// <inheritdoc/>
    void ITargetObserver.RejectionThrown(JsValue promise, JsValue reason)
    {
        // The reason's own text, rendered without running its toString: a rejection reason is any value, and
        // a client watching a log stream must not be what makes a page's code run.
        Report("Uncaught (in promise) " + SafeText(reason), url: null, line: 0);
    }

    private void Report(string text, string? url, int line)
    {
        if (!IsEnabled)
        {
            return;
        }

        EmitDetached(LogEvents.EntryAdded(new EntryAddedEvent
        {
            Entry = new LogEntry
            {
                Source = LogEntrySourceValues.Javascript,
                Level = LogEntryLevelValues.Error,
                Text = text,
                Timestamp = EngineTarget.UnixMilliseconds(),
                Url = string.IsNullOrEmpty(url) ? null : url,

                // The protocol counts a log entry's line from one, unlike a call frame's; a location the
                // engine never filled in is left out rather than reported as line zero.
                LineNumber = line > 0 ? line : null,
            },
        }));
    }

#pragma warning disable JINT0002 // ValueInspector is the engine's getter-free describer; this is what it is for
    private static string SafeText(JsValue value) => Diagnostics.ValueInspector.Describe(value).Description;
#pragma warning restore JINT0002
}

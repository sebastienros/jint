namespace Jint.Browser;

/// <summary>
/// Something a page's script did that nothing in the page handled, recorded as text rather than as a value.
/// </summary>
/// <remarks>
/// <para>
/// It is deliberately not the engine's <c>DiagnosticEvent</c>. That carries a <c>JsValue</c>, and a
/// <c>JsValue</c> belongs to the engine that made it and to the thread that owns it — so handing one to a
/// caller of <see cref="Page.Errors"/> would hand out a reference nobody may safely read. Everything here is
/// rendered on the page loop, at the instant the engine reported it, and is a plain string afterwards.
/// </para>
/// <para>
/// A page that produced one of these is still alive: an uncaught error in a timer callback, an unhandled
/// rejection or a <c>reportError</c> call ends that job and nothing else.
/// </para>
/// </remarks>
public sealed class PageError
{
    internal PageError(PageErrorKind kind, string message, string? source, DateTimeOffset timestamp, string documentUrl)
    {
        Kind = kind;
        Message = message;
        Source = source;
        Timestamp = timestamp;
        DocumentUrl = documentUrl;
    }

    /// <summary>What produced the error.</summary>
    public PageErrorKind Kind { get; }

    /// <summary>The error rendered to a string, including its stack when the value carried one.</summary>
    public string Message { get; }

    /// <summary>Which callback the engine was running, when it was running one; otherwise <c>null</c>.</summary>
    public string? Source { get; }

    /// <summary>When the page recorded it, on the wall clock in UTC.</summary>
    /// <remarks>
    /// <see cref="DateTimeOffset.UtcNow"/> rather than the engine's configured <c>TimeProvider</c>, for the
    /// reason <c>PageRuntime.Now</c> gives about its own clock: a host that substituted a clock for its
    /// timers did not thereby ask for its diagnostics to be stamped with a time that never happened.
    /// </remarks>
    public DateTimeOffset Timestamp { get; }

    /// <summary>The URL of the document the page was showing when it was recorded.</summary>
    /// <remarks>
    /// <b>It is what a host cannot reconstruct afterwards.</b> <see cref="Page.Errors"/> is one list for the
    /// life of the page while <see cref="Page.Url"/> answers where the page is <i>now</i>, so without this a
    /// host that has driven several navigations cannot say which document an entry came from. It is
    /// <c>about:blank</c> for a page that has loaded nothing, and the <i>outgoing</i> document's URL for an
    /// error raised while a navigation is still in flight — the new one has not been committed yet.
    /// </remarks>
    public string DocumentUrl { get; }

    /// <inheritdoc />
    public override string ToString() => Source is null ? Message : Source + ": " + Message;
}

/// <summary>What produced a <see cref="PageError"/>.</summary>
public enum PageErrorKind
{
    /// <summary>A script called <c>reportError</c>, or an uncaught exception reached the global scope.</summary>
    ReportedError,

    /// <summary>An exception escaped a timer, listener, microtask or idle callback.</summary>
    UncaughtCallbackError,

    /// <summary>A promise was rejected with nothing attached to handle it.</summary>
    UnhandledPromiseRejection,

    /// <summary>A worker failed.</summary>
    WorkerError,

    /// <summary>An inline <c>&lt;script&gt;</c> threw while the document was being parsed.</summary>
    ScriptError,

    /// <summary>
    /// A turn of the page's loop ran past <see cref="BrowserOptions.MaxTaskDuration"/> or
    /// <see cref="BrowserOptions.MemoryLimit"/> and was cut short.
    /// </summary>
    /// <remarks>
    /// The turn ends and the page goes on: whatever was running is abandoned, the next timer callback, the
    /// next mailbox request and the rest of a parse all run with a budget of their own. A page that keeps
    /// producing these is a page whose script is in a loop.
    /// </remarks>
    BudgetExceeded,
}

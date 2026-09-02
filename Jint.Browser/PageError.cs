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
    internal PageError(PageErrorKind kind, string message, string? source)
    {
        Kind = kind;
        Message = message;
        Source = source;
    }

    /// <summary>What produced the error.</summary>
    public PageErrorKind Kind { get; }

    /// <summary>The error rendered to a string, including its stack when the value carried one.</summary>
    public string Message { get; }

    /// <summary>Which callback the engine was running, when it was running one; otherwise <c>null</c>.</summary>
    public string? Source { get; }

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
}

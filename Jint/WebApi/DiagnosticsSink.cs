#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi;

/// <summary>
/// Where the engine reports the script errors nobody caught: what <c>reportError</c> was handed, a promise
/// rejected with no handler, and an exception that escaped a callback the engine itself invoked. Requires
/// .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// This is the channel HTML calls <i>reporting an exception</i> —
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception">report an
/// exception</see>. In a browser that algorithm fires an <c>error</c> event at the global object and, if
/// nothing cancels it, lets "the user agent … report exception to a developer console". The sink <i>is</i>
/// that console.
/// </para>
/// <para>
/// The event half applies too, on an engine that enabled <see cref="WebApiFeatures.GlobalEvents"/>: Jint's
/// global object is still not an <c>EventTarget</c>, but the feature gives the engine a synthetic global
/// target that a script registers <c>error</c>, <c>unhandledrejection</c> and <c>rejectionhandled</c>
/// listeners on. <b>Those events feed this sink, they never replace it.</b> A listener calling
/// <c>preventDefault()</c> suppresses a browser's console report and deliberately does not suppress this one:
/// a host's diagnostics channel is not something the script it is running may switch off. So a script can
/// observe its own failures, and a host still hears every one of them.
/// </para>
/// <para>
/// <b>Setting a sink changes what happens to an exception that escapes an engine-invoked callback.</b> With
/// no sink — the default, <see langword="null"/> — a <c>JavaScriptException</c> thrown by a timer callback, a
/// <c>queueMicrotask</c> callback, a <c>requestIdleCallback</c> callback or an event listener erupts out of
/// whatever was running it, because swallowing it would lose it entirely.
/// With a sink it is reported here and the engine carries on, which is what the specifications say to do:
/// HTML invokes a timer handler and a <c>queueMicrotask</c> callback with exception behavior <c>"report"</c>,
/// so do both of the algorithms that run an idle callback, and DOM's <i>inner invoke</i>
/// reports a throwing listener and moves to the next one. Errors that exist to <i>bound</i> execution are
/// never reported and always erupt — a timeout, a cancellation, the statement, memory and recursion budgets —
/// because a budget that turns into a diagnostic no longer bounds anything.
/// </para>
/// <para>
/// <see cref="Null"/> is therefore not the same as no sink at all: it means "report and continue, and discard
/// the report", which is a choice a host has to make deliberately rather than one it can fall into.
/// </para>
/// <para>
/// <b>Threading and value lifetime.</b> An engine only ever calls its sink from the thread running that
/// engine, synchronously, while the failure is still on the stack. A sink installed on an <see cref="Options"/>
/// instance shared by engines that run concurrently is called from each of their threads and must therefore be
/// thread-safe. The <see cref="JsValue"/>s on a <see cref="DiagnosticEvent"/> belong to the engine that
/// reported them: read them inside the call — convert to a string, a CLR value, whatever the host's log
/// wants — and do not stash one for later without knowing what that means. Such a value keeps its engine and
/// realm alive, is not safe to touch from another thread, and may describe globals a
/// <c>RestoreGlobalSnapshot</c> has since replaced. Note too that converting an object to text can run script:
/// <c>ToString()</c> on a <see cref="JsValue"/> calls the object's own <c>toString</c>.
/// </para>
/// <para>
/// An exception thrown by a sink is not caught. It erupts from wherever the report was made — the pump for a
/// timer, <c>dispatchEvent</c> for a listener, the <c>reportError</c> call itself — so a sink that can fail
/// should catch its own failures.
/// </para>
/// <para>
/// This is an abstract class rather than a delegate so that later revisions can add richer overloads without
/// breaking hosts that implement it today. For the same reason <see cref="DiagnosticEventKind"/> may gain
/// members: treat a kind you do not recognize as something worth reporting rather than as an error.
/// </para>
/// </remarks>
public abstract class DiagnosticsSink
{
    /// <summary>
    /// A sink that discards every report. Unlike leaving <c>Options.WebApi.Diagnostics.Sink</c>
    /// <see langword="null"/> this still switches an engine-invoked callback from erupting to
    /// report-and-continue — it says "keep going and tell me nothing", not "leave the errors alone".
    /// </summary>
    public static DiagnosticsSink Null { get; } = new NullDiagnosticsSink();

    /// <summary>
    /// Receives one diagnostic report.
    /// </summary>
    /// <param name="report">
    /// What happened. Its <see cref="DiagnosticEvent.Kind"/> says which of the other members carry anything;
    /// see the remarks on <see cref="DiagnosticsSink"/> for how long its <see cref="JsValue"/>s may be used.
    /// </param>
    public abstract void Report(DiagnosticEvent report);

    private sealed class NullDiagnosticsSink : DiagnosticsSink
    {
        public override void Report(DiagnosticEvent report)
        {
        }
    }
}

/// <summary>
/// One report delivered to a <see cref="DiagnosticsSink"/>. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// The instance is handed to the sink and forgotten by the engine; it is the <see cref="JsValue"/>s on it that
/// are engine-affine, and the remarks on <see cref="DiagnosticsSink"/> say what may be done with them. The
/// constructor is internal because only the engine creates these, which is also what lets further members be
/// added without breaking a host that reads the ones it knows.
/// </remarks>
public sealed class DiagnosticEvent
{
    internal DiagnosticEvent(
        DiagnosticEventKind kind,
        JsValue value,
        JavaScriptException? exception = null,
        DiagnosticCallbackSource? callbackSource = null,
        JsValue? promise = null,
        bool rejectionHandled = false)
    {
        Kind = kind;
        Value = value;
        Exception = exception;
        CallbackSource = callbackSource;
        Promise = promise;
        RejectionHandled = rejectionHandled;
    }

    /// <summary>
    /// What is being reported, and therefore which of the members below mean anything.
    /// </summary>
    public DiagnosticEventKind Kind { get; }

    /// <summary>
    /// The value at the centre of the report: what <c>reportError</c> was given, the value a callback threw,
    /// or the reason a promise was rejected with. Never <see langword="null"/>, but it is
    /// <see cref="JsValue.Undefined"/> for a rejection whose promise is somehow no longer rejected — script
    /// may throw or reject with any value at all, including <c>undefined</c>, so an undefined value here is
    /// not by itself a sign that something is missing.
    /// </summary>
    public JsValue Value { get; }

    /// <summary>
    /// The exception that would have erupted, for
    /// <see cref="DiagnosticEventKind.UncaughtCallbackError"/>; <see langword="null"/> for every other kind.
    /// It carries what the engine knew at the throw — the JavaScript stack trace, the source location and a
    /// CLR-side message — which is usually what a host's log wants rather than the raw
    /// <see cref="Value"/>.
    /// </summary>
    public JavaScriptException? Exception { get; }

    /// <summary>
    /// Which engine-invoked callback the exception escaped from, for
    /// <see cref="DiagnosticEventKind.UncaughtCallbackError"/>; <see langword="null"/> for every other kind.
    /// </summary>
    public DiagnosticCallbackSource? CallbackSource { get; }

    /// <summary>
    /// The promise, for <see cref="DiagnosticEventKind.UnhandledPromiseRejection"/>; <see langword="null"/>
    /// for every other kind. It is the same object
    /// <see cref="PromiseRejectionTrackerEventArgs.Promise"/> reports, so a host with both channels wired can
    /// match them up by identity.
    /// </summary>
    public JsValue? Promise { get; }

    /// <summary>
    /// For <see cref="DiagnosticEventKind.UnhandledPromiseRejection"/>, whether this report says the rejection
    /// has now been <i>handled</i> after all — a handler was attached to a promise that had already been
    /// reported as unhandled. HTML raises the two as <c>unhandledrejection</c> and <c>rejectionhandled</c>;
    /// this is the second of them, and a host that only wants failures should ignore a report where it is
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections
    /// </para>
    /// <para>
    /// <b>The two arrive at <c>HostPromiseRejectionTracker</c>'s cadence, not HTML's.</b> HTML defers its
    /// <c>unhandledrejection</c> event to the end of a microtask checkpoint and skips any promise handled
    /// before then, so <c>Promise.reject(e).catch(f)</c> raises nothing at all in a browser. Jint reports the
    /// tracker's two operations as they happen — which is exactly what
    /// <see cref="Engine.TaskOperations.PromiseRejectionTracker"/> has always done, and what keeps the two
    /// channels telling one story — so the same code produces a report with this
    /// <see langword="false"/> followed by one with it <see langword="true"/>. A host that wants the browser's
    /// shape correlates the pair by <see cref="Promise"/> identity.
    /// </para>
    /// <para>
    /// <b>The <c>unhandledrejection</c> and <c>rejectionhandled</c> events of
    /// <see cref="WebApiFeatures.GlobalEvents"/> inherit that cadence whole</b>, being fired from this very
    /// call. A script that registers those listeners therefore sees the same pair a sink does, in the same
    /// order and at the same moment, rather than the single deferred event a browser would raise; and
    /// cancelling <c>unhandledrejection</c> suppresses nothing here, for the reason
    /// <see cref="DiagnosticsSink"/> gives.
    /// </para>
    /// </remarks>
    public bool RejectionHandled { get; }

    /// <summary>
    /// <c>reportError(e)</c>, or anything else the engine reports as an exception per HTML's <i>report an
    /// exception</i>.
    /// </summary>
    internal static DiagnosticEvent ForReportedError(JsValue value)
        => new(DiagnosticEventKind.ReportedError, value);

    /// <summary>
    /// A <see cref="JavaScriptException"/> that escaped a callback the engine invoked and that a sink is the
    /// reason for not letting erupt.
    /// </summary>
    internal static DiagnosticEvent ForUncaughtCallbackError(JavaScriptException exception, DiagnosticCallbackSource source)
        => new(DiagnosticEventKind.UncaughtCallbackError, exception.Error, exception, source);

    /// <summary>
    /// A failure inside a worker that neither the worker nor the parent's <c>Worker</c> object handled, which
    /// HTML's <i>report an exception</i> reports one level up — at the parent.
    /// </summary>
    internal static DiagnosticEvent ForWorkerError(string message)
        => new(DiagnosticEventKind.WorkerError, JsString.Create(message));

    /// <summary>
    /// The two <c>HostPromiseRejectionTracker</c> operations, which are also what
    /// <see cref="Engine.TaskOperations.PromiseRejectionTracker"/> raises.
    /// </summary>
    internal static DiagnosticEvent ForPromiseRejection(JsPromise promise, PromiseRejectionOperation operation)
        => new(
            DiagnosticEventKind.UnhandledPromiseRejection,
            promise.State == PromiseState.Rejected ? promise.Value : JsValue.Undefined,
            promise: promise,
            rejectionHandled: operation == PromiseRejectionOperation.Handle);
}

/// <summary>
/// What a <see cref="DiagnosticEvent"/> reports. Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// Further members may be added as the web-API surface grows, so a sink should treat an unrecognized kind as
/// something worth reporting rather than as an error.
/// </remarks>
public enum DiagnosticEventKind
{
    /// <summary>
    /// Script called <c>reportError(e)</c>, which HTML defines as reporting <c>e</c> as though it were an
    /// uncaught exception — https://html.spec.whatwg.org/multipage/webappapis.html#dom-reporterror.
    /// <see cref="DiagnosticEvent.Value"/> is the value it was given.
    /// </summary>
    ReportedError,

    /// <summary>
    /// An exception escaped a callback the engine invoked — a timer handler, a <c>queueMicrotask</c> callback,
    /// a <c>requestIdleCallback</c> callback, an event listener — and a sink
    /// being set is why it was reported instead of erupting.
    /// <see cref="DiagnosticEvent.Exception"/> and <see cref="DiagnosticEvent.CallbackSource"/> say what and
    /// where.
    /// </summary>
    UncaughtCallbackError,

    /// <summary>
    /// A promise was rejected with nothing to handle it, or one that had already been reported that way has
    /// since had a handler attached — the two operations of <c>HostPromiseRejectionTracker</c>, told apart by
    /// <see cref="DiagnosticEvent.RejectionHandled"/>.
    /// </summary>
    UnhandledPromiseRejection,

    /// <summary>
    /// A failure inside a <c>Worker</c> this engine created that nothing handled — neither the worker's own
    /// <c>error</c> listeners nor the <c>Worker</c> object's. It is HTML's <i>report an exception</i> reaching
    /// its last step one engine up
    /// (https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DiagnosticEvent.Value"/> is the failure's <b>message, as a string</b>, and that is not a
    /// simplification: the thrown value belongs to the worker's realm and to the worker's thread, so it may not
    /// cross — which is the same reason the <c>ErrorEvent</c> the parent's script sees carries
    /// <c>error: null</c>, exactly as the standard prescribes. A worker that wants its parent to have the real
    /// failure catches it and <c>postMessage</c>s it.
    /// </para>
    /// <para>
    /// A worker's own sink — the one a provider installed on the worker engine — has already seen the same
    /// failure as an <see cref="UncaughtCallbackError"/> or a <see cref="ReportedError"/>, because that channel
    /// is unsuppressible. So a host that wires <i>one</i> sink for a parent and its workers sees the failure
    /// twice, once from each side; this kind is what tells the two apart.
    /// </para>
    /// </remarks>
    WorkerError,
}

/// <summary>
/// Which engine-invoked callback a <see cref="DiagnosticEventKind.UncaughtCallbackError"/> escaped from.
/// Requires .NET 8 or higher.
/// </summary>
public enum DiagnosticCallbackSource
{
    /// <summary>
    /// A <c>setTimeout</c> or <c>setInterval</c> handler. HTML invokes it with exception behavior
    /// <c>"report"</c> —
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timer-initialisation-steps.
    /// An interval whose handler throws keeps running: it is re-armed before the handler is called.
    /// </summary>
    Timer,

    /// <summary>
    /// An <c>EventTarget</c> listener. DOM's <i>inner invoke</i> reports a throwing listener and carries on to
    /// the next one — https://dom.spec.whatwg.org/#concept-event-listener-inner-invoke.
    /// </summary>
    EventListener,

    /// <summary>
    /// A <c>queueMicrotask</c> callback. HTML queues a microtask to invoke it "given null and <c>"report"</c>"
    /// — https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask — which is the
    /// same WebIDL exception behavior a timer handler is invoked with, and the same one WebIDL's <i>report the
    /// exception</i> defines: https://webidl.spec.whatwg.org/#report-the-exception.
    /// </summary>
    Microtask,

    /// <summary>
    /// A <c>requestIdleCallback</c> callback. Both algorithms that reach one invoke it with the same
    /// <c>"report"</c> exception behavior — <i>invoke idle callbacks</i>
    /// (https://w3c.github.io/requestidlecallback/#invoke-idle-callbacks-algorithm) and <i>invoke idle
    /// callback timeout</i>
    /// (https://w3c.github.io/requestidlecallback/#invoke-idle-callback-timeout-algorithm), whose steps both
    /// read "Invoke callback with « deadlineArg » and <c>"report"</c>".
    /// </summary>
    /// <remarks>
    /// A callback the <c>timeout</c> option reached is reported under this source and not under
    /// <see cref="Timer"/>, although the engine does run it from a timer: the timer is how the timeout is
    /// measured, and the callback is still the one <c>requestIdleCallback</c> was given.
    /// </remarks>
    IdleCallback,

    /// <summary>
    /// A <c>PerformanceObserver</c> callback. <i>Queue the PerformanceObserver task</i> invokes it with the
    /// same <c>"report"</c> exception behavior —
    /// https://w3c.github.io/performance-timeline/#queue-the-performance-observer-task.
    /// </summary>
    PerformanceObserver,
}
#endif

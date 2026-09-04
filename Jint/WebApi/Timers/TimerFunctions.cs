#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.WebApi.Timers;

/// <summary>
/// The five timer globals: <c>setTimeout</c>, <c>setInterval</c>, <c>clearTimeout</c>, <c>clearInterval</c>
/// and <c>queueMicrotask</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timers
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// They are global <i>operations</i> rather than members of a namespace object, so unlike <c>console</c>
/// there is nothing here for a script to reach except the functions themselves; this class only owns them so
/// that one realm builds one of each. Each is built on first access, so a script that mentions
/// <c>setTimeout</c> and nothing else creates one function object.
/// </para>
/// <para>
/// <b>The string form of the handler is deliberately not supported.</b> <c>setTimeout("x = 1", 0)</c> is
/// specified to compile the string as a classic script, which is <c>eval</c> by another name and reachable
/// even when a host disabled string compilation; it raises a <c>TypeError</c> here instead, which is what
/// Node does and what every non-browser runtime a script is likely to be written for does.
/// </para>
/// </remarks>
internal sealed class TimerFunctions
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly TimerQueue _timers;

    private ClrFunction? _setTimeout;
    private ClrFunction? _setInterval;
    private ClrFunction? _clearTimeout;
    private ClrFunction? _clearInterval;
    private ClrFunction? _queueMicrotask;

    private TimerFunctions(Engine engine, Realm realm, TimerQueue timers)
    {
        _engine = engine;
        _realm = realm;
        _timers = timers;
    }

    internal static TimerFunctions Create(Engine engine, Realm realm)
    {
        var timers = engine._webApi?.Timers;
        if (timers is null)
        {
            // Unreachable: the globals that reach this property are installed only where the queue was
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The timer globals were reached on an engine that has no timer queue.");
        }

        return new TimerFunctions(engine, realm, timers);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-settimeout
    /// </summary>
    internal ClrFunction SetTimeout =>
        _setTimeout ??= Operation("setTimeout", 1, (_, arguments) => Set(arguments, repeat: false));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-setinterval
    /// </summary>
    internal ClrFunction SetInterval =>
        _setInterval ??= Operation("setInterval", 1, (_, arguments) => Set(arguments, repeat: true));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-cleartimeout
    /// </summary>
    internal ClrFunction ClearTimeout =>
        _clearTimeout ??= Operation("clearTimeout", 0, (_, arguments) => Clear(arguments));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-clearinterval
    /// <para>
    /// The same operation as <c>clearTimeout</c> — the specification gives both a single map of ids, so
    /// either function cancels either kind of timer.
    /// </para>
    /// </summary>
    internal ClrFunction ClearInterval =>
        _clearInterval ??= Operation("clearInterval", 0, (_, arguments) => Clear(arguments));

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask
    /// </summary>
    internal ClrFunction QueueMicrotask =>
        _queueMicrotask ??= Operation("queueMicrotask", 1, (_, arguments) => QueueMicrotaskCallback(arguments));

    /// <summary>
    /// A WebIDL operation: <c>length</c> counts the required arguments only and is configurable but neither
    /// writable nor enumerable — https://webidl.spec.whatwg.org/#dfn-create-operation-function.
    /// </summary>
    private ClrFunction Operation(string name, int length, JsCallDelegate body)
        => new(_engine, _realm, name, body, length, PropertyFlag.Configurable);

    /// <summary>
    /// The timer initialization steps, https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timer-initialisation-steps,
    /// shared by <c>setTimeout</c> and <c>setInterval</c>.
    /// </summary>
    private JsValue Set(JsCallArguments arguments, bool repeat)
    {
        var name = repeat ? "setInterval" : "setTimeout";

        if (arguments.At(0) is not ICallable callback)
        {
            Throw.TypeError(
                _realm,
                $"Failed to execute '{name}': the handler argument must be a function. Passing a string to be compiled is not supported.");
            return JsValue.Undefined;
        }

        if (_timers.Count >= _timers.MaxActiveTimers)
        {
            // Not a specified failure mode — the specification assumes a browser's resources — but an engine
            // embedded in a server cannot let a script register timers without bound. QuotaExceededError is
            // what WebIDL gives "the host refused because a limit was reached", and unlike getRandomValues
            // there is no algorithm here declining to name the numbers: the cap and the count are exactly what
            // https://webidl.spec.whatwg.org/#quotaexceedederror added `quota` and `requested` for.
            var quotaExceeded = _realm.Intrinsics.QuotaExceededError.CreateException(
                $"Failed to execute '{name}': the engine already has {_timers.MaxActiveTimers} active timers, which is its Options.WebApi.Timers.MaxActiveTimers limit.",
                quota: _timers.RefusalQuota,
                requested: _timers.RefusalRequested);

            var location = _engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(_engine, quotaExceeded, in location);
        }

        // WebIDL types the timeout as `long`, so the coercion is ToInt32's: NaN and the infinities become 0,
        // and a value beyond 32 bits wraps exactly as it does in a browser. A negative result — whether the
        // script wrote one or the wrap produced it — is then clamped to 0 by step 7.
        var delay = TypeConverter.ToInt32(arguments.At(1));
        if (delay < 0)
        {
            delay = 0;
        }

        // "Let arguments be the arguments after timeout" — copied, because the caller's array belongs to the
        // engine's argument pool and is reused as soon as this call returns.
        var extraArguments = arguments.Length > 2 ? arguments[2..] : [];

        var entry = new TimerEntry(
            _timers,
            callback,
            extraArguments,
            delay,
            repeat,
            _engine.CaptureEventLoopRegistration());
        return JsNumber.Create(_timers.Schedule(entry));
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-cleartimeout — "the user agent
    /// must remove the entry", and an id that is not there is simply not an error. So an unknown id, a
    /// missing argument and a value that coerces to no id at all are all silent no-ops; the only way this can
    /// throw is the WebIDL <c>long</c> conversion refusing a value <c>ToNumber</c> itself refuses, such as a
    /// symbol.
    /// </summary>
    private JsValue Clear(JsCallArguments arguments)
    {
        _timers.Cancel(TypeConverter.ToInt32(arguments.At(0)));
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask
    /// <para>
    /// The engine's single job queue <em>is</em> the microtask queue, so this is one enqueue: the callback
    /// runs after everything already queued and before any timer is even considered.
    /// </para>
    /// </summary>
    private JsValue QueueMicrotaskCallback(JsCallArguments arguments)
    {
        if (arguments.At(0) is not ICallable callback)
        {
            Throw.TypeError(_realm, "Failed to execute 'queueMicrotask': the callback argument must be a function.");
            return JsValue.Undefined;
        }

        // The current generation, unlike a timer's: this job is registered and queued in one act, so there is
        // no window in which the cycle could have ended in between.
        _engine.AddToEventLoop(() => InvokeMicrotask(callback), EventLoopJobKind.Microtask);
        return JsValue.Undefined;
    }

    /// <summary>
    /// The microtask <c>queueMicrotask</c> queued: "invoke <i>callback</i> given null and <c>"report"</c>",
    /// step 2 of https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-queuemicrotask.
    /// </summary>
    /// <remarks>
    /// WebIDL's <c>"report"</c> exception behavior is <i>report the exception</i>
    /// (https://webidl.spec.whatwg.org/#report-the-exception) and returning undefined, which is what the catch
    /// does whenever the host gave the engine somewhere to report to — the same shape
    /// <c>TimerEntry.Fire</c> and <c>JsEventTarget.InvokePass</c> have, for the same reasons.
    /// </remarks>
    private void InvokeMicrotask(ICallable callback)
    {
        try
        {
            // Through Engine.Call, so the callback owns a call-stack frame: see TimerEntry.Fire.
            _engine.Call(callback, JsValue.Undefined, Arguments.Empty, expression: null);
        }
        catch (JavaScriptException exception) when (_engine._webApi?.Diagnostics is { } diagnostics)
        {
            // Report the exception is HTML's report an exception, whose step 5 fires an `error` event at the
            // global scope before step 6 reaches the console. A no-op unless the GlobalEvents feature is on and
            // a script is listening; see WebApiEngineState.FireGlobalErrorEvent.
            _engine._webApi?.FireGlobalErrorEvent(exception);

            // Only a JavaScriptException, which is exactly the class of failure a script could have caught
            // itself. Everything that exists to bound execution — ExecutionCanceledException,
            // TimeoutException, the statement, memory and recursion budgets — is a JintException but not a
            // JavaScriptException, so none of it is caught here and a constraint still stops the engine. With
            // no sink there is no catch at all and the throw erupts out of whatever is running the queue,
            // exactly as one from a promise reaction handler without a capability does; everything still
            // queued runs either way.
            diagnostics.Report(DiagnosticEvent.ForUncaughtCallbackError(exception, DiagnosticCallbackSource.Microtask));
        }
    }
}
#endif

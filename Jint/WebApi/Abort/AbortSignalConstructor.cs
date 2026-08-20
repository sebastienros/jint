#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.Timers;

namespace Jint.WebApi.Abort;

/// <summary>
/// The <c>AbortSignal</c> interface object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-AbortSignal
/// </para>
/// </summary>
/// <remarks>
/// <c>AbortSignal</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the <c>EventTarget</c>
/// interface object. It declares no constructor operation, which in WebIDL means the interface object exists
/// and is a function but refuses to construct anything —
/// https://webidl.spec.whatwg.org/#es-interface-call — so a signal can only come from an
/// <c>AbortController</c> or from one of the three statics here.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class AbortSignalConstructor : Constructor
{
    private static readonly JsString _functionName = new("AbortSignal");

    internal AbortSignalConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new AbortSignalPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal AbortSignalPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-abort
    /// </summary>
    [JsFunction(Name = "abort", Length = 0)]
    private JsAbortSignal StaticAbort(JsValue thisObject, JsValue reason)
    {
        var signal = CreateSignal();
        signal.SignalAbort(DefaultedReason(reason));
        return signal;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-timeout
    /// </summary>
    /// <remarks>
    /// <para>
    /// The timeout is an entry on the engine's own timer queue, so <b>it fires only while the engine is being
    /// pumped</b> — the same contract <c>setTimeout</c> has, and for the same reason: Jint never starts a
    /// thread to run script. An engine nobody pumps never aborts a timeout signal, and the abort, when it
    /// happens, happens on the engine's thread inside the pump.
    /// </para>
    /// <para>
    /// It also counts against <c>Options.WebApi.Timers.MaxActiveTimers</c>, because a script that can create
    /// signals can otherwise fill the queue without a <c>setTimeout</c> in sight. The queue exists whenever
    /// this feature does, whether or not the timer globals were installed.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "timeout", Length = 1)]
    private JsAbortSignal StaticTimeout(JsValue thisObject, JsValue milliseconds)
    {
        var delay = EnforceRangeMilliseconds(milliseconds);
        var timers = _engine._webApi?.Timers;
        if (timers is null)
        {
            // Unreachable: the global that reaches this function is installed only where the queue is created.
            Throw.InvalidOperationException("AbortSignal.timeout was reached on an engine that has no timer queue.");
            return null!;
        }

        if (timers.Count >= timers.MaxActiveTimers)
        {
            ThrowDomException(
                DomExceptionNames.QuotaExceeded,
                $"Failed to execute 'timeout' on 'AbortSignal': the engine already has {timers.MaxActiveTimers} active timers, which is its Options.WebApi.Timers.MaxActiveTimers limit.");
        }

        var signal = CreateSignal();
        var entry = new TimerEntry(timers, new TimeoutAbortAlgorithm(signal, _realm), [], delay, repeat: false, _engine.EventLoopGeneration);
        timers.Schedule(entry);
        return signal;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-abortsignal-any
    /// </summary>
    [JsFunction(Name = "any", Length = 1)]
    private JsAbortSignal StaticAny(JsValue thisObject, JsValue signals)
    {
        var sources = ReadSignalSequence(signals, "AbortSignal");
        return JsAbortSignal.CreateDependent(_engine, _realm, sources);
    }

    /// <summary>
    /// Builds a fresh, non-aborted signal in this realm. Used by the statics and by
    /// <see cref="AbortControllerConstructor"/>.
    /// </summary>
    internal JsAbortSignal CreateSignal()
    {
        return new JsAbortSignal(_engine, _realm)
        {
            _prototype = PrototypeObject,
        };
    }

    /// <summary>
    /// "Set signal's abort reason to reason if it is given; otherwise to a new <c>AbortError</c>
    /// <c>DOMException</c>." An explicitly passed <see langword="undefined"/> is treated as not given, which
    /// is what every implementation does and what keeps a signal from ever being aborted with a reason of
    /// <c>undefined</c> — the value the specification derives <c>aborted</c> from.
    /// </summary>
    internal JsValue DefaultedReason(JsValue reason)
    {
        if (!reason.IsUndefined())
        {
            return reason;
        }

        return _realm.Intrinsics.DomException.CreateException(DomExceptionNames.Abort, "signal is aborted without reason");
    }

    /// <summary>
    /// The <c>sequence&lt;AbortSignal&gt;</c> conversion, https://webidl.spec.whatwg.org/#es-sequence: the
    /// argument is iterated with the iterator protocol and every element must be an <c>AbortSignal</c>.
    /// <para>
    /// Shared with <c>TaskSignal.any()</c>, whose first argument is the same <c>sequence&lt;AbortSignal&gt;</c>
    /// — hence the interface name, which is only there so the <c>TypeError</c> names the operation the script
    /// actually called.
    /// </para>
    /// </summary>
    internal List<JsAbortSignal> ReadSignalSequence(JsValue signals, string interfaceName)
    {
        var result = new List<JsAbortSignal>();
        var iterator = signals.GetIterator(_realm);

        try
        {
            while (iterator.TryIteratorStepValue(out var value))
            {
                if (value is not JsAbortSignal signal)
                {
                    Throw.TypeError(_realm, $"Failed to execute 'any' on '{interfaceName}': the provided value is not of type 'AbortSignal'.");
                    return result;
                }

                result.Add(signal);
            }
        }
        catch
        {
            iterator.CloseIfNotDone(CompletionType.Throw);
            throw;
        }

        return result;
    }

    /// <summary>
    /// The <c>[EnforceRange] unsigned long long</c> conversion,
    /// https://webidl.spec.whatwg.org/#js-unsigned-long-long: a value that is not a finite number, or whose
    /// integer part falls outside the type, is a <c>TypeError</c> rather than a wrap.
    /// </summary>
    /// <remarks>
    /// The result is then clamped to <see cref="int.MaxValue"/> milliseconds — about 24.8 days — before it
    /// reaches the queue, which is the same ceiling <c>setTimeout</c> has and is what keeps the due-time
    /// arithmetic inside a <see cref="long"/>. Nothing can observe the difference: a timer that far out
    /// requires an engine pumped for a month.
    /// </remarks>
    private long EnforceRangeMilliseconds(JsValue milliseconds)
    {
        var number = TypeConverter.ToNumber(milliseconds);
        if (!double.IsFinite(number))
        {
            Throw.TypeError(_realm, "Failed to execute 'timeout' on 'AbortSignal': the value is not a finite number.");
        }

        var integer = Math.Truncate(number);
        if (integer < 0 || integer > 18446744073709551615d)
        {
            Throw.TypeError(_realm, "Failed to execute 'timeout' on 'AbortSignal': the value is outside the range of an unsigned long long.");
        }

        return integer > int.MaxValue ? int.MaxValue : (long) integer;
    }

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    /// <summary>
    /// What a timeout signal's timer runs: signal abort with a fresh <c>TimeoutError</c>
    /// <c>DOMException</c>.
    /// </summary>
    /// <remarks>
    /// An <see cref="ICallable"/> rather than a <c>ClrFunction</c> so that <c>AbortSignal.timeout()</c>
    /// creates no JavaScript function object for something no script can reach — the timer queue only ever
    /// calls it.
    /// </remarks>
    private sealed class TimeoutAbortAlgorithm : ICallable
    {
        private readonly JsAbortSignal _signal;
        private readonly Realm _realm;

        internal TimeoutAbortAlgorithm(JsAbortSignal signal, Realm realm)
        {
            _signal = signal;
            _realm = realm;
        }

        public JsValue Call(JsValue thisObject, params JsCallArguments arguments)
        {
            var reason = _realm.Intrinsics.DomException.CreateException(DomExceptionNames.Timeout, "signal timed out");
            _signal.SignalAbort(reason);
            return JsValue.Undefined;
        }
    }
}
#endif

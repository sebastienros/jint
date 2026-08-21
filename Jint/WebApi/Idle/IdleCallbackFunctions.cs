#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Timers;

namespace Jint.WebApi.Idle;

/// <summary>
/// The two idle-callback globals: <c>requestIdleCallback</c> and <c>cancelIdleCallback</c>.
/// <para>
/// https://w3c.github.io/requestidlecallback/
/// </para>
/// </summary>
/// <remarks>
/// They are global <i>operations</i> rather than members of a namespace object, so this class only owns them
/// so that one realm builds one of each; each is built on first access. What "idle" means for an engine with
/// no frames is documented on <see cref="IdleCallbackQueue"/>, and it is the part a host has to know.
/// </remarks>
internal sealed class IdleCallbackFunctions
{
    private static readonly JsString _timeoutProperty = new("timeout");

    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly IdleCallbackQueue _callbacks;
    private readonly TimerQueue _timers;

    private ClrFunction? _requestIdleCallback;
    private ClrFunction? _cancelIdleCallback;

    private IdleCallbackFunctions(Engine engine, Realm realm, IdleCallbackQueue callbacks, TimerQueue timers)
    {
        _engine = engine;
        _realm = realm;
        _callbacks = callbacks;
        _timers = timers;
    }

    internal static IdleCallbackFunctions Create(Engine engine, Realm realm)
    {
        var state = engine._webApi;
        if (state?.IdleCallbacks is not { } callbacks || state.Timers is not { } timers)
        {
            // Unreachable: the globals that reach this property are installed only where both queues were
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The idle callback globals were reached on an engine that has no idle callback queue.");
            return null!;
        }

        return new IdleCallbackFunctions(engine, realm, callbacks, timers);
    }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-window-requestidlecallback
    /// </summary>
    internal ClrFunction RequestIdleCallback =>
        _requestIdleCallback ??= Operation("requestIdleCallback", 1, (_, arguments) => Request(arguments));

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-window-cancelidlecallback
    /// </summary>
    internal ClrFunction CancelIdleCallback =>
        _cancelIdleCallback ??= Operation("cancelIdleCallback", 1, (_, arguments) => Cancel(arguments));

    /// <summary>
    /// A WebIDL operation: <c>length</c> counts the required arguments only and is configurable but neither
    /// writable nor enumerable — https://webidl.spec.whatwg.org/#dfn-create-operation-function.
    /// </summary>
    private ClrFunction Operation(string name, int length, JsCallDelegate body)
        => new(_engine, _realm, name, body, length, PropertyFlag.Configurable);

    private JsValue Request(JsCallArguments arguments)
    {
        if (arguments.At(0) is not ICallable callback)
        {
            Throw.TypeError(
                _realm,
                "Failed to execute 'requestIdleCallback': the callback provided as parameter 1 is not a function.");
            return JsValue.Undefined;
        }

        var timeout = ReadTimeout(arguments.At(1));

        if (timeout > 0 && _timers.Count >= _timers.MaxActiveTimers)
        {
            // Not a specified failure mode — the specification assumes a browser's resources — but a timeout
            // is a timer, and a script must not be able to register them without bound. Only the timeout half
            // is capped: a callback with no timeout occupies no schedule slot, it simply waits in a list for
            // the next idle period exactly as a queued job waits for the next pump.
            var quotaExceeded = _realm.Intrinsics.QuotaExceededError.CreateException(
                $"Failed to execute 'requestIdleCallback': the engine already has {_timers.MaxActiveTimers} active timers, which is its Options.WebApi.Timers.MaxActiveTimers limit.",
                quota: _timers.RefusalQuota,
                requested: _timers.RefusalRequested);

            var location = _engine._lastSyntaxElement?.Location ?? default;
            Throw.JavaScriptException(_engine, quotaExceeded, in location);
        }

        return JsNumber.Create(_callbacks.Request(callback, timeout));
    }

    /// <summary>
    /// The <c>IdleRequestOptions</c> dictionary,
    /// https://w3c.github.io/requestidlecallback/#dictdef-idlerequestoptions — one member, <c>timeout</c>,
    /// typed plain <c>unsigned long</c>.
    /// </summary>
    /// <remarks>
    /// Plain, so the conversion is ECMAScript's <c>ToUint32</c> and not <c>[EnforceRange]</c>'s refusal:
    /// <c>NaN</c> and the infinities become 0, and a negative value wraps to a very large one — which a
    /// browser does too, and which then clamps to the same <see cref="int.MaxValue"/> millisecond ceiling
    /// <c>setTimeout</c> has, about 24.8 days.
    /// </remarks>
    private long ReadTimeout(JsValue options)
    {
        if (options.IsUndefined() || options.IsNull())
        {
            return 0;
        }

        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to execute 'requestIdleCallback': the provided value is not of type 'IdleRequestOptions'.");
            return 0;
        }

        var value = dictionary.Get(_timeoutProperty);
        if (value.IsUndefined())
        {
            return 0;
        }

        var timeout = (long) TypeConverter.ToUint32(value);
        return timeout > int.MaxValue ? int.MaxValue : timeout;
    }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#dom-window-cancelidlecallback — a handle that is not there
    /// is simply not an error.
    /// </summary>
    private JsValue Cancel(JsCallArguments arguments)
    {
        var handle = TypeConverter.ToUint32(arguments.At(0));
        if (handle <= int.MaxValue)
        {
            // Handles are handed out from 1 upwards and never exceed int.MaxValue, so anything above it names
            // no callback at all and the lookup is skipped rather than wrapped into a handle that does exist.
            _callbacks.Cancel((int) handle);
        }

        return JsValue.Undefined;
    }
}
#endif

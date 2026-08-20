#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;
using Jint.WebApi.Timers;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>scheduler</c> object — an instance of the <c>Scheduler</c> interface.
/// <para>
/// https://wicg.github.io/scheduling-apis/#sec-scheduler
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>postTask()</c> runs a callback as its own event-loop task and answers with a promise for what it
/// returned; <c>yield()</c> answers with a promise that resolves in a fresh task, which is how a long piece of
/// work breaks itself up without losing its place in the queue. Both order themselves by
/// <see cref="SchedulerTaskPriority"/> — see <see cref="SchedulerQueue"/> for the ordering guarantees against
/// microtasks and timers, which are the part a host has to know.
/// </para>
/// <para>
/// <b>Tasks run only while the engine is being pumped</b>, exactly like the timers: the scheduler puts a job
/// on the engine's event loop and Jint never starts a thread to drain it. A <c>delay</c> rides the very same
/// timer queue <c>setTimeout</c> uses, and so counts against
/// <c>Options.WebApi.Timers.MaxActiveTimers</c> while it waits.
/// </para>
/// <para>
/// <b>Neither method ever throws.</b> Both return promises, and WebIDL turns every exception a
/// promise-returning operation would raise — a bad argument, a receiver that is not a scheduler — into a
/// rejection of the returned promise instead ("And then, if an exception E was thrown: if op has a return type
/// that is a promise type, then return ! Call(%Promise.reject%, %Promise%, «E»)",
/// https://webidl.spec.whatwg.org/#dfn-create-operation-function).
/// </para>
/// <para>
/// Two documented simplifications against WebIDL, the same pair <c>console</c>, <c>crypto</c> and
/// <c>performance</c> carry. There is no <c>Scheduler</c> interface object and no <c>Scheduler.prototype</c>,
/// so <c>postTask</c> and <c>yield</c> are own properties of this object with the attributes an ECMAScript
/// built-in has; both still brand-check their receiver. And the object is installed as an ordinary enumerable
/// data property of the global rather than through the <c>[Replaceable]</c> accessor pair
/// https://wicg.github.io/scheduling-apis/#dom-windoworworkerglobalscope-scheduler gives it.
/// </para>
/// </remarks>
[JsObject]
internal sealed partial class SchedulerInstance : BuiltinShapeObject
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString SchedulerToStringTag = new("Scheduler");

    private static readonly JsString _delayProperty = new("delay");
    private static readonly JsString _priorityProperty = new("priority");
    private static readonly JsString _signalProperty = new("signal");

    private readonly Realm _realm;
    private readonly SchedulerQueue _tasks;
    private readonly TimerQueue _timers;

    private SchedulerInstance(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype,
        SchedulerQueue tasks,
        TimerQueue timers)
        : base(engine)
    {
        _realm = realm;
        _tasks = tasks;
        _timers = timers;
        _prototype = objectPrototype;
    }

    internal static SchedulerInstance Create(Engine engine, Realm realm, ObjectPrototype objectPrototype)
    {
        var state = engine._webApi;
        if (state?.Scheduler is not { } tasks || state.Timers is not { } timers)
        {
            // Unreachable: the global that reaches this property is installed only where both queues were
            // created, in the same block of WebApiRegistration.
            Throw.InvalidOperationException("The scheduler object was reached on an engine that has no scheduler queue.");
            return null!;
        }

        return new SchedulerInstance(engine, realm, objectPrototype, tasks, timers);
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-scheduler-posttask — "the postTask(callback, options)
    /// method steps are to return the result of scheduling a postTask task for this given callback and
    /// options", https://wicg.github.io/scheduling-apis/#schedule-a-posttask-task.
    /// </summary>
    [JsFunction(Name = "postTask", Length = 1)]
    private JsValue PostTask(JsValue thisObject, JsValue callback, JsValue options)
    {
        // Step 1: the promise exists before anything can fail, because everything that can fail from here on
        // rejects it rather than throwing.
        var capability = NewPromiseCapability();

        try
        {
            Brand(thisObject, "Failed to execute 'postTask' on 'Scheduler'");

            if (callback is not ICallable callable)
            {
                Throw.TypeError(_realm, "Failed to execute 'postTask' on 'Scheduler': the callback provided as parameter 1 is not a function.");
                return capability.PromiseInstance;
            }

            var (delay, priority, signal) = ReadPostTaskOptions(options);

            // Step 3: an already-aborted signal means the task never exists at all.
            if (signal is { Aborted: true })
            {
                capability.Reject(signal.Reason);
                return capability.PromiseInstance;
            }

            // Steps 4 to 8: the priority option wins and is immutable; otherwise a TaskSignal governs the
            // priority dynamically; otherwise user-visible.
            var prioritySource = priority is null ? signal as JsTaskSignal : null;
            var state = new SchedulingState(signal, prioritySource, priority ?? SchedulerTaskPriority.UserVisible);

            // Steps 9 and 10.
            var task = new SchedulerTask(_tasks, capability, callable, in state, isContinuation: false);
            task.RegisterAbortSteps();

            // Steps 12 to 14: a delay defers the *enqueueing*, so the task takes its enqueue order — and
            // therefore its place among equal-priority tasks — from the moment the delay elapses, not from
            // the moment postTask was called.
            if (delay > 0)
            {
                ScheduleAfterDelay(task, delay);
            }
            else
            {
                _tasks.Enqueue(task);
            }
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-scheduler-yield — "the yield() method steps are to return
    /// the result of scheduling a yield continuation for this",
    /// https://wicg.github.io/scheduling-apis/#schedule-a-yield-continuation.
    /// </summary>
    /// <remarks>
    /// The continuation inherits the abort source and the priority source of the task it is called in, and a
    /// continuation outranks a task of the same priority — which is what makes a chunked piece of work resume
    /// ahead of the work queued behind it. See <see cref="SchedulerQueue.CurrentState"/> for how far that
    /// inheritance reaches here.
    /// </remarks>
    [JsFunction(Name = "yield", Length = 0)]
    private JsValue Yield(JsValue thisObject)
    {
        var capability = NewPromiseCapability();

        try
        {
            Brand(thisObject, "Failed to execute 'yield' on 'Scheduler'");

            // Steps 2 to 6: the inherited state, or user-visible with nothing to abort it.
            var inherited = _tasks.CurrentState ?? new SchedulingState(null, null, SchedulerTaskPriority.UserVisible);

            // Step 4.
            if (inherited.AbortSource is { Aborted: true } abortSource)
            {
                capability.Reject(abortSource.Reason);
                return capability.PromiseInstance;
            }

            // Steps 7 to 10: no callback — the task's only step is to resolve the promise — and the queue is
            // the continuation one, whose effective priority is a notch above the task queue beside it.
            var task = new SchedulerTask(_tasks, capability, callback: null, in inherited, isContinuation: true);
            task.RegisterAbortSteps();
            _tasks.Enqueue(task);
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// Step 13 of https://wicg.github.io/scheduling-apis/#schedule-a-posttask-task, "run steps after a timeout
    /// … given delay": the wait is an entry on the engine's own timer queue, so it elapses only while the
    /// engine is being pumped and it occupies one of the engine's timer slots until then.
    /// </summary>
    private void ScheduleAfterDelay(SchedulerTask task, long delay)
    {
        if (_timers.Count >= _timers.MaxActiveTimers)
        {
            // Not a specified failure mode — the specification assumes a browser's resources — but a delayed
            // task is a timer, and a script must not be able to register them without bound.
            ThrowDomException(
                DomExceptionNames.QuotaExceeded,
                $"Failed to execute 'postTask' on 'Scheduler': the engine already has {_timers.MaxActiveTimers} active timers, which is its Options.WebApi.Timers.MaxActiveTimers limit.");
        }

        var entry = new TimerEntry(_timers, new DelayedEnqueue(_tasks, task), [], delay, repeat: false, _engine.EventLoopGeneration);
        task.SetDelayTimer(_timers, _timers.Schedule(entry));
    }

    /// <summary>
    /// The <c>SchedulerPostTaskOptions</c> dictionary,
    /// https://wicg.github.io/scheduling-apis/#dictdef-schedulerposttaskoptions.
    /// </summary>
    /// <remarks>
    /// The members are read in lexicographical order — <c>delay</c>, <c>priority</c>, <c>signal</c> — which is
    /// the order https://webidl.spec.whatwg.org/#es-dictionary specifies and not the order the IDL declares
    /// them in. It is observable: an options object whose getters throw, or whose members are each invalid,
    /// reports the first failure in that order.
    /// </remarks>
    private (long Delay, SchedulerTaskPriority? Priority, JsAbortSignal? Signal) ReadPostTaskOptions(JsValue options)
    {
        const string What = "Failed to execute 'postTask' on 'Scheduler'";

        if (options.IsUndefined() || options.IsNull())
        {
            return (0, null, null);
        }

        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, $"{What}: the provided value is not of type 'SchedulerPostTaskOptions'.");
            return default;
        }

        var delayValue = dictionary.Get(_delayProperty);
        var delay = delayValue.IsUndefined() ? 0 : EnforceRangeMilliseconds(delayValue, What);

        var priorityValue = dictionary.Get(_priorityProperty);
        SchedulerTaskPriority? priority = priorityValue.IsUndefined()
            ? null
            : TaskPriorityNames.Parse(_realm, priorityValue, What);

        var signalValue = dictionary.Get(_signalProperty);
        JsAbortSignal? signal = null;
        if (!signalValue.IsUndefined())
        {
            // The member is typed AbortSignal, not AbortSignal?, so null is a TypeError rather than "no
            // signal" — https://webidl.spec.whatwg.org/#es-interface.
            if (signalValue is not JsAbortSignal abortSignal)
            {
                Throw.TypeError(_realm, $"{What}: member signal is not of type 'AbortSignal'.");
                return default;
            }

            signal = abortSignal;
        }

        return (delay, priority, signal);
    }

    /// <summary>
    /// The <c>[EnforceRange] unsigned long long</c> conversion,
    /// https://webidl.spec.whatwg.org/#js-unsigned-long-long: a value that is not a finite number, or whose
    /// integer part falls outside the type, is a <c>TypeError</c> rather than a wrap.
    /// </summary>
    /// <remarks>
    /// The result is then clamped to <see cref="int.MaxValue"/> milliseconds — about 24.8 days — before it
    /// reaches the timer queue, the same ceiling <c>setTimeout</c> and <c>AbortSignal.timeout()</c> have.
    /// </remarks>
    private long EnforceRangeMilliseconds(JsValue value, string what)
    {
        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            Throw.TypeError(_realm, $"{what}: member delay is not a finite number.");
        }

        var integer = Math.Truncate(number);
        if (integer < 0 || integer > 18446744073709551615d)
        {
            Throw.TypeError(_realm, $"{what}: member delay is outside the range of an unsigned long long.");
        }

        return integer > int.MaxValue ? int.MaxValue : (long) integer;
    }

    private PromiseCapability NewPromiseCapability()
        => PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing the
    /// interface raises a <c>TypeError</c> — which, for these two, the caller turns into a rejection.
    /// </summary>
    private void Brand(JsValue thisObject, string what)
    {
        if (thisObject is not SchedulerInstance)
        {
            Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a Scheduler object.");
        }
    }

    /// <summary>
    /// What a delayed <c>postTask</c>'s timer runs: enqueue the task, unless it was aborted while it waited.
    /// </summary>
    /// <remarks>
    /// An <see cref="ICallable"/> rather than a <c>ClrFunction</c> so that a delayed task creates no
    /// JavaScript function object for something no script can reach — the timer queue only ever calls it.
    /// </remarks>
    private sealed class DelayedEnqueue : ICallable
    {
        private readonly SchedulerQueue _scheduler;
        private readonly SchedulerTask _task;

        internal DelayedEnqueue(SchedulerQueue scheduler, SchedulerTask task)
        {
            _scheduler = scheduler;
            _task = task;
        }

        public JsValue Call(JsValue thisObject, params JsCallArguments arguments)
        {
            // "If signal is null or signal is not aborted, then run enqueueSteps": an abort during the delay
            // has already rejected the promise, and there is nothing left to schedule.
            if (!_task.Cancelled)
            {
                _scheduler.Enqueue(_task);
            }

            return JsValue.Undefined;
        }
    }
}
#endif

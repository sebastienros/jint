#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Timers;

namespace Jint.WebApi.Idle;

/// <summary>
/// One engine's idle callbacks: the specification's <i>list of idle request callbacks</i> and <i>list of
/// runnable idle callbacks</i>, plus the idle period they are run in.
/// <para>
/// https://w3c.github.io/requestidlecallback/
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Engine-thread only, and deliberately lock-free</b>, like the timer and scheduler queues beside it.
/// </para>
/// <para>
/// <b>What "idle" means here, stated honestly.</b> The specification is written for a browser, where an idle
/// period is the slack between finishing a frame and having to start the next one, and the deadline is when
/// the next frame is due. Jint has no frames and no display; the only thing that resembles one is the host's
/// own pump. So the mapping is:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>An idle period starts when a pump runs out of everything else.</b> A callback is promoted only at the
/// moment the event-loop job queue has drained <i>and</i> no timer is due — the same exhaustion point a timer
/// is promoted at, taken after it. Every promise reaction, every <c>scheduler.postTask</c> task (whose drain
/// job keeps the job queue non-empty while any remain, at every priority including <c>background</c>) and
/// every due timer therefore runs first. That is the lowest band the engine has, which is where the standard
/// puts these callbacks.
/// </description></item>
/// <item><description>
/// <b>The deadline is a fixed budget from the start of the period</b>, <c>Options.WebApi.Timers.IdleBudget</c>,
/// 50 ms by default — the ceiling the standard itself recommends ("capping idle deadlines to 50ms"). When the
/// budget runs out the period ends and the pump returns to the host, so one pump spends at most one budget on
/// idle work however many callbacks are waiting; the rest run on the next pump. A budget of zero or less says
/// the host has no idle time at all, and then <i>only</i> the <c>timeout</c> option ever runs a callback.
/// </description></item>
/// <item><description>
/// <b>A callback requested while a period is running waits for the next one</b>, because starting a period is
/// what moves the pending list into the runnable list — the specification's own arrangement, and what stops
/// <c>requestIdleCallback</c> re-arming itself from monopolising the engine.
/// </description></item>
/// </list>
/// <para>
/// <b>Callbacks run only while the engine is being pumped</b>, exactly as timers and scheduler tasks do. An
/// engine nobody pumps runs none of them, and a callback that throws erupts out of whatever was pumping — the
/// same contract a timer callback has. The standard says to <i>report</i> such an exception, which presumes
/// the <c>reportError</c> channel Jint does not have yet.
/// </para>
/// </remarks>
internal sealed class IdleCallbackQueue
{
    private readonly Engine _engine;
    private readonly Realm _realm;
    private readonly TimeProvider _timeProvider;
    private readonly TimerQueue _timers;

    /// <summary>
    /// The idle budget in <see cref="TimeProvider.GetTimestamp"/> ticks. Zero means the host declared that it
    /// has no idle time, and no idle period is ever started.
    /// </summary>
    private readonly long _budgetTicks;

    /// <summary>https://w3c.github.io/requestidlecallback/#dfn-list-of-idle-request-callbacks.</summary>
    private readonly Queue<IdleCallbackEntry> _pending = new();

    /// <summary>https://w3c.github.io/requestidlecallback/#dfn-list-of-runnable-idle-callbacks.</summary>
    private readonly Queue<IdleCallbackEntry> _runnable = new();

    /// <summary>
    /// Every live callback by handle — the thing <c>cancelIdleCallback</c> looks in, and the exact count of
    /// callbacks that still have to run. A cancelled or already-run entry is removed from here at once and
    /// discarded from whichever queue holds it when it next surfaces, which is what makes cancellation O(1).
    /// </summary>
    private readonly Dictionary<int, IdleCallbackEntry> _byHandle = new();

    /// <summary>https://w3c.github.io/requestidlecallback/#dfn-idle-callback-identifier, monotonic from 1.</summary>
    private int _nextHandle = 1;

    private bool _periodActive;
    private long _periodDeadline;

    internal IdleCallbackQueue(Engine engine, Realm realm, TimeProvider timeProvider, TimerQueue timers, TimeSpan idleBudget)
    {
        _engine = engine;
        _realm = realm;
        _timeProvider = timeProvider;
        _timers = timers;
        _budgetTicks = idleBudget > TimeSpan.Zero
            ? (long) (idleBudget.TotalSeconds * timeProvider.TimestampFrequency)
            : 0;
    }

    /// <summary>
    /// Whether any callback is still waiting to run <i>and</i> could ever run in an idle period. False when the
    /// budget is non-positive, because then nothing but a <c>timeout</c> can reach a callback and reporting
    /// "work is available now" would spin a host loop that believes it.
    /// </summary>
    internal bool HasPendingWork => _budgetTicks > 0 && _byHandle.Count > 0;

    /// <summary>
    /// The request idle callback algorithm,
    /// https://w3c.github.io/requestidlecallback/#the-requestidlecallback-method: take the next identifier,
    /// append the callback to the list, and — when a positive <c>timeout</c> was given — arrange for the
    /// timeout algorithm to run it if no idle period reaches it first.
    /// </summary>
    /// <returns>The handle, which is what <c>requestIdleCallback</c> returns.</returns>
    internal int Request(ICallable callback, long timeout)
    {
        var handle = _nextHandle;
        _nextHandle = handle == int.MaxValue ? 1 : handle + 1;

        var entry = new IdleCallbackEntry(handle, callback);
        _byHandle[handle] = entry;
        _pending.Enqueue(entry);

        if (timeout > 0)
        {
            // "Wait for timeout milliseconds … queue a task on the queue associated with the idle-task task
            // source … to run the invoke idle callback timeout algorithm". The wait is an entry on the
            // engine's own timer queue, so it elapses only while the engine is being pumped and occupies one
            // of the engine's timer slots until then.
            var timerEntry = new TimerEntry(
                _timers,
                new IdleTimeoutAlgorithm(this, entry),
                [],
                timeout,
                repeat: false,
                _engine.EventLoopGeneration);

            entry.TimeoutTimerId = _timers.Schedule(timerEntry);
        }

        return handle;
    }

    /// <summary>
    /// https://w3c.github.io/requestidlecallback/#the-cancelidlecallback-method: remove the entry with this
    /// handle from both lists. An unknown handle is silently ignored, as the algorithm's "if there is such an
    /// entry" implies.
    /// </summary>
    internal void Cancel(int handle)
    {
        if (_byHandle.Remove(handle, out var entry))
        {
            Retire(entry);
        }
    }

    /// <summary>
    /// The one thing the pump asks of this queue: run a single idle callback if there is one and the budget
    /// allows, otherwise report that there is nothing to do. Called from <c>Engine.TryPromoteDeferredWork</c>
    /// once the job queue has drained and no timer is due.
    /// </summary>
    /// <remarks>
    /// One callback per call, like the timers, so that everything a callback queues — promise reactions,
    /// <c>queueMicrotask</c>, another task — runs before the next callback is even looked at, and so that a
    /// timer that came due meanwhile still wins the next round.
    /// <para>
    /// Returning <see langword="false"/> ends the pump, which is precisely what makes the budget mean
    /// something: it is the boundary between one host pump and the next, and the only pump boundary the engine
    /// has.
    /// </para>
    /// </remarks>
    /// <returns>Whether a callback was run, i.e. whether the pump has more work to do.</returns>
    internal bool TryRunIdleCallback()
    {
        if (_byHandle.Count == 0)
        {
            _periodActive = false;
            return false;
        }

        if (_budgetTicks <= 0)
        {
            // The host declared that it never has idle time; only the timeout option reaches a callback.
            return false;
        }

        var now = _timeProvider.GetTimestamp();

        if (!_periodActive)
        {
            // The start an idle period algorithm, https://w3c.github.io/requestidlecallback/#start-an-idle-period-algorithm:
            // "for each entry in window's list of idle request callbacks, append entry to window's list of
            // runnable idle callbacks; clear window's list of idle request callbacks".
            while (_pending.TryDequeue(out var moved))
            {
                _runnable.Enqueue(moved);
            }

            _periodDeadline = now + _budgetTicks;
            _periodActive = true;
        }
        else if (now >= _periodDeadline)
        {
            // "While now is less than deadline" no longer holds: the period is over, and whatever is left
            // runs in the next one.
            _periodActive = false;
            return false;
        }

        var entry = TakeRunnable();
        if (entry is null)
        {
            // Everything runnable has run. Anything requested during this period is in the pending list and
            // deliberately waits for the next one.
            _periodActive = false;
            return false;
        }

        Invoke(entry, _periodDeadline, didTimeout: false);
        return true;
    }

    /// <summary>
    /// Forgets every callback. Called from <c>Engine.ResetTransientEvaluationState</c>, so a callback requested
    /// by one evaluation cycle can never run against the globals a <c>RestoreGlobalSnapshot</c> put back.
    /// </summary>
    internal void Clear()
    {
        foreach (var entry in _byHandle.Values)
        {
            // The timers are cleared by the same reset, so cancelling here is belt to that braces — and it is
            // what keeps a timeout from being charged against the next cycle's timer quota.
            entry.Cancelled = true;
            CancelTimeoutTimer(entry);
        }

        _byHandle.Clear();
        _pending.Clear();
        _runnable.Clear();
        _periodActive = false;
    }

    /// <summary>
    /// The invoke idle callback timeout algorithm,
    /// https://w3c.github.io/requestidlecallback/#invoke-idle-callback-timeout-algorithm: remove the entry from
    /// both lists and run it with a deadline of <i>now</i> — so its <c>timeRemaining()</c> is zero — and
    /// <c>didTimeout</c> true.
    /// </summary>
    private void RunTimedOut(IdleCallbackEntry entry)
    {
        if (entry.Cancelled)
        {
            // Cancelled, or already run by an idle period that reached it first.
            return;
        }

        _byHandle.Remove(entry.Handle);
        entry.Cancelled = true;

        // The timer that brought us here has already fired, so there is nothing to cancel; clearing the id
        // keeps Retire from asking the queue about an id it no longer owns.
        entry.TimeoutTimerId = 0;

        Invoke(entry, _timeProvider.GetTimestamp(), didTimeout: true);
    }

    /// <summary>
    /// The queue's first entry that has not been cancelled, discarding the marked ones on the way — the other
    /// half of <see cref="Cancel"/> being O(1).
    /// </summary>
    private IdleCallbackEntry? TakeRunnable()
    {
        while (_runnable.TryDequeue(out var candidate))
        {
            if (candidate.Cancelled)
            {
                continue;
            }

            // Taken out of the live set before it runs, so a callback that cancels its own handle from inside
            // itself is the no-op it should be.
            _byHandle.Remove(candidate.Handle);
            candidate.Cancelled = true;
            CancelTimeoutTimer(candidate);
            return candidate;
        }

        return null;
    }

    private void Invoke(IdleCallbackEntry entry, long deadlineTimestamp, bool didTimeout)
    {
        var deadline = new JsIdleDeadline(
            _engine,
            _realm.Intrinsics.IdleDeadline.PrototypeObject,
            _timeProvider,
            deadlineTimestamp,
            didTimeout);

        entry.Callback.Call(JsValue.Undefined, [deadline]);
    }

    private void Retire(IdleCallbackEntry entry)
    {
        entry.Cancelled = true;
        CancelTimeoutTimer(entry);
    }

    private void CancelTimeoutTimer(IdleCallbackEntry entry)
    {
        if (entry.TimeoutTimerId != 0)
        {
            _timers.Cancel(entry.TimeoutTimerId);
            entry.TimeoutTimerId = 0;
        }
    }

    /// <summary>
    /// What a <c>timeout</c> option's timer runs. An <see cref="ICallable"/> rather than a <c>ClrFunction</c>
    /// so that a timeout creates no JavaScript function object for something no script can reach — the timer
    /// queue only ever calls it.
    /// </summary>
    private sealed class IdleTimeoutAlgorithm : ICallable
    {
        private readonly IdleCallbackQueue _queue;
        private readonly IdleCallbackEntry _entry;

        internal IdleTimeoutAlgorithm(IdleCallbackQueue queue, IdleCallbackEntry entry)
        {
            _queue = queue;
            _entry = entry;
        }

        public JsValue Call(JsValue thisObject, params JsCallArguments arguments)
        {
            _queue.RunTimedOut(_entry);
            return JsValue.Undefined;
        }
    }
}

/// <summary>
/// One entry of https://w3c.github.io/requestidlecallback/#dfn-list-of-idle-request-callbacks: a handle, the
/// callback, and the timer a <c>timeout</c> option armed.
/// </summary>
internal sealed class IdleCallbackEntry
{
    internal IdleCallbackEntry(int handle, ICallable callback)
    {
        Handle = handle;
        Callback = callback;
    }

    /// <summary>The value <c>requestIdleCallback</c> returned, and what <c>cancelIdleCallback</c> names.</summary>
    internal int Handle { get; }

    internal ICallable Callback { get; }

    /// <summary>
    /// Set once the entry has left the live set, whether because it was cancelled, because it ran, or because
    /// the evaluation cycle ended. The two queues discard a marked entry when it surfaces.
    /// </summary>
    internal bool Cancelled { get; set; }

    /// <summary>The timer id of the <c>timeout</c> option, or zero when none was given.</summary>
    internal int TimeoutTimerId { get; set; }
}
#endif

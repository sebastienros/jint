#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Timers;

/// <summary>
/// The engine's active timers: HTML's <i>map of setTimeout and setInterval IDs</i> plus the schedule that
/// decides which of them is due next.
/// <para>
/// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timers
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Engine-thread only, and deliberately lock-free.</b> Every member is reached from the engine's pump or
/// from a timer global running on it, which is the same single-thread contract the rest of the engine has.
/// Nothing here starts a thread or a <see cref="System.Threading.Timer"/>: time is read from a
/// <see cref="TimeProvider"/> with <see cref="TimeProvider.GetTimestamp"/>, never
/// <see cref="TimeProvider.CreateTimer"/>. A timer therefore fires on the next pump at or after its due
/// time, and an engine nobody pumps never fires one at all — see <c>Engine.TryPromoteDueTimerJob</c>.
/// </para>
/// <para>
/// The schedule is a min-heap keyed by <see cref="TimerDue"/>, which pairs the due timestamp with a
/// registration sequence number so that timers due at the same instant fire in the order they were
/// registered — the order HTML's task queue gives them, and the one a <c>setTimeout(a, 0)</c>,
/// <c>setTimeout(b, 0)</c> pair is written expecting. Cancelling only marks the entry and drops it from the
/// id map; the heap copy is discarded when it next surfaces, so <c>clearTimeout</c> is O(1) and a cancelled
/// timer can never be promoted.
/// </para>
/// </remarks>
internal sealed class TimerQueue
{
    private readonly TimeProvider _timeProvider;
    private readonly PriorityQueue<TimerEntry, TimerDue> _schedule = new();
    private readonly Dictionary<int, TimerEntry> _active = new();

    /// <summary>Handed out by <see cref="Schedule"/>; monotonic within an engine, starting at 1.</summary>
    private int _nextId = 1;

    /// <summary>Breaks ties between timers due at the same instant, so equal due times fire FIFO.</summary>
    private long _nextSequence;

    internal TimerQueue(Engine engine, TimeProvider timeProvider, int maxActiveTimers, DiagnosticsSink? diagnostics)
    {
        Engine = engine;
        _timeProvider = timeProvider;
        MaxActiveTimers = maxActiveTimers;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// The engine these timers belong to. Reached from <see cref="TimerEntry.Fire"/> alone, and only on the
    /// path where a callback threw — the web-API state it leads to is where HTML's <i>report an exception</i>
    /// finds a global <c>error</c> listener, if there is one.
    /// </summary>
    internal Engine Engine { get; }

    /// <summary>
    /// The most timers that may be registered at once, from <c>Options.WebApi.Timers.MaxActiveTimers</c>,
    /// read once when the engine was built.
    /// </summary>
    internal int MaxActiveTimers { get; }

    /// <summary>How many timers are registered right now, which is what <see cref="MaxActiveTimers"/> caps.</summary>
    internal int Count => _active.Count;

    /// <summary>
    /// The <c>quota</c> a refused registration reports, https://webidl.spec.whatwg.org/#quotaexceedederror:
    /// how many timers this engine permits at once.
    /// </summary>
    /// <remarks>
    /// Clamped at zero rather than reported raw, because <c>MaxActiveTimers</c> is documented to refuse every
    /// timer at "zero or less" and the interface's own constructor refuses a negative <c>quota</c> with a
    /// <c>RangeError</c>. Zero is not a rounding of −5 either: the number of timers a −5 cap permits <i>is</i>
    /// none.
    /// </remarks>
    internal double RefusalQuota => Math.Max(0, MaxActiveTimers);

    /// <summary>
    /// The <c>requested</c> a refused registration reports: the count this engine would have carried had the
    /// registration been allowed, which is always at least <see cref="RefusalQuota"/> — the invariant
    /// https://webidl.spec.whatwg.org/#quotaexceedederror imposes on anything that throws one.
    /// </summary>
    internal double RefusalRequested => (double) Count + 1;

    /// <summary>
    /// Where a callback's exception is reported, or <see langword="null"/> when the host set no sink and the
    /// exception must therefore erupt instead. Held here rather than reached through the engine so that
    /// <see cref="TimerEntry.Fire"/> costs one field read and an entry costs no extra reference at all.
    /// </summary>
    internal DiagnosticsSink? Diagnostics { get; }

    /// <summary>
    /// HTML's <i>timer nesting level</i>, https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timer-nesting-level.
    /// Zero outside a timer callback; while one runs it is that timer's own level, which is what makes a
    /// chain of <c>setTimeout(f, 0)</c> calls clamp to 4ms once it is more than five deep.
    /// </summary>
    internal int NestingLevel { get; set; }

    /// <summary>
    /// The timer initialization steps' first run for an entry: assigns the id, applies the nesting clamp and
    /// puts the entry on the schedule.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timer-initialisation-steps
    /// </para>
    /// </summary>
    /// <returns>The id, which is what <c>setTimeout</c> and <c>setInterval</c> return.</returns>
    internal int Schedule(TimerEntry entry)
    {
        var id = NextId();
        entry.Id = id;
        _active[id] = entry;
        Arm(entry);
        return id;
    }

    /// <summary>
    /// Step 8.2 of the task an interval runs: the timer initialization steps again, with the same id and the
    /// timeout the script originally asked for. Called from <see cref="TimerEntry.Fire"/> <em>before</em> the
    /// callback runs, so a callback that throws does not stop the interval — which is exactly the order the
    /// specification puts those two substeps in.
    /// </summary>
    internal void Reschedule(TimerEntry entry) => Arm(entry);

    /// <summary>
    /// The steps common to both: clamp against the current nesting level, remember the level the resulting
    /// task runs at, and push. The clamp reads <see cref="TimerEntry.RequestedDelay"/> rather than the last
    /// effective delay, because the specification re-runs the initialization steps with the original
    /// <c>timeout</c> argument every time.
    /// </summary>
    private void Arm(TimerEntry entry)
    {
        var nestingLevel = NestingLevel;

        // "If nesting level is greater than 5, and timeout is less than 4, then set timeout to 4."
        var delay = entry.RequestedDelay;
        if (nestingLevel > 5 && delay < 4)
        {
            delay = 4;
        }

        // "Increment nesting level by one. Let task's timer nesting level be nesting level." Saturated
        // rather than wrapped: above 5 the level changes nothing, and an interval that fires two billion
        // times must not wrap its way back under the clamp.
        entry.NestingLevel = nestingLevel < 64 ? nestingLevel + 1 : nestingLevel;

        var due = _timeProvider.GetTimestamp() + (long) (delay * (_timeProvider.TimestampFrequency / 1000.0));
        _schedule.Enqueue(entry, new TimerDue(due, _nextSequence++));
    }

    /// <summary>
    /// <c>clearTimeout</c> / <c>clearInterval</c>: removes the id from the map of active timers. The entry
    /// stays in the schedule, marked, and is discarded when it surfaces — an id that is not registered is
    /// silently ignored, which is what both operations are specified to do.
    /// </summary>
    internal void Cancel(int id)
    {
        if (_active.Remove(id, out var entry))
        {
            entry.Cancelled = true;
        }
    }

    /// <summary>
    /// Takes the next timer that is due now, if any. Cancelled entries in front of it are discarded on the
    /// way, which is the other half of <see cref="Cancel"/> being O(1).
    /// </summary>
    internal bool TryTakeDue([NotNullWhen(true)] out TimerEntry? entry)
    {
        var now = _timeProvider.GetTimestamp();

        while (_schedule.TryPeek(out var candidate, out var due))
        {
            if (candidate.Cancelled)
            {
                _schedule.Dequeue();
                continue;
            }

            if (due.Timestamp > now)
            {
                break;
            }

            _schedule.Dequeue();

            // "If repeat is false, then remove global's map of active timers[id]" — before the handler runs,
            // so a one-shot timer clearing its own id from inside its callback is the no-op it should be.
            if (!candidate.Repeat)
            {
                _active.Remove(candidate.Id);
            }

            entry = candidate;
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// How long until the next timer comes due: <see langword="null"/> when none is scheduled, and zero or
    /// negative when one is due already. The wait loops use it to bound an otherwise idle sleep, since a
    /// timer coming due enqueues nothing and so wakes nobody.
    /// </summary>
    internal TimeSpan? TimeUntilNextDue()
    {
        while (_schedule.TryPeek(out var candidate, out var due))
        {
            if (candidate.Cancelled)
            {
                _schedule.Dequeue();
                continue;
            }

            return _timeProvider.GetElapsedTime(_timeProvider.GetTimestamp(), due.Timestamp);
        }

        return null;
    }

    /// <summary>
    /// Forgets every timer. Called from <c>Engine.ResetTransientEvaluationState</c>, so a timer registered by
    /// one evaluation cycle can never fire into the globals a <c>RestoreGlobalSnapshot</c> put back. The
    /// entries are marked as well as dropped, so one already promoted into an event-loop job is a no-op even
    /// before the generation fence reaches it.
    /// </summary>
    internal void Clear()
    {
        foreach (var entry in _active.Values)
        {
            entry.Cancelled = true;
        }

        _active.Clear();
        _schedule.Clear();
        NestingLevel = 0;
    }

    private int NextId()
    {
        // The wrap needs 2^31 registrations on one engine and is unreachable in practice; the probe is what
        // keeps an id unique against the timers still registered if it ever is reached.
        while (_active.ContainsKey(_nextId))
        {
            _nextId = _nextId == int.MaxValue ? 1 : _nextId + 1;
        }

        var id = _nextId;
        _nextId = id == int.MaxValue ? 1 : id + 1;
        return id;
    }
}

/// <summary>
/// A scheduled timer's position in the queue: when it is due, and — for timers due at the same instant — the
/// order they were registered in.
/// </summary>
/// <param name="Timestamp">A <see cref="TimeProvider.GetTimestamp"/> reading, so monotonic and clock-change proof.</param>
/// <param name="Sequence">Registration order, which makes equal due times fire FIFO.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TimerDue(long Timestamp, long Sequence) : IComparable<TimerDue>
{
    public int CompareTo(TimerDue other)
    {
        var byDueTime = Timestamp.CompareTo(other.Timestamp);
        return byDueTime != 0 ? byDueTime : Sequence.CompareTo(other.Sequence);
    }
}

/// <summary>
/// One registered timer: the callback, the arguments to hand it, and the bookkeeping the schedule needs.
/// </summary>
/// <remarks>
/// The entry is also the event-loop job: <see cref="Job"/> is a delegate created once per timer rather than
/// once per firing, so an interval that runs a million times allocates one.
/// </remarks>
internal sealed class TimerEntry
{
    private readonly TimerQueue _queue;
    private readonly ICallable _callback;
    private readonly JsValue[] _arguments;
    private Action? _job;

    internal TimerEntry(
        TimerQueue queue,
        ICallable callback,
        JsValue[] arguments,
        long requestedDelay,
        bool repeat,
        int generation)
    {
        _queue = queue;
        _callback = callback;
        _arguments = arguments;
        RequestedDelay = requestedDelay;
        Repeat = repeat;
        Generation = generation;
    }

    /// <summary>The id <c>setTimeout</c> returned, assigned by <see cref="TimerQueue.Schedule"/>.</summary>
    internal int Id { get; set; }

    /// <summary>The <c>timeout</c> argument as coerced, in milliseconds; the nesting clamp is applied to a copy.</summary>
    internal long RequestedDelay { get; }

    /// <summary>Whether this is a <c>setInterval</c>, i.e. re-armed when it fires.</summary>
    internal bool Repeat { get; }

    /// <summary>The nesting level the callback runs at; see <see cref="TimerQueue.NestingLevel"/>.</summary>
    internal int NestingLevel { get; set; }

    /// <summary>
    /// The evaluation cycle this timer was registered in. The promoted job carries it, so a timer surviving
    /// into a cycle a <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> started is dropped at
    /// dequeue even if something bypassed <see cref="TimerQueue.Clear"/>.
    /// </summary>
    internal int Generation { get; }

    /// <summary>Set by <see cref="TimerQueue.Cancel"/>; the schedule discards a marked entry lazily.</summary>
    internal bool Cancelled { get; set; }

    /// <summary>The event-loop job that runs this timer's callback, allocated once and reused by an interval.</summary>
    internal Action Job => _job ??= Fire;

    /// <summary>
    /// The task HTML's timer initialization steps queue, step 8.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#dom-settimeout
    /// </para>
    /// </summary>
    private void Fire()
    {
        // Step 8.1: the id may have been cleared between this job being promoted and it being run.
        if (Cancelled)
        {
            return;
        }

        var previousNestingLevel = _queue.NestingLevel;
        _queue.NestingLevel = NestingLevel;
        try
        {
            // Step 8.2 before step 8.4: the interval is re-armed before the callback runs, so an exception
            // escaping the callback does not silently stop it.
            if (Repeat)
            {
                _queue.Reschedule(this);
            }

            // Step 8.4: "If handler is a Function, then invoke handler given arguments and "report"". WebIDL's
            // "report" exception behavior is to report the exception and return undefined, which is what the
            // catch below does whenever the host gave the engine somewhere to report to.
            _callback.Call(JsValue.Undefined, _arguments);
        }
        catch (JavaScriptException exception) when (_queue.Diagnostics is { } diagnostics)
        {
            // WebIDL's "report" behavior is HTML's report an exception, whose step 5 fires an `error` event at
            // the global scope before step 6 reaches the console. A no-op unless the GlobalEvents feature is on
            // and a script is listening; see WebApiEngineState.FireGlobalErrorEvent.
            _queue.Engine._webApi?.FireGlobalErrorEvent(exception);

            // Only a JavaScriptException, which is exactly the class of failure a script could have caught
            // itself. Everything that exists to bound execution — ExecutionCanceledException,
            // TimeoutException, the statement, memory and recursion budgets — is a JintException but not a
            // JavaScriptException, so none of it is caught here and a constraint still stops the engine. With
            // no sink there is no catch at all and the throw erupts out of whatever is pumping the event
            // loop, exactly as one from a promise reaction handler without a capability does; everything still
            // queued runs on the next pump either way.
            diagnostics.Report(DiagnosticEvent.ForUncaughtCallbackError(exception, DiagnosticCallbackSource.Timer));
        }
        finally
        {
            _queue.NestingLevel = previousNestingLevel;
        }
    }
}
#endif

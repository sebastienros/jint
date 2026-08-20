#if NET8_0_OR_GREATER
using Jint.WebApi.Fetch;
using Jint.WebApi.Scheduling;
using Jint.WebApi.Timers;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// The per-engine state behind the opt-in web APIs, or <see langword="null"/> — which is what a default
    /// engine, and an engine that enabled only stateless features such as <c>console</c>, carries. Every hot
    /// path that consults it starts with this field being null, so an engine that has no timers pays one
    /// predictable null check per event-loop drain and nothing else. The pump reaches it through
    /// <c>Engine.TryPromoteDueTimerJob</c> and <c>Engine.TimeUntilNextPumpScheduledWork</c>
    /// (<c>Jint/Engine.Pump.cs</c>), which are declared on every target framework so that neither the event
    /// loop nor the wait loops need a conditional-compilation directive of their own. It is created by
    /// <c>WebApiRegistration.Apply</c> for the features that keep state in it — the timers and the events, the
    /// latter for the time origin <c>Event.timeStamp</c> is measured against and for the queue
    /// <c>AbortSignal.timeout()</c> schedules on.
    /// </summary>
    internal WebApiEngineState? _webApi;

    /// <summary>
    /// Whether the event loop still has jobs queued behind the one running now. Only the web-API scheduler
    /// reads it, to keep a task from overtaking the microtasks of the turn it was posted in — see
    /// <see cref="Runtime.EventLoop.HasPendingJobs"/>.
    /// </summary>
    internal bool HasPendingEventLoopJobs => _eventLoop.HasPendingJobs;
}

/// <summary>
/// Mutable per-engine state for the opt-in web APIs. Created by <c>WebApiRegistration</c> when a feature that
/// needs it is enabled, and engine-affine like everything else on <see cref="Engine"/> — two engines built
/// from one shared <see cref="Options"/> instance get one of these each, so their timers and their time
/// origin are independent.
/// </summary>
internal sealed class WebApiEngineState
{
    private readonly Engine _engine;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The monotonic reading taken when this state was created, which is the instant
    /// <see cref="TimeOrigin"/> names and the one <see cref="CurrentHighResolutionTime"/> counts from.
    /// </summary>
    private readonly long _originTimestamp;

    /// <summary>
    /// The requests this engine has in flight, in registration order. Engine-thread-only, so no lock: a
    /// request is added by <c>fetch</c> and removed by its own settle job, both of which run on the engine's
    /// thread. A list rather than a set because it is bounded by
    /// <c>Options.FetchOptions.MaxConcurrentRequests</c> and never long.
    /// </summary>
    private List<FetchOperation>? _fetches;

    internal WebApiEngineState(Engine engine, TimeProvider timeProvider, TimerQueue? timers, Options.FetchOptions? fetchOptions, SchedulerQueue? scheduler)
    {
        _engine = engine;
        _timeProvider = timeProvider;
        Timers = timers;
        FetchOptions = fetchOptions;
        Scheduler = scheduler;

        // Both halves of the time origin, read back to back: the monotonic reading every later now() is a
        // duration from, and the wall-clock moment that reading corresponds to.
        _originTimestamp = timeProvider.GetTimestamp();
        TimeOrigin = (timeProvider.GetUtcNow() - DateTimeOffset.UnixEpoch).TotalMilliseconds;
    }

    /// <summary>
    /// The engine's active timers, or <see langword="null"/> when nothing that schedules one is enabled.
    /// </summary>
    internal TimerQueue? Timers { get; }

    /// <summary>
    /// The host's fetch settings, or <see langword="null"/> when the feature is off. Read once, when the
    /// engine is built, so that no background thread ever reaches into <see cref="Options"/>.
    /// </summary>
    internal Options.FetchOptions? FetchOptions { get; }

    /// <summary>
    /// How many requests are in flight, which is what <c>Options.FetchOptions.MaxConcurrentRequests</c>
    /// bounds.
    /// </summary>
    internal int ActiveFetchCount => _fetches?.Count ?? 0;

    internal void RegisterFetch(FetchOperation operation) => (_fetches ??= new List<FetchOperation>()).Add(operation);

    internal void UnregisterFetch(FetchOperation operation) => _fetches?.Remove(operation);

    /// <summary>
    /// The engine's prioritized task queues, or <see langword="null"/> when the scheduler feature is off.
    /// Nothing else consults it: the scheduler drains itself through an ordinary event-loop job, so unlike the
    /// timers it needs no hook in the pump.
    /// </summary>
    internal SchedulerQueue? Scheduler { get; }

    /// <summary>
    /// <c>performance.timeOrigin</c>: the moment this state was created, as milliseconds since the Unix
    /// epoch, https://w3c.github.io/hr-time/#dom-performance-timeorigin.
    /// </summary>
    /// <remarks>
    /// Per engine, and deliberately not reset by <see cref="ResetTransientState"/>: a pooled engine keeps the
    /// origin it was built with, so <c>performance.now()</c> can never go backwards across an evaluation
    /// cycle.
    /// </remarks>
    internal double TimeOrigin { get; }

    /// <summary>
    /// <c>performance.now()</c>: https://w3c.github.io/hr-time/#dfn-current-high-resolution-time, the
    /// duration from <see cref="TimeOrigin"/> to now in milliseconds, read from the same monotonic clock the
    /// timers are scheduled against and deliberately not coarsened.
    /// </summary>
    internal double CurrentHighResolutionTime =>
        _timeProvider.GetElapsedTime(_originTimestamp, _timeProvider.GetTimestamp()).TotalMilliseconds;

    /// <summary>
    /// Promotes at most one due timer into an event-loop job. One per call rather than all of them, so that
    /// the reactions a timer's callback queues are run before the next timer is even looked at.
    /// </summary>
    internal bool TryPromoteDueTimerJob()
    {
        var timers = Timers;
        if (timers is null || !timers.TryTakeDue(out var entry))
        {
            return false;
        }

        // Enqueued with the timer's own registration generation rather than the current one: a timer
        // registered before a RestoreGlobalSnapshot is already gone from the queue that restore cleared, and
        // this is the belt to that braces.
        _engine.AddToEventLoop(entry.Job, entry.Generation);
        return true;
    }

    /// <summary>
    /// How long the engine may idle before a timer needs the pump, or <see langword="null"/> when nothing is
    /// scheduled. Zero or negative means one is due right now.
    /// </summary>
    internal TimeSpan? TimeUntilNextDueTimer() => Timers?.TimeUntilNextDue();

    /// <summary>
    /// Drops the state that belongs to the evaluation cycle a <c>RestoreGlobalSnapshot</c> has just ended.
    /// </summary>
    /// <remarks>
    /// A request in flight is cancelled rather than merely forgotten: the generation fence already stops its
    /// response reaching the restored engine, but forgetting it would leave the socket open until the server
    /// answered. Its promise stays pending — settling it is exactly what the fence forbids — which is the
    /// same contract a promise registered before a restore has always had.
    /// </remarks>
    internal void ResetTransientState()
    {
        Timers?.Clear();
        Scheduler?.Clear();

        if (_fetches is not { Count: > 0 } fetches)
        {
            return;
        }

        // Copied before the walk: Abandon cancels a token source, and a synchronous continuation on this very
        // thread would otherwise reach UnregisterFetch and mutate the list under the enumerator.
        var pending = fetches.ToArray();
        fetches.Clear();

        foreach (var fetch in pending)
        {
            fetch.Abandon();
        }
    }
}
#endif

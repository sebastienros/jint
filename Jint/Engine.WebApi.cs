#if NET8_0_OR_GREATER
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
}

/// <summary>
/// Mutable per-engine state for the opt-in web APIs. Created by <c>WebApiRegistration</c> when a feature that
/// needs it is enabled, and engine-affine like everything else on <see cref="Engine"/> — two engines built
/// from one shared <see cref="Options"/> instance get one of these each, so their timers are independent.
/// </summary>
internal sealed class WebApiEngineState
{
    private readonly Engine _engine;
    private readonly TimeProvider _timeProvider;
    private readonly long _timeOrigin;

    internal WebApiEngineState(Engine engine, TimeProvider timeProvider, TimerQueue? timers)
    {
        _engine = engine;
        _timeProvider = timeProvider;
        _timeOrigin = timeProvider.GetTimestamp();
        Timers = timers;
    }

    /// <summary>
    /// The engine's active timers, or <see langword="null"/> when nothing that schedules one is enabled.
    /// </summary>
    internal TimerQueue? Timers { get; }

    /// <summary>
    /// Milliseconds since this engine's <i>time origin</i>, which is the instant the web APIs were installed
    /// on it — https://w3c.github.io/hr-time/#dfn-relative-high-resolution-time. It is what
    /// <c>Event.timeStamp</c> is measured in, and it reads the same <see cref="TimeProvider"/> the timers do,
    /// so a fake clock makes both deterministic together.
    /// </summary>
    internal double RelativeHighResolutionTime()
        => _timeProvider.GetElapsedTime(_timeOrigin, _timeProvider.GetTimestamp()).TotalMilliseconds;

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
    internal void ResetTransientState() => Timers?.Clear();
}
#endif

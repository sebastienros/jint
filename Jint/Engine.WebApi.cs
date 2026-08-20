#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using Jint.WebApi.Timers;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// The per-engine state behind the opt-in web APIs, or <see langword="null"/> — which is what a default
    /// engine, and an engine that enabled only stateless features such as <c>console</c>, carries. Every hot
    /// path that consults it starts with this field being null, so an engine that has no timers pays one
    /// predictable null check per event-loop drain and nothing else.
    /// </summary>
    internal WebApiEngineState? _webApi;

    /// <summary>
    /// Moves the next due timer onto the event loop, if one is due. Called by
    /// <see cref="Runtime.EventLoop.RunAvailableContinuations"/> when the job queue has run dry — which is
    /// what makes the single job queue behave as the microtask queue: every promise reaction already queued
    /// runs before any timer, so <c>Promise.resolve().then(f)</c> beats <c>setTimeout(g, 0)</c>.
    /// </summary>
    /// <returns>Whether a timer was promoted, i.e. whether the pump has more work to do.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryPromoteDueTimerJob()
    {
        var webApi = _webApi;
        return webApi is not null && webApi.TryPromoteDueTimerJob();
    }
}

/// <summary>
/// Mutable per-engine state for the opt-in web APIs. Created by <c>WebApiRegistration</c> when a feature that
/// needs it is enabled, and engine-affine like everything else on <see cref="Engine"/> — two engines built
/// from one shared <see cref="Options"/> instance get one of these each, so their timers are independent.
/// </summary>
internal sealed class WebApiEngineState
{
    private readonly Engine _engine;

    internal WebApiEngineState(Engine engine, TimerQueue? timers)
    {
        _engine = engine;
        Timers = timers;
    }

    /// <summary>
    /// The engine's active timers, or <see langword="null"/> when <see cref="WebApiFeatures.Timers"/> was not
    /// enabled.
    /// </summary>
    internal TimerQueue? Timers { get; }

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

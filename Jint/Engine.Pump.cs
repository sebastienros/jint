// CA1822: on the target frameworks without web-API timers, TryPromoteDueTimerJob below touches no instance
// state. It stays an instance method on every one of them so that the event loop can call it unconditionally
// and keep conditional compilation out of the pump.
#pragma warning disable CA1822

using System.Runtime.CompilerServices;
using Jint.Native.Atomics;

namespace Jint;

// What the event-loop pump asks the engine about work the engine schedules for *itself*: a pending
// Atomics.waitAsync timeout on every target framework, and a due web-API timer on net8.0 and later. Both are
// invisible to EventLoop.Enqueue until they come due — nothing enqueues them and so nothing wakes a waiting
// thread for them — which is why the pump has to look, and why the two idle waits bound themselves by
// TimeUntilNextPumpScheduledWork. Declaring every hook here on every target framework, with the conditional
// compilation inside the bodies, is what keeps #if out of EventLoop.RunAvailableContinuations,
// Engine.DrainEventLoopUntil and Engine.AwaitPromiseSettlementAsync entirely.
public partial class Engine
{
    /// <summary>
    /// This engine's pending finite-timeout <c>Atomics.waitAsync</c> deadlines, or <see langword="null"/> —
    /// which is what every engine carries until the first such wait, and what it goes back to as soon as the
    /// last one is settled or discarded. The field being null is the whole per-job cost of the check in
    /// <see cref="Runtime.EventLoop.RunAvailableContinuations"/> on an engine that has none pending.
    /// </summary>
    private AtomicsWaiterDeadlines? _atomicsWaiterDeadlines;

    /// <summary>
    /// Registers a wait to time out <paramref name="timeoutMilliseconds"/> from now. Called on the engine
    /// thread from <c>Atomics.waitAsync</c>, and only for a finite timeout: a wait asking for none never
    /// enters the registry, because nothing but <c>Atomics.notify</c> can ever end it.
    /// </summary>
    internal void RegisterAtomicsWaiterDeadline(AtomicsInstance.AsyncWaiter waiter, double timeoutMilliseconds)
    {
        (_atomicsWaiterDeadlines ??= new AtomicsWaiterDeadlines()).Add(waiter, timeoutMilliseconds);
    }

    /// <summary>
    /// Settles every <c>Atomics.waitAsync</c> whose timeout has elapsed. Called by the pump once per pass of
    /// its loop — not only when the job queue runs dry, which a timer may wait for but a wait may not: the
    /// microtask spin that test262's <c>$262.agent.setTimeout</c> polyfill is built from keeps the queue
    /// permanently non-empty, so a wait promoted only at exhaustion would never be looked at while such a
    /// script is polling for it.
    /// </summary>
    /// <remarks>
    /// The cost of that decision is one predictable null test per job, and nothing at all beyond it: the
    /// field is null on every engine that has never had a finite-timeout wait pending, and is put back to
    /// null the moment the last one leaves, so an engine that used the feature once does not go on paying a
    /// second load for the rest of its life.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SettleTimedOutAtomicsWaiters()
    {
        var deadlines = _atomicsWaiterDeadlines;
        if (deadlines is not null && deadlines.SettleDue())
        {
            _atomicsWaiterDeadlines = null;
        }
    }

    /// <summary>
    /// Drops the pending deadlines of the evaluation cycle a <c>RestoreGlobalSnapshot</c> has just ended, so
    /// that a wait registered before it can never settle its promise into the globals just restored.
    /// </summary>
    /// <remarks>
    /// Deliberately the whole of it: the waits themselves stay in the waiter lists of their shared data
    /// blocks, exactly as a wait asking for no timeout always has, so what <c>Atomics.notify</c> counts is
    /// unchanged by a restore — and what it wakes is stopped by the generation each waiter captured at
    /// registration, which is the same fence that catches a settlement already promoted into a job.
    /// </remarks>
    internal void DiscardAtomicsWaiterDeadlines()
    {
        _atomicsWaiterDeadlines = null;
    }

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
#if NET8_0_OR_GREATER
        var webApi = _webApi;
        return webApi is not null && webApi.TryPromoteDueTimerJob();
#else
        // Timers are the one web API that needs engine infrastructure, and every line of them is net8.0 and
        // later; downlevel there is nothing to promote and the JIT folds this call away to nothing.
        return false;
#endif
    }

    /// <summary>
    /// How long the engine may idle before work it scheduled for itself needs the pump: the earlier of the
    /// next <c>Atomics.waitAsync</c> deadline and — on net8.0 and later — the next due web-API timer.
    /// <see langword="null"/> when neither pends; zero or negative means something is due right now.
    /// </summary>
    internal TimeSpan? TimeUntilNextPumpScheduledWork()
    {
        var untilWaiter = _atomicsWaiterDeadlines?.TimeUntilNextDeadline();

#if NET8_0_OR_GREATER
        var untilTimer = _webApi?.TimeUntilNextDueTimer();
        if (untilTimer is { } timer && (untilWaiter is not { } waiter || timer < waiter))
        {
            return timer;
        }
#endif

        return untilWaiter;
    }
}

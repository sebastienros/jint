using System.Runtime.CompilerServices;
using Jint.Native.Atomics;

namespace Jint;

// What the event-loop pump asks the engine about work the engine schedules for *itself*: a pending
// Atomics.waitAsync timeout. It is invisible to EventLoop.Enqueue until it comes due — nothing enqueues it
// and so nothing wakes a waiting thread for it — which is why the pump has to look, and why the two idle
// waits bound themselves by TimeUntilNextPumpScheduledWork.
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
    /// its loop, and deliberately not only when the job queue runs dry: the microtask spin that test262's
    /// <c>$262.agent.setTimeout</c> polyfill is built from keeps the queue permanently non-empty, so a wait
    /// settled only at exhaustion would never be looked at while such a script is polling for it.
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
    /// How long the engine may idle before work it scheduled for itself needs the pump: the next
    /// <c>Atomics.waitAsync</c> deadline. <see langword="null"/> when none pends; zero or negative means one
    /// is due right now.
    /// </summary>
    internal TimeSpan? TimeUntilNextPumpScheduledWork() => _atomicsWaiterDeadlines?.TimeUntilNextDeadline();
}

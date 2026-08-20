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
        return webApi is not null && webApi.TryPromoteDeferredWork();
#else
        // Timers are the one web API that needs engine infrastructure, and every line of them is net8.0 and
        // later; downlevel there is nothing to promote and the JIT folds this call away to nothing.
        return false;
#endif
    }

    /// <summary>
    /// Pending finite-timeout <c>Atomics.waitAsync</c> waits, for
    /// <see cref="AdvancedOperations.GetMemoryReport(int)"/>. Declared here beside the registry it reads,
    /// like every other pump hook, so the report needs no conditional compilation of its own.
    /// </summary>
    internal int PendingAtomicsWaiterCount => _atomicsWaiterDeadlines?.PendingCount ?? 0;

    /// <summary>
    /// Registered web-API timers that have not fired and have not been cleared, for
    /// <see cref="AdvancedOperations.GetMemoryReport(int)"/>. Zero on every target framework below .NET 8,
    /// which has no timers to register, and on any engine that did not enable them.
    /// </summary>
    internal int PendingTimerCount
    {
        get
        {
#if NET8_0_OR_GREATER
            return _webApi?.PendingTimerCount ?? 0;
#else
            return 0;
#endif
        }
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

    public partial class AdvancedOperations
    {
        /// <summary>
        /// How long this engine may be left alone before <see cref="ProcessTasks"/> has something to run, or
        /// <see langword="null"/> when it has nothing scheduled at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The canonical host loop is this property plus <see cref="ProcessTasks"/>, and there is
        /// deliberately no third method that drains for a budget.</b> Jint never starts a thread to run script:
        /// a <c>setTimeout</c> callback, a <c>scheduler.postTask</c> task, a settled <c>Atomics.waitAsync</c>
        /// all run on whichever thread calls into the engine, and nowhere else. What a host driving its own
        /// loop was missing is not another way to pump but the answer to <i>when</i> to pump, which is this.
        /// </para>
        /// <para>
        /// The three answers:
        /// <list type="bullet">
        /// <item><description>
        /// <see cref="TimeSpan.Zero"/> — there is work to run <em>now</em>: an event-loop job is queued (a
        /// promise reaction, a scheduler task, a completion that arrived from a background thread), a timer is
        /// already due, or an idle callback is waiting for a pump. Call <see cref="ProcessTasks"/>.
        /// </description></item>
        /// <item><description>
        /// A positive <see cref="TimeSpan"/> — nothing to run yet, and this is how long until the earliest
        /// <em>timed</em> work comes due. That covers <c>setTimeout</c> and <c>setInterval</c>,
        /// <c>AbortSignal.timeout()</c>, a delayed <c>scheduler.postTask</c>, a <c>requestIdleCallback</c>
        /// timeout, and the deadline of an <c>Atomics.waitAsync</c>.
        /// </description></item>
        /// <item><description>
        /// <see langword="null"/> — nothing is scheduled. The engine will produce no work by itself; only
        /// something the host does, or a background completion it is already waiting for, can change that.
        /// </description></item>
        /// </list>
        /// </para>
        /// <para>
        /// <b>It is a hint about the engine's own schedule, never a substitute for waiting on external work.</b>
        /// A <see cref="System.Threading.Tasks.Task"/> a script is awaiting through interop settles when it
        /// settles, and an <c>Atomics.waitAsync</c> woken by another agent's <c>Atomics.notify</c> settles when
        /// that happens; both simply enqueue, and neither has a due time to report. A <see langword="null"/>
        /// therefore means "I have nothing timed pending", not "nothing will ever happen" — which is why the
        /// loop below still has a frame cadence of its own rather than sleeping on this value alone.
        /// </para>
        /// <para>
        /// The answer is a snapshot taken on the calling thread and can be stale the instant it is returned:
        /// a background completion may enqueue a job right after a <see langword="null"/> is handed back. That
        /// is harmless for the loop shape below — the job is simply seen by the next
        /// <see cref="ProcessTasks"/> — and it is another reason not to sleep on the value indefinitely. For
        /// the same reason it may occasionally report <see cref="TimeSpan.Zero"/> for a job that turns out to
        /// belong to an evaluation cycle <see cref="RestoreGlobalSnapshot"/> has ended, which
        /// <see cref="ProcessTasks"/> then discards without running.
        /// </para>
        /// <para>
        /// Reading it is cheap and allocation-free, and an engine with no web APIs and no asynchronous atomics
        /// wait answers from one queue check.
        /// </para>
        /// </remarks>
        /// <example>
        /// A game loop — the shape this exists for. The engine is pumped once per frame, and the pump is
        /// skipped entirely on the frames where it provably has nothing to do:
        /// <code>
        /// while (running)
        /// {
        ///     var until = engine.Advanced.TimeUntilNextScheduledWork;
        ///     if (until is null || until &lt;= frameBudget)
        ///     {
        ///         // Either nothing is scheduled — in which case a job may still have arrived from a
        ///         // background completion — or it comes due within this frame. Either way, pump.
        ///         engine.Advanced.ProcessTasks();
        ///     }
        ///
        ///     RenderFrame();
        ///     SleepUntilNextFrame();
        /// }
        /// </code>
        /// A message pump with no frame of its own can sleep on the value instead, keeping a ceiling so that
        /// work arriving from a background thread is still picked up:
        /// <code>
        /// var until = engine.Advanced.TimeUntilNextScheduledWork ?? TimeSpan.FromMilliseconds(50);
        /// if (until > TimeSpan.Zero)
        /// {
        ///     Thread.Sleep(until &lt; TimeSpan.FromMilliseconds(50) ? until : TimeSpan.FromMilliseconds(50));
        /// }
        ///
        /// engine.Advanced.ProcessTasks();
        /// </code>
        /// </example>
        public TimeSpan? TimeUntilNextScheduledWork
        {
            get
            {
                // Anything already queued is work the pump can run now, so no clock can improve on the
                // answer. This check lives here rather than in the internal aggregate below, because the
                // engine's own wait loops clamp on that aggregate while the queue may be unrunnable from
                // where they stand — a zero there would spin them hot for their caller's whole timeout.
                if (_engine._eventLoop.HasPendingJobs)
                {
                    return TimeSpan.Zero;
                }

#if NET8_0_OR_GREATER
                // An idle callback becomes runnable the moment the job queue drains, so it is work for now
                // rather than work for later.
                if (_engine._webApi is { } webApi && webApi.HasPendingIdleWork)
                {
                    return TimeSpan.Zero;
                }
#endif

                var next = _engine.TimeUntilNextPumpScheduledWork();

                // A timer that came due while nobody was pumping reports a negative remainder; the contract
                // above says zero means "run it now", and there is nothing a host could do with "how late am
                // I".
                return next is { } value && value < TimeSpan.Zero ? TimeSpan.Zero : next;
            }
        }
    }
}

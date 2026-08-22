// CA1822: on the target frameworks without web-API timers, TryPromoteDueTimerJob below touches no instance
// state. It stays an instance method on every one of them so that the event loop can call it unconditionally
// and keep conditional compilation out of the pump.
#pragma warning disable CA1822

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Native.Atomics;
using Jint.Runtime;

namespace Jint;

// What the event-loop pump asks the engine about work the engine schedules for *itself*: a pending
// Atomics.waitAsync timeout on every target framework, and a due web-API timer on net8.0 and later. Both are
// invisible to EventLoop.Enqueue until they come due — nothing enqueues them and so nothing wakes a waiting
// thread for them — which is why the pump has to look, and why the two idle waits bound themselves by
// TimeUntilNextPumpScheduledWork. Declaring every hook here on every target framework, with the conditional
// compilation inside the bodies, is what keeps #if out of EventLoop.RunAvailableContinuations,
// Engine.DrainEventLoopUntil and Engine.AwaitPromiseSettlementAsync entirely.
//
// The host-facing half of the same subject lives here too: TimeUntilNextScheduledWork answers *when* to pump,
// and WaitForScheduledWork parks the calling thread until that answer is "now" — the piece a host driving one
// engine per thread was missing, because a job arriving from another thread has no due time to report and so
// could only be found by polling.
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
    /// Whether the pump has something to run the instant it is called, without consulting any clock: a job is
    /// queued, or — on net8.0 and later — an idle callback is waiting for the queue to drain, which it does the
    /// moment a pump starts.
    /// </summary>
    /// <remarks>
    /// The one home for that composition, so that <see cref="AdvancedOperations.TimeUntilNextScheduledWork"/>
    /// and <see cref="WaitForScheduledWork"/> can never disagree about what "work is available now" means. Like
    /// every other hook in this file it is declared on every target framework with the conditional compilation
    /// inside its body.
    /// </remarks>
    internal bool HasImmediatePumpWork()
    {
        if (_eventLoop.HasPendingJobs)
        {
            return true;
        }

#if NET8_0_OR_GREATER
        return _webApi is { HasPendingIdleWork: true };
#else
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

    /// <summary>
    /// What one pass of the pump wait needs to know: whether there is work the calling thread could run right
    /// now, and otherwise how long the engine's own schedule allows it to idle before there will be.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ScheduledWorkState(bool IsAvailable, TimeSpan? UntilDue);

    /// <summary>
    /// Nothing to run and nothing scheduled: the answer for an engine that only another thread can wake.
    /// </summary>
    private static readonly ScheduledWorkState NoScheduledWork = new(IsAvailable: false, UntilDue: null);

    /// <summary>
    /// The pump wait's view of <see cref="AdvancedOperations.TimeUntilNextScheduledWork"/>, differing from it
    /// in exactly one way: nested inside a running job nothing counts as available.
    /// </summary>
    /// <remarks>
    /// That carve-out is the rule <see cref="Runtime.EventLoop.WaitForWork"/> and
    /// <see cref="DrainEventLoopUntil"/> already apply, for the reason they document — the re-entrancy guard
    /// makes the queue unrunnable from inside a job, so reporting it as available would hand the caller a
    /// <see langword="true"/> whose <see cref="AdvancedOperations.ProcessTasks"/> does nothing, and its loop
    /// would spin hot for the whole of its ceiling. Reporting nothing instead lets the wait run its course and
    /// answer <see langword="false"/>, which costs one bounded idle and no CPU.
    /// </remarks>
    private ScheduledWorkState InspectScheduledWork()
    {
        if (_eventLoop.IsRunningJob)
        {
            return NoScheduledWork;
        }

        // Anything already queued is work the caller could run the moment this returns, and no clock can
        // improve on that answer — the same first question TimeUntilNextScheduledWork asks, through the same
        // helper, so the two can never disagree about what "now" means.
        if (HasImmediatePumpWork())
        {
            return new ScheduledWorkState(IsAvailable: true, UntilDue: null);
        }

        if (TimeUntilNextPumpScheduledWork() is not { } untilDue)
        {
            return NoScheduledWork;
        }

        return untilDue <= TimeSpan.Zero
            ? new ScheduledWorkState(IsAvailable: true, UntilDue: null)
            : new ScheduledWorkState(IsAvailable: false, untilDue);
    }

    /// <summary>
    /// The longest span a single blocking wait may ask for. <see cref="ManualResetEventSlim.Wait(TimeSpan)"/>
    /// and <see cref="Task.Delay(TimeSpan)"/> both reject anything above <see cref="int.MaxValue"/>
    /// milliseconds, and the wait loops around anyway, so a longer ceiling is served by several passes.
    /// </summary>
    private static readonly TimeSpan MaxSingleWaitInterval = TimeSpan.FromMilliseconds(int.MaxValue);

    private static readonly double MillisecondsPerStopwatchTick = 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// How much of the caller's ceiling a wait has already spent, measured on the monotonic clock.
    /// </summary>
    /// <remarks>
    /// <see cref="Stopwatch"/> rather than the wall clock, for the reason
    /// <see cref="Native.Atomics.AtomicsWaiterDeadlines"/> beside it records: a ceiling a host passed must
    /// not be cut short by an NTP step forwards or stretched by one backwards. Elapsed-since-start rather
    /// than an absolute deadline, so that a ceiling as large as <see cref="TimeSpan.MaxValue"/> — which a
    /// host spelling "effectively no bound" may well pass — cannot overflow the arithmetic.
    /// </remarks>
    private static TimeSpan ElapsedSince(long startTimestamp)
        => TimeSpan.FromMilliseconds((Stopwatch.GetTimestamp() - startTimestamp) * MillisecondsPerStopwatchTick);

    /// <summary>
    /// Links the caller's token with a registered <see cref="CancellationConstraint"/>'s, exactly as
    /// <see cref="DrainEventLoopUntil"/> does: an engine that has been cancelled has nothing worth waiting
    /// for, and the two tokens keep different contracts on the way out.
    /// </summary>
    private CancellationToken BuildPumpWaitToken(
        CancellationToken cancellationToken,
        out CancellationTokenSource? linkedTokenSource)
    {
        linkedTokenSource = null;

        var constraintToken = Constraints.Find<CancellationConstraint>()?.Token ?? default;
        if (!cancellationToken.CanBeCanceled)
        {
            return constraintToken;
        }

        if (!constraintToken.CanBeCanceled)
        {
            return cancellationToken;
        }

        linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, constraintToken);
        return linkedTokenSource.Token;
    }

    /// <summary>
    /// Decides which contract a cancellation during an idle wait belongs to, mirroring
    /// <see cref="DrainEventLoopUntil"/>: the caller's own token is reported back to the caller, while the
    /// engine's <see cref="CancellationConstraint"/> surfaces the way per-statement execution reports it.
    /// </summary>
    [DoesNotReturn]
    private static void RethrowPumpWaitCancellation(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Throw.ExecutionCanceledException();
    }

    /// <summary>
    /// How long this pass may block: the smaller of what is left of the caller's ceiling and the engine's own
    /// next due time, clamped to <see cref="MaxSingleWaitInterval"/>. <see langword="null"/> means the ceiling
    /// has run out.
    /// </summary>
    private static TimeSpan? NextPumpWaitInterval(TimeSpan? remaining, TimeSpan? untilDue)
    {
        var interval = Timeout.InfiniteTimeSpan;
        if (remaining is { } left)
        {
            if (left <= TimeSpan.Zero)
            {
                return null;
            }

            interval = left;
        }

        if (untilDue is { } due && (interval == Timeout.InfiniteTimeSpan || due < interval))
        {
            interval = due;
        }

        // Timeout.InfiniteTimeSpan is negative, so it can never trip this — a wait with no bound at all keeps
        // asking for none, and only a genuinely huge ceiling is served in several passes.
        return interval > MaxSingleWaitInterval ? MaxSingleWaitInterval : interval;
    }

    /// <summary>
    /// The body of <see cref="AdvancedOperations.WaitForScheduledWork"/>, which owns the engine for the whole
    /// of it — see that method's remarks for why.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <em>top-level</em> park is a callback-admission window, and needs the pair
    /// <see cref="DrainEventLoopUntil"/> already has: the reservation an authorized cross-thread callback is
    /// matched against, held for the whole park, plus a per-iteration release of the thread that callback has
    /// to acquire in order to take its turn. Neither alone is enough here — the reservation without the yield
    /// admits a callback that then blocks in <c>AcquireHostCall</c> until the park ends, and the yield without
    /// the reservation is an unreserved engine, which is what an unrelated caller is refused by.
    /// </para>
    /// <para>
    /// Both are keyed on the scope that <em>claimed</em> the engine, deliberately not on
    /// <see cref="_ownerDepth"/> (see <see cref="ReleaseEntryReservationIfHeld"/> for what that mistake costs).
    /// A pump reached from inside a running evaluation — host code the script itself invoked — has undertaken
    /// nothing: the thread is in the middle of somebody else's script, and admitting a callback there would
    /// interleave its turn into the middle of that evaluation. Such a park keeps refusing, exactly as it did.
    /// </para>
    /// </remarks>
    internal bool WaitForScheduledWork(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var ownership = EnterHostCall();

        var isTopLevelPark = ownership.IsEntryRoot;
        using var admission = isTopLevelPark ? OpenHostCallbackAdmissionWindow() : default;

        cancellationToken.ThrowIfCancellationRequested();

        var state = InspectScheduledWork();
        if (state.IsAvailable)
        {
            return true;
        }

        var infinite = timeout == Timeout.InfiniteTimeSpan;
        if (!infinite && timeout <= TimeSpan.Zero)
        {
            return false;
        }

        var waitToken = BuildPumpWaitToken(cancellationToken, out var linkedTokenSource);
        try
        {
            var start = Stopwatch.GetTimestamp();
            while (true)
            {
                var remaining = infinite ? (TimeSpan?) null : timeout - ElapsedSince(start);
                if (NextPumpWaitInterval(remaining, state.UntilDue) is not { } interval)
                {
                    return false;
                }

                try
                {
                    // Yields the thread for the idle wait alone, finding the window's reservation rather than
                    // taking one of its own — the shape DrainEventLoopUntil's per-iteration suspension has. A
                    // callback admitted here holds the engine until it finishes, and the resume below waits
                    // for it, which is why the ceiling bounds this wait and not the call around it.
                    using (SuspendHostCallForCallbacks(hasTransferredCallback: isTopLevelPark))
                    {
                        _eventLoop.WaitForWork(completedEvent: null, interval, waitToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    RethrowPumpWaitCancellation(cancellationToken);
                }

                state = InspectScheduledWork();
                if (state.IsAvailable)
                {
                    return true;
                }
            }
        }
        finally
        {
            linkedTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// The body of <see cref="AdvancedOperations.WaitForScheduledWorkAsync"/>. The reservation is taken by the
    /// caller — synchronously, so an engine already in use refuses before a <see cref="Task"/> exists — and
    /// released here, because everything after the first <c>await</c> belongs to this method.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The discipline is the one every async host entry uses: <see cref="ReserveAsyncHostOperation"/> holds the
    /// engine across every await, since no thread stays claimed over one, and each moment that actually reads
    /// engine state re-claims the resuming thread with <see cref="EnterTransferredHostCall"/> — which is what
    /// makes the timer and atomics heaps <see cref="TimeUntilNextPumpScheduledWork"/> walks safe to read from
    /// whichever thread the continuation lands on. Deliberately <em>not</em> mirrored from
    /// <see cref="AwaitPromiseSettlementAsync"/>: its <c>_hostCallbackAdmission</c> bracket around the await
    /// exists to keep a converted host callback admissible while script is suspended, and this wait runs no
    /// script at all. It also leaves <c>_pendingAsyncOperations</c> alone for the same reason — that counter
    /// answers "is an evaluation suspended", and the reservation alone is what a restore has to refuse.
    /// </para>
    /// <para>
    /// What it does share with the synchronous form is the reservation's <em>identity</em>: the anonymous
    /// wildcard rather than a token of its own. Running no script is exactly why — a fresh token can only ever
    /// be carried by a callback converted under the frame that minted it, and this frame converts none, so
    /// such a token matches nothing at all rather than matching narrowly. There is no claiming-scope guard
    /// here because there is nothing for one to do: the reservation is taken synchronously and refuses an
    /// engine any thread already owns, so this form can never be reached from inside a running evaluation.
    /// </para>
    /// </remarks>
    private async Task<bool> WaitForScheduledWorkCoreAsync(object owner, TimeSpan timeout, CancellationToken cancellationToken)
    {
        CancellationTokenSource? linkedTokenSource = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScheduledWorkState state;
            CancellationToken waitToken;
            using (EnterTransferredHostCall(owner))
            {
                state = InspectScheduledWork();
                waitToken = BuildPumpWaitToken(cancellationToken, out linkedTokenSource);
            }

            if (state.IsAvailable)
            {
                return true;
            }

            var infinite = timeout == Timeout.InfiniteTimeSpan;
            if (!infinite && timeout <= TimeSpan.Zero)
            {
                return false;
            }

            var start = Stopwatch.GetTimestamp();
            while (true)
            {
                var remaining = infinite ? (TimeSpan?) null : timeout - ElapsedSince(start);
                if (NextPumpWaitInterval(remaining, state.UntilDue) is not { } interval)
                {
                    return false;
                }

                try
                {
                    if (interval == Timeout.InfiniteTimeSpan)
                    {
                        // Nothing is scheduled and the caller asked for no ceiling, so only an Enqueue can
                        // change the answer — and that is exactly what the unbounded overload waits for.
                        await _eventLoop.WaitForEventAsync(waitToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _eventLoop.WaitForEventAsync(interval, waitToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    RethrowPumpWaitCancellation(cancellationToken);
                }

                // The bounded overload reports a cancelled wait by returning rather than by throwing — it
                // races the registration against a Task.Delay through WhenAny, which never throws — so the
                // token has to be re-read here as well as caught above.
                if (waitToken.IsCancellationRequested)
                {
                    RethrowPumpWaitCancellation(cancellationToken);
                }

                using (EnterTransferredHostCall(owner))
                {
                    state = InspectScheduledWork();
                }

                if (state.IsAvailable)
                {
                    return true;
                }
            }
        }
        finally
        {
            linkedTokenSource?.Dispose();
            ReleaseAsyncHostOperation(owner);
        }
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
        /// A message pump with no frame of its own does not need to read the value at all:
        /// <see cref="WaitForScheduledWork"/> already parks on it, and — unlike a sleep — also wakes on a job
        /// that arrives from another thread, which has no due time for this property to report.
        /// <code>
        /// while (running)
        /// {
        ///     engine.Advanced.ProcessTasks();
        ///     engine.Advanced.WaitForScheduledWork(TimeSpan.FromMilliseconds(50), token);
        /// }
        /// </code>
        /// </example>
        public TimeSpan? TimeUntilNextScheduledWork
        {
            get
            {
                // Anything already queued — and an idle callback, which becomes runnable the moment the queue
                // drains — is work the pump can run now, so no clock can improve on the answer. This check
                // lives here rather than in TimeUntilNextPumpScheduledWork, because the engine's own wait
                // loops clamp on that aggregate while the queue may be unrunnable from where they stand — a
                // zero there would spin them hot for their caller's whole timeout.
                if (_engine.HasImmediatePumpWork())
                {
                    return TimeSpan.Zero;
                }

                var next = _engine.TimeUntilNextPumpScheduledWork();

                // A timer that came due while nobody was pumping reports a negative remainder; the contract
                // above says zero means "run it now", and there is nothing a host could do with "how late am
                // I".
                return next is { } value && value < TimeSpan.Zero ? TimeSpan.Zero : next;
            }
        }

        /// <summary>
        /// Blocks the calling thread until this engine has work worth pumping, or <paramref name="timeout"/>
        /// elapses. Returns <see langword="true"/> when there is (probably) work — call
        /// <see cref="ProcessTasks"/> next — and <see langword="false"/> on timeout.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>It does not pump.</b> The canonical loop is still
        /// <see cref="TimeUntilNextScheduledWork"/>/<see cref="ProcessTasks"/> and there is deliberately no
        /// third method that drains for a budget; this answers only the question a sleep answered badly, which
        /// is <i>how long may this thread idle</i>. What a sleep cannot do is notice a job that arrived from
        /// <em>another</em> thread — a settled interop <see cref="Task"/>, a message posted into this engine,
        /// a module load completing — because such a job has no due time for
        /// <see cref="TimeUntilNextScheduledWork"/> to report and so could only be found by polling. That is
        /// the whole of what this adds: a host that slept on a 20 ms ceiling paid up to 20 ms of latency on
        /// every one of those arrivals.
        /// </para>
        /// <para>
        /// The wait ends as soon as any of these is true, whichever comes first:
        /// <list type="bullet">
        /// <item><description>a job is enqueued, from this thread or any other;</description></item>
        /// <item><description>
        /// work the engine scheduled for <em>itself</em> comes due — a <c>setTimeout</c> or
        /// <c>setInterval</c> callback, an <c>AbortSignal.timeout()</c>, a delayed <c>scheduler.postTask</c>,
        /// an <c>Atomics.waitAsync</c> deadline. The wait is bounded internally by that due time, so a
        /// <c>setTimeout(f, 1)</c> wakes it in about a millisecond rather than at
        /// <paramref name="timeout"/>;
        /// </description></item>
        /// <item><description><paramref name="timeout"/> elapses, which is the <see langword="false"/>;</description></item>
        /// <item><description>
        /// <paramref name="cancellationToken"/> is cancelled, which throws
        /// <see cref="OperationCanceledException"/>.
        /// </description></item>
        /// </list>
        /// A registered <see cref="Constraints.CancellationConstraint"/> ends the wait too, and reports itself
        /// as <see cref="ExecutionCanceledException"/> exactly as per-statement execution does — an engine that
        /// has been cancelled has nothing left worth waiting for. Both exceptions leave the engine usable.
        /// </para>
        /// <para>
        /// <b>Treat <see langword="true"/> as a hint and re-check your own condition.</b> Spurious wakes are
        /// expected: work can be dropped at dequeue because it belongs to an evaluation cycle
        /// <see cref="RestoreGlobalSnapshot"/> has ended, and a wake races anything else the host does. A
        /// <see langword="false"/> is equally not a promise that nothing arrived — it says only that nothing
        /// had arrived when the ceiling ran out.
        /// </para>
        /// <para>
        /// <b>Single drainer.</b> The wait claims the engine for its whole duration, so a second thread calling
        /// it — or calling any other guarded entry — is refused with
        /// <see cref="InvalidOperationException"/>: <i>"This Engine is already in use by another thread or has
        /// an asynchronous operation in progress."</i> That is the engine's ordinary admission rule rather than
        /// anything this method adds, and it is what makes one-thread-per-engine self-enforcing. Enqueueing
        /// into a waiting engine from another thread is unaffected — that path is deliberately unguarded, and
        /// is what wakes the wait.
        /// </para>
        /// <para>
        /// <b>Authorized callbacks are admitted, and one of them can outlast the ceiling.</b> A park is one of
        /// the engine's callback-admission windows (README's Thread-safety section lists them all): a
        /// JavaScript callback the host was handed and converted to a CLR delegate may be dispatched here from
        /// another thread and will wait for its turn rather than being refused, which is the point of parking
        /// the engine's own thread rather than sleeping it. Unrelated public callers are refused throughout,
        /// exactly as they were. The consequence to plan for is that <paramref name="timeout"/> bounds the
        /// <b>idle wait, not the call</b>: an admitted callback holds the engine, and this cannot return until
        /// it finishes, so a host budgeting a frame can be handed control back well after its ceiling by a
        /// callback of its own making. Only a <em>top-level</em> park is a window — one reached from inside a
        /// running evaluation, from host code the script itself invoked, has undertaken nothing and goes on
        /// refusing.
        /// </para>
        /// <para>
        /// <b>Do not call it from inside a job</b> — from host code reached by a promise reaction, a timer
        /// callback or an event listener. The re-entrancy guard makes the queue unrunnable from there, so
        /// nothing counts as available and the call simply idles out its ceiling and answers
        /// <see langword="false"/>. That is the same rule the engine's own wait loops apply, and it is chosen
        /// over refusing outright because this wait — unlike a blocking module import — genuinely can end:
        /// what it cannot do is make the pump you would call next do anything.
        /// </para>
        /// <para>
        /// Available on <b>every</b> target framework and unaffected by which web APIs are enabled.
        /// </para>
        /// </remarks>
        /// <param name="timeout">
        /// The ceiling on how long to block. A non-positive value does not block at all and simply answers
        /// with what is available right now; <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely, which
        /// is only safe together with a <paramref name="cancellationToken"/>.
        /// </param>
        /// <param name="cancellationToken">Ends the wait with an <see cref="OperationCanceledException"/>.</param>
        /// <returns>Whether there is (probably) work for <see cref="ProcessTasks"/> to run.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
        /// <exception cref="ExecutionCanceledException">A registered cancellation constraint fired.</exception>
        /// <exception cref="InvalidOperationException">Another thread is using this engine.</exception>
        /// <example>
        /// One thread per engine — the shape this exists for:
        /// <code>
        /// while (!token.IsCancellationRequested)
        /// {
        ///     engine.Advanced.ProcessTasks();
        ///
        ///     try
        ///     {
        ///         engine.Advanced.WaitForScheduledWork(TimeSpan.FromMilliseconds(50), token);
        ///     }
        ///     catch (OperationCanceledException)
        ///     {
        ///         break;
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool WaitForScheduledWork(TimeSpan timeout, CancellationToken cancellationToken = default)
            => _engine.WaitForScheduledWork(timeout, cancellationToken);

        /// <summary>
        /// The asynchronous form of <see cref="WaitForScheduledWork"/>: the same contract, without holding a
        /// thread while it waits.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything <see cref="WaitForScheduledWork"/> documents applies — it does not pump, a
        /// <see langword="true"/> is a hint, the wait is bounded by the engine's own next due time, a
        /// registered <see cref="Constraints.CancellationConstraint"/> ends it as
        /// <see cref="ExecutionCanceledException"/>, and it is a callback-admission window on the same terms,
        /// including that an admitted callback can carry the returned task past <paramref name="timeout"/>.
        /// Only the ownership differs.
        /// </para>
        /// <para>
        /// <b>Ownership spans the whole await.</b> No thread stays claimed across an <c>await</c>, so this
        /// reserves the engine the way every other asynchronous host entry does: the reservation is taken
        /// <em>synchronously</em>, so an engine already in use is refused with the admission
        /// <see cref="InvalidOperationException"/> before a <see cref="Task"/> exists, and it is held until the
        /// returned task completes. While it is held the engine refuses every unrelated guarded entry from
        /// every thread, this one included — so <see cref="ProcessTasks"/> belongs after the <c>await</c>,
        /// never beside it.
        /// The continuation resumes on whichever thread the runtime hands it, which is why the engine is
        /// re-claimed for each look at its schedule.
        /// </para>
        /// <para>
        /// Because the reservation is taken synchronously, this can never be called from inside a job: a job
        /// runs with the engine already claimed, so the call is refused rather than idling out its ceiling the
        /// way the synchronous form does.
        /// </para>
        /// </remarks>
        /// <param name="timeout">
        /// The ceiling on how long to wait. A non-positive value does not wait at all;
        /// <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely.
        /// </param>
        /// <param name="cancellationToken">Ends the wait with an <see cref="OperationCanceledException"/>.</param>
        /// <returns>Whether there is (probably) work for <see cref="ProcessTasks"/> to run.</returns>
        /// <exception cref="InvalidOperationException">This engine is already in use.</exception>
        public Task<bool> WaitForScheduledWorkAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            // Taken here rather than inside the async body so that the admission failure is reported to the
            // caller synchronously, exactly as EvaluateAsync and its siblings report it; the body owns the
            // release, because everything after its first await belongs to it.
            //
            // Under the engine's anonymous wildcard rather than a token of this operation's own: a park runs
            // no script, so nothing can ever be converted under it and carry that token, and reserving under
            // one refuses every authorized callback instead of admitting the ones this frame issued. Nothing
            // is in force to keep here either — the reservation requires an unowned engine, and an unowned
            // engine has no operation token.
            var owner = _engine.ReserveAsyncHostOperation(_engine.OwnershipReleasedEvent);
            return _engine.WaitForScheduledWorkCoreAsync(owner, timeout, cancellationToken);
        }
    }
}

#nullable enable

using System.Diagnostics;
using Xunit.Sdk;

namespace Jint.Tests.Runtime;

/// <summary>
/// <see cref="Engine.TaskOperations.WaitForScheduledWork"/> and its asynchronous sibling — the pump wait,
/// which parks a host thread until there is something for <see cref="Engine.TaskOperations.ProcessTasks"/>
/// to run.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is gated on <c>NET8_0_OR_GREATER</c>: the wait is core-engine surface on every target
/// framework and knows nothing about which web APIs are enabled. The one source of scheduled work that only
/// exists on .NET 8 and later — a due timer — has its own file, <c>WebApi.PumpWaitTests</c>.
/// </para>
/// <para>
/// Every wait here is released by a thread the test owns, so the wall-clock numbers are ceilings and margins
/// rather than expectations: <see cref="Ceiling"/> is what a wait is given, <see cref="EarlyReturnMargin"/> is
/// the far smaller bound an early return has to beat, and a failure means the wake did not happen at all
/// rather than that it happened slowly.
/// </para>
/// <para>
/// Two rules keep that true on a runner that is not idle, and both matter because #3301 was a healthy wake
/// missing its discriminator by eleven milliseconds. First, a stopwatch is started by <em>the thread that
/// releases the wait, immediately before it releases it</em>, so what is measured is the wake latency alone
/// and never the scheduling of the releasing thread, its settle sleep, or the script that got the waiter
/// there. Second, nothing here is released by a thread-pool timer: <c>CancellationTokenSource.CancelAfter</c>
/// runs its callback on the pool, and a saturated pool grows at roughly one worker per 500 ms, so a timer is
/// the one release whose latency the test cannot bound. Every release below comes from a
/// <see cref="DedicatedThread"/> the test owns.
/// </para>
/// </remarks>
public class WaitForScheduledWorkTests
{
    private const string ConcurrentUseMessage =
        "*already in use by another thread or has an asynchronous operation in progress*";

    /// <summary>
    /// What a wait under test is given. Nothing should ever spend it: it is the bound a broken wake runs into.
    /// </summary>
    /// <remarks>
    /// A minute rather than the ten seconds this was through #3301. The absolute number is what a loaded
    /// runner crosses — a healthy 250 ms wake was measured at 5.011 s against a 5 s midpoint — while the
    /// <em>ratio</em> is what the assertion is about, so moving both together is what widens the margin
    /// without weakening anything. The cost is paid only by a genuine regression, which now takes a minute to
    /// be reported instead of ten seconds.
    /// </remarks>
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The margin an early return has to beat. Half the ceiling — derived from it rather than written out
    /// again, so the two cannot drift — which is what makes the assertion separate "woke" from "ran out the
    /// ceiling" while saying nothing at all about latency.
    /// </summary>
    private static readonly TimeSpan EarlyReturnMargin = TimeSpan.FromTicks(Ceiling.Ticks / 2);

    /// <summary>
    /// Reached only by a genuine wedge — every handshake below is released by a thread the test owns.
    /// </summary>
    private static readonly TimeSpan WedgeCeiling = TestBudgets.WedgeCeiling;

    /// <summary>
    /// An engine with nothing queued and nothing scheduled has nothing to report, so the wait spends its whole
    /// ceiling and answers <see langword="false"/> — and a zero ceiling answers it without waiting at all.
    /// </summary>
    [Fact]
    public void ReturnsFalseOnTimeoutWhenNothingIsPending()
    {
        using var engine = new Engine();

        engine.Tasks.WaitForScheduledWork(TimeSpan.Zero).Should().BeFalse();
        engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50)).Should().BeFalse();

        // The wait owned the engine and gave it back, both times.
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// Work that is already queued is reported without any waiting — the check runs before the ceiling is even
    /// consulted, which a zero ceiling is what proves: it cannot block, so a <see langword="true"/> can only
    /// have come from there.
    /// </summary>
    [Fact]
    public void ReturnsTrueImmediatelyWhenWorkIsAlreadyQueued()
    {
        using var engine = new Engine();
        engine.AddToEventLoop(static () => { });

        engine.Tasks.WaitForScheduledWork(TimeSpan.Zero).Should().BeTrue();

        var elapsed = Stopwatch.StartNew();
        engine.Tasks.WaitForScheduledWork(Ceiling).Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);
    }

    /// <summary>
    /// The reason this API exists: a job enqueued from another thread has no due time, so nothing but a wake
    /// can tell a parked host about it.
    /// </summary>
    [Fact]
    public async Task WakesOnACrossThreadEnqueue()
    {
        using var engine = new Engine();
        using var waiterAboutToPark = new ManualResetEventSlim();

        // Started by the producer immediately before the enqueue, so it measures the wake and nothing else —
        // not this thread reaching the wait, not the producer being scheduled, not the settle sleep.
        var elapsed = new Stopwatch();

        var producer = DedicatedThread.RunAsync(() =>
        {
            waiterAboutToPark.Wait(WedgeCeiling);
            SettleBeforeReleasingTheWait();
            elapsed.Start();
            engine.AddToEventLoop(static () => { });
        });

        waiterAboutToPark.Set();
        engine.Tasks.WaitForScheduledWork(Ceiling).Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);

        await producer;
    }

    /// <summary>
    /// The token ends the wait, whether it was already cancelled when the wait started or fired while it was
    /// parked, and either way the engine is handed back usable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The elapsed assertion is <b>not</b> redundant beside the <see cref="OperationCanceledException"/>, and
    /// #3301 exists because deleting it is the tempting simplification: a wait that never looked at the token
    /// and ran out its whole ceiling still ends up throwing on the way out, since the token is cancelled by
    /// then. Separating "observed the token" from "noticed it at the end" is the whole claim, and only a clock
    /// can make it.
    /// </para>
    /// <para>
    /// So the clock stays and the two things it used to conflate are pulled apart instead. The cancel comes
    /// from a thread the test owns rather than from <c>CancelAfter</c>, whose callback a saturated thread pool
    /// delivers whenever it gets round to it; and the stopwatch starts on that thread immediately before the
    /// cancel, so the only interval measured is the one being asserted about.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task ThrowsOperationCanceledOnTheToken()
    {
        using var engine = new Engine();

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();
        Invoking(() => engine.Tasks.WaitForScheduledWork(Ceiling, alreadyCancelled.Token))
            .Should().Throw<OperationCanceledException>();

        using var cancelledWhileParked = new CancellationTokenSource();
        using var waiterAboutToPark = new ManualResetEventSlim();
        var elapsed = new Stopwatch();

        var canceller = DedicatedThread.RunAsync(() =>
        {
            waiterAboutToPark.Wait(WedgeCeiling);
            SettleBeforeReleasingTheWait();
            elapsed.Start();
            cancelledWhileParked.Cancel();
        });

        waiterAboutToPark.Set();
        Invoking(() => engine.Tasks.WaitForScheduledWork(Ceiling, cancelledWhileParked.Token))
            .Should().Throw<OperationCanceledException>();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);

        await canceller;

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The other direction of the same claim, and the one that pins the wait's <em>own</em> ownership: a thread
    /// parked in the wait holds the engine for the whole of it, so every guarded entry from every other thread
    /// is refused until it returns. That is what makes one-drainer-per-engine self-enforcing rather than
    /// merely advisory — a second host loop cannot quietly become a second drainer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately separate from <see cref="ASecondThreadWaitingIsRefused"/>, which asks the reverse question
    /// and would pass even with the wait's claim removed: the token linkage inside reads
    /// <c>Constraints.Find&lt;CancellationConstraint&gt;()</c>, which takes an admission of its own and would
    /// report the refusal from there. Only entering <em>while</em> a wait is parked distinguishes the two.
    /// </para>
    /// <para>
    /// The park and the probe that detects it are one object (<see cref="TopLevelPark"/>) because they race
    /// each other at the start: the probe is a claim attempt, and a top-level park's entry is refused by any
    /// claim that beats it. That class documents the whole of it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task AParkedWaitOwnsTheEngineForItsWholeDuration()
    {
        using var engine = new Engine();
        using var releaseWait = new CancellationTokenSource();

        var park = TopLevelPark.Start(engine, WedgeCeiling, releaseWait.Token);
        try
        {
            park.WaitUntilOwningTheEngine();
        }
        finally
        {
            releaseWait.Cancel();
        }

        await park.Completed;

        // The claim goes back with the wait rather than outliving it.
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The wait is a guarded entry like any other, so an engine another thread is already using refuses it —
    /// both forms, and the asynchronous one before a <see cref="Task"/> exists at all.
    /// </summary>
    [Fact]
    public async Task ASecondThreadWaitingIsRefused()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var engine = new Engine();
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(WedgeCeiling);
        }));

        var running = DedicatedThread.RunAsync(() => engine.Execute("block()"));
        WaitUntilOwned(entered, running);
        try
        {
            Invoking(() => engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50)))
                .Should().Throw<InvalidOperationException>()
                .WithMessage(ConcurrentUseMessage);

            // Not awaited: the reservation the asynchronous form takes is claimed synchronously, precisely so
            // that an engine already in use is refused before a Task exists.
            Action concurrentAsync = () => _ = engine.Tasks.WaitForScheduledWorkAsync(TimeSpan.FromMilliseconds(50));
            concurrentAsync.Should().Throw<InvalidOperationException>().WithMessage(ConcurrentUseMessage);
        }
        finally
        {
            release.Set();
        }

        await running;
    }

    /// <summary>
    /// The asynchronous form times out the same way, and gives the engine back — the reservation it holds
    /// across the await is released when the task completes, not left behind to refuse the host's next call.
    /// </summary>
    [Fact]
    public async Task ReturnsFalseOnTimeoutWhenNothingIsPendingAsync()
    {
        using var engine = new Engine();

        (await engine.Tasks.WaitForScheduledWorkAsync(TimeSpan.Zero)).Should().BeFalse();
        (await engine.Tasks.WaitForScheduledWorkAsync(TimeSpan.FromMilliseconds(50))).Should().BeFalse();

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// The asynchronous form's pre-check, proved the same way: a zero ceiling cannot wait.
    /// </summary>
    [Fact]
    public async Task ReturnsTrueImmediatelyWhenWorkIsAlreadyQueuedAsync()
    {
        using var engine = new Engine();
        engine.AddToEventLoop(static () => { });

        (await engine.Tasks.WaitForScheduledWorkAsync(TimeSpan.Zero)).Should().BeTrue();

        var elapsed = Stopwatch.StartNew();
        (await engine.Tasks.WaitForScheduledWorkAsync(Ceiling)).Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);
    }

    /// <summary>
    /// The asynchronous form wakes on the same cross-thread enqueue. Its reservation refuses every guarded
    /// entry while it is outstanding, so the producer's handshake below is what an owned engine looks like from
    /// the other side.
    /// </summary>
    [Fact]
    public async Task WakesOnACrossThreadEnqueueAsync()
    {
        using var engine = new Engine();

        using var waiterAboutToPark = new ManualResetEventSlim();
        var elapsed = new Stopwatch();
        var producer = DedicatedThread.RunAsync(() =>
        {
            waiterAboutToPark.Wait(WedgeCeiling);
            SettleBeforeReleasingTheWait();
            elapsed.Start();
            engine.AddToEventLoop(static () => { });
        });

        waiterAboutToPark.Set();
        (await engine.Tasks.WaitForScheduledWorkAsync(Ceiling)).Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);

        await producer;
    }

    /// <summary>
    /// How long a releasing thread lets the waiter settle after being told it is about to park.
    /// </summary>
    private static readonly TimeSpan SettleInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Lets the waiter reach its park before the release — an enqueue, or a cancel — lands.
    /// </summary>
    /// <remarks>
    /// Not a poll, and not something an assertion rides on: <b>both</b> interleavings pass — a release that
    /// beats the wait is seen by the pre-check, and one that does not exercises the wake — so this only decides
    /// which of the two mechanisms the run measures. The waiter's next instruction after the signal is the
    /// interlocked claim inside the wait, so a quarter of a second is not a race anything can realistically
    /// lose; and if it did, the run would still be green, merely testing the weaker of the two paths. The
    /// engine cannot offer a signal here — it does not run script while it waits — and probing its ownership
    /// from this thread would have to <em>take</em> ownership, which is the one thing that could make the
    /// waiter fail. It is also why nothing measures this interval: the stopwatch each caller starts is started
    /// <em>after</em> the sleep, immediately before the release it is timing.
    /// </remarks>
    private static void SettleBeforeReleasingTheWait() => Thread.Sleep(SettleInterval);

    /// <summary>
    /// Waits until <paramref name="running"/> is provably parked inside <c>block()</c>. Ends on the signal, on
    /// that thread finishing without ever reaching <c>block()</c> — reporting whatever stopped it rather than a
    /// timeout — or on <see cref="WedgeCeiling"/>.
    /// </summary>
    private static void WaitUntilOwned(ManualResetEventSlim entered, Task running)
    {
        var elapsed = Stopwatch.StartNew();
        while (!entered.Wait(TimeSpan.FromMilliseconds(20)))
        {
            if (running.IsCompleted)
            {
                // Deliberately not `await running`: that throws whatever stopped the call and loses the
                // sentence saying what was being waited for, which is what a reader needs first.
                var failure = running.Exception?.GetBaseException();
                throw new XunitException(
                    "the owning call returned without ever entering block(): "
                    + (failure is null ? "it returned normally" : $"it failed with {failure.GetType().Name}: {failure.Message}"),
                    failure);
            }

            if (elapsed.Elapsed > WedgeCeiling)
            {
                throw new XunitException($"the owning thread did not enter block() within {WedgeCeiling}");
            }
        }
    }
}

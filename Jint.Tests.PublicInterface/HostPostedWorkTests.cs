#nullable enable

using System.Diagnostics;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.TaskOperations.Post"/> from the outside: the one entry a thread that does not own the
/// engine may call, and the only supported way for host code to get a callback onto the engine's own thread.
/// </summary>
/// <remarks>
/// The shape it exists for is one thread per engine — a pump loop parked in
/// <see cref="Engine.TaskOperations.WaitForScheduledWork"/> while everything else in the process hands it
/// work. Every other public entry is refused while that loop owns the engine, so before this a host had to
/// carry a mailbox of its own and a registered promise to wake the park with.
/// </remarks>
public class HostPostedWorkTests
{
    /// <summary>
    /// What the pump loop is given for one idle wait. Nothing should ever spend it — the post is what ends
    /// the wait — so it is a minute, and the margin below is the assertion.
    /// </summary>
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The margin the wake has to beat, derived from the ceiling so the two cannot drift. Half of it is what
    /// separates "woke on the post" from "ran out the ceiling and pumped on the next lap".
    /// </summary>
    private static readonly TimeSpan EarlyReturnMargin = TimeSpan.FromTicks(Ceiling.Ticks / 2);

    private static readonly TimeSpan WedgeCeiling = TestBudgets.WedgeCeiling;

    /// <summary>
    /// The whole contract in one test: a post from a thread that does not own the engine is accepted rather
    /// than refused, it wakes the parked pump, and the action itself runs on the pumping thread — never on
    /// the poster's, which is what makes it safe to touch the engine from inside one.
    /// </summary>
    [Test]
    public async Task PostWakesAParkedPumpAndRunsOnItsThread()
    {
        using var engine = new Engine();
        using var loopStarted = new ManualResetEventSlim();
        using var actionRan = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();

        var pumpThreadId = 0;
        var actionThreadId = 0;
        var elapsed = new Stopwatch();

        var pump = DedicatedThread.RunAsync(() =>
        {
            Volatile.Write(ref pumpThreadId, Environment.CurrentManagedThreadId);
            loopStarted.Set();

            try
            {
                while (!actionRan.IsSet)
                {
                    engine.Tasks.WaitForScheduledWork(Ceiling, cancellation.Token);
                    engine.Tasks.ProcessTasks();
                }
            }
            catch (OperationCanceledException)
            {
                // How the test releases a loop whose post never arrived, so that a regression is reported
                // by the assertion below rather than by a thread left holding the engine.
            }
        });

        loopStarted.Wait(WedgeCeiling).Should().BeTrue();

        // Lets the loop reach the park before the post lands, so that the wake rather than the pre-check is
        // what ends the wait. Both are correct; this only decides which mechanism the run measures.
        Thread.Sleep(TimeSpan.FromMilliseconds(250));

        elapsed.Start();
        engine.Tasks.Post(() =>
        {
            Volatile.Write(ref actionThreadId, Environment.CurrentManagedThreadId);
            actionRan.Set();
        });

        var observed = actionRan.Wait(WedgeCeiling);
        elapsed.Stop();

        cancellation.Cancel();
        await pump;

        observed.Should().BeTrue();
        actionThreadId.Should().Be(pumpThreadId);
        actionThreadId.Should().NotBe(Environment.CurrentManagedThreadId);
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);
    }

    /// <summary>
    /// A posted action is an ordinary event-loop job: it queues behind everything already queued, and one
    /// posted from inside a running job runs after the jobs that were queued before it rather than nesting
    /// inside the one that posted it.
    /// </summary>
    [Test]
    public void PostedWorkRunsInOrderBehindWhatIsAlreadyQueued()
    {
        using var engine = new Engine();

        var order = new List<string>();

        engine.Tasks.Post(() =>
        {
            order.Add("first");
            engine.Tasks.Post(() => order.Add("posted from inside the first"));
        });

        engine.Tasks.Post(() => order.Add("second"));

        engine.Tasks.ProcessTasks();

        order.Should().Equal("first", "second", "posted from inside the first");
    }

    /// <summary>
    /// A posted action that throws behaves like every other job: the exception erupts out of the pump that
    /// ran it, and everything still queued runs on the next turn rather than being lost with it.
    /// </summary>
    [Test]
    public void AnExceptionFromPostedWorkEruptsFromThePump()
    {
        using var engine = new Engine();

        var behind = false;

        engine.Tasks.Post(() => throw new InvalidOperationException("posted work failed"));
        engine.Tasks.Post(() => behind = true);

        Invoking(() => engine.Tasks.ProcessTasks())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("posted work failed");

        behind.Should().BeFalse();

        engine.Tasks.ProcessTasks();

        behind.Should().BeTrue();
        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
    }

    /// <summary>
    /// A post belongs to the evaluation cycle that was current when it was made, so one made before
    /// <see cref="Engine.AdvancedOperations.RestoreGlobalSnapshot"/> ends that cycle is dropped and one made
    /// after it targets the new cycle and runs.
    /// </summary>
    /// <remarks>
    /// From one thread the two halves of the fence agree — the restore both discards what is queued and moves
    /// the generation on — so this asserts the contract rather than isolating the generation stamp. Only a
    /// post racing a restore from another thread can tell them apart, and what it would be asserting then is
    /// the outcome of that race.
    /// </remarks>
    [Test]
    public void PostBelongsToTheCycleItWasMadeIn()
    {
        using var engine = new Engine();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        var beforeRestore = false;
        var afterRestore = false;

        engine.Tasks.Post(() => beforeRestore = true);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Tasks.Post(() => afterRestore = true);
        engine.Tasks.ProcessTasks();

        beforeRestore.Should().BeFalse();
        afterRestore.Should().BeTrue();
    }

    /// <summary>
    /// <see cref="Engine.Dispose"/> is a barrier: a post afterwards is refused with one documented
    /// exception rather than accepted and left to a pump that will never come.
    /// </summary>
    /// <remarks>
    /// The exception is what makes the refusal usable from the thread that does not own the engine — it is
    /// the one thread that can lose the race with the owner's <c>Dispose</c> at any point — and
    /// <see cref="Engine.IsDisposed"/> is how a caller that would rather not catch asks first.
    /// </remarks>
    [Test]
    public void PostAfterDisposeIsRefused()
    {
        var engine = new Engine();
        engine.Dispose();

        var ran = false;

        Invoking(() => engine.Tasks.Post(() => ran = true))
            .Should().Throw<ObjectDisposedException>()
            .Which.ObjectName.Should().Be(nameof(Engine));

        engine.Tasks.ProcessTasks();

        ran.Should().BeFalse();
    }

    [Test]
    public void PostRefusesANullAction()
    {
        using var engine = new Engine();

        Invoking(() => engine.Tasks.Post(null!)).Should().Throw<ArgumentNullException>();
    }
}

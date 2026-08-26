using System.Diagnostics;

namespace Jint.Tests.Runtime;

/// <summary>
/// What an engine does with a manual-promise completion that arrives from a thread other than the one
/// using it: it enqueues, and it drains inline only when the settling thread can claim the engine.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here measures latency, so nothing here may be written as a wall-clock window. Every wait
/// below ends on a signal raised by a thread the test started itself, and the only clock left is
/// <see cref="HandoffCeiling"/> — a ceiling a wedge reports at instead of hanging CI, never a budget
/// the hand-off has to beat.
/// </para>
/// <para>
/// The blocked side runs on <see cref="DedicatedThread"/> rather than <c>Task.Run</c> for the same
/// reason: its body occupies its thread until the test releases it, and a thread-pool worker that a
/// saturated pool injects at roughly one per 500 ms turned "has the other thread entered the engine
/// yet" into a race a fixed budget could lose. That is the flake this class was reported for
/// (sebastienros/jint#3201, seen on net472 as "Expected boolean to be True").
/// </para>
/// </remarks>
public class EngineConcurrencyTests
{
    /// <summary>
    /// Reached only by a genuine wedge: every wait here is released by a thread the test owns, so a
    /// second is already absurd and two minutes cannot be lost to load.
    /// </summary>
    private static readonly TimeSpan HandoffCeiling = TimeSpan.FromMinutes(2);

    [Test]
    public async Task ConcurrentManualPromiseCompletionOnlyEnqueuesWork()
    {
        var engine = new Engine();
        var promise = engine.Tasks.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        var continued = false;
        engine.SetValue("markContinued", new Action(() => Volatile.Write(ref continued, true)));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        engine.SetValue("block", new Action(() =>
        {
            entered.Set();
            release.Wait(HandoffCeiling);
        }));
        engine.Execute("hostPromise.then(markContinued);");

        var running = DedicatedThread.RunAsync(() => engine.Execute("block()"));

        try
        {
            await WaitUntilBlockedInsideTheEngine(entered, running);

            // The engine belongs to the other thread, which is parked inside block(). A completion
            // arriving here may only enqueue: draining it would run markContinued on THIS thread,
            // concurrently with the script the other thread is in the middle of.
            promise.Resolve(42);
            Volatile.Read(ref continued).Should().BeFalse(
                "a completion arriving from another thread may only enqueue while the engine is in use");
        }
        finally
        {
            // Unblock the engine thread whatever happened above, so a failure reports instead of
            // leaving a thread parked on an event this method is about to dispose.
            release.Set();
        }

        await running;

        // Execute drains the loop on its way out, so the enqueued reaction has run by the time the
        // owning thread's Execute has returned. No polling and no window: this is the same thread
        // that was refused the inline drain above, now doing it at its own turn.
        Volatile.Read(ref continued).Should().BeTrue(
            "the enqueued reaction runs when the thread that owns the engine next drains");
    }

    [Test]
    public void ManualPromiseCompletionDrainsInlineAfterExclusiveThreadHandoff()
    {
        var engine = new Engine();
        var promise = engine.Tasks.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        engine.Execute("globalThis.result = 0; hostPromise.then(value => { result = value; });");

        // The mirror image of the test above: Execute has returned, so no thread owns the engine and the
        // settling thread claims it and drains inline. A thread of the test's own with a Join, never a
        // pool hop — the Join is what puts the drain before the assertion with no window in between.
        var thread = new Thread(() => promise.Resolve(42));
        thread.Start();
        thread.Join(HandoffCeiling).Should().BeTrue("the settling thread must not still be running");

        engine.GetValue("result").AsNumber().Should().Be(42);
    }

    [Test]
    public void SameThreadManualPromiseCompletionDrainsInline()
    {
        var engine = new Engine();
        var promise = engine.Tasks.RegisterPromise();
        engine.SetValue("hostPromise", promise.Promise);
        engine.Execute("globalThis.result = 0; hostPromise.then(value => { result = value; });");

        promise.Resolve(42);

        engine.GetValue("result").AsNumber().Should().Be(42);
    }

    /// <summary>
    /// Waits until the engine thread is provably parked inside <c>block()</c>. It ends on one of three
    /// things, and only the last of them is a clock: the signal, the engine thread finishing without
    /// ever reaching <c>block()</c> — which surfaces whatever stopped it rather than reporting a
    /// timeout — or <see cref="HandoffCeiling"/>.
    /// </summary>
    private static async Task WaitUntilBlockedInsideTheEngine(ManualResetEventSlim entered, Task running)
    {
        var elapsed = Stopwatch.StartNew();
        while (!entered.Wait(TimeSpan.FromMilliseconds(20)))
        {
            if (running.IsCompleted)
            {
                // Rethrows the engine thread's own exception with its stack when it had one.
                await running;
                throw new AssertionException("""engine.Execute("block()") returned without ever entering block()""");
            }

            if (elapsed.Elapsed > HandoffCeiling)
            {
                throw new AssertionException($"the engine thread did not enter block() within {HandoffCeiling}");
            }
        }
    }
}

#nullable enable
using System.Runtime.CompilerServices;

namespace Jint.Tests.Runtime;

/// <summary>
/// A finite <c>Atomics.waitAsync</c> timeout used to be a thread-pool task, and the task used to be
/// unstoppable: it slept out the whole interval whatever happened to the wait, holding the waiter —
/// and through it the engine, its realm and the promise capability — alive in its closure, then
/// enqueued a resolution onto an event loop whose engine the host had long finished with. The
/// interval is whatever the script asked for, so nothing bounded it but the script.
/// <para>
/// The timeout is a deadline on an engine-owned registry now, so the retention question is answered
/// differently but has to be answered the same way: a woken wait leaves an entry behind in that heap
/// until its deadline surfaces, and the heap belongs to the engine, so what is left is a cycle
/// entirely inside the graph the host has dropped — collectable, where a live pool task was not.
/// </para>
/// </summary>
// Shares the garbage-collection collection: the retention test below reads GC state, which cannot be
// isolated from tests running in parallel with it.
[Collection(nameof(GarbageCollectionTests))]
public class AtomicsWaitAsyncTests
{
    [Fact]
    public void AWaitWokenByNotifyDoesNotLeaveItsTimeoutTimerHoldingTheEngine()
    {
        // The wait below asks for three minutes and is woken within microseconds. Anything still holding
        // the engine after that is the timeout timer, because there is nothing else left to hold it.
        var reference = WaitNotifyAndForget();

        CollectUntilDead(reference, TimeSpan.FromSeconds(10))
            .Should().BeTrue("the timeout timer of a woken Atomics.waitAsync still pins the engine that owned it");

        // NoInlining so the engine reference cannot be stack-rooted in this frame across the GC.Collect calls.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference WaitNotifyAndForget()
        {
            var engine = new Engine();
            engine.Execute("""
                var i32a = new Int32Array(new SharedArrayBuffer(8));
                Atomics.waitAsync(i32a, 0, 0, 180000);
                Atomics.notify(i32a, 0);
                """);
            engine.Advanced.ProcessTasks();
            return new WeakReference(engine);
        }
    }

    [Fact]
    public void AWaitNobodyWakesStillTimesOut()
    {
        // The control for the test above: cancelling the timer on a wake must not stop it firing when
        // there is no wake, and the promise must still settle with "timed-out".
        var engine = new Engine();
        engine.Execute("""
            var i32a = new Int32Array(new SharedArrayBuffer(8));
            var outcome = 'pending';
            Atomics.waitAsync(i32a, 0, 0, 50).value.then(function (v) { outcome = v; });
            """);

        DrainUntil(engine, "outcome !== 'pending'", TimeSpan.FromSeconds(10));

        engine.Evaluate("outcome").AsString().Should().Be("timed-out");
    }

    [Fact]
    public void AWaitWokenByNotifyResolvesWithOk()
    {
        // The other control: a wake still wins the race against a timeout that has not elapsed.
        var engine = new Engine();
        engine.Execute("""
            var i32a = new Int32Array(new SharedArrayBuffer(8));
            var outcome = 'pending';
            Atomics.waitAsync(i32a, 0, 0, 180000).value.then(function (v) { outcome = v; });
            var notified = Atomics.notify(i32a, 0);
            """);

        engine.Evaluate("notified").AsNumber().Should().Be(1);

        DrainUntil(engine, "outcome !== 'pending'", TimeSpan.FromSeconds(10));

        engine.Evaluate("outcome").AsString().Should().Be("ok");
    }

    private static void DrainUntil(Engine engine, string condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            engine.Advanced.ProcessTasks();
            if (engine.Evaluate(condition).AsBoolean())
            {
                return;
            }

            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// Collects until <paramref name="reference"/> dies or <paramref name="timeout"/> elapses. The retry is
    /// not a weaker assertion: it was written for the era when the timer task observed its cancellation on a
    /// thread pool thread, leaving a short window in which its state machine was still reachable. Without the
    /// fix the engine stayed alive for the wait's full three minutes, which no plausible window covers.
    /// </summary>
    private static bool CollectUntilDead(WeakReference reference, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

            if (!reference.IsAlive)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(25);
        }
    }
}

#nullable enable
using System.Diagnostics;
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
// Non-parallel for the reason <see cref="GarbageCollectionTests"/> is: the retention test below reads GC
// state, which cannot be isolated from tests running in parallel with it.
[NonParallelizable]
public class AtomicsWaitAsyncTests
{
    [Test]
    public void AWaitWokenByNotifyDoesNotLeaveItsTimeoutTimerHoldingTheEngine()
    {
        // The wait below asks for three minutes and is woken within microseconds. Anything still holding
        // the engine after that is the timeout timer, because there is nothing else left to hold it.
        var reference = WaitNotifyAndForget();

        // Ten seconds is four hundred forced blocking gen-2 collections, and that is not a wall-clock margin:
        // an object that is rooted does not stop being rooted because more time passed, so the budget only
        // has to outlast the transient window the remarks on CollectUntilDead describe. Widening it would buy
        // no reliability and would cost two minutes of full collections on a genuine regression.
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
            engine.Tasks.ProcessTasks();
            return new WeakReference(engine);
        }
    }

    [Test]
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

        DrainUntil(engine, "outcome !== 'pending'");

        engine.Evaluate("outcome").AsString().Should().Be("timed-out");
    }

    [Test]
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

        DrainUntil(engine, "outcome !== 'pending'");

        engine.Evaluate("outcome").AsString().Should().Be("ok");
    }

    /// <summary>
    /// Pumps until <paramref name="condition"/> holds, or fails saying that it never did.
    /// </summary>
    /// <remarks>
    /// The budget is a <see cref="TestBudgets.WedgeCeiling"/>: nothing here asserts how long the pump took —
    /// the callers assert what the wait settled <em>with</em> — so widening it can hide nothing, and the ten
    /// seconds it was could be reached by a runner slow enough without anything being wrong. Running out of
    /// it now says so: this used to return silently, so an exhausted budget reported as
    /// <c>outcome == "pending"</c>, which reads as a settlement defect rather than as the wedge it is.
    /// </remarks>
    private static void DrainUntil(Engine engine, string condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            engine.Tasks.ProcessTasks();
            if (engine.Evaluate(condition).AsBoolean())
            {
                return;
            }

            if (elapsed.Elapsed >= TestBudgets.WedgeCeiling)
            {
                Assert.Fail($"the engine never reached `{condition}` within {TestBudgets.WedgeCeiling}");
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

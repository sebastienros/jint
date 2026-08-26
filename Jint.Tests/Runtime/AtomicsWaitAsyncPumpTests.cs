#nullable enable
using System.Diagnostics;
using Jint.Native;
using Jint.Native.Promise;

namespace Jint.Tests.Runtime;

/// <summary>
/// A finite <c>Atomics.waitAsync</c> timeout used to be a <c>Task.Run</c> whose <c>Task.Delay</c> resumed on a
/// second thread-pool dispatch, so settling a one-millisecond wait needed two threads from a pool that injects
/// roughly one every 500ms once it is saturated — while the engine's own thread, the one the script is polling
/// on, was free the whole time. test262's <c>waitAsync</c> family gives itself a fixed wall-clock lifespan and
/// polls for the outcome on exactly that thread, which is what made it flake on loaded CI runners.
/// <para>
/// The timeout is now a deadline on an engine-owned registry that the event-loop pump reads, so the thread
/// pool is out of the settlement path entirely. These tests pin that: none of them measures how long anything
/// took, and none of them assumes a thread is available.
/// </para>
/// </summary>
public class AtomicsWaitAsyncPumpTests
{
    private const string SharedInt32Array = "var i32a = new Int32Array(new SharedArrayBuffer(8));";

    [Fact]
    public void NothingButThePumpEverSettlesAFiniteTimeout()
    {
        // The structural proof that no Task is in the settlement path. The old timeout task did not need the
        // engine to be pumped in order to run: it resolved the waiter and enqueued the settlement job itself,
        // from the pool. So an engine left alone for far longer than the timeout it was asked for used to end
        // up with a non-empty event loop, and now ends up with an empty one.
        var engine = new Engine();
        engine.Execute(SharedInt32Array + "var outcome = 'pending'; Atomics.waitAsync(i32a, 0, 0, 1).value.then(function (v) { outcome = v; });");

        engine.EventLoop.IsEmpty.Should().BeTrue("nothing is queued yet: the wait has only just been registered");

        // Far more than the millisecond asked for, and the engine is not touched once.
        Thread.Sleep(250);

        engine.EventLoop.IsEmpty.Should().BeTrue("only the pump may settle a timed-out wait, and nothing has pumped");

        engine.Tasks.ProcessTasks();

        engine.Evaluate("outcome").AsString().Should().Be("timed-out");
    }

    [Fact]
    public void ATimedOutWaitSettlesOnTheThreadThatPumped()
    {
        // The other half: the settlement runs as an ordinary event-loop job, so it runs wherever the host
        // called ProcessTasks from — never on a pool thread of the engine's choosing.
        var engine = new Engine();
        engine.SetValue("record", new Func<int>(static () => Environment.CurrentManagedThreadId));
        engine.Execute(SharedInt32Array + "var settledOn = 0; Atomics.waitAsync(i32a, 0, 0, 1).value.then(function () { settledOn = record(); });");

        var pumpedOn = PumpUntil(engine, "settledOn !== 0");

        engine.Evaluate("settledOn").AsNumber().Should().Be(pumpedOn, "the settlement is a job like any other, so it runs on the pumping thread");
    }

    [Fact]
    public void AMicrotaskSpinThatNeverEmptiesTheQueueStillSeesTheTimeout()
    {
        // The shape of test262's $262.agent.setTimeout polyfill, which is a promise chain rather than a real
        // timer: every job it runs queues the next one, so the event loop never runs dry for as long as the
        // script is polling. A timeout looked at only when the queue is exhausted would never be looked at at
        // all here, and the poll would run out its lifespan and report the wait as never having settled.
        var engine = new Engine();
        engine.Execute(SharedInt32Array + """
            var outcome = 'pending';
            var polls = 0;
            Atomics.waitAsync(i32a, 0, 0, 1).value.then(function (v) { outcome = v; });

            function poll() {
                if (outcome !== 'pending' || ++polls > 500000) {
                    return;
                }
                Promise.resolve().then(poll);
            }

            poll();
            """);

        engine.Evaluate("outcome").AsString().Should().Be("timed-out", "the pump must look at the deadline between jobs, not only when it runs out of them");
        engine.Evaluate("polls").AsNumber().Should().BeLessThan(500000, "the poll must have stopped because the wait settled, not because it gave up");
    }

    [Fact]
    public void ANotifyBeatsATimeoutThatHasNotElapsed()
    {
        var engine = new Engine();
        engine.Execute(SharedInt32Array + """
            var outcomes = [];
            Atomics.waitAsync(i32a, 0, 0, 180000).value.then(function (v) { outcomes.push(v); });
            var notified = Atomics.notify(i32a, 0);
            """);

        engine.Evaluate("notified").AsNumber().Should().Be(1);

        PumpUntil(engine, "outcomes.length !== 0");

        engine.Evaluate("outcomes.join()").AsString().Should().Be("ok");
    }

    [Fact]
    public void ATimeoutBeatsANotifyThatArrivesAfterIt()
    {
        var engine = new Engine();
        engine.Execute(SharedInt32Array + "var outcomes = []; Atomics.waitAsync(i32a, 0, 0, 1).value.then(function (v) { outcomes.push(v); });");

        PumpUntil(engine, "outcomes.length !== 0");

        engine.Evaluate("outcomes.join()").AsString().Should().Be("timed-out");

        // The wait left its list when it timed out, exactly as a woken one does, so there is nothing left for
        // a later notify to find — and nothing that could settle the promise a second time.
        engine.Evaluate("Atomics.notify(i32a, 0)").AsNumber().Should().Be(0, "a timed-out wait is no longer a waiter");

        engine.Tasks.ProcessTasks();

        engine.Evaluate("outcomes.join()").AsString().Should().Be("timed-out", "a promise settles exactly once");
    }

    [Fact]
    public void ManyWaitsOnOneIndexAllTimeOutInDeadlineOrder()
    {
        // The registry is a min-heap, so this is the pin that it orders and empties correctly: waits
        // registered out of deadline order must settle in deadline order, and every one of them must settle.
        // One caveat makes the order half conditional: each deadline is "now + delay" at its own
        // registration, so if the thread is descheduled for longer than the smallest gap between two
        // registrations — which a heavily loaded machine can do to it — the deadlines themselves reorder
        // and the settle order legitimately follows. The script therefore times its own registration loop,
        // and the exact-order assertion runs only when the loop was tighter than the smallest gap; that every
        // wait settles exactly once is asserted unconditionally.
        var engine = new Engine();
        engine.Execute(SharedInt32Array + """
            var outcomes = [];
            var delays = { g: 70, a: 10, e: 50, b: 20, f: 60, c: 30, d: 40 };
            var regStart = Date.now();
            Object.keys(delays).forEach(function (name) {
                Atomics.waitAsync(i32a, 0, 0, delays[name]).value.then(function () { outcomes.push(name); });
            });
            var regMillis = Date.now() - regStart;
            """);

        PumpUntil(engine, "outcomes.length === 7");

        engine.Evaluate("outcomes.slice().sort().join('')").AsString().Should().Be("abcdefg");

        if (engine.Evaluate("regMillis").AsNumber() < 10)
        {
            engine.Evaluate("outcomes.join('')").AsString().Should().Be("abcdefg");
        }
    }

    [Fact]
    public void TheBlockingUnwrapCompletesAShortTimeout()
    {
        // UnwrapIfPromise drives DrainEventLoopUntil, whose idle wait nothing wakes when a deadline passes —
        // the clamp on its poll interval is what ends the wait in time.
        var engine = new Engine();
        var promise = engine.Evaluate(SharedInt32Array + "Atomics.waitAsync(i32a, 0, 0, 1).value");

        // The unwrap's own budget is a wedge ceiling here — what is asserted is the value the wait settled
        // with, and a clamp that regressed never ends the wait at all, so no budget can be too generous.
        promise.UnwrapIfPromise(TestBudgets.WedgeCeiling).AsString().Should().Be("timed-out");
    }

    [Fact]
    public async Task TheAsyncUnwrapCompletesAShortTimeout()
    {
        // The same for AwaitPromiseSettlementAsync, whose wait is bounded by the deadline instead.
        var engine = new Engine();
        var result = await engine.EvaluateAsync(SharedInt32Array + "Atomics.waitAsync(i32a, 0, 0, 1).value");

        result.AsString().Should().Be("timed-out");
    }

    [Fact]
    public void ARestoredEngineNeverSeesTheTimeoutOfAWaitTheEndedCycleRegistered()
    {
        var engine = new Engine();
        engine.Execute("var outcome = 'pending';");
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute(SharedInt32Array + "Atomics.waitAsync(i32a, 0, 0, 1).value.then(function (v) { outcome = v; });");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // Well past the millisecond the ended cycle asked for, and pumped repeatedly.
        PumpFor(engine, 200);

        engine.Evaluate("outcome").AsString().Should().Be("pending", "a wait registered before the restore must not settle into the restored globals");
    }

    [Fact]
    public void AWaitAskingForNoTimeoutIsNeverSettledByThePump()
    {
        // An infinite wait registers no deadline at all, so nothing but Atomics.notify can end it. The
        // control for the registry: it must not invent a timeout for a wait that asked for none.
        var engine = new Engine();
        var promise = (JsPromise) engine.Evaluate(SharedInt32Array + "Atomics.waitAsync(i32a, 0, 0).value");

        PumpFor(engine, 100);

        promise.State.Should().Be(PromiseState.Pending);

        engine.Evaluate("Atomics.notify(i32a, 0)").AsNumber().Should().Be(1);
        engine.Tasks.ProcessTasks();

        promise.State.Should().Be(PromiseState.Fulfilled);
        promise.Value.AsString().Should().Be("ok");
    }

    /// <summary>
    /// Pumps the engine on this thread until <paramref name="condition"/> holds, and answers which thread that
    /// was. The bound exists so a broken engine fails the run rather than hanging it — nothing is asserted
    /// about how long the loop actually took, which is the property that keeps these tests off the wall
    /// clock, and which makes <see cref="TestBudgets.WedgeCeiling"/> the right length for it rather than the
    /// thirty seconds a loaded runner could reach on its own (#3379).
    /// </summary>
    private static int PumpUntil(Engine engine, string condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TestBudgets.WedgeCeiling)
        {
            engine.Tasks.ProcessTasks();
            if (engine.Evaluate(condition).AsBoolean())
            {
                return Environment.CurrentManagedThreadId;
            }

            Thread.Sleep(1);
        }

        throw new InvalidOperationException($"The engine never reached `{condition}`.");
    }

    /// <summary>
    /// Pumps for <paramref name="milliseconds"/> without asking for anything, which is how the two tests that
    /// assert something did <em>not</em> happen give it every opportunity to.
    /// </summary>
    private static void PumpFor(Engine engine, int milliseconds)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < milliseconds)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(1);
        }
    }
}

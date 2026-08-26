#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The event loop's generation fence, from the embedder's side. Work a host registers in one evaluation
/// cycle must not settle into the engine after <c>Engine.Advanced.RestoreGlobalSnapshot</c> has ended that
/// cycle — otherwise a pooled engine gets the previous request's continuation running against the next
/// request's globals, the cross-cycle channel a fresh-engine-per-evaluation host never had.
///
/// <para>
/// The awkward cases are the ones whose settle originates off the engine thread, because the generation has
/// to be captured where the work is <em>registered</em> rather than read where it is enqueued.
/// <c>Atomics.waitAsync</c> is one: its timeout fires on a timer thread and its wake can arrive from
/// whichever agent calls <c>Atomics.notify</c> on the shared buffer. Each fenced case is paired with a
/// control proving the same settle does land when no restore intervenes — a fence that works by never
/// delivering anything is not a fence.
/// </para>
/// </summary>
public class HostEventLoopFenceTests
{
    private const string RegisterTimeoutWait = """
        const ta = new Int32Array(new SharedArrayBuffer(8));
        Atomics.waitAsync(ta, 0, 0, 50).value.then(v => { globalThis.settled = v; });
        """;

    private const string RegisterInfiniteWait = """
        globalThis.ta = new Int32Array(new SharedArrayBuffer(8));
        Atomics.waitAsync(globalThis.ta, 0, 0).value.then(v => { globalThis.settled = v; });
        """;

    private static void Pump(Engine engine, TimeSpan duration)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(5);
        }

        engine.Tasks.ProcessTasks();
    }

    private static bool PumpUntilSettled(Engine engine, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            engine.Tasks.ProcessTasks();
            if (!engine.Evaluate("globalThis.settled").IsUndefined())
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return false;
    }

    [Test]
    public void AtomicsWaitAsyncTimeoutDoesNotSettleAfterAGlobalRestore()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute(RegisterTimeoutWait);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // Far longer than the 50 ms the wait was given, and with turns to spare.
        Pump(engine, TimeSpan.FromSeconds(2));

        engine.Evaluate("globalThis.settled").Should().Be(JsValue.Undefined);
    }

    [Test]
    public void AtomicsWaitAsyncTimeoutSettlesWhenNoRestoreIntervenes()
    {
        var engine = new Engine();

        engine.Execute(RegisterTimeoutWait);

        PumpUntilSettled(engine, TimeSpan.FromSeconds(10)).Should().BeTrue("the timeout must still be delivered when no restore ends the cycle");
        engine.Evaluate("globalThis.settled").AsString().Should().Be("timed-out");
    }

    [Test]
    public void AtomicsNotifyDoesNotSettleAWaitAsyncFromAnEndedCycle()
    {
        var engine = new Engine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute(RegisterInfiniteWait);

        // The typed array outlives the restore — a restore reverts global bindings, not object graphs — so
        // the host can hand it back and wake the waiter registered by the cycle that has now ended.
        var typedArray = engine.Evaluate("globalThis.ta");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.SetValue("ta", typedArray);
        engine.Evaluate("Atomics.notify(ta, 0)").AsNumber().Should().Be(1, "the waiter is still on the shared buffer's list");

        Pump(engine, TimeSpan.FromMilliseconds(500));

        engine.Evaluate("globalThis.settled").Should().Be(JsValue.Undefined);
    }

    [Test]
    public void AtomicsNotifySettlesAWaitAsyncWhenNoRestoreIntervenes()
    {
        var engine = new Engine();

        engine.Execute(RegisterInfiniteWait);
        engine.Evaluate("Atomics.notify(globalThis.ta, 0)").AsNumber().Should().Be(1);

        PumpUntilSettled(engine, TimeSpan.FromSeconds(10)).Should().BeTrue("a wake must still be delivered when no restore ends the cycle");
        engine.Evaluate("globalThis.settled").AsString().Should().Be("ok");
    }
}

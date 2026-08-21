#nullable enable

using System.Diagnostics;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.AdvancedOperations.WaitForScheduledWork"/> and
/// <see cref="Engine.AdvancedOperations.WaitForScheduledWorkAsync"/> from the outside — the other half of
/// <see cref="HostScheduledWorkTests"/>, which covers the <em>when to pump</em> question this one parks on.
/// </summary>
/// <remarks>
/// Deliberately <b>not</b> gated on <c>NET8_0_OR_GREATER</c>: the wait is all-target-framework
/// <c>Advanced</c> surface, and everything it is pointed at here — an event-loop job produced by settling a
/// host-registered promise from another thread — is core engine machinery. The .NET 8 sources it also serves
/// (a due timer) are covered in <c>Jint.Tests</c>.
/// </remarks>
public class HostPumpWaitTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan EarlyReturnMargin = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan WedgeCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The shape the wait exists for: one thread parked on this engine, another handing it work. A settled
    /// promise is the public core-engine way to do that — <c>Resolve</c> may be called from any thread and
    /// enqueues onto the engine's own loop — and it stands in for every other cross-thread arrival a host sees
    /// (a message posted from a second engine's thread, an asynchronous module load completing, an interop
    /// <see cref="Task"/> settling). None of them has a due time, so nothing but the wake can report them, and
    /// a host that slept on a ceiling instead paid that ceiling in latency on every one.
    /// </summary>
    /// <remarks>
    /// The wait runs inside a host callback the script itself invokes, which is what makes the handshake
    /// deterministic rather than timed: the engine is owned by this thread from the moment
    /// <see cref="Engine.Execute(string, string?)"/> is entered, so the producer's settle can only ever be
    /// enqueued — never drained on the producer's own thread — whichever side of the park it lands on. The
    /// settle interval below therefore decides only <em>which</em> mechanism the run measures (the pre-check or
    /// the wake), and never whether it passes.
    /// </remarks>
    [Fact]
    public async Task WaitForScheduledWorkWakesOnACrossThreadPost()
    {
        using var engine = new Engine();
        using var producerArmed = new ManualResetEventSlim();

        var manual = engine.Advanced.RegisterPromise();
        var elapsed = new Stopwatch();

        engine.SetValue("hostWork", manual.Promise);
        engine.SetValue("armProducer", new Action(() =>
        {
            elapsed.Start();
            producerArmed.Set();
        }));
        engine.SetValue("park", new Func<bool>(() => engine.Advanced.WaitForScheduledWork(Ceiling)));

        var producer = DedicatedThread.RunAsync(() =>
        {
            producerArmed.Wait(WedgeCeiling);

            // Lets the wait be reached before the settle lands, so the wake rather than the pre-check is what
            // ends it. Both are correct and both pass — see the remarks above.
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
            manual.Resolve(JsValue.Undefined);
        });

        engine.Execute("""
            globalThis.delivered = false;
            hostWork.then(function () { globalThis.delivered = true; });
            armProducer();
            globalThis.reported = park();
            """);

        await producer;

        engine.Evaluate("reported").AsBoolean().Should().BeTrue();
        elapsed.Elapsed.Should().BeLessThan(EarlyReturnMargin);

        // The wait reports work; the pump runs it. Execute's own trailing drain is that pump here.
        engine.Evaluate("delivered").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The asynchronous form is reachable from outside the assembly, keeps the same contract, and hands the
    /// engine back afterwards — the reservation it holds across the await must not outlive the returned task,
    /// or a host's very next call would be refused as concurrent use. What it wakes on is the same set as the
    /// synchronous form's, exercised by <c>Jint.Tests</c>'s <c>WakesOnACrossThreadEnqueueAsync</c>.
    /// </summary>
    [Fact]
    public async Task WaitForScheduledWorkAsyncIsReachable()
    {
        using var engine = new Engine();

        (await engine.Advanced.WaitForScheduledWorkAsync(TimeSpan.FromMilliseconds(50))).Should().BeFalse();

        engine.Evaluate("1 + 1").AsNumber().Should().Be(2);
        (await engine.Advanced.WaitForScheduledWorkAsync(TimeSpan.Zero)).Should().BeFalse();
    }
}

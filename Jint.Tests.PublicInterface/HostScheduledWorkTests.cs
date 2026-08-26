#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.TaskOperations.TimeUntilNextScheduledWork"/> — the answer a host driving its own loop
/// needs before it decides whether to call <see cref="Engine.TaskOperations.ProcessTasks"/>.
///
/// <para>
/// This file is deliberately <b>not</b> gated on <c>NET8_0_OR_GREATER</c>: the property is part of the
/// all-target-framework <c>Tasks</c> surface, and the source it reports on every target framework is a core
/// one — the deadline of an <c>Atomics.waitAsync</c>, whose settle is produced by a background delay rather
/// than by anything the engine does, so nothing else can tell a host when to pump for it. The web-API sources
/// (timers, delayed scheduler tasks, idle callbacks) are covered by
/// <c>WebApiSchedulingSurfaceTests</c>, which is gated because they only exist on .NET 8 and later.
/// </para>
/// </summary>
public class HostScheduledWorkTests
{
    /// <summary>
    /// An engine with nothing scheduled answers <see langword="null"/> — not zero, which would send a host loop
    /// into a hot spin, and not some arbitrary poll interval, which would be a number the engine invented.
    /// </summary>
    [Test]
    public void AnEngineWithNothingScheduledReportsNoWork()
    {
        using var engine = new Engine();
        engine.Execute("var x = 1 + 1;");

        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// An <c>Atomics.waitAsync</c> with a finite timeout is the one wait in the core engine that ends because a
    /// clock reached a number rather than because something enqueued: its "timed-out" resolution is produced by
    /// a background delay. So the engine reports its deadline, and a host loop learns when to pump for it.
    /// </summary>
    /// <remarks>
    /// The bounds are structural rather than timed. The script asks for an hour, so the only assertion is that
    /// the answer is a positive span no larger than what was asked for — true however long the machine stalls
    /// between the two statements, which is what keeps this test off the clock. The wait is then woken with
    /// <c>Atomics.notify</c>, which both proves the deadline stops being reported once the wait has settled and
    /// stops the background delay outliving the test.
    /// </remarks>
    [Test]
    public void AnAsynchronousAtomicsWaitReportsItsTimeoutDeadline()
    {
        using var engine = new Engine();

        engine.Execute("""
            globalThis.ta = new Int32Array(new SharedArrayBuffer(8));
            globalThis.settled = null;
            Atomics.waitAsync(ta, 0, 0, 3600000).value.then(v => { globalThis.settled = v; });
            """);

        var untilDeadline = engine.Tasks.TimeUntilNextScheduledWork;
        untilDeadline.Should().NotBeNull();
        untilDeadline!.Value.Should().BeGreaterThan(TimeSpan.Zero);
        untilDeadline.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(1));

        // Woken by the other route. The waiter is settled from here on, so its deadline predicts nothing and
        // must stop being reported — otherwise a host loop would keep waking for an hour for a wait that has
        // already finished.
        engine.Execute("Atomics.notify(ta, 0);");

        engine.Evaluate("settled").Should().Be(new JsString("ok"));
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// An infinite <c>Atomics.waitAsync</c> has no deadline at all — only another agent's <c>Atomics.notify</c>
    /// can end it, and that arrives as an enqueue, which needs no clock. Reporting a time for it would be
    /// inventing one.
    /// </summary>
    [Test]
    public void AnInfiniteAtomicsWaitReportsNoDeadline()
    {
        using var engine = new Engine();

        engine.Execute("""
            globalThis.ta = new Int32Array(new SharedArrayBuffer(8));
            Atomics.waitAsync(ta, 0, 0).value.then(() => {});
            """);

        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();

        // Leave nothing waiting behind the test.
        engine.Execute("Atomics.notify(ta, 0);");
    }
}

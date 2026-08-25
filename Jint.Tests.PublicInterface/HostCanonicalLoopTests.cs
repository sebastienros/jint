#nullable enable

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The canonical host loop, written the way an embedder writes it — which is the whole claim of
/// <see cref="Engine.Tasks"/>. Jint never starts a thread, so a <c>setTimeout</c> callback runs only on a
/// turn the host gives the engine: <see cref="Engine.TaskOperations.TimeUntilNextScheduledWork"/> says when
/// the next turn is due, and <see cref="Engine.TaskOperations.ProcessTasks"/> takes it.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so what is written below is exactly what a third party can
/// write, against exactly the names it can see. Before this facet existed those two calls were two of some
/// forty members of <c>engine.Advanced</c> — a name that reads as a warning rather than as an instruction,
/// for a loop a host using timers has no choice about.
/// </remarks>
public class HostCanonicalLoopTests
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// A host-supplied clock, so that a test about turns need not sleep for one. Only
    /// <see cref="TimeProvider.GetTimestamp"/> is ever asked for.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }
#endif

    /// <summary>
    /// Pumping an engine that has nothing scheduled is legal and does nothing — the shape a host loop takes
    /// on its very first iteration, before any script has run.
    /// </summary>
    [Fact]
    public void PumpingAnEngineWithNothingScheduledIsANoOp()
    {
        using var engine = new Engine();

        engine.Tasks.ProcessTasks();

        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The whole loop, end to end: turn the timers on, let script schedule a callback, ask when it is due,
    /// and give the engine the turn it needs to run it. Nothing else in the public surface runs that
    /// callback.
    /// </summary>
    [Fact]
    public void TheCanonicalHostLoopRunsATimerCallback()
    {
        var ran = false;
        var clock = new ManualClock();

        using var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));
        engine.SetValue("record", new Action(() => ran = true));

        engine.Execute("setTimeout(() => record(), 25);");

        // Scheduled, not run — and the engine says how long the host may leave it alone.
        ran.Should().BeFalse();
        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.FromMilliseconds(25));

        clock.Advance(25);

        // Due now. A host loop reads zero here and pumps.
        engine.Tasks.TimeUntilNextScheduledWork.Should().Be(TimeSpan.Zero);
        engine.Tasks.ProcessTasks();

        ran.Should().BeTrue();
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    /// <summary>
    /// The same claim as its contrapositive, because that is the one that costs embedders an afternoon: an
    /// engine nobody pumps never runs the callback, however far past the deadline the host gets.
    /// </summary>
    [Fact]
    public void AnEngineNobodyPumpsNeverRunsTheCallback()
    {
        var ran = false;
        var clock = new ManualClock();

        using var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));
        engine.SetValue("record", new Action(() => ran = true));

        engine.Execute("setTimeout(() => record(), 25);");

        // An hour of the host's clock, and no turn: still nothing.
        clock.Advance(60 * 60 * 1000);
        ran.Should().BeFalse();

        engine.Tasks.ProcessTasks();
        ran.Should().BeTrue();
    }
#endif
}

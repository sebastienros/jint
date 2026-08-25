#if NET8_0_OR_GREATER

// Reads a Jint diagnostic API declared outside the compatibility contract; see Jint/JintDiagnosticIds.cs.
#pragma warning disable JINT0001

#nullable enable

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The timer half of <c>Engine.Diagnostics.GetMemoryReport()</c>. A registered timer holds its callback — and
/// through the callback's closure, whatever that closure captured — until it fires or is cleared, which makes
/// "how many are registered" a retention question a pooling host asks. Everything here drives a manual clock,
/// so nothing depends on how long the test takes to run.
/// </summary>
public class MemoryReportTimerTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private static (Engine Engine, ManualClock Clock) TimerEngine()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));
        return (engine, clock);
    }

    [Fact]
    public void PendingTimersAreCountedUntilTheyFire()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("var fired = 0; setTimeout(() => { fired++; }, 50); setTimeout(() => { fired++; }, 100);");
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(2);

        clock.Advance(50);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("fired").AsNumber().Should().Be(1);
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(1);

        clock.Advance(50);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("fired").AsNumber().Should().Be(2);
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(0);
    }

    [Fact]
    public void AClearedTimerStopsBeingCounted()
    {
        var (engine, _) = TimerEngine();

        engine.Execute("var id = setTimeout(() => {}, 1000);");
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(1);

        engine.Execute("clearTimeout(id);");
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(0);
    }

    [Fact]
    public void AnIntervalStaysOneTimerHoweverOftenItHasFired()
    {
        var (engine, clock) = TimerEngine();

        engine.Execute("var ticks = 0; var id = setInterval(() => { ticks++; }, 10);");

        for (var i = 0; i < 5; i++)
        {
            clock.Advance(10);
            engine.Tasks.ProcessTasks();
        }

        engine.Evaluate("ticks").AsNumber().Should().Be(5);
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(1);

        engine.Execute("clearInterval(id);");
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(0);
    }

    [Fact]
    public void RestoringAGlobalSnapshotDropsTheTimersTheCycleScheduled()
    {
        var (engine, _) = TimerEngine();
        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("setTimeout(() => {}, 1000);");
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(1);

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The restore ends the evaluation cycle the timer belongs to, and the report is how a host can see
        // that the timer really went with it rather than merely being unable to fire.
        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(0);
    }

    [Fact]
    public void AnEngineWithoutTheTimerFeatureReportsNone()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        engine.Execute("var x = 1;");

        engine.Diagnostics.GetMemoryReport().PendingTimerCount.Should().Be(0);
    }
}
#endif

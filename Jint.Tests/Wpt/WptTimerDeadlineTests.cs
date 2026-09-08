#if NET8_0_OR_GREATER
#nullable enable

using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.Wpt;

/// <summary>Separates the timer corpus's watchdog deadlines from interval cancellation and late-error cleanup.</summary>
public sealed class WptTimerDeadlineTests
{
    private sealed class ManualClock : TimeProvider
    {
        private long _milliseconds;

        public override long TimestampFrequency => 1000;

        public override long GetTimestamp() => _milliseconds;

        internal void Advance(int milliseconds) => _milliseconds += milliseconds;
    }

    private static Engine NegativeIntervalEngine(TimeProvider clock, WptDiagnosticsSink sink, Action<Engine>? beforeSource = null)
    {
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            webApi.Diagnostics.Sink = sink;
        }));
        sink.Watch(engine);
        engine.Execute(WptCorpus.Prelude);
        beforeSource?.Invoke(engine);
        // Keep upstream's twenty callbacks, negative delay, and uncancelled one-second watchdog intact.
        engine.Execute(WptCorpus.Read("html/webappapis/timers/negative-setinterval.any.js"));
        return engine;
    }

    [Test]
    public void NegativeIntervalCancelsAtTwentyAndItsLateWatchdogIsIgnored()
    {
        var clock = new ManualClock();
        var sink = new WptDiagnosticsSink();
        var engine = NegativeIntervalEngine(clock, sink);

        // HTML clamps each nested interval firing to four milliseconds once nesting exceeds five.
        engine.GetValue("i").Should().Be(6);
        for (var tick = 6; tick < 20; tick++)
        {
            clock.Advance(4);
            engine.Tasks.ProcessTasks();
        }

        engine.GetValue("i").Should().Be(20);
        engine.GetValue("__wpt").AsObject().Get("fileTestComplete").Should().Be(true);
        sink.UncaughtCallbackErrors.Should().BeEmpty();

        clock.Advance(1000);
        engine.Tasks.ProcessTasks();

        engine.GetValue("i").Should().Be(20, "clearInterval must cancel the already re-armed interval");
        sink.UncaughtCallbackErrors.Should().BeEmpty("the file's one test completed before its watchdog fired");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    [Test]
    public void NegativeIntervalWatchdogReportsIfTheHostDoesNotPumpBeforeItsDeadline()
    {
        var clock = new ManualClock();
        var sink = new WptDiagnosticsSink();
        var engine = NegativeIntervalEngine(clock, sink);

        engine.GetValue("i").Should().Be(6);
        // Model a delayed host/thread resumption, without sleeping or weakening upstream's deadline.
        clock.Advance(1001);
        engine.Tasks.ProcessTasks();

        engine.GetValue("i").Should().Be(7);
        engine.GetValue("__wpt").AsObject().Get("fileTestComplete").Should().Be(false);
        sink.UncaughtCallbackErrors.Should().Equal("Timer: reached unreachable code");

        for (var tick = 7; tick < 20; tick++)
        {
            clock.Advance(4);
            engine.Tasks.ProcessTasks();
        }

        engine.GetValue("i").Should().Be(20);
        engine.GetValue("__wpt").AsObject().Get("fileTestComplete").Should().Be(true);
        sink.UncaughtCallbackErrors.Should().HaveCount(1, "later completion cannot erase a prior watchdog failure");
        engine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    private static WptTimerClock ControlledClock(TimeProvider? hostClock = null)
        => WptTimerClock.ForFile(WptTimerClock.File, WptCorpus.Read(WptTimerClock.File),
            WptCorpus.Prelude, hasMetaScripts: false, hostClock)!;

    [Test]
    public void TheSameHostStarvationFailsWithElapsedTimeAndCompletesWithControlledTime()
    {
        var hostClock = new ManualClock();
        var controlledClock = ControlledClock(hostClock);
        var elapsedSink = new WptDiagnosticsSink();
        var controlledSink = new WptDiagnosticsSink();
        var elapsedEngine = NegativeIntervalEngine(hostClock, elapsedSink);
        var controlledEngine = NegativeIntervalEngine(controlledClock, controlledSink);

        // Identical host starvation for both engines, after initial zero-delay firings. Only the source of
        // timer timestamps differs; each engine still uses its real TimerQueue and the unchanged corpus.
        for (var turn = 6; turn < 20; turn++)
        {
            hostClock.Advance(1001);
            elapsedEngine.Tasks.ProcessTasks();
            controlledClock.GetTimestamp().Should().Be((turn - 6) * 4 * TimeSpan.TicksPerMillisecond);
            controlledClock.Advance(controlledEngine.Tasks.TimeUntilNextScheduledWork!.Value);
            controlledClock.RecordPump();
            controlledEngine.Tasks.ProcessTasks();
        }

        elapsedSink.UncaughtCallbackErrors.Should().Equal("Timer: reached unreachable code");
        controlledSink.UncaughtCallbackErrors.Should().BeEmpty();
        controlledEngine.GetValue("i").Should().Be(20);
        controlledEngine.GetValue("__wpt").AsObject().Get("fileTestComplete").Should().Be(true);
        controlledClock.Evidence.Should().Contain("pumps=14, advances=14, timerElapsed=00:00:00.0560000");
        controlledClock.Evidence.Should().Contain("hostElapsed=00:00:14.0140000");

        // The late guard really executes, and the previously queued interval really is cancelled.
        controlledClock.Advance(controlledEngine.Tasks.TimeUntilNextScheduledWork!.Value);
        controlledEngine.Tasks.ProcessTasks();
        controlledEngine.GetValue("i").Should().Be(20);
        controlledSink.UncaughtCallbackErrors.Should().BeEmpty();
        controlledEngine.Tasks.TimeUntilNextScheduledWork.Should().BeNull();
    }

    [Test]
    public void TheCorpusHarnessUsesTheControlledClockOnlyForTheAdmittedFile()
    {
        var outcome = WptHarness.Run(WptTimerClock.File);
        outcome.HarnessError.Should().BeNull();
        outcome.Results.Should().ContainSingle().Which.Passed.Should().BeTrue();
        outcome.TimerClockEvidence.Should().Contain("pumps=15, advances=14, timerElapsed=00:00:00.0560000");

        foreach (var path in WptCorpus.Paths.Where(path => path != WptTimerClock.File))
        {
            WptTimerClock.ForFile(path, "", "", hasMetaScripts: false).Should().BeNull(path);
        }

        WptHarness.RunInline("test(() => assert_true(true), 'ordinary');")
            .TimerClockEvidence.Should().BeNull();
        WptHarness.IsServerBacked(WptTimerClock.File).Should().BeFalse();
        WptHarness.IsBlobUrlBacked(WptTimerClock.File).Should().BeFalse();
        WptHarness.RunsInAWorker(WptTimerClock.File).Should().BeFalse();
    }

    [TestCase("Date.now();")]
    [TestCase("performance.now();")]
    [TestCase("fetch('/resource');")]
    [TestCase("// META: script=helper.js")]
    [TestCase("new Worker('worker.js');")]
    public void ChangedCorpusCannotSilentlyAcquireControlledTime(string addition)
    {
        var exception = Caught.Exception(() => WptTimerClock.ForFile(WptTimerClock.File,
            WptCorpus.Read(WptTimerClock.File) + "\n" + addition, WptCorpus.Prelude, hasMetaScripts: false));
        exception.Should().BeOfType<InvalidOperationException>().Which.Message
            .Should().Contain("admission refused").And.Contain("source SHA256=");
    }

    [Test]
    public void ChangedShimOrMetaDependenciesRequireAnIsolationReview()
    {
        var source = WptCorpus.Read(WptTimerClock.File);
        Caught.Exception(() => WptTimerClock.ForFile(WptTimerClock.File, source,
            WptCorpus.Prelude + "\nDate.now();", hasMetaScripts: false))
            .Should().BeOfType<InvalidOperationException>();
        Caught.Exception(() => WptTimerClock.ForFile(WptTimerClock.File, source,
            WptCorpus.Prelude, hasMetaScripts: true)).Should().BeOfType<InvalidOperationException>();
        WptTimerClock.ForFile(WptTimerClock.File, source,
            WptCorpus.Prelude.Replace("\r\n", "\n").Replace("\n", "\r\n"), hasMetaScripts: false)
            .Should().NotBeNull("a Windows checkout can use CRLF for the locally authored shim");
    }

    [Test]
    public void ControlledTimeDoesNotSuppressAPreCompletionErrorOrReorderMicrotasks()
    {
        var clock = ControlledClock();
        var sink = new WptDiagnosticsSink();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            webApi.Diagnostics.Sink = sink;
        }));
        sink.Watch(engine);
        engine.Execute(WptCorpus.Prelude);
        engine.Execute("""
            setup({ single_test: true });
            var order = [];
            setTimeout(() => {
                order.push('timer');
                queueMicrotask(() => {
                    order.push('microtask');
                    queueMicrotask(() => order.push('nested microtask'));
                });
                throw new Error('before completion');
            }, 4);
            setTimeout(() => { order.push('done'); done(); }, 8);
            """);
        clock.Advance(engine.Tasks.TimeUntilNextScheduledWork!.Value);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("order.join(',')").Should().Be("timer,microtask,nested microtask");
        sink.UncaughtCallbackErrors.Should().ContainSingle().Which.Should().Contain("before completion");
        clock.Advance(engine.Tasks.TimeUntilNextScheduledWork!.Value);
        engine.Tasks.ProcessTasks();
        engine.GetValue("__wpt").AsObject().Get("fileTestComplete").Should().Be(true);
        sink.UncaughtCallbackErrors.Should().ContainSingle().Which.Should().Contain("before completion");
    }


    [Test]
    public void ControlledTimeStillDetectsAnIntervalDelayedPastTheWatchdog()
    {
        var clock = ControlledClock();
        var sink = new WptDiagnosticsSink();
        var engine = NegativeIntervalEngine(clock, sink, engine => engine.Execute("""
            // Simulate a broken delay conversion without changing any corpus bytes or bypassing TimerQueue.
            const nativeSetInterval = setInterval;
            setInterval = callback => nativeSetInterval(callback, 1001);
            """));
        clock.Advance(engine.Tasks.TimeUntilNextScheduledWork!.Value);
        engine.Tasks.ProcessTasks();
        engine.GetValue("i").Should().Be(0);
        engine.GetValue("__wpt").AsObject().Get("fileTestComplete").Should().Be(false);
        sink.UncaughtCallbackErrors.Should().Equal("Timer: reached unreachable code");
    }

    [Test]
    public void ControlledTimeDoesNotDisarmHostCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var clock = ControlledClock();
        var engine = new Engine(options =>
        {
            options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock);
            options.ObserveCancellation(cancellation.Token);
        });
        engine.Execute("setTimeout(() => { while (true) {} }, 4);");
        clock.Advance(engine.Tasks.TimeUntilNextScheduledWork!.Value);
        cancellation.Cancel();
        Caught.Exception(() => engine.Tasks.ProcessTasks()).Should().BeOfType<ExecutionCanceledException>();
    }


    [Test]
    public void ControlledTimerTimeCannotExtendTheRealHarnessDeadline()
    {
        var hostClock = new ManualClock();
        var clock = ControlledClock(hostClock);
        var deadline = TimeSpan.FromMinutes(5);
        clock.DeadlineError(deadline).Should().BeNull();
        hostClock.Advance((int) deadline.TotalMilliseconds);
        clock.GetTimestamp().Should().Be(0, "host time cannot move the timer clock");
        clock.DeadlineError(deadline).Should().Contain("did not complete within 00:05:00");
    }

}
#endif

using System.Globalization;
using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

/// <summary>
/// The timer globals — <see cref="WebApiFeatures.Timers"/>,
/// https://html.spec.whatwg.org/multipage/timers-and-user-prompts.html#timers.
///
/// <para><b>Every row drives a <see cref="ManualClock"/>, and nothing here sleeps or waits.</b> That is
/// not a convenience: on <see cref="TimeProvider.System"/> each <c>setTimeout</c> would pay a real
/// <c>QueryPerformanceCounter</c> read, and a row whose timers fire would be measuring how long the
/// machine took to reach a wall-clock instant. A clock that moves only when this class moves it makes
/// the rows deterministic, instant, and about the queue rather than about the host.</para>
///
/// <para>Three rows over the three things the queue does.
/// <see cref="ScheduleAndCancel"/> is pure registration churn — 500 <c>setTimeout</c>/<c>clearTimeout</c>
/// pairs, so it measures the id map, the min-heap insert and the lazy discard of a cancelled entry, and
/// no callback ever runs. <see cref="FanOutFiring"/> is the other half: 500 zero-delay timers that are all
/// due immediately, so the script's own drain promotes and dispatches every one of them.
/// <see cref="IntervalFiring"/> is the re-arm path, which no <c>setTimeout</c> reaches — one
/// <c>setInterval</c> fired 200 times, each firing re-arming the entry before its callback runs.</para>
///
/// <para><b>Engine isolation.</b> Every row gets its own engine and its own clock, carrying
/// <see cref="WebApiFeatures.Timers"/> alone and warmed with that row's own workload and nothing else.
/// Engine construction stays in <c>[GlobalSetup]</c> for all three. <see cref="IntervalFiring"/> is the one
/// row whose body is not a single <c>Evaluate</c> — the host has to move the clock and pump between
/// firings, exactly as an embedder does — so its measured operation includes 200 <c>ProcessTasks()</c>
/// calls and 200 clock advances; the advance is one addition to a <see cref="long"/> and the pump is what
/// the row is about.</para>
/// </summary>
[MemoryDiagnoser]
[HideColumns("Error", "Gen0", "Gen1", "Gen2")]
[BenchmarkCategory(WebApiBenchmarkSupport.Category)]
public class WebApiTimerBenchmark
{
    /// <summary>How many times <see cref="IntervalFiring"/> lets its interval fire.</summary>
    private const int IntervalFirings = 200;

    /// <summary>
    /// Milliseconds the clock moves per pump in <see cref="IntervalFiring"/>. Four rather than one because
    /// HTML clamps a timer nested more than five deep to a 4ms minimum, and an interval re-arming itself
    /// reaches that depth after six firings — so a 1ms step would fire on one pump in four from there on,
    /// and the row's cost would depend on where in the ramp it happened to be.
    /// </summary>
    private const int IntervalStepMilliseconds = 4;

    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock moves only when this class moves it. Ticks are the unit, so
    /// the due-time arithmetic in <c>TimerQueue.Arm</c> is exact.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    private IsolatedScript _scheduleAndCancel;
    private IsolatedScript _fanOutFiring;

    private Engine _intervalEngine = null!;
    private ManualClock _intervalClock = null!;
    private Prepared<Script> _intervalStart;
    private Prepared<Script> _intervalStop;

    [GlobalSetup]
    public void Setup()
    {
        // Every entry is cancelled, and TimerQueue discards a cancelled entry the moment it surfaces at
        // the head of the schedule — so the drain at the end of the script empties the whole batch and the
        // operation leaves the queue as it found it. That matters more than it looks: a row that let
        // entries pile up would grow the heap for the length of the run and report the growth as its own
        // cost, and the reference check would see the drift.
        _scheduleAndCancel = WebApiBenchmarkSupport.DeterministicRow(
            TimerEngine,
            """
            function noop() { }
            function scheduleAndCancel() {
                var n = 0;
                for (var i = 0; i < 500; i++) {
                    var id = setTimeout(noop, i % 8);
                    clearTimeout(id);
                    n++;
                }
                return n;
            }
            """,
            "scheduleAndCancel()");

        // The completion value is read before the event loop runs, so it is always zero — the count the row
        // is about lands in `fired` during the drain, which is what [GlobalSetup] checks below.
        _fanOutFiring = IsolatedScript.Warm(
            """
            fired = 0;
            for (var i = 0; i < 500; i++) { setTimeout(bump, 0); }
            fired;
            """,
            WebApiBenchmarkSupport.WithFixture(
                TimerEngine,
                """
                var fired = 0;
                function bump() { fired++; }
                """));

        _fanOutFiring.Run();
        WebApiBenchmarkSupport.Expect(_fanOutFiring.Engine, "fired", "500");

        _intervalClock = new ManualClock();
        _intervalEngine = TimerEngine(_intervalClock);
        _intervalEngine.Execute(
            """
            var ticks = 0;
            var intervalId = 0;
            function startInterval() { ticks = 0; intervalId = setInterval(function () { ticks++; }, 1); }
            function stopInterval() { clearInterval(intervalId); }
            """);
        _intervalStart = Engine.PrepareScript("startInterval();");
        _intervalStop = Engine.PrepareScript("stopInterval();");

        // Warmed with this row's own workload and nothing else: one full operation, then the check.
        IntervalFiring();
        WebApiBenchmarkSupport.Expect(_intervalEngine, "ticks", IntervalFirings.ToString(CultureInfo.InvariantCulture));
    }

    private static Engine TimerEngine() => TimerEngine(new ManualClock());

    private static Engine TimerEngine(TimeProvider clock) => new(options => options.UseWebApis(webApi =>
    {
        webApi.Features = WebApiFeatures.Timers;
        webApi.Timers.TimeProvider = clock;
    }));

    /// <summary>500 <c>setTimeout</c>/<c>clearTimeout</c> pairs, no callback ever run.</summary>
    [Benchmark]
    public JsValue ScheduleAndCancel() => _scheduleAndCancel.Run();

    /// <summary>500 zero-delay timers, all promoted and dispatched by the script's own drain.</summary>
    [Benchmark]
    public JsValue FanOutFiring() => _fanOutFiring.Run();

    /// <summary>One <c>setInterval</c> fired 200 times, the host moving the clock and pumping between firings.</summary>
    [Benchmark]
    public void IntervalFiring()
    {
        _intervalEngine.Execute(_intervalStart);

        for (var i = 0; i < IntervalFirings; i++)
        {
            _intervalClock.Advance(IntervalStepMilliseconds);
            _intervalEngine.Advanced.ProcessTasks();
        }

        _intervalEngine.Execute(_intervalStop);
    }
}

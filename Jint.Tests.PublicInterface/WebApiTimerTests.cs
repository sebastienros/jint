#if NET8_0_OR_GREATER
using System.Diagnostics;
using Jint;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The timer globals seen from outside the assembly: what a host has to write to get them, what it gets when
/// it writes nothing, and the promises it can build on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so nothing here can reach the timer queue directly — which
/// is the point. Everything is asserted through script and through the public options surface, exactly as an
/// embedder would have to.
/// </remarks>
public class WebApiTimerTests
{
    /// <summary>
    /// A host-supplied clock, to show that a suite exercising timers need not sleep. Only
    /// <see cref="TimeProvider.GetTimestamp"/> is ever asked for, so this is all it takes.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    [Test]
    public void ADefaultEngineHasNoTimers()
    {
        var engine = new Engine();

        foreach (var name in new[] { "setTimeout", "setInterval", "clearTimeout", "clearInterval", "queueMicrotask" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse();
        }
    }

    [Test]
    public void UseWebApisInstallsTheTimers()
    {
        var engine = new Engine(options => options.UseWebApis());

        foreach (var name in new[] { "setTimeout", "setInterval", "clearTimeout", "clearInterval", "queueMicrotask" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("function");
        }

        // The default set is what UseWebApis() means, and it now names timers.
        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Timers);
    }

    [Test]
    public void AskingForConsoleAloneDoesNotBringTimers()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof setTimeout").AsString().Should().Be("undefined");
    }

    [Test]
    public void AHostRegisteredTimerGlobalWins()
    {
        var marker = new JsString("the host's own setTimeout");

        var engine = new Engine(options => options
            .AddLazyGlobal("setTimeout", _ => marker)
            .UseWebApis());

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("setTimeout").Should().BeSameAs(marker);

        // The names it did not claim are still installed.
        engine.Evaluate("typeof setInterval").AsString().Should().Be("function");
    }

    [Test]
    public void AHostSuppliedClockDrivesTheTimers()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        engine.Execute("var log = []; setTimeout(() => log.push('fired'), 25);");

        // No thread exists to notice time passing: the host's own pump is what runs a timer.
        clock.Advance(10);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("log.length").AsNumber().Should().Be(0);

        clock.Advance(15);
        engine.Tasks.ProcessTasks();
        engine.Evaluate("log.join(',')").AsString().Should().Be("fired");
    }

    [Test]
    public void MaxActiveTimersIsEnforcedAsAQuotaExceededError()
    {
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.MaxActiveTimers = 1));

        engine.Execute("setTimeout(function () {}, 60000);");

        // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface rather than the bare name, so a
        // host's script can read the cap it hit off `quota` instead of parsing the message.
        engine.Evaluate("""
            (() => {
                try { setTimeout(function () {}, 60000); }
                catch (e) { return [e.name, e.constructor === QuotaExceededError, e.quota, e.requested].join('|'); }
                return 'no error';
            })()
            """).AsString().Should().Be("QuotaExceededError|true|1|2");
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void ACapOfZeroOrLessReportsAQuotaOfZeroRatherThanANegativeOne(int maxActiveTimers)
    {
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.MaxActiveTimers = maxActiveTimers));

        // "A value of zero or less refuses every timer" — and the number of timers a −5 cap permits is none,
        // not −5. https://webidl.spec.whatwg.org/#quotaexceedederror refuses a negative `quota` outright, so
        // reporting the raw setting would put a number on the error that its own constructor would reject.
        engine.Evaluate("""
            (() => {
                try { setTimeout(function () {}, 60000); }
                catch (e) { return [e.name, e.quota, e.requested].join('|'); }
                return 'no error';
            })()
            """).AsString().Should().Be("QuotaExceededError|0|1");
    }

    [Test]
    public void OneOptionsInstanceServesSeveralEnginesWithIndependentTimerQueues()
    {
        var options = new Options().UseWebApis(webApi => webApi.Timers.MaxActiveTimers = 1);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("setTimeout(function () {}, 60000);");

        // The second engine's single slot is its own: a queue shared through the options would refuse this.
        second.Execute("setTimeout(function () {}, 60000);");

        first.Evaluate("""
            (() => { try { setTimeout(function () {}, 60000); } catch (e) { return e.name; } return 'no error'; })()
            """).AsString().Should().Be("QuotaExceededError");
    }

    [Test]
    public void ABlockingUnwrapRunsTheTimersItIsWaitingFor()
    {
        var engine = new Engine(options => options.UseWebApis());

        var stopwatch = Stopwatch.StartNew();
        var result = engine
            .Evaluate("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()")
            .UnwrapIfPromise();

        result.AsNumber().Should().Be(42);
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(80));
    }

    [Test]
    public async Task AnAwaitedEvaluationRunsTheTimersItIsWaitingFor()
    {
        var engine = new Engine(options => options.UseWebApis());

        var result = await engine.EvaluateAsync("(async () => { await new Promise(r => setTimeout(r, 100)); return 42; })()");

        result.AsNumber().Should().Be(42);
    }

    [Test]
    public void AHostPumpIsEnoughToRunTimers()
    {
        var engine = new Engine(options => options.UseWebApis());
        var done = false;
        engine.SetValue("markDone", new Action(() => done = true));

        engine.Execute("setTimeout(markDone, 50);");

        var deadline = Stopwatch.StartNew();
        while (!done && deadline.Elapsed < TimeSpan.FromSeconds(5))
        {
            engine.Tasks.ProcessTasks();
            Thread.Sleep(5);
        }

        done.Should().BeTrue();
    }

    /// <summary>
    /// A blocking unwrap runs the timers it finds due, so a timer that is <em>not</em> due leaves the
    /// unwrap's own bound as the only thing that can end the wait — which is what makes the rejection the
    /// design and not a lost race.
    /// </summary>
    /// <remarks>
    /// The clock is the test's own and is never advanced, so "outlives the timeout" is a fact about the
    /// timer rather than a bet on how long this thread takes to reach the next statement. It used to be a
    /// bet: a five-second timer registered inside <see cref="Engine.Evaluate(string, string?)"/> against
    /// the system clock is already due if the machine spends five seconds between that call returning and
    /// the unwrap starting, and the first thing the unwrap does is run the continuations it finds — so the
    /// promise <em>fulfils</em> and nothing is thrown. That is not a hypothetical: sleeping 5.1 s between
    /// those two statements reproduces the CI failure exactly, "Expected: &lt;PromiseRejectedException&gt;
    /// But was: null", and a runner whose test bodies all run at <see cref="ThreadPriority.Lowest"/> under
    /// three concurrent legs has spent far longer than that on far less (sebastienros/jint#3452, and the
    /// 200 ms budget measured at 47 s in sebastienros/jint#3406).
    /// </remarks>
    [Test]
    public void ATimerOutlivingTheUnwrapTimeoutTimesOutByDesign()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        var pending = engine.Evaluate("new Promise(r => setTimeout(r, 5000))");

        Assert.Throws<PromiseRejectedException>(() => pending.UnwrapIfPromise(TimeSpan.FromMilliseconds(200)));
    }

    [Test]
    public void ARestoredEngineNeverRunsTheTimersOfTheCycleThatEnded()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        var ran = false;
        engine.SetValue("mark", new Action(() => ran = true));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("setTimeout(mark, 10);");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        clock.Advance(1000);
        engine.Tasks.ProcessTasks();

        ran.Should().BeFalse();
    }
}
#endif

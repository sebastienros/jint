#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The <c>performance</c> object seen from outside the assembly: what a host has to write to get it, what it
/// gets when it writes nothing, and the promises it can build on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so nothing here can reach the engine's time origin
/// directly. What a host <i>can</i> do is supply the clock, which is what most of these do — the same
/// <see cref="TimeProvider"/> the timers are scheduled against, so one fake drives both.
/// </remarks>
public class WebApiPerformanceTests
{
    /// <summary>
    /// A host-supplied clock. <see cref="TimeProvider.GetTimestamp"/> answers <c>now()</c> and
    /// <see cref="TimeProvider.GetUtcNow"/> is read exactly once, for the time origin.
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
    public void ADefaultEngineHasNoPerformance()
    {
        var engine = new Engine();

        engine.Evaluate("typeof performance").AsString().Should().Be("undefined");
        engine.Evaluate("'performance' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void UseWebApisInstallsPerformance()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof performance").AsString().Should().Be("object");
        engine.Evaluate("typeof performance.now").AsString().Should().Be("function");
        engine.Evaluate("typeof performance.timeOrigin").AsString().Should().Be("number");

        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Performance);
    }

    [Test]
    public void AskingForConsoleAloneDoesNotBringPerformance()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof performance").AsString().Should().Be("undefined");
    }

    [Test]
    public void MeasuresElapsedTimeAgainstTheHostsClock()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.Performance;
            webApi.Timers.TimeProvider = clock;
        }));

        engine.Evaluate("performance.now()").AsNumber().Should().Be(0);

        clock.Advance(1500);

        engine.Evaluate("performance.now()").AsNumber().Should().Be(1500);
        engine.Evaluate("performance.timeOrigin + performance.now()").AsNumber().Should().Be(1500);
    }

    [Test]
    public void OneHostClockDrivesTheTimersAndTheReadingsTogether()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        engine.Execute("var firedAt = -1; setTimeout(() => { firedAt = performance.now(); }, 200);");
        engine.Evaluate("firedAt").AsNumber().Should().Be(-1);

        clock.Advance(200);
        engine.Tasks.ProcessTasks();

        // The whole point of the two features sharing Options.WebApi.Timers.TimeProvider: a host that fakes
        // the clock gets a deterministic answer from both, not a timer that fires against a reading that has
        // not moved.
        engine.Evaluate("firedAt").AsNumber().Should().Be(200);
    }

    [Test]
    public void WorksOnAnEngineThatEnabledNoTimers()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        engine.Evaluate("typeof setTimeout").AsString().Should().Be("undefined");
        engine.Evaluate("typeof performance.now()").AsString().Should().Be("number");
    }

    [Test]
    public void TimeOriginNamesTheMomentTheEngineWasBuilt()
    {
        var before = (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).TotalMilliseconds;
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));
        var after = (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).TotalMilliseconds;

        var origin = engine.Evaluate("performance.timeOrigin").AsNumber();

        // Unix epoch milliseconds, so a host can turn a reading into a wall-clock instant:
        // DateTimeOffset.FromUnixTimeMilliseconds((long) (timeOrigin + now)).
        origin.Should().BeGreaterThanOrEqualTo(before).And.BeLessThanOrEqualTo(after);
    }

    [Test]
    public void ReadingsAreMonotonicOnTheDefaultClock()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        var first = engine.Evaluate("performance.now()").AsNumber();
        var second = engine.Evaluate("performance.now()").AsNumber();

        second.Should().BeGreaterThanOrEqualTo(first);
        first.Should().BeGreaterThanOrEqualTo(0);
    }

    [Test]
    public void AHostRegisteredPerformanceGlobalWins()
    {
        var marker = new JsString("the host's own performance");

        var engine = new Engine(options => options
            .AddLazyGlobal("performance", _ => marker)
            .UseWebApis());

        engine.Evaluate("performance").Should().BeSameAs(marker);
    }

    [Test]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'performance')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof performance')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof performance").AsString().Should().Be("object");
    }

    [Test]
    public void MarksAndMeasuresRunOnTheHostsClockToo()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.Performance;
            webApi.Timers.TimeProvider = clock;
        }));

        engine.Execute("performance.mark('start');");
        clock.Advance(250);
        engine.Execute("var m = performance.measure('work', 'start');");

        // Everything a mark or a measure is stamped with comes from the same reading performance.now() gives,
        // so a host that fakes the clock gets an exact duration instead of a tolerance.
        engine.Evaluate("m.startTime").AsNumber().Should().Be(0);
        engine.Evaluate("m.duration").AsNumber().Should().Be(250);
        engine.Evaluate("performance.getEntriesByType('measure')[0] === m").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void TheEntryInterfacesAreReachableFromScript()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        engine.Evaluate("performance.mark('a') instanceof PerformanceMark").AsBoolean().Should().BeTrue();
        engine.Evaluate("performance.measure('a', 'a') instanceof PerformanceEntry").AsBoolean().Should().BeTrue();

        // And the two the observer half adds, which is what a callback's first argument is checked against.
        engine.Evaluate("typeof PerformanceObserver").AsString().Should().Be("function");
        engine.Evaluate("typeof PerformanceObserverEntryList").AsString().Should().Be("function");
    }

    [Test]
    public void TheTimelineIsBoundedSoALoopCannotGrowItForever()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        // A browser's mark buffer is unbounded because the page it belongs to eventually goes away; an
        // embedded engine has no such event, so the buffer is capped and the overflow is dropped rather than
        // thrown. mark() still answers with the entry it built.
        engine.Execute("for (let i = 0; i < 10050; i++) { performance.mark('m'); }");

        engine.Evaluate("performance.getEntries().length").AsNumber().Should().Be(10000);
        engine.Evaluate("performance.mark('over') instanceof PerformanceMark").AsBoolean().Should().BeTrue();
    }

    [Test]
    public void OneOptionsInstanceGivesEachEngineItsOwnTimeOrigin()
    {
        var clock = new ManualClock();
        var options = new Options().UseWebApis(webApi =>
        {
            webApi.Features = WebApiFeatures.Performance;
            webApi.Timers.TimeProvider = clock;
        });

        var first = new Engine(options);
        clock.Advance(3000);
        var second = new Engine(options);

        // The origin belongs to the engine, never to the shared Options: the engine built three seconds later
        // reads zero where the first reads three thousand.
        first.Evaluate("performance.now()").AsNumber().Should().Be(3000);
        second.Evaluate("performance.now()").AsNumber().Should().Be(0);
        second.Evaluate("performance.timeOrigin").AsNumber().Should().Be(3000);
    }
}
#endif

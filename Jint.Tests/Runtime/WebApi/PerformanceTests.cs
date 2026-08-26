#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// The <c>performance</c> object against High Resolution Time — https://w3c.github.io/hr-time/.
/// </summary>
/// <remarks>
/// Almost everything here runs on a <see cref="ManualClock"/>, because what the two members are is arithmetic
/// over a clock and not a measurement of anything: with the clock in the test's hand, "now() counts from the
/// time origin" is an equality rather than a tolerance. The one test that uses the real clock is the one
/// about monotonicity, where the point is precisely that the readings come from a clock nobody controls.
/// </remarks>
public class PerformanceTests
{
    /// <summary>
    /// A <see cref="TimeProvider"/> whose clock only moves when a test moves it. Ticks are the unit, so both
    /// <see cref="TimeProvider.GetElapsedTime(long, long)"/> and the wall-clock reading behind
    /// <c>timeOrigin</c> are exact.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;

        internal void AdvanceTicks(long ticks) => _timestamp += ticks;
    }

    private static (Engine Engine, ManualClock Clock) PerformanceEngine(
        WebApiFeatures features = WebApiFeatures.Performance,
        ManualClock? clock = null)
    {
        clock ??= new ManualClock();
        var provider = clock;
        var engine = new Engine(options => options.UseWebApis(webApi =>
        {
            webApi.Features = features;
            webApi.Timers.TimeProvider = provider;
        }));

        return (engine, clock);
    }

    [Test]
    public void NowCountsMillisecondsFromTheTimeOrigin()
    {
        var (engine, clock) = PerformanceEngine();

        // The origin is the moment the engine was built, so nothing has elapsed yet.
        engine.Evaluate("performance.now()").AsNumber().Should().Be(0);

        clock.Advance(250);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(250);

        clock.Advance(750);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(1000);
    }

    [Test]
    public void NowHasSubMillisecondResolution()
    {
        var (engine, clock) = PerformanceEngine();

        // A DOMHighResTimeStamp is a double for a reason, and the readings are deliberately not coarsened:
        // a quarter of a millisecond reads as 0.25, not as 0 or 1. (Both figures here are exact in binary,
        // so the assertion is an equality rather than a tolerance.)
        clock.AdvanceTicks(TimeSpan.TicksPerMillisecond / 4);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(0.25);

        clock.AdvanceTicks(TimeSpan.TicksPerMillisecond / 4);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(0.5);
    }

    [Test]
    public void TimeOriginIsTheWallClockMomentTheEngineWasBuilt()
    {
        var clock = new ManualClock();
        clock.Advance(5000);

        var (engine, _) = PerformanceEngine(clock: clock);

        // https://w3c.github.io/hr-time/#dom-performance-timeorigin — "the duration from the estimated
        // monotonic time of the Unix epoch to timeOrigin", in milliseconds.
        engine.Evaluate("performance.timeOrigin").AsNumber().Should().Be(5000);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(0);
    }

    [Test]
    public void TimeOriginPlusNowIsTheCurrentWallClockTime()
    {
        var (engine, clock) = PerformanceEngine();

        clock.Advance(1234);

        // The property that makes the pair useful, and the reason both halves are read from one clock.
        engine.Evaluate("performance.timeOrigin + performance.now()").AsNumber().Should().Be(1234);
    }

    [Test]
    public void TimeOriginDoesNotMove()
    {
        var (engine, clock) = PerformanceEngine();

        var origin = engine.Evaluate("performance.timeOrigin").AsNumber();
        clock.Advance(10_000);

        engine.Evaluate("performance.timeOrigin").AsNumber().Should().Be(origin);
    }

    [Test]
    public void NeverGoesBackwardsOnTheRealClock()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        // The one hard requirement the specification states: "The difference between any two chronologically
        // recorded time values returned from the now() method MUST never be negative".
        engine.Evaluate("""
            (() => {
                let previous = performance.now();
                for (let i = 0; i < 10000; i++) {
                    const current = performance.now();
                    if (current < previous) return 'went backwards at ' + i;
                    previous = current;
                }
                return 'monotonic';
            })()
            """).AsString().Should().Be("monotonic");
    }

    [Test]
    public void SharesItsClockWithTheTimers()
    {
        var (engine, clock) = PerformanceEngine(WebApiFeatures.Performance | WebApiFeatures.Timers);

        engine.Execute("var firedAt = null; setTimeout(() => { firedAt = performance.now(); }, 100);");
        engine.Evaluate("firedAt").IsNull().Should().BeTrue();

        clock.Advance(100);
        engine.Tasks.ProcessTasks();

        // One clock, so the timer's due time and the reading its callback takes agree exactly. Two clocks
        // would make this a tolerance at best, and on a fake provider it would not fire at all.
        engine.Evaluate("firedAt").AsNumber().Should().Be(100);
    }

    [Test]
    public void WorksWithoutTheTimersFeature()
    {
        var (engine, clock) = PerformanceEngine();

        // The engine's web-API state exists for the time origin alone here; the timer queue inside it is
        // null, and nothing that reads the origin may assume otherwise.
        engine.Evaluate("typeof setTimeout").AsString().Should().Be("undefined");

        clock.Advance(42);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(42);
    }

    [Test]
    public void TheTimersFeatureAloneDoesNotBringPerformance()
    {
        var (engine, _) = PerformanceEngine(WebApiFeatures.Timers);

        engine.Evaluate("typeof performance").AsString().Should().Be("undefined");
        engine.Evaluate("typeof setTimeout").AsString().Should().Be("function");
    }

    [Test]
    public void KeepsItsTimeOriginAcrossAGlobalSnapshotRestore()
    {
        var (engine, clock) = PerformanceEngine();

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        clock.Advance(1000);
        var before = engine.Evaluate("performance.now()").AsNumber();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        // The origin belongs to the engine, not to the evaluation cycle: a pooled engine keeps it, so a
        // reading taken after the restore can never be smaller than one taken before it. Rewinding the origin
        // is exactly what would make now() go backwards, which the specification forbids.
        engine.Evaluate("performance.timeOrigin").AsNumber().Should().Be(0);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(before);

        clock.Advance(500);
        engine.Evaluate("performance.now()").AsNumber().Should().Be(1500);
    }

    [Test]
    public void BrandChecksBothMembers()
    {
        var (engine, _) = PerformanceEngine();

        Assert.Throws<JavaScriptException>(() => engine.Evaluate("performance.now.call({})"))!
            .Message.Should().Contain("Performance");
        Assert.Throws<JavaScriptException>(
            () => engine.Evaluate("Object.getOwnPropertyDescriptor(Performance.prototype, 'timeOrigin').get.call({})"))!
            .Message.Should().Contain("Performance");

        engine.Evaluate("(() => { const f = performance.now; return typeof f.call(performance); })()")
            .AsString().Should().Be("number");
    }

    [Test]
    public void IsOneStableObjectWithTheInterfacesToStringTag()
    {
        var (engine, _) = PerformanceEngine();

        engine.Evaluate("performance === performance").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(performance)").AsString().Should().Be("[object Performance]");
        engine.Evaluate("performance[Symbol.toStringTag]").AsString().Should().Be("Performance");
    }

    [Test]
    public void ExposesTimeOriginAsAReadOnlyAccessor()
    {
        var (engine, _) = PerformanceEngine();

        // A WebIDL readonly attribute is an accessor with no setter — never a data property, so that a host
        // cannot be fooled by a script assigning to it. It lives on Performance.prototype, as a browser's
        // does, and carries an attribute's property attributes: enumerable and configurable,
        // https://webidl.spec.whatwg.org/#es-attributes. Node 24 reports the same triple.
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(Performance.prototype, 'timeOrigin')").AsObject();
        descriptor.Get("get").IsCallable().Should().BeTrue();
        descriptor.Get("set").IsUndefined().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();

        // And now() is a regular operation, https://webidl.spec.whatwg.org/#es-operations — enumerable too,
        // which is again what Node 24 reports.
        var now = engine.Evaluate("Object.getOwnPropertyDescriptor(Performance.prototype, 'now')").AsObject();
        now.Get("writable").AsBoolean().Should().BeTrue();
        now.Get("configurable").AsBoolean().Should().BeTrue();
        now.Get("enumerable").AsBoolean().Should().BeTrue();

        // The instance itself carries nothing of its own, which is the same answer a browser gives.
        engine.Evaluate("JSON.stringify(Object.getOwnPropertyNames(performance))").AsString().Should().Be("[]");
        engine.Evaluate("JSON.stringify(Object.keys(performance))").AsString().Should().Be("[]");
    }

    [Test]
    public void CarriesTheUserTimingSurface()
    {
        var (engine, _) = PerformanceEngine();

        foreach (var member in new[] { "mark", "measure", "getEntries", "getEntriesByType", "getEntriesByName", "clearMarks", "clearMeasures" })
        {
            engine.Evaluate($"typeof performance.{member}").AsString().Should().Be("function");
        }
    }

    [Test]
    public void HasNoObserverAndNoResourceTiming()
    {
        var (engine, _) = PerformanceEngine();

        // Absent rather than present-and-throwing, so a library that feature-detects an observer takes its
        // fallback path instead of crashing.
        foreach (var member in new[] { "toJSON", "setResourceTimingBufferSize", "getEntriesByObserver", "eventCounts", "addEventListener" })
        {
            engine.Evaluate($"typeof performance.{member}").AsString().Should().Be("undefined");
        }

        engine.Evaluate("typeof PerformanceObserver").AsString().Should().Be("undefined");
    }

    [Test]
    public void HasTheIdlArity()
    {
        var (engine, _) = PerformanceEngine();

        engine.Evaluate("performance.now.length").AsNumber().Should().Be(0);
        engine.Evaluate("performance.now.name").AsString().Should().Be("now");
    }

    [Test]
    public void IsNotInstalledWithoutItsFlag()
    {
        new Engine(options => options.UseWebApis(WebApiFeatures.Console))
            .Evaluate("typeof performance").AsString().Should().Be("undefined");

        // ... and does not reach into a shadow realm when it is.
        PerformanceEngine().Engine.Evaluate("new ShadowRealm().evaluate('typeof performance')")
            .AsString().Should().Be("undefined");
    }
}
#endif

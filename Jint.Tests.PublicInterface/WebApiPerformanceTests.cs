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

    [Fact]
    public void ADefaultEngineHasNoPerformance()
    {
        var engine = new Engine();

        engine.Evaluate("typeof performance").AsString().Should().Be("undefined");
        engine.Evaluate("'performance' in globalThis").AsBoolean().Should().BeFalse();
    }

    [Fact]
    public void UseWebApisInstallsPerformance()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof performance").AsString().Should().Be("object");
        engine.Evaluate("typeof performance.now").AsString().Should().Be("function");
        engine.Evaluate("typeof performance.timeOrigin").AsString().Should().Be("number");

        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Performance);
    }

    [Fact]
    public void AskingForConsoleAloneDoesNotBringPerformance()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof performance").AsString().Should().Be("undefined");
    }

    [Fact]
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

    [Fact]
    public void OneHostClockDrivesTheTimersAndTheReadingsTogether()
    {
        var clock = new ManualClock();
        var engine = new Engine(options => options.UseWebApis(webApi => webApi.Timers.TimeProvider = clock));

        engine.Execute("var firedAt = -1; setTimeout(() => { firedAt = performance.now(); }, 200);");
        engine.Evaluate("firedAt").AsNumber().Should().Be(-1);

        clock.Advance(200);
        engine.Advanced.ProcessTasks();

        // The whole point of the two features sharing Options.WebApi.Timers.TimeProvider: a host that fakes
        // the clock gets a deterministic answer from both, not a timer that fires against a reading that has
        // not moved.
        engine.Evaluate("firedAt").AsNumber().Should().Be(200);
    }

    [Fact]
    public void WorksOnAnEngineThatEnabledNoTimers()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        engine.Evaluate("typeof setTimeout").AsString().Should().Be("undefined");
        engine.Evaluate("typeof performance.now()").AsString().Should().Be("number");
    }

    [Fact]
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

    [Fact]
    public void ReadingsAreMonotonicOnTheDefaultClock()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        var first = engine.Evaluate("performance.now()").AsNumber();
        var second = engine.Evaluate("performance.now()").AsNumber();

        second.Should().BeGreaterThanOrEqualTo(first);
        first.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void AHostRegisteredPerformanceGlobalWins()
    {
        var marker = new JsString("the host's own performance");

        var engine = new Engine(options => options
            .AddLazyGlobal("performance", _ => marker)
            .UseWebApis());

        engine.Evaluate("performance").Should().BeSameAs(marker);
    }

    [Fact]
    public void IsAnEnumerableDataPropertyOfTheGlobal()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(globalThis, 'performance')").AsObject();
        descriptor.Get("writable").AsBoolean().Should().BeTrue();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DoesNotReachIntoAShadowRealm()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("new ShadowRealm().evaluate('typeof performance')").AsString().Should().Be("undefined");
        engine.Evaluate("typeof performance").AsString().Should().Be("object");
    }

    [Fact]
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

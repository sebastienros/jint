#if NET8_0_OR_GREATER
using Jint;
using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The DOM event and cancellation model seen from outside the assembly: what a host writes to get it, what an
/// engine that did not ask for it has, and the shape a script can rely on.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so every one of these runs against the surface a third party
/// actually has — which for this feature is the globals themselves, since none of the instance types is
/// public.
/// </remarks>
public class WebApiEventTests
{
    private static readonly string[] _globals = ["Event", "CustomEvent", "EventTarget", "AbortController", "AbortSignal"];

    [Fact]
    public void ADefaultEngineHasNoEventGlobals()
    {
        var engine = new Engine();

        foreach (var name in _globals)
        {
            engine.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
            engine.Evaluate($"'{name}' in globalThis").AsBoolean().Should().BeFalse();
        }
    }

    [Fact]
    public void TheEventsFlagInstallsThemAndNothingElseDoes()
    {
        var enabled = new Engine(options => options.UseWebApis(WebApiFeatures.Events));
        var console = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        foreach (var name in _globals)
        {
            enabled.Evaluate($"typeof {name}").AsString().Should().Be("function");
            console.Evaluate($"typeof {name}").AsString().Should().Be("undefined");
        }

        // They are part of the set a host gets for asking for "the web APIs".
        new Options().UseWebApis().WebApi.Features.Should().HaveFlag(WebApiFeatures.Events);
    }

    [Fact]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new JsString("host's own EventTarget");

        var engine = new Engine(options => options
            .AddLazyGlobal("EventTarget", _ => marker)
            .UseWebApis(WebApiFeatures.Events));

        // The host's configuration runs first and the install is non-clobbering.
        engine.Evaluate("EventTarget").Should().BeSameAs(marker);

        // The others are still installed.
        engine.Evaluate("typeof AbortController").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmGetsNothing()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        foreach (var name in _globals)
        {
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void GivesEveryMemberTheAttributesWebIdlAsksFor()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        // Interface objects on the global: writable, configurable, not enumerable.
        foreach (var name in _globals)
        {
            var descriptor = $"Object.getOwnPropertyDescriptor(globalThis, '{name}')";
            engine.Evaluate($"{descriptor}.writable").AsBoolean().Should().BeTrue();
            engine.Evaluate($"{descriptor}.enumerable").AsBoolean().Should().BeFalse();
            engine.Evaluate($"{descriptor}.configurable").AsBoolean().Should().BeTrue();
        }

        // Attributes are enumerable configurable accessors on the interface prototype object.
        var aborted = "Object.getOwnPropertyDescriptor(AbortSignal.prototype, 'aborted')";
        engine.Evaluate($"typeof {aborted}.get").AsString().Should().Be("function");
        engine.Evaluate($"{aborted}.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{aborted}.configurable").AsBoolean().Should().BeTrue();

        // Constants: not writable, enumerable, not configurable.
        var atTarget = "Object.getOwnPropertyDescriptor(Event.prototype, 'AT_TARGET')";
        engine.Evaluate($"{atTarget}.writable").AsBoolean().Should().BeFalse();
        engine.Evaluate($"{atTarget}.enumerable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{atTarget}.configurable").AsBoolean().Should().BeFalse();

        // Operations are non-enumerable, which is a documented simplification of WebIDL — the same one
        // console carries. Pinned so that changing it is a deliberate act.
        var dispatch = "Object.getOwnPropertyDescriptor(EventTarget.prototype, 'dispatchEvent')";
        engine.Evaluate($"{dispatch}.writable").AsBoolean().Should().BeTrue();
        engine.Evaluate($"{dispatch}.enumerable").AsBoolean().Should().BeFalse();
        engine.Evaluate($"{dispatch}.configurable").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void ScriptsGetTheInterfaceShapeTheyExpect()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        engine.Evaluate("Object.getPrototypeOf(CustomEvent) === Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(AbortSignal) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("new CustomEvent('x') instanceof Event").AsBoolean().Should().BeTrue();
        engine.Evaluate("new AbortController().signal instanceof EventTarget").AsBoolean().Should().BeTrue();
    }

    [Fact]
    public void DispatchesAnEventToAHostReadableResult()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        var detail = engine.Evaluate("""
            const bus = new EventTarget();
            let seen = null;
            bus.addEventListener('message', e => { seen = e.detail; }, { once: true });
            bus.dispatchEvent(new CustomEvent('message', { detail: 'payload' }));
            seen;
            """);

        detail.AsString().Should().Be("payload");
    }

    [Fact]
    public void AbortsAnOperationTheWayTheSpecificationRecommends()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        var reason = engine.Evaluate("""
            const controller = new AbortController();
            let captured = null;
            controller.signal.addEventListener('abort', () => { captured = controller.signal.reason; });
            controller.abort('host asked to stop');
            captured;
            """);

        reason.AsString().Should().Be("host asked to stop");
    }

    [Fact]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Events);

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var controller = new AbortController(); controller.abort();");
        second.Execute("var controller = new AbortController();");

        first.Evaluate("controller.signal.aborted").AsBoolean().Should().BeTrue();
        second.Evaluate("controller.signal.aborted").AsBoolean().Should().BeFalse();

        // Nothing is shared between the two, not even the interface objects.
        first.Evaluate("Event").Should().NotBeSameAs(second.Evaluate("Event"));
    }

    [Fact]
    public void ATimeoutSignalFiresOnlyWhileTheEngineIsPumped()
    {
        // A manual clock rather than a real 1ms timeout: Evaluate itself pumps the event loop, so with a
        // wall clock the first assertion races the machine — on a loaded CI runner more than a millisecond
        // passes before the read and the signal has legitimately aborted. With the clock held still the
        // "not yet" half is a fact rather than a race, and advancing it makes the "fires when pumped" half
        // deterministic too.
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Events);
            options.WebApi.Timers.TimeProvider = clock;
        });

        engine.Execute("var signal = AbortSignal.timeout(1);");
        engine.Evaluate("signal.aborted").AsBoolean().Should().BeFalse();

        clock.Advance(TimeSpan.FromMilliseconds(2));
        engine.Advanced.ProcessTasks();

        engine.Evaluate("signal.aborted").AsBoolean().Should().BeTrue();
        engine.Evaluate("signal.reason.name").AsString().Should().Be("TimeoutError");
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp = 1;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan by) => _timestamp += (long) (by.TotalSeconds * TimestampFrequency);
    }

    [Fact]
    public void SurvivesAGlobalSnapshotRestore()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("var bus = new EventTarget();");
        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof bus").AsString().Should().Be("undefined");
        engine.Evaluate("new AbortController().signal.aborted").AsBoolean().Should().BeFalse();
    }
}
#endif

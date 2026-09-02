#if NET8_0_OR_GREATER
#nullable enable

using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>Engine.WebApi.Enable</c> from the inside: the parts of a post-construction install the public
/// surface cannot see. Which descriptor is stored and that it is stored unmaterialized; that the install bumps
/// the own-property version every global-binding and member-read inline cache validates against; and — the
/// half the issue calls the real work — that an engine which already had web-API state has that state
/// <i>extended</i> rather than replaced.
/// </summary>
/// <remarks>
/// The behaviour a third party can observe is pinned in
/// <c>Jint.Tests.PublicInterface.WebApiLiveEnableTests</c>; everything here needs
/// <c>InternalsVisibleTo</c> and belongs on this side of it.
/// </remarks>
public class LiveEnableTests
{
    [Test]
    public void InstallsUnmaterializedLazyDescriptors()
    {
        var engine = new Engine();
        engine.WebApi.Enable(WebApiFeatures.Console);

        var descriptor = engine.Realm.GlobalObject.GetOwnProperty("console");

        descriptor.Should().BeOfType<LazyPropertyDescriptor<Engine>>();
        // CustomJsValue is what routes the read through the resolver; it is cleared the moment the value
        // materializes, which is also what admits the descriptor to the global-binding inline cache.
        (descriptor._flags & PropertyFlag.CustomJsValue).Should().NotBe(PropertyFlag.None);
        descriptor._value.Should().BeNull("enabling a feature must not build the object a script never reads");
    }

    /// <summary>
    /// The design risk of a post-construction install, pinned directly — the same one
    /// <see cref="LazyGlobalInstallationTests.InstallingBumpsTheOwnPropertyVersionOnEveryStoragePath"/> pins
    /// for <c>AddLazyGlobal</c>. At construction time no inline cache exists, so an install that skipped the
    /// bump would still be correct; on a live engine a warmed identifier site holds the previous descriptor by
    /// reference and revalidates it against <c>_propertiesVersion</c> alone.
    /// </summary>
    [Test]
    public void InstallingBumpsTheOwnPropertyVersion()
    {
        var engine = new Engine();
        var global = engine.Realm.GlobalObject;

        var before = global._propertiesVersion;
        engine.WebApi.Enable(WebApiFeatures.Console);
        global._propertiesVersion.Should().NotBe(before, "the first install has to invalidate the caches");

        // ... and again for a second, later call, which is the pooled-engine shape.
        before = global._propertiesVersion;
        engine.WebApi.Enable(WebApiFeatures.Base64);
        global._propertiesVersion.Should().NotBe(before);

        // A call that enables nothing installs nothing, so it must not churn the caches either.
        before = global._propertiesVersion;
        engine.WebApi.Enable(WebApiFeatures.Console).Should().Be(WebApiFeatures.None);
        global._propertiesVersion.Should().Be(before);
    }

    /// <summary>
    /// Installing global object properties creates no lexical binding and injects nothing into an existing
    /// environment, so the two counters that describe those must NOT move — bumping either would be
    /// invalidating caches for a change that did not happen.
    /// </summary>
    [Test]
    public void InstallingDoesNotDisturbTheLexicalCounters()
    {
        var engine = new Engine();
        var globalEnv = engine.Realm.GlobalEnv;

        var lexicalMutations = globalEnv._lexicalMutations;
        var injectionEpoch = engine._envBindingInjectionEpoch;

        engine.WebApi.Enable();

        globalEnv._lexicalMutations.Should().Be(lexicalMutations);
        engine._envBindingInjectionEpoch.Should().Be(injectionEpoch);
    }

    [Test]
    public void TheEngineRecordsTheExpandedClosure()
    {
        var engine = new Engine();
        engine.WebApi.Enable(WebApiFeatures.CacheApi);

        // The Cache API stores Request/Response pairs, so it brings the three features those are built out of
        // — and deliberately not the network.
        engine._webApiFeatures.Should().Be(
            WebApiFeatures.CacheApi | WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files);
        engine._webApiFeatures.Should().NotHaveFlag(WebApiFeatures.Fetch);

        // Options is untouched: it reads back exactly what the host asked for, at options time and here.
        engine.Options.WebApi.Features.Should().Be(WebApiFeatures.None);
    }

    /// <summary>
    /// The state-extension half. A state built for <see cref="WebApiFeatures.Encoding"/> plus
    /// <see cref="WebApiFeatures.Performance"/> carries the time origin and none of the queues a scheduling
    /// feature needs, so enabling one has to attach it — to that very state, because replacing it would move
    /// the time origin and lose everything the engine had already put in it.
    /// </summary>
    /// <remarks>
    /// The engine is built with <see cref="WebApiFeatures.Performance"/> and reaches its state through the
    /// <see cref="WebApiFeatures.Events"/> the closure brings with it —
    /// <c>interface Performance : EventTarget</c> — which is also why it starts with a timer queue: that is
    /// where <c>AbortSignal.timeout()</c> schedules. What it has none of is the scheduler and the idle
    /// callbacks, and those are what this asserts get attached rather than rebuilt.
    /// </remarks>
    [Test]
    public void ExtendingAnExistingStateAttachesTheQueuesWithoutReplacingIt()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        var state = engine._webApi;
        state.Should().NotBeNull();
        state!.Scheduler.Should().BeNull();
        state.IdleCallbacks.Should().BeNull();

        var timeOrigin = state.TimeOrigin;
        var clock = state.TimeProvider;

        engine.WebApi.Enable(WebApiFeatures.Timers | WebApiFeatures.Scheduler | WebApiFeatures.IdleCallback);

        engine._webApi.Should().BeSameAs(state, "the state must be extended, never rebuilt");
        state.Timers.Should().NotBeNull();
        state.Scheduler.Should().NotBeNull();
        state.IdleCallbacks.Should().NotBeNull();
        state.TimeOrigin.Should().Be(timeOrigin, "performance.now() must not be able to go backwards");
        state.TimeProvider.Should().BeSameAs(clock, "everything attached later has to share the engine's clock");
    }

    /// <summary>
    /// The clock a state was built on is the engine's forever: a <c>TimeProvider</c> named by the live door's
    /// own configuration callback must not reach an engine that already has a state, or the timers and
    /// <c>performance.now()</c> would be reading two different clocks.
    /// </summary>
    /// <remarks>
    /// The callback is the only way to say it at all now — an engine's <see cref="Options"/> are read-only
    /// once it has been built from them — and it is deliberately the strongest form of the question: even the
    /// sanctioned post-construction door cannot move a clock an engine is already running on.
    /// </remarks>
    [Test]
    public void ExtendingAnExistingStateDoesNotAdoptALaterClock()
    {
        var options = new Options().UseWebApis(WebApiFeatures.Performance);
        var engine = new Engine(options);
        var original = engine._webApi!.TimeProvider;

        engine.WebApi.Enable(
            WebApiFeatures.Timers,
            webApi => webApi.Timers.TimeProvider = new FakeTimeProvider());

        engine._webApi!.TimeProvider.Should().BeSameAs(original);
        engine._webApi.Timers.Should().NotBeNull();
    }

    /// <summary>
    /// An engine with no state at all is the other branch: it gets one built exactly as construction would
    /// have built it, reading the options group at the moment of the call.
    /// </summary>
    [Test]
    public void AnEngineWithNoStateGetsOneBuiltFromTheOptionsAtEnableTime()
    {
        var clock = new FakeTimeProvider();
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        engine._webApi.Should().BeNull("console keeps no engine state");

        engine.WebApi.Enable(WebApiFeatures.Timers, webApi =>
        {
            webApi.Timers.TimeProvider = clock;
            webApi.Timers.MaxActiveTimers = 3;
        });

        engine._webApi.Should().NotBeNull();
        engine._webApi!.TimeProvider.Should().BeSameAs(clock);
        engine._webApi.Timers!.MaxActiveTimers.Should().Be(3);
    }

    /// <summary>
    /// The storage providers are resolved lazily on first use, so enabling storage live has to fill the slots
    /// that are still empty and may never overwrite one an engine has already been reading and writing.
    /// </summary>
    [Test]
    public void EnablingStorageLiveDoesNotDisplaceAProviderAlreadyInUse()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Storage));
        engine.Execute("localStorage.setItem('k', 'v');");

        var provider = engine._webApi!.LocalStorageProvider;

        // Enabling storage again is a no-op, and so is enabling something else beside it.
        engine.WebApi.Enable(WebApiFeatures.Storage | WebApiFeatures.Base64);

        engine._webApi.LocalStorageProvider.Should().BeSameAs(provider);
        engine.Evaluate("localStorage.getItem('k')").AsString().Should().Be("v");
    }

    /// <summary>A clock that never moves; only its identity matters to these tests.</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => 0;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
#endif

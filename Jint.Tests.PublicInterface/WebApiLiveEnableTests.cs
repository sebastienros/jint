#if NET8_0_OR_GREATER
#nullable enable

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>engine.Advanced.EnableWebApis</c> seen from outside the assembly: turning a web API on for an engine
/// that already exists, which is what a host renting engines from a pool needs when it only learns per
/// request what the script it is about to run wants.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so nothing here can reach the feature record, the timer
/// queue or the property table directly — every assertion goes through script, through the public options
/// surface and through the return value, exactly as an embedder's would. Nothing sleeps: the tests that need
/// time to pass move a host-supplied <see cref="TimeProvider"/> and pump the engine themselves.
/// </remarks>
public class WebApiLiveEnableTests
{
    /// <summary>
    /// A host-supplied clock, so that a suite exercising timers need not sleep. Only
    /// <see cref="TimeProvider.GetTimestamp"/> and <see cref="TimeProvider.GetUtcNow"/> are ever asked for.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_timestamp);

        internal void Advance(int milliseconds) => _timestamp += milliseconds * TimeSpan.TicksPerMillisecond;
    }

    /// <summary>Answers every request from memory, so nothing in this file opens a socket.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (Urls)
            {
                Urls.Add(request.RequestUri!.ToString());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }

    [Fact]
    public void EnablingOnADefaultEngineInstallsTheGlobals()
    {
        var engine = new Engine();
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("typeof TextEncoder").AsString().Should().Be("undefined");

        var added = engine.Advanced.EnableWebApis(WebApiFeatures.Console | WebApiFeatures.Encoding);

        added.Should().Be(WebApiFeatures.Console | WebApiFeatures.Encoding);
        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("new TextEncoder().encode('ab').length").AsNumber().Should().Be(2);

        // DOMException has no flag of its own and arrives with the first web API, live exactly as at options
        // time.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    /// <summary>
    /// The whole promise of the API: an engine enabled live must be the engine options-time enabling would
    /// have produced. Compared over the whole default surface rather than a sample, including the WebIDL
    /// property attributes, which differ per global and are the part a second install path is most likely to
    /// get wrong.
    /// </summary>
    [Fact]
    public void ALiveEnabledEngineHasTheSameSurfaceAsAnOptionsConfiguredOne()
    {
        var configured = new Engine(options => options.UseWebApis());

        var live = new Engine();
        live.Advanced.EnableWebApis();

        const string Survey = """
            Object.getOwnPropertyNames(globalThis)
                .map(n => {
                    const d = Object.getOwnPropertyDescriptor(globalThis, n);
                    return n + ':' + typeof globalThis[n] + ':' + (d.writable ? 'w' : '') + (d.enumerable ? 'e' : '') + (d.configurable ? 'c' : '');
                })
                .sort()
                .join('|')
            """;

        live.Evaluate(Survey).AsString().Should().Be(configured.Evaluate(Survey).AsString());
    }

    [Fact]
    public void ALiveEnabledTimerFiresOnTheHostsClock()
    {
        var clock = new ManualClock();
        var engine = new Engine();

        engine.Advanced.EnableWebApis(WebApiFeatures.Timers, webApi => webApi.Timers.TimeProvider = clock);

        engine.Execute("var log = []; setTimeout(() => log.push('late'), 50);");
        engine.Evaluate("log.length").AsNumber().Should().Be(0);

        // Not yet due: the pump must not run it early.
        clock.Advance(20);
        engine.Advanced.ProcessTasks();
        engine.Evaluate("log.length").AsNumber().Should().Be(0);

        clock.Advance(40);
        engine.Advanced.ProcessTasks();
        engine.Evaluate("log.join(',')").AsString().Should().Be("late");
    }

    /// <summary>
    /// The closure is the options-time closure: fetch's own interfaces are part of its surface, so they arrive
    /// with it here exactly as they do at construction.
    /// </summary>
    [Fact]
    public void EnablingReportsTheWholeClosureItAdded()
    {
        var engine = new Engine();

        var added = engine.Advanced.EnableWebApis(WebApiFeatures.Fetch, webApi => webApi.Fetch.HttpClient = new HttpClient(new StubHandler()));

        added.Should().Be(
            WebApiFeatures.Fetch | WebApiFeatures.Events | WebApiFeatures.Url | WebApiFeatures.Files | WebApiFeatures.Streams);
        engine.Advanced.WebApiFeatures.Should().Be(added);

        foreach (var name in new[] { "fetch", "Request", "Response", "Headers", "AbortController", "URL", "Blob", "ReadableStream" })
        {
            engine.Evaluate($"typeof {name}").AsString().Should().NotBe("undefined");
        }
    }

    /// <summary>
    /// The counterpart of the closure test, and the security-relevant half: no non-network feature may pull a
    /// network feature in. Asserted over every feature the enum declares, one engine each, so a closure rule
    /// added later cannot quietly change the answer.
    /// </summary>
    [Fact]
    public void NoNonNetworkFeatureEverBringsNetworkAccess()
    {
        const WebApiFeatures Network = WebApiFeatures.Fetch | WebApiFeatures.EventSource | WebApiFeatures.WebSocket;

        foreach (WebApiFeatures feature in Enum.GetValues<WebApiFeatures>())
        {
            if (feature == WebApiFeatures.None || (feature & Network) != WebApiFeatures.None)
            {
                continue;
            }

            var engine = new Engine();
            engine.Advanced.EnableWebApis(feature);

            engine.Advanced.WebApiFeatures.Should().NotHaveFlag(WebApiFeatures.Fetch, $"{feature} must not grant fetch");
            engine.Advanced.WebApiFeatures.Should().NotHaveFlag(WebApiFeatures.EventSource, $"{feature} must not grant EventSource");
            engine.Advanced.WebApiFeatures.Should().NotHaveFlag(WebApiFeatures.WebSocket, $"{feature} must not grant WebSocket");

            engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
            engine.Evaluate("typeof EventSource").AsString().Should().Be("undefined");
            engine.Evaluate("typeof WebSocket").AsString().Should().Be("undefined");
        }
    }

    [Fact]
    public void EnablingWhatIsAlreadyOnIsANoOpRatherThanAnError()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));

        // The identity a script can see, captured before the second call.
        engine.Execute("var saved = console;");

        var configureRan = false;
        var added = engine.Advanced.EnableWebApis(WebApiFeatures.Console, _ => configureRan = true);

        added.Should().Be(WebApiFeatures.None);
        configureRan.Should().BeFalse("a call that enables nothing must not even run the configuration delegate");
        engine.Evaluate("console === saved").AsBoolean().Should().BeTrue("an already-installed global must not be re-installed");
    }

    [Fact]
    public void EnablingNothingIsANoOp()
    {
        var engine = new Engine();

        var configureRan = false;
        engine.Advanced.EnableWebApis(WebApiFeatures.None, _ => configureRan = true).Should().Be(WebApiFeatures.None);

        configureRan.Should().BeFalse();
        engine.Advanced.WebApiFeatures.Should().Be(WebApiFeatures.None);
        engine.Evaluate("typeof DOMException").AsString().Should().Be("undefined");
    }

    [Fact]
    public void EnablingUnionsWithWhatTheEngineWasBuiltWith()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Console));
        engine.Execute("var saved = console;");

        var added = engine.Advanced.EnableWebApis(WebApiFeatures.Console | WebApiFeatures.Base64);

        // Only the part that was missing.
        added.Should().Be(WebApiFeatures.Base64);
        engine.Advanced.WebApiFeatures.Should().Be(WebApiFeatures.Console | WebApiFeatures.Base64);

        engine.Evaluate("console === saved").AsBoolean().Should().BeTrue();
        engine.Evaluate("atob(btoa('hi'))").AsString().Should().Be("hi");
    }

    /// <summary>
    /// A global whose value the script has already forced into existence must come through untouched — its
    /// own expandos included, since re-installing would replace the object rather than the descriptor.
    /// </summary>
    [Fact]
    public void AnAlreadyMaterializedGlobalKeepsItsIdentity()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Encoding));

        engine.Execute("var encoder = new TextEncoder(); TextEncoder.marker = 'mine';");

        engine.Advanced.EnableWebApis(WebApiFeatures.Streams);

        engine.Evaluate("TextEncoder.marker").AsString().Should().Be("mine");
        engine.Evaluate("encoder instanceof TextEncoder").AsBoolean().Should().BeTrue();

        // And the pair that needs BOTH flags is completed by the second half arriving.
        engine.Evaluate("typeof TextEncoderStream").AsString().Should().Be("function");
        engine.Evaluate("typeof TextDecoderStream").AsString().Should().Be("function");
    }

    [Fact]
    public void AHostGlobalOfTheSameNameSurvives()
    {
        var engine = new Engine();
        engine.SetValue("console", new { marker = "host" });

        engine.Advanced.EnableWebApis(WebApiFeatures.Console);

        engine.Evaluate("console.marker").AsString().Should().Be("host");
        engine.Evaluate("typeof console.log").AsString().Should().Be("undefined");

        // Only the name the host owns is left alone; the rest of the feature still arrives.
        engine.Evaluate("typeof DOMException").AsString().Should().Be("function");
    }

    /// <summary>
    /// The non-clobbering check has to <i>probe</i> rather than read, or a host's own lazy global would be
    /// built by the mere act of enabling a feature that shares its name — which is exactly what a host chose
    /// laziness to avoid.
    /// </summary>
    [Fact]
    public void AHostLazyGlobalOfTheSameNameIsNotMaterializedByTheCheck()
    {
        var built = 0;
        var engine = new Engine();
        engine.Advanced.AddLazyGlobal("console", _ =>
        {
            built++;
            return JsValue.FromObject(engine, new { marker = "host" });
        });

        engine.Advanced.EnableWebApis(WebApiFeatures.Console);

        built.Should().Be(0, "the existence check must probe, not read");

        engine.Evaluate("console.marker").AsString().Should().Be("host");
        built.Should().Be(1);
    }

    /// <summary>
    /// The state-extension half, which is where the real work is: an engine whose web-API state exists but
    /// carries no timer queue — <see cref="WebApiFeatures.Performance"/> reads the time origin and never
    /// schedules — has to have one attached when a scheduling feature arrives.
    /// </summary>
    [Fact]
    public void EnablingTimersOnAnEngineWhoseStateHasNoQueueAttachesOne()
    {
        var clock = new ManualClock();
        var engine = new Engine(options =>
        {
            options.UseWebApis(WebApiFeatures.Performance);
            options.WebApi.Timers.TimeProvider = clock;
        });

        engine.Advanced.EnableWebApis(WebApiFeatures.Timers).Should().Be(WebApiFeatures.Timers);

        engine.Execute("var log = []; setTimeout(() => log.push('fired'), 10);");
        clock.Advance(20);
        engine.Advanced.ProcessTasks();

        engine.Evaluate("log.join(',')").AsString().Should().Be("fired");

        // The clock is the state's own, so performance and the timers stayed coherent.
        engine.Evaluate("performance.now() >= 20").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The two-piece attachment: <c>requestIdleCallback</c> needs an idle queue, which needs the timer queue,
    /// and neither exists on a state built for <see cref="WebApiFeatures.Performance"/> alone.
    /// </summary>
    [Fact]
    public void EnablingIdleCallbacksOnAnEngineWhoseStateHasNoQueuesAttachesBoth()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Performance));

        engine.Advanced.EnableWebApis(WebApiFeatures.IdleCallback).Should().Be(WebApiFeatures.IdleCallback);

        engine.Execute("var log = []; requestIdleCallback(d => log.push(typeof d.timeRemaining));");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("log.join(',')").AsString().Should().Be("function");
    }

    [Fact]
    public void EnablingTheSchedulerOnAnEngineThatAlreadyHadItsQueueAttachesTheTaskQueues()
    {
        var engine = new Engine(options => options.UseWebApis(WebApiFeatures.Events));

        engine.Advanced.EnableWebApis(WebApiFeatures.Scheduler).Should().Be(WebApiFeatures.Scheduler);

        engine.Execute("var log = []; scheduler.postTask(() => log.push('task'));");
        engine.Advanced.ProcessTasks();

        engine.Evaluate("log.join(',')").AsString().Should().Be("task");
    }

    /// <summary>
    /// The network features carry an options group, and the live door has to accept the same configuration the
    /// options-time extension does — including the policy, which is the setting a host must never be unable to
    /// supply.
    /// </summary>
    [Fact]
    public void TheConfigureDelegateSuppliesTheNetworkPolicyAtEnableTime()
    {
        var handler = new StubHandler();
        var engine = new Engine();

        engine.Advanced.EnableWebApis(WebApiFeatures.Fetch, webApi =>
        {
            webApi.Fetch.HttpClient = new HttpClient(handler);
            webApi.Fetch.UrlFilter = uri => uri.Host.EndsWith(".example.org", StringComparison.OrdinalIgnoreCase);
        });

        engine.Evaluate("fetch('https://api.example.org/x').then(r => r.status)").UnwrapIfPromise().AsNumber().Should().Be(200);

        engine.Evaluate("fetch('https://elsewhere.test/x').then(() => 'resolved', e => e.constructor.name)")
            .UnwrapIfPromise().AsString().Should().Be("TypeError");

        handler.Urls.Should().ContainSingle().Which.Should().Be("https://api.example.org/x");
    }

    [Fact]
    public void EnablingStorageLiveUsesTheProviderTheHostSupplies()
    {
        var provider = new InMemoryStorageProvider();
        var engine = new Engine();

        engine.Advanced.EnableWebApis(WebApiFeatures.Storage, webApi => webApi.Storage.LocalStorageProvider = provider);

        engine.Execute("localStorage.setItem('k', 'v');");

        provider.GetItem("k").Should().Be("v");
        engine.Evaluate("sessionStorage.getItem('k')").Should().Be(JsValue.Null, "the two globals are two stores");
    }

    /// <summary>
    /// A restore returns the global bindings to their state at capture, so globals a live enable installed
    /// afterwards are gone — the same thing that happens to an <c>AddLazyGlobal</c> global and to the ones
    /// <c>SetFetchHandler</c> installs. The engine-side record is deliberately not part of what a restore
    /// reverts, which is why the feature still reads as enabled and why re-enabling cannot bring the globals
    /// back.
    /// </summary>
    [Fact]
    public void ARestoreOfASnapshotTakenBeforeTheEnableRemovesTheGlobalsAgain()
    {
        var engine = new Engine();
        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Advanced.EnableWebApis(WebApiFeatures.Console).Should().Be(WebApiFeatures.Console);
        engine.Evaluate("typeof console").AsString().Should().Be("object");

        engine.Advanced.RestoreGlobalSnapshot(clean);

        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
        engine.Evaluate("'console' in globalThis").AsBoolean().Should().BeFalse();

        // The record survives, so the engine knows about an API whose globals script can no longer name — and
        // asking again is a no-op, not a reinstall.
        engine.Advanced.WebApiFeatures.Should().Be(WebApiFeatures.Console);
        engine.Advanced.EnableWebApis(WebApiFeatures.Console).Should().Be(WebApiFeatures.None);
        engine.Evaluate("typeof console").AsString().Should().Be("undefined");
    }

    /// <summary>The documented remedy: capture after the enable, and every restore carries the globals.</summary>
    [Fact]
    public void ASnapshotTakenAfterTheEnableCarriesTheGlobalsThroughEveryRestore()
    {
        var engine = new Engine();
        engine.Advanced.EnableWebApis(WebApiFeatures.Console | WebApiFeatures.Base64);

        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("globalThis.perRequest = 1; console.marker = 'dirty';");
        engine.Advanced.RestoreGlobalSnapshot(clean);

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("atob(btoa('x'))").AsString().Should().Be("x");
        engine.Evaluate("typeof perRequest").AsString().Should().Be("undefined");

        // ... and the expando on the console object itself does NOT go away, exactly as it does not for an
        // options-time engine: a restore reverts the global binding table, not the objects behind it, and
        // `console` is an intrinsic. That is a documented non-guarantee of the snapshot, not something the
        // live door changes.
        engine.Evaluate("console.marker").AsString().Should().Be("dirty");
    }

    /// <summary>
    /// A global left unread at capture time is still a lazy descriptor, so a restore returns it to that
    /// unmaterialized state rather than dropping it — the contract <c>AddLazyGlobal</c> already has. What a
    /// script <i>replaced</i> the binding with is reverted; what it wrote onto the intrinsic behind the
    /// binding is not, which is the snapshot's documented non-guarantee and is the same for an
    /// options-time engine.
    /// </summary>
    [Fact]
    public void AGlobalStillUnreadAtCaptureIsRestoredToItsBinding()
    {
        var engine = new Engine();
        engine.Advanced.EnableWebApis(WebApiFeatures.Console);

        // Captured without ever reading `console`, so the descriptor is still unmaterialized.
        var clean = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("globalThis.console = 'replaced';");
        engine.Evaluate("typeof console").AsString().Should().Be("string");

        engine.Advanced.RestoreGlobalSnapshot(clean);

        engine.Evaluate("typeof console").AsString().Should().Be("object");
        engine.Evaluate("typeof console.log").AsString().Should().Be("function");
    }

    [Fact]
    public void AShadowRealmStillGetsNothing()
    {
        var engine = new Engine();
        engine.Advanced.EnableWebApis();

        engine.Evaluate("typeof console").AsString().Should().Be("object");

        foreach (var name in new[] { "console", "DOMException", "setTimeout", "TextEncoder", "URL", "structuredClone" })
        {
            engine.Evaluate($"new ShadowRealm().evaluate('typeof {name}')").AsString().Should().Be("undefined");
        }
    }

    /// <summary>
    /// The invalidation story from the only angle a third party has. A prepared script is the public way to
    /// keep one handler tree across evaluations, and its per-site caches engage from the second run — so a
    /// site that has already answered <c>undefined</c> for a name twice must answer with the global once the
    /// feature installs it. (The install's own half of that, the own-property version bump every one of those
    /// caches revalidates against, is pinned inside the assembly by
    /// <c>Jint.Tests.Runtime.WebApi.LiveEnableTests.InstallingBumpsTheOwnPropertyVersion</c>: it is what keeps
    /// this true if a later change ever makes an install replace a name rather than only add one.)
    /// </summary>
    [Fact]
    public void AWarmedIdentifierSiteReResolvesAfterALiveEnable()
    {
        var engine = new Engine();
        var probe = Engine.PrepareScript("typeof console + '/' + typeof globalThis.console");

        // Twice, because the handler-tree caches engage from the second evaluation of a script on an engine.
        engine.Evaluate(probe).AsString().Should().Be("undefined/undefined");
        engine.Evaluate(probe).AsString().Should().Be("undefined/undefined");

        engine.Advanced.EnableWebApis(WebApiFeatures.Console);

        engine.Evaluate(probe).AsString().Should().Be("object/object");
    }

    /// <summary>
    /// The same pin for a member read of the global object rather than a bare identifier, on an engine that
    /// already had web APIs — the shape a pooled host that enables in two stages actually produces.
    /// </summary>
    [Fact]
    public void AWarmedMemberReadSeesAGlobalInstalledAfterIt()
    {
        var engine = new Engine();
        engine.Advanced.EnableWebApis(WebApiFeatures.Console);

        var probe = Engine.PrepareScript("typeof globalThis.btoa");
        engine.Evaluate(probe).AsString().Should().Be("undefined");
        engine.Evaluate(probe).AsString().Should().Be("undefined");

        engine.Advanced.EnableWebApis(WebApiFeatures.Base64);

        engine.Evaluate(probe).AsString().Should().Be("function");
    }

    /// <summary>
    /// The host APIs that refuse an engine which never opted in have to accept one that opted in late — they
    /// read the engine's own record, which the live door updates.
    /// </summary>
    [Fact]
    public void TheFeatureGatedHostApisAcceptALiveEnabledEngine()
    {
        var engine = new Engine();

        Assert.Throws<InvalidOperationException>(() => engine.Advanced.CreateAbortSignal(CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => engine.Advanced.CreateMessagePortPair(engine));

        engine.Advanced.EnableWebApis(WebApiFeatures.Events | WebApiFeatures.Messaging);

        engine.SetValue("hostSignal", engine.Advanced.CreateAbortSignal(CancellationToken.None));
        engine.Evaluate("hostSignal.aborted").AsBoolean().Should().BeFalse();

        var pair = engine.Advanced.CreateMessagePortPair(engine);
        pair.Local.Should().NotBeNull();
        pair.Remote.Should().NotBeNull();
    }
}
#endif

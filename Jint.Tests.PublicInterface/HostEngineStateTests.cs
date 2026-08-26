#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <see cref="Engine.HostDefined"/> —
/// the slot that lets host code handed nothing but an <see cref="Engine"/> get back to the per-request state
/// that engine was built for.
///
/// <para>
/// The case it exists for is the <see cref="Options"/>-time lazy global: a single <see cref="Options"/> instance
/// is shared by every engine built from it, so a factory recorded there cannot capture anything engine-affine,
/// and the only route back used to be a side table keyed by engine — embedders keep a
/// <c>static ConditionalWeakTable&lt;Engine, IServiceProvider&gt;</c> for exactly this and pay its write lock on
/// every evaluation. <see cref="ASharedOptionsLazyGlobalResolvesThroughTheEngineState"/> is that shape end to
/// end.
/// </para>
///
/// <para>
/// The storage is the <c>[[HostDefined]]</c> field of the engine's <em>principal</em> Realm Record — the slot
/// the specification reserves for hosts associating information with a realm, and where HTML puts a realm's
/// settings object. Principal rather than current is the distinction that matters:
/// <see cref="Engine.Intrinsics"/> and <see cref="Engine.Global"/> follow the running execution context's realm
/// and so change identity inside a <c>ShadowRealm</c>, which is correct for them, whereas this must not.
/// <see cref="TheSameStateIsVisibleFromInsideAShadowRealm"/> pins that.
/// </para>
/// </summary>
public class HostEngineStateTests
{
    /// <summary>
    /// Stands in for whatever a host actually attaches — a scoped service provider, a request, a workflow
    /// context. <see cref="Reads"/> lives on the state rather than in a captured counter so that the lazy
    /// factory below can stay <see langword="static"/>, which is the whole point of the arrangement.
    /// </summary>
    private sealed class RequestScope
    {
        public RequestScope(string tenant)
        {
            Tenant = tenant;
        }

        public string Tenant { get; }

        public int Reads { get; set; }
    }

    // ---- the slot itself ----

    [Test]
    public void StateRoundTripsThroughTheEngine()
    {
        var engine = new Engine();
        var scope = new RequestScope("alpha");

        engine.HostDefined = scope;

        engine.HostDefined.Should().BeSameAs(scope, "the engine stores the reference verbatim");
        (engine.HostDefined as RequestScope)!.Tenant.Should().Be("alpha");
    }

    [Test]
    public void TheDefaultIsNull()
    {
        new Engine().HostDefined.Should().BeNull();
        new Engine(options => options.Strict = true).HostDefined.Should().BeNull();
    }

    [Test]
    public void StateCanBeReplacedAndDetached()
    {
        var engine = new Engine();

        engine.HostDefined = new RequestScope("first");
        (engine.HostDefined as RequestScope)!.Tenant.Should().Be("first");

        engine.HostDefined = new RequestScope("second");
        (engine.HostDefined as RequestScope)!.Tenant.Should().Be("second");

        // Anything at all, including a value type — the engine never interprets it.
        engine.HostDefined = 42;
        engine.HostDefined.Should().Be(42);

        engine.HostDefined = null;
        engine.HostDefined.Should().BeNull();
    }

    [Test]
    public void TwoEnginesHoldTheirOwnState()
    {
        var engineA = new Engine();
        var engineB = new Engine();

        var scopeA = new RequestScope("alpha");
        engineA.HostDefined = scopeA;

        engineB.HostDefined.Should().BeNull("attaching to one engine must not reach another");

        engineB.HostDefined = new RequestScope("beta");

        engineA.HostDefined.Should().BeSameAs(scopeA);
        (engineB.HostDefined as RequestScope)!.Tenant.Should().Be("beta");
    }

    // ---- what does not clear it ----

    [Test]
    public void StateSurvivesAGlobalSnapshotRestore()
    {
        // The pooling case: an engine reused across requests captures once and restores between them. The
        // restore reverts global bindings and nothing else, so the slot is the host's to manage.
        var engine = new Engine();
        var first = new RequestScope("first");
        engine.HostDefined = first;

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();
        engine.Execute("var leaked = 1;");

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        engine.Evaluate("typeof leaked").Should().Be("undefined", "the restore really happened");
        engine.HostDefined.Should().BeSameAs(first);

        // The other direction matters just as much for a pooled engine: a restore does not put back the state
        // the engine had at capture time either, so the host swaps it per request itself.
        var second = new RequestScope("second");
        engine.HostDefined = second;
        engine.Advanced.RestoreGlobalSnapshot(snapshot);
        engine.HostDefined.Should().BeSameAs(second);
    }

    // ---- the case it exists for ----

    [Test]
    public void ASharedOptionsLazyGlobalResolvesThroughTheEngineState()
    {
        // Registered once, for the process. The factory is static: it captures nothing, which is what makes
        // the Options instance safe to share by every engine, on every tenant, on every thread.
        var options = new Options().AddLazyGlobal("tenant", static engine =>
        {
            var scope = (RequestScope) engine.HostDefined!;
            scope.Reads++;
            return new JsString(scope.Tenant);
        });

        // Once per request: build the engine, attach the request's state. The factory has not run — it runs on
        // the first script read, which is necessarily after the constructor returned.
        var alpha = new Engine(options);
        var alphaScope = new RequestScope("alpha");
        alpha.HostDefined = alphaScope;

        var beta = new Engine(options);
        var betaScope = new RequestScope("beta");
        beta.HostDefined = betaScope;

        alphaScope.Reads.Should().Be(0);
        betaScope.Reads.Should().Be(0);

        alpha.Evaluate("tenant").Should().Be("alpha");
        beta.Evaluate("tenant + '/' + tenant").Should().Be("beta/beta");

        alphaScope.Reads.Should().Be(1);
        betaScope.Reads.Should().Be(1, "the factory still runs at most once per engine");

        // ...and an engine whose script never mentions the name never reaches the state at all.
        var unused = new Engine(options);
        var unusedScope = new RequestScope("gamma");
        unused.HostDefined = unusedScope;
        unused.Evaluate("1 + 1");
        unusedScope.Reads.Should().Be(0);
    }

    [Test]
    public void ThePerEngineLazyGlobalReachesItToo()
    {
        // The per-engine registration could have closed over the state directly; reading it back through the
        // engine is what lets the same static factory serve both registrations.
        var engine = new Engine();
        engine.HostDefined = new RequestScope("alpha");
        engine.AddLazyGlobal("tenant", static e => new JsString(((RequestScope) e.HostDefined!).Tenant));

        engine.Evaluate("tenant").Should().Be("alpha");
    }

    // ---- why the principal realm and not the current one ----

    [Test]
    public void TheSameStateIsVisibleFromInsideAShadowRealm()
    {
        // A ShadowRealm evaluation runs against a Realm Record of its own, whose [[HostDefined]] the
        // specification starts empty (ShadowRealm step 4 performs InitializeHostDefinedRealm). Reading the
        // *current* realm from a host callback made inside one would therefore see nothing. This reads the
        // principal realm, which does not move — and a host that genuinely wants to furnish the shadow
        // realm's own slot has HostInitializeShadowRealm for it, which Jint exposes as
        // Host.InitializeShadowRealm.
        var engine = new Engine();
        var scope = new RequestScope("alpha");
        engine.HostDefined = scope;

        var readTenant = new Func<string>(() => (engine.HostDefined as RequestScope)?.Tenant ?? "<none>");

        engine.SetValue("readTenant", readTenant);
        engine.SetValue("outerOnly", "outer");

        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("readTenant", readTenant);

        // The script really is running in the shadow realm — the outer engine's global is not in scope there —
        // and the host state reads back the same either way.
        shadowRealm.Evaluate("readTenant() + '/' + (typeof outerOnly)").Should().Be("alpha/undefined");
        engine.Evaluate("readTenant() + '/' + (typeof outerOnly)").Should().Be("alpha/string");
    }

    [Test]
    public void AShadowRealmGetsItsOwnEmptySlotAndTheHostHookCanFillIt()
    {
        // The other half of the rule, and the reason the paragraph above is a design rather than a
        // limitation: a shadow realm's own [[HostDefined]] starts empty because it is a distinct Realm
        // Record, and the specification gives the host HostInitializeShadowRealm to populate it. Without
        // this test the documented escape hatch would be an assertion rather than a fact.
        var engine = new Engine(options => options.UseHostFactory(_ => new ShadowRealmMarkingHost()));
        engine.HostDefined = new RequestScope("principal");

        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();
        shadowRealm.SetValue("readOwnRealm", new Func<string>(() => ShadowRealmMarkingHost.LastInitialized ?? "<none>"));

        shadowRealm.Evaluate("readOwnRealm()").Should()
            .Be("shadow", "the host hook ran for the new realm and wrote its own slot");

        (engine.HostDefined as RequestScope)!.Tenant.Should()
            .Be("principal", "furnishing the shadow realm's slot does not disturb the principal realm's");
    }

    /// <summary>
    /// Writes to the <c>[[HostDefined]]</c> of each shadow realm it is told about, and records what it wrote,
    /// which is the only way to observe a non-principal realm's slot from outside the assembly today.
    /// </summary>
    private sealed class ShadowRealmMarkingHost : Jint.Runtime.Host
    {
        internal static string? LastInitialized;

        public override void InitializeShadowRealm(Jint.Runtime.Realm realm)
        {
            realm.HostDefined = "shadow";
            LastInitialized = realm.HostDefined as string;
        }
    }
}

using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers <see cref="PropertyAccessSemantics"/> — how the engine decides what an embedder's
/// <see cref="ObjectInstance"/> subclass promises about its property reads. These live in the public-interface
/// suite on purpose: the project references Jint without any internals access, so everything used here is
/// genuinely reachable by a third-party host.
///
/// <para>
/// The engine <b>derives</b> the answer from the type and nothing else: a subclass that overrides
/// <c>Get</c> is <see cref="PropertyAccessSemantics.Exotic"/>, one that does not is
/// <see cref="PropertyAccessSemantics.Ordinary"/>. Both directions are safe by construction — a type that
/// never overrides <c>Get</c> cannot have non-ordinary <c>Get</c> semantics, and a type that does override it
/// is the only one that can. <c>SetPropertyAccessSemantics</c> exists for the two shapes the rule cannot see,
/// and both are covered below.
/// </para>
///
/// <para>
/// A second, orthogonal thing is derived the same way: whether the type overrides
/// <c>ObjectInstance.TryGetOwnPropertyValue</c>, which answers the own-property question <em>and</em> produces
/// the value without a <see cref="PropertyDescriptor"/>. That one is not a semantics claim — its answer is
/// what <c>GetOwnProperty</c>'s would have been — so it changes only how many descriptors a read costs. Its
/// <c>false</c> is authoritative, and the tests below pin both halves of that.
/// </para>
/// </summary>
public class HostObjectSemanticsTests
{
    // Jint's host-contract verifiers check the Ordinary contract on every read by recomputing it (one probe to
    // walk the chain looking for side-effect-free descriptors, one more inside the Get it compares against), and
    // check a value-hook answer against the descriptor it claims to match (one more, on the hook lanes only).
    // They run in a Debug build, and in Release when Jint.EnableHostContractVerification was set before the
    // first use of any Jint type — which is what this repository's Release verification leg does
    // (JINT_HOST_CONTRACT_VERIFICATION=1). The unverified count is the one the probes-per-read guard is about.
    private static bool Verifying => HostContractVerificationSwitch.Enabled;

    private static readonly int OrdinaryOwnReadProbes = Verifying ? 3 : 1;
    private static readonly int OrdinaryWarmPrototypeReadProbes = Verifying ? 3 : 1;
    private static readonly int HookMemberReadProbes = Verifying ? 3 : 0;
    private static readonly int HookMemberReadHookCalls = Verifying ? 2 : 1;
    private static readonly int HookComputedReadProbes = Verifying ? 1 : 0;
    private static readonly int HookPrototypeReadProbes = Verifying ? 3 : 0;
    private static readonly int HookAbsentReadProbes = Verifying ? 4 : 0;

    // Deferring to the base implementation is the descriptor lane again, plus the verifier that asks
    // GetOwnProperty a second time on each of the two hook consults a verifying read makes.
    private static readonly int HookDeferredReadProbes = Verifying ? 5 : 1;

    [Fact]
    public void OrdinarySemanticsAgreeWithGetOwnPropertyForEveryReadOutcome()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine)
            .Project("own", "own-value")
            .Project("shadowed", "from-host");

        engine.SetValue("host", host);
        engine.Execute("Object.prototype.inherited = 'from-prototype'; Object.prototype.shadowed = 'from-prototype';");

        // own hit
        engine.Evaluate("host.own").Should().Be("own-value");
        // own hit that shadows a prototype property of the same name
        engine.Evaluate("host.shadowed").Should().Be("from-host");
        // own miss resolved on the prototype
        engine.Evaluate("host.inherited").Should().Be("from-prototype");
        // absent everywhere
        engine.Evaluate("host.absent").Should().Be(JsValue.Undefined);

        // ...and the same answers when the read does not go through the interpreter's member-read path
        host.Get("own").Should().Be("own-value");
        host.Get("shadowed").Should().Be("from-host");
        host.Get("inherited").Should().Be("from-prototype");
        host.Get("absent").Should().Be(JsValue.Undefined);
        engine.Evaluate("host['ow' + 'n']").Should().Be("own-value");
        engine.Evaluate("host['absen' + 't']").Should().Be(JsValue.Undefined);
    }

    [Fact]
    public void AHostThatDoesNotOverrideGetResolvesAnOwnReadFromASingleProbe()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine).Project("value", 42);
        engine.SetValue("host", host);

        engine.Evaluate("host.value").Should().Be(42);

        // The whole point of deriving rather than asking: this host declares nothing, and it still gets the
        // single-probe lane, because not overriding Get already proves its Get is the ordinary one. Before the
        // derivation the same type paid two probes and two descriptor allocations per read — one to prove the
        // own property was not shadowed and one inside Get to fetch it.
        host.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
    }

    [Fact]
    public void AHostThatDoesNotOverrideGetResolvesAMemberCallCalleeFromASingleProbe()
    {
        var engine = new Engine();
        var fn = engine.Evaluate("(function () { return 'called'; })");
        var host = new ProjectedHostObject(engine).Project("fn", fn);
        engine.SetValue("host", host);

        // The member-call callee path shares the same non-plain-receiver completion, so it inherits the lane.
        engine.Evaluate("host.fn()").Should().Be("called");

        host.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
    }

    [Fact]
    public void PrototypeMethodResolvedOffAHostReceiverStaysCorrectAcrossPrototypeMutation()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine);
        engine.SetValue("host", host);

        // One member-call node read four times, so the prototype-method inline cache is warm from the second
        // iteration on. Every mutation must still be observed: an in-place function replacement (no version
        // bump — the cached descriptor is live), a delete + redefine, and a swap to an accessor.
        var result = engine.Evaluate(
            """
            var proto = { greet: function () { return 'v1'; } };
            Object.setPrototypeOf(host, proto);

            var seen = [];
            for (var i = 0; i < 4; i++) {
                seen.push(host.greet());
                if (i === 0) {
                    proto.greet = function () { return 'v2'; };
                } else if (i === 1) {
                    delete proto.greet;
                    Object.defineProperty(proto, 'greet', { value: function () { return 'v3'; }, configurable: true, writable: true });
                } else if (i === 2) {
                    Object.defineProperty(proto, 'greet', { get: function () { return function () { return 'v4'; }; }, configurable: true });
                }
            }
            seen.join(',');
            """);

        result.Should().Be("v1,v2,v3,v4");
    }

    [Fact]
    public void AWarmPrototypeReadProbesTheHostOncePerRead()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine);
        engine.SetValue("host", host);
        engine.Execute("Object.prototype.protoMethod = function () { return 1; };");

        engine.Evaluate("var total = 0; for (var i = 0; i < 10; i++) { total += host.protoMethod(); } total;").Should().Be(10);

        // Ten reads of a prototype method through a single node, and each one asks the host whether it now owns
        // that name. That probe cannot be skipped: the host stores its own-property set itself, so nothing the
        // engine watches moves when a projected member appears, and a cached prototype hit reused on trust would
        // keep shadowing it. Its result is what makes the next line honest — the prototype-method cache still
        // answers the read, so the cost is one probe per read rather than the two an uncached read pays (one to
        // establish the miss, one inside the Get that follows it).
        host.GetOwnPropertyCalls.Should().Be(10 * OrdinaryWarmPrototypeReadProbes);
    }

    [Fact]
    public void AHostThatOverridesGetIsHonouredWithoutDeclaringAnything()
    {
        // The correctness hole the derivation closes. The read path used to treat "carries no exotic flag" as
        // "has ordinary [[Get]]", and a subclass defined outside Jint could not carry that flag: a host whose
        // Get computes a value for names it owns no descriptor for was therefore silently bypassed for every
        // name that happened to resolve on its prototype. Object.prototype alone was enough to trigger it.
        var engine = new Engine();
        var host = new ComputingHostObject(engine);
        engine.SetValue("host", host);
        engine.Execute("Object.prototype.onPrototype = 42;");

        // The host's computed value wins, because overriding Get is itself the declaration.
        engine.Evaluate("host.onPrototype").Should().Be("computed:onPrototype");
    }

    [Fact]
    public void AHostThatOverridesGetRoutesEveryReadThroughIt()
    {
        var engine = new Engine();
        var host = new ComputingHostObject(engine).Project("own", "own-value");
        engine.SetValue("host", host);

        engine.Evaluate("host.own").Should().Be("own-value");
        engine.Evaluate("host.other").Should().Be("computed:other");
        engine.Evaluate("host.toString").Should().Be("computed:toString");

        // A repeated read from one node must not be served from any cache either.
        engine.Evaluate("var seen = []; for (var i = 0; i < 3; i++) { seen.push(host.toString); } seen.join('|');")
            .Should().Be("computed:toString|computed:toString|computed:toString");
    }

    [Fact]
    public void AGetOverrideThatIsOrdinaryCanDeclareItselfBackOntoTheShortLane()
    {
        // Residual case one: the rule sees an override and assumes it may deviate. A host that overrides Get
        // only to observe reads — tracing, metrics, a special case that still agrees with GetOwnProperty — says
        // so and gets the single-probe lane back.
        var engine = new Engine();
        var traced = new TracingHostObject(engine).Project("value", 42);
        engine.SetValue("traced", traced);

        engine.Evaluate("traced.value").Should().Be(42);

        traced.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
    }

    [Fact]
    public void AHostThatDoesNotOverrideGetCanStillDeclareItselfExotic()
    {
        // Residual case two: the rule sees no override and concludes Ordinary, but the host knows its
        // GetOwnProperty is not a stable answer for the same name. Declaring Exotic routes every read through
        // Get, which is what makes the second read observe the changed projection.
        var engine = new Engine();
        var host = new MutatingHostObject(engine);
        engine.SetValue("host", host);

        engine.Evaluate("var seen = []; for (var i = 0; i < 3; i++) { seen.push(host.counter); } seen.join(',');")
            .Should().Be("1,2,3");
    }

    [Fact]
    public void ADeclarationCanBeReplaced()
    {
        // Last call wins, so a subclass can override what its base class declared.
        var engine = new Engine();
        var redeclared = new RedeclaringHostObject(engine, PropertyAccessSemantics.Exotic, PropertyAccessSemantics.Ordinary);
        engine.SetValue("redeclared", redeclared);

        engine.Evaluate("redeclared.value").Should().Be("value");
        redeclared.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
    }

    [Fact]
    public void AnUnknownSemanticsValueIsRejected()
    {
        var engine = new Engine();
        Invoking(() => new RedeclaringHostObject(engine, PropertyAccessSemantics.Ordinary, (PropertyAccessSemantics) 99))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AnOwnReadAnsweredByTheValueHookMaterializesNoDescriptor()
    {
        var engine = new Engine();
        var host = new ValueAnsweringHostObject(engine);
        host.Project("fast", "fast-value");
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.fast").Should().Be("fast-value");
        host.TryGetOwnPropertyValueCalls.Should().Be(HookMemberReadHookCalls);
        host.GetOwnPropertyCalls.Should().Be(HookMemberReadProbes);

        // Computed keys never reach the interpreter's member lane, so they exercise the wiring in Get itself.
        host.Reset();
        engine.Evaluate("host['fas' + 't']").Should().Be("fast-value");
        host.GetOwnPropertyCalls.Should().Be(HookComputedReadProbes);
    }

    [Fact]
    public void AFalseFromTheValueHookIsAnAuthoritativeOwnMiss()
    {
        // The half of the contract that makes the hook usable as a replacement for the probe rather than an
        // addition to it: false says "no own property of this name", so the read continues up the chain
        // without asking GetOwnProperty a second time — and the host is not probed at all.
        var engine = new Engine();
        var host = new ValueAnsweringHostObject(engine);
        engine.SetValue("host", host);
        engine.Execute("Object.prototype.inheritedOnly = 'from-prototype';");

        host.Reset();
        engine.Evaluate("host.inheritedOnly").Should().Be("from-prototype");
        host.GetOwnPropertyCalls.Should().Be(HookPrototypeReadProbes);

        host.Reset();
        engine.Evaluate("host.absent").Should().Be(JsValue.Undefined);
        host.GetOwnPropertyCalls.Should().Be(HookAbsentReadProbes);
    }

    [Fact]
    public void AWarmPrototypeReadOffAValueAnsweringHostDoesNotProbeIt()
    {
        // The cost the ordinary host cannot avoid. It re-establishes the own miss with a real probe before
        // every prototype-cache consult, so ten warm reads cost ten probes; the hook re-establishes the same
        // miss by answering false, so they cost none. Correctness is identical — the question is asked on
        // every read either way, which is what AProjectedPropertyAppearingOnAReceiverShadowsThePrototype...
        // in HostObjectPropertySetChangeTests pins for both.
        var engine = new Engine();
        var host = new ValueAnsweringHostObject(engine);
        engine.SetValue("host", host);
        engine.Execute("Object.prototype.protoMethod = function () { return 1; };");

        host.Reset();
        engine.Evaluate("var total = 0; for (var i = 0; i < 10; i++) { total += host.protoMethod(); } total;").Should().Be(10);

        host.GetOwnPropertyCalls.Should().Be(10 * HookPrototypeReadProbes);
    }

    [Fact]
    public void DeferringToTheBaseImplementationResolvesFromTheDescriptor()
    {
        // What an override does with a key it cannot serve itself. `false` would be a lie — the property is
        // there — so the base implementation, which is exactly GetOwnProperty plus the unwrap, answers it.
        var engine = new Engine();
        var host = new ValueAnsweringHostObject(engine);
        host.Project(ValueAnsweringHostObject.DeferredName, "deferred-value");
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host." + ValueAnsweringHostObject.DeferredName).Should().Be("deferred-value");
        host.GetOwnPropertyCalls.Should().Be(HookDeferredReadProbes);

        // ...and the enumeration paths, which only ever see descriptors, agree with all of it.
        engine.Evaluate("Object.keys(host).join(',')").Should().Be(ValueAnsweringHostObject.DeferredName);
        engine.Evaluate("JSON.stringify(host)").Should().Be("""{"deferred":"deferred-value"}""");
    }

    [Fact]
    public void AHostThatDoesNotOverrideTheValueHookIsNeverAsked()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine).Project("value", 1);
        engine.SetValue("host", host);

        // The hook is derived per type, so a host that never heard of it stays on the descriptor lane at
        // exactly the cost it had before the hook existed — the probe count is the ordinary one, unchanged.
        engine.Evaluate("host.value").Should().Be(1);
        host.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
    }

    // ---- Engine.Advanced.GetPropertyAccessSemantics ----

    [Fact]
    public void TheResolvedSemanticsAreReadableFromOutsideTheJintAssembly()
    {
        // This project has no InternalsVisibleTo, so the call below compiling at all is the guarantee: before
        // it existed, everything that decides the answer — the flag, the probe, the probe counter — was
        // internal, and a host could only infer the derivation from probe counts against its own overrides.
        var engine = new Engine();

        engine.Advanced.GetPropertyAccessSemantics(new ProjectedHostObject(engine))
            .Should().Be(PropertyAccessSemantics.Ordinary);
    }

    [Fact]
    public void AHostThatOverridesGetIsReportedExotic()
    {
        var engine = new Engine();

        engine.Advanced.GetPropertyAccessSemantics(new ComputingHostObject(engine))
            .Should().Be(PropertyAccessSemantics.Exotic);
    }

    [Fact]
    public void TwoHostsThatAnswerEveryReadIdenticallyStillReportTheDerivationThatSeparatesThem()
    {
        // The motivating pin, and the reason the diagnostic is worth a public method. These two types project
        // the same fields and answer every read the same way — one overrides Get with a pure pass-through to
        // base, the other does not override it at all — so no assertion about *values* can tell them apart.
        // The derivation still separates them, and it decides how every read against them is routed. Delete
        // PassThroughGetHostObject's one-line override, or move it to a base class where the probe stops
        // seeing it as an override, and the type silently changes from Exotic to Ordinary with every
        // behavioural test still green. This is the assertion that goes red.
        var engine = new Engine();

        var withoutOverride = new ProjectedHostObject(engine).Project("value", 42);
        var withOverride = new PassThroughGetHostObject(engine).Project("value", 42);

        engine.SetValue("a", withoutOverride);
        engine.SetValue("b", withOverride);
        engine.Evaluate("a.value").Should().Be(42);
        engine.Evaluate("b.value").Should().Be(42);
        engine.Evaluate("a.absent").Should().Be(JsValue.Undefined);
        engine.Evaluate("b.absent").Should().Be(JsValue.Undefined);

        engine.Advanced.GetPropertyAccessSemantics(withoutOverride).Should().Be(PropertyAccessSemantics.Ordinary);
        engine.Advanced.GetPropertyAccessSemantics(withOverride).Should().Be(PropertyAccessSemantics.Exotic);
    }

    [Fact]
    public void ADeclarationIsWhatTheDiagnosticReports()
    {
        // For the two shapes the rule cannot see, the declaration is the answer — so the diagnostic reports
        // the resolved semantics, not the derived ones. Both directions, plus the last-call-wins rule.
        var engine = new Engine();

        // overrides Get, declares Ordinary
        engine.Advanced.GetPropertyAccessSemantics(new TracingHostObject(engine))
            .Should().Be(PropertyAccessSemantics.Ordinary);

        // does not override Get, declares Exotic
        engine.Advanced.GetPropertyAccessSemantics(new MutatingHostObject(engine))
            .Should().Be(PropertyAccessSemantics.Exotic);

        engine.Advanced.GetPropertyAccessSemantics(
                new RedeclaringHostObject(engine, PropertyAccessSemantics.Exotic, PropertyAccessSemantics.Ordinary))
            .Should().Be(PropertyAccessSemantics.Ordinary);

        engine.Advanced.GetPropertyAccessSemantics(
                new RedeclaringHostObject(engine, PropertyAccessSemantics.Ordinary, PropertyAccessSemantics.Exotic))
            .Should().Be(PropertyAccessSemantics.Exotic);
    }

    [Fact]
    public void TheDeclarationIsPerInstanceNotPerType()
    {
        // SetPropertyAccessSemantics is called from a constructor and writes the instance, so two instances of
        // one type can resolve differently. A Type-keyed diagnostic could not report this.
        var engine = new Engine();

        engine.Advanced.GetPropertyAccessSemantics(
                new RedeclaringHostObject(engine, PropertyAccessSemantics.Ordinary, PropertyAccessSemantics.Ordinary))
            .Should().Be(PropertyAccessSemantics.Ordinary);

        engine.Advanced.GetPropertyAccessSemantics(
                new RedeclaringHostObject(engine, PropertyAccessSemantics.Ordinary, PropertyAccessSemantics.Exotic))
            .Should().Be(PropertyAccessSemantics.Exotic);
    }

    [Fact]
    public void TheEnginesOwnObjectsAreClassifiedToo()
    {
        // Non-contractual by documentation — an in-box object's classification may be refined in any release —
        // but pinned here so a host reading these answers in a test sees what they are today, and so a change
        // to any of them is a deliberate edit rather than a silent one.
        var engine = new Engine(options => options.AllowClr(typeof(HostPoint).Assembly));

        // Ordinary is the absence of the exotic claim: a plain object, an array and an ordinary function all
        // have exactly ordinary [[Get]].
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("({ a: 1 })").AsObject())
            .Should().Be(PropertyAccessSemantics.Ordinary);
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("[1, 2, 3]").AsObject())
            .Should().Be(PropertyAccessSemantics.Ordinary);
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("(function f() {})").AsObject())
            .Should().Be(PropertyAccessSemantics.Ordinary);

        // The built-ins that genuinely deviate say so.
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("new Proxy({}, {})").AsObject())
            .Should().Be(PropertyAccessSemantics.Exotic);
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("new Uint8Array(4)").AsObject())
            .Should().Be(PropertyAccessSemantics.Exotic);
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("(function () { return arguments; })(1)").AsObject())
            .Should().Be(PropertyAccessSemantics.Exotic);

        // ...and so does a CLR object wrapper, whose members resolve against the wrapped type rather than a
        // descriptor store.
        engine.SetValue("point", new HostPoint());
        engine.Advanced.GetPropertyAccessSemantics(engine.Evaluate("point").AsObject())
            .Should().Be(PropertyAccessSemantics.Exotic);
    }

    [Fact]
    public void AMissingOrForeignObjectIsRejected()
    {
        var engine = new Engine();
        var other = new Engine();

        Invoking(() => engine.Advanced.GetPropertyAccessSemantics(null!))
            .Should().Throw<ArgumentNullException>();

        // The resolved semantics do not actually depend on the engine, but the guard matches
        // GetObjectRepresentation's so that the mixed-up-engine mistake fails loudly in the multi-engine tests
        // these diagnostics are written for.
        Invoking(() => engine.Advanced.GetPropertyAccessSemantics(new ProjectedHostObject(other)))
            .Should().Throw<ArgumentException>();
    }

    /// <summary>A plain CLR object, so a test can ask about the wrapper the engine builds for it.</summary>
    public sealed class HostPoint
    {
        public int X { get; set; }
    }
}

/// <summary>
/// A host object whose properties live outside the engine — no descriptor exists until one is asked for, which
/// is the shape that used to pay twice for every read. It overrides <see cref="GetOwnProperty"/> and nothing
/// else, so the engine derives <see cref="PropertyAccessSemantics.Ordinary"/> for it. Records the probe count
/// so a test can assert how many the engine needed.
/// </summary>
internal class ProjectedHostObject : ObjectInstance
{
    private readonly Dictionary<string, JsValue> _fields = new Dictionary<string, JsValue>(StringComparer.Ordinal);

    public ProjectedHostObject(Engine engine) : base(engine)
    {
    }

    public int GetOwnPropertyCalls { get; private set; }

    public virtual void Reset() => GetOwnPropertyCalls = 0;

    /// <summary>Seeds the projected native state. Not a JavaScript-visible write.</summary>
    public ProjectedHostObject Project(string name, JsValue value)
    {
        _fields[name] = value;
        return this;
    }

    /// <summary>
    /// Drops a projected field, the way the native state behind a host object changes on its own. Like
    /// <see cref="Project"/> this is not a JavaScript-visible write, so nothing in the engine observes it.
    /// </summary>
    public ProjectedHostObject Unproject(string name)
    {
        _fields.Remove(name);
        return this;
    }

    protected bool TryProject(JsValue property, out JsValue value)
    {
        if (property.IsString())
        {
            return _fields.TryGetValue(property.AsString(), out value);
        }

        value = Undefined;
        return false;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        GetOwnPropertyCalls++;

        if (TryProject(property, out var value))
        {
            return new PropertyDescriptor(value, writable: true, enumerable: true, configurable: true);
        }

        return PropertyDescriptor.Undefined;
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>();
        if ((types & Types.String) != Types.Empty)
        {
            foreach (var name in _fields.Keys)
            {
                keys.Add(new JsString(name));
            }
        }

        return keys;
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        foreach (var field in _fields)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(
                new JsString(field.Key),
                new PropertyDescriptor(field.Value, writable: true, enumerable: true, configurable: true));
        }
    }
}

/// <summary>
/// A host object with genuinely non-ordinary reads: <see cref="Get"/> computes a value for every name it owns
/// no descriptor for, so a read that never reaches <c>Get</c> is a read the host never sees. It declares
/// nothing — overriding <c>Get</c> is what tells the engine so.
/// </summary>
internal sealed class ComputingHostObject : ProjectedHostObject
{
    public ComputingHostObject(Engine engine) : base(engine)
    {
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (TryProject(property, out var value))
        {
            return value;
        }

        return new JsString("computed:" + property);
    }
}

/// <summary>
/// Overrides <see cref="Get"/> with a pure pass-through, so it is derived exotic while behaving exactly like
/// the ordinary host. The control for any test that must produce the same answer under both derived outcomes.
/// </summary>
internal sealed class PassThroughGetHostObject : ProjectedHostObject
{
    public PassThroughGetHostObject(Engine engine) : base(engine)
    {
    }

    public override JsValue Get(JsValue property, JsValue receiver) => base.Get(property, receiver);
}

/// <summary>
/// Overrides <see cref="Get"/> without deviating from it — the shape the derivation rule is deliberately
/// pessimistic about, and the reason <c>SetPropertyAccessSemantics</c> survives.
/// </summary>
internal sealed class TracingHostObject : ProjectedHostObject
{
    public TracingHostObject(Engine engine) : base(engine)
    {
        SetPropertyAccessSemantics(PropertyAccessSemantics.Ordinary);
    }

    public int GetCalls { get; private set; }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        GetCalls++;
        return base.Get(property, receiver);
    }
}

/// <summary>
/// Does not override <see cref="ObjectInstance.Get"/>, yet is not ordinary: its projection changes on every
/// probe, so a descriptor is only ever true for the call that produced it. The other reason
/// <c>SetPropertyAccessSemantics</c> survives.
/// </summary>
internal sealed class MutatingHostObject : ProjectedHostObject
{
    private int _counter;

    public MutatingHostObject(Engine engine) : base(engine)
    {
        SetPropertyAccessSemantics(PropertyAccessSemantics.Exotic);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property.IsString() && string.Equals(property.AsString(), "counter", StringComparison.Ordinal))
        {
            return new PropertyDescriptor(++_counter, writable: true, enumerable: true, configurable: true);
        }

        return base.GetOwnProperty(property);
    }
}

/// <summary>
/// Hands own values straight over from native storage, so an own read materializes no descriptor at all — and
/// answers <c>false</c> for a name it does not project, which the engine takes as proof there is no own
/// property of that name. One name is deliberately left to the base implementation, covering the only correct
/// way to say "not me" about a property that does exist.
/// </summary>
internal sealed class ValueAnsweringHostObject : ProjectedHostObject
{
    public const string DeferredName = "deferred";

    public ValueAnsweringHostObject(Engine engine) : base(engine)
    {
    }

    public int TryGetOwnPropertyValueCalls { get; private set; }

    public override void Reset()
    {
        base.Reset();
        TryGetOwnPropertyValueCalls = 0;
    }

    // Outside the Jint assembly a protected-internal member is inherited as protected, so that is what an
    // embedder writes here.
    protected override bool TryGetOwnPropertyValue(JsValue property, JsValue receiver, out JsValue value)
    {
        TryGetOwnPropertyValueCalls++;

        // Not `return false` — this host owns the name, it just declines to produce the value itself, and
        // false would state the opposite. base is the descriptor-driven answer.
        if (property.IsString() && string.Equals(property.AsString(), DeferredName, StringComparison.Ordinal))
        {
            return base.TryGetOwnPropertyValue(property, receiver, out value);
        }

        return TryProject(property, out value);
    }
}

/// <summary>Declares twice, mimicking a subclass overriding what its base class declared.</summary>
internal sealed class RedeclaringHostObject : ProjectedHostObject
{
    public RedeclaringHostObject(Engine engine, PropertyAccessSemantics first, PropertyAccessSemantics second)
        : base(engine)
    {
        Project("value", "value");
        SetPropertyAccessSemantics(first);
        SetPropertyAccessSemantics(second);
    }
}

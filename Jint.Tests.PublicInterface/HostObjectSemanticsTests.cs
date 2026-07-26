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
/// </summary>
public class HostObjectSemanticsTests
{
    // A Debug build of Jint verifies the Ordinary contract on every read by recomputing it (one probe to walk
    // the chain looking for side-effect-free descriptors, one more inside the Get it compares against).
    // Release strips the verification, and its count is the one the probes-per-read guard is about.
#if DEBUG
    private const int OrdinaryOwnReadProbes = 3;
#else
    private const int OrdinaryOwnReadProbes = 1;
#endif

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
    public void AWarmPrototypeReadDoesNotReprobeTheHost()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine);
        engine.SetValue("host", host);
        engine.Execute("Object.prototype.protoMethod = function () { return 1; };");

        engine.Evaluate("var total = 0; for (var i = 0; i < 10; i++) { total += host.protoMethod(); } total;").Should().Be(10);

        // The ordinary lane must not cost the prototype-method cache: ten reads of a prototype method through a
        // single node probe the receiver once, not ten times. That is why the lane consults that cache before it
        // probes for an own property — doing it the other way round turns the nine cached iterations back into
        // nine virtual probes.
        host.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
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

    /// <summary>Seeds the projected native state. Not a JavaScript-visible write.</summary>
    public ProjectedHostObject Project(string name, JsValue value)
    {
        _fields[name] = value;
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

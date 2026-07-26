using System.Collections.Generic;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers <see cref="PropertyAccessSemantics"/>, the opt-in declaration an embedder's
/// <see cref="ObjectInstance"/> subclass uses to tell the engine how its property reads behave. These live in
/// the public-interface suite on purpose: the project references Jint without any internals access, so
/// everything used here is genuinely reachable by a third-party host.
/// </summary>
public class HostObjectSemanticsTests
{
    // A Debug build of Jint verifies the Ordinary contract on every read by recomputing it (one probe to walk
    // the chain looking for side-effect-free descriptors, one more inside the Get it compares against).
    // Release strips the verification, and its count is the one the two-probes-per-read guard is about.
#if DEBUG
    private const int OrdinaryOwnReadProbes = 3;
#else
    private const int OrdinaryOwnReadProbes = 1;
#endif

    private const int UnspecifiedOwnReadProbes = 2;

    [Fact]
    public void OrdinarySemanticsAgreeWithGetOwnPropertyForEveryReadOutcome()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine, PropertyAccessSemantics.Ordinary)
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
    public void OrdinarySemanticsResolveAnOwnReadFromASingleProbe()
    {
        var engine = new Engine();
        var ordinary = new ProjectedHostObject(engine, PropertyAccessSemantics.Ordinary).Project("value", 42);
        var unspecified = new ProjectedHostObject(engine, PropertyAccessSemantics.Unspecified).Project("value", 42);

        engine.SetValue("ordinary", ordinary);
        engine.SetValue("unspecified", unspecified);

        // Each Evaluate compiles a fresh AST, so both reads below start from a cold inline cache.
        engine.Evaluate("ordinary.value").Should().Be(42);
        engine.Evaluate("unspecified.value").Should().Be(42);

        // The regression guard: an object that declared ordinary [[Get]] is probed exactly once per own read.
        // Without the declaration the engine probes once to prove the own property exists and a second time
        // inside Get to fetch it — for a host that materializes descriptors lazily that is two virtual calls
        // and two descriptor allocations per read, forever.
        ordinary.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
        unspecified.GetOwnPropertyCalls.Should().Be(UnspecifiedOwnReadProbes);
    }

    [Fact]
    public void OrdinarySemanticsResolveAMemberCallCalleeFromASingleProbe()
    {
        var engine = new Engine();
        var fn = engine.Evaluate("(function () { return 'called'; })");
        var ordinary = new ProjectedHostObject(engine, PropertyAccessSemantics.Ordinary).Project("fn", fn);
        var unspecified = new ProjectedHostObject(engine, PropertyAccessSemantics.Unspecified).Project("fn", fn);

        engine.SetValue("ordinary", ordinary);
        engine.SetValue("unspecified", unspecified);

        // The member-call callee path shares the same non-plain-receiver completion, so it inherits the lane.
        engine.Evaluate("ordinary.fn()").Should().Be("called");
        engine.Evaluate("unspecified.fn()").Should().Be("called");

        ordinary.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
        unspecified.GetOwnPropertyCalls.Should().Be(UnspecifiedOwnReadProbes);
    }

    [Fact]
    public void PrototypeMethodResolvedOffAHostReceiverStaysCorrectAcrossPrototypeMutation()
    {
        var engine = new Engine();
        var host = new ProjectedHostObject(engine, PropertyAccessSemantics.Ordinary);
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
        var ordinary = new ProjectedHostObject(engine, PropertyAccessSemantics.Ordinary);
        var unspecified = new ProjectedHostObject(engine, PropertyAccessSemantics.Unspecified);
        engine.SetValue("ordinary", ordinary);
        engine.SetValue("unspecified", unspecified);
        engine.Execute("Object.prototype.protoMethod = function () { return 1; };");

        engine.Evaluate("var total = 0; for (var i = 0; i < 10; i++) { total += ordinary.protoMethod(); } total;").Should().Be(10);
        engine.Evaluate("var total = 0; for (var i = 0; i < 10; i++) { total += unspecified.protoMethod(); } total;").Should().Be(10);

        // The declaration must not cost the prototype-method cache: ten reads of a prototype method through a
        // single node probe the receiver once, exactly as they do without the declaration. That is why the
        // ordinary lane consults that cache before it probes for an own property — doing it the other way round
        // turns the nine cached iterations back into nine virtual probes.
        ordinary.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);
        unspecified.GetOwnPropertyCalls.Should().Be(1);
    }

    [Fact]
    public void HostGetIsBypassedWithoutADeclarationAndHonouredWithExotic()
    {
        // The correctness hole this API closes: the read path treats "does not declare exotic semantics" as
        // "has ordinary [[Get]]" and can answer from a prototype descriptor without ever calling the host's
        // Get. A host that computes a fallback for names it owns no descriptor for is therefore silently
        // bypassed for any name that happens to resolve on its prototype. Declaring Exotic fixes it.
        var undeclared = ReadOnPrototypeName(PropertyAccessSemantics.Unspecified);
        var declared = ReadOnPrototypeName(PropertyAccessSemantics.Exotic);

        // Documents the hole: the prototype's value wins over the host's computed fallback.
        undeclared.Should().Be(42);
        declared.Should().Be("computed:onPrototype");

        static JsValue ReadOnPrototypeName(PropertyAccessSemantics semantics)
        {
            var engine = new Engine();
            var host = new ComputingHostObject(engine, semantics);
            engine.SetValue("host", host);
            engine.Execute("Object.prototype.onPrototype = 42;");
            return engine.Evaluate("host.onPrototype");
        }
    }

    [Fact]
    public void ExoticSemanticsRouteEveryReadThroughGet()
    {
        var engine = new Engine();
        var host = new ComputingHostObject(engine, PropertyAccessSemantics.Exotic).Project("own", "own-value");
        engine.SetValue("host", host);

        engine.Evaluate("host.own").Should().Be("own-value");
        engine.Evaluate("host.other").Should().Be("computed:other");
        engine.Evaluate("host.toString").Should().Be("computed:toString");

        // A repeated read from one node must not be served from any cache either.
        engine.Evaluate("var seen = []; for (var i = 0; i < 3; i++) { seen.push(host.toString); } seen.join('|');")
            .Should().Be("computed:toString|computed:toString|computed:toString");
    }

    [Fact]
    public void DeclarationCanBeReplacedAndUnspecifiedClearsIt()
    {
        var engine = new Engine();

        // Last call wins, so a subclass can override what its base class declared.
        var redeclared = new RedeclaringHostObject(engine, PropertyAccessSemantics.Exotic, PropertyAccessSemantics.Ordinary);
        engine.SetValue("redeclared", redeclared);
        engine.Evaluate("redeclared.value").Should().Be("value");
        redeclared.GetOwnPropertyCalls.Should().Be(OrdinaryOwnReadProbes);

        var cleared = new RedeclaringHostObject(engine, PropertyAccessSemantics.Ordinary, PropertyAccessSemantics.Unspecified);
        engine.SetValue("cleared", cleared);
        engine.Evaluate("cleared.value").Should().Be("value");
        cleared.GetOwnPropertyCalls.Should().Be(UnspecifiedOwnReadProbes);
    }

    [Fact]
    public void AnUnknownSemanticsValueIsRejected()
    {
        var engine = new Engine();
        Invoking(() => new ProjectedHostObject(engine, (PropertyAccessSemantics) 99))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}

/// <summary>
/// A host object whose properties live outside the engine — no descriptor exists until one is asked for, which
/// is the shape that pays twice for every read without a semantics declaration. Records the probe count so a
/// test can assert how many the engine needed.
/// </summary>
internal class ProjectedHostObject : ObjectInstance
{
    private readonly Dictionary<string, JsValue> _fields = new Dictionary<string, JsValue>(StringComparer.Ordinal);

    public ProjectedHostObject(Engine engine, PropertyAccessSemantics semantics) : base(engine)
    {
        SetPropertyAccessSemantics(semantics);
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
/// no descriptor for, so a read that never reaches <c>Get</c> is a read the host never sees.
/// </summary>
internal sealed class ComputingHostObject : ProjectedHostObject
{
    public ComputingHostObject(Engine engine, PropertyAccessSemantics semantics) : base(engine, semantics)
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

/// <summary>Declares twice, mimicking a subclass overriding what its base class declared.</summary>
internal sealed class RedeclaringHostObject : ProjectedHostObject
{
    public RedeclaringHostObject(Engine engine, PropertyAccessSemantics first, PropertyAccessSemantics second)
        : base(engine, first)
    {
        Project("value", "value");
        SetPropertyAccessSemantics(second);
    }
}

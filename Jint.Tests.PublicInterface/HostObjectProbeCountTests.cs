#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Regression guard for how many times the engine asks a <b>host object</b> — a custom
/// <see cref="ObjectInstance"/> subclass, the shape every embedder that projects native data
/// writes — for one of its own properties.
///
/// <para>
/// A host receiver is neither shape-mode nor plain-object, so member reads take the non-plain
/// lane: it first probes <c>GetOwnProperty</c> to establish whether the name is shadowed on the
/// receiver, discards that descriptor, and then calls <c>Get</c>, which probes a second time to
/// produce the value. Two virtual calls, two freshly built <see cref="PropertyDescriptor"/>
/// instances, one of them thrown away — per own-property read.
/// </para>
///
/// <para>
/// <b>These numbers assert today's behaviour, not desired behaviour.</b> They are expected to
/// <b>drop</b> when the host-object ordinary-access-semantics work lands (a host that declares its
/// own-property set up front should be readable in a single probe, and ideally without
/// materializing a descriptor at all). When that happens, update the expected values here
/// deliberately — a silent change in either direction is the thing this test exists to catch.
/// </para>
/// </summary>
public class HostObjectProbeCountTests
{
    [Fact]
    public void SimpleOwnPropertyReadProbesTheHostObjectTwice()
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine);
        engine.SetValue("host", host);

        host.Reset();
        var value = engine.Evaluate("host.prop;");

        value.Should().Be("value-of-prop");

        // Today: one probe to prove the name is not shadowed (descriptor discarded) plus one inside
        // Get to produce the value. Should become 1 — or 0 descriptors — with ordinary access semantics.
        host.GetOwnPropertyCallCount.Should().Be(2);
    }

    [Fact]
    public void EachOwnPropertyReadCostsItsOwnProbes()
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop; host.other; host.prop;");

        // Nothing is cached across reads for a host receiver: three reads, three times the
        // single-read cost.
        host.GetOwnPropertyCallCount.Should().Be(6);
    }

    [Fact]
    public void MissingOwnPropertyReadAlsoProbesTwice()
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.notThere;").Should().BeUndefined();

        // A miss is not cheaper than a hit: the prototype walk re-probes the receiver.
        host.GetOwnPropertyCallCount.Should().Be(2);
    }

    [Fact]
    public void MemberCallBaseProbesOnce()
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop.toUpperCase();").Should().Be("VALUE-OF-PROP");

        // Reading a host property as the base of a member call takes a different lane than reading
        // it as a value, and that lane already costs only one probe — evidence that the extra probe
        // on the value lane is removable rather than intrinsic.
        host.GetOwnPropertyCallCount.Should().Be(1);
    }
}

/// <summary>
/// The minimum viable host object: it projects a couple of fields on demand and counts how often
/// the engine asks. Written against the public surface only, exactly as an embedder must.
/// </summary>
file sealed class ProbeCountingHostObject : ObjectInstance
{
    public ProbeCountingHostObject(Engine engine) : base(engine)
    {
    }

    public int GetOwnPropertyCallCount { get; private set; }

    public void Reset() => GetOwnPropertyCallCount = 0;

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        GetOwnPropertyCallCount++;

        if (!property.IsString())
        {
            return PropertyDescriptor.Undefined;
        }

        var name = property.ToString();
        if (!string.Equals(name, "prop", StringComparison.Ordinal) && !string.Equals(name, "other", StringComparison.Ordinal))
        {
            return PropertyDescriptor.Undefined;
        }

        // A fresh descriptor per probe — the only thing the public API allows.
        return new PropertyDescriptor(new JsString("value-of-" + name), writable: true, enumerable: true, configurable: true);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>(2);
        if ((types & Types.String) != Types.Empty)
        {
            keys.Add(new JsString("prop"));
            keys.Add(new JsString("other"));
        }

        return keys;
    }
}

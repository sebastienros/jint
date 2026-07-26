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
/// lane. Without a <see cref="PropertyAccessSemantics"/> declaration the engine cannot assume the
/// host's <c>Get</c> is the ordinary one, so it probes <c>GetOwnProperty</c> to establish whether
/// the name is shadowed on the receiver, discards that descriptor, and then calls <c>Get</c>, which
/// probes a second time to produce the value. Two virtual calls, two freshly built
/// <see cref="PropertyDescriptor"/> instances, one of them thrown away — per own-property read.
/// </para>
///
/// <para>
/// <b>What changed.</b> These counts were originally written as "today's behaviour, not desired
/// behaviour", with a note that they should drop once a host could declare its access semantics.
/// They have: a host that declares <see cref="PropertyAccessSemantics.Ordinary"/> resolves an
/// own-property <i>hit</i> from a single probe, because the probe that proves the own property is
/// not shadowed <i>is</i> the read. The <see cref="PropertyAccessSemantics.Unspecified"/> column is
/// unchanged and stays here on purpose — it is the default, so it is what a host that declares
/// nothing still pays. Two counts did <i>not</i> drop, and both are recorded here so the difference
/// is not mistaken for an oversight: an own-property <i>miss</i> still ends in a <c>Get</c> that
/// re-probes the receiver, because the probe that establishes the miss cannot also produce a value;
/// and the base of a member call is resolved straight through <c>Get</c> rather than through the
/// member-read lane, so it already cost one probe and the declaration does not touch it.
/// </para>
///
/// <para>
/// The counts are still assertions about behaviour rather than about desirability. The remaining
/// cost a declaration alone cannot remove is the descriptor a hit materializes at all. Update these
/// values deliberately when that changes — a silent change in either direction is the thing this
/// test exists to catch.
/// </para>
/// </summary>
public class HostObjectProbeCountTests
{
    // A Debug build of Jint verifies an Ordinary declaration on every read by recomputing the read
    // through Get and comparing, which costs probes of its own. Release strips the verification
    // entirely, and Release is the configuration these numbers are about.
#if DEBUG
    private const int OrdinaryOwnHit = 3;
    private const int OrdinaryOwnMiss = 4;
#else
    private const int OrdinaryOwnHit = 1;
    private const int OrdinaryOwnMiss = 2;
#endif

    private const int UnspecifiedOwnHit = 2;
    private const int UnspecifiedOwnMiss = 2;

    // The base of a member call is resolved straight through ObjectInstance.Get rather than through
    // the member-read lane, so it costs one probe with or without a declaration — and the Debug
    // verifier, which lives in the member-read lane, never runs for it.
    private const int MemberCallBase = 1;

    [Theory]
    [InlineData(PropertyAccessSemantics.Unspecified, UnspecifiedOwnHit)]
    [InlineData(PropertyAccessSemantics.Ordinary, OrdinaryOwnHit)]
    public void AnOwnPropertyReadProbesTheHostObject(PropertyAccessSemantics semantics, int expectedProbes)
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine, semantics);
        engine.SetValue("host", host);

        host.Reset();
        var value = engine.Evaluate("host.prop;");

        value.Should().Be("value-of-prop");

        // Undeclared: one probe to prove the name is not shadowed (descriptor discarded) plus one
        // inside Get to produce the value. Declared Ordinary: the first probe is the read.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(PropertyAccessSemantics.Unspecified, 3 * UnspecifiedOwnHit)]
    [InlineData(PropertyAccessSemantics.Ordinary, 3 * OrdinaryOwnHit)]
    public void EachOwnPropertyReadCostsItsOwnProbes(PropertyAccessSemantics semantics, int expectedProbes)
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine, semantics);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop; host.other; host.prop;");

        // Nothing is cached across reads for a host receiver, with or without the declaration:
        // three reads, three times the single-read cost.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(PropertyAccessSemantics.Unspecified, UnspecifiedOwnMiss)]
    [InlineData(PropertyAccessSemantics.Ordinary, OrdinaryOwnMiss)]
    public void AMissingOwnPropertyReadIsNoCheaperThanAHit(PropertyAccessSemantics semantics, int expectedProbes)
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine, semantics);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.notThere;").Should().BeUndefined();

        // A miss is not cheaper than a hit, and the declaration does not make it so: the probe that
        // establishes the miss cannot also produce a value, so the read still ends in a Get that
        // re-probes the receiver before walking to the prototype.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(PropertyAccessSemantics.Unspecified)]
    [InlineData(PropertyAccessSemantics.Ordinary)]
    public void AMemberCallBaseProbesOnce(PropertyAccessSemantics semantics)
    {
        var engine = new Engine();
        var host = new ProbeCountingHostObject(engine, semantics);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop.toUpperCase();").Should().Be("VALUE-OF-PROP");

        // Reading a host property as the base of a member call takes a different lane than reading it
        // as a value: it goes straight through ObjectInstance.Get, so it already cost only one probe
        // undeclared — which is the evidence that the extra probe on the value lane was removable
        // rather than intrinsic. That lane is unaffected by the declaration, so this stays at one.
        host.GetOwnPropertyCallCount.Should().Be(MemberCallBase);
    }
}

/// <summary>
/// The minimum viable host object: it projects a couple of fields on demand and counts how often
/// the engine asks. Written against the public surface only, exactly as an embedder must.
/// </summary>
file sealed class ProbeCountingHostObject : ObjectInstance
{
    public ProbeCountingHostObject(Engine engine, PropertyAccessSemantics semantics) : base(engine)
    {
        SetPropertyAccessSemantics(semantics);
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

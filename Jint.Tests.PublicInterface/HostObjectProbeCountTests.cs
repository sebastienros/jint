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
/// A host receiver is neither shape-mode nor plain-object, so member reads take the non-plain lane. What that
/// lane may assume is decided by the object's <see cref="PropertyAccessSemantics"/>, which the engine derives
/// from the type: a subclass that overrides <c>Get</c> is exotic, one that does not is ordinary.
/// </para>
///
/// <para>
/// <b>What changed.</b> These counts were originally written as "today's behaviour, not desired behaviour",
/// with a note that they should drop once the engine could tell an ordinary host from an exotic one. It can, so
/// there are two columns and no host is left paying the old price:
/// </para>
///
/// <list type="bullet">
/// <item><description>
/// <b>Ordinary</b> — a host overriding only <c>GetOwnProperty</c>. An own-property <i>hit</i> costs one probe,
/// because the probe that proves the own property is not shadowed <i>is</i> the read. It used to cost two: one
/// to prove that, discarded, and one inside <c>Get</c> to produce the value. Nothing had to be declared for
/// this — not overriding <c>Get</c> is already proof that <c>Get</c> is the ordinary one.
/// </description></item>
/// <item><description>
/// <b>Exotic</b> — a host overriding <c>Get</c>. Every read is routed through <c>Get</c> and the engine probes
/// nothing on its own behalf, so the count is whatever that <c>Get</c> chooses to do. The host below delegates
/// to <c>base.Get</c>, which probes once. This column is not a regression from the old undeclared behaviour but
/// an improvement on it: such a host used to have its <c>Get</c> bypassed entirely for any name resolving on
/// its prototype.
/// </description></item>
/// </list>
///
/// <para>
/// One count is worth reading twice, because it is the one that did <i>not</i> improve: an own-property
/// <i>miss</i> costs the ordinary host two probes, the same as before. The probe that establishes the miss
/// cannot also produce a value, so the read still ends in a <c>Get</c> that re-probes the receiver before
/// walking to the prototype. The exotic host pays one there simply because only its <c>Get</c> probes at all.
/// </para>
///
/// <para>
/// The counts are still assertions about behaviour rather than about desirability. Update them deliberately —
/// a silent change in either direction is the thing this test exists to catch.
/// </para>
/// </summary>
public class HostObjectProbeCountTests
{
    // A Debug build of Jint verifies the ordinary semantics on every read, by recomputing the read through Get
    // and through GetOwnProperty and comparing, which costs probes of its own. Release strips the verification
    // entirely, and Release is the configuration these numbers are about.
#if DEBUG
    private const int OrdinaryOwnHit = 3;
    private const int OrdinaryOwnMiss = 4;
#else
    private const int OrdinaryOwnHit = 1;
    private const int OrdinaryOwnMiss = 2;
#endif

    // The exotic host's Get delegates to base.Get, which probes once whatever the outcome: on a hit the probe
    // produces the value, on a miss it establishes the miss before the prototype walk. Nothing in the
    // interpreter probes on its own behalf for an exotic receiver, so these are entirely the host's own doing.
    private const int ExoticOwnHit = 1;
    private const int ExoticOwnMiss = 1;

    // The base of a member call resolves straight through ObjectInstance.Get rather than through the
    // member-read lane, so it costs one probe in both columns — and the member lane's Debug verifier never runs
    // for it. This was already true before the derivation; it is what showed that the extra probe on the value
    // lane was removable rather than intrinsic.
    private const int MemberCallBase = 1;

    [Theory]
    [InlineData(false, OrdinaryOwnHit)]
    [InlineData(true, ExoticOwnHit)]
    public void AnOwnPropertyReadProbesTheHostObject(bool overridesGet, int expectedProbes)
    {
        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, overridesGet);
        engine.SetValue("host", host);

        host.Reset();
        var value = engine.Evaluate("host.prop;");

        value.Should().Be("value-of-prop");

        // Ordinary: the probe that proves the name is not shadowed is the read. Exotic: the interpreter probes
        // nothing and the single probe is the one base.Get makes.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(false, 3 * OrdinaryOwnHit)]
    [InlineData(true, 3 * ExoticOwnHit)]
    public void EachOwnPropertyReadCostsItsOwnProbes(bool overridesGet, int expectedProbes)
    {
        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, overridesGet);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop; host.other; host.prop;");

        // Nothing is cached across reads for a host receiver in either column: three reads, three times the
        // single-read cost.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(false, OrdinaryOwnMiss)]
    [InlineData(true, ExoticOwnMiss)]
    public void AMissingOwnPropertyReadIsNoCheaperThanAHit(bool overridesGet, int expectedProbes)
    {
        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, overridesGet);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.notThere;").Should().BeUndefined();

        // For the ordinary host a miss is not cheaper than a hit: the probe that establishes the miss cannot
        // produce a value, so the read still ends in a Get that re-probes the receiver before walking to the
        // prototype. This is the one count the derivation did not move.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMemberCallBaseProbesOnce(bool overridesGet)
    {
        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, overridesGet);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop.toUpperCase();").Should().Be("VALUE-OF-PROP");

        // Reading a host property as the base of a member call takes a different lane than reading it as a
        // value: it goes straight through ObjectInstance.Get, so it already cost only one probe before any of
        // this — which is the evidence that the extra probe on the value lane was removable rather than
        // intrinsic. Neither derived outcome changes it.
        host.GetOwnPropertyCallCount.Should().Be(MemberCallBase);
    }
}

/// <summary>
/// The minimum viable host object: it projects a couple of fields on demand and counts how often the engine
/// asks. Written against the public surface only, exactly as an embedder must. The subclass below overrides
/// <c>Get</c> without changing what it answers, so the two differ in exactly one thing — the semantics the
/// engine derives for them.
/// </summary>
internal class ProbeCountingHostObject : ObjectInstance
{
    protected ProbeCountingHostObject(Engine engine) : base(engine)
    {
    }

    internal static ProbeCountingHostObject Create(Engine engine, bool overridesGet)
        => overridesGet ? new GetOverridingProbeCountingHostObject(engine) : new ProbeCountingHostObject(engine);

    public int GetOwnPropertyCallCount { get; private set; }

    public void Reset() => GetOwnPropertyCallCount = 0;

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        GetOwnPropertyCallCount++;

        if (!TryProject(property, out var value))
        {
            return PropertyDescriptor.Undefined;
        }

        // A fresh descriptor per probe — all a host that only overrides GetOwnProperty can do.
        return new PropertyDescriptor(value, writable: true, enumerable: true, configurable: true);
    }

    private static bool TryProject(JsValue property, out JsValue value)
    {
        if (property.IsString())
        {
            var name = property.ToString();
            if (string.Equals(name, "prop", StringComparison.Ordinal) || string.Equals(name, "other", StringComparison.Ordinal))
            {
                value = new JsString("value-of-" + name);
                return true;
            }
        }

        value = Undefined;
        return false;
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

/// <summary>
/// Identical behaviour, one difference: it overrides <c>Get</c>, so the engine derives
/// <see cref="PropertyAccessSemantics.Exotic"/> for it and routes every read through that override.
/// </summary>
internal sealed class GetOverridingProbeCountingHostObject : ProbeCountingHostObject
{
    public GetOverridingProbeCountingHostObject(Engine engine) : base(engine)
    {
    }

    public override JsValue Get(JsValue property, JsValue receiver) => base.Get(property, receiver);
}

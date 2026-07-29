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
/// from the type: a subclass that overrides <c>Get</c> is exotic, one that does not is ordinary. A second,
/// orthogonal thing is derived alongside it: whether the type overrides
/// <c>ObjectInstance.TryGetOwnPropertyValue</c>, which answers the own-property question and produces the
/// value in one call and without a <see cref="PropertyDescriptor"/>.
/// </para>
///
/// <para>
/// <b>What changed.</b> These counts were originally written as "today's behaviour, not desired behaviour",
/// with a note that they should drop once the engine could tell an ordinary host from an exotic one. It can,
/// and a host can now also answer the question itself, so there are three columns and no host is left paying
/// the old price:
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
/// <b>Ordinary + value hook</b> — the same host, additionally overriding <c>TryGetOwnPropertyValue</c>.
/// <b>Zero</b> probes and no descriptor, on every outcome: the hook's <c>true</c> carries the value, and its
/// <c>false</c> is an authoritative own miss the read continues past without asking again. It is not
/// member-read-only — the base of a member call resolves through <c>Get</c>, which consults the hook too.
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
/// Two counts are worth reading twice. An own-property <i>miss</i> costs the <b>ordinary</b> host two probes,
/// the same as before: the probe that establishes the miss cannot also produce a value, so the read still ends
/// in a <c>Get</c> that re-probes the receiver before walking to the prototype. And a <i>prototype</i> hit
/// costs it one, warm cache or cold — a host stores its own-property set itself, so nothing the engine
/// versions moves when a projected member appears, and the own miss has to be re-established on every single
/// read before the prototype-method cache may be trusted. The value hook is what removes both, because a
/// <c>false</c> re-establishes exactly the same miss at no cost.
/// </para>
///
/// <para>
/// The counts are still assertions about behaviour rather than about desirability. Update them deliberately —
/// a silent change in either direction is the thing this test exists to catch.
/// </para>
/// </summary>
public class HostObjectProbeCountTests
{
    /// <summary>
    /// What one read costs a host of a given shape, in <c>GetOwnProperty</c> calls.
    /// </summary>
    private readonly record struct ProbeCosts(int OwnHit, int OwnMiss, int PrototypeHit, int MemberCallBase);

    // Jint's host-contract verifiers recompute an ordinary read through both Get and GetOwnProperty and compare
    // them, and check a value-hook answer against the descriptor it claims to agree with. Both cost probes of
    // their own. They run in a Debug build, and in Release when Jint.EnableHostContractVerification was set
    // before the first use of any Jint type — which is what this repository's Release verification leg does
    // (JINT_HOST_CONTRACT_VERIFICATION=1). The unverified numbers are the ones an embedder pays, and the ones
    // every cost claim about this lane is about.
    private static ProbeCosts CostsFor(ProbeCountingHostKind kind) => kind switch
    {
        ProbeCountingHostKind.Ordinary => HostContractVerificationSwitch.Enabled
            ? new ProbeCosts(OwnHit: 3, OwnMiss: 4, PrototypeHit: 3, MemberCallBase: 1)
            : new ProbeCosts(OwnHit: 1, OwnMiss: 2, PrototypeHit: 1, MemberCallBase: 1),

        ProbeCountingHostKind.OrdinaryWithValueHook => HostContractVerificationSwitch.Enabled
            ? new ProbeCosts(OwnHit: 3, OwnMiss: 4, PrototypeHit: 3, MemberCallBase: 1)
            : new ProbeCosts(OwnHit: 0, OwnMiss: 0, PrototypeHit: 0, MemberCallBase: 0),

        // The exotic host's Get delegates to base.Get, which probes once whatever the outcome: on a hit the
        // probe produces the value, on a miss it establishes the miss before the prototype walk. Nothing in the
        // interpreter probes on its own behalf for an exotic receiver, so these are entirely the host's own
        // doing — which is also why no verifier runs for them and the counts do not depend on the configuration.
        //
        // The base of a member call resolves straight through ObjectInstance.Get rather than through the
        // member-read lane, so it costs one probe in the two descriptor columns — and the member lane's verifier
        // never runs for it. This was already true before the derivation; it is what showed that the extra probe
        // on the value lane was removable rather than intrinsic. The value hook does reach this lane, because
        // Get consults it, which is what shows the hook is not member-read-only.
        _ => new ProbeCosts(OwnHit: 1, OwnMiss: 1, PrototypeHit: 1, MemberCallBase: 1),
    };

    [Theory]
    [InlineData(ProbeCountingHostKind.Ordinary)]
    [InlineData(ProbeCountingHostKind.OrdinaryWithValueHook)]
    [InlineData(ProbeCountingHostKind.Exotic)]
    public void AnOwnPropertyReadProbesTheHostObject(ProbeCountingHostKind kind)
    {
        var expectedProbes = CostsFor(kind).OwnHit;

        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, kind);
        engine.SetValue("host", host);

        host.Reset();
        var value = engine.Evaluate("host.prop;");

        value.Should().Be("value-of-prop");

        // Ordinary: the probe that proves the name is not shadowed is the read. Value hook: the host hands the
        // value over and no descriptor is built at all. Exotic: the interpreter probes nothing and the single
        // probe is the one base.Get makes.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(ProbeCountingHostKind.Ordinary)]
    [InlineData(ProbeCountingHostKind.OrdinaryWithValueHook)]
    [InlineData(ProbeCountingHostKind.Exotic)]
    public void EachOwnPropertyReadCostsItsOwnProbes(ProbeCountingHostKind kind)
    {
        var expectedProbes = 3 * CostsFor(kind).OwnHit;

        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, kind);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop; host.other; host.prop;");

        // Nothing is cached across reads for a host receiver in any column: three reads, three times the
        // single-read cost.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(ProbeCountingHostKind.Ordinary)]
    [InlineData(ProbeCountingHostKind.OrdinaryWithValueHook)]
    [InlineData(ProbeCountingHostKind.Exotic)]
    public void AMissingOwnPropertyReadIsNoCheaperThanAHit(ProbeCountingHostKind kind)
    {
        var expectedProbes = CostsFor(kind).OwnMiss;

        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, kind);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.notThere;").Should().BeUndefined();

        // For the ordinary host a miss is not cheaper than a hit: the probe that establishes the miss cannot
        // produce a value, so the read still ends in a Get that re-probes the receiver before walking to the
        // prototype. That is the one count the derivation did not move, and the value hook is what moves it —
        // its false answers the same question the discarded probe answered, for nothing. The exotic host pays
        // one here simply because only its Get probes at all.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(ProbeCountingHostKind.Ordinary)]
    [InlineData(ProbeCountingHostKind.OrdinaryWithValueHook)]
    [InlineData(ProbeCountingHostKind.Exotic)]
    public void APrototypeReadReEstablishesTheOwnMissOnTheHost(ProbeCountingHostKind kind)
    {
        var expectedProbes = CostsFor(kind).PrototypeHit;

        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, kind);
        engine.SetValue("host", host);
        engine.Execute("Object.prototype.inherited = 'from-prototype';");

        host.Reset();
        engine.Evaluate("host.inherited;").Should().Be("from-prototype");

        // A read that resolves on the prototype still has to ask this receiver whether it has since started
        // owning the name: a projected member appearing behind the engine's back moves nothing the engine
        // watches, so a prototype answer reused on trust would keep shadowing it. The ordinary host answers
        // that with a probe on every read, warm prototype cache or not; the value hook answers it for free.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }

    [Theory]
    [InlineData(ProbeCountingHostKind.Ordinary)]
    [InlineData(ProbeCountingHostKind.OrdinaryWithValueHook)]
    [InlineData(ProbeCountingHostKind.Exotic)]
    public void AMemberCallBaseProbesOnceUnlessTheHostAnswersItself(ProbeCountingHostKind kind)
    {
        var expectedProbes = CostsFor(kind).MemberCallBase;

        var engine = new Engine();
        var host = ProbeCountingHostObject.Create(engine, kind);
        engine.SetValue("host", host);

        host.Reset();
        engine.Evaluate("host.prop.toUpperCase();").Should().Be("VALUE-OF-PROP");

        // Reading a host property as the base of a member call takes a different lane than reading it as a
        // value: it goes straight through ObjectInstance.Get, so it already cost only one probe before any of
        // this — which is the evidence that the extra probe on the value lane was removable rather than
        // intrinsic. Neither derived Get outcome changes it; the value hook does, because Get consults it.
        host.GetOwnPropertyCallCount.Should().Be(expectedProbes);
    }
}

/// <summary>
/// Which of the three host shapes a fixture is. They differ in exactly the two things the engine derives from
/// the runtime type, and in nothing else: whether <c>Get</c> is overridden, and whether
/// <c>TryGetOwnPropertyValue</c> is.
/// </summary>
public enum ProbeCountingHostKind
{
    /// <summary>Overrides <c>GetOwnProperty</c> and nothing else.</summary>
    Ordinary,

    /// <summary>Also overrides <c>TryGetOwnPropertyValue</c>, so it answers own reads with no descriptor.</summary>
    OrdinaryWithValueHook,

    /// <summary>Overrides <c>Get</c>, without changing what it answers.</summary>
    Exotic,
}

/// <summary>
/// The minimum viable host object: it projects a couple of fields on demand and counts how often the engine
/// asks. Written against the public surface only, exactly as an embedder must. The two subclasses below change
/// exactly one thing each, so the columns differ only in what the engine derives for them.
/// </summary>
internal class ProbeCountingHostObject : ObjectInstance
{
    protected ProbeCountingHostObject(Engine engine) : base(engine)
    {
    }

    internal static ProbeCountingHostObject Create(Engine engine, ProbeCountingHostKind kind) => kind switch
    {
        ProbeCountingHostKind.OrdinaryWithValueHook => new ValueAnsweringProbeCountingHostObject(engine),
        ProbeCountingHostKind.Exotic => new GetOverridingProbeCountingHostObject(engine),
        _ => new ProbeCountingHostObject(engine),
    };

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

    protected static bool TryProject(JsValue property, out JsValue value)
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

/// <summary>
/// Identical behaviour, one difference: it overrides <c>TryGetOwnPropertyValue</c>, answering the same question
/// off the same projection with no descriptor in between. Its <c>false</c> is authoritative, which holds here
/// because the same <c>TryProject</c> decides what <c>GetOwnProperty</c> would have answered.
/// </summary>
internal sealed class ValueAnsweringProbeCountingHostObject : ProbeCountingHostObject
{
    public ValueAnsweringProbeCountingHostObject(Engine engine) : base(engine)
    {
    }

    // Outside the Jint assembly a protected-internal member is inherited as protected, so that is what an
    // embedder writes here.
    protected override bool TryGetOwnPropertyValue(JsValue property, JsValue receiver, out JsValue value)
        => TryProject(property, out value);
}

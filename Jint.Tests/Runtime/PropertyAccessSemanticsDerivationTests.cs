#nullable enable

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.Runtime;

/// <summary>
/// The engine resolves an <see cref="ObjectInstance"/> subclass's <see cref="PropertyAccessSemantics"/> by
/// reflecting on whether the type overrides <c>Get</c>. Reflection is expensive enough that doing it per
/// instance would cost more than the read path it buys, so it must happen once per <b>type</b>. That is
/// invisible from the outside — a per-instance probe would produce exactly the same semantics — which is why
/// these tests reach for the internal probe counter rather than asserting behaviour.
/// </summary>
public class PropertyAccessSemanticsDerivationTests
{
    [Fact]
    public void TheTypeIsProbedOnceHoweverManyInstancesAreBuilt()
    {
        var engine = new Engine();

        for (var i = 0; i < 50; i++)
        {
            _ = new ProbedOrdinaryHost(engine);
        }

        // Deliberately not phrased as a delta around this loop: the count is per type and for the whole process,
        // so "exactly one" holds however many instances exist and whichever test built the first of them. A
        // per-instance probe would put 50 here.
        ObjectInstance.AccessSemanticsProbeCount(typeof(ProbedOrdinaryHost)).Should().Be(1);
    }

    [Fact]
    public void EachTypeIsProbedSeparately()
    {
        var engine = new Engine();

        _ = new ProbedExoticHost(engine);
        _ = new ProbedExoticHost(engine);

        // A subclass does not inherit its base type's cache entry — the answer depends on that type's own
        // virtual dispatch, so it is resolved on its own the first time one is built. ProbedDerivedExoticHost is
        // constructed nowhere else, which is what makes the "not yet" reading below meaningful.
        ObjectInstance.AccessSemanticsProbeCount(typeof(ProbedExoticHost)).Should().Be(1);
        ObjectInstance.AccessSemanticsProbeCount(typeof(ProbedDerivedExoticHost)).Should().Be(0);

        _ = new ProbedDerivedExoticHost(engine);
        _ = new ProbedDerivedExoticHost(engine);

        ObjectInstance.AccessSemanticsProbeCount(typeof(ProbedDerivedExoticHost)).Should().Be(1);
    }

    [Fact]
    public void OverridingGetIsWhatMakesAHostExotic()
    {
        // The behavioural half of the same rule: the exotic host's Get is consulted for a name that resolves on
        // the prototype, the ordinary one's read is answered from its own descriptor without any Get of its own.
        var engine = new Engine();
        engine.SetValue("ordinary", new ProbedOrdinaryHost(engine));
        engine.SetValue("exotic", new ProbedExoticHost(engine));
        engine.Execute("Object.prototype.onPrototype = 'from-prototype';");

        engine.Evaluate("ordinary.onPrototype").Should().Be("from-prototype");
        engine.Evaluate("exotic.onPrototype").Should().Be("computed:onPrototype");
    }
}

/// <summary>Overrides <c>GetOwnProperty</c> only, so the derivation resolves it to ordinary.</summary>
file class ProbedOrdinaryHost : ObjectInstance
{
    public ProbedOrdinaryHost(Engine engine) : base(engine)
    {
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
        => property.IsString() && string.Equals(property.ToString(), "own", StringComparison.Ordinal)
            ? new PropertyDescriptor("own-value", writable: true, enumerable: true, configurable: true)
            : PropertyDescriptor.Undefined;
}

/// <summary>Overrides <c>Get</c>, so the derivation resolves it to exotic.</summary>
file class ProbedExoticHost : ProbedOrdinaryHost
{
    public ProbedExoticHost(Engine engine) : base(engine)
    {
    }

    public override JsValue Get(JsValue property, JsValue receiver) => new JsString("computed:" + property);
}

/// <summary>Inherits the <c>Get</c> override rather than declaring its own — still exotic, probed on its own.</summary>
file sealed class ProbedDerivedExoticHost : ProbedExoticHost
{
    public ProbedDerivedExoticHost(Engine engine) : base(engine)
    {
    }
}

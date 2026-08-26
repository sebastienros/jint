#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A value a host registers on a shadow realm's global object has to belong to <em>that</em> realm: script
/// running inside the realm must see an ordinary object or function of the intrinsics it can reach.
/// </summary>
/// <remarks>
/// The dual of <c>CrossRealmAttributionTests</c>, which pins the same rule for what a built-in produces. A
/// wrapper built while the principal realm was running and then installed on a shadow realm's global carried
/// the principal realm's <c>Object.prototype</c>, so <c>instanceof Object</c> inside the realm answered
/// <see langword="false"/> for a value the host had just handed it — and a realm is exactly a distinct set of
/// intrinsics.
/// </remarks>
public class ShadowRealmValueRealmTests
{
    public sealed class Company
    {
        public Company(string name) => Name = name;

        public string Name { get; }

        public string Shout() => Name.ToUpperInvariant();
    }

    [Test]
    public void ATypedHostObjectBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("company", new Company("acme"));

        shadowRealm.Evaluate("company instanceof Object").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(company) === Object.prototype").Should().BeTrue();
    }

    [Test]
    public void AnUntypedHostObjectBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        object company = new Company("acme");
        shadowRealm.SetValue("company", company);

        shadowRealm.Evaluate("company instanceof Object").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(company) === Object.prototype").Should().BeTrue();
    }

    [Test]
    public void ADelegateBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("shout", new Func<string, string>(s => s.ToUpperInvariant()));

        shadowRealm.Evaluate("shout instanceof Function").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(shout) === Function.prototype").Should().BeTrue();
        shadowRealm.Evaluate("shout('acme')").Should().Be("ACME");
    }

    [Test]
    public void ATypeReferenceBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("Company", typeof(Company));

        // a type reference has a prototype object of its own, which is where the realm's chain is joined
        shadowRealm.Evaluate("Object.getPrototypeOf(Object.getPrototypeOf(Company)) === Object.prototype").Should().BeTrue();
        shadowRealm.Evaluate("new Company('acme') instanceof Object").Should().BeTrue();
    }

    [Test]
    public void AProjectedArrayBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("names", new[] { "acme", "initech" });

        shadowRealm.Evaluate("names instanceof Array").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(names) === Array.prototype").Should().BeTrue();
    }

    /// <summary>
    /// The other half of the same rule: registering on a shadow realm must not make the engine's own
    /// registrations answer to a realm the script cannot reach.
    /// </summary>
    [Test]
    public void TheEnginesOwnRegistrationsStillBelongToThePrincipalRealm()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("company", new Company("acme"));
        engine.SetValue("company", new Company("initech"));

        engine.Evaluate("company instanceof Object").Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(company) === Object.prototype").Should().BeTrue();
        engine.Evaluate("company.Name").Should().Be("initech");
        shadowRealm.Evaluate("company.Name").Should().Be("acme");
    }

    /// <summary>
    /// The negative half, stated directly: the wrapper must not answer to the principal realm's
    /// <c>Object</c>. The probe is the principal constructor itself, handed in through the one overload that
    /// installs a value untouched, so the comparison happens inside the realm against the very intrinsic the
    /// wrapper used to inherit from.
    /// </summary>
    [Test]
    public void AHostObjectIsNotAnInstanceOfThePrincipalRealmsObject()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("PrincipalObject", (JsValue) engine.Intrinsics.Object);
        shadowRealm.SetValue("company", new Company("acme"));

        shadowRealm.Evaluate("company instanceof PrincipalObject").Should().BeFalse();
        shadowRealm.Evaluate("company instanceof Object").Should().BeTrue();
    }
}

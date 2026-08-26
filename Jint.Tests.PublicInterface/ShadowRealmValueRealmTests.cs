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

    /// <summary>
    /// The three members <see cref="Jint.Runtime.Interop.ObjectWrapper"/> builds eagerly in its constructor
    /// — <c>Symbol.dispose</c>, <c>Symbol.asyncDispose</c> and <c>toJSON</c> — are functions like any other
    /// and belong to the realm the object they hang off belongs to.
    /// </summary>
    /// <remarks>
    /// They used to be built through the public <c>ClrFunction(Engine, string, …)</c> constructor, which pins
    /// the engine's <em>original</em> intrinsics. That is right for a function a host wires up against an
    /// engine (#2893, <c>HostClrFunctionRealmTests</c>) and wrong for one the engine builds for an object it
    /// is creating, because the object's own prototype comes from the running realm. The two disagreed on the
    /// same object: <c>handle.Dispose instanceof Function</c> was true while
    /// <c>handle[Symbol.dispose] instanceof Function</c> was false (#3365).
    /// </remarks>
    [Test]
    public void TheDisposeMemberOfAHostObjectBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("handle", new Handle());

        // the lazily resolved member, which reads engine.Realm when it is materialized and was always right
        shadowRealm.Evaluate("handle.Dispose instanceof Function").Should().BeTrue();

        shadowRealm.Evaluate("typeof handle[Symbol.dispose]").Should().Be("function");
        shadowRealm.Evaluate("handle[Symbol.dispose] instanceof Function").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(handle[Symbol.dispose]) === Function.prototype").Should().BeTrue();
    }

#if !NETFRAMEWORK
    [Test]
    public void TheAsyncDisposeMemberOfAHostObjectBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("handle", new AsyncHandle());

        shadowRealm.Evaluate("handle[Symbol.asyncDispose] instanceof Function").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(handle[Symbol.asyncDispose]) === Function.prototype").Should().BeTrue();
    }
#endif

    [Test]
    public void TheToJsonMemberOfAHostObjectBelongsToTheRealmItIsRegisteredOn()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("payload", new Payload());

        shadowRealm.Evaluate("payload.toJSON instanceof Function").Should().BeTrue();
        shadowRealm.Evaluate("Object.getPrototypeOf(payload.toJSON) === Function.prototype").Should().BeTrue();
        shadowRealm.Evaluate("JSON.stringify(payload)").Should().Be("\"acme\"");
    }

    /// <summary>
    /// The negative half, stated against the very intrinsic the members used to inherit from.
    /// </summary>
    [Test]
    public void AnEagerlyBuiltMemberIsNotAFunctionOfThePrincipalRealm()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        shadowRealm.SetValue("PrincipalFunction", (JsValue) engine.Intrinsics.Function);
        shadowRealm.SetValue("handle", new Handle());

        shadowRealm.Evaluate("handle[Symbol.dispose] instanceof PrincipalFunction").Should().BeFalse();
        shadowRealm.Evaluate("handle[Symbol.dispose] instanceof Function").Should().BeTrue();
    }

    /// <summary>
    /// The other half of the rule, and why the answer is "the realm the wrapper was created in" rather than a
    /// realm stored on the wrapper: an object the host registers on the engine keeps the principal realm's
    /// intrinsics, exactly as its own prototype does.
    /// </summary>
    [Test]
    public void TheEagerlyBuiltMembersOfAnEngineRegistrationStayInThePrincipalRealm()
    {
        var engine = new Engine();
        engine.Intrinsics.ShadowRealm.Construct();

        engine.SetValue("handle", new Handle());

        engine.Evaluate("handle[Symbol.dispose] instanceof Function").Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(handle[Symbol.dispose]) === Function.prototype").Should().BeTrue();
    }

    /// <summary>
    /// Disposal kept working throughout, because a call never consults the prototype — which is why the
    /// defect was invisible to everything except a feature detection or a reach for <c>call</c>/<c>bind</c>.
    /// </summary>
    [Test]
    public void ADisposableHostObjectIsStillDisposableInsideTheRealm()
    {
        var engine = new Engine();
        var shadowRealm = engine.Intrinsics.ShadowRealm.Construct();

        var handle = new Handle();
        shadowRealm.SetValue("handle", handle);

        shadowRealm.Evaluate("handle[Symbol.dispose].call(handle)");

        handle.Disposed.Should().BeTrue();
    }

    public sealed class Handle : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

#if !NETFRAMEWORK
    public sealed class AsyncHandle : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => default;
    }
#endif

    public sealed class Payload
    {
        public string toJSON() => "acme";
    }
}

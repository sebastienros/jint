using System.Reflection;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Covers what a host <see cref="ObjectInstance"/> subclass may and may not say about its own extensibility.
/// These live in the public-interface suite on purpose: the project references Jint without any internals
/// access, so what compiles here is exactly what compiles for a third-party embedder.
///
/// <para>
/// <c>ObjectInstance.Extensible</c> used to be <c>virtual</c> over an <c>internal</c> setter, so a subclass
/// could override the getter to a constant and keep a setter that no longer fed it. That is the shape Squidex
/// ships twice (<c>ContentDataObject</c>, <c>ContentFieldObject</c>: <c>public override bool Extensible =&gt;
/// true;</c>), and against the unfixed engine it made <c>Object.preventExtensions</c> a silent lie — it
/// returned the object, <c>Object.isExtensible</c> went on answering <see langword="true"/>, strict mode threw
/// nothing, <c>Object.seal</c> and <c>Object.freeze</c> became no-ops, and the object kept accepting new own
/// properties afterwards. A script cannot detect any of that.
/// </para>
///
/// <para>
/// The getter is no longer virtual, so that shape does not compile. The legitimate intent behind it — an
/// object nothing may ever make non-extensible — is expressed by overriding <see cref="ObjectInstance.PreventExtensions"/>
/// and returning <see langword="false"/>, which is an answer <c>[[PreventExtensions]]</c> is allowed to give
/// and which makes <c>Object.preventExtensions</c> raise a <c>TypeError</c> instead of lying.
/// </para>
/// </summary>
public class HostExtensibilityContractTests
{
    /// <summary>
    /// Whether Jint's host-contract verifiers are running: always in a Debug build, and in Release when
    /// <c>Jint.EnableHostContractVerification</c> was set before the first use of any Jint type — which is what
    /// this repository's Release verification leg does (<c>JINT_HOST_CONTRACT_VERIFICATION=1</c>). Static so
    /// <see cref="IgnoreUnlessAttribute" /> can read it while the test tree is built.
    /// </summary>
    public static bool Verifying => HostContractVerificationSwitch.Enabled;

    /// <inheritdoc cref="Verifying" />
    public static bool NotVerifying => !Verifying;

    /// <summary>Nothing overridden: extensibility behaves as it does on any other object.</summary>
    private sealed class OrdinaryHost : ObjectInstance
    {
        public OrdinaryHost(Engine engine) : base(engine)
        {
        }
    }

    /// <summary>
    /// The supported way to say "this object is always extensible" — the intent behind Squidex's getter
    /// override, expressed where the spec puts it.
    /// </summary>
    private sealed class PermanentlyExtensibleHost : ObjectInstance
    {
        public PermanentlyExtensibleHost(Engine engine) : base(engine)
        {
        }

        public override bool PreventExtensions() => false;
    }

    /// <summary>
    /// The one way left to break the invariant: report success without taking effect. It is what a host
    /// migrating the getter override might reach for first, which is why it has a verifier.
    /// </summary>
    private sealed class LyingHost : ObjectInstance
    {
        public LyingHost(Engine engine) : base(engine)
        {
        }

        public override bool PreventExtensions() => true;
    }

    /// <summary>
    /// The compile-time half of the fix, asserted the only way a running test can: the getter a host would
    /// have had to override is not virtual, and the setter that used to be left behind by such an override is
    /// not public. <c>public override bool Extensible =&gt; true;</c> is therefore CS0506 for an embedder.
    /// </summary>
    [Test]
    public void TheExtensibleGetterCannotBeOverriddenByAHostSubclass()
    {
        var extensible = typeof(ObjectInstance).GetProperty(nameof(ObjectInstance.Extensible), BindingFlags.Public | BindingFlags.Instance);

        extensible.Should().NotBeNull();
        extensible!.GetMethod.Should().NotBeNull();
        extensible.GetMethod!.IsVirtual.Should().BeFalse("a host that overrides the getter keeps a setter that no longer feeds it");
        extensible.SetMethod?.IsPublic.Should().NotBe(true, "extensibility is changed through PreventExtensions, not by assignment");

        // ... while the hook that expresses the same intent honestly is still there to override
        typeof(ObjectInstance).GetMethod(nameof(ObjectInstance.PreventExtensions), BindingFlags.Public | BindingFlags.Instance)!
            .IsVirtual.Should().BeTrue();
    }

    [Test]
    public void AnOrdinaryHostSubclassIsPreventedSealedAndFrozenLikeAnyOtherObject()
    {
        var engine = new Engine();
        engine.SetValue("host", new OrdinaryHost(engine));

        engine.Evaluate("Object.isExtensible(host)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.preventExtensions(host) === host").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isExtensible(host)").AsBoolean().Should().BeFalse();

        engine.Execute("host.added = 1;");
        engine.Evaluate("'added' in host").AsBoolean().Should().BeFalse();

        engine.Evaluate("Object.seal(host) === host").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isSealed(host)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.freeze(host) === host").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isFrozen(host)").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The migration target for Squidex's shape, observed from script. The object stays extensible forever,
    /// and every route that would have reported a success that did not happen now fails loudly instead.
    /// </summary>
    [Test]
    public void AHostThatRefusesToBecomeNonExtensibleIsVisiblyRefusing()
    {
        var engine = new Engine();
        engine.SetValue("host", new PermanentlyExtensibleHost(engine));

        engine.Evaluate("Object.isExtensible(host)").AsBoolean().Should().BeTrue();

        // Object.preventExtensions throws when [[PreventExtensions]] answers false — in both modes, since the
        // throw is the abstract operation's own, not a strict-mode assignment refusal
        Invoking(() => engine.Evaluate("Object.preventExtensions(host)"))
            .Should().Throw<JavaScriptException>().WithMessage("Cannot prevent extensions");
        Invoking(() => engine.Evaluate("(function () { 'use strict'; Object.preventExtensions(host); })()"))
            .Should().Throw<JavaScriptException>().WithMessage("Cannot prevent extensions");

        Invoking(() => engine.Evaluate("Object.seal(host)")).Should().Throw<JavaScriptException>();
        Invoking(() => engine.Evaluate("Object.freeze(host)")).Should().Throw<JavaScriptException>();

        // Reflect reports the same refusal as a value, which is what it exists for
        engine.Evaluate("Reflect.preventExtensions(host)").AsBoolean().Should().BeFalse();

        // ... and after all four the object is exactly what it said it was
        engine.Evaluate("Object.isExtensible(host)").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isSealed(host)").AsBoolean().Should().BeFalse();
        engine.Evaluate("Object.isFrozen(host)").AsBoolean().Should().BeFalse();
        engine.Execute("host.added = 1;");
        engine.Evaluate("host.added").AsNumber().Should().Be(1);
    }

    /// <summary>
    /// Every script-visible route into <c>[[PreventExtensions]]</c> is verified, not just
    /// <c>Object.preventExtensions</c>.
    /// </summary>
    [IgnoreUnless(nameof(Verifying), "host-contract verification is off in this run")]
    [TestCase("Object.preventExtensions(host)")]
    [TestCase("Object.seal(host)")]
    [TestCase("Object.freeze(host)")]
    [TestCase("Reflect.preventExtensions(host)")]
    public void APreventExtensionsThatReportsSuccessWithoutTakingEffectIsReported(string script)
    {
        var engine = new Engine();
        engine.SetValue("host", new LyingHost(engine));

        Invoking(() => engine.Evaluate(script))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*PreventExtensions() returned true but*Extensible is still true*");
    }

    /// <summary>
    /// The verifier is opt-in, so with it off the lie is silent — which is the whole reason the getter had to
    /// stop being overridable rather than merely being checked.
    /// </summary>
    [Test, IgnoreUnless(nameof(NotVerifying), "host-contract verification is on in this run")]
    public void WithVerificationOffTheSameLieCostsNothingAndIsNotReported()
    {
        var engine = new Engine();
        engine.SetValue("host", new LyingHost(engine));

        engine.Evaluate("Object.preventExtensions(host) === host").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.isExtensible(host)").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// The verifier looks at the type, not at the instance, so an in-box object that legitimately answers
    /// <c>true</c> from a <c>[[PreventExtensions]]</c> of its own is not swept up — a Proxy in particular,
    /// where reading extensibility runs a user <c>isExtensible</c> trap and a verifier that did so would be an
    /// observable side effect.
    /// </summary>
    [Test]
    public void AProxyIsNotSweptUpByTheVerifier()
    {
        var engine = new Engine();
        var log = engine.Evaluate("""
            const calls = [];
            const p = new Proxy({}, {
                isExtensible(t) { calls.push('isExtensible'); return Reflect.isExtensible(t); },
                preventExtensions(t) { calls.push('preventExtensions'); return Reflect.preventExtensions(t); }
            });
            Object.preventExtensions(p);
            calls.join(',');
            """).AsString();

        // the preventExtensions trap, then the isExtensible-invariant read the algorithm itself performs
        log.Should().Be("preventExtensions");
        engine.Evaluate("Object.isExtensible(p)").AsBoolean().Should().BeFalse();
    }
}

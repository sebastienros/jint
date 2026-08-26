#nullable enable

using System.Collections.Generic;
using System.Reflection;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Jint's host-contract verifiers — the checks that catch an embedder's <c>ObjectInstance</c> subclass
/// answering one extension point in a way that contradicts another — used to be compiled out of Release
/// entirely. Since Jint ships Release-only, "run your suite against a Debug Jint" meant cloning the repository
/// and building it, which is a real cost an embedder pays exactly once before deciding not to.
///
/// <para>
/// They are now gated on an <see cref="AppContext"/> switch instead, so the <b>shipped package</b> can verify:
/// </para>
///
/// <code>
/// AppContext.SetSwitch("Jint.EnableHostContractVerification", true);
/// </code>
///
/// <para>
/// The gate is a <c>static readonly bool</c>, read once at type initialization, so the JIT folds it and a
/// process that never sets the switch pays exactly what compile-time elision cost. The same property is what
/// makes the switch order-sensitive, and that is the whole contract this file pins: <b>set it before the first
/// use of any Jint type</b>.
/// </para>
///
/// <para>
/// Both halves cannot be observed in one process — the flag is read once — so the test below reads the gate out
/// of a <em>second</em> copy of the Jint assembly loaded into its own <see cref="AssemblyLoadContext"/>, which
/// gets its own statics and therefore its own first read of the switch. That is also the only reflective step
/// here: the gate itself is internal, deliberately, since an embedder sets the switch and never names the type.
/// </para>
/// </summary>
public class HostContractVerificationTests
{
    private const string SwitchName = "Jint.EnableHostContractVerification";

    private static bool ReadGate(Assembly jint)
    {
        var gate = jint.GetType("Jint.Runtime.HostContractVerification", throwOnError: true)!;
        var enabled = gate.GetField("Enabled", BindingFlags.NonPublic | BindingFlags.Static);
        enabled.Should().NotBeNull("the verification gate is the field this capability is built on");
        return (bool) enabled!.GetValue(null)!;
    }

    [Test]
    public void TheDefaultIsOnForADebugBuildAndOffForTheShippedRelease()
    {
        // Reading it here also forces this process's copy to initialize while the switch is still whatever the
        // harness left it, so the test below cannot accidentally change it for the rest of a parallel suite run.
        var enabled = ReadGate(typeof(Engine).Assembly);

#if DEBUG
        enabled.Should().BeTrue("a Debug build of Jint verifies host contracts without being asked");
#else
        if (HostContractVerificationSwitch.RequestedByTheEnvironment())
        {
            enabled.Should().BeTrue(
                "this run asked for verification through " + HostContractVerificationSwitch.EnvironmentVariableName +
                ", and the harness set the switch from a module initializer — before the first use of any Jint type");
        }
        else
        {
            enabled.Should().BeFalse("the shipped Release package must cost nothing until an embedder opts in");
        }
#endif
    }

    /// <summary>
    /// The ordering claim, proven through <b>behaviour</b> rather than through the gate's field: a host whose
    /// <c>ProbeOwnProperty</c> contradicts its own <c>GetOwnProperty</c> throws exactly when the verifiers are
    /// running. A `true` here means Jint read the switch this harness set, and read it in time — the whole
    /// contract of a <c>static readonly</c> gate.
    /// </summary>
    [Test]
    public void TheHarnessSetsTheSwitchEarlyEnoughForJintToObserveIt()
    {
        var engine = new Engine();
        engine.SetValue("host", new SelfContradictingHost(engine));

        var act = () => engine.Evaluate("Object.keys(host).join(',')");

        if (HostContractVerificationSwitch.Enabled)
        {
            act.Should().Throw<InvalidOperationException>(
                "the verifiers are on in this run, so the contradiction must be reported").WithMessage("*lied*");
        }
        else
        {
            act.Should().NotThrow("nothing is verifying in this run, so the contradiction is believed");
            engine.Evaluate("Object.keys(host).join(',')").AsString().Should().Be("honest");
        }
    }

    /// <summary>
    /// Denies through its probe one name its own <c>GetOwnProperty</c> plainly serves. The engine trusts the
    /// probe and never re-asks, so the key simply vanishes from every enumeration — silently, unless something
    /// is verifying.
    /// </summary>
    private sealed class SelfContradictingHost : ObjectInstance
    {
        public SelfContradictingHost(Engine engine) : base(engine)
        {
        }

        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            var name = property.ToString();
            return name is "honest" or "lied"
                ? new PropertyDescriptor(name, writable: true, enumerable: true, configurable: true)
                : PropertyDescriptor.Undefined;
        }

        // outside the Jint assembly a `protected internal` member is visible as `protected`
        protected override OwnPropertyProbe ProbeOwnProperty(JsValue property)
            => property.ToString() == "lied" ? OwnPropertyProbe.Missing : base.ProbeOwnProperty(property);

        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
            => [new JsString("honest"), new JsString("lied")];
    }

#if NET8_0_OR_GREATER && !DEBUG
    [Test]
    public void TheSwitchTurnsVerificationOnInAReleaseBuild()
    {
        var requested = HostContractVerificationSwitch.RequestedByTheEnvironment();

        // force this process's copy to initialize first, with the switch at whatever the harness left it
        ReadGate(typeof(Engine).Assembly).Should().Be(requested);

        AppContext.SetSwitch(SwitchName, true);
        try
        {
            var context = new AssemblyLoadContext("jint-with-host-contract-verification", isCollectible: true);
            var freshJint = context.LoadFromAssemblyPath(typeof(Engine).Assembly.Location);

            ReadGate(freshJint).Should().BeTrue(
                "a Release build that reads the switch before its first use of Jint must verify host contracts");
        }
        finally
        {
            // back to what the harness asked for, so nothing later in the process sees a switch it did not set
            AppContext.SetSwitch(SwitchName, requested);
        }
    }
#endif
}

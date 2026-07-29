#nullable enable

using System.Reflection;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

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

    [Fact]
    public void TheDefaultIsOnForADebugBuildAndOffForTheShippedRelease()
    {
        // Reading it here also forces this process's copy to initialize while the switch is still unset, so the
        // test below cannot accidentally turn verification on for the rest of a parallel suite run.
        var enabled = ReadGate(typeof(Engine).Assembly);

#if DEBUG
        enabled.Should().BeTrue("a Debug build of Jint verifies host contracts without being asked");
#else
        enabled.Should().BeFalse("the shipped Release package must cost nothing until an embedder opts in");
#endif
    }

#if NET8_0_OR_GREATER && !DEBUG
    [Fact]
    public void TheSwitchTurnsVerificationOnInAReleaseBuild()
    {
        // force this process's copy to initialize first, with the switch unset
        ReadGate(typeof(Engine).Assembly).Should().BeFalse();

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
            AppContext.SetSwitch(SwitchName, false);
        }
    }
#endif
}

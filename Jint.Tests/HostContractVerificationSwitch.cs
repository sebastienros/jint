#nullable enable

using System.Runtime.CompilerServices;

namespace Jint.Tests;

/// <summary>
/// Turns Jint's <b>host-contract verifiers</b> on for a Release run of this repository's own test suites, so the
/// exact configuration an embedder is told to use is one the harness itself exercises.
///
/// <para>
/// The verifiers are gated on <c>HostContractVerification.Enabled</c>, which is <see langword="true"/> in a Debug
/// build and, in Release, whatever the AppContext switch <c>Jint.EnableHostContractVerification</c> said at
/// <em>type initialization</em>. That last word is the whole difficulty: the flag is a <c>static readonly bool</c>
/// read once, so the switch has to be set before the first use of any Jint type — a test fixture, a class
/// constructor or an assembly-level <c>IAsyncLifetime</c> all run far too late.
/// </para>
///
/// <para>
/// A <see cref="ModuleInitializerAttribute">module initializer</see> is early enough by construction: the runtime
/// runs it before any code in this assembly executes, and Jint is only ever reached <em>from</em> this assembly's
/// code. It also needs nothing per target framework — <c>net472</c> has no
/// <c>ModuleInitializerAttribute</c> in its BCL, but the repository's global <c>PolySharp</c> reference emits one,
/// and the compiler recognizes the attribute by name rather than by identity.
/// </para>
///
/// <para>
/// The opt-in is the environment variable <c>JINT_HOST_CONTRACT_VERIFICATION</c> (<c>1</c> or <c>true</c>):
/// </para>
///
/// <code>
/// JINT_HOST_CONTRACT_VERIFICATION=1 dotnet test -c Release
/// </code>
///
/// <para>
/// It is deliberately <b>not</b> the default. The Release probe-count and no-descriptor pins in
/// <c>Jint.Tests.PublicInterface</c> are the regression net for the claim that the gate folds to zero cost when
/// nobody asked for it, and they can only mean that in a run where nobody did.
/// </para>
/// </summary>
public static class HostContractVerificationSwitch
{
    /// <summary>The AppContext switch Jint's gate reads.</summary>
    public const string SwitchName = "Jint.EnableHostContractVerification";

    /// <summary>The environment variable this harness turns into that switch.</summary>
    public const string EnvironmentVariableName = "JINT_HOST_CONTRACT_VERIFICATION";

    /// <summary>
    /// Whether the verifiers actually run in this process. Computed exactly the way Jint's own gate computes it,
    /// and from the environment rather than from the switch, so it cannot be disturbed by a test that flips the
    /// switch after Jint has already read it.
    /// </summary>
    public static readonly bool Enabled =
#if DEBUG
        true;
#else
        RequestedByTheEnvironment();
#endif

    /// <summary>
    /// Whether the run was asked to verify. Distinct from <see cref="Enabled"/> only in a Debug build, where the
    /// verifiers are on whether or not anyone asked.
    /// </summary>
    public static bool RequestedByTheEnvironment()
    {
        var requested = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return string.Equals(requested, "1", StringComparison.Ordinal)
            || string.Equals(requested, "true", StringComparison.OrdinalIgnoreCase);
    }

    [ModuleInitializer]
    internal static void SetSwitchBeforeAnyJintTypeIsTouched()
    {
        if (RequestedByTheEnvironment())
        {
            AppContext.SetSwitch(SwitchName, true);
        }
    }
}

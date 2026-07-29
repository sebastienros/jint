using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Jint.Runtime;

/// <summary>
/// The gate for Jint's <b>host-contract verifiers</b> — the checks that catch an embedder's
/// <see cref="Jint.Native.Object.ObjectInstance"/> subclass answering one of the engine's extension points in a
/// way that contradicts another. Every one of them exists because the engine <em>trusts</em> the host hook it
/// checks and cannot afford to re-verify it on the hot path, so a violation is otherwise silent: a key vanishes
/// from every enumeration, or a read resolves on the prototype for a property that exists.
/// <para>
/// <b>How to turn it on.</b> A Debug build of Jint has it on and needs nothing. The shipped <em>Release</em>
/// package — the one on NuGet — enables it through the AppContext switch
/// <c>Jint.EnableHostContractVerification</c>:
/// </para>
/// <code>
/// AppContext.SetSwitch("Jint.EnableHostContractVerification", true);
/// </code>
/// <para>
/// or, equivalently, in the host application's <c>runtimeconfig.json</c> / <c>App.config</c>. Set it
/// <b>before the first use of any Jint type</b>: the flag below is a <c>static readonly</c> read once at
/// type initialization, and flipping the switch afterwards has no effect for the rest of the process. Turn it
/// on in a test or staging host, never in production — the verifiers deliberately redo work the lanes they
/// check exist to avoid.
/// </para>
/// <para>
/// <b>Why a runtime switch rather than <c>[Conditional("DEBUG")]</c>.</b> The verifiers used to be elided at
/// compile time, which made "run your suite against a Debug Jint" the only way to reach them — and Jint ships
/// Release-only, so reaching them meant cloning the repository and building it. A <c>static readonly bool</c>
/// initialized at type-init is a JIT constant, so <c>if (HostContractVerification.Enabled)</c> folds away
/// entirely in a Release process that never set the switch: the guarded code is not merely skipped, it is not
/// emitted. The cost with the switch off is what compile-time elision cost, which is nothing. This is also why
/// the guard has to be written at the <em>call site</em>: <c>[Conditional]</c> removes the call, and a runtime
/// flag cannot.
/// </para>
/// </summary>
internal static class HostContractVerification
{
    /// <summary>
    /// The AppContext switch that enables the verifiers in a Release build.
    /// </summary>
    internal const string SwitchName = "Jint.EnableHostContractVerification";

    /// <summary>
    /// Whether the host-contract verifiers run. Read once at type initialization — never per check — so the
    /// JIT treats it as a constant and folds every <c>if (Enabled)</c> guard, including the guarded block, out
    /// of the generated code when it is <see langword="false"/>.
    /// </summary>
    internal static readonly bool Enabled =
#if DEBUG
        true;
#else
        AppContext.TryGetSwitch(SwitchName, out var enabled) && enabled;
#endif

    /// <summary>
    /// Reports a violated host contract. It throws rather than writing a diagnostic, because the verifiers now
    /// run in Release too, where <see cref="System.Diagnostics.Debug"/> is compiled out and a violation would
    /// otherwise be reported into nothing — and because the audience is a host's own test suite, which needs a
    /// failure it can see.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Fail(string message) => throw new InvalidOperationException(message);
}

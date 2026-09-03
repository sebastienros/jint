#if PUBLIC_API_BASELINES

using PublicApiGenerator;
using VerifyNUnit;

namespace Jint.Tests.Browser.Mcp;

/// <summary>
/// Snapshots Jint.Browser.Mcp's public API surface, so that every change to it arrives as a reviewable diff.
/// </summary>
/// <remarks>
/// The package publishes four things: the one extension a host composes with, the options it takes, the
/// session behind them, and the result shapes a tool answers with. Everything else — the serializer context,
/// the result envelope — is internal, and this baseline is where a promotion out of that is declared rather
/// than noticed after a release. One baseline for both assets, for the reason
/// <see cref="Jint.Tests.Browser.PublicApiTest"/> gives about <c>Jint.Browser</c>: neither package compiles
/// anything conditionally.
/// </remarks>
public class McpPublicApiTest
{
    [Test]
    public async Task PublicApiHasNotChangedUnintentionally()
    {
        var options = new ApiGeneratorOptions
        {
            ExcludeAttributes =
            [
                "System.Diagnostics.DebuggerDisplayAttribute",
                "System.Reflection.AssemblyMetadataAttribute",
                "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
                "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
                "System.Runtime.CompilerServices.IsReadOnlyAttribute",
                "System.Runtime.CompilerServices.NullableAttribute",
                "System.Runtime.CompilerServices.NullableContextAttribute",
                "System.Runtime.CompilerServices.RefSafetyRulesAttribute",
                "System.Runtime.Versioning.TargetFrameworkAttribute",
            ],
        };

        var publicApi = typeof(global::Jint.Browser.Mcp.BrowserAgent).Assembly.GeneratePublicApi(options);

        await Verifier.Verify(publicApi, extension: "txt")
            .UseDirectory("../Verify")
            .UseFileName("McpPublicApiTest")
            .DisableRequireUniquePrefix();
    }
}

#endif

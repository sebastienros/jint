#if PUBLIC_API_BASELINES

using PublicApiGenerator;
using VerifyNUnit;

namespace Jint.Tests.Browser.PlaywrightAdapter;

/// <summary>Snapshots the public entry point exposed by the Jint.Browser.Playwright package.</summary>
public sealed class PlaywrightPublicApiTest
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

        var publicApi = typeof(global::Jint.Browser.Playwright.JintPlaywright).Assembly.GeneratePublicApi(options);

        await Verifier.Verify(publicApi, extension: "txt")
            .UseDirectory("../Verify")
            .UseFileName("PlaywrightPublicApiTest")
            .DisableRequireUniquePrefix();
    }
}

#endif

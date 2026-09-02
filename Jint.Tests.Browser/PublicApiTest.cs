#if PUBLIC_API_BASELINES

using System.Runtime.CompilerServices;
using PublicApiGenerator;
using VerifyTests;
using VerifyTests.DiffPlex;
using VerifyNUnit;

namespace Jint.Tests.Browser;

/// <summary>
/// Snapshots Jint.Browser's public API surface, so that every change to it arrives as a reviewable diff.
/// </summary>
/// <remarks>
/// <para>
/// The package publishes exactly one thing: the host API a caller drives a page with — <c>Browser</c>,
/// <c>BrowserContext</c>, <c>Page</c> and what they take and answer. Everything else, the whole DOM binding
/// included, stays internal until the runtime that consumes it has settled what a host actually holds, and
/// this baseline is where a promotion out of that is declared rather than noticed after a release.
/// </para>
/// <para>
/// One baseline, not one per target framework, because the package has no conditional compilation:
/// <see cref="NoSourceFileIsGatedByTargetFramework"/> is what keeps that true rather than assumed.
/// </para>
/// </remarks>
public class PublicApiTest
{
    [Test]
    public async Task PublicApiHasNotChangedUnintentionally()
    {
        var options = new ApiGeneratorOptions
        {
            // These say how the compiler encoded something rather than what the contract is, and they churn
            // the baseline whenever an unrelated file is touched.
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

        var publicApi = typeof(global::Jint.Browser.Browser).Assembly.GeneratePublicApi(options);

        await Verifier.Verify(publicApi, extension: "txt")
            .UseDirectory("Verify")
            .UseFileName("PublicApiTest")
            .DisableRequireUniquePrefix();
    }

    /// <summary>
    /// No source file in the package is gated on a target framework, which is what makes one baseline
    /// enough for both assets.
    /// </summary>
    [Test]
    public void NoSourceFileIsGatedByTargetFramework()
    {
        var gated = new List<string>();

        foreach (var file in Directory.EnumerateFiles(RepositoryPaths.SourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(RepositoryPaths.Root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            var line = 0;
            foreach (var text in File.ReadLines(file))
            {
                line++;
                var trimmed = text.TrimStart();
                if (trimmed.StartsWith("#if", StringComparison.Ordinal) || trimmed.StartsWith("#elif", StringComparison.Ordinal))
                {
                    gated.Add($"  {relative}:{line}: {trimmed}");
                }
            }
        }

        Assert.That(
            gated.Count == 0,
            $"""
            {gated.Count} source line(s) in Jint.Browser compile conditionally, so its net8.0 and net10.0
            assets no longer necessarily have the same public surface and one baseline no longer covers both.

            {string.Join(Environment.NewLine, gated)}

            Either drop the condition, or split PublicApiTest into a baseline per target framework the way
            Jint.Tests.PublicInterface does.
            """);
    }
}

internal static class PublicApiDiffFormat
{
    /// <summary>
    /// Makes a baseline mismatch print the lines that changed rather than both files in full.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize() => VerifyDiffPlex.Initialize(OutputType.Compact);
}

#endif

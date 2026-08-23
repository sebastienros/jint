#if !NETFRAMEWORK
#nullable enable

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using PublicApiGenerator;
using VerifyTests;
using VerifyTests.DiffPlex;
using VerifyXunit;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Snapshots Jint's public API surface, once per target framework it ships, so that every change to it
/// arrives as a reviewable diff.
/// </summary>
/// <remarks>
/// This is the only guard the repository has against an unintended public API change — there is no ApiCompat
/// run and no shipped/unshipped API files. A failure here is therefore not automatically a bug: read the
/// diff, and when the change is deliberate, accept the new baseline and carry the same diff into the v5
/// migration guide. When it is not deliberate, the diff is the bug report. Never hand-edit a baseline.
/// </remarks>
/// <remarks>
/// <para>
/// The baselines are generated from the built assemblies in <c>artifacts/bin/Jint/</c>, read through a
/// <see cref="MetadataLoadContext"/>, rather than from the <c>Jint</c> this test process loaded. That is the
/// whole point: Jint ships five target frameworks whose public surfaces genuinely differ — everything under
/// <c>Jint/WebApi/</c> is behind <c>#if NET8_0_OR_GREATER</c>, and <c>SUPPORTS_HALF</c> and its siblings add
/// members downlevel targets do not have — while a test project can only ever <em>load</em> two of them
/// (<c>net10.0</c>, and <c>net472</c> resolving Jint's <c>net462</c> asset). A single newest-target-framework
/// baseline would hide exactly what a downlevel consumer needs to know, and <c>netstandard2.0</c> and
/// <c>netstandard2.1</c> — which no test project can execute at all — would never be covered.
/// </para>
/// <para>
/// It composes because <c>PublicApiGenerator</c> reads the assembly with Mono.Cecil from
/// <see cref="Assembly.Location"/> and never reflects over it, so a metadata-only <see cref="Assembly"/> is
/// all it needs. The one thing it does resolve through the reflection API is the type-forward list, which is
/// off by default and stays off here.
/// </para>
/// <para>
/// The theory rows come from <c>Jint.csproj</c>'s own <c>TargetFrameworks</c>, so adding a target framework
/// adds a row that fails until its baseline is accepted, rather than silently shipping an unsnapshotted
/// surface. The assemblies themselves are kept present and current by the
/// <c>BuildJintForEveryShippedTargetFramework</c> target in this project's <c>.csproj</c>.
/// </para>
/// </remarks>
public class PublicApiTest
{
    /// <summary>
    /// The repository root, found by walking up from the test binary — with <c>UseArtifactsOutput</c> the
    /// output always sits at <c>&lt;root&gt;/artifacts/bin/&lt;project&gt;/&lt;configuration&gt;_&lt;tfm&gt;/</c>.
    /// </summary>
    private static readonly string _repositoryRoot = FindRepositoryRoot();

    /// <summary>
    /// The artifacts pivot this test binary itself was built into, so that a Debug run reads Debug output.
    /// </summary>
    private static readonly string _configuration = FindConfiguration();

    /// <summary>
    /// What <c>Jint.csproj</c> says it ships, so that a new target framework is a new row rather than a
    /// silently unsnapshotted surface.
    /// </summary>
    private static readonly string[] _shippedTargetFrameworks = ReadShippedTargetFrameworks();

    public static TheoryData<string> ShippedTargetFrameworks() => new(_shippedTargetFrameworks);

    [Theory]
    [MemberData(nameof(ShippedTargetFrameworks))]
    public async Task PublicApiHasNotChangedUnintentionally(string targetFramework)
    {
        var assemblyPath = ShippedAssemblyPath(targetFramework);
        if (!File.Exists(assemblyPath))
        {
            Assert.Fail($"""
                Jint has no {targetFramework} build output at '{assemblyPath}'.

                The public API baselines are generated from the built assemblies, because three of the five
                target frameworks Jint ships cannot be loaded by any test project. Build them with:

                    dotnet build -c Release Jint/Jint.csproj
                """);
        }

        using var context = new MetadataLoadContext(new PathAssemblyResolver(ResolverPaths(assemblyPath)));
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        var options = new ApiGeneratorOptions
        {
            // These say how the compiler encoded something, not what the contract is, and they churn the
            // baseline whenever an unrelated file is touched. InternalsVisibleTo earns its place twice over
            // here: Jint grants it four times, and each grant carries a 320-character strong-name public key.
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

        var publicApi = assembly.GeneratePublicApi(options);

        await Verifier.Verify(publicApi, extension: "txt")
            .UseDirectory("Verify")
            .UseFileName($"PublicApiTest_{targetFramework}")
            .DisableRequireUniquePrefix();
    }

    /// <summary>
    /// Proves the baselines above describe the build this test run was produced by, rather than whatever
    /// happened to be left in <c>artifacts/</c> from an earlier one.
    /// </summary>
    /// <remarks>
    /// The <c>Jint.dll</c> beside this test binary is a byte copy of one of the five, so a matching module
    /// version id is exact — a deterministic compilation derives it from the content — and can only fail
    /// when the artifacts tree and this test binary came from different builds.
    /// </remarks>
    [Fact]
    public void TheSnapshottedAssembliesAreTheOnesThisTestRunWasBuiltFrom()
    {
        var loaded = typeof(Engine).Assembly.ManifestModule.ModuleVersionId;

        var found = new List<string>();
        foreach (var targetFramework in _shippedTargetFrameworks)
        {
            var assemblyPath = ShippedAssemblyPath(targetFramework);
            if (!File.Exists(assemblyPath))
            {
                continue;
            }

            using var context = new MetadataLoadContext(new PathAssemblyResolver(ResolverPaths(assemblyPath)));
            var mvid = context.LoadFromAssemblyPath(assemblyPath).ManifestModule.ModuleVersionId;
            found.Add($"  {targetFramework}: {mvid}");
            if (mvid == loaded)
            {
                return;
            }
        }

        Assert.Fail($"""
            None of the assemblies the public API baselines are generated from is the Jint this test process
            loaded ({loaded}), so the baselines would be verified against a stale artifacts tree.

            Rebuild with:

                dotnet build -c Release Jint/Jint.csproj

            What is in '{Path.Combine(_repositoryRoot, "artifacts", "bin", "Jint")}':
            {(found.Count == 0 ? "  (nothing)" : string.Join(Environment.NewLine, found))}
            """);
    }

    private static string ShippedAssemblyPath(string targetFramework)
        => Path.Combine(_repositoryRoot, "artifacts", "bin", "Jint", $"{_configuration}_{targetFramework}", "Jint.dll");

    /// <summary>
    /// Cecil resolves the assembly's own references while it walks it, so the directory beside this test
    /// binary (which carries Acornima) and the running runtime's directory both have to be reachable.
    /// </summary>
    private static List<string> ResolverPaths(string assemblyPath)
    {
        var paths = new List<string> { assemblyPath };
        paths.AddRange(Directory.GetFiles(AppContext.BaseDirectory, "*.dll"));

        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (!string.IsNullOrEmpty(runtimeDirectory))
        {
            paths.AddRange(Directory.GetFiles(runtimeDirectory, "*.dll"));
        }

        return paths;
    }

    private static string[] ReadShippedTargetFrameworks()
    {
        var project = Path.Combine(_repositoryRoot, "Jint", "Jint.csproj");
        var declaration = XDocument.Load(project).Descendants("TargetFrameworks").FirstOrDefault()
            ?? throw new InvalidOperationException($"'{project}' declares no <TargetFrameworks>, so the public API baselines cannot know what Jint ships.");

        return declaration.Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(targetFramework => targetFramework.Trim())
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Jint", "Jint.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"No directory containing 'Jint/Jint.csproj' above '{AppContext.BaseDirectory}'. The public API baselines are generated from the repository's own build output and cannot be produced from a detached copy of this assembly.");
    }

    private static string FindConfiguration()
    {
        // artifacts/bin/<project>/<configuration>_<tfm> - the layout Directory.Build.props opts into with
        // UseArtifactsOutput. Anything else (a plain bin/<configuration>/<tfm> layout, say) means the pivot
        // is not in the directory name, and Release is what this repository builds.
        var leaf = new DirectoryInfo(AppContext.BaseDirectory).Name;
        var separator = leaf.IndexOf('_');
        return separator > 0 ? leaf.Substring(0, separator) : "release";
    }
}

internal static class PublicApiDiffFormat
{
    /// <summary>
    /// Makes a baseline mismatch print the lines that changed. Without it Verify falls back to printing both
    /// files in full, and a baseline here is 2,800 lines of API — a diff nobody reads is a baseline nobody
    /// reviews, which is the one thing this suite cannot afford.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize() => VerifyDiffPlex.Initialize(OutputType.Compact);
}
#endif

namespace Jint.Tests.Browser;

/// <summary>
/// Where the files this suite checks live, found from the test binary rather than from a working directory.
/// </summary>
/// <remarks>
/// These tests check files in the repository - the build configuration, contract and generated
/// code — so they are checks on a checkout rather than on the loaded assembly, and they say so when run from
/// a detached copy instead of failing obscurely.
/// </remarks>
internal static class RepositoryPaths
{
    /// <summary>The repository root.</summary>
    internal static string Root { get; } = FindRoot();

    /// <summary>The package's own sources, which the public-API baseline scans for conditional compilation.</summary>
    internal static string SourceDirectory => Path.Combine(Root, "Jint.Browser");

    /// <summary>The generator and its contract.</summary>
    internal static string GeneratorDirectory => Path.Combine(Root, "tools", "dom-bindings");

    /// <summary>The curated half of the binding.</summary>
    internal static string OverridesPath => Path.Combine(GeneratorDirectory, "overrides.json");

    /// <summary>The explicit interface and member contract consumed by the emitter.</summary>
    internal static string ContractPath => Path.Combine(GeneratorDirectory, "contract.json");

    /// <summary>The checked-in generated code.</summary>
    internal static string GeneratedDirectory => Path.Combine(Root, "Jint.Browser", "Dom", "Generated");

    /// <summary>Line endings as the emitter writes them, so a Windows checkout compares equal.</summary>
    internal static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, ".claude", "rules")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No repository root with an 'AGENTS.md' and a '.claude/rules' directory above '{AppContext.BaseDirectory}'. Build configuration, the contract and generated code cannot be checked from a detached copy of this assembly.");
    }
}

namespace Jint.Tests.Browser;

/// <summary>
/// Where the files this suite checks live, found from the test binary rather than from a working directory.
/// </summary>
/// <remarks>
/// Three of these tests are about files in the repository — the pin, the override table and the generated
/// code — so they are checks on a checkout rather than on the loaded assembly, and they say so when run from
/// a detached copy instead of failing obscurely.
/// </remarks>
internal static class RepositoryPaths
{
    /// <summary>The repository root.</summary>
    internal static string Root { get; } = FindRoot();

    /// <summary>The generator, its pin and its override table.</summary>
    internal static string GeneratorDirectory => Path.Combine(Root, "tools", "dom-bindings");

    /// <summary>The pinned AngleSharp versions the checked-in code was generated from.</summary>
    internal static string PinPath => Path.Combine(GeneratorDirectory, "pin.json");

    /// <summary>The curated half of the binding.</summary>
    internal static string OverridesPath => Path.Combine(GeneratorDirectory, "overrides.json");

    /// <summary>The checked-in generated code.</summary>
    internal static string GeneratedDirectory => Path.Combine(Root, "Jint.Browser", "Dom", "Generated");

    /// <summary>The central package versions, which are also the generator's pin.</summary>
    internal static string PackagesPath => Path.Combine(Root, "Directory.Packages.props");

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
            $"No repository root with an 'AGENTS.md' and a '.claude/rules' directory above '{AppContext.BaseDirectory}'. The pin, the override table and the generated code are checked in the repository they live in and cannot be checked from a detached copy of this assembly.");
    }
}

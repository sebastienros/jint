using System.Collections.Frozen;

namespace Jint.Tests.Browser.Fixtures;

/// <summary>
/// The obstacle course's files, as embedded resources keyed by their path under <c>Fixtures/</c>
/// (<c>todomvc-react/index.html</c>, <c>vendor/react-18.3.1/react.production.min.js</c>, …).
/// </summary>
/// <remarks>
/// <para>
/// Embedded rather than read from the checkout, for the reason <c>WptCorpus</c> gives: a suite that reads a
/// working directory is a suite that behaves differently under <c>dotnet test</c>, under an IDE and under a
/// single-file publish. The logical name carries the path, so what a fixture's <c>&lt;script src&gt;</c> asks
/// for is the key the corpus is stored under.
/// </para>
/// <para>
/// Everything here is text. The vendored bundles are the libraries' own published files, byte for byte, and
/// <c>Fixtures/README.md</c> is the inventory: where each came from, at which version, under which licence,
/// and what the fixture that loads it exercises.
/// </para>
/// </remarks>
internal static class FixtureCorpus
{
    private const string ResourcePrefix = "browser-fixtures/";

    private static readonly FrozenDictionary<string, string> _resourceNames = BuildResourceIndex();

    /// <summary>Every file in the corpus, as a path under <c>Fixtures/</c>.</summary>
    internal static IReadOnlyCollection<string> Files => _resourceNames.Keys;

    /// <summary>
    /// The fixture directories — every top-level directory of the corpus except <c>vendor/</c>, which holds
    /// libraries rather than fixtures.
    /// </summary>
    internal static IReadOnlyList<string> FixtureNames { get; } = _resourceNames.Keys
        .Where(static path => path.Contains('/', StringComparison.Ordinal))
        .Select(static path => path[..path.IndexOf('/', StringComparison.Ordinal)])
        .Where(static name => !string.Equals(name, "vendor", StringComparison.Ordinal))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static name => name, StringComparer.Ordinal)
        .ToArray();

    /// <summary>The vendored libraries, as the directory names under <c>vendor/</c>.</summary>
    internal static IReadOnlyList<string> VendorNames { get; } = _resourceNames.Keys
        .Where(static path => path.StartsWith("vendor/", StringComparison.Ordinal))
        .Select(static path => path.Split('/'))
        .Where(static segments => segments.Length > 2)
        .Select(static segments => segments[1])
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static name => name, StringComparer.Ordinal)
        .ToArray();

    /// <summary>Whether the corpus holds a file at this path.</summary>
    internal static bool Contains(string path) => _resourceNames.ContainsKey(path);

    /// <summary>Reads one file, failing loudly rather than serving an empty body.</summary>
    internal static string Read(string path)
    {
        if (!TryRead(path, out var content))
        {
            throw new FileNotFoundException($"The obstacle course has no '{path}'. It holds {_resourceNames.Count} files.");
        }

        return content;
    }

    /// <summary>Reads one file, answering whether the corpus holds it.</summary>
    internal static bool TryRead(string path, out string content)
    {
        if (!_resourceNames.TryGetValue(path, out var resourceName))
        {
            content = "";
            return false;
        }

        using var stream = typeof(FixtureCorpus).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"'{resourceName}' is indexed but the assembly does not carry it.");

        using var reader = new StreamReader(stream);
        content = reader.ReadToEnd();
        return true;
    }

    private static FrozenDictionary<string, string> BuildResourceIndex()
    {
        var assembly = typeof(FixtureCorpus).Assembly;
        var index = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // MSBuild builds the logical name from %(RecursiveDir), which carries the host's separator.
            index[name.Substring(ResourcePrefix.Length).Replace('\\', '/')] = name;
        }

        return index.ToFrozenDictionary(StringComparer.Ordinal);
    }
}

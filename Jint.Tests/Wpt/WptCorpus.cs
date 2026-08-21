#if NET8_0_OR_GREATER
#nullable enable

using System.Collections.Frozen;
using System.Reflection;
using System.Text;

namespace Jint.Tests.Wpt;

/// <summary>
/// The vendored web-platform-tests tree, as embedded resources keyed by their path inside that tree
/// (<c>url/resources/urltestdata.json</c>, <c>common/subset-tests.js</c>, …).
/// </summary>
/// <remarks>
/// <para>
/// Everything under <c>Jint.Tests/Wpt/Vendor</c> is embedded with an explicit <c>LogicalName</c> of
/// <c>wpt/&lt;path&gt;</c> — see <c>Jint.Tests.csproj</c> — so the key a script asks for is the key the
/// corpus is stored under, and no probing by file-name suffix is needed. Provenance, the pinned upstream
/// commit and the list of files deliberately not vendored are in <c>Vendor/README.md</c>.
/// </para>
/// </remarks>
internal static class WptCorpus
{
    private const string ResourcePrefix = "wpt/";
    private const string PreludeResourceName = "wpt-prelude/testharness-shim.js";

    private static readonly FrozenDictionary<string, string> _resourceNames = BuildResourceIndex();

    /// <summary>
    /// The harness shim, which is Jint's own file rather than a vendored one — see its header for what it
    /// implements and where it deliberately differs from upstream's <c>testharness.js</c>.
    /// </summary>
    internal static string Prelude { get; } = ReadResource(PreludeResourceName);

    /// <summary>
    /// Every vendored path, normalised to forward slashes: <c>url/url-constructor.any.js</c>,
    /// <c>encoding/resources/encodings.js</c>, and so on.
    /// </summary>
    internal static IEnumerable<string> Paths => _resourceNames.Keys;

    /// <summary>
    /// The <c>.any.js</c> files of one suite directory, ordered by path so the theory's cases are stable.
    /// </summary>
    internal static IReadOnlyList<string> TestFiles(string suite)
    {
        var prefix = suite + "/";
        var files = new List<string>();
        foreach (var path in _resourceNames.Keys)
        {
            // Only the suite's own directory: a resources/ sub-directory holds corpora, never tests.
            if (path.StartsWith(prefix, StringComparison.Ordinal)
                && path.EndsWith(".any.js", StringComparison.Ordinal)
                && path.IndexOf('/', prefix.Length) < 0)
            {
                files.Add(path);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// Whether the corpus holds <paramref name="path"/>.
    /// </summary>
    internal static bool Contains(string path) => _resourceNames.ContainsKey(path);

    /// <summary>
    /// Reads a vendored file. The path is the one in the wpt tree, with forward slashes.
    /// </summary>
    internal static string Read(string path)
    {
        if (!_resourceNames.TryGetValue(path, out var resourceName))
        {
            throw new FileNotFoundException($"The vendored web-platform-tests corpus has no \"{path}\".", path);
        }

        return ReadResource(resourceName);
    }

    /// <summary>
    /// Resolves a reference made from inside <paramref name="fromDirectory"/> — a <c>// META: script=</c>
    /// line or a <c>fetch()</c> of a corpus file — to a path in the tree.
    /// </summary>
    /// <remarks>
    /// A leading slash means the wpt root, matching how wptserve resolves <c>/common/subset-tests.js</c>;
    /// anything else is relative to the file that named it. A query or fragment is dropped, because a
    /// variant's <c>?1-1000</c> selects a shard rather than a different file. The walk refuses to leave the
    /// vendored tree, so a corpus that grew a <c>../</c> escape is a failure here rather than a read of
    /// whatever happened to be next to it.
    /// </remarks>
    internal static string ResolveReference(string fromDirectory, string reference)
    {
        var cut = reference.AsSpan().IndexOfAny('?', '#');
        var trimmed = cut < 0 ? reference : reference.Substring(0, cut);

        var rooted = trimmed.StartsWith('/');
        var segments = new List<string>();
        if (!rooted && fromDirectory.Length > 0)
        {
            segments.AddRange(fromDirectory.Split('/'));
        }

        foreach (var segment in trimmed.TrimStart('/').Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"\"{reference}\" from \"{fromDirectory}\" points outside the vendored web-platform-tests tree.");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join("/", segments);
    }

    /// <summary>
    /// The directory part of a path in the tree, or the empty string for a file at its root.
    /// </summary>
    internal static string DirectoryOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path.Substring(0, slash);
    }

    private static FrozenDictionary<string, string> BuildResourceIndex()
    {
        var assembly = typeof(WptCorpus).Assembly;
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

    private static string ReadResource(string resourceName)
    {
        var assembly = typeof(WptCorpus).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource \"{resourceName}\" is missing.", resourceName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
#endif

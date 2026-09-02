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
    private const string HarnessReportResourceName = "wpt-prelude/testharnessreport.js";

    private static readonly FrozenDictionary<string, string> _resourceNames = BuildResourceIndex();

    /// <summary>
    /// The harness shim, which is Jint's own file rather than a vendored one — see its header for what it
    /// implements and where it deliberately differs from upstream's <c>testharness.js</c>.
    /// </summary>
    internal static string Prelude { get; } = ReadResource(PreludeResourceName);

    /// <summary>
    /// The <c>resources/testharnessreport.js</c> the server answers with, which is Jint's own file rather
    /// than a vendored one: it is the slot the browser lane overlays to get results out of a page.
    /// </summary>
    internal static string HarnessReport { get; } = ReadResource(HarnessReportResourceName);

    /// <summary>
    /// The two roots of the wpt tree that hold helpers rather than tests: <c>resources/</c>, which is the
    /// harness itself, and <c>common/</c>, which is what every standard's suites share.
    /// </summary>
    /// <remarks>
    /// Neither is ever a suite. A directory becomes a suite by being named in one of
    /// <c>WptTestRunner</c>'s suite arrays and reached by a <c>[TestCaseSource]</c>, and these two hold no
    /// <c>.any.js</c> at all — but "holds none today" is a property of the corpus rather than a rule, and a
    /// re-vendor that brought one in would otherwise turn the harness's own source into a test case with no
    /// subject. So <see cref="TestFiles"/> refuses to be asked about them and
    /// <c>WptTestRunner.EveryVendoredFileIsAccountedFor</c> fails if one ever holds a <c>.any.js</c>.
    /// </remarks>
    internal static readonly string[] SharedDirectories = ["common", "resources"];

    /// <summary>
    /// The file extensions that make a vendored file a <b>browser lane</b> test — a document a page is
    /// navigated to, rather than a script an engine is handed.
    /// </summary>
    /// <remarks>
    /// <c>.xht</c> is upstream's older spelling of <c>.xhtml</c> and is here for the same reason
    /// <c>Jint.Tests.csproj</c> embeds it: nothing at this pin uses it, and a corpus bump that brought one in
    /// should find it accounted for rather than invisible.
    /// </remarks>
    internal static readonly string[] BrowserTestExtensions = [".html", ".htm", ".xhtml", ".xht"];

    /// <summary>
    /// The directories whose documents the browser lane runs, as paths in the wpt tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It lives here rather than in <c>Jint.Tests.Browser</c> because <i>this</i> project's inventory check is
    /// what needs it: a <c>.html</c> file vendored under a directory no lane claims would be embedded, byte-
    /// verified and never run, which is the same silence the minimum-test table exists to break for
    /// <c>.any.js</c>. <c>WptTestRunner.EveryVendoredFileIsAccountedFor</c> holds every vendored document to
    /// this list, and <c>Jint.Tests.Browser</c>'s <c>WptBrowserTestRunner</c> holds the list to its own
    /// theories from the other side — so a directory named here with no test-case source reaching it fails
    /// there.
    /// </para>
    /// <para>
    /// The rule for what may be added is the <c>.any.js</c> rule: a suite is one directory, because
    /// <see cref="BrowserTestFiles"/> lists a directory's own files and never descends.
    /// </para>
    /// </remarks>
    internal static readonly string[] BrowserSuites =
    [
        "dom/events",
        "html/webappapis/scripting/events",
        "html/webappapis/scripting/processing-model-2",
    ];

    /// <summary>
    /// Every vendored path, normalised to forward slashes: <c>url/url-constructor.any.js</c>,
    /// <c>encoding/resources/encodings.js</c>, and so on.
    /// </summary>
    internal static IEnumerable<string> Paths => _resourceNames.Keys;

    /// <summary>
    /// Whether <paramref name="path"/> lives under one of the <see cref="SharedDirectories"/>.
    /// </summary>
    internal static bool IsShared(string path)
    {
        foreach (var shared in SharedDirectories)
        {
            if (path.Length > shared.Length
                && path.StartsWith(shared, StringComparison.Ordinal)
                && path[shared.Length] == '/')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The <c>.any.js</c> files of one suite directory, ordered by path so the theory's cases are stable.
    /// </summary>
    internal static IReadOnlyList<string> TestFiles(string suite)
    {
        foreach (var shared in SharedDirectories)
        {
            if (string.Equals(suite, shared, StringComparison.Ordinal)
                || suite.StartsWith(shared + "/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"\"{suite}\" is a shared helper directory of the wpt tree, not a suite — see WptCorpus.SharedDirectories.");
            }
        }

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
    /// The browser lane's test documents in one suite directory, ordered by path so the theory's cases are
    /// stable.
    /// </summary>
    /// <remarks>
    /// The same rule as <see cref="TestFiles"/>, for the same reason: a directory's own files and never its
    /// sub-directories, so a suite is a directory and a file belongs to exactly one. A <c>resources/</c> or
    /// <c>support/</c> child therefore holds the helper documents a test frames or navigates to and never a
    /// case of its own.
    /// </remarks>
    internal static IReadOnlyList<string> BrowserTestFiles(string suite)
    {
        var prefix = suite + "/";
        var files = new List<string>();
        foreach (var path in _resourceNames.Keys)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal)
                && IsBrowserTestFile(path)
                && path.IndexOf('/', prefix.Length) < 0)
            {
                files.Add(path);
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// Whether <paramref name="path"/> names a document rather than a script — see
    /// <see cref="BrowserTestExtensions"/>.
    /// </summary>
    internal static bool IsBrowserTestFile(string path)
    {
        foreach (var extension in BrowserTestExtensions)
        {
            if (path.EndsWith(extension, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="path"/> lives under one of the <see cref="BrowserSuites"/>, at any depth — so
    /// a suite's own <c>resources/</c> and <c>support/</c> helpers count as claimed too.
    /// </summary>
    internal static bool IsUnderABrowserSuite(string path)
    {
        foreach (var suite in BrowserSuites)
        {
            if (path.Length > suite.Length
                && path.StartsWith(suite, StringComparison.Ordinal)
                && path[suite.Length] == '/')
            {
                return true;
            }
        }

        return false;
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
    /// Reads a vendored file, or answers <see langword="null"/> when the corpus does not hold it — which is
    /// what a <c>.headers</c> sidecar lookup needs, since most files have none.
    /// </summary>
    internal static string? TryRead(string path)
        => _resourceNames.TryGetValue(path, out var resourceName) ? ReadResource(resourceName) : null;

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

using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;

namespace Jint.CldrDates.Generator;

internal sealed record Pin(string CldrVersion, string CldrReleaseTag, string CldrJsonVersion, PinnedPackage[] Packages, PinnedFile SupplementalData)
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static Pin Load(string path)
    {
        return JsonSerializer.Deserialize<Pin>(File.ReadAllText(path), _options)
               ?? throw new InvalidDataException(path + " is empty");
    }

    internal PinnedPackage Package(string name)
    {
        return Array.Find(Packages, p => string.Equals(p.Name, name, StringComparison.Ordinal))
               ?? throw new InvalidDataException("pin.json names no package " + name);
    }
}

internal sealed record PinnedPackage(string Name, string Tarball, string Integrity)
{
    internal string FileName => Tarball[(Tarball.LastIndexOf('/') + 1)..];
}

internal sealed record PinnedFile(string Path, string Url, string Sha256)
{
    internal string FileName => Path[(Path.LastIndexOf('/') + 1)..];
}

/// <summary>
/// The pinned inputs, fetched (or read from the cache directory) and checked against the hashes in <c>pin.json</c>
/// before a byte of them is read.
/// </summary>
internal sealed class Inputs
{
    private Inputs(Dictionary<string, byte[]> datesFiles, Dictionary<string, byte[]> coreFiles, XDocument supplementalData)
    {
        DatesFiles = datesFiles;
        CoreFiles = coreFiles;
        SupplementalData = supplementalData;
    }

    /// <summary>
    /// <c>package/main/&lt;locale&gt;/ca-gregorian.json</c> and <c>dateFields.json</c> from cldr-dates-full.
    /// </summary>
    internal Dictionary<string, byte[]> DatesFiles { get; }

    /// <summary>
    /// <c>package/availableLocales.json</c>, <c>package/defaultContent.json</c> and <c>package/supplemental/*.json</c> from cldr-core.
    /// </summary>
    internal Dictionary<string, byte[]> CoreFiles { get; }

    internal XDocument SupplementalData { get; }

    internal static async Task<Inputs> LoadAsync(Pin pin, string cacheDirectory)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("jint-cldr-dates-generator");

        var dates = pin.Package("cldr-dates-full");
        var core = pin.Package("cldr-core");

        var datesTarball = await FetchAsync(http, dates.Tarball, Path.Combine(cacheDirectory, dates.FileName), bytes => Integrity(bytes) == dates.Integrity, "npm integrity " + dates.Integrity).ConfigureAwait(false);
        var coreTarball = await FetchAsync(http, core.Tarball, Path.Combine(cacheDirectory, core.FileName), bytes => Integrity(bytes) == core.Integrity, "npm integrity " + core.Integrity).ConfigureAwait(false);
        var supplemental = pin.SupplementalData;
        var xml = await FetchAsync(http, supplemental.Url, Path.Combine(cacheDirectory, supplemental.FileName), bytes => Sha256(bytes) == supplemental.Sha256, "SHA-256 " + supplemental.Sha256).ConfigureAwait(false);

        var datesFiles = ReadTarball(datesTarball, static name =>
            name.StartsWith("package/main/", StringComparison.Ordinal)
            && (name.EndsWith("/ca-gregorian.json", StringComparison.Ordinal) || name.EndsWith("/dateFields.json", StringComparison.Ordinal)));
        var coreFiles = ReadTarball(coreTarball, static name =>
            string.Equals(name, "package/availableLocales.json", StringComparison.Ordinal)
            || string.Equals(name, "package/defaultContent.json", StringComparison.Ordinal)
            || name.StartsWith("package/supplemental/", StringComparison.Ordinal));

        using var xmlStream = new MemoryStream(xml);
        return new Inputs(datesFiles, coreFiles, XDocument.Load(xmlStream));
    }

    internal static string Integrity(byte[] bytes) => "sha512-" + Convert.ToBase64String(SHA512.HashData(bytes));

    internal static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal JsonDocument CoreJson(string name)
    {
        if (!CoreFiles.TryGetValue("package/" + name, out var bytes))
        {
            throw new InvalidDataException("cldr-core has no " + name);
        }

        return JsonDocument.Parse(bytes);
    }

    private static async Task<byte[]> FetchAsync(HttpClient http, string url, string cachePath, Func<byte[], bool> verify, string expected)
    {
        if (File.Exists(cachePath))
        {
            var cached = await File.ReadAllBytesAsync(cachePath).ConfigureAwait(false);
            if (verify(cached))
            {
                Console.WriteLine($"  {Path.GetFileName(cachePath)}: cached copy matches {expected}");
                return cached;
            }

            Console.WriteLine($"  {Path.GetFileName(cachePath)}: cached copy does not match {expected}; downloading it again");
        }

        var bytes = await http.GetByteArrayAsync(new Uri(url)).ConfigureAwait(false);
        if (!verify(bytes))
        {
            throw new InvalidDataException($"{url} does not match {expected}. Do not update the pin to whatever was downloaded: find out why it changed.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllBytesAsync(cachePath, bytes).ConfigureAwait(false);
        Console.WriteLine($"  {Path.GetFileName(cachePath)}: downloaded, matches {expected}");
        return bytes;
    }

    private static Dictionary<string, byte[]> ReadTarball(byte[] tarball, Func<string, bool> wanted)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var gzip = new GZipStream(new MemoryStream(tarball), CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        while (reader.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || !wanted(entry.Name) || entry.DataStream is null)
            {
                continue;
            }

            using var buffer = new MemoryStream();
            entry.DataStream.CopyTo(buffer);
            files[entry.Name] = buffer.ToArray();
        }

        return files;
    }
}

/// <summary>
/// CLDR's inheritance between the locales cldr-json carries: https://www.unicode.org/reports/tr35/#Parent_Locales.
/// </summary>
/// <remarks>
/// The explicit table is read from <c>supplementalData.xml</c>'s <c>&lt;parentLocales&gt;</c> element (the one with
/// no <c>component</c>, which is the one that governs date and time data) and must agree with cldr-core's
/// <c>parentLocales.json</c>. Its <c>root</c> entry carries <c>localeRules="nonlikelyScript"</c>: a
/// language-and-script locale whose script is not the language's likely one inherits from the root, listed or not.
/// Otherwise the parent is the locale with its last subtag removed, and a language's parent is the root. The root
/// is <c>und</c>, the name cldr-json gives it.
/// <para>
/// cldr-json leaves out the default-content locales (<c>ca-ES</c>, <c>en-US</c>, <c>de-DE</c>), which CLDR keeps as
/// empty locales that inherit everything, so a parent it does not carry is skipped for that parent's own parent —
/// <c>ca-ES-valencia</c> inherits from <c>ca</c> — provided <c>defaultContent.json</c> lists it.
/// </para>
/// </remarks>
internal sealed class ParentLocales
{
    private readonly Dictionary<string, string> _explicit;
    private readonly Dictionary<string, string> _likelyScripts;
    private readonly Dictionary<string, string?> _effective = new(StringComparer.Ordinal);

    private ParentLocales(Dictionary<string, string> explicitParents, Dictionary<string, string> likelyScripts)
    {
        _explicit = explicitParents;
        _likelyScripts = likelyScripts;
    }

    internal const string Root = "und";

    internal static ParentLocales Read(Inputs inputs, IReadOnlyList<string> locales)
    {
        var fromXml = new Dictionary<string, string>(StringComparer.Ordinal);
        var element = inputs.SupplementalData.Root!.Element("parentLocales")
                      ?? throw new InvalidDataException("supplementalData.xml has no <parentLocales>");
        if (element.Attribute("component") is not null)
        {
            throw new InvalidDataException("the first <parentLocales> in supplementalData.xml is component-specific; expected the general one first");
        }

        var sawRule = false;
        foreach (var entry in element.Elements("parentLocale"))
        {
            var parent = Normalize(entry.Attribute("parent")!.Value);
            if (entry.Attribute("localeRules") is { } rules)
            {
                if (!string.Equals(rules.Value, "nonlikelyScript", StringComparison.Ordinal) || !string.Equals(parent, Root, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"unknown parentLocale rule {rules.Value} -> {parent}; the generator only knows nonlikelyScript -> root");
                }

                sawRule = true;
            }

            foreach (var locale in entry.Attribute("locales")!.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                fromXml.Add(Normalize(locale), parent);
            }
        }

        if (!sawRule)
        {
            throw new InvalidDataException("supplementalData.xml no longer states the nonlikelyScript rule; revisit ParentLocales before regenerating");
        }

        using (var json = inputs.CoreJson("supplemental/parentLocales.json"))
        {
            var node = json.RootElement.GetProperty("supplemental").GetProperty("parentLocales");
            var fromJson = node.GetProperty("parentLocale").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
            if (fromJson.Count != fromXml.Count || fromJson.Any(p => !fromXml.TryGetValue(p.Key, out var parent) || !string.Equals(parent, p.Value, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("cldr-core's parentLocales.json and supplementalData.xml's <parentLocales> disagree");
            }

            var rule = node.GetProperty("_localeRules").GetProperty("parentLocale").GetProperty("nonlikelyScript").GetString();
            if (!string.Equals(rule, "root", StringComparison.Ordinal))
            {
                throw new InvalidDataException("cldr-core's parentLocales.json states nonlikelyScript -> " + rule);
            }
        }

        var likelyScripts = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var json = inputs.CoreJson("supplemental/likelySubtags.json"))
        {
            foreach (var entry in json.RootElement.GetProperty("supplemental").GetProperty("likelySubtags").EnumerateObject())
            {
                if (entry.Name.Contains('-', StringComparison.Ordinal))
                {
                    continue;
                }

                var subtags = entry.Value.GetString()!.Split('-');
                if (subtags.Length == 3)
                {
                    likelyScripts[entry.Name] = subtags[1];
                }
            }
        }

        HashSet<string> defaultContent;
        using (var json = inputs.CoreJson("defaultContent.json"))
        {
            defaultContent = json.RootElement.GetProperty("defaultContent").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        }

        var parents = new ParentLocales(fromXml, likelyScripts);
        var present = locales.ToHashSet(StringComparer.Ordinal);
        if (!present.Contains(Root))
        {
            throw new InvalidDataException("cldr-json no longer carries the root as " + Root);
        }

        foreach (var locale in locales)
        {
            var parent = parents.CldrParentOf(locale);
            while (parent is not null && !present.Contains(parent))
            {
                if (!defaultContent.Contains(parent))
                {
                    throw new InvalidDataException($"{locale}: its parent {parent} is neither a locale cldr-json carries nor a default-content locale");
                }

                parent = parents.CldrParentOf(parent);
            }

            parents._effective.Add(locale, parent);
        }

        return parents;
    }

    /// <summary>
    /// The locale <paramref name="locale"/>, which must be one cldr-json carries, inherits from: the nearest ancestor
    /// cldr-json carries, or <see langword="null"/> for the root.
    /// </summary>
    internal string? ParentOf(string locale) => _effective[locale];

    private string? CldrParentOf(string locale)
    {
        if (string.Equals(locale, Root, StringComparison.Ordinal))
        {
            return null;
        }

        if (_explicit.TryGetValue(locale, out var parent))
        {
            return parent;
        }

        var subtags = locale.Split('-');
        if (subtags.Length == 2 && subtags[1].Length == 4 && char.IsAsciiLetter(subtags[1][0]))
        {
            if (!_likelyScripts.TryGetValue(subtags[0], out var likelyScript))
            {
                throw new InvalidDataException($"{locale}: likelySubtags.json has no likely script for {subtags[0]}, so the nonlikelyScript rule cannot be applied");
            }

            if (!string.Equals(likelyScript, subtags[1], StringComparison.Ordinal))
            {
                return Root;
            }
        }

        return Truncate(locale);
    }

    /// <summary>
    /// The locale with its last subtag removed; a bare language truncates to the root.
    /// </summary>
    internal static string Truncate(string locale)
    {
        var dash = locale.LastIndexOf('-');
        return dash < 0 ? Root : locale[..dash];
    }

    private static string Normalize(string cldrId)
    {
        return string.Equals(cldrId, "root", StringComparison.Ordinal) ? Root : cldrId.Replace('_', '-');
    }
}

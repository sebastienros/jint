#nullable enable
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

internal sealed class XmlCorpusCase
{
    public string Key { get; init; } = "";
    public string Catalog { get; init; } = "";
    public string Collection { get; init; } = "";
    public string Id { get; init; } = "";
    public string Uri { get; init; } = "";
    public string InputPath { get; init; } = "";
    public string Description { get; init; } = "";
    public string Sections { get; init; } = "";
    public string Category { get; init; } = "";
    public string Recommendation { get; init; } = "";
    public string? Version { get; init; }
    public string? Edition { get; init; }
    public string Namespace { get; init; } = "";
    public string Entities { get; init; } = "";
    public string? Output { get; init; }
    public string? OutputPath { get; init; }
    public string? Output3 { get; init; }
    public string? Output3Path { get; init; }
    public XmlDecodingDecision Decoding { get; init; } = new();
    public string Disposition { get; init; } = "";
    public string Reason { get; init; } = "";
    public string ResourceProfile { get; init; } = "";
    public string[] ResourceSignals { get; init; } = [];
}

internal sealed class XmlDecodingDecision
{
    public string Decision { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Declared { get; init; }
    public string? Detail { get; init; }
}

internal sealed class XmlCorpusLock
{
    public string Suite { get; init; } = "";
    public string ArchiveSha256 { get; init; } = "";
    public string ClarkZipSha256 { get; init; } = "";
    public string CasesSha256 { get; init; } = "";
    public int FileCount { get; init; }
    public int RowCount { get; init; }
    public Dictionary<string, int> Categories { get; init; } = new();
    public string[] MetadataPaths { get; init; } = [];
    public XmlFileLock[] Files { get; init; } = [];
    public XmlClarkChange[] ClarkChanged { get; init; } = [];
    public XmlClarkChange[] ClarkAdded { get; init; } = [];
    public XmlClarkChange[] ClarkOriginalOnly { get; init; } = [];
}

internal sealed class XmlFileLock
{
    public string Path { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string Source { get; init; } = "";
}

internal sealed class XmlClarkChange
{
    public string Path { get; init; } = "";
    public string? W3cSha256 { get; init; }
    public string? OriginalSha256 { get; init; }
}

/// <summary>Offline W3C 20130923 fixture store. No resolver reaches the parser.</summary>
internal static class XmlCorpus
{
    internal const string ArchiveDigest = "9b61db9f5dbffa545f4b8d78422167083a8568c59bd1129f94138f936cf6fc1f";
    internal const string ClarkDigest = "a919d7142fe6f72af51fc796b4df40732f385c9eb313b8993c6d39cc92acc410";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<XmlCorpusCase[]> CasesLazy = new(LoadCases);
    private static readonly Lazy<XmlCorpusLock> LockLazy = new(LoadLock);
    private static readonly Lazy<IReadOnlyDictionary<string, byte[]>> FilesLazy = new(LoadAndVerifyFiles);
    private static readonly Lazy<string> RootLazy = new(FindRoot);

    internal static string Root => RootLazy.Value;
    internal static IReadOnlyList<XmlCorpusCase> Cases => CasesLazy.Value;
    internal static XmlCorpusLock Lock => LockLazy.Value;
    internal static IReadOnlyDictionary<string, byte[]> Files => FilesLazy.Value;

    internal static XmlCorpusCase Case(string key) => Cases.Single(row => row.Key == key);

    internal static byte[] Bytes(string path)
    {
        EnsureSafePath(path);
        return Files.TryGetValue(path, out var bytes)
            ? bytes
            : throw new InvalidDataException($"Fixture path is not in the pinned archive: {path}");
    }

    internal static void EnsureSafePath(string path)
    {
        if (!path.StartsWith("xmlconf/", StringComparison.Ordinal) ||
            path.Contains('\\') || path.Contains(':') || path.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException($"Unsafe corpus path: {path}");
        }
    }

    private static XmlCorpusCase[] LoadCases()
    {
        var manifestPath = Path.Combine(Root, "cases.json");
        VerifyDigest(File.ReadAllBytes(manifestPath), Lock.CasesSha256, manifestPath);
        var cases = JsonSerializer.Deserialize<XmlCorpusCase[]>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("Empty W3C case manifest");
        if (cases.Length != 2585 || cases.Select(row => row.Key).Distinct(StringComparer.Ordinal).Count() != cases.Length)
        {
            throw new InvalidDataException("W3C case census or (catalog, ID) uniqueness changed");
        }
        foreach (var row in cases)
        {
            EnsureSafePath(row.Catalog);
            EnsureSafePath(row.InputPath);
            if (row.OutputPath is not null) EnsureSafePath(row.OutputPath);
            if (row.Output3Path is not null) EnsureSafePath(row.Output3Path);
            if (row.Key != row.Catalog + "#" + row.Id)
                throw new InvalidDataException($"Unstable W3C case key: {row.Key}");
        }
        return cases;
    }

    private static XmlCorpusLock LoadLock()
    {
        var result = JsonSerializer.Deserialize<XmlCorpusLock>(File.ReadAllText(Path.Combine(Root, "corpus.lock.json")), JsonOptions)
            ?? throw new InvalidDataException("Empty W3C corpus lock");
        if (result.ArchiveSha256 != ArchiveDigest || result.ClarkZipSha256 != ClarkDigest ||
            result.RowCount != 2585 || result.FileCount != 3386 || result.Files.Length != result.FileCount ||
            result.ClarkChanged.Length != 9 || result.ClarkAdded.Length != 13)
        {
            throw new InvalidDataException("Pinned W3C corpus lock drift");
        }
        return result;
    }

    private static IReadOnlyDictionary<string, byte[]> LoadAndVerifyFiles()
    {
        var archivePath = Path.Combine(Root, "Cache", "xmlts20130923.tar.gz");
        if (!File.Exists(archivePath))
            throw new InvalidDataException($"Required W3C archive missing: {archivePath}. Explicitly run Tools/import_corpus.py restore.");
        var archiveBytes = File.ReadAllBytes(archivePath);
        VerifyDigest(archiveBytes, ArchiveDigest, archivePath);
        var originalPath = Path.Combine(Root, "Vendor", "xmltest.zip");
        var originalBytes = File.ReadAllBytes(originalPath);
        VerifyDigest(originalBytes, ClarkDigest, originalPath);

        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var compressed = new MemoryStream(archiveBytes, writable: false))
        using (var gzip = new GZipStream(compressed, CompressionMode.Decompress))
        using (var reader = new TarReader(gzip))
        {
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                if (entry.EntryType == TarEntryType.Directory) continue;
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                    throw new InvalidDataException($"Non-regular W3C archive entry: {entry.Name}");
                EnsureSafePath(entry.Name);
                if (entry.DataStream is null)
                {
                    if (entry.Length != 0)
                        throw new InvalidDataException($"Missing W3C archive entry: {entry.Name}");
                    if (!files.TryAdd(entry.Name, []))
                        throw new InvalidDataException($"Duplicate W3C archive entry: {entry.Name}");
                    continue;
                }
                using var buffer = new MemoryStream();
                entry.DataStream.CopyTo(buffer);
                if (!files.TryAdd(entry.Name, buffer.ToArray()))
                    throw new InvalidDataException($"Duplicate W3C archive entry: {entry.Name}");
            }
        }
        if (files.Count != Lock.FileCount)
            throw new InvalidDataException($"W3C archive file census drift: {files.Count}");

        using var zip = new ZipArchive(new MemoryStream(originalBytes, writable: false), ZipArchiveMode.Read);
        foreach (var item in Lock.Files)
        {
            EnsureSafePath(item.Path);
            if (!files.TryGetValue(item.Path, out var archiveFile))
                throw new InvalidDataException($"Missing locked W3C file: {item.Path}");
            VerifyDigest(archiveFile, item.Sha256, item.Path);
            switch (item.Source)
            {
                case "edinburgh-vendor":
                    var vendorPath = Path.Combine(Root, "Vendor", "Edinburgh", item.Path.Replace('/', Path.DirectorySeparatorChar));
                    VerifyDigest(File.ReadAllBytes(vendorPath), item.Sha256, vendorPath);
                    break;
                case "unchanged-clark-zip":
                    var zipName = item.Path["xmlconf/".Length..];
                    var zipEntry = zip.GetEntry(zipName) ?? throw new InvalidDataException($"Missing unchanged Clark entry: {zipName}");
                    using (var stream = zipEntry.Open())
                    using (var buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        VerifyDigest(buffer.ToArray(), item.Sha256, zipName);
                    }
                    break;
                case "verified-cache":
                    break;
                default:
                    throw new InvalidDataException($"Unknown W3C source route for {item.Path}: {item.Source}");
            }
        }
        if (Lock.Files.Select(item => item.Path).Distinct(StringComparer.Ordinal).Count() != files.Count)
            throw new InvalidDataException("W3C file lock contains duplicate or missing paths");
        foreach (var changed in Lock.ClarkChanged)
        {
            var current = files[changed.Path];
            VerifyDigest(current, changed.W3cSha256 ?? "", changed.Path);
            var original = zip.GetEntry(changed.Path["xmlconf/".Length..])
                ?? throw new InvalidDataException($"Missing changed original Clark entry: {changed.Path}");
            using var originalStream = original.Open();
            using var copy = new MemoryStream();
            originalStream.CopyTo(copy);
            VerifyDigest(copy.ToArray(), changed.OriginalSha256 ?? "", changed.Path + " in original ZIP");
            if (current.AsSpan().SequenceEqual(copy.GetBuffer().AsSpan(0, checked((int)copy.Length))))
                throw new InvalidDataException($"Clark changed entry unexpectedly matches W3C: {changed.Path}");
        }
        foreach (var added in Lock.ClarkAdded)
        {
            VerifyDigest(files[added.Path], added.W3cSha256 ?? "", added.Path);
            if (zip.GetEntry(added.Path["xmlconf/".Length..]) is not null)
                throw new InvalidDataException($"Clark added entry unexpectedly exists in original ZIP: {added.Path}");
        }
        foreach (var old in Lock.ClarkOriginalOnly)
        {
            if (files.ContainsKey(old.Path))
                throw new InvalidDataException($"Original-only Clark entry unexpectedly exists in W3C: {old.Path}");
            var entry = zip.GetEntry(old.Path["xmlconf/".Length..])
                ?? throw new InvalidDataException($"Missing original-only Clark entry: {old.Path}");
            using var originalStream = entry.Open();
            using var copy = new MemoryStream();
            originalStream.CopyTo(copy);
            VerifyDigest(copy.ToArray(), old.OriginalSha256 ?? "", old.Path + " in original ZIP");
        }
        var notice = zip.GetEntry("xmltest/readme.html")
            ?? throw new InvalidDataException("James Clark's embedded redistribution notice is missing");
        using (var noticeStream = new StreamReader(notice.Open()))
        {
            var text = noticeStream.ReadToEnd();
            if (!text.Contains("redistribute the file <code>xmltest.zip</code>", StringComparison.Ordinal) ||
                !text.Contains("no modifications of any kind", StringComparison.Ordinal))
                throw new InvalidDataException("James Clark's embedded redistribution notice changed");
        }
        foreach (var row in Cases)
        {
            if (!files.ContainsKey(row.Catalog) || !files.ContainsKey(row.InputPath) ||
                (row.OutputPath is not null && !files.ContainsKey(row.OutputPath)) ||
                (row.Output3Path is not null && !files.ContainsKey(row.Output3Path)))
                throw new InvalidDataException($"W3C case refers to missing bytes: {row.Key}");
        }
        var vendoredPaths = Directory.EnumerateFiles(Path.Combine(Root, "Vendor", "Edinburgh"), "*", SearchOption.AllDirectories).ToArray();
        if (vendoredPaths.Length != Lock.Files.Count(item => item.Source == "edinburgh-vendor") ||
            vendoredPaths.Any(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0))
            throw new InvalidDataException("Edinburgh vendor tree has extra, missing, or linked files");
        return files;
    }

    internal static void VerifyDigest(byte[] bytes, string expected, string name)
    {
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (actual != expected)
            throw new InvalidDataException($"SHA-256 mismatch for {name}: {actual}");
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Jint.Tests.HtmlParser", "Xml", "Conformance");
            if (File.Exists(Path.Combine(candidate, "cases.json"))) return candidate;
        }
        throw new InvalidDataException("Cannot locate the XML conformance source directory");
    }
}

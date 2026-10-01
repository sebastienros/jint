#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jint.Tests.HtmlParser.Xml.Conformance;

internal sealed record XmlPreparedInputRow
{
    public string Key { get; init; } = "";
    public string InputPath { get; init; } = "";
    public string RawSha256 { get; init; } = "";
    public string Codec { get; init; } = "";
    public string DecodedUtf8Sha256 { get; init; } = "";
    public int Utf16Length { get; init; }
    public string Declared { get; init; } = "";
    public string Decision { get; init; } = "";
}

internal sealed class XmlPreparedInputException(string signature, string message) : IOException(message)
{
    internal string Signature { get; } = signature;
}

/// <summary>Six pinned source-to-string adaptations. This is test input storage, not an XML codec.</summary>
internal static class XmlPreparedInputs
{
    private const string Catalog = "xmlconf/japanese/japanese.xml#";
    private static readonly string[] Names =
    [
        "pr-xml-euc-jp", "pr-xml-iso-2022-jp", "pr-xml-shift_jis",
        "weekly-euc-jp", "weekly-iso-2022-jp", "weekly-shift_jis"
    ];
    private static readonly HashSet<string> Keys = Names.Select(name => Catalog + name).ToHashSet(StringComparer.Ordinal);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex Declaration = new("^<\\?xml\\s+[^?]*?\\bencoding\\s*=\\s*(['\"])([^'\"]+)\\1", RegexOptions.IgnoreCase);
    private static readonly Lazy<IReadOnlyDictionary<string, XmlPreparedInputRow>> RowsLazy = new(LoadRows);

    internal static IReadOnlyDictionary<string, XmlPreparedInputRow> Rows => RowsLazy.Value;

    internal static (string? Text, XmlDecodingDecision Decision) Load(
        XmlCorpusCase row, byte[] raw, bool useArtifactOverride = false, byte[]? artifactOverride = null)
    {
        var rows = Rows; // Validate the complete key registry before route selection.
        if (!Keys.Contains(row.Key)) return XmlByteDecoder.Decode(raw);
        var entry = rows[row.Key];
        var artifact = useArtifactOverride ? artifactOverride : Artifact(entry, raw);
        return DecodePrepared(row, raw, entry, artifact);
    }

    /// <summary>
    /// Reads the cached UTF-8 artifact, producing it from the pinned raw bytes when it is absent or stale.
    /// Returns <c>null</c> when it cannot be produced, which <see cref="DecodePrepared"/> reports as a harness failure.
    /// </summary>
    internal static byte[]? Artifact(XmlPreparedInputRow entry, byte[] raw)
    {
        var path = Path.Combine(XmlCorpusCache.Directory, "DecodedJapanese", entry.RawSha256 + ".utf8");
        if (XmlCorpusCache.TryReadVerified(path, entry.DecodedUtf8Sha256) is { } cached) return cached;
        var produced = Produce(entry, raw);
        if (produced is not null) XmlCorpusCache.WriteAtomically(path, produced);
        return produced;
    }

    // The provider is queried directly rather than registered, so the generic XmlByteDecoder route keeps seeing
    // only the in-box encodings and its strict-decode-error outcome for these inputs stays observable.
    private static byte[]? Produce(XmlPreparedInputRow entry, byte[] raw)
    {
        if (Hash(raw) != entry.RawSha256) return null;
        var name = entry.Codec switch
        {
            "euc_jp" => "euc-jp",
            "iso2022_jp" => "iso-2022-jp",
            "shift_jis" => "shift_jis",
            _ => null
        };
        var encoding = name is null ? null : CodePagesEncodingProvider.Instance.GetEncoding(
            name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        if (encoding is null) return null;
        try
        {
            var bytes = StrictUtf8.GetBytes(encoding.GetString(raw));
            return Hash(bytes) == entry.DecodedUtf8Sha256 ? bytes : null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // Supplied-byte seam for corruption probes; no test mutates the shared cache.
    internal static (string Text, XmlDecodingDecision Decision) DecodePrepared(
        XmlCorpusCase row, byte[] raw, XmlPreparedInputRow entry, byte[]? artifact)
    {
        if (row.Key != entry.Key || row.InputPath != entry.InputPath)
            throw new XmlPreparedInputException("prepared-case-identity", row.Key);
        if (HasSignature(raw))
            throw new XmlPreparedInputException("prepared-raw-signature", row.Key);
        if (Hash(raw) != entry.RawSha256)
            throw new XmlPreparedInputException("prepared-raw-hash", row.Key);
        if (artifact is null)
            throw new XmlPreparedInputException("prepared-artifact-missing", row.Key);
        if (HasSignature(artifact))
            throw new XmlPreparedInputException("prepared-artifact-bom", row.Key);
        string text;
        try
        {
            text = StrictUtf8.GetString(artifact);
        }
        catch (DecoderFallbackException error)
        {
            throw new XmlPreparedInputException("prepared-artifact-utf8", $"{row.Key}: {error.Message}");
        }
        if (Hash(artifact) != entry.DecodedUtf8Sha256)
            throw new XmlPreparedInputException("prepared-artifact-hash", row.Key);
        if (text.Length != entry.Utf16Length)
            throw new XmlPreparedInputException("prepared-artifact-length", row.Key);
        var declaration = Declaration.Match(text);
        if (!declaration.Success || declaration.Groups[2].Value != entry.Declared)
            throw new XmlPreparedInputException("prepared-declaration", row.Key);
        return (text, new XmlDecodingDecision
        {
            Decision = entry.Decision, Status = "decoded", Declared = entry.Declared
        });
    }

    internal static IReadOnlyDictionary<string, XmlPreparedInputRow> ValidateRegistry(
        IReadOnlyList<XmlPreparedInputRow> entries, XmlCorpusLock corpusLock)
    {
        if (entries.Count != Keys.Count || entries.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() != Keys.Count ||
            entries.Any(item => !Keys.Contains(item.Key)))
            throw new XmlPreparedInputException("prepared-registry", "Prepared input registry must contain the exact six unique keys");
        var locked = corpusLock.Files.ToDictionary(item => item.Path, item => item.Sha256, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var name = entry.Key[Catalog.Length..];
            var suffix = name.StartsWith("pr-xml-", StringComparison.Ordinal) ? name["pr-xml-".Length..] : name["weekly-".Length..];
            var declared = name == "weekly-shift_jis" ? "Shift_JIS" : suffix;
            var (codec, decision) = suffix switch
            {
                "euc-jp" => ("euc_jp", "prepared-euc-jp"),
                "iso-2022-jp" => ("iso2022_jp", "prepared-iso-2022-jp"),
                "shift_jis" => ("shift_jis", "prepared-shift-jis"),
                _ => throw new XmlPreparedInputException("prepared-registry", entry.Key)
            };
            var path = "xmlconf/japanese/" + name + ".xml";
            if (entry.InputPath != path || entry.Codec != codec || entry.Decision != decision ||
                entry.Declared != declared || !IsSha(entry.RawSha256) || !IsSha(entry.DecodedUtf8Sha256) ||
                entry.Utf16Length <= 0 || !locked.TryGetValue(path, out var lockedSha) || lockedSha != entry.RawSha256)
                throw new XmlPreparedInputException("prepared-registry-identity", entry.Key);
        }
        return entries.ToDictionary(item => item.Key, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, XmlPreparedInputRow> LoadRows()
    {
        var path = Path.Combine(XmlCorpus.Root, "prepared-inputs.json");
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException error)
        {
            throw new XmlPreparedInputException("prepared-metadata-missing", error.Message);
        }
        if (Hash(bytes) != XmlCorpus.Lock.PreparedInputsSha256)
            throw new XmlPreparedInputException("prepared-metadata-hash", path);
        XmlPreparedInputRow[] entries;
        try
        {
            entries = JsonSerializer.Deserialize<XmlPreparedInputRow[]>(bytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new XmlPreparedInputException("prepared-metadata-json", path);
        }
        catch (JsonException error)
        {
            throw new XmlPreparedInputException("prepared-metadata-json", error.Message);
        }
        var rows = ValidateRegistry(entries, XmlCorpus.Lock);
        foreach (var entry in rows.Values)
        {
            var caseRow = XmlCorpus.Case(entry.Key);
            if (caseRow.InputPath != entry.InputPath || caseRow.Decoding.Status != "decoded" ||
                caseRow.Decoding.Decision != entry.Decision || caseRow.Decoding.Declared != entry.Declared ||
                caseRow.Disposition != "optional-error-review" || caseRow.Category != "error")
                throw new XmlPreparedInputException("prepared-manifest-identity", entry.Key);
        }
        return rows;
    }

    private static bool HasSignature(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) || bytes.StartsWith(new byte[] { 0xfe, 0xff }) ||
        bytes.StartsWith(new byte[] { 0xff, 0xfe }) || bytes.StartsWith(new byte[] { 0x00, 0x3c }) ||
        bytes.StartsWith(new byte[] { 0x3c, 0x00 });

    private static bool IsSha(string text) => text.Length == 64 && text.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

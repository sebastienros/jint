using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Jint.CldrDates.Generator;

// Regenerates Jint/Native/Intl/Data/DateTimePatterns.bin and DateTimePatternData.Data.cs from the CLDR release
// pinned in tools/cldr-dates/pin.json. See tools/cldr-dates/README.md. The build never runs this.

string? cacheDirectory = null;
string? repository = null;
var check = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--cache" when i + 1 < args.Length:
            cacheDirectory = args[++i];
            break;
        case "--repository" when i + 1 < args.Length:
            repository = args[++i];
            break;
        case "--check":
            check = true;
            break;
        default:
            Console.Error.WriteLine("usage: Jint.CldrDates.Generator [--cache <directory>] [--repository <directory>] [--check]");
            return 2;
    }
}

repository ??= FindRepository(Directory.GetCurrentDirectory());
cacheDirectory ??= Path.Combine(Path.GetTempPath(), "jint-cldr-dates");
var toolDirectory = Path.Combine(repository, "tools", "cldr-dates");
var dataDirectory = Path.Combine(repository, "Jint", "Native", "Intl", "Data");
var binPath = Path.Combine(dataDirectory, "DateTimePatterns.bin");
var codePath = Path.Combine(dataDirectory, "DateTimePatternData.Data.cs");

var pin = Pin.Load(Path.Combine(toolDirectory, "pin.json"));
Console.WriteLine($"CLDR {pin.CldrVersion} ({pin.CldrReleaseTag}) via cldr-json {pin.CldrJsonVersion}; inputs cached in {cacheDirectory}");
var inputs = await Inputs.LoadAsync(pin, cacheDirectory).ConfigureAwait(false);
var ids = CldrExtraction.LocaleIds(inputs);
var parents = ParentLocales.Read(inputs, ids);
var locales = CldrExtraction.ExtractAll(inputs, ids);
Console.WriteLine($"  {locales.Count} locales, {SlotLayout.Names.Length} slots each, {locales.Sum(l => l.Formats.Count)} availableFormats entries kept");

var result = PatternFile.Write(pin.CldrVersion, locales, parents);
PatternFile.VerifyRoundTrip(result.Bytes, locales);
Console.WriteLine("  round trip: every locale decodes to what was written");
PrintStatistics(result);

var code = EmitCode(pin, locales.Count, result);
if (check)
{
    var committed = File.Exists(codePath) ? File.ReadAllText(codePath) : "";
    var match = Regex.Match(committed, "PayloadSha256 = \"([0-9a-f]{64})\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    if (!match.Success || !string.Equals(match.Groups[1].Value, result.PayloadSha256, StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"--check: the regenerated content (payload SHA-256 {result.PayloadSha256}) differs from the committed one ({(match.Success ? match.Groups[1].Value : "none")})");
        return 1;
    }

    var sameBytes = File.Exists(binPath) && File.ReadAllBytes(binPath).AsSpan().SequenceEqual(result.Bytes);
    Console.WriteLine(sameBytes
        ? "--check: identical to the committed resource, byte for byte"
        : "--check: same content as the committed resource; the deflate output differs, which a different runtime's zlib can cause");
}
else
{
    await File.WriteAllBytesAsync(binPath, result.Bytes).ConfigureAwait(false);
    await File.WriteAllTextAsync(codePath, code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)).ConfigureAwait(false);
    Console.WriteLine($"  wrote {binPath}");
    Console.WriteLine($"  wrote {codePath}");
}

return 0;

static string FindRepository(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Jint.slnx")))
        {
            return directory.FullName;
        }
    }

    throw new InvalidOperationException("Run this from inside the Jint repository, or pass --repository.");
}

static string EmitCode(Pin pin, int localeCount, PatternFileResult result)
{
    var dates = pin.Package("cldr-dates-full");
    var core = pin.Package("cldr-core");
    var payloadLength = result.IndexRawLength + result.Blocks.Sum(b => b.RawLength);
    var fileSha256 = Inputs.Sha256(result.Bytes);
    var invariant = CultureInfo.InvariantCulture;
    var builder = new StringBuilder();
    builder.Append(invariant, $$"""
        // Generated from CLDR {{pin.CldrVersion}} (https://github.com/unicode-org/cldr/releases/tag/{{pin.CldrReleaseTag}}) by tools/cldr-dates;
        // do not edit. The locale data is cldr-json {{pin.CldrJsonVersion}}'s, which resolves each locale's inheritance itself:
        //   {{dates.FileName}}, npm integrity {{dates.Integrity}}
        //   {{core.FileName}}, npm integrity {{core.Integrity}}
        // and the parent locales are {{pin.SupplementalData.Path}} at {{pin.CldrReleaseTag}}, SHA-256 {{pin.SupplementalData.Sha256}}.
        // DateTimePatterns.bin carries, for each of the {{localeCount}} locales cldr-json has, the Gregorian calendar's availableFormats
        // (without the skeletons with a quarter or week field and the -count- and -alt- variants), dateTimeFormats and their atTime
        // variants, dateFormats, timeFormats, appendItems with the field display names they use, and the month, weekday, era and
        // am/pm names. Every locale but the root stores only what differs from its CLDR parent, and the records are deflated
        // one block per language ({{result.Blocks.Count}} blocks). tools/cldr-dates/README.md describes the layout.
        //   DateTimePatterns.bin: {{result.Bytes.Length}} bytes, SHA-256 {{fileSha256}}
        //   inflated content: {{payloadLength}} bytes, SHA-256 {{result.PayloadSha256}}
        // Unicode License v3 (CREDITS.txt). The data is CLDR's own: update it from a later release, not entry by entry.

        namespace Jint.Native.Intl.Data;

        internal sealed partial class DateTimePatternData
        {
            internal const string CldrVersion = "{{pin.CldrVersion}}";
            internal const int LocaleCount = {{localeCount}};
            internal const int SlotCount = {{SlotLayout.Names.Length}};

            /// <summary>
            /// The SHA-256 of the inflated index followed by every inflated block in order, which does not depend on the
            /// deflate encoder that wrote the resource.
            /// </summary>
            internal const string PayloadSha256 = "{{result.PayloadSha256}}";
        }

        """);
    return builder.ToString().ReplaceLineEndings("\n");
}

static void PrintStatistics(PatternFileResult result)
{
    var payload = result.Blocks.Sum(b => b.RawLength);
    Console.WriteLine($"DateTimePatterns.bin: {result.Bytes.Length:N0} bytes");
    Console.WriteLine($"  index: {result.IndexRawLength:N0} bytes inflated, {result.IndexCompressedLength:N0} deflated; {result.ParentCount} parent entries differ from truncation");
    Console.WriteLine($"  {result.Blocks.Count} language blocks: {payload:N0} bytes inflated, {result.Blocks.Sum(b => b.CompressedLength):N0} deflated");
    foreach (var language in new[] { "en", "de", "ja", "und" })
    {
        var block = result.Blocks.Single(b => b.Language == language);
        Console.WriteLine($"  {language}: {block.Locales.Count} locales, {block.RawLength:N0} bytes inflated, {block.CompressedLength:N0} deflated");
    }

    foreach (var block in result.Blocks.OrderByDescending(b => b.RawLength).Take(5))
    {
        Console.WriteLine($"  largest: {block.Language} {block.RawLength:N0} / {block.CompressedLength:N0} ({block.Locales.Count} locales)");
    }
}

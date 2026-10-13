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
var calendarBinPath = Path.Combine(dataDirectory, "DateTimeCalendarPatterns.bin");
var calendarCodePath = Path.Combine(dataDirectory, "DateTimeCalendarPatternData.Data.cs");

var pin = Pin.Load(Path.Combine(toolDirectory, "pin.json"));
Console.WriteLine($"CLDR {pin.CldrVersion} ({pin.CldrReleaseTag}) via cldr-json {pin.CldrJsonVersion}; inputs cached in {cacheDirectory}");
var inputs = await Inputs.LoadAsync(pin, cacheDirectory).ConfigureAwait(false);
var ids = CldrExtraction.LocaleIds(inputs);
var parents = ParentLocales.Read(inputs, ids);
var locales = CldrExtraction.ExtractAll(inputs, ids);
IntervalResolution.Resolve(locales, parents);
Console.WriteLine($"  {locales.Count} locales, {SlotLayout.Names.Length} slots each, {locales.Sum(l => l.Formats.Count)} availableFormats entries kept, {locales.Sum(l => l.Intervals.Count)} interval patterns");

var result = PatternFile.Write(pin.CldrVersion, locales, parents);
PatternFile.VerifyRoundTrip(result.Bytes, locales);
Console.WriteLine("  round trip: every locale decodes to what was written");
PrintStatistics(result);

var code = EmitCode(pin, locales.Count, result);

var calendars = CalendarExtraction.ExtractAll(inputs, ids, parents);
Console.WriteLine($"  {calendars.Count} other calendars: {string.Join(", ", calendars.Select(c => c.Spec.CldrName))}");
var calendarResult = CalendarPatternFile.Write(pin.CldrVersion, calendars, parents);
CalendarPatternFile.VerifyRoundTrip(calendarResult.Bytes, calendars, parents);
Console.WriteLine("  calendar round trip: every locale of every calendar decodes to what was written");
PrintCalendarStatistics(calendarResult);
var calendarCode = EmitCalendarCode(pin, locales.Count, calendars, calendarResult);

var failed = false;
foreach (var (name, path, bytesPath, payload, bytes, text) in new[]
         {
             ("DateTimePatterns.bin", codePath, binPath, result.PayloadSha256, result.Bytes, code),
             ("DateTimeCalendarPatterns.bin", calendarCodePath, calendarBinPath, calendarResult.PayloadSha256, calendarResult.Bytes, calendarCode),
         })
{
    var committed = File.Exists(path) ? File.ReadAllText(path) : "";
    var match = Regex.Match(committed, "PayloadSha256 = \"([0-9a-f]{64})\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    var sameContent = match.Success && string.Equals(match.Groups[1].Value, payload, StringComparison.Ordinal);
    if (check)
    {
        if (!sameContent)
        {
            Console.Error.WriteLine($"--check: {name}'s regenerated content (payload SHA-256 {payload}) differs from the committed one ({(match.Success ? match.Groups[1].Value : "none")})");
            failed = true;
            continue;
        }

        var sameBytes = File.Exists(bytesPath) && File.ReadAllBytes(bytesPath).AsSpan().SequenceEqual(bytes);
        Console.WriteLine(sameBytes
            ? $"--check: {name} is identical to the committed resource, byte for byte"
            : $"--check: {name} has the same content as the committed resource; the deflate output differs, which a different runtime's zlib can cause");
    }
    else if (sameContent)
    {
        // Rewriting it would change nothing but, under another runtime's zlib, the deflated bytes.
        Console.WriteLine($"  {name}: the content is the committed one; left as it is");
    }
    else
    {
        await File.WriteAllBytesAsync(bytesPath, bytes).ConfigureAwait(false);
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)).ConfigureAwait(false);
        Console.WriteLine($"  wrote {bytesPath}");
        Console.WriteLine($"  wrote {path}");
    }
}

return failed ? 1 : 0;

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
        // variants, dateFormats, timeFormats, appendItems with the field display names they use, the month, weekday, era,
        // am/pm and flexible day-period names, the intervalFormats with their fallback, and the day period rules of
        // cldr-core's supplemental/dayPeriods.json as ICU looks them up for the locale. Every locale but the root stores only
        // what differs from its CLDR parent, and the records are deflated one block per language ({{result.Blocks.Count}} blocks).
        // tools/cldr-dates/README.md describes the layout.
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

static string EmitCalendarCode(Pin pin, int localeCount, List<CalendarData> calendars, PatternFileResult result)
{
    var dates = pin.Package("cldr-dates-full");
    var payloadLength = result.IndexRawLength + result.Blocks.Sum(b => b.RawLength);
    var fileSha256 = Inputs.Sha256(result.Bytes);
    var invariant = CultureInfo.InvariantCulture;
    var packages = new StringBuilder();
    foreach (var name in calendars.Select(c => c.Spec.Package).Distinct(StringComparer.Ordinal))
    {
        var package = pin.Package(name);
        packages.Append(invariant, $"//   {package.FileName}, npm integrity {package.Integrity}").Append('\n');
    }

    var builder = new StringBuilder();
    builder.Append(invariant, $$"""
        // Generated from CLDR {{pin.CldrVersion}} (https://github.com/unicode-org/cldr/releases/tag/{{pin.CldrReleaseTag}}) by tools/cldr-dates;
        // do not edit. The calendar data is cldr-json {{pin.CldrJsonVersion}}'s, which resolves each locale's inheritance itself:
        {{packages.ToString().TrimEnd('\n')}}
        // and the generic calendar's availableFormats, from {{dates.FileName}}, which DateTimePatternData.Data.cs pins.
        // DateTimeCalendarPatterns.bin carries, for each of the {{localeCount}} locales and each of {{calendars.Count}} calendars
        // ({{string.Join(", ", calendars.Select(c => c.Spec.CldrName))}}),
        // the calendar's availableFormats, dateFormats, the time formats and atTime patterns ICU reads for it, appendItems, the
        // month names of a calendar with months of its own, the era names, and the intervalFormats with their fallback. Every
        // locale but the root stores only what differs from its CLDR parent's, in DateTimePatterns.bin's language blocks
        // ({{result.Blocks.Count}}). tools/cldr-dates/README.md describes the layout.
        //   DateTimeCalendarPatterns.bin: {{result.Bytes.Length}} bytes, SHA-256 {{fileSha256}}
        //   inflated content: {{payloadLength}} bytes, SHA-256 {{result.PayloadSha256}}
        // Unicode License v3 (CREDITS.txt). The data is CLDR's own: update it from a later release, not entry by entry.

        namespace Jint.Native.Intl.Data;

        internal sealed partial class DateTimeCalendarPatternData
        {
            internal const string CldrVersion = "{{pin.CldrVersion}}";
            internal const int LocaleCount = {{localeCount}};
            internal const int CalendarCount = {{calendars.Count}};

            /// <summary>
            /// The SHA-256 of the inflated index followed by every inflated block in order, which does not depend on the
            /// deflate encoder that wrote the resource.
            /// </summary>
            internal const string PayloadSha256 = "{{result.PayloadSha256}}";
        }

        """);
    return builder.ToString().ReplaceLineEndings("\n");
}

static void PrintCalendarStatistics(PatternFileResult result)
{
    var payload = result.Blocks.Sum(b => b.RawLength);
    Console.WriteLine($"DateTimeCalendarPatterns.bin: {result.Bytes.Length:N0} bytes");
    Console.WriteLine($"  index: {result.IndexRawLength:N0} bytes inflated, {result.IndexCompressedLength:N0} deflated");
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

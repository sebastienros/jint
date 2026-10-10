#nullable enable

using System.Text;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.DateTimeFormat.prototype.formatRange</c> and <c>formatRangeToParts</c> for a component bag and a
/// <c>dateStyle</c>/<c>timeStyle</c>, written through CLDR 48.2's <c>intervalFormats</c> the way ICU's
/// <c>DateIntervalFormat</c> writes them as V8 drives it (sebastienros/jint#4158, #4247):
/// https://tc39.es/ecma402/#sec-partitiondatetimerangepattern.
/// </summary>
/// <remarks>
/// <para>
/// The golden table is <c>tools/cldr-dates/reference/range-golden.tsv</c>, read from where the reference model wrote it:
/// 51 locales, 23 bags — 15 component bags and 8 date and time styles — and up to six pairs of dates, each pair
/// differing first in the field it is named after, with the text and every part's type, source and length. The model —
/// <c>tools/cldr-dates/reference/interval_format.py</c>, a model of ICU's <c>DateIntervalFormat</c> over CLDR 48.2 —
/// writes the table from the data itself rather than from Node's output. Its last column is what Node 24.19 (ICU 78.3)
/// writes where it differs from the model, with U+202F written as U+0020, as Jint writes it; it is empty on every row
/// but those <see cref="KnownIcuDifferences"/> lists.
/// </para>
/// <para>
/// The bags are formatted with <c>calendar: "gregory"</c> and <c>numberingSystem: "latn"</c>, as the format matcher's
/// golden table is.
/// </para>
/// </remarks>
public class IntlDateTimeFormatRangeTests
{
    private const string GoldenResource = "cldr-dates-range-golden.tsv";

    private static readonly Dictionary<string, char> TypeCodes = new(StringComparer.Ordinal)
    {
        ["literal"] = 'l',
        ["era"] = 'G',
        ["year"] = 'y',
        ["month"] = 'M',
        ["day"] = 'd',
        ["weekday"] = 'E',
        ["dayPeriod"] = 'a',
        ["hour"] = 'h',
        ["minute"] = 'm',
        ["second"] = 's',
        ["fractionalSecond"] = 'S',
        ["timeZoneName"] = 'z',
    };

    private static readonly Dictionary<string, char> SourceCodes = new(StringComparer.Ordinal)
    {
        ["shared"] = 's',
        ["startRange"] = '1',
        ["endRange"] = '2',
    };

    /// <summary>
    /// Every row where ICU's answer differs from the golden table's, and why. None differs in text; each differs in the
    /// source of a part V8 reports as shared although ICU writes it from the end date, which
    /// https://tc39.es/ecma402/#sec-partitiondatetimerangepattern does not allow a shared part (see
    /// <see cref="DateTimeRangePattern.FromRuns"/>).
    /// </summary>
    private static readonly Dictionary<(string Locale, string Bag, string Pair), string> KnownIcuDifferences = new()
    {
        // "E d LLL تا E d MMM y": the stand-alone month before the dash and the format one after it are two fields to
        // ICU, which pairs neither and reports both months as shared; Jint reads them as one field.
        [("fa", "EEEE_y_MMMM_d", "day")] = "L and M read as two fields",
        [("fa", "EEEE_y_MMMM_d", "month")] = "L and M read as two fields",
        [("fa", "y_MMM_d", "month")] = "L and M read as two fields",

        // "d – d MMM y" for a month difference writes the month once, the end date's: an endRange part to Jint.
        [("sw", "y_MMM_d", "month")] = "the end date's month, written once",

        // The same two shapes, in the styles whose skeletons reach the same interval patterns.
        [("fa", "d_full", "day")] = "L and M read as two fields",
        [("fa", "d_full", "month")] = "L and M read as two fields",
        [("fa", "d_long", "month")] = "L and M read as two fields",
        [("fa", "d_medium", "month")] = "L and M read as two fields",
        [("sw", "d_long", "month")] = "the end date's month, written once",
        [("sw", "d_medium", "month")] = "the end date's month, written once",
    };

    [Test]
    public void EveryGoldenRowFormatsAsTheReferenceModel()
    {
        var engine = new Engine();
        var range = engine.Evaluate("""
            (function (locale, options, start, end) {
                var f = new Intl.DateTimeFormat(locale, Object.assign({ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, JSON.parse(options)));
                var parts = f.formatRangeToParts(start, end);
                return [f.formatRange(start, end), parts.map(function (p) { return p.value; }).join(''), parts.map(function (p) { return p.type + ':' + p.source + ':' + p.value.length; }).join(' ')];
            })
            """);

        var table = ReadGoldenTable();
        var problems = new List<string>();
        foreach (var golden in table.Rows)
        {
            var (start, end) = table.Pairs[golden.Pair];
            var result = engine.Invoke(range, golden.Locale, table.Bags[golden.Bag], start, end).AsArray();
            var text = result[0].AsString();
            var label = $"{golden.Locale} {golden.Bag} {golden.Pair}";
            Check(problems, label, "formatRange", golden.Text, text);
            Check(problems, label, "formatRangeToParts", golden.Text, result[1].AsString());
            Check(problems, label, "parts", golden.Parts, EncodeParts(result[2].AsString()));
        }

        problems.Should().BeEmpty();
        table.Rows.Count.Should().Be(5610);
    }

    [Test]
    public void TheGoldenTableDiffersFromIcuOnlyWhereDocumented()
    {
        var differing = new Dictionary<(string, string, string), string>();
        foreach (var golden in ReadGoldenTable().Rows)
        {
            if (golden.Icu.Length > 0)
            {
                differing.Add((golden.Locale, golden.Bag, golden.Pair), golden.Icu);
            }
        }

        differing.Keys.Should().BeEquivalentTo(KnownIcuDifferences.Keys);
    }

    /// <summary>
    /// The design's rows (sebastienros/jint#4158) and the shapes behind them: one field differing is written once for
    /// each date inside the locale's own interval pattern; two dates that differ only where the format does not look
    /// are one date; a range CLDR has no interval pattern for is the two whole dates in the fallback. What Node 24.19
    /// writes, with U+0020 for U+202F.
    /// </summary>
    [TestCase("en", "{ month: 'short', day: 'numeric' }", "2022-12-24", "2022-12-27", "Dec 24\u2009–\u200927")]
    [TestCase("en", "{ month: 'short', day: 'numeric' }", "2022-11-24", "2022-12-03", "Nov 24\u2009–\u2009Dec 3")]
    [TestCase("en", "{ month: 'short', day: 'numeric' }", "2022-12-24", "2023-01-03", "Dec 24, 2022\u2009–\u2009Jan 3, 2023")]
    [TestCase("en", "{ year: 'numeric', month: 'short', day: 'numeric' }", "2022-11-24", "2022-12-03", "Nov 24\u2009–\u2009Dec 3, 2022")]
    [TestCase("en", "{ weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' }", "2022-12-24", "2022-12-27", "Saturday, December 24\u2009–\u2009Tuesday, December 27, 2022")]
    [TestCase("en", "{ day: 'numeric' }", "2022-12-24", "2023-01-03", "12/24/2022\u2009–\u20091/3/2023")]
    [TestCase("en", "{ month: 'long' }", "2022-12-24", "2022-12-27", "December")]
    [TestCase("en", "{ year: 'numeric', month: '2-digit', day: '2-digit' }", "2022-01-03", "2022-01-05", "1/3/2022\u2009–\u20091/5/2022")]
    [TestCase("de", "{ month: 'short', day: 'numeric' }", "2022-12-24", "2022-12-27", "24.–27. Dez.")]
    [TestCase("de", "{ weekday: 'short', day: 'numeric', month: 'long' }", "2022-12-24", "2022-12-27", "Sa., 24.\u2009–\u2009Di., 27. Dezember")]
    [TestCase("fr", "{ month: 'short', day: 'numeric' }", "2022-12-24", "2022-12-27", "24–27 déc.")]
    [TestCase("ja", "{ month: 'short', day: 'numeric' }", "2022-12-24", "2022-12-27", "12/24～12/27")]
    [TestCase("ja", "{ weekday: 'short', month: 'long', day: 'numeric' }", "2022-12-24", "2022-12-27", "12/24(土)～12/27(火)")]
    [TestCase("zh", "{ weekday: 'short', month: 'long', day: 'numeric' }", "2022-12-24", "2022-12-27", "12/24周六至12/27周二")]
    [TestCase("ko", "{ weekday: 'short', month: 'long', day: 'numeric' }", "2022-12-24", "2022-12-27", "12월 24일 (토)~27일 (화)")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:05", "2022-12-24T09:45", "9:05\u2009–\u20099:45 AM")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:05", "2022-12-24T15:45", "9:05 AM\u2009–\u20093:45 PM")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:05", "2022-12-25T15:45", "12/24/2022, 9:05 AM\u2009–\u200912/25/2022, 3:45 PM")]
    [TestCase("en", "{ hour: '2-digit', minute: '2-digit', hour12: true }", "2022-12-24T09:05", "2022-12-24T15:45", "9:05 AM\u2009–\u20093:45 PM")]
    [TestCase("en", "{ hour: 'numeric', minute: '2-digit', hourCycle: 'h23' }", "2022-12-24T09:05", "2022-12-24T15:45", "09:05\u2009–\u200915:45")]
    [TestCase("de", "{ hour: 'numeric' }", "2022-12-24T06:05", "2022-12-24T11:45", "06–11 Uhr")]
    [TestCase("ja", "{ hour: 'numeric', minute: 'numeric' }", "2022-12-24T00:05", "2022-12-24T12:45", "0時05分～12時45分")]
    [TestCase("en", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:00", "2022-12-24T15:00", "December 24, 2022, 9:00 AM\u2009–\u20093:00 PM")]
    [TestCase("en", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:00", "2022-12-25T15:00", "December 24, 2022 at 9:00 AM\u2009–\u2009December 25, 2022 at 3:00 PM")]
    [TestCase("en", "{ year: '2-digit', month: '2-digit', day: '2-digit', hour: 'numeric', minute: 'numeric', second: 'numeric' }", "2022-01-05T09:00", "2022-01-05T15:00", "1/5/22, 9:00:00 AM\u2009–\u20093:00:00 PM")]
    [TestCase("fr", "{ year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:00", "2022-12-24T15:00", "24/12/2022, 09:00\u2009–\u200915:00")]
    [TestCase("de", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:00", "2022-12-24T15:00", "24. Dezember 2022, 09:00–15:00 Uhr")]
    [TestCase("en", "{ year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "2022-12-24T06:05", "2022-12-24T06:05:30", "12/24/2022, 6:05 AM")]
    [TestCase("en", "{ hour: 'numeric', dayPeriod: 'short' }", "2022-12-24T09:00", "2022-12-24T15:00", "9 in the morning\u2009–\u20093 in the afternoon")]
    [TestCase("en", "{ weekday: 'short' }", "2022-12-24", "2022-12-27", "Sat\u2009–\u2009Tue")]
    [TestCase("en", "{ era: 'short', year: 'numeric' }", "2022-12-24", "2023-12-24", "2022\u2009–\u20092023 AD")]
    [TestCase("en", "{ minute: 'numeric', second: 'numeric' }", "2022-12-24T09:02:03", "2022-12-24T10:02:13", "02:03\u2009–\u200902:13")]
    public void ARangeIsWrittenWithTheLocalesIntervalPattern(string locale, string options, string start, string end, string expected)
    {
        var engine = new Engine();
        var script = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options}))";
        var dates = $"new Date('{start}Z'), new Date('{end}Z')";

        engine.Evaluate($"{script}.formatRange({dates})").AsString().Should().Be(expected);
        engine.Evaluate($"{script}.formatRangeToParts({dates}).map(function (p) {{ return p.value; }}).join('')").AsString().Should().Be(expected);
    }

    /// <summary>
    /// The parts carry the source https://tc39.es/ecma402/#sec-partitiondatetimerangepattern gives them from the range
    /// pattern: the fields written for one date and the text between them are that date's, the rest is shared, and a
    /// range that collapses is the start date with every part shared.
    /// </summary>
    [TestCase("en", "{ month: 'short', day: 'numeric' }", "2022-12-24", "2022-12-27", "month:shared:Dec|literal:shared: |day:startRange:24|literal:shared:\u2009–\u2009|day:endRange:27")]
    [TestCase("en", "{ month: 'short', day: 'numeric' }", "2022-11-24", "2022-12-03", "month:startRange:Nov|literal:startRange: |day:startRange:24|literal:shared:\u2009–\u2009|month:endRange:Dec|literal:endRange: |day:endRange:3")]
    [TestCase("en", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "2022-12-24T09:00", "2022-12-24T15:00",
        "month:shared:December|literal:shared: |day:shared:24|literal:shared:, |year:shared:2022|literal:shared:, |hour:startRange:9|literal:startRange::"
        + "|minute:startRange:00|literal:startRange: |dayPeriod:startRange:AM|literal:shared:\u2009–\u2009|hour:endRange:3|literal:endRange::"
        + "|minute:endRange:00|literal:endRange: |dayPeriod:endRange:PM")]
    [TestCase("ja", "{ weekday: 'short', month: 'long', day: 'numeric' }", "2022-12-24", "2022-12-27",
        "month:startRange:12|literal:startRange:/|day:startRange:24|literal:startRange:(|weekday:startRange:土|literal:shared:)～"
        + "|month:endRange:12|literal:endRange:/|day:endRange:27|literal:endRange:(|weekday:endRange:火|literal:shared:)")]
    [TestCase("en", "{ month: 'long' }", "2022-12-24", "2022-12-27", "month:shared:December")]
    public void EachPartCarriesItsSource(string locale, string options, string start, string end, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options})).formatRangeToParts(new Date('{start}Z'), new Date('{end}Z'))"
                        + ".map(function (p) { return p.type + ':' + p.source + ':' + p.value; }).join('|')")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-partitiondatetimerangepattern compares fractional seconds at the digits the format
    /// writes, so two dates a millisecond apart are one date to a format with one fractional digit. ICU compares the
    /// milliseconds, and V8 writes <c>05:01.2 – 05:01.2</c>; Jint follows the specification.
    /// </summary>
    [Test]
    public void FractionalSecondsAreComparedAtTheDigitsTheFormatWrites()
    {
        var engine = new Engine();
        string Range(int digits, int startMilliseconds, int endMilliseconds)
            => engine.Evaluate($"new Intl.DateTimeFormat('en', {{ minute: 'numeric', second: 'numeric', fractionalSecondDigits: {digits}, timeZone: 'UTC' }})"
                               + $".formatRange(Date.UTC(2022, 10, 4, 9, 5, 1, {startMilliseconds}), Date.UTC(2022, 10, 4, 9, 5, 1, {endMilliseconds}))").AsString();

        Range(1, 234, 235).Should().Be("05:01.2");
        Range(3, 234, 235).Should().Be("05:01.234\u2009–\u200905:01.235");
        Range(1, 234, 567).Should().Be("05:01.2\u2009–\u200905:01.5");
        engine.Evaluate("new Intl.DateTimeFormat('en', { minute: 'numeric', second: 'numeric', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 10, 4, 9, 5, 1, 234), Date.UTC(2022, 10, 4, 9, 5, 1, 567))")
            .AsString().Should().Be("05:01");
    }

    /// <summary>
    /// CLDR 48.2 writes U+2009 THIN SPACE around the dash of its intervals and fallback, and a range keeps it, as V8 does;
    /// U+202F, which CLDR writes in its times, is U+0020 here as in every lane (V8 keeps it in a range). A component bag's
    /// range and a <c>dateStyle</c> range agree on the separator test262 reads off the latter, and so does the Chinese
    /// and Dangi lane, whose fixed separator is the fallback's.
    /// </summary>
    [Test]
    public void ARangeKeepsCldrsThinSpacesAroundItsDash()
    {
        var engine = new Engine();
        var range = engine.Evaluate("new Intl.DateTimeFormat('en', { hour: 'numeric', minute: 'numeric', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 11, 24, 9), Date.UTC(2022, 11, 24, 15))").AsString();
        range.Should().Be("9:00 AM\u2009\u2013\u20093:00 PM");
        range.Should().NotContain("\u202F");

        var separator = engine.Evaluate("new Intl.DateTimeFormat('en-US', { dateStyle: 'short' }).formatRangeToParts(86400000, 366 * 86400000).find(function (p) { return p.type === 'literal' && p.source === 'shared'; }).value").AsString();
        separator.Should().Be("\u2009\u2013\u2009");
        engine.Evaluate("new Intl.DateTimeFormat('en-US').formatRangeToParts(new Date(2019, 0, 3), new Date(2019, 0, 5)).find(function (p) { return p.type === 'literal' && p.source === 'shared'; }).value")
            .AsString().Should().Be(separator);
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-chinese', { dateStyle: 'medium', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 11, 24), Date.UTC(2023, 2, 6))")
            .AsString().Should().Contain(separator);
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-chinese', { dateStyle: 'medium', timeZone: 'UTC' }).formatRangeToParts(Date.UTC(2022, 11, 24), Date.UTC(2023, 2, 6)).map(function (p) { return p.value; }).join('')")
            .AsString().Should().Contain(separator);
    }

    /// <summary>
    /// A <c>dateStyle</c> or <c>timeStyle</c> range is written through the interval patterns of the style's own skeleton,
    /// as V8 creates ICU's <c>DateIntervalFormat</c> for it, rather than as two whole dates with their shared ends cut
    /// off: what Node 24.19 writes, with U+0020 for U+202F.
    /// </summary>
    [TestCase("de", "{ dateStyle: 'medium' }", "2022-12-24", "2022-12-27", "24.\u201327.12.2022",
        "day:startRange:24|literal:shared:.\u2013|day:endRange:27|literal:shared:.|month:shared:12|literal:shared:.|year:shared:2022")]
    [TestCase("en", "{ dateStyle: 'medium' }", "2022-12-24", "2022-12-27", "Dec 24\u2009\u2013\u200927, 2022",
        "month:shared:Dec|literal:shared: |day:startRange:24|literal:shared:\u2009\u2013\u2009|day:endRange:27|literal:shared:, |year:shared:2022")]
    [TestCase("en", "{ dateStyle: 'full' }", "2022-12-24", "2022-12-27", "Saturday, December 24\u2009\u2013\u2009Tuesday, December 27, 2022", null)]
    [TestCase("en", "{ dateStyle: 'short' }", "2022-12-24", "2023-03-06", "12/24/22\u2009\u2013\u20093/6/23", null)]
    [TestCase("fr", "{ dateStyle: 'long' }", "2022-12-24", "2023-02-02", "24 d\u00e9cembre 2022\u2009\u2013\u20092 f\u00e9vrier 2023", null)]
    [TestCase("ja", "{ dateStyle: 'long' }", "2022-12-24", "2022-12-27", "2022/12/24\uff5e2022/12/27", null)]
    [TestCase("en", "{ dateStyle: 'medium' }", "2022-12-24T15:07", "2022-12-24T17:42", "Dec 24, 2022",
        "month:shared:Dec|literal:shared: |day:shared:24|literal:shared:, |year:shared:2022")]
    [TestCase("en", "{ timeStyle: 'short' }", "2022-12-24T15:07", "2022-12-24T17:42", "3:07\u2009\u2013\u20095:42 PM",
        "hour:startRange:3|literal:startRange::|minute:startRange:07|literal:shared:\u2009\u2013\u2009|hour:endRange:5|literal:endRange::|minute:endRange:42|literal:shared: |dayPeriod:shared:PM")]
    [TestCase("en", "{ timeStyle: 'short' }", "2022-12-24T15:07", "2022-12-24T15:07", "3:07 PM", null)]
    [TestCase("en", "{ dateStyle: 'medium', timeStyle: 'short' }", "2022-12-24T15:07", "2022-12-24T17:42", "Dec 24, 2022, 3:07\u2009\u2013\u20095:42 PM", null)]
    [TestCase("en", "{ dateStyle: 'medium', timeStyle: 'short' }", "2022-12-24T15:07", "2022-12-27T09:04", "Dec 24, 2022, 3:07 PM\u2009\u2013\u2009Dec 27, 2022, 9:04 AM", null)]
    public void AStyleRangeIsWrittenWithTheLocalesIntervalPattern(string locale, string options, string start, string end, string expected, string? parts)
    {
        var engine = new Engine();
        var script = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options}))";
        var dates = $"new Date('{start}Z'), new Date('{end}Z')";

        engine.Evaluate($"{script}.formatRange({dates})").AsString().Should().Be(expected);
        var actualParts = engine.Evaluate($"{script}.formatRangeToParts({dates}).map(function (p) {{ return p.type + ':' + p.source + ':' + p.value; }}).join('|')").AsString();
        if (parts is not null)
        {
            actualParts.Should().Be(parts);
        }
    }

    /// <summary>
    /// A Temporal value goes through the style's interval patterns too: a <c>PlainDate</c> keeps the <c>dateStyle</c>, as
    /// a <c>Date</c> of the same days does.
    /// </summary>
    [Test]
    public void ATemporalStyleRangeIsWrittenWithTheLocalesIntervalPattern()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en', { dateStyle: 'medium' }).formatRange(Temporal.PlainDate.from('2022-12-24'), Temporal.PlainDate.from('2022-12-27'))")
            .AsString().Should().Be("Dec 24\u2009\u2013\u200927, 2022");
        engine.Evaluate("new Intl.DateTimeFormat('de', { dateStyle: 'medium' }).formatRange(Temporal.PlainDate.from('2022-12-24'), Temporal.PlainDate.from('2022-12-27'))")
            .AsString().Should().Be("24.\u201327.12.2022");
    }

    /// <summary>
    /// The years are the dates' own on either side of the common era, and outside the range a .NET DateTime can hold:
    /// a range across eras is written with the eras.
    /// </summary>
    [Test]
    public void ARangeAcrossErasWritesTheEras()
    {
        var engine = new Engine();
        engine.Evaluate("""
            (function () {
                var start = new Date(0);
                start.setUTCFullYear(-5, 10, 24);
                var end = new Date(0);
                end.setUTCFullYear(2022, 11, 24);
                return new Intl.DateTimeFormat('en', { year: 'numeric', timeZone: 'UTC' }).formatRange(start, end);
            })()
            """).AsString().Should().Be("6 BC\u2009–\u20092022 AD");

        engine.Evaluate("new Intl.DateTimeFormat('en', { year: 'numeric', month: 'numeric', day: 'numeric', timeZone: 'UTC' }).formatRange(-8.64e15, 8.64e15)")
            .AsString().Should().Be("4/20/271822 BC\u2009–\u20099/13/275760 AD");
    }

    /// <summary>
    /// A Temporal value is written with its own format record, as <c>format()</c> writes it, and its range with that
    /// record's range patterns.
    /// </summary>
    [TestCase("new Temporal.PlainDate(2021, 8, 4), new Temporal.PlainDate(2021, 8, 5)", "8/4/2021\u2009–\u20098/5/2021")]
    [TestCase("new Temporal.PlainDateTime(2021, 8, 4, 0, 30, 45), new Temporal.PlainDateTime(2021, 8, 4, 23, 30, 45)", "8/4/2021, 12:30:45 AM\u2009–\u200911:30:45 PM")]
    [TestCase("new Temporal.PlainTime(0, 30, 45), new Temporal.PlainTime(23, 30, 45)", "12:30:45 AM\u2009–\u200911:30:45 PM")]
    [TestCase("new Temporal.PlainYearMonth(2021, 8, 'gregory'), new Temporal.PlainYearMonth(2021, 9, 'gregory')", "8/2021\u2009–\u20099/2021")]
    [TestCase("new Temporal.PlainMonthDay(8, 4, 'gregory'), new Temporal.PlainMonthDay(8, 5, 'gregory')", "8/4\u2009–\u20098/5")]
    public void TemporalValuesAreWrittenWithTheirOwnRangePatterns(string values, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"new Intl.DateTimeFormat('en-US', {{ timeZone: 'Pacific/Apia' }}).formatRange({values})").AsString().Should().Be(expected);
    }

    /// <summary>
    /// The numbering system's digits are written over every field of a range, and a host provider's names that differ
    /// from the default provider's replace CLDR's there as they do in <c>format()</c>.
    /// </summary>
    [Test]
    public void ARangeWritesTheNumberingSystemAndTheHostsNames()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en-u-nu-arab', { month: 'short', day: 'numeric', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 11, 24), Date.UTC(2022, 11, 27))")
            .AsString().Should().Be("Dec ٢٤\u2009–\u2009٢٧");

        var host = new Engine(options => options.Intl.CldrProvider = new ShoutedMonths());
        host.Evaluate("new Intl.DateTimeFormat('en', { month: 'long', day: 'numeric', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 10, 24), Date.UTC(2022, 11, 3))")
            .AsString().Should().Be("NOVEMBER 24\u2009–\u2009DECEMBER 3");
    }

    /// <summary>
    /// A style's range goes through the interval patterns (<see cref="AStyleRangeIsWrittenWithTheLocalesIntervalPattern"/>);
    /// the Chinese calendar keeps the lane it had, the collapsing it wrote, joined by U+2009 EN DASH U+2009.
    /// </summary>
    [Test]
    public void StylesUseTheIntervalPatternsAndTheChineseCalendarKeepsItsLane()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 11, 24), Date.UTC(2022, 11, 27))")
            .AsString().Should().Be("Dec 24\u2009–\u200927, 2022");
        engine.Evaluate("new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeZone: 'UTC' }).formatRange(Date.UTC(2022, 10, 24), Date.UTC(2023, 11, 27))")
            .AsString().Should().Be("Nov 24, 2022\u2009–\u2009Dec 27, 2023");
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-chinese', { timeZone: 'UTC' }).formatRangeToParts(Date.UTC(2000, 0, 1), Date.UTC(1900, 0, 1)).filter(function (p) { return p.type === 'relatedYear'; }).map(function (p) { return p.source + '=' + p.value; }).join()")
            .AsString().Should().Be("startRange=1999,endRange=1899");
    }

    /// <summary>
    /// Every locale the data carries writes a range of every kind for the bags a date, a time and both make: the text
    /// is the concatenation of the parts, and a range that does not collapse has a part from each date.
    /// </summary>
    [Test]
    public void EveryLocaleWritesEveryKindOfRange()
    {
        var engine = new Engine();
        var check = engine.Evaluate("""
            (function (locale) {
                var bags = [{ month: 'short', day: 'numeric' }, { year: 'numeric', month: 'numeric', day: 'numeric' },
                            { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' },
                            { hour: 'numeric', minute: 'numeric' }, { hour: 'numeric', minute: 'numeric', hourCycle: 'h23' },
                            { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric' }];
                var pairs = [[Date.UTC(2022, 11, 24, 9, 4), Date.UTC(2022, 11, 24, 9, 42)], [Date.UTC(2022, 11, 24, 9, 4), Date.UTC(2022, 11, 24, 15, 7)],
                             [Date.UTC(2022, 11, 24, 9, 4), Date.UTC(2022, 11, 27, 15, 7)], [Date.UTC(2022, 11, 24, 9, 4), Date.UTC(2023, 2, 6, 15, 7)]];
                var problems = [];
                for (var b = 0; b < bags.length; b++) {
                    var f = new Intl.DateTimeFormat(locale, Object.assign({ timeZone: 'UTC' }, bags[b]));
                    for (var p = 0; p < pairs.length; p++) {
                        var text = f.formatRange(pairs[p][0], pairs[p][1]);
                        var parts = f.formatRangeToParts(pairs[p][0], pairs[p][1]);
                        var sources = parts.map(function (x) { return x.source; });
                        var single = sources.every(function (s) { return s === 'shared'; });
                        if (parts.map(function (x) { return x.value; }).join('') !== text
                            || parts.some(function (x) { return x.type === 'unknown'; })
                            || (!single && (sources.indexOf('startRange') < 0 || sources.indexOf('endRange') < 0))
                            || (single && text !== f.format(pairs[p][0]))) {
                            problems.push(locale + ' ' + JSON.stringify(bags[b]) + ' #' + p + ': ' + text);
                        }
                    }
                }
                return problems.join('\n');
            })
            """);

        var problems = new List<string>();
        foreach (var locale in Jint.Native.Intl.Data.DateTimePatternData.Shared.Locales)
        {
            if (engine.Invoke(check, locale).AsString() is { Length: > 0 } found)
            {
                problems.Add(found);
            }
        }

        problems.Should().BeEmpty();
    }

    private static string EncodeParts(string parts)
    {
        var builder = new StringBuilder();
        foreach (var part in parts.Split(' '))
        {
            var fields = part.Split(':');
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(TypeCodes.TryGetValue(fields[0], out var type) ? type : '?');
            builder.Append(SourceCodes.TryGetValue(fields[1], out var source) ? source : '?');
            builder.Append(fields[2]);
        }

        return builder.ToString();
    }

    private static void Check(List<string> problems, string label, string what, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            problems.Add($"{label} {what}: expected \"{expected}\", got \"{actual}\"");
        }
    }

    private sealed record GoldenRow(string Locale, string Bag, string Pair, string Text, string Parts, string Icu);

    private sealed record GoldenTable(Dictionary<string, string> Bags, Dictionary<string, (double Start, double End)> Pairs, List<GoldenRow> Rows);

    private static GoldenTable ReadGoldenTable()
    {
        using var stream = typeof(IntlDateTimeFormatRangeTests).Assembly.GetManifestResourceStream(GoldenResource)
                           ?? throw new InvalidOperationException("The golden table " + GoldenResource + " is not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var table = new GoldenTable(new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, (double, double)>(StringComparer.Ordinal), []);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var columns = line.Split('\t');
            switch (columns[0])
            {
                case "bag":
                    table.Bags.Add(columns[1], columns[2]);
                    break;
                case "pair":
                    table.Pairs.Add(columns[1], (double.Parse(columns[2], System.Globalization.CultureInfo.InvariantCulture), double.Parse(columns[3], System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                default:
                    columns.Length.Should().Be(6, line);
                    table.Rows.Add(new GoldenRow(columns[0], columns[1], columns[2], Unescape(columns[3]), columns[4], Unescape(columns[5])));
                    break;
            }
        }

        return table;
    }

    private static string Unescape(string value)
        => Regex.Replace(value, @"\\u([0-9a-f]{4})", m => ((char) Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    /// <summary>Overrides the long month names, in upper case.</summary>
    private sealed class ShoutedMonths : DefaultCldrProvider
    {
        public override string[]? GetMonthNames(string locale, string style, string? calendar)
            => string.Equals(style, "long", StringComparison.Ordinal)
                ? ["JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER"]
                : base.GetMonthNames(locale, style, calendar);
    }
}

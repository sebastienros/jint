#nullable enable

using System.Text;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Intl;
using Jint.Native.Intl.Data;

namespace Jint.Tests.Runtime;

/// <summary>
/// An <c>Intl.DateTimeFormat</c> whose calendar is not the Gregorian one, written in that calendar's own CLDR 48.2 shape:
/// its patterns, month names and eras, matched and rendered the way the Gregorian calendar is (sebastienros/jint#4204).
/// </summary>
/// <remarks>
/// <para>
/// The golden table is <c>tools/cldr-dates/reference/golden-calendars.tsv</c>, which the reference model
/// (<c>format_matcher.py golden-calendars</c>) writes from CLDR 48.2: 11 locales, 11 calendars and 21 component bags and
/// styles, each at two instants, with the pattern the model chose and what <c>resolvedOptions()</c> reports. Its last
/// column is what Node 24.19 (ICU 78.3) writes where it differs from the model; <see cref="TheGoldenTableDiffersFromIcuOnlyAsDocumented"/>
/// says why each differs.
/// </para>
/// <para>
/// Every case is formatted with <c>numberingSystem: "latn"</c>, so that a locale's own digits do not stand between the
/// matcher and the comparison.
/// </para>
/// </remarks>
public class IntlDateTimeFormatCalendarTests
{
    private const string GoldenResource = "cldr-dates-golden-calendars.tsv";

    private static readonly long[] GoldenInstants =
    [
        new DateTimeOffset(2022, 12, 24, 15, 7, 9, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        new DateTimeOffset(2023, 3, 6, 9, 4, 5, TimeSpan.Zero).ToUnixTimeMilliseconds(),
    ];

    /// <summary>
    /// The rows whose text ICU writes differently: Hebrew's date styles, which CLDR writes in Hebrew numerals
    /// (<c>numbers="hebr"</c>) and Jint, which does not carry the override, in the formatter's digits.
    /// </summary>
    private static readonly HashSet<(string Locale, string Calendar, string Case)> KnownTextDifferences =
    [
        ("he", "hebrew", "d_full"),
        ("he", "hebrew", "d_long"),
        ("he", "hebrew", "d_medium"),
        ("he", "hebrew", "d_short"),
        ("he", "hebrew", "dt_medium_short"),
        ("he", "hebrew", "dt_full_short"),
    ];

    [Test]
    public void EveryGoldenRowFormatsAsTheReferenceModel()
    {
        var engine = new Engine();
        var row = engine.Evaluate("""
            (function (locale, calendar, options, first, second) {
                var f = new Intl.DateTimeFormat(locale, Object.assign({ timeZone: 'UTC', calendar: calendar, numberingSystem: 'latn' }, JSON.parse(options)));
                var ro = f.resolvedOptions();
                var keys = ['dateStyle', 'timeStyle', 'weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName'];
                var record = keys.filter(function (k) { return ro[k] !== undefined; }).map(function (k) { return k + '=' + ro[k]; }).join(',');
                if (ro.hourCycle !== undefined) {
                    record += ',hourCycle=' + ro.hourCycle;
                }
                function joined(date) { return f.formatToParts(date).map(function (p) { return p.value; }).join(''); }
                return [f, f.format(first), f.format(second), joined(first), joined(second), record, ro.calendar];
            })
            """);

        var problems = new List<string>();
        var rows = ReadGoldenTable();
        foreach (var golden in rows)
        {
            var result = engine.Invoke(row, golden.Locale, golden.Calendar, golden.Options, GoldenInstants[0], GoldenInstants[1]).AsArray();
            var formatter = (JsDateTimeFormat) result[0];
            var first = result[1].AsString();
            var second = result[2].AsString();
            var label = $"{golden.Locale} {golden.Calendar} {golden.Case}";
            var pattern = golden.Case.StartsWith("d_", StringComparison.Ordinal) || golden.Case.StartsWith("dt_", StringComparison.Ordinal)
                ? formatter.GetStylePattern(plain: false).Pattern
                : formatter.GetComponentPattern().Pattern;

            Check(problems, label, "calendar", golden.Calendar, result[6].AsString());
            Check(problems, label, "pattern", golden.Pattern, pattern);
            Check(problems, label, "format@1", golden.Texts[0], first);
            Check(problems, label, "format@2", golden.Texts[1], second);
            Check(problems, label, "formatToParts@1", first, result[3].AsString());
            Check(problems, label, "formatToParts@2", second, result[4].AsString());
            Check(problems, label, "resolvedOptions", golden.ResolvedOptions, result[5].AsString());
        }

        problems.Should().BeEmpty();
        rows.Count.Should().Be(2541);
    }

    /// <summary>
    /// Where ICU writes anything else, the row is one of <see cref="KnownTextDifferences"/>, or ICU writes the same text and
    /// reports a format record V8 reads off the pattern differently, in one of two ways Jint follows the specification
    /// over: https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.resolvedoptions reports the fields of the format
    /// record, the chosen pattern's.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>V8 recognizes an era only in a run of three or more <c>G</c>, so the one-letter era every one of these
    /// calendars adds to a numeric date (<c>M/d/y G</c>) is not in its <c>resolvedOptions()</c>.</item>
    /// <item>V8 reads a letter inside a quoted literal as a field: the "d" of Spanish <c>'de'</c> a day, the "s" of
    /// <c>'las'</c> a second.</item>
    /// </list>
    /// </remarks>
    [Test]
    public void TheGoldenTableDiffersFromIcuOnlyAsDocumented()
    {
        var textDifferences = new HashSet<(string, string, string)>();
        var oneLetterEras = 0;
        var quotedLiterals = 0;
        var problems = new List<string>();
        foreach (var golden in ReadGoldenTable())
        {
            if (golden.Icu.Length == 0)
            {
                continue;
            }

            var label = $"{golden.Locale} {golden.Calendar} {golden.Case}";
            var icu = golden.Icu.Split('|');
            icu.Length.Should().Be(3, label);
            if (!string.Equals(icu[0], golden.Texts[0], StringComparison.Ordinal) || !string.Equals(icu[1], golden.Texts[1], StringComparison.Ordinal))
            {
                textDifferences.Add((golden.Locale, golden.Calendar, golden.Case));
                continue;
            }

            var ours = Record(golden.ResolvedOptions);
            var theirs = Record(icu[2]);
            foreach (var key in ours.Keys.Union(theirs.Keys))
            {
                ours.TryGetValue(key, out var mine);
                theirs.TryGetValue(key, out var other);
                if (string.Equals(mine, other, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(key, "era", StringComparison.Ordinal) && other is null && LongestEraRun(golden.Pattern) < 3)
                {
                    oneLetterEras++;
                }
                else if (mine is null && QuotedLetters(golden.Pattern).Any(letter => FieldOf(letter) == key))
                {
                    quotedLiterals++;
                }
                else
                {
                    problems.Add($"{label}: {key} is {mine ?? "absent"} here and {other ?? "absent"} in ICU");
                }
            }
        }

        problems.Should().BeEmpty();
        textDifferences.Should().BeEquivalentTo(KnownTextDifferences);
        oneLetterEras.Should().Be(574);
        quotedLiterals.Should().Be(22);
    }

    /// <summary>
    /// The issue's examples and one date of every calendar, as Node 24.19 writes them (<c>numberingSystem: "latn"</c>).
    /// </summary>
    [TestCase("en-u-ca-buddhist", "{}", "12/24/2565 BE", "month=12,day=24,year=2565,era=BE")]
    [TestCase("en-u-ca-buddhist", "{ dateStyle: 'long' }", "December 24, 2565 BE", "month=December,day=24,year=2565,era=BE")]
    [TestCase("th-u-ca-buddhist", "{ dateStyle: 'long' }", "24 ธันวาคม 2565", "day=24,month=ธันวาคม,year=2565")]
    [TestCase("ja-u-ca-japanese", "{ dateStyle: 'long' }", "令和4年12月24日", "era=令和,year=4,month=12,day=24")]
    [TestCase("ja-u-ca-japanese", "{}", "R4/12/24", "era=R,year=4,month=12,day=24")]
    [TestCase("ja-u-ca-japanese", "{ era: 'long', year: 'numeric' }", "令和4年", "era=令和,year=4")]
    [TestCase("en-u-ca-japanese", "{ dateStyle: 'long' }", "December 24, 4 Reiwa", "month=December,day=24,year=4,era=Reiwa")]
    [TestCase("en-u-ca-hebrew", "{ month: 'long' }", "Kislev", "month=Kislev")]
    [TestCase("en-u-ca-hebrew", "{}", "30 Kislev 5783", "day=30,month=Kislev,year=5783")]
    [TestCase("he-u-ca-hebrew", "{}", "30 בכסלו 5783", "day=30,month=כסלו,year=5783")]
    [TestCase("fa-u-ca-persian", "{ month: 'long' }", "دی", "month=دی")]
    [TestCase("fa-u-ca-persian", "{ dateStyle: 'full' }", "1401 دی 3, شنبه", "year=1401,month=دی,day=3,weekday=شنبه")]
    [TestCase("en-u-ca-persian", "{ dateStyle: 'long' }", "Dey 3, 1401 AP", "month=Dey,day=3,year=1401,era=AP")]
    [TestCase("ar-u-ca-islamic-umalqura", "{ dateStyle: 'long' }", "30 جمادى الأولى 1444 هـ", "day=30,month=جمادى الأولى,year=1444,era=هـ")]
    [TestCase("en-u-ca-islamic-civil", "{ dateStyle: 'full' }", "Saturday, Jumada I 30, 1444 AH", "weekday=Saturday,month=Jumada I,day=30,year=1444,era=AH")]
    [TestCase("am-u-ca-ethiopic", "{ dateStyle: 'long' }", "15 ታኅሣሥ 2015 AM", "day=15,month=ታኅሣሥ,year=2015,era=AM")]
    [TestCase("en-u-ca-coptic", "{ dateStyle: 'long' }", "Kiahk 15, 1739 AM", "month=Kiahk,day=15,year=1739,era=AM")]
    [TestCase("en-u-ca-ethioaa", "{ dateStyle: 'long' }", "Tahsas 15, 7515 AA", "month=Tahsas,day=15,year=7515,era=AA")]
    [TestCase("hi-u-ca-indian", "{ dateStyle: 'long' }", "3 पौष 1944 शक", "day=3,month=पौष,year=1944,era=शक")]
    [TestCase("zh-Hant-u-ca-roc", "{ dateStyle: 'long' }", "民國111年12月24日", "era=民國,year=111,month=12,day=24")]
    [TestCase("zh-Hant-u-ca-roc", "{ dateStyle: 'short', timeStyle: 'short' }", "民國111/12/24 下午3:07", "era=民國,year=111,month=12,day=24,dayPeriod=下午,hour=3,minute=07")]
    public void ACalendarWritesItsOwnShape(string locale, string options, string expected, string parts)
    {
        var engine = new Engine();
        var formatter = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC', numberingSystem: 'latn' }}, {options}))";

        engine.Evaluate($"{formatter}.format(Date.UTC(2022, 11, 24, 15, 7, 9))").AsString().Should().Be(expected);
        engine.Evaluate($"{formatter}.formatToParts(Date.UTC(2022, 11, 24, 15, 7, 9)).filter(function (p) {{ return p.type !== 'literal'; }}).map(function (p) {{ return p.type + '=' + p.value; }}).join()")
            .AsString().Should().Be(parts);
    }

    /// <summary>
    /// A Hebrew month is named by CLDR's key for it, which in a leap year has Adar I and Adar II; and its number is its
    /// place in its year, so Adar is the sixth month of a common year.
    /// </summary>
    [TestCase("{ month: 'long', day: 'numeric' }", 2024, 3, 15, "5 Adar II")]
    [TestCase("{ month: 'long', day: 'numeric' }", 2024, 2, 15, "6 Adar I")]
    [TestCase("{ month: 'numeric', day: 'numeric' }", 2024, 3, 15, "5 Adar II")]
    [TestCase("{ year: 'numeric', month: 'long' }", 2023, 3, 6, "Adar 5783")]
    [TestCase("{ month: 'numeric' }", 2023, 3, 6, "6")]
    public void AHebrewMonthIsNamedForItsYear(string options, int year, int month, int day, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"new Intl.DateTimeFormat('en-u-ca-hebrew', Object.assign({{ timeZone: 'UTC' }}, {options})).format(Date.UTC({year}, {month - 1}, {day}))")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// A Japanese date is in the era of its day, Meiji to Reiwa. CLDR names 232 eras before Meiji, which the data does
    /// not carry: an earlier date is written in its Gregorian year, with the locale's Gregorian era where ICU writes 嘉永.
    /// </summary>
    [TestCase("ja", "{}", 2019, 4, 30, "H31/4/30")]
    [TestCase("ja", "{}", 2019, 5, 1, "R1/5/1")]
    [TestCase("en", "{ dateStyle: 'medium' }", 1989, 1, 7, "Jan 7, 64 Shōwa")]
    [TestCase("en", "{ dateStyle: 'medium' }", 1989, 1, 8, "Jan 8, 1 Heisei")]
    [TestCase("ja", "{}", 1850, 6, 1, "AD1850/6/1")]
    [TestCase("en", "{ era: 'short', year: 'numeric' }", 1850, 6, 1, "1850 AD")]
    public void AJapaneseDateIsInTheEraOfItsDay(string locale, string options, int year, int month, int day, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"new Intl.DateTimeFormat('{locale}-u-ca-japanese', Object.assign({{ timeZone: 'UTC' }}, {options})).format(Date.UTC({year}, {month - 1}, {day}))")
            .AsString().Should().Be(expected);
    }

    [Test]
    public void ARepublicOfChinaYearBefore1912IsInTheEraBeforeIt()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-roc', { era: 'long', year: 'numeric', timeZone: 'UTC' }).format(Date.UTC(1900, 0, 1))")
            .AsString().Should().Be("12 B.R.O.C.");
    }

    /// <summary>
    /// A calendar's ranges are written with its own interval patterns and its own fields, as Node 24.19 writes them, a
    /// <c>dateStyle</c>/<c>timeStyle</c> range (#4247) as well as a component bag's.
    /// </summary>
    [TestCase("en-u-ca-buddhist", "{ year: 'numeric', month: 'short', day: 'numeric' }", "2022-12-24T00:00Z", "2022-12-27T00:00Z",
        "Dec 24\u2009–\u200927, 2565 BE",
        "month:shared=Dec|literal:shared= |day:startRange=24|literal:shared=\u2009–\u2009|day:endRange=27|literal:shared=, |year:shared=2565|literal:shared= |era:shared=BE")]
    [TestCase("en-u-ca-hebrew", "{ year: 'numeric', month: 'long', day: 'numeric' }", "2022-12-24T00:00Z", "2023-03-06T00:00Z",
        "30 Kislev\u2009–\u200913 Adar 5783",
        "day:startRange=30|literal:startRange= |month:startRange=Kislev|literal:shared=\u2009–\u2009|day:endRange=13|literal:endRange= |month:endRange=Adar|literal:shared= |year:shared=5783")]
    [TestCase("ja-u-ca-japanese", "{ year: 'numeric', month: 'numeric', day: 'numeric' }", "2019-04-30T00:00Z", "2019-05-01T00:00Z",
        "H31/04/30～R1/05/01",
        "era:startRange=H|year:startRange=31|literal:startRange=/|month:startRange=04|literal:startRange=/|day:startRange=30|literal:shared=～|era:endRange=R|year:endRange=1|literal:endRange=/|month:endRange=05|literal:endRange=/|day:endRange=01")]
    [TestCase("en-u-ca-persian", "{ year: 'numeric', month: 'long', day: 'numeric' }", "2022-12-24T00:00Z", "2022-12-30T00:00Z",
        "Dey 3\u2009–\u20099, 1401 AP",
        "month:shared=Dey|literal:shared= |day:startRange=3|literal:shared=\u2009–\u2009|day:endRange=9|literal:shared=, |year:shared=1401|literal:shared= |era:shared=AP")]
    [TestCase("en-u-ca-buddhist", "{ dateStyle: 'medium' }", "2022-12-24T00:00Z", "2022-12-27T00:00Z",
        "Dec 24\u2009–\u200927, 2565 BE",
        "month:shared=Dec|literal:shared= |day:startRange=24|literal:shared=\u2009–\u2009|day:endRange=27|literal:shared=, |year:shared=2565|literal:shared= |era:shared=BE")]
    [TestCase("th-u-ca-buddhist", "{ dateStyle: 'medium' }", "2022-12-24T00:00Z", "2023-01-03T00:00Z",
        "24 ธ.ค. 2565 – 3 ม.ค. 2566",
        "day:startRange=24|literal:startRange= |month:startRange=ธ.ค.|literal:startRange= |year:startRange=2565|literal:shared= – |day:endRange=3|literal:endRange= |month:endRange=ม.ค.|literal:endRange= |year:endRange=2566")]
    [TestCase("de-u-ca-hebrew", "{ dateStyle: 'long' }", "2022-12-24T00:00Z", "2023-03-06T00:00Z",
        "AM 5783 Kislew 30\u2009–\u2009Adar 13",
        "era:shared=AM|literal:shared= |year:shared=5783|literal:shared= |month:startRange=Kislew|literal:startRange= |day:startRange=30|literal:shared=\u2009–\u2009|month:endRange=Adar|literal:endRange= |day:endRange=13")]
    [TestCase("en-u-ca-islamic-civil", "{ dateStyle: 'full' }", "2022-12-24T00:00Z", "2022-12-27T00:00Z",
        "Saturday, Jumada I 30\u2009–\u2009Tuesday, Jumada II 3, 1444 AH",
        "weekday:startRange=Saturday|literal:startRange=, |month:startRange=Jumada I|literal:startRange= |day:startRange=30|literal:shared=\u2009–\u2009|weekday:endRange=Tuesday|literal:endRange=, |month:endRange=Jumada II|literal:endRange= |day:endRange=3|literal:shared=, |year:shared=1444|literal:shared= |era:shared=AH")]
    [TestCase("zh-Hant-u-ca-roc", "{ dateStyle: 'short', timeStyle: 'short' }", "2022-12-24T06:05Z", "2022-12-24T19:45Z",
        "民國111/12/24 6:05 清晨\u2009–\u20097:45 晚上",
        "era:shared=民國|year:shared=111|literal:shared=/|month:shared=12|literal:shared=/|day:shared=24|literal:shared= |hour:startRange=6|literal:startRange=:|minute:startRange=05|literal:startRange= |dayPeriod:startRange=清晨|literal:shared=\u2009–\u2009|hour:endRange=7|literal:endRange=:|minute:endRange=45|literal:endRange= |dayPeriod:endRange=晚上")]
    public void ACalendarWritesItsOwnRanges(string locale, string options, string start, string end, string expected, string parts)
    {
        var engine = new Engine();
        var formatter = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options}))";
        var range = $"new Date('{start}'), new Date('{end}')";

        engine.Evaluate($"{formatter}.formatRange({range})").AsString().Should().Be(expected);
        engine.Evaluate($"{formatter}.formatRangeToParts({range}).map(function (p) {{ return p.type + ':' + p.source + '=' + p.value; }}).join('|')")
            .AsString().Should().Be(parts);
    }

    /// <summary>
    /// ICU keeps a calendar's date, time and joining patterns in one array that a locale has only where it defines one of
    /// them, so British English writes a Buddhist hour with en-001's twelve-hour time patterns among its candidates, and an
    /// hour asked for in its own 24-hour cycle comes out of the availableFormats with two digits, where its Gregorian one
    /// has one.
    /// </summary>
    [Test]
    public void ACalendarsTimeStylesAreTheOnesItsDatePatternsComeWith()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en-GB-u-ca-buddhist', { hour: 'numeric', minute: '2-digit', timeZone: 'UTC' }).format(Date.UTC(2023, 2, 6, 9, 4))")
            .AsString().Should().Be("09:04");
        engine.Evaluate("new Intl.DateTimeFormat('en-GB', { hour: 'numeric', minute: '2-digit', timeZone: 'UTC' }).format(Date.UTC(2023, 2, 6, 9, 4))")
            .AsString().Should().Be("9:04");
    }

    /// <summary>
    /// ICU adds a calendar's own availableFormats entries from every level of the chain before the generic calendar's it
    /// inherits, so British English's Hebrew weekday, day and long month take the comma of <c>en</c>'s Hebrew
    /// <c>MMMEd</c>, which ties with its own generic <c>MMMMEEEEd</c>.
    /// </summary>
    [Test]
    public void ACalendarsOwnEntriesWinItsTies()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en-GB-u-ca-hebrew', { weekday: 'short', month: 'long', day: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Sat, 30 Kislev");
        engine.Evaluate("new Intl.DateTimeFormat('en-GB', { weekday: 'short', month: 'long', day: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Sat 24 December");
    }

    /// <summary>
    /// A host provider that answers a calendar's month names still names its months, by the calendar's month number as
    /// <see cref="ICldrProvider.GetMonthNames"/> promises; one that does not leaves CLDR's.
    /// </summary>
    [Test]
    public void AHostsCalendarMonthNamesStillWin()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new HebrewMonthNumbers());
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-hebrew', { month: 'long', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Month 3");
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-persian', { month: 'long', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Dey");
    }

    private static Dictionary<string, string> Record(string record)
        => record.Split([','], StringSplitOptions.RemoveEmptyEntries).Select(f => f.Split('=')).ToDictionary(f => f[0], f => f[1], StringComparer.Ordinal);

    /// <summary>The longest run of <c>G</c> outside the pattern's quoted literals.</summary>
    private static int LongestEraRun(string pattern)
        => PatternToken.Tokenize(pattern).Where(t => t.IsField && t.Letter == 'G').Select(t => t.Length).DefaultIfEmpty(0).Max();

    /// <summary>The letters inside the pattern's quoted literals.</summary>
    private static HashSet<char> QuotedLetters(string pattern)
    {
        var letters = new HashSet<char>();
        var quoted = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                {
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (quoted && char.IsAsciiLetter(c))
            {
                letters.Add(c);
            }
        }

        return letters;
    }

    /// <summary>The <c>resolvedOptions()</c> property V8 reads a pattern letter as.</summary>
    private static string? FieldOf(char letter) => letter switch
    {
        'd' => "day",
        's' => "second",
        'm' => "minute",
        'h' or 'H' or 'k' or 'K' => "hour",
        'v' or 'z' => "timeZoneName",
        'b' or 'B' => "dayPeriod",
        _ => null,
    };

    private static void Check(List<string> problems, string label, string what, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            problems.Add($"{label} {what}: expected \"{expected}\", got \"{actual}\"");
        }
    }

    private sealed record GoldenRow(string Locale, string Calendar, string Case, string Options, string Pattern, string[] Texts, string ResolvedOptions, string Icu);

    private static List<GoldenRow> ReadGoldenTable()
    {
        using var stream = typeof(IntlDateTimeFormatCalendarTests).Assembly.GetManifestResourceStream(GoldenResource)
                           ?? throw new InvalidOperationException("The golden table " + GoldenResource + " is not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var rows = new List<GoldenRow>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var columns = line.Split('\t');
            columns.Length.Should().Be(9, line);
            rows.Add(new GoldenRow(
                columns[0],
                columns[1],
                columns[2],
                columns[3],
                Unescape(columns[4]),
                [Unescape(columns[5]), Unescape(columns[6])],
                columns[7],
                Unescape(columns[8])));
        }

        return rows;
    }

    private static string Unescape(string value)
        => Regex.Replace(value, @"\\u([0-9a-f]{4})", m => ((char) Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    /// <summary>Names the Hebrew months by number, and answers nothing for any other calendar.</summary>
    private sealed class HebrewMonthNumbers : DefaultCldrProvider
    {
        public override string[]? GetMonthNames(string locale, string style, string? calendar)
            => string.Equals(calendar, "hebrew", StringComparison.Ordinal)
                ? Enumerable.Range(1, 13).Select(m => "Month " + m.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray()
                : base.GetMonthNames(locale, style, calendar);
    }
}

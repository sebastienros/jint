#nullable enable

using System.Text;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Intl;
using Jint.Native.Intl.Data;

namespace Jint.Tests.Runtime;

/// <summary>
/// An <c>Intl.DateTimeFormat</c> component bag resolved through the format matcher (<see cref="DateTimePatternGenerator"/>)
/// over CLDR 48.2, and the one renderer <c>format()</c> and <c>formatToParts()</c> share (sebastienros/jint#4158).
/// </summary>
/// <remarks>
/// <para>
/// The golden table is <c>tools/cldr-dates/reference/golden.tsv</c>, read from where the reference model wrote it:
/// 51 locales and 25 bags, each at two instants, the pattern the model chose and the format record
/// <c>resolvedOptions()</c> reports. The model — <c>tools/cldr-dates/reference/format_matcher.py</c>, a model of ICU's
/// <c>DateTimePatternGenerator</c> as V8 drives it — writes the table from CLDR 48.2 itself rather than from Node's
/// output, so a difference between CLDR 48.0 (Node's) and 48.2 (Jint's) cannot sit in it. Its last column is what
/// Node 24.19 (ICU 78.3) writes where it differs from the model; <see cref="KnownIcuDifferences"/> is that list,
/// explained.
/// </para>
/// <para>
/// The bags are formatted with <c>calendar: "gregory"</c> and <c>numberingSystem: "latn"</c>, so that a locale whose
/// own calendar or digits are another (<c>th</c>, <c>fa</c>, <c>bn</c>) is compared on its patterns.
/// </para>
/// </remarks>
public class IntlDateTimeFormatMatcherTests
{
    private const string GoldenResource = "cldr-dates-golden.tsv";

    private static readonly long[] GoldenInstants =
    [
        new DateTimeOffset(2022, 12, 24, 15, 7, 9, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        new DateTimeOffset(2023, 3, 6, 9, 4, 5, TimeSpan.Zero).ToUnixTimeMilliseconds(),
    ];

    /// <summary>
    /// Every row where ICU's answer differs from the golden table's, and why. None differs in text; each differs in
    /// what <c>resolvedOptions()</c> reports, and Jint follows the specification rather than V8 in both kinds.
    /// </summary>
    private static readonly Dictionary<(string Locale, string Bag), string> KnownIcuDifferences = new()
    {
        // V8 reads resolvedOptions() off the pattern without taking its quoted literals out, so the "d" of 'de' or
        // 'del' is a day, the "m" of 'm'. or 'năm' a minute, the "s" of 'les', 'las' or 'às' a second, the "v" of 'v'
        // a time zone, the "b" of 'ob' a day period and the "h" of 'tháng' an hour. The format record of
        // https://tc39.es/ecma402/#sec-datetimeformat-format-record has only the pattern's fields.
        [("ca", "y_MMMM")] = "quoted 'del' read as a day",
        [("ca", "y_MMM")] = "quoted 'del' read as a day",
        [("ca", "y_MMMM_d_j_mm")] = "quoted 'les' read as a second",
        [("cs", "y_MMMM_d_j_mm")] = "quoted 'v' read as a time zone",
        [("es", "y_MMMM")] = "quoted 'de' read as a day",
        [("es", "y_MMMM_d_j_mm")] = "quoted 'las' read as a second",
        [("es-MX", "y_MMMM")] = "quoted 'de' read as a day",
        [("es-MX", "y_MMMM_d_j_mm")] = "quoted 'las' read as a second",
        [("lt", "EEEE_y_MMMM_d")] = "quoted 'm' read as a minute",
        [("lt", "y_MMMM")] = "quoted 'm' read as a minute",
        [("lt", "G_y")] = "quoted 'm' read as a minute",
        [("lt", "GGGG_y_MMMM_d")] = "quoted 'm' read as a minute",
        [("pt", "y_MMMM")] = "quoted 'de' read as a day",
        [("pt", "y_MMM")] = "quoted 'de' read as a day",
        [("pt", "y_MMMM_d_j_mm")] = "quoted 'às' read as a second",
        [("pt-PT", "y_MMMM")] = "quoted 'de' read as a day",
        [("pt-PT", "y_MMMM_d_j_mm")] = "quoted 'às' read as a second",
        [("sl", "y_MMMM_d_j_mm")] = "quoted 'ob' read as a day period",
        [("vi", "y_MMMM")] = "quoted 'năm' read as a minute",
        [("vi", "y_MM")] = "quoted 'tháng' read as an hour",

        // test262's intl402/DateTimeFormat/prototype/resolvedOptions/hourCycle-default.js puts Japanese with
        // hour12: true on [[hourCycle12]], the first 12-hour cycle CLDR allows for JP, which is h11; V8 answers h12.
        [("ja", "h_mm_h12")] = "hour12: true resolves to h11, as test262 expects",
    };

    [Test]
    public void EveryGoldenRowFormatsAsTheReferenceModel()
    {
        var engine = new Engine();
        var row = engine.Evaluate("""
            (function (locale, options, first, second) {
                var f = new Intl.DateTimeFormat(locale, Object.assign({ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, JSON.parse(options)));
                var ro = f.resolvedOptions();
                var keys = ['weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName'];
                var record = keys.filter(function (k) { return ro[k] !== undefined; }).map(function (k) { return k + '=' + ro[k]; }).join(',');
                if (ro.hour !== undefined) {
                    record += ',hourCycle=' + ro.hourCycle;
                }
                function joined(date) { return f.formatToParts(date).map(function (p) { return p.value; }).join(''); }
                return [f, f.format(first), f.format(second), joined(first), joined(second), record];
            })
            """);

        var problems = new List<string>();
        var rows = ReadGoldenTable();
        foreach (var golden in rows)
        {
            var result = engine.Invoke(row, golden.Locale, golden.Options, GoldenInstants[0], GoldenInstants[1]).AsArray();
            var formatter = (JsDateTimeFormat) result[0];
            var first = result[1].AsString();
            var second = result[2].AsString();
            var label = $"{golden.Locale} {golden.Bag}";

            Check(problems, label, "pattern", golden.Pattern, formatter.GetComponentPattern().Pattern);
            Check(problems, label, "format@1", golden.Texts[0], first);
            Check(problems, label, "format@2", golden.Texts[1], second);
            Check(problems, label, "formatToParts@1", first, result[3].AsString());
            Check(problems, label, "formatToParts@2", second, result[4].AsString());
            Check(problems, label, "resolvedOptions", golden.ResolvedOptions, result[5].AsString());
        }

        problems.Should().BeEmpty();
        rows.Count.Should().Be(1275);
    }

    [Test]
    public void TheGoldenTableDiffersFromIcuOnlyWhereDocumented()
    {
        var differing = new Dictionary<(string, string), string>();
        foreach (var golden in ReadGoldenTable())
        {
            if (golden.Icu.Length == 0)
            {
                continue;
            }

            differing.Add((golden.Locale, golden.Bag), golden.Icu);

            // ICU writes the same text; only what it reports differs.
            var icu = golden.Icu.Split('|');
            icu.Length.Should().Be(3);
            icu[0].Should().Be(golden.Texts[0], $"{golden.Locale} {golden.Bag}");
            icu[1].Should().Be(golden.Texts[1], $"{golden.Locale} {golden.Bag}");
            icu[2].Should().NotBe(golden.ResolvedOptions);
        }

        differing.Keys.Should().BeEquivalentTo(KnownIcuDifferences.Keys);
    }

    /// <summary>
    /// The issue's table (sebastienros/jint#4158) and the design's rows for the other scripts, with each locale's own
    /// calendar and digits: what Node 24.19 writes.
    /// </summary>
    [TestCase("de", "{ weekday: 'short', day: 'numeric', month: 'long' }", "Sa., 24. Dezember")]
    [TestCase("en", "{ weekday: 'short', day: 'numeric', month: 'long' }", "Sat, December 24")]
    [TestCase("de", "{ weekday: 'long', day: 'numeric', month: 'long' }", "Samstag, 24. Dezember")]
    [TestCase("en", "{ year: 'numeric', month: 'long' }", "December 2022")]
    [TestCase("en-GB", "{ weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' }", "Sat, 24 Dec 2022")]
    [TestCase("es", "{ month: 'long', day: 'numeric' }", "24 de diciembre")]
    [TestCase("ru", "{ weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' }", "сб, 24 дек. 2022 г.")]
    [TestCase("fr", "{ year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric' }", "24/12/2022 15:07:09")]
    [TestCase("ja", "{ weekday: 'short', month: 'long', day: 'numeric' }", "12月24日(土)")]
    [TestCase("ja", "{ day: 'numeric' }", "24日")]
    [TestCase("ja", "{ year: 'numeric', month: 'long', day: 'numeric' }", "2022年12月24日")]
    [TestCase("zh", "{ month: 'long', day: 'numeric' }", "12月24日")]
    [TestCase("zh", "{ year: 'numeric', month: '2-digit' }", "2022年12月")]
    [TestCase("ko", "{ year: 'numeric', month: 'numeric', day: 'numeric' }", "2022. 12. 24.")]
    [TestCase("ko", "{ hour: 'numeric', minute: 'numeric' }", "오후 3:07")]
    [TestCase("ar", "{ weekday: 'short', month: 'long', day: 'numeric' }", "السبت، 24 ديسمبر")]
    [TestCase("de", "{ era: 'short', year: 'numeric' }", "2022 n. Chr.")]
    [TestCase("ja", "{ era: 'short', year: 'numeric' }", "西暦2022年")]
    [TestCase("en", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "December 24, 2022 at 3:07 PM")]
    [TestCase("de", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }", "24. Dezember 2022 um 15:07")]
    [TestCase("en", "{ weekday: 'short', hour: 'numeric', minute: 'numeric' }", "Sat 3:07 PM")]
    [TestCase("en", "{ weekday: 'narrow', month: 'narrow', day: 'numeric' }", "S, D 24")]
    [TestCase("en", "{ era: 'narrow', year: 'numeric' }", "2022 A")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric', second: 'numeric', fractionalSecondDigits: 3 }", "3:07:09.123 PM")]
    [TestCase("en", "{ hour: 'numeric', dayPeriod: 'short' }", "3 in the afternoon")]
    [TestCase("en", "{ dayPeriod: 'long' }", "in the afternoon")]
    [TestCase("de", "{ hour: 'numeric', dayPeriod: 'short' }", "15 Uhr")]
    [TestCase("en", "{ timeZoneName: 'short' }", "12/24/2022, UTC")]
    [TestCase("en", "{ year: 'numeric', timeZoneName: 'long' }", "2022, Coordinated Universal Time")]
    [TestCase("en", "{ minute: 'numeric' }", "7")]
    [TestCase("en", "{ hour: '2-digit', hourCycle: 'h12' }", "03 PM")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric', hourCycle: 'h24' }", "15:07")]
    [TestCase("en-GB", "{ month: 'numeric', day: 'numeric' }", "24/12")]
    [TestCase("ja-u-nu-arab", "{ month: 'short' }", "١٢月")]
    public void AComponentBagWritesTheLocalesMatchedPattern(string locale, string options, string expected)
    {
        var engine = new Engine();
        var script = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options}))";

        engine.Evaluate($"{script}.format(Date.UTC(2022, 11, 24, 15, 7, 9, 123))").AsString().Should().Be(expected);
        engine.Evaluate($"{script}.formatToParts(Date.UTC(2022, 11, 24, 15, 7, 9, 123)).map(function (p) {{ return p.value; }}).join('')")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.resolvedoptions reads a component bag's fields off the
    /// format record, the pattern the matcher chose, not off the options that asked for it.
    /// </summary>
    [TestCase("en-GB", "{ month: 'numeric', day: 'numeric' }", "month=2-digit,day=2-digit")]
    [TestCase("ja", "{ month: 'long' }", "month=numeric")]
    [TestCase("de", "{ hour: 'numeric' }", "hour=2-digit,hourCycle=h23")]
    [TestCase("en", "{ hour: 'numeric', minute: 'numeric', second: 'numeric' }", "hour=numeric,minute=2-digit,second=2-digit,hourCycle=h12")]
    [TestCase("fr", "{ year: 'numeric', month: 'numeric', day: 'numeric' }", "year=numeric,month=2-digit,day=2-digit")]
    [TestCase("es", "{ year: 'numeric', month: 'long' }", "year=numeric,month=long")]
    [TestCase("de", "{ hour: 'numeric', dayPeriod: 'short' }", "hour=2-digit,hourCycle=h23")]
    [TestCase("en", "{ hour: 'numeric', dayPeriod: 'short' }", "dayPeriod=short,hour=numeric,hourCycle=h12")]
    [TestCase("de", "{ hour: 'numeric', minute: 'numeric', timeZoneName: 'shortOffset' }", "hour=2-digit,minute=2-digit,timeZoneName=shortOffset,hourCycle=h23")]
    [TestCase("en", "{ second: 'numeric', fractionalSecondDigits: 2 }", "second=numeric,fractionalSecondDigits=2")]
    public void ResolvedOptionsReportTheChosenPattern(string locale, string options, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($$"""
            (function () {
                var ro = new Intl.DateTimeFormat('{{locale}}', Object.assign({ timeZone: 'UTC' }, {{options}})).resolvedOptions();
                var keys = ['weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName'];
                var record = keys.filter(function (k) { return ro[k] !== undefined; }).map(function (k) { return k + '=' + ro[k]; }).join(',');
                return ro.hourCycle === undefined ? record : record + ',hourCycle=' + ro.hourCycle;
            })()
            """).AsString().Should().Be(expected);
    }

    /// <summary>
    /// <c>formatMatcher: "basic"</c> is read and validated, and answered by the best-fit matcher, as V8 answers it:
    /// https://tc39.es/ecma402/#sec-basicformatmatcher over CLDR's availableFormats as they are disagrees with ICU on
    /// about half of the bags.
    /// </summary>
    [TestCase("de", "{ weekday: 'short', day: 'numeric', month: 'long' }")]
    [TestCase("ja", "{ year: 'numeric', month: 'long', day: 'numeric', hour: 'numeric', minute: 'numeric' }")]
    [TestCase("en", "{ hour: 'numeric', minute: '2-digit', second: '2-digit' }")]
    public void TheBasicFormatMatcherIsAnsweredByBestFit(string locale, string options)
    {
        var engine = new Engine();
        string Format(string matcher)
            => engine.Evaluate($"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC', formatMatcher: '{matcher}' }}, {options})).format(Date.UTC(2022, 11, 24, 15, 7, 9))").AsString();

        Format("basic").Should().Be(Format("best fit"));
        engine.Evaluate("(function () { try { new Intl.DateTimeFormat('en', { formatMatcher: 'nope' }); } catch (e) { return e.constructor.name; } })()")
            .AsString().Should().Be("RangeError");
    }

    /// <summary>
    /// A year of zero or less is written <c>1 - year</c> (https://tc39.es/ecma402/#sec-formatdatetimepattern step
    /// 15.f.ii), with or without an era beside it, and the era is the one before the common era.
    /// </summary>
    [TestCase(0, "{ year: 'numeric' }", "1")]
    [TestCase(-1, "{ year: 'numeric' }", "2")]
    [TestCase(-5, "{ year: '2-digit' }", "06")]
    [TestCase(0, "{ era: 'short', year: 'numeric' }", "1 BC")]
    [TestCase(-1, "{ era: 'long', year: 'numeric' }", "2 Before Christ")]
    [TestCase(1, "{ era: 'short', year: 'numeric' }", "1 AD")]
    public void AYearOfZeroOrLessIsWrittenOneMinusTheYear(int year, string options, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($$"""
            (function () {
                var d = new Date(0);
                d.setUTCFullYear({{year}}, 5, 15);
                var f = new Intl.DateTimeFormat('en', Object.assign({ timeZone: 'UTC' }, {{options}}));
                var joined = f.formatToParts(d).map(function (p) { return p.value; }).join('');
                return f.format(d) === joined ? joined : 'format() ' + f.format(d) + ' is not formatToParts() ' + joined;
            })()
            """).AsString().Should().Be(expected);
    }

    /// <summary>
    /// CLDR 42 and later write U+202F in time patterns; every lane writes a plain space, in <c>format()</c> and in
    /// <c>formatToParts()</c> alike, so the one is always the concatenation of the other.
    /// </summary>
    [Test]
    public void TheNarrowNoBreakSpaceIsAPlainSpaceInEveryPart()
    {
        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en', { hour: 'numeric', minute: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24, 15, 7))")
            .AsString().Should().Be("3:07 PM");
        engine.Evaluate("new Intl.DateTimeFormat('en', { hour: 'numeric', minute: 'numeric', timeZone: 'UTC' }).formatToParts(Date.UTC(2022, 11, 24, 15, 7))[3].value")
            .AsString().Should().Be(" ");

        // es names its narrow day periods with one: "p. m." as CLDR writes it, with the space made plain.
        engine.Evaluate("new Intl.DateTimeFormat('es', { hour: 'numeric', hourCycle: 'h12', timeZone: 'UTC' }).formatToParts(Date.UTC(2022, 11, 24, 15, 7)).map(function (p) { return p.value; }).join('')")
            .AsString().Should().NotContain(" ");
    }

    /// <summary>
    /// A host's names win only where they differ from what <see cref="DefaultCldrProvider.Instance"/> answers: a
    /// provider that overrides something else, or that implements the interface and delegates the names (as the
    /// test262 harness's does), keeps CLDR's format-context names — German's "Sa." rather than .NET's stand-alone "Sa".
    /// </summary>
    [Test]
    public void AProviderAnsweringAsTheDefaultKeepsTheCldrNames()
    {
        foreach (var provider in new ICldrProvider[] { new UnrelatedOverride(), new DelegatingProvider() })
        {
            var engine = new Engine(options => options.Intl.CldrProvider = provider);
            engine.Evaluate("new Intl.DateTimeFormat('de', { weekday: 'short', day: 'numeric', month: 'long', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
                .AsString().Should().Be("Sa., 24. Dezember");
            engine.Evaluate("new Intl.DateTimeFormat('de', { era: 'short', year: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
                .AsString().Should().Be("2022 n. Chr.");
        }
    }

    /// <summary>
    /// A host's names that differ from the default's are written in the format and the stand-alone context alike.
    /// </summary>
    [Test]
    public void AHostsOwnNamesWinInBothContexts()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new ShoutedGermanNames());

        // stand-alone: "cccc" and "LLLL"
        engine.Evaluate("new Intl.DateTimeFormat('de', { weekday: 'long', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("SAMSTAG");
        engine.Evaluate("new Intl.DateTimeFormat('de', { month: 'long', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("DEZEMBER");

        // format context: "EEEE, d. MMMM"
        engine.Evaluate("new Intl.DateTimeFormat('de', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("SAMSTAG, 24. DEZEMBER");

        // the widths the host does not answer differently for stay CLDR's
        engine.Evaluate("new Intl.DateTimeFormat('de', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Sa., 24. Dez.");
    }

    /// <summary>
    /// A calendar that counts the Gregorian months writes them in the Gregorian shape with its own year; one that
    /// counts months of its own writes its own month number and day; the Chinese and Dangi calendars keep the lane
    /// that writes a related year and a year name.
    /// </summary>
    [Test]
    public void OtherCalendarsWriteTheirOwnFields()
    {
        var engine = new Engine();
        string Parts(string locale, string options)
            => engine.Evaluate($"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options})).formatToParts(Date.UTC(2022, 11, 24)).filter(function (p) {{ return p.type !== 'literal'; }}).map(function (p) {{ return p.type + '=' + p.value; }}).join()").AsString();

        Parts("en-u-ca-buddhist", "{ year: 'numeric', month: 'long', day: 'numeric' }").Should().Be("month=December,day=24,year=2565");
        Parts("en-u-ca-hebrew", "{ year: 'numeric', month: 'numeric', day: 'numeric' }").Should().Be("month=3,day=30,year=5783");
        Parts("zh-u-ca-chinese", "{ year: 'numeric', month: 'numeric', day: 'numeric' }").Should().Contain("relatedYear=2022");
        engine.Evaluate("new Intl.DateTimeFormat('en-u-ca-chinese', { year: 'numeric', month: 'long', timeZone: 'UTC' }).resolvedOptions().month")
            .AsString().Should().Be("long");
    }

    /// <summary>
    /// ICU adds a locale's own availableFormats before those it inherits and gives a tie to the first added, which is
    /// why Australian English writes no comma after an abbreviated weekday beside a long month: its own
    /// <c>MMMMEEEEd</c> ties with the <c>MMMEd</c> it inherits from <c>en-001</c>, and comes first.
    /// </summary>
    [Test]
    public void ATieGoesToTheEntryTheLocaleHoldsItself()
    {
        var enAu = DateTimePatternData.Shared.GetLocale("en-AU");
        enAu.Parent!.Locale.Should().Be("en-001");
        enAu.OwnFormats.ToArray().Select(e => e.Skeleton).Should().Contain("MMMMEEEEd").And.NotContain("MMMEd");
        enAu.Parent.OwnFormats.ToArray().Select(e => e.Skeleton).Should().Contain("MMMEd");

        var engine = new Engine();
        engine.Evaluate("new Intl.DateTimeFormat('en-AU', { weekday: 'short', month: 'long', day: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Sat 24 December");
        engine.Evaluate("new Intl.DateTimeFormat('en-NZ', { weekday: 'short', month: 'long', day: 'numeric', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("Sat, 24 December");
    }

    /// <summary>
    /// Every locale the data carries resolves a pattern for each of the bags https://tc39.es/ecma402/#sec-intl.datetimeformat-internal-slots
    /// requires [[formats]] to cover, and the pattern has exactly the fields asked for.
    /// </summary>
    [Test]
    public void EveryLocaleResolvesEveryRequiredSubset()
    {
        (string Skeleton, string Fields)[] subsets =
        [
            ("EEEEyMMMMdhms", "EyMdhms"), ("EEEEyMMMMd", "EyMd"), ("yMMMMd", "yMd"), ("yMMMM", "yM"), ("MMMMd", "Md"),
            ("hms", "hms"), ("hm", "hm"), ("Hms", "Hms"), ("Hm", "Hm"),
        ];

        var problems = new List<string>();
        foreach (var locale in DateTimePatternData.Shared.Locales)
        {
            var generator = DateTimePatternGenerator.ForLocale(locale);
            foreach (var (skeleton, fields) in subsets)
            {
                var hourCycle = skeleton.Contains('H') ? "h23" : "h12";
                var pattern = generator.GetPattern(skeleton, hourCycle, '.');
                var found = FieldLetters(pattern.Pattern);
                if (!string.Equals(found, fields, StringComparison.Ordinal))
                {
                    problems.Add($"{locale} {skeleton}: {pattern.Pattern} has {found}");
                }
            }
        }

        problems.Should().BeEmpty();
    }

    private static string FieldLetters(string pattern)
    {
        var letters = new SortedSet<char>();
        foreach (var token in PatternToken.Tokenize(pattern))
        {
            if (token.IsField)
            {
                letters.Add(token.Letter switch
                {
                    'L' => 'M',
                    'c' or 'e' => 'E',
                    'k' or 'K' => token.Letter == 'k' ? 'H' : 'h',
                    _ => token.Letter,
                });
            }
        }

        // The implied day period of a 12-hour pattern is not a field that was asked for.
        letters.Remove('a');
        letters.Remove('b');
        letters.Remove('B');
        var order = "EyMdhHms";
        return new string(order.Where(letters.Contains).ToArray());
    }

    private static void Check(List<string> problems, string label, string what, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            problems.Add($"{label} {what}: expected \"{expected}\", got \"{actual}\"");
        }
    }

    private sealed record GoldenRow(string Locale, string Bag, string Options, string Pattern, string[] Texts, string ResolvedOptions, string Icu);

    private static List<GoldenRow> ReadGoldenTable()
    {
        using var stream = typeof(IntlDateTimeFormatMatcherTests).Assembly.GetManifestResourceStream(GoldenResource)
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
            columns.Length.Should().Be(8, line);
            rows.Add(new GoldenRow(
                columns[0],
                columns[1],
                columns[2],
                Unescape(columns[3]),
                [Unescape(columns[4]), Unescape(columns[5])],
                columns[6],
                Unescape(columns[7])));
        }

        return rows;
    }

    private static string Unescape(string value)
        => Regex.Replace(value, @"\\u([0-9a-f]{4})", m => ((char) Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

    /// <summary>Overrides a member that has nothing to do with dates.</summary>
    private sealed class UnrelatedOverride : DefaultCldrProvider
    {
        public override string? GetCurrencyDisplayName(string locale, string code) => "Space Credits";
    }

    /// <summary>Overrides the weekday and month names, in upper case.</summary>
    private sealed class ShoutedGermanNames : DefaultCldrProvider
    {
        public override string[]? GetWeekdayNames(string locale, string style)
            => string.Equals(style, "long", StringComparison.Ordinal)
                ? ["SONNTAG", "MONTAG", "DIENSTAG", "MITTWOCH", "DONNERSTAG", "FREITAG", "SAMSTAG"]
                : base.GetWeekdayNames(locale, style);

        public override string[]? GetMonthNames(string locale, string style, string? calendar)
            => string.Equals(style, "long", StringComparison.Ordinal)
                ? ["JANUAR", "FEBRUAR", "MÄRZ", "APRIL", "MAI", "JUNI", "JULI", "AUGUST", "SEPTEMBER", "OKTOBER", "NOVEMBER", "DEZEMBER"]
                : base.GetMonthNames(locale, style, calendar);
    }

    /// <summary>Implements the interface itself and hands every question to the default provider.</summary>
    private sealed class DelegatingProvider : ICldrProvider
    {
        private readonly ICldrProvider _inner = DefaultCldrProvider.Instance;

        public ListPatterns? GetListPatterns(string locale, string type, string style) => _inner.GetListPatterns(locale, type, style);
        public RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style) => _inner.GetRelativeTimePatterns(locale, unit, style);
        public string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style) => _inner.GetRelativeTimeSpecialPhrase(locale, unit, value, past, style);
        public string? GetNumberingSystemDigits(string numberingSystem) => _inner.GetNumberingSystemDigits(numberingSystem);
        public string? GetDefaultNumberingSystem(string locale) => _inner.GetDefaultNumberingSystem(locale);
        public CurrencyData? GetCurrencyData(string locale, string currencyCode) => _inner.GetCurrencyData(locale, currencyCode);
        public UnitPatterns? GetUnitPatterns(string locale, string unit, string style) => _inner.GetUnitPatterns(locale, unit, style);
        public string? GetDefaultCalendar(string locale) => _inner.GetDefaultCalendar(locale);
        public string[]? GetMonthNames(string locale, string style, string? calendar) => _inner.GetMonthNames(locale, style, calendar);
        public string[]? GetWeekdayNames(string locale, string style) => _inner.GetWeekdayNames(locale, style);
        public string[]? GetDayPeriods(string locale, string style, string? calendar) => _inner.GetDayPeriods(locale, style, calendar);
        public string[]? GetEraNames(string locale, string style, string? calendar) => _inner.GetEraNames(locale, style, calendar);
        public string? GetCurrencyDisplayName(string locale, string code) => _inner.GetCurrencyDisplayName(locale, code);
        public WeekInfo? GetWeekInfo(string locale) => _inner.GetWeekInfo(locale);
        public IReadOnlyCollection<string> GetSupportedCollations() => _inner.GetSupportedCollations();
        public IReadOnlyCollection<string> GetSupportedCurrencies() => _inner.GetSupportedCurrencies();
        public IReadOnlyCollection<string> GetSupportedNumberingSystems() => _inner.GetSupportedNumberingSystems();
        public IReadOnlyCollection<string> GetSupportedTimeZones() => _inner.GetSupportedTimeZones();
        public IReadOnlyCollection<string> GetSupportedUnits() => _inner.GetSupportedUnits();
    }
}

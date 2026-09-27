#nullable enable

using System.Text;
using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Intl;
using Jint.Native.Intl.Data;

namespace Jint.Tests.Runtime;

/// <summary>
/// An <c>Intl.DateTimeFormat</c> <c>dateStyle</c> and <c>timeStyle</c> written through CLDR 48.2's <c>dateFormats</c>,
/// <c>timeFormats</c> and <c>atTime</c> <c>dateTimeFormats</c>, in the resolved hour cycle, by the one renderer
/// <c>format()</c> and <c>formatToParts()</c> share; and Temporal's AdjustDateTimeStyleFormat over them
/// (sebastienros/jint#4158).
/// </summary>
/// <remarks>
/// <para>
/// The golden table is <c>tools/cldr-dates/reference/golden-styles.tsv</c>, which the reference model
/// (<c>format_matcher.py golden-styles</c>) writes from CLDR 48.2: 51 locales, the four date and four time styles alone
/// and in all sixteen pairs, twelve hour-cycle cases, and seventeen Temporal cases, each at two instants with the
/// pattern the model chose, the UTC zone name the pattern writes and what <c>resolvedOptions()</c> reports. Its last
/// column is what Node 24.19 (ICU 78.3) writes where it differs from the model; <see cref="KnownIcuDifferences"/> is
/// that list, explained. Node's V8 does not ship Temporal, so a Temporal case was asked of ICU as the component bag the
/// model's AdjustDateTimeStyleFormat matched, which is what an engine built on ICU writes for it.
/// </para>
/// <para>
/// The zone column is CLDR's <c>Etc/UTC</c> name; Jint writes the English one for every locale (localized zone names
/// are not embedded), so a row's text is compared with Jint's zone part put back to CLDR's, and
/// <see cref="EveryGoldenRowFormatsAsTheReferenceModel"/> counts the rows where the two differ.
/// </para>
/// </remarks>
public class IntlDateTimeFormatStyleTests
{
    private const string GoldenResource = "cldr-dates-golden-styles.tsv";

    /// <summary>
    /// The rows whose <c>timeStyle</c> writes a long zone name CLDR localizes — the seven <c>full</c> time-style cases of
    /// each of the 46 locales but English and Filipino — where Jint writes "Coordinated Universal Time" and CLDR, say,
    /// "Koordinierte Weltzeit".
    /// </summary>
    private const int RowsWithAnEnglishZoneName = 322;

    private static readonly long[] GoldenInstants =
    [
        new DateTimeOffset(2022, 12, 24, 15, 7, 9, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        new DateTimeOffset(2023, 3, 6, 9, 4, 5, TimeSpan.Zero).ToUnixTimeMilliseconds(),
    ];

    /// <summary>
    /// Every row where ICU's answer differs from the golden table's, and why. None differs in text; each differs in what
    /// <c>resolvedOptions()</c> reports, and Jint follows the specification rather than V8 in both kinds.
    /// </summary>
    private static readonly Dictionary<(string Locale, string Case), string> KnownIcuDifferences = new()
    {
        // A Temporal case's ICU answer is the resolvedOptions() of the component bag it matched, and V8 reads a letter
        // inside a quoted literal of that bag's pattern as a field: the "d" of 'de' or 'del' a day, the "m" of 'm'. or
        // 'năm' a minute, the "v" of 'v' a time zone, the "b" of 'ob' a day period.
        [("ca", "PYM_full")] = "quoted 'del' read as a day",
        [("ca", "PYM_long")] = "quoted 'del' read as a day",
        [("ca", "PYM_medium")] = "quoted 'del' read as a day",
        [("cs", "PDT_full_full")] = "quoted 'v' read as a time zone",
        [("cs", "PDT_long_long")] = "quoted 'v' read as a time zone",
        [("es", "PYM_full")] = "quoted 'de' read as a day",
        [("es", "PYM_long")] = "quoted 'de' read as a day",
        [("es-MX", "PYM_full")] = "quoted 'de' read as a day",
        [("es-MX", "PYM_long")] = "quoted 'de' read as a day",
        [("lt", "PYM_full")] = "quoted 'm' read as a minute",
        [("lt", "PYM_long")] = "quoted 'm' read as a minute",
        [("pt", "PYM_full")] = "quoted 'de' read as a day",
        [("pt", "PYM_long")] = "quoted 'de' read as a day",
        [("pt", "PYM_medium")] = "quoted 'de' read as a day",
        [("pt-PT", "PYM_full")] = "quoted 'de' read as a day",
        [("pt-PT", "PYM_long")] = "quoted 'de' read as a day",
        [("sl", "PDT_full_full")] = "quoted 'ob' read as a day period",
        [("sl", "PDT_long_long")] = "quoted 'ob' read as a day period",
        [("vi", "PYM_full")] = "quoted 'năm' read as a minute",
        [("vi", "PYM_long")] = "quoted 'năm' read as a minute",

        // test262's intl402/DateTimeFormat/prototype/resolvedOptions/hourCycle-default.js puts Japanese with
        // hour12: true on [[hourCycle12]], the first 12-hour cycle CLDR allows for JP, which is h11; V8 answers h12.
        [("ja", "t_medium_12")] = "hour12: true resolves to h11, as test262 expects",
        [("ja", "dt_long_long_12")] = "hour12: true resolves to h11, as test262 expects",
    };

    [Test]
    public void EveryGoldenRowFormatsAsTheReferenceModel()
    {
        var engine = new Engine();
        var styleRow = engine.Evaluate("""
            (function (locale, options, first, second) {
                var f = new Intl.DateTimeFormat(locale, Object.assign({ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }, JSON.parse(options)));
                function parts(date) { return f.formatToParts(date).map(function (p) { return [p.type, p.value]; }); }
                return [f, f.format(first), f.format(second), parts(first), parts(second), resolved(f.resolvedOptions())];
            })
            """);
        var temporalRow = engine.Evaluate("""
            (function (locale, options, first, second) {
                var o = JSON.parse(options);
                var kind = o.temporal;
                delete o.temporal;
                var styleOptions = Object.assign({ calendar: 'gregory', numberingSystem: 'latn' }, o);
                function value(ms) {
                    var d = new Date(ms);
                    var y = d.getUTCFullYear(), m = d.getUTCMonth() + 1, day = d.getUTCDate();
                    var h = d.getUTCHours(), min = d.getUTCMinutes(), s = d.getUTCSeconds();
                    switch (kind) {
                        case 'PlainYearMonth': return Temporal.PlainYearMonth.from({ year: y, month: m, calendar: 'gregory' });
                        case 'PlainMonthDay': return Temporal.PlainMonthDay.from({ monthCode: 'M' + (m < 10 ? '0' : '') + m, day: day, calendar: 'gregory' });
                        case 'PlainDate': return new Temporal.PlainDate(y, m, day, 'gregory');
                        case 'PlainTime': return new Temporal.PlainTime(h, min, s);
                        default: return new Temporal.PlainDateTime(y, m, day, h, min, s, 0, 0, 0, 'gregory');
                    }
                }
                var f = new Intl.DateTimeFormat(locale, Object.assign({ timeZone: 'UTC' }, styleOptions));
                function joined(v) { return f.formatToParts(v).map(function (p) { return p.value; }).join(''); }
                var a = value(first), b = value(second);
                return [a.toLocaleString(locale, styleOptions), b.toLocaleString(locale, styleOptions), f.format(a), f.format(b), joined(a), joined(b)];
            })
            """);
        engine.Execute("""
            function resolved(ro) {
                var keys = ['dateStyle', 'timeStyle', 'weekday', 'era', 'year', 'month', 'day', 'dayPeriod', 'hour', 'minute', 'second', 'fractionalSecondDigits', 'timeZoneName'];
                var record = keys.filter(function (k) { return ro[k] !== undefined; }).map(function (k) { return k + '=' + ro[k]; }).join(',');
                return ro.hourCycle === undefined ? record : record + ',hourCycle=' + ro.hourCycle;
            }
            """);

        var problems = new List<string>();
        var englishZoneNames = 0;
        var rows = ReadGoldenTable();
        foreach (var golden in rows)
        {
            var label = $"{golden.Locale} {golden.Case}";
            if (golden.Options.Contains("\"temporal\"", StringComparison.Ordinal))
            {
                var result = engine.Invoke(temporalRow, golden.Locale, golden.Options, GoldenInstants[0], GoldenInstants[1]).AsArray();
                for (var i = 0; i < 2; i++)
                {
                    Check(problems, label, $"toLocaleString@{i + 1}", golden.Texts[i], result[i].AsString());
                    Check(problems, label, $"format@{i + 1}", golden.Texts[i], result[2 + i].AsString());
                    Check(problems, label, $"formatToParts@{i + 1}", golden.Texts[i], result[4 + i].AsString());
                }

                var (pattern, record) = TemporalFormatOf(engine, golden);
                Check(problems, label, "pattern", golden.Pattern, pattern.Pattern);
                Check(problems, label, "record", golden.ResolvedOptions, record);
                continue;
            }

            var styled = engine.Invoke(styleRow, golden.Locale, golden.Options, GoldenInstants[0], GoldenInstants[1]).AsArray();
            var formatter = (JsDateTimeFormat) styled[0];
            Check(problems, label, "pattern", golden.Pattern, formatter.GetStylePattern(plain: false).Pattern);
            for (var i = 0; i < 2; i++)
            {
                var format = styled[1 + i].AsString();
                var text = new StringBuilder();
                foreach (var part in styled[3 + i].AsArray())
                {
                    var type = part.AsArray()[0].AsString();
                    var value = part.AsArray()[1].AsString();
                    if (string.Equals(type, "timeZoneName", StringComparison.Ordinal))
                    {
                        // Jint writes CLDR's English Etc/UTC names, and the golden table the locale's own.
                        var english = golden.Pattern.Contains("zzzz", StringComparison.Ordinal) ? "Coordinated Universal Time" : "UTC";
                        Check(problems, label, $"zone@{i + 1}", english, value);
                        if (i == 0 && !string.Equals(value, golden.Zone, StringComparison.Ordinal))
                        {
                            englishZoneNames++;
                        }

                        value = golden.Zone;
                    }

                    text.Append(value);
                }

                Check(problems, label, $"format@{i + 1}", golden.Texts[i], text.ToString());
                Check(problems, label, $"format is formatToParts@{i + 1}", format, string.Concat(styled[3 + i].AsArray().Select(p => p.AsArray()[1].AsString())));
            }

            Check(problems, label, "resolvedOptions", golden.ResolvedOptions, styled[5].AsString());
        }

        problems.Should().BeEmpty();
        rows.Count.Should().Be(2703);
        englishZoneNames.Should().Be(RowsWithAnEnglishZoneName);
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

            differing.Add((golden.Locale, golden.Case), golden.Icu);

            // ICU writes the same text; only what it reports differs.
            var icu = golden.Icu.Split('|');
            icu.Length.Should().Be(3);
            icu[0].Should().Be(golden.Texts[0], $"{golden.Locale} {golden.Case}");
            icu[1].Should().Be(golden.Texts[1], $"{golden.Locale} {golden.Case}");
            icu[2].Should().NotBe(golden.ResolvedOptions);
        }

        differing.Keys.Should().BeEquivalentTo(KnownIcuDifferences.Keys);
    }

    /// <summary>
    /// The design's rows (sebastienros/jint#4158) and more: what Node 24.19 writes, the long zone name apart.
    /// </summary>
    [TestCase("de", "{ dateStyle: 'medium' }", "24.12.2022")]
    [TestCase("de", "{ dateStyle: 'short' }", "24.12.22")]
    [TestCase("en-GB", "{ dateStyle: 'short' }", "24/12/2022")]
    [TestCase("ar", "{ dateStyle: 'medium', numberingSystem: 'latn' }", "24‏/12‏/2022")]
    [TestCase("en", "{ dateStyle: 'full', timeStyle: 'short' }", "Saturday, December 24, 2022 at 3:07 PM")]
    [TestCase("en", "{ dateStyle: 'long', timeStyle: 'short' }", "December 24, 2022 at 3:07 PM")]
    [TestCase("de", "{ dateStyle: 'medium', timeStyle: 'short' }", "24.12.2022, 15:07")]
    [TestCase("de", "{ dateStyle: 'long', timeStyle: 'short' }", "24. Dezember 2022 um 15:07")]
    [TestCase("fr", "{ dateStyle: 'full', timeStyle: 'medium' }", "samedi 24 décembre 2022 à 15:07:09")]
    [TestCase("es", "{ dateStyle: 'long', timeStyle: 'short' }", "24 de diciembre de 2022 a las 15:07")]
    [TestCase("en-GB", "{ dateStyle: 'long', timeStyle: 'long' }", "24 December 2022 at 15:07:09 UTC")]
    [TestCase("vi", "{ dateStyle: 'short', timeStyle: 'short' }", "15:07 24/12/22")]
    [TestCase("ko", "{ timeStyle: 'medium' }", "오후 3:07:09")]
    [TestCase("zh", "{ timeStyle: 'long' }", "UTC 15:07:09")]
    [TestCase("zh-Hant", "{ timeStyle: 'medium' }", "下午3:07:09")]
    [TestCase("ja", "{ timeStyle: 'full' }", "15時07分09秒 Coordinated Universal Time")]
    [TestCase("es-MX", "{ timeStyle: 'short' }", "3:07 p.m.")]
    [TestCase("he", "{ timeStyle: 'short', hour12: true }", "3:07 PM")]
    public void AStyleWritesTheLocalesCldrPattern(string locale, string options, string expected)
    {
        var engine = new Engine();
        var script = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options}))";

        engine.Evaluate($"{script}.format(Date.UTC(2022, 11, 24, 15, 7, 9))").AsString().Should().Be(expected);
        engine.Evaluate($"{script}.formatToParts(Date.UTC(2022, 11, 24, 15, 7, 9)).map(function (p) {{ return p.value; }}).join('')")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// A time style is written in the resolved hour cycle: where the locale's pattern writes another, its skeleton is
    /// matched again with the day period dropped and the cycle's hour letter, as V8 derives [[pattern]] and
    /// [[pattern12]] from ICU — so the day period and the hour's width follow the locale's pattern for that cycle.
    /// Midnight and noon tell the four cycles apart. What Node 24.19 writes.
    /// </summary>
    [TestCase("ko", "{ timeStyle: 'short', hourCycle: 'h11' }", 12, "오후 0:07", "dayPeriod,literal,hour,literal,minute")]
    [TestCase("ja", "{ timeStyle: 'medium', hourCycle: 'h11' }", 12, "午後0:07:09", "dayPeriod,hour,literal,minute,literal,second")]
    [TestCase("en-GB", "{ timeStyle: 'short', hourCycle: 'h11' }", 12, "00:07 pm", "hour,literal,minute,literal,dayPeriod")]
    [TestCase("de", "{ timeStyle: 'short', hour12: true }", 15, "03:07 PM", "hour,literal,minute,literal,dayPeriod")]
    [TestCase("zh", "{ timeStyle: 'long', hourCycle: 'h24' }", 0, "UTC 24:07:09", "timeZoneName,literal,hour,literal,minute,literal,second")]
    [TestCase("en", "{ dateStyle: 'full', timeStyle: 'short', hourCycle: 'h24' }", 0, "Saturday, December 24, 2022 at 24:07", "weekday,literal,month,literal,day,literal,year,literal,hour,literal,minute")]
    [TestCase("ja", "{ dateStyle: 'long', timeStyle: 'long', hour12: true }", 15, "2022/12/24 午後3:07:09 UTC", "year,literal,month,literal,day,literal,dayPeriod,hour,literal,minute,literal,second,literal,timeZoneName")]
    public void ATimeStyleIsWrittenInTheResolvedHourCycle(string locale, string options, int hour, string expected, string types)
    {
        var engine = new Engine();
        var script = $"new Intl.DateTimeFormat('{locale}', Object.assign({{ timeZone: 'UTC' }}, {options}))";
        var date = $"Date.UTC(2022, 11, 24, {hour}, 7, 9)";

        engine.Evaluate($"{script}.format({date})").AsString().Should().Be(expected);
        engine.Evaluate($"{script}.formatToParts({date}).map(function (p) {{ return p.type; }}).join()").AsString().Should().Be(types);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-adjustdatetimestyleformat: a Temporal value writes the style's own format
    /// when it has every field the format writes (a <c>PlainDate</c> under any <c>dateStyle</c>), and otherwise the
    /// format the matcher chooses for the fields it has — the component bag an ICU engine writes the same text for.
    /// A <c>PlainTime</c> keeps a <c>zh-Hant</c> time's flexible day period, which is a field it has.
    /// </summary>
    [TestCase("Temporal.PlainYearMonth.from({ year: 2022, month: 12, calendar: 'gregory' })", "de", "{ dateStyle: 'short', calendar: 'gregory' }", "12/22")]
    [TestCase("Temporal.PlainYearMonth.from({ year: 2022, month: 12, calendar: 'gregory' })", "ru", "{ dateStyle: 'medium', calendar: 'gregory' }", "дек. 2022 г.")]
    [TestCase("Temporal.PlainYearMonth.from({ year: 2022, month: 12, calendar: 'gregory' })", "ja", "{ dateStyle: 'full', calendar: 'gregory' }", "2022/12")]
    [TestCase("Temporal.PlainMonthDay.from({ monthCode: 'M12', day: 24, calendar: 'gregory' })", "ja", "{ dateStyle: 'long', calendar: 'gregory' }", "12/24")]
    [TestCase("Temporal.PlainMonthDay.from({ monthCode: 'M12', day: 24, calendar: 'gregory' })", "de", "{ dateStyle: 'medium', calendar: 'gregory' }", "24.12.")]
    [TestCase("new Temporal.PlainTime(15, 7, 9)", "ko", "{ timeStyle: 'full' }", "오후 3:07:09")]
    [TestCase("new Temporal.PlainTime(15, 7, 9)", "zh-Hant", "{ timeStyle: 'long' }", "下午3:07:09")]
    [TestCase("new Temporal.PlainDateTime(2022, 12, 24, 15, 7, 9)", "en-GB", "{ dateStyle: 'short', timeStyle: 'full' }", "24/12/2022, 15:07:09")]
    [TestCase("new Temporal.PlainDateTime(2022, 12, 24, 15, 7, 9)", "en", "{ dateStyle: 'full', timeStyle: 'full' }", "Saturday, December 24, 2022 at 3:07:09 PM")]
    [TestCase("new Temporal.PlainDate(2022, 12, 24)", "de", "{ dateStyle: 'medium' }", "24.12.2022")]
    public void ATemporalValueWritesTheStyleItsFieldsAllow(string value, string locale, string options, string expected)
    {
        var engine = new Engine();
        engine.Evaluate($"{value}.toLocaleString('{locale}', {options})").AsString().Should().Be(expected);
        engine.Evaluate($"new Intl.DateTimeFormat('{locale}', {options}).format({value})").AsString().Should().Be(expected);
    }

    /// <summary>
    /// A host's names reach a style the way they reach a component bag: where they differ from what
    /// <see cref="DefaultCldrProvider.Instance"/> answers, and in both contexts.
    /// </summary>
    [Test]
    public void AHostsOwnNamesReachAStyle()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new EveningDayPeriods());
        engine.Evaluate("new Intl.DateTimeFormat('en', { dateStyle: 'long', timeStyle: 'short', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24, 15, 7))")
            .AsString().Should().Be("December 24, 2022 at 3:07 EVE");
        engine.Evaluate("new Intl.DateTimeFormat('en', { dateStyle: 'full', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24))")
            .AsString().Should().Be("SATURDAY, December 24, 2022");
    }

    /// <summary>
    /// A provider that answers as the default does — one overriding something else, or implementing the interface and
    /// delegating, as the test262 harness's does — leaves a style with CLDR's names: Mexican Spanish writes CLDR's
    /// "p.m." where .NET's culture has "p. m.".
    /// </summary>
    [Test]
    public void AProviderAnsweringAsTheDefaultKeepsTheCldrNamesInAStyle()
    {
        foreach (var provider in new ICldrProvider[] { new UnrelatedOverride(), DefaultCldrProvider.Instance })
        {
            var engine = new Engine(options => options.Intl.CldrProvider = provider);
            engine.Evaluate("new Intl.DateTimeFormat('es-MX', { timeStyle: 'short', timeZone: 'UTC' }).format(Date.UTC(2022, 11, 24, 15, 7))")
                .AsString().Should().Be("3:07 p.m.");
        }
    }

    /// <summary>
    /// The style patterns every locale carries resolve, and a time style in each hour cycle writes that cycle's hour
    /// letter and no other, and no am/pm in a 24-hour cycle. A 12-hour cycle need not write one: CLDR's <c>fr-CM</c>
    /// 12-hour patterns have none, and ICU writes <c>03:07:09</c> for its <c>hourCycle: "h11"</c> too.
    /// </summary>
    [Test]
    public void EveryLocaleResolvesEveryStyleInEveryHourCycle()
    {
        var problems = new List<string>();
        foreach (var locale in DateTimePatternData.Shared.Locales)
        {
            var generator = DateTimePatternGenerator.ForLocale(locale);
            for (var date = DateTimeStyleWidth.Full; date <= DateTimeStyleWidth.Short; date++)
            {
                for (var time = DateTimeStyleWidth.Full; time <= DateTimeStyleWidth.Short; time++)
                {
                    foreach (var hourCycle in new[] { "h11", "h12", "h23", "h24" })
                    {
                        var pattern = generator.GetStylePattern(date, time, hourCycle, '.');
                        var letter = DateTimePatternGenerator.HourLetter(hourCycle);
                        var fields = pattern.Runs.Where(r => !r.IsLiteral).Select(r => r.Field).ToList();
                        var twelveHour = hourCycle is "h11" or "h12";
                        if (!fields.Contains(letter) || fields.Any(f => f is 'h' or 'H' or 'k' or 'K' && f != letter)
                            || (!twelveHour && fields.Any(f => f is 'a' or 'b' or 'B'))
                            || !fields.Contains('y') || !fields.Contains('d') || !fields.Contains('m'))
                        {
                            problems.Add($"{locale} {date}/{time} {hourCycle}: {pattern.Pattern}");
                        }
                    }
                }
            }
        }

        problems.Should().BeEmpty();
    }

    private static (DateTimeFormatPattern Pattern, string Record) TemporalFormatOf(Engine engine, GoldenRow golden)
    {
        // The formatter each Temporal type's toLocaleString builds (CreateDateTimeFormat with its required and defaults),
        // asked for the pattern it writes a wall-clock value with.
        var options = engine.Evaluate($"(function () {{ var o = JSON.parse('{golden.Options}'); var kind = o.temporal; delete o.temporal; return [kind, Object.assign({{ timeZone: 'UTC', calendar: 'gregory', numberingSystem: 'latn' }}, o)]; }})()").AsArray();
        var (required, defaults) = options[0].AsString() switch
        {
            "PlainYearMonth" => (DateTimeRequired.YearMonth, DateTimeDefaults.YearMonth),
            "PlainMonthDay" => (DateTimeRequired.MonthDay, DateTimeDefaults.MonthDay),
            "PlainDate" => (DateTimeRequired.Date, DateTimeDefaults.Date),
            "PlainTime" => (DateTimeRequired.Time, DateTimeDefaults.Time),
            _ => (DateTimeRequired.Any, DateTimeDefaults.All),
        };

        var formatter = engine.Realm.Intrinsics.DateTimeFormat.CreateDateTimeFormat(golden.Locale, options[1], required: required, defaults: defaults);
        var pattern = formatter.GetStylePattern(plain: true);

        // A format the matcher chose reports its fields, as the component bag it was matched from does; one the style
        // kept reports the style.
        if (golden.ResolvedOptions.StartsWith("dateStyle=", StringComparison.Ordinal) || golden.ResolvedOptions.StartsWith("timeStyle=", StringComparison.Ordinal))
        {
            var styles = new List<string>();
            if (formatter.DateStyle is not null)
            {
                styles.Add("dateStyle=" + formatter.DateStyle);
            }

            if (formatter.TimeStyle is not null)
            {
                styles.Add("timeStyle=" + formatter.TimeStyle);
                styles.Add("hourCycle=" + formatter.ResolvedHourCycle);
            }

            return (pattern, string.Join(",", styles));
        }

        var record = new List<string>();
        void Add(string key, object? value)
        {
            if (value is not null)
            {
                record.Add(key + "=" + value);
            }
        }

        Add("weekday", pattern.Weekday);
        Add("era", pattern.Era);
        Add("year", pattern.Year);
        Add("month", pattern.Month);
        Add("day", pattern.Day);
        Add("dayPeriod", pattern.DayPeriod);
        Add("hour", pattern.Hour);
        Add("minute", pattern.Minute);
        Add("second", pattern.Second);
        Add("fractionalSecondDigits", pattern.FractionalSecondDigits);
        Add("timeZoneName", pattern.TimeZoneName);
        if (pattern.Hour is not null)
        {
            record.Add("hourCycle=" + formatter.ResolvedHourCycle);
        }

        return (pattern, string.Join(",", record));
    }

    private static void Check(List<string> problems, string label, string what, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            problems.Add($"{label} {what}: expected \"{expected}\", got \"{actual}\"");
        }
    }

    private sealed record GoldenRow(string Locale, string Case, string Options, string Pattern, string[] Texts, string Zone, string ResolvedOptions, string Icu);

    private static List<GoldenRow> ReadGoldenTable()
    {
        using var stream = typeof(IntlDateTimeFormatStyleTests).Assembly.GetManifestResourceStream(GoldenResource)
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
                Unescape(columns[3]),
                [Unescape(columns[4]), Unescape(columns[5])],
                Unescape(columns[6]),
                columns[7],
                Unescape(columns[8])));
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

    /// <summary>Names the afternoon and the long weekdays its own way, and answers every other name as the default does.</summary>
    private sealed class EveningDayPeriods : DefaultCldrProvider
    {
        public override string[]? GetDayPeriods(string locale, string style, string? calendar) => ["MORN", "EVE"];

        public override string[]? GetWeekdayNames(string locale, string style)
            => string.Equals(style, "long", StringComparison.Ordinal)
                ? ["SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY"]
                : base.GetWeekdayNames(locale, style);
    }
}

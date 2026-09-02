#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// https://tc39.es/ecma402/#table-datetimeformat-components makes <c>"long"</c>, <c>"short"</c> and
/// <c>"narrow"</c> textual month formats, so a formatter answering one of them with a number is answering a
/// different format. Which name it writes depends on what the resolved calendar counts months in, and there
/// are two answers rather than one.
/// </summary>
public class IntlCalendarMonthNameTests
{
    private readonly Engine _engine = new();

    /// <summary>2024-01-15 is 5 Shevat 5784, 4 Rajab 1445, 25 Dey 1402, 6 Toba 1740 and 25 Pausa 1945.</summary>
    private const string January15 = "Date.UTC(2024, 0, 15)";

    /// <summary>2024-09-08 is the third of the thirteenth month in both the Coptic and the Ethiopic year.</summary>
    private const string September8 = "Date.UTC(2024, 8, 8)";

    private string Month(string calendar, string style, string locale = "en-US", string date = January15)
        => MonthOn(_engine, calendar, style, locale, date);

    private static string MonthOn(Engine engine, string calendar, string style, string locale, string date)
        => engine.Evaluate(
                $"new Intl.DateTimeFormat('{locale}-u-ca-{calendar}', {{ month: '{style}', timeZone: 'UTC' }}).format({date})")
            .AsString();

    /// <summary>
    /// The defect this pins: every calendar but <c>gregory</c> wrote a bare number for all three textual
    /// styles. <c>buddhist</c>, <c>japanese</c> and <c>roc</c> differ from <c>gregory</c> in era and year
    /// only - ICU writes "January" for all four - so the locale's own month name is the one to write.
    /// </summary>
    /// <remarks>
    /// The expected <c>narrow</c> value is the abbreviated name rather than a single letter because that is
    /// what this branch writes for <c>gregory</c>; narrow month names are a separate gap, and the point here
    /// is that these three calendars write whatever <c>gregory</c> writes.
    /// </remarks>
    [Theory]
    [InlineData("buddhist", "long", "January")]
    [InlineData("buddhist", "short", "Jan")]
    [InlineData("buddhist", "narrow", "Jan")]
    [InlineData("japanese", "long", "January")]
    [InlineData("japanese", "short", "Jan")]
    [InlineData("japanese", "narrow", "Jan")]
    [InlineData("roc", "long", "January")]
    [InlineData("roc", "short", "Jan")]
    [InlineData("roc", "narrow", "Jan")]
    public void ACalendarCountingGregorianMonthsWritesTheGregorianName(string calendar, string style, string expected)
    {
        Month(calendar, style).Should().Be(expected);
    }

    /// <summary>
    /// The same rule without a literal: whatever a locale writes for a Gregorian month, these three write for
    /// the same month, in every style and in a locale whose own names are not English.
    /// </summary>
    [Theory]
    [InlineData("buddhist")]
    [InlineData("japanese")]
    [InlineData("roc")]
    public void SuchACalendarAgreesWithGregoryOnEveryMonthStyle(string calendar)
    {
        foreach (var style in new[] { "long", "short", "narrow", "numeric", "2-digit" })
        {
            foreach (var locale in new[] { "en-US", "de-DE", "th-TH", "ja-JP" })
            {
                Month(calendar, style, locale).Should().Be(
                    Month("gregory", style, locale),
                    "{0} counts the Gregorian months, for {1} in {2}", calendar, style, locale);
            }
        }
    }

    /// <summary>
    /// A calendar counting months of its own is the other case, and the number is what the engine has: Jint
    /// ships no month-name data for one, and a number is never a wrong name. ICU itself writes the number for
    /// <c>"narrow"</c> on every one of these.
    /// </summary>
    [Theory]
    [InlineData("islamic-civil", "7")]
    [InlineData("islamic-tbla", "7")]
    [InlineData("islamic-umalqura", "7")]
    [InlineData("hebrew", "5")]
    [InlineData("persian", "10")]
    [InlineData("coptic", "5")]
    [InlineData("ethiopic", "5")]
    [InlineData("ethioaa", "5")]
    [InlineData("indian", "10")]
    [InlineData("chinese", "12")]
    [InlineData("dangi", "12")]
    public void ACalendarWithMonthsOfItsOwnKeepsTheNumberWithNoDataForIt(string calendar, string expected)
    {
        foreach (var style in new[] { "long", "short", "narrow" })
        {
            Month(calendar, style).Should().Be(expected, "{0} has no {1} month names to write", calendar, style);
        }
    }

    /// <summary>
    /// <see cref="ICldrProvider.GetMonthNames"/> takes the calendar for exactly this, and had no reachable
    /// effect through <c>Intl</c> at all: a host supplying Hebrew month names had nowhere for them to be
    /// written.
    /// </summary>
    [Theory]
    [InlineData("long", "Shevat")]
    [InlineData("short", "Sh")]
    [InlineData("narrow", "S")]
    public void AHostsMonthNamesForTheCalendarAreWritten(string style, string expected)
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new HebrewMonths());

        MonthOn(engine, "hebrew", style, "en-US", January15).Should().Be(expected);
    }

    /// <summary>
    /// A thirteenth month is a month, and the calendars that have one carry a name for it. The
    /// <see cref="System.Globalization.DateTimeFormatInfo"/> arrays the shipped provider reads carry twelve
    /// names, which is why the calendar's own names are read from the provider rather than out of them.
    /// </summary>
    [Fact]
    public void AThirteenthMonthHasAName()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new CopticMonths());

        MonthOn(engine, "coptic", "long", "en-US", September8).Should().Be("Nasie");
        MonthOn(engine, "coptic", "long", "en-US", January15).Should().Be("Toba");
    }

    /// <summary>The calendar the formatter resolved is the one the provider is asked about.</summary>
    [Fact]
    public void TheProviderIsAskedWithTheResolvedCalendar()
    {
        var provider = new RecordsWhatItWasAsked();
        var engine = new Engine(options => options.Intl.CldrProvider = provider);

        MonthOn(engine, "persian", "long", "en-US", January15);

        provider.Calendars.Should().Contain("persian");
    }

    /// <summary><c>format</c> is the concatenation of the parts <c>formatToParts</c> walks, name included.</summary>
    [Fact]
    public void FormatAndFormatToPartsWriteTheSameName()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new HebrewMonths());

        var script = $$"""
            (function () {
                var f = new Intl.DateTimeFormat('en-US-u-ca-hebrew', { year: 'numeric', month: 'long', day: 'numeric', timeZone: 'UTC' });
                var parts = f.formatToParts({{January15}}).map(function (p) { return p.value; }).join('');
                return [f.format({{January15}}), parts].join('~');
            })()
            """;

        var pieces = engine.Evaluate(script).AsString().Split('~');
        pieces[0].Should().Contain("Shevat");
        pieces[1].Should().Be(pieces[0]);
    }

    /// <summary>The numeric styles are untouched: they were never the defect.</summary>
    [Theory]
    [InlineData("buddhist", "numeric", "1")]
    [InlineData("buddhist", "2-digit", "01")]
    [InlineData("hebrew", "numeric", "5")]
    [InlineData("hebrew", "2-digit", "05")]
    [InlineData("coptic", "numeric", "5")]
    [InlineData("coptic", "2-digit", "05")]
    public void ANumericMonthStaysANumber(string calendar, string style, string expected)
    {
        Month(calendar, style).Should().Be(expected);
    }

    /// <summary>
    /// The shipped provider reads month names out of <see cref="System.Globalization.CultureInfo"/>, and those
    /// are the Gregorian ones. Answering with them for <c>hebrew</c> would be a wrong name rather than no name,
    /// and the engine would write "May" for Shevat.
    /// </summary>
    [Theory]
    [InlineData("gregory")]
    [InlineData("iso8601")]
    [InlineData("buddhist")]
    [InlineData("japanese")]
    [InlineData("roc")]
    [InlineData(null)]
    public void TheShippedProviderAnswersForTheCalendarsItsNamesAreFor(string? calendar)
    {
        DefaultCldrProvider.Instance.GetMonthNames("en-US", "long", calendar).Should().HaveCount(12);
    }

    [Theory]
    [InlineData("hebrew")]
    [InlineData("persian")]
    [InlineData("islamic-civil")]
    [InlineData("coptic")]
    [InlineData("chinese")]
    [InlineData("mayan")]
    public void TheShippedProviderHasNoNamesForACalendarCountingItsOwnMonths(string calendar)
    {
        DefaultCldrProvider.Instance.GetMonthNames("en-US", "long", calendar).Should().BeNull();
    }
}

/// <summary>
/// A host provider that answers one question of its own and delegates the rest.
/// <see cref="DefaultCldrProvider"/> is sealed on this branch, so a host writes the delegation itself, which
/// is what <c>Jint.Tests.Test262</c>'s ICU provider does too.
/// </summary>
file abstract class DelegatingCldrProvider : ICldrProvider
{
    private static readonly DefaultCldrProvider Fallback = DefaultCldrProvider.Instance;

    public virtual string[]? GetMonthNames(string locale, string style, string? calendar)
        => Fallback.GetMonthNames(locale, style, calendar);

    protected static string[]? Delegated(string locale, string style, string? calendar)
        => Fallback.GetMonthNames(locale, style, calendar);

    public ListPatterns? GetListPatterns(string locale, string type, string style) => Fallback.GetListPatterns(locale, type, style);
    public RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style) => Fallback.GetRelativeTimePatterns(locale, unit, style);
    public string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style) => Fallback.GetRelativeTimeSpecialPhrase(locale, unit, value, past, style);
    public string? GetNumberingSystemDigits(string numberingSystem) => Fallback.GetNumberingSystemDigits(numberingSystem);
    public string? GetDefaultNumberingSystem(string locale) => Fallback.GetDefaultNumberingSystem(locale);
    public CompactPatterns? GetCompactPatterns(string locale, string style) => Fallback.GetCompactPatterns(locale, style);
    public CurrencyData? GetCurrencyData(string locale, string currencyCode) => Fallback.GetCurrencyData(locale, currencyCode);
    public UnitPatterns? GetUnitPatterns(string locale, string unit, string style) => Fallback.GetUnitPatterns(locale, unit, style);
    public DateTimePatterns? GetDateTimePatterns(string locale, string? dateStyle, string? timeStyle) => Fallback.GetDateTimePatterns(locale, dateStyle, timeStyle);
    public string[]? GetWeekdayNames(string locale, string style) => Fallback.GetWeekdayNames(locale, style);
    public string[]? GetDayPeriods(string locale, string style, string? calendar) => Fallback.GetDayPeriods(locale, style, calendar);
    public string[]? GetEraNames(string locale, string style, string? calendar) => Fallback.GetEraNames(locale, style, calendar);
    public string? GetCurrencyDisplayName(string locale, string code) => Fallback.GetCurrencyDisplayName(locale, code);
    public string? GetLikelySubtags(string locale) => Fallback.GetLikelySubtags(locale);
    public WeekInfo? GetWeekInfo(string locale) => Fallback.GetWeekInfo(locale);
    public string SelectPluralCategory(string locale, double value, string type) => Fallback.SelectPluralCategory(locale, value, type);
    public IReadOnlyCollection<string> GetSupportedCalendars() => Fallback.GetSupportedCalendars();
    public IReadOnlyCollection<string> GetSupportedCollations() => Fallback.GetSupportedCollations();
    public IReadOnlyCollection<string> GetSupportedCurrencies() => Fallback.GetSupportedCurrencies();
    public IReadOnlyCollection<string> GetSupportedNumberingSystems() => Fallback.GetSupportedNumberingSystems();
    public IReadOnlyCollection<string> GetSupportedTimeZones() => Fallback.GetSupportedTimeZones();
    public IReadOnlyCollection<string> GetSupportedUnits() => Fallback.GetSupportedUnits();
}

/// <summary>A host with the thirteen Hebrew month names Jint ships none of, and no other datum.</summary>
file sealed class HebrewMonths : DelegatingCldrProvider
{
    private static readonly string[] Wide =
    [
        "Tishri", "Heshvan", "Kislev", "Tevet", "Shevat", "Adar I", "Adar II",
        "Nisan", "Iyar", "Sivan", "Tammuz", "Av", "Elul"
    ];

    public override string[]? GetMonthNames(string locale, string style, string? calendar)
    {
        if (!string.Equals(calendar, "hebrew", StringComparison.Ordinal))
        {
            return Delegated(locale, style, calendar);
        }

        return style switch
        {
            "long" => Wide,
            "short" => Abbreviate(2),
            "narrow" => Abbreviate(1),
            _ => null
        };
    }

    private static string[] Abbreviate(int length)
    {
        var result = new string[Wide.Length];
        for (var i = 0; i < Wide.Length; i++)
        {
            result[i] = Wide[i].Substring(0, Math.Min(length, Wide[i].Length));
        }

        return result;
    }
}

/// <summary>A host answering for the thirteen months of the Coptic year.</summary>
file sealed class CopticMonths : DelegatingCldrProvider
{
    private static readonly string[] Wide =
    [
        "Tout", "Baba", "Hator", "Kiahk", "Toba", "Amshir", "Baramhat",
        "Baramouda", "Bashans", "Paona", "Epep", "Mesra", "Nasie"
    ];

    public override string[]? GetMonthNames(string locale, string style, string? calendar)
        => string.Equals(calendar, "coptic", StringComparison.Ordinal) && string.Equals(style, "long", StringComparison.Ordinal)
            ? Wide
            : Delegated(locale, style, calendar);
}

/// <summary>A host that adds nothing and remembers what it was asked.</summary>
file sealed class RecordsWhatItWasAsked : DelegatingCldrProvider
{
    public List<string> Calendars { get; } = [];

    public override string[]? GetMonthNames(string locale, string style, string? calendar)
    {
        Calendars.Add(calendar ?? "<null>");
        return Delegated(locale, style, calendar);
    }
}

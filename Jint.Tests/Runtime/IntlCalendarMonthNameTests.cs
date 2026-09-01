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
    /// only — ICU writes "January" for all four — so the locale's own month name is the one to write.
    /// </summary>
    [TestCase("buddhist", "long", "January")]
    [TestCase("buddhist", "short", "Jan")]
    [TestCase("buddhist", "narrow", "J")]
    [TestCase("japanese", "long", "January")]
    [TestCase("japanese", "short", "Jan")]
    [TestCase("japanese", "narrow", "J")]
    [TestCase("roc", "long", "January")]
    [TestCase("roc", "short", "Jan")]
    [TestCase("roc", "narrow", "J")]
    public void ACalendarCountingGregorianMonthsWritesTheGregorianName(string calendar, string style, string expected)
    {
        Month(calendar, style).Should().Be(expected);
    }

    /// <summary>
    /// The same rule without a literal: whatever a locale writes for a Gregorian month, these three write for
    /// the same month, in every style and in a locale whose own names are not English.
    /// </summary>
    [TestCase("buddhist")]
    [TestCase("japanese")]
    [TestCase("roc")]
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
    [TestCase("islamic-civil", "7")]
    [TestCase("islamic-tbla", "7")]
    [TestCase("islamic-umalqura", "7")]
    [TestCase("hebrew", "5")]
    [TestCase("persian", "10")]
    [TestCase("coptic", "5")]
    [TestCase("ethiopic", "5")]
    [TestCase("ethioaa", "5")]
    [TestCase("indian", "10")]
    [TestCase("chinese", "12")]
    [TestCase("dangi", "12")]
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
    [TestCase("long", "Shevat")]
    [TestCase("short", "Sh")]
    [TestCase("narrow", "S")]
    public void AHostsMonthNamesForTheCalendarAreWritten(string style, string expected)
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new HebrewMonths());

        MonthOn(engine, "hebrew", style, "en-US", January15).Should().Be(expected);
    }

    /// <summary>
    /// A thirteenth month is a month, and the calendars that have one carry a name for it. The
    /// <see cref="System.Globalization.DateTimeFormatInfo"/> arrays a formatter seeds carry twelve names and a
    /// trailing empty, which is why the calendar's own names are read from the provider rather than out of them.
    /// </summary>
    [Test]
    public void AThirteenthMonthHasAName()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new CopticMonths());

        MonthOn(engine, "coptic", "long", "en-US", September8).Should().Be("Nasie");
        MonthOn(engine, "coptic", "long", "en-US", January15).Should().Be("Toba");
    }

    /// <summary>The calendar the formatter resolved is the one the provider is asked about.</summary>
    [Test]
    public void TheProviderIsAskedWithTheResolvedCalendar()
    {
        var provider = new RecordsWhatItWasAsked();
        var engine = new Engine(options => options.Intl.CldrProvider = provider);

        MonthOn(engine, "persian", "long", "en-US", January15);

        provider.Calendars.Should().Contain("persian");
    }

    /// <summary>
    /// <c>dateStyle</c> writes its month through the locale's own pattern rather than through the
    /// <c>month</c> option, and the two lanes have to agree: an <c>MMMM</c> run had no name left to write either.
    /// </summary>
    [TestCase("full")]
    [TestCase("long")]
    [TestCase("medium")]
    public void ADateStylePatternWritesTheCalendarsMonthName(string dateStyle)
    {
        var value = _engine.Evaluate(
                $"new Intl.DateTimeFormat('en-US-u-ca-buddhist', {{ dateStyle: '{dateStyle}', timeZone: 'UTC' }}).format({January15})")
            .AsString();

        value.Should().Contain("Jan").And.Contain("2567");
    }

    /// <summary>
    /// A <c>dateStyle</c> resolves to a pattern whose <c>MMMM</c> run is written by the other lane, and a
    /// host's name for the calendar has to reach that one too.
    /// </summary>
    [TestCase("full")]
    [TestCase("long")]
    public void APatternsTextualMonthTakesAHostsNameForTheCalendar(string dateStyle)
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new HebrewMonths());

        engine.Evaluate(
                $"new Intl.DateTimeFormat('en-US-u-ca-hebrew', {{ dateStyle: '{dateStyle}', timeZone: 'UTC' }}).format({January15})")
            .AsString().Should().Contain("Shevat");
    }

    /// <summary><c>format</c> is the concatenation of the parts <c>formatToParts</c> walks, name included.</summary>
    [Test]
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
    [TestCase("buddhist", "numeric", "1")]
    [TestCase("buddhist", "2-digit", "01")]
    [TestCase("hebrew", "numeric", "5")]
    [TestCase("hebrew", "2-digit", "05")]
    [TestCase("coptic", "numeric", "5")]
    [TestCase("coptic", "2-digit", "05")]
    public void ANumericMonthStaysANumber(string calendar, string style, string expected)
    {
        Month(calendar, style).Should().Be(expected);
    }

    /// <summary>
    /// The shipped provider reads month names out of <see cref="System.Globalization.CultureInfo"/>, and those
    /// are the Gregorian ones. Answering with them for <c>hebrew</c> would be a wrong name rather than no name,
    /// and a host subclass delegating to <c>base</c> would inherit the lie.
    /// </summary>
    [TestCase("gregory")]
    [TestCase("iso8601")]
    [TestCase("buddhist")]
    [TestCase("japanese")]
    [TestCase("roc")]
    [TestCase((string?) null)]
    public void TheShippedProviderAnswersForTheCalendarsItsNamesAreFor(string? calendar)
    {
        DefaultCldrProvider.Instance.GetMonthNames("en-US", "long", calendar).Should().HaveCount(12);
    }

    [TestCase("hebrew")]
    [TestCase("persian")]
    [TestCase("islamic-civil")]
    [TestCase("coptic")]
    [TestCase("chinese")]
    [TestCase("mayan")]
    public void TheShippedProviderHasNoNamesForACalendarCountingItsOwnMonths(string calendar)
    {
        DefaultCldrProvider.Instance.GetMonthNames("en-US", "long", calendar).Should().BeNull();
    }
}

/// <summary>A host with the thirteen Hebrew month names Jint ships none of, and no other datum.</summary>
file sealed class HebrewMonths : DefaultCldrProvider
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
            return base.GetMonthNames(locale, style, calendar);
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
file sealed class CopticMonths : DefaultCldrProvider
{
    private static readonly string[] Wide =
    [
        "Tout", "Baba", "Hator", "Kiahk", "Toba", "Amshir", "Baramhat",
        "Baramouda", "Bashans", "Paona", "Epep", "Mesra", "Nasie"
    ];

    public override string[]? GetMonthNames(string locale, string style, string? calendar)
        => string.Equals(calendar, "coptic", StringComparison.Ordinal) && string.Equals(style, "long", StringComparison.Ordinal)
            ? Wide
            : base.GetMonthNames(locale, style, calendar);
}

/// <summary>A host that adds nothing and remembers what it was asked.</summary>
file sealed class RecordsWhatItWasAsked : DefaultCldrProvider
{
    public List<string> Calendars { get; } = [];

    public override string[]? GetMonthNames(string locale, string style, string? calendar)
    {
        Calendars.Add(calendar ?? "<null>");
        return base.GetMonthNames(locale, style, calendar);
    }
}

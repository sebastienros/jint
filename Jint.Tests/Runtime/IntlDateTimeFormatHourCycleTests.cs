#nullable enable

using System.Globalization;

namespace Jint.Tests.Runtime;

/// <summary>
/// The hour cycle an <c>Intl.DateTimeFormat</c> resolves: https://tc39.es/ecma402/#sec-createdatetimeformat
/// takes it from <c>hour12</c>, then the <c>hourCycle</c> option or the <c>-u-hc-</c> keyword, and otherwise
/// from the locale's data — <c>[[hourCycle]]</c>, and <c>[[hourCycle12]]</c> or <c>[[hourCycle24]]</c> when
/// <c>hour12</c> is given.
/// </summary>
/// <remarks>
/// Until issue #4179 that locale data was a hard-coded list of languages — twelve 24-hour ones, <c>ja</c> on
/// the 0-11 clock and everyone else on the 12-hour one — and it disagreed with CLDR's <c>timeData</c>, which
/// <c>Intl.Locale.prototype.getHourCycles</c> reads, for 323 of the 605 .NET specific cultures: British
/// English wrote <c>3:07 PM</c> and Mexican Spanish <c>15:07</c>. The locale data is now that same table, for
/// the region https://tc39.es/ecma402/#sec-regionpreference picks: the preferred cycle, and the first 12-hour
/// and 24-hour cycles the region allows. Every expectation here is Node 24's (ICU 78, CLDR 48) as well, but
/// one: <c>ja</c> with <c>hour12: true</c>, which test262 expects on the 0-11 clock and V8 puts on the 1-12.
/// </remarks>
public class IntlDateTimeFormatHourCycleTests
{
    private readonly Engine _engine = new();

    private string Evaluate(string script) => _engine.Evaluate(script).AsString();

    private string ResolvedHourCycle(string locale, string options = "")
        => Evaluate($"new Intl.DateTimeFormat('{locale}', {{ hour: 'numeric'{options} }}).resolvedOptions().hourCycle");

    /// <summary>
    /// With no option and no keyword, the cycle is CLDR's preferred one for the locale. The first four moved:
    /// Britain and Japan are 24-hour clocks, and Mexico and the rest of Latin America 12-hour ones.
    /// </summary>
    [TestCase("en-GB", "h23")]
    [TestCase("ja-JP", "h23")]
    [TestCase("ja", "h23")]
    [TestCase("es-MX", "h12")]
    [TestCase("es-419", "h12")]
    [TestCase("zh", "h23")]
    [TestCase("cs-CZ", "h23")]
    [TestCase("en-US", "h12")]
    [TestCase("en", "h12")]
    [TestCase("de-DE", "h23")]
    [TestCase("es-ES", "h23")]
    [TestCase("ko-KR", "h12")]
    public void TheDefaultIsCldrsPreferredHourCycle(string locale, string expected)
    {
        ResolvedHourCycle(locale).Should().Be(expected);
    }

    /// <summary>
    /// CLDR keys a few entries by language and region, and those decide ahead of the region's own:
    /// <c>fr_CA</c> is a 24-hour clock in a 12-hour country, <c>en_001</c> a 12-hour clock in a 24-hour world.
    /// </summary>
    [TestCase("fr-CA", "h23")]
    [TestCase("en-CA", "h12")]
    [TestCase("en-001", "h12")]
    [TestCase("en-150", "h23")]
    public void TheLanguageAndRegionDecideAheadOfTheRegion(string locale, string expected)
    {
        ResolvedHourCycle(locale).Should().Be(expected);
    }

    /// <summary>
    /// <c>hour12</c> picks the locale's own 12-hour or 24-hour cycle: the first of each kind CLDR allows in the
    /// region. Japan allows the 0-11 clock ahead of the 1-12 one, which is test262's
    /// <c>resolvedOptions/hourCycle-default.js</c> <c>ja</c> row; no region allows the 1-24 clock.
    /// </summary>
    [TestCase("en-GB", "h12", "h23")]
    [TestCase("en-US", "h12", "h23")]
    [TestCase("es-MX", "h12", "h23")]
    [TestCase("ja-JP", "h11", "h23")]
    [TestCase("ja", "h11", "h23")]
    [TestCase("de-DE", "h12", "h23")]
    [TestCase("fr-CA", "h12", "h23")]
    [TestCase("en-001", "h12", "h23")]
    public void Hour12PicksTheLocalesOwnTwelveOrTwentyFourHourCycle(string locale, string twelveHour, string twentyFourHour)
    {
        ResolvedHourCycle(locale, ", hour12: true").Should().Be(twelveHour);
        ResolvedHourCycle(locale, ", hour12: false").Should().Be(twentyFourHour);
    }

    /// <summary>
    /// The region is the formatter's data locale's — the matched culture's own, or its likely one — not the
    /// language's: English in Germany or Ireland is a 24-hour clock. A <c>-u-rg-</c> override is not one of
    /// the formatter's relevant extension keys, so the data locale never carries it, and it moves
    /// <c>getHourCycles()</c> without moving the formatter.
    /// </summary>
    [TestCase("en-DE", "h23")]
    [TestCase("en-IE", "h23")]
    [TestCase("en-IN", "h12")]
    [TestCase("en-US-u-rg-gbzzzz", "h12")]
    [TestCase("en-GB-u-rg-uszzzz", "h23")]
    public void TheRegionIsTheDataLocales(string locale, string expected)
    {
        ResolvedHourCycle(locale).Should().Be(expected);
    }

    /// <summary>
    /// An option or a keyword is still ahead of the locale's data, and <c>hour12</c> ahead of both.
    /// </summary>
    [Test]
    public void AnOptionOrAKeywordStillDecidesAheadOfTheLocale()
    {
        ResolvedHourCycle("en-GB", ", hourCycle: 'h12'").Should().Be("h12");
        ResolvedHourCycle("en-GB-u-hc-h11").Should().Be("h11");
        ResolvedHourCycle("es-MX", ", hourCycle: 'h24'").Should().Be("h24");
        ResolvedHourCycle("en-GB-u-hc-h24", ", hour12: true").Should().Be("h12");
        ResolvedHourCycle("ja-JP", ", hourCycle: 'h23', hour12: true").Should().Be("h11");
    }

    /// <summary>
    /// The formatter's default and <c>Intl.Locale.prototype.getHourCycles()[0]</c> are one reading of one
    /// table, for every specific culture this machine has. They disagreed for 323 of .NET 10's 605.
    /// </summary>
    [Test]
    public void TheDefaultIsTheFirstHourCycleTheLocaleReportsForEveryCulture()
    {
        var disagreements = new List<string>();
        var compared = 0;
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            if (culture.Name.Length == 0)
            {
                continue;
            }

            var formatter = $"new Intl.DateTimeFormat('{culture.Name}', {{ hour: 'numeric' }}).resolvedOptions()";
            var hourCycle = Evaluate($"{formatter}.hourCycle");
            var reported = Evaluate($"new Intl.Locale({formatter}.locale).getHourCycles()[0]");
            compared++;

            if (!string.Equals(hourCycle, reported, StringComparison.Ordinal))
            {
                disagreements.Add($"{culture.Name}: {hourCycle}, getHourCycles()[0] {reported}");
            }
        }

        disagreements.Should().BeEmpty();
        compared.Should().BeGreaterThan(100);
    }

    /// <summary>
    /// The cycle <c>resolvedOptions()</c> reports is the one <c>format</c> writes with: a 24-hour locale
    /// writes 15:07 and no day period, a 12-hour one 3:07 and the locale's own designator.
    /// </summary>
    [TestCase("en-GB", "15:07", "00:07")]
    [TestCase("zh", "15:07", "00:07")]
    [TestCase("de-DE", "15:07", "00:07")]
    [TestCase("en-US", "3:07 PM", "12:07 AM")]
    public void FormatWritesTheResolvedHourCycle(string locale, string afternoon, string pastMidnight)
    {
        var formatter = $"new Intl.DateTimeFormat('{locale}', {{ hour: 'numeric', minute: 'numeric', timeZone: 'UTC' }})";

        Evaluate($"{formatter}.format(Date.UTC(2024, 0, 15, 15, 7))").Should().Be(afternoon);
        Evaluate($"{formatter}.format(Date.UTC(2024, 0, 15, 0, 7))").Should().Be(pastMidnight);
    }

    /// <summary>
    /// Mexican Spanish is a 12-hour clock now, written with the culture's own designator, and in both lanes.
    /// </summary>
    [Test]
    public void ATwelveHourLocaleWritesItsDayPeriodInBothLanes()
    {
        const string Formatter = "new Intl.DateTimeFormat('es-MX', { hour: 'numeric', minute: 'numeric', timeZone: 'UTC' })";
        var designator = new CultureInfo("es-MX", useUserOverride: false).DateTimeFormat.PMDesignator;

        Evaluate($"{Formatter}.format(Date.UTC(2024, 0, 15, 15, 7))").Should().Be("3:07 " + designator);
        Evaluate($"{Formatter}.formatToParts(Date.UTC(2024, 0, 15, 15, 7)).map(function (p) {{ return p.type; }}).join()")
            .Should().Be("hour,literal,minute,literal,dayPeriod");
    }

    /// <summary>
    /// <c>formatToParts</c> and <c>timeStyle</c> read the same cycle: British English writes no day period.
    /// </summary>
    [Test]
    public void TheOtherLanesWriteTheResolvedHourCycle()
    {
        Evaluate("new Intl.DateTimeFormat('en-GB', { hour: 'numeric', minute: 'numeric', timeZone: 'UTC' }).formatToParts(Date.UTC(2024, 0, 15, 15, 7)).map(function (p) { return p.type; }).join()")
            .Should().Be("hour,literal,minute");
        Evaluate("new Intl.DateTimeFormat('en-GB', { timeStyle: 'short', timeZone: 'UTC' }).format(Date.UTC(2024, 0, 15, 15, 7))")
            .Should().Be("15:07");
        Evaluate("new Intl.DateTimeFormat('en-GB', { timeStyle: 'short', timeZone: 'UTC' }).resolvedOptions().hourCycle")
            .Should().Be("h23");
    }

    /// <summary>
    /// <c>Date.prototype.toLocaleString</c>, <c>toLocaleTimeString</c> and Temporal's <c>toLocaleString</c>
    /// construct an <c>Intl.DateTimeFormat</c>, so they take the same default.
    /// </summary>
    [Test]
    public void TheToLocaleStringMethodsTakeTheSameDefault()
    {
        Evaluate("new Date(Date.UTC(2024, 0, 15, 15, 7)).toLocaleTimeString('en-GB', { timeZone: 'UTC' })")
            .Should().Be("15:07:00");
        Evaluate("new Date(Date.UTC(2024, 0, 15, 15, 7)).toLocaleString('en-GB', { timeZone: 'UTC' })")
            .Should().EndWith(" 15:07:00");
        Evaluate("new Date(Date.UTC(2024, 0, 15, 15, 7)).toLocaleTimeString('es-MX', { timeZone: 'UTC', hour: 'numeric', minute: 'numeric' })")
            .Should().StartWith("3:07 ");
        Evaluate("Temporal.PlainTime.from('15:07').toLocaleString('en-GB')")
            .Should().Be("15:07:00");
        Evaluate("Temporal.PlainDateTime.from('2024-01-15T15:07').toLocaleString('ja-JP')")
            .Should().EndWith(" 15:07:00");
    }
}

#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>ca</c> is a relevant extension key of <c>Intl.DateTimeFormat</c>, and its locale default is
/// <c>keyLocaleData[0]</c> — https://tc39.es/ecma402/#sec-resolvelocale step 13.c — which
/// <see cref="ICldrProvider.GetDefaultCalendar"/> answers for, the way
/// <see cref="ICldrProvider.GetDefaultNumberingSystem"/> answers for <c>nu</c>.
/// </summary>
public class IntlCalendarDefaultTests
{
    private readonly Engine _engine = new();

    /// <summary>
    /// CLDR's <c>calendarPreferenceData</c> names four regions that prefer something other than
    /// <c>gregory</c>, and every other region takes territory <c>001</c>'s answer.
    /// </summary>
    [TestCase("en-US", "gregory")]
    [TestCase("de-DE", "gregory")]
    [TestCase("ar-EG", "gregory")]
    [TestCase("ja-JP", "gregory")]
    [TestCase("ko-KR", "gregory")]
    [TestCase("zh-TW", "gregory")]
    [TestCase("he-IL", "gregory")]
    [TestCase("th-TH", "buddhist")]
    [TestCase("fa-IR", "persian")]
    [TestCase("ps-AF", "persian")]
    [TestCase("ar-SA", "islamic-umalqura")]
    public void TheLocalesOwnCalendarIsTheDefault(string locale, string expected)
    {
        _engine.Evaluate($"new Intl.DateTimeFormat('{locale}').resolvedOptions().calendar")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// The table is keyed by region, and a locale that names none still has one: <c>th</c> maximizes to
    /// <c>th-Thai-TH</c>.
    /// </summary>
    [TestCase("th", "buddhist")]
    [TestCase("fa", "persian")]
    [TestCase("ps", "persian")]
    [TestCase("ar", "gregory")]
    [TestCase("en", "gregory")]
    public void TheRegionIsFoundThroughLikelySubtags(string locale, string expected)
    {
        _engine.Evaluate($"new Intl.DateTimeFormat('{locale}').resolvedOptions().calendar")
            .AsString().Should().Be(expected);
    }

    /// <summary>
    /// The defect: <c>ar-SA</c> could name no calendar but <c>islamic</c>, because the answer came from
    /// <c>CultureInfo.Calendar</c> and both of .NET's Hijri classes mapped to that one identifier — while
    /// <c>islamic</c> is a calendar https://tc39.es/ecma402/#sec-createdatetimeformat step 9 says a formatter
    /// must resolve away from, and <c>islamic-umalqura</c> was one an explicit option could already reach.
    /// </summary>
    [Test]
    public void ArabicSaudiResolvesToUmmAlQuraAndFormatsInIt()
    {
        _engine.Evaluate("new Intl.DateTimeFormat('ar-SA').resolvedOptions().calendar")
            .AsString().Should().Be("islamic-umalqura");

        // …and the year it writes is that calendar's, where it used to be the Gregorian year beside a
        // Hijri day and month — 2026-08-27 came out as "14/3/2026"
        var script = """
            (function () {
                var d = new Date(Date.UTC(2026, 7, 27));
                var hijri = new Intl.DateTimeFormat('ar-SA', { year: 'numeric', timeZone: 'UTC' }).format(d);
                var gregorian = new Intl.DateTimeFormat('en-US', { year: 'numeric', timeZone: 'UTC' }).format(d);
                return hijri !== gregorian;
            })()
            """;
        _engine.Evaluate(script).AsBoolean().Should().BeTrue();
    }

    /// <summary>The two things ResolveLocale lets outrank the locale's own answer still do.</summary>
    [Test]
    public void AnExplicitRequestStillWins()
    {
        _engine.Evaluate("new Intl.DateTimeFormat('ar-SA-u-ca-gregory').resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
        _engine.Evaluate("new Intl.DateTimeFormat('th-TH', { calendar: 'gregory' }).resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
        _engine.Evaluate("new Intl.DateTimeFormat('en', { calendar: 'islamic-umalqura' }).resolvedOptions().calendar")
            .AsString().Should().Be("islamic-umalqura");
    }

    /// <summary>A host provider's answer is the locale default, and it reaches what the formatter writes.</summary>
    [Test]
    public void AHostProviderAnswersForTheCalendarToo()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new EverywhereIsHebrew());

        engine.Evaluate("new Intl.DateTimeFormat('en-US').resolvedOptions().calendar")
            .AsString().Should().Be("hebrew");

        // …and an explicit request still outranks it
        engine.Evaluate("new Intl.DateTimeFormat('en-US', { calendar: 'gregory' }).resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
    }

    /// <summary>
    /// <c>keyLocaleData</c> is built out of the calendars the implementation supports, so a provider naming
    /// one outside that list has not named a candidate — it has named nothing.
    /// </summary>
    [Test]
    public void ACalendarTheEngineCannotFormatIsNotAdopted()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new NamesACalendarNobodyHas());

        engine.Evaluate("new Intl.DateTimeFormat('en-US').resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
    }

    /// <summary>A provider with no opinion is answered the way one with no numbering-system opinion is.</summary>
    [Test]
    public void AProviderWithNoOpinionFallsBackToGregory()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new NoOpinion());

        engine.Evaluate("new Intl.DateTimeFormat('th-TH').resolvedOptions().calendar")
            .AsString().Should().Be("gregory");
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern step 13 reads the field values out of
    /// <c>dateTimeFormat.[[Calendar]]</c> — the calendar <c>resolvedOptions()</c> reports — so the two cannot
    /// come apart. They did wherever the locale's own <see cref="System.Globalization.CultureInfo"/> carried a
    /// non-Gregorian <see cref="System.Globalization.Calendar"/>: .NET applied that one on top of whatever Jint
    /// had already applied, and <c>ar-SA</c> asked for <c>gregory</c> answered <c>"gregory"</c> and wrote
    /// <c>14/3/1448</c>.
    /// </summary>
    [TestCase("ar-SA", "{ calendar: 'gregory', timeZone: 'UTC' }", "1448")]
    [TestCase("ar-SA-u-ca-gregory", "{ timeZone: 'UTC' }", "1448")]
    [TestCase("th-TH", "{ calendar: 'gregory', timeZone: 'UTC' }", "2569")]
    [TestCase("fa-IR", "{ calendar: 'gregory', timeZone: 'UTC' }", "1405")]
    public void ARequestedGregorianCalendarIsTheOneItFormatsIn(string locale, string options, string otherCalendarYear)
    {
        var formatted = Format(locale, options);

        formatted.Calendar.Should().Be("gregory");
        formatted.Value.Should().Contain("2026").And.NotContain(otherCalendarYear);
        formatted.Parts.Should().Be(formatted.Value);
    }

    /// <summary>The Gregorian month and day come out too, not only the year.</summary>
    [TestCase("ar-SA", "{ calendar: 'gregory', timeZone: 'UTC' }")]
    [TestCase("th-TH", "{ calendar: 'gregory', timeZone: 'UTC' }")]
    [TestCase("fa-IR", "{ calendar: 'gregory', timeZone: 'UTC' }")]
    public void ARequestedGregorianCalendarWritesTheGregorianMonthAndDay(string locale, string options)
    {
        Fields(locale, options).Should().Be("2026-8-27");
    }

    /// <summary>
    /// The other direction of the same invariant: with nothing requested the locale's own calendar is still
    /// resolved, and is still the one the date is written in — now through Jint's own conversion rather than
    /// .NET's.
    /// </summary>
    [TestCase("ar-SA", "islamic-umalqura", "1448-3-14")]
    [TestCase("th-TH", "buddhist", "2569-8-27")]
    [TestCase("fa-IR", "persian", "1405-6-5")]
    [TestCase("en-US", "gregory", "2026-8-27")]
    public void TheLocalesOwnCalendarIsStillTheOneItFormatsIn(string locale, string expectedCalendar, string expectedFields)
    {
        var formatted = Format(locale, "{ timeZone: 'UTC' }");

        formatted.Calendar.Should().Be(expectedCalendar);
        formatted.Parts.Should().Be(formatted.Value);
        Fields(locale, "{ timeZone: 'UTC' }").Should().Be(expectedFields);
    }

    /// <summary>
    /// A calendar a locale's own <see cref="System.Globalization.CultureInfo"/> knows nothing about is
    /// reached the same way, so asking <c>ar-SA</c> for the Buddhist calendar writes a Buddhist year rather
    /// than a Hijri one.
    /// </summary>
    [Test]
    public void AThirdCalendarIsNeitherOfTheOtherTwo()
    {
        var formatted = Format("ar-SA", "{ calendar: 'buddhist', timeZone: 'UTC' }");

        formatted.Calendar.Should().Be("buddhist");
        formatted.Value.Should().Contain("2569");
        formatted.Parts.Should().Be(formatted.Value);
    }

    /// <summary>
    /// The month a name is written for moves with the resolved calendar's month, in every style — including
    /// <c>"narrow"</c>, whose name is not read out of a pattern but derived from the abbreviated slot, which
    /// the shipped <see cref="DefaultCldrProvider"/> fills from the locale's own calendar.
    /// </summary>
    [TestCase("narrow")]
    [TestCase("short")]
    [TestCase("long")]
    [TestCase("numeric")]
    public void TheMonthNameMovesWithTheResolvedCalendarsMonth(string style)
    {
        // 2026-08-05 and 2026-08-27 are one Gregorian month and two Hijri ones, Safar and Rabi' I.
        var options = $"{{ calendar: 'gregory', month: '{style}', timeZone: 'UTC' }}";

        MonthOn("Date.UTC(2026, 7, 5)", options).Should().Be(MonthOn("Date.UTC(2026, 7, 27)", options));
        MonthOn("Date.UTC(2026, 8, 27)", options).Should().NotBe(MonthOn("Date.UTC(2026, 7, 27)", options));
    }

    private string MonthOn(string date, string options)
        => _engine.Evaluate($"new Intl.DateTimeFormat('ar-SA', {options}).format(new Date({date}))").AsString();

    /// <summary>
    /// The culture a formatter adjusts is its own clone: the process-wide cache hands out a read-only
    /// instance every engine shares, so one formatter asking for <c>gregory</c> must not be the next one's
    /// calendar.
    /// </summary>
    [Test]
    public void OneFormattersCalendarIsNotTheNextOnes()
    {
        Format("ar-SA", "{ calendar: 'gregory', timeZone: 'UTC' }").Value.Should().Contain("2026");
        Format("ar-SA", "{ timeZone: 'UTC' }").Value.Should().Contain("1448");

        new Engine().Evaluate(
            "new Intl.DateTimeFormat('ar-SA', { timeZone: 'UTC' }).format(new Date(Date.UTC(2026, 7, 27)))")
            .AsString().Should().Contain("1448");
    }

    /// <summary>2026-08-27 is 14 Rabi' I 1448, 5 Shahrivar 1405 and 27 August 2569 BE.</summary>
    private (string Calendar, string Value, string Parts) Format(string locale, string options)
    {
        var script = $$"""
            (function () {
                var d = new Date(Date.UTC(2026, 7, 27));
                var f = new Intl.DateTimeFormat('{{locale}}', {{options}});
                var parts = f.formatToParts(d).map(function (p) { return p.value; }).join('');
                return [f.resolvedOptions().calendar, f.format(d), parts].join('~');
            })()
            """;

        var pieces = _engine.Evaluate(script).AsString().Split('~');
        return (pieces[0], pieces[1], pieces[2]);
    }

    /// <summary>The year, month and day <c>formatToParts</c> names, so a separator cannot stand in for one.</summary>
    private string Fields(string locale, string options)
    {
        var script = $$"""
            (function () {
                var d = new Date(Date.UTC(2026, 7, 27));
                var f = new Intl.DateTimeFormat('{{locale}}', {{options}});
                var seen = {};
                f.formatToParts(d).forEach(function (p) { seen[p.type] = p.value; });
                return [seen.year, seen.month, seen.day].join('-');
            })()
            """;

        return _engine.Evaluate(script).AsString();
    }

    /// <summary>The shipped provider's own answers, read directly.</summary>
    [Test]
    public void TheShippedProviderReadsCldrsTable()
    {
        DefaultCldrProvider.Instance.GetDefaultCalendar("ar-SA").Should().Be("islamic-umalqura");
        DefaultCldrProvider.Instance.GetDefaultCalendar("th-TH").Should().Be("buddhist");
        DefaultCldrProvider.Instance.GetDefaultCalendar("fa-IR").Should().Be("persian");
        DefaultCldrProvider.Instance.GetDefaultCalendar("en-US").Should().Be("gregory");
    }
}

/// <summary>One datum, and the other eighteen members inherited.</summary>
file sealed class EverywhereIsHebrew : DefaultCldrProvider
{
    public override string? GetDefaultCalendar(string locale) => "hebrew";
}

/// <summary>A provider naming a calendar this engine has no conversions for.</summary>
file sealed class NamesACalendarNobodyHas : DefaultCldrProvider
{
    public override string? GetDefaultCalendar(string locale) => "mayan";
}

/// <summary>A provider that declines to answer.</summary>
file sealed class NoOpinion : DefaultCldrProvider
{
    public override string? GetDefaultCalendar(string locale) => null;
}

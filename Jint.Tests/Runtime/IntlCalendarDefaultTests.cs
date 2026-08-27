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

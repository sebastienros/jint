#nullable enable

using Jint.Native.Intl;
using Jint.Native.Temporal;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Locale.prototype.getCalendars</c> is https://tc39.es/ecma402/#sec-calendarsoflocale: CLDR's
/// <c>calendarPreferenceData</c> for the region https://tc39.es/ecma402/#sec-regionpreference picks, each
/// calendar kept only if https://tc39.es/ecma402/#sec-availablecalendars has it.
/// </summary>
/// <remarks>
/// Until issue #4159 the answer was <c>gregory</c> for every locale that did not ask for a calendar itself.
/// Each list below is CLDR 48.2's ordering with <c>islamic</c> and <c>islamic-rgsa</c> left out, the two
/// identifiers Jint accepts but has no calendar for.
/// </remarks>
public class IntlLocaleCalendarsTests
{
    private readonly Engine _engine = new();

    private string Calendars(string tag) => Calendars(_engine, tag);

    private static string Calendars(Engine engine, string tag)
        => engine.Evaluate($"JSON.stringify(new Intl.Locale('{tag}').getCalendars())").AsString();

    /// <summary>A region's own ordering, most preferred first.</summary>
    [TestCase("en-US", """["gregory"]""")]
    [TestCase("th-TH", """["buddhist","gregory"]""")]
    [TestCase("fa-IR", """["persian","gregory","islamic-civil","islamic-tbla"]""")]
    [TestCase("ps-AF", """["persian","gregory","islamic-civil","islamic-tbla"]""")]
    [TestCase("ar-SA", """["gregory","islamic-umalqura"]""")]
    [TestCase("ar-AE", """["gregory","islamic-umalqura","islamic-civil","islamic-tbla"]""")]
    [TestCase("ar-EG", """["gregory","coptic","islamic-civil","islamic-tbla"]""")]
    [TestCase("he-IL", """["gregory","hebrew","islamic-civil","islamic-tbla"]""")]
    [TestCase("ja-JP", """["gregory","japanese"]""")]
    [TestCase("ko-KR", """["gregory","dangi"]""")]
    [TestCase("zh-TW", """["gregory","roc","chinese"]""")]
    [TestCase("am-ET", """["gregory","ethiopic"]""")]
    [TestCase("hi-IN", """["gregory","indian"]""")]
    public void ARegionsOwnCalendars(string tag, string expected)
    {
        Calendars(tag).Should().Be(expected);
    }

    /// <summary>
    /// Each level of the priority order, on a tag that also carries every lower level: <c>TH</c> is
    /// Buddhist first, <c>JP</c> lists the Japanese calendar, <c>inka</c> is in <c>IN</c>, which lists the
    /// Indian national calendar, <c>fa</c> is likely <c>IR</c>, Persian first, and <c>eo</c> has no likely
    /// region. These are the levels test262's <c>region-priority.js</c> uses.
    /// </summary>
    [TestCase("fa-JP-u-sd-inka-rg-thzzzz", """["buddhist","gregory"]""")]
    [TestCase("fa-JP-u-sd-inka", """["gregory","japanese"]""")]
    [TestCase("fa-u-sd-inka", """["gregory","indian"]""")]
    [TestCase("fa", """["persian","gregory","islamic-civil","islamic-tbla"]""")]
    [TestCase("eo", """["gregory"]""")]
    public void EachSignalOutranksTheOnesBelowIt(string tag, string expected)
    {
        Calendars(tag).Should().Be(expected);
    }

    /// <summary>
    /// The override is used only "if calendar preference data for regionOverride are available", and CLDR
    /// lists only the regions that differ from the world's <c>gregory</c>. So an override naming a region it
    /// does not list keeps the region subtag's ordering - for an unknown region, and for the United States -
    /// while one naming a region it lists wins.
    /// </summary>
    [TestCase("th-TH-u-rg-zzzzzz", """["buddhist","gregory"]""")]
    [TestCase("th-TH-u-rg-uszzzz", """["buddhist","gregory"]""")]
    [TestCase("en-US-u-rg-irzzzz", """["persian","gregory","islamic-civil","islamic-tbla"]""")]
    public void AnOverrideCldrDoesNotListKeepsTheRegion(string tag, string expected)
    {
        Calendars(tag).Should().Be(expected);
    }

    /// <summary>A calendar the locale itself carries is the whole answer.</summary>
    [Test]
    public void TheLocalesOwnCalendarIsTheWholeAnswer()
    {
        Calendars("th-TH-u-ca-gregory").Should().Be("""["gregory"]""");
        _engine.Evaluate("JSON.stringify(new Intl.Locale('th', { calendar: 'japanese' }).getCalendars())")
            .AsString().Should().Be("""["japanese"]""");
    }

    /// <summary>
    /// The first calendar listed is the one <c>Intl.DateTimeFormat</c> defaults to: both read the same CLDR
    /// table, through the same region for a locale without keywords.
    /// </summary>
    [TestCase("en-US")]
    [TestCase("de-DE")]
    [TestCase("ar-EG")]
    [TestCase("ar-SA")]
    [TestCase("ja-JP")]
    [TestCase("ko-KR")]
    [TestCase("zh-TW")]
    [TestCase("he-IL")]
    [TestCase("th-TH")]
    [TestCase("th")]
    [TestCase("fa-IR")]
    [TestCase("fa")]
    [TestCase("ps-AF")]
    [TestCase("ps")]
    public void TheFirstCalendarIsTheOneDateTimeFormatDefaultsTo(string tag)
    {
        var first = _engine.Evaluate($"new Intl.Locale('{tag}').getCalendars()[0]").AsString();
        var formatter = _engine.Evaluate($"new Intl.DateTimeFormat('{tag}').resolvedOptions().calendar").AsString();

        first.Should().Be(formatter);
    }

    /// <summary>
    /// The list is filtered against the available calendars of the engine asking, so a host calendar
    /// provider that claims <c>islamic</c> gets it listed where CLDR lists it.
    /// </summary>
    [Test]
    public void ACalendarAHostProviderClaimsIsListed()
    {
        var engine = new Engine(options => options.Temporal.CalendarProvider = new WithObservationalIslamic());

        Calendars(engine, "ar-SA").Should().Be("""["gregory","islamic-umalqura","islamic"]""");
        Calendars(engine, "en-US").Should().Be("""["gregory"]""");
    }

    /// <summary>
    /// <see cref="ICldrProvider"/> has no member for the ordering, so a host moving the default calendar moves
    /// the formatter and not this list. Deliberate for now, and documented on
    /// <see cref="ICldrProvider.GetDefaultCalendar"/>.
    /// </summary>
    [Test]
    public void AHostDefaultCalendarMovesTheFormatterAndNotTheList()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new EverywhereIsHebrew());

        engine.Evaluate("new Intl.DateTimeFormat('en-US').resolvedOptions().calendar").AsString().Should().Be("hebrew");
        Calendars(engine, "en-US").Should().Be("""["gregory"]""");
    }

    private sealed class WithObservationalIslamic : DefaultCalendarProvider
    {
        public override IReadOnlyCollection<string> GetSupportedCalendars() => [.. base.GetSupportedCalendars(), "islamic"];
    }

    private sealed class EverywhereIsHebrew : DefaultCldrProvider
    {
        public override string? GetDefaultCalendar(string locale) => "hebrew";
    }
}

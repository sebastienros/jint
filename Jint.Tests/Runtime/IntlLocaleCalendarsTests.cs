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
    /// The formatter's default and this list are two <see cref="ICldrProvider"/> members, because the
    /// specification keys the one by the matched locale and the other by the whole tag. A host moving only the
    /// default calendar moves the formatter and not this list, and one moving only the list moves only the list;
    /// both members document that a host wanting them to agree overrides both.
    /// </summary>
    [Test]
    public void AHostMovingOneOfTheTwoMovesOnlyItsOwnReader()
    {
        var defaultOnly = new Engine(options => options.Intl.CldrProvider = new EverywhereIsHebrew());

        defaultOnly.Evaluate("new Intl.DateTimeFormat('en-US').resolvedOptions().calendar").AsString().Should().Be("hebrew");
        Calendars(defaultOnly, "en-US").Should().Be("""["gregory"]""");

        var listOnly = new Engine(options => options.Intl.CldrProvider = new AnswersWith(["hebrew", "gregory"]));

        listOnly.Evaluate("new Intl.DateTimeFormat('en-US').resolvedOptions().calendar").AsString().Should().Be("gregory");
        Calendars(listOnly, "en-US").Should().Be("""["hebrew","gregory"]""");
    }

    /// <summary>
    /// <see cref="ICldrProvider.GetCalendars"/> is asked for the whole tag, keywords included, and not at all
    /// when the locale carries its own calendar.
    /// </summary>
    [Test]
    public void AProviderIsAskedForTheWholeTag()
    {
        var provider = new RecordsWhatItWasAsked();
        var engine = new Engine(options => options.Intl.CldrProvider = provider);

        Calendars(engine, "en-US-u-rg-thzzzz").Should().Be("""["buddhist","gregory"]""");
        Calendars(engine, "th-TH-u-ca-gregory").Should().Be("""["gregory"]""");

        provider.Asked.Should().Equal("en-US-u-rg-thzzzz");
    }

    /// <summary>
    /// A provider with no opinion leaves the ordering to the embedded data, read for the same region.
    /// </summary>
    [TestCase("th", """["buddhist","gregory"]""")]
    [TestCase("en-US-u-rg-irzzzz", """["persian","gregory","islamic-civil","islamic-tbla"]""")]
    [TestCase("fa-u-sd-inka", """["gregory","indian"]""")]
    [TestCase("en-US", """["gregory"]""")]
    public void AProviderWithNoOpinionFallsBackThroughTheSameRegionPreference(string tag, string expected)
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new AnswersWith(null));

        Calendars(engine, tag).Should().Be(expected);
    }

    /// <summary>
    /// A host's answer goes through the same steps CLDR's does: each identifier canonicalized, kept once, and
    /// only if the engine can format in it — and <c>gregory</c> alone when nothing is left.
    /// </summary>
    [Test]
    public void AHostAnswerIsCanonicalizedFilteredAndListedOnce()
    {
        var engine = new Engine(options => options.Intl.CldrProvider =
            new AnswersWith(["mayan", "ethiopic-amete-alem", null!, "gregory", "GREGORY", "islamicc", "islamic", "ethioaa"]));
        Calendars(engine, "en-US").Should().Be("""["ethioaa","gregory","islamic-civil"]""");

        var nothingLeft = new Engine(options => options.Intl.CldrProvider = new AnswersWith(["mayan", ""]));
        Calendars(nothingLeft, "en-US").Should().Be("""["gregory"]""");

        var empty = new Engine(options => options.Intl.CldrProvider = new AnswersWith([]));
        Calendars(empty, "th").Should().Be("""["gregory"]""");
    }

    /// <summary>
    /// A host calling the shipped provider directly gets CLDR's own ordering — the identifiers the engine then
    /// leaves out included — in a new array every time, and a region CLDR lists nothing for is the world's.
    /// </summary>
    [Test]
    public void TheDefaultProviderAnswersCldrsOwnOrdering()
    {
        var provider = DefaultCldrProvider.Instance;

        provider.GetCalendars("ar-SA").Should().Equal("gregory", "islamic-umalqura", "islamic", "islamic-rgsa");
        provider.GetCalendars("th").Should().Equal("buddhist", "gregory");
        provider.GetCalendars("en-US-u-rg-thzzzz").Should().Equal("buddhist", "gregory");
        provider.GetCalendars("en-US").Should().Equal("gregory");
        provider.GetCalendars("eo").Should().Equal("gregory");

        var first = provider.GetCalendars("th")!;
        first[0] = "hebrew";
        provider.GetCalendars("th").Should().NotBeSameAs(first).And.Equal("buddhist", "gregory");
        Calendars("th").Should().Be("""["buddhist","gregory"]""");
    }

    private sealed class WithObservationalIslamic : DefaultCalendarProvider
    {
        public override IReadOnlyCollection<string> GetSupportedCalendars() => [.. base.GetSupportedCalendars(), "islamic"];
    }

    private sealed class EverywhereIsHebrew : DefaultCldrProvider
    {
        public override string? GetDefaultCalendar(string locale) => "hebrew";
    }

    private sealed class AnswersWith(string[]? calendars) : DefaultCldrProvider
    {
        public override string[]? GetCalendars(string locale) => calendars;
    }

    private sealed class RecordsWhatItWasAsked : DefaultCldrProvider
    {
        public List<string> Asked { get; } = [];

        public override string[]? GetCalendars(string locale)
        {
            Asked.Add(locale);
            return base.GetCalendars(locale);
        }
    }
}

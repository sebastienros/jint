#nullable enable

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Locale.prototype.getHourCycles</c> is https://tc39.es/ecma402/#sec-hourcyclesoflocale: CLDR's
/// <c>timeData</c> for the language and the region https://tc39.es/ecma402/#sec-regionpreference picks.
/// </summary>
/// <remarks>
/// Until issue #4159 the answer was one cycle read off the .NET culture's short time pattern, so it depended
/// on the machine's globalization data and ignored the <c>-u-rg-</c> and <c>-u-sd-</c> keywords. Each list
/// below is CLDR 48.2's preferred hour format followed by its allowed ones, as hour cycles and without
/// repeats. test262's <c>intl402/Locale/prototype/getHourCycles/*-region*.js</c> files pin the priority
/// order by comparing locales with each other; these pin the answers themselves.
/// </remarks>
public class IntlLocaleHourCyclesTests
{
    private readonly Engine _engine = new();

    private string HourCycles(string tag) => _engine.Evaluate($"JSON.stringify(new Intl.Locale('{tag}').getHourCycles())").AsString();

    /// <summary>
    /// A region's own list: the preferred cycle first, then the others in common use. Japan lists the
    /// 0-11 clock as well, and Iceland only the 24-hour one.
    /// </summary>
    [TestCase("en-US", """["h12","h23"]""")]
    [TestCase("en-GB", """["h23","h12"]""")]
    [TestCase("de-DE", """["h23","h12"]""")]
    [TestCase("ko-KR", """["h12","h23"]""")]
    [TestCase("ja-JP", """["h23","h11","h12"]""")]
    [TestCase("is-IS", """["h23"]""")]
    public void ARegionsOwnHourCycles(string tag, string expected)
    {
        HourCycles(tag).Should().Be(expected);
    }

    /// <summary>
    /// CLDR keys some entries by language and region, and those come before the region's own:
    /// <c>fr_CA</c> is a 24-hour clock in a 12-hour region, <c>en_001</c> a 12-hour clock in a 24-hour
    /// world. <c>und</c> is no language CLDR keys anything by, so it reads the region alone.
    /// </summary>
    [TestCase("fr-CA", """["h23","h12"]""")]
    [TestCase("en-CA", """["h12","h23"]""")]
    [TestCase("und-CA", """["h12","h23"]""")]
    [TestCase("en-001", """["h12","h23"]""")]
    [TestCase("eo-001", """["h23","h12"]""")]
    [TestCase("ku-SY", """["h23","h12"]""")]
    [TestCase("ar-SY", """["h12","h23"]""")]
    public void TheLanguageAndRegionAreTriedBeforeTheRegion(string tag, string expected)
    {
        HourCycles(tag).Should().Be(expected);
    }

    /// <summary>
    /// Each level of the priority order, on a tag that also carries every lower level: <c>GB</c> is a
    /// 24-hour region and <c>US</c> a 12-hour one, <c>en</c> is likely <c>en-US</c>, and <c>eo</c> has no
    /// likely region, so it is the world's. These are the levels test262's <c>region-priority.js</c> uses.
    /// </summary>
    [TestCase("en-US-u-sd-gbeng-rg-gbzzzz", """["h23","h12"]""")]
    [TestCase("en-US-u-sd-gbeng", """["h12","h23"]""")]
    [TestCase("en-u-sd-gbeng", """["h23","h12"]""")]
    [TestCase("en", """["h12","h23"]""")]
    [TestCase("eo", """["h23","h12"]""")]
    public void EachSignalOutranksTheOnesBelowIt(string tag, string expected)
    {
        HourCycles(tag).Should().Be(expected);
    }

    /// <summary>
    /// The override is looked up with the language too, so a French speaker in the United States whose
    /// preferences are Canadian gets <c>fr_CA</c>'s 24-hour clock, not <c>CA</c>'s 12-hour one.
    /// </summary>
    [TestCase("fr-US-u-rg-cazzzz", """["h23","h12"]""")]
    [TestCase("en-US-u-rg-cazzzz", """["h12","h23"]""")]
    public void TheOverrideIsLookedUpWithTheLanguage(string tag, string expected)
    {
        HourCycles(tag).Should().Be(expected);
    }

    /// <summary>
    /// A keyword naming a region CLDR has no time data for. The override is used only "if time data for
    /// regionOverride are available", so <c>rg</c> falls back to the region subtag; a subdivision is the
    /// region, so <c>sd</c> reaches a region with no data, and the algorithm answers <c>h23</c> alone - its
    /// own fallback, not the world's list. Haiti is a real region CLDR 48.2 lists no time data for.
    /// </summary>
    [TestCase("en-US-u-rg-zzzzzz", """["h12","h23"]""")]
    [TestCase("en-u-sd-zzzzzz", """["h23"]""")]
    [TestCase("ht-HT", """["h23"]""")]
    public void AKeywordOrRegionWithoutTimeData(string tag, string expected)
    {
        HourCycles(tag).Should().Be(expected);
    }

    /// <summary>An hour cycle the locale itself carries is the whole answer.</summary>
    [Test]
    public void TheLocalesOwnHourCycleIsTheWholeAnswer()
    {
        HourCycles("en-US-u-hc-h23").Should().Be("""["h23"]""");
        HourCycles("ja-JP-u-hc-h11-rg-uszzzz").Should().Be("""["h11"]""");
        _engine.Evaluate("JSON.stringify(new Intl.Locale('en-GB', { hourCycle: 'h24' }).getHourCycles())")
            .AsString().Should().Be("""["h24"]""");
    }

    /// <summary>Each call builds a new array, so a script writing to one cannot change the next answer.</summary>
    [Test]
    public void EachCallReturnsANewArray()
    {
        _engine.Evaluate("""
            (function () {
                var locale = new Intl.Locale('en-US');
                var first = locale.getHourCycles();
                first[0] = 'h24';
                first.push('h11');
                return first !== locale.getHourCycles() && JSON.stringify(new Intl.Locale('en-US').getHourCycles());
            })()
            """).AsString().Should().Be("""["h12","h23"]""");
    }
}

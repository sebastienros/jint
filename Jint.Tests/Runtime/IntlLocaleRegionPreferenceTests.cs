#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Locale.prototype.getWeekInfo</c> reads its week data for the region
/// https://tc39.es/ecma402/#sec-regionpreference picks: a <c>-u-rg-</c> override CLDR has week data for,
/// then the region subtag, then the region of a <c>-u-sd-</c> subdivision, then the region Add Likely
/// Subtags supplies, then <c>001</c>.
/// </summary>
/// <remarks>
/// Until issue #4159 only the region subtag was read, so a tag without one fell straight to <c>001</c>
/// (<c>en</c> was Monday, where <c>en-US</c> is Sunday) and neither keyword was looked at. test262's
/// <c>intl402/Locale/prototype/getWeekInfo/*-region*.js</c> files pin the priority order with real
/// regions; the malformed and unknown keyword values, and the embedded-data fallback a provider with no
/// opinion takes, are pinned here.
/// </remarks>
public class IntlLocaleRegionPreferenceTests
{
    private readonly Engine _engine = new();

    private int FirstDay(string tag) => (int) _engine.Evaluate($"new Intl.Locale('{tag}').getWeekInfo().firstDay").AsNumber();

    private string Weekend(string tag) => _engine.Evaluate($"JSON.stringify(new Intl.Locale('{tag}').getWeekInfo().weekend)").AsString();

    /// <summary>The table issue #4159 was filed with.</summary>
    [TestCase("en-US-u-rg-gbzzzz", 1)]
    [TestCase("en-GB-u-rg-uszzzz", 7)]
    [TestCase("en", 7)]
    [TestCase("en-US", 7)]
    public void TheIssueTable(string tag, int expected)
    {
        FirstDay(tag).Should().Be(expected);
    }

    /// <summary>
    /// Each level of the priority order, on a tag that also carries every lower level: <c>fa</c> is likely
    /// <c>IR</c> (Saturday, a Friday weekend), <c>inka</c> is in <c>IN</c> (Sunday, a Sunday weekend),
    /// <c>JP</c> is Sunday with a Saturday-Sunday weekend, and <c>AF</c> is Saturday with a
    /// Thursday-Friday weekend. <c>eo</c> has no likely region, so it is the world's Monday.
    /// </summary>
    [TestCase("fa-JP-u-sd-inka-rg-afzzzz", 6, "[4,5]")]
    [TestCase("fa-JP-u-sd-inka", 7, "[6,7]")]
    [TestCase("fa-u-sd-inka", 7, "[7]")]
    [TestCase("fa", 6, "[5]")]
    [TestCase("eo", 1, "[6,7]")]
    public void EachSignalOutranksTheOnesBelowIt(string tag, int firstDay, string weekend)
    {
        FirstDay(tag).Should().Be(firstDay);
        Weekend(tag).Should().Be(weekend);
    }

    /// <summary>
    /// A subdivision is only consulted when the tag has no region subtag of its own.
    /// </summary>
    [TestCase("en-u-sd-afgh", 6)]
    [TestCase("en-US-u-sd-afgh", 7)]
    [TestCase("en-GB-u-sd-usca", 1)]
    public void TheSubdivisionStandsInForAMissingRegionSubtag(string tag, int expected)
    {
        FirstDay(tag).Should().Be(expected);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-canonicalunicodesubdivision answers undefined for a value that is not a
    /// <c>unicode_subdivision_id</c> - a region of two letters or three digits followed by one to four
    /// alphanumerics - so each of these tags reads as if the keyword were absent: <c>US</c>'s Sunday, which
    /// for the bare <c>en</c> is reached through its likely region.
    /// </summary>
    [TestCase("en-US-u-rg")]
    [TestCase("en-US-u-rg-gbzzzzz")]
    [TestCase("en-US-u-rg-123")]
    [TestCase("en-US-u-rg-gb1-zzz")]
    [TestCase("en-US-u-rg-g1zzz")]
    [TestCase("en-u-sd")]
    [TestCase("en-u-sd-gbengxx")]
    [TestCase("en-u-sd-12abc")]
    public void AMalformedKeywordValueIsIgnored(string tag)
    {
        FirstDay(tag).Should().Be(7);
    }

    /// <summary>
    /// The region of a well-formed value is canonicalized like the region subtag of <c>"und-" + region</c>,
    /// so a deprecated code and a numeric one both reach the region they alias.
    /// </summary>
    [TestCase("en-US-u-rg-ukzzzz", 1)]
    [TestCase("en-US-u-rg-826zzzz", 1)]
    [TestCase("en-u-sd-ukeng", 1)]
    public void TheKeywordsRegionIsCanonicalized(string tag, int expected)
    {
        FirstDay(tag).Should().Be(expected);
    }

    /// <summary>
    /// A well-formed value naming a region CLDR has no week data for behaves differently for the two keys.
    /// The override is used only "if week data for region regionOverride are available", so <c>rg</c>
    /// falls back to the region subtag; the subdivision is the region, so <c>sd</c> skips the likely
    /// subtags and the region with no data reads the world's Monday.
    /// </summary>
    [TestCase("en-US-u-rg-zzzzzz", 7)]
    [TestCase("en-u-sd-zzzzzz", 1)]
    public void AKeywordNamingARegionWithoutWeekData(string tag, int expected)
    {
        FirstDay(tag).Should().Be(expected);
    }

    /// <summary>The first-day keyword still wins over whichever region was picked.</summary>
    [Test]
    public void TheFirstDayKeywordStillWinsOverTheRegionOverride()
    {
        FirstDay("en-u-fw-sun-rg-gbzzzz").Should().Be(7);
        Weekend("en-u-fw-sun-rg-afzzzz").Should().Be("[4,5]");
    }

    /// <summary>
    /// A provider that answers null for a locale leaves getWeekInfo on the embedded CLDR week data, and
    /// that fallback picks its region the same way the default provider does.
    /// </summary>
    [Test]
    public void AProviderWithNoOpinionFallsBackThroughTheSameRegionPreference()
    {
        var engine = new Engine(options => options.Intl.CldrProvider = new NoWeekInfo());

        engine.Evaluate("new Intl.Locale('en').getWeekInfo().firstDay").AsNumber().Should().Be(7);
        engine.Evaluate("new Intl.Locale('en-US-u-rg-gbzzzz').getWeekInfo().firstDay").AsNumber().Should().Be(1);
        engine.Evaluate("JSON.stringify(new Intl.Locale('en-u-sd-afgh').getWeekInfo().weekend)").AsString().Should().Be("[4,5]");
    }

    /// <summary>A host calling the default provider directly gets the same region the script does.</summary>
    [Test]
    public void TheDefaultProviderPicksTheSameRegion()
    {
        DefaultCldrProvider.Instance.GetWeekInfo("en-US-u-rg-gbzzzz")!.FirstDay.Should().Be(DayOfWeek.Monday);
        DefaultCldrProvider.Instance.GetWeekInfo("en")!.FirstDay.Should().Be(DayOfWeek.Sunday);
        DefaultCldrProvider.Instance.GetWeekInfo("fa")!.Weekend.Should().Equal(DayOfWeek.Friday);
    }

    private sealed class NoWeekInfo : DefaultCldrProvider
    {
        public override WeekInfo? GetWeekInfo(string locale) => null;
    }
}

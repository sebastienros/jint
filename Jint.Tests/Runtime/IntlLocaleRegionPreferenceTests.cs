#nullable enable

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
/// regions; the malformed and unknown keyword values are pinned here. On this branch <c>getWeekInfo</c>
/// reads the embedded CLDR week data directly and never asks the configured <c>ICldrProvider</c>, so the
/// provider tests <c>main</c> has for this change have nothing to exercise here.
/// </remarks>
public class IntlLocaleRegionPreferenceTests
{
    private readonly Engine _engine = new();

    private int FirstDay(string tag) => (int) _engine.Evaluate($"new Intl.Locale('{tag}').getWeekInfo().firstDay").AsNumber();

    private string Weekend(string tag) => _engine.Evaluate($"JSON.stringify(new Intl.Locale('{tag}').getWeekInfo().weekend)").AsString();

    /// <summary>The table issue #4159 was filed with.</summary>
    [Theory]
    [InlineData("en-US-u-rg-gbzzzz", 1)]
    [InlineData("en-GB-u-rg-uszzzz", 7)]
    [InlineData("en", 7)]
    [InlineData("en-US", 7)]
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
    [Theory]
    [InlineData("fa-JP-u-sd-inka-rg-afzzzz", 6, "[4,5]")]
    [InlineData("fa-JP-u-sd-inka", 7, "[6,7]")]
    [InlineData("fa-u-sd-inka", 7, "[7]")]
    [InlineData("fa", 6, "[5]")]
    [InlineData("eo", 1, "[6,7]")]
    public void EachSignalOutranksTheOnesBelowIt(string tag, int firstDay, string weekend)
    {
        FirstDay(tag).Should().Be(firstDay);
        Weekend(tag).Should().Be(weekend);
    }

    /// <summary>
    /// A subdivision is only consulted when the tag has no region subtag of its own.
    /// </summary>
    [Theory]
    [InlineData("en-u-sd-afgh", 6)]
    [InlineData("en-US-u-sd-afgh", 7)]
    [InlineData("en-GB-u-sd-usca", 1)]
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
    [Theory]
    [InlineData("en-US-u-rg")]
    [InlineData("en-US-u-rg-gbzzzzz")]
    [InlineData("en-US-u-rg-123")]
    [InlineData("en-US-u-rg-gb1-zzz")]
    [InlineData("en-US-u-rg-g1zzz")]
    [InlineData("en-u-sd")]
    [InlineData("en-u-sd-gbengxx")]
    [InlineData("en-u-sd-12abc")]
    public void AMalformedKeywordValueIsIgnored(string tag)
    {
        FirstDay(tag).Should().Be(7);
    }

    /// <summary>
    /// The region of a well-formed value is canonicalized like the region subtag of <c>"und-" + region</c>,
    /// so a deprecated code and a numeric one both reach the region they alias.
    /// </summary>
    [Theory]
    [InlineData("en-US-u-rg-ukzzzz", 1)]
    [InlineData("en-US-u-rg-826zzzz", 1)]
    [InlineData("en-u-sd-ukeng", 1)]
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
    [Theory]
    [InlineData("en-US-u-rg-zzzzzz", 7)]
    [InlineData("en-u-sd-zzzzzz", 1)]
    public void AKeywordNamingARegionWithoutWeekData(string tag, int expected)
    {
        FirstDay(tag).Should().Be(expected);
    }

    /// <summary>The first-day keyword still wins over whichever region was picked.</summary>
    [Fact]
    public void TheFirstDayKeywordStillWinsOverTheRegionOverride()
    {
        FirstDay("en-u-fw-sun-rg-gbzzzz").Should().Be(7);
        Weekend("en-u-fw-sun-rg-afzzzz").Should().Be("[4,5]");
    }
}

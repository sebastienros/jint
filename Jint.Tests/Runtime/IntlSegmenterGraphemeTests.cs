#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Segmenter</c> with <c>granularity: "grapheme"</c> follows the extended grapheme cluster rules of Unicode UAX #29
/// (https://www.unicode.org/reports/tr29/#Grapheme_Cluster_Boundary_Rules), GB1 to GB999, which ICU's root rules
/// (icu4c/source/data/brkitr/rules/char.txt) implement without tailoring, and which ECMA-402 leaves to the implementation
/// (https://tc39.es/ecma402/#sec-intl.segmenter.prototype.segment). The clusters are those of Unicode 17.0.0 on every
/// target framework, not those of the runtime's own Unicode data.
/// Every expected value was produced by Node 26.13.1 (ICU 78.3, Unicode 17.0) with
/// <c>[...new Intl.Segmenter('en', { granularity: 'grapheme' }).segment(s)].map(x => x.segment).join('|')</c>.
/// </summary>
public class IntlSegmenterGraphemeTests
{
    private const string Segment = "[...new Intl.Segmenter('en', { granularity: 'grapheme' }).segment(input)].map(x => x.segment).join('|')";

    [Theory]
    // The six inputs .NET Framework's text elements split: an emoji modifier, an emoji ZWJ sequence, a flag, CR LF,
    // a Hangul syllable of conjoining jamo, and a mark on a letter newer than its Unicode data.
    [InlineData("\U0001F44D\U0001F3FD", "\U0001F44D\U0001F3FD")]
    [InlineData("\U0001F469\u200D\U0001F4BB", "\U0001F469\u200D\U0001F4BB")]
    [InlineData("\U0001F1EB\U0001F1EE", "\U0001F1EB\U0001F1EE")]
    [InlineData("\r\n", "\r\n")]
    [InlineData("\u1100\u1161\u11A8", "\u1100\u1161\u11A8")]
    [InlineData("\U0001E4D0\u0301", "\U0001E4D0\u0301")]

    // GB3 to GB5: line breaks and controls
    [InlineData("a\r\nb", "a|\r\n|b")]
    [InlineData("\n\r", "\n|\r")]
    [InlineData("\r\r\n", "\r|\r\n")]
    [InlineData("a\u0301\r\u0301", "a\u0301|\r|\u0301")]
    [InlineData("\0\u0301", "\0|\u0301")]
    [InlineData("a\u00AD", "a|\u00AD")]
    [InlineData("\u200B\u0301", "\u200B|\u0301")]

    // GB6 to GB8: Hangul syllables
    [InlineData("\u1100\u1100\u1161", "\u1100\u1100\u1161")]
    [InlineData("\uAC00\u11A8\u11A8", "\uAC00\u11A8\u11A8")]
    [InlineData("\uAC01\u11A8", "\uAC01\u11A8")]
    [InlineData("\uAC01\u1161", "\uAC01|\u1161")]
    [InlineData("\u1161\u11A8\u1100", "\u1161\u11A8|\u1100")]
    [InlineData("\u11A8\u1161", "\u11A8|\u1161")]
    [InlineData("\uD55C\uAD6D\uC5B4", "\uD55C|\uAD6D|\uC5B4")]

    // GB9 and GB9a: extending and spacing marks
    [InlineData("e\u0301\u0302x", "e\u0301\u0302|x")]
    [InlineData("a\u200Db", "a\u200D|b")]
    [InlineData("\u0915\u093F", "\u0915\u093F")]
    [InlineData("a\u0903", "a\u0903")]
    [InlineData("\u0E01\u0E33", "\u0E01\u0E33")]
    [InlineData(" \u0301", " \u0301")]

    // GB9b: prepended concatenation marks
    [InlineData("\u0600a", "\u0600a")]
    [InlineData("\u0600\u0600a", "\u0600\u0600a")]
    [InlineData("\u0600\r", "\u0600|\r")]
    [InlineData("\U000110BD\U00011083", "\U000110BD\U00011083")]
    [InlineData("\u0600 ", "\u0600 ")]

    // GB9c: Indic conjuncts, a consonant joined to the one before it by a virama (InCB=Linker)
    [InlineData("\u0915\u094D\u0937", "\u0915\u094D\u0937")]
    [InlineData("\u0915\u094D\u200D\u0937", "\u0915\u094D\u200D\u0937")]
    [InlineData("\u0915\u093C\u094D\u0937", "\u0915\u093C\u094D\u0937")]
    [InlineData("\u0915\u094D\u094D\u0937", "\u0915\u094D\u094D\u0937")]
    [InlineData("\u0915\u094D\u0937\u094D\u0930", "\u0915\u094D\u0937\u094D\u0930")]
    [InlineData("\u0915\u0937", "\u0915|\u0937")]
    [InlineData("\u0915\u093F\u0937", "\u0915\u093F|\u0937")]
    [InlineData("a\u094D\u0937", "a\u094D|\u0937")]
    [InlineData("\u0995\u09CD\u09B7", "\u0995\u09CD\u09B7")]
    [InlineData("\u0D15\u0D4D\u0D37", "\u0D15\u0D4D\u0D37")]
    [InlineData("\u0915\u094Da", "\u0915\u094D|a")]

    // GB11: emoji ZWJ sequences
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466", "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466")]
    [InlineData("\U0001F3F3\uFE0F\u200D\U0001F308", "\U0001F3F3\uFE0F\u200D\U0001F308")]
    [InlineData("\U0001F44B\U0001F3FD\u200D\U0001F525", "\U0001F44B\U0001F3FD\u200D\U0001F525")]
    [InlineData("a\u200D\U0001F600", "a\u200D|\U0001F600")]
    [InlineData("\U0001F600\u200D\u200D\U0001F600", "\U0001F600\u200D\u200D|\U0001F600")]
    [InlineData("\U0001F600\u0301\u200D\U0001F600", "\U0001F600\u0301\u200D\U0001F600")]
    [InlineData("\u00A9\u200D\U0001F600", "\u00A9\u200D\U0001F600")]
    [InlineData("\U0001F600\u200D a", "\U0001F600\u200D| |a")]
    [InlineData("1\uFE0F\u20E3", "1\uFE0F\u20E3")]

    // GB12 and GB13: regional indicators pair up from the start of a run
    [InlineData("\U0001F1EB\U0001F1EE\U0001F1F8\U0001F1EA", "\U0001F1EB\U0001F1EE|\U0001F1F8\U0001F1EA")]
    [InlineData("\U0001F1EB\U0001F1EE\U0001F1F8", "\U0001F1EB\U0001F1EE|\U0001F1F8")]
    [InlineData("a\U0001F1EB\U0001F1EE\U0001F1EBb", "a|\U0001F1EB\U0001F1EE|\U0001F1EB|b")]
    [InlineData("\U0001F1EB\u0301\U0001F1EE", "\U0001F1EB\u0301|\U0001F1EE")]
    [InlineData("\u0600\U0001F1EB\U0001F1EE\U0001F1F8", "\u0600\U0001F1EB\U0001F1EE|\U0001F1F8")]

    // ASCII and other text
    [InlineData("Hello, world!", "H|e|l|l|o|,| |w|o|r|l|d|!")]
    [InlineData("a\tb ", "a|\t|b| ")]
    [InlineData("caf\u00E9", "c|a|f|\u00E9")]
    [InlineData("cafe\u0301s", "c|a|f|e\u0301|s")]
    [InlineData("\U0001D400\U0001D401", "\U0001D400|\U0001D401")]
    [InlineData("\u65E5\u672C", "\u65E5|\u672C")]
    [InlineData("\u0E2A\u0E27\u0E31\u0E2A", "\u0E2A|\u0E27\u0E31|\u0E2A")]
    public void GraphemesFollowUax29(string input, string expected)
    {
        SegmentsOf(input).Should().Be(expected);
    }

    /// <summary>
    /// A lone surrogate is a code point of its own, of Grapheme_Cluster_Break Other, as ICU and Node treat it: GB999
    /// breaks on both sides of it, and GB9 joins a mark to it. These strings are built in code, not in attributes: an
    /// attribute stores its strings as UTF-8, which has no lone surrogate.
    /// </summary>
    [Fact]
    public void LoneSurrogatesAreClustersOfTheirOwn()
    {
        SegmentsOf("\uD800a").Should().Be("\uD800|a");
        SegmentsOf("a\uDC00\u0301").Should().Be("a|\uDC00\u0301");
        SegmentsOf("\uDC00\uD800").Should().Be("\uDC00|\uD800");
        SegmentsOf("\uD800\u0301").Should().Be("\uD800\u0301");
        SegmentsOf("\uD83D").Should().Be("\uD83D");
        SegmentsOf("\U0001F600\uD83D").Should().Be("\U0001F600|\uD83D");
    }

    /// <summary>
    /// The official conformance test of UAX #29 for Unicode 17.0.0 (GraphemeBreakTest.txt, vendored under
    /// tools/text-segmentation/), every line of it: the boundaries Intl.Segmenter finds are exactly the ones the line
    /// marks with ÷. Node 26 passes all of them.
    /// </summary>
    [Fact]
    public void TheUnicodeGraphemeBreakTestPasses()
    {
        var (cases, failures) = UnicodeBreakTestFile.Run("unicode-GraphemeBreakTest.txt", "# GraphemeBreakTest-17.0.0.txt", "grapheme");

        cases.Should().Be(766);
        failures.Should().BeEmpty("{0} of {1} lines fail, the first: {2}", failures.Count, cases, string.Join(Environment.NewLine, failures.Take(40)));
    }

    /// <summary>
    /// The segment data objects carry the code unit index each cluster starts at, and <c>containing</c> finds the cluster a
    /// code unit belongs to, also inside a surrogate pair, a flag and an emoji ZWJ sequence; the default granularity is
    /// the grapheme.
    /// </summary>
    [Fact]
    public void IndicesAndContainingFollowTheClusters()
    {
        var engine = new Engine();
        engine.SetValue("input", "a\r\n\U0001F1EB\U0001F1EE\U0001F469\u200D\U0001F4BBe\u0301");

        engine.Execute("const segs = new Intl.Segmenter('en').segment(input);");

        engine.Evaluate("[...segs].map(x => x.index).join()").AsString().Should().Be("0,1,3,7,12");
        engine.Evaluate("segs.containing(2).segment").AsString().Should().Be("\r\n");
        engine.Evaluate("segs.containing(5).segment").AsString().Should().Be("\U0001F1EB\U0001F1EE");
        engine.Evaluate("segs.containing(9).index").AsNumber().Should().Be(7);
        engine.Evaluate("segs.containing(13).segment").AsString().Should().Be("e\u0301");
        engine.Evaluate("'isWordLike' in segs.containing(0)").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// A long cluster and a long run of regional indicators are found in one pass: GB9 joins a hundred thousand marks to
    /// their base, and GB12 and GB13 pair the indicators from the start of the run.
    /// </summary>
    [Fact]
    public void LongClustersAreSegmentedInOnePass()
    {
        var engine = new Engine();
        engine.SetValue("marks", "a" + new string('\u0301', 100000) + "b");
        engine.SetValue("flags", string.Concat(Enumerable.Repeat("\U0001F1EB", 50001)));

        engine.Evaluate("[...new Intl.Segmenter('en').segment(marks)].map(x => x.segment.length).join()").AsString().Should().Be("100001,1");
        engine.Evaluate("const f = [...new Intl.Segmenter('en').segment(flags)]; f.length + ',' + f[0].segment.length + ',' + f[f.length - 1].segment.length").AsString().Should().Be("25001,4,2");
    }

    /// <summary>
    /// The properties of every ASCII character come from a table read without a search, the rest from a binary search of
    /// the runs; the two must be one property.
    /// </summary>
    [Fact]
    public void TheAsciiTableAgreesWithTheRuns()
    {
        for (var c = 0; c < 0x80; c++)
        {
            GraphemeBreaker.PropertiesOf(c).Should().Be(GraphemeBreaker.PropertiesOfNonAscii(c), "U+{0:X4} is ASCII", c);
        }
    }

    /// <summary>
    /// The Grapheme_Cluster_Break, Extended_Pictographic and Indic_Conjunct_Break properties of Unicode 17.0.0, on every
    /// target framework: the table does not depend on the runtime's Unicode data, so a character added after .NET
    /// Framework's data, such as a Nag Mundari letter, has its properties there as well.
    /// </summary>
    [Theory]
    [InlineData(0x000A, "LF", 0)]
    [InlineData(0x000D, "CR", 0)]
    [InlineData(0x0000, "Control", 0)]
    [InlineData(0x00AD, "Control", 0)]
    [InlineData(0x200B, "Control", 0)]
    [InlineData(0x0301, "Extend", GraphemeBreaker.InCBExtend)]
    [InlineData(0x093C, "Extend", GraphemeBreaker.InCBExtend)]
    [InlineData(0x094D, "Extend", GraphemeBreaker.InCBLinker)]
    [InlineData(0x0915, "Other", GraphemeBreaker.InCBConsonant)]
    [InlineData(0x0903, "SpacingMark", 0)]
    [InlineData(0x0600, "Prepend", 0)]
    [InlineData(0x200D, "ZWJ", GraphemeBreaker.InCBExtend)]
    [InlineData(0x1100, "L", 0)]
    [InlineData(0x1161, "V", 0)]
    [InlineData(0x11A8, "T", 0)]
    [InlineData(0xAC00, "LV", 0)]
    [InlineData(0xAC01, "LVT", 0)]
    [InlineData(0x00A9, "Other", GraphemeBreaker.ExtendedPictographic)]
    [InlineData(0x2701, "Other", 0)]
    [InlineData(0xD800, "Other", 0)]
    [InlineData(0x1F1E6, "RegionalIndicator", 0)]
    [InlineData(0x1F3FD, "Extend", GraphemeBreaker.InCBExtend)]
    [InlineData(0x1F600, "Other", GraphemeBreaker.ExtendedPictographic)]
    [InlineData(0x1E4D0, "Other", 0)]
    [InlineData(0xE0020, "Extend", GraphemeBreaker.InCBExtend)]
    [InlineData(0x10FFFF, "Other", 0)]
    public void PropertiesComeFromTheUnicodeTable(int codePoint, string expectedClass, int expectedFlags)
    {
        var properties = GraphemeBreaker.PropertiesOf(codePoint);
        ((GraphemeBreaker.GraphemeClass) (properties & 0x0F)).ToString().Should().Be(expectedClass);
        (properties & ~0x0F).Should().Be(expectedFlags);
    }

    private static string SegmentsOf(string input)
    {
        var engine = new Engine();
        engine.SetValue("input", input);
        return engine.Evaluate(Segment).AsString();
    }
}

#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Segmenter</c> with <c>granularity: "word"</c> follows the word boundary rules of Unicode UAX #29
/// (https://www.unicode.org/reports/tr29/#Word_Boundary_Rules), WB1 to WB999, as ICU's root rules tailor them
/// (icu4c/source/data/brkitr/rules/word.txt), which is what ECMA-402 leaves to the implementation
/// (https://tc39.es/ecma402/#sec-intl.segmenter.prototype.segment). ICU's dictionaries for Chinese, Japanese, Thai,
/// Lao, Khmer and Myanmar are not applied: a run of those characters stays one word.
/// Every expected value was produced by Node 26.13.1 (ICU 78.3, Unicode 17.0) with
/// <c>[...new Intl.Segmenter('en', { granularity: 'word' }).segment(s)].map(x => x.segment + (x.isWordLike ? '*' : '')).join('|')</c>:
/// the segments of a row are joined with <c>|</c>, and a word-like segment is marked with <c>*</c>.
/// </summary>
public class IntlSegmenterWordTests
{
    private const string Segment = "[...new Intl.Segmenter('en', { granularity: 'word' }).segment(input)].map(x => x.segment + (x.isWordLike ? '*' : '')).join('|')";

    [Theory]
    // sebastienros/jint#4683: the eleven inputs of the issue that were segmented wrongly...
    [InlineData("e-mail me", "e*|-|mail*| |me*")]
    [InlineData("co-op's", "co*|-|op's*")]
    [InlineData("don\u2019t", "don\u2019t*")]
    [InlineData("a.b", "a.b*")]
    [InlineData("foo.bar baz", "foo.bar*| |baz*")]
    [InlineData("x@y.com", "x*|@|y.com*")]
    [InlineData("U.S.A. now", "U.S.A*|.| |now*")]
    [InlineData("e.g.", "e.g*|.")]
    [InlineData("a_b c", "a_b*| |c*")]
    [InlineData("thumbs \U0001F44D\U0001F3FD up", "thumbs*| |\U0001F44D\U0001F3FD| |up*")]
    [InlineData("\U0001F469\u200D\U0001F4BB dev", "\U0001F469\u200D\U0001F4BB| |dev*")]
    // ...and the seven it found right
    [InlineData("can't stop", "can't*| |stop*")]
    [InlineData("version 1.2.3 ok", "version*| |1.2.3*| |ok*")]
    [InlineData("3.14 pi", "3.14*| |pi*")]
    [InlineData("hello, world!", "hello*|,| |world*|!")]
    [InlineData("123,456.78", "123,456.78*")]
    [InlineData("l'avion", "l'avion*")]
    [InlineData("1,000", "1,000*")]
    public void IssueInputsFollowUax29(string input, string expected)
    {
        SegmentsOf(input).Should().Be(expected);
    }

    [Theory]
    // WB3, WB3a and WB3b: a line break is a segment of its own, CR LF one segment
    [InlineData("a\r\nb", "a*|\r\n|b*")]
    [InlineData("a\rb", "a*|\r|b*")]
    [InlineData("a\n\nb", "a*|\n|\n|b*")]
    [InlineData("a\u0085b", "a*|\u0085|b*")]
    [InlineData("a\u2028b", "a*|\u2028|b*")]
    [InlineData("\r\n\u0301a", "\r\n|\u0301|a*")]
    // WB3c: ZWJ × Extended_Pictographic, also after a ZWJ that starts the text
    [InlineData("\u200D\U0001F600", "\u200D\U0001F600")]
    [InlineData("a\u200D\U0001F600", "a\u200D\U0001F600")]
    [InlineData("\U0001F600\u200D\U0001F600", "\U0001F600\u200D\U0001F600")]
    [InlineData("\U0001F3F3\uFE0F\u200D\U0001F308", "\U0001F3F3\uFE0F\u200D\U0001F308")]
    [InlineData("\U0001F600\u200D\u0301\U0001F600", "\U0001F600\u200D\u0301|\U0001F600")]
    // WB3d: horizontal white space stays together, other spaces do not
    [InlineData("a  b", "a*|  |b*")]
    [InlineData("a \u3000b", "a*| \u3000|b*")]
    [InlineData("a\u00A0b", "a*|\u00A0|b*")]
    [InlineData("a\t b", "a*|\t| |b*")]
    // WB4: Extend, Format and ZWJ belong to the character before them, except at the start of the text
    [InlineData("a\u0301b", "a\u0301b*")]
    [InlineData("a\u00ADb", "a\u00ADb*")]
    [InlineData("\u0301a", "\u0301|a*")]
    [InlineData(" \u0301 a", " \u0301| |a*")]
    // WB5 to WB7: letters, and MidLetter or MidNumLet between two of them
    [InlineData("abc", "abc*")]
    [InlineData("\u05D0\u05D1a", "\u05D0\u05D1a*")]
    [InlineData("a:b", "a:b*")]
    [InlineData("a\u00B7b", "a\u00B7b*")]
    [InlineData("a'b", "a'b*")]
    [InlineData("a..b", "a*|.|.|b*")]
    [InlineData("a.", "a*|.")]
    [InlineData(".a", ".|a*")]
    [InlineData("a.\u0301b", "a.\u0301b*")]
    [InlineData("a\u0301.b", "a\u0301.b*")]
    [InlineData("a.b.c", "a.b.c*")]
    [InlineData("a.:b", "a*|.|:|b*")]
    // WB7a to WB7c: Hebrew letters and quotation marks
    [InlineData("\u05D0'", "\u05D0'*")]
    [InlineData("\u05D0\"\u05D1", "\u05D0\"\u05D1*")]
    [InlineData("\u05D0\"", "\u05D0*|\"")]
    [InlineData("a\"b", "a*|\"|b*")]
    [InlineData("\u05D0'\u0301", "\u05D0'\u0301")]
    // WB8 to WB12: numbers, and MidNum or MidNumLet between two digits
    [InlineData("123", "123*")]
    [InlineData("a1", "a1*")]
    [InlineData("1a", "1a*")]
    [InlineData("1.2", "1.2*")]
    [InlineData("1,2", "1,2*")]
    [InlineData("1;2", "1;2*")]
    [InlineData("1:2", "1*|:|2*")]
    [InlineData("1'2", "1'2*")]
    [InlineData("1..2", "1*|.|.|2*")]
    [InlineData("1.", "1*|.")]
    [InlineData("1.a", "1*|.|a*")]
    [InlineData("a.1", "a*|.|1*")]
    [InlineData("1.2.3.4", "1.2.3.4*")]
    [InlineData("\u0661\u066B\u0662", "\u0661\u066B\u0662*")]
    [InlineData("$5", "$|5*")]
    [InlineData("50%", "50*|%")]
    // WB13 to WB13b: Katakana, and ExtendNumLet joining letters, numbers and Katakana
    [InlineData("\u30C6\u30AD\u30B9\u30C8", "\u30C6\u30AD\u30B9\u30C8*")]
    [InlineData("\u30C6\u0301\u30C6", "\u30C6\u0301\u30C6*")]
    [InlineData("_", "_")]
    [InlineData("__", "__*")]
    [InlineData("a_", "a_*")]
    [InlineData("_a", "_a*")]
    [InlineData("1_", "1_*")]
    [InlineData("_1", "_1*")]
    [InlineData("\u30C6_", "\u30C6_*")]
    [InlineData("a__b", "a__b*")]
    [InlineData("a_\u0301b", "a_\u0301b*")]
    [InlineData("\u203F", "\u203F")]
    // WB15 and WB16: regional indicators pair up
    [InlineData("\U0001F1EB\U0001F1EE\U0001F1F8\U0001F1EA", "\U0001F1EB\U0001F1EE|\U0001F1F8\U0001F1EA")]
    [InlineData("\U0001F1EB\U0001F1EE\U0001F1F8", "\U0001F1EB\U0001F1EE|\U0001F1F8")]
    [InlineData("a\U0001F1EB\U0001F1EE", "a*|\U0001F1EB\U0001F1EE")]
    [InlineData("\U0001F1EB\u0301\U0001F1EE", "\U0001F1EB\u0301\U0001F1EE")]
    // WB999: everything else is a segment of its own
    [InlineData("#tag", "#|tag*")]
    [InlineData("@user", "@|user*")]
    [InlineData("a\u200B", "a*|\u200B")]
    [InlineData("\U0001FAE8 x", "\U0001FAE8| |x*")]
    [InlineData("\U0001FAF7\U0001F3FB", "\U0001FAF7\U0001F3FB")]
    // Other scripts
    [InlineData("\u05D0\u05BC", "\u05D0\u05BC*")]
    [InlineData("\u0915\u094D\u0937", "\u0915\u094D\u0937*")]
    [InlineData("\u0645\u0631\u062D\u0628\u0627", "\u0645\u0631\u062D\u0628\u0627*")]
    // Letters newer than the Unicode data of .NET Framework: Nag Mundari (15.0), Arabic Extended-B (14.0), Sidetic (17.0)
    [InlineData("\U0001E4D0\U0001E4D1", "\U0001E4D0\U0001E4D1*")]
    [InlineData("\u0870\u0871", "\u0870\u0871*")]
    [InlineData("\U00010940\U00010941", "\U00010940\U00010941*")]
    public void WordsFollowUax29(string input, string expected)
    {
        SegmentsOf(input).Should().Be(expected);
    }

    /// <summary>
    /// <c>isWordLike</c> is ICU's rule status: a segment is word-like when the rule it ends with gives it a status of a
    /// number, a letter, kana or an ideograph (UBRK_WORD_NUMBER and above). The status of most rules belongs to their
    /// last letter and holds over the Extend and Format characters after it, but some (WB7a, WB13a, a Hangul syllable)
    /// end at their last character, so <c>a_</c> is word-like and <c>a_</c> with a combining mark after it is not.
    /// </summary>
    [Theory]
    [InlineData("\u00AA", "\u00AA*")]
    [InlineData("\u02B0", "\u02B0*")]
    [InlineData("\u216B", "\u216B*")]
    [InlineData("\u00B2", "\u00B2")]
    [InlineData("\u00BD", "\u00BD")]
    [InlineData("\u2081", "\u2081")]
    [InlineData("\u00A9", "\u00A9")]
    [InlineData("\u24C2", "\u24C2*")]
    [InlineData("\u24C2a", "\u24C2a*")]
    [InlineData("\u0E50", "\u0E50*")]
    [InlineData("\u65E5", "\u65E5*")]
    [InlineData("\u306E", "\u306E*")]
    [InlineData("\u30C6", "\u30C6*")]
    [InlineData("\u3005", "\u3005")]
    [InlineData("\U00018B00", "\U00018B00*")]
    [InlineData("\U00016FE4", "\U00016FE4*")]
    [InlineData("\uD55C", "\uD55C*")]
    [InlineData("\uD55C\u0301", "\uD55C\u0301")]
    [InlineData("\uD55C\U00016FE4", "\uD55C\U00016FE4*")]
    [InlineData("a_\u0301", "a_\u0301")]
    public void IsWordLikeFollowsIcuRuleStatus(string input, string expected)
    {
        SegmentsOf(input).Should().Be(expected);
    }

    /// <summary>
    /// ICU's root rules leave Hangul syllables out of the letters of WB5, WB9 and WB10, and join a run of them; Thai,
    /// Lao, Khmer and Myanmar letters (Line_Break=Complex_Context) are letters there, though UAX #29 gives them no
    /// Word_Break value of their own.
    /// </summary>
    [Theory]
    [InlineData("a\uD55C", "a*|\uD55C*")]
    [InlineData("\uD55Ca", "\uD55C*|a*")]
    [InlineData("1\uD55C", "1*|\uD55C*")]
    [InlineData("\uD55C\uD55C x", "\uD55C\uD55C*| |x*")]
    [InlineData("\u0E01", "\u0E01*")]
    [InlineData("\u0E01 \u0E02", "\u0E01*| |\u0E02*")]
    [InlineData("a\u0E01", "a\u0E01*")]
    public void IcuTailoringOfHangulAndComplexContext(string input, string expected)
    {
        SegmentsOf(input).Should().Be(expected);
    }

    /// <summary>
    /// ICU hands a run of Chinese, Japanese, Thai, Lao, Khmer or Myanmar characters to a dictionary, which Jint does not
    /// have: the run stays one word-like segment, the one ICU's rules give before the dictionary splits it. Node gives
    /// <c>日本語*|の*|テキスト*</c> and <c>สวัสดี*|ครับ*</c>; a run of Katakana is one word in both.
    /// </summary>
    [Theory]
    [InlineData("\u65E5\u672C\u8A9E\u306E\u30C6\u30AD\u30B9\u30C8", "\u65E5\u672C\u8A9E\u306E\u30C6\u30AD\u30B9\u30C8*")]
    [InlineData("\u30C6\u30AD\u30B9\u30C8", "\u30C6\u30AD\u30B9\u30C8*")]
    [InlineData("\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35\u0E04\u0E23\u0E31\u0E1A", "\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35\u0E04\u0E23\u0E31\u0E1A*")]
    public void DictionaryScriptsStayOneRun(string input, string expected)
    {
        SegmentsOf(input).Should().Be(expected);
    }

    /// <summary>
    /// The official conformance test of UAX #29 for Unicode 17.0.0 (WordBreakTest.txt, vendored under
    /// tools/text-segmentation/), every line of it: the boundaries Intl.Segmenter finds are exactly the ones the line
    /// marks with ÷. Node 26 passes all of them.
    /// </summary>
    [Fact]
    public void TheUnicodeWordBreakTestPasses()
    {
        var (cases, failures) = UnicodeBreakTestFile.Run("unicode-WordBreakTest.txt", "# WordBreakTest-17.0.0.txt", "word");

        cases.Should().Be(1944);
        failures.Should().BeEmpty("{0} of {1} lines fail, the first: {2}", failures.Count, cases, string.Join(Environment.NewLine, failures.Take(40)));
    }

    /// <summary>
    /// The segment data objects carry the code unit index each word starts at, and <c>containing</c> finds the
    /// segment a code unit belongs to, also inside a surrogate pair and an emoji sequence.
    /// </summary>
    [Fact]
    public void IndicesAndContainingFollowTheWords()
    {
        var engine = new Engine();
        engine.SetValue("input", "a.b \U0001F469\u200D\U0001F4BB \U0001E4D0x");

        engine.Execute("const segs = new Intl.Segmenter('en', { granularity: 'word' }).segment(input);");

        engine.Evaluate("[...segs].map(x => x.index).join()").AsString().Should().Be("0,3,4,9,10");
        engine.Evaluate("segs.containing(1).segment").AsString().Should().Be("a.b");
        engine.Evaluate("segs.containing(6).segment").AsString().Should().Be("\U0001F469\u200D\U0001F4BB");
        engine.Evaluate("segs.containing(6).isWordLike").AsBoolean().Should().BeFalse();
        engine.Evaluate("segs.containing(11).segment").AsString().Should().Be("\U0001E4D0x");
        engine.Evaluate("segs.containing(11).isWordLike").AsBoolean().Should().BeTrue();
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
            WordBreaker.PropertiesOf(c).Should().Be(WordBreaker.PropertiesOfNonAscii(c), "U+{0:X4} is ASCII", c);
        }
    }

    /// <summary>
    /// The classes and flags of Unicode 17.0.0 as ICU's root rules see them, on every target framework: the table does
    /// not depend on the runtime's Unicode data, so a character added after .NET Framework's data, such as a CJK
    /// extension ideograph or a Sidetic letter, has its class there as well.
    /// </summary>
    [Theory]
    [InlineData(0x0027, "SingleQuote", 0)]
    [InlineData(0x002E, "MidNumLet", 0)]
    [InlineData(0x003A, "MidLetter", 0)]
    [InlineData(0x003B, "MidNum", 0)]
    [InlineData(0x005F, "ExtendNumLet", 0)]
    [InlineData(0x000B, "Newline", 0)]
    [InlineData(0x0085, "Newline", 0)]
    [InlineData(0x00A9, "Other", WordBreaker.ExtendedPictographic)]
    [InlineData(0x00AD, "Format", 0)]
    [InlineData(0x00B7, "MidLetter", 0)]
    [InlineData(0x05D0, "HebrewLetter", 0)]
    [InlineData(0x0E01, "ALetter", 0)]
    [InlineData(0x0E31, "Extend", 0)]
    [InlineData(0x200D, "ZWJ", 0)]
    [InlineData(0x2019, "MidNumLet", 0)]
    [InlineData(0x203F, "ExtendNumLet", 0)]
    [InlineData(0x24C2, "ALetter", WordBreaker.ExtendedPictographic)]
    [InlineData(0x3000, "WSegSpace", 0)]
    [InlineData(0x3005, "Other", WordBreaker.KanaKanji)]
    [InlineData(0x3031, "Katakana", WordBreaker.KanaKanji)]
    [InlineData(0x306E, "Other", WordBreaker.KanaKanji | WordBreaker.IdeographicOrHiragana)]
    [InlineData(0x30C6, "Katakana", WordBreaker.KanaKanji)]
    [InlineData(0x65E5, "Other", WordBreaker.KanaKanji | WordBreaker.IdeographicOrHiragana)]
    [InlineData(0xAC00, "HangulSyllable", 0)]
    [InlineData(0xD7A3, "HangulSyllable", 0)]
    [InlineData(0x10940, "ALetter", 0)]
    [InlineData(0x16FE4, "Extend", WordBreaker.IdeographicOrHiragana)]
    [InlineData(0x16FF0, "Other", WordBreaker.KanaKanji)]
    [InlineData(0x18B00, "Other", WordBreaker.IdeographicOrHiragana)]
    [InlineData(0x1E4D0, "ALetter", 0)]
    [InlineData(0x1F1E6, "RegionalIndicator", 0)]
    [InlineData(0x1F3FD, "Extend", 0)]
    [InlineData(0x1F600, "Other", WordBreaker.ExtendedPictographic)]
    [InlineData(0x323B0, "Other", WordBreaker.KanaKanji | WordBreaker.IdeographicOrHiragana)]
    [InlineData(0xE0020, "Extend", 0)]
    [InlineData(0x10FFFF, "Other", 0)]
    public void PropertiesComeFromTheUnicodeTable(int codePoint, string expectedClass, int expectedFlags)
    {
        var properties = WordBreaker.PropertiesOf(codePoint);
        ((WordBreaker.WordClass) (properties & 0x1F)).ToString().Should().Be(expectedClass);
        (properties & ~0x1F).Should().Be(expectedFlags);
    }

    private static string SegmentsOf(string input)
    {
        var engine = new Engine();
        engine.SetValue("input", input);
        return engine.Evaluate(Segment).AsString();
    }
}

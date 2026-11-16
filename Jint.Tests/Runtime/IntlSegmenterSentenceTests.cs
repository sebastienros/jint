#nullable enable

using Jint.Native.Intl;

namespace Jint.Tests.Runtime;

/// <summary>
/// <c>Intl.Segmenter</c> with <c>granularity: "sentence"</c> follows the sentence boundary rules of Unicode UAX #29
/// (https://www.unicode.org/reports/tr29/#Sentence_Boundary_Rules), SB3 to SB11, the way ICU's root rules do.
/// Every expected value in <see cref="SentencesFollowUax29"/> was produced by Node 26 (ICU) with
/// <c>[...new Intl.Segmenter('en', { granularity: 'sentence' }).segment(s)].map(x => x.segment)</c>; the
/// segments of a row are joined with <c>|</c>.
/// </summary>
public class IntlSegmenterSentenceTests
{
    [Theory]
    // SB8 (a full stop before a lowercase letter, however far, does not end the sentence)
    [InlineData("ab cd. ef", "ab cd. ef")]
    [InlineData("Mr. smith went.", "Mr. smith went.")]
    [InlineData("Mr. Smith went.", "Mr. |Smith went.")]
    [InlineData("e.g. this", "e.g. this")]
    [InlineData("e.g. This", "e.g. |This")]
    [InlineData("etc. and", "etc. and")]
    [InlineData("a. b", "a. b")]
    [InlineData("A. b", "A. b")]
    [InlineData("a. 1 b", "a. 1 b")]
    [InlineData("Hi. 5 apples", "Hi. 5 apples")]
    [InlineData("He said. 5 apples", "He said. 5 apples")]
    [InlineData("a. , b", "a. , b")]
    [InlineData("Hi. )x", "Hi. )x")]
    [InlineData("x.  y", "x.  y")]
    [InlineData("a. (b) c", "a. (b) c")]
    [InlineData(". a", ". a")]
    [InlineData("a.b", "a.b")]
    [InlineData("a.b. C", "a.b. |C")]
    [InlineData("Hi.\u00a0there", "Hi.\u00a0there")]
    [InlineData("Hi. \u00adthere", "Hi. \u00adthere")]
    [InlineData("a.\u0301 b", "a.\u0301 b")]
    [InlineData("Hi. \u0301b", "Hi. \u0301b")]
    [InlineData("one... two", "one... two")]
    [InlineData("one... Two", "one... |Two")]

    // SB9 and SB10 (closing punctuation and spaces after a terminator stay with the sentence)
    [InlineData("He said \"Hi.\" Then left.", "He said \"Hi.\" |Then left.")]
    [InlineData("He said \"Hi.\" then left.", "He said \"Hi.\" then left.")]
    [InlineData("Hello (world.) Next", "Hello (world.) |Next")]
    [InlineData("Hello (world.) next", "Hello (world.) next")]
    [InlineData("Hi.) x", "Hi.) x")]
    [InlineData("Hi.\u201d There", "Hi.\u201d |There")]
    [InlineData("\u201cHi.\u201d there", "\u201cHi.\u201d there")]
    [InlineData("Hi. \u201cThere\u201d", "Hi. |\u201cThere\u201d")]
    [InlineData("Hi!\u201d Then", "Hi!\u201d |Then")]
    [InlineData("Hi!) then", "Hi!) |then")]

    // SB8a and SB11 (a question mark or an exclamation mark ends the sentence)
    [InlineData("Hi! there", "Hi! |there")]
    [InlineData("Hi! There", "Hi! |There")]
    [InlineData("What? no", "What? |no")]
    [InlineData("What? No.", "What? |No.")]
    [InlineData("Hi?! there", "Hi?! |there")]
    [InlineData("Hi.? There", "Hi.? |There")]
    [InlineData("a! , b", "a! , b")]
    [InlineData("a? ; b", "a? ; b")]
    [InlineData("! a", "! |a")]
    [InlineData("a!b", "a!|b")]
    [InlineData("a?b. C", "a?|b. |C")]

    // SB6 (a full stop between a letter and a digit)
    [InlineData("3.14 is pi.", "3.14 is pi.")]
    [InlineData("3. 14 is pi.", "3. 14 is pi.")]
    [InlineData("1.5. x", "1.5. x")]
    [InlineData("a.5", "a.5")]

    // SB7 (an uppercase letter after a full stop that follows a letter)
    [InlineData("U.S. Army", "U.S. |Army")]
    [InlineData("U.S. army", "U.S. army")]
    [InlineData("A.B. c", "A.B. c")]
    [InlineData("U.S.A. is", "U.S.A. is")]
    [InlineData("a . B", "a . |B")]
    [InlineData("a.B", "a.B")]
    [InlineData("a. B", "a. |B")]
    [InlineData("1. B", "1. |B")]

    // SB3 and SB4 (paragraph separators always end a sentence)
    [InlineData("Hello.\nWorld", "Hello.\n|World")]
    [InlineData("Hello.\r\nWorld", "Hello.\r\n|World")]
    [InlineData("Hello\nWorld. x", "Hello\n|World. x")]
    [InlineData("Hi.  \n\nNext", "Hi.  \n|\n|Next")]
    [InlineData("Hi.\u2029Next", "Hi.\u2029|Next")]
    [InlineData("Hi\u2028Next", "Hi\u2028|Next")]
    [InlineData("Hi\u0085Next", "Hi\u0085|Next")]
    [InlineData("Hello\rWorld", "Hello\r|World")]
    [InlineData("a\r\n\r\nb", "a\r\n|\r\n|b")]
    [InlineData("\n", "\n")]
    [InlineData("\r\n", "\r\n")]
    [InlineData("x\n", "x\n")]

    // Other scripts and terminators
    [InlineData("\u65e5\u672c\u8a9e\u3002\u6b21\u306e\u6587\u3002", "\u65e5\u672c\u8a9e\u3002|\u6b21\u306e\u6587\u3002")]
    [InlineData("\u65e5\u672c\u8a9e\u3002\u3000\u6b21", "\u65e5\u672c\u8a9e\u3002\u3000|\u6b21")]
    [InlineData("\u0967. \u0916", "\u0967. |\u0916")]
    [InlineData("Hi.\u0e01", "Hi.|\u0e01")]
    [InlineData("Hi.\uff0e There", "Hi.\uff0e |There")]
    [InlineData("a\u2024 b", "a\u2024 b")]
    [InlineData("Hi\u0589 there", "Hi\u0589 |there")]
    [InlineData("\ud835\udc00. \ud835\udc1b", "\ud835\udc00. \ud835\udc1b")]
    [InlineData("\ud835\udc00\ud835\udc00. \ud835\udc00", "\ud835\udc00\ud835\udc00. |\ud835\udc00")]
    [InlineData("abc. ", "abc. ")]
    [InlineData("abc", "abc")]
    [InlineData(".", ".")]
    [InlineData("Hi.\u200b There", "Hi.\u200b |There")]
    [InlineData("Hi.\tThere", "Hi.\t|There")]
    public void SentencesFollowUax29(string input, string expected)
    {
        var engine = new Engine();
        engine.SetValue("input", input);

        var actual = engine.Evaluate("[...new Intl.Segmenter('en', { granularity: 'sentence' }).segment(input)].map(x => x.segment).join('|')").AsString();

        actual.Should().Be(expected);
    }

    /// <summary>
    /// The official conformance test of UAX #29 for Unicode 17.0.0 (SentenceBreakTest.txt, vendored under
    /// tools/text-segmentation/), every line of it. Node 26 passes all of them.
    /// </summary>
    [Fact]
    public void TheUnicodeSentenceBreakTestPasses()
    {
        var (cases, failures) = UnicodeBreakTestFile.Run("unicode-SentenceBreakTest.txt", "# SentenceBreakTest-17.0.0.txt", "sentence");

        cases.Should().Be(512);
        failures.Should().BeEmpty("{0} of {1} lines fail, the first: {2}", failures.Count, cases, string.Join(Environment.NewLine, failures.Take(40)));
    }

    /// <summary>
    /// The segment data objects carry the code unit index each sentence starts at, and <c>containing</c> finds the
    /// sentence a code unit belongs to, also across a surrogate pair.
    /// </summary>
    [Fact]
    public void IndicesAndContainingFollowTheSentences()
    {
        var engine = new Engine();
        engine.SetValue("input", "Mr. smith went. Then 𝐀. Done.");

        engine.Execute("const segs = new Intl.Segmenter('en', { granularity: 'sentence' }).segment(input);");

        // "Mr. smith went. " is one sentence (SB8), "Then <U+1D400>. " the second, "Done." the third.
        engine.Evaluate("[...segs].map(x => x.index).join()").AsString().Should().Be("0,16,25");
        engine.Evaluate("segs.containing(0).segment").AsString().Should().Be("Mr. smith went. ");
        engine.Evaluate("segs.containing(15).index").AsNumber().Should().Be(0);
        engine.Evaluate("segs.containing(22).segment").AsString().Should().Be("Then 𝐀. ");
        engine.Evaluate("segs.containing(25).segment").AsString().Should().Be("Done.");
        engine.Evaluate("'isWordLike' in segs.containing(0)").AsBoolean().Should().BeFalse();
    }

    /// <summary>
    /// A long run of spaces after a full stop must not make each space re-run the look-ahead of SB8: one
    /// <c>segment()</c> call stays linear. The look-ahead ends at the first letter, so the outcome is the one
    /// Node gives: one sentence when a lowercase letter follows (<c>)b</c>), two when an uppercase one does (<c>)B</c>).
    /// </summary>
    [Theory]
    [InlineData(")b", "100004")]
    [InlineData(")B", "100002,2")]
    public void LongRunsOfSpacesAreSegmentedWithoutRescanning(string tail, string expectedLengths)
    {
        var engine = new Engine();
        engine.SetValue("input", "a." + new string(' ', 100000) + tail);

        var actual = engine.Evaluate("[...new Intl.Segmenter('en', { granularity: 'sentence' }).segment(input)].map(x => x.segment.length).join()").AsString();

        actual.Should().Be(expectedLengths);
    }

    /// <summary>
    /// The class of every ASCII character comes from a switch and the rest from the table of the Unicode Character
    /// Database; the two must be one property.
    /// </summary>
    [Fact]
    public void TheAsciiFastPathAgreesWithTheTable()
    {
        for (var c = 0; c < 0x80; c++)
        {
            SentenceBreaker.ClassOfAscii((char) c).Should().Be(SentenceBreaker.ClassOfNonAscii(c), "U+{0:X4} is ASCII", c);
        }
    }

    /// <summary>
    /// Sentence_Break values of Unicode 17.0.0 (SentenceBreakProperty.txt), on every target framework: the table does not
    /// depend on the general categories the runtime knows, so a character added after .NET Framework's data, such as a
    /// CJK extension ideograph, is a letter there as well.
    /// </summary>
    [Theory]
    [InlineData(0x0085, "ParaSep")]
    [InlineData(0x00A0, "Sp")]
    [InlineData(0x00AD, "Extend")]
    [InlineData(0x0589, "STerm")]
    [InlineData(0x0663, "Numeric")]
    [InlineData(0x10D0, "OLetter")]
    [InlineData(0x2014, "SContinue")]
    [InlineData(0x2024, "ATerm")]
    [InlineData(0x2029, "ParaSep")]
    [InlineData(0x201C, "Close")]
    [InlineData(0x24D0, "Lower")]
    [InlineData(0x1D400, "Upper")]
    [InlineData(0xE0020, "Extend")]
    [InlineData(0x20000, "OLetter")]
    [InlineData(0x323B0, "OLetter")]
    [InlineData(0x20AC, "Other")]
    [InlineData(0x10FFFF, "Other")]
    public void ClassesComeFromTheUnicodeTable(int codePoint, string expected)
    {
        SentenceBreaker.ClassOf(codePoint).ToString().Should().Be(expected);
    }
}

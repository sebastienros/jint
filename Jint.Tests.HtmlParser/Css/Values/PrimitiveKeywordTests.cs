using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Cascade 5 § 7.3, Editor's Draft 2026-09-23: six defaulting keywords.
[TestFixture]
public sealed class PrimitiveKeywordTests
{
    [TestCase("initial", (int) CssWideKeyword.Initial)]
    [TestCase("inherit", (int) CssWideKeyword.Inherit)]
    [TestCase("unset", (int) CssWideKeyword.Unset)]
    [TestCase("revert", (int) CssWideKeyword.Revert)]
    [TestCase("revert-layer", (int) CssWideKeyword.RevertLayer)]
    [TestCase("revert-rule", (int) CssWideKeyword.RevertRule)]
    public void RecognizesEveryWideKeyword(string source, int expected)
    {
        var result = CssPrimitiveParser.ParseWideKeyword(MarkupParser.ParseCssComponentValues($"  {source}  "), new CssValueWork(default));
        result.IsMatch.Should().BeTrue();
        result.Value.Should().Be((CssWideKeyword) expected);
        result.Value.CanonicalSpelling().Should().Be(source);
        result.Span.Start.Should().Be(2);
        result.Span.Length.Should().Be(source.Length);
    }

    [Test]
    public void EscapesAndAsciiCaseAreDecodedBeforeRecognition()
    {
        var result = CssPrimitiveParser.ParseWideKeyword(MarkupParser.ParseCssComponentValues("\\49 NiTiAl"), new CssValueWork(default));
        result.IsMatch.Should().BeTrue();
        result.Value.Should().Be(CssWideKeyword.Initial);
        result.Value.CanonicalSpelling().Should().Be("initial");
    }

    [TestCase("initial extra", 8)]
    [TestCase("calc(1)", 0)]
    [TestCase("initial()", 0)]
    [TestCase("unknown", 0)]
    public void NonMatchingWholeListsReportFirstOffendingComponent(string source, int offset)
    {
        var result = CssPrimitiveParser.ParseWideKeyword(MarkupParser.ParseCssComponentValues(source), new CssValueWork(default));
        result.IsMatch.Should().BeFalse();
        result.Span.Start.Should().Be(offset);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Test]
    public void EmptyInputAndDefaultResultHaveNoPayloadOrProvenance()
    {
        var empty = CssPrimitiveParser.ParseWideKeyword(MarkupParser.ParseCssComponentValues("  "), new CssValueWork(default));
        empty.IsMatch.Should().BeFalse();
        empty.Span.Length.Should().Be(0);
        default(CssPrimitiveResult<CssWideKeyword>).IsMatch.Should().BeFalse();
        Assert.Throws<InvalidOperationException>(() => _ = default(CssPrimitiveResult<CssWideKeyword>).Value);
        Assert.Throws<InvalidOperationException>(() => _ = CssWideKeyword.None.CanonicalSpelling());
    }

    [Test]
    public void InvalidFirstKeywordWinsOverFollowingValidKeyword()
    {
        var result = CssPrimitiveParser.ParseWideKeyword(MarkupParser.ParseCssComponentValues("bogus initial"), new CssValueWork(default));
        result.IsMatch.Should().BeFalse();
        result.Span.Start.Should().Be(0);
    }
}

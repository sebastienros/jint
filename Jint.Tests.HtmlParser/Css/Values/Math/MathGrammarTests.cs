using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathGrammarTests
{
    [TestCase("calc(1 + 2)", "calc(3)")]
    [TestCase("calc(1 + 2 * 3)", "calc(7)")]
    [TestCase("calc((1 + 2) * 3)", "calc(9)")]
    [TestCase("min(3, 2, 1)", "calc(1)")]
    [TestCase("max(3, 2, 1)", "calc(3)")]
    [TestCase("clamp(1, 5, 3)", "calc(3)")]
    [TestCase("clamp(none, 5, 3)", "calc(3)")]
    [TestCase("clamp(1, 5, none)", "calc(5)")]
    [TestCase("c\\61lc(2)", "calc(2)")]
    [TestCase("calc(PI)", "calc(3.141593)")]
    [TestCase("calc(e)", "calc(2.718282)")]
    public void BasicFunctionsAndConstants(string source, string expected)
    {
        var result = MathTest.Parse(source, MathTest.Number);
        result.Status.Should().Be(CssMathParseStatus.Match, source);
        CssMathSerializer.SerializeSpecified(result.Value, new CssValueWork(default)).Should().Be(expected);
    }

    [TestCase("calc(1+2)")]
    [TestCase("calc(1+ 2)")]
    [TestCase("calc(1 +2)")]
    [TestCase("calc(1/**/+ 2)")]
    [TestCase("calc(- 2)")]
    [TestCase("calc(1, 2)")]
    [TestCase("calc(1 +)")]
    [TestCase("min(1,)")]
    [TestCase("clamp(none, none, 2)")]
    [TestCase("calc([1])")]
    [TestCase("calc(foo)")]
    [TestCase("calc(1px 2px)")]
    public void RejectsInvalidBasicGrammar(string source) =>
        MathTest.Parse(source, MathTest.Number).Status.Should().Be(CssMathParseStatus.NoMatch, source);

    [Test]
    public void ParserAcceptsRecoveredClosingParenthesis()
    {
        var result = MathTest.Parse("calc((1 + 2)", MathTest.Number);
        result.Status.Should().Be(CssMathParseStatus.Match);
    }

    [Test]
    public void ErrorSpansStayAtTheResponsibleOperatorOrFrameEnd()
    {
        MathTest.Parse("calc(1px + 1s)", MathTest.Length).Span.Start.Should().Be(9);
        MathTest.Parse("calc(1 + )", MathTest.Number).Span.Start.Should().Be(9);
        MathTest.Parse("calc(1 + ", MathTest.Number).Span.Start.Should().Be(9);
    }

    [TestCase("1")]
    [TestCase("(1)")]
    [TestCase("var(--x)")]
    [TestCase("ſin(1)")]
    public void EntryIsOnlyNamedBasicMathFunction(string source) =>
        MathTest.Parse(source, MathTest.Number).Status.Should().Be(CssMathParseStatus.NoMatch);
}

internal static class MathTest
{
    internal static readonly CssMathContext Number = new(CssMathProduction.Number, CssMathPercentageMode.Forbidden);
    internal static readonly CssMathContext Length = new(CssMathProduction.Length, CssMathPercentageMode.Forbidden);
    internal static readonly CssMathContext Angle = new(CssMathProduction.Angle, CssMathPercentageMode.Forbidden);
    internal static readonly CssMathContext LengthPercentage = new(CssMathProduction.LengthPercentage, CssMathPercentageMode.Length);
    internal static readonly CssMathContext RawPercentage = new(CssMathProduction.Percentage, CssMathPercentageMode.Raw);

    internal static CssMathParseResult Parse(string source, CssMathContext context, CssValueWork? work = null)
    {
        var components = MarkupParser.ParseCssComponentValues(source);
        components.Count.Should().BeGreaterThan(0);
        return CssMathParser.ParseMath(components[0], context, work ?? new CssValueWork(default));
    }
}

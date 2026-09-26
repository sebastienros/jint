using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathTypeTests
{
    [TestCase("calc(1px / 1px)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1px * 1px / 1px)", CssMathProduction.Length, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1deg)", CssMathProduction.Angle, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1s)", CssMathProduction.Time, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1hz)", CssMathProduction.Frequency, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1dppx)", CssMathProduction.Resolution, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1fr)", CssMathProduction.Flex, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1%)", CssMathProduction.Percentage, CssMathPercentageMode.Raw)]
    [TestCase("calc(1px + 1%)", CssMathProduction.LengthPercentage, CssMathPercentageMode.Length)]
    public void MatchesTypedProduction(string source, int production, int percentages)
    {
        var result = MathTest.Parse(source, new CssMathContext((CssMathProduction) production,
            (CssMathPercentageMode) percentages));
        result.Status.Should().Be(CssMathParseStatus.Match, source);
    }

    [TestCase("calc(1px + 1s)")]
    [TestCase("calc(0 * (1px + 1s))")]
    [TestCase("calc(min(1px * 1px) / 1px)")]
    [TestCase("calc(1%)")]
    public void DoesNotEraseTypeErrorsOrForbiddenPercentages(string source) =>
        MathTest.Parse(source, MathTest.Number).Status.Should().Be(CssMathParseStatus.NoMatch);

    [Test]
    public void GroupedIntermediateCompositeCanCancel()
    {
        MathTest.Parse("calc((1px * 1px) / 1px)", MathTest.Length).Status.Should().Be(CssMathParseStatus.Match);
    }

    [Test]
    public void RawPercentagesCannotBeAddedToNumbers()
    {
        var context = new CssMathContext(CssMathProduction.NumberOrPercentage, CssMathPercentageMode.Raw);
        MathTest.Parse("calc(1% + 1)", context).Status.Should().Be(CssMathParseStatus.NoMatch);
        MathTest.Parse("calc(1%)", context).Status.Should().Be(CssMathParseStatus.Match);
        MathTest.Parse("calc(1)", context).Status.Should().Be(CssMathParseStatus.Match);
    }

    [Test]
    public void DirectNumberAndLengthContextsMayCarryTheirPercentageMode()
    {
        var number = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Raw);
        var length = new CssMathContext(CssMathProduction.Length, CssMathPercentageMode.Length);
        MathTest.Parse("calc(1% / 1%)", number).Status.Should().Be(CssMathParseStatus.Match);
        MathTest.Parse("calc(1px + 1%)", length).Status.Should().Be(CssMathParseStatus.Match);
    }
}

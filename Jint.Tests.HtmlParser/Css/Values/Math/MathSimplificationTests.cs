using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathSimplificationTests
{
    [TestCase("calc(1in + 2px)", "calc(98px)", CssMathProduction.Length, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1kHz)", "calc(1000hz)", CssMathProduction.Frequency, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1turn)", "calc(360deg)", CssMathProduction.Angle, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1000ms)", "calc(1s)", CssMathProduction.Time, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(96dpi)", "calc(1dppx)", CssMathProduction.Resolution, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(1em + 2px)", "calc(1em + 2px)", CssMathProduction.Length, CssMathPercentageMode.Forbidden)]
    [TestCase("calc(20px + 0%)", "calc(0% + 20px)", CssMathProduction.LengthPercentage, CssMathPercentageMode.Length)]
    [TestCase("min(3%, 2%)", "calc(2%)", CssMathProduction.Percentage, CssMathPercentageMode.Raw)]
    public void SimplifiesWithAvailableSpecifiedInformation(string source, string expected,
        int production, int percentages)
    {
        var value = MathTest.Parse(source, new CssMathContext((CssMathProduction) production,
            (CssMathPercentageMode) percentages)).Value;
        CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be(expected);
    }

    [Test]
    public void BasisDependentPercentagesBlockMagnitudeComparison()
    {
        var value = MathTest.Parse("min(10%, 20%)", MathTest.LengthPercentage).Value;
        value.GetNode(value.RootIndex).Kind.Should().Be(CssMathNodeKind.Min);
    }

    [TestCase("calc(1em + 1px + 2px)", "calc(1em + 3px)")]
    [TestCase("calc(1px - 1px + 0%)", "calc(0% + 0px)")]
    [TestCase("calc(1em * 2)", "calc(2em)")]
    [TestCase("calc((1px + 2em) * 2)", "calc(4em + 2px)")]
    [TestCase("calc(2 * (1px + 2em))", "calc(4em + 2px)")]
    [TestCase("calc(1px - (2px + 3em))", "calc(-3em - 1px)")]
    [TestCase("calc(1px / 1px)", "calc(1)")]
    [TestCase("calc(1em / 1em)", "calc(1em / 1em)")]
    [TestCase("calc(1em / 1px)", "calc(1em / 1px)")]
    [TestCase("calc(1fr / 1fr)", "calc(1fr / 1fr)")]
    [TestCase("calc((1em + 1px) * 2 + 1px)", "calc(2em + 3px)")]
    [TestCase("calc(2 * (1em + 1px) + 3 * (2em + 2px))", "calc(8em + 8px)")]
    [TestCase("clamp(none, 1em, 2px)", "min(1em, 2px)")]
    public void SimplifiesSafeMixedExpressions(string source, string expected)
    {
        var context = source.Contains('%') ? MathTest.LengthPercentage :
            source.Contains(" / 1", StringComparison.Ordinal) ? MathTest.Number : MathTest.Length;
        var value = MathTest.Parse(source, context).Value;
        CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be(expected);
    }

    [Test]
    public void RelativeUnitIsNotFoldedIntoAChangedNumericType()
    {
        const string source = "calc(1em * 1s / 1px)";
        var context = new CssMathContext(CssMathProduction.Time, CssMathPercentageMode.Forbidden);
        var value = MathTest.Parse(source, context);
        value.Status.Should().Be(CssMathParseStatus.Match);
        var serialized = CssMathSerializer.SerializeSpecified(value.Value, new CssValueWork(default));
        var reparsed = MathTest.Parse(serialized, context);
        reparsed.Status.Should().Be(CssMathParseStatus.Match);
        CssMathSerializer.SerializeSpecified(reparsed.Value, new CssValueWork(default)).Should().Be(serialized);
    }

    [Test]
    public void MinCombinesComparableUnitsWithoutResolvingRelativeOnes()
    {
        var value = MathTest.Parse("min(3px, 2px, 1em)", MathTest.Length).Value;
        CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be("min(2px, 1em)");
    }

    [Test]
    public void SignedZeroSurvivesUnresolvedCalculation()
    {
        var value = MathTest.Parse("calc(-1 * 0px + 1em)", MathTest.Length).Value;
        var serialized = CssMathSerializer.SerializeSpecified(value, new CssValueWork(default));
        serialized.Should().Contain("(-1 * 0px)");
        MathTest.Parse(serialized, MathTest.Length).Status.Should().Be(CssMathParseStatus.Match);
    }

    [Test]
    public void MinAndMaxApplyCssSignedZeroOrdering()
    {
        var minimum = MathTest.Parse("min(calc(-1 * 0), 0)", MathTest.Number).Value;
        var maximum = MathTest.Parse("max(calc(-1 * 0), 0)", MathTest.Number).Value;
        double.IsNegative(minimum.GetNode(minimum.RootIndex).Numeric.Value).Should().BeTrue();
        double.IsNegative(maximum.GetNode(maximum.RootIndex).Numeric.Value).Should().BeFalse();
        CssMathSerializer.SerializeSpecified(minimum, new CssValueWork(default)).Should().Be("calc(0)");
    }

    [TestCase("clamp(10, 5, 3)", "calc(10)")]
    [TestCase("calc(0 / 0)", "calc(NaN)")]
    [TestCase("calc(1 / 0)", "calc(infinity)")]
    [TestCase("calc(-1 / 0)", "calc(-infinity)")]
    [TestCase("calc(infinity - infinity)", "calc(NaN)")]
    public void KeepsSpecialArithmeticUntilTopLevel(string source, string expected)
    {
        var value = MathTest.Parse(source, MathTest.Number).Value;
        CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be(expected);
    }
}

using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathNumberTests
{
    [TestCase("calc(1e9999)", double.MaxValue)]
    [TestCase("calc(-1e9999)", -double.MaxValue)]
    [TestCase("calc(1e-9999)", 0d)]
    [TestCase("calc(-0)", 0d)]
    public void FiniteSourceRepresentationIsBounded(string source, double expected)
    {
        var value = MathTest.Parse(source, MathTest.Number).Value;
        value.GetNode(value.RootIndex).Numeric.Value.Should().Be(expected);
    }

    [Test]
    public void AngleOverflowUsesWholeTurnEndpoint()
    {
        var value = MathTest.Parse("calc(1e9999turn)",
            new CssMathContext(CssMathProduction.Angle, CssMathPercentageMode.Forbidden)).Value;
        value.GetNode(value.RootIndex).Numeric.Value.Should().Be(CssMathNumbers.AngleLimit);
    }

    [Test]
    public void UnitScaleIsAppliedBeforeFiniteSaturation()
    {
        var value = MathTest.Parse("calc(1e309ms)",
            new CssMathContext(CssMathProduction.Time, CssMathPercentageMode.Forbidden)).Value;
        value.GetNode(value.RootIndex).Numeric.Value.Should().Be(1e306);
    }

    [Test]
    public void ExplicitInfinityDiffersFromFiniteOverflow()
    {
        var infinity = MathTest.Parse("calc(infinity)", MathTest.Number).Value;
        var finite = MathTest.Parse("calc(1e9999)", MathTest.Number).Value;
        double.IsPositiveInfinity(infinity.GetNode(infinity.RootIndex).Numeric.Value).Should().BeTrue();
        double.IsFinite(finite.GetNode(finite.RootIndex).Numeric.Value).Should().BeTrue();
    }

    [Test]
    public void LateStickyDigitChangesSeventeenDigitRounding()
    {
        var tie = MathTest.Parse("calc(1.119290892397251450)", MathTest.Number).Value;
        var sticky = MathTest.Parse("calc(1.11929089239725145" + new string('0', 8000) + "1)",
            MathTest.Number).Value;
        var down = double.Parse("1.1192908923972514", System.Globalization.CultureInfo.InvariantCulture);
        var up = double.Parse("1.1192908923972515", System.Globalization.CultureInfo.InvariantCulture);
        tie.GetNode(tie.RootIndex).Numeric.Value.Should().Be(down);
        sticky.GetNode(sticky.RootIndex).Numeric.Value.Should().Be(up);
        up.Should().NotBe(down);
    }

    [Test]
    public void RangeRemainsMetadataAtSpecifiedParse()
    {
        var context = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
            new CssMathRange(0, 1));
        var value = MathTest.Parse("calc(10)", context).Value;
        value.Context.Range.Upper.Should().Be(1);
        value.GetNode(value.RootIndex).Numeric.Value.Should().Be(10);
    }

    [Test]
    public void UnderflowDoesNotChangeExactBareLiteralRangeDecision()
    {
        var work = new CssValueWork(default);
        var zero = CssNumber.FromValidatedToken("0", work);
        var negativeTiny = CssNumber.FromValidatedToken("-1e-9999", work);
        var literalRange = new CssNumericRange(zero, true, null, true);
        literalRange.Contains(negativeTiny, work).Should().BeFalse();

        var calculation = MathTest.Parse("calc(-1e-9999)", new CssMathContext(
            CssMathProduction.Number, CssMathPercentageMode.Forbidden, new CssMathRange(0, 1))).Value;
        calculation.Context.Range.Lower.Should().Be(0);
        var represented = calculation.GetNode(calculation.RootIndex).Numeric.Value;
        represented.Should().Be(0);
        double.IsNegative(represented).Should().BeTrue();
    }
}

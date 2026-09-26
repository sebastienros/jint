using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathSerializationTests
{
    [TestCase("calc(1 / 3)", "calc(0.333333)")]
    [TestCase("calc(1in + 2px)", "calc(98px)")]
    [TestCase("calc(10px + 1em)", "calc(1em + 10px)")]
    [TestCase("calc(10px + 1vmin + 10%)", "calc(10% + 10px + 1vmin)")]
    public void ExpectedSpecifiedOutput(string source, string expected)
    {
        var context = source.Contains('%') ? MathTest.LengthPercentage :
            source.Contains("px", StringComparison.Ordinal) || source.Contains("em", StringComparison.Ordinal) ||
            source.Contains("in", StringComparison.Ordinal) ? MathTest.Length : MathTest.Number;
        var result = MathTest.Parse(source, context);
        result.Status.Should().Be(CssMathParseStatus.Match);
        var serialized = CssMathSerializer.SerializeSpecified(result.Value, new CssValueWork(default));
        serialized.Should().Be(expected);
        var reparsed = MathTest.Parse(serialized, context);
        reparsed.Status.Should().Be(CssMathParseStatus.Match);
        CssMathSerializer.SerializeSpecified(reparsed.Value, new CssValueWork(default)).Should().Be(expected);
    }

    [Test]
    public void CultureDoesNotAffectFixedNotation()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var value = MathTest.Parse("calc(1 / 3)", MathTest.Number).Value;
            CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be("calc(0.333333)");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Test]
    public void FiniteExtremeUsesFixedNotationAndRoundTripsAtRepresentationLimit()
    {
        var value = MathTest.Parse("calc(1e9999)", MathTest.Number).Value;
        var text = CssMathSerializer.SerializeSpecified(value, new CssValueWork(default));
        text.Should().StartWith("calc(17976931348623157");
        text.Should().NotContain("E");
        MathTest.Parse(text, MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);
    }

    [Test]
    public void NonfiniteDimensionalDenominatorRemainsGrouped()
    {
        const string source = "calc(1em / (infinity * 1px))";
        var parsed = MathTest.Parse(source, MathTest.Number);
        parsed.Status.Should().Be(CssMathParseStatus.Match);
        var serialized = CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default));
        serialized.Should().Be(source);
        var reparsed = MathTest.Parse(serialized, MathTest.Number);
        reparsed.Status.Should().Be(CssMathParseStatus.Match);
        CssMathSerializer.SerializeSpecified(reparsed.Value, new CssValueWork(default)).Should().Be(serialized);
    }
}

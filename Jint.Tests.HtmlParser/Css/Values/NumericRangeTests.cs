using System.Globalization;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

// CSS Values 4 § 5.1, Editor's Draft 2026-09-23: literal range endpoints.
[TestFixture]
public sealed class NumericRangeTests
{
    [Test]
    public void ComparesDecimalSpellingExactlyIncludingSignedZero()
    {
        Compare("1", "1.0").Should().Be(0);
        Compare("1e0", "1").Should().Be(0);
        Compare("12.3400", "12.34").Should().Be(0);
        Compare("1.234", "1.235").Should().BeNegative();
        Compare("100", "99.999").Should().BePositive();
        Compare("-100", "-99.999").Should().BeNegative();
        Compare("-0", "0").Should().Be(0);
        Number("-0").IsNegativeZero.Should().BeTrue();
        Number("-0.000e999").IsNegativeZero.Should().BeTrue();
        Number("+0").IsNegativeZero.Should().BeFalse();
        Number("-0.1").Sign.Should().Be(-1);
        Number("0").Sign.Should().Be(0);
        Number("+0.1").Sign.Should().Be(1);
    }

    [Test]
    public void HugeAndTinyExponentsRemainOrderedWithoutOverflowOrUnderflow()
    {
        Compare("1e999999999999999999", "9e999999999999999998").Should().BePositive();
        Compare("1e-999999999999999999", "9e-999999999999999998").Should().BeNegative();
        Compare("1e" + new string('9', 5000), "1e" + new string('9', 4999) + "8").Should().BePositive();
        Compare("1e-" + new string('9', 5000), "1e-" + new string('9', 4999) + "8").Should().BeNegative();
        Compare("1e999999999999999999", "10e999999999999999998").Should().Be(0);
        Compare("1e-999999999999999999", "0.1e-999999999999999998").Should().Be(0);
    }

    [Test]
    public void BoundsUseExactLiteralCoordinatesAndEndpointFlags()
    {
        var work = new CssValueWork(default);
        var closed = new CssNumericRange(Number("0"), true, Number("1"), true);
        closed.Contains(Number("0"), work).Should().BeTrue();
        closed.Contains(Number("1"), work).Should().BeTrue();
        closed.Contains(Number("1e-999999999999999999"), work).Should().BeTrue();
        closed.Contains(Number("-1e-999999999999999999"), work).Should().BeFalse();
        closed.Contains(Number("1e999999999999999999"), work).Should().BeFalse();
        var open = new CssNumericRange(Number("0"), false, Number("1"), false);
        open.Contains(Number("-0"), work).Should().BeFalse();
        open.Contains(Number("1"), work).Should().BeFalse();
        open.Contains(Number("0.5"), work).Should().BeTrue();
        new CssNumericRange(null, false, null, false).Contains(Number("-1e999999"), work).Should().BeTrue();
    }

    [Test]
    public void NumericComparisonIsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Compare("1.5", "1.4").Should().BePositive();
            Compare("1e100000", "9e99999").Should().BePositive();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private static CssNumber Number(string source) =>
        CssPrimitiveParser.ParseNumericAtom(MarkupParser.ParseCssComponentValues(source), new CssValueWork(default)).Value.Number;

    private static int Compare(string left, string right) => Number(left).CompareTo(Number(right), new CssValueWork(default));
}

using System.Globalization;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

public sealed class CssNumberTests
{
    [TestCase("1", "1.0", 0)]
    [TestCase("1e0", "1", 0)]
    [TestCase("12.3400", "12.34", 0)]
    [TestCase("1.234", "1.235", -1)]
    [TestCase("100", "99.999", 1)]
    [TestCase("-100", "-99.999", -1)]
    [TestCase("-0", "0", 0)]
    [TestCase("1e999999999999999999", "9e999999999999999998", 1)]
    [TestCase("1e-999999999999999999", "9e-999999999999999998", -1)]
    [TestCase("1e999999999999999999", "10e999999999999999998", 0)]
    [TestCase("1e-999999999999999999", "0.1e-999999999999999998", 0)]
    public void ComparesDecimalSpellingExactly(string left, string right, int expected)
    {
        Math.Sign(Number(left).CompareTo(Number(right), new CssValueWork(default))).Should().Be(expected);
    }

    [Test]
    public void PreservesSignedZeroAndHugeExponents()
    {
        Number("-0").IsNegativeZero.Should().BeTrue();
        Number("-0.000e999").IsNegativeZero.Should().BeTrue();
        Number("+0").IsNegativeZero.Should().BeFalse();
        Number("-0.1").Sign.Should().Be(-1);
        Number("0").Sign.Should().Be(0);
        Number("+0.1").Sign.Should().Be(1);
        Number("1e" + new string('9', 5000)).CompareTo(
            Number("1e" + new string('9', 4999) + "8"), new CssValueWork(default)).Should().BePositive();
        Number("1e-" + new string('9', 5000)).CompareTo(
            Number("1e-" + new string('9', 4999) + "8"), new CssValueWork(default)).Should().BeNegative();
    }

    [Test]
    public void NumericComparisonIsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Number("1.5").CompareTo(Number("1.4"), new CssValueWork(default)).Should().BePositive();
            Number("1e100000").CompareTo(Number("9e99999"), new CssValueWork(default)).Should().BePositive();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void ComparisonPollsAfterScanningHasFinished()
    {
        var left = Number("1e" + new string('9', 12000));
        var right = Number("1e" + new string('9', 11999) + "8");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 3) cancellation.Cancel();
        });
        Action compare = () => left.CompareTo(right, work);
        compare.Should().Throw<OperationCanceledException>();
        checks.Should().Be(3);
    }

    private static CssNumber Number(string source) =>
        CssNumber.FromValidatedToken(source, new CssValueWork(default));
}

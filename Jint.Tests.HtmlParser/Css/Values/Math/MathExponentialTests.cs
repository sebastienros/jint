using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathExponentialTests
{
    private static readonly double[] Bases =
        [double.NaN, double.NegativeInfinity, -2d, -0d, 0d, 0.5d, 1d, 2d, double.PositiveInfinity];
    private static readonly double[] Values =
        [double.NaN, double.NegativeInfinity, -2d, -0d, 0d, 0.5d, 1d, 2d, double.PositiveInfinity];

    // Authored CSS Values 4 §10.5.1 fixtures and the reviewed zero/infinite-base policy.
    [Test]
    public void LogCompleteReviewedPolicyMatrix()
    {
        var expected = new double[,]
        {
            { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN },
            { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN },
            { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN },
            { double.NaN, double.NaN, double.NaN, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NaN, double.NegativeInfinity, double.NegativeInfinity },
            { double.NaN, double.NaN, double.NaN, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NaN, double.NegativeInfinity, double.NegativeInfinity },
            { double.NaN, double.NaN, double.NaN, 0d, 0d, 1d, double.NaN, -1d, -0d },
            { double.NaN, double.NaN, double.NaN, 0d, 0d, 0d, double.NaN, 0d, 0d },
            { double.NaN, double.NaN, double.NaN, -0d, -0d, -1d, double.NaN, 1d, 0d },
            { double.NaN, double.NaN, double.NaN, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NaN, double.PositiveInfinity, double.PositiveInfinity }
        };
        var work = new CssValueWork(default);
        for (var a = 0; a < Values.Length; a++)
        for (var b = 0; b < Bases.Length; b++)
        {
            var actual = CssMathExponential.Log(Values[a], Bases[b], work);
            var wanted = expected[a, b];
            if (double.IsNaN(wanted)) double.IsNaN(actual).Should().BeTrue($"a={a}, b={b}");
            else if (wanted == 0d) Bits(actual).Should().Be(Bits(wanted), $"a={a}, b={b}");
            else actual.Should().Be(wanted, $"a={a}, b={b}");
        }
        CssMathExponential.Log(2d, null, work).Should().Be(CssMathExponential.Log(2d, System.Math.E, work));
    }

    [Test]
    public void LogSignedQuotientSurvivesNestedReciprocal()
    {
        foreach (var (source, expected) in new[]
        {
            ("calc(1 / log(0.5, infinity))", double.NegativeInfinity),
            ("calc(1 / log(2, infinity))", double.PositiveInfinity),
            ("calc(1 / log(0.5, 0))", double.PositiveInfinity),
            ("calc(1 / log(2, 0))", double.NegativeInfinity)
        })
            MathTest.Parse(source, MathTest.Number).Value.GetNode(0).Numeric.Value.Should().Be(expected);
    }

    [Test]
    public void PowSpecialValueTableAndLargeParity()
    {
        var work = new CssValueWork(default);
        var bases = new[] { double.NegativeInfinity, -0d, 0d, double.PositiveInfinity };
        var powers = new[] { -3d, -2.5d, -2d, 0d, 2d, 2.5d, 3d };
        var expected = new double[,]
        {
            { -0d, 0d, 0d, 1d, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity },
            { double.NegativeInfinity, double.PositiveInfinity, double.PositiveInfinity, 1d, 0d, 0d, -0d },
            { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, 1d, 0d, 0d, 0d },
            { 0d, 0d, 0d, 1d, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity }
        };
        for (var a = 0; a < bases.Length; a++)
        for (var b = 0; b < powers.Length; b++)
        {
            var actual = CssMathExponential.Pow(bases[a], powers[b], work);
            var wanted = expected[a, b];
            if (wanted == 0d) Bits(actual).Should().Be(Bits(wanted), $"a={a}, b={b}");
            else actual.Should().Be(wanted, $"a={a}, b={b}");
        }
        foreach (var exponent in new[] { double.NegativeInfinity, double.PositiveInfinity })
        {
            double.IsNaN(CssMathExponential.Pow(-1d, exponent, work)).Should().BeTrue();
            double.IsNaN(CssMathExponential.Pow(1d, exponent, work)).Should().BeTrue();
        }
        var finiteBases = new[] { -2d, -1d, -0.5d, 0.5d, 1d, 2d };
        var infiniteExponentResults = new double[,]
        {
            { 0d, double.PositiveInfinity },
            { double.NaN, double.NaN },
            { double.PositiveInfinity, 0d },
            { double.PositiveInfinity, 0d },
            { double.NaN, double.NaN },
            { 0d, double.PositiveInfinity }
        };
        for (var i = 0; i < finiteBases.Length; i++)
        for (var j = 0; j < 2; j++)
        {
            var actual = CssMathExponential.Pow(finiteBases[i], j == 0 ? double.NegativeInfinity :
                double.PositiveInfinity, work);
            var wanted = infiniteExponentResults[i, j];
            if (double.IsNaN(wanted)) double.IsNaN(actual).Should().BeTrue();
            else if (wanted == 0d) Bits(actual).Should().Be(Bits(0d));
            else actual.Should().Be(wanted);
        }
        double.IsNaN(CssMathExponential.Pow(double.NaN, 0d, work)).Should().BeTrue();
        double.IsNaN(CssMathExponential.Pow(double.NaN, -0d, work)).Should().BeTrue();
        double.IsNaN(CssMathExponential.Pow(-2d, 0.5d, work)).Should().BeTrue();
        Bits(CssMathExponential.Pow(-0d, 9007199254740991d, work)).Should().Be(Bits(-0d));
        Bits(CssMathExponential.Pow(-0d, 9007199254740992d, work)).Should().Be(Bits(0d));
        CssMathExponential.Pow(-2d, 3d, work).Should().Be(-8d);
    }

    [Test]
    public void SqrtExpAndHypotSpecialValuesAndScaling()
    {
        var work = new CssValueWork(default);
        Bits(CssMathExponential.Sqrt(-0d, work)).Should().Be(Bits(-0d));
        double.IsNaN(CssMathExponential.Sqrt(double.NegativeInfinity, work)).Should().BeTrue();
        double.IsNaN(CssMathExponential.Sqrt(double.NaN, work)).Should().BeTrue();
        CssMathExponential.Sqrt(double.PositiveInfinity, work).Should().Be(double.PositiveInfinity);
        CssMathExponential.Exp(-0d, work).Should().Be(1d);
        CssMathExponential.Exp(0d, work).Should().Be(1d);
        Bits(CssMathExponential.Exp(double.NegativeInfinity, work)).Should().Be(Bits(0d));
        CssMathExponential.Exp(double.PositiveInfinity, work).Should().Be(double.PositiveInfinity);
        double.IsNaN(CssMathExponential.Exp(double.NaN, work)).Should().BeTrue();
        Bits(CssMathExponential.Hypot([-0d, 0d], work)).Should().Be(Bits(0d));
        double.IsNaN(CssMathExponential.Hypot([double.NaN, double.PositiveInfinity], work)).Should().BeTrue();
        double.IsNaN(CssMathExponential.Hypot([double.PositiveInfinity, double.NaN], work)).Should().BeTrue();
        CssMathExponential.Hypot([3d, 4d], work).Should().Be(5d);
        CssMathExponential.Hypot([3e200, 4e200], work).Should().BeApproximately(5e200, 1e186);
        CssMathExponential.Hypot([3e-200, 4e-200], work).Should().BeApproximately(5e-200, 1e-212);
        CssMathExponential.Hypot([3d * double.Epsilon, 4d * double.Epsilon], work)
            .Should().Be(5d * double.Epsilon);
        double.IsFinite(CssMathExponential.Hypot([double.MaxValue / 2d, double.MaxValue / 2d], work))
            .Should().BeTrue();
        // Exact binary64 inputs give a length 1.7976931348623155816e308
        // (160-digit decimal evaluation), whose nearest double is one ULP
        // below double.MaxValue. Scaling by the maximum falsely overflows.
        const double nearMaximum = 1.797689170944583e308;
        const double smallLeg = 3.775155583210863e305;
        var expectedNearMaximum = System.Math.BitDecrement(double.MaxValue);
        CssMathExponential.Hypot([nearMaximum, smallLeg], work).Should().Be(expectedNearMaximum);
        MathTest.Parse("hypot(1.797689170944583e308, 3.775155583210863e305)", MathTest.Number)
            .Value.GetNode(0).Numeric.Value.Should().Be(expectedNearMaximum);
        CssMathExponential.Hypot([double.MaxValue, double.MaxValue], work)
            .Should().Be(double.PositiveInfinity);
        Assert.Throws<ArgumentException>(() => CssMathExponential.Hypot([], work));
    }

    [Test]
    public void FiniteTranscendentalSamples()
    {
        var work = new CssValueWork(default);
        CssMathExponential.Pow(9d, 0.5d, work).Should().BeApproximately(3d, 1e-12);
        CssMathExponential.Sqrt(2d, work).Should().BeApproximately(1.4142135623730951d, 1e-12);
        CssMathExponential.Log(8d, 2d, work).Should().BeApproximately(3d, 1e-12);
        CssMathExponential.Exp(1d, work).Should().BeApproximately(2.718281828459045d, 1e-12);
    }

    [TestCase("pow(2, 3)", "calc(8)")]
    [TestCase("sqrt(9)", "calc(3)")]
    [TestCase("hypot(3px, 4px)", "calc(5px)")]
    [TestCase("log(0)", "calc(-infinity)")]
    [TestCase("log(8, 2)", "calc(3)")]
    [TestCase("exp(0)", "calc(1)")]
    public void ResolvedSpecifiedSerialization(string source, string expected)
    {
        var context = source.Contains("px", StringComparison.Ordinal) ? MathTest.Length : MathTest.Number;
        var value = MathTest.Parse(source, context).Value;
        var text = CssMathSerializer.SerializeSpecified(value, new CssValueWork(default));
        text.Should().Be(expected);
        CssMathSerializer.SerializeSpecified(MathTest.Parse(text, context).Value,
            new CssValueWork(default)).Should().Be(text);
    }

    [TestCase("pow()")]
    [TestCase("pow(2)")]
    [TestCase("sqrt(1, 2)")]
    [TestCase("hypot()")]
    [TestCase("log()")]
    [TestCase("log(1, 2, 3)")]
    [TestCase("exp(1, 2)")]
    [TestCase("pow(2px, 3)")]
    [TestCase("sqrt(2px)")]
    [TestCase("log(2, 2px)")]
    [TestCase("exp(2px)")]
    [TestCase("hypot(2px, 2s)")]
    [TestCase("pow(1+ 2, 3)")]
    public void InvalidGrammarAndTypesAreNoMatch(string source) =>
        MathTest.Parse(source, MathTest.Number).Status.Should().Be(CssMathParseStatus.NoMatch);

    [Test]
    public void UnresolvedDependenciesAndExplicitDefaultBaseRoundTrip()
    {
        var lengthPercentage = MathTest.LengthPercentage;
        foreach (var (source, context, expected) in new[]
        {
            ("hypot(3em, 4em)", MathTest.Length, "hypot(3em, 4em)"),
            ("hypot(3%, 4px)", lengthPercentage, "hypot(3%, 4px)"),
            ("hypot(3%, 4%)", MathTest.RawPercentage, "hypot(3%, 4%)"),
            ("log(1em / 1em, e)", MathTest.Number, "log(1em / 1em)"),
            ("log(1em / 1em, 1 * e)", MathTest.Number, "log(1em / 1em)"),
            ("log(1em / 1em, exp(1))", MathTest.Number, "log(1em / 1em)"),
            ("log(1em / 1em, e + 0)", MathTest.Number, "log(1em / 1em)")
        })
        {
            var parsed = MathTest.Parse(source, context);
            parsed.Status.Should().Be(CssMathParseStatus.Match, source);
            var text = CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default));
            text.Should().Be(expected);
            var again = MathTest.Parse(text, context);
            again.Status.Should().Be(CssMathParseStatus.Match);
            again.Value.Type.Should().Be(parsed.Value.Type);
            CssMathSerializer.SerializeSpecified(again.Value, new CssValueWork(default)).Should().Be(text);
        }
        var rawNumber = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Raw);
        foreach (var source in new[]
        {
            "pow(10% / 5%, 2)", "sqrt(10% / 5%)", "log(10% / 5%, 2)", "exp(10% / 5%)"
        })
        {
            var value = MathTest.Parse(source, rawNumber).Value;
            value.Type.IsScalar.Should().BeTrue();
            value.Type.Hint.Should().Be(CssPercentHint.Percent);
            value.GetNode(0).Kind.Should().NotBe(CssMathNodeKind.Numeric);
        }
    }

    [Test]
    public void EscapedNamesRecoveredEofAndLongExponentUseExistingPipeline()
    {
        MathTest.Parse("\\70ow(2, 3)", MathTest.Number).Value.GetNode(0).Numeric.Value.Should().Be(8d);
        MathTest.Parse("sqrt(9", MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);
        var literal = "1." + new string('0', 8192) + "1e+999999999";
        MathTest.Parse($"sqrt({literal})", MathTest.Number).Value.GetNode(0).Numeric.Value
            .Should().BeApproximately(System.Math.Sqrt(double.MaxValue), 1e139);
    }

    [Test]
    public void HypotWideArgumentsChargeLinearWorkAndCancel()
    {
        static int Checks(int count)
        {
            var source = "hypot(" + string.Join(", ", Enumerable.Repeat("1em", count)) + ")";
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var parsed = MathTest.Parse(source, MathTest.Length, work);
            parsed.Status.Should().Be(CssMathParseStatus.Match);
            CssMathSerializer.SerializeSpecified(parsed.Value, work).Length.Should().BeGreaterThan(count);
            return checks;
        }
        Checks(512).Should().BeLessThan(Checks(256) * 3);
        var wide = new double[8192];
        Array.Fill(wide, 1d);
        using var cancellation = new CancellationTokenSource();
        var reached = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++reached == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathExponential.Hypot(wide, work));
        reached.Should().Be(3);
    }

    [Test]
    public void ExponentialKernelChecksCancellationAtEntryAndExit()
    {
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 2) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathExponential.Log(2d, 2d, work));
        checks.Should().Be(2);
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
}

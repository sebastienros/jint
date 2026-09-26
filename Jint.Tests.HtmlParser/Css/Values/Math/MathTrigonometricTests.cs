using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathTrigonometricTests
{
    // WPT 2136eb1501a106c42cd8977bb31c81b57b785bc8,
    // css/css-values/acos-asin-atan-atan2-serialize.html (BSD-3-Clause).
    [TestCase("acos(1)", "calc(0deg)")]
    [TestCase("acos(-1)", "calc(180deg)")]
    [TestCase("acos(2)", "calc(NaN * 1deg)")]
    [TestCase("acos(0.5)", "calc(60deg)")]
    [TestCase("asin(1)", "calc(90deg)")]
    [TestCase("asin(-1)", "calc(-90deg)")]
    [TestCase("asin(2)", "calc(NaN * 1deg)")]
    [TestCase("asin(0.5)", "calc(30deg)")]
    [TestCase("atan(1)", "calc(45deg)")]
    [TestCase("atan(infinity)", "calc(90deg)")]
    [TestCase("atan2(1s, 1000ms)", "calc(45deg)")]
    [TestCase("atan2(infinity, infinity)", "calc(45deg)")]
    [TestCase("atan2(-infinity, -infinity)", "calc(-135deg)")]
    [TestCase("atan2(infinity, 10)", "calc(90deg)")]
    [TestCase("atan2(10, infinity)", "calc(0deg)")]
    [TestCase("atan2(NaN, 10)", "calc(NaN * 1deg)")]
    public void PinnedWptSpecifiedSerialization(string source, string expected)
    {
        var parsed = MathTest.Parse(source, MathTest.Angle);
        parsed.Status.Should().Be(CssMathParseStatus.Match, source);
        var text = CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default));
        text.Should().Be(expected);
        CssMathSerializer.SerializeSpecified(MathTest.Parse(text, MathTest.Angle).Value,
            new CssValueWork(default)).Should().Be(text);
    }

    // WPT computed.html at the same pin; these are resolved numeric cases, not
    // claims about property computed style in the native specified-value parser.
    [TestCase("atan2(0,-1)", 180d)]
    [TestCase("atan2(1,-1)", 135d)]
    [TestCase("atan2(-1,1)", -45d)]
    [TestCase("atan2(1px,-1px)", 135d)]
    [TestCase("atan2(1deg,-1deg)", 135d)]
    [TestCase("asin(sin(0.25turn))", 90d)]
    public void PinnedWptResolvedSamples(string source, double expected)
    {
        var parsed = MathTest.Parse(source, MathTest.Angle);
        parsed.Status.Should().Be(CssMathParseStatus.Match, source);
        parsed.Value.GetNode(0).Numeric.Value.Should().BeApproximately(expected, 1e-12);
    }

    [TestCase("sin(90deg)", "calc(1)")]
    [TestCase("sin(-90deg)", "calc(-1)")]
    [TestCase("cos(180deg)", "calc(-1)")]
    [TestCase("cos(90deg)", "calc(0)")]
    [TestCase("tan(90deg)", "calc(infinity)")]
    [TestCase("tan(270deg)", "calc(-infinity)")]
    [TestCase("tan(450deg)", "calc(infinity)")]
    [TestCase("sin(450deg)", "calc(1)")]
    [TestCase("sin(infinity)", "calc(NaN)")]
    [TestCase("cos(-infinity)", "calc(NaN)")]
    [TestCase("tan(NaN)", "calc(NaN)")]
    [TestCase("atan(-infinity)", "calc(-90deg)")]
    public void AuthoredCardinalsAndDomains(string source, string expected)
    {
        var context = source.StartsWith("atan", StringComparison.Ordinal) ? MathTest.Angle : MathTest.Number;
        var parsed = MathTest.Parse(source, context);
        parsed.Status.Should().Be(CssMathParseStatus.Match, source);
        CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default)).Should().Be(expected);
    }

    [Test]
    public void DegreeCardinalsDoNotBecomeRadianEpsilonTests()
    {
        var work = new CssValueWork(default);
        CssMathTrigonometric.Evaluate(CssMathFunction.Tan, 90d, true, work)
            .Should().Be(double.PositiveInfinity);
        double.IsFinite(CssMathTrigonometric.Evaluate(CssMathFunction.Tan, System.Math.PI / 2d,
            false, work)).Should().BeTrue();
        CssMathTrigonometric.Evaluate(CssMathFunction.Sin, 45d, true, work)
            .Should().BeApproximately(System.Math.Sqrt(0.5d), 1e-12);
        CssMathTrigonometric.Evaluate(CssMathFunction.Sin, System.Math.PI / 4d, false, work)
            .Should().BeApproximately(System.Math.Sqrt(0.5d), 1e-12);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CssMathTrigonometric.Evaluate(CssMathFunction.Pow, 1d, false, work));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CssMathTrigonometric.Evaluate(CssMathFunction.Asin, 1d, true, work));
    }

    [TestCase("tan(4.75turn)", "calc(-infinity)")]
    [TestCase("tan(-4.75turn)", "calc(infinity)")]
    [TestCase("tan(1300grad)", "calc(infinity)")]
    [TestCase("tan(-1300grad)", "calc(-infinity)")]
    [TestCase("tan(1500grad)", "calc(-infinity)")]
    [TestCase("tan(1700grad)", "calc(infinity)")]
    [TestCase("cos(23.25turn)", "calc(0)")]
    [TestCase("cos(-23.25turn)", "calc(0)")]
    public void ExactMultiRevolutionQuarterTurnsRemainCardinal(string source, string expected)
    {
        var parsed = MathTest.Parse(source, MathTest.Number);
        parsed.Status.Should().Be(CssMathParseStatus.Match);
        CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default)).Should().Be(expected);
    }

    [Test]
    public void NearbyTurnIsNotSnappedToACardinal()
    {
        var value = MathTest.Parse("tan(4.750000000000001turn)", MathTest.Number).Value;
        double.IsFinite(value.GetNode(0).Numeric.Value).Should().BeTrue();
    }

    private static readonly double[] Axis =
        [double.NegativeInfinity, -1d, -0d, 0d, 1d, double.PositiveInfinity];
    private static readonly double[,] Expected =
    {
        { -135d, -90d, -90d, -90d, -90d, -45d },
        { -180d, -135d, -90d, -90d, -45d, -0d },
        { -180d, -180d, -180d, -0d, -0d, -0d },
        { 180d, 180d, 180d, 0d, 0d, 0d },
        { 180d, 135d, 90d, 90d, 45d, 0d },
        { 135d, 90d, 90d, 90d, 90d, 45d }
    };

    [Test]
    public void Atan2CompleteSpecSignMatrixIncludingNegative180Boundary()
    {
        var work = new CssValueWork(default);
        for (var y = 0; y < Axis.Length; y++)
        for (var x = 0; x < Axis.Length; x++)
        {
            var actual = CssMathTrigonometric.Atan2(Axis[y], Axis[x], work);
            var expected = Expected[y, x];
            if (expected == 0d) Bits(actual).Should().Be(Bits(expected), $"y={y}, x={x}");
            else actual.Should().Be(expected, $"y={y}, x={x}");
        }
        double.IsNaN(CssMathTrigonometric.Atan2(double.NaN, 1d, work)).Should().BeTrue();
        double.IsNaN(CssMathTrigonometric.Atan2(1d, double.NaN, work)).Should().BeTrue();
    }

    [Test]
    public void ArithmeticNegativeZeroSurvivesNestedAtan2()
    {
        var source = "calc(atan2(0 / -infinity, -1) / 4)";
        var parsed = MathTest.Parse(source, MathTest.Angle);
        parsed.Status.Should().Be(CssMathParseStatus.Match);
        parsed.Value.GetNode(0).Numeric.Value.Should().Be(-45d);
        Bits(CssMathTrigonometric.Evaluate(CssMathFunction.Asin, -0d, false,
            new CssValueWork(default))).Should().Be(Bits(-0d));
        Bits(CssMathTrigonometric.Evaluate(CssMathFunction.Sin, -0d, false,
            new CssValueWork(default))).Should().Be(Bits(-0d));
        Bits(CssMathTrigonometric.Evaluate(CssMathFunction.Atan, -0d, false,
            new CssValueWork(default))).Should().Be(Bits(-0d));
    }

    [TestCase("sin()")]
    [TestCase("cos(1, 2)")]
    [TestCase("atan2(1)")]
    [TestCase("atan2(1, 2, 3)")]
    [TestCase("asin(1deg)")]
    [TestCase("sin(1px)")]
    [TestCase("atan2(1px, 1s)")]
    [TestCase("sin(1+ 2)")]
    public void InvalidGrammarAndTypesAreNoMatch(string source)
    {
        MathTest.Parse(source, MathTest.Angle).Status.Should().Be(CssMathParseStatus.NoMatch);
    }

    [Test]
    public void ResultTypesHintsAndUnresolvedBasesSurviveSerialization()
    {
        var anglePercent = new CssMathContext(CssMathProduction.AnglePercentage,
            CssMathPercentageMode.Angle);
        var value = MathTest.Parse("atan2(10%, 1deg)", anglePercent).Value;
        value.Type.Angle.Should().Be(1);
        value.Type.Hint.Should().Be(CssPercentHint.Angle);
        value.GetNode(0).Kind.Should().Be(CssMathNodeKind.Atan2);
        var text = CssMathSerializer.SerializeSpecified(value, new CssValueWork(default));
        text.Should().Be("atan2(10%, 1deg)");
        MathTest.Parse(text, anglePercent).Value.Type.Should().Be(value.Type);

        var relative = MathTest.Parse("atan2(1em, -1em)", MathTest.Angle).Value;
        relative.GetNode(0).Kind.Should().Be(CssMathNodeKind.Atan2);
        CssMathSerializer.SerializeSpecified(relative, new CssValueWork(default))
            .Should().Be("atan2(1em, -1em)");
        var forward = MathTest.Parse("atan(sin(10%))", anglePercent).Value;
        forward.Type.Angle.Should().Be(1);
        forward.Type.Hint.Should().Be(CssPercentHint.Angle);
        forward.GetNode(0).Kind.Should().Be(CssMathNodeKind.Atan);
        forward.GetNode(forward.GetChild(forward.GetNode(0).ChildStart)).Kind.Should().Be(CssMathNodeKind.Sin);
    }

    [Test]
    public void EscapedNamesRecoveredEofAndLongLiteralsUseExistingPipeline()
    {
        MathTest.Parse("\\73in(90deg)", MathTest.Number).Value.GetNode(0).Numeric.Value.Should().Be(1d);
        MathTest.Parse("atan2(1, 1", MathTest.Angle).Status.Should().Be(CssMathParseStatus.Match);
        var literal = "1." + new string('0', 8192) + "1e+999999999";
        MathTest.Parse($"atan({literal})", MathTest.Angle).Value.GetNode(0).Numeric.Value
            .Should().Be(90d);
        MathTest.Parse("sin(pow(1,))", MathTest.Number).Status
            .Should().Be(CssMathParseStatus.RequiresLaterGrammar);
    }

    [Test]
    public void WideUnresolvedCallsAndCancellationUseBoundedWork()
    {
        static int Checks(int count)
        {
            var source = "calc(" + string.Join(" + ", Enumerable.Repeat("sin(1em / 1em)", count)) + ")";
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var parsed = MathTest.Parse(source, MathTest.Number, work);
            parsed.Status.Should().Be(CssMathParseStatus.Match);
            CssMathSerializer.SerializeSpecified(parsed.Value, work).Length.Should().BeGreaterThan(count);
            return checks;
        }
        Checks(256).Should().BeLessThan(Checks(128) * 3);
        var source = "calc(" + string.Join(" + ", Enumerable.Repeat("sin(1em / 1em)", 4096)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        using var cancellation = new CancellationTokenSource();
        var reached = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++reached == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() =>
            CssMathParser.ParseMath(component, MathTest.Number, work));
        reached.Should().Be(3);
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
}

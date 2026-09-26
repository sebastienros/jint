#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathSteppedTests
{
    [TestCase("round(2.5)", "calc(3)")]
    [TestCase("round(-2.5)", "calc(-2)")]
    [TestCase("round(up, -2.5)", "calc(-2)")]
    [TestCase("round(down, -2.5)", "calc(-3)")]
    [TestCase("round(to-zero, -2.5)", "calc(-2)")]
    [TestCase("round(nearest, 2.5, 1)", "calc(3)")]
    [TestCase("round(UP, 2.5, 1)", "calc(3)")]
    [TestCase("r\\6fund(2.5)", "calc(3)")]
    [TestCase("mod(-18px, 5px)", "calc(2px)")]
    [TestCase("rem(-18px, 5px)", "calc(-3px)")]
    [TestCase("round(3in, 2.54cm)", "calc(288px)")]
    public void ResolvedFunctionsSerialize(string source, string expected)
    {
        var context = source.Contains("px", StringComparison.Ordinal) || source.Contains("in", StringComparison.Ordinal)
            ? MathTest.Length : MathTest.Number;
        var result = MathTest.Parse(source, context);
        result.Status.Should().Be(CssMathParseStatus.Match, source);
        CssMathSerializer.SerializeSpecified(result.Value, new CssValueWork(default)).Should().Be(expected);
    }

    [TestCase("round(1px)", CssMathProduction.Length, CssMathPercentageMode.Forbidden)]
    [TestCase("round(up, 1px)", CssMathProduction.Length, CssMathPercentageMode.Forbidden)]
    [TestCase("round(line-width, 1)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("mod(1)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("rem(1, 2, 3)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("round(sideways, 1)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("round(up 1, 2)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("round(1, 2px)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("mod(1px, 1s)", CssMathProduction.Length, CssMathPercentageMode.Forbidden)]
    [TestCase("round(1+ 2)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    [TestCase("round(1/**/+ 2)", CssMathProduction.Number, CssMathPercentageMode.Forbidden)]
    public void RejectsBadGrammarAndTypes(string source, int production, int mode) =>
        MathTest.Parse(source, new CssMathContext((CssMathProduction) production,
            (CssMathPercentageMode) mode)).Status.Should().Be(CssMathParseStatus.NoMatch);

    [Test]
    public void ErrorSpansIdentifyArgumentOrFrameEnd()
    {
        MathTest.Parse("mod(1px, 1s)", MathTest.Length).Span.Start.Should().Be(9);
        MathTest.Parse("round(1,)", MathTest.Number).Span.Start.Should().Be(8);
        MathTest.Parse("round(sideways, 1)", MathTest.Number).Span.Start.Should().Be(6);
        MathTest.Parse("round(1,", MathTest.Number).Span.Start.Should().Be(8);
    }

    [TestCase("round(line-width, 0.2px)", "round(line-width, 0.2px)")]
    [TestCase("round(line-width, 0.2px, 1px)", "round(line-width, 0.2px, 1px)")]
    [TestCase("round(up, 2em, 1em)", "round(up, 2em, 1em)")]
    [TestCase("round(\\75 p, 2em, 1em)", "round(up, 2em, 1em)")]
    [TestCase("round(down, 2em, 1em)", "round(down, 2em, 1em)")]
    [TestCase("round(to-zero, 2em, 1em)", "round(to-zero, 2em, 1em)")]
    [TestCase("round(nearest, 2em, 1em)", "round(2em, 1em)")]
    [TestCase("mod(10em, 3em)", "mod(10em, 3em)")]
    [TestCase("rem(10%, 3%)", "rem(10%, 3%)")]
    public void DependenciesStayFunctionalAcrossRoundTrip(string source, string expected)
    {
        var context = source.Contains('%') ? MathTest.LengthPercentage : MathTest.Length;
        var first = MathTest.Parse(source, context);
        first.Status.Should().Be(CssMathParseStatus.Match);
        var text = CssMathSerializer.SerializeSpecified(first.Value, new CssValueWork(default));
        text.Should().Be(expected);
        var second = MathTest.Parse(text, context);
        second.Status.Should().Be(CssMathParseStatus.Match);
        second.Value.Type.Should().Be(first.Value.Type);
        second.Value.GetNode(second.Value.RootIndex).Kind.Should().Be(first.Value.GetNode(first.Value.RootIndex).Kind);
        CssMathSerializer.SerializeSpecified(second.Value, new CssValueWork(default)).Should().Be(text);
    }

    [Test]
    public void PercentageBasisIsNeverInvented()
    {
        MathTest.Parse("round(10%)", MathTest.RawPercentage).Status.Should().Be(CssMathParseStatus.NoMatch);
        MathTest.Parse("round(10%, 3%)", MathTest.RawPercentage).Value.GetNode(0).Kind.Should().Be(CssMathNodeKind.Numeric);
        var hinted = MathTest.Parse("mod(10%, 3%)", MathTest.LengthPercentage).Value;
        hinted.GetNode(0).Kind.Should().Be(CssMathNodeKind.Mod);
        CssMathSerializer.SerializeSpecified(hinted, new CssValueWork(default)).Should().Be("mod(10%, 3%)");
    }

    [Test]
    public void RoundingStrategyPayloadIsGuardedAndSurvivesFreeze()
    {
        var value = MathTest.Parse("round(line-width, 1em)", MathTest.Length).Value;
        value.GetNode(value.RootIndex).RoundingStrategy.Should().Be(CssRoundingStrategy.LineWidth);
        var other = MathTest.Parse("mod(1em, 2em)", MathTest.Length).Value;
        Assert.Throws<InvalidOperationException>(() => _ = other.GetNode(other.RootIndex).RoundingStrategy);
    }

    [Test]
    public void NumericSpecialValuesAndZeroBits()
    {
        var work = new CssValueWork(default);
        CssMathStepped.Round(-2.5, 1, CssRoundingStrategy.Nearest, work).Should().Be(-2);
        CssMathStepped.Round(2.5, -1, CssRoundingStrategy.Nearest, work).Should().Be(3);
        CssMathStepped.Round(-2.5, -1, CssRoundingStrategy.Nearest, work).Should().Be(-2);
        CssMathStepped.Mod(-18, 5, work).Should().Be(2);
        CssMathStepped.Mod(18, -5, work).Should().Be(-2);
        CssMathStepped.Rem(-18, 5, work).Should().Be(-3);
        CssMathStepped.Rem(18, -5, work).Should().Be(3);
        Bits(CssMathStepped.Round(-0d, 1, CssRoundingStrategy.Up, work)).Should().Be(Bits(-0d));
        Bits(CssMathStepped.Round(-0.2, 1, CssRoundingStrategy.Up, work)).Should().Be(Bits(-0d));
        Bits(CssMathStepped.Round(0.2, 1, CssRoundingStrategy.Down, work)).Should().Be(Bits(0d));
        Bits(CssMathStepped.Mod(-0d, 5, work)).Should().Be(Bits(0d));
        Bits(CssMathStepped.Mod(0d, -5, work)).Should().Be(Bits(-0d));
        Bits(CssMathStepped.Rem(-0d, 5, work)).Should().Be(Bits(-0d));
        double.IsNaN(CssMathStepped.Mod(-0d, double.PositiveInfinity, work)).Should().BeTrue();
        double.IsNaN(CssMathStepped.Rem(double.PositiveInfinity, 2, work)).Should().BeTrue();
        double.IsNaN(CssMathStepped.Round(double.PositiveInfinity, double.PositiveInfinity,
            CssRoundingStrategy.Nearest, work)).Should().BeTrue();
        double.IsNaN(CssMathStepped.Round(1, 0, CssRoundingStrategy.Nearest, work)).Should().BeTrue();
        double.IsNaN(CssMathStepped.Round(double.NaN, 1, CssRoundingStrategy.Nearest, work)).Should().BeTrue();
        double.IsNaN(CssMathStepped.Mod(1, double.NaN, work)).Should().BeTrue();
        CssMathStepped.Rem(1, double.PositiveInfinity, work).Should().Be(1);
        CssMathStepped.Round(double.NegativeInfinity, -2, CssRoundingStrategy.Down, work).Should().Be(double.NegativeInfinity);
        CssMathStepped.Round(1, double.PositiveInfinity, CssRoundingStrategy.Up, work).Should().Be(double.PositiveInfinity);
        CssMathStepped.Round(-1, double.NegativeInfinity, CssRoundingStrategy.Down, work).Should().Be(double.NegativeInfinity);
        Bits(CssMathStepped.Round(-1, double.PositiveInfinity, CssRoundingStrategy.Nearest, work)).Should().Be(Bits(-0d));
        Bits(CssMathStepped.Round(-0d, double.PositiveInfinity, CssRoundingStrategy.Down, work)).Should().Be(Bits(-0d));
        Bits(CssMathStepped.Round(0d, double.NegativeInfinity, CssRoundingStrategy.Up, work)).Should().Be(Bits(0d));
    }

    [Test]
    public void RemainderHandlesOverflowAndExcludedEndpoint()
    {
        var work = new CssValueWork(default);
        var tiny = double.Epsilon;
        CssMathStepped.Rem(double.MaxValue, tiny, work).Should().Be(0);
        CssMathStepped.Mod(-tiny, double.MaxValue, work).Should().Be(System.Math.BitDecrement(double.MaxValue));
        CssMathStepped.Mod(tiny, -double.MaxValue, work).Should().Be(-System.Math.BitDecrement(double.MaxValue));
        CssMathStepped.Round(double.MaxValue, tiny, CssRoundingStrategy.Nearest, work).Should().Be(double.MaxValue);
    }

    [Test]
    public void ModAndRemCoverEverySpecialSignClassPair()
    {
        // Rows: A = -∞, -3, -0, +0, +3, +∞, NaN.
        // Columns: B = -∞, -2, -0, +0, +2, +∞, NaN.
        var values = new[] { double.NegativeInfinity, -3d, -0d, 0d, 3d, double.PositiveInfinity, double.NaN };
        var steps = new[] { double.NegativeInfinity, -2d, -0d, 0d, 2d, double.PositiveInfinity, double.NaN };
        string[][] mod =
        [
            ["NaN", "NaN", "NaN", "NaN", "NaN", "NaN", "NaN"],
            ["-3", "-1", "NaN", "NaN", "1", "NaN", "NaN"],
            ["-0", "-0", "NaN", "NaN", "+0", "NaN", "NaN"],
            ["NaN", "-0", "NaN", "NaN", "+0", "+0", "NaN"],
            ["NaN", "-1", "NaN", "NaN", "1", "3", "NaN"],
            ["NaN", "NaN", "NaN", "NaN", "NaN", "NaN", "NaN"],
            ["NaN", "NaN", "NaN", "NaN", "NaN", "NaN", "NaN"]
        ];
        string[][] rem =
        [
            ["NaN", "NaN", "NaN", "NaN", "NaN", "NaN", "NaN"],
            ["-3", "-1", "NaN", "NaN", "-1", "-3", "NaN"],
            ["-0", "-0", "NaN", "NaN", "-0", "-0", "NaN"],
            ["+0", "+0", "NaN", "NaN", "+0", "+0", "NaN"],
            ["3", "1", "NaN", "NaN", "1", "3", "NaN"],
            ["NaN", "NaN", "NaN", "NaN", "NaN", "NaN", "NaN"],
            ["NaN", "NaN", "NaN", "NaN", "NaN", "NaN", "NaN"]
        ];
        var work = new CssValueWork(default);
        for (var a = 0; a < values.Length; a++)
        for (var b = 0; b < steps.Length; b++)
        {
            AssertNumber(mod[a][b], CssMathStepped.Mod(values[a], steps[b], work));
            AssertNumber(rem[a][b], CssMathStepped.Rem(values[a], steps[b], work));
        }
    }

    [Test]
    public void LineWidthUsesSuppliedDevicePixelAndFinalSnap()
    {
        var work = new CssValueWork(default);
        CssMathStepped.RoundLineWidth(0.2, null, 1, work).Should().Be(1);
        CssMathStepped.RoundLineWidth(-0.2, null, 1, work).Should().Be(-1);
        CssMathStepped.RoundLineWidth(1.7, null, 0.5, work).Should().Be(1.5);
        CssMathStepped.RoundLineWidth(-1.7, null, 0.5, work).Should().Be(-1.5);
        CssMathStepped.RoundLineWidth(1.5, null, 2, work).Should().Be(2);
        CssMathStepped.RoundLineWidth(2.5, 2.5, 1, work).Should().Be(2);
        CssMathStepped.RoundLineWidth(0.2, 1, 0.5, work).Should().Be(1);
        CssMathStepped.RoundLineWidth(0.2, null, 0.5, work).Should().Be(0.5);
        Bits(CssMathStepped.RoundLineWidth(-0d, null, 1, work)).Should().Be(Bits(-0d));
        CssMathStepped.RoundLineWidth(1, double.PositiveInfinity, 0.5, work).Should().Be(double.PositiveInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => CssMathStepped.RoundLineWidth(1, null, 0, work));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssMathStepped.RoundLineWidth(1, null, double.NaN, work));
        Assert.Throws<ArgumentOutOfRangeException>(() => CssMathStepped.Round(1, 1, CssRoundingStrategy.LineWidth, work));
    }

    [Test]
    public void LongLiteralAndExponentStayBoundedInsideSteppedFunctions()
    {
        var literal = "1." + new string('0', 8192) + "1e+999999999";
        var value = MathTest.Parse($"round({literal})", MathTest.Number).Value;
        value.GetNode(value.RootIndex).Numeric.Value.Should().Be(double.MaxValue);
        var modular = MathTest.Parse($"mod({literal}, 2)", MathTest.Number).Value;
        modular.GetNode(modular.RootIndex).Numeric.Value.Should().Be(0);
    }

    [Test]
    public void DeepAlternatingFunctionsAndWideCallsUseLinearWork()
    {
        var nested = "1px";
        for (var i = 0; i < 64; i++)
            nested = i % 2 == 0 ? $"round(line-width, {nested})" : $"mod({nested}, 2px)";
        var context = new CssMathContext(CssMathProduction.Length, CssMathPercentageMode.Forbidden,
            maximumNestingDepth: 65);
        var parsed = MathTest.Parse(nested, context);
        parsed.Status.Should().Be(CssMathParseStatus.Match);
        var serialized = CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default));
        MathTest.Parse(serialized, context).Status.Should().Be(CssMathParseStatus.Match);

        static int Count(int count)
        {
            var source = "calc(" + string.Join(" + ", Enumerable.Repeat("round(line-width, 1px)", count)) + ")";
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var value = MathTest.Parse(source, MathTest.Length, work).Value;
            CssMathSerializer.SerializeSpecified(value, work).Length.Should().BeGreaterThan(count);
            return checks;
        }
        var small = Count(128);
        var large = Count(256);
        large.Should().BeLessThan(small * 3);
    }

    [Test]
    public void CancellationDuringSteppedParsingAndSerializationPublishesNothing()
    {
        var source = "round(line-width, " + string.Join(" + ", Enumerable.Repeat("1px", 4096)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Length, work));
        checks.Should().Be(3);

        var value = MathTest.Parse("round(line-width, 1em + 2px)", MathTest.Length).Value;
        using var serializeCancellation = new CancellationTokenSource();
        var serializeChecks = 0;
        var serializeWork = new CssValueWork(serializeCancellation.Token, () =>
        {
            if (++serializeChecks == 3) serializeCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathSerializer.SerializeSpecified(value, serializeWork));
        serializeChecks.Should().Be(3);
    }

    [Test]
    public void ConcurrentIndependentCallsLeaveInputAndValuesUnchanged()
    {
        const string source = "round(line-width, mod(10em, 3em), 2em)";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        var initial = CssMathParser.ParseMath(component, MathTest.Length, new CssValueWork(default)).Value;
        var expected = CssMathSerializer.SerializeSpecified(initial, new CssValueWork(default));
        var outputs = new string[16];
        Parallel.For(0, outputs.Length, i =>
        {
            var parsed = CssMathParser.ParseMath(component, MathTest.Length, new CssValueWork(default));
            outputs[i] = CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default));
        });
        outputs.Should().OnlyContain(output => output == expected);
        CssMathSerializer.SerializeSpecified(initial, new CssValueWork(default)).Should().Be(expected);
        CssMathParser.ParseMath(component, MathTest.Length, new CssValueWork(default)).Status.Should().Be(CssMathParseStatus.Match);
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

    private static void AssertNumber(string expected, double actual)
    {
        if (expected == "NaN") { double.IsNaN(actual).Should().BeTrue(); return; }
        if (expected == "+0") { Bits(actual).Should().Be(Bits(0d)); return; }
        if (expected == "-0") { Bits(actual).Should().Be(Bits(-0d)); return; }
        actual.Should().Be(double.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }
}

using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathSignTests
{
    [TestCase("abs(2px)", "calc(2px)")]
    [TestCase("abs(-2px)", "calc(2px)")]
    [TestCase("abs(-infinity)", "calc(infinity)")]
    [TestCase("abs(NaN)", "calc(NaN)")]
    [TestCase("sign(2px)", "calc(1)")]
    [TestCase("sign(-2px)", "calc(-1)")]
    [TestCase("sign(infinity)", "calc(1)")]
    [TestCase("sign(-infinity)", "calc(-1)")]
    [TestCase("sign(NaN)", "calc(NaN)")]
    [TestCase("\\61 bs(-2px)", "calc(2px)")]
    [TestCase("S\\69GN(-2px)", "calc(-1)")]
    public void ResolvedArgumentsFoldAndSerialize(string source, string expected)
    {
        var context = source.Contains("abs", StringComparison.OrdinalIgnoreCase) ||
            source.Contains("\\61 bs", StringComparison.OrdinalIgnoreCase)
            ? MathTest.Length : MathTest.Number;
        if (!source.Contains("px", StringComparison.Ordinal)) context = MathTest.Number;
        var parsed = MathTest.Parse(source, context);
        parsed.Status.Should().Be(CssMathParseStatus.Match, source);
        var text = CssMathSerializer.SerializeSpecified(parsed.Value, new CssValueWork(default));
        text.Should().Be(expected);
        CssMathSerializer.SerializeSpecified(MathTest.Parse(text, context).Value,
            new CssValueWork(default)).Should().Be(text);
    }

    [TestCase("abs()", 4)]
    [TestCase("sign()", 5)]
    [TestCase("abs(1, 2)", 5)]
    [TestCase("sign(1, 2)", 6)]
    [TestCase("abs(1+ 2)", 5)]
    public void RejectsArityAndOperatorGrammarAtSourceSpan(string source, int offset)
    {
        var result = MathTest.Parse(source, MathTest.Number);
        result.Status.Should().Be(CssMathParseStatus.NoMatch);
        result.Span.Start.Should().Be(offset);
    }

    [Test]
    public void TypesAllArgumentsBeforeFoldingAndKeepsHintsWithoutDimensions()
    {
        var rawNumber = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Raw);
        var signed = MathTest.Parse("sign(10%)", rawNumber).Value;
        signed.Type.IsScalar.Should().BeTrue();
        signed.Type.Hint.Should().Be(CssPercentHint.Percent);
        signed.GetNode(0).Kind.Should().Be(CssMathNodeKind.Sign);
        MathTest.Parse("abs(10%)", MathTest.RawPercentage).Value.Type.Percent.Should().Be(1);
        MathTest.Parse("sign(1px + 1s)", MathTest.Number).Status.Should().Be(CssMathParseStatus.NoMatch);
        MathTest.Parse("abs(0 * (1px + 1s))", MathTest.Number).Status.Should().Be(CssMathParseStatus.NoMatch);
        MathTest.Parse("sign(1px)", MathTest.Length).Status.Should().Be(CssMathParseStatus.NoMatch);
        var hinted = MathTest.Parse("calc(sign(10%) * 1px)", MathTest.LengthPercentage).Value;
        hinted.Type.Length.Should().Be(1);
        hinted.Type.Hint.Should().Be(CssPercentHint.Length);
        CssMathSerializer.SerializeSpecified(hinted, new CssValueWork(default))
            .Should().Be("calc(1px * sign(10%))");
    }

    [Test]
    public void MakeConsistentCopiesOnlyCompatibleHints()
    {
        var number = default(CssNumericType);
        number.TryMakeConsistent(new CssNumericType(length: 1, hint: CssPercentHint.Length),
            out var result).Should().BeTrue();
        result.IsScalar.Should().BeTrue();
        result.Hint.Should().Be(CssPercentHint.Length);
        new CssNumericType(angle: 1, hint: CssPercentHint.Angle)
            .TryMakeConsistent(new CssNumericType(length: 1, hint: CssPercentHint.Length), out _)
            .Should().BeFalse();
    }

    [TestCase("abs(-2em)", "calc(2em)")]
    [TestCase("abs(-2vw)", "calc(2vw)")]
    [TestCase("abs(-2ex)", "abs(-2ex)")]
    [TestCase("sign(2em)", "sign(2em)")]
    [TestCase("sign(0em)", "sign(0em)")]
    [TestCase("abs(-10%)", "abs(-10%)")]
    [TestCase("sign(10%)", "sign(10%)")]
    public void UnresolvedBasesKeepTheRightDependency(string source, string expected)
    {
        var context = source.Contains('%') ?
            source.StartsWith("abs", StringComparison.Ordinal) ? MathTest.RawPercentage :
                new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Raw) :
            source.StartsWith("abs", StringComparison.Ordinal) ? MathTest.Length : MathTest.Number;
        var result = MathTest.Parse(source, context);
        result.Status.Should().Be(CssMathParseStatus.Match, source);
        var text = CssMathSerializer.SerializeSpecified(result.Value, new CssValueWork(default));
        text.Should().Be(expected);
        var again = MathTest.Parse(text, context).Value;
        again.Type.Should().Be(result.Value.Type);
        CssMathSerializer.SerializeSpecified(again, new CssValueWork(default)).Should().Be(text);
    }

    [Test]
    public void KernelAndArithmeticNegativeZeroKeepTheirBits()
    {
        var work = new CssValueWork(default);
        Bits(CssMathSign.Abs(-0d, work)).Should().Be(Bits(0d));
        Bits(CssMathSign.Sign(-0d, work)).Should().Be(Bits(-0d));
        Bits(CssMathSign.Sign(0d, work)).Should().Be(Bits(0d));
        CssMathSign.Sign(double.PositiveInfinity, work).Should().Be(1);
        CssMathSign.Sign(double.NegativeInfinity, work).Should().Be(-1);
        double.IsNaN(CssMathSign.Abs(double.NaN, work)).Should().BeTrue();
        double.IsNaN(CssMathSign.Sign(double.NaN, work)).Should().BeTrue();
        Bits(MathTest.Parse("abs(calc(0 / -infinity))", MathTest.Number)
            .Value.GetNode(0).Numeric.Value).Should().Be(Bits(0d));
        Bits(MathTest.Parse("sign(calc(0 / -infinity))", MathTest.Number)
            .Value.GetNode(0).Numeric.Value).Should().Be(Bits(-0d));
        Bits(MathTest.Parse("sign(-0)", MathTest.Number)
            .Value.GetNode(0).Numeric.Value).Should().Be(Bits(0d));
        Bits(CssMathSign.Sign(-double.Epsilon, work)).Should().Be(Bits(-1d));
        Bits(CssMathSign.Sign(double.Epsilon, work)).Should().Be(Bits(1d));
    }

    [Test]
    public void RecoveredEndAndLongLiteralUseTheExistingComponentAndConversion()
    {
        MathTest.Parse("abs(2px", MathTest.Length).Status.Should().Be(CssMathParseStatus.Match);
        MathTest.Parse("sign(-2px", MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);
        var literal = "1." + new string('0', 8192) + "1e+999999999";
        MathTest.Parse($"sign({literal})", MathTest.Number).Value.GetNode(0).Numeric.Value.Should().Be(1);
        MathTest.Parse($"abs(-{literal})", MathTest.Number).Value.GetNode(0).Numeric.Value.Should().Be(double.MaxValue);
    }

    [Test]
    public void PendingFamiliesStayPendingEvenWhenMalformedOrNested()
    {
        foreach (var source in new[] { "abs(sin())", "sign(pow(1,))", "calc(abs(1) + log())" })
            MathTest.Parse(source, MathTest.Number).Status.Should().Be(CssMathParseStatus.RequiresLaterGrammar);
    }

    [Test]
    public void DeepAndWideSignNodesUseLinearWork()
    {
        var nested = "1em";
        for (var i = 0; i < 64; i++) nested = i % 2 == 0 ? $"sign({nested})" : $"abs({nested})";
        var value = MathTest.Parse(nested, MathTest.Number).Value;
        var serialized = CssMathSerializer.SerializeSpecified(value, new CssValueWork(default));
        MathTest.Parse(serialized, MathTest.Number).Status.Should().Be(CssMathParseStatus.Match);

        static int Count(int count)
        {
            var source = "calc(" + string.Join(" + ", Enumerable.Repeat("sign(1em)", count)) + ")";
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var parsed = MathTest.Parse(source, MathTest.Number, work).Value;
            CssMathSerializer.SerializeSpecified(parsed, work).Length.Should().BeGreaterThan(count);
            return checks;
        }
        Count(256).Should().BeLessThan(Count(128) * 3);
    }

    [Test]
    public void CancellationDuringSignParsingPublishesNoResult()
    {
        var source = "calc(" + string.Join(" + ", Enumerable.Repeat("sign(1em)", 4096)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 3) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Number, work));
        checks.Should().Be(3);
    }

    [Test]
    public void LateCancellationAndSerializationUseTheirOwnScheduledCheckpoints()
    {
        var source = "calc(" + string.Join(" + ", Enumerable.Repeat("sign(1em)", 512)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        var totalChecks = 0;
        var value = CssMathParser.ParseMath(component, MathTest.Number,
            new CssValueWork(default, () => totalChecks++)).Value;
        totalChecks.Should().BeGreaterThan(3);
        using var parseCancellation = new CancellationTokenSource();
        var reached = 0;
        var parseWork = new CssValueWork(parseCancellation.Token, () =>
        {
            if (++reached == totalChecks - 2) parseCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Number, parseWork));
        reached.Should().Be(totalChecks - 2);

        using var serializeCancellation = new CancellationTokenSource();
        var emittedChecks = 0;
        var serializeWork = new CssValueWork(serializeCancellation.Token, () =>
        {
            if (++emittedChecks == 5) serializeCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathSerializer.SerializeSpecified(value, serializeWork));
        emittedChecks.Should().Be(5);
    }

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);
}

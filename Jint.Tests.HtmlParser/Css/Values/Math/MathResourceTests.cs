using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;

namespace Jint.Tests.HtmlParser.Css.Values.Math;

[TestFixture]
public sealed class MathResourceTests
{
    [Test]
    public void ContextAndResultDefaultsAreGuarded()
    {
        var context = default(CssMathContext);
        Assert.Throws<InvalidOperationException>(() => _ = context.Expected);
        Assert.Throws<InvalidOperationException>(() => _ = context.Percentages);
        Assert.Throws<InvalidOperationException>(() => _ = context.Range);
        Assert.Throws<InvalidOperationException>(() => _ = default(CssMathParseResult).Value);
        default(CssMathParseResult).Status.Should().Be(CssMathParseStatus.None);
        Assert.Throws<ArgumentException>(() => _ = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Length));
        Assert.Throws<ArgumentException>(() => _ = new CssMathRange(2, 1));
    }

    [Test]
    public void CapturedNestingLimitIsInclusive()
    {
        var component = MarkupParser.ParseCssComponentValues("calc((1))")[0];
        var atLimit = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
            maximumNestingDepth: 2);
        CssMathParser.ParseMath(component, atLimit, new CssValueWork(default)).Status.Should().Be(CssMathParseStatus.Match);
        var below = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
            maximumNestingDepth: 1);
        var exception = Assert.Throws<ParseLimitException>(() =>
            CssMathParser.ParseMath(component, below, new CssValueWork(default)));
        exception!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        exception.Observed.Should().Be(2);
    }

    [Test]
    public void PendingSubtreeStillEnforcesOriginalNestingLimit()
    {
        var component = MarkupParser.ParseCssComponentValues("calc(round((1)))")[0];
        var context = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
            maximumNestingDepth: 2);
        var exception = Assert.Throws<ParseLimitException>(() =>
            CssMathParser.ParseMath(component, context, new CssValueWork(default)));
        exception!.Observed.Should().Be(3);
    }

    [Test]
    public void FlatLongSumHasLinearArenaAndRoundTrips()
    {
        var source = "calc(" + string.Join(" + ", Enumerable.Repeat("1", 4096)) + ")";
        var value = MathTest.Parse(source, MathTest.Number).Value;
        value.NodeCount.Should().Be(1);
        CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be("calc(4096)");
    }

    [Test]
    public void DeepAlternatingFunctionsUseExplicitFrames()
    {
        const int depth = 64;
        var source = string.Concat(Enumerable.Repeat("calc(", depth)) + "1" + new string(')', depth);
        var context = new CssMathContext(CssMathProduction.Number, CssMathPercentageMode.Forbidden,
            maximumNestingDepth: depth);
        var value = MathTest.Parse(source, context).Value;
        value.NodeCount.Should().Be(1);
        CssMathSerializer.SerializeSpecified(value, new CssValueWork(default)).Should().Be("calc(1)");
    }

    [Test]
    public void ComponentLimitsAreNotChargedAgainByMathReader()
    {
        const string source = "calc(1px + 2px)";
        var component = MarkupParser.ParseCssComponentValues(source,
            new CssParseOptions { Limits = new ParseLimits { MaxInputCharacters = source.Length,
                MaxTokenCharacters = 5, MaxNestingDepth = 1 } })[0];
        var context = new CssMathContext(CssMathProduction.Length, CssMathPercentageMode.Forbidden,
            maximumNestingDepth: 1);
        CssMathParser.ParseMath(component, context, new CssValueWork(default)).Status.Should().Be(CssMathParseStatus.Match);
    }

    [Test]
    public void SerializationChecksItsOwnCallerWorkState()
    {
        var value = MathTest.Parse("calc(1em + 2px)", MathTest.Length).Value;
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checkpoints == 5) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathSerializer.SerializeSpecified(value, work));
        checkpoints.Should().BeGreaterThan(4);
    }

    [Test]
    public void WorkCheckpointsScaleLinearlyForFlatSums()
    {
        static int Count(int terms)
        {
            var source = "calc(" + string.Join(" + ", Enumerable.Repeat("1px", terms)) + ")";
            var component = MarkupParser.ParseCssComponentValues(source)[0];
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            CssMathParser.ParseMath(component, MathTest.Length, work).Status.Should().Be(CssMathParseStatus.Match);
            return checks;
        }

        var small = Count(256);
        var large = Count(512);
        large.Should().BeLessThan(small * 3);
    }

    [Test]
    public void AlternatingGroupedSumsDoNotRevisitEveryAncestor()
    {
        static int Count(int depth)
        {
            var expression = "1em";
            for (var i = 0; i < depth; i++) expression = "1px - (" + expression + " + 1em)";
            var component = MarkupParser.ParseCssComponentValues("calc(" + expression + ")")[0];
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var value = CssMathParser.ParseMath(component, MathTest.Length, work).Value;
            value.NodeCount.Should().BeLessThan(5);
            return checks;
        }

        var small = Count(64);
        var large = Count(128);
        large.Should().BeLessThan(small * 3);
    }

    [Test]
    public void CancellationAfterComponentParseDuringNumberConversionPublishesNothing()
    {
        var source = "calc(1." + new string('1', 10000) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checkpoints == 5) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Number, work));
        checkpoints.Should().BeGreaterThan(4);
    }

    [Test]
    public void CancellationDuringArenaFreezeReturnsNoValue()
    {
        var source = "calc(" + string.Join(" + ", Enumerable.Repeat("1px", 512)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        var checkpoints = 0;
        var probe = new CssValueWork(default, () => checkpoints++);
        CssMathParser.ParseMath(component, MathTest.Length, probe).Status.Should().Be(CssMathParseStatus.Match);
        checkpoints.Should().BeGreaterThan(3);

        using var cancellation = new CancellationTokenSource();
        var reached = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++reached == checkpoints - 2) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Length, work));
        reached.Should().Be(checkpoints - 2);
    }

    [Test]
    public void CancellationDuringWideFunctionTypeScanReturnsNoValue()
    {
        var source = "min(" + string.Join(", ", Enumerable.Repeat("1px", 8192)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        using var cancellation = new CancellationTokenSource();
        var reachedTypeScan = false;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (!CalledFrom("Frame", "Finish")) return;
            reachedTypeScan = true;
            cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Length, work));
        reachedTypeScan.Should().BeTrue();
    }

    [Test]
    public void CancellationDuringWideComparisonReturnsNoValue()
    {
        var source = "min(" + string.Join(", ", Enumerable.Repeat("1px", 8192)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        using var cancellation = new CancellationTokenSource();
        var reachedComparison = false;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (!CalledFrom("CssMathSimplifier", "TryFoldComparison")) return;
            reachedComparison = true;
            cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssMathParser.ParseMath(component, MathTest.Length, work));
        reachedComparison.Should().BeTrue();
    }

    [Test]
    public void WideUnresolvedArenaHasFreezeCheckpoints()
    {
        var source = "min(" + string.Join(", ", Enumerable.Repeat("calc(1em / 1px)", 4096)) + ")";
        var component = MarkupParser.ParseCssComponentValues(source)[0];
        var checkpoints = 0;
        var freezeCheckpoints = 0;
        var work = new CssValueWork(default, () =>
        {
            checkpoints++;
            if (CalledFrom("CssMathSimplifier", "Freeze")) freezeCheckpoints++;
        });
        CssMathParser.ParseMath(component, MathTest.Number, work).Status.Should().Be(CssMathParseStatus.Match);
        checkpoints.Should().BeGreaterThan(0);
        freezeCheckpoints.Should().BeGreaterThan(20);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NestedOmittedClampsMaterializeSumOnce(bool negate)
    {
        static (CssMathValue Value, int Checks) Build(int depth, bool negate)
        {
            var builder = new CssMathBuilder(new CssValueWork(default));
            var span = default(CssSourceSpan);
            var length = CssNumericType.FromUnit(CssUnit.Px);
            var root = builder.Add(CssMathNodeKind.Numeric, length, span,
                new CssMathNumeric(1, CssNumericKind.Dimension, CssUnit.Px, span));
            for (var i = 0; i < depth; i++)
            {
                var px = builder.Add(CssMathNodeKind.Numeric, length, span,
                    new CssMathNumeric(1, CssNumericKind.Dimension, CssUnit.Px, span));
                var em = builder.Add(CssMathNodeKind.Numeric, length, span,
                    new CssMathNumeric(1, CssNumericKind.Dimension, CssUnit.Em, span));
                var minimum = builder.Add(CssMathNodeKind.Min, length, span, children: [px, em]);
                var lower = builder.Add(CssMathNodeKind.AbsentBound, default, span);
                var upper = builder.Add(CssMathNodeKind.AbsentBound, default, span);
                var forwarded = builder.Add(CssMathNodeKind.Clamp, length, span,
                    children: [lower, root, upper]);
                if (negate) forwarded = builder.Add(CssMathNodeKind.Negate, length, span,
                    children: [forwarded]);
                root = builder.Add(CssMathNodeKind.Sum, length, span, children: [minimum, forwarded]);
            }
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var value = CssMathSimplifier.Freeze(builder, root, MathTest.Length, span, work);
            return (value, checks);
        }

        var small = Build(128, negate);
        var large = Build(256, negate);
        large.Value.NodeCount.Should().BeLessThan(small.Value.NodeCount * 3);
        large.Checks.Should().BeLessThan(small.Checks * 3);
        var serialized = CssMathSerializer.SerializeSpecified(small.Value, new CssValueWork(default));
        var reparsed = MathTest.Parse(serialized, MathTest.Length);
        reparsed.Status.Should().Be(CssMathParseStatus.Match);
        CssMathSerializer.SerializeSpecified(reparsed.Value, new CssValueWork(default)).Should().Be(serialized);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ForwardedProductsMaterializeOnce(bool clamp)
    {
        static (CssMathValue Value, int Checks) Build(int depth, bool useClamp)
        {
            var builder = new CssMathBuilder(new CssValueWork(default));
            var span = default(CssSourceSpan);
            var length = CssNumericType.FromUnit(CssUnit.Px);
            var px = builder.Add(CssMathNodeKind.Numeric, length, span,
                new CssMathNumeric(4, CssNumericKind.Dimension, CssUnit.Px, span));
            var em = builder.Add(CssMathNodeKind.Numeric, length, span,
                new CssMathNumeric(2, CssNumericKind.Dimension, CssUnit.Em, span));
            var root = builder.Add(CssMathNodeKind.Max, length, span, children: [px, em]);
            for (var i = 0; i < depth; i++)
            {
                int forwarded;
                if (useClamp)
                {
                    var lower = builder.Add(CssMathNodeKind.AbsentBound, default, span);
                    var upper = builder.Add(CssMathNodeKind.AbsentBound, default, span);
                    forwarded = builder.Add(CssMathNodeKind.Clamp, length, span,
                        children: [lower, root, upper]);
                }
                else forwarded = builder.Add(CssMathNodeKind.Min, length, span, children: [root]);
                var scalar = builder.Add(CssMathNodeKind.Numeric, default, span,
                    new CssMathNumeric(2, CssNumericKind.Number, CssUnit.None, span));
                root = builder.Add(CssMathNodeKind.Product, length, span, children: [forwarded, scalar]);
            }
            var checks = 0;
            var work = new CssValueWork(default, () => checks++);
            var value = CssMathSimplifier.Freeze(builder, root, MathTest.Length, span, work);
            return (value, checks);
        }

        var small = Build(128, clamp);
        var large = Build(256, clamp);
        var deep = Build(1024, clamp);
        large.Value.NodeCount.Should().BeLessThan(small.Value.NodeCount * 3);
        large.Checks.Should().BeLessThan(small.Checks * 3);
        deep.Checks.Should().BeGreaterThan(large.Checks);
        deep.Checks.Should().BeLessThan(large.Checks * 6);
        var serialized = CssMathSerializer.SerializeSpecified(Build(32, clamp).Value, new CssValueWork(default));
        var reparsed = MathTest.Parse(serialized, MathTest.Length);
        reparsed.Status.Should().Be(CssMathParseStatus.Match);
        CssMathSerializer.SerializeSpecified(reparsed.Value, new CssValueWork(default)).Should().Be(serialized);
    }

    private static bool CalledFrom(string typeName, string methodName) =>
        new System.Diagnostics.StackTrace().GetFrames()!.Any(frame =>
            frame.GetMethod() is { } method && method.Name == methodName &&
            method.DeclaringType?.Name == typeName);
}

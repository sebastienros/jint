#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.References;

[TestFixture]
public sealed class ReferenceWorkTests
{
    [Test]
    public void ExactConfiguredDepthBoundarySurvivesStandaloneParseAndAnalysis()
    {
        var limits = new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 2 } };
        var input = CssReferenceInput.Parse("foo(var(--x))", limits, default);
        CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue, new CssValueWork(default)).Kind
            .Should().Be(CssReferenceAnalysisKind.Deferred);
        var ex = Assert.Throws<ParseLimitException>(() =>
            CssReferenceInput.Parse("foo(bar(var(--x)))", limits, default));
        ex!.Kind.Should().Be(ParseLimitKind.NestingDepth);
        ex.Observed.Should().Be(3);
    }

    [Test]
    public void CancellationAtAnalysisCheckpointNeverPublishesPartialProgram()
    {
        var input = CssReferenceInput.Parse(string.Concat(Enumerable.Repeat("var(--x) ", 200)), null, default);
        using var cts = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cts.Token, () =>
        {
            if (++checks == 10) cts.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() =>
            CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue, work));
        checks.Should().BeGreaterThanOrEqualTo(10);
    }

    [Test]
    public void CancellationAtLastPublicationCheckpointPropagates()
    {
        var input = CssReferenceInput.Parse("var(--a) var(--b)", null, default);
        var total = 0;
        CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue,
            new CssValueWork(default, () => total++));
        using var cts = new CancellationTokenSource();
        var current = 0;
        Assert.Throws<OperationCanceledException>(() =>
            CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue,
                new CssValueWork(cts.Token, () =>
                {
                    if (++current == total) cts.Cancel();
                })));
    }

    [Test]
    public void WideAndDeepDoubledInputsHaveLinearCheckpointGrowth()
    {
        static int Count(string source)
        {
            var input = CssReferenceInput.Parse(source, null, default);
            var count = 0;
            CssReferenceParser.Analyze(input, CssReferenceUse.PropertyValue,
                new CssValueWork(default, () => count++));
            return count;
        }

        var wide = string.Concat(Enumerable.Repeat("var(--x,env(a 12345678901234567890)) ", 128));
        var first = Count(wide);
        var doubled = Count(wide + wide);
        doubled.Should().BeLessThan(first * 3);

        var deep = new string('(', 128) + "var(--x)" + new string(')', 128);
        var deepFirst = Count(deep);
        var deepDouble = Count(new string('(', 256) + "var(--x)" + new string(')', 256));
        deepDouble.Should().BeLessThan(deepFirst * 3);
    }
}

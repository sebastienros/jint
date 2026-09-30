using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Values;

public sealed class CssEasingValuesTests
{
    [TestCase("LINEAR", "linear", "Linear")]
    [TestCase("ease", "ease", "CubicBezier")]
    [TestCase(" ease-IN ", "ease-in", "CubicBezier")]
    [TestCase("ease-out", "ease-out", "CubicBezier")]
    [TestCase("ease-in-out", "ease-in-out", "CubicBezier")]
    [TestCase("cubic-bezier(.25, .1, .25, 1.0)", "cubic-bezier(0.25, 0.1, 0.25, 1)", "CubicBezier")]
    [TestCase("cubic-bezier(0, -2, 1, 3)", "cubic-bezier(0, -2, 1, 3)", "CubicBezier")]
    [TestCase("steps(2, end)", "steps(2)", "Steps")]
    [TestCase("steps(2)", "steps(2)", "Steps")]
    [TestCase("steps(2, start)", "steps(2, start)", "Steps")]
    [TestCase("steps(2, jump-end)", "steps(2, jump-end)", "Steps")]
    [TestCase("steps(2, jump-start)", "steps(2, jump-start)", "Steps")]
    [TestCase("steps(2, jump-none)", "steps(2, jump-none)", "Steps")]
    [TestCase("steps(1, jump-both)", "steps(1, jump-both)", "Steps")]
    [TestCase("step-start", "step-start", "Steps")]
    [TestCase("step-end", "step-end", "Steps")]
    [TestCase(@"e\61 se", "ease", "CubicBezier")]
    [TestCase("steps(2, /* boundary */ start)", "steps(2, start)", "Steps")]
    [TestCase("linear(0, .5, 1)", "linear(0, 0.5, 1)", "PiecewiseLinear")]
    [TestCase("linear(0 20% 40%, 1)", "linear(0 20%, 0 40%, 1)", "PiecewiseLinear")]
    [TestCase("linear(0 50%, .5 20%, 1)", "linear(0 50%, 0.5 50%, 1)", "PiecewiseLinear")]
    [TestCase("linear(.5)", "linear(0.5)", "PiecewiseLinear")]
    [TestCase("linear(20% 40% 0, 1)", "linear(0 20%, 0 40%, 1)", "PiecewiseLinear")]
    public void ParsesAndSerializes(string text, string expected, string kind)
    {
        CssEasingValues.TryParse(text, new CssValueWork(default), out var serialization, out var easing).Should().BeTrue();
        serialization.Should().Be(expected);
        easing.Kind.ToString().Should().Be(kind);
    }

    [TestCase("")]
    [TestCase("inherit")]
    [TestCase("auto")]
    [TestCase("linear ease")]
    [TestCase("ease, linear")]
    [TestCase("cubic-bezier(0, 0, 2, 1)")]
    [TestCase("cubic-bezier(-.1, 0, 1, 1)")]
    [TestCase("cubic-bezier(0 0 1 1)")]
    [TestCase("cubic-bezier(0, 0, 1)")]
    [TestCase("cubic-bezier(0, 0, 1, 1, 0)")]
    [TestCase("cubic-bezier(0%, 0, 1, 1)")]
    [TestCase("steps(0)")]
    [TestCase("steps(-1)")]
    [TestCase("steps(1, jump-none)")]
    [TestCase("steps(1.0)")]
    [TestCase("steps(1e2)")]
    [TestCase("steps(2,)")]
    [TestCase("steps(2, middle)")]
    [TestCase("steps(2 start)")]
    [TestCase("linear()")]
    [TestCase("linear(0, 1,)")]
    [TestCase("linear(0 0% 20% 30%, 1)")]
    [TestCase("linear(0px, 1)")]
    [TestCase("linear(0 20% 1, 1)")]
    [TestCase("linear(20% 0 40%, 1)")]
    public void RejectsInvalidGrammar(string text)
    {
        CssEasingValues.TryParse(text, new CssValueWork(default), out _, out _).Should().BeFalse();
    }

    [TestCase("linear", 0.4, false, 0.4)]
    [TestCase("ease-in-out", 0.5, false, 0.5)]
    [TestCase("ease", 0.5, false, 0.8024033876)]
    [TestCase("cubic-bezier(.5, 1, .5, 0)", -0.5, false, -1)]
    [TestCase("cubic-bezier(.5, 1, .5, 0)", 1.5, false, 2)]
    [TestCase("steps(4)", 0.5, false, 0.5)]
    [TestCase("steps(4)", 0.5, true, 0.25)]
    [TestCase("step-start", 0, false, 1)]
    [TestCase("step-start", 0, true, 0)]
    [TestCase("step-end", 1, false, 1)]
    [TestCase("step-end", 1, true, 0)]
    [TestCase("steps(2, jump-none)", 0.5, false, 1)]
    [TestCase("steps(2, jump-both)", 0.5, false, 2d / 3)]
    [TestCase("steps(2, start)", 1, false, 1)]
    [TestCase("linear(0, .8 50%, 1)", 0.25, false, 0.4)]
    [TestCase("linear(0 50%, 1 50%)", 0.5, false, 1)]
    [TestCase("linear(0 50%, 1 50%)", 0.5, true, 0)]
    [TestCase("linear(.5)", -1, false, 0.5)]
    [TestCase("linear(0, 1)", -0.5, false, -0.5)]
    [TestCase("linear(0, 1)", 1.5, false, 1.5)]
    public void EvaluatesWithoutLosingBoundaryDirection(string text, double input, bool before, double expected)
    {
        CssEasingValues.TryParse(text, new CssValueWork(default), out _, out var easing).Should().BeTrue();
        easing.Evaluate(input, before).Should().BeApproximately(expected, 1e-9);
    }

    [Test]
    public void ParsingChecksTheConsumersWorkBudget()
    {
        var checks = 0;
        CssEasingValues.TryParse("linear(0, 1)", new CssValueWork(default, () => checks++), out _, out _).Should().BeTrue();
        checks.Should().BeGreaterThan(0);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => CssEasingValues.TryParse("ease", new CssValueWork(cancellation.Token), out _, out _);
        action.Should().Throw<OperationCanceledException>();
    }
}

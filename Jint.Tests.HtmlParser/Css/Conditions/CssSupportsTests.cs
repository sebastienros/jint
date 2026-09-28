#nullable enable

using Jint.HtmlParser;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Conditions;

[TestFixture]
public sealed class CssSupportsTests
{
    [TestCase("color", "red", true)]
    [TestCase("COLOR", "red", true)]
    [TestCase(" color", "red", false)]
    [TestCase("c\\6flor", "red", false)]
    [TestCase("color", "red!important", false)]
    [TestCase("color", "red;display:block", false)]
    [TestCase("color", "red) or (display:block", false)]
    [TestCase("color", "red} @supports (display:block){}", false)]
    [TestCase("color", "var(--missing)", true)]
    [TestCase("width", "calc(1px + 2px)", true)]
    [TestCase("border-color", "red", false)]
    [TestCase("made-up", "inherit", false)]
    [TestCase("--", "", true)]
    [TestCase("--literal name", "anything", true)]
    [TestCase("--x", " ", true)]
    [TestCase("--x", "var(--other)", true)]
    [TestCase("--x", "!important", false)]
    [TestCase("--x", ";", false)]
    [TestCase("--x", "attr(data-x)", false)]
    public void DeclarationQueriesUseLiteralNamesAndRealValueGrammar(string name, string value, bool expected) =>
        CssSupports.EvaluateDeclaration(name, value, null, new CssValueWork(default)).Should().Be(expected);

    [TestCase("color:red", true)]
    [TestCase("color:red) or (width:1px", true)]
    [TestCase("c\\6flor:red", true)]
    [TestCase("(color:red !IMPORTANT)", true)]
    [TestCase("color:red !important", true)]
    [TestCase("(color:var(--missing))", true)]
    [TestCase("(--x:)", true)]
    [TestCase("(--:)", false)]
    [TestCase("not ()", true)]
    [TestCase("not (123)", true)]
    [TestCase("not (not garbage)", true)]
    [TestCase("(color:red) or ()", true)]
    [TestCase("(color:red;) ", false)]
    [TestCase("(color:red) or (width:bogus)", true)]
    [TestCase("(color:red) and (width:bogus)", false)]
    [TestCase("not (width:bogus)", true)]
    [TestCase("not unknown-function(foo)", true)]
    [TestCase("not (unknown-feature foo)", true)]
    [TestCase("(color:red) and ((width:1px) or (width:bogus))", true)]
    [TestCase("(color:red) or (width:1px) and (display:block)", false)]
    [TestCase("(color:red) or (width:1px) trailing", false)]
    [TestCase("(color:red) or )", false)]
    [TestCase("not (color:red) and (display:block)", false)]
    [TestCase("unknown-function(foo)", false)]
    [TestCase("(future-feature whatever)", false)]
    [TestCase("(border-color:red)", false)]
    [TestCase("(color:red", true)]
    public void ConditionsConsumeTheirEntireGrammar(string text, bool expected) =>
        CssSupports.EvaluateCondition(text, null, new CssValueWork(default)).Should().Be(expected);

    [TestCase("div > .a", true)]
    [TestCase(":is(div, span)", true)]
    [TestCase(":where(div, :unknown)", false)]
    [TestCase(":is(div, :where(span, :unknown))", false)]
    [TestCase(":is(div,)", false)]
    [TestCase("div, span", false)]
    [TestCase("ns|div", false)]
    [TestCase(":is(div, ns|span)", false)]
    [TestCase("[ns|attr]", false)]
    [TestCase("*|div", true)]
    [TestCase("div:lang(en)", true)]
    [TestCase("div:host", false)]
    [TestCase("div::-webkit-made-up", false)]
    [TestCase("div::before", true)]
    [TestCase("col || td", true)]
    [TestCase("div:has(> span)", true)]
    public void SelectorQueriesInspectImplementedCapabilities(string selector, bool expected) =>
        CssSupports.EvaluateCondition("selector(" + selector + ")", null, new CssValueWork(default)).Should().Be(expected);

    [Test]
    public void OrdinarySelectorCompilationStillRecoversForgivingBranches()
    {
        var selector = SelectorCompiler.Compile(":is(div, :unknown)", null, default);
        selector.Branches.Count.Should().Be(1);
    }

    [Test]
    public void DeepConditionsUseAnExplicitStack()
    {
        const int depth = 12000;
        var text = new string('(', depth) + "color:red" + new string(')', depth);
        CssSupports.EvaluateCondition(text, null, new CssValueWork(default)).Should().BeTrue();
    }

    [Test]
    public void WideConditionsDoNotSkipWorkAfterATrueOperand()
    {
        var text = "(color:red) or " + string.Join(" or ", Enumerable.Repeat("(color:bogus)", 3000));
        var checkpoints = 0;
        CssSupports.EvaluateCondition(text, null, new CssValueWork(default, () => checkpoints++)).Should().BeTrue();
        checkpoints.Should().BeGreaterThan(10);
        CssSupports.EvaluateCondition(text + " trailing", null, new CssValueWork(default)).Should().BeFalse();
    }

    [Test]
    public void DeepAndWideTraversalChargeLinearWork()
    {
        static int Count(string text)
        {
            var checks = 0;
            CssSupports.EvaluateCondition(text, null, new CssValueWork(default, () => checks++));
            return checks;
        }
        static string Deep(int size) => new string('(', size) + "color:red" + new string(')', size);
        static string Wide(int size) => string.Join(" or ", Enumerable.Repeat("(color:red)", size));
        Count(Deep(2000)).Should().BeLessThan(3 * Count(Deep(1000)));
        Count(Wide(2000)).Should().BeLessThan(3 * Count(Wide(1000)));
    }

    [Test]
    public void CancellationDuringOneLongTokenPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 8) cancellation.Cancel();
        });
        Action evaluate = () => CssSupports.EvaluateCondition("unknown(" + new string('a', 50000) + ")", null, work);
        evaluate.Should().Throw<OperationCanceledException>();
        checks.Should().Be(8);
    }

    [Test]
    public void LimitsAndHostCheckpointFailuresPropagate()
    {
        var options = new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 2 } };
        Action limited = () => CssSupports.EvaluateCondition("(((color:red)))", options, new CssValueWork(default));
        limited.Should().Throw<ParseLimitException>();
        var failure = new InvalidOperationException("host checkpoint");
        Action host = () => CssSupports.EvaluateCondition("(color:red)", null,
            new CssValueWork(default, () => throw failure));
        host.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(failure);
    }
}

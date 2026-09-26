#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssTransformTests
{
    [TestCase("translate", "-1in 20% 2pt", "-96px 20% 2.666667px")]
    [TestCase("translate", "calc(-10px + 20%) 0", "calc(-10px + 20%)")]
    [TestCase("translate", "calc(2em - 50%) -3vw", "calc(40px - 50%) -24px")]
    [TestCase("translate", "0 0 0", "0px")]
    [TestCase("rotate", "-.25turn", "-90deg")]
    [TestCase("rotate", "45deg 1 2 3", "1 2 3 45deg")]
    [TestCase("rotate", "0 0 0 45deg", "0 0 0 45deg")]
    [TestCase("rotate", "calc(1 + 1) 0 0 calc(.25turn)", "x 90deg")]
    [TestCase("rotate", "0 0 -2 .5turn", "-180deg")]
    [TestCase("rotate", "0", "0deg")]
    [TestCase("scale", "-50% 200% 100%", "-0.5 2")]
    [TestCase("scale", "calc(50% * 2)", "1")]
    [TestCase("scale", "calc(-2 * .25) 1 2", "-0.5 1 2")]
    [TestCase("scale", "calc(2em / 1px)", "40")]
    [TestCase("scale", "1 1 1", "1")]
    [TestCase("translate", "none", "none")]
    [TestCase("rotate", "none", "none")]
    [TestCase("scale", "none", "none")]
    public void ComponentsComputeThroughSharedNumericMetricsWithoutChangingIdentity(string name, string declared, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse(name + ":" + declared);
        var work = new CssValueWork(default);
        var query = Query(document, [(target, block)], work, new NativeCssMetrics { FontSize = 20 });
        var matching = new SelectorMatchWork(document, default);
        var result = query.GetProperty(target, name, ref matching);
        result.Text.Should().Be(expected);
        result.Value!.Kind.Should().Be(declared == "none" ? CssPropertyValueKind.Keyword : CssPropertyValueKind.Transform);
        block.GetPropertyValue(name, work).Should().NotBeEmpty();
    }

    [TestCase("translate", "2em", "C6:font-size")]
    [TestCase("translate", "calc(2em - 50%)", "C6:font-size")]
    [TestCase("translate", "2cqw", "C6:container-length")]
    [TestCase("scale", "calc(2em / 1px)", "C6:font-size")]
    [TestCase("rotate", "calc(2em / 1px) 0 0 45deg", "C6:font-size")]
    public void MissingMetricsRemainNamedFailuresAndDoNotBlockUnrelatedReads(string name, string declared, string blocker)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("display:block;" + name + ":" + declared);
        var query = Query(document, [(target, block)], new(default));
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        Action read = () => query.GetProperty(target, name, ref matching);
        read.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
    }

    [TestCase("translate", "10px 20%", "none")]
    [TestCase("rotate", "x 45deg", "none")]
    [TestCase("scale", "2", "none")]
    public void ExplicitInheritanceAndInvalidVariableWinnersUseTheNormalCascade(string name, string parentValue, string initial)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        document.AppendChild(parent);
        parent.AppendChild(child);
        var parentBlock = CssDeclarationBlock.Parse(name + ":" + parentValue);
        foreach (var declaration in new[] { "", name + ":unset", name + ":inherit", name + ":initial",
                     name + ":" + parentValue + ";--bad:bogus;" + name + ":var(--bad) !important" })
        {
            var block = CssDeclarationBlock.Parse(declaration);
            var query = Query(document, [(parent, parentBlock), (child, block)], new(default));
            var matching = new SelectorMatchWork(document, default);
            var result = query.GetProperty(child, name, ref matching);
            result.Text.Should().Be(declaration == name + ":inherit" ? parentValue : initial);
            if (declaration.Contains("var(--bad)", StringComparison.Ordinal))
                result.Disposition.Should().Be(NativeCssDisposition.InvalidAtComputedValue);
        }
    }

    [Test]
    public void LongTransformMathComputationPollsWorkCancellation()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var source = "translate:hypot(" + string.Join(", ", Enumerable.Repeat("1em, 1px", 4096)) + ")";
        var block = CssDeclarationBlock.Parse(source);
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (armed && ++polls == 8) cancellation.Cancel(); });
        var query = Query(document, [(target, block)], work, new NativeCssMetrics { FontSize = 20 });
        var matching = new SelectorMatchWork(document, cancellation.Token);
        armed = true;
        Action read = () => query.GetProperty(target, "translate", ref matching);
        read.Should().Throw<OperationCanceledException>();
    }

    private static NativeCssQuery Query(Document document,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline, CssValueWork work,
        NativeCssMetrics? metrics = null) =>
        new(document, [], inline, new CssMediaEnvironment { Width = 800, Height = 600 }, new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, metrics);
}

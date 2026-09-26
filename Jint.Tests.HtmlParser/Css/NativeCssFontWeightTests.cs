#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssFontWeightTests
{
    [TestCase("normal", "400")]
    [TestCase("bold", "700")]
    [TestCase("456.5", "456.5")]
    [TestCase("calc(2000)", "1000")]
    [TestCase("calc(-100)", "1")]
    [TestCase("initial", "400")]
    [TestCase("bolder", "700")]
    [TestCase("lighter", "100")]
    public void RootWeightsComputeToNumbers(string declared, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var block = CssDeclarationBlock.Parse("font-weight:" + declared);
        var work = new CssValueWork(default);
        var query = Query(document, [(root, block)], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "font-weight", ref matching).Text.Should().Be(expected);
        block.GetPropertyValue("font-weight", work).Should().NotBeEmpty();
    }

    [TestCase("1", "bolder", "400")]
    [TestCase("349.5", "bolder", "400")]
    [TestCase("350", "bolder", "700")]
    [TestCase("549.5", "bolder", "700")]
    [TestCase("550", "bolder", "900")]
    [TestCase("899.5", "bolder", "900")]
    [TestCase("900.5", "bolder", "900.5")]
    [TestCase("99.5", "lighter", "99.5")]
    [TestCase("100", "lighter", "100")]
    [TestCase("549.5", "lighter", "100")]
    [TestCase("550", "lighter", "400")]
    [TestCase("749.5", "lighter", "400")]
    [TestCase("750", "lighter", "700")]
    [TestCase("1000", "lighter", "700")]
    [TestCase("456.5", "inherit", "456.5")]
    [TestCase("456.5", "unset", "456.5")]
    [TestCase("bold", "var(--w)", "900")]
    public void RelativeAndInheritedWeightsUseTheComputedParent(string parentValue, string childValue, string expected)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("main");
        var child = document.CreateElement("div");
        document.AppendChild(parent);
        parent.AppendChild(child);
        var parentBlock = CssDeclarationBlock.Parse("font-weight:" + parentValue);
        var childBlock = CssDeclarationBlock.Parse("--w:bolder;font-weight:" + childValue);
        var work = new CssValueWork(default);
        var query = Query(document, [(parent, parentBlock), (child, childBlock)], work);
        var matching = new SelectorMatchWork(document, default);
        var computed = query.GetProperty(child, "FONT-WEIGHT", ref matching);
        computed.Text.Should().Be(expected);
        query.GetProperty(child, "font-weight", ref matching).Should().BeSameAs(computed);
        childBlock.GetPropertyValue("font-weight", work).Should().Be(childValue);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeepRelativeWeightsAreIterativeAndCancelable(bool cancel)
    {
        var document = Document.CreateHtml();
        Node parent = document;
        var inline = new List<(Element, CssDeclarationBlock)>();
        var block = CssDeclarationBlock.Parse("font-weight:bolder");
        for (var i = 0; i < 8192; i++)
        {
            var element = document.CreateElement("div");
            parent.AppendChild(element);
            inline.Add((element, block));
            parent = element;
        }
        using var cancellation = new CancellationTokenSource();
        var armed = false;
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (armed && ++polls == 128) cancellation.Cancel(); });
        var query = Query(document, inline, work);
        var matching = new SelectorMatchWork(document, cancellation.Token);
        armed = cancel;
        if (cancel)
        {
            Action read = () => query.GetProperty((Element) parent, "font-weight", ref matching);
            read.Should().Throw<OperationCanceledException>();
        }
        else query.GetProperty((Element) parent, "font-weight", ref matching).Text.Should().Be("900");
    }

    private static NativeCssQuery Query(Document document,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline, CssValueWork work) =>
        new(document, [], inline, new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
}

#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssTextAlignTests
{
    [TestCase("rtl", "ltr", "match-parent", "right", "left")]
    [TestCase("ltr", "rtl", "match-parent", "left", "right")]
    [TestCase("rtl", "ltr", "inherit", "start", "end")]
    [TestCase("rtl", "ltr", "unset", "start", "end")]
    [TestCase("rtl", "ltr", "initial", "start", "auto")]
    [TestCase("rtl", "ltr", "var(--align)", "right", "left")]
    public void CorrespondingParentLonghandsUseTheParentDirectionOnlyForMatchParent(string parentDirection,
        string childDirection, string declared, string expectedAll, string expectedLast)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        var grandchild = document.CreateElement("span");
        document.AppendChild(root);
        root.AppendChild(child);
        child.AppendChild(grandchild);
        var work = new CssValueWork(default);
        var query = Query(document, [(root, CssDeclarationBlock.Parse("direction:" + parentDirection
            + ";text-align-all:start;text-align-last:end")),
            (child, CssDeclarationBlock.Parse("direction:" + childDirection + ";--align:match-parent;text-align:" + declared)),
            (grandchild, CssDeclarationBlock.Parse("text-align:inherit"))], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "text-align-all", ref matching).Text.Should().Be(expectedAll);
        query.GetProperty(child, "text-align-last", ref matching).Text.Should().Be(expectedLast);
        query.GetProperty(grandchild, "text-align-all", ref matching).Text.Should().Be(expectedAll);
        query.GetProperty(grandchild, "text-align-last", ref matching).Text.Should().Be(expectedLast);
    }

    [Test]
    public void RootMatchParentUsesStartAndPhysicalParentValuesArePreserved()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var query = Query(document, [(root, CssDeclarationBlock.Parse("direction:rtl;text-align:match-parent")),
            (child, CssDeclarationBlock.Parse("direction:ltr;text-align:match-parent"))], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "text-align-all", ref matching).Text.Should().Be("start");
        query.GetProperty(root, "text-align-last", ref matching).Text.Should().Be("start");
        query.GetProperty(child, "text-align-all", ref matching).Text.Should().Be("right");
        query.GetProperty(child, "text-align-last", ref matching).Text.Should().Be("right");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeepMatchParentDependenciesAreIterativeAndCancelable(bool cancel)
    {
        var document = Document.CreateHtml();
        Node parent = document;
        var inline = new List<(Element, CssDeclarationBlock)>();
        var block = CssDeclarationBlock.Parse("text-align:match-parent");
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
            Action read = () => query.GetProperty((Element) parent, "text-align-all", ref matching);
            read.Should().Throw<OperationCanceledException>();
        }
        else query.GetProperty((Element) parent, "text-align-all", ref matching).Text.Should().Be("left");
    }

    private static NativeCssQuery Query(Document document,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline, CssValueWork work) =>
        new(document, [], inline, new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
}

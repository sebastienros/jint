#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssFontSizeTests
{
    [TestCase("xx-small", "9.6px")]
    [TestCase("x-small", "12px")]
    [TestCase("small", "14.222222px")]
    [TestCase("medium", "16px")]
    [TestCase("large", "19.2px")]
    [TestCase("x-large", "24px")]
    [TestCase("xx-large", "32px")]
    [TestCase("xxx-large", "48px")]
    [TestCase("larger", "19.2px")]
    [TestCase("smaller", "13.333333px")]
    [TestCase("2rem", "32px")]
    [TestCase("150%", "24px")]
    [TestCase("calc(50% + 2px)", "10px")]
    [TestCase("calc(-1px)", "0px")]
    [TestCase("0", "0px")]
    public void RootSizesUseTheHostInitialBasis(string declared, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var work = new CssValueWork(default);
        var query = Query(document, [(root, CssDeclarationBlock.Parse("font-size:" + declared))], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "font-size", ref matching).Text.Should().Be(expected);
    }

    [TestCase("2em")]
    [TestCase("calc(1em + 100%)")]
    public void ParentRootAndOwnMetricsHaveDifferentBasesAndWarmResultsAreReused(string grandchildSize)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        var grandchild = document.CreateElement("span");
        document.AppendChild(root);
        root.AppendChild(child);
        child.AppendChild(grandchild);
        var work = new CssValueWork(default);
        var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
        var query = Query(document,
            [(root, CssDeclarationBlock.Parse("font-size:20px")),
             (child, CssDeclarationBlock.Parse("--s:150%;font-size:var(--s);width:2em;height:2rem")),
             (grandchild, CssDeclarationBlock.Parse("font-size:" + grandchildSize + ";width:2em"))], work, diagnostics);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "width", ref matching).Text.Should().Be("60px");
        query.GetProperty(child, "height", ref matching).Text.Should().Be("40px");
        var size = query.GetProperty(grandchild, "font-size", ref matching);
        size.Text.Should().Be("60px");
        query.GetProperty(grandchild, "width", ref matching).Text.Should().Be("120px");
        query.GetProperty(grandchild, "font-size", ref matching).Should().BeSameAs(size);
        foreach (var element in new[] { root, child, grandchild })
            diagnostics.Queries.Single().Elements![element].ComputedPublications["font-size"].Should().Be(1);
    }

    [Test]
    public void AbsoluteSizesAndUnrelatedPropertiesDoNotWarmFontAncestors()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var diagnostics = new NativeCssQueryDiagnostics(captureDetails: true);
        var query = Query(document,
            [(root, CssDeclarationBlock.Parse("font-size:2ch")),
             (child, CssDeclarationBlock.Parse("font-size:33px;width:10px;color:red"))], work, diagnostics);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "color", ref matching).Text.Should().Be("rgb(255, 0, 0)");
        query.GetProperty(child, "width", ref matching).Text.Should().Be("10px");
        diagnostics.Queries.Single().ComputedPublications.Should().NotContainKey("font-size");
        query.GetProperty(child, "font-size", ref matching).Text.Should().Be("33px");
        diagnostics.Queries.Single().Elements!.Should().NotContainKey(root);
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(root, "font-size", ref matching))!
            .Blocker.Should().Be("C6:zero-advance");
    }

    [TestCase("inherit", "20px")]
    [TestCase("unset", "20px")]
    [TestCase("initial", "16px")]
    [TestCase("larger", "24px")]
    [TestCase("smaller", "16.666667px")]
    [TestCase("var(--missing)", "20px")]
    public void RelativeKeywordsAndCssWideValuesUseTheCorrectBasis(string declared, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var query = Query(document, [(root, CssDeclarationBlock.Parse("font-size:20px")),
            (child, CssDeclarationBlock.Parse("font-size:" + declared))], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "font-size", ref matching).Text.Should().Be(expected);
    }

    [Test]
    public void MediumUsesTheExplicitHostInitialFontSize()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var work = new CssValueWork(default);
        var query = new NativeCssQuery(document, [], [(root, CssDeclarationBlock.Parse("font-size:xxx-large"))],
            new CssMediaEnvironment { InitialFontSize = 24 }, new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "font-size", ref matching).Text.Should().Be("72px");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeepFontSizeDependenciesAreIterativeAndCancelable(bool cancel)
    {
        var document = Document.CreateHtml();
        Node parent = document;
        var inline = new List<(Element, CssDeclarationBlock)>();
        var block = CssDeclarationBlock.Parse("font-size:100%");
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
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = Query(document, inline, work, diagnostics);
        var matching = new SelectorMatchWork(document, cancellation.Token);
        armed = cancel;
        if (cancel)
        {
            Action read = () => query.GetProperty((Element) parent, "font-size", ref matching);
            read.Should().Throw<OperationCanceledException>();
        }
        else
        {
            var result = query.GetProperty((Element) parent, "font-size", ref matching);
            result.Text.Should().Be("16px");
            diagnostics.Queries.Single().ComputedPublications["font-size"].Should().Be(8192);
            query.GetProperty((Element) parent, "font-size", ref matching).Should().BeSameAs(result);
            diagnostics.Queries.Single().ComputedPublications["font-size"].Should().Be(8192);
        }
    }

    private static NativeCssQuery Query(Document document,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline, CssValueWork work,
        NativeCssQueryDiagnostics? diagnostics = null) =>
        new(document, [], inline, new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, diagnostics: diagnostics);
}

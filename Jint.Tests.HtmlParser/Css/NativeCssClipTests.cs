using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssClipTests
{
    [TestCase("", "auto")]
    [TestCase("clip:inherit", "rect(10px, auto, -20px, 0px)")]
    [TestCase("clip:initial", "auto")]
    [TestCase("clip:unset", "auto")]
    [TestCase("clip:rect(0, 0, 0, 0)", "rect(0px, 0px, 0px, 0px)")]
    [TestCase("clip:rect(auto auto auto auto)", "rect(auto, auto, auto, auto)")]
    [TestCase("clip:rect(-2em, 3rem, calc(1em + 2rem), auto)", "rect(-40px, 30px, 40px, auto)")]
    [TestCase("clip:rect(min(2em, 3rem), max(-1em, -2rem), calc(-3px), 0)", "rect(30px, -20px, -3px, 0px)")]
    [TestCase("clip:rect(1in, -2.54cm, 72pt, 0)", "rect(96px, -96px, 96px, 0px)")]
    [TestCase("clip:rect(10vw, 10vh, -5vw, auto)", "rect(80px, 60px, -40px, auto)")]
    [TestCase("--clip:rect(2em, auto, -1rem, 0);clip:var(--clip)", "rect(40px, auto, -10px, 0px)")]
    [TestCase("--edges:2em, auto, -1rem, 0;clip:rect(var(--edges))", "rect(40px, auto, -10px, 0px)")]
    [TestCase("--edge:2em;clip:rect(0, var(--edge), 0, auto)", "rect(0px, 40px, 0px, auto)")]
    [TestCase("--edge:10%;clip:rect(0, var(--edge), 0, auto)", "auto")]
    [TestCase("clip:rect(1px, 2px, 3px, 4px);clip:var(--missing)", "auto")]
    [TestCase("clip:rect(env(clip-top), 0, auto, 0)", "rect(40px, 0px, auto, 0px)")]
    [TestCase("clip:var(--missing, rect(0 0 0 0))", "rect(0px, 0px, 0px, 0px)")]
    public void ComputesEdgesWithoutClampingOrInventingAutoGeometry(string source, string expected)
    {
        var (document, root, child) = Tree();
        var work = new CssValueWork(default);
        var query = Query(document,
            [(root, CssDeclarationBlock.Parse("font-size:10px;clip:rect(1em, auto, -2em, 0)")),
             (child, CssDeclarationBlock.Parse("font-size:20px;" + source))], work);
        var matching = new SelectorMatchWork(document, default);
        var value = query.GetProperty(child, "clip", ref matching);
        value.Text.Should().Be(expected);
        query.GetProperty(child, "clip", ref matching).Should().BeSameAs(value);
        if (expected.StartsWith("rect(", StringComparison.Ordinal))
            value.Value!.Kind.Should().Be(CssPropertyValueKind.ClipRectangle);
    }

    [TestCase("auto")]
    [TestCase("rect(auto, auto, auto, auto)")]
    [TestCase("rect(1px, 2px, 3px, 4px)")]
    public void IndependentRectanglesDoNotDemandFontOrBoxMetrics(string source)
    {
        var (document, _, child) = Tree();
        var work = new CssValueWork(default);
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = Query(document,
            [(child, CssDeclarationBlock.Parse("font-size:2ch;width:3cqw;clip:" + source))], work, diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "clip", ref matching).Text.Should().Be(source);
        diagnostics.Queries.Single().ComputedPublications.Keys.Should().Equal("clip");
    }

    [Test]
    public void GlyphMetricsRemainAnExplicitDependency()
    {
        var (document, _, child) = Tree();
        var inline = new[] { (child, CssDeclarationBlock.Parse("clip:rect(-2ch, auto, 0, 0)")) };
        var query = Query(document, inline, new CssValueWork(default));
        var matching = new SelectorMatchWork(document, default);
        Action read = () => query.GetProperty(child, "clip", ref matching);
        read.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be("C6:zero-advance");
        query = Query(document, inline, new CssValueWork(default), new NativeCssMetrics { ZeroAdvance = 7 });
        query.GetProperty(child, "clip", ref matching).Text.Should().Be("rect(-14px, auto, 0px, 0px)");
    }

    [Test]
    public void CancellationCannotPublishAPartialRectangle()
    {
        var (document, _, child) = Tree();
        using var cancellation = new CancellationTokenSource();
        var work = new CssValueWork(cancellation.Token);
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = Query(document, [(child, CssDeclarationBlock.Parse("clip:rect(1em, 2rem, 3px, auto)"))],
            work, diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, cancellation.Token);
        cancellation.Cancel();
        Action read = () => query.GetProperty(child, "clip", ref matching);
        read.Should().Throw<OperationCanceledException>();
        diagnostics.Queries.Single().ComputedPublications.Should().BeEmpty();
    }

    private static (Document Document, Element Root, Element Child) Tree()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        return (document, root, child);
    }

    private static NativeCssQuery Query(Document document, IReadOnlyList<(Element, CssDeclarationBlock)> inline,
        CssValueWork work, NativeCssMetrics? metrics = null, NativeCssQueryDiagnostics? diagnostics = null) =>
        new(document, [], inline, new CssMediaEnvironment { Width = 800, Height = 600 },
            new(document, null, null, null),
            CssEnvironmentSnapshot.Create(
                [CssEnvironmentBinding.Create("clip-top", [], CssReferenceInput.Parse("2em", null, default), work)], work),
            work, metrics, diagnostics: diagnostics);
}

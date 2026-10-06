using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssLayerTests
{
    [TestCase("@layer a {#t{display:none}} @layer b {div{display:block}}", "block")]
    [TestCase("@layer a {div{display:block!important}} @layer b {#t{display:none!important}}", "block")]
    [TestCase("div{display:block} @layer a {#t{display:none}}", "block")]
    [TestCase("div{display:none!important} @layer a {div{display:block!important}}", "block")]
    [TestCase("@layer a,b; @layer b {div{display:block}} @layer a {div{display:none}}", "block")]
    [TestCase("@layer a{div{display:none}} @layer b{div{display:block}} @layer a{div{display:flex}}", "block")]
    [TestCase("@layer a{div{display:block} @layer nested {#t{display:none}}}", "block")]
    [TestCase("@layer a{div{display:none!important} @layer nested {div{display:block!important}}}", "block")]
    [TestCase("@layer a.b {div{display:none}} @layer c {div{display:block}} @layer a.c {div{display:flex}}", "block")]
    [TestCase("@layer {div{display:none}} @layer {div{display:block}}", "block")]
    [TestCase("@layer A {div{display:none}} @layer a {div{display:block}} @layer A {div{display:flex}}", "block")]
    [TestCase("@media not all {@layer b{}} @layer a,b; @layer a{div{display:none}} @layer b{div{display:block}}", "block")]
    [TestCase("@supports (unknown-property:bogus) {@layer b{}} @layer a,b; @layer a{div{display:none}} @layer b{div{display:block}}", "block")]
    [TestCase("@container (width > 999999px) {@layer b{}} @layer a,b; @layer a{div{display:block}} @layer b{div{display:none}}", "none")]
    [TestCase("@layer a{div{display:block}} @layer b{div{display:none} #t{display:revert-layer}}", "block")]
    [TestCase("@layer a{div{display:block}} div{display:none} #t{display:revert-layer}", "block")]
    [TestCase("@layer a{div{display:block}} @layer b{div{display:revert-layer!important}} @layer c{div{display:none!important}}", "block")]
    [TestCase("@layer a{div{--d:block}} @layer b{div{--d:revert-layer}} div{display:var(--d)}", "revert-layer")]
    [TestCase("@layer a{div{display:block}} @layer b{div{all:revert-layer}}", "block")]
    public void LayerPrecedenceAndRollbackFollowTheCascadeRatherThanSpecificity(string css, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        target.SetAttribute("id", "t");
        var query = Query(document, [new(CssStyleSheet.Parse(css), NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be(expected);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InlineRollbackKeepsStylesheetDeclarations(bool important)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse("display:revert-layer" + (important ? "!important" : ""));
        var work = new CssValueWork(default);
        var query = new NativeCssQuery(document,
            [new(CssStyleSheet.Parse("@layer a{div{display:block}} div{display:flex}"), NativeCssOrigin.Author)],
            [(target, block)], new(), new(document, null, null, null), work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("flex");
    }

    [Test]
    public void LayerOrderSpansSheetsButNeverCrossesOrigins()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var query = Query(document,
        [
            new(CssStyleSheet.Parse("@layer b,a; @layer a{div{display:none!important}}"), NativeCssOrigin.User),
            new(CssStyleSheet.Parse("@layer a,b; @layer b{div{display:block!important}}"), NativeCssOrigin.User),
            new(CssStyleSheet.Parse("@layer b,a; @layer a{div{display:flex!important}}"), NativeCssOrigin.Author)
        ]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
    }

    [Test]
    public void ImportLayersJoinTheImportingOriginAndMutationsInvalidateTheOrder()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var sheet = CssStyleSheet.Parse("@layer a,b; @import 'child'; @layer a{div{display:none}}");
        var child = CssStyleSheet.Parse("@layer b{div{display:block}}");
        ((CssImportRule) sheet.Rules[1]).SetStyleSheet(child, null, null, new(default));
        var query = Query(document, [new(sheet, NativeCssOrigin.Author)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "display", ref matching).Text.Should().Be("block");
        sheet.DeleteRule(0);
        Action stale = () => query.GetProperty(target, "display", ref matching);
        stale.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
        Query(document, [new(sheet, NativeCssOrigin.Author)]).GetProperty(target, "display", ref matching).Text.Should().Be("none");
    }

    private static NativeCssQuery Query(Document document, NativeCssSheet[] sheets)
    {
        var work = new CssValueWork(default);
        return new NativeCssQuery(document, sheets, [], new CssMediaEnvironment(), new(document, null, null, null),
            work);
    }
}

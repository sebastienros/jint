#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssBoxQueryTests
{
    [TestCase("padding:1px", "padding", "1px")]
    [TestCase("padding:0", "padding", "0px")]
    [TestCase("margin:-0", "margin", "0px")]
    [TestCase("min-width:0", "min-width", "0px")]
    [TestCase("padding:calc(-2px)", "padding-top", "0px")]
    [TestCase("padding:calc(-2%)", "padding-top", "0%")]
    [TestCase("margin:calc(-2px)", "margin-left", "-2px")]
    [TestCase("margin:-3%", "margin-top", "-3%")]
    [TestCase("padding:25%", "padding-right", "25%")]
    [TestCase("padding:calc(50% - 2px)", "padding-bottom", "calc(50% - 2px)")]
    [TestCase("margin:auto", "margin", "auto")]
    [TestCase("min-width:calc(-2px)", "min-width", "0px")]
    [TestCase("max-height:calc(-2px)", "max-height", "0px")]
    [TestCase("max-width:none", "max-width", "none")]
    [TestCase("--edges:1px 2px 3px 4px;padding:var(--edges)", "padding", "1px 2px 3px 4px")]
    [TestCase("--edges:-1px auto;margin:var(--edges)", "margin", "-1px auto")]
    [TestCase("--edges:-1px;padding:var(--edges)", "padding", "0px")]
    [TestCase("gap:calc(-2px)", "row-gap", "0px")]
    [TestCase("gap:calc(-2%)", "column-gap", "0%")]
    [TestCase("gap:calc(50% - 2px)", "row-gap", "calc(50% - 2px)")]
    [TestCase("gap:normal", "gap", "normal")]
    [TestCase("gap:thin thick", "gap", "thin thick")]
    [TestCase("--edges:1px 2%;grid-gap:var(--edges)", "gap", "1px 2%")]
    [TestCase("--edges:-1px;gap:var(--edges)", "gap", "normal")]
    [TestCase("--alignment:last baseline;place-content:var(--alignment)", "place-content", "last baseline")]
    [TestCase("place-content:safe center unsafe right", "justify-content", "unsafe right")]
    public void ComputesPhysicalSidesWithoutLosingPercentageBases(string source, string name, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var block = CssDeclarationBlock.Parse(source);
        var query = Query(document, [(target, block)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, name, ref matching).Text.Should().Be(expected);
    }

    [Test]
    public void UnresolvedPaddingMathCarriesItsClampAndMarginDoesNot()
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var query = Query(document, [(target, CssDeclarationBlock.Parse("padding:calc(50% - 2px);margin:calc(50% - 2px)"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "padding-top", ref matching).Value!.Math.Context.Range.Lower.Should().Be(0);
        query.GetProperty(target, "margin-top", ref matching).Value!.Math.Context.Range.Lower.Should().BeNull();
    }

    [TestCase("margin-left", "-2em", "-40px")]
    [TestCase("padding-left", "2em", "40px")]
    [TestCase("min-width", "2em", "40px")]
    [TestCase("max-height", "2em", "40px")]
    [TestCase("row-gap", "2em", "40px")]
    [TestCase("column-gap", "2em", "40px")]
    public void RelativeLengthsUseTheDeclaredFontSize(string name, string source, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var inline = new[] { (target, CssDeclarationBlock.Parse("font-size:20px;" + name + ":" + source)) };
        var query = Query(document, inline);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, name, ref matching).Text.Should().Be(expected);
    }

    [TestCase("margin-left", "-2ch", "-14px")]
    [TestCase("padding-left", "2ch", "14px")]
    [TestCase("row-gap", "2ch", "14px")]
    public void GlyphRelativeLengthsStillRequireRealMetrics(string name, string source, string expected)
    {
        var document = Document.CreateHtml();
        var target = document.CreateElement("div");
        var inline = new[] { (target, CssDeclarationBlock.Parse(name + ":" + source)) };
        var query = Query(document, inline);
        var matching = new SelectorMatchWork(document, default);
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(target, name, ref matching))!
            .Blocker.Should().Be("C6:zero-advance");
        query = Query(document, inline, new NativeCssMetrics { ZeroAdvance = 7 });
        query.GetProperty(target, name, ref matching).Text.Should().Be(expected);
    }

    [Test]
    public void EmAndRemUseDifferentDeclaredFontSizeContexts()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var target = document.CreateElement("span");
        document.AppendChild(root);
        root.AppendChild(target);
        var query = Query(document,
            [(root, CssDeclarationBlock.Parse("font-size:10px")),
             (target, CssDeclarationBlock.Parse("font-size:20px;margin-left:2em;padding-left:2rem"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(target, "margin-left", ref matching).Text.Should().Be("40px");
        query.GetProperty(target, "padding-left", ref matching).Text.Should().Be("20px");
    }

    [TestCase("margin-left", "0px")]
    [TestCase("padding-top", "0px")]
    [TestCase("min-width", "auto")]
    [TestCase("max-height", "none")]
    [TestCase("row-gap", "normal")]
    [TestCase("column-gap", "normal")]
    public void PhysicalBoxValuesAreNotInheritedWithoutAnExplicitKeyword(string name, string initial)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        var child = document.CreateElement("span");
        document.AppendChild(parent);
        parent.AppendChild(child);
        var parentBlock = CssDeclarationBlock.Parse(name + ":20px");
        var matching = new SelectorMatchWork(document, default);
        Query(document, [(parent, parentBlock)]).GetProperty(child, name, ref matching).Text.Should().Be(initial);
        Query(document, [(parent, parentBlock), (child, CssDeclarationBlock.Parse(name + ":inherit"))])
            .GetProperty(child, name, ref matching).Text.Should().Be("20px");
    }

    [Test]
    public void ContentAlignmentIsNotInheritedUnlessExplicitlyRequested()
    {
        var document = MarkupParser.ParseHtml("<div><span></span></div>");
        var parent = (Element) document.DocumentElement!.LastChild!.FirstChild!;
        var child = (Element) parent.FirstChild!;
        var source = CssDeclarationBlock.Parse("place-content:space-between safe right");
        var matching = new SelectorMatchWork(document, default);
        Query(document, [(parent, source)]).GetProperty(child, "place-content", ref matching).Text.Should().Be("normal");
        Query(document, [(parent, source), (child, CssDeclarationBlock.Parse("place-content:inherit"))])
            .GetProperty(child, "place-content", ref matching).Text.Should().Be("space-between safe right");
    }

    private static NativeCssQuery Query(Document document, IReadOnlyList<(Element, CssDeclarationBlock)> inline,
        NativeCssMetrics? metrics = null)
    {
        var work = new CssValueWork(default);
        return new(document, [], inline, new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work, metrics);
    }
}

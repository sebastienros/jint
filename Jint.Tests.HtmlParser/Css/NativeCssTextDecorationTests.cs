#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssTextDecorationTests
{
    [TestCase("-2px", "-2px")]
    [TestCase("calc(-4px)", "-4px")]
    [TestCase("0", "0px")]
    [TestCase("-25%", "-25%")]
    [TestCase("2em", "40px")]
    [TestCase("3rem", "36px")]
    [TestCase("calc(50% + 2em)", "calc(50% + 40px)")]
    [TestCase("auto", "auto")]
    [TestCase("from-font", "from-font")]
    [TestCase("hairline", "hairline")]
    [TestCase("thin", "thin")]
    [TestCase("medium", "medium")]
    [TestCase("thick", "thick")]
    public void ThicknessRetainsPercentagesKeywordsAndSignedValuesButComputesActualRelativeMetrics(string declared, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var query = Query(document, [(root, CssDeclarationBlock.Parse("font-size:12px;color:red")),
            (child, CssDeclarationBlock.Parse("font-size:20px;color:blue;text-decoration-thickness:" + declared))], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "text-decoration-thickness", ref matching).Text.Should().Be(expected);
    }

    [Test]
    public void ResolvedShorthandUsesActualColorWithoutDestroyingInheritedCurrentColor()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        var undecorated = document.CreateElement("span");
        document.AppendChild(root);
        root.AppendChild(child);
        child.AppendChild(undecorated);
        var parentBlock = CssDeclarationBlock.Parse("color:red;text-decoration:underline currentcolor");
        var childBlock = CssDeclarationBlock.Parse("color:blue;text-decoration:inherit");
        var work = new CssValueWork(default);
        var inline = new (Element, CssDeclarationBlock)[]
            { (root, parentBlock), (child, childBlock), (undecorated, CssDeclarationBlock.Parse("color:green")) };
        var query = Query(document, inline, work);
        var matching = new SelectorMatchWork(document, default);
        parentBlock.GetPropertyValue("text-decoration-color").Should().Be("currentcolor");
        query.GetProperty(root, "text-decoration", ref matching).Text.Should().Be("underline auto solid rgb(255, 0, 0)");
        var color = query.GetProperty(child, "text-decoration-color", ref matching);
        color.Text.Should().Be("rgb(0, 0, 255)");
        color.Value!.Color.Kind.Should().Be(CssColorKind.CurrentColor);
        query.GetProperty(child, "text-decoration", ref matching).Text.Should().Be("underline auto solid rgb(0, 0, 255)");
        query.GetProperty(child, "text-decoration-color", ref matching).Should().BeSameAs(color);
        query.GetProperty(undecorated, "text-decoration-line", ref matching).Text.Should().Be("none");
        query.GetProperty(undecorated, "text-decoration-color", ref matching).Text.Should().Be("rgb(0, 128, 0)");
        childBlock.SetProperty("color", "green");
        Assert.Throws<InvalidOperationException>(() => query.GetProperty(child, "text-decoration", ref matching));
        query = Query(document, inline, work);
        query.GetProperty(child, "text-decoration", ref matching).Text.Should().Be("underline auto solid rgb(0, 128, 0)");
    }

    [TestCase("underline 2px wavy red", "underline 2px wavy rgb(255, 0, 0)")]
    [TestCase("underline underline", "none auto solid rgb(0, 0, 255)")]
    [TestCase("var(--missing)", "none auto solid rgb(0, 0, 255)")]
    public void DeferredShorthandSubstitutesAllFourGroupsOrResetsAllOnInvalidValues(string replacement, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var block = CssDeclarationBlock.Parse("color:blue;--dec:" + replacement + ";text-decoration:var(--dec)");
        var work = new CssValueWork(default);
        var query = Query(document, [(root, block)], work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "text-decoration", ref matching).Text.Should().Be(expected);
    }

    private static NativeCssQuery Query(Document document,
        IReadOnlyList<(Element Element, CssDeclarationBlock Block)> inline, CssValueWork work) =>
        new(document, [], inline, new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
}

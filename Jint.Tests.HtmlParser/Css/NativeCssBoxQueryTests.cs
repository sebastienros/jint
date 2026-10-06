#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssBoxQueryTests
{
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
        var source = CssDeclarationBlock.Parse("align-content:space-between");
        var matching = new SelectorMatchWork(document, default);
        Query(document, [(parent, source)]).GetProperty(child, "align-content", ref matching).Text.Should().Be("normal");
        Query(document, [(parent, source), (child, CssDeclarationBlock.Parse("align-content:inherit"))])
            .GetProperty(child, "align-content", ref matching).Text.Should().Be("space-between");
    }

    private static NativeCssQuery Query(Document document, IReadOnlyList<(Element, CssDeclarationBlock)> inline)
    {
        var work = new CssValueWork(default);
        return new(document, [], inline, new CssMediaEnvironment(), new(document, null, null, null),
            work);
    }
}

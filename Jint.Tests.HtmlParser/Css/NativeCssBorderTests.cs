using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssBorderTests
{
    [TestCase("border:thin solid red", "border", "1px solid rgb(255, 0, 0)")]
    [TestCase("border:thick solid currentcolor;color:blue", "border-color", "rgb(0, 0, 255)")]
    [TestCase("border-width:thick", "border-width", "0px")]
    [TestCase("border:thick hidden", "border-width", "0px")]
    [TestCase("border-style:solid;border-width:0 2em 3rem", "border-width", "0px 40px 30px")]
    [TestCase("border-style:solid;border-width:calc(-1em)", "border-width", "0px")]
    [TestCase("border-style:solid;border-width:.1px 1.9px", "border-width", "1px")]
    [TestCase("border-radius:1em 2rem / 10% 0", "border-radius", "20px / 10% 0px")]
    [TestCase("border-radius:calc(-2em) 5%", "border-radius", "0px 5%")]
    [TestCase("outline:auto", "outline", "3px auto auto")]
    [TestCase("outline:solid;color:red", "outline", "3px solid rgb(255, 0, 0)")]
    [TestCase("outline-width:5px", "outline-width", "0px")]
    [TestCase("outline-offset:-2em", "outline-offset", "-40px")]
    [TestCase("--b:1em solid blue;border:var(--b)", "border", "20px solid rgb(0, 0, 255)")]
    [TestCase("border:solid red;--b:10%;border-width:var(--b)", "border-width", "3px")]
    [TestCase("border:solid red;border:var(--missing)", "border-width", "0px")]
    [TestCase("border-radius:5px;--r:-10%;border-radius:var(--r)", "border-radius", "0px")]
    [TestCase("border-left:1px solid red;border-inline-start:2px dashed blue", "border-left", "2px dashed rgb(0, 0, 255)")]
    [TestCase("border-inline-start:2px dashed blue;border-left:1px solid red", "border-left", "1px solid rgb(255, 0, 0)")]
    [TestCase("border-left:1px solid red!important;border-inline-start:2px dashed blue", "border-left", "1px solid rgb(255, 0, 0)")]
    [TestCase("direction:rtl;border-inline-start:solid 2em", "border-right", "40px solid rgb(0, 0, 0)")]
    [TestCase("writing-mode:vertical-rl;border-block-start:solid 2em", "border-right", "40px solid rgb(0, 0, 0)")]
    [TestCase("writing-mode:vertical-lr;direction:rtl;border-inline-start:solid 2em", "border-bottom", "40px solid rgb(0, 0, 0)")]
    [TestCase("writing-mode:sideways-lr;border-inline-start:solid 2em", "border-bottom", "40px solid rgb(0, 0, 0)")]
    [TestCase("writing-mode:vertical-rl;border-start-start-radius:1em 20%", "border-top-right-radius", "20px 20%")]
    [TestCase("direction:rtl;border-start-end-radius:10%", "border-top-left-radius", "10%")]
    [TestCase("--b:2em solid red;border-inline-start:var(--b)", "border-left", "40px solid rgb(255, 0, 0)")]
    public void SpecifiedValuesComputeWithRealDependencies(string source, string name, string expected)
    {
        var (document, root, child) = Tree();
        var query = Query(document, [(root, CssDeclarationBlock.Parse("font-size:10px")),
            (child, CssDeclarationBlock.Parse("font-size:20px;color:black;" + source))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, name, ref matching).Text.Should().Be(expected);
        query.GetProperty(child, name, ref matching).Text.Should().Be(expected);
    }

    [Test]
    public void WidthInheritanceUsesComputedLengthNotTheParentsZeroUsedWidth()
    {
        var (document, root, child) = Tree();
        var query = Query(document, [(root, CssDeclarationBlock.Parse("border-width:2em;font-size:10px")),
            (child, CssDeclarationBlock.Parse("border-width:inherit;border-style:solid;font-size:20px"))]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "border-top-width", ref matching).Text.Should().Be("0px");
        query.GetProperty(child, "border-top-width", ref matching).Text.Should().Be("20px");
    }

    [TestCase(1, ".1px 1.9px", "1px")]
    [TestCase(2, ".1px 1.9px", "0.5px 1.5px")]
    [TestCase(1, ".0000001px", "1px")]
    public void WidthsSnapToDevicePixels(double resolution, string input, string expected)
    {
        var (document, _, child) = Tree();
        var query = Query(document, [(child, CssDeclarationBlock.Parse("border-style:solid;border-width:" + input))], resolution);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "border-width", ref matching).Text.Should().Be(expected);
    }

    [TestCase("horizontal-tb", "ltr", "1px 4px 2px 3px", "1px 2px 4px 3px")]
    [TestCase("horizontal-tb", "rtl", "1px 3px 2px 4px", "2px 1px 3px 4px")]
    [TestCase("vertical-rl", "ltr", "3px 1px 4px 2px", "3px 1px 2px 4px")]
    [TestCase("vertical-rl", "rtl", "4px 1px 3px 2px", "4px 2px 1px 3px")]
    [TestCase("vertical-lr", "ltr", "3px 2px 4px 1px", "1px 3px 4px 2px")]
    [TestCase("vertical-lr", "rtl", "4px 2px 3px 1px", "2px 4px 3px 1px")]
    [TestCase("sideways-rl", "ltr", "3px 1px 4px 2px", "3px 1px 2px 4px")]
    [TestCase("sideways-rl", "rtl", "4px 1px 3px 2px", "4px 2px 1px 3px")]
    [TestCase("sideways-lr", "ltr", "4px 2px 3px 1px", "2px 4px 3px 1px")]
    [TestCase("sideways-lr", "rtl", "3px 2px 4px 1px", "1px 3px 4px 2px")]
    public void AllLogicalSidesAndCornersMapInEveryWritingDirection(
        string writingMode, string direction, string widths, string radii)
    {
        var (document, _, child) = Tree();
        var block = CssDeclarationBlock.Parse("writing-mode:" + writingMode + ";direction:" + direction +
            ";border-block-start:1px solid;border-block-end:2px solid;" +
            "border-inline-start:3px solid;border-inline-end:4px solid;" +
            "border-start-start-radius:1px;border-start-end-radius:2px;" +
            "border-end-start-radius:3px;border-end-end-radius:4px");
        var query = Query(document, [(child, block)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "border-width", ref matching).Text.Should().Be(widths);
        query.GetProperty(child, "border-radius", ref matching).Text.Should().Be(radii);
        query.GetProperty(child, "border-block-width", ref matching).Text.Should().Be("1px 2px");
        query.GetProperty(child, "border-inline-width", ref matching).Text.Should().Be("3px 4px");
        query.GetProperty(child, "border-start-start-radius", ref matching).Text.Should().Be("1px");
    }

    [Test]
    public void MutationsAndCancellationInvalidateWarmMappedValues()
    {
        var (document, _, child) = Tree();
        var block = CssDeclarationBlock.Parse("border-inline-start:solid 2em;font-size:20px");
        var query = Query(document, [(child, block)]);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "border-left-width", ref matching).Text.Should().Be("40px");
        block.SetProperty("direction", "rtl");
        Action read = () => query.GetProperty(child, "border-left-width", ref matching);
        read.Should().Throw<InvalidOperationException>().WithMessage(NativeCssQuery.Invalidated);
        query = Query(document, [(child, block)]);
        query.GetProperty(child, "border-left-width", ref matching).Text.Should().Be("0px");
        query.GetProperty(child, "border-right-width", ref matching).Text.Should().Be("40px");
        using var cancellation = new CancellationTokenSource();
        query = Query(document, [(child, block)], token: cancellation.Token);
        cancellation.Cancel();
        read.Should().Throw<OperationCanceledException>();
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
        double resolution = 1, CancellationToken token = default)
    {
        var work = new CssValueWork(token);
        return new(document, [], inline, new CssMediaEnvironment { Width = 800, Height = 600, Resolution = resolution },
            new(document, null, null, null), CssEnvironmentSnapshot.Create([], work), work);
    }
}

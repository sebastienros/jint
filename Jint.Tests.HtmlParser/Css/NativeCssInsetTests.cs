#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssInsetTests
{
    [TestCase("", "auto")]
    [TestCase("inherit", "-10px")]
    [TestCase("initial", "auto")]
    [TestCase("unset", "auto")]
    [TestCase("-9999em", "-199980px")]
    [TestCase("-25%", "-25%")]
    [TestCase("25%", "25%")]
    [TestCase("0", "0px")]
    [TestCase("calc(25% - 2em)", "calc(25% - 40px)")]
    [TestCase("calc(-2px)", "-2px")]
    [TestCase("-10vw", "-80px")]
    [TestCase("var(--inset)", "-40px")]
    [TestCase("var(--invalid)", "auto")]
    [TestCase("env(inset-offset)", "-5%")]
    public void StaticPhysicalInsetsKeepTheirComputedValuesWithoutAContainingBlock(string declaration, string expected)
    {
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            var document = Document.CreateHtml();
            var root = document.CreateElement("html");
            var child = document.CreateElement("span");
            document.AppendChild(root);
            root.AppendChild(child);
            var work = new CssValueWork(default);
            var environment = CssEnvironmentSnapshot.Create(
                [CssEnvironmentBinding.Create("inset-offset", [], CssReferenceInput.Parse("-5%", null, default), work)], work);
            var query = new NativeCssQuery(document, [],
                [(root, CssDeclarationBlock.Parse(side + ":-10px")),
                 (child, CssDeclarationBlock.Parse("position:static;font-size:20px;--inset:-2em;--invalid:stretch;"
                     + (declaration.Length == 0 ? "" : side + ":" + declaration)))],
                new CssMediaEnvironment { Width = 800 }, new(document, null, null, null), environment, work);
            var matching = new SelectorMatchWork(document, default);
            var value = query.GetProperty(child, side, ref matching);
            value.Text.Should().Be(expected);
            query.GetProperty(child, side, ref matching).Should().BeSameAs(value);
        }
    }

    [Test]
    public void PercentageInsetsDoNotWarmUnrelatedFontOrGeometryDependencies()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("span");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = new NativeCssQuery(document, [],
            [(root, CssDeclarationBlock.Parse("font-size:2ch;width:2cqw")),
             (child, CssDeclarationBlock.Parse("font-size:3ch;width:2cqw;left:25%"))],
            new CssMediaEnvironment(), new(document, null, null, null), CssEnvironmentSnapshot.Create([], work), work,
            diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(child, "left", ref matching).Text.Should().Be("25%");
        diagnostics.Queries.Single().ComputedPublications.Keys.Should().Equal("left");
    }
}

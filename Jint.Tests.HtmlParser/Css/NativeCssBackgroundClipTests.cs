#nullable enable

using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssBackgroundClipTests
{
    [TestCase("", "border-box")]
    [TestCase("background-clip:inherit", "content-box, text, content-box")]
    [TestCase("background-clip:initial", "border-box")]
    [TestCase("background-clip:unset", "border-box")]
    [TestCase("background-clip:var(--clip)", "border-area text, padding-box")]
    [TestCase("background-clip:var(--invalid)", "border-box")]
    [TestCase("background-clip:env(clip-region)", "text, text")]
    public void ComputedListsRetainTheirCountAndHaveNoFontOrGeometryDemand(string declaration, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var environment = CssEnvironmentSnapshot.Create(
            [CssEnvironmentBinding.Create("clip-region", [], CssReferenceInput.Parse("text, text", null, default), work)], work);
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = new NativeCssQuery(document, [],
            [(root, CssDeclarationBlock.Parse("font-size:2ch;background-clip:content-box,text,content-box")),
             (child, CssDeclarationBlock.Parse("font-size:3ch;width:2cqw;--clip:text border-area,padding-box;--invalid:text text;" + declaration))],
            new CssMediaEnvironment(), new(document, null, null, null), environment, work, diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, default);
        var value = query.GetProperty(child, "background-clip", ref matching);
        value.Text.Should().Be(expected);
        value.Value!.Kind.Should().Be(CssPropertyValueKind.KeywordList);
        query.GetProperty(child, "background-clip", ref matching).Should().BeSameAs(value);
        var record = diagnostics.Queries.Single();
        record.ComputedPublications.Keys.Should().Equal("background-clip");
        record.ComputedPublications.Should().NotContainKey("font-size");
        record.ComputedPublications.Should().NotContainKey("width");
    }
}

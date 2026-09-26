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

public sealed class NativeCssCursorTests
{
    [TestCase("", "pointer")]
    [TestCase("cursor:inherit", "pointer")]
    [TestCase("cursor:unset", "pointer")]
    [TestCase("cursor:initial", "auto")]
    [TestCase("cursor:auto", "auto")]
    [TestCase("cursor:var(--cursor)", "grab")]
    [TestCase("cursor:var(--invalid)", "pointer")]
    [TestCase("cursor:env(cursor-kind)", "zoom-in")]
    public void ComputedKeywordsUseInheritanceAndReferencesWithoutMetricDemand(string declaration, string expected)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        var child = document.CreateElement("div");
        document.AppendChild(root);
        root.AppendChild(child);
        var work = new CssValueWork(default);
        var environment = CssEnvironmentSnapshot.Create(
            [CssEnvironmentBinding.Create("cursor-kind", [], CssReferenceInput.Parse("zoom-in", null, default), work)], work);
        var diagnostics = new NativeCssQueryDiagnostics();
        var query = new NativeCssQuery(document, [],
            [(root, CssDeclarationBlock.Parse("font-size:2ch;cursor:pointer")),
             (child, CssDeclarationBlock.Parse("font-size:3ch;width:2cqw;--cursor:grab;--invalid:pointer text;" + declaration))],
            new CssMediaEnvironment(), new(document, null, null, null), environment, work, diagnostics: diagnostics);
        var matching = new SelectorMatchWork(document, default);
        var value = query.GetProperty(child, "cursor", ref matching);
        value.Text.Should().Be(expected);
        value.Value!.Kind.Should().Be(CssPropertyValueKind.Keyword);
        query.GetProperty(child, "cursor", ref matching).Should().BeSameAs(value);
        diagnostics.Queries.Single().ComputedPublications.Keys.Should().Equal("cursor");
    }

    [Test]
    public void AnUnstyledRootComputesToAuto()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var work = new CssValueWork(default);
        var query = new NativeCssQuery(document, [], [], new CssMediaEnvironment(), new(document, null, null, null),
            CssEnvironmentSnapshot.Create([], work), work);
        var matching = new SelectorMatchWork(document, default);
        query.GetProperty(root, "cursor", ref matching).Text.Should().Be("auto");
    }

    [Test]
    public void SubstitutedImagesKeepThePendingBoundaryInsteadOfPublishingTheFallback()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        var work = new CssValueWork(default);
        var query = new NativeCssQuery(document, [],
            [(root, CssDeclarationBlock.Parse("--image:url(cursor.cur) 1 2, pointer;cursor:var(--image)"))],
            new CssMediaEnvironment(), new(document, null, null, null), CssEnvironmentSnapshot.Create([], work), work);
        var matching = new SelectorMatchWork(document, default);
        Assert.Throws<CssIncompleteGrammarException>(() => query.GetProperty(root, "cursor", ref matching))!
            .Blocker.Should().Be("V6:cursor-images");
    }
}

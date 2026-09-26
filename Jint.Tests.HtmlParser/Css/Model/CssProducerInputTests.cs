using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

// The real C1 -> C2/C5 seams required by the next sheet producer. These tests intentionally
// use immutable inputs directly; syntax editors do not become a second mutable CSSOM shadow.
[TestFixture]
public sealed class CssProducerInputTests
{
    [Test]
    public void SelectorAndDeclarationsConsumeTheSameSheetParseWithOriginalOffsets()
    {
        const string source = "/* prefix */ .target, #chosen { opacity:.5; overflow:var(--X); --X:hidden; }";
        var parser = new CssSyntaxParser(source, null, default);
        var syntax = parser.ParseStyleSheet()[0];
        var selector = new SelectorCompiler.Worker(source, new SelectorParseContext(), default).Compile(syntax.Prelude);
        var body = parser.ParseBlockContents(syntax.Block!.Value);
        var declarations = CssDeclarationBlock.FromDeclarations(source, body[0].Declarations,
            CssDeclarationContext.Style, 0, new CssValueWork(default));
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var elements = new[] { document.CreateElement("div"), document.CreateElement("div"), document.CreateElement("div") };
        foreach (var element in elements) root.AppendChild(element);
        elements[0].SetAttribute("class", "target");
        elements[1].SetAttribute("id", "chosen");
        SelectorMatcher.TryMatch(selector, elements[0], out var classSpecificity).Should().BeTrue();
        SelectorMatcher.TryMatch(selector, elements[1], out var idSpecificity).Should().BeTrue();
        classSpecificity.Should().Be(new SelectorSpecificity(0, 1, 0));
        idSpecificity.Should().Be(new SelectorSpecificity(1, 0, 0));
        SelectorMatcher.Matches(selector, elements[2]).Should().BeFalse();
        var value = declarations.GetDeclaration(1).PendingShorthand!.Value;
        value.References.Input.Components.Should().BeSameAs(body[0].Declarations[1].Value);
        value.References[0].Span.Start.Should().Be(source.IndexOf("var", StringComparison.Ordinal));
        declarations.GetPropertyValue("overflow").Should().Be("var(--X)");
    }

    [Test]
    public void KnownPendingDeclarationSurfacesFromAValidatedSelectorBody()
    {
        const string source = ".target { display:block; border-color:red; }";
        var parser = new CssSyntaxParser(source, null, default);
        var syntax = parser.ParseStyleSheet()[0];
        var selector = new SelectorCompiler.Worker(source, new SelectorParseContext(), default).Compile(syntax.Prelude);
        selector.Branches.Count.Should().Be(1);
        var body = parser.ParseBlockContents(syntax.Block!.Value);
        var failure = Assert.Throws<CssIncompleteGrammarException>(() => CssDeclarationBlock.FromDeclarations(source,
            body[0].Declarations, CssDeclarationContext.Style, 0, new CssValueWork(default)))!;
        failure.PropertyName.Should().Be("border-color");
        failure.Blocker.Should().Be("V1:border-color");
        failure.Span.Start.Should().Be(source.IndexOf("border-color", StringComparison.Ordinal));
    }
}

#nullable enable
using Jint.Browser.Accessibility;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Accessibility;

public sealed class ContentDomTests
{
    [Test]
    public void QueriesRetainNativeIdentityAndTemplateBoundaries()
    {
        var document = PageFixture.Parse("<main><p id=t>a<b>b</b><!--gap-->c</p><!--gap--><p id=n>next</p><template><p id=inert>inert</p></template></main>");
        var target = ContentDom.ElementById(document, "t")!;

        ContentDom.ElementById(document, "t").Should().BeSameAs(target);
        ContentDom.NextElementSibling(target).Should().BeSameAs(ContentDom.ElementById(document, "n"));
        ContentDom.TextContent(target).Should().Be("abc");
        ContentDom.ElementById(document, "inert").Should().BeNull();
        ContentDom.Children(ContentDom.First(document, "main")!).Should().HaveCount(3);
    }

    [Test]
    public void ClassTokensUseOnlyAsciiWhitespace()
    {
        var target = PageFixture.Target("<p id=t class='a\tb\nc\rd\fe f'></p>");
        ContentDom.ClassNames(target).Should().Equal("a", "b", "c", "d", "e f");
    }

    [Test]
    public void TextContentReadsNativeCdataWithoutComments()
    {
        var document = MarkupParser.ParseXml("<root>A<![CDATA[B]]><child>C</child><!--ignored--></root>");
        ContentDom.TextContent(document.DocumentElement!).Should().Be("ABC");
    }

    [Test]
    public void InertParsingNeverRunsScripts()
    {
        var document = PageFixture.Parse("<script>document.write('<p id=injected>script</p>')</script><p id=t>parsed</p>");
        ContentDom.ElementById(document, "injected").Should().BeNull();
        ContentDom.TextContent(ContentDom.ElementById(document, "t")!).Should().Be("parsed");
    }

    [TestCase("")]
    [TestCase("urn:foreign")]
    public void ForeignInputsDoNotUseHtmlLabelOrFocusRules(string namespaceUri)
    {
        var document = MarkupParser.ParseXml($"<root xmlns='{namespaceUri}'><input id='t' type='submit' value='Wrong label' /></root>");
        var input = ContentDom.ElementById(document, "t")!;
        ContentDom.HtmlName(input).Should().BeNull();

        var names = new AccessibleName(new ElementVisibility(useComputedStyle: false));
        names.Compute(input, "button").Should().BeEmpty();
        var node = AccessibilityTree.Build(input, AccessibilityOptions.Full with { UseComputedStyle = false })!;
        node.Properties.Should().NotContain(property => property.Name == AxPropertyName.Focusable);
    }

    [TestCase("<progress id=t value=12 max=10>", 0, 10, "10")]
    [TestCase("<progress id=t value=-1 max=0>", 0, 1, "0")]
    [TestCase("<meter id=t min=2 max=1 value=0>", 2, 2, "2")]
    [TestCase("<meter id=t min=-2 max=4 value=3>", -2, 4, "3")]
    public void NumericControlViewsUseNativeAttributes(string html, double minimum, double maximum, string value)
    {
        var target = PageFixture.Target(html);
        var role = target.LocalName == "progress" ? "progressbar" : "meter";
        ControlValue.Range(target, role).Should().Be((minimum, maximum));
        ControlValue.For(target, role).Should().Be(value);
    }
}

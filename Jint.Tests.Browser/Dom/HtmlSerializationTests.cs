using AngleSharp.Xml.Parser;
using Jint.Browser.Dom;

namespace Jint.Tests.Browser.Dom;

using Browser = global::Jint.Browser.Browser;

public sealed class HtmlSerializationTests
{
    [TestCase("axx>", "axx&gt;")]
    [TestCase("some<>", "some&lt;&gt;")]
    [TestCase("&lt;<>\"\u00a0&", "&amp;lt;&lt;&gt;&quot;&nbsp;&amp;")]
    public void AttributeValuesEscapeAnglesWithoutDoubleEscaping(string value, string escaped)
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Engine.SetValue("value", value);
        fixture.Execute("var el = document.createElement('el'); el.setAttribute('attr', value); document.querySelector('main').append(el);");
        fixture.Text("el.outerHTML").Should().Be("<el attr=\"" + escaped + "\"></el>");
        fixture.Text("document.querySelector('main').innerHTML").Should().Be("<el attr=\"" + escaped + "\"></el>");
        fixture.Text("el.getAttribute('attr')").Should().Be(value);
    }

    [TestCase("axx>", "axx&gt;")]
    [TestCase("some<>", "some&lt;&gt;")]
    public void XmlDocumentElementsUseTheHtmlMarkupGetter(string value, string escaped)
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Engine.SetValue("value", value);
        fixture.Execute("var el = new Document().createElement('el'); el.setAttribute('attr', value);");
        fixture.Text("el.outerHTML").Should().Be("<el attr=\"" + escaped + "\"></el>");
    }

    [Test]
    public void XmlMarkupGettersPreserveNativeNamesEmptyTagsAndNonbreakingSpaces()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        using var xml = new XmlParser().ParseDocument(
            "<root xmlns='urn:root' xmlns:p='urn:p'><p:leaf/></root>");
        var root = xml.DocumentElement!;
        root.FirstElementChild!.SetAttribute("data", "\u00a0");
        var nativeInner = root.InnerHtml;
        var nativeOuter = root.OuterHtml;
        fixture.Engine.SetValue("xml", DomBindings.Wrap(fixture.Engine, xml));
        fixture.Text("xml.documentElement.innerHTML").Should().Be(nativeInner);
        fixture.Text("xml.documentElement.outerHTML").Should().Be(nativeOuter);
        nativeInner.Should().Contain("<p:leaf data=\"&nbsp;\">");
        nativeOuter.Should().Contain("xmlns=\"urn:root\" xmlns:p=\"urn:p\"");
    }

    [Test]
    public void AttributeNamesAndNamespaceSpellingRemainNative()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var el = document.createElement('el');
            el.setAttribute('a"<', '<>');
            el.setAttributeNS('http://www.w3.org/XML/1998/namespace', 'xml:lang', '<>');
            """);
        fixture.Text("el.outerHTML").Should().Be("""<el a"<="&lt;&gt;" xml:lang="&lt;&gt;"></el>""");
    }

    [Test]
    public void TextCommentsAndRawTextKeepNativeEscaping()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var el = document.createElement('el');
            el.append(document.createTextNode('<>&'), document.createComment('<>'));
            var script = document.createElement('script');
            script.textContent = 'a < b && c > d';
            el.append(script);
            """);
        fixture.Text("el.outerHTML").Should().Be("<el>&lt;&gt;&amp;<!--<>--><script>a < b && c > d</script></el>");
    }

    [Test]
    public void TemplatesAndShadowRootsUseTheSameSerialization()
    {
        using var fixture = DomTestFixture.Create("<main></main>");
        fixture.Execute("""
            var template = document.createElement('template');
            template.innerHTML = '<el attr="some<>"></el>';
            var shadow = document.querySelector('main').attachShadow({mode:'open'});
            shadow.append(template.content.cloneNode(true));
            """);
        fixture.Text("template.innerHTML").Should().Be("<el attr=\"some&lt;&gt;\"></el>");
        fixture.Text("shadow.innerHTML").Should().Be("<el attr=\"some&lt;&gt;\"></el>");
    }

    [Test]
    public async Task PageContentUsesTheSameAttributeEscaping()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync("<!doctype html><el attr='some<>'></el>");
        (await page.ContentAsync()).Should().Contain("<el attr=\"some&lt;&gt;\"></el>");
        (await page.EvaluateAsync<string>("new XMLSerializer().serializeToString(document.querySelector('el'))"))
            .Should().Be("<el attr=\"some&lt;>\" />");
    }
}

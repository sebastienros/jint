using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class DocumentReadTests
{
    [Test]
    public void IndexedQueriesKeepEscapesQuirksScopesAndWrapperIdentity()
    {
        using var fixture = DomTestFixture.Create("<!doctype html><div id=root><p id='a:b'></p><p id='a:b'></p></div>");
        fixture.Engine.Evaluate("""
            const root = document.getElementById('root');
            const first = document.getElementById('a:b');
            const second = root.lastChild;
            if (document.querySelector('#a\\:b') !== first) throw Error('escape');
            root.insertBefore(second, first);
            if (document.getElementById('a:b') !== second || document.querySelector('#a\\:b') !== second)
                throw Error('order');
            second.id = 'other';
            if (document.querySelector('#a\\:b') !== first) throw Error('id');
            root.innerHTML = '<span id="a:b"></span>';
            if (document.querySelector('#a\\:b') !== root.firstChild) throw Error('replace');
            const fragment = document.createDocumentFragment();
            fragment.append(root.firstChild);
            if (document.getElementById('a:b') !== null || fragment.getElementById('a:b') !== fragment.firstChild ||
                fragment.querySelector('#a\\:b') !== fragment.firstChild) throw Error('fragment');
            true
            """).Should().Be(true);
        using var quirks = DomTestFixture.Create("<div id=MiXeD></div>");
        quirks.Engine.Evaluate("document.querySelector('#mixed') === document.getElementById('MiXeD') && document.getElementById('mixed') === null")
            .Should().Be(true);
    }

    [Test]
    public void TitleUsesFirstNativeHtmlTitleAndCollapsesOnlyAsciiWhitespace()
    {
        using var fixture = DomTestFixture.Create("<title> \t first\n\u00A0 title \f </title><title>second</title>");
        DomDocumentReads.Title(DomRealm.Of(fixture.Engine), fixture.Document).Should().Be("first \u00A0 title");
    }

    [Test]
    public void IdLookupIgnoresNamespacedAttributesAndDoesNotEnterTemplateContents()
    {
        using var fixture = DomTestFixture.Create("<div></div><template><span id=target></span></template><p id=target></p>");
        var root = fixture.Document.DocumentElement!.LastChild!;
        var div = (Element) root.FirstChild!;
        div.SetAttributeNS("urn:test", "test:id", "target");
        DomDocumentReads.ById(DomRealm.Of(fixture.Engine), fixture.Document, "target")!.LocalName.Should().Be("p");
        DomDocumentReads.ById(DomRealm.Of(fixture.Engine), fixture.Document, "").Should().BeNull();
    }
}

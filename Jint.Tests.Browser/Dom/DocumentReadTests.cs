using Jint.Browser.Dom;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class DocumentReadTests
{
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

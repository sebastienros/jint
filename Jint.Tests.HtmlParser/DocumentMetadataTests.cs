#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class DocumentMetadataTests
{
    [Test]
    public void FactoriesAndClonesPreserveCanonicalMetadata()
    {
        var html = Document.CreateHtml();
        html.Kind.Should().Be(DocumentKind.Html);
        html.ContentType.Should().Be("text/html");
        html.CharacterSet.Should().Be("UTF-8");

        var xml = Document.CreateXml();
        xml.ContentType.Should().Be("application/xml");
        var svg = Document.CreateXml("image/svg+xml");
        svg.ContentType.Should().Be("image/svg+xml");
        svg.CharacterSet.Should().Be("UTF-8");

        var clone = (Document)svg.CloneNode();
        clone.Kind.Should().Be(DocumentKind.Xml);
        clone.ContentType.Should().Be(svg.ContentType);
        clone.CharacterSet.Should().Be(svg.CharacterSet);
    }

    [Test]
    public void XmlMimeEssenceMustBeCanonical()
    {
        foreach (var valid in new[] { "text/xml", "application/xml", "application/xhtml+xml", "image/svg+xml", "custom/a+xml" })
        {
            Document.CreateXml(valid).ContentType.Should().Be(valid);
        }

        Assert.Throws<ArgumentNullException>(() => Document.CreateXml(null!));
        foreach (var invalid in new[] { "", "text/html", "Text/Xml", "image/svg+xml; charset=utf-8", " image/svg+xml", "image//svg+xml", "image/+xml", "image/svg+xml/other", "imäge/svg+xml" })
        {
            Assert.Throws<ArgumentException>(() => Document.CreateXml(invalid));
        }
    }

    [Test]
    public void XhtmlCreationKeepsCaseAndHtmlNamespace()
    {
        var xhtml = Document.CreateXml("application/xhtml+xml");
        var element = xhtml.CreateElement("FOO");
        element.LocalName.Should().Be("FOO");
        element.NamespaceUri.Should().Be(Namespaces.Html);
        xhtml.CreateAttribute("BAR").LocalName.Should().Be("BAR");

        var html = Document.CreateHtml().CreateElement("FOO");
        html.LocalName.Should().Be("foo");
        html.NamespaceUri.Should().Be(Namespaces.Html);
        var xml = Document.CreateXml().CreateElement("FOO");
        xml.LocalName.Should().Be("FOO");
        xml.NamespaceUri.Should().BeNull();
    }

    [Test]
    public void ImportKeepsDestinationMetadata()
    {
        var source = Document.CreateXml("image/svg+xml");
        var destination = Document.CreateXml("application/xhtml+xml");
        var imported = destination.ImportNode(source.CreateElement("Example"));
        imported.OwnerDocument.Should().BeSameAs(destination);
        destination.ContentType.Should().Be("application/xhtml+xml");
        source.ContentType.Should().Be("image/svg+xml");
    }
}

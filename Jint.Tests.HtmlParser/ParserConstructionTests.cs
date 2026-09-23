#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ParserConstructionTests
{
    [Test]
    public void ParsedNamesAndAttributesKeepResolvedComponentsAndSourceOrder()
    {
        var document = Document.CreateXml();
        var element = document.CreateParsedElement(null, "xmlns", null);
        var attributes = new[]
        {
            new ParserAttribute(null, "MiXeD", null, "one"),
            new ParserAttribute("urn:example", "Name", "p", "two")
        };
        element.InitializeParsedAttributes(attributes, CancellationToken.None);
        attributes[0] = new ParserAttribute(null, "changed", null, "three");
        document.AppendParsedChild(element);

        element.LocalName.Should().Be("xmlns");
        element.NamespaceUri.Should().BeNull();
        element.Attributes.Select(attribute => attribute.Name).Should().Equal("MiXeD", "p:Name");
        element.Attributes.Select(attribute => attribute.Value).Should().Equal("one", "two");
        element.Attributes.All(attribute => ReferenceEquals(attribute.OwnerElement, element) &&
                                           ReferenceEquals(attribute.OwnerDocument, document)).Should().BeTrue();
        Assert.That(Assert.Throws<DomException>(() => document.CreateElementNS(null, "xmlns"))!.Name,
            Is.EqualTo("NamespaceError"));
    }

    [Test]
    public void FreshDeepConstructionAndOwnerGuards()
    {
        var document = Document.CreateXml();
        var other = Document.CreateXml();
        var root = document.CreateParsedElement(null, "root", null);
        document.AppendParsedChild(root);
        var current = root;
        for (var i = 0; i < 20_000; i++)
        {
            var child = document.CreateParsedElement(null, "next", null);
            current.AppendParsedChild(child);
            current = child;
        }

        current.OwnerDocument.Should().BeSameAs(document);
        root.ChildCount.Should().Be(1);
        Assert.Throws<InvalidOperationException>(() => root.AppendParsedChild(other.CreateElement("foreign")));
        Assert.Throws<InvalidOperationException>(() => root.AppendParsedChild(current));
        Assert.Throws<InvalidOperationException>(() => root.InitializeParsedAttributes([], CancellationToken.None));
    }

    [Test]
    public void ParsedCDataRetainsActualHtmlDocumentOwner()
    {
        var html = Document.CreateHtml();
        Assert.That(Assert.Throws<DomException>(() => html.CreateCDataSection("x"))!.Name, Is.EqualTo("NotSupportedError"));
        var fragment = html.CreateDocumentFragment();
        var cdata = html.CreateParsedCDataSection("x");
        fragment.AppendParsedChild(cdata);
        cdata.OwnerDocument.Should().BeSameAs(html);
        cdata.ParentNode.Should().BeSameAs(fragment);
    }

    [Test]
    public void CanceledInitialAttributeBatchDoesNotPublishAttributes()
    {
        var element = Document.CreateXml().CreateParsedElement(null, "root", null);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => element.InitializeParsedAttributes(
            [new ParserAttribute(null, "first", null, "value")], canceled.Token));
        element.AttributeCount.Should().Be(0);
    }

    [Test]
    public void WideInitialAttributeBatchPreservesEveryEntry()
    {
        var element = Document.CreateXml().CreateParsedElement(null, "root", null);
        var attributes = new ParserAttribute[5_000];
        for (var i = 0; i < attributes.Length; i++)
        {
            attributes[i] = new ParserAttribute(null, "a" + i, null, "v" + i);
        }

        element.InitializeParsedAttributes(attributes, CancellationToken.None);
        element.AttributeCount.Should().Be(attributes.Length);
        element.Attributes.First().Name.Should().Be("a0");
        element.Attributes.Last().Value.Should().Be("v4999");
    }
}

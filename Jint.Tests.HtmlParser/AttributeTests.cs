#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class AttributeTests
{
    [Test]
    public void OrderedAttributesKeepIdentityAcrossValueUpdates()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("DIV");
        element.SetAttribute("ID", "first");
        element.SetAttributeNS("urn:test", "a:mode", "one");
        element.SetAttribute("class", "one");
        var id = element.GetAttributeNode("id")!;
        var mode = element.GetAttributeNodeNS("urn:test", "mode")!;

        element.SetAttribute("id", "second");
        element.SetAttributeNS("urn:test", "b:mode", "two");
        element.GetAttributeNode("ID").Should().BeSameAs(id);
        element.GetAttributeNodeNS("urn:test", "mode").Should().BeSameAs(mode);
        element.Attributes.Select(attribute => attribute.Name).Should().Equal("id", "a:mode", "class");
        element.GetAttribute("ID").Should().Be("second");
        element.GetAttributeNS("urn:test", "mode").Should().Be("two");
        element.AttributeCount.Should().Be(3);
    }

    [Test]
    public void QualifiedNameOperationsFindNamespacedAttributes()
    {
        var element = Document.CreateXml().CreateElement("item");
        element.SetAttributeNS(Namespaces.Xml, "xml:lang", "en");
        var attribute = element.GetAttributeNodeNS(Namespaces.Xml, "lang")!;

        element.GetAttribute("xml:lang").Should().Be("en");
        element.GetAttributeNode("xml:lang").Should().BeSameAs(attribute);
        element.SetAttribute("xml:lang", "fr");
        element.GetAttributeNodeNS(Namespaces.Xml, "lang").Should().BeSameAs(attribute);
        attribute.Value.Should().Be("fr");
        element.RemoveAttribute("xml:lang");
        attribute.OwnerElement.Should().BeNull();
        element.AttributeCount.Should().Be(0);
    }

    [Test]
    public void SvgAttributesPreserveCaseInHtmlDocuments()
    {
        var document = Document.CreateHtml();
        var svg = document.CreateElementNS(Namespaces.Svg, "svg");
        svg.SetAttribute("viewBox", "0 0 1 1");
        svg.GetAttribute("viewBox").Should().Be("0 0 1 1");
        svg.GetAttribute("VIEWBOX").Should().BeNull();
        svg.Attributes.Single().Name.Should().Be("viewBox");

        var html = document.CreateElement("div");
        html.SetAttribute("DATA-X", "v");
        html.Attributes.Single().Name.Should().Be("data-x");
    }

    [Test]
    public void AttributesEnumeratorDoesNotExposeMutableStore()
    {
        var element = Document.CreateHtml().CreateElement("div");
        element.SetAttribute("id", "x");
        Assert.That(element.Attributes is ICollection<Attr>, Is.False);
        element.Attributes.Single().OwnerElement.Should().BeSameAs(element);
    }

    [Test]
    public void ReplacingAndRemovingAttributeNodesUpdatesOwnership()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("item");
        var old = document.CreateAttributeNS("urn:test", "a:key");
        var replacement = document.CreateAttributeNS("urn:test", "b:key");
        element.SetAttributeNode(old).Should().BeNull();
        element.SetAttributeNode(replacement).Should().BeSameAs(old);
        old.OwnerElement.Should().BeNull();
        replacement.OwnerElement.Should().BeSameAs(element);
        element.GetAttributeNodeNS("urn:test", "key").Should().BeSameAs(replacement);
        element.RemoveAttributeNode(replacement).Should().BeSameAs(replacement);
        replacement.OwnerElement.Should().BeNull();
        element.AttributeCount.Should().Be(0);
    }

    [Test]
    public void AttachedAttributeCannotBeSharedAndInvalidNamesDoNotMutateStore()
    {
        var document = Document.CreateXml();
        var first = document.CreateElement("first");
        var second = document.CreateElement("second");
        var attribute = document.CreateAttribute("id");
        first.SetAttributeNode(attribute);
        Assert.That(Assert.Throws<DomException>(() => second.SetAttributeNode(attribute))!.Name, Is.EqualTo("InUseAttributeError"));
        Assert.That(Assert.Throws<DomException>(() => first.SetAttributeNS(null, "p:name", "x"))!.Name, Is.EqualTo("NamespaceError"));
        Assert.That(Assert.Throws<DomException>(() => first.SetAttributeNS("urn:wrong", "xml:lang", "x"))!.Name, Is.EqualTo("NamespaceError"));
        first.AttributeCount.Should().Be(1);
        first.GetAttributeNode("id").Should().BeSameAs(attribute);
        second.AttributeCount.Should().Be(0);
    }

    [Test]
    public void HtmlAndXmlCreationHaveDistinctNamespaceAndCaseRules()
    {
        var html = Document.CreateHtml();
        var xml = Document.CreateXml();
        var htmlElement = html.CreateElement("DiV");
        var xmlElement = xml.CreateElement("DiV");
        htmlElement.NamespaceUri.Should().Be(Namespaces.Html);
        htmlElement.LocalName.Should().Be("div");
        xmlElement.NamespaceUri.Should().BeNull();
        xmlElement.LocalName.Should().Be("DiV");
        Assert.That(Assert.Throws<DomException>(() => html.CreateCDataSection("x"))!.Name, Is.EqualTo("NotSupportedError"));
        xml.CreateCDataSection("x").NodeType.Should().Be(NodeType.CDataSection);
        xml.CreateProcessingInstruction("target", "data").NodeType.Should().Be(NodeType.ProcessingInstruction);
    }

    [Test]
    public void FactoriesRejectInvalidNamesAndForbiddenXmlTerminators()
    {
        var document = Document.CreateXml();
        Assert.That(Assert.Throws<DomException>(() => document.CreateElementNS(null, "bad name"))!.Name, Is.EqualTo("InvalidCharacterError"));
        Assert.That(Assert.Throws<DomException>(() => document.CreateAttributeNS(null, "p:id"))!.Name, Is.EqualTo("NamespaceError"));
        Assert.That(Assert.Throws<DomException>(() => document.CreateCDataSection("a]]>b"))!.Name, Is.EqualTo("InvalidCharacterError"));
        document.CreateProcessingInstruction("xml", "x").Target.Should().Be("xml");
        Assert.That(Assert.Throws<DomException>(() => document.CreateProcessingInstruction("target", "a?>b"))!.Name, Is.EqualTo("InvalidCharacterError"));
        document.CreateDocumentType("").Name.Should().BeEmpty();
        Assert.That(Assert.Throws<DomException>(() => document.CreateDocumentType("a>b"))!.Name, Is.EqualTo("InvalidCharacterError"));
        document.CreateElement("x:y").LocalName.Should().Be("x:y");
        document.CreateElement("a@b").LocalName.Should().Be("a@b");
        document.CreateElementNS("urn:test", "p:a@b").LocalName.Should().Be("a@b");
        document.CreateAttribute("1").LocalName.Should().Be("1");
        document.CreateAttributeNS("urn:test", "p:1").LocalName.Should().Be("1");
        Assert.That(Assert.Throws<DomException>(() => document.CreateElementNS(null, ":x"))!.Name, Is.EqualTo("InvalidCharacterError"));
        Assert.That(Assert.Throws<DomException>(() => document.CreateAttributeNS(null, ":x"))!.Name, Is.EqualTo("InvalidCharacterError"));
        Assert.That(Assert.Throws<DomException>(() => document.CreateElement("_a?"))!.Name, Is.EqualTo("InvalidCharacterError"));
    }

    [Test]
    public void PublicDataSettersRejectNull()
    {
        var document = Document.CreateXml();
        var text = document.CreateTextNode("x");
        var comment = document.CreateComment("x");
        var cdata = document.CreateCDataSection("x");
        var pi = document.CreateProcessingInstruction("target", "x");
        var attribute = document.CreateAttribute("id");
        Assert.Throws<ArgumentNullException>(() => text.Data = null!);
        Assert.Throws<ArgumentNullException>(() => comment.Data = null!);
        Assert.Throws<ArgumentNullException>(() => cdata.Data = null!);
        Assert.Throws<ArgumentNullException>(() => pi.Data = null!);
        Assert.Throws<ArgumentNullException>(() => attribute.Value = null!);
    }
}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class CloningTests
{
    [Test]
    public void EveryNativeKindGetsANewDetachedIdentity()
    {
        var document = Document.CreateXml();
        Node[] nodes =
        [
            document.CreateElementNS(Namespaces.Svg, "s:svg"),
            document.CreateTextNode("text"),
            document.CreateCDataSection("cdata"),
            document.CreateProcessingInstruction("target", "data"),
            document.CreateComment("comment"),
            document.CreateDocumentType("root", "public", "system"),
            document.CreateDocumentFragment(),
            document
        ];

        foreach (var source in nodes)
        {
            var copy = source.CloneNode();
            copy.Should().NotBeSameAs(source);
            copy.GetType().Should().Be(source.GetType());
            copy.NodeType.Should().Be(source.NodeType);
            copy.ParentNode.Should().BeNull();
            copy.ChildCount.Should().Be(0);
            copy.OwnerDocument.Should().BeSameAs(source is Document ? null : document);

            if (source is not Document)
            {
                var imported = Document.CreateHtml().ImportNode(source);
                imported.Should().NotBeSameAs(source);
                imported.GetType().Should().Be(source.GetType());
                imported.ParentNode.Should().BeNull();
                imported.ChildCount.Should().Be(0);
                imported.OwnerDocument!.Kind.Should().Be(DocumentKind.Html);
            }
        }

        var doctype = (DocumentType)nodes[5].CloneNode();
        (doctype.Name, doctype.PublicId, doctype.SystemId).Should().Be(("root", "public", "system"));
        ((ProcessingInstruction)nodes[3].CloneNode()).Target.Should().Be("target");
    }

    [Test]
    public void DeepDocumentAndFragmentCopiesKeepOrderLinksAndOwnership()
    {
        var source = Document.CreateXml();
        var doctype = source.CreateDocumentType("root");
        var root = source.CreateElement("root");
        var fragment = source.CreateDocumentFragment();
        var child = source.CreateElement("child");
        child.AppendChild(source.CreateTextNode("leaf"));
        fragment.AppendChild(child);
        fragment.AppendChild(source.CreateComment("tail"));
        var fragmentCopy = (DocumentFragment)fragment.CloneNode(deep: true);
        root.AppendChild(fragment);
        source.AppendChild(doctype);
        source.AppendChild(root);

        var shallow = (Document)source.CloneNode();
        shallow.Kind.Should().Be(DocumentKind.Xml);
        shallow.ChildCount.Should().Be(0);

        var copy = (Document)source.CloneNode(deep: true);
        copy.Kind.Should().Be(DocumentKind.Xml);
        copy.ChildCount.Should().Be(2);
        copy.Doctype.Should().NotBeSameAs(doctype);
        copy.DocumentElement.Should().NotBeSameAs(root);
        copy.FirstChild.Should().BeSameAs(copy.Doctype);
        copy.Doctype!.NextSibling.Should().BeSameAs(copy.DocumentElement);
        copy.DocumentElement!.PreviousSibling.Should().BeSameAs(copy.Doctype);
        copy.DocumentElement.OwnerDocument.Should().BeSameAs(copy);
        var copiedChild = (Element)copy.DocumentElement.FirstChild!;
        copiedChild.OwnerDocument.Should().BeSameAs(copy);
        ((Text)copiedChild.FirstChild!).Data.Should().Be("leaf");
        copiedChild.NextSibling.Should().BeOfType<Comment>().Which.Data.Should().Be("tail");
        source.DocumentElement.Should().BeSameAs(root);
        root.FirstChild.Should().BeSameAs(child);

        fragmentCopy.OwnerDocument.Should().BeSameAs(source);
        fragmentCopy.ChildNodes.Select(node => node.NodeType).Should().Equal(NodeType.Element, NodeType.Comment);
        fragmentCopy.FirstChild.Should().NotBeSameAs(child);
    }

    [Test]
    public void ImportPreservesNamesAttributesAndDataAcrossDocumentKinds()
    {
        var xml = Document.CreateXml();
        var html = Document.CreateHtml();
        var source = xml.CreateElementNS(Namespaces.Html, "DiV");
        var first = xml.CreateAttributeNS("urn:example", "P:Mode");
        first.Value = "A";
        source.SetAttributeNode(first);
        source.SetAttribute("MiXeD", "B");
        var cdata = xml.CreateCDataSection("valid");
        cdata.Data = "later]]>allowed";
        var pi = xml.CreateProcessingInstruction("go", "valid");
        pi.Data = "later?>allowed";
        source.AppendChild(cdata);
        source.AppendChild(pi);

        var imported = (Element)html.ImportNode(source, deep: true);
        imported.OwnerDocument.Should().BeSameAs(html);
        imported.ParentNode.Should().BeNull();
        imported.LocalName.Should().Be("DiV");
        imported.NamespaceUri.Should().Be(Namespaces.Html);
        imported.Attributes.Select(a => a.Name).Should().Equal("P:Mode", "MiXeD");
        imported.Attributes.Select(a => a.Value).Should().Equal("A", "B");
        imported.Attributes.All(a => ReferenceEquals(a.OwnerDocument, html) && ReferenceEquals(a.OwnerElement, imported)).Should().BeTrue();
        imported.Attributes.First().Should().NotBeSameAs(first);
        ((CDataSection)imported.FirstChild!).Data.Should().Be("later]]>allowed");
        ((ProcessingInstruction)imported.LastChild!).Data.Should().Be("later?>allowed");
        imported.FirstChild!.OwnerDocument.Should().BeSameAs(html);
        source.OwnerDocument.Should().BeSameAs(xml);
        source.Attributes.First().Should().BeSameAs(first);
        source.FirstChild.Should().BeSameAs(cdata);
        first.OwnerElement.Should().BeSameAs(source);

        var shallow = (Element)html.ImportNode(source);
        shallow.ChildCount.Should().Be(0);
        shallow.Attributes.Select(a => a.Name).Should().Equal("P:Mode", "MiXeD");
        shallow.Attributes.First().Should().NotBeSameAs(first);

        var xmlImport = (Element)xml.ImportNode(html.CreateElement("MiXeD"));
        xmlImport.LocalName.Should().Be("mixed");
        xmlImport.NamespaceUri.Should().Be(Namespaces.Html);
        xmlImport.OwnerDocument.Should().BeSameAs(xml);
    }

    [Test]
    public void AttributeCopiesStayDetachedAndIndependent()
    {
        var sourceDocument = Document.CreateXml();
        var destination = Document.CreateHtml();
        var element = sourceDocument.CreateElement("item");
        var original = sourceDocument.CreateAttributeNS("urn:x", "p:Name");
        original.Value = "first";
        element.SetAttributeNode(original);
        original.Prefix = "different";

        var clone = original.Clone();
        var imported = destination.ImportAttribute(original);
        foreach (var copy in new[] { clone, imported })
        {
            copy.Should().NotBeSameAs(original);
            copy.OwnerElement.Should().BeNull();
            copy.NamespaceUri.Should().Be("urn:x");
            copy.LocalName.Should().Be("Name");
            copy.Prefix.Should().Be("different");
            copy.Value.Should().Be("first");
        }

        clone.OwnerDocument.Should().BeSameAs(sourceDocument);
        imported.OwnerDocument.Should().BeSameAs(destination);
        imported.Value = "changed";
        imported.Prefix = "new";
        original.Value.Should().Be("first");
        original.Prefix.Should().Be("different");
        original.OwnerElement.Should().BeSameAs(element);
    }

    [Test]
    public void DeepCloneAndImportUseAnIterativeWalk()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateHtml();
        var root = source.CreateElement("root");
        var leaf = root;
        for (var i = 0; i < 20_000; i++)
        {
            var next = source.CreateElement("n");
            leaf.AppendChild(next);
            leaf = next;
        }

        var clone = root.CloneNode(deep: true);
        var imported = destination.ImportNode(root, deep: true);
        foreach (var tree in new[] { clone, imported })
        {
            var current = tree;
            for (var i = 0; i < 20_000; i++)
            {
                current = current.FirstChild!;
            }

            current.ChildCount.Should().Be(0);
            current.OwnerDocument.Should().BeSameAs(ReferenceEquals(tree, clone) ? source : destination);
        }

        leaf.OwnerDocument.Should().BeSameAs(source);
        root.ParentNode.Should().BeNull();
    }

    [Test]
    public void InvalidInputsAndUnsupportedKindsFailBeforeChangingSource()
    {
        var document = Document.CreateXml();
        var other = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        Assert.Throws<ArgumentNullException>(() => other.ImportNode(null!));
        Assert.Throws<ArgumentNullException>(() => other.ImportAttribute(null!));
        Assert.That(Assert.Throws<DomException>(() => other.ImportNode(document))!.Name, Is.EqualTo("NotSupportedError"));

        var unsupported = new UnsupportedNode(document);
        Assert.That(Assert.Throws<DomException>(() => unsupported.CloneNode())!.Name, Is.EqualTo("NotSupportedError"));
        Assert.That(Assert.Throws<DomException>(() => other.ImportNode(unsupported))!.Name, Is.EqualTo("NotSupportedError"));
        document.DocumentElement.Should().BeSameAs(root);
        root.OwnerDocument.Should().BeSameAs(document);
    }

    private sealed class UnsupportedNode(Document owner) : Node(owner)
    {
        public override NodeType NodeType => (NodeType)99;
    }
}

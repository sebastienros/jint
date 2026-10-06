#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class NativeTreeTests
{
    [Test]
    public void LinksAndDetachedTreesRemainConsistentAcrossMoves()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("DIV");
        var first = document.CreateTextNode("one");
        var middle = document.CreateComment("middle");
        var last = document.CreateTextNode("three");
        root.AppendChild(first);
        root.AppendChild(last);
        root.InsertBefore(middle, last);
        document.AppendChild(root);

        root.ChildNodes.Should().Equal(first, middle, last);
        root.ChildCount.Should().Be(3);
        first.PreviousSibling.Should().BeNull();
        first.NextSibling.Should().BeSameAs(middle);
        middle.PreviousSibling.Should().BeSameAs(first);
        middle.NextSibling.Should().BeSameAs(last);
        last.NextSibling.Should().BeNull();
        root.FirstChild.Should().BeSameAs(first);
        root.LastChild.Should().BeSameAs(last);
        root.LocalName.Should().Be("div");

        root.AppendChild(first);
        root.ChildNodes.Should().Equal(middle, last, first);
        root.RemoveChild(last).Should().BeSameAs(last);
        last.ParentNode.Should().BeNull();
        last.PreviousSibling.Should().BeNull();
        last.NextSibling.Should().BeNull();
        last.OwnerDocument.Should().BeSameAs(document);
        root.ChildNodes.Should().Equal(middle, first);
    }

    [Test]
    public void DocumentInsertionChecksWholeResultBeforeChangingAnything()
    {
        var document = Document.CreateHtml();
        var doctype = document.CreateDocumentType("html");
        var root = document.CreateElement("html");
        document.AppendChild(doctype);
        document.AppendChild(root);
        var fragment = document.CreateDocumentFragment();
        var comment = document.CreateComment("safe");
        var otherRoot = document.CreateElement("html");
        fragment.AppendChild(comment);
        fragment.AppendChild(otherRoot);

        Assert.That(Assert.Throws<DomException>(() => document.AppendChild(fragment))!.Name, Is.EqualTo("HierarchyRequestError"));
        document.ChildNodes.Should().Equal(doctype, root);
        fragment.ChildNodes.Should().Equal(comment, otherRoot);
        Assert.That(Assert.Throws<DomException>(() => document.AppendChild(document))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => document.InsertBefore(root, doctype))!.Name, Is.EqualTo("HierarchyRequestError"));
        document.ChildNodes.Should().Equal(doctype, root);
    }

    [Test]
    public void AncestorRejectionPrecedesAReferenceChildFromAnotherParent()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("main");
        var child = document.CreateElement("div");
        var unrelated = document.CreateElement("p");
        document.AppendChild(parent);
        parent.AppendChild(child);

        Assert.Throws<DomException>(() => child.InsertBefore(parent, unrelated))!.Name
            .Should().Be("HierarchyRequestError");
        Assert.Throws<DomException>(() => child.ReplaceChild(parent, unrelated))!.Name
            .Should().Be("HierarchyRequestError");
        Assert.Throws<DomException>(() => child.EnsurePreInsert(parent, unrelated))!.Name
            .Should().Be("HierarchyRequestError");
        parent.ChildNodes.Should().Equal(child);
        child.ChildNodes.Should().BeEmpty();
        unrelated.ParentNode.Should().BeNull();
    }

    [Test]
    public void DocumentOrderAllowsMovingExistingNodesButNotDoctypeAfterRoot()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var doctype = document.CreateDocumentType("root");
        var note = document.CreateComment("note");
        document.AppendChild(note);
        document.AppendChild(doctype);
        document.AppendChild(root);

        document.InsertBefore(note, root);
        document.ChildNodes.Should().Equal(doctype, note, root);
        Assert.That(Assert.Throws<DomException>(() => document.AppendChild(doctype))!.Name, Is.EqualTo("HierarchyRequestError"));
        document.ChildNodes.Should().Equal(doctype, note, root);
        document.DocumentElement.Should().BeSameAs(root);
        document.Doctype.Should().BeSameAs(doctype);
    }

    [Test]
    public void DocumentAppendsAncillaryOnlyFragmentsInOrder()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        var fragment = document.CreateDocumentFragment();
        var comment = document.CreateComment("after");
        var instruction = document.CreateProcessingInstruction("style", "x");
        fragment.AppendChild(comment);
        fragment.AppendChild(instruction);

        document.AppendChild(fragment);
        document.ChildNodes.Should().Equal(root, comment, instruction);
        fragment.ChildCount.Should().Be(0);
    }

    [Test]
    public void ReplacementAndFragmentInsertionPreserveIdentityAndEmptyFragment()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var a = document.CreateElement("a");
        var b = document.CreateElement("b");
        var c = document.CreateElement("c");
        root.AppendChild(a);
        root.AppendChild(b);
        root.AppendChild(c);
        document.AppendChild(root);

        root.ReplaceChild(c, a).Should().BeSameAs(a);
        root.ChildNodes.Should().Equal(c, b);
        a.ParentNode.Should().BeNull();
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(a);
        fragment.AppendChild(document.CreateTextNode("x"));
        root.ReplaceChild(fragment, b).Should().BeSameAs(b);
        root.ChildNodes.Should().Equal(c, a, root.LastChild!);
        fragment.ChildCount.Should().Be(0);
        b.ParentNode.Should().BeNull();
        root.LastChild.Should().BeOfType<Text>().Which.Data.Should().Be("x");
    }

    [Test]
    public void CyclesAndInvalidChildKindsLeaveDetachedTreesIntact()
    {
        var document = Document.CreateXml();
        var outer = document.CreateElement("outer");
        var inner = document.CreateElement("inner");
        outer.AppendChild(inner);
        Assert.That(Assert.Throws<DomException>(() => inner.AppendChild(outer))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => outer.AppendChild(document.CreateDocumentType("x")))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => outer.AppendChild(document))!.Name, Is.EqualTo("HierarchyRequestError"));
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(document.CreateElement("child"));
        Assert.That(Assert.Throws<DomException>(() => fragment.AppendChild(fragment))!.Name, Is.EqualTo("HierarchyRequestError"));
        fragment.ChildCount.Should().Be(1);
        outer.ChildNodes.Should().Equal(inner);
        inner.ParentNode.Should().BeSameAs(outer);
    }

    [Test]
    public void InvalidReplacementLeavesBothTreesUnchanged()
    {
        var document = Document.CreateXml();
        var doctype = document.CreateDocumentType("root");
        var root = document.CreateElement("root");
        var other = document.CreateElement("other");
        document.AppendChild(doctype);
        document.AppendChild(root);
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(other);
        fragment.AppendChild(document.CreateElement("third"));

        Assert.That(Assert.Throws<DomException>(() => document.ReplaceChild(fragment, root))!.Name, Is.EqualTo("HierarchyRequestError"));
        document.ChildNodes.Should().Equal(doctype, root);
        fragment.ChildCount.Should().Be(2);
        other.ParentNode.Should().BeSameAs(fragment);
        root.ParentNode.Should().BeSameAs(document);
    }

    [Test]
    public void MovingSubtreeBetweenDocumentsChangesOwnersButNotIdentity()
    {
        var firstDocument = Document.CreateXml();
        var secondDocument = Document.CreateXml();
        var root = firstDocument.CreateElementNS(Namespaces.Svg, "svg:svg");
        var child = firstDocument.CreateElementNS(Namespaces.Svg, "svg:path");
        child.SetAttributeNS(Namespaces.Xml, "xml:lang", "en");
        var attribute = child.GetAttributeNodeNS(Namespaces.Xml, "lang")!;
        root.AppendChild(child);
        firstDocument.AppendChild(root);

        secondDocument.AppendChild(root);
        firstDocument.DocumentElement.Should().BeNull();
        secondDocument.DocumentElement.Should().BeSameAs(root);
        root.OwnerDocument.Should().BeSameAs(secondDocument);
        child.OwnerDocument.Should().BeSameAs(secondDocument);
        attribute.OwnerDocument.Should().BeSameAs(secondDocument);
        child.GetAttributeNodeNS(Namespaces.Xml, "lang").Should().BeSameAs(attribute);
    }

    [Test]
    public void ReplacingWithForeignEmptyFragmentAdoptsTheFragment()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var fragment = source.CreateDocumentFragment();
        var root = destination.CreateElement("root");
        destination.AppendChild(root);

        destination.ReplaceChild(fragment, root).Should().BeSameAs(root);
        destination.DocumentElement.Should().BeNull();
        fragment.OwnerDocument.Should().BeSameAs(destination);
        fragment.ChildCount.Should().Be(0);
    }

    [Test]
    public void DeepSubtreeAdoptionUsesBoundedNativeStack()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateElement("root");
        var current = root;
        for (var i = 0; i < 20_000; i++)
        {
            var child = source.CreateElement("n");
            current.AppendChild(child);
            current = child;
        }

        destination.AppendChild(root);
        current.OwnerDocument.Should().BeSameAs(destination);
        root.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void XmlDocumentRejectsCDataOutsideItsElement()
    {
        var document = Document.CreateXml();
        var cdata = document.CreateCDataSection("loose");
        Assert.That(Assert.Throws<DomException>(() => document.AppendChild(cdata))!.Name, Is.EqualTo("HierarchyRequestError"));
        cdata.ParentNode.Should().BeNull();

        var root = document.CreateElement("root");
        document.AppendChild(root);
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(cdata);
        Assert.That(Assert.Throws<DomException>(() => document.InsertBefore(fragment, root))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => document.ReplaceChild(fragment, root))!.Name, Is.EqualTo("HierarchyRequestError"));
        document.DocumentElement.Should().BeSameAs(root);
        cdata.ParentNode.Should().BeSameAs(fragment);
    }
}

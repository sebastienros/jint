#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class AdoptionTests
{
    [Test]
    public void ExplicitAdoptionPreservesIdentityAndAttachedAttributes()
    {
        var source = Document.CreateXml("image/svg+xml");
        var destination = Document.CreateXml("application/xhtml+xml");
        var root = source.CreateElement("root");
        var child = source.CreateElement("child");
        child.SetAttributeNS("urn:example", "p:Name", "value");
        var attribute = child.Attributes.Single();
        root.AppendChild(child);
        source.AppendChild(root);

        destination.AdoptNode(root).Should().BeSameAs(root);
        source.DocumentElement.Should().BeNull();
        root.ParentNode.Should().BeNull();
        root.OwnerDocument.Should().BeSameAs(destination);
        child.OwnerDocument.Should().BeSameAs(destination);
        attribute.OwnerDocument.Should().BeSameAs(destination);
        attribute.OwnerElement.Should().BeSameAs(child);
        child.Attributes.Single().Should().BeSameAs(attribute);
        source.ContentType.Should().Be("image/svg+xml");
        destination.ContentType.Should().Be("application/xhtml+xml");
    }

    [Test]
    public void SameDocumentAdoptionStillDetaches()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var child = document.CreateTextNode("value");
        root.AppendChild(child);
        document.AppendChild(root);

        document.AdoptNode(child).Should().BeSameAs(child);
        child.ParentNode.Should().BeNull();
        child.OwnerDocument.Should().BeSameAs(document);
        root.ChildCount.Should().Be(0);
    }

    [Test]
    public void EveryNativeNonDocumentKindCanBeAdopted()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateHtml();
        Node[] nodes =
        [
            source.CreateElement("element"),
            source.CreateTextNode("text"),
            source.CreateCDataSection("cdata"),
            source.CreateProcessingInstruction("go", "data"),
            source.CreateComment("comment"),
            source.CreateDocumentType("root"),
            source.CreateDocumentFragment()
        ];

        foreach (var node in nodes)
        {
            destination.AdoptNode(node).Should().BeSameAs(node);
            node.OwnerDocument.Should().BeSameAs(destination);
        }
    }

    [Test]
    public void FragmentOperationsKeepTheirDistinctOwnershipRules()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var target = destination.CreateElement("target");
        destination.AppendChild(target);

        var append = source.CreateDocumentFragment();
        var appendChild = source.CreateTextNode("append");
        append.AppendChild(appendChild);
        target.AppendChild(append);
        append.OwnerDocument.Should().BeSameAs(source);
        appendChild.OwnerDocument.Should().BeSameAs(destination);

        var insert = source.CreateDocumentFragment();
        var insertChild = source.CreateTextNode("insert");
        insert.AppendChild(insertChild);
        target.InsertBefore(insert, appendChild);
        insert.OwnerDocument.Should().BeSameAs(source);
        insertChild.OwnerDocument.Should().BeSameAs(destination);

        var replace = source.CreateDocumentFragment();
        var replaceChild = source.CreateTextNode("replace");
        replace.AppendChild(replaceChild);
        target.ReplaceChild(replace, appendChild);
        replace.OwnerDocument.Should().BeSameAs(destination);
        replaceChild.OwnerDocument.Should().BeSameAs(destination);

        var replaceAll = source.CreateDocumentFragment();
        var replaceAllChild = source.CreateTextNode("all");
        replaceAll.AppendChild(replaceAllChild);
        target.ReplaceChildren(replaceAll);
        replaceAll.OwnerDocument.Should().BeSameAs(source);
        replaceAllChild.OwnerDocument.Should().BeSameAs(destination);

        var adopted = source.CreateDocumentFragment();
        var adoptedChild = source.CreateTextNode("kept");
        adopted.AppendChild(adoptedChild);
        destination.AdoptNode(adopted).Should().BeSameAs(adopted);
        adopted.OwnerDocument.Should().BeSameAs(destination);
        adoptedChild.OwnerDocument.Should().BeSameAs(destination);
        adoptedChild.ParentNode.Should().BeSameAs(adopted);

        foreach (var empty in new[] { source.CreateDocumentFragment(), source.CreateDocumentFragment(), source.CreateDocumentFragment() })
        {
            target.AppendChild(empty);
            empty.OwnerDocument.Should().BeSameAs(source);
        }

        var emptyInsert = source.CreateDocumentFragment();
        target.InsertBefore(emptyInsert, replaceAllChild);
        emptyInsert.OwnerDocument.Should().BeSameAs(source);

        var emptyReplace = source.CreateDocumentFragment();
        target.ReplaceChild(emptyReplace, replaceAllChild);
        emptyReplace.OwnerDocument.Should().BeSameAs(destination);
        var emptyReplaceAll = source.CreateDocumentFragment();
        target.ReplaceChildren(emptyReplaceAll);
        emptyReplaceAll.OwnerDocument.Should().BeSameAs(source);
        var emptyAdopted = source.CreateDocumentFragment();
        destination.AdoptNode(emptyAdopted);
        emptyAdopted.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void DeepExplicitAdoptionUsesIterativeWalk()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateElement("root");
        var leaf = root;
        for (var i = 0; i < 20_000; i++)
        {
            var next = source.CreateElement("next");
            leaf.AppendChild(next);
            leaf = next;
        }

        destination.AdoptNode(root);
        leaf.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void DocumentAndNullAreRejectedBeforeMutation()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var root = source.CreateElement("root");
        source.AppendChild(root);
        Assert.Throws<ArgumentNullException>(() => destination.AdoptNode(null!));
        Assert.That(Assert.Throws<DomException>(() => destination.AdoptNode(source))!.Name, Is.EqualTo("NotSupportedError"));
        source.DocumentElement.Should().BeSameAs(root);
    }
}

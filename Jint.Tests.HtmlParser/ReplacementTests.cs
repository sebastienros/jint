#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ReplacementTests
{
    [Test]
    public void ReplaceAllCanClearOrMoveAnExistingDescendant()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var branch = document.CreateElement("branch");
        var leaf = document.CreateElement("leaf");
        branch.AppendChild(leaf);
        root.AppendChild(branch);
        root.AppendChild(document.CreateComment("old"));

        root.ReplaceChildren(leaf);
        root.ChildNodes.Should().Equal(leaf);
        leaf.ParentNode.Should().BeSameAs(root);
        branch.ParentNode.Should().BeNull();
        branch.ChildCount.Should().Be(0);
        root.ReplaceChildren();
        root.ChildCount.Should().Be(0);
        leaf.ParentNode.Should().BeNull();
    }

    [Test]
    public void DocumentReplacementValidatesCompleteShapeBeforeMutation()
    {
        var source = Document.CreateXml();
        var destination = Document.CreateXml();
        var doctype = destination.CreateDocumentType("root");
        var root = destination.CreateElement("root");
        destination.AppendChild(doctype);
        destination.AppendChild(root);

        var invalid = source.CreateDocumentFragment();
        var first = source.CreateElement("first");
        var second = source.CreateElement("second");
        invalid.AppendChild(first);
        invalid.AppendChild(second);
        Assert.That(Assert.Throws<DomException>(() => destination.ReplaceChildren(invalid))!.Name, Is.EqualTo("HierarchyRequestError"));
        destination.ChildNodes.Should().Equal(doctype, root);
        invalid.ChildNodes.Should().Equal(first, second);
        first.OwnerDocument.Should().BeSameAs(source);
        second.OwnerDocument.Should().BeSameAs(source);

        var element = source.CreateElement("new");
        destination.ReplaceChildren(element);
        destination.ChildNodes.Should().Equal(element);
        element.OwnerDocument.Should().BeSameAs(destination);
        var replacementDoctype = source.CreateDocumentType("new");
        destination.ReplaceChildren(replacementDoctype);
        destination.ChildNodes.Should().Equal(replacementDoctype);
        replacementDoctype.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void FailedCycleAndInvalidKindLeaveBothTreesUntouched()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var child = document.CreateElement("child");
        root.AppendChild(child);
        document.AppendChild(root);

        Assert.That(Assert.Throws<DomException>(() => child.ReplaceChildren(root))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => root.ReplaceChildren(document.CreateDocumentType("x")))!.Name, Is.EqualTo("HierarchyRequestError"));
        Assert.That(Assert.Throws<DomException>(() => document.ReplaceChildren(document.CreateTextNode("x")))!.Name, Is.EqualTo("HierarchyRequestError"));
        root.ChildNodes.Should().Equal(child);
        document.DocumentElement.Should().BeSameAs(root);
    }

    [Test]
    public void SameNodeInsertionAndReplacementStillTakeMutationPath()
    {
        var document = Document.CreateXml();
        var root = document.CreateElement("root");
        var first = document.CreateElement("first");
        var last = document.CreateElement("last");
        root.AppendChild(first);
        root.AppendChild(last);

        root.InsertBefore(first, first).Should().BeSameAs(first);
        root.ChildNodes.Should().Equal(first, last);
        root.ReplaceChild(last, last).Should().BeSameAs(last);
        root.ChildNodes.Should().Equal(first, last);
    }
}

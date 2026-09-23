#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser;

public class ParserInsertionTests
{
    [Test]
    public void FreshInsertionMaintainsIdentityOrderAndEveryLink()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("parent");
        var first = document.CreateTextNode("first");
        var last = document.CreateTextNode("last");
        parent.AppendParsedChild(first);
        parent.AppendParsedChild(last);

        var front = document.CreateTextNode("front");
        var middle = document.CreateTextNode("middle");
        var tail = document.CreateTextNode("tail");
        parent.InsertParsedBefore(front, first, CancellationToken.None);
        parent.InsertParsedBefore(middle, last, CancellationToken.None);
        parent.InsertParsedBefore(tail, null, CancellationToken.None);

        parent.ChildNodes.Should().Equal(front, first, middle, last, tail);
        parent.ChildCount.Should().Be(5);
        parent.FirstChild.Should().BeSameAs(front);
        parent.LastChild.Should().BeSameAs(tail);
        front.PreviousSibling.Should().BeNull();
        tail.NextSibling.Should().BeNull();
        var children = parent.ChildNodes.ToArray();
        for (var i = 0; i < children.Length; i++)
        {
            children[i].ParentNode.Should().BeSameAs(parent);
            children[i].OwnerDocument.Should().BeSameAs(document);
            children[i].PreviousSibling.Should().BeSameAs(i == 0 ? null : children[i - 1]);
            children[i].NextSibling.Should().BeSameAs(i == children.Length - 1 ? null : children[i + 1]);
        }
    }

    [Test]
    public void InvalidDirectPreconditionsLeaveLinksStampAndRecordsUntouched()
    {
        var document = Document.CreateXml();
        var other = Document.CreateXml();
        var parent = document.CreateElement("parent");
        var first = document.CreateTextNode("first");
        parent.AppendChild(first);
        var foreignReference = document.CreateElement("foreign-reference");
        var detachedWithChild = document.CreateElement("detached-with-child");
        detachedWithChild.AppendChild(document.CreateTextNode("child"));
        var observed = document.CreateElement("observed");
        using var observedNode = document.ObserveMutations(observed, new MutationObserverOptions { Attributes = true });
        using var records = document.ObserveMutations(parent, new MutationObserverOptions { ChildList = true });
        var stamp = document.MutationStamp;
        var otherStamp = other.MutationStamp;

        AssertRejected(parent, document.CreateElement("unused"), foreignReference);
        AssertRejected(parent, document.CreateElement("unused"), document.CreateElement("detached-reference"));
        AssertRejected(parent, other.CreateElement("wrong-owner"), first);
        AssertRejected(parent, first, first);
        AssertRejected(parent, detachedWithChild, first);
        AssertRejected(parent, observed, first);
        AssertRejected(parent, document.CreateDocumentFragment(), first);
        AssertRejected(parent, document, first);
        var stalePrevious = document.CreateTextNode("stale-previous");
        SetLink(stalePrevious, nameof(Node.PreviousSibling), first);
        AssertRejected(parent, stalePrevious, first);
        var staleNext = document.CreateTextNode("stale-next");
        SetLink(staleNext, nameof(Node.NextSibling), first);
        AssertRejected(parent, staleNext, first);

        var detached = document.CreateElement("detached");
        AssertRejected(detached, detached, null);
        Assert.That(Assert.Throws<DomException>(() => first.InsertParsedBefore(
            document.CreateTextNode("invalid-container"), null, CancellationToken.None))!.Name,
            Is.EqualTo("HierarchyRequestError"));
        var inert = Document.CreateHtml().CreateElement("template").TemplateContent!.OwnerDocument!;
        var template = inert.CreateParsedElement(Namespaces.Html, "template", null);
        var ownContent = template.TemplateContent!;
        AssertRejected(ownContent, template, null);
        ownContent.ChildCount.Should().Be(0);
        template.ParentNode.Should().BeNull();

        parent.ChildNodes.Should().Equal(first);
        parent.ChildCount.Should().Be(1);
        first.PreviousSibling.Should().BeNull();
        first.NextSibling.Should().BeNull();
        document.MutationStamp.Should().Be(stamp);
        other.MutationStamp.Should().Be(otherStamp);
        records.TakeRecords().Should().BeEmpty();
        observedNode.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void ObservedDestinationGetsOneAdditionWithExactBoundaries()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("parent");
        var first = document.CreateTextNode("first");
        var last = document.CreateTextNode("last");
        parent.AppendChild(first);
        parent.AppendChild(last);
        using var subscription = document.ObserveMutations(parent, new MutationObserverOptions { ChildList = true });
        var stamp = document.MutationStamp;
        var child = document.CreateParsedElement(Namespaces.Html, "b", null);
        child.InitializeParsedAttributes([new ParserAttribute(null, "id", null, "fresh")], CancellationToken.None);

        parent.InsertParsedBefore(child, last, CancellationToken.None);

        document.MutationStamp.Should().BeGreaterThan(stamp);
        parent.ChildNodes.Should().Equal(first, child, last);
        child.OwnerDocument.Should().BeSameAs(document);
        child.GetAttribute("id").Should().Be("fresh");
        var record = subscription.TakeRecords().Single();
        record.Target.Should().BeSameAs(parent);
        record.Kind.Should().Be(MutationRecordKind.ChildList);
        record.AddedNodes.Should().Equal(child);
        record.RemovedNodes.Should().BeEmpty();
        record.PreviousSibling.Should().BeSameAs(first);
        record.NextSibling.Should().BeSameAs(last);
    }

    [Test]
    public void ObservedTemplateContentUsesItsActualOwnerAndDoesNotNotifyHost()
    {
        var document = Document.CreateHtml();
        var template = document.CreateElement("template");
        var content = template.TemplateContent!;
        var owner = content.OwnerDocument!;
        var reference = owner.CreateTextNode("table");
        content.AppendParsedChild(reference);
        using var hostRecords = document.ObserveMutations(template, new MutationObserverOptions
        {
            ChildList = true, Subtree = true
        });
        using var contentRecords = document.ObserveMutations(content, new MutationObserverOptions { ChildList = true });
        var stamp = owner.MutationStamp;
        var child = owner.CreateParsedElement(Namespaces.Html, "tr", null);

        content.InsertParsedBefore(child, reference, CancellationToken.None);

        child.ParentNode.Should().BeSameAs(content);
        child.OwnerDocument.Should().BeSameAs(owner);
        content.ChildNodes.Should().Equal(child, reference);
        owner.MutationStamp.Should().BeGreaterThan(stamp);
        contentRecords.TakeRecords().Single().AddedNodes.Should().Equal(child);
        hostRecords.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void CancellationBeforeCommitLeavesNoChangeAndAfterCommitLeavesCompleteChange()
    {
        var document = Document.CreateXml();
        var parent = document.CreateElement("parent");
        var reference = document.CreateTextNode("reference");
        parent.AppendChild(reference);
        using var subscription = document.ObserveMutations(parent, new MutationObserverOptions { ChildList = true });
        var before = document.MutationStamp;
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var discarded = document.CreateTextNode("discarded");
        Assert.Throws<OperationCanceledException>(() => parent.InsertParsedBefore(discarded, reference, canceled.Token));
        discarded.ParentNode.Should().BeNull();
        parent.ChildNodes.Should().Equal(reference);
        document.MutationStamp.Should().Be(before);
        subscription.TakeRecords().Should().BeEmpty();

        using var canceledAfterCommit = new CancellationTokenSource();
        var committed = document.CreateTextNode("committed");
        var checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => parent.InsertParsedBefore(committed, reference,
            () =>
            {
                checkpoints++;
                canceledAfterCommit.Cancel();
            }, canceledAfterCommit.Token));
        checkpoints.Should().Be(1);
        parent.ChildNodes.Should().Equal(committed, reference);
        parent.FirstChild.Should().BeSameAs(committed);
        parent.LastChild.Should().BeSameAs(reference);
        parent.ChildCount.Should().Be(2);
        committed.ParentNode.Should().BeSameAs(parent);
        committed.PreviousSibling.Should().BeNull();
        committed.NextSibling.Should().BeSameAs(reference);
        reference.PreviousSibling.Should().BeSameAs(committed);
        document.MutationStamp.Should().BeGreaterThan(before);
        var record = subscription.TakeRecords().Single();
        record.AddedNodes.Should().Equal(committed);
        record.RemovedNodes.Should().BeEmpty();
        record.PreviousSibling.Should().BeNull();
        record.NextSibling.Should().BeSameAs(reference);
    }

    [Test]
    public void DeepFreshInsertionCallsOneCommitCheckpointPerLevel()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateParsedElement(Namespaces.Html, "root", null);
        document.AppendParsedChild(parent);
        var checkpoints = 0;
        const int depth = 10_000;
        for (var i = 0; i < depth; i++)
        {
            var table = document.CreateTextNode("table");
            parent.AppendParsedChild(table);
            var next = document.CreateParsedElement(Namespaces.Html, "next", null);
            parent.InsertParsedBefore(next, table, () => checkpoints++, CancellationToken.None);
            parent = next;
        }

        checkpoints.Should().Be(depth);
        parent.ChildCount.Should().Be(0);
        parent.OwnerDocument.Should().BeSameAs(document);
    }

    private static void AssertRejected(Node parent, Node child, Node? reference)
    {
        Assert.Throws<InvalidOperationException>(() => parent.InsertParsedBefore(child, reference,
            CancellationToken.None));
    }

    private static void SetLink(Node node, string property, Node value)
        => typeof(Node).GetProperty(property)!.SetValue(node, value);
}

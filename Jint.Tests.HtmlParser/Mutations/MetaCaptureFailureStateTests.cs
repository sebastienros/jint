#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class MetaCaptureFailureStateTests
{
    private static MutationSubscription Capture(Document document, Element target)
    {
        var subscription = document.ObserveMutations(target, new MutationObserverOptions { ChildList = true, Subtree = true });
        subscription.CaptureHtmlMetaInsertions = true;
        return subscription;
    }

    private static void CancelSecondCapture(MutationSubscription subscription, OperationCanceledException failure)
    {
        var captures = 0;
        subscription.CreateCaptureWork = () =>
        {
            Action<int>? checkpoint = ++captures == 2 ? _ => throw failure : null;
            return (checkpoint, default);
        };
    }

    [TestCase("append")]
    [TestCase("replace")]
    [TestCase("replace-all")]
    public void WarmCleanTextAreaTracksEachCommittedLinkBeforeLaterCaptureCancels(string operation)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var textarea = document.CreateElement("textarea"); root.AppendChild(textarea);
        var old = document.CreateTextNode("old"); textarea.AppendChild(old);
        var state = textarea.GetHtmlState()!.TextArea!;
        state.GetValue(default).Should().Be("old");
        var fragment = document.CreateDocumentFragment();
        var first = document.CreateTextNode("first"); var second = document.CreateTextNode("second");
        fragment.AppendChild(first); fragment.AppendChild(second);
        using var subscription = Capture(document, textarea);
        var failure = new OperationCanceledException("capture two");
        CancelSecondCapture(subscription, failure);
        subscription.PendingRecord = _ => throw new InvalidOperationException("unwind notification");
        void Insert()
        {
            if (operation == "append") textarea.AppendChild(fragment);
            else if (operation == "replace") textarea.ReplaceChild(fragment, old);
            else textarea.ReplaceChildren(fragment);
        }

        Assert.Throws<OperationCanceledException>(Insert).Should().BeSameAs(failure);
        first.ParentNode.Should().BeSameAs(textarea); second.ParentNode.Should().BeNull();
        state.DirtyValue.Should().BeFalse();
        state.GetValue(default).Should().Be(operation == "append" ? "oldfirst" : "first");
        var record = subscription.TakeRecords().Single();
        record.AddedNodes.Should().Equal(first);
        if (operation == "append") record.RemovedNodes.Should().BeEmpty();
        else record.RemovedNodes.Should().Equal(old);
        document.RangeOperationDepth.Should().Be(0);
    }

    [Test]
    public void RangeSinkCannotMaskCaptureFailureAndRunsOnceAfterMutationPendingSignal()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var old = document.CreateTextNode("old"); root.AppendChild(old);
        var range = new DomRange(document); range.SelectNodeContents(new(root));
        using var rangeSubscription = range.ObserveChanges(document);
        using var mutationSubscription = Capture(document, root);
        var fragment = document.CreateDocumentFragment();
        var first = document.CreateTextNode("first"); var second = document.CreateTextNode("second");
        fragment.AppendChild(first); fragment.AppendChild(second);
        var failure = new OperationCanceledException("capture two");
        CancelSecondCapture(mutationSubscription, failure);
        var rangeFailure = new InvalidOperationException("range scheduler");
        var signals = new List<string>();
        mutationSubscription.PendingRecord = _ => signals.Add("mutation");
        document.PendingRangeChanges = () => { signals.Add("range"); throw rangeFailure; };

        Assert.Throws<OperationCanceledException>(() => root.InsertBefore(fragment, old)).Should().BeSameAs(failure);
        signals.Should().BeEmpty();
        range.Start.Should().Be(new BoundaryPoint(new(root), 0));
        range.End.Should().Be(new BoundaryPoint(new(root), 2));
        rangeSubscription.TakePendingChange().Should().BeTrue();
        document.RangeOperationDepth.Should().Be(0); document.ChangedRanges.Should().BeNull();
        Assert.Throws<InvalidOperationException>(() => document.FlushPendingMutationNotifications()).Should().BeSameAs(rangeFailure);
        signals.Should().Equal("mutation", "range");
        document.FlushPendingMutationNotifications();
        signals.Should().Equal("mutation", "range");
        mutationSubscription.TakeRecords().Single().AddedNodes.Should().Equal(first);
        rangeSubscription.TakePendingChange().Should().BeFalse();
    }

    [TestCase("replace")]
    [TestCase("replace-all")]
    public void ReservationChecksCallerBudgetBeforeReplacementRemovesExistingChildren(string operation)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var old = document.CreateElement("old"); root.AppendChild(old);
        var fragment = document.CreateDocumentFragment();
        var incoming = document.CreateElement("meta"); fragment.AppendChild(incoming);
        using var subscription = Capture(document, root);
        var failure = new OperationCanceledException("allocation budget");
        var checks = 0;
        subscription.CreateCaptureWork = () => ((Action<int>?) (_ => { checks++; throw failure; }), default);
        void Replace()
        {
            if (operation == "replace") root.ReplaceChild(fragment, old);
            else root.ReplaceChildren(fragment);
        }

        Assert.Throws<OperationCanceledException>(Replace).Should().BeSameAs(failure);
        checks.Should().Be(1);
        root.FirstChild.Should().BeSameAs(old); old.ParentNode.Should().BeSameAs(root);
        incoming.ParentNode.Should().BeSameAs(fragment);
        subscription.TakeRecords().Should().BeEmpty();
        document.RangeOperationDepth.Should().Be(0);
    }

    [Test]
    public void CancellationBetweenRemovalsPublishesOnlyActuallyRemovedPrefix()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var first = document.CreateElement("first"); var second = document.CreateElement("second");
        root.AppendChild(first); root.AppendChild(second);
        var incoming = document.CreateElement("meta");
        using var subscription = Capture(document, root);
        var failure = new OperationCanceledException("next removal budget");
        subscription.CreateCaptureWork = () => ((Action<int>?) (_ =>
        {
            if (first.ParentNode is null) throw failure;
        }), default);
        subscription.PendingRecord = _ => throw new InvalidOperationException("unwind notification");

        Assert.Throws<OperationCanceledException>(() => root.ReplaceChildren(incoming)).Should().BeSameAs(failure);
        root.FirstChild.Should().BeSameAs(second);
        incoming.ParentNode.Should().BeNull();
        var record = subscription.TakeRecords().Single();
        record.RemovedNodes.Should().Equal(first);
        record.AddedNodes.Should().BeEmpty(); record.HtmlMetaInsertions.Should().BeEmpty();
        document.RangeOperationDepth.Should().Be(0);
    }

    [Test]
    public void ReservedFailureSegmentsPreserveNormalAndMultipleFailedPrefixRecordOrder()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        using var subscription = Capture(document, root);
        var before = document.CreateElement("before"); root.AppendChild(before);
        var committed = new List<Node> { before };
        for (var i = 0; i < 2; i++)
        {
            var fragment = document.CreateDocumentFragment();
            var first = document.CreateElement("first"); var second = document.CreateElement("second");
            fragment.AppendChild(first); fragment.AppendChild(second);
            var failure = new OperationCanceledException("capture two");
            CancelSecondCapture(subscription, failure);
            Assert.Throws<OperationCanceledException>(() => root.AppendChild(fragment)).Should().BeSameAs(failure);
            committed.Add(first);
            subscription.CreateCaptureWork = null;
            var between = document.CreateElement("between"); root.AppendChild(between); committed.Add(between);
        }

        subscription.TakeRecords().Select(record => record.AddedNodes.Single()).Should().Equal(committed);
        subscription.TakeRecords().Should().BeEmpty();
        var staleSignals = 0; subscription.PendingRecord = _ => staleSignals++;
        document.FlushPendingMutationNotifications();
        staleSignals.Should().Be(0);
    }
}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class CrossDocumentDeferredRangeTests
{
    [TestCase("carrier")]
    [TestCase("throw")]
    [TestCase("direct")]
    [TestCase("ordinary")]
    public void FailedCrossDocumentInsertionCarriesAllSelectionTargetsAndCoalescesTheirSignals(string flush)
    {
        var destination = Document.CreateHtml(); var source = Document.CreateHtml(); var selection = Document.CreateHtml();
        var root = destination.CreateElement("html"); destination.AppendChild(root);
        var old = destination.CreateTextNode("old"); root.AppendChild(old);
        var fragment = source.CreateDocumentFragment();
        var first = source.CreateTextNode("first"); var second = source.CreateTextNode("second");
        fragment.AppendChild(first); fragment.AppendChild(second);
        var destinationRange = new DomRange(destination); destinationRange.SelectNodeContents(new(root));
        var sourceRange = new DomRange(source); sourceRange.SelectNodeContents(new(fragment));
        var duplicateRange = new DomRange(source); duplicateRange.SelectNodeContents(new(fragment));
        using var a = destinationRange.ObserveChanges(destination);
        using var b = sourceRange.ObserveChanges(source);
        using var c = sourceRange.ObserveChanges(selection);
        using var duplicate = duplicateRange.ObserveChanges(source);
        using var mutations = destination.ObserveMutations(root, new MutationObserverOptions { ChildList = true });
        mutations.CaptureHtmlMetaInsertions = true;
        var captures = 0; var failure = new OperationCanceledException("capture two");
        mutations.CreateCaptureWork = () => (++captures == 2 ? (Action<int>?) (_ => throw failure) : null, default);
        var signals = new List<string>();
        destination.PendingRangeChanges = () => signals.Add("A");
        var callbackFailure = new InvalidOperationException("source range scheduler");
        source.PendingRangeChanges = () => { signals.Add("B"); if (flush == "throw") throw callbackFailure; };
        selection.PendingRangeChanges = () => signals.Add("C");

        Assert.Throws<OperationCanceledException>(() => root.InsertBefore(fragment, old)).Should().BeSameAs(failure);
        signals.Should().BeEmpty();
        destinationRange.End.Should().Be(new BoundaryPoint(new(root), 2));
        sourceRange.End.Should().Be(new BoundaryPoint(new(fragment), 0));
        a.TakePendingChange().Should().BeTrue(); b.TakePendingChange().Should().BeTrue();
        c.TakePendingChange().Should().BeTrue(); duplicate.TakePendingChange().Should().BeTrue();
        destination.RangeOperationDepth.Should().Be(0); source.RangeOperationDepth.Should().Be(0);

        if (flush == "direct") source.FlushPendingMutationNotifications();
        else if (flush == "ordinary")
        {
            fragment.AppendChild(source.CreateTextNode("new"));
            sourceRange.SetEnd(new(fragment), 1);
        }
        if (flush == "throw")
        {
            Assert.Throws<InvalidOperationException>(() => destination.FlushPendingMutationNotifications()).Should().BeSameAs(callbackFailure);
            signals.Should().Equal("A", "B");
        }
        destination.FlushPendingMutationNotifications();
        signals.Should().Equal(flush == "direct" ? new[] { "B", "A", "C" }
            : flush == "ordinary" ? new[] { "B", "C", "A" } : new[] { "A", "B", "C" });
        destination.FlushPendingMutationNotifications(); source.FlushPendingMutationNotifications(); selection.FlushPendingMutationNotifications();
        signals.Count.Should().Be(3);
    }

    [Test]
    public void CarrierSchedulesTargetRangesWithoutFlushingTargetMutationTickets()
    {
        var carrier = Document.CreateHtml(); var target = Document.CreateHtml();
        var node = target.CreateElement("html"); target.AppendChild(node);
        using var observer = target.ObserveMutations(node, new MutationObserverOptions { ChildList = true });
        observer.Enqueue(new MutationRecord(MutationRecordKind.ChildList, node));
        target.PublishMutationNotifications(new MutationNotificationTicket(MutationTracking.Match(node, MutationRecordKind.ChildList)!));
        var mutationSignals = 0; observer.PendingRecord = _ => mutationSignals++;
        var rangeSignals = 0; target.PendingRangeChanges = () => rangeSignals++;
        target.MarkDeferredRangeSignal(carrier);

        carrier.FlushPendingMutationNotifications();
        rangeSignals.Should().Be(1); mutationSignals.Should().Be(0);
        target.FlushPendingMutationNotifications();
        rangeSignals.Should().Be(1); mutationSignals.Should().Be(1);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ClearingCarrierOrTargetUnlinksSchedulingNodesAndAllowsReuse(bool clearCarrier)
    {
        var carrier = Document.CreateHtml(); var target = Document.CreateHtml();
        var signals = 0; target.PendingRangeChanges = () => signals++;
        target.MarkDeferredRangeSignal(carrier);
        (clearCarrier ? carrier : target).ClearPendingMutationNotifications();
        carrier.FlushPendingMutationNotifications(); target.FlushPendingMutationNotifications();
        signals.Should().Be(0);
        target.MarkDeferredRangeSignal(carrier);
        carrier.FlushPendingMutationNotifications(); target.FlushPendingMutationNotifications();
        signals.Should().Be(1);
    }
}

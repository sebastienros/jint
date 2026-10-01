#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class RangeChangeTests
{
    [Test]
    public void EndpointPairsAndOppositeEndCollapseScheduleOnlyAfterCoherentChanges()
    {
        var doc = Document.CreateHtml(); var text = doc.CreateTextNode("abcd"); var range = new DomRange(doc);
        using var subscription = range.ObserveChanges(doc);
        var signals = 0; doc.PendingRangeChanges = () => signals++;
        range.SelectNodeContents(new(text)); signals.Should().Be(1);
        subscription.TakePendingChange().Should().BeTrue(); subscription.TakePendingChange().Should().BeFalse();
        range.SetStart(new(text), 2); signals.Should().Be(2);
        var other = doc.CreateTextNode("xyz"); range.SetEnd(new(other), 1);
        range.Start.Should().Be(range.End); signals.Should().Be(3);
        range.SetEnd(new(other), 1); signals.Should().Be(3);
        Assert.Throws<DomException>(() => range.SetEnd(new(other), uint.MaxValue)); signals.Should().Be(3);
    }
    [Test]
    public void MultipleAssociationsDisconnectIndependentlyAndNoSinkNeedsNoQueue()
    {
        var doc = Document.CreateHtml(); var other = Document.CreateHtml(); var range = new DomRange(doc);
        using var a = range.ObserveChanges(doc); using var b = range.ObserveChanges(other);
        range.SelectNodeContents(new(doc.CreateTextNode("x")));
        a.TakePendingChange().Should().BeTrue(); b.TakePendingChange().Should().BeTrue();
        a.Disconnect(); range.Collapse(true);
        a.TakePendingChange().Should().BeFalse(); b.TakePendingChange().Should().BeTrue();
        doc.ChangedRanges.Should().BeNull(); other.ChangedRanges.Should().BeNull();
    }
    [Test]
    public void ReplaceAllBatchesAutomaticFixupsAcrossRangesAndObserverSuppression()
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var a = doc.CreateTextNode("a"); var b = doc.CreateTextNode("b"); parent.AppendChild(a); parent.AppendChild(b);
        var first = new DomRange(doc); first.SelectNodeContents(new(a)); var second = new DomRange(doc); second.SelectNodeContents(new(b));
        using var aSubscription = first.ObserveChanges(doc); using var bSubscription = second.ObserveChanges(doc);
        var signals = 0; doc.PendingRangeChanges = () => signals++;
        parent.ReplaceChildren(); signals.Should().Be(1);
        first.Start.Should().Be(new BoundaryPoint(new(parent), 0)); second.Start.Should().Be(first.Start);
        aSubscription.TakePendingChange().Should().BeTrue(); bSubscription.TakePendingChange().Should().BeTrue();
        doc.RangeOperationDepth.Should().Be(0); doc.ChangedRanges.Should().BeNull();
    }
    [Test]
    public void SplitAndNormalizeBatchTransferAndReplacementPhases()
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var text = doc.CreateTextNode("abcd"); parent.AppendChild(text);
        var range = new DomRange(doc); range.SetStart(new(text), 3); range.SetEnd(new(text), 4);
        using var subscription = range.ObserveChanges(doc); var signals = 0; doc.PendingRangeChanges = () => signals++;
        var tail = NativeCharacterData.SplitText(text, 2); signals.Should().Be(1); range.Start.Container.Node.Should().BeSameAs(tail);
        subscription.TakePendingChange().Should().BeTrue();
        NativeCharacterData.Normalize(parent); signals.Should().Be(2); range.Start.Should().Be(new BoundaryPoint(new(text), 3));
        range.End.Should().Be(new BoundaryPoint(new(text), 4)); subscription.TakePendingChange().Should().BeTrue();
    }
    [Test]
    public void RangeContentOperationBatchesOtherRangesAndDirectAssociation()
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var a = doc.CreateTextNode("abc"); var b = doc.CreateTextNode("def"); parent.AppendChild(a); parent.AppendChild(b);
        var range = new DomRange(doc); range.SelectNodeContents(new(parent)); var other = new DomRange(doc); other.SelectNodeContents(new(b));
        using var s = range.ObserveChanges(doc); using var t = other.ObserveChanges(doc); var signals = 0; doc.PendingRangeChanges = () => signals++;
        range.ExtractContents(); signals.Should().Be(1); s.TakePendingChange().Should().BeTrue(); t.TakePendingChange().Should().BeTrue();
        parent.ChildCount.Should().Be(0); doc.ChangedRanges.Should().BeNull();
    }
    [Test]
    public void AdoptionKeepsSelectionDocumentSeparateAndBalancesBothOwners()
    {
        var source = Document.CreateHtml(); var destination = Document.CreateHtml(); var root = source.CreateElement("div"); var text = source.CreateTextNode("abcd"); root.AppendChild(text);
        var range = new DomRange(source); range.SelectNodeContents(new(text)); using var subscription = range.ObserveChanges(source);
        var signals = 0; source.PendingRangeChanges = () => signals++;
        destination.AdoptNode(root); signals.Should().Be(0); text.Data = "abcd"; signals.Should().Be(1);
        subscription.TakePendingChange().Should().BeTrue(); range.Collapsed.Should().BeTrue();
        source.RangeOperationDepth.Should().Be(0); destination.RangeOperationDepth.Should().Be(0);
    }
    [Test]
    public void SchedulerFailureStillMarksAllSubscriptionsAndAllowsRecovery()
    {
        var source = Document.CreateHtml(); var destination = Document.CreateHtml(); var text = source.CreateTextNode("abcd");
        var range = new DomRange(source); range.SelectNodeContents(new(text)); using var s = range.ObserveChanges(source); using var t = range.ObserveChanges(destination);
        var failure = new InvalidOperationException("schedule"); var destinationSignals = 0;
        source.PendingRangeChanges = () => throw failure; destination.PendingRangeChanges = () => destinationSignals++;
        Assert.Throws<InvalidOperationException>(() => text.Data = "abcd").Should().BeSameAs(failure);
        s.TakePendingChange().Should().BeTrue(); t.TakePendingChange().Should().BeTrue(); destinationSignals.Should().Be(1);
        source.RangeOperationDepth.Should().Be(0); destination.RangeOperationDepth.Should().Be(0);
        source.PendingRangeChanges = null; range.SelectNodeContents(new(text)); s.TakePendingChange().Should().BeTrue();
    }
    [Test]
    public void NestedMutationScopeDefersSignalsUntilItsOwnerFinishes()
    {
        var doc = Document.CreateHtml(); var range = new DomRange(doc); using var subscription = range.ObserveChanges(doc);
        var signals = 0; doc.PendingRangeChanges = () => signals++;
        using (new RangeMutationScope(doc))
        {
            range.SelectNodeContents(new(doc.CreateTextNode("x"))); signals.Should().Be(0);
            subscription.TakePendingChange().Should().BeFalse();
        }
        signals.Should().Be(1); subscription.TakePendingChange().Should().BeTrue();
    }
}

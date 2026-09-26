#nullable enable
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

[NonParallelizable]
public class NotificationLifetimeTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<RangeChangeSubscription> AbandonedSubscription(DomRange range, Document document)
        => new(range.ObserveChanges(document));

    [Test]
    public void AbandonedSubscriptionsAreWeakAndNewAssociationStillWorks()
    {
        var doc = Document.CreateHtml(); var range = doc.CreateRange();
        var dropped = Enumerable.Range(0, 32).Select(_ => AbandonedSubscription(range, doc)).ToArray();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        foreach (var item in dropped) item.TryGetTarget(out _).Should().BeFalse();
        var signals = 0; doc.PendingRangeChanges = () => signals++;
        using var active = range.ObserveChanges(doc); range.SelectNodeContents(new(doc.CreateTextNode("x")));
        signals.Should().Be(1); active.TakePendingChange().Should().BeTrue();
        GC.KeepAlive(range);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object>[] DroppedLiveObjects(Document document)
    {
        var root = document.CreateElement("div"); var text = document.CreateTextNode("abc"); root.AppendChild(text);
        var range = document.CreateRange(); range.SelectNodeContents(new(text));
        var iterator = new DomNodeIterator(new(root), uint.MaxValue); iterator.Next(null); iterator.Next(null);
        var subscription = range.ObserveChanges(document);
        return [new(root), new(text), new(range), new(iterator), new(subscription)];
    }

    [Test]
    public void RetainedDocumentDoesNotRetainDroppedRangesIteratorsOrDetachedSubtrees()
    {
        var doc = Document.CreateHtml(); var dropped = DroppedLiveObjects(doc);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        foreach (var reference in dropped) reference.TryGetTarget(out _).Should().BeFalse();
        GC.KeepAlive(doc);
    }

    [Test]
    public void NeverSubscribedDataMutationAllocatesNoNotificationOrTrackingStructures()
    {
        var doc = Document.CreateHtml(); var text = doc.CreateTextNode("abc"); text.Data = "abc";
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) text.Data = "abc";
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0); doc.RangeBuckets.Should().BeNull(); doc.IteratorSlots.Should().BeNull(); doc.ChangedRanges.Should().BeNull();
    }

    [Test]
    public void TwoOwnershipScopesFlushAllPendingRangesDespiteOneThrowingSink()
    {
        var first = Document.CreateHtml(); var second = Document.CreateHtml(); var a = first.CreateRange(); var b = second.CreateRange();
        using var x = a.ObserveChanges(first); using var y = b.ObserveChanges(second);
        var failure = new InvalidOperationException("scheduler"); var secondSignals = 0;
        first.PendingRangeChanges = () => throw failure; second.PendingRangeChanges = () => secondSignals++;
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var operation = new RangeMutationScope(first, second);
            a.SelectNodeContents(new(first.CreateTextNode("a"))); b.SelectNodeContents(new(second.CreateTextNode("b")));
            first.RangeOperationDepth.Should().Be(1); second.RangeOperationDepth.Should().Be(1);
        }).Should().BeSameAs(failure);
        first.RangeOperationDepth.Should().Be(0); second.RangeOperationDepth.Should().Be(0);
        first.ChangedRanges.Should().BeNull(); second.ChangedRanges.Should().BeNull(); secondSignals.Should().Be(1);
        x.TakePendingChange().Should().BeTrue(); y.TakePendingChange().Should().BeTrue();
    }
}

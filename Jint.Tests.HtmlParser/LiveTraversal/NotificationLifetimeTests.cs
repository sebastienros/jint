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
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<DomRange>[] DropDenseEndpointBucket(Document document, Node container)
    {
        var live = new DomRange[512];
        var dropped = new WeakReference<DomRange>[live.Length];
        for (var i = 0; i < live.Length; i++)
        {
            live[i] = document.CreateRange();
            live[i].SelectNodeContents(new(container));
            dropped[i] = new(live[i]);
        }
        GC.KeepAlive(live);
        return dropped;
    }

    [Test]
    public void FirstDataWriteReleasesAbandonedDenseBucketAndLaterWritesAllocateNothing()
    {
        var doc = Document.CreateHtml(); var text = doc.CreateTextNode("abc");
        var dropped = DropDenseEndpointBucket(doc, text);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        foreach (var item in dropped) item.TryGetTarget(out _).Should().BeFalse();
        text.Data = "abc";
        text.RangeEndpoints.Should().BeNull(); doc.RangeBuckets.Should().BeNull();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) text.Data = "abc";
        (GC.GetAllocatedBytesForCurrentThread() - before).Should().Be(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InsertionReleasesAbandonedDenseParentBucketAndKeepsLiveHandles(bool append)
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var child = doc.CreateTextNode("abc"); parent.AppendChild(child);
        var dropped = DropDenseEndpointBucket(doc, parent);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        foreach (var item in dropped) item.TryGetTarget(out _).Should().BeFalse();
        parent.InsertBefore(doc.CreateComment("inserted"), append ? null : child);
        parent.RangeEndpoints.Should().BeNull(); doc.RangeBuckets.Should().BeNull();
        // Recreate registrations after release, then exercise indexed handle
        // reassignment and a later removal to detect stale bucket membership.
        var first = doc.CreateRange(); first.SelectNodeContents(new(parent));
        var second = doc.CreateRange(); second.SelectNodeContents(new(parent));
        first.SelectNodeContents(new(child)); parent.RemoveChild(child);
        first.Start.Container.Node.Should().BeSameAs(parent); first.Collapsed.Should().BeTrue();
        second.End.Offset.Should().Be(1);
    }

    [Test]
    public void DenseDeadPruningKeepsSurvivingHandleIndicesCoherent()
    {
        var doc = Document.CreateHtml(); var text = doc.CreateTextNode("abc");
        var dropped = DropDenseEndpointBucket(doc, text);
        var surviving = doc.CreateRange(); surviving.SelectNodeContents(new(text));
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        foreach (var item in dropped) item.TryGetTarget(out _).Should().BeFalse();
        text.Data = "abc"; surviving.Collapsed.Should().BeTrue(); text.RangeEndpoints!.Entries.Count.Should().Be(2);
        // Moving each surviving handle after swap removal must clear the old
        // bucket rather than removing a different entry through a stale index.
        surviving.SelectNodeContents(new(doc)); text.RangeEndpoints.Should().BeNull();
        doc.RangeEndpoints!.Entries.Count.Should().Be(2); doc.RangeBuckets!.Count.Should().Be(1);
    }

}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class RegistrationWorkTests
{
    [Test]
    public void DenseEndpointBucketRegistrationHasLinearWork()
    {
        var small = RegisterRanges(512);
        var large = RegisterRanges(1024);
        large.Should().BeLessThanOrEqualTo(2 * small + 200);
        large.Should().BeLessThanOrEqualTo(36 * 1024);
    }

    private static int RegisterRanges(int count)
    {
        var document = Document.CreateHtml();
        var ranges = new DomRange[count];
        var work = 0;
        for (var i = 0; i < ranges.Length; i++)
            ranges[i] = new DomRange(document, _ => work++);
        document.RangeBuckets!.Count.Should().Be(1);
        document.RangeEndpoints!.Entries.Count.Should().Be(2 * count);
        // Reassignment exercises constant-time handle removal from a dense bucket.
        var text = document.CreateTextNode("x");
        foreach (var range in ranges) range.SelectNodeContents(new(text));
        text.Data = "x";
        foreach (var range in ranges) range.Collapsed.Should().BeTrue();
        GC.KeepAlive(ranges);
        return work;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DenseIteratorRootRegistrationHasLinearWork(bool attributeRoot)
    {
        var small = RegisterIterators(512, attributeRoot);
        var large = RegisterIterators(1024, attributeRoot);
        large.Should().BeLessThanOrEqualTo(2 * small + 100);
        large.Should().BeLessThanOrEqualTo(18 * 1024);
    }

    private static int RegisterIterators(int count, bool attributeRoot)
    {
        var document = Document.CreateHtml();
        var identity = attributeRoot ? new DomNodeIdentity(document.CreateAttribute("x")) : new DomNodeIdentity(document);
        var iterators = new DomNodeIterator[count];
        var work = 0;
        for (var i = 0; i < iterators.Length; i++)
            iterators[i] = new DomNodeIterator(identity, uint.MaxValue, _ => work++);
        document.IteratorSlots!.Count.Should().Be(count);
        (identity.Node?.RootIterators ?? identity.Attribute!.RootIterators)!.Count.Should().Be(count);
        foreach (var iterator in iterators) iterator.Next(null, default)!.Value.Should().Be(identity);
        GC.KeepAlive(iterators);
        return work;
    }
}

public class SubscriptionRegistrationWorkTests
{
    [Test]
    public void DenseSubscriptionRegistrationHasLinearWork()
    {
        var small = RegisterSubscriptions(512);
        var large = RegisterSubscriptions(1024);
        large.Should().BeLessThanOrEqualTo(2 * small + 100);
        large.Should().BeLessThanOrEqualTo(9 * 1024);
    }
    private static int RegisterSubscriptions(int count)
    {
        var document = Document.CreateHtml(); var range = document.CreateRange();
        var subscriptions = new RangeChangeSubscription[count]; var work = 0;
        for (var i = 0; i < count; i++) subscriptions[i] = range.ObserveChanges(document, _ => work++);
        range.SelectNodeContents(new(document.CreateTextNode("x")));
        foreach (var subscription in subscriptions) subscription.TakePendingChange().Should().BeTrue();
        foreach (var subscription in subscriptions) subscription.Disconnect();
        using var active = range.ObserveChanges(document);
        range.Collapse(true); active.TakePendingChange().Should().BeTrue();
        foreach (var subscription in subscriptions) subscription.TakePendingChange().Should().BeFalse();
        GC.KeepAlive(subscriptions);
        return work;
    }
}

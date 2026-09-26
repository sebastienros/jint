#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class MutationNotificationOrderTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ReentrantAttributeTransitionsReachDrainingSubscriberInFifoOrder(bool reverse)
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        element.SetAttribute("x", "before");
        var options = new MutationObserverOptions { Attributes = true, AttributeOldValue = true };
        using var registeredFirst = document.ObserveMutations(element, options);
        using var registeredSecond = document.ObserveMutations(element, options);
        var reentrant = reverse ? registeredSecond : registeredFirst;
        var draining = reverse ? registeredFirst : registeredSecond;
        var delivered = new List<MutationRecord>();
        var reentries = 0;
        reentrant.PendingRecord = _ =>
        {
            reentrant.PendingRecord = null;
            reentries++;
            element.SetAttribute("x", "inner");
            element.RemoveAttribute("x");
            element.SetAttribute("x", "added");
        };
        draining.PendingRecord = subscription =>
        {
            var records = subscription.TakeRecords();
            records.Should().NotBeEmpty("a trailing signal must be skipped after a nested callback drains the queue");
            delivered.AddRange(records);
        };
        element.SetAttribute("x", "outer");
        reentries.Should().Be(1);
        delivered.Select(record => record.AttributeNewValue).Should().Equal("outer", "inner", null, "added");
        delivered.Select(record => record.OldValue).Should().Equal("before", "outer", "inner", null);
        reentrant.TakeRecords().Select(record => record.AttributeNewValue).Should().Equal("outer", "inner", null, "added");
        draining.TakeRecords().Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EarlierCallbackDrainingOrDisconnectingLaterSubscriberSkipsTrailingSignal(bool disconnect)
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        using var first = document.ObserveMutations(element, new MutationObserverOptions { Attributes = true });
        using var later = document.ObserveMutations(element, new MutationObserverOptions { Attributes = true });
        var notifications = 0;
        later.PendingRecord = _ => notifications++;
        first.PendingRecord = _ =>
        {
            if (disconnect) later.Disconnect();
            else later.TakeRecords().Select(record => record.AttributeNewValue).Should().Equal("outer");
        };
        element.SetAttribute("x", "outer");
        notifications.Should().Be(0);
        later.TakeRecords().Should().BeEmpty();
        first.TakeRecords().Select(record => record.AttributeNewValue).Should().Equal("outer");
    }

    [Test]
    public void ReentrantCharacterDataQueuesOuterRecordBeforeNestedRecord()
    {
        var document = Document.CreateXml();
        var text = document.CreateTextNode("before");
        var options = new MutationObserverOptions { CharacterData = true, CharacterDataOldValue = true };
        using var first = document.ObserveMutations(text, options);
        using var later = document.ObserveMutations(text, options);
        var delivered = new List<MutationRecord>();
        first.PendingRecord = _ =>
        {
            first.PendingRecord = null;
            text.Data = "inner";
        };
        later.PendingRecord = subscription => delivered.AddRange(subscription.TakeRecords());
        text.Data = "outer";
        delivered.Select(record => record.OldValue).Should().Equal("before", "outer");
        first.TakeRecords().Select(record => record.OldValue).Should().Equal("before", "outer");
        text.Data.Should().Be("inner");
    }

    [Test]
    public void ReentrantChildListQueuesOuterRecordBeforeNestedRecord()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        var outer = document.CreateElement("outer");
        var inner = document.CreateElement("inner");
        var options = new MutationObserverOptions { ChildList = true };
        using var first = document.ObserveMutations(element, options);
        using var later = document.ObserveMutations(element, options);
        var delivered = new List<MutationRecord>();
        first.PendingRecord = _ =>
        {
            first.PendingRecord = null;
            element.AppendChild(inner);
        };
        later.PendingRecord = subscription => delivered.AddRange(subscription.TakeRecords());
        element.AppendChild(outer);
        delivered.SelectMany(record => record.AddedNodes).Should().Equal(outer, inner);
        first.TakeRecords().SelectMany(record => record.AddedNodes).Should().Equal(outer, inner);
        element.ChildNodes.Should().Equal(outer, inner);
    }

    [Test]
    public void PendingCallbackExceptionPropagatesAfterAllMatchingRecordsAreQueued()
    {
        var document = Document.CreateXml();
        var element = document.CreateElement("root");
        var options = new MutationObserverOptions { Attributes = true };
        using var first = document.ObserveMutations(element, options);
        using var later = document.ObserveMutations(element, options);
        var exception = new InvalidOperationException("host scheduling failure");
        var laterNotifications = 0;
        first.PendingRecord = _ => throw exception;
        later.PendingRecord = _ => laterNotifications++;
        Caught.Exception(() => element.SetAttribute("x", "outer")).Should().BeSameAs(exception);
        laterNotifications.Should().Be(0);
        first.TakeRecords().Select(record => record.AttributeNewValue).Should().Equal("outer");
        later.TakeRecords().Select(record => record.AttributeNewValue).Should().Equal("outer");
        element.GetAttribute("x").Should().Be("outer");
    }
}

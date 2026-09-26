#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class DeferredMetaNotificationTests
{
    private static Element Meta(Document document, string content)
    {
        var meta = document.CreateElement("meta");
        meta.SetAttribute("http-equiv", "default-style"); meta.SetAttribute("content", content);
        return meta;
    }

    private static (Document Document, Element Root, Element First, Element Second, MutationSubscription A,
        MutationSubscription B, OperationCanceledException Failure) FailedPrefix(string operation)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html"); document.AppendChild(root);
        var old = document.CreateElement("old"); root.AppendChild(old);
        var fragment = document.CreateDocumentFragment();
        var first = Meta(document, "first"); var second = Meta(document, "second");
        fragment.AppendChild(first); fragment.AppendChild(second);
        var options = new MutationObserverOptions { ChildList = true, Subtree = true };
        var a = document.ObserveMutations(root, options); a.CaptureHtmlMetaInsertions = true;
        var b = document.ObserveMutations(root, options); b.CaptureHtmlMetaInsertions = true;
        var failure = new OperationCanceledException("second capture canceled");
        var captures = 0;
        a.CreateCaptureWork = () =>
        {
            Action<int>? checkpoint = ++captures == 2 ? _ => throw failure : null;
            return (checkpoint, default);
        };
        var unexpected = new InvalidOperationException("notification ran during unwinding");
        a.PendingRecord = _ => throw unexpected;
        b.PendingRecord = _ => throw unexpected;
        void Insert()
        {
            if (operation == "append") root.AppendChild(fragment);
            else if (operation == "replace") root.ReplaceChild(fragment, old);
            else root.ReplaceChildren(fragment);
        }
        Assert.Throws<OperationCanceledException>(Insert).Should().BeSameAs(failure);
        first.ParentNode.Should().BeSameAs(root);
        second.ParentNode.Should().BeNull();
        if (operation == "append") old.ParentNode.Should().BeSameAs(root);
        else old.ParentNode.Should().BeNull();
        return (document, root, first, second, a, b, failure);
    }

    [TestCase("append")]
    [TestCase("replace")]
    [TestCase("replace-all")]
    public void CaptureFailurePublishesOnlyCommittedPrefixAndFlushPreservesCursorAcrossCallbackException(string operation)
    {
        var state = FailedPrefix(operation);
        using var a = state.A; using var b = state.B;
        IReadOnlyList<MutationRecord>? firstRecords = null, secondRecords = null;
        var notificationFailure = new InvalidOperationException("later host notification failed");
        var aNotifications = 0; var bNotifications = 0;
        a.PendingRecord = subscription =>
        {
            aNotifications++;
            firstRecords = subscription.TakeRecords();
            throw notificationFailure;
        };
        b.PendingRecord = subscription => { bNotifications++; secondRecords = subscription.TakeRecords(); };
        Assert.Throws<InvalidOperationException>(() => state.Document.FlushPendingMutationNotifications()).Should().BeSameAs(notificationFailure);
        aNotifications.Should().Be(1); bNotifications.Should().Be(0);
        var firstRecord = firstRecords!.Single();
        firstRecord.AddedNodes.Should().Equal(state.First);
        firstRecord.HtmlMetaInsertions.Select(fact => fact.Content).Should().Equal("first");
        firstRecord.HtmlMetaInsertions.Single().Document.Should().BeSameAs(state.Document);
        if (operation != "append") firstRecord.RemovedNodes.Should().ContainSingle();
        state.Document.FlushPendingMutationNotifications();
        aNotifications.Should().Be(1); bNotifications.Should().Be(1);
        var secondRecord = secondRecords!.Single();
        secondRecord.AddedNodes.Should().Equal(state.First);
        secondRecord.HtmlMetaInsertions.Should().BeSameAs(firstRecord.HtmlMetaInsertions);
        state.Document.FlushPendingMutationNotifications();
        bNotifications.Should().Be(1);
    }

    [TestCase("drain")]
    [TestCase("disconnect")]
    [TestCase("reenter")]
    public void FlushSkipsDrainedOrDisconnectedQueuesAndNestedFlushDoesNotRepeatCursor(string action)
    {
        var state = FailedPrefix("append");
        using var a = state.A; using var b = state.B;
        var delivered = new List<MutationRecord>();
        var bNotifications = 0;
        b.PendingRecord = subscription => { bNotifications++; delivered.AddRange(subscription.TakeRecords()); };
        a.PendingRecord = subscription =>
        {
            a.PendingRecord = null;
            subscription.TakeRecords();
            if (action == "drain") delivered.AddRange(b.TakeRecords());
            else if (action == "disconnect") b.Disconnect();
            else
            {
                state.Document.FlushPendingMutationNotifications();
                state.Root.AppendChild(Meta(state.Document, "reentered"));
            }
        };
        state.Document.FlushPendingMutationNotifications();
        bNotifications.Should().Be(action == "reenter" ? 1 : 0);
        delivered.SelectMany(record => record.HtmlMetaInsertions).Select(fact => fact.Content).Should().Equal(
            action == "reenter" ? new[] { "first", "reentered" } : action == "drain" ? new[] { "first" } : Array.Empty<string>());
        state.Document.FlushPendingMutationNotifications();
        b.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void DisposalAndDocumentCleanupReleaseDeferredScheduling()
    {
        var state = FailedPrefix("append");
        state.A.Dispose(); state.B.Dispose();
        state.Document.FlushPendingMutationNotifications();
        state.Document.ClearPendingMutationNotifications();
        state.Document.FlushPendingMutationNotifications();
        state.A.CreateCaptureWork.Should().BeNull();
        state.A.CaptureHtmlMetaInsertions.Should().BeFalse();
    }
}

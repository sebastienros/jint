#nullable enable
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

[NonParallelizable]
public class MutationLifetimeTests
{
    [Test]
    public void HoldingSubscriptionAloneDoesNotRetainTargetOrDocument()
    {
        var (subscription, target, document) = CreateDetachedRegistration();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        target.TryGetTarget(out _).Should().BeFalse();
        document.TryGetTarget(out _).Should().BeFalse();
        subscription.TakeRecords().Should().BeEmpty();
        subscription.Disconnect();
    }

    [Test]
    public void DrainedQueueReleasesItsNodeReferences()
    {
        var (subscription, target) = CreateAndDrainQueuedRecord();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        target.TryGetTarget(out _).Should().BeFalse();
        subscription.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MutationSubscription, WeakReference<Node>, WeakReference<Document>) CreateDetachedRegistration()
    {
        var document = Document.CreateXml();
        var target = document.CreateElement("target");
        var subscription = document.ObserveMutations(target, new MutationObserverOptions { Attributes = true });
        return (subscription, new WeakReference<Node>(target), new WeakReference<Document>(document));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MutationSubscription, WeakReference<Node>) CreateAndDrainQueuedRecord()
    {
        var document = Document.CreateXml();
        var target = document.CreateElement("target");
        var subscription = document.ObserveMutations(target, new MutationObserverOptions { Attributes = true });
        target.SetAttribute("x", "value");
        subscription.TakeRecords().Should().ContainSingle();
        return (subscription, new WeakReference<Node>(target));
    }
}

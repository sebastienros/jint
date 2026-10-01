using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class MutationConnectivityTests
{
    [Test]
    public void DetachedShadowRecordsRemainDisconnectedAfterTheHostIsInserted()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        using var observer = document.ObserveMutations(shadow, new MutationObserverOptions { ChildList = true, Subtree = true });
        shadow.AppendChild(document.CreateElement("span"));
        document.AppendChild(host);

        observer.TakeRecords().Single().TargetWasConnected.Should().BeFalse();
    }

    [Test]
    public void ShadowHostMovesPreserveEachMutationsOriginalConnectedness()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        using var observer = document.ObserveMutations(shadow, new MutationObserverOptions { ChildList = true });
        var child = document.CreateElement("span");
        shadow.AppendChild(child);
        document.RemoveChild(host);
        shadow.RemoveChild(child);
        document.AppendChild(host);
        shadow.AppendChild(child);
        document.RemoveChild(host);

        observer.TakeRecords().Select(record => record.TargetWasConnected).Should().Equal(true, false, true);
    }

    [Test]
    public void TransientRemovedSubtreeRecordsUseTheirDetachedTarget()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var branch = document.CreateElement("div");
        root.AppendChild(branch);
        using var observer = document.ObserveMutations(document, new MutationObserverOptions { ChildList = true, Subtree = true });
        root.RemoveChild(branch);
        branch.AppendChild(document.CreateElement("span"));
        root.AppendChild(branch);

        observer.TakeRecordsForDelivery().Select(record => record.TargetWasConnected).Should().Equal(true, false, true);
    }

    [Test]
    public void NestedShadowConnectivityDoesNotWidenObserverScope()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var outer = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var nested = document.CreateElement("section");
        outer.AppendChild(nested);
        var inner = ShadowTree.Attach(nested, new ShadowRootInit(ShadowRootMode.Open), default);
        using var outside = document.ObserveMutations(document, new MutationObserverOptions { ChildList = true, Subtree = true });
        using var inside = document.ObserveMutations(inner, new MutationObserverOptions { ChildList = true });
        inner.AppendChild(document.CreateElement("span"));

        outside.TakeRecords().Should().BeEmpty();
        inside.TakeRecords().Single().TargetWasConnected.Should().BeTrue();
    }
}

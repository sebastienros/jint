#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Mutations;

public sealed class HtmlMetaInsertionTests
{
    private static (Document Document, Element Root) Create()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("html");
        document.AppendChild(root);
        return (document, root);
    }

    private static Element Meta(Document document, string content)
    {
        var meta = document.CreateElement("meta");
        meta.SetAttribute("http-equiv", "DeFaUlT-StYlE");
        meta.SetAttribute("content", content);
        return meta;
    }

    private static MutationSubscription Observe(Document document, Node root, bool capture = true)
    {
        var subscription = document.ObserveMutations(root,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        subscription.CaptureHtmlMetaInsertions = capture;
        return subscription;
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void EarlierObserverCannotRewriteInsertionFactsBeforeLaterDrain(int change)
    {
        var (document, root) = Create();
        var content = new string(' ', 2) + "original" + new string(' ', 2);
        var meta = Meta(document, content);
        using var first = Observe(document, root);
        using var later = Observe(document, root);
        first.PendingRecord = _ =>
        {
            first.PendingRecord = null;
            if (change == 0) meta.SetAttribute("content", "changed");
            else if (change == 1) meta.SetAttribute("http-equiv", "different");
            else if (change == 2) root.RemoveChild(meta);
            else Document.CreateHtml().AdoptNode(meta);
        };
        root.AppendChild(meta);
        var firstRecords = first.TakeRecords();
        var laterRecords = later.TakeRecords();
        var fact = laterRecords.SelectMany(record => record.HtmlMetaInsertions).Single();
        fact.Document.Should().BeSameAs(document);
        fact.Content.Should().BeSameAs(content);
        firstRecords[0].HtmlMetaInsertions.Should().BeSameAs(laterRecords[0].HtmlMetaInsertions);
    }

    [TestCase("append")]
    [TestCase("replace")]
    [TestCase("replace-all")]
    public void FragmentAndReplacementActionsFollowSuccessfulLinkOrder(string operation)
    {
        var (document, root) = Create();
        var old = document.CreateElement("old");
        root.AppendChild(old);
        var fragment = document.CreateDocumentFragment();
        var first = Meta(document, "first");
        var second = Meta(document, "second");
        fragment.AppendChild(first); fragment.AppendChild(second);
        using var subscription = Observe(document, root);
        if (operation == "append") root.AppendChild(fragment);
        else if (operation == "replace") root.ReplaceChild(fragment, old);
        else root.ReplaceChildren(fragment);
        var record = subscription.TakeRecords().Single();
        record.AddedNodes.Should().Equal(first, second);
        record.HtmlMetaInsertions.Select(fact => fact.Content).Should().Equal("first", "second");
        record.HtmlMetaInsertions.Select(fact => fact.Document).Should().OnlyContain(owner => ReferenceEquals(owner, document));
        if (operation != "append") record.RemovedNodes.Should().Equal(old);
    }

    [Test]
    public void SourceDetachCallbackPrecedesCaptureAndReinsertionCreatesNewAction()
    {
        var (document, root) = Create();
        var source = document.CreateElement("source");
        root.AppendChild(source);
        var meta = Meta(document, "before detach");
        source.AppendChild(meta);
        using var sourceSubscription = document.ObserveMutations(source, new MutationObserverOptions { ChildList = true });
        sourceSubscription.PendingRecord = _ => meta.SetAttribute("content", "after detach");
        using var target = Observe(document, root);
        root.AppendChild(meta);
        target.TakeRecords().SelectMany(record => record.HtmlMetaInsertions).Select(fact => fact.Content).Should().Equal("after detach");
        root.RemoveChild(meta);
        target.TakeRecords().SelectMany(record => record.HtmlMetaInsertions).Should().BeEmpty();
        meta.SetAttribute("content", "reinserted");
        root.AppendChild(meta);
        target.TakeRecords().SelectMany(record => record.HtmlMetaInsertions).Select(fact => fact.Content).Should().Equal("reinserted");
    }

    [Test]
    public void AttachedShadowDescendantsAreIncludedAndTemplateContentsAreExcluded()
    {
        var (document, root) = Create();
        var host = document.CreateElement("div");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed), default);
        shadow.AppendChild(Meta(document, "shadow"));
        host.AppendChild(Meta(document, "light"));
        var template = document.CreateElement("template");
        var content = template.TemplateContent!;
        content.AppendChild(Meta(content.OwnerDocument!, "inert"));
        host.AppendChild(template);
        using var subscription = Observe(document, root);
        root.AppendChild(host);
        subscription.TakeRecords().SelectMany(record => record.HtmlMetaInsertions).Select(fact => fact.Content)
            .Should().Equal("shadow", "light");
    }

    [Test]
    public void OnlyExactHtmlDefaultStyleWithPresentNonemptyContentProducesFacts()
    {
        var (document, root) = Create();
        var container = document.CreateElement("div");
        container.AppendChild(Meta(document, " "));
        container.AppendChild(Meta(document, ""));
        var missing = Meta(document, "missing"); missing.RemoveAttribute("content"); container.AppendChild(missing);
        var other = Meta(document, "other"); other.SetAttribute("http-equiv", " default-style"); container.AppendChild(other);
        var foreign = document.CreateElementNS(Namespaces.Svg, "meta");
        foreign.SetAttribute("http-equiv", "default-style"); foreign.SetAttribute("content", "foreign"); container.AppendChild(foreign);
        using var subscription = Observe(document, root);
        root.AppendChild(container);
        subscription.TakeRecords().SelectMany(record => record.HtmlMetaInsertions).Select(fact => fact.Content).Should().Equal(" ");
    }

    [Test]
    public void CaptureWorkBoundsWideAttributesAndMutationInvalidatesReadBeforeLink()
    {
        var (document, root) = Create();
        var meta = Meta(document, "original");
        for (var i = 0; i < 1024; i++) meta.SetAttribute("x" + i, "value");
        using var subscription = Observe(document, root);
        using var cancellation = new CancellationTokenSource();
        subscription.CreateCaptureWork = () => (units => { if (units >= 256) cancellation.Cancel(); }, cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => root.AppendChild(meta));
        meta.ParentNode.Should().BeNull();
        subscription.TakeRecords().Should().BeEmpty();
        subscription.CreateCaptureWork = () => (_ => meta.SetAttribute("content", "reentrant"), default);
        Assert.Throws<InvalidOperationException>(() => root.AppendChild(meta));
        meta.ParentNode.Should().BeNull();
        subscription.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void CheckpointExceptionKeepsItsIdentityAndPreventsDestinationLink()
    {
        var (document, root) = Create();
        var meta = Meta(document, "value");
        using var subscription = Observe(document, root);
        var sentinel = new InvalidOperationException("host budget sentinel");
        subscription.CreateCaptureWork = () => (_ => throw sentinel, default);
        Assert.Throws<InvalidOperationException>(() => root.AppendChild(meta)).Should().BeSameAs(sentinel);
        meta.ParentNode.Should().BeNull();
        subscription.TakeRecords().Should().BeEmpty();
        document.FlushPendingMutationNotifications();
    }

    [Test]
    public void DirectSingleReplacementCarriesInsertionFactAndRemovalIsNotAnAction()
    {
        var (document, root) = Create();
        var old = document.CreateElement("old"); root.AppendChild(old);
        var replacement = Meta(document, "replacement");
        using var subscription = Observe(document, root);
        root.ReplaceChild(replacement, old);
        var record = subscription.TakeRecords().Single();
        record.AddedNodes.Should().Equal(replacement);
        record.RemovedNodes.Should().Equal(old);
        record.HtmlMetaInsertions.Select(fact => fact.Content).Should().Equal("replacement");
        root.ReplaceChildren();
        subscription.TakeRecords().Single().HtmlMetaInsertions.Should().BeEmpty();
    }

    [Test]
    public void UnrequestedUnmatchedAndDisconnectedCaptureLanesStayCold()
    {
        var (document, root) = Create();
        using var ordinary = Observe(document, root, capture: false);
        ordinary.CreateCaptureWork = () => throw new InvalidOperationException("cold provider invoked");
        document.MayCaptureHtmlMetaInsertions.Should().BeFalse();
        root.AppendChild(Meta(document, "ordinary"));
        ordinary.TakeRecords().Single().HtmlMetaInsertions.Should().BeEmpty();
        using var detached = Observe(document, document.CreateElement("div"));
        detached.CreateCaptureWork = () => throw new InvalidOperationException("unmatched provider invoked");
        root.AppendChild(Meta(document, "unmatched"));
        ordinary.TakeRecords().Single().HtmlMetaInsertions.Should().BeEmpty();
        var detachedRoot = document.CreateElement("div");
        using var unconnected = Observe(document, detachedRoot);
        unconnected.CreateCaptureWork = () => throw new InvalidOperationException("disconnected provider invoked");
        detachedRoot.AppendChild(Meta(document, "disconnected"));
        unconnected.TakeRecords().Single().HtmlMetaInsertions.Should().BeEmpty();
        document.FlushPendingMutationNotifications();
        typeof(Document).GetField("_pendingMutationHead",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(document).Should().BeNull();
    }
}

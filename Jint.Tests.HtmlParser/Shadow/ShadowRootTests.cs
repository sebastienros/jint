#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Shadow;

public class ShadowRootTests
{
    [Test]
    public void AttachmentKeepsExactRootIdentityAndChecksInputsBeforeMutation()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var registry = new CustomElementRegistryIdentity(true);
        var init = new ShadowRootInit(ShadowRootMode.Closed, true, true, SlotAssignmentMode.Manual, true);
        var root = ShadowTree.Attach(host, init, new ShadowAttachmentContext(registry, false, true));

        host.AttachedShadowRoot.Should().BeSameAs(root);
        host.OpenShadowRoot.Should().BeNull();
        root.Host.Should().BeSameAs(host);
        ((DocumentFragment)root).Host.Should().BeSameAs(host);
        root.ParentNode.Should().BeNull();
        root.PreviousSibling.Should().BeNull();
        root.NextSibling.Should().BeNull();
        root.OwnerDocument.Should().BeSameAs(document);
        root.NodeType.Should().Be(NodeType.DocumentFragment);
        host.ChildCount.Should().Be(0);
        root.Mode.Should().Be(ShadowRootMode.Closed);
        root.DelegatesFocus.Should().BeTrue();
        root.Serializable.Should().BeTrue();
        root.Clonable.Should().BeTrue();
        root.SlotAssignment.Should().Be(SlotAssignmentMode.Manual);
        root.AvailableToElementInternals.Should().BeTrue();
        root.Declarative.Should().BeFalse();
        root.KeepCustomElementRegistryNull.Should().BeFalse();
        root.CustomElementRegistry.Should().BeSameAs(registry);

        Assert.Throws<ArgumentNullException>(() => ShadowTree.Attach(null!, init, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShadowTree.Attach(host,
            new ShadowRootInit((ShadowRootMode)99), default));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Open, SlotAssignment: (SlotAssignmentMode)99), default));
        Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(host, init, default))!.Name,
            Is.EqualTo("NotSupportedError"));
        host.AttachedShadowRoot.Should().BeSameAs(root);

        var openHost = document.CreateElement("span");
        var open = ShadowTree.Attach(openHost, new ShadowRootInit(ShadowRootMode.Open), default);
        openHost.OpenShadowRoot.Should().BeSameAs(open);
        open.AvailableToElementInternals.Should().BeFalse();
    }

    [Test]
    public void HostNamesFollowCurrentCustomNameRulesAndResolvedDisableShadow()
    {
        var document = Document.CreateXml();
        foreach (var name in new[] { "article", "h6", "x-foo", "a-😍", "a-\U00010000", "a-=" })
        {
            var host = document.CreateElementNS(Namespaces.Html, name);
            ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default).Host.Should().BeSameAs(host);
        }

        foreach (var name in new[] { "a", "select", "font-face", "annotation-xml", "a-B", "a-/", "a- ", "_a-b" })
        {
            var host = document.CreateParsedElement(Namespaces.Html, name, null);
            Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(host,
                new ShadowRootInit(ShadowRootMode.Open), default))!.Name, Is.EqualTo("NotSupportedError"));
            host.AttachedShadowRoot.Should().BeNull();
        }

        var svg = document.CreateElementNS(Namespaces.Svg, "x-host");
        Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(svg,
            new ShadowRootInit(ShadowRootMode.Open), default))!.Name, Is.EqualTo("NotSupportedError"));
        var disabled = document.CreateElementNS(Namespaces.Html, "x-host");
        Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(disabled,
            new ShadowRootInit(ShadowRootMode.Open), new ShadowAttachmentContext(null, true, false)))!.Name,
            Is.EqualTo("NotSupportedError"));
        disabled.AttachedShadowRoot.Should().BeNull();
    }

    [Test]
    public void DeclarativeTemplateUsesTheRealRootAndImperativeReusePreservesFlags()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("section");
        var template = document.CreateElement("template");
        var oldContent = template.TemplateContent!;
        var registry = new CustomElementRegistryIdentity(true);
        var root = ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Open, true, true, SlotAssignmentMode.Manual, true),
            new ShadowAttachmentContext(registry, false, false));
        ShadowTree.SetDeclarativeTemplateContent(template, root, true);
        template.TemplateContent.Should().BeSameAs(root);
        oldContent.Host.Should().BeSameAs(template);
        root.Host.Should().BeSameAs(host);
        root.OwnerDocument.Should().BeSameAs(document);
        root.Declarative.Should().BeTrue();
        root.AvailableToElementInternals.Should().BeTrue();
        root.KeepCustomElementRegistryNull.Should().BeTrue();
        root.AppendChild(document.CreateElement("span"));
        root.AppendChild(document.CreateTextNode("tail"));

        Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Closed), default))!.Name, Is.EqualTo("NotSupportedError"));
        root.ChildCount.Should().Be(2);

        var reused = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        reused.Should().BeSameAs(root);
        reused.ChildCount.Should().Be(0);
        reused.Declarative.Should().BeFalse();
        reused.DelegatesFocus.Should().BeTrue();
        reused.Serializable.Should().BeTrue();
        reused.Clonable.Should().BeTrue();
        reused.SlotAssignment.Should().Be(SlotAssignmentMode.Manual);
        reused.CustomElementRegistry.Should().BeSameAs(registry);
        reused.KeepCustomElementRegistryNull.Should().BeTrue();
        Assert.That(Assert.Throws<DomException>(() => ShadowTree.Attach(host,
            new ShadowRootInit(ShadowRootMode.Open), default))!.Name, Is.EqualTo("NotSupportedError"));
    }

    [Test]
    public void DeclarativeSetterRejectsPublishedOrAlteredTemplatesBeforeChanges()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var template = document.CreateElement("template");
        template.TemplateContent!.AppendChild(template.TemplateContent.OwnerDocument!.CreateTextNode("old"));
        Assert.Throws<InvalidOperationException>(() => ShadowTree.SetDeclarativeTemplateContent(template, root, false));
        template.TemplateContent.Should().NotBeSameAs(root);
        root.Declarative.Should().BeFalse();

        var published = document.CreateElement("template");
        host.AppendChild(published);
        Assert.Throws<InvalidOperationException>(() => ShadowTree.SetDeclarativeTemplateContent(published, root, false));
        published.TemplateContent.Should().NotBeSameAs(root);
    }

    [Test]
    public void OrdinaryAndComposedRootsAndConnectivityStayDistinct()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed), default);
        var child = document.CreateElement("span");
        root.AppendChild(child);
        ShadowTree.GetRoot(child, false, default).Should().BeSameAs(root);
        ShadowTree.GetRoot(child, true, default).Should().BeSameAs(host);
        ShadowTree.IsConnected(child, default).Should().BeFalse();
        document.AppendChild(host);
        ShadowTree.GetRoot(child, false, default).Should().BeSameAs(root);
        ShadowTree.GetRoot(child, true, default).Should().BeSameAs(document);
        ShadowTree.IsConnected(child, default).Should().BeTrue();

        var template = document.CreateElement("template");
        var inertChild = template.TemplateContent!.OwnerDocument!.CreateTextNode("inert");
        template.TemplateContent.AppendChild(inertChild);
        document.DocumentElement!.AppendChild(template);
        ShadowTree.GetRoot(inertChild, true, default).Should().BeSameAs(template.TemplateContent);
        ShadowTree.IsConnected(inertChild, default).Should().BeFalse();

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ShadowTree.GetRoot(child, false, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => ShadowTree.IsConnected(child, canceled.Token));
    }

    [Test]
    public void HostInclusiveCyclesCrossShadowAndTemplateBoundaries()
    {
        var document = Document.CreateHtml();
        var outer = document.CreateElement("div");
        var outerRoot = ShadowTree.Attach(outer, new ShadowRootInit(ShadowRootMode.Open), default);
        Assert.That(Assert.Throws<DomException>(() => outerRoot.AppendChild(outer))!.Name,
            Is.EqualTo("HierarchyRequestError"));
        var inner = document.CreateElement("section");
        outerRoot.AppendChild(inner);
        var innerRoot = ShadowTree.Attach(inner, new ShadowRootInit(ShadowRootMode.Open), default);
        Assert.That(Assert.Throws<DomException>(() => innerRoot.AppendChild(outer))!.Name,
            Is.EqualTo("HierarchyRequestError"));
        var template = document.CreateElement("template");
        innerRoot.AppendChild(template);
        Assert.That(Assert.Throws<DomException>(() => template.TemplateContent!.AppendChild(outer))!.Name,
            Is.EqualTo("HierarchyRequestError"));
        outer.ParentNode.Should().BeNull();
        outerRoot.ChildCount.Should().Be(1);
        innerRoot.ChildCount.Should().Be(1);
    }

    [Test]
    public void DeepRootAscentPollsCancellationDuringAndAtTheFinalStep()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var leaf = (Node)root;
        for (var i = 0; i < 20_000; i++)
        {
            var child = document.CreateElement("span");
            leaf.AppendChild(child);
            leaf = child;
        }

        ShadowTree.GetRoot(leaf, false, default).Should().BeSameAs(root);
        ShadowTree.GetRoot(leaf, true, default).Should().BeSameAs(host);
        using var canceledMidWalk = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => ShadowTree.GetRoot(leaf, true,
            steps =>
            {
                if (steps == 512) canceledMidWalk.Cancel();
            }, canceledMidWalk.Token));

        using var canceledAtLastPoll = new CancellationTokenSource();
        var shortLeaf = (Node)root;
        for (var i = 0; i < 256; i++)
        {
            shortLeaf = shortLeaf.FirstChild!;
        }

        Assert.Throws<OperationCanceledException>(() => ShadowTree.GetRoot(shortLeaf, false,
            steps =>
            {
                if (steps == 256) canceledAtLastPoll.Cancel();
            }, canceledAtLastPoll.Token));
    }
}

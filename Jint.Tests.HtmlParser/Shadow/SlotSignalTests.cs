#nullable enable
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Shadow;

[NonParallelizable]
public class SlotSignalTests
{
    [Test]
    public void NamedAndFallbackMutationsSignalAtIntermediateSteps()
    {
        var document = Document.CreateHtml();
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        var light = document.CreateElement("span");
        host.AppendChild(light);
        signals.Should().ContainSingle().Which.Should().BeSameAs(slot);
        signals.Clear();

        host.ReplaceChildren(light);
        signals.Should().Equal(slot, slot);
        signals.Clear();
        host.RemoveChild(light);
        signals.Should().ContainSingle().Which.Should().BeSameAs(slot);
        signals.Clear();

        var fallback = document.CreateTextNode("fallback");
        slot.AppendChild(fallback);
        slot.ReplaceChildren(fallback);
        signals.Should().Equal(slot, slot, slot);
        document.SlotChangeSignal = null;
        signals.Clear();
        slot.RemoveChild(fallback);
        signals.Should().BeEmpty();
    }

    [Test]
    public void RemovingFirstDuplicateSignalsSurvivorThenDetachedSlot()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var first = document.CreateElement("slot");
        var second = document.CreateElement("slot");
        root.AppendChild(first);
        root.AppendChild(second);
        var light = document.CreateElement("span");
        host.AppendChild(light);
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;
        root.RemoveChild(first);

        signals.Should().Equal(second, first);
        SlotAssignment.AssignedNodes(first, false, default).Should().BeEmpty();
        SlotAssignment.AssignedNodes(second, false, default).Should().ContainSingle().Which.Should().BeSameAs(light);
        SlotAssignment.GetAssignedSlot(light).Should().BeSameAs(second);
    }

    [Test]
    public void ManualReorderingSignalsOnceAndReadsDoNotReconcileOtherRoot()
    {
        var document = Document.CreateHtml();
        var h1 = document.CreateElement("div");
        var h2 = document.CreateElement("div");
        var r1 = ShadowTree.Attach(h1, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var r2 = ShadowTree.Attach(h2, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var s1 = document.CreateElement("slot");
        var s2 = document.CreateElement("slot");
        r1.AppendChild(s1);
        r2.AppendChild(s2);
        var a = document.CreateTextNode("a");
        var b = document.CreateTextNode("b");
        h1.AppendChild(a);
        h1.AppendChild(b);
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;
        SlotAssignment.Assign(s1, [a, b]);
        SlotAssignment.Assign(s1, [b, a, b]);
        signals.Should().Equal(s1, s1);
        signals.Clear();
        SlotAssignment.Assign(s2, [a]);
        SlotAssignment.FindSlot(a, false, default).Should().BeNull();
        SlotAssignment.GetAssignedSlot(a).Should().BeSameAs(s1);
        SlotAssignment.AssignedNodes(s1, false, default).Should().Equal(b, a);
        signals.Should().BeEmpty();
        a.Data = "edited";
        signals.Should().BeEmpty();
    }

    [Test]
    public void CrossRootStaleAssignmentOnlyReconcilesAtSpecifiedSteps()
    {
        var document = Document.CreateHtml();
        var h1 = document.CreateElement("div");
        var h2 = document.CreateElement("div");
        var r1 = ShadowTree.Attach(h1, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var r2 = ShadowTree.Attach(h2, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var s1 = document.CreateElement("slot");
        var s2 = document.CreateElement("slot");
        r1.AppendChild(s1);
        r2.AppendChild(s2);
        var n = document.CreateElement("span");
        h1.AppendChild(n);
        SlotAssignment.Assign(s1, [n]);
        SlotAssignment.Assign(s2, [n]);
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;

        s1.SetAttribute("name", "");
        s1.GetAttributeNode("name")!.Value = "";
        s1.RemoveAttribute("name");
        SlotAssignment.AssignedNodes(s1, false, default).Should().ContainSingle().Which.Should().BeSameAs(n);
        signals.Should().BeEmpty();
        n.SetAttribute("slot", "");
        n.GetAttributeNode("slot")!.Value = "";
        n.RemoveAttribute("slot");
        SlotAssignment.AssignedNodes(s1, false, default).Should().ContainSingle().Which.Should().BeSameAs(n);
        signals.Should().BeEmpty();
        h1.AppendChild(document.CreateElement("b"));
        SlotAssignment.AssignedNodes(s1, false, default).Should().ContainSingle().Which.Should().BeSameAs(n);
        signals.Should().BeEmpty();

        n.SetAttribute("slot", "x");
        SlotAssignment.AssignedNodes(s1, false, default).Should().BeEmpty();
        SlotAssignment.GetAssignedSlot(n).Should().BeSameAs(s1);
        signals.Should().ContainSingle().Which.Should().BeSameAs(s1);
        signals.Clear();

        SlotAssignment.Assign(s1, [n]);
        SlotAssignment.Assign(s2, [n]);
        signals.Clear();
        r1.AppendChild(document.CreateElement("b"));
        SlotAssignment.AssignedNodes(s1, false, default).Should().BeEmpty();
        SlotAssignment.GetAssignedSlot(n).Should().BeSameAs(s1);
        signals.Should().ContainSingle().Which.Should().BeSameAs(s1);
    }

    [Test]
    public void NamedSlotAttributeChangeSignalsOldBeforeNew()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var a = document.CreateElement("slot");
        a.SetAttribute("name", "a");
        var b = document.CreateElement("slot");
        b.SetAttribute("name", "b");
        root.AppendChild(a);
        root.AppendChild(b);
        var child = document.CreateElement("span");
        child.SetAttribute("slot", "b");
        host.AppendChild(child);
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;

        child.SetAttribute("slot", "a");
        signals.Should().Equal(b, a);
        SlotAssignment.GetAssignedSlot(child).Should().BeSameAs(a);
        SlotAssignment.AssignedNodes(b, false, default).Should().BeEmpty();
        SlotAssignment.AssignedNodes(a, false, default).Should().ContainSingle().Which.Should().BeSameAs(child);
    }

    [Test]
    public void DetachedManualIntentIsComputedOnHostInsertionButNotStored()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        var node = document.CreateTextNode("detached");
        SlotAssignment.Assign(slot, [node]);
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;

        host.AppendChild(node);
        SlotAssignment.FindSlot(node, false, default).Should().BeSameAs(slot);
        SlotAssignment.AssignedNodes(slot, true, default).Should().ContainSingle().Which.Should().BeSameAs(node);
        SlotAssignment.AssignedNodes(slot, false, default).Should().BeEmpty();
        SlotAssignment.GetAssignedSlot(node).Should().BeNull();
        signals.Should().BeEmpty();
    }

    [Test]
    public void CloningSignalsOnlyCopiedSlotsInInsertionOrder()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open, Clonable: true), default);
        var named = document.CreateElement("slot");
        named.SetAttribute("name", "named");
        var fallbackSlot = document.CreateElement("slot");
        fallbackSlot.SetAttribute("name", "missing");
        fallbackSlot.AppendChild(document.CreateTextNode("fallback"));
        root.AppendChild(named);
        root.AppendChild(fallbackSlot);
        var light = document.CreateElement("span");
        light.SetAttribute("slot", "named");
        host.AppendChild(light);
        using var sourceRecords = document.ObserveMutations(root,
            new MutationObserverOptions { ChildList = true, Subtree = true });
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;

        var copy = (Element)host.CloneNode(true);
        var copiedRoot = copy.AttachedShadowRoot!;
        var copiedNamed = (Element)copiedRoot.FirstChild!;
        var copiedFallback = (Element)copiedNamed.NextSibling!;
        signals.Should().Equal(copiedNamed, copiedFallback);
        SlotAssignment.AssignedNodes(copiedNamed, false, default)
            .Should().ContainSingle().Which.Should().BeSameAs(copy.FirstChild);
        SlotAssignment.AssignedNodes(copiedFallback, true, default)
            .Should().ContainSingle().Which.Should().BeSameAs(copiedFallback.FirstChild);
        SlotAssignment.AssignedNodes(named, false, default)
            .Should().ContainSingle().Which.Should().BeSameAs(light);
        signals.Should().NotContain(named).And.NotContain(fallbackSlot);
        sourceRecords.TakeRecords().Should().BeEmpty();
    }

    [Test]
    public void DeclarativeRootUsesTheSameSignalSink()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var template = document.CreateElement("template");
        ShadowTree.SetDeclarativeTemplateContent(template, root, false);
        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        var signals = new List<Element>();
        document.SlotChangeSignal = signals.Add;
        host.AppendChild(document.CreateTextNode("light"));
        signals.Should().ContainSingle().Which.Should().BeSameAs(slot);
        template.TemplateContent.Should().BeSameAs(root);
    }

    [Test]
    public void ManualIntentAndReleasedSignalSinkDoNotKeepNodesAlive()
    {
        var (slot, detached) = CreateManualIntent();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        detached.TryGetTarget(out _).Should().BeFalse();
        GC.KeepAlive(slot);

        var (document, signaledSlot) = CreateAndReleaseSink();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        signaledSlot.TryGetTarget(out _).Should().BeFalse();
        GC.KeepAlive(document);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Element, WeakReference<Node>) CreateManualIntent()
    {
        var document = Document.CreateHtml();
        var slot = document.CreateElement("slot");
        var detached = document.CreateTextNode("detached");
        SlotAssignment.Assign(slot, [detached]);
        return (slot, new WeakReference<Node>(detached));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Document, WeakReference<Element>) CreateAndReleaseSink()
    {
        var document = Document.CreateHtml();
        var captured = new List<Element>();
        document.SlotChangeSignal = captured.Add;
        var slot = document.CreateElement("slot");
        captured.Add(slot);
        document.SlotChangeSignal = null;
        captured.Clear();
        return (document, new WeakReference<Element>(slot));
    }
}

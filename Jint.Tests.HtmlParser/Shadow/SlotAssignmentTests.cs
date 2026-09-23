#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Shadow;

public class SlotAssignmentTests
{
    [Test]
    public void NamedAssignmentUsesFirstSlotAndSnapshotsStayStable()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var first = document.CreateElement("slot");
        first.SetAttribute("name", "x");
        var second = document.CreateElement("slot");
        second.SetAttribute("name", "x");
        root.AppendChild(first);
        root.AppendChild(second);
        var one = document.CreateElement("span");
        one.SetAttribute("slot", "x");
        var two = document.CreateElement("span");
        two.SetAttribute("slot", "x");
        host.AppendChild(one);
        var snapshot = SlotAssignment.AssignedNodes(first, false, default);
        host.AppendChild(two);

        snapshot.Should().ContainSingle().Which.Should().BeSameAs(one);
        SlotAssignment.AssignedNodes(first, false, default).Should().Equal(one, two);
        SlotAssignment.AssignedNodes(second, false, default).Should().BeEmpty();
        SlotAssignment.FindSlot(one, false, default).Should().BeSameAs(first);
        SlotAssignment.GetAssignedSlot(one).Should().BeSameAs(first);
        root.InsertBefore(second, first);
        SlotAssignment.AssignedNodes(second, false, default).Should().Equal(one, two);
        SlotAssignment.GetAssignedSlot(one).Should().BeSameAs(second);
    }

    [Test]
    public void ManualIntentComputedLookupAndStoredAssignmentRemainDistinctAcrossRoots()
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
        var fallback = document.CreateTextNode("fallback");
        s1.AppendChild(fallback);
        var n = document.CreateElement("span");
        h1.AppendChild(n);

        SlotAssignment.Assign(s1, [n, n]);
        SlotAssignment.GetAssignedSlot(n).Should().BeSameAs(s1);
        SlotAssignment.Assign(s2, [n]);
        SlotAssignment.FindSlot(n, false, default).Should().BeNull();
        SlotAssignment.GetAssignedSlot(n).Should().BeSameAs(s1);
        SlotAssignment.AssignedNodes(s1, false, default).Should().ContainSingle().Which.Should().BeSameAs(n);
        SlotAssignment.AssignedNodes(s1, true, default).Should().ContainSingle().Which.Should().BeSameAs(fallback);
        SlotAssignment.GetAssignedSlot(n).Should().BeSameAs(s1);
        SlotAssignment.Assign(s1, []);
        SlotAssignment.GetAssignedSlot(n).Should().BeSameAs(s1);
        SlotAssignment.AssignedNodes(s1, false, default).Should().BeEmpty();
    }

    [Test]
    public void FlatteningUsesFreshDistributionAndSlottableFallback()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var outer = document.CreateElement("slot");
        var inner = document.CreateElement("slot");
        var fallback = document.CreateElement("em");
        var ignored = document.CreateComment("ignored");
        inner.AppendChild(ignored);
        inner.AppendChild(fallback);
        outer.AppendChild(inner);
        root.AppendChild(outer);
        var text = document.CreateTextNode("text");
        host.AppendChild(text);
        SlotAssignment.AssignedNodes(outer, false, default).Should().ContainSingle().Which.Should().BeSameAs(text);
        SlotAssignment.AssignedNodes(outer, true, default).Should().ContainSingle().Which.Should().BeSameAs(text);
        host.RemoveChild(text);
        SlotAssignment.AssignedNodes(outer, true, default).Should().ContainSingle().Which.Should().BeSameAs(fallback);
        SlotAssignment.AssignedElements(outer, true, default).Should().ContainSingle().Which.Should().BeSameAs(fallback);
    }

    [Test]
    public void ClosedAndNamespacedAttributesAndNonSlottables()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed), default);
        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        var element = document.CreateElement("span");
        element.SetAttributeNS("urn:test", "slot", "elsewhere");
        var text = document.CreateTextNode("t");
        var cdata = document.CreateParsedCDataSection("c");
        var pi = document.CreateProcessingInstruction("p", "x");
        host.AppendChild(element);
        host.AppendChild(text);
        host.AppendChild(cdata);
        host.AppendChild(pi);
        SlotAssignment.AssignedNodes(slot, false, default).Should().Equal(element, text, cdata);
        SlotAssignment.FindSlot(element, true, default).Should().BeNull();
        SlotAssignment.FindSlot(pi, false, default).Should().BeNull();
        Assert.Throws<ArgumentException>(() => SlotAssignment.Assign(slot, [pi]));
        SlotAssignment.AssignedNodes(slot, false, default).Should().HaveCount(3);
    }

    [Test]
    public void AttributeAndFragmentMutationLanesUpdateStoredLists()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var first = document.CreateElement("slot");
        var second = document.CreateElement("slot");
        second.SetAttribute("name", "b");
        root.AppendChild(first);
        root.AppendChild(second);
        var a = document.CreateElement("i");
        var b = document.CreateElement("b");
        b.SetAttribute("slot", "b");
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(a);
        fragment.AppendChild(b);
        host.AppendChild(fragment);
        SlotAssignment.AssignedNodes(first, false, default).Should().ContainSingle().Which.Should().BeSameAs(a);
        SlotAssignment.AssignedNodes(second, false, default).Should().ContainSingle().Which.Should().BeSameAs(b);

        var attachedSlot = b.GetAttributeNode("slot")!;
        attachedSlot.Value = "";
        SlotAssignment.AssignedNodes(first, false, default).Should().Equal(a, b);
        SlotAssignment.AssignedNodes(second, false, default).Should().BeEmpty();
        var replacement = document.CreateAttribute("slot");
        replacement.Value = "b";
        b.SetAttributeNode(replacement);
        SlotAssignment.GetAssignedSlot(b).Should().BeSameAs(second);
        b.RemoveAttributeNode(replacement);
        SlotAssignment.GetAssignedSlot(b).Should().BeSameAs(first);

        var newSlotName = document.CreateAttribute("name");
        newSlotName.Value = "";
        second.SetAttributeNode(newSlotName);
        SlotAssignment.GetAssignedSlot(a).Should().BeSameAs(first);
        root.InsertBefore(second, first);
        SlotAssignment.GetAssignedSlot(a).Should().BeSameAs(second);
        host.ReplaceChildren();
        SlotAssignment.AssignedNodes(first, false, default).Should().BeEmpty();
        SlotAssignment.AssignedNodes(second, false, default).Should().BeEmpty();
    }

    [Test]
    public void CloneBuildsIndependentDistributionAndManualStateIsNotCopied()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual, Clonable: true), default);
        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        var child = document.CreateElement("span");
        host.AppendChild(child);
        SlotAssignment.Assign(slot, [child]);
        var copy = (Element)host.CloneNode(true);
        var copySlot = (Element)copy.AttachedShadowRoot!.FirstChild!;
        var copyChild = copy.FirstChild!;
        SlotAssignment.GetAssignedSlot(copyChild).Should().BeNull();
        SlotAssignment.AssignedNodes(copySlot, false, default).Should().BeEmpty();
        SlotAssignment.FindSlot(copyChild, false, default).Should().BeNull();
        SlotAssignment.GetAssignedSlot(child).Should().BeSameAs(slot);
    }

    [Test]
    public void InvalidAssignLeavesManualIntentAndStoredAssignmentUntouched()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open,
            SlotAssignment: SlotAssignmentMode.Manual), default);
        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        var child = document.CreateTextNode("x");
        host.AppendChild(child);
        SlotAssignment.Assign(slot, [child]);
        Assert.Throws<ArgumentException>(() => SlotAssignment.Assign(slot, [document.CreateComment("bad")]));
        Assert.Throws<ArgumentException>(() => SlotAssignment.AssignedNodes(host, false, default));
        Assert.Throws<ArgumentNullException>(() => SlotAssignment.GetAssignedSlot(null!));
        SlotAssignment.GetAssignedSlot(child).Should().BeSameAs(slot);
        SlotAssignment.AssignedNodes(slot, false, default).Should().ContainSingle().Which.Should().BeSameAs(child);
    }

    [Test]
    public void DuplicateSlotAppendsAndRemovalsKeepTheRootIndex()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var first = document.CreateElement("slot");
        root.AppendChild(first);
        var index = root.SlotState;
        index.Should().NotBeNull();
        var light = document.CreateTextNode("light");
        host.AppendChild(light);

        for (var i = 0; i < 2048; i++)
        {
            root.AppendChild(document.CreateElement("slot"));
        }

        root.SlotState.Should().BeSameAs(index);
        SlotAssignment.GetAssignedSlot(light).Should().BeSameAs(first);
        for (var i = 0; i < 2048; i++)
        {
            root.RemoveChild(root.FirstChild!);
        }

        root.SlotState.Should().BeSameAs(index);
        SlotAssignment.GetAssignedSlot(light).Should().BeSameAs(root.FirstChild);
    }

    [Test]
    public void BulkFragmentOfDuplicateSlotsKeepsTheRootIndex()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        root.AppendChild(document.CreateElement("slot"));
        var index = root.SlotState;
        var fragment = document.CreateDocumentFragment();
        for (var i = 0; i < 2048; i++)
        {
            fragment.AppendChild(document.CreateElement("slot"));
        }

        root.AppendChild(fragment);
        root.SlotState.Should().BeSameAs(index);
        root.ChildCount.Should().Be(2049);
        fragment.ChildCount.Should().Be(0);
    }

    [Test]
    public void AppendingFirstSlotForAnExistingLightChildUsesTheHostIndex()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        root.AppendChild(document.CreateElement("slot"));
        var index = root.SlotState;
        var child = document.CreateElement("span");
        child.SetAttribute("slot", "late");
        host.AppendChild(child);
        SlotAssignment.GetAssignedSlot(child).Should().BeNull();

        var late = document.CreateElement("slot");
        late.SetAttribute("name", "late");
        root.AppendChild(late);
        root.SlotState.Should().BeSameAs(index);
        SlotAssignment.GetAssignedSlot(child).Should().BeSameAs(late);
        SlotAssignment.AssignedNodes(late, false, default).Should().ContainSingle().Which.Should().BeSameAs(child);
    }

    [Test]
    public void ManualTransfersAndNestedFallbackQueriesHaveBoundedWork()
    {
        static int Transfer(int count)
        {
            var document = Document.CreateHtml();
            var first = document.CreateElement("slot");
            var second = document.CreateElement("slot");
            var nodes = new Node[count];
            for (var i = 0; i < count; i++)
            {
                nodes[i] = document.CreateTextNode("x");
            }

            SlotAssignment.Assign(first, nodes);
            return SlotAssignment.AssignMeasured(second, nodes);
        }

        static int FlattenWork(int count)
        {
            var document = Document.CreateHtml();
            var host = document.CreateElement("div");
            var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
            var outer = document.CreateElement("slot");
            outer.SetAttribute("name", "outer");
            root.AppendChild(outer);
            var parent = outer;
            for (var i = 0; i < count; i++)
            {
                var light = document.CreateElement("span");
                light.SetAttribute("slot", "unmatched");
                host.AppendChild(light);
                var nested = document.CreateElement("slot");
                nested.SetAttribute("name", "nested");
                parent.AppendChild(nested);
                parent = nested;
            }

            var leaf = document.CreateTextNode("fallback");
            parent.AppendChild(leaf);
            var work = 0;
            SlotAssignment.AssignedNodes(outer, true, steps => work = steps, default)
                .Should().ContainSingle().Which.Should().BeSameAs(leaf);
            return work;
        }

        var transfer64 = Transfer(64);
        var transfer128 = Transfer(128);
        transfer128.Should().BeLessThan(transfer64 * 3);
        var flatten64 = FlattenWork(64);
        var flatten128 = FlattenWork(128);
        flatten128.Should().BeLessThan(flatten64 * 3);
    }

    [Test]
    public void ColdIndexAndSnapshotMaterializationPollCancellation()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        var root = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var slottable = document.CreateElement("span");
        host.AppendChild(slottable);
        for (var i = 0; i < 4096; i++)
        {
            root.AppendChild(document.CreateElement("div"));
        }

        // Invalidate only the empty root-local index to exercise its cold path.
        root.SlotState = null;
        using var canceledIndex = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => SlotAssignment.FindSlot(slottable, false,
            steps =>
            {
                if (steps == 256) canceledIndex.Cancel();
            }, canceledIndex.Token));
        root.SlotState.Should().BeNull();

        var slot = document.CreateElement("slot");
        root.AppendChild(slot);
        host.AppendChild(document.CreateTextNode("a"));
        host.AppendChild(document.CreateTextNode("b"));
        host.AppendChild(document.CreateTextNode("c"));
        using var canceledSnapshot = new CancellationTokenSource();
        var finalSteps = 0;
        Assert.Throws<OperationCanceledException>(() => SlotAssignment.AssignedNodes(slot, false,
            steps =>
            {
                finalSteps = steps;
                canceledSnapshot.Cancel();
            }, canceledSnapshot.Token));
        finalSteps.Should().Be(4);
        SlotAssignment.AssignedNodes(slot, false, default).Should().HaveCount(4);
    }
}

#nullable enable
using System.Runtime.CompilerServices;
using Jint.Browser.Dom;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Dom;

public sealed class NativeLabelTests
{
    [Test]
    public void LabelsFollowDuplicateIdsAndImplicitAssociationOnTheNativeTree()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var first = document.CreateElement("input");
        first.SetAttribute("id", "control");
        root.AppendChild(first);
        var second = document.CreateElement("input");
        second.SetAttribute("id", "control");
        root.AppendChild(second);
        var explicitLabel = document.CreateElement("label");
        explicitLabel.SetAttribute("for", "control");
        root.AppendChild(explicitLabel);
        var implicitLabel = document.CreateElement("label");
        root.AppendChild(implicitLabel);
        implicitLabel.AppendChild(first);

        HtmlLabelAssociation.LabelsFor(second).Should().Equal(explicitLabel);
        HtmlLabelAssociation.LabelsFor(first).Should().Equal(implicitLabel);
        root.InsertBefore(first, second);
        HtmlLabelAssociation.LabelsFor(first).Should().Equal(explicitLabel);
        first.SetAttribute("type", "HiDdEn");
        HtmlLabelAssociation.LabelsFor(first).Should().BeEmpty();
        HtmlLabelAssociation.ControlFor(explicitLabel).Should().BeNull();
    }

    [Test]
    public void LabelLookupChecksWhileScanningAnInputsAttributes()
    {
        var document = Document.CreateHtml();
        var label = document.CreateElement("label");
        var input = document.CreateElement("input");
        for (var i = 0; i < 4096; i++) input.SetAttribute("data-" + i, "x");
        label.AppendChild(input);
        var checks = 0;
        var failure = Caught.Exception(() => HtmlLabelAssociation.ControlFor(label, _ =>
        {
            if (++checks == 3) throw new ReadStopped();
        }));
        failure.Should().BeOfType<ReadStopped>();
        checks.Should().Be(3);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LabelListReadsCarryTheInvocationCheckpoint(bool indexed)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var input = document.CreateElement("input");
        input.SetAttribute("id", new string('a', 8192));
        root.AppendChild(input);
        var label = document.CreateElement("label");
        label.SetAttribute("for", new string('a', 8192));
        root.AppendChild(label);
        var list = DomLabelNodeList.Of(input);
        var checks = 0;
        void Check(int _) { if (++checks == 3) throw new ReadStopped(); }
        var failure = Caught.Exception(() =>
        {
            if (indexed) _ = list.ReadItem(0, Check, default);
            else _ = list.ReadLength(Check, default);
        });
        failure.Should().BeOfType<ReadStopped>();
        checks.Should().Be(3);
    }

    [Test]
    public void FirstIndexedLabelReadDoesNotWalkAnUnrelatedSuffix()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var input = document.CreateElement("input");
        input.SetAttribute("id", "control");
        root.AppendChild(input);
        var label = document.CreateElement("label");
        label.SetAttribute("for", "control");
        root.AppendChild(label);
        for (var i = 0; i < 5000; i++) root.AppendChild(document.CreateElement("div"));
        var work = 0;
        DomLabelNodeList.Of(input).ReadItem(0, count => work += count, default).Should().BeSameAs(label);
        work.Should().BeLessThan(1024, "finding the first label must not visit the unrelated suffix");
    }

    [Test]
    public void RetainedLabelsObserveDuplicateIdsImplicitOrderAndNewRoots()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var input = document.CreateElement("input");
        input.SetAttribute("id", "control");
        var explicitLabel = document.CreateElement("label");
        explicitLabel.SetAttribute("for", "control");
        root.AppendChild(explicitLabel);
        var implicitLabel = document.CreateElement("label");
        root.AppendChild(implicitLabel);
        implicitLabel.AppendChild(input);
        var list = DomLabelNodeList.Of(input);
        void Expect(params Element[] expected)
        {
            DomLabelNodeList.Of(input).Should().BeSameAs(list);
            list.ReadLength(null, default).Should().Be(expected.Length);
            for (var i = 0; i < expected.Length; i++)
                list.ReadItem((uint) i, null, default).Should().BeSameAs(expected[i]);
            list.ReadItem((uint) expected.Length, null, default).Should().BeNull();
            list.ReadItem(uint.MaxValue, null, default).Should().BeNull();
        }
        Expect(explicitLabel, implicitLabel);
        var duplicate = document.CreateElement("div");
        duplicate.SetAttribute("id", "control");
        root.InsertBefore(duplicate, explicitLabel);
        Expect(implicitLabel);
        root.RemoveChild(duplicate);
        Expect(explicitLabel, implicitLabel);
        explicitLabel.SetAttribute("for", "other");
        var preceding = document.CreateElement("input");
        implicitLabel.InsertBefore(preceding, input);
        Expect();
        preceding.SetAttribute("type", "hidden");
        Expect(implicitLabel);

        var other = Document.CreateHtml();
        var otherRoot = other.CreateElement("div");
        other.AppendChild(otherRoot);
        var otherLabel = other.CreateElement("label");
        otherLabel.SetAttribute("for", "control");
        otherRoot.AppendChild(otherLabel);
        otherRoot.AppendChild(input);
        input.OwnerDocument.Should().BeSameAs(other);
        Expect(otherLabel);
        input.SetAttribute("type", "hidden");
        Expect();
        input.SetAttribute("type", "checkbox");
        Expect(otherLabel);
        otherRoot.RemoveChild(input);
        Expect();
        var detached = other.CreateElement("label");
        detached.AppendChild(input);
        Expect(detached);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LabelReadsHonorCancellationAtTheirFinalCheckpoint(bool indexed)
    {
        var document = Document.CreateHtml();
        var label = document.CreateElement("label");
        var input = document.CreateElement("input");
        label.AppendChild(input);
        var list = DomLabelNodeList.Of(input);
        using var stopping = new CancellationTokenSource();
        var checks = 0;
        void Check(int _) { if (++checks == 2) stopping.Cancel(); }
        Caught.Exception(() =>
        {
            if (indexed) _ = list.ReadItem(0, Check, stopping.Token);
            else _ = list.ReadLength(Check, stopping.Token);
        }).Should().BeOfType<OperationCanceledException>();
        checks.Should().Be(2);
    }

    [Test]
    [NonParallelizable]
    public void TheAssociatedLabelsListDoesNotRootItsControl()
    {
        var weak = CreateTransientLabels();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        weak.Control.TryGetTarget(out _).Should().BeFalse();
        weak.List.TryGetTarget(out _).Should().BeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<Element> Control, WeakReference<DomLabelNodeList> List) CreateTransientLabels()
    {
        var input = Document.CreateHtml().CreateElement("input");
        var list = DomLabelNodeList.Of(input);
        return (new WeakReference<Element>(input), new WeakReference<DomLabelNodeList>(list));
    }

    private sealed class ReadStopped : Exception { }
}

#nullable enable
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class CheckedStateWorkTests
{
    private static Element Radio(Document document, string name = "g")
    {
        var input = document.CreateElement("input");
        input.InitializeParsedAttributes(new[] { new ParserAttribute(null, "type", null, "radio"),
            new ParserAttribute(null, "name", null, name) }, default);
        return input;
    }

    [TestCase(1)]
    [TestCase(1000)]
    [TestCase(10000)]
    public void HotFactsAllocateNothingAndSelectionVisitsOnlyCheckedMembers(int size)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document);
        root.AppendParsedChild(a);
        for (var i = 1; i < size; i++) root.AppendParsedChild(Radio(document));
        var probe = new HtmlCheckedWorkProbe();
        document.CheckedWorkProbe = probe;
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(size);
        probe.Builds.Should().Be(1);
        var units = probe.Units;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) HtmlCheckableState.GetRadioGroupFacts(a, default);
        var difference = GC.GetAllocatedBytesForCurrentThread() - allocated;
        difference.Should().Be(0);
        probe.Units.Should().Be(units);
        HtmlCheckableState.Get(a)!.SetChecked(true, default);
        units = probe.Units;
        HtmlCheckableState.Get((Element) root.LastChild!)!.SetChecked(true, default);
        (probe.Units - units).Should().BeLessThanOrEqualTo(1);
        root.SetAttribute("class", "unrelated");
        HtmlCheckableState.Get(a)!.SetIndeterminate(true);
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(size);
        probe.Builds.Should().Be(1);
        var appended = Radio(document);
        root.AppendParsedChild(appended);
        HtmlCheckableState.GetRadioGroupFacts(appended, default).MemberCount.Should().Be(size + 1);
        probe.Builds.Should().Be(1);
    }

    [Test]
    public void CollapsingLargeCheckedSetLeavesDenseConstantWorkSelection()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 10000; i++)
        {
            var input = Radio(document);
            input.SetAttribute("checked", "");
            root.AppendParsedChild(input);
        }
        var last = (Element) root.LastChild!;
        HtmlCheckableState.GetRadioGroupFacts(last, default).CheckedCount.Should().Be(10000);
        HtmlCheckableState.Get(last)!.SetChecked(true, default);
        var state = HtmlCheckableState.Get(last)!;
        state.Group!.Checked.Count.Should().Be(1);
        state.CheckedPosition.Should().Be(0);
        var probe = new HtmlCheckedWorkProbe();
        document.CheckedWorkProbe = probe;
        for (var i = 0; i < 1000; i++)
        {
            state.SetChecked(true, default);
            HtmlCheckableState.FirstCheckedRadio(last, default).Should().BeSameAs(last);
        }
        probe.Units.Should().Be(1000);
        state.Group.Checked.Count.Should().Be(1);
    }

    [Test]
    public void IdResetOrderingHasLinearWideGapWorkAndCompleteCandidateCoverage()
    {
        var small = IdResetWork(128);
        var large = IdResetWork(256);
        large.Should().BeLessThanOrEqualTo(2 * small + 32);
    }

    private static long IdResetWork(int count)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        for (var i = 0; i < count; i++)
        {
            for (var j = 0; j < 8; j++) root.AppendParsedChild(document.CreateElement("div"));
            var control = document.CreateElement("input");
            control.SetAttribute("form", i % 2 == 0 ? "f" : "");
            root.AppendParsedChild(control);
        }
        var unrelated = document.CreateElement("div");
        unrelated.SetAttribute("id", "x");
        root.AppendChild(unrelated);
        using var probe = new HtmlFormWorkProbe(document);
        unrelated.SetAttribute("id", "y");
        probe.ResetCandidates.Should().Be(count);
        return probe.Visits;
    }

    [Test]
    public void TailMetadataHasBoundedColdWorkAndConstantHotFacts()
    {
        var document = Document.CreateHtml();
        var source = document.CreateElement("input");
        var attributes = Enumerable.Range(0, 10000)
            .Select(i => new ParserAttribute(null, $"data-{i}", null, "x"))
            .Concat(new[] { new ParserAttribute(null, "type", null, "radio"),
                new ParserAttribute(null, "name", null, "g"), new ParserAttribute(null, "required", null, "") }).ToArray();
        source.InitializeParsedAttributes(attributes, default);
        // Exercise the unpublished clone-attribute phase before its component is installed.
        var target = document.CreateElement("input");
        target.CopyAttributesFrom(source, document);
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = n => { if (n == 300) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.GetRadioGroupFacts(target, cancellation.Token));
        probe.Units.Should().Be(512);
        target.ExistingCheckedState.Should().BeNull();
        probe.Checkpoint = null;
        var facts = HtmlCheckableState.GetRadioGroupFacts(target, default);
        facts.Should().Be(new HtmlRadioGroupFacts(true, 1, 0, 1));
        var units = probe.Units;
        for (var i = 0; i < 1000; i++) HtmlCheckableState.GetRadioGroupFacts(target, default);
        probe.Units.Should().Be(units);
        HtmlCheckableState.Get(target)!.SetChecked(true, default);
        probe.Units.Should().Be(units);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CheckablePredicatesUseOriginalTokenDuringColdMetadata(bool indeterminate)
    {
        var document = Document.CreateHtml();
        var source = document.CreateElement("input");
        var attributes = Enumerable.Range(0, 1000).Select(i => new ParserAttribute(null, $"data-{i}", null, "x"))
            .Append(new ParserAttribute(null, "type", null, "checkbox")).ToArray();
        source.InitializeParsedAttributes(attributes, default);
        var target = document.CreateElement("input");
        target.CopyAttributesFrom(source, document);
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = n => { if (n == 300) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        var exception = Assert.Throws<OperationCanceledException>(() =>
        {
            if (indeterminate) HtmlCheckableState.MatchesIndeterminate(target, cancellation.Token);
            else HtmlCheckableState.MatchesUnchecked(target, cancellation.Token);
        });
        exception!.CancellationToken.Should().Be(cancellation.Token);
        probe.Units.Should().Be(512);
        target.ExistingCheckedState.Should().BeNull();
        probe.Checkpoint = null;
        HtmlCheckableState.MatchesUnchecked(target, default).Should().BeTrue();
        HtmlCheckableState.MatchesIndeterminate(target, default).Should().BeFalse();
    }

    [Test]
    public void RepeatedIdResetsReuseCandidateOrderDespiteUnrelatedTreeGrowth()
    {
        var small = CachedIdResetWork(10);
        var large = CachedIdResetWork(10000);
        large.Should().Be(small);
    }

    private static long CachedIdResetWork(int unrelatedCount)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var control = document.CreateElement("input");
        control.SetAttribute("form", "f");
        root.AppendChild(control);
        var idNode = document.CreateElement("div");
        idNode.SetAttribute("id", "x");
        root.AppendChild(idNode); // Prime ordered candidate snapshot.
        for (var i = 0; i < unrelatedCount; i++) root.AppendParsedChild(document.CreateElement("div"));
        using var probe = new HtmlFormWorkProbe(document);
        for (var i = 0; i < 10; i++) idNode.SetAttribute("id", $"changed-{i}");
        probe.ResetCandidates.Should().Be(10);
        return probe.Visits;
    }

    [Test]
    public void CandidateMovesInvalidateCachedOrderAndEqualValuesPreserveIt()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var a = Radio(document);
        var b = Radio(document);
        a.SetAttribute("form", "f");
        b.SetAttribute("form", "f");
        root.AppendChild(a);
        root.AppendChild(b);
        var index = document.FormIndex!;
        var snapshot = index.ResetCandidates();
        snapshot.Should().Equal(a, b);
        a.SetAttribute("form", "f");
        index.ResetCandidates().Should().BeSameAs(snapshot);
        root.InsertBefore(b, a);
        index.ResetCandidates().Should().Equal(b, a);
        root.RemoveChild(a);
        index.ResetCandidates().Should().Equal(b);
    }

    [Test]
    public void UnrelatedElementsDoNotAllocateHtmlStateDuringBatchOrClone()
    {
        var document = Document.CreateHtml();
        var div = document.CreateElement("div");
        div.InitializeParsedAttributes(new[] { new ParserAttribute(null, "class", null, "x") }, default);
        HtmlCheckableState.Get(div).Should().BeNull();
        div.HasHtmlState.Should().BeFalse();
        ((Element) div.CloneNode()).HasHtmlState.Should().BeFalse();
    }

    [Test]
    public void ColdBuildAndSnapshotChargeNonElementsAndHonorCancellation()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document);
        root.AppendParsedChild(a);
        for (var i = 0; i < 1024; i++) root.AppendParsedChild(document.CreateComment("x"));
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = n => { if (n == 300) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.GetRadioGroupFacts(a, cancellation.Token));
        probe.Units.Should().Be(512);
        root.RadioIndex.Should().BeNull();
        HtmlCheckableState.Get(a)!.Index.Should().BeNull();
        probe.Checkpoint = null;
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(1);
        using var snapshotCancel = new CancellationTokenSource();
        var start = probe.Units;
        probe.Checkpoint = n => { if (n == start + 300) snapshotCancel.Cancel(); };
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.SnapshotRadioGroup(a, snapshotCancel.Token));
        (probe.Units - start).Should().Be(512);
        HtmlCheckableState.Get(a)!.Checked.Should().BeFalse();
    }

    [Test]
    public void DeepAscentAndLongNamesUseOneBoundedWorkCounter()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var current = root;
        for (var i = 0; i < 600; i++)
        {
            var next = document.CreateElement("div");
            current.AppendParsedChild(next);
            current = next;
        }
        var a = Radio(document, new string('x', 4096));
        current.AppendParsedChild(a);
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = n => { if (n == 600) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.GetRadioGroupFacts(a, cancellation.Token));
        // Applicability now adds an earlier explicit cancellation boundary.
        // The same shared counter must still stop within the original ceiling.
        probe.Units.Should().BeInRange(600, 768);
        root.RadioIndex.Should().BeNull();
        probe.Checkpoint = null;
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(1);
    }

    [Test]
    public void CancellationDuringNameHashingLeavesNoPublishedBucket()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var input = Radio(document, new string('x', 10000));
        root.AppendParsedChild(input);
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = n => { if (n == 300) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.GetRadioGroupFacts(input, cancellation.Token));
        probe.Units.Should().Be(512);
        root.RadioIndex.Should().BeNull();
        input.ExistingCheckedState!.Index.Should().BeNull();
        probe.Checkpoint = null;
        HtmlCheckableState.GetRadioGroupFacts(input, default).MemberCount.Should().Be(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DistinctNamesAndUnnamedSingletonsBootstrapOnce(bool unnamed)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 1000; i++) root.AppendParsedChild(Radio(document, unnamed ? "" : $"g-{i}"));
        var probe = new HtmlCheckedWorkProbe();
        document.CheckedWorkProbe = probe;
        foreach (var input in root.ChildNodes.Cast<Element>())
            HtmlCheckableState.GetRadioGroupFacts(input, default).MemberCount.Should().Be(1);
        probe.Builds.Should().Be(1);
        var units = probe.Units;
        foreach (var input in root.ChildNodes.Cast<Element>()) HtmlCheckableState.GetRadioGroupFacts(input, default);
        probe.Units.Should().Be(units);
    }

    [Test]
    public void CanceledMultiCheckedSelectionHasNoPartialExclusion()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        for (var i = 0; i < 600; i++)
        {
            var input = Radio(document);
            input.SetAttribute("checked", "");
            root.AppendParsedChild(input);
        }
        var first = (Element) root.FirstChild!;
        HtmlCheckableState.GetRadioGroupFacts(first, default).CheckedCount.Should().Be(600);
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = n => { if (n == 300) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.Get(first)!.SetChecked(true, cancellation.Token));
        HtmlCheckableState.GetRadioGroupFacts(first, default).CheckedCount.Should().Be(600);
        HtmlCheckableState.Get(first)!.DirtyCheckedness.Should().BeFalse();
        probe.Checkpoint = null;
        HtmlCheckableState.Get(first)!.SetChecked(true, default);
        HtmlCheckableState.GetRadioGroupFacts(first, default).CheckedCount.Should().Be(1);
    }

    [Test]
    public void DisconnectingHostPreservesShadowOrdinaryRootMembership()
    {
        var document = Document.CreateHtml();
        var host = document.CreateElement("div");
        document.AppendChild(host);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Closed), default);
        var a = Radio(document);
        var b = Radio(document);
        shadow.AppendChild(a);
        shadow.AppendChild(b);
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(2);
        document.RemoveChild(host);
        HtmlCheckableState.GetRadioGroupFacts(a, default).MemberCount.Should().Be(2);
        HtmlCheckableState.GetRadioGroupFacts(b, default).MemberCount.Should().Be(2);
        HtmlCheckableState.Get(a)!.SetChecked(true, default);
        HtmlCheckableState.Get(b)!.SetChecked(true, default);
        HtmlCheckableState.Get(a)!.Checked.Should().BeFalse();
    }

    [Test]
    public void CommittedParserInsertionCompletesHooksBeforeCancellationIsReported()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var a = Radio(document);
        a.SetAttribute("checked", "");
        root.AppendParsedChild(a);
        var b = Radio(document);
        b.SetAttribute("checked", "");
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => root.InsertParsedBefore(b, null, cancellation.Cancel, cancellation.Token));
        b.ParentNode.Should().BeSameAs(root);
        HtmlCheckableState.GetRadioGroupFacts(b, default).MemberCount.Should().Be(2);
        HtmlCheckableState.Get(a)!.Checked.Should().BeFalse();
        HtmlCheckableState.Get(b)!.Checked.Should().BeTrue();
    }

    [Test]
    public void PreCanceledHotQueriesAndAssignmentsAreSilent()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document);
        root.AppendChild(a);
        HtmlCheckableState.GetRadioGroupFacts(a, default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var stamp = document.MutationStamp;
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.GetRadioGroupFacts(a, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.Get(a)!.SetChecked(true, cancellation.Token));
        document.MutationStamp.Should().Be(stamp);
        HtmlCheckableState.Get(a)!.DirtyCheckedness.Should().BeFalse();
    }

    [Test]
    public void MovingIndexedDetachedRootRetiresAllOldHandles()
    {
        var document = Document.CreateHtml();
        var oldRoot = document.CreateElement("div");
        for (var i = 0; i < 1000; i++) oldRoot.AppendParsedChild(Radio(document));
        var first = (Element) oldRoot.FirstChild!;
        HtmlCheckableState.GetRadioGroupFacts(first, default).MemberCount.Should().Be(1000);
        var oldIndex = oldRoot.RadioIndex!;
        var destination = document.CreateElement("div");
        var existing = Radio(document);
        destination.AppendChild(existing);
        HtmlCheckableState.GetRadioGroupFacts(existing, default);
        destination.AppendChild(oldRoot);
        oldRoot.RadioIndex.Should().BeNull();
        oldIndex.RegisteredCount.Should().Be(0);
        HtmlCheckableState.GetRadioGroupFacts(first, default).MemberCount.Should().Be(1001);
        first.ExistingCheckedState!.Index.Should().BeSameAs(destination.RadioIndex);
    }

    [Test]
    [NonParallelizable]
    public void SnapshotRetainsRemovedMembersOnlyUntilReleased()
    {
        var package = SnapshotRemoval();
        Collect();
        Alive(package.Weak).Should().BeTrue();
        package.Snapshot = null;
        Collect();
        Alive(package.Weak).Should().BeFalse();
        GC.KeepAlive(package.Document);
    }

    private static void Collect()
    {
        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Alive(WeakReference<Element> weak) => weak.TryGetTarget(out _);

    private sealed class SnapshotPackage(Document document, WeakReference<Element> weak, IReadOnlyList<Element> snapshot)
    {
        internal Document Document { get; } = document;
        internal WeakReference<Element> Weak { get; } = weak;
        internal IReadOnlyList<Element>? Snapshot { get; set; } = snapshot;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SnapshotPackage SnapshotRemoval()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var a = Radio(document);
        root.AppendChild(a);
        var snapshot = HtmlCheckableState.SnapshotRadioGroup(a, default);
        root.RemoveChild(a);
        return new SnapshotPackage(document, new WeakReference<Element>(a), snapshot);
    }

    [Test]
    [NonParallelizable]
    public void RetainedDocumentDoesNotKeepRemovedRadiosAlive()
    {
        var (document, weak) = RemovedRadio();
        for (var i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        weak.TryGetTarget(out _).Should().BeFalse();
        GC.KeepAlive(document);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Document Document, WeakReference<Element> Weak) RemovedRadio()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        document.AppendChild(root);
        var a = Radio(document);
        a.SetAttribute("form", "missing");
        root.AppendChild(a);
        document.FormIndex!.ResetCandidates();
        HtmlCheckableState.GetRadioGroupFacts(a, default);
        root.RemoveChild(a);
        return (document, new WeakReference<Element>(a));
    }
}

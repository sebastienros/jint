#nullable enable
using System.Reflection;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssStyleSheetRevisionSnapshotTests
{
    private static CssValueWork Work() => new(default);
    private static CssImportRule Import(CssStyleSheet sheet, int index = 0) => (CssImportRule) sheet.Rules[index];

    [Test]
    public void CaptureRejectsMutationOfVisitedSiblingDuringLaterSiblingWork()
    {
        var root = CssStyleSheet.Parse("@import 'a'; @import 'b';");
        var a = CssStyleSheet.Parse("a {}");
        // B's unassociated import prefix supplies charged edges after A has been visited.
        var b = CssStyleSheet.Parse(string.Concat(Enumerable.Repeat("@import 'pending';", 5000)));
        Import(root).SetStyleSheet(a, null, null, Work());
        Import(root, 1).SetStyleSheet(b, null, null, Work());
        var calls = 0;
        var work = new CssValueWork(default, () => { if (++calls == 2) a.Disabled = true; });
        CssStyleSheetRevisionSnapshot? published = null;
        Assert.Throws<InvalidOperationException>(() => published = CssStyleSheetRevisionSnapshot.Capture(root, work));
        calls.Should().BeGreaterThanOrEqualTo(2);
        published.Should().BeNull();
        a.Disabled.Should().BeTrue();
    }

    [Test]
    public void CheckpointRuleListReplacementFailsWithExplicitInvalidation()
    {
        var root = CssStyleSheet.Parse("@import 'wide';");
        var child = CssStyleSheet.Parse(string.Concat(Enumerable.Repeat("@import 'pending';", 5000)));
        Import(root).SetStyleSheet(child, null, null, Work());
        var calls = 0;
        var work = new CssValueWork(default, () => { if (++calls == 2) child.ReplaceText("replacement {}"); });
        CssStyleSheetRevisionSnapshot? published = null;
        Assert.Throws<InvalidOperationException>(() => published = CssStyleSheetRevisionSnapshot.Capture(root, work));
        published.Should().BeNull();
        ((CssStyleRule) child.Rules[0]).SelectorText.Should().Be("replacement");
    }

    [Test]
    public void FinalCaptureCallbackCannotPublishRefreshedOrMixedRevisions()
    {
        var root = CssStyleSheet.Parse("a {}");
        var calls = 0;
        CssStyleSheetRevisionSnapshot? published = null;
        var work = new CssValueWork(default, () => { if (++calls == 2) root.Disabled = true; });
        Assert.Throws<InvalidOperationException>(() => published = CssStyleSheetRevisionSnapshot.Capture(root, work));
        calls.Should().Be(2);
        published.Should().BeNull();
    }

    [Test]
    public void IsCurrentRunsHostCallbacksBeforeComparingAnyRevision()
    {
        var root = CssStyleSheet.Parse("@import 'child';");
        var child = CssStyleSheet.Parse("a {}");
        Import(root).SetStyleSheet(child, null, null, Work());
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(root, Work());
        var calls = 0;
        var work = new CssValueWork(default, () => { calls++; root.Disabled = true; });
        snapshot.IsCurrent(work).Should().BeFalse();
        calls.Should().Be(1);
        snapshot.IsCurrent(Work()).Should().BeFalse();
    }

    [Test]
    public void InactiveDescendantsAndRepeatedRootsAreCapturedWithoutFlatteningTheRootSet()
    {
        var root = CssStyleSheet.Parse("@import 'child' print; root { border-color:red; }");
        var child = CssStyleSheet.Parse("child { border-color:red; }");
        child.Disabled = true;
        Import(root).SetStyleSheet(child, null, null, Work());
        var stamp = root.Stamp;
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(new[] { root, root, child }, Work());
        snapshot.Count.Should().Be(2);
        snapshot.IsCurrent(Work()).Should().BeTrue();
        root.Stamp.Should().Be(stamp);
        root.Rules.Count.Should().Be(2);
        child.InsertRule("new {}", 1);
        root.Stamp.Should().Be(stamp);
        snapshot.IsCurrent(Work()).Should().BeFalse();
    }

    [Test]
    public void EmptyRootsReuseTheSingletonAndColdDuplicateRootsNeedNoGraphIdentitySet()
    {
        var first = CssStyleSheetRevisionSnapshot.Capture(Array.Empty<CssStyleSheet>(), Work());
        var second = CssStyleSheetRevisionSnapshot.Capture(Array.Empty<CssStyleSheet>(), Work());
        first.Should().BeSameAs(second);
        first.Count.Should().Be(0);
        first.IsCurrent(Work()).Should().BeTrue();
        var root = CssStyleSheet.Parse("a {}");
        var duplicateRoots = CssStyleSheetRevisionSnapshot.Capture(new[] { root, root }, Work());
        duplicateRoots.Count.Should().Be(2);
        duplicateRoots.IsCurrent(Work()).Should().BeTrue();
        root.Disabled = true;
        duplicateRoots.IsCurrent(Work()).Should().BeFalse();
    }

    [Test]
    public void CancellationAndFatalCheckpointsNeverPublishACapture()
    {
        var root = CssStyleSheet.Parse("a {}");
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(root, Work());
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        CssStyleSheetRevisionSnapshot? published = null;
        var work = new CssValueWork(cancellation.Token, () => { if (++calls == 2) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => published = CssStyleSheetRevisionSnapshot.Capture(root, work));
        published.Should().BeNull();
        Assert.Throws<OperationCanceledException>(() => snapshot.IsCurrent(new CssValueWork(cancellation.Token)));
        var failure = new ApplicationException("host checkpoint failure");
        var thrown = Assert.Throws<ApplicationException>(() => snapshot.IsCurrent(new CssValueWork(default, () => throw failure)));
        thrown.Should().BeSameAs(failure);
        snapshot.IsCurrent(Work()).Should().BeTrue();
    }

    [Test]
    public void SaturationCannotBeCapturedOrReusedAndNeverSilentlyRetries()
    {
        var root = CssStyleSheet.Parse("a {}");
        typeof(CssStyleSheet).GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(root, ulong.MaxValue - 1);
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(root, Work());
        root.Disabled = true;
        snapshot.IsCurrent(Work()).Should().BeFalse();
        var calls = 0;
        Assert.Throws<InvalidOperationException>(() => CssStyleSheetRevisionSnapshot.Capture(root,
            new CssValueWork(default, () => calls++)));
        calls.Should().Be(2);
    }

    [Test]
    public void ColdTenThousandStyleRulesCostConstantWorkAndNoGraphCollections()
    {
        var small = CssStyleSheet.Parse("a { border-color:red; }");
        var large = CssStyleSheet.Parse(string.Concat(Enumerable.Repeat("a { border-color:red; }", 10000)));
        var calls = 0;
        var work = new CssValueWork(default, () => calls++);
        // Warm the same capture path before measuring allocation on this test's thread.
        CssStyleSheetRevisionSnapshot.Capture(small, work);
        CssStyleSheetRevisionSnapshot.Capture(large, work);
        calls = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(large, work);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        calls.Should().Be(2); // Walking charged style-rule edges would trigger extra checkpoints.
        allocated.Should().BeLessThanOrEqualTo(128); // One root-entry array and the snapshot itself.
        snapshot.Count.Should().Be(1);
        snapshot.IsCurrent(Work()).Should().BeTrue();
    }

    [TestCase(1000)]
    [TestCase(5000)]
    public void DeepGraphsChargeLinearWorkAndVerificationDoesNotReenterHostCallbacks(int depth)
    {
        var root = CssStyleSheet.Parse("@import 'child';");
        var tail = root;
        for (var i = 0; i < depth; i++)
        {
            var child = CssStyleSheet.Parse(i == depth - 1 ? "end {}" : "@import 'child';");
            Import(tail).SetStyleSheet(child, null, null, Work());
            tail = child;
        }
        var calls = 0;
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(root, new CssValueWork(default, () => calls++));
        snapshot.Count.Should().Be(depth + 1);
        // Node, edge, materialization and verification charges: at most five units per sheet.
        calls.Should().BeLessThanOrEqualTo(2 + (5 * (depth + 1) / 4096));
        calls = 0;
        snapshot.IsCurrent(new CssValueWork(default, () => calls++)).Should().BeTrue();
        calls.Should().Be(1 + (snapshot.Count / 4096));
        var visitedTail = tail;
        var checks = 0;
        snapshot.IsCurrent(new CssValueWork(default, () => { if (++checks == 1) visitedTail.Disabled = true; }))
            .Should().BeFalse();
    }

    [Test]
    public void WideSharedDescendantsAreDeduplicatedAndCaptureCancellationIsBounded()
    {
        const int width = 3000;
        var roots = new CssStyleSheet[width];
        for (var i = 0; i < width; i++)
        {
            roots[i] = CssStyleSheet.Parse("@import 'child';");
            Import(roots[i]).SetStyleSheet(CssStyleSheet.Parse("child {}"), null, null, Work());
        }
        // Listing an imported child as another root must retain one revision witness.
        var repeated = roots.Concat(new[] { Import(roots[0]).StyleSheet! }).ToArray();
        var snapshot = CssStyleSheetRevisionSnapshot.Capture(repeated, Work());
        snapshot.Count.Should().Be(width * 2);
        var calls = 0;
        var work = new CssValueWork(default, () => { if (++calls == 3) throw new OperationCanceledException(); });
        CssStyleSheetRevisionSnapshot? published = null;
        Assert.Throws<OperationCanceledException>(() => published = CssStyleSheetRevisionSnapshot.Capture(repeated, work));
        published.Should().BeNull();
    }
}

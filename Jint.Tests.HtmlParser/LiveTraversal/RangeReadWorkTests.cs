#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class RangeReadWorkTests
{
    public enum Read { Ancestor, Text, Boundaries, Point, Contains, Intersects }

    private static object Run(Read read, DomRange range, DomRange other, DomNodeIdentity node,
        Action<int>? checkpoint, CancellationToken token = default) => read switch
    {
        Read.Ancestor => range.GetCommonAncestor(checkpoint, token),
        Read.Text => range.GetText(checkpoint, token),
        Read.Boundaries => range.CompareBoundaryPoints(0, other, checkpoint, token),
        Read.Point => range.ComparePoint(node, 0, checkpoint, token),
        Read.Contains => range.IsPointInRange(node, 0, checkpoint, token),
        _ => range.IntersectsNode(node, checkpoint, token)
    };

    [TestCase(Read.Ancestor)]
    [TestCase(Read.Text)]
    [TestCase(Read.Boundaries)]
    [TestCase(Read.Point)]
    [TestCase(Read.Contains)]
    [TestCase(Read.Intersects)]
    public void DeepReadsPreserveBudgetSentinelAndLeaveEndpointsAndNotificationsUntouched(Read read)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var leaf = root;
        for (var i = 0; i < 1024; i++)
        {
            var child = document.CreateElement("deep");
            leaf.AppendParsedChild(child);
            leaf = child;
        }
        var text = document.CreateTextNode("x");
        leaf.AppendChild(text);
        var range = document.CreateRange();
        range.SetStart(new(root), 0);
        range.SetEnd(new(text), 1);
        var other = document.CreateRange();
        other.SelectNodeContents(new(text));
        using var subscription = range.ObserveChanges(document);
        var start = range.Start;
        var end = range.End;
        var sentinel = new InvalidOperationException("actual host budget sentinel");
        var last = -1;
        var exception = Assert.Throws<InvalidOperationException>(() => Run(read, range, other, new(text), count =>
        {
            count.Should().BeGreaterThanOrEqualTo(last);
            (count - last).Should().BeLessThanOrEqualTo(256);
            last = count;
            if (count >= 256) throw sentinel;
        }));
        exception.Should().BeSameAs(sentinel);
        last.Should().Be(256);
        range.Start.Should().Be(start);
        range.End.Should().Be(end);
        subscription.TakePendingChange().Should().BeFalse();
        Run(read, range, other, new(text), null); // The failed invocation retains no active state.
    }

    [TestCase(Read.Boundaries)]
    [TestCase(Read.Point)]
    [TestCase(Read.Contains)]
    [TestCase(Read.Intersects)]
    public void WideSiblingReadsPreserveCancellationToken(Read read)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        for (var i = 0; i < 1024; i++) root.AppendParsedChild(document.CreateElement("child"));
        var range = document.CreateRange();
        range.SelectNodeContents(new(root));
        var other = document.CreateRange();
        other.SelectNodeContents(new(root.LastChild!));
        using var cancellation = new CancellationTokenSource();
        var last = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => Run(read, range, other, new(root.LastChild!), count =>
        {
            last = count;
            if (count >= 256) cancellation.Cancel();
        }, cancellation.Token));
        last.Should().Be(256);
        exception!.CancellationToken.Should().Be(cancellation.Token);
    }

    [Test]
    public void NestedRootAndBoundaryReadsShareTheirPollingRemainderWithoutRepeatedRoots()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var leaf = root;
        for (var i = 0; i < 90; i++)
        {
            var child = document.CreateElement("child");
            leaf.AppendParsedChild(child);
            leaf = child;
        }
        var range = document.CreateRange();
        range.SelectNodeContents(new(root));
        var counts = new List<int>();
        range.IsPointInRange(new(leaf), 0, counts.Add, default).Should().BeTrue();
        counts.Should().Contain(256); // Neither individual root nor comparison reaches 256.
        counts.Should().BeInAscendingOrder();
        counts[^1].Should().Be(284); // Two roots (92) and two comparisons (96 each).
    }

    [TestCase(Read.Ancestor)]
    [TestCase(Read.Text)]
    [TestCase(Read.Boundaries)]
    [TestCase(Read.Point)]
    [TestCase(Read.Contains)]
    [TestCase(Read.Intersects)]
    public void FastAndEmptyPathsCheckEntryAndExit(Read read)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var range = document.CreateRange();
        range.SelectNodeContents(new(root));
        var calls = 0;
        var sentinel = new InvalidOperationException("exit budget");
        Assert.Throws<InvalidOperationException>(() => Run(read, range, range, new(root), _ =>
        {
            if (++calls == 2) throw sentinel;
        })).Should().BeSameAs(sentinel);
        calls.Should().Be(2);
    }

    [Test]
    public void MutationDuringCopyIsRejectedBeforeReadingStaleCharacterOffsets()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode(new string('x', 1024));
        var range = document.CreateRange();
        range.SelectNodeContents(new(text));
        Assert.Throws<InvalidOperationException>(() => range.GetText(count =>
        {
            if (count == 256) text.Data = "";
        }, default));
        range.GetText().Should().BeEmpty();
    }

    [Test]
    public void DetachedAdoptionAndRangeOnlyMutationAreRejectedAtEntry()
    {
        var document = Document.CreateHtml();
        var destination = Document.CreateHtml();
        var root = document.CreateElement("root");
        var range = document.CreateRange();
        range.SelectNodeContents(new(root));
        Assert.Throws<InvalidOperationException>(() => range.IntersectsNode(new(root), _ => destination.AdoptNode(root), default));
        range.SelectNodeContents(new(root));
        var text = destination.CreateTextNode("x");
        Assert.Throws<InvalidOperationException>(() => range.GetCommonAncestor(_ => range.SelectNodeContents(new(text)), default));
    }

    [Test]
    public void SourceRangeAndForeignArgumentOwnersAreObservedEvenOnFalseResults()
    {
        var document = Document.CreateHtml();
        var foreign = Document.CreateHtml();
        var root = document.CreateElement("root");
        var otherRoot = foreign.CreateElement("other");
        otherRoot.AppendChild(foreign.CreateTextNode("x"));
        var range = document.CreateRange();
        range.SelectNodeContents(new(root));
        var other = foreign.CreateRange();
        other.SelectNodeContents(new(otherRoot));
        Assert.Throws<InvalidOperationException>(() => range.CompareBoundaryPoints(0, other,
            _ => other.Collapse(), default));
        Assert.Throws<InvalidOperationException>(() => range.IsPointInRange(new(otherRoot), 0,
            _ => otherRoot.AppendChild(foreign.CreateElement("mutated")), default));
    }

    [Test]
    public void OrdinaryFastReadsAllocateNothingAndNotifyNothing()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var range = document.CreateRange();
        var identity = new DomNodeIdentity(root);
        range.SelectNodeContents(identity);
        using var subscription = range.ObserveChanges(document);
        var start = range.Start;
        var end = range.End;
        void ReadAll()
        {
            range.GetCommonAncestor();
            range.GetText();
            range.CompareBoundaryPoints(0, range);
            range.ComparePoint(identity, 0);
            range.IsPointInRange(identity, 0);
            range.IntersectsNode(identity);
        }
        for (var i = 0; i < 100; i++) ReadAll();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) ReadAll();
        (GC.GetAllocatedBytesForCurrentThread() - allocated).Should().Be(0);
        range.Start.Should().Be(start);
        range.End.Should().Be(end);
        subscription.TakePendingChange().Should().BeFalse();
    }

    [Test]
    public void HostCancellationExceptionPassesThroughEvenIfTheCallbackAlsoMutated()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode("x");
        var range = document.CreateRange();
        range.SelectNodeContents(new(text));
        using var cancellation = new CancellationTokenSource();
        var sentinel = new OperationCanceledException(cancellation.Token);
        Assert.Throws<OperationCanceledException>(() => range.GetText(_ =>
        {
            text.Data = "changed";
            throw sentinel;
        }, default)).Should().BeSameAs(sentinel);
        range.GetText().Should().BeEmpty();
    }

    [Test]
    public void ReadReentryDoesNotShareOrRetainTheOuterInvocationState()
    {
        var document = Document.CreateHtml();
        var text = document.CreateTextNode(new string('x', 512));
        var range = document.CreateRange();
        range.SelectNodeContents(new(text));
        var last = 0;
        range.GetText(count =>
        {
            range.GetCommonAncestor().Node.Should().BeSameAs(text);
            count.Should().BeGreaterThanOrEqualTo(last);
            last = count;
        }, default).Should().Be(text.Data);
        last.Should().Be(513);
    }

    [TestCase(Read.Ancestor)]
    [TestCase(Read.Text)]
    [TestCase(Read.Boundaries)]
    [TestCase(Read.Point)]
    [TestCase(Read.Contains)]
    [TestCase(Read.Intersects)]
    public void AlreadyCanceledTokenWinsBeforeHostCheckpoint(Read read)
    {
        var document = Document.CreateHtml();
        var range = document.CreateRange();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => Run(read, range, range, new(document), _ =>
        {
            calls++;
            throw new InvalidOperationException("must not run");
        }, cancellation.Token));
        calls.Should().Be(0);
        exception!.CancellationToken.Should().Be(cancellation.Token);
    }

    [Test]
    public void ArgumentExceptionsRetainPrecedenceOverCancellationAndCallbacks()
    {
        var range = Document.CreateHtml().CreateRange();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action<int> forbidden = _ => throw new InvalidOperationException("must not run");
        Assert.Throws<DomException>(() => range.CompareBoundaryPoints(4, null!, forbidden, cancellation.Token))!.Name
            .Should().Be("NotSupportedError");
        Assert.Throws<ArgumentNullException>(() => range.CompareBoundaryPoints(0, null!, forbidden, cancellation.Token));
        Assert.Throws<ArgumentException>(() => range.ComparePoint(default, 0, forbidden, cancellation.Token));
        var detached = new DomNodeIdentity(Document.CreateHtml().CreateDocumentType("html"));
        Assert.Throws<DomException>(() => range.ComparePoint(detached, uint.MaxValue))!.Name.Should().Be("WrongDocumentError");
        range.IsPointInRange(detached, uint.MaxValue).Should().BeFalse();
    }
}

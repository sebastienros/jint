#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class LiveTraversalTests
{
    [TestCase(false)] [TestCase(true)]
    public void IteratorAcceptsDetachedFilterResultButPromotesRepairedCandidate(bool backwards)
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div");
        var a = doc.CreateElement("a"); var b = doc.CreateElement("b"); var c = doc.CreateElement("c");
        root.AppendChild(a); root.AppendChild(b); root.AppendChild(c);
        var iterator = new DomNodeIterator(new(root), 1);
        iterator.Next(null, default); iterator.Next(null, default); iterator.Next(null, default);
        if (backwards) iterator.Previous(null, default);
        var result = backwards ? iterator.Previous(Filter, default) : iterator.Next(Filter, default);
        var removed = backwards ? a : c;
        result!.Value.Node.Should().BeSameAs(removed);
        iterator.Reference.Node.Should().BeSameAs(b);
        iterator.PointerBeforeReference.Should().Be(backwards);
        ushort Filter(DomNodeIdentity node) { root.RemoveChild(node.Node!); return 1; }
    }
    [Test]
    public void IteratorTwoCaughtReentrantCallsPreserveOuterCandidateAndRecoverAfterThrow()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); root.AppendChild(doc.CreateTextNode("x"));
        var iterator = new DomNodeIterator(new(root), uint.MaxValue);
        iterator.Next(node =>
        {
            Assert.Throws<DomException>(() => iterator.Next(null, default))!.Name.Should().Be("InvalidStateError");
            Assert.Throws<DomException>(() => iterator.Next(null, default))!.Name.Should().Be("InvalidStateError");
            return 1;
        }, default)!.Value.Node.Should().BeSameAs(root);
        var failure = new InvalidOperationException("filter failure");
        Assert.Throws<InvalidOperationException>(() => iterator.Next(node => { root.RemoveChild(node.Node!); throw failure; }, default)).Should().BeSameAs(failure);
        iterator.Reference.Node.Should().BeSameAs(root);
        iterator.Next(null, default).Should().BeNull();
    }
    [Test]
    public void AttributeRootsAreSingletonsAndMasksSuppressCallbacks()
    {
        var doc = Document.CreateHtml(); var attr = doc.CreateAttribute("x"); var iterator = new DomNodeIterator(new(attr), 2);
        iterator.Next(null, default)!.Value.Attribute.Should().BeSameAs(attr);
        iterator.Detach(); iterator.Next(null, default).Should().BeNull();
        iterator.Previous(null, default)!.Value.Attribute.Should().BeSameAs(attr);
        var excluded = new DomNodeIterator(new(attr), 1);
        excluded.Next(_ => throw new InvalidOperationException(), default).Should().BeNull();
        var walker = new DomTreeWalker(new(attr), uint.MaxValue);
        walker.Next(null, default).Should().BeNull(); walker.Parent(null, default).Should().BeNull();
    }
    [Test]
    public void WalkerAllMovementsSkipAndRejectAndOutOfRootCurrent()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("root"); var a = doc.CreateElement("a"); var b = doc.CreateElement("b"); var c = doc.CreateElement("c");
        var aa = doc.CreateElement("aa"); a.AppendChild(aa); root.AppendChild(a); root.AppendChild(b); root.AppendChild(c);
        var walker = new DomTreeWalker(new(root), uint.MaxValue);
        walker.FirstChild(Filter, default)!.Value.Node.Should().BeSameAs(aa);
        walker.Parent(null, default)!.Value.Node.Should().BeSameAs(a);
        walker.NextSibling(Filter, default)!.Value.Node.Should().BeSameAs(c);
        walker.PreviousSibling(Filter, default)!.Value.Node.Should().BeSameAs(aa);
        walker.Current = new(root); walker.LastChild(null, default)!.Value.Node.Should().BeSameAs(c);
        walker.Previous(Filter, default)!.Value.Node.Should().BeSameAs(aa);
        walker.Next(Filter, default)!.Value.Node.Should().BeSameAs(c);
        root.RemoveChild(c); walker.Current.Node.Should().BeSameAs(c);
        var outside = doc.CreateElement("outside"); outside.AppendChild(doc.CreateTextNode("z")); walker.Current = new(outside);
        walker.FirstChild(null, default)!.Value.Node.Should().BeSameAs(outside.FirstChild);
        walker.Previous(_ => 99, default).Should().BeNull();
        ushort Filter(DomNodeIdentity identity) => ReferenceEquals(identity.Node, a) ? (ushort) 3 : ReferenceEquals(identity.Node, b) ? (ushort) 2 : (ushort) 1;
    }
    [Test]
    public void IteratorAdoptionRehomesRootAndRootRemovalDoesNotRetarget()
    {
        var doc = Document.CreateHtml(); var other = Document.CreateHtml(); var root = doc.CreateElement("div"); var child = doc.CreateElement("a"); root.AppendChild(child);
        var iterator = new DomNodeIterator(new(root), 1); iterator.Next(null, default); iterator.Next(null, default);
        other.AdoptNode(root); root.RemoveChild(child); iterator.Reference.Node.Should().BeSameAs(root);
        iterator.PointerBeforeReference.Should().BeFalse();
    }
}

[NonParallelizable]
public class LiveTrackingLifetimeTests
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<DomRange> DroppedRange(Document doc, Node child)
    {
        var range = new DomRange(doc); range.SelectNodeContents(new(child)); return new(range);
    }
    [Test]
    public void SweptOwnerBucketCanBeReusedAndRemoved()
    {
        var doc = Document.CreateHtml(); var parent = doc.CreateElement("div"); var child = doc.CreateTextNode("abc"); parent.AppendChild(child);
        var dead = DroppedRange(doc, child);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); dead.TryGetTarget(out _).Should().BeFalse();
        var sweep = new DomRange(doc); sweep.SelectNodeContents(new(parent));
        var reused = new DomRange(doc); reused.SelectNodeContents(new(child));
        parent.RemoveChild(child); reused.Start.Should().Be(new BoundaryPoint(new(parent), 0)); reused.Collapsed.Should().BeTrue();
        GC.KeepAlive(sweep);
    }
}

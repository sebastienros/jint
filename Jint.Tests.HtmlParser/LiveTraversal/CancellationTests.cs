#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.LiveTraversal;

public class CancellationTests
{
    [Test]
    public void SelectedTextCopyChecksWithin256CharactersAndKeepsLivePoints()
    {
        var doc = Document.CreateHtml(); var text = doc.CreateTextNode(new string('x', 4096)); var range = doc.CreateRange(); range.SelectNodeContents(new(text));
        using var cancellation = new CancellationTokenSource(); var observed = 0;
        Assert.Throws<OperationCanceledException>(() => range.GetText(steps =>
        {
            observed = steps; if (steps >= 256) cancellation.Cancel();
        }, cancellation.Token));
        observed.Should().Be(256); range.End.Offset.Should().Be(4096); text.Length.Should().Be(4096);
    }
    [Test]
    public void WalkerFinalAncestorAscentChecksWithin256LinksWithoutCallingFilter()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); var leaf = root;
        for (var i = 0; i < 4096; i++) { var child = doc.CreateElement("i"); leaf.AppendParsedChild(child); leaf = child; }
        var walker = new DomTreeWalker(new(root), 0) { Current = new(leaf) };
        using var cancellation = new CancellationTokenSource(); var observed = 0;
        Assert.Throws<OperationCanceledException>(() => walker.Parent(_ => throw new InvalidOperationException("mask must suppress filter"), steps =>
        {
            observed = steps; if (steps >= 256) cancellation.Cancel();
        }, cancellation.Token));
        observed.Should().Be(256); walker.Current.Node.Should().BeSameAs(leaf);
    }
    [Test]
    public void CancellationAfterCallbackPreservesMutationAndClearsActiveAndCandidate()
    {
        var doc = Document.CreateHtml(); var root = doc.CreateElement("div"); var child = doc.CreateTextNode("x"); root.AppendChild(child);
        var iterator = new DomNodeIterator(new(root), uint.MaxValue); iterator.Next(null);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => iterator.Next(identity =>
        {
            root.RemoveChild(identity.Node!); cancellation.Cancel(); return 1;
        }, cancellation.Token));
        iterator.Reference.Node.Should().BeSameAs(root); child.ParentNode.Should().BeNull();
        iterator.Next(null).Should().BeNull(); iterator.Previous(null)!.Value.Node.Should().BeSameAs(root);
    }
}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Traversal;

public class NodeTraversalTests
{
    [Test]
    public void DescendantsUsePreorderAndStayWithinTheirRoot()
    {
        var document = Document.CreateHtml();
        var outer = document.CreateElement("outer");
        var root = document.CreateElement("root");
        var first = document.CreateElement("first");
        var nested = document.CreateElement("nested");
        var second = document.CreateElement("second");
        var last = document.CreateElement("last");
        document.AppendChild(outer);
        outer.AppendChild(root);
        outer.AppendChild(document.CreateElement("outside"));
        root.AppendChild(document.CreateComment("before"));
        root.AppendChild(first);
        first.AppendChild(document.CreateTextNode("text"));
        first.AppendChild(nested);
        root.AppendChild(document.CreateProcessingInstruction("test", "data"));
        root.AppendChild(second);
        second.AppendChild(document.CreateComment("inside"));
        root.AppendChild(last);

        NodeTraversal.DescendantElements(root, default).Should().Equal(first, nested, second, last);
        NodeTraversal.DescendantElements(outer, default).First().Should().BeSameAs(root);
    }

    [Test]
    public void DetachedElementsAndExplicitFragmentRootsAreTraversed()
    {
        var document = Document.CreateHtml();
        var detached = document.CreateElement("detached");
        var child = document.CreateElement("child");
        detached.AppendChild(child);
        NodeTraversal.DescendantElements(detached, default).Should().Equal(child);

        var fragment = document.CreateDocumentFragment();
        var first = document.CreateElement("first");
        var nested = document.CreateElement("nested");
        var second = document.CreateElement("second");
        fragment.AppendChild(document.CreateComment("before"));
        fragment.AppendChild(first);
        first.AppendChild(nested);
        fragment.AppendChild(second);
        NodeTraversal.DescendantElements(fragment, default).Should().Equal(first, nested, second);
    }

    [Test]
    public void SiblingsSkipNonElementsInBothDirections()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("parent");
        var first = document.CreateElement("first");
        var middle = document.CreateElement("middle");
        var last = document.CreateElement("last");
        var comment = document.CreateComment("comment");
        var text = document.CreateTextNode("text");
        parent.AppendChild(first);
        parent.AppendChild(comment);
        parent.AppendChild(text);
        parent.AppendChild(middle);
        parent.AppendChild(document.CreateComment("other"));
        parent.AppendChild(last);

        NodeTraversal.NextElementSibling(first, default).Should().BeSameAs(middle);
        NodeTraversal.PreviousElementSibling(middle, default).Should().BeSameAs(first);
        NodeTraversal.NextElementSibling(comment, default).Should().BeSameAs(middle);
        NodeTraversal.PreviousElementSibling(text, default).Should().BeSameAs(first);
        NodeTraversal.NextElementSibling(last, default).Should().BeNull();
        NodeTraversal.PreviousElementSibling(first, default).Should().BeNull();
        NodeTraversal.NextElementSibling(document.CreateElement("detached"), default).Should().BeNull();
    }

    [Test]
    public void EmptyTreesAndRequiredNullArguments()
    {
        var document = Document.CreateHtml();
        NodeTraversal.DescendantElements(document, default).Should().BeEmpty();
        NodeTraversal.DescendantElements(document.CreateElement("empty"), default).Should().BeEmpty();
        NodeTraversal.DescendantElements(document.CreateDocumentFragment(), default).Should().BeEmpty();

        var invalid = NodeTraversal.DescendantElements(null!, default);
        Assert.Throws<ArgumentNullException>(() => invalid.ToList());
        Assert.Throws<ArgumentNullException>(() => NodeTraversal.PreviousElementSibling(null!, default));
        Assert.Throws<ArgumentNullException>(() => NodeTraversal.NextElementSibling(null!, default));
    }

    [Test]
    public void DeepChainsAreTraversedWithoutRecursion()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var current = root;
        for (var i = 0; i < 20_000; i++)
        {
            var child = document.CreateElement("child");
            current.AppendClonedChild(child);
            current = child;
        }

        var count = 0;
        foreach (var element in NodeTraversal.DescendantElements(root, default))
        {
            count++;
            if (count == 20_000)
            {
                element.Should().BeSameAs(current);
            }
        }

        count.Should().Be(20_000);
    }

    [Test]
    public void PreCancellationIsCheckedOnEnumerationAndSiblingEntry()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var child = document.CreateElement("child");
        root.AppendChild(child);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var descendants = NodeTraversal.DescendantElements(root, source.Token);
        Assert.Throws<OperationCanceledException>(() => descendants.ToList());
        Assert.Throws<OperationCanceledException>(() => NodeTraversal.PreviousElementSibling(child, source.Token));
        Assert.Throws<OperationCanceledException>(() => NodeTraversal.NextElementSibling(child, source.Token));
    }

    [Test]
    public void CancellationDuringLongSiblingAndDescendantWalksIsObserved()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var current = document.CreateElement("first");
        root.AppendChild(current);
        for (var i = 0; i < 600; i++)
        {
            root.AppendChild(document.CreateComment("skip"));
        }
        var last = document.CreateElement("last");
        root.AppendChild(last);

        using var nextCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => NodeTraversal.NextElementSibling(
            current, nextCancellation.Cancel, nextCancellation.Token));
        using var previousCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => NodeTraversal.PreviousElementSibling(
            last, previousCancellation.Cancel, previousCancellation.Token));

        using var walkCancellation = new CancellationTokenSource();
        using var walk = NodeTraversal.DescendantElements(root, walkCancellation.Token).GetEnumerator();
        walk.MoveNext().Should().BeTrue();
        walk.Current.Should().BeSameAs(current);
        walkCancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => walk.MoveNext());
    }

    [Test]
    public void FinalDeepAscentPollsCancellationAfterTheLastYield()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        var current = root;
        // 1 root-child link + 597 child checks + 2 * 597 ascent steps =
        // 1792 work steps. The seventh checkpoint is the final parent link.
        const int depth = 597;
        for (var i = 0; i < depth; i++)
        {
            var child = document.CreateElement("child");
            current.AppendClonedChild(child);
            current = child;
        }

        using var source = new CancellationTokenSource();
        var lastWasYielded = false;
        var checkpoints = 0;
        using var walk = NodeTraversal.DescendantElements(root, () =>
        {
            checkpoints++;
            if (checkpoints == 7)
            {
                lastWasYielded.Should().BeTrue();
                source.Cancel();
            }
        }, source.Token).GetEnumerator();

        for (var i = 0; i < depth; i++)
        {
            walk.MoveNext().Should().BeTrue();
        }
        walk.Current.Should().BeSameAs(current);
        lastWasYielded = true;

        Assert.Throws<OperationCanceledException>(() => walk.MoveNext());
        checkpoints.Should().Be(7);
    }
}

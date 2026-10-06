#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

public sealed class NthPositionWorkTests
{
    private static CompiledSelector Parse(string source) => SelectorCompiler.Compile(source, null, default);

    [TestCase(":nth-child(n)")]
    [TestCase(":nth-last-child(n)")]
    [TestCase(":nth-of-type(n)")]
    [TestCase(":nth-last-of-type(n)")]
    [TestCase(":nth-child(n of .x)")]
    [TestCase(":nth-last-child(n of :is(.x, .missing))")]
    [TestCase(":nth-child(n of :nth-child(odd))")]
    public void RepeatedForwardAndReverseMatchesHaveLinearNativeWork(string selector)
    {
        foreach (var size in new[] { 1024, 4096 })
        {
            var (document, root, items) = Tree(size);
            var program = Parse(selector);
            var work = new SelectorMatchWork(document, default);
            var cell = work.EnsureCell();
            for (var i = 0; i < size; i++)
            {
                SelectorMatcher.Matches(program, items[i], null, default, ref work)
                    .Should().Be(!selector.Contains("of :nth") || i % 2 == 0);
            }
            var forward = cell.Native.Steps;
            // Count actual native steps, not time or number of result publications.
            forward.Should().BeLessThan(size * 100);
            for (var i = size - 1; i >= 0; i--)
                SelectorMatcher.Matches(program, items[i], null, default, ref work)
                    .Should().Be(!selector.Contains("of :nth") || i % 2 == 0);
            (cell.Native.Steps - forward).Should().BeLessThan(size * 40);
            var queryWork = new SelectorMatchWork(root, default);
            var queryCell = queryWork.EnsureCell();
            SelectorMatcher.QuerySelectorAll(program, root, default, ref queryWork)
                .Should().HaveCount(selector.Contains("of :nth") ? size / 2 : size);
            queryCell.Native.Steps.Should().BeLessThan(size * 100);
        }
    }

    [Test]
    public void FilterIndexesRespectScopeAndSubjectMembership()
    {
        var (document, root, items) = Tree(8);
        var work = new SelectorMatchWork(document, default);
        var program = Parse(":nth-child(1 of :scope)");
        foreach (var item in items)
            SelectorMatcher.Matches(program, item, null, default, ref work).Should().BeTrue();
        SelectorMatcher.QuerySelectorAll(program, root).Should().BeEmpty();
        var missing = Parse(":nth-last-child(-n+3 of .missing)");
        foreach (var item in items)
            SelectorMatcher.Matches(missing, item, root, default, ref work).Should().BeFalse();
    }

    [Test]
    public void ExpandedNamesAndNegativeFormulasIgnoreCharacterSiblings()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        var elements = new[] { document.CreateElement("a"), document.CreateElementNS(Namespaces.Svg, "a"),
            document.CreateElement("b"), document.CreateElement("a"), document.CreateElementNS(Namespaces.Svg, "a") };
        foreach (var element in elements)
        {
            root.AppendChild(document.CreateComment("gap"));
            root.AppendChild(element);
            root.AppendChild(document.CreateTextNode("gap"));
        }
        SelectorMatcher.QuerySelectorAll(Parse(":nth-of-type(2)"), root).Should().Equal(elements[3], elements[4]);
        SelectorMatcher.QuerySelectorAll(Parse(":nth-last-of-type(2)"), root).Should().Equal(elements[0], elements[1]);
        SelectorMatcher.QuerySelectorAll(Parse(":nth-child(-2n+5)"), root).Should().Equal(elements[0], elements[2], elements[4]);
        SelectorMatcher.QuerySelectorAll(Parse(":nth-last-child(-n+2)"), root).Should().Equal(elements[3], elements[4]);
    }

    [TestCase(":nth-child(n)")]
    [TestCase(":nth-child(n of .x)")]
    public void WarmIndexesRejectMutationAndCancellation(string selector)
    {
        var (document, root, items) = Tree(512);
        var program = Parse(selector);
        var work = new SelectorMatchWork(document, default);
        SelectorMatcher.Matches(program, items[0], root, default, ref work).Should().BeTrue();
        root.InsertBefore(items[^1], items[0]);
        Action stale = () => SelectorMatcher.Matches(program, items[0], root, default, ref work);
        stale.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        SelectorMatcher.QuerySelectorAll(Parse(":nth-child(1)"), root).Should().Equal(items[^1]);
        using var source = new CancellationTokenSource();
        var canceled = new SelectorMatchWork(document, source.Token);
        SelectorMatcher.Matches(program, items[0], root, default, ref canceled).Should().BeTrue();
        source.Cancel();
        Action read = () => SelectorMatcher.Matches(program, items[0], root, default, ref canceled);
        read.Should().Throw<OperationCanceledException>();
    }

    [TestCase(":nth-child(n)")]
    [TestCase(":nth-child(n of .x)")]
    public void IndexBuildRejectsCheckpointMutationBeforePublication(string selector)
    {
        var (document, root, items) = Tree(1024);
        var program = Parse(selector);
        var changed = false;
        Action read = () => SelectorMatcher.Matches(program, items[0], root, () =>
        {
            if (changed) return;
            changed = true;
            items[1].SetAttribute("class", "missing");
        }, default);
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        changed.Should().BeTrue();
        SelectorMatcher.QuerySelectorAll(program, root).Should().HaveCount(selector.Contains(" of ") ? 1023 : 1024);
    }

    [TestCase(":nth-child(2)")]
    [TestCase(":nth-last-of-type(2)")]
    [TestCase(":nth-child(2 of .x)")]
    public void SharedCompiledProgramKeepsIndexesInEachDocumentTraversal(string selector)
    {
        var program = Parse(selector);
        var (left, leftRoot, leftItems) = Tree(4);
        var (right, rightRoot, rightItems) = Tree(6);
        var leftWork = new SelectorMatchWork(left, default);
        var rightWork = new SelectorMatchWork(right, default);
        SelectorMatcher.Matches(program, leftItems[1], leftRoot, default, ref leftWork).Should().BeTrue();
        SelectorMatcher.Matches(program, rightItems[1], rightRoot, default, ref rightWork)
            .Should().Be(!selector.Contains("of-type"));
        right.AdoptNode(leftItems[0]);
        rightRoot.AppendChild(leftItems[0]);
        Action stale = () => SelectorMatcher.Matches(program, leftItems[1], leftRoot, default, ref leftWork);
        stale.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
        SelectorMatcher.QuerySelectorAll(program, leftRoot).Should().HaveCount(1);
    }

    [TestCase(":nth-child(n)")]
    [TestCase(":nth-child(n of .x)")]
    public void CanceledIndexBuildDoesNotPublishAPartialResult(string selector)
    {
        var (document, root, items) = Tree(1024);
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        var work = new SelectorMatchWork(document, source.Token, () =>
        {
            if (++checkpoints == 2) source.Cancel();
        });
        var program = Parse(selector);
        Action read = () => SelectorMatcher.Matches(program, items[0], root, default, ref work);
        read.Should().Throw<OperationCanceledException>();
        checkpoints.Should().Be(2);
        SelectorMatcher.QuerySelectorAll(program, root).Should().HaveCount(items.Length);
    }

    [TestCase(":nth-child(n)")]
    [TestCase(":nth-child(n of .x)")]
    public void CachedIndexReadStillChecksTheMutationWitness(string selector)
    {
        var (document, root, items) = Tree(512);
        var change = false;
        var work = new SelectorMatchWork(document, default, () =>
        {
            if (change) root.RemoveChild(items[1]);
        });
        var program = Parse(selector);
        SelectorMatcher.Matches(program, items[0], root, default, ref work).Should().BeTrue();
        change = true;
        Action read = () => SelectorMatcher.Matches(program, items[0], root, default, ref work);
        read.Should().Throw<InvalidOperationException>().WithMessage(SelectorMatchWork.Invalidated);
    }

    [TestCase(":nth-child(n)")]
    [TestCase(":nth-child(n of .x)")]
    public void InterruptedIndexBuildCanRetryWithTheSameUnchangedWork(string selector)
    {
        var (document, root, items) = Tree(512);
        var interrupt = true;
        var failure = new ApplicationException("host checkpoint");
        var work = new SelectorMatchWork(document, default, () =>
        {
            if (interrupt) throw failure;
        });
        var program = Parse(selector);
        Action read = () => SelectorMatcher.Matches(program, items[0], root, default, ref work);
        read.Should().Throw<ApplicationException>().Which.Should().BeSameAs(failure);
        interrupt = false;
        SelectorMatcher.Matches(program, items[^1], root, default, ref work).Should().BeTrue();
    }

    private static (Document Document, Element Root, Element[] Items) Tree(int size)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("root");
        document.AppendChild(root);
        var items = new Element[size];
        for (var i = 0; i < size; i++)
        {
            root.AppendChild(document.CreateComment("gap"));
            items[i] = document.CreateElement(i % 2 == 0 ? "a" : "b");
            items[i].SetAttribute("class", "x");
            root.AppendChild(items[i]);
        }
        return (document, root, items);
    }
}

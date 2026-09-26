#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class FormIndexWorkTests
{
    [Test]
    public void RepeatedExplicitControlInsertionDoesNotRescanDuplicateIds()
    {
        var small = CountExplicitInsertions(128);
        var large = CountExplicitInsertions(256);
        TestContext.Out.WriteLine($"explicit duplicate-ID insertion visits: {small}, {large}");
        large.Should().BeLessThanOrEqualTo(2 * small);
    }

    [Test]
    public void DuplicateIdOrderChangeRebuildsOnlyOncePerChangedBucket()
    {
        var small = CountDuplicateOrderChange(128);
        var large = CountDuplicateOrderChange(256);
        TestContext.Out.WriteLine($"duplicate-ID order-change visits: {small}, {large}");
        large.Should().BeLessThanOrEqualTo(2 * small + 32);
    }

    [Test]
    public void DeepControlFreeParserAndCloneAppendsWithIdsAvoidFormRootWalks()
    {
        var parserSmall = CountDeepAppends(128, clone: false);
        var parserLarge = CountDeepAppends(256, clone: false);
        var cloneSmall = CountDeepAppends(128, clone: true);
        var cloneLarge = CountDeepAppends(256, clone: true);
        TestContext.Out.WriteLine($"control-free deep parser/clone visits: {parserSmall}, {parserLarge}, {cloneSmall}, {cloneLarge}");
        (parserSmall + parserLarge + cloneSmall + cloneLarge).Should().Be(0);
    }

    [Test]
    public void SnapshotRootAscentChecksCancellationBeforeAndDuringWalk()
    {
        var document = Document.CreateHtml();
        var current = document.CreateElement("div");
        document.AppendChild(current);
        for (var i = 0; i < 600; i++)
        {
            var child = document.CreateElement("div");
            current.AppendParsedChild(child);
            current = child;
        }

        var form = document.CreateElement("form");
        current.AppendParsedChild(form);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            HtmlFormState.SnapshotAssociatedElements(form, canceled.Token));

        using var duringWalk = new CancellationTokenSource();
        var checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlFormState.SnapshotAssociatedElements(form,
            _ =>
            {
                checkpoints++;
                duringWalk.Cancel();
            }, duringWalk.Token));
        checkpoints.Should().Be(1);
    }

    [Test]
    public void DetachedRootIndexIsReleasedWhenRootJoinsAnotherTree()
    {
        var document = Document.CreateHtml();
        var container = document.CreateElement("main");
        document.AppendChild(container);
        var detached = document.CreateElement("section");
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        detached.AppendChild(form);
        var control = document.CreateElement("input");
        control.SetAttribute("form", "f");
        detached.AppendChild(control);
        var detachedIndex = HtmlFormIndex.GetOrCreate(detached);
        detached.FormIndex.Should().BeSameAs(detachedIndex);
        container.AppendChild(detached);
        detached.FormIndex.Should().BeNull();
        document.FormIndex.Should().NotBeNull();
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        container.RemoveChild(detached);
        document.FormIndex!.Referencing("f").Should().BeEmpty();
    }

    private static long CountExplicitInsertions(int count)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "x");
        root.AppendChild(form);
        for (var i = 0; i < count; i++)
        {
            var duplicate = document.CreateElement("div");
            duplicate.SetAttribute("id", "x");
            root.AppendChild(duplicate);
        }

        using var probe = new HtmlFormWorkProbe(document);
        for (var i = 0; i < count; i++)
        {
            var control = document.CreateElement("input");
            control.SetAttribute("form", "x");
            root.AppendChild(control);
            HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        }

        return probe.Visits;
    }

    private static long CountDuplicateOrderChange(int count)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        for (var i = 0; i < count; i++)
        {
            var unrelated = document.CreateElement("div");
            unrelated.SetAttribute("id", "other");
            root.AppendChild(unrelated);
        }

        var blocker = document.CreateElement("div");
        blocker.SetAttribute("id", "x");
        root.AppendChild(blocker);
        for (var i = 0; i < count; i++)
        {
            var duplicate = document.CreateElement("div");
            duplicate.SetAttribute("id", "x");
            root.AppendChild(duplicate);
        }

        var form = document.CreateElement("form");
        form.SetAttribute("id", "x");
        root.AppendChild(form);
        var control = document.CreateElement("input");
        control.SetAttribute("form", "x");
        root.AppendChild(control);
        HtmlFormState.GetOwner(control).Should().BeNull();
        using var probe = new HtmlFormWorkProbe(document);
        root.RemoveChild(blocker);
        HtmlFormState.GetOwner(control).Should().BeNull();
        root.InsertBefore(form, root.FirstChild);
        HtmlFormState.GetOwner(control).Should().BeSameAs(form);
        return probe.Visits;
    }

    private static long CountDeepAppends(int count, bool clone)
    {
        var document = Document.CreateHtml();
        var current = document.CreateElement("div");
        document.AppendChild(current);
        var source = document.CreateElement("div");
        source.SetAttribute("id", "source-id");
        using var probe = new HtmlFormWorkProbe(document);
        for (var i = 0; i < count; i++)
        {
            var child = clone ? NodeCloner.Clone(source, document, deep: false) : document.CreateElement("div");
            if (!clone)
            {
                ((Element)child).SetAttribute("id", $"node-{i}");
            }

            if (clone)
            {
                current.AppendClonedChild(child);
            }
            else
            {
                current.AppendParsedChild(child);
            }

            current = (Element)child;
        }

        current.SetAttribute("class", "plain");
        current.SetAttribute("id", "unrelated");
        return probe.Visits;
    }
}

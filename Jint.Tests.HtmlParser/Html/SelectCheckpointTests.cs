#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectCheckpointTests
{
    private sealed class CheckpointException : Exception;

    [TestCase(512)]
    [TestCase(549)]
    public void ProducerBoundaryChecksDoNotCountCompletedWorkAgain(int completedUnits)
    {
        var observed = new List<int>();
        var context = HtmlSelectWorkContext.Create(observed.Add, default)!;
        var report = context.CreateCheckpointAdapter();
        report(0);
        report(256);
        report(512);
        if (completedUnits != 512) report(completedUnits);
        report(completedUnits);
        report(completedUnits);
        context.Check();

        observed.Should().Equal(0, 256, 512, completedUnits, completedUnits, completedUnits);
    }

    [TestCase("option")]
    [TestCase("select")]
    public void FirstViewCheckpointInterruptsMetadataWithoutPublishingEnhancedState(string kind)
    {
        var document = Document.CreateHtml(); var element = document.CreateElement(kind);
        element.InitializeParsedAttributes(Enumerable.Range(0, 1024)
            .Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        var observed = new List<int>();
        void Check(int units) { observed.Add(units); if (units == 256) throw new CheckpointException(); }
        Assert.Throws<CheckpointException>(() =>
        {
            if (kind == "option") element.GetHtmlState()!.GetOptionState(Check, default);
            else element.GetHtmlState()!.GetSelectState(Check, default);
        });
        observed.Should().Contain(256);
        element.ExistingSelectState.Should().BeNull(); element.ExistingOptionState.Should().BeNull();
        document.SelectWorkProbe.Should().BeNull();
    }

    [Test]
    public void OneCounterSpansInventoryMetadataTextAndValueMatching()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        for (var i = 0; i < 1000; i++)
        {
            var option = document.CreateElement("option");
            option.AppendClonedChild(document.CreateTextNode("abcdefgh")); select.AppendChild(option);
        }
        var state = select.GetHtmlState()!.Select!;
        var stamp = document.MutationStamp;
        var observed = new List<int>();
        void Check(int units) { observed.Add(units); if (units >= 4096) throw new CheckpointException(); }
        Assert.Throws<CheckpointException>(() => state.SetValue("missing", Check, default));
        observed.Zip(observed.Skip(1)).All(pair => pair.First <= pair.Second).Should().BeTrue();
        observed.Should().Contain(4096);
        state.GetSelectedIndex(default).Should().Be(0); document.MutationStamp.Should().Be(stamp);
        foreach (var option in HtmlSelectCore.Enumerate(select, default)) option.GetOptionCore().DirtySelectedness.Should().BeFalse();
    }

    [Test]
    public void OptionTextChecksRealCharactersAndDoesNotInstallDocumentProbe()
    {
        var document = Document.CreateHtml(); var option = document.CreateElement("option");
        option.AppendParsedChild(document.CreateTextNode(new string('x', 1000000)));
        var state = option.GetHtmlState()!.Option!;
        var last = 0;
        Assert.Throws<CheckpointException>(() => state.GetText(units =>
        {
            last = units; if (units == 256) throw new CheckpointException();
        }, default));
        last.Should().Be(256); document.SelectWorkProbe.Should().BeNull();
    }

    [Test]
    public void DisabledFallbackSharesCounterAndCancelsBeforeFlagsChange()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var disabled = document.CreateElement("option");
        disabled.InitializeParsedAttributes(Enumerable.Range(0, 1024)
            .Select(i => new ParserAttribute(null, "data-" + i, null, "x"))
            .Append(new ParserAttribute(null, "disabled", null, "")).ToArray(), default);
        select.AppendParsedChild(disabled);
        var chosen = document.CreateElement("option"); select.AppendParsedChild(chosen);
        var state = chosen.GetHtmlState()!.Option!; var stamp = document.MutationStamp;
        Assert.Throws<CheckpointException>(() => state.SetSelected(false, units =>
        {
            if (units == 256) throw new CheckpointException();
        }, default));
        state.Selected.Should().BeTrue(); state.DirtySelectedness.Should().BeFalse();
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void DefaultSelectedPreparesFallbackBeforePublishingAttributeOrFlags()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        for (var i = 0; i < 1000; i++)
        {
            var disabled = document.CreateElement("option"); disabled.SetAttribute("disabled", ""); select.AppendParsedChild(disabled);
        }
        var chosen = document.CreateElement("option"); chosen.SetAttribute("selected", ""); select.AppendParsedChild(chosen);
        var state = chosen.GetHtmlState()!.Option!; var stamp = document.MutationStamp;
        Assert.Throws<CheckpointException>(() => state.SetDefaultSelected(false, units =>
        {
            if (units == 256) throw new CheckpointException();
        }, default));
        chosen.GetAttribute("selected").Should().Be(string.Empty);
        state.DefaultSelected.Should().BeTrue(); state.Selected.Should().BeTrue(); state.DirtySelectedness.Should().BeFalse();
        document.MutationStamp.Should().Be(stamp);
        state.SetDefaultSelected(false, null, default);
        state.DefaultSelected.Should().BeFalse(); state.Selected.Should().BeTrue(); // First enabled option remains the fallback.
    }

    [Test]
    public void SelectedContentCloneCheckpointRunsBeforeSelectionPublication()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select"); document.AppendChild(select);
        var content = document.CreateElement("selectedcontent"); select.AppendChild(content);
        var first = document.CreateElement("option"); first.AppendClonedChild(document.CreateTextNode("first")); select.AppendChild(first);
        var second = document.CreateElement("option"); var subtree = document.CreateElement("div");
        subtree.InitializeParsedAttributes(Enumerable.Range(0, 1024)
            .Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        second.AppendClonedChild(subtree); select.AppendChild(second);
        HtmlSelectedContent.MaybeCloneOption(first, default);
        var state = select.GetHtmlState()!.Select!; var stamp = document.MutationStamp;
        Assert.Throws<CheckpointException>(() => state.SetSelectedIndex(1, units =>
        {
            if (units == 256) throw new CheckpointException();
        }, default));
        state.GetSelectedIndex(default).Should().Be(0); ((Text) content.FirstChild!).Data.Should().Be("first");
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void CollectionGrowthChecksDetachedPreparationAndRemovalChecksBetweenMutations()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var options = select.GetHtmlState()!.Select!.Options;
        Assert.Throws<CheckpointException>(() => options.SetLength(1000, units =>
        {
            if (units == 256) throw new CheckpointException();
        }, default));
        options.Count.Should().Be(0);
        options.SetLength(1000, default);
        Assert.Throws<CheckpointException>(() => options.SetLength(0, units =>
        {
            if (units == 256) throw new CheckpointException();
        }, default));
        options.Count.Should().BeGreaterThan(0).And.BeLessThan(1000);
        options.Count.Should().Be(select.ChildCount);
        foreach (var option in HtmlSelectCore.Enumerate(select, default)) option.GetOptionCore().CachedNearestSelect.Should().BeSameAs(select);
    }
}

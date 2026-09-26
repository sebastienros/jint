#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectPostMutationCheckpointTests
{
    private sealed class UnexpectedCheckpointException : Exception;

    [Test]
    public void TextMutationChecksCancellationAfterItsCoherentCommit()
    {
        var document = Document.CreateHtml(); var option = document.CreateElement("option");
        option.AppendChild(document.CreateTextNode("old"));
        var state = option.GetHtmlState()!.Option!;
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.SetText("new", _ =>
        {
            if (option.FirstChild is Text { Data: "new" }) cts.Cancel();
        }, cts.Token));
        option.ChildCount.Should().Be(1); ((Text) option.FirstChild!).Data.Should().Be("new");
    }

    [Test]
    public void RemovalChecksCancellationAfterMembershipAndSelectionAreCoherent()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var first = document.CreateElement("option"); var second = document.CreateElement("option");
        select.AppendChild(first); select.AppendChild(second);
        var state = select.GetHtmlState()!.Select!;
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.Options.Remove(0, _ =>
        {
            if (first.ParentNode is null) cts.Cancel();
        }, cts.Token));
        state.Options.Count.Should().Be(1); state.GetSelectedIndex(default).Should().Be(0);
        second.GetOptionCore().Selected.Should().BeTrue(); first.GetOptionCore().CachedNearestSelect.Should().BeNull();
    }

    [Test]
    public void AdditionChecksCancellationAfterSelectedEntrantWins()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var first = document.CreateElement("option"); select.AppendChild(first);
        var entrant = document.CreateElement("option"); entrant.SetAttribute("selected", "");
        var state = select.GetHtmlState()!.Select!;
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.Options.Add(entrant, (int?) null, _ =>
        {
            if (entrant.ParentNode == select) cts.Cancel();
        }, cts.Token));
        state.Options.Count.Should().Be(2); state.GetSelectedIndex(default).Should().Be(1);
        first.GetOptionCore().Selected.Should().BeFalse(); entrant.GetOptionCore().Selected.Should().BeTrue();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IndexedGrowthChecksAfterEachCommittedMutation(bool cancelAfterIncoming)
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        select.AppendChild(document.CreateElement("option"));
        var incoming = document.CreateElement("option");
        var options = select.GetHtmlState()!.Select!.Options;
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => options.SetIndexed(2, incoming, _ =>
        {
            if (cancelAfterIncoming ? incoming.ParentNode == select : select.ChildCount == 2) cts.Cancel();
        }, cts.Token));
        options.Count.Should().Be(cancelAfterIncoming ? 3 : 2);
        incoming.ParentNode.Should().Be(cancelAfterIncoming ? select : null);
        options.Count.Should().Be(select.ChildCount);
    }

    [Test]
    public void FinalLengthRemovalChecksCancellationAfterLastMutation()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        select.AppendChild(document.CreateElement("option"));
        var options = select.GetHtmlState()!.Select!.Options;
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => options.SetLength(0, _ =>
        {
            if (select.ChildCount == 0) cts.Cancel();
        }, cts.Token));
        options.Count.Should().Be(0);
    }

    [Test]
    public void AttributeCommitChecksCancellationAfterValueAndMetadataAgree()
    {
        var document = Document.CreateHtml(); var option = document.CreateElement("option");
        var state = option.GetHtmlState()!.Option!;
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.SetValue("new", _ =>
        {
            if (option.GetAttribute("value") == "new") cts.Cancel();
        }, cts.Token));
        option.GetAttribute("value").Should().Be("new"); state.GetValue(default).Should().Be("new");
    }

    [Test]
    public void ViewPublicationChecksCancellationWithoutDiscardingCompletedMetadata()
    {
        var document = Document.CreateHtml(); var option = document.CreateElement("option");
        option.SetAttribute("selected", "");
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => option.GetHtmlState()!.GetOptionState(_ =>
        {
            if (option.ExistingOptionState is not null) cts.Cancel();
        }, cts.Token));
        option.ExistingOptionState!.DefaultSelected.Should().BeTrue(); option.GetOptionCore().Selected.Should().BeTrue();
    }

    [Test]
    public void PostcheckNeverReplacesAnOriginalNativeException()
    {
        var document = Document.CreateHtml(); var state = document.CreateElement("option").GetHtmlState()!.Option!;
        var calls = 0;
        Assert.Throws<ArgumentNullException>(() => state.SetText(null!, _ =>
        {
            if (++calls > 1) throw new UnexpectedCheckpointException();
        }, default));
        calls.Should().Be(1);
    }

    [Test]
    public void SelectedContentCloneChecksActualTextareaCopyWorkBeforeSelectionChanges()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select"); document.AppendChild(select);
        var content = document.CreateElement("selectedcontent"); select.AppendChild(content);
        var first = document.CreateElement("option"); first.AppendChild(document.CreateTextNode("first")); select.AppendChild(first);
        var second = document.CreateElement("option"); var textarea = document.CreateElement("textarea");
        textarea.AppendChild(document.CreateTextNode(new string('x', 1000000))); second.AppendChild(textarea); select.AppendChild(second);
        HtmlSelectedContent.MaybeCloneOption(first, default);
        var state = select.GetHtmlState()!.Select!; state.GetSelectedIndex(default).Should().Be(0);
        var stamp = document.MutationStamp;
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        using var cts = new CancellationTokenSource();
        var reported = new List<int>();
        Assert.Throws<OperationCanceledException>(() => state.SetSelectedIndex(1, units =>
        {
            reported.Add(units); if (units >= 256) cts.Cancel();
        }, cts.Token));
        probe.Units.Should().BeLessThan(256); // The callback came from the text helper's actual scan.
        reported.Max().Should().BeGreaterThanOrEqualTo(256);
        reported.Zip(reported.Skip(1)).All(pair => pair.First <= pair.Second).Should().BeTrue();
        state.GetSelectedIndex(default).Should().Be(0); ((Text) content.FirstChild!).Data.Should().Be("first");
        document.MutationStamp.Should().Be(stamp); textarea.GetHtmlState()!.TextArea!.DirtyValue.Should().BeFalse();
    }
}

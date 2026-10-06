#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectStateTests
{
    private static Element Option(Document document, string text = "", bool selected = false)
    {
        var option = document.CreateElement("option");
        if (text.Length != 0) option.AppendChild(document.CreateTextNode(text));
        if (selected) option.SetAttribute("selected", "");
        return option;
    }
    [Test]
    public void ViewIsStableAndRequiresExactHtmlNames()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        select.GetHtmlState()!.Select.Should().BeSameAs(select.GetHtmlState()!.Select);
        select.GetHtmlState()!.Option.Should().BeNull();
        var option = document.CreateElement("option");
        option.GetHtmlState()!.Option.Should().BeSameAs(option.GetHtmlState()!.Option);
        option.GetHtmlState()!.Select.Should().BeNull();
        var xml = Document.CreateXml();
        xml.CreateElementNS(Namespaces.Html, "SELECT").GetHtmlState()!.Select.Should().BeNull();
        xml.CreateElementNS(Namespaces.Html, "select").GetHtmlState()!.Select.Should().NotBeNull();
        xml.CreateElement("select").GetHtmlState().Should().BeNull();
    }
    [Test]
    public void SelectionDirtyFlagsDefaultAttributesAndResetRemainIndependent()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var a = Option(document, "a");
        var b = Option(document, "b", true);
        select.AppendChild(a);
        select.AppendChild(b);
        var state = select.GetHtmlState()!.Select!;
        state.GetSelectedIndex(default).Should().Be(1);
        a.GetHtmlState()!.Option!.Selected.Should().BeFalse();
        state.SetSelectedIndex(0, default);
        a.GetHtmlState()!.Option!.DirtySelectedness.Should().BeTrue();
        b.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
        a.SetAttribute("selected", "");
        a.RemoveAttribute("selected");
        state.GetValue(default).Should().Be("a");
        b.RemoveAttribute("selected");
        b.SetAttribute("selected", "false");
        state.GetValue(default).Should().Be("b");
        state.SetSelectedIndex(-1, default);
        state.GetSelectedIndex(default).Should().Be(-1);
        state.SetUserValidity(true);
        state.Reset(default);
        state.UserValidity.Should().BeFalse();
        state.GetValue(default).Should().Be("b");
        a.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
        b.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
    }
    [Test]
    public void DeselectingAnOptionAsksForFallbackWithoutDirtyingPeers()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var a = Option(document); var b = Option(document);
        select.AppendChild(a); select.AppendChild(b);
        b.GetHtmlState()!.Option!.SetSelected(true, default);
        a.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
        b.GetHtmlState()!.Option!.SetSelected(false, default);
        a.GetHtmlState()!.Option!.Selected.Should().BeTrue();
        a.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
        b.GetHtmlState()!.Option!.DirtySelectedness.Should().BeTrue();
        select.SetAttribute("size", "2");
        a.GetHtmlState()!.Option!.SetSelected(false, default);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(-1);
    }
    [Test]
    public void MultipleTransitionsKeepLastSelectedAndNoMatchDoesNotRepairOnRead()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        select.SetAttribute("multiple", "");
        var a = Option(document, "same", true); var b = Option(document, "same", true);
        select.AppendChild(a); select.AppendChild(b);
        var state = select.GetHtmlState()!.Select!;
        state.SelectedOptions.Count.Should().Be(2);
        state.SetValue("same", default);
        state.GetSelectedIndex(default).Should().Be(0);
        b.GetHtmlState()!.Option!.SetSelected(true, default);
        select.RemoveAttribute("multiple");
        state.GetSelectedIndex(default).Should().Be(1);
        state.SetValue("missing", default);
        state.GetSelectedIndex(default).Should().Be(-1);
        state.GetSelectedIndex(default).Should().Be(-1);
        state.SelectedOptions.Count.Should().Be(0);
    }
    [TestCase("0", 0u)]
    [TestCase(" +2trailing", 2u)]
    [TestCase("-1", 1u)]
    [TestCase("invalid", 1u)]
    public void DisplaySizeUsesHtmlPrefixParsingIncludingZero(string raw, uint expected)
    {
        var select = Document.CreateHtml().CreateElement("select");
        select.SetAttribute("size", raw);
        select.GetHtmlState()!.Select!.GetDisplaySize(default).Should().Be(expected);
    }
    [Test]
    public void ListPrunesBarriersAndSecondOptgroupAcrossWrappers()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var state = select.GetHtmlState()!.Select!;
        var wrapper = document.CreateElement("div"); select.AppendChild(wrapper);
        var a = Option(document); wrapper.AppendChild(a);
        foreach (var barrier in new[] { "hr", "datalist", "option", "select" })
        {
            var container = document.CreateElement(barrier); wrapper.AppendChild(container);
            container.AppendChild(Option(document));
        }
        var group = document.CreateElement("optgroup"); wrapper.AppendChild(group);
        var b = Option(document); group.AppendChild(b);
        var nested = document.CreateElement("optgroup"); group.AppendChild(nested);
        var excluded = Option(document); nested.AppendChild(excluded);
        state.Options.Count.Should().Be(3); // a, the option barrier itself, b
        b.GetHtmlState()!.Option!.GetIndex(default).Should().Be(2);
        excluded.GetHtmlState()!.Option!.GetIndex(default).Should().Be(0);
        nested.RemoveChild(excluded); wrapper.AppendChild(excluded);
        state.Options.Count.Should().Be(4);
    }
    [Test]
    public void FallbackUsesOptionDisablednessRatherThanSelectDisabledness()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select"); select.SetAttribute("disabled", "");
        var group = document.CreateElement("optgroup"); group.SetAttribute("disabled", "");
        select.AppendChild(group);
        var disabled = Option(document); group.AppendChild(disabled);
        var a = Option(document, "a"); select.AppendChild(a);
        select.GetHtmlState()!.Select!.GetValue(default).Should().Be("a");
        a.GetHtmlState()!.Option!.IsDisabled(default).Should().BeFalse();
        disabled.GetHtmlState()!.Option!.IsDisabled(default).Should().BeTrue();
        disabled.GetHtmlState()!.Option!.SetSelected(true, default);
        disabled.GetHtmlState()!.Option!.Selected.Should().BeTrue();
    }
    [Test]
    public void LiveCollectionsNamedAccessSnapshotsAndReordering()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var state = select.GetHtmlState()!.Select!;
        var options = state.Options; var selected = state.SelectedOptions;
        var a = Option(document, "a"); a.SetAttribute("name", "needle");
        var b = Option(document, "b"); b.SetAttribute("id", "needle");
        options.Add(a, (int?) null, default); options.Add(b, (int?) null, default);
        options.NamedItem("needle", default).Should().BeSameAs(a);
        var snapshot = options.Snapshot(default);
        options.Add(b, a, default);
        options.Item(0, default).Should().BeSameAs(b);
        snapshot[0].Should().BeSameAs(a);
        state.Options.Should().BeSameAs(options); state.SelectedOptions.Should().BeSameAs(selected);
        options.SetLength(4, default); options.Count.Should().Be(4);
        options.SetLength(1, default); options.Count.Should().Be(1);
        options.SetIndexed(3, a, default); options.Count.Should().Be(4);
        options.Item(3, default).Should().BeSameAs(a);
        options.SetIndexed(3, null, default); options.Count.Should().Be(3);
        options.NamedItem("", default).Should().BeNull();
    }
    [Test]
    public void TextValueAndLabelSkipScriptsImagesAndCollapseOnlyAsciiWhitespace()
    {
        var document = Document.CreateHtml();
        var option = Option(document, " \t A\r\n");
        var span = document.CreateElement("span"); span.AppendChild(document.CreateTextNode("B\u00a0 C "));
        option.AppendChild(span);
        foreach (var ns in new[] { Namespaces.Html, Namespaces.Svg })
        {
            var script = document.CreateElementNS(ns, "script"); script.AppendChild(document.CreateTextNode("hidden")); option.AppendChild(script);
        }
        var img = document.CreateElement("img"); img.AppendChild(document.CreateTextNode("also hidden")); option.AppendChild(img);
        var state = option.GetHtmlState()!.Option!;
        state.GetText(default).Should().Be("A B\u00a0 C");
        state.GetValue(default).Should().Be("A B\u00a0 C");
        state.SetValue(""); state.GetValue(default).Should().BeEmpty();
        state.SetLabel(""); state.GetLabel(default).Should().BeEmpty();
        state.SetText("  new\n text  ", default); state.GetText(default).Should().Be("new text");
    }
    [Test]
    public void CloneImportAndAdoptionCopyFlagsWithoutSourceOwnershipOrMutation()
    {
        var source = Document.CreateHtml();
        var select = source.CreateElement("select");
        var a = Option(source, "a", true); var b = Option(source, "b");
        select.AppendChild(a); select.AppendChild(b);
        b.GetHtmlState()!.Option!.SetSelected(true, default);
        var stamp = source.MutationStamp;
        var clone = (Element) select.CloneNode(true);
        source.MutationStamp.Should().Be(stamp);
        clone.GetHtmlState()!.Select!.GetValue(default).Should().Be("b");
        ((Element) clone.LastChild!).GetHtmlState()!.Option!.DirtySelectedness.Should().BeTrue();
        var target = Document.CreateHtml(); var targetStamp = target.MutationStamp;
        var imported = (Element) target.ImportNode(select, true);
        target.MutationStamp.Should().Be(targetStamp);
        imported.GetHtmlState()!.Select!.GetValue(default).Should().Be("b");
        target.AdoptNode(b);
        b.GetHtmlState()!.Option!.Selected.Should().BeTrue();
        select.GetHtmlState()!.Select!.GetValue(default).Should().Be("a");
        b.GetHtmlState()!.Option!.GetForm(default).Should().BeNull();
    }
    [Test]
    public void OptionConstructorFourthArgumentOverridesDefaultWithoutDirtying()
    {
        var document = Document.CreateHtml();
        var option = HtmlOptionState.Create(document, "text", null, true, false, default);
        var state = option.GetHtmlState()!.Option!;
        state.DefaultSelected.Should().BeTrue(); state.Selected.Should().BeFalse(); state.DirtySelectedness.Should().BeFalse();
    }
    [Test]
    public void ParsedBatchAndAttachedAttributeTransitionsRemainSynchronous()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var option = document.CreateElement("option");
        option.InitializeParsedAttributes(new[] { new ParserAttribute(null, "selected", null, "") }, default);
        select.AppendParsedChild(option);
        option.GetHtmlState()!.Option!.Selected.Should().BeTrue();
        select.SetAttribute("size", "2");
        option.RemoveAttribute("selected");
        option.GetHtmlState()!.Option!.Selected.Should().BeFalse();
        option.SetAttribute("selected", "");
        option.GetAttributeNodeNS(null, "selected")!.Value = "false";
        option.GetHtmlState()!.Option!.Selected.Should().BeTrue();
    }
    [Test]
    public void ParserTailAppendAndTailTruncationHaveLinearWork()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        for (var i = 0; i < 5000; i++) select.AppendParsedChild(Option(document));
        probe.Units.Should().BeLessThan(40064);
        var before = probe.Units;
        select.GetHtmlState()!.Select!.Options.SetLength(1, default);
        (probe.Units - before).Should().BeLessThan(40064);
        var revision = select.GetHtmlState()!.Select!.MembershipRevision;
        document.CreateElement("div").SetAttribute("class", "unrelated");
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(1);
        select.GetHtmlState()!.Select!.MembershipRevision.Should().Be(revision);
    }
    [Test]
    public void CancellationDuringTextMatchingLeavesFlagsUnchanged()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var a = Option(document, "a"); var b = Option(document, new string('b', 1024));
        select.AppendChild(a); select.AppendChild(b);
        var state = select.GetHtmlState()!.Select!;
        var stamp = document.MutationStamp;
        using var cts = new CancellationTokenSource();
        document.SelectWorkProbe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => state.SetValue("no match", cts.Token));
        state.GetSelectedIndex(default).Should().Be(0);
        b.GetHtmlState()!.Option!.DirtySelectedness.Should().BeFalse();
        document.MutationStamp.Should().Be(stamp);
    }
}

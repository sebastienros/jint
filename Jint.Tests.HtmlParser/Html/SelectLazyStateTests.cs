#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectLazyStateTests
{
    [Test]
    public void NativeInsertionAndCloneKeepEnhancedViewsColdAndPreserveDirtyHistory()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        var first = document.CreateElement("option");
        var second = document.CreateElement("option");
        first.InitializeParsedAttributes([new ParserAttribute(null, "selected", null, "")], default);
        select.AppendParsedChild(first);
        select.AppendParsedChild(second);
        second.GetOptionCore().SetSelected(true, default);
        first.RemoveAttribute("selected");
        first.SetAttribute("selected", ""); // The dirty peer retains its current history.
        second.GetOptionCore().SetSelected(true, default);
        var clone = (Element) NodeCloner.Clone(select, document, true);
        select.ExistingSelectState.Should().BeNull();
        first.ExistingOptionState.Should().BeNull(); second.ExistingOptionState.Should().BeNull();
        clone.ExistingSelectState.Should().BeNull();
        foreach (var option in HtmlSelectCore.Enumerate(clone, default)) option.ExistingOptionState.Should().BeNull();
        clone.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(1);
        ((Element) clone.LastChild!).GetHtmlState()!.Option!.DirtySelectedness.Should().BeTrue();
        select.GetHtmlState()!.Select!.HasOptionInventory.Should().BeFalse();
    }

    [Test]
    public void FirstEnhancedOptionAndSelectMetadataReadIsCancellableWithoutPublishingView()
    {
        var document = Document.CreateHtml();
        foreach (var kind in new[] { "option", "select" })
        {
            var element = document.CreateElement(kind);
            element.InitializeParsedAttributes(Enumerable.Range(0, 1024)
                .Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
            using var cts = new CancellationTokenSource();
            var probe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } };
            document.SelectWorkProbe = probe;
            var stamp = document.MutationStamp;
            Assert.Throws<OperationCanceledException>(() =>
            {
                if (kind == "option") element.GetHtmlState()!.GetOptionState(cts.Token);
                else element.GetHtmlState()!.GetSelectState(cts.Token);
            });
            element.ExistingOptionState.Should().BeNull(); element.ExistingSelectState.Should().BeNull();
            probe.Units.Should().Be(256); document.MutationStamp.Should().Be(stamp);
            document.SelectWorkProbe = null;
        }
    }

    [Test]
    public void ColdSelectCloneDoesNotPerformDerivedMetadataSecondPass()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        select.InitializeParsedAttributes(Enumerable.Range(0, 1024)
            .Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        var clone = (Element) NodeCloner.Clone(select, document, true);
        probe.Units.Should().Be(1027); // Copy attributes, then the light/template/shadow frame phases.
        select.ExistingSelectCore.Should().BeNull(); clone.ExistingSelectCore.Should().BeNull();
        select.ExistingSelectState.Should().BeNull(); clone.ExistingSelectState.Should().BeNull();
    }

    [Test]
    public void SelectedContentConnectionAndParserFinishPerformEffectsWithoutEnhancedViews()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select"); document.AppendChild(select);
        var option = document.CreateElement("option"); option.AppendParsedChild(document.CreateTextNode("before"));
        select.AppendChild(option);
        var content = document.CreateElement("selectedcontent"); select.AppendChild(content);
        ((Text) content.FirstChild!).Data.Should().Be("before");
        option.ReplaceChildren(document.CreateTextNode("after"));
        HtmlSelectedContent.MaybeCloneOption(option, default);
        ((Text) content.FirstChild!).Data.Should().Be("after");
        select.ExistingSelectState.Should().BeNull(); option.ExistingOptionState.Should().BeNull();
    }

    [TestCase(1000)]
    [TestCase(10000)]
    public void DisabledTailRemovalPreservesExhaustedNativeFallback(int size)
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        for (var i = 0; i < size; i++)
        {
            var option = document.CreateElement("option"); option.SetAttribute("disabled", "");
            select.AppendParsedChild(option);
        }
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        while (select.LastChild is { } option) select.RemoveChild(option);
        probe.Units.Should().BeLessThan(4L * size + 16);
        select.ExistingSelectState.Should().BeNull();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ColdAndMaterializedViewsHaveIdenticalMutationHistory(bool materialized)
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var options = Enumerable.Range(0, 4).Select(_ => document.CreateElement("option")).ToArray();
        foreach (var option in options) select.AppendParsedChild(option);
        if (materialized)
        {
            select.GetHtmlState()!.Select!.SelectedOptions.Count.Should().Be(1);
            foreach (var option in options) option.GetHtmlState()!.Option!.Selected.Should().Be(option == options[0]);
        }
        options[2].GetOptionCore().SetSelected(true, default);
        options[0].SetAttribute("selected", "");
        options[2].RemoveAttribute("selected");
        select.SetAttribute("multiple", "");
        options[3].SetAttribute("selected", "");
        select.RemoveAttribute("multiple");
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(3);
        select.RemoveChild(options[3]);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
        select.GetHtmlState()!.Select!.Reset(default);
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
        foreach (var option in options.Take(3)) option.GetOptionCore().DirtySelectedness.Should().BeFalse();
    }
}

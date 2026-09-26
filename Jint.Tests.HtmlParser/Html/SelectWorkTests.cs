#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectWorkTests
{
    [TestCase(1000)]
    [TestCase(10000)]
    public void OptgroupBatchInsertionAndRemovalBuildAffectedInventoryOnce(int size)
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var group = document.CreateElement("optgroup");
        for (var i = 0; i < size; i++) group.AppendParsedChild(document.CreateElement("option"));
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        select.AppendChild(group);
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(size);
        probe.Units.Should().BeLessThan(12L * size + 20);
        for (var i = 0; i < size; i++) select.AppendParsedChild(document.CreateElement("option"));
        var before = probe.Units;
        select.RemoveChild(group);
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(size);
        (probe.Units - before).Should().BeLessThan(12L * size + 20);
    }
    [TestCase(1000)]
    [TestCase(10000)]
    public void ParsedOptgroupTailAppendDoesNotRescanPriorOptions(int size)
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var group = document.CreateElement("optgroup"); select.AppendChild(group);
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        for (var i = 0; i < size; i++) group.AppendParsedChild(document.CreateElement("option"));
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(size);
        probe.Units.Should().BeLessThan(12L * size + 20);
    }
    [Test]
    public void ActiveSelectionCompactsAfterLargeMultipleSelectionHighWater()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select"); select.SetAttribute("multiple", "");
        for (var i = 0; i < 10000; i++)
        {
            var option = document.CreateElement("option"); option.SetAttribute("selected", ""); select.AppendParsedChild(option);
        }
        var state = select.GetHtmlState()!.Select!;
        state.SetSelectedIndex(0, default);
        state.GetEnabledSelectedContent(default); // Warm this independent tree view.
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        for (var i = 0; i < 100; i++) state.SetSelectedIndex(0, default);
        probe.Units.Should().Be(100);
        var first = (Element) select.FirstChild!;
        first.GetHtmlState()!.Option!.SelectionIndexOwner.Should().BeSameAs(state);
        first.GetHtmlState()!.Option!.SelectedPosition.Should().Be(0);
        ((Element) select.LastChild!).GetHtmlState()!.Option!.SelectionIndexOwner.Should().BeNull();
    }
    [Test]
    public void IntrinsicAttributeFactsAreCachedAcrossLargeUnrelatedAttributeMaps()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select"); var option = document.CreateElement("option");
        var unrelated = Enumerable.Range(0, 10000).Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray();
        var selectAttributes = unrelated.Concat(new[] { new ParserAttribute(null, "multiple", null, ""), new ParserAttribute(null, "size", null, " +3tail") }).ToArray();
        var optionAttributes = unrelated.Concat(new[] { new ParserAttribute(null, "value", null, "value"), new ParserAttribute(null, "label", null, "label"), new ParserAttribute(null, "id", null, "id"), new ParserAttribute(null, "name", null, "name"), new ParserAttribute(null, "selected", null, "") }).ToArray();
        select.InitializeParsedAttributes(selectAttributes, default); option.InitializeParsedAttributes(optionAttributes, default);
        select.AppendParsedChild(option);
        var state = select.GetHtmlState()!.Select!; var optionState = option.GetHtmlState()!.Option!;
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        for (var i = 0; i < 100; i++)
        {
            state.Multiple.Should().BeTrue(); state.GetDisplaySize(default).Should().Be(3);
            optionState.DefaultSelected.Should().BeTrue(); optionState.GetValue(default).Should().Be("value"); optionState.GetLabel(default).Should().Be("label");
            state.Options.NamedItem("id", default).Should().BeSameAs(option);
        }
        probe.Units.Should().Be(300); // One option visit and two matched id code units.
        option.GetAttributeNodeNS(null, "value")!.Value = "new";
        option.GetAttributeNodeNS(null, "label")!.Value = "new label";
        option.GetAttributeNodeNS(null, "id")!.Value = "new id";
        select.GetAttributeNodeNS(null, "size")!.Value = "4";
        state.GetDisplaySize(default).Should().Be(4); optionState.GetValue(default).Should().Be("new"); optionState.GetLabel(default).Should().Be("new label");
        state.Options.NamedItem("new id", default).Should().BeSameAs(option);
        option.RemoveAttribute("selected"); optionState.DefaultSelected.Should().BeFalse();
        select.RemoveAttribute("multiple"); state.Multiple.Should().BeFalse();
    }
    [Test]
    public void ColdMetadataRefreshChargesAttributesAndCancelsBeforePublishing()
    {
        var document = Document.CreateHtml(); var option = document.CreateElement("option");
        option.InitializeParsedAttributes(Enumerable.Range(0, 10000).Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        var state = option.GetHtmlState()!.Option!;
        using var cts = new CancellationTokenSource();
        var probe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } }; document.SelectWorkProbe = probe;
        Assert.Throws<OperationCanceledException>(() => state.RefreshMetadata(cts.Token));
        probe.Units.Should().Be(256); state.DefaultSelected.Should().BeFalse();
    }
    [Test]
    public void DeselectFallbackPreparationCancelsBeforeDirtyOrSelectedFlagsChange()
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        for (var i = 0; i < 1000; i++)
        {
            var option = document.CreateElement("option"); option.SetAttribute("disabled", ""); select.AppendParsedChild(option);
        }
        var chosen = document.CreateElement("option"); chosen.SetAttribute("selected", ""); select.AppendChild(chosen);
        var state = chosen.GetHtmlState()!.Option!; var stamp = document.MutationStamp;
        using var cts = new CancellationTokenSource(); document.SelectWorkProbe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => state.SetSelected(false, cts.Token));
        state.Selected.Should().BeTrue(); state.DirtySelectedness.Should().BeFalse(); document.MutationStamp.Should().Be(stamp);
    }
    [TestCase(1000)]
    [TestCase(5000)]
    public void DeepWrapperInsertionCarriesAncestryOnceInsteadOfWalkingPerOption(int size)
    {
        var document = Document.CreateHtml(); var select = document.CreateElement("select");
        var wrapper = document.CreateElement("div"); var current = wrapper;
        for (var i = 0; i < size; i++)
        {
            current.AppendParsedChild(document.CreateElement("option"));
            var next = document.CreateElement("div"); current.AppendParsedChild(next); current = next;
        }
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        select.AppendChild(wrapper);
        select.GetHtmlState()!.Select!.Options.Count.Should().Be(size);
        probe.Units.Should().BeLessThan(16L * size + 64);
    }
    [TestCase("option")]
    [TestCase("select")]
    public void CloneColdMetadataSecondPassIsCancellable(string kind)
    {
        var document = Document.CreateHtml(); var element = document.CreateElement(kind);
        element.InitializeParsedAttributes(Enumerable.Range(0, 1024).Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        using var cts = new CancellationTokenSource();
        var probe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 1280) cts.Cancel(); } }; document.SelectWorkProbe = probe;
        var stamp = document.MutationStamp;
        Assert.Throws<OperationCanceledException>(() => NodeCloner.Clone(element, document, true, cancellationToken: cts.Token));
        probe.Units.Should().Be(1280); document.MutationStamp.Should().Be(stamp);
    }
    [TestCase("option")]
    [TestCase("select")]
    public void ParsedMetadataCancelsBeforeAttributeBatchPublication(string kind)
    {
        var document = Document.CreateHtml(); var element = document.CreateElement(kind);
        using var cts = new CancellationTokenSource();
        document.SelectWorkProbe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } };
        var attributes = Enumerable.Range(0, 1024).Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray();
        Assert.Throws<OperationCanceledException>(() => element.InitializeParsedAttributes(attributes, cts.Token));
        element.AttributeCount.Should().Be(0);
        element.GetHtmlState()!.ExistingSelect.Should().BeNull(); element.GetHtmlState()!.ExistingOption.Should().BeNull();
    }

    [TestCase(1000)]
    [TestCase(10000)]
    public void OrdinaryParserLeafAppendDoesNotWalkAncestorsForSelectBookkeeping(int size)
    {
        var document = Document.CreateHtml(); var root = document.CreateElement("div"); var current = root;
        var probe = new HtmlSelectWorkProbe(); document.SelectWorkProbe = probe;
        for (var i = 0; i < size; i++)
        {
            var next = document.CreateElement("div"); current.AppendParsedChild(next); current = next;
        }
        probe.Units.Should().Be(0);
    }

    [Test]
    public void CombinedClonePassesCancellationIntoColdInputValueConstruction()
    {
        var document = Document.CreateHtml(); var attributes = document.CreateElement("div");
        attributes.InitializeParsedAttributes(Enumerable.Range(0, 1024).Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        var input = document.CreateElement("input");
        input.CopyAttributesFrom(attributes, document); // A trusted cold source, no value-state read yet.
        input.GetHtmlState()!.ExistingInputValue.Should().BeNull();
        using var cts = new CancellationTokenSource();
        var probe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 1025) cts.Cancel(); } }; document.SelectWorkProbe = probe;
        var stamp = document.MutationStamp;
        Assert.Throws<OperationCanceledException>(() => NodeCloner.Clone(input, document, true, cancellationToken: cts.Token));
        input.GetHtmlState()!.ExistingInputValue.Should().BeNull();
        probe.Units.Should().Be(1025); document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ClonePollsCancellationDuringColdSourceCheckednessMetadataScan()
    {
        var document = Document.CreateHtml(); var attributes = document.CreateElement("div");
        attributes.InitializeParsedAttributes(Enumerable.Range(0, 1024).Select(i => new ParserAttribute(null, "data-" + i, null, "x")).ToArray(), default);
        var input = document.CreateElement("input"); input.CopyAttributesFrom(attributes, document);
        input.ExistingCheckedState.Should().BeNull();
        using var cts = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } }; document.CheckedWorkProbe = probe;
        var stamp = document.MutationStamp;
        Assert.Throws<OperationCanceledException>(() => NodeCloner.Clone(input, document, true, cancellationToken: cts.Token));
        input.ExistingCheckedState.Should().BeNull(); input.GetHtmlState()!.ExistingInputValue.Should().BeNull();
        probe.Units.Should().Be(256); document.MutationStamp.Should().Be(stamp);
    }
    [Test]
    public void RawTextareaCloneCopyCancelsColdChildScanBeforeAllocationOrFlagPublication()
    {
        var document = Document.CreateHtml(); var source = document.CreateElement("textarea");
        source.AppendParsedChild(document.CreateTextNode(new string('x', 1000000)));
        var destination = document.CreateElement("textarea");
        var sourceState = source.GetHtmlState()!.TextArea!; var target = destination.GetHtmlState()!.TextArea!;
        target.SetValue("prior", default); target.SetSelectionRange(1, 2, "forward", default); target.SetUserValidity(true);
        var stamp = document.MutationStamp;
        using var cts = new CancellationTokenSource();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<OperationCanceledException>(() => target.CopyFrom(sourceState, _ => cts.Cancel(), cts.Token));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().BeLessThan(65536); // Cancellation during the count pass precedes the large copy allocation.
        target.GetValue(default).Should().Be("prior"); target.DirtyValue.Should().BeTrue(); target.UserValidity.Should().BeTrue();
        target.Selection.Should().Be(new HtmlTextSelection(1, 2, HtmlSelectionDirection.Forward));
        sourceState.DirtyValue.Should().BeFalse(); document.MutationStamp.Should().Be(stamp);
    }

}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class DisablednessWorkTests
{
    [Test]
    public void WideFieldsetScansFirstLegendOnceAcrossUnrelatedChanges()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        for (var i = 0; i < 768; i++)
        {
            fieldset.AppendParsedChild(document.CreateElement("div"));
        }

        var first = document.CreateElement("legend");
        fieldset.AppendChild(first);
        var second = document.CreateElement("legend");
        fieldset.AppendChild(second);
        var control = document.CreateElement("input");
        second.AppendChild(control);
        var checkpoints = 0;
        HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Disabled);
        checkpoints.Should().Be(3);

        for (var i = 0; i < 64; i++)
        {
            var appendedControl = document.CreateElement("input");
            fieldset.AppendParsedChild(appendedControl);
            fieldset.SetAttribute("data-sequence", i.ToString());
            control.SetAttribute("title", i.ToString());
            HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Disabled);
            HtmlDisabledness.GetState(appendedControl, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Disabled);
        }

        checkpoints.Should().Be(3);
        fieldset.RemoveChild(first);
        HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Enabled);
        checkpoints.Should().BeGreaterThan(3);
    }

    [Test]
    public void RejectedHierarchyMutationKeepsCachedFirstLegend()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        for (var i = 0; i < 512; i++)
        {
            fieldset.AppendParsedChild(document.CreateElement("div"));
        }

        var legend = document.CreateElement("legend");
        fieldset.AppendChild(legend);
        var control = document.CreateElement("input");
        legend.AppendChild(control);
        var checkpoints = 0;
        HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Enabled);
        checkpoints.Should().Be(2);
        Assert.Throws<DomException>(() => fieldset.ReplaceChild(fieldset, legend));
        HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Enabled);
        checkpoints.Should().Be(2);
    }

    [Test]
    public void CancelledLegendScanPublishesNeitherLegendNorKnownNull()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        for (var i = 0; i < 600; i++)
        {
            fieldset.AppendParsedChild(document.CreateElement("div"));
        }

        var control = document.CreateElement("input");
        fieldset.AppendChild(control);
        using var source = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.GetState(control,
            _ => source.Cancel(), source.Token));
        var checkpoints = 0;
        HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Disabled);
        checkpoints.Should().BeGreaterThan(0);
        HtmlDisabledness.GetState(control, _ => checkpoints++, default).Should().Be(HtmlDisabledState.Disabled);
        checkpoints.Should().Be(2);
    }

    [Test]
    public void CancelDuringFinalDeepAscentOfDisablednessAndSelectSearch()
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElement("div");
        document.AppendChild(parent);
        for (var i = 0; i < 600; i++)
        {
            var child = document.CreateElement("div");
            parent.AppendParsedChild(child);
            parent = child;
        }

        var input = document.CreateElement("input");
        parent.AppendParsedChild(input);
        using var disabledCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.GetState(input,
            _ => disabledCancellation.Cancel(), disabledCancellation.Token));
        State(input).Should().Be(HtmlDisabledState.Enabled);

        var option = document.CreateElement("option");
        parent.AppendParsedChild(option);
        using var optionCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.IsOptionDisabled(option,
            _ => optionCancellation.Cancel(), optionCancellation.Token));
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();
        using var selectCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlSelectAncestry.GetNearestSelect(option,
            _ => selectCancellation.Cancel(), selectCancellation.Token));
        HtmlSelectAncestry.GetNearestSelect(option, default).Should().BeNull();
    }

    private static HtmlDisabledState State(Element element) => HtmlDisabledness.GetState(element, default);
}

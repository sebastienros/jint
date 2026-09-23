#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class DisablednessWorkTests
{
    [Test]
    public void OwnDisabledAttributeSearchChargesEveryInspectedAttribute()
    {
        foreach (var name in new[] { "input", "fieldset", "optgroup", "option" })
        {
            foreach (var scenario in new[] { "absent", "late", "namespaced" })
            {
                var document = Document.CreateHtml();
                var element = document.CreateElement(name);
                if (scenario == "namespaced")
                {
                    element.SetAttributeNS("urn:test", "x:disabled", "");
                }

                AddUnrelatedAttributes(element);
                if (scenario == "late")
                {
                    element.SetAttribute("disabled", "false");
                }

                using var cancellation = new CancellationTokenSource();
                var checkpoints = 0;
                Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.GetState(element,
                    _ =>
                    {
                        checkpoints++;
                        cancellation.Cancel();
                    }, cancellation.Token), $"{name}, {scenario}");
                checkpoints.Should().Be(1);
                State(element).Should().Be(scenario == "late"
                    ? HtmlDisabledState.Disabled : HtmlDisabledState.Enabled);
            }
        }
    }

    [Test]
    public void AncestorFieldsetAndOptgroupAttributeSearchUseTheSameWorkCadence()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        AddUnrelatedAttributes(fieldset);
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        var input = document.CreateElement("input");
        fieldset.AppendChild(input);
        using var fieldsetCancellation = new CancellationTokenSource();
        var fieldsetCheckpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.GetState(input,
            _ =>
            {
                fieldsetCheckpoints++;
                fieldsetCancellation.Cancel();
            }, fieldsetCancellation.Token));
        fieldsetCheckpoints.Should().Be(1);
        State(input).Should().Be(HtmlDisabledState.Disabled);

        var group = document.CreateElement("optgroup");
        AddUnrelatedAttributes(group);
        group.SetAttribute("disabled", "");
        var option = document.CreateElement("option");
        group.AppendChild(option);
        using var optionCancellation = new CancellationTokenSource();
        var optionCheckpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.IsOptionDisabled(option,
            _ =>
            {
                optionCheckpoints++;
                optionCancellation.Cancel();
            }, optionCancellation.Token));
        optionCheckpoints.Should().Be(1);
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeTrue();

        using var selectorCancellation = new CancellationTokenSource();
        var selectorCheckpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.GetState(option,
            _ =>
            {
                selectorCheckpoints++;
                selectorCancellation.Cancel();
            }, selectorCancellation.Token));
        selectorCheckpoints.Should().Be(1);
        State(option).Should().Be(HtmlDisabledState.Disabled);
    }

    [Test]
    public void OptionAndOptgroupAttributeScansShareOneInvocationCounter()
    {
        var document = Document.CreateHtml();
        var group = document.CreateElement("optgroup");
        var option = document.CreateElement("option");
        group.AppendChild(option);
        for (var i = 0; i < 200; i++)
        {
            option.SetAttribute($"data-option-{i}", "x");
            group.SetAttribute($"data-group-{i}", "x");
        }

        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => HtmlDisabledness.IsOptionDisabled(option,
            _ =>
            {
                checkpoints++;
                cancellation.Cancel();
            }, cancellation.Token));
        checkpoints.Should().Be(1);
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();
    }

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

    private static void AddUnrelatedAttributes(Element element)
    {
        for (var i = 0; i < 320; i++)
        {
            element.SetAttribute($"data-{i}", "x");
        }
    }
}

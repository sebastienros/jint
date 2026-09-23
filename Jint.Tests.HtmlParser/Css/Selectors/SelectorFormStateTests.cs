#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorFormStateTests
{
    private static CompiledSelector Parse(string source)
        => SelectorCompiler.Compile(source, null, default);

    private static bool Matches(string source, Element element)
        => SelectorMatcher.Matches(Parse(source), element);

    [Test]
    public void EveryInputTypeUsesNativeRequiredApplicability()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        foreach (HtmlInputType type in Enum.GetValues(typeof(HtmlInputType)))
        {
            var input = document.CreateElement("input");
            input.SetAttribute("type", HtmlInputTypes.Info(type).Keyword);
            root.AppendChild(input);
            var applies = HtmlInputTypes.Info(type).RequiredApplies;
            Matches(":enabled", input).Should().BeTrue(type.ToString());
            Matches(":disabled", input).Should().BeFalse(type.ToString());
            Matches(":optional", input).Should().Be(applies, type.ToString());
            Matches(":required", input).Should().BeFalse(type.ToString());

            input.SetAttribute("required", "");
            Matches(":required", input).Should().Be(applies, type.ToString());
            Matches(":optional", input).Should().BeFalse(type.ToString());
            input.SetAttribute("disabled", "");
            input.SetAttribute("readonly", "");
            Matches(":required", input).Should().Be(applies, type.ToString());
            Matches(":disabled", input).Should().BeTrue(type.ToString());
        }
    }

    [Test]
    public void SevenDisabledKindsAgreeWithNativeStateAndRequirednessIsIndependent()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        foreach (var name in new[] { "button", "input", "select", "textarea", "fieldset", "optgroup", "option" })
        {
            var element = document.CreateElement(name);
            root.AppendChild(element);
            var state = element.GetHtmlState()!.GetDisabledState(default);
            state.Should().Be(HtmlDisabledState.Enabled, name);
            Matches(":enabled", element).Should().BeTrue(name);
            Matches(":disabled", element).Should().BeFalse(name);
            element.SetAttribute("disabled", "");
            state = element.GetHtmlState()!.GetDisabledState(default);
            state.Should().Be(HtmlDisabledState.Disabled, name);
            Matches(":disabled", element).Should().BeTrue(name);
            Matches(":enabled", element).Should().BeFalse(name);
            Matches(":optional", element).Should().Be(name is "input" or "select" or "textarea", name);
        }
        Matches(":enabled", root).Should().BeFalse();
        Matches(":disabled", root).Should().BeFalse();
        Matches(":required", root).Should().BeFalse();
        Matches(":optional", root).Should().BeFalse();
    }

    [Test]
    public void ExactNamespaceCaseAndTypeRulesApplyInHtmlAndXml()
    {
        var html = Document.CreateHtml();
        var input = html.CreateElement("input");
        html.AppendChild(input);
        input.SetAttribute("type", "EmAiL");
        input.SetAttribute("required", "");
        Matches(":required", input).Should().BeTrue();
        input.SetAttribute("type", " hidden");
        Matches(":required", input).Should().BeTrue();
        input.SetAttribute("type", "HIDDEN");
        Matches(":required", input).Should().BeFalse();
        Matches(":optional", input).Should().BeFalse();
        Matches(":enabled", input).Should().BeTrue();

        var xml = Document.CreateXml();
        var xhtml = xml.CreateElementNS(Namespaces.Html, "input");
        xml.AppendChild(xhtml);
        xhtml.SetAttributeNS("urn:other", "x:required", "");
        xhtml.SetAttributeNS("urn:other", "x:type", "hidden");
        Matches(":optional", xhtml).Should().BeTrue();
        xhtml.SetAttribute("required", "");
        Matches(":required", xhtml).Should().BeTrue();
        var xmlSelect = xml.CreateElementNS(Namespaces.Html, "select");
        xmlSelect.SetAttribute("REQUIRED", "");
        Matches(":optional", xmlSelect).Should().BeTrue();
        xmlSelect.SetAttribute("required", "");
        Matches(":required", xmlSelect).Should().BeTrue();
        var upper = xml.CreateElementNS(Namespaces.Html, "INPUT");
        var foreign = xml.CreateElementNS("urn:foreign", "input");
        var noNamespace = xml.CreateElementNS(null, "input");
        foreach (var lookalike in new[] { upper, foreign, noNamespace })
        {
            lookalike.SetAttribute("required", "");
            lookalike.SetAttribute("disabled", "");
            Matches(":required, :optional, :enabled, :disabled", lookalike).Should().BeFalse();
        }
    }

    [Test]
    public void FieldsetAndOptionAncestryTrackReorderingAndBoundaries()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        var first = document.CreateElement("legend");
        var later = document.CreateElement("legend");
        var safe = document.CreateElement("input");
        var blocked = document.CreateElement("input");
        document.AppendChild(fieldset);
        fieldset.AppendChild(first);
        first.AppendChild(safe);
        fieldset.AppendChild(later);
        later.AppendChild(blocked);
        fieldset.SetAttribute("disabled", "");
        Matches(":enabled", safe).Should().BeTrue();
        Matches(":disabled", blocked).Should().BeTrue();
        fieldset.InsertBefore(later, first);
        Matches(":disabled", safe).Should().BeTrue();
        Matches(":enabled", blocked).Should().BeTrue();
        fieldset.RemoveChild(later);
        Matches(":enabled", safe).Should().BeTrue();
        Matches(":enabled", blocked).Should().BeTrue();

        var select = document.CreateElement("select");
        var group = document.CreateElement("optgroup");
        var option = document.CreateElement("option");
        fieldset.AppendChild(select);
        select.AppendChild(group);
        group.AppendChild(option);
        Matches(":disabled", option).Should().BeTrue();
        select.SetAttribute("disabled", "");
        fieldset.RemoveAttribute("disabled");
        Matches(":disabled", option).Should().BeTrue();
        select.RemoveAttribute("disabled");
        group.SetAttribute("disabled", "");
        Matches(":disabled", option).Should().BeTrue();
        var barrier = document.CreateElement("datalist");
        select.AppendChild(barrier);
        barrier.AppendChild(option);
        Matches(":enabled", option).Should().BeTrue();
    }

    [Test]
    public void NestedFieldsetsAndRadioPeersKeepElementLocalAnswers()
    {
        var document = Document.CreateHtml();
        var outer = document.CreateElement("fieldset");
        var legend = document.CreateElement("legend");
        var inner = document.CreateElement("fieldset");
        var outside = document.CreateElement("input");
        document.AppendChild(outer);
        outer.AppendChild(legend);
        legend.AppendChild(inner);
        outer.AppendChild(outside);
        outer.SetAttribute("disabled", "");
        Matches(":disabled", outside).Should().BeTrue();
        Matches(":enabled", inner).Should().BeTrue();
        inner.SetAttribute("disabled", "");
        Matches(":disabled", inner).Should().BeTrue();
        var inside = document.CreateElement("input");
        inner.AppendChild(inside);
        Matches(":disabled", inside).Should().BeTrue();
        inner.RemoveAttribute("disabled");
        Matches(":enabled", inside).Should().BeTrue();

        var requiredRadio = document.CreateElement("input");
        var optionalRadio = document.CreateElement("input");
        foreach (var radio in new[] { requiredRadio, optionalRadio })
        {
            radio.SetAttribute("type", "radio");
            radio.SetAttribute("name", "same");
            legend.AppendChild(radio);
        }
        requiredRadio.SetAttribute("required", "");
        Matches(":required", requiredRadio).Should().BeTrue();
        Matches(":optional", optionalRadio).Should().BeTrue();
    }

    [Test]
    public void CompiledProgramsObserveMutationsAndSnapshotsKeepOriginalMembers()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        var input = document.CreateElement("input");
        document.AppendChild(root);
        root.AppendChild(input);
        var required = Parse("input:required");
        var optional = Parse("input:optional");
        var first = SelectorMatcher.QuerySelectorAll(optional, root);
        first.Should().Equal(input);
        input.SetAttribute("required", "");
        SelectorMatcher.QuerySelectorAll(required, root).Should().Equal(input);
        SelectorMatcher.QuerySelectorAll(optional, root).Should().BeEmpty();
        first.Should().Equal(input);
        var attribute = input.GetAttributeNodeNS(null, "required")!;
        input.RemoveAttributeNode(attribute);
        SelectorMatcher.Matches(optional, input).Should().BeTrue();
        input.SetAttributeNode(attribute);
        SelectorMatcher.Matches(required, input).Should().BeTrue();
        attribute.Value = "still-present";
        SelectorMatcher.Matches(required, input).Should().BeTrue();
        input.SetAttribute("type", "hidden");
        SelectorMatcher.Matches(required, input).Should().BeFalse();
        input.SetAttribute("type", "text");
        SelectorMatcher.Matches(required, input).Should().BeTrue();
        var replacement = document.CreateAttribute("required");
        replacement.Value = "";
        input.SetAttributeNode(replacement);
        SelectorMatcher.Matches(required, input).Should().BeTrue();
        root.RemoveChild(input);
        SelectorMatcher.QuerySelectorAll(required, root).Should().BeEmpty();
        first.Should().Equal(input);
        var destination = Document.CreateHtml();
        var destinationRoot = destination.CreateElement("main");
        destination.AppendChild(destinationRoot);
        destinationRoot.AppendChild(destination.AdoptNode(input));
        SelectorMatcher.QuerySelectorAll(required, destinationRoot).Should().Equal(input);
        input.RemoveAttributeNode(replacement);
        SelectorMatcher.QuerySelectorAll(optional, destinationRoot).Should().Equal(input);
    }

    [Test]
    public void LogicalNthHasAndColumnsComposeWithFormStates()
    {
        var document = Document.CreateHtml();
        var table = document.CreateElement("table");
        var group = document.CreateElement("colgroup");
        var column = document.CreateElement("col");
        var row = document.CreateElement("tr");
        var cell = document.CreateElement("td");
        var input = document.CreateElement("input");
        document.AppendChild(table);
        table.AppendChild(group);
        group.AppendChild(column);
        table.AppendChild(row);
        row.AppendChild(cell);
        cell.AppendChild(input);
        input.SetAttribute("required", "");
        SelectorMatcher.Matches(Parse("col || td > input:required"), input).Should().BeTrue();
        SelectorMatcher.Matches(Parse("col:enabled || td"), cell).Should().BeFalse();
        SelectorMatcher.Matches(Parse("table:has(> colgroup > col || td > input:optional)"), table).Should().BeFalse();
        SelectorMatcher.Matches(Parse("input:is(:required, :disabled):not(:optional)"), input).Should().BeTrue();
        SelectorMatcher.Matches(Parse("input:where(:required)"), input).Should().BeTrue();
        SelectorMatcher.Matches(Parse("input:nth-child(1 of :required)"), input).Should().BeTrue();
        SelectorMatcher.Matches(Parse("input:nth-last-child(1 of :required)"), input).Should().BeTrue();
        input.SetAttribute("disabled", "");
        SelectorMatcher.Matches(Parse("col:has(|| td > input:disabled)"), column).Should().BeTrue();
        input.RemoveAttribute("required");
        SelectorMatcher.Matches(Parse("table:has(> colgroup > col || td > input:optional)"), table).Should().BeTrue();
    }

    [Test]
    public void EveryEntryAndMatchingBranchSpecificityObserveCurrentState()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        var input = document.CreateElement("input");
        document.AppendChild(root);
        root.AppendChild(input);
        var program = Parse("input:optional, #missing:disabled, input:required");
        SelectorMatcher.Matches(program, input).Should().BeTrue();
        SelectorMatcher.TryMatch(program, input, out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(0, 1, 1));
        SelectorMatcher.Closest(program, input).Should().BeSameAs(input);
        SelectorMatcher.QuerySelector(program, root).Should().BeSameAs(input);
        SelectorMatcher.QuerySelectorAll(program, root).Should().Equal(input);
        input.SetAttribute("id", "own");
        var varied = Parse("#own:required, input:optional");
        SelectorMatcher.TryMatch(varied, input, out specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(0, 1, 1));
        input.SetAttribute("required", "");
        SelectorMatcher.TryMatch(program, input, out specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(0, 1, 1));
        SelectorMatcher.TryMatch(varied, input, out specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 1, 0));
        var unsupported = Parse("input:required, :valid");
        NUnit.Framework.Assert.Throws<InvalidOperationException>(() => SelectorMatcher.Matches(unsupported, input));
        NUnit.Framework.Assert.Throws<InvalidOperationException>(() =>
            SelectorMatcher.Matches(Parse("input:where(:optional, :valid)"), input));
    }

    [Test]
    public void ShadowTemplateAndAssignedSlotDoNotChangeOrdinaryAncestry()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        var host = document.CreateElement("div");
        document.AppendChild(fieldset);
        fieldset.AppendChild(host);
        fieldset.SetAttribute("disabled", "");
        var light = document.CreateElement("input");
        light.SetAttribute("slot", "s");
        host.AppendChild(light);
        Matches(":disabled", light).Should().BeTrue();
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var slot = document.CreateElement("slot");
        slot.SetAttribute("name", "s");
        shadow.AppendChild(slot);
        var shadowInput = document.CreateElement("input");
        slot.AppendChild(shadowInput);
        Matches(":enabled", shadowInput).Should().BeTrue();
        var template = document.CreateElement("template");
        fieldset.AppendChild(template);
        var templateInput = document.CreateElement("input");
        template.TemplateContent!.AppendChild(templateInput);
        Matches(":enabled", templateInput).Should().BeTrue();
    }

    [Test]
    public void SelectorAndNativeAttributeWorkShareOneCancellationCadence()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        for (var index = 0; index < 251; index++)
        {
            input.SetAttribute("data-" + index, "");
        }
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
            SelectorMatcher.Matches(Parse(":optional"), input, null, () =>
            {
                checkpoints++;
                source.Cancel();
            }, source.Token));
        checkpoints.Should().Be(1);
        SelectorMatcher.Matches(Parse(":optional"), input).Should().BeTrue();
    }

    [Test]
    public void DeepAncestryAndColdFirstLegendScansCancelAndRetry()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        document.AppendChild(fieldset);
        fieldset.SetAttribute("disabled", "");
        Node current = fieldset;
        for (var index = 0; index < 300; index++)
        {
            var wrapper = document.CreateElement("div");
            current.AppendChild(wrapper);
            current = wrapper;
        }
        var deepInput = document.CreateElement("input");
        current.AppendChild(deepInput);
        using (var source = new CancellationTokenSource())
        {
            NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
                SelectorMatcher.Matches(Parse(":disabled"), deepInput, null, source.Cancel, source.Token));
        }
        Matches(":disabled", deepInput).Should().BeTrue();

        var first = document.CreateElement("legend");
        for (var index = 0; index < 300; index++)
        {
            fieldset.AppendChild(document.CreateElement("span"));
        }
        fieldset.AppendChild(first);
        var safe = document.CreateElement("input");
        first.AppendChild(safe);
        using (var source = new CancellationTokenSource())
        {
            NUnit.Framework.Assert.Throws<OperationCanceledException>(() =>
                SelectorMatcher.Matches(Parse(":enabled"), safe, null, source.Cancel, source.Token));
        }
        Matches(":enabled", safe).Should().BeTrue();
    }

    [Test]
    public void FirstLegendCacheAvoidsRepeatedWidePrefixScans()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        document.AppendChild(fieldset);
        fieldset.SetAttribute("disabled", "");
        for (var index = 0; index < 300; index++)
        {
            fieldset.AppendChild(document.CreateElement("span"));
        }
        var legend = document.CreateElement("legend");
        var control = document.CreateElement("input");
        fieldset.AppendChild(legend);
        legend.AppendChild(control);
        var program = Parse(":enabled");
        var cold = 0;
        SelectorMatcher.Matches(program, control, null, () => cold++, default).Should().BeTrue();
        var warm = 0;
        SelectorMatcher.Matches(program, control, null, () => warm++, default).Should().BeTrue();
        cold.Should().BeGreaterThan(0);
        warm.Should().Be(0);
    }
}

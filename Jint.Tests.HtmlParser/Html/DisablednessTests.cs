#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class DisablednessTests
{
    [Test]
    public void ApplicabilityAndBooleanPresenceUseExactHtmlNamesAndNamespace()
    {
        var document = Document.CreateHtml();
        foreach (var name in new[] { "button", "input", "select", "textarea", "fieldset", "optgroup", "option" })
        {
            var element = document.CreateElement(name);
            State(element).Should().Be(HtmlDisabledState.Enabled);
            element.GetHtmlState()!.GetDisabledState(default).Should().Be(HtmlDisabledState.Enabled);
            element.SetAttribute("disabled", "false");
            State(element).Should().Be(HtmlDisabledState.Disabled);
            element.RemoveAttribute("disabled");
            State(element).Should().Be(HtmlDisabledState.Enabled);
        }

        foreach (var name in new[] { "div", "a", "form", "output", "object", "img", "legend", "label", "keygen", "x-widget" })
        {
            var element = document.CreateElement(name);
            element.SetAttribute("disabled", "");
            State(element).Should().Be(HtmlDisabledState.Inapplicable);
        }

        var svg = document.CreateElementNS(Namespaces.Svg, "input");
        svg.SetAttribute("disabled", "");
        State(svg).Should().Be(HtmlDisabledState.Inapplicable);
        var xml = Document.CreateXml();
        var uppercase = xml.CreateElementNS(Namespaces.Html, "Input");
        uppercase.SetAttribute("disabled", "");
        State(uppercase).Should().Be(HtmlDisabledState.Inapplicable);
        State(xml.CreateElementNS(Namespaces.Html, "input")).Should().Be(HtmlDisabledState.Enabled);

        var qualified = document.CreateElement("input");
        qualified.SetAttributeNS("urn:test", "x:disabled", "");
        State(qualified).Should().Be(HtmlDisabledState.Enabled);
        Assert.Throws<ArgumentNullException>(() => HtmlDisabledness.GetState(null!, default));
        Assert.Throws<ArgumentNullException>(() => HtmlSelectAncestry.GetNearestSelect(null!, default));
        Assert.Throws<ArgumentNullException>(() => HtmlDisabledness.IsOptionDisabled(null!, default));
        Assert.Throws<ArgumentException>(() => HtmlDisabledness.IsOptionDisabled(qualified, default));
    }

    [Test]
    public void FirstDirectHtmlLegendExemptsOnlyItsOwnDescendants()
    {
        var document = Document.CreateHtml();
        var outer = document.CreateElement("fieldset");
        outer.SetAttribute("disabled", "");
        document.AppendChild(outer);
        outer.AppendChild(document.CreateElement("div"));
        var nestedLegend = document.CreateElement("legend");
        ((Element)outer.FirstChild!).AppendChild(nestedLegend);
        var nestedControl = document.CreateElement("input");
        nestedLegend.AppendChild(nestedControl);
        State(nestedControl).Should().Be(HtmlDisabledState.Disabled);

        var first = document.CreateElement("legend");
        outer.AppendChild(first);
        var exempt = document.CreateElement("input");
        first.AppendChild(exempt);
        State(exempt).Should().Be(HtmlDisabledState.Enabled);
        var inner = document.CreateElement("fieldset");
        inner.SetAttribute("disabled", "");
        first.AppendChild(inner);
        var innerControl = document.CreateElement("input");
        inner.AppendChild(innerControl);
        State(inner).Should().Be(HtmlDisabledState.Disabled);
        State(innerControl).Should().Be(HtmlDisabledState.Disabled);

        var second = document.CreateElement("legend");
        outer.AppendChild(second);
        var secondControl = document.CreateElement("input");
        second.AppendChild(secondControl);
        State(secondControl).Should().Be(HtmlDisabledState.Disabled);
        outer.InsertBefore(second, first);
        State(secondControl).Should().Be(HtmlDisabledState.Enabled);
        State(exempt).Should().Be(HtmlDisabledState.Disabled);
        outer.RemoveChild(second);
        State(exempt).Should().Be(HtmlDisabledState.Enabled);
        State(secondControl).Should().Be(HtmlDisabledState.Enabled);

        var nonHtmlLegend = document.CreateElementNS(Namespaces.Svg, "legend");
        outer.InsertBefore(nonHtmlLegend, first);
        State(exempt).Should().Be(HtmlDisabledState.Enabled);
        first.SetAttribute("class", "changed");
        State(exempt).Should().Be(HtmlDisabledState.Enabled);
    }

    [Test]
    public void OuterFieldsetStillDisablesAnInnerLegendExemption()
    {
        var document = Document.CreateHtml();
        var outer = document.CreateElement("fieldset");
        outer.SetAttribute("disabled", "");
        document.AppendChild(outer);
        var inner = document.CreateElement("fieldset");
        inner.SetAttribute("disabled", "");
        outer.AppendChild(inner);
        var legend = document.CreateElement("legend");
        inner.AppendChild(legend);
        var control = document.CreateElement("input");
        legend.AppendChild(control);
        State(control).Should().Be(HtmlDisabledState.Disabled);
        outer.RemoveAttribute("disabled");
        State(control).Should().Be(HtmlDisabledState.Enabled);
        inner.RemoveAttribute("disabled");
        State(control).Should().Be(HtmlDisabledState.Enabled);
    }

    [Test]
    public void OptionDisablednessAndNearestSelectUseDifferentBarriers()
    {
        var document = Document.CreateHtml();
        var select = document.CreateElement("select");
        select.SetAttribute("disabled", "");
        document.AppendChild(select);
        var group = document.CreateElement("optgroup");
        select.AppendChild(group);
        var wrapper = document.CreateElement("div");
        group.AppendChild(wrapper);
        var option = document.CreateElement("option");
        wrapper.AppendChild(option);
        HtmlSelectAncestry.GetNearestSelect(option, default).Should().BeSameAs(select);
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();
        State(option).Should().Be(HtmlDisabledState.Disabled);
        State(group).Should().Be(HtmlDisabledState.Disabled);
        select.RemoveAttribute("disabled");
        State(option).Should().Be(HtmlDisabledState.Enabled);
        group.SetAttribute("disabled", "false");
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeTrue();
        State(option).Should().Be(HtmlDisabledState.Disabled);
        group.RemoveAttribute("disabled");
        option.SetAttribute("disabled", "");
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeTrue();
        option.RemoveAttribute("disabled");

        var nestedGroup = document.CreateElement("optgroup");
        group.AppendChild(nestedGroup);
        nestedGroup.AppendChild(option);
        HtmlSelectAncestry.GetNearestSelect(option, default).Should().BeNull();
        group.SetAttribute("disabled", "");
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();
        State(option).Should().Be(HtmlDisabledState.Enabled);
        nestedGroup.SetAttribute("disabled", "");
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeTrue();
        State(option).Should().Be(HtmlDisabledState.Disabled);

        foreach (var barrierName in new[] { "option", "hr", "datalist" })
        {
            var barrier = document.CreateElement(barrierName);
            group.AppendChild(barrier);
            barrier.AppendChild(option);
            HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();
            HtmlSelectAncestry.GetNearestSelect(option, default).Should().BeNull();
            State(option).Should().Be(HtmlDisabledState.Enabled);
        }
    }

    [Test]
    public void OptionSelectorStateInheritsDisabledSelectButOptionPredicateDoesNot()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        var select = document.CreateElement("select");
        fieldset.AppendChild(select);
        var group = document.CreateElement("optgroup");
        select.AppendChild(group);
        var option = document.CreateElement("option");
        group.AppendChild(option);
        State(select).Should().Be(HtmlDisabledState.Disabled);
        State(group).Should().Be(HtmlDisabledState.Disabled);
        State(option).Should().Be(HtmlDisabledState.Disabled);
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();

        var legend = document.CreateElement("legend");
        fieldset.InsertBefore(legend, select);
        legend.AppendChild(select);
        State(select).Should().Be(HtmlDisabledState.Enabled);
        State(group).Should().Be(HtmlDisabledState.Enabled);
        State(option).Should().Be(HtmlDisabledState.Enabled);
        HtmlDisabledness.IsOptionDisabled(option, default).Should().BeFalse();
    }

    [Test]
    public void OrdinaryAncestryIgnoresFormOwnerSlotsShadowAndTemplateHost()
    {
        var document = Document.CreateHtml();
        var disabledForm = document.CreateElement("form");
        disabledForm.SetAttribute("id", "f");
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        root.AppendChild(fieldset);
        fieldset.AppendChild(disabledForm);
        var outside = document.CreateElement("input");
        outside.SetAttribute("form", "f");
        root.AppendChild(outside);
        HtmlFormState.GetOwner(outside).Should().BeSameAs(disabledForm);
        State(outside).Should().Be(HtmlDisabledState.Enabled);

        var host = document.CreateElement("div");
        fieldset.AppendChild(host);
        var light = document.CreateElement("input");
        light.SetAttribute("slot", "s");
        host.AppendChild(light);
        State(light).Should().Be(HtmlDisabledState.Disabled);
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        var slot = document.CreateElement("slot");
        slot.SetAttribute("name", "s");
        shadow.AppendChild(slot);
        var shadowControl = document.CreateElement("input");
        slot.AppendChild(shadowControl);
        State(shadowControl).Should().Be(HtmlDisabledState.Enabled);

        var template = document.CreateElement("template");
        fieldset.AppendChild(template);
        var inert = document.CreateElement("input");
        template.TemplateContent!.AppendChild(inert);
        State(inert).Should().Be(HtmlDisabledState.Enabled);
    }

    [Test]
    public void ReplacingCloningAndAdoptingLegendsUpdateAnswersImmediately()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        var legend = document.CreateElement("legend");
        var control = document.CreateElement("input");
        legend.AppendChild(control);
        fieldset.AppendChild(legend);
        State(control).Should().Be(HtmlDisabledState.Enabled);

        var replacement = document.CreateElement("legend");
        var other = document.CreateElement("input");
        replacement.AppendChild(other);
        fieldset.ReplaceChild(replacement, legend);
        State(other).Should().Be(HtmlDisabledState.Enabled);
        State(control).Should().Be(HtmlDisabledState.Enabled);
        fieldset.AppendChild(legend);
        State(control).Should().Be(HtmlDisabledState.Disabled);
        var clone = (Element)fieldset.CloneNode(true);
        var cloneControl = (Element)((Element)clone.LastChild!).FirstChild!;
        State(cloneControl).Should().Be(HtmlDisabledState.Disabled);
        Assert.Throws<DomException>(() => fieldset.AppendChild(fieldset));
        State(other).Should().Be(HtmlDisabledState.Enabled);

        var destination = Document.CreateHtml();
        destination.AdoptNode(replacement);
        State(other).Should().Be(HtmlDisabledState.Enabled);
        State(control).Should().Be(HtmlDisabledState.Enabled);
        destination.AdoptNode(fieldset);
        State(control).Should().Be(HtmlDisabledState.Enabled);
    }

    [Test]
    public void ParserCloneFragmentAndReplaceAllInvalidateCachedLegend()
    {
        var document = Document.CreateHtml();
        var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", "");
        document.AppendChild(fieldset);
        var original = document.CreateElement("legend");
        var originalControl = document.CreateElement("input");
        original.AppendChild(originalControl);
        fieldset.AppendChild(original);
        State(originalControl).Should().Be(HtmlDisabledState.Enabled);

        var parserLegend = document.CreateElement("legend");
        var parserControl = document.CreateElement("input");
        // A parser-created legend enters through the trusted fresh-child path.
        fieldset.AppendParsedChild(parserLegend);
        parserLegend.AppendParsedChild(parserControl);
        State(originalControl).Should().Be(HtmlDisabledState.Enabled);
        State(parserControl).Should().Be(HtmlDisabledState.Disabled);

        var clonedLegend = (Element)parserLegend.CloneNode(true);
        fieldset.AppendClonedChild(clonedLegend);
        State(((Element)clonedLegend.FirstChild!)).Should().Be(HtmlDisabledState.Disabled);

        var fragment = document.CreateDocumentFragment();
        var newFirst = document.CreateElement("legend");
        var newControl = document.CreateElement("input");
        newFirst.AppendChild(newControl);
        fragment.AppendChild(newFirst);
        fieldset.ReplaceChild(fragment, original);
        State(newControl).Should().Be(HtmlDisabledState.Enabled);
        State(originalControl).Should().Be(HtmlDisabledState.Enabled);
        Assert.Throws<DomException>(() => fieldset.ReplaceChild(fieldset, newFirst));
        State(newControl).Should().Be(HtmlDisabledState.Enabled);

        fieldset.ReplaceChildren(clonedLegend);
        State(((Element)clonedLegend.FirstChild!)).Should().Be(HtmlDisabledState.Enabled);
        State(newControl).Should().Be(HtmlDisabledState.Enabled);
    }

    private static HtmlDisabledState State(Element element) => HtmlDisabledness.GetState(element, default);
}

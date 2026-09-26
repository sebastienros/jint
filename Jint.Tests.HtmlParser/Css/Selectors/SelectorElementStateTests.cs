#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Css.Selectors;

[TestFixture]
public sealed class SelectorElementStateTests
{
    private const string XLink = "http://www.w3.org/1999/xlink";
    private static CompiledSelector Parse(string text) => SelectorCompiler.Compile(text, null, default);
    private static bool Matches(string text, Element element) => SelectorMatcher.Matches(Parse(text), element);

    [TestCase("checkbox", true)]
    [TestCase("text", false)]
    [TestCase("file", false)]
    [TestCase("unknown", false)]
    public void InitialCheckednessReadsKeepNonradioInputsCold(string type, bool matches)
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.InitializeParsedAttributes(new ParserAttribute[]
        {
            new(null, "type", null, type), new(null, "checked", null, "false")
        }, default);
        var stamp = document.MutationStamp;
        Matches(":checked", input).Should().Be(matches);
        Matches(":indeterminate", input).Should().BeFalse();
        input.ExistingCheckedState.Should().BeNull();
        input.ExistingInputValueState.Should().BeNull();
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void DirtyCheckednessAndIndeterminatenessOverrideContentAttributes()
    {
        var input = Document.CreateHtml().CreateElement("input");
        input.InitializeParsedAttributes(new ParserAttribute[]
        {
            new(null, "type", null, "checkbox"), new(null, "checked", null, "")
        }, default);
        input.ExistingInputValueState.Should().BeNull();
        var state = HtmlCheckableState.Get(input)!;
        state.SetChecked(false, default);
        state.SetIndeterminate(true);
        Matches(":checked", input).Should().BeFalse();
        Matches(":indeterminate", input).Should().BeTrue();
        input.RemoveAttribute("checked");
        input.SetAttribute("checked", "");
        Matches(":checked", input).Should().BeFalse();
        state.SetChecked(true, default);
        Matches(":checked:indeterminate", input).Should().BeTrue();
        input.ExistingInputValueState.Should().BeNull();
    }

    [Test]
    public void RadioGroupIndeterminatenessTracksNativeCheckedness()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var first = Radio();
        var second = Radio();
        root.AppendChild(first);
        root.AppendChild(second);
        Matches(":indeterminate", first).Should().BeTrue();
        Matches(":indeterminate", second).Should().BeTrue();
        HtmlCheckableState.Get(second)!.SetChecked(true, default);
        Matches(":checked", second).Should().BeTrue();
        Matches(":checked", first).Should().BeFalse();
        Matches(":indeterminate", first).Should().BeFalse();
        Matches(":indeterminate", second).Should().BeFalse();
        Element Radio()
        {
            var input = document.CreateElement("input");
            input.SetAttribute("type", "radio");
            input.SetAttribute("name", "group");
            return input;
        }
    }

    [Test]
    public void OptionSelectednessUsesIntrinsicHistoryAndColdAttributePresence()
    {
        var document = Document.CreateHtml();
        var option = document.CreateElement("option");
        Matches(":checked", option).Should().BeFalse();
        option.ExistingOptionCore.Should().BeNull();
        option.ExistingOptionState.Should().BeNull();
        option.InitializeParsedAttributes(new ParserAttribute[] { new(null, "selected", null, "false") }, default);
        var core = option.ExistingOptionCore!;
        core.Should().NotBeNull();
        Matches(":checked", option).Should().BeTrue();
        option.ExistingOptionCore.Should().BeSameAs(core);
        option.ExistingOptionState.Should().BeNull();
        core.SetSelected(false, default);
        Matches(":checked", option).Should().BeFalse();
        core.SetSelected(true, default);
        option.RemoveAttribute("selected");
        Matches(":checked", option).Should().BeTrue();
        Matches(":indeterminate", option).Should().BeFalse();
        option.ExistingOptionCore.Should().BeSameAs(core);
        option.ExistingOptionState.Should().BeNull();
    }

    [Test]
    public void IndeterminateProgressUsesValuePresenceEvenWhenTheValueIsInvalid()
    {
        var progress = Document.CreateHtml().CreateElement("progress");
        Matches(":indeterminate", progress).Should().BeTrue();
        progress.SetAttribute("value", "invalid");
        Matches(":indeterminate", progress).Should().BeFalse();
        progress.RemoveAttribute("value");
        progress.SetAttributeNS("urn:foreign", "f:value", "");
        Matches(":indeterminate", progress).Should().BeTrue();
        Matches(":indeterminate", progress.OwnerDocument!.CreateElementNS(Namespaces.Svg, "progress")).Should().BeFalse();
    }

    [TestCase("details")]
    [TestCase("dialog")]
    public void OpenAndLegacyClosedReadActualHtmlElementAttributePresence(string name)
    {
        var element = Document.CreateHtml().CreateElement(name);
        Matches(":open", element).Should().BeFalse();
        Matches(":closed", element).Should().BeTrue();
        element.SetAttribute("open", "false");
        Matches(":open", element).Should().BeTrue();
        Matches(":closed", element).Should().BeFalse();
        element.RemoveAttribute("open");
        element.SetAttributeNS("urn:foreign", "f:open", "");
        Matches(":open", element).Should().BeFalse();
        Matches(":closed", element).Should().BeTrue();
        var foreign = element.OwnerDocument!.CreateElementNS(Namespaces.Svg, name);
        foreign.SetAttribute("open", "");
        Matches(":open, :closed", foreign).Should().BeFalse();
    }

    [TestCase(null, false, true)]
    [TestCase("1", false, true)]
    [TestCase("  +1tail", false, true)]
    [TestCase("wide", false, true)]
    [TestCase("0", false, false)]
    [TestCase("4", false, false)]
    [TestCase("1", true, false)]
    public void LegacyClosedSelectApplicabilityUsesPureDisplaySizeMetadata(string? size, bool multiple, bool closed)
    {
        var select = Document.CreateHtml().CreateElement("select");
        var attributes = new List<ParserAttribute>();
        if (size is not null) attributes.Add(new(null, "size", null, size));
        if (multiple) attributes.Add(new(null, "multiple", null, ""));
        attributes.Add(new(null, "open", null, ""));
        select.InitializeParsedAttributes(attributes.ToArray(), default);
        Matches(":closed", select).Should().Be(closed);
        Matches(":open", select).Should().BeFalse();
        select.ExistingSelectCore.Should().BeNull();
        select.ExistingSelectState.Should().BeNull();
    }

    [TestCase("file", true)]
    [TestCase("FILE", true)]
    [TestCase("text", false)]
    [TestCase("color", false)]
    [TestCase(" file", false)]
    public void LegacyClosedInputsPreserveOnlyTheEstablishedFilePickerCategory(string type, bool closed)
    {
        var input = Document.CreateHtml().CreateElement("input");
        input.InitializeParsedAttributes(new ParserAttribute[]
        {
            new(null, "type", null, type), new(null, "open", null, "")
        }, default);
        Matches(":closed", input).Should().Be(closed);
        Matches(":open", input).Should().BeFalse();
        input.ExistingInputValueState.Should().BeNull();
        input.ExistingCheckedState.Should().BeNull();
        Matches(":open, :closed", input.OwnerDocument!.CreateElement("div")).Should().BeFalse();
    }

    [Test]
    public void HyperlinksUseExactElementAndAttributeNamespacesAndNoVisitedHistory()
    {
        var document = Document.CreateHtml();
        foreach (var element in new[] { document.CreateElement("a"), document.CreateElement("area"),
                     document.CreateElementNS(Namespaces.Svg, "a") })
        {
            Matches(":link, :any-link, :visited", element).Should().BeFalse();
            element.SetAttribute("href", "");
            Matches(":link:any-link", element).Should().BeTrue();
            Matches(":visited", element).Should().BeFalse();
            element.RemoveAttribute("href");
            element.SetAttributeNS("urn:foreign", "f:href", "");
            Matches(":link, :any-link", element).Should().BeFalse();
        }
        var svg = document.CreateElementNS(Namespaces.Svg, "a");
        svg.SetAttributeNS(XLink, "x:href", "");
        Matches(":link:any-link", svg).Should().BeTrue();
        var foreign = document.CreateElementNS("urn:foreign", "a");
        foreign.SetAttribute("href", "");
        Matches(":any-link", foreign).Should().BeFalse();
        var wrongCase = document.CreateElementNS(Namespaces.Svg, "A");
        wrongCase.SetAttribute("href", "");
        Matches(":any-link", wrongCase).Should().BeFalse();
        var htmlXlink = document.CreateElement("a");
        htmlXlink.SetAttributeNS(XLink, "x:href", "");
        Matches(":any-link", htmlXlink).Should().BeFalse();
    }

    [TestCase(Namespaces.Html)]
    [TestCase(Namespaces.Svg)]
    [TestCase("urn:foreign")]
    public void NestedSvgAnchorsAreInactiveOnlyUnderActualHyperlinkAncestors(string ancestorNamespace)
    {
        var document = Document.CreateHtml();
        var parent = document.CreateElementNS(ancestorNamespace, "a");
        parent.SetAttribute("href", "");
        var child = document.CreateElementNS(Namespaces.Svg, "a");
        child.SetAttributeNS(XLink, "x:href", "");
        parent.AppendChild(child);
        Matches(":link:any-link", child).Should().Be(ancestorNamespace == "urn:foreign");
        parent.RemoveAttribute("href");
        Matches(":any-link", child).Should().BeTrue();
    }

    [Test]
    public void EveryAcceptedStageAPredicateHasCapabilitySupportAndClassSpecificity()
    {
        var document = Document.CreateHtml();
        var details = document.CreateElement("details");
        foreach (var text in new[] { ":checked", ":indeterminate", ":open", ":closed", ":link", ":any-link", ":visited" })
        {
            var selector = Parse(text);
            selector.MaximumSpecificity.Should().Be(new SelectorSpecificity(0, 1, 0));
            SelectorMatcher.Supports(selector, new Jint.HtmlParser.Css.Values.CssValueWork(default)).Should().BeTrue();
            SelectorMatcher.Matches(selector, details); // Evaluated false answers remain supported answers.
        }
        Assert.Throws<InvalidOperationException>(() => Matches(":default", details));
        Assert.Throws<InvalidOperationException>(() => Matches(":valid", details));
    }
}

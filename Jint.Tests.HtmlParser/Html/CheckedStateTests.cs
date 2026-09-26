#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class CheckedStateTests
{
    private static HtmlInputCheckedState State(Element element) => HtmlCheckableState.Get(element)!;
    private static Element Radio(Document document, string? name = "g", bool check = false)
    {
        var element = document.CreateElement("input");
        element.SetAttribute("type", "radio");
        if (name is not null) element.SetAttribute("name", name);
        if (check) element.SetAttribute("checked", "false");
        return element;
    }

    [Test]
    public void AttributePresenceDirtinessAndReplacementAreIndependent()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document, check: true);
        var b = Radio(document, check: true);
        root.AppendChild(a);
        root.AppendChild(b);
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeTrue();
        State(b).SetChecked(true, default);
        State(a).Checked.Should().BeFalse();
        State(a).DirtyCheckedness.Should().BeFalse();
        State(a).DefaultChecked.Should().BeTrue();
        a.GetAttributeNode("checked")!.Value = "new";
        State(a).Checked.Should().BeFalse();
        var replacement = document.CreateAttribute("checked");
        a.SetAttributeNode(replacement);
        State(a).Checked.Should().BeFalse();
        a.RemoveAttribute("checked");
        a.SetAttribute("checked", "false");
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeFalse();
        State(b).DirtyCheckedness.Should().BeTrue();
        b.RemoveAttribute("checked");
        State(b).SetChecked(true, default);
        State(b).SetDefaultChecked(false);
        State(b).Checked.Should().BeTrue();
        State(b).SetIndeterminate(true);
        HtmlCheckednessAlgorithms.ResetCheckedness(b, default);
        State(b).Checked.Should().BeFalse();
        State(b).DirtyCheckedness.Should().BeFalse();
        State(b).Indeterminate.Should().BeTrue();
    }

    [Test]
    public void DisconnectedDuplicatesSurviveReadsAndConnectionUsesCurrentFlags()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document, check: true);
        var b = Radio(document, check: true);
        root.AppendChild(a);
        root.AppendChild(b);
        HtmlCheckableState.GetRadioGroupFacts(a, default).CheckedCount.Should().Be(2);
        HtmlCheckableState.FirstCheckedRadio(b, default).Should().BeSameAs(a);
        HtmlCheckableState.SnapshotRadioGroup(b, default).Should().Equal(a, b);
        document.AppendChild(root);
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeFalse();
        State(b).SetChecked(true, default);
        State(a).Checked.Should().BeFalse();
        root.RemoveChild(a);
        State(a).SetChecked(true, default);
        root.RemoveChild(b);
        root.AppendChild(a);
        root.AppendChild(b);
        // root is connected, sequential insertion makes the later trigger win.
        State(a).Checked.Should().BeFalse();
        State(b).Checked.Should().BeTrue();
    }

    [TestCase("g", "G", false)]
    [TestCase("groüp", "groüp", true)]
    [TestCase(" ", " ", true)]
    [TestCase("", "", false)]
    [TestCase(null, null, false)]
    [TestCase("é", "é", false)]
    public void NamesAreExactAndEmptyNamesAreSingletons(string? first, string? second, bool same)
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document, first, true);
        var b = Radio(document, second, true);
        root.AppendChild(a);
        root.AppendChild(b);
        HtmlCheckableState.SameRadioGroup(a, b, default).Should().Be(same);
        b.SetAttribute("name", second ?? "");
        State(a).Checked.Should().Be(!same);
        HtmlCheckableState.SameRadioGroup(a, a, default).Should().BeTrue();
        b.SetAttribute("required", "");
        b.SetAttribute("disabled", "");
        HtmlCheckableState.GetRadioGroupFacts(b, default).RequiredCount.Should().Be(1);
    }

    [Test]
    public void UnrelatedIdResetRunsIntermediateOwnerStore()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        var form = document.CreateElement("form");
        form.SetAttribute("id", "f");
        root.AppendChild(form);
        var a = Radio(document);
        a.SetAttribute("form", "f");
        root.AppendChild(a);
        var b = Radio(document);
        root.AppendChild(b);
        var unrelated = document.CreateElement("div");
        unrelated.SetAttribute("id", "x");
        root.AppendChild(unrelated);
        State(a).SetChecked(true, default);
        State(b).SetChecked(true, default);
        unrelated.SetAttribute("id", "y");
        HtmlFormState.GetOwner(a).Should().BeSameAs(form);
        State(a).Checked.Should().BeTrue();
        State(b).Checked.Should().BeFalse();
        State(b).DirtyCheckedness.Should().BeTrue();
        State(b).SetChecked(true, default);
        a.SetAttribute("form", "f"); // Equal form writes still reset ownership.
        State(b).Checked.Should().BeFalse();
        State(b).SetChecked(true, default);
        unrelated.SetAttribute("id", "y"); // Equal ID is not an ID change.
        State(b).Checked.Should().BeTrue();
        var newcomer = document.CreateElement("div");
        newcomer.SetAttribute("id", "");
        root.AppendChild(newcomer);
        State(b).Checked.Should().BeFalse();
        State(b).SetChecked(true, default);
        root.RemoveChild(newcomer);
        State(b).Checked.Should().BeFalse();
    }

    [Test]
    public void EveryTypeRetainsFlagsAndWrongKindsHaveEmptyFacts()
    {
        var document = Document.CreateXml();
        var input = document.CreateElementNS(Namespaces.Html, "input");
        State(input).SetChecked(true, default);
        State(input).SetIndeterminate(true);
        foreach (var type in Enum.GetValues<HtmlInputType>())
        {
            input.SetAttribute("type", HtmlInputTypes.Info(type).Keyword);
            State(input).Checked.Should().BeTrue();
            State(input).Indeterminate.Should().BeTrue();
            HtmlCheckableState.MatchesChecked(input).Should().Be(type is HtmlInputType.Checkbox or HtmlInputType.Radio);
        }
        foreach (var unrelated in new[] { document.CreateElementNS(Namespaces.Html, "Input"),
                     document.CreateElementNS(Namespaces.Svg, "input"), document.CreateElement("input") })
        {
            HtmlCheckableState.Get(unrelated).Should().BeNull();
            HtmlCheckableState.GetRadioGroupFacts(unrelated, default).Should().Be(default(HtmlRadioGroupFacts));
            HtmlCheckableState.SnapshotRadioGroup(unrelated, default).Should().BeEmpty();
            HtmlCheckableState.SameRadioGroup(unrelated, unrelated, default).Should().BeFalse();
        }
        input.SetAttribute("type", "checkbox");
        HtmlCheckableState.MatchesIndeterminate(input, default).Should().BeTrue();
        input.SetAttributeNS("urn:x", "x:checked", "");
        State(input).DefaultChecked.Should().BeFalse();
    }

    [Test]
    public void CloneCopiesCleanUncheckedDefaultAndAllFlags()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("div");
        var a = Radio(document, check: true);
        var b = Radio(document, check: true);
        root.AppendChild(a);
        root.AppendChild(b);
        State(b).SetChecked(true, default);
        State(a).SetIndeterminate(true);
        var copy = (Element) a.CloneNode();
        State(copy).Checked.Should().BeFalse();
        State(copy).DefaultChecked.Should().BeTrue();
        State(copy).DirtyCheckedness.Should().BeFalse();
        State(copy).Indeterminate.Should().BeTrue();
        HtmlCheckableState.GetRadioGroupFacts(copy, default).MemberCount.Should().Be(1);
        var imported = (Element) Document.CreateHtml().ImportNode(b, false);
        State(imported).DirtyCheckedness.Should().BeTrue();
        State(imported).Checked.Should().BeTrue();
    }

    [Test]
    public void StateWritesAdvanceRevisionWithoutAttributeRecordsAndAlgorithmPreservesDirty()
    {
        var document = Document.CreateHtml();
        var input = Radio(document);
        using var observer = document.ObserveMutations(input, new MutationObserverOptions { Attributes = true });
        var stamp = document.MutationStamp;
        State(input).SetChecked(false, default);
        document.MutationStamp.Should().BeGreaterThan(stamp);
        observer.TakeRecords().Should().BeEmpty();
        HtmlCheckednessAlgorithms.Set(input, true, HtmlCheckedChangeOrigin.Algorithm, default);
        State(input).DirtyCheckedness.Should().BeTrue();
        HtmlCheckednessAlgorithms.ResetCheckedness(input, default);
        HtmlCheckednessAlgorithms.Set(input, false, HtmlCheckedChangeOrigin.UserInteraction, default);
        State(input).DirtyCheckedness.Should().BeFalse();
        HtmlCheckednessAlgorithms.Set(input, true, HtmlCheckedChangeOrigin.UserInteraction, default);
        State(input).DirtyCheckedness.Should().BeTrue();
    }

    [Test]
    public void ParserBatchOrderAndConnectedParserInsertionAreDistinctTriggers()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main");
        document.AppendChild(root);
        foreach (var order in new[] { new[] { "checked", "type", "name" }, new[] { "name", "type", "checked" } })
        {
            var a = document.CreateElement("input");
            var batch = order.Select(name => new ParserAttribute(null, name, null,
                name == "type" ? "radio" : name == "name" ? "g" : "")).ToArray();
            a.InitializeParsedAttributes(batch, default);
            root.AppendParsedChild(a);
            State(a).Checked.Should().BeTrue();
        }
        var inputs = root.ChildNodes.Cast<Element>().ToArray();
        State(inputs[0]).Checked.Should().BeFalse();
        State(inputs[1]).Checked.Should().BeTrue();
        var clone = (Element) root.CloneNode(true);
        State((Element) clone.FirstChild!).Checked.Should().BeFalse();
    }
}

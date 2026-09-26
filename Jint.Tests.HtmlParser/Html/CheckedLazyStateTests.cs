#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Html;

namespace Jint.Tests.HtmlParser.Html;

public class CheckedLazyStateTests
{
    private static Element Input(Document document, string type, bool check = false)
    {
        var input = document.CreateElement("input");
        var attributes = new List<ParserAttribute> { new(null, "type", null, type), new(null, "name", null, "g") };
        if (check) attributes.Add(new(null, "checked", null, ""));
        input.InitializeParsedAttributes(attributes.ToArray(), default);
        return input;
    }

    [Test]
    public void RealParsingKeepsNonradiosColdAndPreservesRadioExclusionHistory()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        session.AppendInput(string.Concat(Enum.GetValues<HtmlInputType>().Select(type =>
            $"<input type='{HtmlInputTypes.Info(type).Keyword}' name=g checked>")) + "<input type=radio name=g checked>", isFinal: true);
        HtmlParseStep step;
        do { step = session.Drive(100000, default); } while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var body = document.DocumentElement!.LastChild!;
        foreach (var input in body.ChildNodes.OfType<Element>())
        {
            input.ExistingInputValueState.Should().BeNull();
            if (input.GetAttribute("type") == "radio") input.ExistingCheckedState.Should().NotBeNull();
            else input.ExistingCheckedState.Should().BeNull();
        }
        var radios = body.ChildNodes.OfType<Element>().Where(input => input.GetAttribute("type") == "radio").ToArray();
        radios[0].ExistingCheckedState!.Checked.Should().BeFalse();
        radios[0].ExistingCheckedState!.DirtyCheckedness.Should().BeFalse();
        radios[0].GetAttributeNode("checked").Should().NotBeNull();
        radios[1].ExistingCheckedState!.Checked.Should().BeTrue();
    }

    [Test]
    public void SelectorsGroupQueriesOwnerMovesAndColdClonesDoNotEnhanceNonradios()
    {
        var document = Document.CreateHtml();
        var form = document.CreateElement("form"); document.AppendChild(form);
        foreach (var type in Enum.GetValues<HtmlInputType>().Where(type => type != HtmlInputType.Radio))
        {
            var input = Input(document, HtmlInputTypes.Info(type).Keyword, check: true);
            form.AppendChild(input);
            var checkable = type == HtmlInputType.Checkbox;
            HtmlCheckableState.MatchesChecked(input).Should().Be(checkable);
            HtmlCheckableState.MatchesDefaultCheckable(input).Should().Be(checkable);
            HtmlCheckableState.MatchesUnchecked(input, default).Should().BeFalse();
            HtmlCheckableState.MatchesIndeterminate(input, default).Should().BeFalse();
            HtmlCheckableState.GetRadioGroupFacts(input, default).Should().Be(default(HtmlRadioGroupFacts));
            HtmlCheckableState.SnapshotRadioGroup(input, default).Should().BeEmpty();
            var copy = (Element) input.CloneNode();
            var other = Document.CreateHtml();
            var imported = (Element) other.ImportNode(input);
            other.AdoptNode(input);
            input.ExistingCheckedState.Should().BeNull();
            copy.ExistingCheckedState.Should().BeNull();
            imported.ExistingCheckedState.Should().BeNull();
            HtmlCheckableState.Get(input)!.Checked.Should().BeTrue();
            HtmlCheckableState.Get(copy)!.Checked.Should().BeTrue();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FirstSemanticAccessLoadsLogicalDefaultWithoutMutation(bool check)
    {
        var input = Input(Document.CreateHtml(), "checkbox", check);
        input.ExistingCheckedState.Should().BeNull();
        var stamp = input.OwnerDocument!.MutationStamp;
        var state = HtmlCheckableState.Get(input)!;
        state.Checked.Should().Be(check);
        state.DefaultChecked.Should().Be(check);
        state.DirtyCheckedness.Should().BeFalse();
        state.Indeterminate.Should().BeFalse();
        HtmlCheckableState.Get(input).Should().BeSameAs(state);
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ColdConstructorCancellationDoesNotPublishFlagsOrMutation()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.InitializeParsedAttributes(Enumerable.Range(0, 1000).Select(i => new ParserAttribute(null, "data-" + i, null, "x"))
            .Append(new(null, "checked", null, "")).ToArray(), default);
        using var cancellation = new CancellationTokenSource();
        var probe = new HtmlCheckedWorkProbe { Checkpoint = units => { if (units == 300) cancellation.Cancel(); } };
        document.CheckedWorkProbe = probe;
        var work = new HtmlCheckedWork(probe, cancellation.Token);
        var stamp = document.MutationStamp;
        var exception = Assert.Throws<OperationCanceledException>(() => HtmlCheckableState.Get(input, ref work));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        input.ExistingCheckedState.Should().BeNull();
        probe.Units.Should().Be(512);
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void GroupIndexBuildDoesNotMaterializeUnrelatedInputs()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main"); document.AppendChild(root);
        var unrelated = Enumerable.Range(0, 40).Select(i => Input(document, i % 2 == 0 ? "text" : "checkbox", check: true)).ToArray();
        foreach (var input in unrelated) root.AppendChild(input);
        var radio = Input(document, "radio"); root.AppendChild(radio);
        HtmlCheckableState.GetRadioGroupFacts(radio, default).MemberCount.Should().Be(1);
        foreach (var input in unrelated) input.ExistingCheckedState.Should().BeNull();
    }

    [Test]
    public void RadioLoserAndExplicitFlagsSurviveLeavingAndReenteringRadio()
    {
        var document = Document.CreateHtml();
        var root = document.CreateElement("main"); document.AppendChild(root);
        var first = Input(document, "radio", true); var second = Input(document, "radio", true);
        root.AppendChild(first); root.AppendChild(second);
        var loser = first.ExistingCheckedState!;
        loser.Checked.Should().BeFalse(); loser.DefaultChecked.Should().BeTrue();
        first.SetAttribute("type", "text");
        first.ExistingCheckedState.Should().BeSameAs(loser);
        first.SetAttribute("type", "radio");
        loser.Checked.Should().BeFalse();
        second.ExistingCheckedState!.Checked.Should().BeTrue();
        var text = Input(document, "text", true);
        var flags = HtmlCheckableState.Get(text)!;
        flags.SetChecked(false, default); flags.SetIndeterminate(true);
        text.SetAttribute("type", "radio"); root.AppendChild(text);
        text.ExistingCheckedState.Should().BeSameAs(flags);
        flags.Checked.Should().BeFalse(); flags.DirtyCheckedness.Should().BeTrue(); flags.Indeterminate.Should().BeTrue();
    }

    [Test]
    public void WarmAndColdDefaultsAndGroupingHistoriesAgree()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var worlds = new[] { Document.CreateHtml(), Document.CreateHtml() };
            var roots = worlds.Select(world => world.CreateElement("main")).ToArray();
            var forms = worlds.Select(world => world.CreateElement("form")).ToArray();
            for (var i = 0; i < 2; i++) { worlds[i].AppendChild(roots[i]); forms[i].SetAttribute("id", "f"); roots[i].AppendChild(forms[i]); }
            var controls = worlds.Select(world => Input(world, "checkbox", seed % 2 == 0)).ToArray();
            _ = HtmlCheckableState.Get(controls[0]);
            var random = new Random(seed);
            for (var step = 0; step < 40; step++)
            {
                var operation = random.Next(7); var value = random.Next(2) == 0;
                for (var i = 0; i < 2; i++)
                {
                    var input = controls[i];
                    switch (operation)
                    {
                        case 0: if (value) input.SetAttribute("checked", ""); else input.RemoveAttribute("checked"); break;
                        case 1: input.SetAttribute("type", value ? "radio" : "text"); break;
                        case 2: input.SetAttribute("name", value ? "g" : "G"); break;
                        case 3: input.SetAttribute("form", value ? "f" : ""); break;
                        case 4: (value ? forms[i] : roots[i]).AppendChild(input); break;
                        case 5: input.SetAttribute("required", value ? "" : "false"); break;
                        default: input.RemoveAttribute("form"); break;
                    }
                }
            }
            var expected = HtmlCheckableState.Get(controls[0])!;
            var actual = HtmlCheckableState.Get(controls[1])!;
            (actual.Checked, actual.DefaultChecked, actual.DirtyCheckedness, actual.Indeterminate, actual.Type, actual.Name)
                .Should().Be((expected.Checked, expected.DefaultChecked, expected.DirtyCheckedness, expected.Indeterminate, expected.Type, expected.Name));
            HtmlCheckableState.GetRadioGroupFacts(controls[1], default).Should().Be(HtmlCheckableState.GetRadioGroupFacts(controls[0], default));
            for (var i = 0; i < 2; i++) HtmlCheckednessAlgorithms.ResetCheckedness(controls[i], default);
            actual.Checked.Should().Be(expected.Checked);
        }
    }
    [Test]
    public void ColdAttributeReplacementAndNamespaceRoutesRetainDerivableDefaults()
    {
        var document = Document.CreateHtml();
        var input = Input(document, "checkbox", true);
        input.GetAttributeNode("checked")!.Value = "false";
        var replacement = document.CreateAttribute("checked");
        replacement.Value = "replacement";
        input.SetAttributeNode(replacement);
        input.RemoveAttributeNode(replacement);
        input.SetAttributeNS("urn:test", "checked", "ignored");
        input.SetAttribute("required", ""); input.SetAttribute("name", "G");
        input.ExistingCheckedState.Should().BeNull();
        HtmlCheckableState.MatchesUnchecked(input, default).Should().BeTrue();
        input.ExistingCheckedState.Should().BeNull();
        input.SetAttribute("checked", "");
        HtmlCheckableState.MatchesUnchecked(input, default).Should().BeTrue();
        input.SetAttributeNS(null, "checked", "");
        input.ExistingCheckedState.Should().BeNull();
        var copy = (Element) input.CloneNode();
        var other = Document.CreateHtml();
        var imported = (Element) other.ImportNode(input);
        other.AdoptNode(input);
        foreach (var element in new[] { input, copy, imported })
        {
            element.ExistingCheckedState.Should().BeNull();
            var state = HtmlCheckableState.Get(element)!;
            state.Checked.Should().BeTrue(); state.DefaultChecked.Should().BeTrue();
            state.DirtyCheckedness.Should().BeFalse(); state.Indeterminate.Should().BeFalse();
            state.Name.Should().Be("G"); state.RegisteredRequired.Should().BeTrue();
        }
    }

}

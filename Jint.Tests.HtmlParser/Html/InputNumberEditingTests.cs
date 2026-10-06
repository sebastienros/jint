#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class InputNumberEditingTests
{
    private static Element Input(string value = "")
    {
        var document = Document.CreateHtml(); var input = document.CreateElement("input");
        input.InitializeParsedAttributes([new(null, "type", null, "number"), new(null, "value", null, value)], default);
        return input;
    }
    private static HtmlInputValueState State(Element input) => input.GetHtmlState()!.InputValue!;
    private static bool Edit(HtmlInputValueState state, string value, Action<int>? checkpoint = null, CancellationToken token = default)
        => state.ApplyUserValue(value, new((uint) value.Length, (uint) value.Length, HtmlSelectionDirection.None), checkpoint, token);
    private static (string, string, bool, bool, HtmlValueChangeOrigin, bool, HtmlTextSelection) Snapshot(HtmlInputValueState state)
        => (state.GetValue(default), state.GetEditingValue(default), state.BadInput, state.DirtyValue,
            state.LastValueChangeOrigin, state.UserValidity, state.Selection);

    [TestCase("-", "", true)]
    [TestCase("1e", "", true)]
    [TestCase("", "", false)]
    [TestCase("junk", "", true)]
    [TestCase("2e308", "", true)]
    [TestCase(" 1", "", true)]
    [TestCase("１", "", true)]
    [TestCase("0001.00", "1", false)]
    [TestCase("1e2", "100", false)]
    [TestCase("-0", "0", false)]
    [TestCase("0.3", "0.3", false)]
    public void AuthorDisplayAndCanonicalApiAreDistinctWithoutPublicSelection(string display, string api, bool bad)
    {
        var state = State(Input("5"));
        Edit(state, display).Should().BeTrue();
        state.GetValue(default).Should().Be(api); state.GetEditingValue(default).Should().Be(display);
        state.BadInput.Should().Be(bad); state.DirtyValue.Should().BeTrue();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.HasTextBuffer.Should().BeFalse(); state.HasEditingBuffer.Should().BeTrue();
        state.GetFacts(default).TextLength.Should().BeNull(); state.GetSelection(default).Should().BeNull();
        Assert.Throws<DomException>(() => state.SetSelectionRange(0, 1, null, default))!.Name.Should().Be("InvalidStateError");
        state.GetEditingSelection(default).Should().Be(new HtmlTextSelection((uint) display.Length, (uint) display.Length, HtmlSelectionDirection.None));
    }

    [Test]
    public void IncrementalPartialEditingDirtiesEvenWhenApiRemainsEmpty()
    {
        var state = State(Input()); state.SetUserValidity(true);
        Edit(state, "-").Should().BeTrue(); state.GetValue(default).Should().Be(""); state.BadInput.Should().BeTrue();
        state.GetValueAsNumber(default).Should().Be(double.NaN); state.HasNumericCoordinate.Should().BeTrue();
        var stamp = state.Element.OwnerDocument!.MutationStamp;
        Edit(state, "1e").Should().BeTrue(); state.GetValue(default).Should().Be("");
        state.HasNumericCoordinate.Should().BeFalse(); state.Element.OwnerDocument.MutationStamp.Should().BeGreaterThan(stamp);
        state.UserValidity.Should().BeTrue();
        Edit(state, "1e2").Should().BeTrue(); state.GetValue(default).Should().Be("100"); state.BadInput.Should().BeFalse();
        Edit(state, "-"); Edit(state, "-1"); state.GetValue(default).Should().Be("-1"); state.BadInput.Should().BeFalse();
        Edit(state, ""); state.GetValue(default).Should().Be(""); state.GetEditingValue(default).Should().Be(""); state.BadInput.Should().BeFalse();
    }

    [Test]
    public void PrivateSelectionUsesDisplayLengthAndSelectionOnlyEditsStayClean()
    {
        var state = State(Input("1"));
        state.SetEditingSelection(0, 1, "backward", default).Should().BeTrue();
        state.DirtyValue.Should().BeFalse();
        state.ApplyUserValue("0001", new(1, 3, HtmlSelectionDirection.Backward), default).Should().BeTrue();
        state.GetValue(default).Should().Be("1");
        state.GetEditingSelection(default).Should().Be(new HtmlTextSelection(1, 3, HtmlSelectionDirection.Backward));
        state.SetEditingSelection(0, 99, "forward", default);
        state.Selection.Should().Be(new HtmlTextSelection(0, 4, HtmlSelectionDirection.Forward));
        var stamp = state.Element.OwnerDocument!.MutationStamp;
        var before = Snapshot(state);
        state.ApplyUserValue("0001", state.Selection, default).Should().BeTrue();
        Snapshot(state).Should().Be(before); state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [TestCase("readonly")]
    [TestCase("disabled")]
    public void ReadonlyAndDisabledRejectEditorWritesButAllowScriptWrites(string attribute)
    {
        var input = Input("1"); var state = State(input);
        input.SetAttribute(attribute, ""); var before = Snapshot(state);
        Edit(state, "-").Should().BeFalse(); Snapshot(state).Should().Be(before);
        state.SetValueAsNumber(2, default); state.GetValue(default).Should().Be("2");
    }

    [Test]
    public void DisabledAncestorRejectsEditorWrites()
    {
        var input = Input("1"); var document = input.OwnerDocument!; var fieldset = document.CreateElement("fieldset");
        fieldset.SetAttribute("disabled", ""); fieldset.AppendChild(input); document.AppendChild(fieldset);
        var state = State(input); var before = Snapshot(state);
        Edit(state, "-").Should().BeFalse(); Snapshot(state).Should().Be(before);
    }

    [TestCase("string")]
    [TestCase("number")]
    [TestCase("step")]
    [TestCase("reset")]
    [TestCase("type")]
    public void SuccessfulScriptPathsClearPresentationEvenForEqualApiWrites(string path)
    {
        var input = Input(); var state = State(input); Edit(state, "-"); state.SetUserValidity(true);
        switch (path)
        {
            case "string": state.SetValue("", default); break;
            case "number": state.SetValueAsNumber(double.NaN, default); break;
            case "step": input.SetAttribute("max", "0"); state.StepUp(0, default); break;
            case "reset": state.ResetValue(default); break;
            default: input.SetAttribute("type", "text"); input.SetAttribute("type", "number"); break;
        }
        state.BadInput.Should().BeFalse(); state.GetEditingValue(default).Should().Be(state.GetValue(default));
        state.Selection.Should().Be(path == "step" ? new HtmlTextSelection(1, 1, HtmlSelectionDirection.None) : default(HtmlTextSelection));
        if (path != "type") state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        state.UserValidity.Should().Be(path != "reset");
    }

    [Test]
    public void ExceptionsEarlyStepsSameTypeAndUnrelatedAttributesPreserveDisplay()
    {
        var input = Input(); var state = State(input); Edit(state, "1e");
        var before = Snapshot(state);
        Assert.Throws<ArgumentException>(() => state.SetValueAsNumber(double.PositiveInfinity, default));
        Snapshot(state).Should().Be(before);
        input.SetAttribute("min", "2"); input.SetAttribute("max", "1"); state.StepUp(1, default);
        Snapshot(state).Should().Be(before);
        input.SetAttribute("type", "NUMBER"); input.SetAttribute("value", "99"); input.SetAttribute("data-x", "x");
        Snapshot(state).Should().Be(before); state.GetDefaultValue(default).Should().Be("99");
        input.SetAttribute("step", "any");
        Assert.Throws<DomException>(() => state.StepUp(1, default)); Snapshot(state).Should().Be(before);
    }

    [Test]
    public void CloneImportResetPresentationAndAdoptionRetainsIt()
    {
        var input = Input("default"); var state = State(input); Edit(state, "-"); state.SetUserValidity(true);
        var other = Document.CreateHtml();
        foreach (var copy in new[] { (Element) input.CloneNode(), (Element) other.ImportNode(input) })
        {
            var cloned = State(copy);
            cloned.GetValue(default).Should().Be(""); cloned.GetEditingValue(default).Should().Be("");
            cloned.BadInput.Should().BeFalse(); cloned.DirtyValue.Should().BeTrue(); cloned.UserValidity.Should().BeFalse();
            cloned.Selection.Should().Be(default(HtmlTextSelection));
        }
        other.AdoptNode(input); State(input).Should().BeSameAs(state);
        state.GetEditingValue(default).Should().Be("-"); state.BadInput.Should().BeTrue();
    }

    [TestCase(0, "short")]
    [TestCase(300, "short")]
    [TestCase(0, "long")]
    [TestCase(150, "comparison")]
    public void CancellationAndBudgetFailureAreAtomicAcrossAncestryGrammarAndComparison(int ancestors, string stage)
    {
        var input = Input(); var document = input.OwnerDocument!; Node parent = document;
        for (var i = 0; i < ancestors; i++) { var container = document.CreateElement("div"); parent.AppendChild(container); parent = container; }
        parent.AppendChild(input); var state = State(input);
        var original = stage == "comparison" ? new string('x', 200) : "-";
        Edit(state, original); state.SetUserValidity(true);
        var before = Snapshot(state); var stamp = document.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        var replacement = stage == "long" ? new string('0', 10000) + "1" : stage == "comparison" ? new string('x', 199) + "y" : "1e2";
        var exception = Assert.Throws<OperationCanceledException>(() => Edit(state, replacement, n => { counts.Add(n); cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        counts.Count.Should().Be(1); counts[0].Should().BeInRange(1, 256);
        Snapshot(state).Should().Be(before); document.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => Edit(state, replacement, _ => throw new InvalidOperationException("budget")));
        Snapshot(state).Should().Be(before); document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void SourceScansAndTailComparisonsShareOneIncreasingCounter()
    {
        var state = State(Input()); var counts = new List<int>();
        Edit(state, new string('0', 300) + "1", counts.Add).Should().BeTrue();
        counts.Should().Contain(256).And.Contain(512).And.BeInAscendingOrder().And.OnlyHaveUniqueItems();
        counts.Last().Should().BeGreaterThan(512).And.BeLessThan(768);
        state.GetValue(default).Should().Be("1"); state.GetEditingValue(default).Length.Should().Be(301);
    }

    [Test]
    public void ParserAndColdCloneKeepNumberEditorAbsentUntilDemand()
    {
        var input = Input("0001"); var clone = (Element) input.CloneNode();
        input.ExistingInputValueState.Should().BeNull(); clone.ExistingInputValueState.Should().BeNull();
        var stamp = input.OwnerDocument!.MutationStamp; var state = State(input);
        state.GetValue(default).Should().Be("0001"); state.GetEditingValue(default).Should().Be("0001"); state.BadInput.Should().BeFalse();
        state.DirtyValue.Should().BeFalse(); input.OwnerDocument.MutationStamp.Should().Be(stamp);
        State(clone).GetEditingValue(default).Should().Be("0001");
    }

    [Test]
    public void ScriptReplacementMovesCaretToEndAndNextEditAppends()
    {
        var state = State(Input()); Edit(state, "-");
        state.SetValue("123", default);
        state.GetEditingValue(default).Should().Be("123");
        state.Selection.Should().Be(new HtmlTextSelection(3, 3, HtmlSelectionDirection.None));
        var position = (int) state.Selection.Start;
        var display = state.GetEditingValue(default);
        state.ApplyUserValue(display.Insert(position, "4"), new(4, 4, HtmlSelectionDirection.None), default);
        state.GetValue(default).Should().Be("1234");
    }

    [Test]
    public void EqualScriptValuePreservesSelectionAndClearedDisplayClampsIt()
    {
        var state = State(Input("1"));
        state.SetEditingSelection(0, 1, "backward", default);
        state.SetValue("1", default);
        state.Selection.Should().Be(new HtmlTextSelection(0, 1, HtmlSelectionDirection.Backward));
        state.ApplyUserValue("0001", new(2, 4, HtmlSelectionDirection.Backward), default);
        state.SetValueAsNumber(1, default);
        state.GetEditingValue(default).Should().Be("1");
        state.Selection.Should().Be(new HtmlTextSelection(1, 1, HtmlSelectionDirection.Backward));
    }

    [TestCase("1e")]
    [TestCase("0001")]
    public void SelectUsesPresentationWithoutGrantingPublicSelection(string display)
    {
        var state = State(Input()); Edit(state, display);
        state.Select(default);
        state.Selection.Should().Be(new HtmlTextSelection(0, (uint) display.Length, HtmlSelectionDirection.None));
        state.GetSelection(default).Should().BeNull();
        Assert.Throws<DomException>(() => state.SetSelectionRange(0, 1, null, default))!.Name.Should().Be("InvalidStateError");
        state.GetEditingValue(default).Should().Be(display);
    }

    [TestCase("text")]
    [TestCase("email")]
    [TestCase("number")]
    public void SelectRetainsUnavailableFamilyGuardAfterTypeTransition(string destination)
    {
        var input = Input(); var state = State(input);
        input.SetAttribute("type", "color"); input.SetAttribute("type", destination);
        state.IsAvailable.Should().BeFalse();
        var stamp = input.OwnerDocument!.MutationStamp;
        Assert.Throws<NotSupportedException>(() => state.Select(default));
        state.Selection.Should().Be(default(HtmlTextSelection));
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }
}

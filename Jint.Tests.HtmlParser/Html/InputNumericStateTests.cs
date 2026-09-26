#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class InputNumericStateTests
{
    private static Element Input(string type, string? value = null, string? min = null, string? max = null, string? step = null)
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        var attributes = new List<ParserAttribute> { new(null, "type", null, type) };
        foreach (var (name, text) in new[] { ("value", value), ("min", min), ("max", max), ("step", step) })
            if (text is not null) attributes.Add(new(null, name, null, text));
        input.InitializeParsedAttributes(attributes.ToArray(), default);
        return input;
    }
    private static HtmlInputValueState State(Element input) => input.GetHtmlState()!.InputValue!;
    private static (string, bool, HtmlValueChangeOrigin, bool, HtmlTextSelection) Snapshot(HtmlInputValueState state)
        => (state.GetValue(default), state.DirtyValue, state.LastValueChangeOrigin, state.UserValidity, state.Selection);

    [TestCase("number", "00042", "00042", 42d)]
    [TestCase("number", "2e308", "", double.NaN)]
    [TestCase("number", " 42", "", double.NaN)]
    [TestCase("date", "1970-01-02", "1970-01-02", 86400000d)]
    [TestCase("month", "1969-12", "1969-12", -1d)]
    [TestCase("week", "1970-W01", "1970-W01", -259200000d)]
    [TestCase("time", "23:59:59.001", "23:59:59.001", 86399001d)]
    [TestCase("datetime-local", "1970-01-01 00:00:00.000", "1970-01-01T00:00", 0d)]
    [TestCase("range", null, "50", 50d)]
    public void FamiliesUseOneSanitizedValueAndDeferDerivedNumericModels(string type, string? raw, string expected, double number)
    {
        var input = Input(type, raw);
        input.ExistingInputValueState.Should().BeNull();
        var stamp = input.OwnerDocument!.MutationStamp;
        var state = State(input);
        state.GetValue(default).Should().Be(expected);
        state.GetDefaultValue(default).Should().Be(raw ?? "");
        state.IsAvailable.Should().BeTrue(); state.DirtyValue.Should().BeFalse();
        state.HasNumericCoordinate.Should().BeFalse(); state.HasDateCoordinate.Should().BeFalse();
        state.HasNumericConstraints.Should().BeFalse();
        state.GetValueAsNumber(default).Should().Be(number);
        state.HasNumericCoordinate.Should().BeTrue(); state.HasNumericConstraints.Should().BeFalse();
        state.GetFacts(default).TextLength.Should().BeNull();
        state.GetSelection(default).Should().BeNull();
        if (type == "number") state.GetEditingSelection(default).Should().Be(default(HtmlTextSelection));
        else
        {
            state.GetEditingSelection(default).Should().BeNull();
            state.ApplyUserValue("1", default, default).Should().BeFalse();
        }
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [TestCase("number", "1")]
    [TestCase("date", "1970-01-01")]
    [TestCase("month", "1970-01")]
    [TestCase("week", "1970-W01")]
    [TestCase("time", "00:00")]
    [TestCase("datetime-local", "1970-01-01T00:00")]
    public void RawConstraintsRemainLazyThroughCreationAndAttributeInvalidation(string type, string value)
    {
        var input = Input(type, value, step: new string('0', 10000) + "1");
        input.SetAttribute("min", new string('9', 10000));
        input.ExistingInputValueState.Should().BeNull();
        var state = State(input);
        state.HasNumericConstraints.Should().BeFalse();
        state.GetValue(default).Should().Be(value);
        state.GetValueAsNumber(default);
        state.HasNumericConstraints.Should().BeFalse();
        state.GetNumericFacts(default).Applies.Should().BeTrue();
        state.HasNumericConstraints.Should().BeTrue();
        input.SetAttribute("max", "2");
        state.HasNumericConstraints.Should().BeFalse();
        state.HasNumericCoordinate.Should().BeTrue();
        state.GetValue(default).Should().Be(value);
        state.HasNumericConstraints.Should().BeFalse();
        input.SetAttributeNS("urn:test", "min", "ignored");
        state.GetNumericFacts(default);
        state.GetValueAsDate(default);
        var stamp = input.OwnerDocument!.MutationStamp;
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            state.GetValue(default); state.GetValueAsNumber(default); state.GetNumericFacts(default); state.GetValueAsDate(default);
        }
        (GC.GetAllocatedBytesForCurrentThread() - bytes).Should().Be(0);
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
        state.GetValueAsDate(default);
        bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { state.GetValueAsDate(default); state.GetNumericFacts(default); }
        (GC.GetAllocatedBytesForCurrentThread() - bytes).Should().Be(0);
    }

    [Test]
    public void NumericEqualWriteAndZeroStepDirtyAndPreserveUserValidityWithoutAttributeRecords()
    {
        foreach (var useStep in new[] { false, true })
        {
            var input = Input("number", "1"); var state = State(input);
            state.SetUserValidity(true);
            using var observer = input.OwnerDocument!.ObserveMutations(input, new MutationObserverOptions { Attributes = true });
            var stamp = input.OwnerDocument!.MutationStamp;
            if (useStep) state.StepUp(0, default); else state.SetValueAsNumber(1, default);
            state.GetValue(default).Should().Be("1"); state.DirtyValue.Should().BeTrue(); state.UserValidity.Should().BeTrue();
            state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
            input.OwnerDocument.MutationStamp.Should().BeGreaterThan(stamp);
            observer.TakeRecords().Should().BeEmpty();
            input.SetAttribute("value", "2");
            state.GetValue(default).Should().Be("1"); state.GetDefaultValue(default).Should().Be("2");
            state.ResetValue(default);
            state.GetValue(default).Should().Be("2"); state.DirtyValue.Should().BeFalse(); state.UserValidity.Should().BeFalse();
        }
    }

    [TestCase("number", "1", "2", "1", null)]
    [TestCase("number", "1", "0", "0.5", null)]
    [TestCase("number", "1e308", null, null, "1e308")]
    public void EarlyStepReturnPreservesValueFlagsAndStamp(string type, string value, string? min, string? max, string? step)
    {
        var input = Input(type, value, min, max, step); var state = State(input);
        state.SetUserValidity(true);
        var before = Snapshot(state); var stamp = input.OwnerDocument!.MutationStamp;
        state.StepUp(1, default);
        Snapshot(state).Should().Be(before); input.OwnerDocument.MutationStamp.Should().Be(stamp);
        input.SetAttribute("value", "2"); state.GetValue(default).Should().Be("2");
    }

    [Test]
    public void ExceptionsHaveRequiredPrecedenceAndPreserveState()
    {
        var text = State(Input("text", "unchanged"));
        var before = Snapshot(text); var stamp = text.Element.OwnerDocument!.MutationStamp;
        Assert.Throws<ArgumentException>(() => text.SetValueAsNumber(double.PositiveInfinity, default));
        Assert.Throws<DomException>(() => text.SetValueAsNumber(double.NaN, default))!.Name.Should().Be("InvalidStateError");
        Assert.Throws<DomException>(() => text.SetValueAsDate(null, default))!.Name.Should().Be("InvalidStateError");
        Assert.Throws<DomException>(() => text.StepUp(1, default))!.Name.Should().Be("InvalidStateError");
        text.GetValueAsNumber(default).Should().Be(double.NaN); text.GetValueAsDate(default).HasDate.Should().BeFalse();
        Snapshot(text).Should().Be(before); text.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
        var number = State(Input("number", "1", step: "any"));
        before = Snapshot(number); stamp = number.Element.OwnerDocument!.MutationStamp;
        Assert.Throws<DomException>(() => number.StepDown(0, default))!.Name.Should().Be("InvalidStateError");
        Snapshot(number).Should().Be(before); number.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [TestCase("date", -0.5, "1969-12-31")]
    [TestCase("month", -0.5, "1969-12")]
    [TestCase("week", -0.5, "1970-W01")]
    [TestCase("time", -0.5, "23:59:59.999")]
    [TestCase("datetime-local", -0.5, "1969-12-31T23:59:59.999")]
    public void NumericTemporalWritesUseApprovedFloorPolicy(string type, double number, string expected)
    {
        var state = State(Input(type));
        state.SetValueAsNumber(number, default);
        state.GetValue(default).Should().Be(expected); state.DirtyValue.Should().BeTrue();
        state.SetValueAsNumber(double.NaN, default); state.GetValue(default).Should().Be("");
    }

    [TestCase("date", "275760-09-14")]
    [TestCase("month", "275760-10")]
    [TestCase("week", "275760-W38")]
    public void DateResultsDistinguishInvalidDateFromAbsentAndNumericTimeClip(string type, string value)
    {
        var state = State(Input(type, value));
        state.GetValueAsNumber(default).Should().BeGreaterThan(0);
        var result = state.GetValueAsDate(default);
        result.HasDate.Should().BeTrue(); result.UtcMilliseconds.Should().Be(double.NaN);
        state.GetValue(default).Should().Be(value);
        state.SetValueAsDate(null, default);
        state.GetValue(default).Should().Be(""); state.DirtyValue.Should().BeTrue();
        state.GetValueAsDate(default).HasDate.Should().BeFalse();
        state.SetValueAsDate(0, default);
        state.GetValue(default).Should().Be(type switch { "month" => "1970-01", "week" => "1970-W01", _ => "1970-01-01" });
        state.SetValueAsDate(double.NaN, default); state.GetValue(default).Should().Be("");
    }

    [Test]
    public void RangeNaNWritesSanitizeAndConstraintChangesRetainDirtyAndHistory()
    {
        var input = Input("range", "40", "0", "100", "20"); var state = State(input);
        state.SetValueAsNumber(double.NaN, default);
        state.GetValue(default).Should().Be("60"); state.DirtyValue.Should().BeTrue();
        input.SetAttribute("value", "80"); state.GetValue(default).Should().Be("60");
        input.SetAttribute("max", "40"); state.GetValue(default).Should().Be("40"); state.DirtyValue.Should().BeTrue();
        state.ResetValue(default); state.GetValue(default).Should().Be("40"); state.DirtyValue.Should().BeFalse();
        state = State(Input("range")); state.SetValueAsNumber(double.NaN, default); state.GetValue(default).Should().Be("50");
        var cold = Input("range");
        cold.SetAttribute("min", "80"); cold.SetAttribute("min", "0");
        State(cold).GetValue(default).Should().Be("80");
        State(cold).DirtyValue.Should().BeFalse();
        var other = Document.CreateHtml();
        foreach (var copy in new[] { (Element) cold.CloneNode(), (Element) other.ImportNode(cold) })
        {
            State(copy).GetValue(default).Should().Be("80");
            State(copy).DirtyValue.Should().BeFalse();
        }
    }

    [Test]
    public void DirtyDefaultChangesInvalidateBaseAndReflectedAttributeIdentity()
    {
        var input = Input("number", "1", step: "2"); var state = State(input);
        state.SetValue("3", default);
        state.GetNumericFacts(default).StepMismatch.Should().BeFalse(); state.GetValueAsNumber(default).Should().Be(3);
        input.GetAttributeNode("value")!.Value = "0";
        state.HasNumericConstraints.Should().BeFalse(); state.HasNumericCoordinate.Should().BeTrue();
        state.GetValue(default).Should().Be("3"); state.GetNumericFacts(default).StepMismatch.Should().BeTrue();
        var min = input.OwnerDocument!.CreateAttribute("min"); min.Value = "2"; input.SetAttributeNode(min);
        state.GetNumericFacts(default).StepMismatch.Should().BeTrue();
        min.Value = "1"; state.GetNumericFacts(default).StepMismatch.Should().BeFalse();
        input.SetAttributeNS("urn:test", "min", "0"); state.HasNumericConstraints.Should().BeTrue();
        input.RemoveAttributeNode(min); state.GetNumericFacts(default).StepMismatch.Should().BeTrue();
        input.OwnerDocument.CreateElement("div").SetAttribute("min", "999");
        state.HasNumericConstraints.Should().BeTrue();
    }

    [TestCase("date", "1970-01-01", ".5")]
    [TestCase("month", "1970-01", ".5")]
    [TestCase("week", "1970-W01", ".5")]
    [TestCase("time", "00:00", ".0005")]
    public void ProjectedEqualStepsStillDirtyTheAuthoritativeValue(string type, string value, string step)
    {
        var state = State(Input(type, value, step: step));
        state.StepUp(1, default); state.GetValue(default).Should().Be(value); state.DirtyValue.Should().BeTrue();
    }

    [Test]
    public void CloneImportAdoptionKeepCurrentValueAndDropDerivedAuthority()
    {
        var input = Input("date", "1970-01-01"); var state = State(input);
        state.SetValueAsNumber(86400000, default); state.SetUserValidity(true);
        state.GetValueAsDate(default); state.GetNumericFacts(default); state.GetValueAsNumber(default);
        var other = Document.CreateHtml();
        foreach (var copy in new[] { (Element) input.CloneNode(), (Element) other.ImportNode(input) })
        {
            var cloned = State(copy);
            cloned.GetValue(default).Should().Be("1970-01-02"); cloned.DirtyValue.Should().BeTrue(); cloned.UserValidity.Should().BeFalse();
            cloned.GetDefaultValue(default).Should().Be("1970-01-01");
            cloned.HasNumericConstraints.Should().BeFalse(); cloned.HasNumericCoordinate.Should().BeFalse(); cloned.HasDateCoordinate.Should().BeFalse();
        }
        other.AdoptNode(input); State(input).Should().BeSameAs(state); state.HasNumericConstraints.Should().BeTrue();
        state.GetValueAsNumber(default).Should().Be(86400000);
    }

    [TestCase("number", "00042")]
    [TestCase("date", "1970-01-01")]
    [TestCase("range", "40")]
    public void TypeTransitionsTransferTheSingleStoreAcrossValueAndDefaultModes(string type, string value)
    {
        var input = Input(type, value); var state = State(input);
        state.SetValue(value, default); state.GetNumericFacts(default);
        input.SetAttribute("type", "text");
        State(input).Should().BeSameAs(state); state.GetValue(default).Should().Be(value); state.DirtyValue.Should().BeTrue();
        state.HasNumericConstraints.Should().BeFalse();
        input.SetAttribute("type", "hidden"); input.GetAttribute("value").Should().Be(value);
        input.SetAttribute("type", type); state.DirtyValue.Should().BeFalse(); state.GetValue(default).Should().Be(value);
        input.SetAttribute("type", "file"); input.SetAttribute("value", value); input.SetAttribute("type", type);
        state.IsAvailable.Should().BeTrue(); state.DirtyValue.Should().BeFalse(); state.GetValue(default).Should().Be(value);
    }

    [TestCase("number", "00042", "text")]
    [TestCase("date", "1970-01-01", "text")]
    [TestCase("range", null, "hidden")]
    public void ColdAndObservedTypeTransitionsAgree(string type, string? value, string destination)
    {
        var cold = Input(type, value); var warm = Input(type, value); _ = State(warm);
        cold.SetAttribute("type", destination); warm.SetAttribute("type", destination);
        Snapshot(State(cold)).Should().Be(Snapshot(State(warm)));
        cold.GetAttribute("value").Should().Be(warm.GetAttribute("value"));
    }

    [TestCase("number", "1", false)]
    [TestCase("range", "50", false)]
    [TestCase("date", "1970-01-01", true)]
    public void CancelledLongSanitizationLeavesEveryObservableFieldUntouched(string type, string initial, bool temporal)
    {
        var state = State(Input(type, initial)); state.SetUserValidity(true);
        var before = Snapshot(state); var stamp = state.Element.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        var value = temporal ? new string('0', 10000) + "1970-01-01" : new string('0', 10000) + "1";
        var exception = Assert.Throws<OperationCanceledException>(() => state.SetValue(value, n => { if (n == 256) cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        Snapshot(state).Should().Be(before); state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void StepCheckpointExceptionDoesNotCommitAndCancellationKeepsOriginalToken()
    {
        var state = State(Input("number", "1")); state.SetUserValidity(true);
        var before = Snapshot(state); var stamp = state.Element.OwnerDocument!.MutationStamp;
        Assert.Throws<InvalidOperationException>(() => state.Step(1, false, _ => throw new InvalidOperationException("budget"), default));
        Snapshot(state).Should().Be(before); state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
        using var cancellation = new CancellationTokenSource();
        var exception = Assert.Throws<OperationCanceledException>(() => state.Step(1, false, _ => cancellation.Cancel(), cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        Snapshot(state).Should().Be(before); state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ReversedTimeFactsComeFromNativeConstraintsAndNeverChangeTheValue()
    {
        var state = State(Input("time", "12:00", "23:00", "01:00", "any"));
        var stamp = state.Element.OwnerDocument!.MutationStamp;
        var facts = state.GetNumericFacts(default);
        facts.HasReversedRange.Should().BeTrue(); facts.Underflow.Should().BeTrue(); facts.Overflow.Should().BeTrue();
        facts.StepMismatch.Should().BeFalse(); facts.HasAllowedStep.Should().BeFalse();
        state.GetValue(default).Should().Be("12:00");
        state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
        state.SetValue("00:30", default);
        facts = state.GetNumericFacts(default);
        facts.Underflow.Should().BeFalse(); facts.Overflow.Should().BeFalse();
    }

    [Test]
    public void ExactStepPublicationMayCollapseWithoutChangingTheMismatchRule()
    {
        var state = State(Input("number", "9007199254740992", min: "0", step: "3"));
        state.GetNumericFacts(default).StepMismatch.Should().BeTrue();
        state.StepUp(0, default);
        state.GetValue(default).Should().Be("9007199254740992"); state.DirtyValue.Should().BeTrue();
        state.GetNumericFacts(default).StepMismatch.Should().BeTrue();
        state.StepDown(1, default);
        state.GetValue(default).Should().Be("9007199254740990"); state.GetNumericFacts(default).StepMismatch.Should().BeFalse();
    }

    [Test]
    public void HugeLexicalTemporalStringDoesNotRequireNumericOrDateConversion()
    {
        var value = new string('9', 10000) + "-01-01";
        var input = Input("date", value); var stamp = input.OwnerDocument!.MutationStamp;
        var state = State(input);
        state.GetValue(default).Should().BeSameAs(value);
        state.HasNumericCoordinate.Should().BeFalse(); state.HasDateCoordinate.Should().BeFalse(); state.HasNumericConstraints.Should().BeFalse();
        state.GetValueAsNumber(default).Should().Be(double.NaN);
        state.GetValueAsDate(default).HasDate.Should().BeTrue(); state.GetValueAsDate(default).UtcMilliseconds.Should().Be(double.NaN);
        state.GetValue(default).Should().BeSameAs(value); input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void NumericTransitionClearsObsoleteTextSelectionAndScriptWritesClearUserOrigin()
    {
        var input = Input("text", "1"); var state = State(input);
        state.ApplyUserValue("12", new(1, 2, HtmlSelectionDirection.Backward), default);
        state.SetUserValidity(true);
        input.SetAttribute("type", "number");
        state.GetValue(default).Should().Be("12"); state.DirtyValue.Should().BeTrue();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User); state.Selection.Should().Be(default(HtmlTextSelection));
        state.SetValueAsNumber(12, default);
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser); state.UserValidity.Should().BeTrue();
        input.SetAttribute("type", "text"); state.GetSelection(default).Should().Be(default(HtmlTextSelection));
    }

    [TestCase("number", "1")]
    [TestCase("date", "1970-01-01")]
    public void StepBudgetCanCancelWhileReadingLongConstraintAttributes(string type, string value)
    {
        var state = State(Input(type, value, step: new string('0', 10000) + "1"));
        state.SetUserValidity(true); var before = Snapshot(state); var stamp = state.Element.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        var exception = Assert.Throws<OperationCanceledException>(() => state.Step(1, false,
            units => { if (units == 300) cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        state.HasNumericConstraints.Should().BeFalse(); Snapshot(state).Should().Be(before);
        state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }
}

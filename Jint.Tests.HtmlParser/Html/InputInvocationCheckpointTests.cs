#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class InputInvocationCheckpointTests
{
    private static Element Input(string type, string value, int extraAttributes = 0)
    {
        var document = Document.CreateHtml(); var input = document.CreateElement("input");
        var attributes = Enumerable.Range(0, extraAttributes).Select(i => new ParserAttribute(null, "data-" + i, null, "x"))
            .Append(new(null, "type", null, type)).Append(new(null, "value", null, value)).ToArray();
        input.InitializeParsedAttributes(attributes, default); return input;
    }
    private static HtmlInputValueState State(Element input) => input.GetHtmlState()!.GetInputValueState(default)!;
    private static (string, string, bool, bool, HtmlValueChangeOrigin, bool, HtmlTextSelection) Snapshot(HtmlInputValueState state)
        => (state.GetValue(default), state.GetEditingValue(default), state.BadInput, state.DirtyValue,
            state.LastValueChangeOrigin, state.UserValidity, state.Selection);

    [TestCase("text", "long")]
    [TestCase("number", "long")]
    [TestCase("date", "long")]
    [TestCase("range", "long")]
    [TestCase("text", "metadata")]
    [TestCase("text", "tail")]
    public void ColdInitializationBudgetAndTokenFailuresDoNotPublish(string type, string stage)
    {
        var value = stage == "long" ? type == "date" ? new string('0', 10000) + "1970-01-01" : new string('0', 10000) + "1" : "abc";
        var input = Input(type, value, stage == "metadata" ? 1000 : 0);
        var view = input.GetHtmlState()!; var stamp = input.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        var exception = Assert.Throws<OperationCanceledException>(() => view.GetInputValueState(n => { counts.Add(n); cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        counts.Count.Should().Be(1); counts[0].Should().BeInRange(1, 256);
        view.ExistingInputValue.Should().BeNull(); input.OwnerDocument.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => view.GetInputValueState(_ => throw new InvalidOperationException("budget"), default));
        view.ExistingInputValue.Should().BeNull(); input.OwnerDocument.MutationStamp.Should().Be(stamp);
        var completed = view.GetInputValueState(default)!;
        view.GetInputValueState(default).Should().BeSameAs(completed);
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void MetadataAndSanitizerShareOneCounterWithOneTailBeforePublication()
    {
        var input = Input("text", new string('x', 60), extraAttributes: 200); var counts = new List<int>();
        input.GetHtmlState()!.GetInputValueState(counts.Add, default)!.GetValue(default).Length.Should().Be(60);
        counts.Should().Equal(256, 263); // Constructor + 202 actual attributes + 60 characters.
    }

    [TestCase("number")]
    [TestCase("date")]
    [TestCase("facts")]
    public void LongNumericReadsPollTheCallerBeforePublishingDerivedResult(string operation)
    {
        var value = new string('9', 10000) + "-01-01";
        var input = Input("date", value); var state = State(input);
        var before = Snapshot(state); var stamp = input.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        void Read(Action<int> checkpoint, CancellationToken token)
        {
            if (operation == "number") state.GetValueAsNumber(checkpoint, token);
            else if (operation == "date") state.GetValueAsDate(checkpoint, token);
            else state.GetNumericFacts(checkpoint, token);
        }
        var exception = Assert.Throws<OperationCanceledException>(() => Read(n => { counts.Add(n); cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Should().Equal(256);
        state.HasNumericCoordinate.Should().BeFalse(); state.HasDateCoordinate.Should().BeFalse();
        Snapshot(state).Should().Be(before); input.OwnerDocument.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => Read(_ => throw new InvalidOperationException("budget"), default));
        Snapshot(state).Should().Be(before); input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ConstraintAndValueScansShareCadenceAndWarmReadCallbacksAllocateNothing()
    {
        var input = Input("number", "3");
        input.SetAttribute("min", new string('0', 300) + "1"); input.SetAttribute("step", new string('0', 300) + "1");
        var state = State(input); var counts = new List<int>();
        state.GetNumericFacts(counts.Add, default).StepMismatch.Should().BeFalse();
        counts.Should().Contain(256).And.Contain(512).And.Contain(768).And.Contain(1024)
            .And.BeInAscendingOrder().And.OnlyHaveUniqueItems();
        counts.Last().Should().BeInRange(1025, 1279);
        state.GetValueAsNumber(default); state.GetValueAsDate(default);
        Action<int> checkpoint = static _ => { };
        // Warm the actual callback overloads before measuring their cached path.
        state.GetValueAsNumber(checkpoint, default); state.GetNumericFacts(checkpoint, default); state.GetValueAsDate(checkpoint, default);
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { state.GetValueAsNumber(checkpoint, default); state.GetNumericFacts(checkpoint, default); state.GetValueAsDate(checkpoint, default); }
        (GC.GetAllocatedBytesForCurrentThread() - bytes).Should().Be(0);
    }

    [TestCase("value")]
    [TestCase("default")]
    [TestCase("reset")]
    [TestCase("splice")]
    public void LongTextOperationsCancelBeforeValueFlagsSelectionOrDefaultMutation(string operation)
    {
        var input = Input("text", new string('a', 1000)); var state = State(input);
        state.SetSelectionRange(1, 2, "backward", default); state.SetUserValidity(true);
        var before = Snapshot(state); var originalDefault = state.GetDefaultValue(default); var stamp = input.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        void Run(Action<int> checkpoint)
        {
            if (operation == "value") state.SetValue(new string('b', 1000), checkpoint, cancellation.Token);
            else if (operation == "default") state.SetDefaultValue(new string('b', 1000), checkpoint, cancellation.Token);
            else if (operation == "reset") state.ResetValue(checkpoint, cancellation.Token);
            else state.SetRangeText(new string('b', 1000), 1, 2, HtmlRangeTextMode.Preserve, checkpoint, cancellation.Token);
        }
        var exception = Assert.Throws<OperationCanceledException>(() => Run(n => { counts.Add(n); cancellation.Cancel(); }));
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Should().Equal(256);
        Snapshot(state).Should().Be(before); state.GetDefaultValue(default).Should().Be(originalDefault);
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void TextReplacementCounterIncludesActualCopiesSanitizationAndComparison()
    {
        var state = State(Input("text", new string('a', 300))); var counts = new List<int>();
        state.SetRangeText("b", 299, 300, HtmlRangeTextMode.End, counts.Add, default);
        counts.Should().Equal(256, 512, 768, 903); // One entry, 300 copies, 300 sanitizer chars, 301 comparison units, one selection phase.
        state.GetValue(default).Should().Be(new string('a', 299) + "b");
        state.Selection.Should().Be(new HtmlTextSelection(300, 300, HtmlSelectionDirection.None));
    }

    [Test]
    public void ShortNumericWriteTailCancellationPreservesPartialEditor()
    {
        var state = State(Input("number", ""));
        state.ApplyUserValue("-", new(1, 1, HtmlSelectionDirection.None), default); state.SetUserValidity(true);
        var before = Snapshot(state); var stamp = state.Element.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.SetValueAsNumber(double.NaN, _ => cancellation.Cancel(), cancellation.Token));
        Snapshot(state).Should().Be(before); state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void SelectionBudgetFailurePrecedesBoundedOffsetWrites()
    {
        var state = State(Input("text", "abc")); state.SetSelectionRange(1, 2, "backward", default);
        var before = Snapshot(state); var stamp = state.Element.OwnerDocument!.MutationStamp;
        Action<int> budget = _ => throw new InvalidOperationException("budget");
        foreach (var operation in new Action[]
        {
            () => state.SetSelectionStart(0, budget, default), () => state.SetSelectionEnd(3, budget, default),
            () => state.SetSelectionDirection("forward", budget, default), () => state.SetSelectionRange(0, 3, null, budget, default),
            () => state.SetEditingSelection(0, 3, null, budget, default), () => state.Select(budget, default)
        })
        {
            Assert.Throws<InvalidOperationException>(() => operation()); Snapshot(state).Should().Be(before);
            state.Element.OwnerDocument.MutationStamp.Should().Be(stamp);
        }
    }

    [Test]
    public void WarmStateLookupStillHonorsCallbackAndOriginalCancellationToken()
    {
        var input = Input("number", "1"); var view = input.GetHtmlState()!; var state = State(input);
        using var cancellation = new CancellationTokenSource();
        var exception = Assert.Throws<OperationCanceledException>(() => view.GetInputValueState(_ => cancellation.Cancel(), cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token); view.ExistingInputValue.Should().BeSameAs(state);
    }
}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class TextAreaInvocationCheckpointTests
{
    private static Element TextArea(string value)
    {
        var document = Document.CreateHtml(); var element = document.CreateElement("textarea");
        element.AppendChild(document.CreateTextNode(value)); return element;
    }
    private static HtmlTextAreaState State(Element element) => element.GetHtmlState()!.TextArea!;
    private static (string, bool, HtmlValueChangeOrigin, bool, HtmlTextSelection) Snapshot(HtmlTextAreaState state)
        => (state.GetValue(default), state.DirtyValue, state.LastValueChangeOrigin, state.UserValidity, state.Selection);

    [TestCase("value")]
    [TestCase("default")]
    [TestCase("length")]
    [TestCase("submission")]
    [TestCase("selection")]
    [TestCase("select")]
    public void ColdChildProjectionReadsAndSelectionPollCallerWithoutPublishingFlags(string operation)
    {
        var element = TextArea(new string('x', 10000)); var state = State(element);
        var stamp = element.OwnerDocument!.MutationStamp; using var cancellation = new CancellationTokenSource();
        var counts = new List<int>();
        void Run(Action<int> checkpoint, CancellationToken token)
        {
            if (operation == "value") state.GetValue(checkpoint, token);
            else if (operation == "default") state.GetDefaultValue(checkpoint, token);
            else if (operation == "length") state.GetTextLength(checkpoint, token);
            else if (operation == "submission") state.GetSubmissionValue(checkpoint, token);
            else if (operation == "selection") state.SetSelectionRange(1, 2, null, checkpoint, token);
            else state.Select(checkpoint, token);
        }
        var exception = Assert.Throws<OperationCanceledException>(() => Run(n => { counts.Add(n); if (n >= 256) cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        counts.Should().Equal(2, 256);
        state.DirtyValue.Should().BeFalse(); state.Selection.Should().Be(default(HtmlTextSelection));
        element.OwnerDocument.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => Run(_ => throw new InvalidOperationException("budget"), default));
        state.DirtyValue.Should().BeFalse(); state.Selection.Should().Be(default(HtmlTextSelection));
        counts.Clear(); state.GetValue(counts.Add, default).Length.Should().Be(10000);
        counts.Count.Should().BeGreaterThan(1); // A failed read did not publish a warm API cache.
        element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ChildVisitsCopiesAndNormalizationShareActualCounter()
    {
        var element = TextArea(new string('x', 60)); var state = State(element); var counts = new List<int>();
        state.GetValue(counts.Add, default).Length.Should().Be(60);
        counts.Should().Equal(2, 63, 123); // Allocation boundaries retain actual visits/copies; normalization continues the counter.
        counts.Clear(); state.GetValue(counts.Add, default).Length.Should().Be(60); counts.Should().Equal(1);
        counts.Clear(); state.GetDefaultValue(counts.Add, default).Length.Should().Be(60); counts.Should().Equal(2, 63, 63);
    }

    [TestCase("wrap")]
    [TestCase("cols")]
    public void SubmissionPollsActualWrapWorkAndLongColumnAttribute(string stage)
    {
        var element = TextArea(stage == "wrap" ? new string('x', 10000) : "abc"); var state = State(element);
        state.GetValue(default);
        element.SetAttribute("wrap", "hard"); element.SetAttribute("cols", stage == "cols" ? new string('0', 10000) + "2" : "2");
        var before = Snapshot(state); var stamp = element.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        var exception = Assert.Throws<OperationCanceledException>(() => state.GetSubmissionValue(n => { counts.Add(n); cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token); counts.Should().Equal(256);
        Snapshot(state).Should().Be(before); element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [TestCase("value")]
    [TestCase("reset")]
    [TestCase("splice")]
    public void ScriptWritesAndReplacementCancelBeforeAnyValueFlagOrSelectionCommit(string operation)
    {
        var element = TextArea(new string('a', 1000)); var state = State(element);
        state.SetSelectionRange(1, 2, "backward", default); state.SetUserValidity(true);
        var before = Snapshot(state); var stamp = element.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource(); var counts = new List<int>();
        void Run(Action<int> checkpoint, CancellationToken token)
        {
            if (operation == "value") state.SetValue(new string('b', 1000), checkpoint, token);
            else if (operation == "reset") state.Reset(checkpoint, token);
            else state.SetRangeText(new string('b', 1000), 1, 2, HtmlRangeTextMode.End, checkpoint, token);
        }
        var exception = Assert.Throws<OperationCanceledException>(() => Run(n => { counts.Add(n); if (n >= 256) cancellation.Cancel(); }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        if (operation == "reset") counts.Should().Equal(1, 256);
        else counts.Should().Equal(256);
        Snapshot(state).Should().Be(before); element.OwnerDocument.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => Run(_ => throw new InvalidOperationException("budget"), default));
        Snapshot(state).Should().Be(before); element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ShortSelectionTailCancelsBeforeOffsetChange()
    {
        var element = TextArea("abc"); var state = State(element); var before = Snapshot(state); var stamp = element.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.SetSelectionStart(2, _ => cancellation.Cancel(), cancellation.Token));
        Snapshot(state).Should().Be(before); element.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void CopyTailFailureLeavesTargetAndColdSourceUnchanged()
    {
        var sourceElement = TextArea("abc"); var source = State(sourceElement);
        var targetElement = TextArea("target"); var target = State(targetElement); var before = Snapshot(target);
        var sourceStamp = sourceElement.OwnerDocument!.MutationStamp; var targetStamp = targetElement.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => target.CopyFrom(source, _ => cancellation.Cancel(), cancellation.Token));
        Snapshot(target).Should().Be(before);
        var counts = new List<int>(); source.GetValue(counts.Add, default).Should().Be("abc"); counts.Should().Equal(2, 6, 9);
        sourceElement.OwnerDocument.MutationStamp.Should().Be(sourceStamp); targetElement.OwnerDocument.MutationStamp.Should().Be(targetStamp);
    }

    [Test]
    public void WarmCallbacksRemainAllocationFreeAndStillCheckTheCaller()
    {
        var element = TextArea("abc"); var state = State(element); state.GetValue(default);
        Action<int> checkpoint = static _ => { };
        state.GetValue(checkpoint, default); state.GetSelection(checkpoint, default);
        var stamp = element.OwnerDocument!.MutationStamp; var bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) { state.GetValue(checkpoint, default); state.GetTextLength(checkpoint, default); state.GetEditingSelection(checkpoint, default); }
        (GC.GetAllocatedBytesForCurrentThread() - bytes).Should().Be(0);
        element.OwnerDocument.MutationStamp.Should().Be(stamp);
        Assert.Throws<InvalidOperationException>(() => state.GetValue(_ => throw new InvalidOperationException("budget"), default));
    }
}

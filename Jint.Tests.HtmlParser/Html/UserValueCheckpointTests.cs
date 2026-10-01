#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class UserValueCheckpointTests
{
    private static (Document Document, Element Element, Func<string> Value,
        Func<string, Action<int>, CancellationToken, bool> Apply, Func<bool> Dirty,
        Func<HtmlValueChangeOrigin> Origin, Func<HtmlTextSelection> Selection) Control(string kind,
        string initial, int ancestors = 0)
    {
        var document = Document.CreateHtml();
        Node parent = document;
        for (var i = 0; i < ancestors; i++)
        {
            var container = document.CreateElement("div");
            parent.AppendChild(container);
            parent = container;
        }
        var element = document.CreateElement(kind);
        parent.AppendChild(element);
        var selection = new HtmlTextSelection(1, 2, HtmlSelectionDirection.Backward);
        if (kind == "input")
        {
            element.SetAttribute("value", initial);
            var state = element.GetHtmlState()!.InputValue!;
            state.SetSelectionRange(1, 2, "backward", default);
            state.SetUserValidity(true);
            return (document, element, () => state.GetValue(default),
                (value, checkpoint, token) => state.ApplyUserValue(value, selection, checkpoint, token),
                () => state.DirtyValue, () => state.LastValueChangeOrigin, () => state.Selection);
        }
        var textarea = element.GetHtmlState()!.TextArea!;
        element.AppendChild(document.CreateTextNode(initial));
        textarea.SetSelectionRange(1, 2, "backward", default);
        textarea.SetUserValidity(true);
        return (document, element, () => textarea.GetValue(default),
            (value, checkpoint, token) => textarea.ApplyUserValue(value, selection, checkpoint, token),
            () => textarea.DirtyValue, () => textarea.LastValueChangeOrigin, () => textarea.Selection);
    }

    [TestCase("input")]
    [TestCase("textarea")]
    public void ShortStagesShareCadenceAndFlushTheirTail(string kind)
    {
        var initial = new string('a', 60);
        var control = Control(kind, initial, ancestors: 160);
        var counts = new List<int>();
        control.Apply(new string('a', 59) + "b", counts.Add, default).Should().BeTrue();
        counts.Should().Contain(256);
        counts.Last().Should().BeGreaterThan(256).And.BeLessThan(512);
        counts.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        control.Value().Should().Be(new string('a', 59) + "b");
        control.Dirty().Should().BeTrue();
        control.Origin().Should().Be(HtmlValueChangeOrigin.User);
    }

    [TestCase("input")]
    [TestCase("textarea")]
    public void FinalTailCancellationPrecedesEveryObservableCommit(string kind)
    {
        var control = Control(kind, "abc");
        var stamp = control.Document.MutationStamp;
        var original = control.Selection();
        using var cancellation = new CancellationTokenSource();
        var count = 0;
        var exception = Assert.Throws<OperationCanceledException>(() => control.Apply("xyz", steps =>
        {
            count = steps;
            cancellation.Cancel();
        }, cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        count.Should().BeGreaterThan(0).And.BeLessThan(256);
        control.Value().Should().Be("abc");
        control.Dirty().Should().BeFalse();
        control.Origin().Should().Be(HtmlValueChangeOrigin.NonUser);
        control.Selection().Should().Be(original);
        control.Document.MutationStamp.Should().Be(stamp);
        (kind == "input" ? control.Element.GetHtmlState()!.InputValue!.UserValidity
            : control.Element.GetHtmlState()!.TextArea!.UserValidity).Should().BeTrue();
    }

    [TestCase("input", 300, 3)]
    [TestCase("textarea", 300, 3)]
    [TestCase("input", 0, 2048)]
    [TestCase("textarea", 0, 2048)]
    [TestCase("input", 0, 150)]
    [TestCase("textarea", 0, 150)]
    public void CancellationDuringAncestrySanitizerOrComparisonIsAtomic(string kind, int ancestors, int length)
    {
        var initial = new string('a', length);
        var control = Control(kind, initial, ancestors);
        var stamp = control.Document.MutationStamp;
        var original = control.Selection();
        using var cancellation = new CancellationTokenSource();
        var counts = new List<int>();
        Assert.Throws<OperationCanceledException>(() => control.Apply(new string('a', length - 1) + "b", steps =>
        {
            counts.Add(steps);
            cancellation.Cancel();
        }, cancellation.Token));
        counts.Should().Equal(256);
        control.Value().Should().Be(initial);
        control.Dirty().Should().BeFalse();
        control.Origin().Should().Be(HtmlValueChangeOrigin.NonUser);
        control.Selection().Should().Be(original);
        control.Document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ColdTextareaChildProjectionUsesTheSameCheckpointBeforePublishing()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("textarea");
        var text = document.CreateTextNode(new string('a', 2048));
        element.AppendChild(text);
        var state = element.GetHtmlState()!.TextArea!;
        var stamp = document.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.ApplyUserValue("short", default,
            _ => cancellation.Cancel(), cancellation.Token));
        state.DirtyValue.Should().BeFalse();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        state.Selection.Should().Be(default(HtmlTextSelection));
        document.MutationStamp.Should().Be(stamp);
        text.Data = "after";
        state.GetValue(default).Should().Be("after");
    }

    [TestCase("input")]
    [TestCase("textarea")]
    public void ConstraintCheckpointExceptionIsNotConvertedIntoAnEdit(string kind)
    {
        var control = Control(kind, "abc");
        var failure = new InvalidOperationException("Budget exhausted");
        var stamp = control.Document.MutationStamp;
        Assert.Throws<InvalidOperationException>(() => control.Apply("different", _ => throw failure, default))
            .Should().BeSameAs(failure);
        control.Value().Should().Be("abc");
        control.Dirty().Should().BeFalse();
        control.Document.MutationStamp.Should().Be(stamp);
    }
}

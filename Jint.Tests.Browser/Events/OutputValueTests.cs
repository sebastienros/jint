using Jint.Browser.Events;
using Jint.Browser.Accessibility;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Events;

public sealed class OutputValueTests
{
    [Test]
    public void ResetRestoresTheSavedDefaultAndReturnsToLiveDescendantText()
    {
        var document = ContentDom.Parse("<output id=o>initial<b> text</b></output>");
        var output = ContentDom.ElementById(document, "o")!;
        BrowserOutputValue.GetDefaultValue(output, null, default).Should().Be("initial text");
        BrowserOutputValue.SetValue(output, "current", null, default);
        output.ReplaceChildren(document.CreateTextNode("external"));
        BrowserOutputValue.GetDefaultValue(output, null, default).Should().Be("initial text");
        BrowserOutputValue.SetDefaultValue(output, "new default", null, default);
        BrowserOutputValue.GetValue(output, null, default).Should().Be("external");
        BrowserOutputValue.Reset(output, null, default);
        BrowserOutputValue.GetValue(output, null, default).Should().Be("new default");
        output.ReplaceChildren(document.CreateTextNode("live again"));
        BrowserOutputValue.GetDefaultValue(output, null, default).Should().Be("live again");
    }

    [Test]
    public void EmptySavedDefaultSurvivesRepeatedValueAssignments()
    {
        var document = ContentDom.Parse("<output id=o></output>");
        var output = ContentDom.ElementById(document, "o")!;
        BrowserOutputValue.SetValue(output, "first", null, default);
        BrowserOutputValue.SetValue(output, "second", null, default);
        BrowserOutputValue.GetDefaultValue(output, null, default).Should().BeEmpty();
        BrowserOutputValue.Reset(output, null, default);
        output.ChildCount.Should().Be(0);
    }

    [Test]
    public void CloneStartsWithItsOwnLiveDefaultWhileAdoptionPreservesTheSavedDefault()
    {
        var document = ContentDom.Parse("<output id=o>initial</output>");
        var output = ContentDom.ElementById(document, "o")!;
        BrowserOutputValue.SetValue(output, "current", null, default);
        var clone = (Element) output.CloneNode(deep: true);
        BrowserOutputValue.GetDefaultValue(clone, null, default).Should().Be("current");
        var destination = ContentDom.Parse("<main></main>");
        destination.AdoptNode(output);
        BrowserOutputValue.GetDefaultValue(output, null, default).Should().Be("initial");
        BrowserOutputValue.Reset(output, null, default);
        BrowserOutputValue.GetValue(output, null, default).Should().Be("initial");
    }

    [Test]
    public void CanceledAssignmentPreservesBothValueAndDefault()
    {
        var document = ContentDom.Parse("<output id=o>initial</output>");
        var output = ContentDom.ElementById(document, "o")!;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action assign = () => BrowserOutputValue.SetValue(output, "changed", null, cancellation.Token);
        assign.Should().Throw<OperationCanceledException>();
        BrowserOutputValue.GetValue(output, null, default).Should().Be("initial");
        BrowserOutputValue.SetDefaultValue(output, "still live", null, default);
        BrowserOutputValue.GetValue(output, null, default).Should().Be("still live");
    }

    [Test]
    public void CachedDefaultReadObservesCancellationRequestedByItsCheckpoint()
    {
        var document = ContentDom.Parse("<output id=o>initial</output>");
        var output = ContentDom.ElementById(document, "o")!;
        BrowserOutputValue.SetValue(output, "current", null, default);
        using var cancellation = new CancellationTokenSource();
        Action read = () => BrowserOutputValue.GetDefaultValue(output, _ => cancellation.Cancel(), cancellation.Token);
        read.Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void DescendantTextReadObservesCancellationAtTheFinalMaterializationCheckpoint()
    {
        var document = ContentDom.Parse("<output id=o></output>");
        var output = ContentDom.ElementById(document, "o")!;
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        Action read = () => BrowserOutputValue.GetValue(output, _ =>
        {
            if (++checkpoints == 3) cancellation.Cancel();
        }, cancellation.Token);
        read.Should().Throw<OperationCanceledException>();
    }

    [Test]
    public void CancellationAtThePostCommitCheckpointPreservesTheCoherentMutation()
    {
        var document = ContentDom.Parse("<output id=o>initial</output>");
        var output = ContentDom.ElementById(document, "o")!;
        using var cancellation = new CancellationTokenSource();
        Action assign = () => BrowserOutputValue.SetValue(output, "committed", _ =>
        {
            if (output.FirstChild is Text { Data: "committed" }) cancellation.Cancel();
        }, cancellation.Token);
        assign.Should().Throw<OperationCanceledException>();
        BrowserOutputValue.GetValue(output, null, default).Should().Be("committed");
        BrowserOutputValue.GetDefaultValue(output, null, default).Should().Be("initial");
    }

    [Test]
    public void DescendantTextReadsCheckLongStringsAndExcludeTemplateContents()
    {
        var document = ContentDom.Parse("<output id=o><template>inert</template><span></span></output>");
        var output = ContentDom.ElementById(document, "o")!;
        output.AppendChild(document.CreateTextNode(new string('x', 1024)));
        var checkpoints = 0;
        BrowserOutputValue.GetValue(output, _ => checkpoints++, default).Should().Be(new string('x', 1024));
        checkpoints.Should().BeGreaterThanOrEqualTo(4);
    }
}

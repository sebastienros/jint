#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class SelectedContentTests
{
    private static (Document Document, Element Select, Element Content, Element First, Element Second) Create()
    {
        var document = Document.CreateHtml(); var html = document.CreateElement("html"); document.AppendChild(html);
        var select = document.CreateElement("select"); html.AppendChild(select);
        var button = document.CreateElement("button"); select.AppendChild(button);
        var content = document.CreateElement("selectedcontent"); button.AppendChild(content);
        var first = document.CreateElement("option"); first.AppendChild(document.CreateTextNode("first")); select.AppendChild(first);
        var second = document.CreateElement("option"); second.AppendChild(document.CreateTextNode("second")); select.AppendChild(second);
        return (document, select, content, first, second);
    }
    [Test]
    public void ValueAndIndexCloneSelectedChildrenAndNoMatchClearsContent()
    {
        var (_, select, content, first, second) = Create();
        var state = select.GetHtmlState()!.Select!;
        state.SetSelectedIndex(1, default);
        ((Text) content.FirstChild!).Data.Should().Be("second");
        content.FirstChild.Should().NotBeSameAs(second.FirstChild);
        state.SetValue("first", default);
        ((Text) content.FirstChild!).Data.Should().Be("first");
        first.GetHtmlState()!.Option!.SetLabel("a different label");
        state.SetValue("first", default);
        ((Text) content.FirstChild!).Data.Should().Be("first");
        state.SetValue("missing", default); content.ChildCount.Should().Be(0);
    }
    [Test]
    public void ParserCompletionClonesAfterOptionContentsAreComplete()
    {
        var (document, select, content, first, _) = Create();
        first.AppendParsedChild(document.CreateTextNode(" complete"));
        first.GetHtmlState()!.Option!.ParserFinished(default);
        content.ChildCount.Should().Be(2);
        ((Text) content.LastChild!).Data.Should().Be(" complete");
        select.GetHtmlState()!.Select!.GetSelectedIndex(default).Should().Be(0);
    }
    [Test]
    public void PostConnectionUpdatesPrimaryClearsOthersAndRemovalPromotesNext()
    {
        var (document, select, content, _, _) = Create();
        var other = document.CreateElement("selectedcontent"); other.AppendChild(document.CreateTextNode("stale"));
        select.AppendChild(other);
        other.ChildCount.Should().Be(0);
        ((Text) content.FirstChild!).Data.Should().Be("first");
        content.ParentNode!.RemoveChild(content);
        ((Text) other.FirstChild!).Data.Should().Be("first");
    }
    [Test]
    public void NestedOptionOrSelectedContentAndSecondAncestorSelectDisableConnection()
    {
        var (document, select, content, _, _) = Create();
        var nested = document.CreateElement("selectedcontent"); content.AppendChild(nested);
        nested.GetHtmlState()!.SelectedContentDisabled.Should().BeTrue();
        var outer = document.CreateElement("select");
        var option = document.CreateElement("option"); outer.AppendChild(option);
        option.AppendChild(select);
        document.FirstChild!.AppendChild(outer);
        content.GetHtmlState()!.SelectedContentDisabled.Should().BeTrue();
        select.GetHtmlState()!.Select!.SetValue("second", default);
        content.ChildCount.Should().BeGreaterThan(0); // Disabled target retains its existing clone.
    }
    [Test]
    public void UserChangesReportActualSelectionAndNotificationCompletesNativeState()
    {
        var (_, select, content, first, second) = Create(); var state = select.GetHtmlState()!.Select!;
        state.ApplyUserSelection(second, true, default).Should().BeTrue();
        state.UserValidity.Should().BeFalse();
        state.ApplyUserSelection(second, true, default).Should().BeFalse();
        state.CompleteUserSelection(default);
        state.UserValidity.Should().BeTrue(); state.FallbackButtonText.Should().Be("second");
        ((Text) content.FirstChild!).Data.Should().Be("second");
        select.SetAttribute("disabled", ""); state.ApplyUserSelection(first, true, default).Should().BeFalse();
        state.SetValue("first", default); state.GetValue(default).Should().Be("first");
    }
    [Test]
    public void CancelledSelectedContentClonePreparationLeavesSelectionAndDomUntouched()
    {
        var (document, select, content, first, second) = Create();
        for (var i = 0; i < 1000; i++) second.AppendChild(document.CreateElement("span"));
        var state = select.GetHtmlState()!.Select!; state.SetValue("first", default);
        var oldContent = content.FirstChild; var stamp = document.MutationStamp;
        using var cts = new CancellationTokenSource();
        document.SelectWorkProbe = new HtmlSelectWorkProbe { Checkpoint = units => { if (units == 256) cts.Cancel(); } };
        Assert.Throws<OperationCanceledException>(() => state.SetSelectedIndex(1, cts.Token));
        first.GetHtmlState()!.Option!.Selected.Should().BeTrue(); second.GetHtmlState()!.Option!.Selected.Should().BeFalse();
        content.FirstChild.Should().BeSameAs(oldContent); document.MutationStamp.Should().Be(stamp);
    }
    [Test]
    public void FallbackButtonUsesSemanticLabelWhileIdlLabelKeepsExplicitEmptyValue()
    {
        var (_, select, _, _, second) = Create();
        var option = second.GetHtmlState()!.Option!;
        option.SetLabel(""); option.GetLabel(default).Should().BeEmpty(); option.GetSemanticLabel(default).Should().Be("second");
        var state = select.GetHtmlState()!.Select!;
        state.ApplyUserSelection(second, true, default).Should().BeTrue(); state.CompleteUserSelection(default);
        state.FallbackButtonText.Should().Be("second");
    }

}

#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class TextAreaStateTests
{
    private static (Document Document, Element Element, HtmlTextAreaState State) Create()
    {
        var document = Document.CreateHtml();
        var element = document.CreateElement("textarea");
        return (document, element, element.GetHtmlState()!.TextArea!);
    }

    [Test]
    public void ViewRequiresExactHtmlNameAndHasStableIdentity()
    {
        var html = Document.CreateHtml();
        var textarea = html.CreateElement("textarea");
        textarea.GetHtmlState()!.TextArea.Should().BeSameAs(textarea.GetHtmlState()!.TextArea);
        html.CreateElement("input").GetHtmlState()!.TextArea.Should().BeNull();
        var xml = Document.CreateXml();
        xml.CreateElementNS(Namespaces.Html, "textarea").GetHtmlState()!.TextArea.Should().NotBeNull();
        xml.CreateElementNS(Namespaces.Html, "TEXTAREA").GetHtmlState()!.TextArea.Should().BeNull();
        xml.CreateElement("textarea").GetHtmlState().Should().BeNull();
    }

    [Test]
    public void RawDefaultAndApiValuesRemainDistinctAcrossChildKinds()
    {
        var (document, element, state) = Create();
        var first = document.CreateTextNode("A\r");
        var second = document.CreateTextNode("\nB");
        element.AppendChild(first);
        element.AppendChild(document.CreateComment("hidden"));
        element.AppendChild(second);
        var nested = document.CreateElement("span");
        nested.AppendChild(document.CreateTextNode("not direct"));
        element.AppendChild(nested);
        state.GetDefaultValue(default).Should().Be("A\r\nB");
        state.GetValue(default).Should().Be("A\nB");
        state.GetTextLength(default).Should().Be(3);

        state.SetValue("X\r\nY", default);
        state.GetValue(default).Should().Be("X\nY");
        state.GetDefaultValue(default).Should().Be("A\r\nB");
        first.Data = "changed";
        state.GetValue(default).Should().Be("X\nY");
        state.GetDefaultValue(default).Should().Be("changed\nB");
    }

    [Test]
    public void XmlHtmlTextareaIncludesDirectCDataAndProcessingInstructionIsExcluded()
    {
        var document = Document.CreateXml();
        var element = document.CreateElementNS(Namespaces.Html, "textarea");
        var state = element.GetHtmlState()!.TextArea!;
        element.AppendChild(document.CreateTextNode("A\r"));
        element.AppendChild(document.CreateCDataSection("\nB"));
        var instruction = document.CreateProcessingInstruction("target", "ignored");
        element.AppendChild(instruction);
        state.GetDefaultValue(default).Should().Be("A\r\nB");
        state.GetValue(default).Should().Be("A\nB");
        instruction.Data = "changed";
        state.GetDefaultValue(default).Should().Be("A\r\nB");
    }

    [Test]
    public void CleanMutationsUpdateValueAndDirtyMutationsOnlyUpdateDefault()
    {
        var (document, element, state) = Create();
        var text = document.CreateTextNode("abc");
        element.AppendChild(text);
        state.SetSelectionRange(3, 3, "backward", default);
        text.Data = "a";
        state.GetValue(default).Should().Be("a");
        state.Selection.Should().Be(new HtmlTextSelection(1, 1, HtmlSelectionDirection.Backward));
        element.AppendChild(document.CreateTextNode("bc"));
        state.GetValue(default).Should().Be("abc");
        state.Selection.Start.Should().Be(1);
        state.SetValue("user independent", default);
        element.ReplaceChildren(document.CreateTextNode("new default"));
        state.GetValue(default).Should().Be("user independent");
        state.GetDefaultValue(default).Should().Be("new default");
        state.Reset(default);
        state.GetValue(default).Should().Be("new default");
        state.DirtyValue.Should().BeFalse();
    }

    [Test]
    public void ReplaceAllRemovalsClampBeforeReplacementInsertionAcrossCrLfBoundaries()
    {
        var (document, element, state) = Create();
        element.AppendChild(document.CreateTextNode("AB\r"));
        element.AppendChild(document.CreateTextNode("\nCD"));
        state.GetValue(default).Should().Be("AB\nCD");
        state.SetSelectionRange(4, 5, "forward", default);
        element.ReplaceChildren(document.CreateTextNode("longer replacement"));
        state.GetValue(default).Should().Be("longer replacement");
        state.Selection.Should().Be(new HtmlTextSelection(0, 0, HtmlSelectionDirection.Forward));
    }

    [Test]
    public void FragmentInsertionAndCommentReplacementUseChildrenChangedSemantics()
    {
        var (document, element, state) = Create();
        var fragment = document.CreateDocumentFragment();
        fragment.AppendChild(document.CreateTextNode("A\r"));
        fragment.AppendChild(document.CreateTextNode("\nB"));
        element.AppendChild(fragment);
        state.GetValue(default).Should().Be("A\nB");
        var comment = document.CreateComment("ignored");
        element.AppendChild(comment);
        comment.Data = "also ignored";
        state.GetValue(default).Should().Be("A\nB");
        element.RemoveChild(element.FirstChild!);
        state.GetValue(default).Should().Be("\nB");
    }

    [Test]
    public void CloneCopiesRawDirtyBeforeDescendantsAndLeavesOtherStateFresh()
    {
        var (document, element, state) = Create();
        element.AppendChild(document.CreateTextNode("old"));
        state.SetValue("raw\r\nvalue", default);
        state.SetSelectionRange(2, 4, "forward", default);
        state.SetUserValidity(true);
        var clone = (Element) element.CloneNode(true);
        var cloned = clone.GetHtmlState()!.TextArea!;
        cloned.GetValue(default).Should().Be("raw\nvalue");
        cloned.GetDefaultValue(default).Should().Be("old");
        cloned.DirtyValue.Should().BeTrue();
        cloned.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        cloned.Selection.Should().Be(new HtmlTextSelection(0, 0, HtmlSelectionDirection.None));
        cloned.UserValidity.Should().BeFalse();

        state.Reset(default);
        var shallow = (Element) element.CloneNode();
        var shallowState = shallow.GetHtmlState()!.TextArea!;
        shallowState.GetValue(default).Should().Be("old");
        shallowState.GetDefaultValue(default).Should().BeEmpty();
        shallow.AppendChild(document.CreateTextNode("next"));
        shallowState.GetValue(default).Should().Be("next");
    }

    [Test]
    public void CleanShallowCloneClampsWhenFirstAppendReplacesCopiedRawValue()
    {
        var (document, element, state) = Create();
        element.AppendChild(document.CreateTextNode("abcdef"));
        var shallow = (Element) element.CloneNode();
        var cloned = shallow.GetHtmlState()!.TextArea!;
        cloned.GetValue(default).Should().Be("abcdef");
        cloned.SetSelectionRange(4, 5, "backward", default);

        shallow.AppendChild(document.CreateComment("ignored"));
        cloned.GetValue(default).Should().BeEmpty();
        cloned.Selection.Should().Be(new HtmlTextSelection(0, 0, HtmlSelectionDirection.Backward));
    }

    [Test]
    public void ImportDoesNotInvalidateDestinationAndAdoptionKeepsStateIdentity()
    {
        var (source, element, state) = Create();
        element.AppendChild(source.CreateTextNode("default"));
        state.SetValue("current", default);
        var destination = Document.CreateHtml();
        var sourceStamp = source.MutationStamp;
        var destinationStamp = destination.MutationStamp;
        var copy = (Element) destination.ImportNode(element, true);
        source.MutationStamp.Should().Be(sourceStamp);
        destination.MutationStamp.Should().Be(destinationStamp);
        copy.GetHtmlState()!.TextArea!.GetValue(default).Should().Be("current");
        copy.GetHtmlState()!.TextArea!.GetDefaultValue(default).Should().Be("default");

        destination.AdoptNode(element);
        element.GetHtmlState()!.TextArea.Should().BeSameAs(state);
        state.GetValue(default).Should().Be("current");
        element.OwnerDocument.Should().BeSameAs(destination);
        element.FirstChild!.OwnerDocument.Should().BeSameAs(destination);
    }

    [Test]
    public void ParsedAppendInvalidatesLazilyAndUnrelatedChangesReuseApiValue()
    {
        var (document, element, state) = Create();
        var text = document.CreateTextNode("");
        element.AppendParsedChild(text);
        for (var i = 0; i < 256; i++)
        {
            text.AppendParsedData("ab".AsSpan(), default);
        }

        var api = state.GetValue(default);
        api.Should().Be(string.Concat(Enumerable.Repeat("ab", 256)));
        document.CreateElement("div").SetAttribute("id", "other");
        state.GetValue(default).Should().BeSameAs(api);
        text.AppendParsedData("\r\nZ".AsSpan(), default);
        state.GetValue(default).Should().Be(api + "\nZ");
    }

    [Test]
    public void ChildCollectionAndValuePreparationCancelBeforeStatePublication()
    {
        var (document, element, state) = Create();
        var text = document.CreateTextNode("");
        element.AppendParsedChild(text);
        text.AppendParsedData(new string('a', 1024).AsSpan(), default);
        using var collectCancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => HtmlTextAreaMutations.CollectChildText(element,
            _ => collectCancellation.Cancel(), collectCancellation.Token));
        state.DirtyValue.Should().BeFalse();
        state.GetValue(default).Should().Be(new string('a', 1024));

        using var setCancellation = new CancellationTokenSource();
        var stamp = document.MutationStamp;
        Assert.Throws<OperationCanceledException>(() => state.SetValue(new string('b', 1024),
            _ => setCancellation.Cancel(), setCancellation.Token));
        state.DirtyValue.Should().BeFalse();
        state.GetValue(default).Should().Be(new string('a', 1024));
        state.Selection.Should().Be(new HtmlTextSelection(0, 0, HtmlSelectionDirection.None));
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void SelectionUsesUnsignedUtf16OffsetsAndExactDirections()
    {
        var (_, _, state) = Create();
        state.SetValue("A\ud83d\ude00B", default);
        state.SetSelectionRange(2, uint.MaxValue, "forward", default);
        state.Selection.Should().Be(new HtmlTextSelection(2, 4, HtmlSelectionDirection.Forward));
        state.SetSelectionRange(3, 1, "backward", default);
        state.Selection.Should().Be(new HtmlTextSelection(1, 1, HtmlSelectionDirection.Backward));
        state.SetSelectionStart(3, default);
        state.Selection.Should().Be(new HtmlTextSelection(3, 3, HtmlSelectionDirection.Backward));
        state.SetSelectionEnd(2, default);
        state.Selection.Should().Be(new HtmlTextSelection(2, 2, HtmlSelectionDirection.Backward));
        state.SetSelectionDirection("FORWARD", default);
        state.Selection.Direction.Should().Be(HtmlSelectionDirection.None);
        state.Select(default);
        state.Selection.Should().Be(new HtmlTextSelection(0, 4, HtmlSelectionDirection.None));
    }

    [Test]
    public void ScriptValueShrinkClampsBeforeApplyingEndSelection()
    {
        var (_, _, state) = Create();
        state.SetValue("abcdef", default);
        state.SetSelectionRange(4, 5, "backward", default);
        state.SetValue("", default);
        state.Selection.Should().Be(new HtmlTextSelection(0, 0, HtmlSelectionDirection.None));
    }

    [Test]
    public void RangeReplacementPreservesEachEndpointIncludingRemovedEnd()
    {
        var (_, _, state) = Create();
        state.SetValue("abcdef", default);
        state.SetSelectionRange(4, 4, "backward", default);
        state.SetRangeText("X", 1, 4, HtmlRangeTextMode.Preserve, default);
        state.GetValue(default).Should().Be("aXef");
        state.Selection.Should().Be(new HtmlTextSelection(1, 2, HtmlSelectionDirection.None));
        state.SetRangeText("\r\n", 1, 2, HtmlRangeTextMode.Select, default);
        state.GetValue(default).Should().Be("a\nef");
        state.Selection.Should().Be(new HtmlTextSelection(1, 3, HtmlSelectionDirection.None));
        state.SetRangeText("Q", 1, 3, HtmlRangeTextMode.End, default);
        state.Selection.Should().Be(new HtmlTextSelection(2, 2, HtmlSelectionDirection.None));
        state.SetRangeText("R", 1, 2, HtmlRangeTextMode.Start, default);
        state.Selection.Should().Be(new HtmlTextSelection(1, 1, HtmlSelectionDirection.None));
    }

    [Test]
    public void RangeErrorDirtiesOnlyAndCanceledPreparationKeepsState()
    {
        var (_, element, state) = Create();
        state.SetDefaultValue("default", default);
        state.SetSelectionRange(2, 3, "forward", default);
        state.SetUserValidity(true);
        Assert.That(Assert.Throws<DomException>(() => state.SetRangeText("x", 4, 2,
            HtmlRangeTextMode.Preserve, default))!.Name, Is.EqualTo("IndexSizeError"));
        state.DirtyValue.Should().BeTrue();
        state.GetValue(default).Should().Be("default");
        state.Selection.Should().Be(new HtmlTextSelection(2, 3, HtmlSelectionDirection.Forward));
        state.UserValidity.Should().BeTrue();
        element.ReplaceChildren(element.OwnerDocument!.CreateTextNode("other"));
        state.GetValue(default).Should().Be("default");

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => state.SetValue("new", canceled.Token));
        Assert.Throws<OperationCanceledException>(() => state.SetRangeText("new", 0, 1,
            HtmlRangeTextMode.Select, canceled.Token));
        state.GetValue(default).Should().Be("default");
        state.Selection.Should().Be(new HtmlTextSelection(2, 3, HtmlSelectionDirection.Forward));
    }

    [Test]
    public void InvalidRangeFreezesUnreadCleanRawValueBeforeDirtiness()
    {
        var (document, element, state) = Create();
        var child = document.CreateTextNode("before");
        element.AppendChild(child);
        Assert.That(Assert.Throws<DomException>(() => state.SetRangeText("", 2, 1,
            HtmlRangeTextMode.Preserve, default))!.Name, Is.EqualTo("IndexSizeError"));
        state.DirtyValue.Should().BeTrue();
        child.Data = "after";
        state.GetDefaultValue(default).Should().Be("after");
        state.GetValue(default).Should().Be("before");
    }

    [Test]
    public void EqualScriptWriteChangesOriginWithoutMovingSelection()
    {
        var (_, _, state) = Create();
        state.ApplyUserValue("ab", new HtmlTextSelection(1, 1, HtmlSelectionDirection.Backward), default)
            .Should().BeTrue();
        state.SetUserValidity(true);
        state.SetValue("ab", default);
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        state.Selection.Should().Be(new HtmlTextSelection(1, 1, HtmlSelectionDirection.Backward));
        state.UserValidity.Should().BeTrue();
        state.ApplyUserValue("abc", new HtmlTextSelection(2, 2, HtmlSelectionDirection.Forward), default)
            .Should().BeTrue();
        state.SetRangeText("X", 0, 1, HtmlRangeTextMode.Preserve, default);
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        state.Reset(default);
        state.UserValidity.Should().BeFalse();
        state.DirtyValue.Should().BeFalse();
    }

    [Test]
    public void UserEditingRespectsCurrentMutabilityAndDefaultDoesNotEraseOrigin()
    {
        var (document, element, state) = Create();
        element.SetAttribute("maxlength", "1");
        state.ApplyUserValue("ab", new HtmlTextSelection(2, 2, HtmlSelectionDirection.None), default)
            .Should().BeTrue();
        state.DirtyValue.Should().BeTrue();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.SetDefaultValue("different", default);
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.GetValue(default).Should().Be("ab");
        state.GetSelection(default).Start.Should().Be(2);
        state.SetEditingSelection(0, 1, "forward", default).Should().BeTrue();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        element.SetAttributeNS(null, "readonly", "");
        state.ApplyUserValue("blocked", new HtmlTextSelection(0, 0, HtmlSelectionDirection.None), default)
            .Should().BeFalse();
        state.SetValue("script", default);
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        element.RemoveAttribute("readonly");
        element.SetAttribute("disabled", "");
        state.ApplyUserValue("blocked", new HtmlTextSelection(0, 0, HtmlSelectionDirection.None), default)
            .Should().BeFalse();
        state.SetSelectionRange(0, 1, null, default);
        document.MutationStamp.Should().BeGreaterThan(0);
    }

    [Test]
    public void ForeignNamespaceReadonlyDoesNotDisableUserEditing()
    {
        var (_, element, state) = Create();
        element.SetAttributeNS("urn:other", "readonly", "");
        state.ApplyUserValue("allowed", new HtmlTextSelection(7, 7, HtmlSelectionDirection.None), default)
            .Should().BeTrue();
        state.GetValue(default).Should().Be("allowed");
        element.SetAttributeNS(null, "readonly", "");
        state.ApplyUserValue("blocked", new HtmlTextSelection(0, 0, HtmlSelectionDirection.None), default)
            .Should().BeFalse();
        state.GetValue(default).Should().Be("allowed");
    }

    [Test]
    public void SubmissionHardWrapAndApiCacheIgnoreUnrelatedDocumentEdits()
    {
        var (document, element, state) = Create();
        state.SetValue("A\ud83d\ude00BC\r\nDE", default);
        var api = state.GetValue(default);
        state.GetSubmissionValue(default).Should().BeSameAs(api);
        element.SetAttribute("wrap", "hard");
        element.SetAttribute("cols", "2");
        state.GetSubmissionValue(default).Should().Be("A\ud83d\ude00\nBC\nDE");
        document.CreateElement("div").SetAttribute("id", "unrelated");
        state.GetValue(default).Should().BeSameAs(api);
        element.SetAttribute("wrap", "HARD");
        state.GetSubmissionValue(default).Should().Be("A\ud83d\ude00\nBC\nDE");
        element.SetAttribute("wrap", "invalid");
        state.GetSubmissionValue(default).Should().BeSameAs(api);
    }
}

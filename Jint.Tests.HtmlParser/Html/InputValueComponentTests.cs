#nullable enable
using Jint.HtmlParser;

namespace Jint.Tests.HtmlParser.Html;

public class InputValueComponentTests
{
    private static HtmlInputValueState Create(string type = "text", string? value = null, bool multiple = false)
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        var attributes = new List<ParserAttribute> { new(null, "type", null, type) };
        if (value is not null) attributes.Add(new(null, "value", null, value));
        if (multiple) attributes.Add(new(null, "multiple", null, ""));
        input.InitializeParsedAttributes(attributes.ToArray(), default);
        return input.GetHtmlState()!.InputValue!;
    }

    [TestCase("text", " a\r\nb ", " ab ")]
    [TestCase("search", " a\r\nb ", " ab ")]
    [TestCase("tel", " a\r\nb ", " ab ")]
    [TestCase("password", " a\r\nb ", " ab ")]
    [TestCase("url", " \tA\r\nB \t", "AB")]
    [TestCase("email", " \tA\r\nB \t", "AB")]
    public void TextFamiliesInitializeSanitizedDefaultsWithInitialSelection(string type, string raw, string expected)
    {
        var state = Create(type, raw);
        state.GetValue(default).Should().Be(expected);
        state.GetDefaultValue(default).Should().Be(raw);
        state.DirtyValue.Should().BeFalse();
        state.GetEditingSelection(default).Should().Be(default(HtmlTextSelection));
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
    }

    [TestCase("hidden")]
    [TestCase("submit")]
    [TestCase("image")]
    [TestCase("reset")]
    [TestCase("button")]
    public void DefaultFamiliesAreLiteralAttributeValues(string type)
    {
        Create(type).GetValue(default).Should().Be("");
        Create(type, "a\r\nb").GetValue(default).Should().Be("a\r\nb");
    }
    [TestCase("checkbox")]
    [TestCase("radio")]
    public void DefaultOnDistinguishesAbsentAndPresentEmpty(string type)
    {
        Create(type).GetValue(default).Should().Be("on");
        Create(type, "").GetValue(default).Should().Be("");
        Create(type, "a\r\nb").GetValue(default).Should().Be("a\r\nb");
    }

    [Test]
    public void ScriptAssignmentsDirtyEqualValuesButRetainEqualSelection()
    {
        var state = Create("text", "abc");
        state.SetSelectionRange(1, 2, "backward", default);
        state.SetValue("a\rbc", default);
        state.GetValue(default).Should().Be("abc");
        state.DirtyValue.Should().BeTrue();
        state.GetSelection(default).Should().Be(new HtmlTextSelection(1, 2, HtmlSelectionDirection.Backward));
        state.SetValue("new", default);
        state.GetSelection(default).Should().Be(new HtmlTextSelection(3, 3, HtmlSelectionDirection.None));
        state.GetDefaultValue(default).Should().Be("abc");
    }

    [Test]
    public void NullableUnsignedSelectionsAndSurrogateHalfOffsetsFollowUtf16()
    {
        var state = Create("text", "A😀Z");
        state.SetSelectionRange(2, uint.MaxValue, "backward", default);
        state.GetSelection(default).Should().Be(new HtmlTextSelection(2, 4, HtmlSelectionDirection.Backward));
        state.SetSelectionEnd(null, default);
        state.GetSelection(default).Should().Be(new HtmlTextSelection(0, 0, HtmlSelectionDirection.Backward));
        state.SetSelectionStart(uint.MaxValue, default);
        state.GetSelection(default).Should().Be(new HtmlTextSelection(4, 4, HtmlSelectionDirection.Backward));
        state.SetSelectionDirection("FORWARD", default);
        state.GetSelection(default)!.Value.Direction.Should().Be(HtmlSelectionDirection.None);
        state.SetSelectionRange(3, 1, "forward", default);
        state.GetSelection(default).Should().Be(new HtmlTextSelection(1, 1, HtmlSelectionDirection.Forward));
    }

    [Test]
    public void EmailUsesTheSameInternalSelectionButRejectsPublicMutators()
    {
        var state = Create("email", " a , \r\nb ", multiple: true);
        state.GetValue(default).Should().Be("a,b");
        state.GetSelection(default).Should().BeNull();
        Assert.Throws<DomException>(() => state.SetSelectionRange(0, 2, null, default))!.Name.Should().Be("InvalidStateError");
        state.SetEditingSelection(1, 2, "backward", default).Should().BeTrue();
        state.GetEditingSelection(default).Should().Be(new HtmlTextSelection(1, 2, HtmlSelectionDirection.Backward));
        state.DirtyValue.Should().BeFalse();
        state.ApplyUserValue(" a@b , c@d ", new HtmlTextSelection(100, 100, HtmlSelectionDirection.None), default).Should().BeTrue();
        state.GetValue(default).Should().Be("a@b,c@d");
        state.GetEditingSelection(default)!.Value.End.Should().Be(7);
        state.DirtyValue.Should().BeTrue();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.UserValidity.Should().BeFalse();
    }

    [TestCase((int) HtmlRangeTextMode.Select, 1u, 2u)]
    [TestCase((int) HtmlRangeTextMode.Start, 1u, 1u)]
    [TestCase((int) HtmlRangeTextMode.End, 2u, 2u)]
    [TestCase((int) HtmlRangeTextMode.Preserve, 1u, 2u)]
    public void RangeReplacementPreservesOrderedEndpointBoundaryRules(int mode, uint start, uint end)
    {
        var state = Create("text", "abcdef");
        state.SetSelectionRange(4, 4, "backward", default);
        state.SetRangeText("X", 1, 4, (HtmlRangeTextMode) mode, default);
        state.GetValue(default).Should().Be("aXef");
        state.GetSelection(default).Should().Be(new HtmlTextSelection(start, end, HtmlSelectionDirection.None));
        state.DirtyValue.Should().BeTrue();
    }

    [Test]
    public void RangeErrorDirtiesBeforeThrowingWithoutChangingOtherState()
    {
        var state = Create("text", "abc");
        state.SetUserValidity(true);
        state.SetSelectionRange(1, 2, "backward", default);
        Assert.Throws<DomException>(() => state.SetRangeText("X", 2, 1, HtmlRangeTextMode.Preserve, default))!.Name.Should().Be("IndexSizeError");
        state.DirtyValue.Should().BeTrue();
        state.GetValue(default).Should().Be("abc");
        state.GetSelection(default).Should().Be(new HtmlTextSelection(1, 2, HtmlSelectionDirection.Backward));
        state.UserValidity.Should().BeTrue();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        var email = Create("email", "abc");
        Assert.Throws<DomException>(() => email.SetRangeText("X", 2, 1, HtmlRangeTextMode.Preserve, default))!.Name.Should().Be("InvalidStateError");
        email.DirtyValue.Should().BeFalse();
    }

    [Test]
    public void RangeReplacementUsesSuppliedReplacementLengthBeforeSanitizedClamping()
    {
        var state = Create("text", "abcdef");
        state.SetSelectionRange(6, 6, null, default);
        state.SetRangeText("X\r\n", 1, 4, HtmlRangeTextMode.End, default);
        state.GetValue(default).Should().Be("aXef");
        state.GetSelection(default).Should().Be(new HtmlTextSelection(4, 4, HtmlSelectionDirection.None));
    }

    [Test]
    public void UnsupportedFamiliesAreNamedAndNeverReturnTextFallbacks()
    {
        foreach (var type in new[] { "color", "file" })
        {
            var state = Create(type, "sentinel");
            state.IsAvailable.Should().BeFalse();
            Assert.Throws<NotSupportedException>(() => state.GetValue(default))!.Message.Should().Contain(type);
            Assert.Throws<NotSupportedException>(() => state.SetValue("", default));
            state.GetDefaultValue(default).Should().Be("sentinel");
            state.GetEditingSelection(default).Should().BeNull();
            state.GetFacts(default).TextLength.Should().BeNull();
        }
    }

    [Test]
    public void ValueResetOnlyResetsThisComponentAndUsesOrdinarySelectionClamp()
    {
        var state = Create("text", "abc");
        state.ApplyUserValue("abcdef", new HtmlTextSelection(2, 5, HtmlSelectionDirection.Backward), default);
        state.SetUserValidity(true);
        state.ResetValue(default);
        state.GetValue(default).Should().Be("abc");
        state.GetSelection(default).Should().Be(new HtmlTextSelection(2, 3, HtmlSelectionDirection.Backward));
        state.DirtyValue.Should().BeFalse();
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        state.UserValidity.Should().BeFalse();
    }

    [Test]
    public void ReadonlyAndDisabledRejectUserEditsButAllowScriptSelectionAndValue()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.SetAttribute("readonly", "");
        input.SetAttribute("disabled", "");
        input.SetAttribute("value", "abc");
        var state = input.GetHtmlState()!.InputValue!;
        state.ApplyUserValue("different", default, default).Should().BeFalse();
        state.GetValue(default).Should().Be("abc");
        state.SetSelectionRange(1, 2, "backward", default);
        state.SetValue("changed", default);
        state.GetValue(default).Should().Be("changed");
        state.SetEditingSelection(1, 3, "forward", default).Should().BeTrue();
        state.GetEditingSelection(default).Should().Be(new HtmlTextSelection(1, 3, HtmlSelectionDirection.Forward));
    }

    [Test]
    public void CloneComponentCopiesValueAndDirtyButClearsInteractionMetadata()
    {
        var source = Create("text", "default");
        source.ApplyUserValue("current", new HtmlTextSelection(2, 5, HtmlSelectionDirection.Backward), default);
        source.SetUserValidity(true);
        var copy = Create("text", "default");
        copy.CopyFrom(source);
        copy.GetValue(default).Should().Be("current");
        copy.GetDefaultValue(default).Should().Be("default");
        copy.DirtyValue.Should().BeTrue();
        copy.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        copy.UserValidity.Should().BeFalse();
        copy.GetSelection(default).Should().Be(default(HtmlTextSelection));
        source.GetSelection(default).Should().Be(new HtmlTextSelection(2, 5, HtmlSelectionDirection.Backward));
    }

    [Test]
    public void PureInitializationAndHotReadsDoNotMutateOrAllocate()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.SetAttribute("value", "a\r\nb");
        var stamp = document.MutationStamp;
        var state = new HtmlInputValueState(input);
        state.GetValue(default).Should().Be("ab");
        document.MutationStamp.Should().Be(stamp);
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            state.GetValue(default);
            state.GetDefaultValue(default);
            state.GetFacts(default);
            state.GetSelection(default);
        }
        (GC.GetAllocatedBytesForCurrentThread() - bytes).Should().Be(0);
        document.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ExplicitCancellationLeavesValueDirtyOriginSelectionAndValidityUntouched()
    {
        var state = Create("text", "abc");
        state.SetSelectionRange(1, 2, "backward", default);
        state.SetUserValidity(true);
        using var cancellation = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => state.SetValue(new string('x', 10000), _ => cancellation.Cancel(), cancellation.Token));
        state.GetValue(default).Should().Be("abc");
        state.DirtyValue.Should().BeFalse();
        state.UserValidity.Should().BeTrue();
        state.GetSelection(default).Should().Be(new HtmlTextSelection(1, 2, HtmlSelectionDirection.Backward));
    }
    [Test]
    public void AllSupportedTransitionsUseOneStateAndTransferValueModes()
    {
        string[] types = ["text", "search", "tel", "url", "email", "password", "hidden", "submit", "image", "reset", "button", "checkbox", "radio"];
        foreach (var from in types)
        foreach (var to in types)
        foreach (var value in new[] { "", "current" })
        {
            var state = Create(from, "default");
            state.SetValue(value, default);
            var oldMode = state.ValueMode;
            state.Element.SetAttribute("type", to);
            state.Element.GetHtmlState()!.InputValue.Should().BeSameAs(state);
            state.IsAvailable.Should().BeTrue($"{from} -> {to}");
            var expected = oldMode == HtmlInputValueMode.Value && state.ValueMode != HtmlInputValueMode.Value && value.Length == 0 ? "default" : value;
            state.GetValue(default).Should().Be(expected, $"{from} -> {to}");
            state.DirtyValue.Should().Be(oldMode == HtmlInputValueMode.Value, $"{from} -> {to}");
        }
    }

    [Test]
    public void UnsupportedCrossingsDiscardCurrentValueUntilExplicitRecovery()
    {
        var state = Create("text", "default");
        state.SetValue("secret", default);
        state.Element.SetAttribute("type", "color");
        state.IsAvailable.Should().BeFalse();
        state.Element.SetAttribute("type", "text");
        state.IsAvailable.Should().BeFalse();
        Assert.Throws<NotSupportedException>(() => state.GetValue(default));
        state.GetDefaultValue(default).Should().Be("default");
        state.SetValue("new", default);
        state.GetValue(default).Should().Be("new");
        state.Element.SetAttribute("type", "file");
        state.Element.SetAttribute("type", "text");
        state.GetValue(default).Should().Be("default");
        state.DirtyValue.Should().BeFalse();
    }

    [Test]
    public void SameStateTypeChangePreservesUserOriginAndSelection()
    {
        var state = Create("text", "default");
        state.ApplyUserValue("user", new(1, 3, HtmlSelectionDirection.Backward), default);
        state.SetUserValidity(true);
        state.Element.SetAttribute("type", "TeXt");
        state.GetValue(default).Should().Be("user");
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.GetSelection(default).Should().Be(new HtmlTextSelection(1, 3, HtmlSelectionDirection.Backward));
        state.UserValidity.Should().BeTrue();
    }

    [Test]
    public void AttributeRoutesUpdateCleanValueButPreserveDirtyValue()
    {
        var state = Create("text", "default");
        var element = state.Element;
        element.GetAttributeNode("value")!.Value = "attached\r\n";
        state.GetValue(default).Should().Be("attached");
        var replacement = element.OwnerDocument!.CreateAttribute("value");
        replacement.Value = "replacement";
        element.SetAttributeNode(replacement);
        state.GetValue(default).Should().Be("replacement");
        element.RemoveAttributeNode(replacement);
        state.GetValue(default).Should().Be("");
        state.ApplyUserValue("user", default, default);
        state.SetDefaultValue("newdefault", default);
        state.GetValue(default).Should().Be("user");
        state.GetDefaultValue(default).Should().Be("newdefault");
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        element.SetAttributeNS("urn:test", "value", "ignored");
        state.GetDefaultValue(default).Should().Be("newdefault");
    }

    [Test]
    public void TypeRecordPrecedesNestedValueTransferRecord()
    {
        var state = Create("text", "default");
        state.SetValue("current", default);
        using var records = state.Element.OwnerDocument!.ObserveMutations(state.Element,
            new MutationObserverOptions { Attributes = true, AttributeOldValue = true });
        state.Element.SetAttribute("type", "checkbox");
        records.TakeRecords().Select(record => (record.AttributeName, record.OldValue))
            .Should().Equal(("type", "text"), ("value", "default"));
        state.GetValue(default).Should().Be("current");
        state.Element.GetHtmlState()!.CheckedState!.Type.Should().Be(HtmlInputType.Checkbox);
    }

    [Test]
    public void NativeCloneImportAndAdoptionUseOwnedValueState()
    {
        var source = Create("text", "default");
        source.ApplyUserValue("current", new(2, 5, HtmlSelectionDirection.Backward), default);
        source.SetUserValidity(true);
        var document = Document.CreateHtml();
        foreach (var element in new[] { (Element) source.Element.CloneNode(), (Element) document.ImportNode(source.Element) })
        {
            var copy = element.GetHtmlState()!.InputValue!;
            copy.GetValue(default).Should().Be("current");
            copy.GetDefaultValue(default).Should().Be("default");
            copy.DirtyValue.Should().BeTrue();
            copy.UserValidity.Should().BeFalse();
            copy.GetSelection(default).Should().Be(default(HtmlTextSelection));
            copy.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.NonUser);
        }
        document.AdoptNode(source.Element);
        source.Element.GetHtmlState()!.InputValue.Should().BeSameAs(source);
        source.GetSelection(default).Should().Be(new HtmlTextSelection(2, 5, HtmlSelectionDirection.Backward));
        source.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
    }

    [Test]
    public void ColdTransitionsAndOtherElementsDoNotReplayOrAllocateInputState()
    {
        var document = Document.CreateHtml();
        var input = document.CreateElement("input");
        input.SetAttribute("value", " a\r\nb ");
        input.SetAttribute("type", "url");
        input.GetHtmlState()!.InputValue!.GetValue(default).Should().Be("ab");
        var div = document.CreateElement("div");
        div.SetAttribute("value", "x");
        div.HasHtmlState.Should().BeFalse();
        div.GetHtmlState()!.InputValue.Should().BeNull();
        var xml = Document.CreateXml().CreateElement("input");
        xml.SetAttribute("type", "text");
        xml.GetHtmlState().Should().BeNull();
    }

    [Test]
    public void EmailMultipleAndReadonlyAreCachedThroughAllAttributeRoutes()
    {
        var state = Create("email", "a , b");
        state.Element.SetAttribute("multiple", "");
        state.GetValue(default).Should().Be("a,b");
        state.Element.GetAttributeNode("multiple")!.Value = "ignored";
        state.GetValue(default).Should().Be("a,b");
        state.ApplyUserValue(" user , next ", new(0, 4, HtmlSelectionDirection.Forward), default);
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.Element.RemoveAttribute("multiple");
        state.GetValue(default).Should().Be("user,next");
        state.LastValueChangeOrigin.Should().Be(HtmlValueChangeOrigin.User);
        state.Element.SetAttribute("readonly", "");
        state.ReadOnly.Should().BeTrue();
        state.ApplyUserValue("blocked", default, default).Should().BeFalse();
        state.Element.RemoveAttribute("readonly");
        state.ReadOnly.Should().BeFalse();
        state.ApplyUserValue("allowed", default, default).Should().BeTrue();
    }

    [Test]
    public void ParsedAttributePermutationHasOneFinalSanitization()
    {
        foreach (var names in new[] { new[] { "type", "value", "multiple" }, new[] { "multiple", "value", "type" }, new[] { "value", "type", "multiple" } })
        {
            var input = Document.CreateHtml().CreateElement("input");
            input.InitializeParsedAttributes(names.Select(name => new ParserAttribute(null, name, null,
                name == "type" ? "email" : name == "value" ? " a , b " : "")).ToArray(), default);
            var state = input.GetHtmlState()!.InputValue!;
            state.GetValue(default).Should().Be("a,b");
            state.DirtyValue.Should().BeFalse();
            state.GetEditingSelection(default).Should().Be(default(HtmlTextSelection));
        }
    }

    [Test]
    public void ValueResetDoesNotResetCheckednessOrItsDirtyFlag()
    {
        var state = Create("checkbox", "default");
        var checkedState = state.Element.GetHtmlState()!.CheckedState!;
        HtmlCheckednessAlgorithms.Set(state.Element, true, HtmlCheckedChangeOrigin.UserInteraction, default);
        state.SetValue("current", default);
        state.ResetValue(default);
        checkedState.Checked.Should().BeTrue();
        checkedState.DirtyCheckedness.Should().BeTrue();
        state.GetValue(default).Should().Be("current");
    }

    [Test]
    public void ExplicitRecoveryOfUnavailableDefaultModeAdvancesDocumentStamp()
    {
        var state = Create("color");
        state.Element.SetAttribute("type", "hidden");
        state.IsAvailable.Should().BeFalse();
        var stamp = state.Element.OwnerDocument!.MutationStamp;
        state.ResetValue(default);
        state.GetValue(default).Should().Be("");
        state.Element.OwnerDocument.MutationStamp.Should().BeGreaterThan(stamp);
    }

}

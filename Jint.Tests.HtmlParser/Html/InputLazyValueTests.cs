#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Html;
using Jint.HtmlParser.Serialization;

namespace Jint.Tests.HtmlParser.Html;

public class InputLazyValueTests
{
    private static Element Input(string type = "text", string value = " a , b ", bool multiple = false)
    {
        var element = Document.CreateHtml().CreateElement("input");
        var attributes = new List<ParserAttribute> { new(null, "type", null, type), new(null, "value", null, value) };
        if (multiple) attributes.Add(new(null, "multiple", null, ""));
        element.InitializeParsedAttributes(attributes.ToArray(), default);
        return element;
    }

    [Test]
    public void ParsingKeepsAllInputValueFamiliesColdIncludingLongRawStrings()
    {
        var document = Document.CreateHtml();
        var session = new HtmlParserSession(document);
        var raw = new string('0', 10000) + "1";
        session.AppendInput(string.Concat(Enum.GetValues<HtmlInputType>().Select(type =>
            $"<input type='{HtmlInputTypes.Info(type).Keyword}' value='{raw}' data-raw='yes'>")), isFinal: true);
        HtmlParseStep step;
        do { step = session.Drive(100000, default); } while (step.Kind == HtmlParseStepKind.Yielded);
        step.Kind.Should().Be(HtmlParseStepKind.Complete);
        var inputs = SelectorMatcher.QuerySelectorAll(SelectorCompiler.Compile("input[data-raw]", null, default), document);
        inputs.Should().HaveCount(22);
        foreach (var input in inputs)
        {
            input.ExistingInputValueState.Should().BeNull();
            input.GetAttribute("value").Should().Be(raw);
            HtmlMarkupSerializer.Serialize(input).Should().Contain(raw);
            input.ExistingInputValueState.Should().BeNull();
        }
    }

    [TestCase("text", " ab ")]
    [TestCase("url", "ab")]
    [TestCase("email", "ab")]
    [TestCase("number", "")]
    [TestCase("date", "")]
    public void FirstSemanticReadInitializesOnceWithoutAdvancingStamp(string type, string? expected)
    {
        var input = Input(type, " a\r\nb ");
        input.ExistingInputValueState.Should().BeNull();
        var stamp = input.OwnerDocument!.MutationStamp;
        var state = input.GetHtmlState()!.InputValue!;
        input.GetHtmlState()!.InputValue.Should().BeSameAs(state);
        state.DirtyValue.Should().BeFalse();
        state.Selection.Should().Be(default(HtmlTextSelection));
        if (expected is not null) state.GetValue(default).Should().Be(expected);
        else state.IsAvailable.Should().BeFalse();
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ReflectionSameEffectiveTypeAndUnrelatedWritesRemainCold()
    {
        var input = Input();
        input.SetAttribute("data-x", "changed");
        input.SetAttribute("readonly", "");
        input.SetAttribute("type", "TeXt");
        input.GetAttributeNode("value")!.Value = " attached\r\n ";
        var replacement = input.OwnerDocument!.CreateAttribute("value");
        replacement.Value = " replacement\r\n ";
        input.SetAttributeNode(replacement);
        input.RemoveAttributeNode(replacement);
        input.SetAttribute("value", " final\r\n ");
        input.RemoveAttribute("readonly");
        input.SetAttributeNS("urn:test", "type", "email");
        input.ExistingInputValueState.Should().BeNull();
        input.GetHtmlState()!.InputValue!.GetValue(default).Should().Be(" final ");
    }

    [Test]
    public void TypeAndEmailMultipleChangesRetainIrreversibleHistoryBeforeFirstRead()
    {
        var text = Input("text", " a ");
        text.SetAttribute("type", "url");
        text.ExistingInputValueState.Should().NotBeNull();
        text.SetAttribute("type", "text");
        text.GetHtmlState()!.InputValue!.GetValue(default).Should().Be("a");
        text.GetAttribute("value").Should().Be(" a ");
        var email = Input("email", " a , b ");
        email.SetAttribute("multiple", "");
        email.ExistingInputValueState.Should().NotBeNull();
        email.RemoveAttribute("multiple");
        email.GetHtmlState()!.InputValue!.GetValue(default).Should().Be("a,b");
        email.GetAttribute("value").Should().Be(" a , b ");
        var unavailable = Input("color", "42");
        unavailable.SetAttribute("type", "text");
        unavailable.GetHtmlState()!.InputValue!.IsAvailable.Should().BeFalse();
        unavailable.GetHtmlState()!.InputValue!.ResetValue(default);
        unavailable.GetHtmlState()!.InputValue!.GetValue(default).Should().Be("42");
    }

    [TestCase("hidden")]
    [TestCase("checkbox")]
    [TestCase("text")]
    public void FilenameToSupportedModeUsesIndependentDefault(string destination)
    {
        var input = Input("file", "default");
        input.SetAttribute("type", destination);
        var state = input.GetHtmlState()!.InputValue!;
        state.IsAvailable.Should().BeTrue();
        state.GetValue(default).Should().Be("default");
        state.DirtyValue.Should().BeFalse();
    }

    [TestCase("hidden")]
    [TestCase("radio")]
    public void UnknownValueToDefaultRemainsUnavailableUntilExplicitRecovery(string destination)
    {
        var input = Input("color", "42");
        input.SetAttribute("type", destination);
        var state = input.GetHtmlState()!.InputValue!;
        state.IsAvailable.Should().BeFalse();
        state.GetDefaultValue(default).Should().Be("42");
        state.SetValue("known", default);
        state.GetValue(default).Should().Be("known");
    }

    [Test]
    public void ColdCloneImportAndAdoptionDoNotConstructValueState()
    {
        var input = Input("url", " a ");
        var other = Document.CreateHtml();
        var clone = (Element) input.CloneNode();
        var imported = (Element) other.ImportNode(input);
        other.AdoptNode(input);
        input.ExistingInputValueState.Should().BeNull();
        clone.ExistingInputValueState.Should().BeNull();
        imported.ExistingInputValueState.Should().BeNull();
        foreach (var element in new[] { input, clone, imported })
            element.GetHtmlState()!.InputValue!.GetValue(default).Should().Be("a");
    }

    [Test]
    public void ColdAndMaterializedAttributeHistoriesHaveTheSameSemanticResult()
    {
        string[] types = ["text", "url", "email", "hidden", "checkbox", "number", "file"];
        for (var seed = 0; seed < 40; seed++)
        {
            var warm = Input(types[seed % types.Length]);
            var cold = Input(types[seed % types.Length]);
            _ = warm.GetHtmlState()!.InputValue;
            var random = new Random(seed);
            for (var i = 0; i < 40; i++)
            {
                var operation = random.Next(4);
                var type = types[random.Next(types.Length)];
                var value = random.Next(2) == 0 ? " a , b " : "";
                foreach (var element in new[] { warm, cold })
                {
                    switch (operation)
                    {
                        case 0: element.SetAttribute("type", type); break;
                        case 1: element.SetAttribute("value", value); break;
                        case 2:
                            if (value.Length == 0) element.RemoveAttribute("multiple");
                            else element.SetAttribute("multiple", "");
                            break;
                        default: element.SetAttribute("readonly", value); break;
                    }
                }
            }
            var expected = warm.GetHtmlState()!.InputValue!;
            var actual = cold.GetHtmlState()!.InputValue!;
            actual.GetFacts(default).Should().Be(expected.GetFacts(default), $"seed {seed}");
            actual.GetDefaultValue(default).Should().Be(expected.GetDefaultValue(default));
            if (expected.IsAvailable) actual.GetValue(default).Should().Be(expected.GetValue(default));
            actual.Selection.Should().Be(expected.Selection);
            if (HtmlInputValueState.IsSupportedType(actual.Type))
            {
                actual.ResetValue(default);
                expected.ResetValue(default);
                actual.GetValue(default).Should().Be(expected.GetValue(default));
                actual.GetFacts(default).Should().Be(expected.GetFacts(default));
            }
        }
    }
    [Test]
    public void CancelledFirstSemanticAccessDoesNotPublishOrAdvanceStamp()
    {
        var input = Input("email", new string('a', 10000));
        var view = input.GetHtmlState()!;
        var stamp = input.OwnerDocument!.MutationStamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = Assert.Throws<OperationCanceledException>(() => view.GetInputValueState(cancellation.Token));
        exception!.CancellationToken.Should().Be(cancellation.Token);
        view.ExistingInputValue.Should().BeNull();
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
        var state = view.GetInputValueState(default)!;
        view.GetInputValueState(default).Should().BeSameAs(state);
        state.GetValue(default).Should().Be(new string('a', 10000));
        input.OwnerDocument.MutationStamp.Should().Be(stamp);
    }

    [Test]
    public void ColdCloneChargesAttributesWithoutAnInputValueConstructionBoundary()
    {
        var input = Input("text", new string('a', 10000));
        var document = input.OwnerDocument!;
        var probe = new HtmlSelectWorkProbe();
        document.SelectWorkProbe = probe;
        var copy = (Element) NodeCloner.Clone(input, document, false, cancellationToken: default);
        input.ExistingInputValueState.Should().BeNull();
        copy.ExistingInputValueState.Should().BeNull();
        var coldUnits = probe.Units;
        _ = input.GetHtmlState()!.GetInputValueState(default);
        var warmProbe = new HtmlSelectWorkProbe();
        document.SelectWorkProbe = warmProbe;
        var warmCopy = (Element) NodeCloner.Clone(input, document, false, cancellationToken: default);
        warmCopy.ExistingInputValueState.Should().NotBeNull();
        warmProbe.Units.Should().Be(coldUnits + 1);
    }

}

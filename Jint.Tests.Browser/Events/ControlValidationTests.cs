using Jint.Browser.Accessibility;
using Jint.Browser.Dom;
using Jint.Browser.Events;
using Jint.HtmlParser;

namespace Jint.Tests.Browser.Events;

public sealed class ControlValidationTests
{
    [TestCase("<input id=t type=number required>", (int) ControlValidityFlags.ValueMissing)]
    [TestCase("<input id=t type=date required>", (int) ControlValidityFlags.ValueMissing)]
    [TestCase("<input id=t type=number min=3 max=7 step=2 value=8>", (int) (ControlValidityFlags.RangeOverflow | ControlValidityFlags.StepMismatch))]
    [TestCase("<input id=t type=number min=3 step=2 value=1>", (int) ControlValidityFlags.RangeUnderflow)]
    [TestCase("<input id=t type=date min=2024-01-02 step=2 value=2024-01-03>", (int) ControlValidityFlags.StepMismatch)]
    [TestCase("<input id=t type=range required value=invalid>", (int) ControlValidityFlags.None)]
    [TestCase("<input id=t type=number value=3 pattern=x minlength=8 maxlength=0>", (int) ControlValidityFlags.None)]
    public void NumericAndTemporalValidityUsesNativeFactsAndStateApplicability(string markup, int expected)
    {
        var realm = DomRealm.Of(new Engine());
        var input = ContentDom.ElementById(ContentDom.Parse(markup), "t")!;
        BrowserControlValidation.Read(realm, input).Flags.Should().Be((ControlValidityFlags) expected);
    }

    [Test]
    public void LengthConstraintsUseNativeUserEditProvenance()
    {
        var engine = new Engine();
        var realm = DomRealm.Of(engine);
        var input = ContentDom.ElementById(ContentDom.Parse("<input id=t maxlength=2 minlength=2>"), "t")!;
        var state = input.GetHtmlState()!.GetInputValueState(default)!;
        state.SetValue("long", default);
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.None);
        state.ApplyUserValue("user", new HtmlTextSelection(4, 4, HtmlSelectionDirection.None), default);
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.TooLong);
        state.ApplyUserValue("x", new HtmlTextSelection(1, 1, HtmlSelectionDirection.None), default);
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.TooShort);
    }

    [Test]
    public void RequiredRadioGroupIncludesItsDisabledRequiredPeer()
    {
        var realm = DomRealm.Of(new Engine());
        var document = ContentDom.Parse("<form><input type=radio name=g required disabled><input id=t type=radio name=g></form>");
        var input = ContentDom.ElementById(document, "t")!;
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.ValueMissing);
        HtmlCheckednessAlgorithms.Set(input, true, HtmlCheckedChangeOrigin.UserInteraction, default);
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.None);
    }

    [Test]
    public void PatternUsesUnicodeSetsAndBuiltinExecAfterAPrototypeOverride()
    {
        var engine = new Engine();
        engine.Execute("RegExp.prototype.exec = () => { throw new Error('overridden'); }");
        var realm = DomRealm.Of(engine);
        var input = ContentDom.ElementById(ContentDom.Parse("<input id=t pattern='[a-z&&[^q]]+' value=abc>"), "t")!;
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.None);
        input.GetHtmlState()!.GetInputValueState(default)!.SetValue("q", default);
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.PatternMismatch);
        input.SetAttribute("pattern", "[");
        BrowserControlValidation.Read(realm, input).Flags.Should().Be(ControlValidityFlags.None);
    }

    [Test]
    public void SelectPlaceholderMustBeTheSoleSelectedFirstOptionOutsideAnOptgroup()
    {
        var realm = DomRealm.Of(new Engine());
        var document = ContentDom.Parse("<select id=a required><option value=''>Choose</option><option>x</option></select><select id=b required><optgroup><option value='' selected>Empty valid option</option></optgroup></select>");
        var first = ContentDom.ElementById(document, "a")!;
        var grouped = ContentDom.ElementById(document, "b")!;
        BrowserControlValidation.Read(realm, first).Flags.Should().Be(ControlValidityFlags.ValueMissing);
        BrowserControlValidation.Read(realm, grouped).Flags.Should().Be(ControlValidityFlags.None);
    }

    [Test]
    public void BarredControlRetainsCustomErrorButHasNoValidationMessage()
    {
        var realm = DomRealm.Of(new Engine());
        var input = ContentDom.ElementById(ContentDom.Parse("<input id=t disabled>"), "t")!;
        BrowserControlValidation.SetCustomValidity(input, "custom");
        var snapshot = BrowserControlValidation.Read(realm, input);
        snapshot.WillValidate.Should().BeFalse();
        snapshot.Flags.Should().Be(ControlValidityFlags.CustomError);
        BrowserControlValidation.ValidationMessage(realm, input).Should().BeEmpty();
        BrowserControlValidation.SetCustomValidity(input, "");
        BrowserControlValidation.Read(realm, input).IsValid.Should().BeTrue();
    }
}

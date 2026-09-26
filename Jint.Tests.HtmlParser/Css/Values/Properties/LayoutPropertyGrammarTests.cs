using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.Properties;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

[TestFixture]
public sealed class LayoutPropertyGrammarTests
{
    [TestCase("width", "12PX", "12px")]
    [TestCase("height", "-0", "0px")]
    [TestCase("width", "1in", "96px")]
    [TestCase("height", "25%", "25%")]
    [TestCase("width", "1.25rem", "1.25rem")]
    [TestCase("width", "max-content", "max-content")]
    [TestCase("height", "stretch", "stretch")]
    [TestCase("flex-basis", "CONTENT", "content")]
    [TestCase("width", "fit-content(20%)", "fit-content(20%)")]
    [TestCase("height", "fit-content(calc(1in + 2px))", "fit-content(calc(98px))")]
    [TestCase("width", "calc(100% - 2rem)", "calc(100% - 2rem)")]
    [TestCase("width", "calc(-1px)", "calc(-1px)")]
    [TestCase("flex-grow", "+1.25", "1.25")]
    [TestCase("flex-shrink", "calc(-1)", "calc(-1)")]
    [TestCase("flex-direction", "c\\6f lumn-reverse", "column-reverse")]
    [TestCase("flex-wrap", "WRAP-reverse", "wrap-reverse")]
    [TestCase("direction", "RTL", "rtl")]
    [TestCase("flex-flow", "wrap", "row wrap")]
    [TestCase("flex-flow", "wrap column", "column wrap")]
    [TestCase("flex", "none", "0 0 auto")]
    [TestCase("flex", "auto", "1 1 auto")]
    [TestCase("flex", "1", "1 1 0px")]
    [TestCase("flex", "0", "0 1 0px")]
    [TestCase("flex", "20px", "1 1 20px")]
    [TestCase("flex", "2 3", "2 3 0px")]
    [TestCase("flex", "2 3 0", "2 3 0px")]
    [TestCase("flex", "auto 2 3", "2 3 auto")]
    [TestCase("flex", "calc(2) calc(3) 10%", "calc(2) calc(3) 10%")]
    [TestCase("align-items", "first baseline", "baseline")]
    [TestCase("align-self", "last baseline", "last baseline")]
    [TestCase("align-self", "safe normal", "safe normal")]
    [TestCase("justify-self", "unsafe left", "unsafe left")]
    [TestCase("justify-items", "right legacy", "legacy right")]
    [TestCase("align-items", "SAFE self-end", "safe self-end")]
    [TestCase("place-items", "first baseline last baseline", "baseline last baseline")]
    [TestCase("place-items", "safe center", "safe center")]
    [TestCase("place-items", "center legacy left", "center legacy left")]
    [TestCase("place-self", "auto", "auto")]
    [TestCase("place-self", "safe normal unsafe right", "safe normal unsafe right")]
    public void AcceptsAndCanonicallyReparses(string name, string source, string expected)
    {
        var result = CssPropertyParser.Parse(name, source);
        result.Status.Should().Be(CssPropertyStatus.Valid);
        result.Value.Serialize().Should().Be(expected);
        var again = CssPropertyParser.Parse(name, expected);
        again.Status.Should().Be(CssPropertyStatus.Valid);
        again.Value.Serialize().Should().Be(expected);
    }

    [TestCase("width", "-1e-999999px")]
    [TestCase("height", "-1%")]
    [TestCase("width", "1")]
    [TestCase("width", "1fr")]
    [TestCase("width", "none")]
    [TestCase("width", "content")]
    [TestCase("width", "fit-content(auto)")]
    [TestCase("width", "fit-content(1px, 2px)")]
    [TestCase("height", "calc(1s)")]
    [TestCase("flex-grow", "-1e-999999")]
    [TestCase("flex-grow", "1%")]
    [TestCase("flex-shrink", "calc(1px)")]
    [TestCase("flex-direction", "diagonal")]
    [TestCase("flex-wrap", "wrap nowrap")]
    [TestCase("direction", "auto")]
    [TestCase("flex", "1 auto 2")]
    [TestCase("flex", "0 1 2")]
    [TestCase("flex", "1 2 3")]
    [TestCase("flex", "-1 2 auto")]
    [TestCase("flex-flow", "row column")]
    [TestCase("align-items", "auto")]
    [TestCase("align-items", "left")]
    [TestCase("align-items", "safe normal")]
    [TestCase("align-items", "safe stretch")]
    [TestCase("align-self", "unsafe baseline")]
    [TestCase("align-self", "center safe")]
    [TestCase("justify-self", "legacy left")]
    [TestCase("justify-items", "auto")]
    [TestCase("justify-items", "safe normal")]
    [TestCase("justify-items", "legacy start")]
    [TestCase("place-items", "auto")]
    [TestCase("place-items", "left center")]
    [TestCase("place-self", "center legacy left")]
    [TestCase("place-items", "safe safe center")]
    public void RejectsInvalidGrammar(string name, string source) =>
        CssPropertyParser.Parse(name, source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("width", "anchor-size(width)", "sizing:anchor-size")]
    [TestCase("flex-basis", "calc-size(auto, size)", "sizing:calc-size")]
    [TestCase("flex", "1 anchor-size(width)", "sizing:anchor-size")]
    public void UnfinishedFunctionsRemainNamedBlockers(string name, string source, string blocker)
    {
        var result = CssPropertyParser.Parse(name, source);
        result.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        result.Blocker.Should().Be(blocker);
    }

    [Test]
    public void RetainsTypedSpecifiedPayloadsAndRanges()
    {
        var width = CssPropertyParser.Parse("width", "10%").Value;
        width.Numeric.Kind.Should().Be(CssNumericKind.Percentage);
        width.Numeric.Number.Spelling.Should().Be("10");
        var calculation = CssPropertyParser.Parse("flex-grow", "calc(-2)").Value;
        calculation.Math.Context.Range.Lower.Should().Be(0);
        var flex = CssPropertyParser.Parse("flex", "2").Value;
        flex.Components.Count.Should().Be(3);
        flex.Components[0].Numeric.Number.Spelling.Should().Be("2");
        flex.Components[2].Numeric.Unit.Should().Be(CssUnit.Px);
        Action wrong = () => _ = width.Components;
        wrong.Should().Throw<InvalidOperationException>();
    }

    [TestCase("flex")]
    [TestCase("flex-flow")]
    [TestCase("place-items")]
    [TestCase("place-self")]
    public void ReferencesRemainWholeDeferredShorthands(string name)
    {
        var result = CssPropertyParser.Parse(name, "var(--layout, env(foo, center))");
        result.Status.Should().Be(CssPropertyStatus.Deferred);
        result.Value.References.Count.Should().Be(2);
        result.Value.Kind.Should().Be(CssPropertyValueKind.Deferred);
    }

    [Test]
    public void NewMetadataHasRealDefaultsAndNoInventedResets()
    {
        CssPropertyRegistry.Completed["width"].InitialValue.Should().Be("auto");
        CssPropertyRegistry.Completed["direction"].Inherited.Should().BeTrue();
        CssPropertyRegistry.Completed["justify-items"].InitialValue.Should().Be("legacy");
        CssPropertyRegistry.Completed["flex"].Longhands.Should().Equal("flex-grow", "flex-shrink", "flex-basis");
        CssPropertyRegistry.Completed["flex"].ResetOnlyLonghands.Should().BeEmpty();
        CssPropertyParser.Parse("width", "1px", CssDeclarationContext.FontFace).Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        CssPropertyParser.Parse("min-width", "1px").Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
    }

    [TestCase("width", "123456789", "px")]
    [TestCase("flex-grow", "123456789", "")]
    [TestCase("align-items", "center", "")]
    public void CancelsDuringLongValueWorkAfterSyntax(string name, string word, string suffix)
    {
        var source = name == "align-items" ? new string('a', 16000) : string.Concat(Enumerable.Repeat(word, 2000)) + suffix;
        var input = CssReferenceInput.Parse(source, null, default);
        using var cancellation = new CancellationTokenSource();
        var checkpoints = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++checkpoints == 3) cancellation.Cancel(); });
        Action parse = () =>
        {
            if (name == "align-items") CssAlignmentPropertyParser.Parse(CssPropertyGrammar.AlignItems, [input.Components[0]], work);
            else CssSizingPropertyParser.Numeric(input.Components[0], name == "flex-grow", 0, work);
        };
        parse.Should().Throw<OperationCanceledException>();
        checkpoints.Should().Be(3);
    }
}

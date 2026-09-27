using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class ContentAlignmentAndGapTests
{
    [TestCase("align-content", "FIRST baseline", "baseline")]
    [TestCase("align-content", "last baseline", "last baseline")]
    [TestCase("align-content", "s\\61 fe center", "safe center")]
    [TestCase("align-content", "space-evenly", "space-evenly")]
    [TestCase("justify-content", "unsafe right", "unsafe right")]
    [TestCase("justify-content", "space-around", "space-around")]
    [TestCase("place-content", "baseline", "baseline")]
    [TestCase("place-content", "last baseline start", "last baseline")]
    [TestCase("place-content", "first baseline space-between", "baseline space-between")]
    [TestCase("place-content", "safe center", "safe center")]
    [TestCase("place-content", "safe center unsafe right", "safe center unsafe right")]
    [TestCase("place-content", "stretch", "stretch")]
    [TestCase("gap", "0", "0px")]
    [TestCase("gap", "NORMAL 1in", "normal 96px")]
    [TestCase("gap", "10% 10%", "10%")]
    [TestCase("grid-gap", "2em 3rem", "2em 3rem")]
    [TestCase("grid-row-gap", "THIN", "thin")]
    [TestCase("grid-column-gap", "medium", "medium")]
    [TestCase("column-gap", "thick", "thick")]
    [TestCase("row-gap", "calc(-2px)", "calc(-2px)")]
    [TestCase("gap", "calc(20% - 1em) normal", "calc(20% - 1em) normal")]
    public void ParsesAndRoundTripsSpecifiedValues(string property, string source, string expected)
    {
        var result = CssPropertyParser.Parse(property, source);
        result.Status.Should().Be(CssPropertyStatus.Valid);
        result.Value.Serialize().Should().Be(expected);
        CssPropertyParser.Parse(property, expected).Value.Serialize().Should().Be(expected);
    }

    [TestCase("align-content", "left")]
    [TestCase("align-content", "auto")]
    [TestCase("align-content", "self-start")]
    [TestCase("align-content", "safe stretch")]
    [TestCase("align-content", "safe space-between")]
    [TestCase("align-content", "unsafe normal")]
    [TestCase("align-content", "safe self-end")]
    [TestCase("align-content", "baseline first")]
    [TestCase("justify-content", "baseline")]
    [TestCase("justify-content", "last baseline")]
    [TestCase("justify-content", "legacy left")]
    [TestCase("justify-content", "unsafe self-start")]
    [TestCase("place-content", "left center")]
    [TestCase("place-content", "center baseline")]
    [TestCase("place-content", "last baseline last baseline")]
    [TestCase("place-content", "safe center unsafe right junk")]
    [TestCase("align-items", "space-between")]
    [TestCase("justify-self", "space-evenly")]
    [TestCase("gap", "auto")]
    [TestCase("gap", "1")]
    [TestCase("gap", "1fr")]
    [TestCase("gap", "1px, 2px")]
    [TestCase("gap", "1px 2px 3px")]
    [TestCase("row-gap", "1px 2px")]
    [TestCase("gap", "-1e-9999px")]
    [TestCase("column-gap", "-1%")]
    [TestCase("gap", "calc(1s)")]
    [TestCase("gap", "thin inherit")]
    public void RejectsInvalidGrammar(string property, string source) =>
        CssPropertyParser.Parse(property, source).Status.Should().Be(CssPropertyStatus.Invalid);

    [Test]
    public void RegisteredEntriesHaveInitialValuesContextsAndShorthandMembership()
    {
        foreach (var name in new[] { "align-content", "justify-content", "place-content", "row-gap", "column-gap", "gap" })
        {
            var metadata = CssPropertyRegistry.Find(name, CssDeclarationContext.Style)!;
            metadata.InitialValue.Should().Be("normal");
            metadata.Inherited.Should().BeFalse();
            CssPropertyParser.Parse(name, metadata.InitialValue).Status.Should().Be(CssPropertyStatus.Valid);
            CssPropertyParser.Parse(name, "inherit", CssDeclarationContext.Keyframe).Status.Should().Be(CssPropertyStatus.Valid);
            CssPropertyParser.Parse(name, "normal", CssDeclarationContext.FontFace).Status.Should().NotBe(CssPropertyStatus.Valid);
        }
        CssPropertyRegistry.Find("gap", CssDeclarationContext.Style)!.Longhands.Should().Equal("row-gap", "column-gap");
        CssPropertyRegistry.Find("place-content", CssDeclarationContext.Style)!.Longhands.Should().Equal("align-content", "justify-content");
        CssPropertyParser.Parse("gap", "var(--space)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse("place-content", "env(alignment)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse("row-gap", "calc(20% - 1em)").Value.Math.Context.Range.Lower.Should().Be(0);
    }
}

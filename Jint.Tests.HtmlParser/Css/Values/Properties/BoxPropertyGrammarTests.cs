#nullable enable
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Values.Properties;

public sealed class BoxPropertyGrammarTests
{
    [TestCase("margin", "1px", "1px")]
    [TestCase("margin", "1px 2px 1px 2px", "1px 2px")]
    [TestCase("padding", "1px 2px 3px 2px", "1px 2px 3px")]
    [TestCase("padding", "1px 1px 1px 1px", "1px")]
    [TestCase("padding", "1px 2px 3px 4px", "1px 2px 3px 4px")]
    [TestCase("margin", "-1px AUTO -3% 2em", "-1px auto -3% 2em")]
    [TestCase("margin-left", "-1e-999999px", "0px")]
    [TestCase("padding-top", "-0", "0px")]
    [TestCase("padding", "1in 2rem 3% 4PX", "96px 2rem 3% 4px")]
    [TestCase("margin-right", "calc(-2px)", "calc(-2px)")]
    [TestCase("padding-bottom", "calc(-2px)", "calc(-2px)")]
    [TestCase("padding-left", "calc(50% - 2em)", "calc(50% - 2em)")]
    [TestCase("min-width", "auto", "auto")]
    [TestCase("max-height", "none", "none")]
    [TestCase("min-height", "fit-content(20%)", "fit-content(20%)")]
    [TestCase("max-width", "min-content", "min-content")]
    public void AcceptsAndCompressesTypedPhysicalBoxValues(string name, string source, string expected)
    {
        var result = CssPropertyParser.Parse(name, source);
        result.Status.Should().Be(CssPropertyStatus.Valid);
        result.Value.Serialize().Should().Be(expected);
        CssPropertyParser.Parse(name, expected).Value.Serialize().Should().Be(expected);
    }

    [TestCase("padding", "-1e-999999px")]
    [TestCase("padding-left", "-1%")]
    [TestCase("padding", "auto")]
    [TestCase("margin", "none")]
    [TestCase("margin", "1px 2px 3px 4px 5px")]
    [TestCase("padding", "1px,2px")]
    [TestCase("padding", "1px junk")]
    [TestCase("margin-top", "1px 2px")]
    [TestCase("padding", "2")]
    [TestCase("margin", "-1e-999999")]
    [TestCase("margin", "calc(1s)")]
    [TestCase("padding", "1fr")]
    [TestCase("min-width", "none")]
    [TestCase("max-height", "auto")]
    [TestCase("max-width", "-1e-999999px")]
    public void RejectsInvalidGrammarBeforeNumericRounding(string name, string source) =>
        CssPropertyParser.Parse(name, source).Status.Should().Be(CssPropertyStatus.Invalid);

    [TestCase("margin", "anchor-size(width)", "box:anchor-size")]
    [TestCase("min-width", "anchor-size(width)", "sizing:anchor-size")]
    [TestCase("max-height", "calc-size(auto, size)", "sizing:calc-size")]
    [TestCase("min-height", "contain", "sizing:contain")]
    public void RecognizedUnsupportedExtensionsRemainExplicitDebt(string name, string source, string blocker)
    {
        var result = CssPropertyParser.Parse(name, source);
        result.Status.Should().Be(CssPropertyStatus.UnimplementedGrammar);
        result.Blocker.Should().Be(blocker);
    }

    [TestCase("margin")]
    [TestCase("padding")]
    public void WideAndDeferredValuesUseTheSharedPropertyBoundary(string name)
    {
        CssPropertyParser.Parse(name, "inherit").Value.Text.Should().Be("inherit");
        CssPropertyParser.Parse(name, "var(--edges)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyRegistry.Find(name + "-top", CssDeclarationContext.Style)!.Inherited.Should().BeFalse();
    }
}

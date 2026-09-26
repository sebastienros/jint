using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssWhiteSpaceDeclarationTests
{
    [TestCase("normal", "collapse", "wrap", "none", "normal")]
    [TestCase("pre", "preserve", "nowrap", "none", "pre")]
    [TestCase("pre-wrap", "preserve", "wrap", "none", "pre-wrap")]
    [TestCase("pre-line", "preserve-breaks", "wrap", "none", "pre-line")]
    [TestCase("nowrap", "collapse", "nowrap", "none", "nowrap")]
    [TestCase("break-spaces", "break-spaces", "wrap", "none", "break-spaces")]
    [TestCase("preserve-spaces", "preserve-spaces", "wrap", "none", "preserve-spaces")]
    [TestCase("discard", "discard", "wrap", "none", "discard")]
    [TestCase("discard-inner discard-before nowrap preserve-breaks", "preserve-breaks", "nowrap", "discard-before discard-inner",
        "preserve-breaks nowrap discard-before discard-inner")]
    public void ShorthandExpandsAllThreeLonghandsAndSerializesCanonicalValues(string source, string collapse,
        string wrap, string trim, string expected)
    {
        var block = CssDeclarationBlock.Parse("white-space:" + source);
        block.GetPropertyValue("white-space-collapse").Should().Be(collapse);
        block.GetPropertyValue("text-wrap-mode").Should().Be(wrap);
        block.GetPropertyValue("white-space-trim").Should().Be(trim);
        block.GetPropertyValue("white-space").Should().Be(expected);
        CssDeclarationBlock.Parse(block.CssText).GetPropertyValue("white-space").Should().Be(expected);
    }

    [TestCase("discard-before nowrap discard-after")]
    [TestCase("discard-inner nowrap preserve-breaks discard-before")]
    [TestCase("pre nowrap")]
    [TestCase("collapse preserve")]
    [TestCase("wrap nowrap")]
    [TestCase("discard-inner discard-inner")]
    [TestCase("none discard-before")]
    [TestCase("preserve balance")]
    public void InvalidShorthandPreservesThePriorValidDeclaration(string source)
    {
        var block = CssDeclarationBlock.Parse("white-space:pre");
        var stamp = block.Stamp;
        block.SetProperty("white-space", source);
        block.Stamp.Should().Be(stamp);
        block.GetPropertyValue("white-space").Should().Be("pre");
    }

    [Test]
    public void ShorthandResetsTrimAndLonghandMetadataHasIndividualInheritance()
    {
        var block = CssDeclarationBlock.Parse("white-space-trim:discard-inner; white-space:normal !important");
        block.GetPropertyValue("white-space-trim").Should().Be("none");
        block.GetPropertyPriority("white-space").Should().Be("important");
        CssPropertyRegistry.Find("white-space-collapse", CssDeclarationContext.Style)!.Inherited.Should().BeTrue();
        CssPropertyRegistry.Find("text-wrap-mode", CssDeclarationContext.Style)!.Inherited.Should().BeTrue();
        CssPropertyRegistry.Find("white-space-trim", CssDeclarationContext.Style)!.Inherited.Should().BeFalse();
        block.SetProperty("white-space", "inherit");
        block.GetPropertyValue("white-space-trim").Should().Be("inherit");
        block.SetProperty("white-space", "var(--mode)");
        block.GetPropertyValue("white-space").Should().Be("var(--mode)");
    }
}

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssLayoutDeclarationTests
{
    [TestCase("flex", "2", "flex-grow", "2", "flex-shrink", "1", "flex-basis", "0%", "2 1 0%")]
    [TestCase("flex-flow", "wrap", "flex-direction", "row", "flex-wrap", "wrap", null, null, "row wrap")]
    [TestCase("gap", "10px", "row-gap", "10px", "column-gap", "10px", null, null, "10px")]
    [TestCase("gap", "normal 5%", "row-gap", "normal", "column-gap", "5%", null, null, "normal 5%")]
    public void ExpandsAndSerializes(string name, string source, string first, string firstValue,
        string second, string secondValue, string? third, string? thirdValue, string expected)
    {
        var block = CssDeclarationBlock.Parse(name + ": " + source + " !important");
        block.GetPropertyValue(first).Should().Be(firstValue);
        block.GetPropertyValue(second).Should().Be(secondValue);
        if (third is not null) block.GetPropertyValue(third).Should().Be(thirdValue);
        block.GetPropertyValue(name).Should().Be(expected);
        block.GetPropertyPriority(name).Should().Be("important");
        block.CssText.Should().Be(name + ": " + expected + " !important;");
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
        block.RemoveProperty(name).Should().Be(expected);
        block.Count.Should().Be(0);
    }

    [TestCase("flex", "flex-grow")]
    [TestCase("flex-flow", "flex-direction")]
    [TestCase("gap", "row-gap")]
    public void WideKeywordExpandsToEveryLonghand(string shorthand, string longhand)
    {
        var block = CssDeclarationBlock.Parse(shorthand + ": inherit");
        block.GetPropertyValue(longhand).Should().Be("inherit");
        block.GetPropertyValue(shorthand).Should().Be("inherit");
        block.CssText.Should().Be(shorthand + ": inherit;");
    }

    [Test]
    public void FlexShorthandResetsOmittedMembersAndPreservesTheirTypedValues()
    {
        var block = CssDeclarationBlock.Parse("flex-grow: 4; flex-shrink: 5; flex-basis: 30%; flex: 2");
        block.Count.Should().Be(3);
        block.GetPropertyValue("flex").Should().Be("2 1 0%");
        block.SetProperty("flex", "none");
        block.GetPropertyValue("flex").Should().Be("0 0 auto");
        block.GetDeclaration(0).Value.Should().Be("0");
    }

    [Test]
    public void MixedImportanceAndWideKeywordsCannotSerializeAsShorthand()
    {
        var block = CssDeclarationBlock.Parse("flex: 2; flex-grow: 3 !important");
        block.GetPropertyValue("flex").Should().Be("");
        block.GetPropertyPriority("flex").Should().Be("");
        block.CssText.Should().Contain("flex-grow: 3 !important;").And.Contain("flex-basis: 0%;");
    }


    [Test]
    public void RetainsLargeIntegerCoordinatesThroughExpansionAndReconstruction()
    {
        const string source = "flex: 1000000000000001 1234567890123456 fit-content(1000000000000001px)";
        var block = CssDeclarationBlock.Parse(source);
        block.GetPropertyValue("flex-grow").Should().Be("1000000000000001");
        block.GetPropertyValue("flex-shrink").Should().Be("1234567890123456");
        block.GetPropertyValue("flex-basis").Should().Be("fit-content(1000000000000001px)");
        block.CssText.Should().Be(source + ";");
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
    }

    [Test]
    public void GapAliasesShareCascadeRemovalAndSerialization()
    {
        var block = CssDeclarationBlock.Parse("grid-gap:1px 2px;row-gap:3px;grid-column-gap:4px !important");
        block.GetPropertyValue("grid-row-gap").Should().Be("3px");
        block.GetPropertyValue("column-gap").Should().Be("4px");
        block.GetPropertyValue("gap").Should().BeEmpty();
        block.SetProperty("grid-gap", "5px");
        block.GetPropertyValue("gap").Should().Be("5px");
        block.CssText.Should().Be("gap: 5px;");
        block.RemoveProperty("grid-gap").Should().Be("5px");
        block.Count.Should().Be(0);
    }




}

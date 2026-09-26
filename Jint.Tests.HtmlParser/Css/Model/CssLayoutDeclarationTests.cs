using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssLayoutDeclarationTests
{
    [TestCase("flex", "2", "flex-grow", "2", "flex-shrink", "1", "flex-basis", "0px", "2 1 0px")]
    [TestCase("flex-flow", "wrap", "flex-direction", "row", "flex-wrap", "wrap", null, null, "row wrap")]
    [TestCase("place-items", "safe center", "align-items", "safe center", "justify-items", "safe center", null, null, "safe center")]
    [TestCase("place-self", "last baseline start", "align-self", "last baseline", "justify-self", "start", null, null, "last baseline start")]
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
    [TestCase("place-items", "align-items")]
    [TestCase("place-self", "align-self")]
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
        block.GetPropertyValue("flex").Should().Be("2 1 0px");
        block.SetProperty("flex", "none");
        block.GetPropertyValue("flex").Should().Be("0 0 auto");
        block.GetDeclaration(0).Value.Numeric.Number.Sign.Should().Be(0);
    }

    [Test]
    public void MixedImportanceAndWideKeywordsCannotSerializeAsShorthand()
    {
        var block = CssDeclarationBlock.Parse("flex: 2; flex-grow: 3 !important");
        block.GetPropertyValue("flex").Should().Be("");
        block.GetPropertyPriority("flex").Should().Be("");
        block.CssText.Should().Contain("flex-grow: 3 !important;").And.Contain("flex-basis: 0px;");
        block = CssDeclarationBlock.Parse("place-items: center; justify-items: inherit");
        block.GetPropertyValue("place-items").Should().Be("");
    }

    [TestCase("flex", "flex-grow", "2")]
    [TestCase("flex-flow", "flex-direction", "row")]
    [TestCase("place-items", "align-items", "center")]
    [TestCase("place-self", "align-self", "auto")]
    public void PendingShorthandSharesOneIdentityAndPartialOverrideBreaksReconstruction(
        string name, string longhand, string replacement)
    {
        var block = CssDeclarationBlock.Parse(name + ": var(--x, env(foo)) !important");
        var pending = block.GetDeclaration(0).PendingShorthand;
        pending.Should().NotBeNull();
        for (var i = 1; i < block.Count; i++) block.GetDeclaration(i).PendingShorthand.Should().BeSameAs(pending);
        block.GetPropertyValue(longhand).Should().Be("");
        block.GetPropertyValue(name).Should().Be("var(--x, env(foo))");
        block.CssText.Should().Be(name + ": var(--x, env(foo)) !important;");
        block.SetProperty(longhand, replacement, "important");
        block.GetPropertyValue(name).Should().Be("");
        block.GetPropertyValue(longhand).Should().Be(replacement);
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

    [TestCase("width", "contain", "sizing:contain")]
    [TestCase("height", "contain", "sizing:contain")]
    [TestCase("flex-basis", "contain", "sizing:contain")]
    [TestCase("flex", "1 2 contain", "sizing:contain")]
    [TestCase("align-self", "anchor-center", "alignment:anchor-center")]
    [TestCase("justify-self", "anchor-center", "alignment:anchor-center")]
    [TestCase("place-self", "anchor-center center", "alignment:anchor-center")]
    [TestCase("place-self", "center anchor-center", "alignment:anchor-center")]
    public void PendingLayoutGrammarAbortsSetAndReplacementAtomically(string name, string value, string blocker)
    {
        var block = CssDeclarationBlock.Parse("flex: 2; place-self: center");
        var stamp = block.Stamp;
        var text = block.CssText;
        var first = block.GetDeclaration(0);
        Action set = () => block.SetProperty(name, value);
        var failure = set.Should().Throw<CssIncompleteGrammarException>().Which;
        failure.PropertyName.Should().Be(name);
        failure.Blocker.Should().Be(blocker);
        block.CssText.Should().Be(text);
        block.Stamp.Should().Be(stamp);
        block.GetDeclaration(0).Should().BeSameAs(first);
        Action replace = () => block.ReplaceText("height: 20px; " + name + ": " + value);
        replace.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        block.CssText.Should().Be(text);
        block.Stamp.Should().Be(stamp);
        block.GetDeclaration(0).Should().BeSameAs(first);
        block.SetProperty(name, value, "invalid-priority");
        block.Stamp.Should().Be(stamp);
    }

    [Test]
    public void KnownMissingSizingGrammarAbortsAtomically()
    {
        var block = CssDeclarationBlock.Parse("flex: 2");
        var stamp = block.Stamp;
        var text = block.CssText;
        Action set = () => block.SetProperty("flex", "1 anchor-size(width)");
        set.Should().Throw<CssIncompleteGrammarException>();
        block.CssText.Should().Be(text);
        block.Stamp.Should().Be(stamp);
        Action replace = () => block.ReplaceText("width: 10px; flex-basis: calc-size(auto, size)");
        replace.Should().Throw<CssIncompleteGrammarException>();
        block.CssText.Should().Be(text);
        block.Stamp.Should().Be(stamp);
    }
}

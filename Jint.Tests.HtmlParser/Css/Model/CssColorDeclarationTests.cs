using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssColorDeclarationTests
{
    [Test]
    public void ExplicitDeclarationParsingOwnsTypedColorsAndPreservesPriority()
    {
        var block = CssDeclarationBlock.Parse("color: RED !important; background-color: hsl(240 100% 50%); color: blue");
        block.GetPropertyValue("color").Should().Be("red");
        block.GetPropertyPriority("color").Should().Be("important");
        block.GetPropertyValue("background-color").Should().Be("rgb(0, 0, 255)");
        block.GetDeclaration(0).Value.Color.Kind.Should().Be(CssColorKind.Named);
        var stamp = block.Stamp;
        block.SetProperty("color", "rgb(255 0 0 / none)", "important");
        block.GetPropertyValue("color").Should().Be("color(srgb 1 0 0 / none)");
        block.Stamp.Should().NotBe(stamp);
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
        block.RemoveProperty("color").Should().Be("color(srgb 1 0 0 / none)");
        block.Count.Should().Be(1);
    }

    [TestCase("lab(50% 0 0)", "color:lab")]
    [TestCase("color(display-p3 1 0 0)", "color:color")]
    [TestCase("rgb(from red r g b)", "color:relative-rgb")]
    [TestCase("rgb(calc(sign(1em - 10px) * 10%) 0 0)", "color:channel-environment")]
    public void PendingColorGrammarAbortsMutationAndReplacementAtomically(string source, string blocker)
    {
        var block = CssDeclarationBlock.Parse("color: red; background-color: transparent");
        var stamp = block.Stamp;
        var text = block.CssText;
        var first = block.GetDeclaration(0);
        Action set = () => block.SetProperty("color", source);
        set.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
        block.GetDeclaration(0).Should().BeSameAs(first);
        Action replace = () => block.ReplaceText("color: blue; background-color: " + source);
        replace.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
        block.GetDeclaration(0).Should().BeSameAs(first);
        block.SetProperty("color", source, "invalid-priority");
        block.Stamp.Should().Be(stamp);
    }

    [Test]
    public void InvalidCssAndDeferredReferencesFollowExistingDeclarationSemantics()
    {
        var block = CssDeclarationBlock.Parse("color: red");
        var stamp = block.Stamp;
        block.SetProperty("color", "rgb(1px 0 0)");
        block.GetPropertyValue("color").Should().Be("red");
        block.Stamp.Should().Be(stamp);
        block.SetProperty("color", "rgb(var(--r) 0 0)");
        block.GetDeclaration(0).Value.Kind.Should().Be(CssPropertyValueKind.Deferred);
        block.GetPropertyValue("color").Should().Be("rgb(var(--r) 0 0)");
        block.ReplaceText("color: rgb(1px 0 0); background-color: BLUE");
        block.Count.Should().Be(1);
        block.GetPropertyValue("background-color").Should().Be("blue");
    }
}

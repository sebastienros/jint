using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssAllDeclarationTests
{
    [TestCase("initial")]
    [TestCase("inherit")]
    [TestCase("unset")]
    [TestCase("revert")]
    [TestCase("revert-layer")]
    [TestCase("revert-rule")]
    public void AllExpandsAndReconstructsEveryKnownOrdinaryLonghand(string value)
    {
        var block = CssDeclarationBlock.Parse("all:" + value + "!important;direction:rtl;--x:red");
        var metadata = CssPropertyRegistry.Completed["all"];
        metadata.Longhands.Should().Contain("border-image-source").And.Contain("font-kerning").And.Contain("clip-path");
        metadata.Longhands.Should().NotContain("src").And.NotContain("direction").And.NotContain("unicode-bidi")
            .And.NotContain("margin").And.NotContain("word-wrap");
        metadata.Longhands.Should().OnlyHaveUniqueItems();
        foreach (var name in metadata.Longhands)
        {
            block.GetPropertyValue(name).Should().Be(value, name);
            block.GetPropertyPriority(name).Should().Be("important", name);
        }
        block.GetPropertyValue("all").Should().Be(value);
        block.GetPropertyPriority("all").Should().Be("important");
        block.CssText.Should().Be("all: " + value + " !important; direction: rtl; --x: red;");
        block.RemoveProperty("all").Should().Be(value);
        block.CssText.Should().Be("direction: rtl; --x: red;");
    }

    [TestCase("red")]
    [TestCase("initial inherit")]
    [TestCase("1")]
    [TestCase("initial!important")]
    public void InvalidAllDoesNotMutate(string input)
    {
        var block = CssDeclarationBlock.Parse("color:red");
        var stamp = block.Stamp;
        block.SetProperty("all", input);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be("color: red;");
    }

    [Test]
    public void LonghandOverridesAndPartialRemovalPreserveOtherResets()
    {
        var block = CssDeclarationBlock.Parse("all:initial");
        block.SetProperty("display", "flex");
        block.GetPropertyValue("all").Should().BeEmpty();
        block.GetPropertyValue("display").Should().Be("flex");
        block.GetPropertyValue("border-image-source").Should().Be("initial");
        var text = block.CssText;
        var copy = CssDeclarationBlock.Parse(text);
        copy.GetPropertyValue("display").Should().Be("flex");
        copy.GetPropertyValue("border-image-source").Should().Be("initial");
        block.RemoveProperty("opacity").Should().Be("initial");
        block.GetPropertyValue("opacity").Should().BeEmpty();
        block.RemoveProperty("border-image-source").Should().Be("initial");
        block.GetPropertyValue("border-image-source").Should().BeEmpty();
        block.SetProperty("all", "unset");
        block.GetPropertyValue("display").Should().Be("unset");
        block.GetPropertyValue("all").Should().Be("unset");
    }

    [Test]
    public void PriorityAndPendingSubstitutionRetainOneShorthandIdentity()
    {
        var block = CssDeclarationBlock.Parse("display:flex!important;all:initial;opacity:.5");
        block.GetPropertyValue("display").Should().Be("flex");
        block.GetPropertyValue("opacity").Should().Be("0.5");
        block.GetPropertyValue("visibility").Should().Be("initial");
        block.SetProperty("all", "var(--reset)", "important");
        block.GetPropertyValue("all").Should().Be("var(--reset)");
        block.GetPropertyValue("display").Should().BeEmpty();
        block.GetPropertyPriority("display").Should().Be("important");
        block.CssText.Should().Be("all: var(--reset) !important;");
        block.RemoveProperty("border-image-source").Should().BeEmpty();
        block.GetPropertyPriority("border-image-source").Should().BeEmpty();
        block.GetPropertyPriority("display").Should().Be("important");
    }

    [Test]
    public void CancellationAndPendingNonWideGrammarsStillFailAtomically()
    {
        var block = CssDeclarationBlock.Parse("all:initial");
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action set = () => block.SetProperty("all", "inherit", cancellationToken: cancellation.Token);
        set.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        Action replace = () => block.ReplaceText("all:initial;border-image-source:url(a)");
        replace.Should().Throw<CssIncompleteGrammarException>();
        block.Stamp.Should().Be(stamp);
        CssPropertyParser.Parse("all", "initial", CssDeclarationContext.FontFace).Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
        CssPropertyParser.Parse("made-up", "initial").Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
        var work = new CssValueWork(default);
        block.ResolveProperty("unicode-bidi", work).Should().BeNull();
        block.ResolveProperty("--x", work).Should().BeNull();
    }
}

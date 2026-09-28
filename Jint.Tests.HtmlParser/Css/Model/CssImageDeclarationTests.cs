using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssImageDeclarationTests
{
    [TestCase("NONE", "none")]
    [TestCase("URL(a.svg), none, src('b.png')", "url(\"a.svg\"), none, src(\"b.png\")")]
    [TestCase("url(),url(#p)", "url(\"\"), url(\"#p\")")]
    public void ImageLayersKeepTheirCountOrderAndTypedReferences(string input, string expected)
    {
        var value = CssPropertyParser.Parse("background-image", input).Value;
        value.Kind.Should().Be(CssPropertyValueKind.ImageList);
        value.Components.Should().NotBeEmpty();
        var block = CssDeclarationBlock.Parse("background-image:" + input + "!important");
        block.GetPropertyValue("background-image").Should().Be(expected);
        block.GetPropertyPriority("background-image").Should().Be("important");
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
        block.RemoveProperty("background-image").Should().Be(expected);
        block.Count.Should().Be(0);
    }

    [TestCase("url(a) none")]
    [TestCase(",none")]
    [TestCase("none,")]
    [TestCase("none,,none")]
    [TestCase("red")]
    [TestCase("url(a), initial")]
    [TestCase("src(unquoted)")]
    [TestCase("bogus(red)")]
    public void InvalidImageListsPreserveTheOldValue(string input)
    {
        var block = CssDeclarationBlock.Parse("background-image:url(old)");
        var stamp = block.Stamp;
        CssPropertyParser.Parse("background-image", input).Status.Should().Be(CssPropertyStatus.Invalid);
        block.SetProperty("background-image", input);
        block.Stamp.Should().Be(stamp);
    }

    [TestCase("linear-gradient(red, blue)", "V1:image:linear-gradient")]
    [TestCase("LINEAR-GRADIENT(red, blue)", "V1:image:linear-gradient")]
    [TestCase("image-set(url(a) 1x)", "V1:image:image-set")]
    [TestCase("url('a' type('image/png'))", "V1:image-url-modifiers")]
    public void PendingImageGrammarsAreAtomicNamedFailures(string input, string blocker)
    {
        var block = CssDeclarationBlock.Parse("color:red");
        Action replace = () => block.ReplaceText("color:blue;background-image:" + input);
        replace.Should().Throw<CssIncompleteGrammarException>().Which.Blocker.Should().Be(blocker);
        block.CssText.Should().Be("color: red;");
    }

    [Test]
    public void ImageReferencesKeepSubstitutionContextsAndCancellation()
    {
        CssPropertyParser.Parse("background-image", "src(var(--image)), var(--other)").Status.Should().Be(CssPropertyStatus.Deferred);
        CssPropertyParser.Parse("background-image", "none", CssDeclarationContext.FontFace).Status.Should().Be(CssPropertyStatus.UnsupportedProperty);
        CssPropertyRegistry.Completed["background-image"].Inherited.Should().BeFalse();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action parse = () => CssPropertyParser.Parse("background-image", "url(a)", cancellationToken: cancellation.Token);
        parse.Should().Throw<OperationCanceledException>();
    }

    [TestCase("background-position", "initial initial")]
    [TestCase("background-repeat", "initial initial")]
    [TestCase("border", "red inherit")]
    [TestCase("font-family", "unset, serif")]
    [TestCase("animation", "revert 1s")]
    [TestCase("animation", "1s revert")]
    public void ReservedWideKeywordsCannotBeMixedEvenInPendingGrammars(string name, string input)
    {
        CssPropertyParser.Parse(name, input).Status.Should().Be(CssPropertyStatus.Invalid);
        var block = CssDeclarationBlock.Parse("color:red");
        var stamp = block.Stamp;
        block.SetProperty(name, input);
        block.Stamp.Should().Be(stamp);
    }
}

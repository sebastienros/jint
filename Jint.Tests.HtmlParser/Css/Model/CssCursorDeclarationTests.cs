#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssCursorDeclarationTests
{
    [TestCase("bogus")]
    [TestCase("pointer text")]
    [TestCase("pointer, text")]
    [TestCase("1 2 pointer")]
    public void InvalidReplacementPreservesThePriorDeclaration(string source)
    {
        var block = CssDeclarationBlock.Parse("cursor:grab !important");
        var stamp = block.Stamp;
        var text = block.CssText;
        block.SetProperty("cursor", source);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }

    [TestCase("url(cursor.cur), pointer")]
    [TestCase("url('cursor.cur') 4 5, auto")]
    [TestCase("image-set(url(cursor.cur) 1x), text")]
    public void PendingImageReplacementPreservesThePriorDeclaration(string source)
    {
        var block = CssDeclarationBlock.Parse("cursor:grab !important");
        var stamp = block.Stamp;
        var text = block.CssText;
        Assert.Throws<CssIncompleteGrammarException>(() => block.SetProperty("cursor", source))!
            .Blocker.Should().Be("V6:cursor-images");
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }

    [Test]
    public void CancellationLeavesThePriorDeclarationIntact()
    {
        var block = CssDeclarationBlock.Parse("cursor:grab");
        var stamp = block.Stamp;
        var text = block.CssText;
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 32) cancellation.Cancel(); });
        var source = string.Join(" ", Enumerable.Repeat("pointer", 16384));
        Action write = () => block.SetProperty("cursor", source, null, null, work, cancellation.Token);
        write.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }
}

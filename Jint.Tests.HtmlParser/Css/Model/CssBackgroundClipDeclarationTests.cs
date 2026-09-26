#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssBackgroundClipDeclarationTests
{
    [TestCase("border-box text")]
    [TestCase("text text")]
    [TestCase("padding-box,,text")]
    [TestCase("content-box, initial")]
    public void InvalidListReplacementPreservesThePriorValueAndStamp(string source)
    {
        var block = CssDeclarationBlock.Parse("background-clip:content-box, text !important");
        var stamp = block.Stamp;
        var text = block.CssText;
        block.SetProperty("background-clip", source);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(text);
    }

    [Test]
    public void BackgroundShorthandKeepsItsNamedPendingBoundary()
    {
        Assert.Throws<CssIncompleteGrammarException>(() => CssDeclarationBlock.Parse("background:red"))!
            .Blocker.Should().Be("V1:background");
    }

    [Test]
    public void MidListCancellationLeavesThePriorDeclarationIntact()
    {
        var block = CssDeclarationBlock.Parse("background-clip:content-box");
        var stamp = block.Stamp;
        var work = new CssValueWork(default);
        var text = block.CssText;
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var cancellable = new CssValueWork(cancellation.Token, () => { if (++polls == 64) cancellation.Cancel(); });
        var source = string.Join(",", Enumerable.Repeat("border-area text", 16384));
        Action write = () => block.SetProperty("background-clip", source, null, null, cancellable, cancellation.Token);
        write.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        block.Serialize(work).Should().Be(text);
    }
}

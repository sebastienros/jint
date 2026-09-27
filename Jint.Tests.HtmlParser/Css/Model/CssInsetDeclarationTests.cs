#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssInsetDeclarationTests
{
    [TestCase("stretch")]
    [TestCase("2")]
    [TestCase("1px 2px")]
    [TestCase("auto, 2px")]
    public void InvalidReplacementPreservesEverySideAndImportance(string source)
    {
        foreach (var side in new[] { "top", "right", "bottom", "left" })
        {
            var block = CssDeclarationBlock.Parse(side + ":-9999em !important");
            var stamp = block.Stamp;
            var text = block.CssText;
            block.SetProperty(side, source);
            block.Stamp.Should().Be(stamp);
            block.CssText.Should().Be(text);
            CssDeclarationBlock.Parse(text).CssText.Should().Be(text);
        }
    }

    [Test]
    public void AnchorPendingAndCancellationPreserveThePriorDeclaration()
    {
        var block = CssDeclarationBlock.Parse("left:-9999em !important");
        var stamp = block.Stamp;
        var text = block.CssText;
        Assert.Throws<CssIncompleteGrammarException>(() => block.SetProperty("left", "calc(anchor(left) + 2px)"))!
            .Blocker.Should().Be("inset:anchor");
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++polls == 32) cancellation.Cancel(); });
        var source = "calc(" + string.Join(" + ", Enumerable.Repeat("1px", 8192)) + ")";
        Action write = () => block.SetProperty("left", source, null, null, work, cancellation.Token);
        write.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }
}

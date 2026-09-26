#nullable enable

using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

public sealed class CssTextDecorationDeclarationTests
{
    private static readonly string[] Names =
        ["text-decoration-line", "text-decoration-thickness", "text-decoration-style", "text-decoration-color"];

    [Test]
    public void ShorthandResetsItsFourGroupsAndRoundtripsCanonicalCssText()
    {
        var block = CssDeclarationBlock.Parse("text-decoration:underline 3px wavy red");
        block.SetProperty("text-decoration", "overline", "important");
        block.GetPropertyValue("text-decoration-line").Should().Be("overline");
        block.GetPropertyValue("text-decoration-thickness").Should().Be("auto");
        block.GetPropertyValue("text-decoration-style").Should().Be("solid");
        block.GetPropertyValue("text-decoration-color").Should().Be("currentcolor");
        block.GetPropertyValue("text-decoration").Should().Be("overline auto solid currentcolor");
        block.GetPropertyPriority("text-decoration").Should().Be("important");
        CssDeclarationBlock.Parse(block.CssText).CssText.Should().Be(block.CssText);
        var metadata = CssPropertyRegistry.Completed["text-decoration"];
        metadata.Longhands.Should().Equal(Names);
        metadata.ResetOnlyLonghands.Should().BeEmpty();
        foreach (var name in Names) CssPropertyRegistry.Completed[name].Inherited.Should().BeFalse();
        // Underline offset/position retain their separate pending obligations. A completed
        // shorthand must neither resolve nor remove unrelated authored declarations.
        block = CssDeclarationBlock.ParseUnresolved("text-underline-offset:3px;text-underline-position:under;text-decoration:underline",
            CssDeclarationContext.Style, null, new CssValueWork(default), default);
        block.SetProperty("text-decoration", "overline");
        block.SerializeSource(new CssValueWork(default)).Should().Contain("text-underline-offset: 3px;")
            .And.Contain("text-underline-position: under;");
        Assert.Throws<CssIncompleteGrammarException>(() => block.GetPropertyValue("text-underline-offset"));
        Assert.Throws<CssIncompleteGrammarException>(() => block.GetPropertyValue("text-underline-position"));
    }

    [TestCase("inherit")]
    [TestCase("initial")]
    [TestCase("unset")]
    [TestCase("revert")]
    [TestCase("revert-layer")]
    [TestCase("revert-rule")]
    public void WideKeywordsExpandAcrossAllFourGroups(string keyword)
    {
        var block = CssDeclarationBlock.Parse("text-decoration:" + keyword);
        foreach (var name in Names) block.GetPropertyValue(name).Should().Be(keyword);
        block.GetPropertyValue("text-decoration").Should().Be(keyword);
    }

    [Test]
    public void DeferredGroupsShareIdentityAndPartialReplacementBreaksReconstruction()
    {
        var block = CssDeclarationBlock.Parse("text-decoration:var(--decoration) !important");
        var pending = block.GetDeclaration(0).PendingShorthand;
        for (var i = 1; i < 4; i++) block.GetDeclaration(i).PendingShorthand.Should().BeSameAs(pending);
        block.GetPropertyValue("text-decoration").Should().Be("var(--decoration)");
        block.SetProperty("text-decoration-color", "blue");
        block.GetPropertyValue("text-decoration").Should().Be("");
        block.GetPropertyPriority("text-decoration").Should().Be("");
        block.GetPropertyValue("text-decoration-color").Should().Be("blue");
        block.RemoveProperty("text-decoration");
        foreach (var name in Names) block.GetPropertyValue(name).Should().BeEmpty();
    }

    [TestCase("underline red overline")]
    [TestCase("solid dashed")]
    [TestCase("none underline")]
    public void InvalidReplacementPreservesThePriorDeclaration(string replacement)
    {
        var block = CssDeclarationBlock.Parse("text-decoration:underline 2px wavy red");
        var stamp = block.Stamp;
        var text = block.CssText;
        block.SetProperty("text-decoration", replacement);
        block.Stamp.Should().Be(stamp);
        block.CssText.Should().Be(text);
    }

    [Test]
    public void CancellationLeavesThePriorDeclarationAndStampIntact()
    {
        var block = CssDeclarationBlock.Parse("text-decoration:underline 2px solid red");
        var work = new CssValueWork(default);
        var text = block.CssText;
        var stamp = block.Stamp;
        using var cancellation = new CancellationTokenSource();
        var polls = 0;
        var cancellable = new CssValueWork(cancellation.Token, () => { if (++polls == 4) cancellation.Cancel(); });
        Action write = () => block.SetProperty("text-decoration", "overline /*" + new string('x', 16384) + "*/ blue",
            null, null, cancellable, cancellation.Token);
        write.Should().Throw<OperationCanceledException>();
        block.Stamp.Should().Be(stamp);
        block.Serialize(work).Should().Be(text);
    }
}

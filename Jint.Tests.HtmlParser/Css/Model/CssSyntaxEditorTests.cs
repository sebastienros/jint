#nullable enable
using System.Reflection;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssSyntaxEditorTests
{
    [Test]
    public void SheetKeepsLiveViewAndRuleIdentityThroughEdits()
    {
        var sheet = CssSyntaxStyleSheet.Parse("@unknown x; a { color:red } b{}");
        var view = sheet.Rules;
        ((object) view is IList<CssSyntaxRule>).Should().BeFalse();
        var retained = view[1];
        var untouched = view[2];
        var initial = sheet.Stamp;
        initial.Value.Should().Be(0);
        sheet.Serialize().Should().Contain("@unknown");
        retained.Syntax.Span.Start.Should().Be(12);
        sheet.Stamp.Should().Be(initial);

        sheet.InsertRule("@other y;", 1).Should().Be(1);
        ReferenceEquals(sheet.Rules, view).Should().BeTrue();
        ReferenceEquals(view[2], retained).Should().BeTrue();
        ReferenceEquals(view[3], untouched).Should().BeTrue();
        retained.ReplaceSyntax("p { made-up: impossible }");
        ReferenceEquals(view[2], retained).Should().BeTrue();
        retained.Syntax.Prelude[0].Token.Text.Should().Be("p");
        retained.ParentStyleSheet.Should().BeSameAs(sheet);
        retained.Stamp.Value.Should().Be(1);
        sheet.Stamp.Value.Should().Be(2);

        sheet.DeleteRule(2);
        retained.ParentStyleSheet.Should().BeNull();
        var detachedSheetStamp = sheet.Stamp;
        retained.ReplaceSyntax("q{}");
        retained.Stamp.Value.Should().Be(2);
        sheet.Stamp.Should().Be(detachedSheetStamp);
        ReferenceEquals(view[2], untouched).Should().BeTrue();
    }

    [Test]
    public void FailedRuleEditsKeepIdentityContentsAndStamps()
    {
        var sheet = CssSyntaxStyleSheet.Parse("a{} b{}");
        var first = sheet.Rules[0];
        var text = sheet.Serialize();
        var stamp = sheet.Stamp;
        Assert.Throws<CssParseException>(() => sheet.InsertRule("c{} d{}", 1));
        Assert.Throws<CssParseException>(() => first.ReplaceSyntax("bare"));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.InsertRule("c{}", 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => sheet.DeleteRule(2));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => first.ReplaceSyntax("c{}", cancellationToken: canceled.Token));
        Assert.Throws<ParseLimitException>(() => sheet.InsertRule("long-name{}", 1,
            new CssParseOptions { Limits = new ParseLimits { MaxTokenCharacters = 4 } }));
        sheet.Stamp.Should().Be(stamp);
        first.Stamp.Value.Should().Be(0);
        sheet.Serialize().Should().Be(text);
        ReferenceEquals(sheet.Rules[0], first).Should().BeTrue();
    }

    [Test]
    public void DeclarationListKeepsDuplicatesUnknownsAndInvalidValuesInOrder()
    {
        var block = CssSyntaxDeclarationBlock.Parse("color:red; color:bogus; made-up: ???; --X: 1 !important");
        var view = block.Declarations;
        view.Select(item => item.Name).Should().ContainInOrder("color", "color", "made-up", "--X");
        view[1].Value[0].Token.Text.Should().Be("bogus");
        view[3].IsImportant.Should().BeTrue();
        ((object) view is IList<global::Jint.HtmlParser.Css.CssDeclarationSyntax>).Should().BeFalse();
        var first = view[0];

        block.InsertDeclaration("COLOR: chartreuse", 1);
        ReferenceEquals(block.Declarations, view).Should().BeTrue();
        ReferenceEquals(view[0], first).Should().BeTrue();
        block.ReplaceDeclaration("--X: 2", 4);
        block.DeleteDeclaration(2);
        view.Select(item => item.Name).Should().ContainInOrder("color", "COLOR", "made-up", "--X");
        block.Stamp.Value.Should().Be(3);
        var serialized = block.Serialize();
        serialized.Should().Contain("color:red;");
        serialized.Should().Contain("COLOR:chartreuse;");
        block.Stamp.Value.Should().Be(3);
    }

    [Test]
    public void DeclarationReplacementIsAtomicAndRecoveryCanReplaceWithEmptyList()
    {
        var block = CssSyntaxDeclarationBlock.Parse("x:1; y:2");
        var view = block.Declarations;
        var first = view[0];
        var text = block.Serialize();
        Assert.Throws<CssParseException>(() => block.ReplaceDeclaration("broken", 0));
        Assert.Throws<CssParseException>(() => block.InsertDeclaration("a:1; b:2", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => block.DeleteDeclaration(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => block.ReplaceDeclaration("a:1", 2));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => block.ReplaceText("a:1", cancellationToken: canceled.Token));
        Assert.Throws<ParseLimitException>(() => block.ReplaceText("verylong:1",
            new CssParseOptions { Limits = new ParseLimits { MaxTokenCharacters = 4 } }));
        ReferenceEquals(view[0], first).Should().BeTrue();
        block.Serialize().Should().Be(text);
        block.Stamp.Value.Should().Be(0);

        block.ReplaceText("broken; @unknown x;");
        ReferenceEquals(block.Declarations, view).Should().BeTrue();
        view.Should().BeEmpty();
        block.Serialize().Should().BeEmpty();
        block.Stamp.Value.Should().Be(1);
    }

    [Test]
    public void StampsSaturateAndRemainNonReusable()
    {
        var sheet = CssSyntaxStyleSheet.Parse("a{}");
        var rule = sheet.Rules[0];
        var block = CssSyntaxDeclarationBlock.Parse("x:1");
        SetVersion(sheet, ulong.MaxValue - 1);
        SetVersion(rule, ulong.MaxValue - 1);
        SetVersion(block, ulong.MaxValue - 1);
        var before = sheet.Stamp;
        before.CanReuse.Should().BeTrue();
        rule.ReplaceSyntax("b{}");
        block.ReplaceDeclaration("x:2", 0);
        rule.Stamp.CanReuse.Should().BeFalse();
        sheet.Stamp.CanReuse.Should().BeFalse();
        block.Stamp.CanReuse.Should().BeFalse();
        sheet.DeleteRule(0);
        block.DeleteDeclaration(0);
        sheet.Stamp.Value.Should().Be(ulong.MaxValue);
        block.Stamp.Value.Should().Be(ulong.MaxValue);
        before.Value.Should().Be(ulong.MaxValue - 1);
    }

    [Test]
    public void RuleProjectionObservesCancellationAfterACompletedBatch()
    {
        var parsed = new CssSyntaxParser(string.Concat(Enumerable.Repeat("a{}", 1000)), null, default)
            .ParseStyleSheet();
        using var cancellation = new CancellationTokenSource();
        var lastBatch = -1;
        Assert.Throws<OperationCanceledException>(() =>
            CssSyntaxStyleSheet.FromSyntax(parsed, cancellation.Token, index =>
            {
                lastBatch = index;
                if (index == 256) cancellation.Cancel();
            }));
        lastBatch.Should().Be(256);
    }

    private static void SetVersion(object target, ulong value) =>
        target.GetType().GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);
}

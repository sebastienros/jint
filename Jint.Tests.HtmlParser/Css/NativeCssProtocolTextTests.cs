#nullable enable
using Jint.Browser.DevTools;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

[TestFixture]
public sealed class NativeCssProtocolTextTests
{
    [Test]
    public void CoverageRangesIndexTheReturnedTextAndRefreshAfterCssomEdits()
    {
        var work = new CssValueWork(default);
        var sheet = CssStyleSheet.Parse("@media all { div { opacity:.5; } }");
        var group = (CssMediaRule) sheet.Rules[0];
        var rule = (CssStyleRule) group.Rules[0];
        var original = CssStyleSheetText.Of(sheet, work);
        original.TryRangeOf(rule, out var range).Should().BeTrue();
        original.Text[range.Start..range.End].Should().Be("div { opacity: 0.5; }");
        original.Text.Should().NotContain("\r");
        rule.Style.SetProperty("opacity", ".75");
        var current = CssStyleSheetText.Of(sheet, work);
        current.TryRangeOf(rule, out range).Should().BeTrue();
        current.Text[range.Start..range.End].Should().Be("div { opacity: 0.75; }");
        original.Text.Should().Contain("opacity: 0.5;");
        var unrelated = CssStyleSheet.Parse("div { opacity:.75; }");
        current.TryRangeOf(unrelated.Rules[0], out _).Should().BeFalse();
    }

    [Test]
    public void ProtocolTextSerializationHonorsHostWorkCheckpoints()
    {
        var sheet = CssStyleSheet.Parse("div { opacity:.5; }");
        var work = new CssValueWork(default, () => throw new OperationCanceledException());
        Assert.Throws<OperationCanceledException>(() => CssStyleSheetText.Of(sheet, work));
    }
}

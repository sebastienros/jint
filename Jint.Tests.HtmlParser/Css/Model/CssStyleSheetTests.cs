#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssStyleSheetTests
{
    [Test]
    public void ValidatedRulesMatchNativeElementsAndRecoverUnknownRules()
    {
        var sheet = CssStyleSheet.Parse("@unknown x { color:red; } .target { opacity:.5; bogus:1; } ??? { color:red; }");
        sheet.Rules.Count.Should().Be(1);
        var rule = (CssStyleRule) sheet.Rules[0];
        var document = Document.CreateHtml();
        var element = document.CreateElement("div");
        element.SetAttribute("class", "target");
        rule.TryMatch(element, out var specificity).Should().BeTrue();
        specificity.ClassCount.Should().Be(1);
        rule.Style.GetPropertyValue("opacity").Should().Be("0.5");
        rule.ParentStyleSheet.Should().BeSameAs(sheet);
        rule.ParentRule.Should().BeNull();
    }

    [Test]
    public void LiveListAndDeclarationIdentitySurviveMutationsAndDetach()
    {
        var sheet = CssStyleSheet.Parse("a { opacity:.5; }");
        var list = sheet.Rules;
        var rule = (CssStyleRule) list[0];
        var style = rule.Style;
        var originalSpan = rule.SourceSpan;
        var before = sheet.Stamp;
        style.SetProperty("opacity", ".8");
        sheet.Stamp.Should().NotBe(before);
        rule.Stamp.Value.Should().Be(1);
        rule.Style.Should().BeSameAs(style);
        sheet.InsertRule("b { display:block; }", 1).Should().Be(1);
        list.Should().BeSameAs(sheet.Rules);
        list.Count.Should().Be(2);
        list[0].Should().BeSameAs(rule);
        rule.SourceSpan.Should().Be(originalSpan);
        sheet.DeleteRule(0);
        before = sheet.Stamp;
        style.SetProperty("opacity", ".9");
        sheet.Stamp.Should().Be(before);
        rule.ParentStyleSheet.Should().BeNull();
        rule.ParentRule.Should().BeNull();
        list.Count.Should().Be(1);
    }

    [Test]
    public void StrictInsertionKeepsBoundsAndCompletionFailuresAtomic()
    {
        var sheet = CssStyleSheet.Parse("a {}");
        var before = sheet.Stamp;
        Assert.Throws<DomException>(() => sheet.InsertRule("@media all {}", -1))!.Name.Should().Be("IndexSizeError");
        Assert.Throws<DomException>(() => sheet.InsertRule("a {} b {}", 0))!.Name.Should().Be("SyntaxError");
        Assert.Throws<DomException>(() => sheet.InsertRule("@unknown;", 0))!.Name.Should().Be("SyntaxError");
        Assert.Throws<CssIncompleteGrammarException>(() => sheet.InsertRule("a {color:red}", 0))!.PropertyName.Should().Be("color");
        Assert.Throws<CssIncompleteRuleGrammarException>(() => sheet.InsertRule("@supports (x:y) {}", 0))!.Blocker.Should().Be("R2:supports");
        Assert.Throws<CssIncompleteRuleGrammarException>(() => sheet.InsertRule("a { & b {} }", 0))!.Blocker.Should().Be("C2:nesting-selector-context");
        Assert.Throws<DomException>(() => sheet.DeleteRule(1))!.Name.Should().Be("IndexSizeError");
        sheet.Stamp.Should().Be(before);
        sheet.Rules.Count.Should().Be(1);
    }

    [Test]
    public void SelectorSetterKeepsSyntaxFailuresAndCancellationAtomic()
    {
        var sheet = CssStyleSheet.Parse("a {}");
        var rule = (CssStyleRule) sheet.Rules[0];
        var selector = rule.Selector;
        var before = sheet.Stamp;
        rule.SetSelectorText("???");
        rule.Selector.Should().BeSameAs(selector);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => rule.SetSelectorText("b", cancellationToken: cancellation.Token));
        rule.Selector.Should().BeSameAs(selector);
        sheet.Stamp.Should().Be(before);
        rule.SetSelectorText("  b, .c  ");
        rule.SelectorText.Should().Be("b, .c");
        sheet.Stamp.Should().NotBe(before);
    }

    [Test]
    public void SerializationRangesUseRuleIdentityAndUtf16Offsets()
    {
        var sheet = CssStyleSheet.Parse("[data-x='😀{'] { --x:😀; } b { display:block; }");
        var snapshot = sheet.SerializeWithRanges();
        snapshot.Ranges.Count.Should().Be(2);
        foreach (var rule in sheet.Rules)
        {
            var range = snapshot.Ranges[rule];
            snapshot.Text[range.Start..range.End].Should().Be(rule.CssText);
        }
        var first = snapshot.Ranges[sheet.Rules[0]];
        snapshot.Ranges[sheet.Rules[1]].Start.Should().Be(first.End + 1);
        var before = sheet.Stamp;
        sheet.Disabled = true;
        sheet.Stamp.Should().NotBe(before);
        before = sheet.Stamp;
        sheet.Disabled = true;
        sheet.Stamp.Should().Be(before);
        snapshot.Text.Should().Be(sheet.Serialize());
    }
}

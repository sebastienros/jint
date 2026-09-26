#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssMediaRuleTests
{
    [Test]
    public void NestedGroupsKeepIdentityRangesAndSeparateDetachedOwnership()
    {
        var sheet = CssStyleSheet.Parse("@media screen { a {opacity:.5} @media (width >= 1px) { b {display:block} } } c {}");
        var root = (CssMediaRule) sheet.Rules[0];
        var nested = (CssMediaRule) root.Rules[1];
        var child = (CssStyleRule) nested.Rules[0];
        child.ParentRule.Should().BeSameAs(nested);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        var snapshot = sheet.SerializeWithRanges();
        snapshot.Ranges.Count.Should().Be(5);
        foreach (var rule in new CssRule[] { root, nested, child, root.Rules[0], sheet.Rules[1] })
        {
            var range = snapshot.Ranges[rule];
            snapshot.Text[range.Start..range.End].Should().Be(rule.CssText);
        }
        CssStyleSheet.Parse(snapshot.Text).Serialize().Should().Be(snapshot.Text);
        var media = root.Media;
        media.SetMediaText("print");
        root.Media.Should().BeSameAs(media);
        root.Stamp.Value.Should().Be(1);
        sheet.Stamp.Value.Should().Be(1);
        var before = sheet.Stamp;
        child.Style.SetProperty("opacity", ".6");
        sheet.Stamp.Should().NotBe(before);
        nested.Stamp.Value.Should().Be(1);
        root.Stamp.Value.Should().Be(2);
        sheet.DeleteRule(0);
        before = sheet.Stamp;
        child.Style.SetProperty("opacity", ".7");
        sheet.Stamp.Should().Be(before);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        child.ParentRule.Should().BeSameAs(nested);
        root.ParentStyleSheet.Should().BeNull();
        root.Rules.Should().HaveCount(2);
        var view = root.Rules;
        root.InsertRule("d {}", 2);
        view.Should().BeSameAs(root.Rules);
        view.Should().HaveCount(3);
        sheet.Stamp.Should().Be(before);
        root.DeleteRule(2);
        Assert.Throws<DomException>(() => root.InsertRule("@supports (x:y) {}", -1))!.Name.Should().Be("IndexSizeError");
    }

    [Test]
    public void CharsetIsUnknownAndEofRecoveredSelectorsRoundTrip()
    {
        var sheet = CssStyleSheet.Parse("@charset \"utf-8\"; a {}");
        sheet.Rules.Count.Should().Be(1);
        Assert.Throws<DomException>(() => sheet.InsertRule("@charset \"utf-8\";", 0))!.Name.Should().Be("SyntaxError");
        var rule = (CssStyleRule) sheet.Rules[0];
        rule.SetSelectorText(":is(.a");
        rule.SelectorText.Should().Be(":is(.a)");
        CssStyleSheet.Parse(rule.CssText).Rules.Count.Should().Be(1);
        rule.SetSelectorText("[x=\"a");
        rule.SelectorText.Should().Be("[x=\"a\"]");
        CssStyleSheet.Parse(rule.CssText).Rules.Count.Should().Be(1);
    }

    [TestCase("(color) or f(")]
    [TestCase("(color) or f(\"x")]
    public void EofRecoveredGeneralEnclosedCannotAbsorbSheetRules(string condition)
    {
        var sheet = CssStyleSheet.Parse("@media all {a {opacity:.5}} b{}");
        var media = (CssMediaRule) sheet.Rules[0];
        media.Media.SetMediaText(condition);
        media.Media.Matches(new CssMediaEnvironment()).Should().BeTrue();
        var reparsed = CssStyleSheet.Parse(sheet.Serialize());
        reparsed.Rules.Count.Should().Be(2);
        ((CssMediaRule) reparsed.Rules[0]).Rules.Count.Should().Be(1);
        ((CssMediaRule) reparsed.Rules[0]).Media.Matches(new CssMediaEnvironment()).Should().BeTrue();
    }

    [Test]
    public void CallerWorkAndSelectorEnvironmentReachNativeConsumers()
    {
        var sheet = CssStyleSheet.Parse("input:focus {display:block}");
        var document = Document.CreateHtml();
        var element = document.CreateElement("input");
        document.AppendChild(element);
        var work = new SelectorMatchWork(document, default);
        var rule = (CssStyleRule) sheet.Rules[0];
        rule.TryMatch(element, out _, null, new SelectorEnvironment(document, element, null, null), ref work).Should().BeTrue();
        var checkpoints = 0;
        sheet.SerializeWithRanges(new CssValueWork(default, () => checkpoints++)).Ranges.Count.Should().Be(1);
        checkpoints.Should().BeGreaterThan(0);
    }

    [Test]
    public void ActiveRulesFollowMediaDisabledAndExplicitAttachmentChanges()
    {
        var sheet = CssStyleSheet.Parse("a {} @media screen { b {} @media (width:1px) { c {} } } @media print {d {}}");
        var device = new CssMediaEnvironment();
        var work = new CssValueWork(default);
        sheet.ApplicableStyleRules(device, work).Select(x => x.SelectorText).Should().Equal("a", "b");
        sheet.ApplicableStyleRules(device with { Type = "print" }, work).Select(x => x.SelectorText).Should().Equal("a", "d");
        sheet.Media.SetMediaText("print");
        sheet.ApplicableStyleRules(device, work).Should().BeEmpty();
        sheet.Media.SetMediaText("");
        sheet.Disabled = true;
        sheet.ApplicableStyleRules(device, work).Should().BeEmpty();
        sheet.Disabled = false;
        var before = sheet.Stamp;
        var attachment = new CssStyleSheetAttachment { BaseUrl = new Uri("https://example.test/css/"), SourceUrl = new Uri("https://example.test/css/style.css") };
        sheet.SetAttachment(attachment);
        sheet.Attachment.Should().BeSameAs(attachment);
        sheet.Stamp.Should().NotBe(before);
        before = sheet.Stamp;
        sheet.SetAttachment(attachment with { });
        sheet.Stamp.Should().Be(before);
    }

    [Test]
    public void WholeSheetReplacementPreservesTheLiveViewAndIsAtomic()
    {
        var sheet = CssStyleSheet.Parse("@media screen { a {} }");
        var view = sheet.Rules;
        var old = (CssMediaRule) view[0];
        var before = sheet.Stamp;
        Assert.Throws<CssIncompleteRuleGrammarException>(() => sheet.ReplaceText("@media screen { @container (width > 1px) {} }"));
        sheet.Stamp.Should().Be(before);
        view[0].Should().BeSameAs(old);
        sheet.ReplaceText("b {}");
        view.Should().BeSameAs(sheet.Rules);
        ((CssStyleRule) view[0]).SelectorText.Should().Be("b");
        old.ParentStyleSheet.Should().BeNull();
        before = sheet.Stamp;
        ((CssStyleRule) old.Rules[0]).Style.SetProperty("opacity", ".2");
        sheet.Stamp.Should().Be(before);
    }

    [Test]
    public void DeepGroupsBuildAttachSerializeAndMutateWithoutRecursion()
    {
        var source = string.Concat(Enumerable.Repeat("@media screen {", 1200)) + "a {}" + new string('}', 1200);
        var sheet = CssStyleSheet.Parse(source);
        CssRule rule = sheet.Rules[0];
        for (var i = 0; i < 1200; i++) rule = ((CssMediaRule) rule).Rules[0];
        ((CssStyleRule) rule).Style.SetProperty("opacity", ".5");
        sheet.Stamp.Value.Should().Be(1);
        sheet.SerializeWithRanges().Ranges.Count.Should().Be(1201);
    }
}

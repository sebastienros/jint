using Jint.Browser.Styling;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssParsingTests
{
    private static CssValueWork Work() => new(default);
    private static CssStyleSheet Parse(string source) => NativeCssParsing.CreateSheet(source, Work());
    private static CssRuleList Read(CssStyleSheet sheet) => NativeCssParsing.ReadRules(sheet.Rules, Work());
    private static CssRuleList Read(CssRule rule) => NativeCssParsing.ReadRules(rule.Rules, Work());
    private static CssStyleRule[] Applicable(CssStyleSheet sheet, string media = "screen") =>
        NativeCssParsing.ApplicableRules(sheet, new CssMediaEnvironment { Type = media }, Work()).OfType<CssStyleRule>().ToArray();

    [Test]
    public void ImportsDoNotDemandUnrelatedRuleGrammarsAndKeepTheirIdentity()
    {
        var sheet = Parse("@import 'child.css' (width > 100px); @scope (.unused) {}");
        sheet.Rules.Should().BeEmpty("model getters do not invoke parsers");
        var import = (CssImportRule) NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().Single();
        import.Href.Should().Be("child.css");
        import.Media.Count.Should().Be(0);
        import.ParentStyleSheet.Should().BeSameAs(sheet);
        NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().Single().Should().BeSameAs(import);
        var stamp = sheet.Stamp;
        Read(sheet)[1].Should().BeOfType<CssGenericRule>();
        sheet.Stamp.Should().Be(stamp);
        NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().Single().Should().BeSameAs(import);
    }

    [TestCase("p { @scope (.unused) {} }")]
    [TestCase("@media print { @scope (.unused) {} }")]
    [TestCase("@supports (color:red) { @scope (.unused) {} }")]
    public void ImportDiscoveryStopsAtTheFirstValidRuleInsteadOfInterpretingItsBody(string barrier)
    {
        var sheet = Parse("@import 'valid.css'; " + barrier + " @scope (.unused) {} @import 'ignored.css';");
        var imports = NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().ToArray();
        imports.Select(import => import.Href).Should().Equal("valid.css");
        NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().Should().Equal(imports);
    }

    [TestCase("@media print")]
    [TestCase("@supports (unknown-property:value)")]
    public void FalseConditionsLeaveTheirBodiesUnparsedUntilCssomDemandsThem(string condition)
    {
        var sheet = Parse(condition + " { @scope (.unused) {} } p { color:red }");
        Applicable(sheet).Select(rule => rule.SelectorText).Should().Equal("p");
        var group = (CssGroupingRule) sheet.Rules[0];
        group.Rules.Should().BeEmpty();
        var stamp = sheet.Stamp;
        Read(group).Single().Should().BeOfType<CssGenericRule>();
        group.Rules.Count.Should().Be(1);
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void ChangedMediaDemandsTheBodyWithTheNewOperationsWork()
    {
        var sheet = Parse("@media print { @scope (.unused) {} }");
        Applicable(sheet).Should().BeEmpty();
        Applicable(sheet, "print").Should().BeEmpty();
        sheet.Rules[0].Rules.Single().Should().BeOfType<CssGenericRule>();
    }

    [Test]
    public void MediaParsingUsesCurrentWorkAndAnExplicitSetterWinsOverTheOldSource()
    {
        var rule = (CssMediaRule) Read(Parse("@media (width >= 1px) { p {} }"))[0];
        rule.Media.Count.Should().Be(0);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => NativeCssParsing.ReadMedia(rule.Media, new CssValueWork(cancellation.Token)));
        rule.Media.Count.Should().Be(0);
        rule.Media.SetMediaText("print");
        NativeCssParsing.ReadMedia(rule.Media, Work()).MediaText.Should().Be("print");
        rule.Media.Matches(new CssMediaEnvironment()).Should().BeFalse();
        rule.Media.Matches(new CssMediaEnvironment { Type = "print" }).Should().BeTrue();
    }

    [Test]
    public void ReentrantMediaReadsFailWithoutPoisoningTheCache()
    {
        var media = ((CssMediaRule) Read(Parse("@media screen {}"))[0]).Media;
        var work = new CssValueWork(default, () => NativeCssParsing.ReadMedia(media, Work()));
        Assert.Throws<InvalidOperationException>(() => NativeCssParsing.ReadMedia(media, work));
        media.Count.Should().Be(0);
        NativeCssParsing.ReadMedia(media, Work()).MediaText.Should().Be("screen");
    }

    [Test]
    public void AReplacementDuringMediaParsingCannotPublishTheOldCondition()
    {
        var media = ((CssMediaRule) Read(Parse("@media screen {}"))[0]).Media;
        var replaced = false;
        var work = new CssValueWork(default, () =>
        {
            if (replaced) return;
            replaced = true;
            NativeCssParsing.SetMediaSource(media, "print", Work());
        });
        Assert.Throws<InvalidOperationException>(() => NativeCssParsing.ReadMedia(media, work));
        NativeCssParsing.ReadMedia(media, Work()).MediaText.Should().Be("print");
    }

    [Test]
    public void DescendantsParsedAfterDetachmentKeepHistoricalLinksButCannotChangeTheSheet()
    {
        var sheet = Parse("@media screen { @supports (color:red) { p { color:red } } }");
        var root = (CssMediaRule) Read(sheet)[0];
        root.Rules.Should().BeEmpty();
        sheet.DeleteRule(0);
        var nested = (CssSupportsRule) Read(root)[0];
        var child = (CssStyleRule) Read(nested)[0];
        nested.ParentRule.Should().BeSameAs(root);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        root.ParentStyleSheet.Should().BeNull();
        var stamp = sheet.Stamp;
        child.Style.SetProperty("color", "blue");
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void ReplacementDetachesPreviouslyDiscoveredImportsWithoutParsingUndemandedRules()
    {
        var sheet = Parse("@import 'old.css'; @scope (.unused) {}");
        var list = sheet.Rules;
        var old = (CssImportRule) NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().Single();
        NativeCssParsing.ReplaceSource(sheet, "@import 'new.css'; p {}", Work());
        sheet.Rules.Should().BeSameAs(list);
        old.ParentStyleSheet.Should().BeNull();
        var current = (CssImportRule) NativeCssParsing.ImportRules(sheet, Work()).OfType<CssImportRule>().Single();
        current.Href.Should().Be("new.css");
        Read(sheet)[0].Should().BeSameAs(current);
        list.Count.Should().Be(2);
    }

    [Test]
    public void EveryRuleParsingCheckpointCanBeCanceledAndRetriedWithoutPublishingPartialLists()
    {
        const string text = "@media screen { a {} b {} @supports (color:red) { c {} } }";
        var baseline = (CssMediaRule) Read(Parse(text))[0];
        var calls = 0;
        NativeCssParsing.ReadRules(baseline.Rules, new CssValueWork(default, () => calls++));
        calls.Should().BeGreaterThan(0);
        for (var target = 1; target <= calls; target++)
        {
            var sheet = Parse(text);
            var root = (CssMediaRule) Read(sheet)[0];
            var stamp = sheet.Stamp;
            using var cancellation = new CancellationTokenSource();
            var checks = 0;
            var work = new CssValueWork(cancellation.Token, () => { if (++checks == target) cancellation.Cancel(); });
            Assert.Throws<OperationCanceledException>(() => NativeCssParsing.ReadRules(root.Rules, work));
            root.Rules.Should().BeEmpty();
            sheet.Stamp.Should().Be(stamp);
            Read(root).Count.Should().Be(3);
            root.Rules.All(rule => ReferenceEquals(rule.ParentRule, root) &&
                ReferenceEquals(rule.ParentStyleSheet, sheet)).Should().BeTrue();
        }
    }

    [Test]
    public void ACompletedParseDoesNotRetainTheOriginalCancellationOrCheckpoint()
    {
        using var cancellation = new CancellationTokenSource();
        var active = true;
        var work = new CssValueWork(cancellation.Token, () => active.Should().BeTrue());
        var sheet = NativeCssParsing.CreateSheet("@media screen { p { color:red } }", work);
        active = false;
        cancellation.Cancel();
        Applicable(sheet).Single().Style.GetPropertyValue("color").Should().Be("red");
    }

    [Test]
    public void AReplacementDuringParsingCannotRestoreTheOldSource()
    {
        var sheet = Parse("p {}");
        var replaced = false;
        var work = new CssValueWork(default, () =>
        {
            if (replaced) return;
            replaced = true;
            NativeCssParsing.ReplaceSource(sheet, "a {}", Work());
        });
        Assert.Throws<InvalidOperationException>(() => NativeCssParsing.ReadRules(sheet.Rules, work));
        ((CssStyleRule) Read(sheet).Single()).SelectorText.Should().Be("a");
    }

    [Test]
    public void NegativeIndicesDoNotDemandUnparsedRuleGrammars()
    {
        var sheet = Parse("@media print { @scope (.unused) {} } @scope (.unused) {}");
        Assert.Throws<DomException>(() => sheet.InsertRule("p {}", -1))!.Name.Should().Be("IndexSizeError");
        Assert.Throws<DomException>(() => sheet.DeleteRule(-1))!.Name.Should().Be("IndexSizeError");
        sheet.Rules.Should().BeEmpty();
        var group = (CssGroupingRule) Read(Parse("@media print { @scope (.unused) {} }"))[0];
        Assert.Throws<DomException>(() => group.InsertRule("p {}", -1))!.Name.Should().Be("IndexSizeError");
        Assert.Throws<DomException>(() => group.DeleteRule(-1))!.Name.Should().Be("IndexSizeError");
        group.Rules.Should().BeEmpty();
    }
}

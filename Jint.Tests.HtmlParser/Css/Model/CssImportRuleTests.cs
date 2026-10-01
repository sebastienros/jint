#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssImportRuleTests
{
    private static CssValueWork Work() => new(default);
    private static CssImportRule Import(CssStyleSheet sheet, int index = 0) => (CssImportRule) sheet.Rules[index];

    [TestCase("@import 'a\\2e css';", "a.css")]
    [TestCase("@IMPORT url(a\\2e css);", "a.css")]
    [TestCase("@\\69mport u\\72l(\"a\\2e css\");", "a.css")]
    [TestCase("@import '';", "")]
    [TestCase("@import url();", "")]
    public void UrlsDecodeSpecifiedTextAndExposeRealImportRules(string source, string href)
    {
        var sheet = CssStyleSheet.Parse(source);
        var rule = Import(sheet);
        rule.Type.Should().Be(CssRuleType.Import);
        ((int) rule.Type).Should().Be(3);
        rule.Href.Should().Be(href);
        rule.StyleSheet.Should().BeNull();
        rule.ParentStyleSheet.Should().BeSameAs(sheet);
        rule.ParentRule.Should().BeNull();
        sheet.Serialize().Should().Be("@import url(\"" + href + "\");");
    }

    // CSS Syntax §3.2: an encoding declaration is not a rule, so it neither appears in cssRules nor
    // closes import placement, and insertRule() rejects it.
    [Test]
    public void CharsetDeclarationIsNotARule()
    {
        var sheet = CssStyleSheet.Parse("@charset \"utf-8\"; @CHARSET 'x'; @import 'ok.css'; a {}");
        sheet.Rules.Count.Should().Be(2);
        Import(sheet).Href.Should().Be("ok.css");
        Assert.Throws<DomException>(() => sheet.InsertRule("@charset \"utf-8\";", 0))!.Name.Should().Be("SyntaxError");
    }

    [Test]
    public void InvalidStatementsAndInvalidSelectorsRecoverWithoutClosingImportPlacement()
    {
        var sheet = CssStyleSheet.Parse("@import; @import url(a b); @import url(a) {} ??? {} @import 'ok.css'; a {} @import 'late.css';");
        sheet.Rules.Count.Should().Be(2);
        Import(sheet).Href.Should().Be("ok.css");
        sheet.Rules[1].Should().BeOfType<CssStyleRule>();
        var nested = CssStyleSheet.Parse("@media all { @import 'nested.css'; a {} } b { @import 'nested.css'; }");
        nested.Rules.Count.Should().Be(2);
        nested.Rules[0].Rules.Count.Should().Be(1);
        nested.Rules[0].Rules[0].Should().BeOfType<CssStyleRule>();
        nested.Rules[1].Rules.Count.Should().Be(0);
    }

    [TestCase("layer", "R1:import-prelude-layer")]
    [TestCase("layer(theme)", "R1:import-prelude-layer")]
    [TestCase("supports(display:block)", "R1:import-prelude-supports")]
    public void UnsupportedImportConditionsRemainNamedCompletionBlockers(string condition, string blocker)
    {
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssStyleSheet.Parse("@import 'a' " + condition + ";"))!
            .Blocker.Should().Be(blocker);
        // Invalid late imports are discarded before interpreting their unsupported prelude.
        CssStyleSheet.Parse("a {} @import 'a' " + condition + ";").Rules.Count.Should().Be(1);
        CssStyleSheet.Parse("@namespace 'x';").Rules.Single().Should().BeOfType<CssGenericRule>();
    }

    [Test]
    public void InsertionParsesAfterBoundsAndBeforeHierarchyAndIsAtomic()
    {
        var sheet = CssStyleSheet.Parse("@import 'a'; a {}");
        var stamp = sheet.Stamp;
        Assert.Throws<DomException>(() => sheet.InsertRule("@import;", 3))!.Name.Should().Be("IndexSizeError");
        Assert.Throws<DomException>(() => sheet.InsertRule("@import;", 2))!.Name.Should().Be("SyntaxError");
        Assert.Throws<DomException>(() => sheet.InsertRule("@import 'b';", 2))!.Name.Should().Be("HierarchyRequestError");
        Assert.Throws<DomException>(() => sheet.InsertRule("b {}", 0))!.Name.Should().Be("HierarchyRequestError");
        var group = (CssMediaRule) CssStyleSheet.Parse("@media all {}").Rules[0];
        Assert.Throws<DomException>(() => group.InsertRule("@import;", 0))!.Name.Should().Be("SyntaxError");
        Assert.Throws<DomException>(() => group.InsertRule("@import 'b';", 0))!.Name.Should().Be("HierarchyRequestError");
        sheet.Stamp.Should().Be(stamp);
        sheet.InsertRule("@import 'b';", 1).Should().Be(1);
        sheet.InsertRule("b {}", 2).Should().Be(2);
    }

    [Test]
    public void ChildIdentityMediaAndFinalAttachmentRemainDistinctForRepeatedUrls()
    {
        var sheet = CssStyleSheet.Parse("@import 'same.css' screen and (min-width: 1px); @import 'same.css';");
        var first = Import(sheet);
        var second = Import(sheet, 1);
        var media = first.Media;
        var child = CssStyleSheet.Parse("a { border-color:red; }");
        var other = CssStyleSheet.Parse("b {}");
        var final = new Uri("https://example.test/redirect/child.css");
        first.SetStyleSheet(child, final, final, Work());
        second.SetStyleSheet(other, final, final, Work());
        child.Attachment.OwnerNode.Should().BeNull();
        child.Attachment.ImportOwner.Should().BeSameAs(first);
        child.Attachment.SourceUrl.Should().Be(final);
        child.Attachment.BaseUrl.Should().Be(final);
        first.StyleSheet.Should().BeSameAs(child);
        second.StyleSheet.Should().NotBeSameAs(child);
        child.Media.Should().BeSameAs(media);
        var parentStamp = sheet.Stamp;
        var childStamp = child.Stamp;
        media.SetMediaText("print");
        first.Media.Should().BeSameAs(media);
        child.Media.Should().BeSameAs(media);
        sheet.Stamp.Should().NotBe(parentStamp);
        child.Stamp.Should().NotBe(childStamp);
        // Child declarations are not serialized or materialized by the parent's CSS text.
        sheet.Serialize().Should().Be("@import url(\"same.css\") print;\n@import url(\"same.css\");");
        sheet.SerializeWithRanges().Ranges.Count.Should().Be(2);
    }

    [Test]
    public void ImportedStylesFollowSourceOrderAndInheritScopeThroughAllImportOwners()
    {
        var document = Document.CreateHtml();
        var owner = document.CreateElement("style");
        var host = document.CreateElement("div");
        var shadow = ShadowTree.Attach(host, new ShadowRootInit(ShadowRootMode.Open), default);
        shadow.AppendChild(owner);
        var root = CssStyleSheet.Parse("@import 'one'; @import 'two'; root {}");
        root.SetAttachment(new CssStyleSheetAttachment { OwnerNode = owner });
        var one = CssStyleSheet.Parse("@import 'deep'; one {}");
        var deep = CssStyleSheet.Parse("deep {}");
        var two = CssStyleSheet.Parse("two {}");
        Import(root).SetStyleSheet(one, null, null, Work());
        Import(one).SetStyleSheet(deep, null, null, Work());
        Import(root, 1).SetStyleSheet(two, null, null, Work());
        root.ApplicableStyleRules(new CssMediaEnvironment(), Work()).Select(rule => rule.SelectorText)
            .Should().Equal("deep", "one", "two", "root");
        deep.EffectiveOwnerNode(Work()).Should().BeSameAs(owner);
        deep.EffectiveOwnerNode(Work())!.TreeShadowRoot.Should().BeSameAs(shadow);
        var revisions = root.ImportedStyleSheets(Work()).ToDictionary(sheet => sheet, sheet => sheet.Stamp);
        deep.InsertRule("new {}", 1);
        revisions[root].Should().Be(root.Stamp);
        revisions[deep].Should().NotBe(deep.Stamp);
        two.Disabled = true;
        root.ImportedStyleSheets(Work()).Should().Contain(two);
        root.ApplicableStyleRules(new CssMediaEnvironment(), Work()).Select(rule => rule.SelectorText)
            .Should().Equal("deep", "new", "one", "root");
        one.Media.SetMediaText("print");
        root.ApplicableStyleRules(new CssMediaEnvironment(), Work()).Select(rule => rule.SelectorText)
            .Should().Equal("root");
    }

    [Test]
    public void RemovingImportPreservesHistoricalChildOwnershipAndStopsParentNotifications()
    {
        var sheet = CssStyleSheet.Parse("@import 'a';");
        var rule = Import(sheet);
        var child = CssStyleSheet.Parse("a {}");
        rule.SetStyleSheet(child, null, null, Work());
        sheet.DeleteRule(0);
        var stamp = sheet.Stamp;
        rule.ParentStyleSheet.Should().BeNull();
        rule.StyleSheet.Should().BeSameAs(child);
        child.Attachment.ImportOwner.Should().BeSameAs(rule);
        child.Rules[0].ParentStyleSheet.Should().BeSameAs(child);
        rule.Media.SetMediaText("print");
        child.InsertRule("b {}", 1);
        sheet.Stamp.Should().Be(stamp);
        sheet.ImportedStyleSheets(Work()).Should().Equal(sheet);
    }

    [Test]
    public void PublishedChildCannotBeRetargetedThroughRetainedImportAndMediaWrappers()
    {
        var sheet = CssStyleSheet.Parse("@import 'a';");
        var import = Import(sheet);
        var child = CssStyleSheet.Parse("a {}");
        import.SetStyleSheet(child, null, null, Work());
        var media = child.Media;
        Assert.Throws<InvalidOperationException>(() => child.SetAttachment(new CssStyleSheetAttachment()));
        Assert.Throws<InvalidOperationException>(() => child.SetAttachment(new CssStyleSheetAttachment
        { ImportOwner = import, OwnerNode = Document.CreateHtml().CreateElement("style") }));
        var stamp = sheet.Stamp;
        Assert.Throws<InvalidOperationException>(() => import.SetStyleSheet(null, null, null, Work()));
        Assert.Throws<InvalidOperationException>(() => import.SetStyleSheet(CssStyleSheet.Parse("b {}"), null, null, Work()));
        import.StyleSheet.Should().BeSameAs(child);
        child.Media.Should().BeSameAs(media);
        child.Attachment.ImportOwner.Should().BeSameAs(import);
        sheet.Stamp.Should().Be(stamp);
        sheet.ReplaceText("@import 'new';");
        var afterReplacement = sheet.Stamp;
        media.SetMediaText("print");
        child.Media.Should().BeSameAs(import.Media);
        sheet.Stamp.Should().Be(afterReplacement);
        Import(sheet).StyleSheet.Should().BeNull();
    }

    [Test]
    public void MalformedMediaRecoversToNeverMatchingAndEofUrlRecoveryStaysSupported()
    {
        var import = Import(CssStyleSheet.Parse("@import 'a' screen and;"));
        import.Media.MediaText.Should().Be("not all");
        import.Media.Matches(new CssMediaEnvironment()).Should().BeFalse();
        Import(CssStyleSheet.Parse("@import url(\"a.css\"")).Href.Should().Be("a.css");
        Import(CssStyleSheet.Parse("@import 'a.css")).Href.Should().Be("a.css");
    }

    [Test]
    public void DeepImportGraphsUseIterativeTraversalAndCancelledPublicationIsAtomic()
    {
        const int depth = 5000;
        var root = CssStyleSheet.Parse("@import 'child';");
        var tail = root;
        for (var i = 0; i < depth; i++)
        {
            var child = CssStyleSheet.Parse(i == depth - 1 ? "end {}" : "@import 'child';");
            Import(tail).SetStyleSheet(child, null, null, Work());
            tail = child;
        }
        root.ImportedStyleSheets(Work()).Length.Should().Be(depth + 1);
        root.ApplicableStyleRules(new CssMediaEnvironment(), Work()).Single().SelectorText.Should().Be("end");
        tail.EffectiveOwnerNode(Work()).Should().BeNull();
        var target = CssStyleSheet.Parse("@import 'candidate';");
        var targetStamp = target.Stamp;
        var childStamp = root.Stamp;
        var checks = 0;
        var cancelled = new CssValueWork(default, () => { if (++checks == 3) throw new OperationCanceledException(); });
        Assert.Throws<OperationCanceledException>(() => Import(target).SetStyleSheet(root, null, null, cancelled));
        Import(target).StyleSheet.Should().BeNull();
        target.Stamp.Should().Be(targetStamp);
        root.Stamp.Should().Be(childStamp);
        root.Attachment.ImportOwner.Should().BeNull();
    }

    [Test]
    public void WideImportOccurrencesKeepSeparateChildrenAndBoundTraversalWork()
    {
        const int width = 2000;
        var source = string.Concat(Enumerable.Repeat("@import 'same';", width));
        var root = CssStyleSheet.Parse(source);
        foreach (var rule in root.Rules)
            ((CssImportRule) rule).SetStyleSheet(CssStyleSheet.Parse("a {}"), null, null, Work());
        root.ImportedStyleSheets(Work()).Length.Should().Be(width + 1);
        root.ApplicableStyleRules(new CssMediaEnvironment(), Work()).Length.Should().Be(width);
        var checks = 0;
        var work = new CssValueWork(default, () => { if (++checks == 3) throw new OperationCanceledException(); });
        Assert.Throws<OperationCanceledException>(() => root.ApplicableStyleRules(new CssMediaEnvironment(), work));
    }

    [Test]
    public void CheckpointCompetingAttachmentCannotPublishAChildWithAnotherOwner()
    {
        var outer = Import(CssStyleSheet.Parse("@import 'outer';"));
        var competitor = Import(CssStyleSheet.Parse("@import 'competitor';"));
        var candidate = CssStyleSheet.Parse("a {}");
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (++checks == 2) competitor.SetStyleSheet(candidate, null, null, Work());
        });
        Assert.Throws<InvalidOperationException>(() => outer.SetStyleSheet(candidate, null, null, work));
        outer.StyleSheet.Should().BeNull();
        competitor.StyleSheet.Should().BeSameAs(candidate);
        candidate.Attachment.ImportOwner.Should().BeSameAs(competitor);
        candidate.Media.Should().BeSameAs(competitor.Media);
        candidate.Media.Should().NotBeSameAs(outer.Media);
    }

    [Test]
    public void CheckpointReentryOnSameImportCannotRetargetItsPublishedChild()
    {
        var import = Import(CssStyleSheet.Parse("@import 'outer';"));
        var candidate = CssStyleSheet.Parse("a {}");
        var winner = CssStyleSheet.Parse("b {}");
        var candidateStamp = candidate.Stamp;
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (++checks == 2) import.SetStyleSheet(winner, null, null, Work());
        });
        Assert.Throws<InvalidOperationException>(() => import.SetStyleSheet(candidate, null, null, work));
        import.StyleSheet.Should().BeSameAs(winner);
        winner.Attachment.ImportOwner.Should().BeSameAs(import);
        winner.Media.Should().BeSameAs(import.Media);
        candidate.Attachment.ImportOwner.Should().BeNull();
        candidate.Stamp.Should().Be(candidateStamp);
    }

    [Test]
    public void CheckpointCandidateDescendantMutationInvalidatesAttachmentSnapshot()
    {
        var import = Import(CssStyleSheet.Parse("@import 'outer';"));
        var candidate = CssStyleSheet.Parse("@import 'child';");
        var descendant = CssStyleSheet.Parse("a {}");
        Import(candidate).SetStyleSheet(descendant, null, null, Work());
        var candidateStamp = candidate.Stamp;
        var importStamp = import.Stamp;
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (++checks == 2) descendant.InsertRule("b {}", 1);
        });
        Assert.Throws<InvalidOperationException>(() => import.SetStyleSheet(candidate, null, null, work));
        import.StyleSheet.Should().BeNull();
        import.Stamp.Should().Be(importStamp);
        candidate.Attachment.ImportOwner.Should().BeNull();
        candidate.Stamp.Should().Be(candidateStamp);
        descendant.Rules.Count.Should().Be(2);
    }

    [Test]
    public void CheckpointParentReplacementInvalidatesImportBeforePublication()
    {
        var parent = CssStyleSheet.Parse("@import 'outer';");
        var import = Import(parent);
        var candidate = CssStyleSheet.Parse("a {}");
        var checks = 0;
        var work = new CssValueWork(default, () =>
        {
            if (++checks == 2) parent.ReplaceText("b {}");
        });
        Assert.Throws<InvalidOperationException>(() => import.SetStyleSheet(candidate, null, null, work));
        import.StyleSheet.Should().BeNull();
        import.ParentStyleSheet.Should().BeNull();
        candidate.Attachment.ImportOwner.Should().BeNull();
        parent.Rules[0].Should().BeOfType<CssStyleRule>();
    }

    [Test]
    public void CyclesAndSharedChildIdentitiesAreRejectedBeforePublication()
    {
        var root = CssStyleSheet.Parse("@import 'a'; @import 'b';");
        var child = CssStyleSheet.Parse("@import 'root';");
        Import(root).SetStyleSheet(child, null, null, Work());
        Assert.Throws<InvalidOperationException>(() => Import(root, 1).SetStyleSheet(child, null, null, Work()));
        Assert.Throws<InvalidOperationException>(() => Import(child).SetStyleSheet(root, null, null, Work()));
        Import(child).StyleSheet.Should().BeNull();
        root.ImportedStyleSheets(Work()).Length.Should().Be(2);
    }
}

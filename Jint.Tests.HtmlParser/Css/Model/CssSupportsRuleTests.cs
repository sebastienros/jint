#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Conditions;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssSupportsRuleTests
{
    [TestCase("(color:red)", true)]
    [TestCase("(border-color:red)", true)]
    [TestCase("(box-shadow:none)", true)]
    [TestCase("(unknown-property:anything)", false)]
    [TestCase("not (border-color:red)", false)]
    [TestCase("future-feature(anything)", false)]
    [TestCase("not future-feature(anything)", true)]
    [TestCase("selector(div > .a)", true)]
    [TestCase("selector(:unknown)", false)]
    [TestCase("(color:red) and ((width:1px) or (not-a-property:bogus))", true)]
    [TestCase("(color:red) or ()", true)]
    public void CapabilityQueriesRetainUnsimplifiedConditionsAndRealChildren(string condition, bool expected)
    {
        var sheet = CssStyleSheet.Parse("@supports " + condition + " { a { box-shadow:none } }");
        var rule = (CssSupportsRule) sheet.Rules[0];
        rule.Type.Should().Be(CssRuleType.Supports);
        rule.ConditionText.Should().Be(condition);
        rule.Matches.Should().Be(expected);
        rule.Rules.Count.Should().Be(1);
        var child = (CssStyleRule) rule.Rules[0];
        child.ParentRule.Should().BeSameAs(rule);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default))
            .Should().Equal(expected ? new[] { child } : Array.Empty<CssStyleRule>());
        // Parsing and condition filtering keep child declaration values cold, even when active.
        child.Style.GetPropertyValue("box-shadow").Should().Be("none");
    }

    [TestCase("")]
    [TestCase("color:red")]
    [TestCase("(color:red) or (width:1px) and (display:block)")]
    [TestCase("(color:red) or (width:1px) trailing")]
    [TestCase("not (color:red) and (display:block)")]
    [TestCase("(color:red) or )")]
    public void InvalidPreludesDropTheirBodiesAndInsertionFailsAtomically(string condition)
    {
        var source = "@supports " + condition + " { @scope unknown {} }";
        var sheet = CssStyleSheet.Parse(source + " a {}");
        sheet.Rules.Count.Should().Be(1);
        var old = sheet.Rules[0];
        var stamp = sheet.Stamp;
        Assert.Throws<DomException>(() => sheet.InsertRule(source, 0))!.Name.Should().Be("SyntaxError");
        sheet.Rules[0].Should().BeSameAs(old);
        sheet.Stamp.Should().Be(stamp);
        if (condition == "color:red")
            CssSupports.EvaluateCondition(condition, null, new CssValueWork(default)).Should().BeTrue();
    }

    [Test]
    public void FalseConditionsDoNotHideKnownUnimplementedChildGrammars()
    {
        var sheet = CssStyleSheet.Parse("@supports (unknown-property:none) { @scope unknown {} }");
        sheet.Rules[0].Rules.Single().Should().BeOfType<CssGenericRule>();
        Assert.Throws<CssIncompleteRuleGrammarException>(() =>
            CssStyleSheet.Parse("a { @supports (color:red) { & {} } }"))!
            .Blocker.Should().Be("C2:nesting-selector-context");
    }

    [Test]
    public void MixedConditionGroupsAndNestedStyleRulesKeepOrderAndContext()
    {
        var sheet = CssStyleSheet.Parse("a {} @supports (color:red) { b { & > c {} } " +
            "@media screen { d {} @supports not (color:red) { e {} } f {} } g {} } h {}");
        var device = new CssMediaEnvironment();
        sheet.ApplicableStyleRules(device, new CssValueWork(default)).Select(rule => rule.SelectorText)
            .Should().Equal("a", "b", "& > c", "d", "f", "g", "h");
        sheet.ApplicableStyleRules(device with { Type = "print" }, new CssValueWork(default))
            .Select(rule => rule.SelectorText).Should().Equal("a", "b", "& > c", "g", "h");
        var supports = (CssSupportsRule) sheet.Rules[1];
        var parent = (CssStyleRule) supports.Rules[0];
        var child = (CssStyleRule) parent.Rules[0];
        var document = Document.CreateHtml();
        var b = document.CreateElement("b");
        var c = document.CreateElement("c");
        b.AppendChild(c);
        child.TryMatch(c, out _).Should().BeTrue();
        child.TryMatch(document.CreateElement("c"), out _).Should().BeFalse();
    }

    [TestCase("@supports (color:red) { a {opacity:.5} @media screen { b {} } } c {}")]
    [TestCase("@supports future(foo) { a {}")]
    [TestCase("@supports (color:red) { a {}")]
    public void SerializationPreservesConditionsChildrenAndExactRangesAfterEofRecovery(string source)
    {
        var sheet = CssStyleSheet.Parse(source);
        var snapshot = sheet.SerializeWithRanges();
        foreach (var entry in snapshot.Ranges)
            snapshot.Text[entry.Value.Start..entry.Value.End].Should().Be(entry.Key.CssText);
        CssStyleSheet.Parse(snapshot.Text).Serialize().Should().Be(snapshot.Text);
        var root = (CssSupportsRule) sheet.Rules[0];
        ((CssSupportsRule) CssStyleSheet.Parse(snapshot.Text).Rules[0]).Matches.Should().Be(root.Matches);
    }

    [Test]
    public void LiveChildrenStampsAndDetachedOwnershipFollowTheRemovedRoot()
    {
        var sheet = CssStyleSheet.Parse("@supports (color:red) { @media screen { a {} } }");
        var root = (CssSupportsRule) sheet.Rules[0];
        var nested = (CssMediaRule) root.Rules[0];
        var child = (CssStyleRule) nested.Rules[0];
        var view = root.Rules;
        root.InsertRule("b {}", 1);
        root.Rules.Should().BeSameAs(view);
        view.Count.Should().Be(2);
        child.Style.SetProperty("opacity", ".5");
        root.Stamp.Value.Should().Be(2);
        sheet.Stamp.Value.Should().Be(2);
        sheet.DeleteRule(0);
        root.ParentRule.Should().BeNull();
        root.ParentStyleSheet.Should().BeNull();
        child.ParentRule.Should().BeSameAs(nested);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        var stamp = sheet.Stamp;
        child.Style.SetProperty("opacity", ".6");
        nested.Media.SetMediaText("print");
        root.InsertRule("c {}", 2);
        root.DeleteRule(1);
        sheet.Stamp.Should().Be(stamp);
        view.Count.Should().Be(2);
    }

    [Test]
    public void BoundsWinOverParseAndCanceledWorkInBothGroupKinds()
    {
        var sheet = CssStyleSheet.Parse("@supports (color:red) {} @media screen {}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        foreach (var group in sheet.Rules.Cast<CssGroupingRule>())
        {
            var work = new CssValueWork(cancellation.Token);
            Assert.Throws<DomException>(() => group.InsertRule("@scope unknown {}", -1, null, work, cancellation.Token))!
                .Name.Should().Be("IndexSizeError");
            Assert.Throws<DomException>(() => group.DeleteRule(0, work))!.Name.Should().Be("IndexSizeError");
            group.Stamp.Value.Should().Be(0);
        }
    }

    [Test]
    public void CanceledInsertReplaceAndDeletePreserveListsLinksAndStamps()
    {
        var sheet = CssStyleSheet.Parse("@supports (color:red) { a {} }");
        var root = (CssSupportsRule) sheet.Rules[0];
        var old = root.Rules[0];
        var stamp = sheet.Stamp;
        var source = "@supports (color:red) {" + string.Concat(Enumerable.Repeat("a {}", 5000)) + "}";
        using var insertion = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(insertion.Token, () => { if (++checks == 8) insertion.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => root.InsertRule(source, 0, null, work, insertion.Token));
        root.Rules.Count.Should().Be(1);
        root.Rules[0].Should().BeSameAs(old);
        old.ParentRule.Should().BeSameAs(root);
        root.Stamp.Value.Should().Be(0);
        sheet.Stamp.Should().Be(stamp);
        using var replacement = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(replacement.Token, () => { if (++checks == 8) replacement.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => sheet.ReplaceText(source, null, work, replacement.Token));
        sheet.Rules[0].Should().BeSameAs(root);
        sheet.Stamp.Should().Be(stamp);
        sheet.ReplaceText(source);
        root = (CssSupportsRule) sheet.Rules[0];
        stamp = sheet.Stamp;
        using var deletion = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(deletion.Token, () => { if (++checks == 2) deletion.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => sheet.DeleteRule(0, work));
        sheet.Rules[0].Should().BeSameAs(root);
        root.ParentStyleSheet.Should().BeSameAs(sheet);
        root.Rules[0].ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void DeepGroupsShareBoundedWorkWithoutRecursion()
    {
        const int depth = 1200;
        var source = string.Concat(Enumerable.Repeat("@supports (color:red) {", depth)) + "a {}" + new string('}', depth);
        var sheet = CssStyleSheet.Parse(source);
        sheet.SerializeWithRanges().Ranges.Count.Should().Be(depth + 1);
        sheet.ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default)).Length.Should().Be(1);
        var root = sheet.Rules[0];
        var leaf = root;
        while (leaf.Rules.Count != 0) leaf = leaf.Rules[0];
        ((CssStyleRule) leaf).Style.SetProperty("opacity", ".5");
        sheet.Stamp.Value.Should().Be(1);
        sheet.DeleteRule(0);
        leaf.ParentStyleSheet.Should().BeSameAs(sheet);
        var stamp = sheet.Stamp;
        ((CssStyleRule) leaf).Style.SetProperty("opacity", ".6");
        sheet.Stamp.Should().Be(stamp);
        var options = new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 2 } };
        Assert.Throws<ParseLimitException>(() => CssStyleSheet.Parse(source, options));
    }
}

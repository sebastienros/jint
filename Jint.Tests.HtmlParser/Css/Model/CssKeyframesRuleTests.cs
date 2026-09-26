#nullable enable
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssKeyframesRuleTests
{
    [TestCase("from", "0%")]
    [TestCase("TO", "100%")]
    [TestCase("from, 50.00%, 5e1%, to, from", "0%, 50%, 50%, 100%, 0%")]
    [TestCase("+.50%, -0%, 0e999999999999999999999%", "0.5%, 0%, 0%")]
    [TestCase("100.00000000000000000000000000000000000%", "100%")]
    [TestCase("1e-999999999999999999999%", "1e-999999999999999999999%")]
    public void ClassicKeysNormalizeWhilePreservingOrderAndMultiplicity(string keys, string expected)
    {
        var sheet = CssStyleSheet.Parse("@keyframes fade { " + keys + " { opacity:.5 } }");
        var rule = (CssKeyframesRule) sheet.Rules[0];
        rule.Type.Should().Be(CssRuleType.Keyframes);
        rule.Name.Should().Be("fade");
        var child = (CssKeyframeRule) rule.Rules[0];
        child.Type.Should().Be(CssRuleType.Keyframe);
        child.KeyText.Should().Be(expected);
        child.ParentRule.Should().BeSameAs(rule);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        child.Style.GetPropertyValue("opacity").Should().Be("0.5");
        sheet.ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default)).Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("0")]
    [TestCase("-0.000000000000000000000000000001%")]
    [TestCase("100.00000000000000000000000000000000001%")]
    [TestCase("1e999999999999999999999%")]
    [TestCase("from,")]
    [TestCase(",from")]
    [TestCase("from,,to")]
    [TestCase("from to")]
    [TestCase("calc(50%)")]
    [TestCase("50 %")]
    [TestCase("foo 50%")]
    public void InvalidSelectorsDropInSheetsAndAllEditsPreserveState(string keys)
    {
        CssStyleSheet.Parse("@keyframes x { " + keys + " {} to {} }").Rules.Count.Should().Be(1);
        var sheet = CssStyleSheet.Parse("@keyframes x { from {opacity:.5} }");
        var root = (CssKeyframesRule) sheet.Rules[0];
        var child = (CssKeyframeRule) root.Rules[0];
        var stamp = sheet.Stamp;
        root.AppendRule(keys + " {}");
        root.FindRule(keys).Should().BeNull();
        root.DeleteRule(keys);
        Assert.Throws<DomException>(() => child.SetKeyText(keys))!.Name.Should().Be("SyntaxError");
        child.KeyText.Should().Be("0%");
        root.Rules.Count.Should().Be(1);
        root.Rules[0].Should().BeSameAs(child);
        sheet.Stamp.Should().Be(stamp);
        child.Stamp.Value.Should().Be(0);
    }

    [Test]
    public void LastExactOrderedMatchWinsAndAppendNeverMergesRules()
    {
        var sheet = CssStyleSheet.Parse("@keyframes x { from,50% {} 50%,from {} from,5e1% {} from,50%,from {} }");
        var root = (CssKeyframesRule) sheet.Rules[0];
        var list = root.Rules;
        var first = root.Rules[0];
        var last = root.Rules[2];
        root.FindRule("0%, 50.000%").Should().BeSameAs(last);
        root.FindRule("from").Should().BeNull();
        root.DeleteRule("FROM, +50%");
        last.ParentRule.Should().BeNull();
        last.ParentStyleSheet.Should().BeNull();
        root.FindRule("0%,50%").Should().BeSameAs(first);
        root.AppendRule("from, 50% {opacity:.25}");
        root.Rules.Should().BeSameAs(list);
        list.Count.Should().Be(4);
        root.FindRule("from,50%").Should().BeSameAs(list[3]);
        ((CssKeyframeRule) first).SetKeyText("to, to");
        root.FindRule("100%,1e2%").Should().BeSameAs(first);
        sheet.Stamp.Value.Should().Be(3);
    }

    [TestCase("fade", "fade")]
    [TestCase("\\66 ade", "fade")]
    [TestCase("\"none\"", "none")]
    [TestCase("\"a b\"", "a b")]
    [TestCase("\"\"", "")]
    public void NamesParseOnceAndCssomSetterAcceptsStrings(string spelling, string name)
    {
        var sheet = CssStyleSheet.Parse("@keyframes " + spelling + " { from {} }");
        var root = (CssKeyframesRule) sheet.Rules[0];
        root.Name.Should().Be(name);
        root.SetName("inherit");
        sheet.Serialize().Should().StartWith("@keyframes \"inherit\"");
        root.SetName("two words\"}");
        ((CssKeyframesRule) CssStyleSheet.Parse(sheet.Serialize()).Rules[0]).Name.Should().Be(root.Name);
        root.SetName("");
        ((CssKeyframesRule) CssStyleSheet.Parse(sheet.Serialize()).Rules[0]).Name.Should().Be("");
    }

    [TestCase("none")]
    [TestCase("INHERIT")]
    [TestCase("default")]
    [TestCase("revert-layer")]
    [TestCase("revert-rule")]
    [TestCase("x y")]
    [TestCase("50%")]
    public void InvalidAtRuleNamesDropWithoutDemandingTheirBodies(string name)
    {
        var sheet = CssStyleSheet.Parse("@keyframes " + name + " { entry 50% {} } a {}");
        sheet.Rules.Count.Should().Be(1);
        var old = sheet.Rules[0];
        var stamp = sheet.Stamp;
        Assert.Throws<DomException>(() => sheet.InsertRule("@keyframes " + name + " {}", 0))!.Name.Should().Be("SyntaxError");
        sheet.Rules[0].Should().BeSameAs(old);
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void ImportantDeclarationsDisappearBeforeSerializationOrLazyResolution()
    {
        var sheet = CssStyleSheet.Parse("@keyframes x { from { border-color:red !important; opacity:.5; border-color:blue; --x:red !important; --x:blue } }");
        var frame = (CssKeyframeRule) sheet.Rules[0].Rules[0];
        frame.Style.SerializeSource(new CssValueWork(default)).Should().NotContain("important");
        frame.Style.SerializeSource(new CssValueWork(default)).Should().Contain("border-color: blue;");
        frame.Style.GetPropertyValue("opacity").Should().Be("0.5");
        frame.Style.GetPropertyValue("--x").Should().Be("blue");
        Assert.Throws<CssIncompleteGrammarException>(() => frame.Style.GetPropertyValue("border-color"));
        Assert.Throws<CssIncompleteGrammarException>(() => _ = frame.CssText);
        frame.Style.ReplaceText("opacity:.25 !important; --x:green");
        frame.Style.CssText.Should().Be("--x: green;");
        frame.Style.SetProperty("opacity", ".75", "important");
        frame.Style.GetPropertyValue("opacity").Should().BeEmpty();
    }

    [TestCase("@media screen { @keyframes x { from {opacity:.5} to {} } } a {}")]
    [TestCase("@supports (color:red) { @keyframes x { from {--x:func(abc")]
    [TestCase("@keyframes x { from {--x:\"unterminated")]
    public void RecoveredSerializationRangesAndSourceSpansRemainSafe(string source)
    {
        var sheet = CssStyleSheet.Parse(source);
        var snapshot = sheet.SerializeWithRanges();
        foreach (var pair in snapshot.Ranges)
        {
            snapshot.Text[pair.Value.Start..pair.Value.End].Should().Be(pair.Key.CssText);
            pair.Key.SourceSpan.Start.Should().BeGreaterThanOrEqualTo(0);
            (pair.Key.SourceSpan.Start + pair.Key.SourceSpan.Length).Should().BeLessThanOrEqualTo(source.Length);
        }
        CssStyleSheet.Parse(snapshot.Text).Serialize().Should().Be(snapshot.Text);
    }

    [Test]
    public void RemovedDefinitionRetainsDescendantHistoryButStopsSheetStampPropagation()
    {
        var sheet = CssStyleSheet.Parse("@media screen { @keyframes x { from {} } }");
        var group = (CssMediaRule) sheet.Rules[0];
        var root = (CssKeyframesRule) group.Rules[0];
        var child = (CssKeyframeRule) root.Rules[0];
        group.DeleteRule(0);
        child.ParentRule.Should().BeSameAs(root);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        root.ParentRule.Should().BeNull();
        var stamp = sheet.Stamp;
        child.SetKeyText("to");
        child.Style.SetProperty("opacity", ".5");
        root.AppendRule("50% {}");
        root.SetName("other");
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void TimelineSelectorsRemainExplicitPendingAndReplacementIsAtomic()
    {
        var sheet = CssStyleSheet.Parse("a {}");
        var old = sheet.Rules[0];
        var stamp = sheet.Stamp;
        Assert.Throws<CssIncompleteRuleGrammarException>(() => sheet.ReplaceText("@keyframes x { entry 50% {} }"))!
            .Blocker.Should().Be("R3:keyframes-timeline-range-selectors");
        sheet.Rules[0].Should().BeSameAs(old);
        sheet.Stamp.Should().Be(stamp);
        var root = (CssKeyframesRule) CssStyleSheet.Parse("@keyframes x {}").Rules[0];
        root.AppendRule("entry 50% {}");
        root.FindRule("entry 50%").Should().BeNull();
        root.Rules.Should().BeEmpty();
    }

    [Test]
    public void LargeListsKeyComparisonsAndOutputShareCancelableWork()
    {
        var source = "@keyframes x {" + string.Concat(Enumerable.Repeat("from {}", 5000)) + "}";
        var sheet = CssStyleSheet.Parse(source);
        var root = (CssKeyframesRule) sheet.Rules[0];
        var stamp = sheet.Stamp;
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++checks == 5) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => root.DeleteRule("to", null, work));
        root.Rules.Count.Should().Be(5000);
        sheet.Stamp.Should().Be(stamp);
        using var output = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(output.Token, () => { if (++checks == 8) output.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => sheet.SerializeWithRanges(work));
        using var append = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(append.Token, () => { if (++checks == 8) append.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => root.AppendRule(string.Join(',', Enumerable.Repeat("from", 5000)) + " {}", null, work));
        root.Rules.Count.Should().Be(5000);
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void LeadingZeroExponentSerializationRemainsCancelableAfterExactValidation()
    {
        // Bypass tokenization to put the work budget directly on validation + serialization.
        // Exact validation uses fewer than 40 checkpoints; scanning the raw exponent again
        // must continue polling rather than handing the entire string to an unbounded BCL scan.
        var literal = "1e-" + new string('0', 100_000) + "1";
        var components = new global::Jint.HtmlParser.Css.CssComponentValueList([
            global::Jint.HtmlParser.Css.CssComponentValue.FromToken(new global::Jint.HtmlParser.Css.CssToken(
                global::Jint.HtmlParser.Css.CssTokenKind.Percentage, default, numberText: literal))]);
        CssKeyframeKeys.FromComponents(components, new CssValueWork(default), out _)!.Text.Should().Be("0.1%");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++checks == 40) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => CssKeyframeKeys.FromComponents(components, work, out _));
        checks.Should().Be(40);
    }

    [Test]
    public void CanceledDefinitionReplacementAndSettersLeaveAllPublishedStateUntouched()
    {
        var sheet = CssStyleSheet.Parse("@keyframes x {from {}}");
        var root = (CssKeyframesRule) sheet.Rules[0];
        var child = (CssKeyframeRule) root.Rules[0];
        var stamp = sheet.Stamp;
        var source = "@keyframes y {" + string.Concat(Enumerable.Repeat("to {}", 5000)) + "}";
        using var replacement = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(replacement.Token, () => { if (++checks == 8) replacement.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => sheet.ReplaceText(source, null, work, replacement.Token));
        sheet.Rules[0].Should().BeSameAs(root);
        child.ParentRule.Should().BeSameAs(root);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
        using var setter = new CancellationTokenSource();
        setter.Cancel();
        work = new CssValueWork(setter.Token);
        Assert.Throws<OperationCanceledException>(() => child.SetKeyText("to", null, work));
        Assert.Throws<OperationCanceledException>(() => root.SetName("other", work));
        Assert.Throws<OperationCanceledException>(() => root.DeleteRule("from", null, work));
        root.Name.Should().Be("x");
        child.KeyText.Should().Be("0%");
        root.Rules.Count.Should().Be(1);
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void DeepDefinitionOwnershipUsesTheSharedIterativeTraversalAndLimits()
    {
        // This inventory must exceed the work polling interval before its final checkpoint.
        // 1200 groups charged only 3605 units and never reached the second callback.
        const int depth = 3000;
        var source = string.Concat(Enumerable.Repeat("@media screen {", depth)) + "@keyframes x {from {}}" + new string('}', depth);
        var sheet = CssStyleSheet.Parse(source);
        sheet.SerializeWithRanges().Ranges.Count.Should().Be(depth + 2);
        sheet.ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default)).Should().BeEmpty();
        var leaf = sheet.Rules[0];
        while (leaf.Rules.Count != 0) leaf = leaf.Rules[0];
        ((CssKeyframeRule) leaf).SetKeyText("to");
        sheet.Stamp.Value.Should().Be(1);
        var root = sheet.Rules[0];
        var stamp = sheet.Stamp;
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () => { if (++checks == 2) cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => sheet.DeleteRule(0, work));
        checks.Should().Be(2);
        sheet.Rules.Count.Should().Be(1);
        sheet.Rules[0].Should().BeSameAs(root);
        root.ParentStyleSheet.Should().BeSameAs(sheet);
        leaf.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
        var options = new CssParseOptions { Limits = new ParseLimits { MaxNestingDepth = 4 } };
        Assert.Throws<ParseLimitException>(() => CssStyleSheet.Parse(source, options));
    }
}

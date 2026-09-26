#nullable enable
using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css.Model;

[TestFixture]
public sealed class CssNestingTests
{
    [TestCase("& > button", true, false)]
    [TestCase("button", true, true)]
    [TestCase("> button", true, false)]
    public void ExactBrowserSelectorAndRelativeSelectorsRequireMatchingParent(string selector,
        bool directMatch, bool deepMatch)
    {
        var sheet = CssStyleSheet.Parse("main { " + selector + " { display:none; color:red } }");
        var parent = (CssStyleRule) sheet.Rules[0];
        var child = (CssStyleRule) parent.Rules[0];
        var document = Document.CreateHtml();
        var main = document.CreateElement("main");
        var direct = document.CreateElement("button");
        var wrapper = document.CreateElement("div");
        var deep = document.CreateElement("button");
        main.AppendChild(direct);
        main.AppendChild(wrapper);
        wrapper.AppendChild(deep);
        child.TryMatch(direct, out _).Should().Be(directMatch);
        child.TryMatch(deep, out _).Should().Be(deepMatch);
        child.TryMatch(document.CreateElement("button"), out _).Should().BeFalse();
        child.Style.GetPropertyValue("display").Should().Be("none");
        child.Style.GetPropertyValue("color").Should().Be("red");
        parent.Style.Count.Should().Be(0);
        sheet.ApplicableStyleRules(new CssMediaEnvironment(), new CssValueWork(default)).Should().Equal(parent, child);
    }

    [Test]
    public void ParentListMaximumSpecificityDoesNotRequireTheMostSpecificBranchToMatch()
    {
        var sheet = CssStyleSheet.Parse(".parent, #unmatched { & > button {} }");
        var parent = (CssStyleRule) sheet.Rules[0];
        var child = (CssStyleRule) parent.Rules[0];
        var document = Document.CreateHtml();
        var main = document.CreateElement("main");
        main.SetAttribute("class", "parent");
        var button = document.CreateElement("button");
        main.AppendChild(button);
        child.TryMatch(button, out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 1));
        main.RemoveAttribute("class");
        child.TryMatch(button, out _).Should().BeFalse();
    }

    [Test]
    public void TwoLevelsAndMixedSelectorBranchesComposeWithoutExpansion()
    {
        var sheet = CssStyleSheet.Parse("main, #unused { section { & > button, span {} } }");
        var parent = (CssStyleRule) sheet.Rules[0];
        var middle = (CssStyleRule) parent.Rules[0];
        var child = (CssStyleRule) middle.Rules[0];
        child.Selector.Branches.Count.Should().Be(2);
        var document = Document.CreateHtml();
        var main = document.CreateElement("main");
        var section = document.CreateElement("section");
        var button = document.CreateElement("button");
        var span = document.CreateElement("span");
        main.AppendChild(section);
        section.AppendChild(button);
        button.AppendChild(span);
        child.TryMatch(button, out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 2));
        child.TryMatch(span, out specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 2));
        section.RemoveChild(button);
        child.TryMatch(span, out _).Should().BeFalse();
    }

    [TestCase(":is(&) > button")]
    [TestCase(":where(&) > button")]
    [TestCase("& > button, button")]
    public void NestingInsideFunctionsAndAcrossCommaBranchesUsesTheParentContext(string selector)
    {
        var sheet = CssStyleSheet.Parse("main { " + selector + " {} }");
        var child = (CssStyleRule) ((CssStyleRule) sheet.Rules[0]).Rules[0];
        var document = Document.CreateHtml();
        var main = document.CreateElement("main");
        var button = document.CreateElement("button");
        main.AppendChild(button);
        child.TryMatch(button, out _).Should().BeTrue();
        child.TryMatch(document.CreateElement("button"), out _).Should().BeFalse();
    }

    [Test]
    public void ForgivenUnknownFunctionsContainingNestingDoNotAddAnImplicitAncestor()
    {
        var sheet = CssStyleSheet.Parse("main { :is(:unknown(&), .target) {} }");
        var child = (CssStyleRule) ((CssStyleRule) sheet.Rules[0]).Rules[0];
        var document = Document.CreateHtml();
        var target = document.CreateElement("button");
        target.SetAttribute("class", "target");
        child.TryMatch(target, out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(0, 1, 0));
    }

    [Test]
    public void SelectorMutationRebindsDescendantsAndInvalidatesTheSheet()
    {
        var sheet = CssStyleSheet.Parse("main { section { & > button {} } }");
        var parent = (CssStyleRule) sheet.Rules[0];
        var middle = (CssStyleRule) parent.Rules[0];
        var child = (CssStyleRule) middle.Rules[0];
        var document = Document.CreateHtml();
        var main = document.CreateElement("main");
        var section = document.CreateElement("section");
        var button = document.CreateElement("button");
        main.AppendChild(section);
        section.AppendChild(button);
        child.TryMatch(button, out _).Should().BeTrue();
        var stamp = sheet.Stamp;
        var childStamp = child.Stamp;
        parent.SetSelectorText("article, #new");
        sheet.Stamp.Should().NotBe(stamp);
        child.Stamp.Should().NotBe(childStamp);
        child.TryMatch(button, out _).Should().BeFalse();
        main.SetAttribute("id", "new");
        child.TryMatch(button, out var specificity).Should().BeTrue();
        specificity.Should().Be(new SelectorSpecificity(1, 0, 2));
        middle.SetSelectorText("&");
        child.TryMatch(button, out _).Should().BeFalse();
        parent.SelectorText.Should().Be("article, #new");
        child.SelectorText.Should().Be("& > button");
    }

    [Test]
    public void SerializationRangesParentLinksAndChangesRetainNestedIdentityAfterDetach()
    {
        var sheet = CssStyleSheet.Parse("main { display:block; & > button { color:red; } section { span {} } } aside {}");
        var parent = (CssStyleRule) sheet.Rules[0];
        var child = (CssStyleRule) parent.Rules[0];
        var middle = (CssStyleRule) parent.Rules[1];
        var leaf = middle.Rules[0];
        var snapshot = sheet.SerializeWithRanges();
        snapshot.Ranges.Count.Should().Be(5);
        foreach (var rule in new CssRule[] { parent, child, middle, leaf, sheet.Rules[1] })
        {
            var range = snapshot.Ranges[rule];
            snapshot.Text[range.Start..range.End].Should().Be(rule.CssText);
        }
        CssStyleSheet.Parse(snapshot.Text).Serialize().Should().Be(snapshot.Text);
        child.ParentRule.Should().BeSameAs(parent);
        leaf.ParentRule.Should().BeSameAs(middle);
        leaf.ParentStyleSheet.Should().BeSameAs(sheet);
        var stamp = sheet.Stamp;
        child.Style.SetProperty("display", "none");
        sheet.Stamp.Should().NotBe(stamp);
        var rules = parent.Rules;
        parent.Rules.Should().BeSameAs(rules);
        sheet.DeleteRule(0);
        leaf.ParentStyleSheet.Should().BeSameAs(sheet);
        leaf.ParentRule.Should().BeSameAs(middle);
        stamp = sheet.Stamp;
        child.SetSelectorText("& > a");
        child.Style.SetProperty("display", "block");
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void FiniteGrammarBoundariesAndInvalidNestedSelectorsRemainExplicit()
    {
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssStyleSheet.Parse("main { @media all { & {} } }"))!
            .Blocker.Should().Be("C2:nesting-selector-context");
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssStyleSheet.Parse("main { & {} color:red; }"))!
            .Blocker.Should().Be("C2:interleaved-declarations");
        Assert.Throws<CssIncompleteRuleGrammarException>(() => CssStyleSheet.Parse("main { @supports (display:block) { & {} } }"))!
            .Blocker.Should().Be("C2:nesting-selector-context");
        var sheet = CssStyleSheet.Parse("main { ??? { color:red; } & > button {} }");
        ((CssStyleRule) sheet.Rules[0]).Rules.Count.Should().Be(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeepAndWideTreesPollDuringParseTraversalSerializationAndMutation(bool wide)
    {
        var source = new StringBuilder("main {");
        for (var i = 0; i < 5000; i++) source.Append(wide ? "& {}" : "& {");
        if (!wide) source.Append('}', 5000);
        source.Append('}');
        var text = source.ToString();
        using var parseCancellation = new CancellationTokenSource();
        var parseChecks = 0;
        var parseWork = new CssValueWork(parseCancellation.Token, () =>
        {
            if (++parseChecks == 8) parseCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => CssStyleSheet.Parse(text, null, parseWork, parseCancellation.Token));
        var sheet = CssStyleSheet.Parse(text);
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 4) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => sheet.ApplicableStyleRules(new CssMediaEnvironment(), work));
        using var serializationCancellation = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(serializationCancellation.Token, () =>
        {
            if (++checks == 2) serializationCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => sheet.SerializeWithRanges(work));
        using var mutationCancellation = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(mutationCancellation.Token, () =>
        {
            if (++checks == 12) mutationCancellation.Cancel();
        });
        var parent = (CssStyleRule) sheet.Rules[0];
        var selector = parent.Selector;
        var childSelector = ((CssStyleRule) parent.Rules[0]).Selector;
        var stamp = sheet.Stamp;
        Assert.Throws<OperationCanceledException>(() => parent.SetSelectorText("article", null, work, mutationCancellation.Token));
        parent.Selector.Should().BeSameAs(selector);
        ((CssStyleRule) parent.Rules[0]).Selector.Should().BeSameAs(childSelector);
        sheet.Stamp.Should().Be(stamp);
        using var detachCancellation = new CancellationTokenSource();
        checks = 0;
        work = new CssValueWork(detachCancellation.Token, () =>
        {
            if (++checks == 2) detachCancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => sheet.DeleteRule(0, work));
        sheet.Rules[0].Should().BeSameAs(parent);
        parent.ParentStyleSheet.Should().BeSameAs(sheet);
        ((CssStyleRule) parent.Rules[0]).ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReplacingALargeOldTreeStagesDetachWithInvocationWorkBeforePublication(bool wide)
    {
        var baseline = CssStyleSheet.Parse("old {}");
        var baselineChecks = 0;
        baseline.ReplaceText("new {}", null, new CssValueWork(default, () => baselineChecks++), default);
        var source = wide
            ? "main {" + string.Concat(Enumerable.Repeat("& {}", 5000)) + "}"
            : "main {" + string.Concat(Enumerable.Repeat("& {", 5000)) + new string('}', 5001);
        var sheet = CssStyleSheet.Parse(source);
        var root = (CssStyleRule) sheet.Rules[0];
        var child = root.Rules[0];
        var stamp = sheet.Stamp;
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == baselineChecks) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => sheet.ReplaceText("new {}", null, work, cancellation.Token));
        sheet.Rules[0].Should().BeSameAs(root);
        root.ParentStyleSheet.Should().BeSameAs(sheet);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
    }

    [Test]
    public void MediaDeletionStagesTheNestedSubtreeBeforeRemovingItsListEntry()
    {
        var sheet = CssStyleSheet.Parse("@media all { main {" +
            string.Concat(Enumerable.Repeat("& {", 5000)) + new string('}', 5002));
        var media = (CssMediaRule) sheet.Rules[0];
        var root = (CssStyleRule) media.Rules[0];
        var child = root.Rules[0];
        var stamp = sheet.Stamp;
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new CssValueWork(cancellation.Token, () =>
        {
            if (++checks == 2) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => media.DeleteRule(0, work));
        media.Rules[0].Should().BeSameAs(root);
        root.ParentRule.Should().BeSameAs(media);
        child.ParentStyleSheet.Should().BeSameAs(sheet);
        sheet.Stamp.Should().Be(stamp);
        media.DeleteRule(0);
        media.Rules.Count.Should().Be(0);
        root.ParentRule.Should().BeNull();
        child.ParentStyleSheet.Should().BeSameAs(sheet);
    }

    [Test]
    public void RepeatedParentReferencesHaveLinearValidationAndMatchingWork()
    {
        static int Checks(int depth, bool matches)
        {
            var sheet = CssStyleSheet.Parse("main {" +
                string.Concat(Enumerable.Repeat("&& {", depth)) + new string('}', depth + 1));
            var rule = (CssStyleRule) sheet.Rules[0];
            while (rule.Rules.Count != 0) rule = (CssStyleRule) rule.Rules[0];
            var element = Document.CreateHtml().CreateElement(matches ? "main" : "aside");
            var checks = 0;
            var work = new SelectorMatchWork(element, default, () =>
            {
                if (++checks > 1000) throw new InvalidOperationException("Nesting work exceeded the linear test ceiling.");
            });
            rule.TryMatch(element, out _, null, default, ref work).Should().Be(matches);
            return checks;
        }
        foreach (var matches in new[] { false, true })
        {
            var smaller = Checks(128, matches);
            var larger = Checks(256, matches);
            smaller.Should().BeGreaterThan(0);
            larger.Should().BeLessThanOrEqualTo(3 * smaller + 4);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DeepAndWideCompiledParentReferencesShareSelectorMatchCancellationWork(bool wide)
    {
        var text = wide
            ? string.Join(",", Enumerable.Repeat("main", 5000)) + " { & {} }"
            : "main {" + string.Concat(Enumerable.Repeat("& {", 5000)) + new string('}', 5001);
        var sheet = CssStyleSheet.Parse(text);
        var rule = (CssStyleRule) sheet.Rules[0];
        while (rule.Rules.Count != 0) rule = (CssStyleRule) rule.Rules[0];
        var document = Document.CreateHtml();
        var element = document.CreateElement("main");
        using var cancellation = new CancellationTokenSource();
        var checks = 0;
        var work = new SelectorMatchWork(element, cancellation.Token, () =>
        {
            if (++checks == 2) cancellation.Cancel();
        });
        Assert.Throws<OperationCanceledException>(() => rule.TryMatch(element, out _, null, default, ref work));
    }
}

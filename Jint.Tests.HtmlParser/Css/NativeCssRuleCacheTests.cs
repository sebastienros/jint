using Jint.Browser.Styling;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssRuleCacheTests
{
    [Test]
    public void WarmPreparationCreatesIndependentOwnersAndPreservesSourceCoordinates()
    {
        var cache = new NativeCssRuleCache();
        var raw = Raw(" /* prefix */ a:hover {margin:0 2px; --value: x; color:red!important}");
        var first = Read(cache, raw);
        var second = Read(cache, raw);
        second.Selector.Should().BeSameAs(first.Selector);
        second.Should().NotBeSameAs(first);
        second.Style.Should().NotBeSameAs(first.Style);
        second.CssText.Should().Be(first.CssText);
        second.SourceSpan.Should().Be(first.SourceSpan);
        var work = new CssValueWork(default);
        var firstSheet = new CssStyleSheet();
        var secondSheet = new CssStyleSheet();
        first.Attach(firstSheet, null, work);
        second.Attach(secondSheet, null, work);
        var secondStamp = secondSheet.Stamp;
        first.Style.SetProperty("color", "blue");
        first.SetSelectorText("b:focus");
        secondSheet.Stamp.Should().Be(secondStamp);
        second.SelectorText.Should().Be("a:hover");
        second.Style.GetPropertyValue("color").Should().Be("red");
        second.ParentStyleSheet.Should().BeSameAs(secondSheet);
        Read(cache, raw).CssText.Should().Be(second.CssText);
        var shifted = Read(cache, Raw("     a:hover {margin:0 2px; --value: x; color:red!important}"));
        shifted.SourceSpan.Start.Should().Be(5);
        shifted.Selector.Should().NotBeSameAs(second.Selector);
    }

    [Test]
    public void NestedRulesAlwaysUseTheCurrentParentSelector()
    {
        var cache = new NativeCssRuleCache();
        var parentA = Read(cache, Raw("a {}"));
        var parentB = Read(cache, Raw("b {}"));
        var raw = Raw("& .child {display:flex}");
        var a = (CssStyleRule) cache.Parse(raw, parentA, new(default))!.Value.Rule;
        var b = (CssStyleRule) cache.Parse(raw, parentB, new(default))!.Value.Rule;
        a.Selector.Should().NotBeSameAs(b.Selector);
        a.CssText.Should().Be(b.CssText);
        parentA.SetSelectorText("section");
        var updated = (CssStyleRule) cache.Parse(raw, parentA, new(default))!.Value.Rule;
        updated.Selector.Should().NotBeSameAs(a.Selector);
    }

    [Test]
    public void GroupingAndNestedBodiesRetainFreshModelsAndCurrentSource()
    {
        const string source = "@media screen {a{display:none; & .child {color:red}}}";
        var work = new CssValueWork(default);
        var first = NativeCssParsing.CreateSheet(source, work);
        var second = NativeCssParsing.CreateSheet(new string(source.ToCharArray()), work);
        NativeCssParsing.ReadTree(first.Rules, work);
        NativeCssParsing.ReadTree(second.Rules, work);
        var a = (CssStyleRule) first.Rules[0].Rules[0];
        var b = (CssStyleRule) second.Rules[0].Rules[0];
        b.Selector.Should().BeSameAs(a.Selector);
        b.Rules[0].Should().NotBeSameAs(a.Rules[0]);
        b.Rules[0].ParentRule.Should().BeSameAs(b);
        b.Rules[0].ParentStyleSheet.Should().BeSameAs(second);
        ((CssStyleRule) a.Rules[0]).Style.SetProperty("color", "blue");
        ((CssStyleRule) b.Rules[0]).Style.GetPropertyValue("color").Should().Be("red");
        a.SetSelectorText("section");
        b.SelectorText.Should().Be("a");
    }

    [Test]
    public async Task ConcurrentReadersCannotMutateThePrivatePrototype()
    {
        var cache = new NativeCssRuleCache();
        var raw = Raw("a {color:red}");
        var original = Read(cache, raw);
        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            var rule = Read(cache, raw);
            rule.Style.SetProperty("color", "blue");
            rule.SetSelectorText("b");
            return rule;
        })));
        results.Select(rule => rule.Style).Distinct().Should().HaveCount(16);
        Read(cache, raw).CssText.Should().Be(original.CssText);
    }

    [Test]
    public void EntryAndStorageBudgetsEvictAndOversizedRulesAreNotRetained()
    {
        var cache = new NativeCssRuleCache();
        var raw = Raw("a {color:red}");
        var original = Read(cache, raw);
        for (var index = 0; index < 8192; index++) Read(cache, Raw($".a{index} {{}}"));
        Read(cache, raw).Selector.Should().NotBeSameAs(original.Selector);

        original = Read(cache, raw);
        var value = new string('a', 60 * 1024);
        for (var index = 0; index < 18; index++) Read(cache, Raw($".b{index} {{--long:{value}}}"));
        Read(cache, raw).Selector.Should().NotBeSameAs(original.Selector);
        var oversized = Raw("a {--long:" + new string('x', 64 * 1024) + "}");
        Read(cache, oversized).Selector.Should().NotBeSameAs(Read(cache, oversized).Selector);
    }

    [Test]
    public void WarmHitsHonorCancellationAndCheckpointFailures()
    {
        var cache = new NativeCssRuleCache();
        var raw = Raw("a {color:red}");
        Read(cache, raw);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => cache.Parse(raw, null, new(cancellation.Token)));
        var failure = new ApplicationException("checkpoint");
        Assert.Throws<ApplicationException>(() => cache.Parse(raw, null, new(default, () => throw failure)))
            .Should().BeSameAs(failure);
        Read(cache, raw).Style.GetPropertyValue("color").Should().Be("red");
    }

    private static CssRawRule Raw(string source) => new NativeCssSyntaxCache().Parse(source, new(default))[0];
    private static CssStyleRule Read(NativeCssRuleCache cache, CssRawRule raw) =>
        (CssStyleRule) cache.Parse(raw, null, new(default))!.Value.Rule;
}

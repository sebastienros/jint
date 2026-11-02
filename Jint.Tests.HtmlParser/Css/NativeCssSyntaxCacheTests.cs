#nullable enable
using Jint.Browser.Styling;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Values;

namespace Jint.Tests.HtmlParser.Css;

public sealed class NativeCssSyntaxCacheTests
{
    [Test]
    public void ExactSourceReusesOnlyImmutableBoundariesAndEvictsOldEntries()
    {
        var cache = new NativeCssSyntaxCache();
        var work = new CssValueWork(default);
        const string first = "a {display:none}";
        var boundaries = cache.Parse(first, work);
        cache.Parse(new string(first.ToCharArray()), work).Should().BeSameAs(boundaries);
        cache.Parse("a {display:flex}", work).Should().NotBeSameAs(boundaries);
        for (var i = 0; i < 16; i++) cache.Parse($".entry{i} {{}}", work);
        cache.Parse(first, work).Should().NotBeSameAs(boundaries);
    }

    [Test]
    public void OversizedTextAndRuleCountsRemainUncached()
    {
        var cache = new NativeCssSyntaxCache();
        var work = new CssValueWork(default);
        var oversizedText = "a{}" + new string(' ', 2 * 1024 * 1024);
        cache.Parse(oversizedText, work).Should().NotBeSameAs(cache.Parse(oversizedText, work));
        var oversizedRules = string.Concat(Enumerable.Repeat("a{}", 40000));
        cache.Parse(oversizedRules, work).Should().NotBeSameAs(cache.Parse(oversizedRules, work));
    }

    [Test]
    public async Task WarmSyntaxCanBeReadConcurrentlyWithoutSharingMutableModels()
    {
        const string source = "@media screen {a{display:none}}";
        var cache = new NativeCssSyntaxCache();
        var raw = cache.Parse(source, new(default));
        var reads = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => cache.Parse(source, new(default)))));
        foreach (var read in reads) read.Should().BeSameAs(raw);
        var work = new CssValueWork(default);
        var first = NativeCssParsing.CreateSheet(source, work);
        var second = NativeCssParsing.CreateSheet(source, work);
        NativeCssParsing.ReadTree(first.Rules, work);
        NativeCssParsing.ReadTree(second.Rules, work);
        first.Rules[0].Should().NotBeSameAs(second.Rules[0]);
        var a = (CssStyleRule) first.Rules[0].Rules[0];
        var b = (CssStyleRule) second.Rules[0].Rules[0];
        a.Should().NotBeSameAs(b);
        a.Style.SetProperty("display", "flex");
        b.Style.GetPropertyValue("display").Should().Be("none");
        b.ParentStyleSheet.Should().BeSameAs(second);
    }

    [Test]
    public void WarmHitsStillHonorCancellationAndHostFailures()
    {
        var cache = new NativeCssSyntaxCache();
        const string source = "a{display:none}";
        cache.Parse(source, new(default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => cache.Parse(source, new(cancellation.Token)));
        var failure = new ApplicationException("checkpoint");
        Assert.Throws<ApplicationException>(() => cache.Parse(source, new(default, () => throw failure)))
            .Should().BeSameAs(failure);
        cache.Parse(source, new(default)).Should().HaveCount(1);
    }
}

#nullable enable

using Jint.Browser.Runtime;

namespace Jint.Tests.Browser.Views;

using Browser = global::Jint.Browser.Browser;

public sealed class NativeCssAllInputMediaTests
{
    [TestCase("any-pointer", "coarse", "pointer", "fine")]
    [TestCase("any-pointer", "none", "pointer", "fine")]
    [TestCase("any-hover", "none", "hover", "hover")]
    [TestCase("display-mode", "fullscreen", "display-mode", "fullscreen")]
    [TestCase("display-mode", "standalone", "display-mode", "standalone")]
    [TestCase("display-mode", "minimal-ui", "display-mode", "minimal-ui")]
    [TestCase("display-mode", "picture-in-picture", "display-mode", "picture-in-picture")]
    public async Task CascadeAndMatchMediaReadIndependentLiveFeaturesAndReset(string feature, string value,
        string primaryFeature, string primaryValue)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($$"""
            <style>
              #t { position: relative }
              @media ({{feature}}: {{value}}) { #t { position: absolute } }
            </style>
            <div id=t>text</div>
            """);
        var read = "getComputedStyle(document.getElementById('t')).position";
        var matches = $"matchMedia('({feature}: {value})').matches";
        (await page.EvaluateAsync<string>(read)).Should().Be("relative");
        (await page.EvaluateAsync<bool>(matches)).Should().BeFalse();

        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            var features = new Dictionary<string, string> { [primaryFeature] = primaryValue };
            features[feature] = value;
            runtime.SetMedia(runtime.Media with { Features = features });
            return 0;
        });
        (await page.EvaluateAsync<string>(read)).Should().Be("absolute");
        (await page.EvaluateAsync<bool>(matches)).Should().BeTrue();
        (await page.EvaluateAsync<bool>($"matchMedia('({primaryFeature}: {primaryValue})').matches"))
            .Should().BeTrue();

        await page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.SetMedia(PageMediaEnvironment.Default);
            return 0;
        });
        (await page.EvaluateAsync<string>(read)).Should().Be("relative");
        (await page.EvaluateAsync<bool>(matches)).Should().BeFalse();
        page.Errors.Should().BeEmpty();
    }

    [TestCase("any-pointer", "none", false)]
    [TestCase("any-hover", "none", false)]
    [TestCase("display-mode", "standalone", true)]
    public async Task BooleanRulesUseTheSameDefaultAndResetSnapshot(string feature, string value, bool expected)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync($$"""
            <style>#t { position:relative } @media ({{feature}}) { #t { position:absolute } }</style>
            <div id=t>text</div>
            """);
        var read = "getComputedStyle(document.getElementById('t')).position";
        (await page.EvaluateAsync<string>(read)).Should().Be("absolute");
        (await page.EvaluateAsync<bool>($"matchMedia('({feature})').matches")).Should().BeTrue();
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Features = new Dictionary<string, string> { [feature] = value } });
            return 0;
        });
        (await page.EvaluateAsync<string>(read)).Should().Be(expected ? "absolute" : "relative");
        (await page.EvaluateAsync<bool>($"matchMedia('({feature})').matches")).Should().Be(expected);
        await page.RunOnLoopAsync(engine =>
        {
            PageRuntime.Find(engine)!.SetMedia(PageMediaEnvironment.Default);
            return 0;
        });
        (await page.EvaluateAsync<string>(read)).Should().Be("absolute");
        (await page.EvaluateAsync<bool>($"matchMedia('({feature})').matches")).Should().BeTrue();
        page.Errors.Should().BeEmpty();
    }
}

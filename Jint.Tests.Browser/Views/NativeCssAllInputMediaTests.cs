#nullable enable

using System.Text;
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

    [TestCase("any-pointer", "fine", "invalid")]
    [TestCase("any-pointer", "fine", "coarse fine")]
    [TestCase("any-hover", "hover", "invalid")]
    [TestCase("any-hover", "hover", "coarse")]
    [TestCase("display-mode", "browser", "invalid")]
    [TestCase("display-mode", "browser", "none")]
    public async Task InvalidHostValuesRemainUnknownInCascadeAndMatchMedia(string feature, string valid, string invalid)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        var queries = new[]
        {
            $"({feature})", $"not ({feature})", $"({feature}: {valid})", $"not ({feature}: {valid})",
            $"({feature}: {invalid})", $"not ({feature}: {invalid})"
        };
        await page.SetContentAsync(QueryFixture(queries));
        await page.RunOnLoopAsync(engine =>
        {
            var runtime = PageRuntime.Find(engine)!;
            runtime.SetMedia(runtime.Media with { Features = new Dictionary<string, string> { [feature] = invalid } });
            return 0;
        });
        for (var i = 0; i < queries.Length; i++)
        {
            (await page.EvaluateAsync<string>($"getComputedStyle(document.getElementById('t{i}')).position"))
                .Should().Be("relative", queries[i]);
            (await page.EvaluateAsync<bool>($"matchMedia('{queries[i]}').matches")).Should().BeFalse(queries[i]);
        }
        page.Errors.Should().BeEmpty();
    }

    [TestCase("any-pointer", "invalid")]
    [TestCase("any-pointer", "coarse fine")]
    [TestCase("any-hover", "invalid")]
    [TestCase("any-hover", "coarse")]
    [TestCase("display-mode", "invalid")]
    [TestCase("display-mode", "none")]
    public async Task InvalidRequestedValuesRemainUnknownWithAValidHostSnapshot(string feature, string invalid)
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        var queries = new[]
        {
            $"({feature}: {invalid})", $"not ({feature}: {invalid})", $"({feature}:)", $"not ({feature}:)"
        };
        await page.SetContentAsync(QueryFixture(queries));
        for (var i = 0; i < queries.Length; i++)
        {
            (await page.EvaluateAsync<string>($"getComputedStyle(document.getElementById('t{i}')).position"))
                .Should().Be("relative", queries[i]);
            (await page.EvaluateAsync<bool>($"matchMedia('{queries[i]}').matches")).Should().BeFalse(queries[i]);
        }
        page.Errors.Should().BeEmpty();
    }

    private static string QueryFixture(string[] queries)
    {
        var html = new StringBuilder("<style>div { position:relative }");
        for (var i = 0; i < queries.Length; i++)
            html.Append("@media ").Append(queries[i]).Append(" { #t").Append(i).Append(" { position:absolute } }");
        html.Append("</style>");
        for (var i = 0; i < queries.Length; i++) html.Append("<div id=t").Append(i).Append(">text</div>");
        return html.ToString();
    }
}

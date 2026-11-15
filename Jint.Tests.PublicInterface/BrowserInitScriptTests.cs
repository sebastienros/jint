#nullable enable
#if NET8_0_OR_GREATER
using Jint.Browser;

namespace Jint.Tests.PublicInterface;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// An embedder with no friend access installs scripts that run before a document's own, on a page and on a context.
/// </summary>
public sealed class BrowserInitScriptTests
{
    [Test]
    public async Task APageInitScriptRunsWhileTheNextDocumentIsLoading()
    {
        await using var browser = new Browser(new BrowserOptions());
        var page = await browser.NewPageAsync();

        await page.AddInitScriptAsync("window.__early = document.readyState");
        await page.SetContentAsync("<!doctype html><script>window.__seenByPage = window.__early;</script>", "https://example.org/");

        (await page.EvaluateAsync<string>("String(window.__early)")).Should().Be("loading");
        (await page.EvaluateAsync<string>("String(window.__seenByPage)")).Should().Be("loading");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AContextInitScriptSeedsStorageWithItsArgumentBeforeThePageReadsIt()
    {
        await using var browser = new Browser(new BrowserOptions());
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await context.AddInitScriptAsync(
            "(seed) => { if (location.protocol === 'https:') localStorage.setItem('theme', seed.theme); }",
            new { theme = "dark" });
        await page.AddInitScriptAsync("(name) => { window.stubbed = name; }", "matchMedia");
        await page.SetContentAsync("<!doctype html><script>window.theme = localStorage.getItem('theme');</script>", "https://example.org/");

        (await page.EvaluateAsync<string>("window.theme + ':' + window.stubbed")).Should().Be("dark:matchMedia");
        page.Errors.Should().BeEmpty();
    }
}
#endif

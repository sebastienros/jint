#nullable enable
#if NET8_0_OR_GREATER
using Jint.Browser;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// A host resizing a page after it opened, and giving each context its own device profile, through nothing but
/// <c>Jint.Browser</c>'s public surface (#4622).
/// </summary>
public sealed class HostBrowserViewportTests
{
    [Test]
    public async Task AHostCanResizeAnOpenPageAndTheDocumentHearsIt()
    {
        await using var browser = new Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(1440, 900) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<!doctype html><script>addEventListener('resize', () => document.title = innerWidth)</script>",
            "http://127.0.0.1/");

        await page.SetViewportSizeAsync(390, 844);

        (await page.EvaluateAsync<string>("innerWidth + ' ' + matchMedia('(max-width: 599px)').matches + ' ' + document.title"))
            .Should().Be("390 true 390");
        page.Viewport.Should().Be(new Viewport(390, 844, 1));

        await page.SetViewportAsync(new Viewport(768, 1024, 2));
        (await page.EvaluateAsync<string>("innerWidth + '@' + devicePixelRatio")).Should().Be("768@2");
    }

    [Test]
    public async Task AHostCanOpenContextsWithDifferentViewports()
    {
        await using var browser = new Jint.Browser.Browser(new BrowserOptions { Viewport = new Viewport(1440, 900) });
        var mobile = await browser.NewContextAsync(new BrowserContextOptions
        {
            Viewport = new Viewport(390, 844, 3),
            HasTouch = true,
        });

        var phone = await mobile.NewPageAsync();
        var desktop = await browser.NewPageAsync();

        (await phone.EvaluateAsync<string>("innerWidth + '@' + devicePixelRatio + ' ' + ('ontouchstart' in window)"))
            .Should().Be("390@3 true");
        (await desktop.EvaluateAsync<string>("innerWidth + '@' + devicePixelRatio + ' ' + ('ontouchstart' in window)"))
            .Should().Be("1440@1 false");
    }
}
#endif

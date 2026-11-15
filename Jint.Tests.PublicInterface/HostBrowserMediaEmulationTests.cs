#nullable enable
#if NET8_0_OR_GREATER
using Jint.Browser;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// Media emulation is reachable by an embedder driving <c>Jint.Browser</c> in process, with no protocol
/// client attached: the browser default, the context default and the page member all compile and take
/// effect against the published surface alone.
/// </summary>
public sealed class HostBrowserMediaEmulationTests
{
    private const string Probe =
        "JSON.stringify({ dark: matchMedia('(prefers-color-scheme: dark)').matches, reduce: matchMedia('(prefers-reduced-motion: reduce)').matches, print: matchMedia('print').matches })";

    [Test]
    public async Task AnEmbedderCanEmulateMediaOnAPageAndAsBrowserAndContextDefaults()
    {
        await using var browser = new global::Jint.Browser.Browser(new BrowserOptions
        {
            MediaEmulation = new MediaEmulation { ReducedMotion = "reduce" },
        });
        var context = await browser.NewContextAsync(new BrowserContextOptions
        {
            MediaEmulation = new MediaEmulation { ColorScheme = "dark" },
        });
        var page = await context.NewPageAsync();

        await page.SetContentAsync(
            "<!doctype html><script>window.atParse = " + Probe + ";</script>",
            "http://127.0.0.1/");
        (await page.EvaluateAsync<string>("window.atParse"))
            .Should().Be("""{"dark":true,"reduce":true,"print":false}""");

        await page.EvaluateAsync(
            "window.heard = []; matchMedia('print').addEventListener('change', e => heard.push(e.matches));");
        await page.EmulateMediaAsync(new MediaEmulation { Media = "print", ColorScheme = "light" });

        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":false,"reduce":true,"print":true}""");
        (await page.EvaluateAsync<string>("heard.join()")).Should().Be("true");

        // Clearing an override goes back to the default the page opened with, not past it.
        await page.EmulateMediaAsync(new MediaEmulation { Media = "", ColorScheme = "" });

        (await page.EvaluateAsync<string>(Probe)).Should().Be("""{"dark":true,"reduce":true,"print":false}""");
        (await page.EvaluateAsync<string>("heard.join()")).Should().Be("true,false");
        page.Errors.Should().BeEmpty();
    }
}
#endif

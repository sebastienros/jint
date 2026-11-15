#nullable enable
using Jint.Browser;
using Jint.Tests.Browser.Navigation;
using Jint.Tests.Browser.Parsing;

namespace Jint.Tests.Browser.Runtime;

// The test namespace sits under Jint.Tests.Browser, so the bare name Browser binds to that namespace rather
// than to the type. The alias belongs inside the namespace declaration, where it wins that lookup.
using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <see cref="Page.SetViewportAsync(Viewport)"/>, <see cref="Page.SetViewportSizeAsync"/> and
/// <see cref="BrowserContextOptions.Viewport"/>: what a page hears when its window is resized after it opened,
/// and what one context's pages open with (#4622).
/// </summary>
/// <remarks>
/// Every assertion is read through script, because the question is what the document is running under, not
/// what the page's bookkeeping holds.
/// </remarks>
public sealed class ViewportResizeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>A synchronous refusal, which is what each of these is: nothing is posted to the loop.</summary>
    private static Action Refused(Func<object> call) => () => call();

    [Test]
    public async Task TheIssueReproReportsTheNewWidthMatchesTheQueryAndFiresResize()
    {
        await using var browser = new Browser(new BrowserOptions { Viewport = new Viewport(1440, 900) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            "<!doctype html><script>addEventListener('resize', () => document.title = innerWidth)</script>",
            "http://127.0.0.1/");

        (await page.EvaluateAsync<string>("innerWidth + ' ' + matchMedia('(max-width: 599px)').matches"))
            .Should().Be("1440 false");

        await page.SetViewportSizeAsync(390, 844);

        (await page.EvaluateAsync<string>("innerWidth + ' ' + matchMedia('(max-width: 599px)').matches"))
            .Should().Be("390 true");
        (await page.EvaluateAsync<string>("document.title")).Should().Be("390");
        page.Viewport.Should().Be(new Viewport(390, 844, 1));
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ResizeFiresOncePerSizeChangeBeforeTheMediaQueryListsHearChange()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <!doctype html><script>
              window.log = [];
              addEventListener('resize', e => log.push('resize:' + innerWidth + 'x' + innerHeight + ':' + e.isTrusted + ':' + e.bubbles));
              visualViewport.addEventListener('resize', () => log.push('visual:' + visualViewport.width));
              matchMedia('(max-width: 599px)').addEventListener('change', e => log.push('mql:' + e.matches));
              matchMedia('(resolution: 2dppx)').addEventListener('change', e => log.push('dppx:' + e.matches));
            </script>
            """,
            "http://127.0.0.1/");

        await page.SetViewportSizeAsync(390, 844);
        (await page.EvaluateAsync<string>("log.join('|')"))
            .Should().Be("resize:390x844:true:false|visual:390|mql:true");

        // A second write of the same size is no change at all, and changes nothing a page can hear.
        await page.SetViewportSizeAsync(390, 844);
        (await page.EvaluateAsync<int>("log.length")).Should().Be(3);

        // Only the device scale factor moving is not a resize: the window is the same size.
        await page.SetViewportAsync(new Viewport(390, 844, 2));
        (await page.EvaluateAsync<string>("log.slice(3).join('|') + ' ' + devicePixelRatio"))
            .Should().Be("dppx:true 2");

        // The height alone is a resize, and a query about the width does not hear it.
        await page.SetViewportSizeAsync(390, 600);
        (await page.EvaluateAsync<string>("log.slice(4).join('|')")).Should().Be("resize:390x600:true:false|visual:390");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task SetViewportSizeKeepsTheDeviceScaleFactor()
    {
        await using var browser = new Browser(new BrowserOptions { Viewport = new Viewport(1280, 720, 3) });
        var page = await browser.NewPageAsync();

        await page.SetViewportSizeAsync(600, 400);

        page.Viewport.Should().Be(new Viewport(600, 400, 3));
        (await page.EvaluateAsync<string>("innerWidth + 'x' + innerHeight + '@' + devicePixelRatio"))
            .Should().Be("600x400@3");
    }

    [Test]
    public async Task StylesheetMediaRulesAreMatchedAgainstTheNewWidth()
    {
        await using var browser = new Browser(new BrowserOptions { Viewport = new Viewport(1440, 900) });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <!doctype html>
            <style>
              #menu { color: rgb(0, 0, 255); }
              @media (max-width: 599px) { #menu { color: rgb(255, 0, 0); } }
            </style>
            <nav id="menu">menu</nav>
            """,
            "http://127.0.0.1/");

        const string color = "getComputedStyle(document.getElementById('menu')).color";
        (await page.EvaluateAsync<string>(color)).Should().Be("rgb(0, 0, 255)");

        foreach (var (width, expected) in new[]
                 {
                     (320, "rgb(255, 0, 0)"), (390, "rgb(255, 0, 0)"), (600, "rgb(0, 0, 255)"),
                     (768, "rgb(0, 0, 255)"), (1440, "rgb(0, 0, 255)"), (599, "rgb(255, 0, 0)"),
                 })
        {
            await page.SetViewportSizeAsync(width, 900);
            (await page.EvaluateAsync<string>(color)).Should().Be(expected, "at {0} px", width);
        }

        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AResizeListenerThatClosesAMenuRunsBeforeTheTaskCompletes()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(
            """
            <!doctype html><nav id="menu" class="open"></nav>
            <script>
              addEventListener('resize', () => { if (innerWidth >= 768) menu.classList.remove('open'); });
            </script>
            """,
            "http://127.0.0.1/");

        await page.SetViewportSizeAsync(390, 844);
        (await page.EvaluateAsync<string>("menu.className")).Should().Be("open");

        await page.SetViewportSizeAsync(1024, 768);
        (await page.EvaluateAsync<string>("menu.className")).Should().Be("");
    }

    [Test]
    public async Task TheResizedViewportSurvivesNavigation()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        await page.SetViewportAsync(new Viewport(768, 1024, 2));
        await page.SetContentAsync("<!doctype html><p>next document</p>", "http://127.0.0.1/next");

        (await page.EvaluateAsync<string>("innerWidth + 'x' + innerHeight + '@' + devicePixelRatio"))
            .Should().Be("768x1024@2");
    }

    [Test]
    public async Task ASourceSetSelectsAgainAfterTheViewportMoves()
    {
        await using var loopback = await LoopbackPage.CreateAsync(
            server => server
                .Map("/small.png", _ => LoopbackResponse.Raw(ImageBytes.Png(1, 1), "image/png"))
                .Map("/large.png", _ => LoopbackResponse.Raw(ImageBytes.Png(2, 2), "image/png"))
                .Map("/narrow.png", _ => LoopbackResponse.Raw(ImageBytes.Png(3, 3), "image/png"))
                .Map("/wide.png", _ => LoopbackResponse.Raw(ImageBytes.Png(4, 4), "image/png"))
                .MapHtml("/", """
                    <!doctype html><html><body>
                    <img id="a" sizes="100vw" srcset="/small.png 400w, /large.png 1600w">
                    <picture>
                      <source media="(max-width: 599px)" srcset="/narrow.png">
                      <img id="b" src="/wide.png">
                    </picture>
                    <script>
                      window.loads = [];
                      a.addEventListener('load', () => loads.push('a:' + a.currentSrc.replace(/^.*\//, '')));
                      b.addEventListener('load', () => loads.push('b:' + b.currentSrc.replace(/^.*\//, '')));
                    </script>
                    </body></html>
                    """),
            configureBrowser: options => options.Viewport = new Viewport(390, 844));
        var page = loopback.Page;

        await page.NavigateAsync(loopback.Url("/"));
        await page.WaitForIdleAsync(Timeout);

        const string selected = "[a, b].map(i => i.currentSrc.replace(/^.*\\//, '') + ':' + i.naturalWidth).join(' ')";
        (await page.EvaluateAsync<string>(selected)).Should().Be("small.png:1 narrow.png:3");

        await page.SetViewportSizeAsync(1440, 900);
        (await page.EvaluateAsync<string>(selected)).Should().Be("large.png:2 wide.png:4");
        await page.WaitForIdleAsync(Timeout);
        (await page.EvaluateAsync<string>("loads.slice(-2).sort().join(' ')")).Should().Be("a:large.png b:wide.png");

        // An image whose selection did not move is not fetched again.
        var requests = page.Requests.Count;
        await page.SetViewportSizeAsync(1280, 900);
        await page.WaitForIdleAsync(Timeout);
        page.Requests.Count.Should().Be(requests);
        (await page.EvaluateAsync<string>(selected)).Should().Be("large.png:2 wide.png:4");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task EachContextOpensItsPagesAtItsOwnViewport()
    {
        await using var browser = new Browser(new BrowserOptions { Viewport = new Viewport(1440, 900) });
        var desktop = await browser.NewPageAsync();
        var tablet = await (await browser.NewContextAsync(new BrowserContextOptions { Viewport = new Viewport(768, 1024, 2) }))
            .NewPageAsync();
        var mobileContext = await browser.NewContextAsync(new BrowserContextOptions
        {
            Viewport = new Viewport(390, 844, 3),
            HasTouch = true,
        });
        var mobile = await mobileContext.NewPageAsync();

        const string probe = "innerWidth + '@' + devicePixelRatio + ' ' + matchMedia('(max-width: 599px)').matches + ' ' + ('ontouchstart' in window)";
        (await desktop.EvaluateAsync<string>(probe)).Should().Be("1440@1 false false");
        (await tablet.EvaluateAsync<string>(probe)).Should().Be("768@2 false false");
        (await mobile.EvaluateAsync<string>(probe)).Should().Be("390@3 true true");

        // A second page of the context opens at the context's size, not at the size the first was resized to.
        await mobile.SetViewportSizeAsync(320, 640);
        var second = await mobileContext.NewPageAsync();
        second.Viewport.Should().Be(new Viewport(390, 844, 3));
        mobile.Viewport.Should().Be(new Viewport(320, 640, 3));
        desktop.Viewport.Should().Be(new Viewport(1440, 900, 1));
    }

    [Test]
    public async Task ABadViewportIsRefusedBeforeItReachesThePage()
    {
        await using var browser = new Browser();
        var page = await browser.NewPageAsync();

        Refused(() => page.SetViewportSizeAsync(0, 100)).Should().Throw<ArgumentOutOfRangeException>();
        Refused(() => page.SetViewportSizeAsync(100, -1)).Should().Throw<ArgumentOutOfRangeException>();
        Refused(() => page.SetViewportAsync(new Viewport(100, 100, 0))).Should().Throw<ArgumentOutOfRangeException>();
        Refused(() => page.SetViewportAsync(new Viewport(100, 100, double.NaN))).Should().Throw<ArgumentOutOfRangeException>();
        Refused(() => page.SetViewportAsync(new Viewport(100, 100, double.PositiveInfinity))).Should().Throw<ArgumentOutOfRangeException>();
        Refused(() => new BrowserContextOptions { Viewport = new Viewport(0, 100) }).Should().Throw<ArgumentOutOfRangeException>();

        page.Viewport.Should().Be(Viewport.Default);

        await page.CloseAsync();
        Refused(() => page.SetViewportSizeAsync(100, 100)).Should().Throw<ObjectDisposedException>();
    }
}

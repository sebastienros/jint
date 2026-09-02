using Jint.Browser;
using Jint.DevTools;
using Jint.Tests.Browser.Navigation;
using PuppeteerSharp;
using PageContextOptions = Jint.Browser.BrowserContextOptions;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// A real client library driving a real page, over a real socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only test in the suite that can claim client compatibility.</b> Everything else asserts
/// what this server answers; this asserts that a library nobody here wrote, written against Chrome, is
/// satisfied by it — and it exercises the page half, which <c>Jint.Tests.DevTools</c>'s own PuppeteerSharp
/// suite explicitly cannot: an engine target has no document, so <c>NewPageAsync</c> and <c>GoToAsync</c>
/// are exactly the calls that were out of reach there.
/// </para>
/// <para>
/// <b>No browser is downloaded and none is launched.</b> <c>ConnectAsync</c> speaks to an endpoint that
/// already exists, which is what this server is; Puppeteer's browser-fetching machinery is never touched.
/// The version is the one <c>tools/devtools-protocol/handshakes/puppeteersharp-dotnet.json</c> was recorded
/// with, so what it sends here is what that file says it sends.
/// </para>
/// <para>
/// Every wait is bounded, generously: the bound is there to stop a hang, not to assert a speed.
/// </para>
/// </remarks>
[NonParallelizable]
public class PuppeteerSharpPageTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    [Test]
    public async Task PuppeteerOpensAPageNavigatesEvaluatesAndCloses()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>First</title></head><body><p id='greeting'>hello</p></body></html>");
        origin.MapHtml("/two", "<html><head><title>Second</title></head><body><p id='greeting'>again</p></body></html>");

        await using var pages = new global::Jint.Browser.Browser();
        var context = await pages.NewContextAsync(new PageContextOptions { UrlFilter = origin.Owns });

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        browser.IsConnected.Should().BeTrue();

        // The client's own newPage, which is Target.createBrowserContext + createTarget through the host.
        var page = await browser.NewPageAsync().WaitAsync(Bound);

        // The navigation runs and the document loads; the *response object* is null, and that is a stated
        // gap rather than a failure -- a client builds one out of Network.responseReceived, and the Network
        // events arrive with the interception work (campaign item C3). What the client can see today is the
        // page it ended up on, which is what everything below reads.
        (await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound)).Should().BeNull();

        (await page.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("First");
        (await page.EvaluateExpressionAsync<string>("document.getElementById('greeting').textContent").WaitAsync(Bound)).Should().Be("hello");

        // A second navigation, and the client keeps up with it: its own idea of where the page is comes from
        // the frameNavigated and targetInfoChanged it was sent.
        await page.GoToAsync(origin.Url("/two")).WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("Second");
        page.Url.Should().Be(origin.Url("/two"));

        // A same-document move, which the client sees as navigatedWithinDocument rather than as a load.
        await page.EvaluateExpressionAsync("history.pushState({}, '', '/two/deeper')").WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("location.pathname").WaitAsync(Bound)).Should().Be("/two/deeper");

        await page.CloseAsync().WaitAsync(Bound);

        browser.Disconnect();
        browser.IsConnected.Should().BeFalse();

        await context.CloseAsync();
    }

    [Test]
    public async Task PuppeteerReadsAPageAHostAlreadyOpened()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>Existing</title></head><body>one</body></html>");

        await using var pages = new global::Jint.Browser.Browser();
        var context = await pages.NewContextAsync(new PageContextOptions { UrlFilter = origin.Owns });
        var hosted = await context.NewPageAsync();
        await hosted.NavigateAsync(origin.Url("/one"));

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        // Every page the browser already had is a target, which is what makes attaching to a host's own
        // running page the point of the whole thing rather than a special case.
        var found = await WaitForPageAsync(browser, origin.Url("/one"));
        (await found.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("Existing");

        browser.Disconnect();
        await context.CloseAsync();
    }

    /// <summary>The client's page for one URL, waiting for the target list to reach it.</summary>
    private static async Task<IPage> WaitForPageAsync(IBrowser browser, string url)
    {
        var deadline = Environment.TickCount64 + (long) Bound.TotalMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            foreach (var page in await browser.PagesAsync().WaitAsync(Bound))
            {
                if (string.Equals(page.Url, url, StringComparison.Ordinal))
                {
                    return page;
                }
            }

            await Task.Delay(25);
        }

        Assert.Fail($"no page for '{url}' reached the client within {Bound}.");
        return null!;
    }
}

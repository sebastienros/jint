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

        // And the domain that is ours, reached the way a client reaches any command its library has never
        // heard of. This is the snippet the README gives, compiled.
        var cdp = await page.CreateCDPSessionAsync().WaitAsync(Bound);
        var answer = await cdp.SendAsync("Jint.getMarkdown", new { mainContentOnly = false }).WaitAsync(Bound);
        answer!.Value.GetProperty("markdown").GetString().Should().Contain("again");

        await page.CloseAsync().WaitAsync(Bound);

        browser.Disconnect();
        browser.IsConnected.Should().BeFalse();

        await context.CloseAsync();
    }

    /// <summary>
    /// The <c>$</c>, <c>$$</c>, <c>click</c> and <c>waitForSelector</c> half, which is the whole reason the
    /// <c>DOM</c> domain and the mouse exist.
    /// </summary>
    /// <remarks>
    /// Every one of these goes through a code path no other test reaches: a client library turns a
    /// <c>RemoteObject</c> with <c>subtype: "node"</c> into an element handle, asks the <c>DOM</c> domain
    /// where that element is, and clicks the point it was told — and the click reaches a listener the page
    /// registered because the flat box model gave both sides the same answer.
    /// </remarks>
    [Test]
    public async Task PuppeteerFindsElementsMeasuresThemAndClicksThem()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml(
            "/form",
            """
            <html><head><title>Form</title></head><body>
              <p class="row">one</p>
              <p class="row">two</p>
              <button id="go">Go</button>
              <a id="away" href="/next">Away</a>
              <script>
                window.clicks = [];
                document.getElementById('go').addEventListener('click', e => {
                  window.clicks.push(e.type + ':' + e.isTrusted + ':' + e.detail + ':' + (e.target.id));
                });
                document.getElementById('go').addEventListener('mousemove', () => window.clicks.push('mousemove'));
                setTimeout(() => {
                  const late = document.createElement('div');
                  late.id = 'late';
                  late.textContent = 'here';
                  document.body.appendChild(late);
                }, 50);
              </script>
            </body></html>
            """);

        origin.MapHtml("/next", "<html><head><title>Next</title></head><body><p>arrived</p></body></html>");

        await using var pages = new global::Jint.Browser.Browser();
        var context = await pages.NewContextAsync(new PageContextOptions { UrlFilter = origin.Owns });

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/form")).WaitAsync(Bound);

        // $ and $$: one element handle, and a count.
        var button = await page.QuerySelectorAsync("#go").WaitAsync(Bound);
        button.Should().NotBeNull("a client builds an element handle out of a node's subtype");

        (await page.QuerySelectorAllAsync("p.row").WaitAsync(Bound)).Should().HaveCount(2);

        // A handle is something the client evaluates against.
        (await button.EvaluateFunctionAsync<string>("e => e.id").WaitAsync(Bound)).Should().Be("go");

        // …and something it can measure, against the same model the page reads through
        // getBoundingClientRect.
        var box = await button.BoundingBoxAsync().WaitAsync(Bound);
        var script = await page.EvaluateExpressionAsync<string>(
            "(() => { const r = document.getElementById('go').getBoundingClientRect(); return [r.x, r.y, r.width, r.height].join(','); })()")
            .WaitAsync(Bound);

        $"{box!.X},{box.Y},{box.Width},{box.Height}".Should().Be(script);

        // Hovering is a mouse move at that point, and the listener hears it.
        await button.HoverAsync().WaitAsync(Bound);

        // And a click is the sequence, at the centre of the box, with the trust a client's input carries.
        await button.ClickAsync().WaitAsync(Bound);

        var clicks = await page.EvaluateExpressionAsync<string>("window.clicks.join('|')").WaitAsync(Bound);
        clicks.Should().Contain("mousemove");
        clicks.Should().Contain("click:true:1:go", "a client driving a page stands in for a user, so its input is trusted");

        // waitForSelector, for something a timer put there after the load.
        var late = await page.WaitForSelectorAsync("#late").WaitAsync(Bound);
        (await late.EvaluateFunctionAsync<string>("e => e.textContent").WaitAsync(Bound)).Should().Be("here");

        // A link's activation behaviour is a navigation, which the client then sees.
        await Task.WhenAll(
            page.WaitForNavigationAsync(),
            page.ClickAsync("#away")).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("Next");

        // The one thing it cannot do, refused in a sentence that names what to ask for instead.
        var refusal = await CatchAsync(() => page.ScreenshotDataAsync().WaitAsync(Bound));

        refusal.Should().NotBeNull("this browser renders no pixels and says so rather than answering an image");
        refusal!.Message.Should().Contain("renders no pixels");
        refusal.Message.Should().Contain("Jint.getMarkdown");

        browser.Disconnect();
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

    /// <summary>Runs <paramref name="work"/> and answers what it threw, or <see langword="null"/>.</summary>
    private static async Task<Exception?> CatchAsync(Func<Task> work)
    {
        try
        {
            await work();
            return null;
        }
#pragma warning disable CA1031 // the test is about which exception a refusal produces, whatever kind it is
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return exception;
        }
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

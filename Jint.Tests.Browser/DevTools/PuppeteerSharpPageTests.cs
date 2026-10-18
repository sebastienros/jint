using Jint.Browser;
using Jint.DevTools;
using Jint.Tests.Browser.Navigation;
using PuppeteerSharp;
using PuppeteerSharp.PageAccessibility;
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

    /// <summary>What every context of this test's browser sends through, and what <see cref="NothingLeftTheMachine"/> reads.</summary>
    private readonly LoopbackOnlyTransport _network = new();

    /// <summary>
    /// Fails a test whose browser was not built through <see cref="NewBrowser"/>, or whose page reached for a
    /// host that is not loopback.
    /// </summary>
    [TearDown]
    public void NothingLeftTheMachine()
    {
        try
        {
            _network.AssertNothingEscaped();
        }
        finally
        {
            _network.Dispose();
        }
    }

    /// <summary>
    /// A browser every context of which loads only what <paramref name="filter"/> admits, through
    /// <see cref="_network"/> — the default context included, which is where <c>browser.NewPageAsync()</c> opens
    /// a page.
    /// </summary>
    private global::Jint.Browser.Browser NewBrowser(Func<Uri, bool> filter) => new(_network.Install(new BrowserOptions(), filter));

    [Test]
    public async Task PuppeteerBrowserSessionCoexistsWithPageAutomation()
    {
        await using var pages = NewBrowser(static uri => uri.IsLoopback);
        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();
        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
            DefaultViewport = null,
        }).WaitAsync(Bound);

        var session = await browser.CreateCDPSessionAsync().WaitAsync(Bound);
        var version = await session.SendAsync("Browser.getVersion").WaitAsync(Bound);
        version!.Value.GetProperty("product").GetString().Should().StartWith("Jint/");
        await using var page = await browser.NewPageAsync().WaitAsync(Bound);
        await session.DetachAsync().WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<int>("6 * 7").WaitAsync(Bound)).Should().Be(42);
        await page.CloseAsync().WaitAsync(Bound);
        browser.Disconnect();
    }

    [Test]
    public async Task PuppeteerOpensAPageNavigatesEvaluatesAndCloses()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>First</title></head><body><p id='greeting'>hello</p></body></html>");
        origin.MapHtml("/two", "<html><head><title>Second</title></head><body><p id='greeting'>again</p></body></html>");

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        browser.IsConnected.Should().BeTrue();

        // The client's own newPage, which is Target.createTarget with no browserContextId, so the page opens in
        // the browser's default context, as it would in Chrome. That is why NewBrowser bounds every context
        // rather than one created beside the default.
        var page = await browser.NewPageAsync().WaitAsync(Bound);
        pages.DefaultContext.Pages.Should().ContainSingle("a createTarget that names no context opens its page in the default one");

        // The navigation runs and the client gets a response object back, which it builds out of the
        // Network.responseReceived for the document request -- the one whose requestId is the loaderId.
        var response = await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);
        response.Should().NotBeNull();
        response!.Status.Should().Be(System.Net.HttpStatusCode.OK);
        response.Url.Should().Be(origin.Url("/one"));
        response.Headers.Should().ContainKey("content-type");
        (await response.TextAsync()).Should().Contain("hello");

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

        await using var pages = NewBrowser(origin.Owns);

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
    }

    /// <summary>
    /// The keyboard, through the client's own API: <c>TypeAsync</c>, <c>PressAsync</c>, a held modifier and
    /// the <kbd>Tab</kbd> that moves focus.
    /// </summary>
    /// <remarks>
    /// Every one of these is <c>Input.dispatchKeyEvent</c> as PuppeteerSharp composes it — <c>keyDown</c> with
    /// <c>text</c> for a character, <c>rawKeyDown</c> without for an editing key, its own running modifier bit
    /// field, and <c>Input.insertText</c> for a character its US layout has no key for. What the page sees is
    /// what a page sees in Chrome, and the assertions are written on that rather than on the wire.
    /// </remarks>
    [Test]
    public async Task PuppeteerTypesEditsAndSubmits()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml(
            "/form",
            """
            <html><head><title>Form</title></head><body>
              <form id="f" action="/done" method="get">
                <input id="name" name="name" type="text">
                <input id="city" name="city" type="text">
                <button id="go" type="submit">Go</button>
              </form>
              <script>
                window.events = [];
                const name = document.getElementById('name');
                name.addEventListener('input', e => window.events.push('input:' + e.inputType + ':' + (e.data ?? '')));
                name.addEventListener('change', () => window.events.push('change:' + name.value));
                document.getElementById('city').addEventListener('focus', () => window.events.push('focus:city'));
              </script>
            </body></html>
            """);

        origin.MapHtml("/done", "<html><head><title>Done</title></head><body><p>submitted</p></body></html>");

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/form")).WaitAsync(Bound);

        // type() clicks the element and then sends one key press per character.
        await page.TypeAsync("#name", "hello").WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("document.getElementById('name').value").WaitAsync(Bound))
            .Should().Be("hello");

        (await page.EvaluateExpressionAsync<string>("window.events.join('|')").WaitAsync(Bound))
            .Should().Be("input:insertText:h|input:insertText:e|input:insertText:l|input:insertText:l|input:insertText:o");

        // A held modifier: Shift down, two arrows, Shift up — and the client puts the modifier in each event's
        // bit field, which is what makes the selection extend rather than the caret move.
        await page.Keyboard.PressAsync("End").WaitAsync(Bound);
        await page.Keyboard.DownAsync("Shift").WaitAsync(Bound);
        await page.Keyboard.PressAsync("ArrowLeft").WaitAsync(Bound);
        await page.Keyboard.PressAsync("ArrowLeft").WaitAsync(Bound);
        await page.Keyboard.UpAsync("Shift").WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>(
            "(() => { const n = document.getElementById('name'); return [n.selectionStart, n.selectionEnd].join(','); })()")
            .WaitAsync(Bound)).Should().Be("3,5");

        // …and the selection is what the next key replaces.
        await page.Keyboard.PressAsync("Backspace").WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("document.getElementById('name').value").WaitAsync(Bound))
            .Should().Be("hel");

        // Input.insertText, which is how a client sends a character its keyboard layout has no key for.
        await page.Keyboard.SendCharacterAsync("ü").WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("document.getElementById('name').value").WaitAsync(Bound))
            .Should().Be("helü");

        // Tab moves focus along the sequential focus order, firing the focus events on the way.
        await page.Keyboard.PressAsync("Tab").WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("document.activeElement.id").WaitAsync(Bound)).Should().Be("city");
        (await page.EvaluateExpressionAsync<string>("window.events.join('|')").WaitAsync(Bound))
            .Should().Contain("focus:city");

        // Enter in a single-line control is HTML's implicit submission, which navigates.
        await Task.WhenAll(
            page.WaitForNavigationAsync(),
            page.Keyboard.PressAsync("Enter")).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("Done");
        page.Url.Should().Contain("name=hel");

        browser.Disconnect();
    }

    /// <summary>
    /// The emulation half, driven by the library's own members rather than by raw commands.
    /// </summary>
    /// <remarks>
    /// Each of these is a page-level API a client library offers because Chrome answers it, and each of them
    /// is a different one of the two shapes C5 delivers: <c>EmulateMediaFeaturesAsync</c> moves the document
    /// that is loaded, while <c>EmulateTimezoneAsync</c> and <c>SetJavaScriptEnabledAsync</c> are read when
    /// the next document's engine is built — which is exactly the order a client uses them in, since both are
    /// set before a <c>goto</c>.
    /// </remarks>
    [Test]
    public async Task PuppeteerEmulatesTheMediaTheTimeZoneAndScriptExecution()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml(
            "/emulation",
            """
            <html><head><title>Emulation</title></head><body>
              <script>
                window.ran = true;
                window.changes = [];
                window.dark = matchMedia('(prefers-color-scheme: dark)');
                window.dark.addEventListener('change', e => window.changes.push(e.matches));
              </script>
            </body></html>
            """);

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);

        // The time zone is set before the navigation, which is where a client sets it and where it becomes
        // effective: the engine one document runs in is built with it.
        await page.EmulateTimezoneAsync("Asia/Tokyo").WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/emulation")).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<int>("new Date().getTimezoneOffset()").WaitAsync(Bound)).Should().Be(-540);
        (await page.EvaluateExpressionAsync<bool>("window.ran === true").WaitAsync(Bound)).Should().BeTrue();
        (await page.EvaluateExpressionAsync<bool>("window.dark.matches").WaitAsync(Bound)).Should().BeFalse();

        // …and the media features move the document that is already showing, with the change event that goes
        // with them.
        await page.EmulateMediaFeaturesAsync(
        [
            new MediaFeatureValue { MediaFeature = MediaFeature.PrefersColorScheme, Value = "dark" },
        ]).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<bool>("window.dark.matches").WaitAsync(Bound)).Should().BeTrue();
        (await page.EvaluateExpressionAsync<string>("window.changes.join('|')").WaitAsync(Bound)).Should().Be("true");

        // Turning scripting off stops the *next* document's own scripts and leaves evaluation working, which
        // is what the client's own documentation promises.
        await page.SetJavaScriptEnabledAsync(false).WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/emulation")).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("typeof window.ran").WaitAsync(Bound)).Should().Be("undefined");
        (await page.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("Emulation");

        browser.Disconnect();
    }

    /// <summary>
    /// <c>page.accessibility.snapshot()</c>, which is the accessibility tree read through a library that
    /// knows nothing about how it was computed.
    /// </summary>
    [Test]
    public async Task PuppeteerTakesAnAccessibilitySnapshot()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml(
            "/aria",
            """
            <html><head><title>Aria</title></head><body>
              <h1>Report</h1>
              <button>Save changes</button>
              <input aria-label="Your name">
            </body></html>
            """);

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/aria")).WaitAsync(Bound);

        var snapshot = await page.Accessibility.SnapshotAsync().WaitAsync(Bound);

        snapshot.Should().NotBeNull("the client builds its tree out of Accessibility.getFullAXTree");
        snapshot!.Role.Should().Be("RootWebArea");
        snapshot.Name.Should().Be("Aria");

        var flattened = Flatten(snapshot).ToArray();

        flattened.Should().Contain(node => node.Role == "button" && node.Name == "Save changes");
        flattened.Should().Contain(node => node.Role == "textbox" && node.Name == "Your name");
        flattened.Should().Contain(node => node.Role == "heading" && node.Name == "Report");

        browser.Disconnect();
    }

    private static IEnumerable<SerializedAXNode> Flatten(SerializedAXNode node)
    {
        yield return node;

        foreach (var child in node.Children ?? [])
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    [Test]
    public async Task PuppeteerSeesEveryRequestThePageMakes()
    {
        using var origin = new LoopbackServer();
        origin.Map("/rows.json", _ => LoopbackResponse.Json("""{"rows":[1,2,3]}"""));
        origin.MapHtml("/one", "<html><head><title>Requests</title></head><body>one</body></html>");

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);

        var requested = new List<string>();
        var answered = new List<int>();
        page.Request += (_, e) => requested.Add(e.Request.Url);
        page.Response += (_, e) => answered.Add((int) e.Response.Status);

        await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);
        await page.EvaluateExpressionAsync("fetch('/rows.json').then(r => r.text())").WaitAsync(Bound);

        await WaitUntilAsync(() => requested.Contains(origin.Url("/rows.json")));

        requested.Should().Contain(origin.Url("/one"));
        answered.Should().AllSatisfy(status => status.Should().Be(200));

        await page.CloseAsync().WaitAsync(Bound);
        browser.Disconnect();
    }

    [Test]
    public async Task PuppeteerInterceptsRequestsAndAnswersThemItself()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>Origin</title></head><body>from the server</body></html>");
        origin.Map("/blocked.js", _ => LoopbackResponse.Script("globalThis.__blocked = true;"));

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);
        await page.SetRequestInterceptionAsync(true).WaitAsync(Bound);

        page.Request += async (_, e) =>
        {
            if (e.Request.Url.EndsWith("/blocked.js", StringComparison.Ordinal))
            {
                await e.Request.AbortAsync();
                return;
            }

            if (e.Request.Url.EndsWith("/one", StringComparison.Ordinal))
            {
                await e.Request.RespondAsync(new ResponseData
                {
                    Status = System.Net.HttpStatusCode.OK,
                    ContentType = "text/html; charset=utf-8",
                    Body = "<html><head><title>Fulfilled</title></head>"
                        + "<body><script src=\"/blocked.js\"></script>from the client</body></html>",
                });

                return;
            }

            await e.Request.ContinueAsync();
        };

        var fulfilled = await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);
        fulfilled.Should().NotBeNull();
        (await fulfilled!.TextAsync()).Should().Contain("from the client");

        // The document the page really parsed is the client's, not the origin's.
        (await page.EvaluateExpressionAsync<string>("document.title").WaitAsync(Bound)).Should().Be("Fulfilled");
        origin.Received.Should().NotContain(request => request.Path == "/one",
            because: "a request the client fulfilled never reaches the origin");
        origin.Received.Should().NotContain(request => request.Path == "/blocked.js",
            because: "a request the client aborted never reaches the origin either");

        await page.CloseAsync().WaitAsync(Bound);
        browser.Disconnect();
    }

    [Test]
    public async Task PuppeteerSetsHeadersCookiesAndTakesThePageOffline()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>Headers</title></head><body>one</body></html>");

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);

        await page.SetExtraHttpHeadersAsync(new Dictionary<string, string> { ["X-Client"] = "puppeteer" }).WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);

        origin.Received.Single(request => request.Path == "/one").Header("X-Client").Should().Be("puppeteer");

        await page.SetCookieAsync(new CookieParam { Name = "seen", Value = "yes", Url = origin.Url("/one") }).WaitAsync(Bound);
        var cookies = await page.GetCookiesAsync().WaitAsync(Bound);
        cookies.Should().Contain(cookie => cookie.Name == "seen" && cookie.Value == "yes");

        // And the page's own script reads the same jar, which is what a login flow depends on.
        (await page.EvaluateExpressionAsync<string>("document.cookie").WaitAsync(Bound)).Should().Contain("seen=yes");

        await page.SetOfflineModeAsync(true).WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<bool>("navigator.onLine").WaitAsync(Bound)).Should().BeFalse();
        var offline = async () => await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);
        await offline.Should().ThrowAsync<Exception>("a page that believes it is offline cannot navigate");

        // And back on again, through the same client call.
        await page.SetOfflineModeAsync(false).WaitAsync(Bound);
        var online = await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);
        online.Should().NotBeNull();
        online!.Ok.Should().BeTrue();
        (await page.EvaluateExpressionAsync<bool>("navigator.onLine").WaitAsync(Bound)).Should().BeTrue();

        await page.CloseAsync().WaitAsync(Bound);
        browser.Disconnect();
    }

    /// <summary>
    /// <c>ConnectOptions.BlockList</c> refuses a page's <b>own</b> requests to a blocked URL, as Chrome does, and
    /// not only the navigations PuppeteerSharp checks itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PuppeteerSharp sends the list as <c>Network.emulateNetworkConditionsByRule</c> — one offline entry per
    /// pattern, beside the deprecated top-level <c>offline</c> — from
    /// <c>ChromeTargetManager.MaybeSetupNetworkBlockListAsync</c> on every target it attaches to, and checks a
    /// <c>GoToAsync</c> against the list itself. A <c>fetch</c> the page makes reaches no client-side check: it is
    /// refused here, by the page's network conditions, with Chrome's <c>net::ERR_INTERNET_DISCONNECTED</c>.
    /// </para>
    /// <para>
    /// The patterns are the URLPattern syntax the protocol names. The browser admits every host, so that what
    /// refuses a blocked URL is the client's list and not a filter of ours, and the transport answers every
    /// host that is not loopback in process (<see cref="LoopbackOnlyTransport.AnswerOtherHostsInProcess"/>): a
    /// blocked request that slipped through would be answered there rather than reach the network, which is
    /// also what the test reads to prove that none did.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PuppeteerBlockListFailsThePagesOwnRequestsToABlockedUrl()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>Allowed</title></head><body>one</body></html>");
        origin.Map("/data", _ => LoopbackResponse.Text("went"));

        _network.AnswerOtherHostsInProcess = true;
        await using var pages = NewBrowser(static _ => true);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        using var log = new RecordingLoggerFactory();
        await using var browser = await Puppeteer.ConnectAsync(
            new ConnectOptions
            {
                BrowserWSEndpoint = server.BrowserWebSocketUrl,
                BlockList = ["*://example.com/*", "*://*.evil.com/*"],
            },
            log).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);
        var failures = new System.Collections.Concurrent.ConcurrentQueue<string>();
        page.RequestFailed += (_, e) => failures.Enqueue(e.Request.Url + " " + e.Request.FailureText);

        var allowed = await page.GoToAsync(origin.Url("/one")).WaitAsync(Bound);
        allowed.Should().NotBeNull();
        allowed!.Ok.Should().BeTrue();

        (await FetchAsync(page, "http://example.com/data")).Should().Be("refused");
        (await FetchAsync(page, "https://cdn.evil.com/tracker.js")).Should().Be("refused");

        (await FetchAsync(page, "/data")).Should().Be("went", "the origin is on neither pattern");
        (await FetchAsync(page, "http://evil.com/data")).Should().Be("went", "*.evil.com names a subdomain, and URLPattern reads it so");

        await WaitUntilAsync(() => failures.Count >= 2);
        failures.Should().BeEquivalentTo(
            "http://example.com/data net::ERR_INTERNET_DISCONNECTED",
            "https://cdn.evil.com/tracker.js net::ERR_INTERNET_DISCONNECTED");
        _network.Answered.Should().Equal(["http://evil.com/data"], "a blocked request opens no connection, so only the allowed one was answered");

        // PuppeteerSharp logs and swallows a refused list, so the log is where a refusal would show. The page's
        // attachment accepted it; the tab's has no Network domain and answers -32601.
        log.Errors.Where(entry => entry.Message == "Failed to setup network block list").Should().OnlyContain(
            entry => entry.Exception != null && entry.Exception.Message.Contains("wasn't found", StringComparison.Ordinal));

        await page.CloseAsync().WaitAsync(Bound);
        browser.Disconnect();
    }

    /// <summary>Fetches <paramref name="url"/> from the page, answering its body or <c>refused</c>.</summary>
    private static Task<string> FetchAsync(IPage page, string url)
        => page.EvaluateFunctionAsync<string>("url => fetch(url, {cache: 'no-store'}).then(r => r.text(), () => 'refused')", url).WaitAsync(Bound);

    /// <summary>An <see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> that keeps every error a client logged.</summary>
    private sealed class RecordingLoggerFactory : Microsoft.Extensions.Logging.ILoggerFactory
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Message, Exception? Exception)> _errors = new();

        /// <summary>Every entry logged at error level or above, as its message and its exception.</summary>
        internal IReadOnlyCollection<(string Message, Exception? Exception)> Errors => [.. _errors];

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Recorder(this);

        public void AddProvider(Microsoft.Extensions.Logging.ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class Recorder(RecordingLoggerFactory owner) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(
                Microsoft.Extensions.Logging.LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
                {
                    owner._errors.Enqueue((formatter(state, exception), exception));
                }
            }
        }
    }

    /// <summary>Polls a condition, failing rather than hanging.</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = Environment.TickCount64 + (long) Bound.TotalMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"the condition was still false after {Bound}.");
    }

    [Test]
    public async Task PuppeteerReadsAPageAHostAlreadyOpened()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml("/one", "<html><head><title>Existing</title></head><body>one</body></html>");

        await using var pages = NewBrowser(origin.Owns);
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

    /// <summary>
    /// <c>page.coverage.startCSSCoverage()</c>, which is the whole reason the <c>CSS</c> domain's
    /// rule-usage half exists.
    /// </summary>
    /// <remarks>
    /// Puppeteer builds this out of five calls and this test asserts none of them: what it asserts is the
    /// thing only a client can, which is that the five fit together. The library enables <c>DOM</c> and
    /// <c>CSS</c>, starts rule-usage tracking, collects a <c>styleSheetAdded</c> and reads that sheet's text,
    /// stops tracking and then <b>slices that text with the offsets it was handed</b>. A header with an empty
    /// <c>sourceURL</c>, a text that did not come from the same serialization as the offsets, or a range past
    /// the end of it, and what comes back is empty or garbage rather than the rules the page used.
    /// </remarks>
    [Test]
    public async Task PuppeteerReadsCssCoverageOffThePage()
    {
        using var origin = new LoopbackServer();
        origin.MapHtml(
            "/styled",
            """
            <html><head><title>Styled</title><style>
            .used { color: rgb(1, 2, 3) }
            .never { color: rgb(4, 5, 6) }
            </style></head>
            <body><p id="box" class="used">text</p></body></html>
            """);

        await using var pages = NewBrowser(origin.Owns);

        await using var server = new DevToolsServer();
        await server.AddBrowser(pages);
        await server.StartAsync();

        await using var browser = await Puppeteer.ConnectAsync(new ConnectOptions
        {
            BrowserWSEndpoint = server.BrowserWebSocketUrl,
        }).WaitAsync(Bound);

        var page = await browser.NewPageAsync().WaitAsync(Bound);
        await page.GoToAsync(origin.Url("/styled")).WaitAsync(Bound);

        await page.Coverage.StartCSSCoverageAsync().WaitAsync(Bound);
        var entries = await page.Coverage.StopCSSCoverageAsync().WaitAsync(Bound);

        entries.Should().NotBeEmpty("the page has a style sheet and one of its rules matched an element");

        var entry = entries.Single(candidate => candidate.Text.Contains(".used", StringComparison.Ordinal));
        entry.Url.Should().Be(origin.Url("/styled"), "an inline sheet is reported under the document's URL");
        entry.Ranges.Should().NotBeEmpty();

        var covered = string.Concat(entry.Ranges.Select(range => entry.Text[range.Start..range.End]));
        covered.Should().Contain(".used");
        covered.Should().NotContain(".never", "no element carries that class");

        await page.CloseAsync().WaitAsync(Bound);
        browser.Disconnect();
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

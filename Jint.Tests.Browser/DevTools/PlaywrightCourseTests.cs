using Jint.Browser;
using Jint.DevTools;
using Jint.Tests.Browser.Fixtures;
using Jint.Tests.Browser.Navigation;
using Microsoft.Playwright;
using PageContextOptions = Jint.Browser.BrowserContextOptions;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// Playwright for .NET driving the obstacle course over <c>connectOverCDP</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second client, and it is not a duplicate of the first.</b> Playwright sends a different protocol —
/// 47 commands against PuppeteerSharp's 45 — and it reaches a page differently at every step: it discovers
/// the browser over HTTP rather than being handed a socket, it insists that every page target name a browser
/// context, it scrolls an element into view and asks for its content quads before clicking rather than
/// describing the node, and its locators re-resolve on every use instead of holding a handle. A server that
/// satisfies one and not the other is a server written to one client's habits, and two of the three defects
/// this suite found were exactly that.
/// </para>
/// <para>
/// <b>Every interaction here passes <c>Force</c>, and that is a finding rather than a style.</b> Playwright's
/// actionability check ends in <c>if (style.visibility !== "visible") return false</c>, and
/// <c>getComputedStyle(el).visibility</c> is the <b>empty string</b> here for every element — AngleSharp's
/// cascade reports what something declared, and <c>visibility: visible</c> is CSS's initial value, so nothing
/// declares it. So Playwright believes every element on every page is hidden, and an unforced
/// <c>ClickAsync</c> or <c>WaitForSelectorAsync</c> waits out its timeout. The divergence is recorded in
/// <c>Jint.Browser/AGENTS.md</c> and pinned by <see cref="PlaywrightCallsEveryElementHidden"/>; this suite
/// drives past it through Playwright's own documented escape hatches rather than pretending it is not there.
/// </para>
/// <para>
/// <b>It needs a driver, and that is the whole of why this suite is gated.</b> Playwright for .NET is a
/// wrapper over a Node program; the NuGet package carries both the program and a node runtime for the host's
/// RID, so nothing is downloaded and <c>Microsoft.Playwright.Program.Main(["install"])</c> is <i>not</i>
/// needed — <c>connectOverCDP</c> attaches to an endpoint that already exists and never wants a browser. What
/// it does cost is a process start and an IPC handshake on every run, which is why the default legs leave it
/// alone and the <c>browser-clients</c> CI leg sets <c>JINT_BROWSER_CLIENTS</c>.
/// </para>
/// </remarks>
[NonParallelizable]
public class PlaywrightCourseTests
{
    /// <summary>The switch the <c>browser-clients</c> leg sets. Any non-empty value turns the suite on.</summary>
    internal const string Gate = "JINT_BROWSER_CLIENTS";

    /// <summary>Waiting for an element to exist, which is as far as an actionability check can get here.</summary>
    private static readonly LocatorWaitForOptions Attached = new() { State = WaitForSelectorState.Attached };

    /// <summary>Clicking without the actionability check that would refuse every element on every page.</summary>
    private static LocatorClickOptions Forced => new() { Force = true };

    [SetUp]
    public void OnlyWhereTheDriverIsWanted()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Gate)))
        {
            Assert.Ignore(
                $"Playwright for .NET starts a Node driver process, so this suite runs only where {Gate} is set — "
                + "the browser-clients CI leg. Nothing is downloaded either way; the package carries the driver.");
        }
    }

    /// <summary>The React TodoMVC, driven by locators, <c>Fill</c> and the keyboard.</summary>
    [Test]
    public async Task PlaywrightDrivesTheReactTodoList()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("todomvc-react");

        // The document arrives with an empty <div id="root">, so this is a real wait: React fills it in from
        // a bundle the page had to fetch.
        await page.Locator(".new-todo").WaitForAsync(Attached);

        foreach (var todo in new[] { "buy milk", "write the fixture", "read the standard" })
        {
            // Fill is Playwright's own: it selects everything and writes the value through the page's own
            // setter before firing `input`, which is why it reaches a controlled React field at all.
            await page.Locator(".new-todo").FillAsync(todo, new LocatorFillOptions { Force = true });

            // Focus and then the keyboard, rather than Locator.PressAsync, which has no Force of its own.
            await page.Locator(".new-todo").FocusAsync();
            await page.Keyboard.PressAsync("Enter");
        }

        await page.Locator(".todo-list li").Nth(2).WaitForAsync(Attached);

        (await page.Locator(".todo-list li label").AllTextContentsAsync())
            .Should().Equal("buy milk", "write the fixture", "read the standard");

        // A locator re-resolves on every use, so this click is a fresh query, a scrollIntoViewIfNeeded, a
        // getContentQuads and a mouse event at the point it was told -- a path PuppeteerSharp never takes.
        await page.Locator(".todo-list li .toggle").Nth(1).ClickAsync(Forced);

        await page.Locator(".todo-list li.completed").WaitForAsync(Attached);
        (await page.Locator(".todo-count strong").TextContentAsync()).Should().Be("2");

        // A filter is an ordinary link to a fragment, so this also drives a same-document navigation.
        await page.Locator(".filters a[href='#/active']").ClickAsync(Forced);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 2");

        await page.Locator(".filters a[href='#/']").ClickAsync(Forced);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 3");

        // GetByRole goes through Playwright's *injected script*, not the Accessibility domain: its role engine
        // is JavaScript it evaluates in the page, so what this exercises is the DOM and HTML-AAM's implicit
        // roles rather than the tree Accessibility.getFullAXTree answers with. Both paths are covered -- the
        // PuppeteerSharp suite takes the other one through Accessibility.SnapshotAsync.
        //
        // IncludeHidden for the same reason every click here is forced: the role engine drops an element that
        // is hidden for ARIA, and its hidden test is the visibility check that the empty `visibility` fails.
        var destroy = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "x", IncludeHidden = true }).First;
        await destroy.ClickAsync(Forced);

        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 2");
        (await page.Locator(".todo-list li label").AllTextContentsAsync())
            .Should().Equal("write the fixture", "read the standard");

        await page.CloseAsync();
    }

    /// <summary>The router, and the emulated colour scheme the page reads through <c>matchMedia</c>.</summary>
    [Test]
    public async Task PlaywrightMovesThroughTheRouterAndEmulatesTheColourScheme()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("spa-router");

        (await page.Locator("#view").TextContentAsync()).Should().Be("the home view");

        await page.Locator(".route[href='/spa-router/about']").ClickAsync(Forced);
        await page.WaitForFunctionAsync("() => location.pathname === '/spa-router/about'");

        // GoBack is Page.getNavigationHistory + Page.navigateToHistoryEntry, and the entry it lands on is a
        // pushState sibling -- so the page hears popstate and is never reloaded.
        await page.GoBackAsync();
        await page.WaitForFunctionAsync("() => location.pathname === '/spa-router/index.html'");
        await page.WaitForFunctionAsync("() => document.querySelector('#pops').textContent === '1'");

        // Emulation.setEmulatedMedia, which the page hears as a media query changing under it.
        (await page.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: dark)').matches"))
            .Should().BeFalse("a page with no override is light");

        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });

        (await page.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: dark)').matches"))
            .Should().BeTrue("the client said the scheme is dark, so the page's own matchMedia says so too");

        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Light });

        (await page.EvaluateAsync<bool>("() => matchMedia('(prefers-color-scheme: dark)').matches"))
            .Should().BeFalse();

        await page.CloseAsync();
    }

    /// <summary>A route the client fulfils itself, and the cookies its context reads afterwards.</summary>
    [Test]
    public async Task PlaywrightFulfilsARouteAndReadsTheCookies()
    {
        await using var lane = await ClientLane.OpenAsync(server => FixtureRoutes.CookieLogin(server));
        var page = await lane.NewPageAsync("fetch-json");

        // RouteAsync is Playwright's Fetch.enable plus a matcher of its own: the page's fetch never leaves
        // the process.
        await page.RouteAsync(
            "**/rows.json",
            route => route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/json",
                Body = """{"rows":[{"name":"fulfilled","count":7}]}""",
            }));

        await page.ReloadAsync();

        await page.WaitForFunctionAsync(
            "() => document.querySelector('#rows li').textContent === 'fulfilled (7)'");

        lane.Server.Received.Count(request => request.Path == "/fetch-json/rows.json")
            .Should().Be(1, "the first load reached the origin and the reload was answered by the client");

        // And the cookie half, over the same context: sign in, then read the jar the client can see.
        await page.GotoAsync(lane.Url("cookie-login"));
        await page.Locator("#sign-in").ClickAsync(Forced);
        await page.Locator("#welcome").WaitForAsync(Attached);

        (await page.Locator("#welcome").TextContentAsync()).Should().Be("welcome, ada");

        var cookies = await page.Context.CookiesAsync();
        cookies.Should().Contain(cookie => cookie.Name == "session" && cookie.Value == "ada-is-in");
        cookies.Should().Contain(cookie => cookie.Name == "theme");

        await page.CloseAsync();
    }

    /// <summary>
    /// The reason every interaction above is forced: Playwright reads an empty <c>visibility</c> as hidden.
    /// </summary>
    /// <remarks>
    /// <b>This asserts a defect, and it is here so that fixing the defect fails it.</b> CSSOM says
    /// <c>getComputedStyle</c> answers the resolved value of every supported property; AngleSharp answers only
    /// what the cascade declared, and nothing declares <c>visibility: visible</c> because it is the initial
    /// value. Playwright's actionability check is <c>style.visibility !== "visible"</c>, so it concludes that
    /// every element of every page is hidden — <c>IsVisibleAsync</c> is <see langword="false"/> for an element
    /// with a real 1280×16 box, and an unforced click waits out its timeout. The repository's standing
    /// decision (see <c>Views/ComputedStyleTests</c>) is to record this rather than paper over it with an
    /// initial-value table; what is new is that a supported client is unusable without one.
    /// </remarks>
    [Test]
    public async Task PlaywrightCallsEveryElementHidden()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("todomvc-react");

        await page.Locator(".new-todo").WaitForAsync(Attached);

        (await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.new-todo')).visibility"))
            .Should().BeEmpty("nothing in the cascade declares visibility, and AngleSharp does not resolve initial values");

        (await page.EvaluateAsync<string>(
            "() => { const r = document.querySelector('.new-todo').getBoundingClientRect(); return r.width + 'x' + r.height; }"))
            .Should().Be("1280x16", "the flat box model gives it a real box, so the box is not what is missing");

        (await page.Locator(".new-todo").IsVisibleAsync())
            .Should().BeFalse("Playwright's check ends in style.visibility !== 'visible', and the empty string is not 'visible'");

        await page.CloseAsync();
    }

    /// <summary>A server serving the course, a browser, a protocol server and a connected Playwright.</summary>
    private sealed class ClientLane : IAsyncDisposable
    {
        private readonly IPlaywright _playwright;

        private ClientLane(
            LoopbackServer server,
            global::Jint.Browser.Browser pages,
            DevToolsServer protocol,
            IPlaywright playwright,
            IBrowser client,
            IBrowserContext context)
        {
            Server = server;
            Pages = pages;
            Protocol = protocol;
            _playwright = playwright;
            Client = client;
            Context = context;
        }

        internal LoopbackServer Server { get; }

        internal global::Jint.Browser.Browser Pages { get; }

        internal DevToolsServer Protocol { get; }

        internal IBrowser Client { get; }

        internal IBrowserContext Context { get; }

        internal static async Task<ClientLane> OpenAsync(Action<LoopbackServer>? routes = null)
        {
            var server = FixtureOrigin.Serve(new LoopbackServer());
            routes?.Invoke(server);

            var pages = new global::Jint.Browser.Browser();
            await pages.NewContextAsync(new PageContextOptions { UrlFilter = server.Owns }).ConfigureAwait(false);

            var protocol = new DevToolsServer();
            await protocol.AddBrowser(pages).ConfigureAwait(false);
            await protocol.StartAsync().ConfigureAwait(false);

            var playwright = await Playwright.CreateAsync().ConfigureAwait(false);

            // The HTTP form, which is the one Playwright's own documentation gives and the one the recording
            // was made with: it reads webSocketDebuggerUrl out of /json/version and connects to that.
            var client = await playwright.Chromium.ConnectOverCDPAsync(protocol.BrowserHttpUrl).ConfigureAwait(false);

            // connectOverCDP adopts the browser's existing contexts rather than making one, which is the
            // difference between attaching to a browser and launching one.
            var context = client.Contexts.Count > 0
                ? client.Contexts[0]
                : await client.NewContextAsync().ConfigureAwait(false);

            return new ClientLane(server, pages, protocol, playwright, client, context);
        }

        internal string Url(string fixture) => FixtureOrigin.Url(Server, fixture);

        internal async Task<IPage> NewPageAsync(string fixture)
        {
            var page = await Context.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync(Url(fixture)).ConfigureAwait(false);
            return page;
        }

        public async ValueTask DisposeAsync()
        {
            await Client.CloseAsync().ConfigureAwait(false);
            _playwright.Dispose();
            await Protocol.DisposeAsync().ConfigureAwait(false);
            await Pages.CloseAsync().ConfigureAwait(false);
            Server.Dispose();
        }
    }
}

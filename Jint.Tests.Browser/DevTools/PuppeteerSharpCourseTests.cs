using System.Text.Json;
using Jint.Browser;
using Jint.DevTools;
using Jint.Tests.Browser.Fixtures;
using Jint.Tests.Browser.Navigation;
using PuppeteerSharp;
using PageContextOptions = Jint.Browser.BrowserContextOptions;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// PuppeteerSharp driving the obstacle course over a real socket: the same three fixtures the in-process
/// suite drives, reached the way an automation script reaches a page.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the fixtures rather than another hand-written document.</b> <c>PuppeteerSharpPageTests</c> already
/// says that <c>$</c>, <c>click</c> and <c>waitForSelector</c> work on a page written to exercise them. What
/// this says is different and is the point of the course: a client's own input — trusted, through the
/// protocol, with none of <c>Fixtures/harness.js</c> — drives a page built by React, a router built on
/// <c>pushState</c> and a login built on cookies, and gets the same end state the in-process suite asserts.
/// </para>
/// <para>
/// <b>Nothing is launched and nothing is downloaded.</b> <c>ConnectAsync</c> speaks to an endpoint that
/// already exists. The version is the one
/// <c>tools/devtools-protocol/handshakes/puppeteersharp-dotnet.json</c> was recorded with, so what it sends
/// here is what that file says it sends — and this suite sends more than the recording's scenario does, which
/// is why it can find a command the replay cannot.
/// </para>
/// </remarks>
[NonParallelizable]
public class PuppeteerSharpCourseTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    /// <summary>The React TodoMVC, driven by a client's own typing, clicking and evaluating.</summary>
    [Test]
    public async Task PuppeteerDrivesTheReactTodoList()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("todomvc-react");

        // waitForSelector is what a script does first, and it is a real wait here: the document arrives with
        // an empty <div id="root"> and React fills it in from a bundle the page had to fetch.
        await page.WaitForSelectorAsync(".new-todo").WaitAsync(Bound);

        foreach (var todo in new[] { "buy milk", "write the fixture", "read the standard" })
        {
            await page.TypeAsync(".new-todo", todo).WaitAsync(Bound);
            await page.Keyboard.PressAsync("Enter").WaitAsync(Bound);
            await page.WaitForFunctionAsync(
                "n => document.querySelectorAll('.todo-list li').length === n",
                Array.IndexOf(new[] { "buy milk", "write the fixture", "read the standard" }, todo) + 1)
                .WaitAsync(Bound);
        }

        var titles = await page.EvaluateFunctionAsync<string[]>(
            "() => Array.from(document.querySelectorAll('.todo-list li label'), e => e.textContent)")
            .WaitAsync(Bound);

        titles.Should().Equal("buy milk", "write the fixture", "read the standard");

        // A click on a checkbox, through the client's mouse, at the box the DOM domain measured.
        var toggles = await page.QuerySelectorAllAsync(".todo-list li .toggle").WaitAsync(Bound);
        await toggles[1].ClickAsync().WaitAsync(Bound);

        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li.completed').length === 1")
            .WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("document.querySelector('.todo-count strong').textContent")
            .WaitAsync(Bound)).Should().Be("2");

        // The filters are ordinary links to a fragment, so this is also a same-document navigation the client
        // never hears about as a load.
        await page.ClickAsync(".filters a[href='#/active']").WaitAsync(Bound);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 2")
            .WaitAsync(Bound);

        // getContent is the whole serialized document, which is a different path from every evaluate above.
        var content = await page.GetContentAsync().WaitAsync(Bound);
        content.Should().Contain("read the standard");
        content.Should().Contain("<html");

        // And the accessibility tree, over a page whose entire structure was built by script.
        var snapshot = await page.Accessibility.SnapshotAsync().WaitAsync(Bound);
        snapshot.Should().NotBeNull();

        // Serialized rather than walked: the node type is the client library's, and what this asserts is that
        // the tree the client was handed names what the page rendered.
        var tree = JsonSerializer.Serialize(snapshot);
        tree.Should().Contain("read the standard");
        tree.Should().Contain("checkbox");

        // The one refusal, in the sentence that names what to ask for instead.
        var refusal = await Catch(() => page.ScreenshotDataAsync().WaitAsync(Bound));
        refusal.Should().NotBeNull("this browser renders no pixels");
        refusal!.Message.Should().Contain("renders no pixels");
        refusal.Message.Should().Contain("Jint.getMarkdown");

        // …and the command the refusal names, which answers over the same session.
        var cdp = await page.CreateCDPSessionAsync().WaitAsync(Bound);
        var markdown = await cdp.SendAsync("Jint.getMarkdown", new { mainContentOnly = false }).WaitAsync(Bound);
        markdown!.Value.GetProperty("markdown").GetString().Should().Contain("read the standard");

        await page.CloseAsync().WaitAsync(Bound);
        lane.Client.Disconnect();
    }

    /// <summary>The router, driven through the client's own back and forward.</summary>
    [Test]
    public async Task PuppeteerDrivesThePushStateRouter()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("spa-router");

        await page.WaitForSelectorAsync("#view").WaitAsync(Bound);
        (await page.EvaluateExpressionAsync<string>("document.querySelector('#view').textContent").WaitAsync(Bound))
            .Should().Be("the home view");

        await page.ClickAsync(".route[href='/spa-router/about']").WaitAsync(Bound);
        await page.WaitForFunctionAsync("() => location.pathname === '/spa-router/about'").WaitAsync(Bound);

        await page.ClickAsync(".route[href='/spa-router/contact']").WaitAsync(Bound);
        await page.WaitForFunctionAsync("() => location.pathname === '/spa-router/contact'").WaitAsync(Bound);

        // GoBackAsync is Page.getNavigationHistory + Page.navigateToHistoryEntry, and the entry it lands on is
        // a pushState sibling — so the page hears popstate and is never reloaded.
        await page.GoBackAsync().WaitAsync(Bound);
        await page.WaitForFunctionAsync("() => location.pathname === '/spa-router/about'").WaitAsync(Bound);
        await page.WaitForFunctionAsync("() => document.querySelector('#pops').textContent === '1'").WaitAsync(Bound);

        await page.GoForwardAsync().WaitAsync(Bound);
        await page.WaitForFunctionAsync("() => location.pathname === '/spa-router/contact'").WaitAsync(Bound);

        // And a link the router does not claim really navigates, which the client sees as a load.
        await Task.WhenAll(
            page.WaitForNavigationAsync(),
            page.ClickAsync("#external")).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("document.querySelector('#left').textContent").WaitAsync(Bound))
            .Should().Be("a real navigation, not a pushState");

        await page.CloseAsync().WaitAsync(Bound);
        lane.Client.Disconnect();
    }

    /// <summary>The cookie login, and the jar the client reads afterwards.</summary>
    [Test]
    public async Task PuppeteerSignsInAndReadsTheJar()
    {
        await using var lane = await ClientLane.OpenAsync(server => FixtureRoutes.CookieLogin(server));
        var page = await lane.NewPageAsync("cookie-login");

        await page.WaitForSelectorAsync("#sign-in").WaitAsync(Bound);

        await Task.WhenAll(
            page.WaitForNavigationAsync(),
            page.ClickAsync("#sign-in")).WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("document.querySelector('#welcome').textContent").WaitAsync(Bound))
            .Should().Be("welcome, ada");

        // Storage.getCookies answers the whole jar, HttpOnly included — which is exactly the difference
        // between a client and the document it is driving.
        var cookies = await page.GetCookiesAsync().WaitAsync(Bound);
        cookies.Should().Contain(cookie => cookie.Name == "session" && cookie.Value == "ada-is-in");
        cookies.Should().Contain(cookie => cookie.Name == "theme");

        (await page.EvaluateExpressionAsync<string>("document.cookie").WaitAsync(Bound))
            .Should().NotContain("session=", "an HttpOnly cookie is invisible to the document");

        await page.CloseAsync().WaitAsync(Bound);
        lane.Client.Disconnect();
    }

    /// <summary>Request interception over the fetch fixture: the client answers the page's own request.</summary>
    [Test]
    public async Task PuppeteerInterceptsTheFixturesFetch()
    {
        await using var lane = await ClientLane.OpenAsync();

        var page = await lane.Client.NewPageAsync().WaitAsync(Bound);
        await page.SetRequestInterceptionAsync(true).WaitAsync(Bound);

        page.Request += async (_, e) =>
        {
            if (e.Request.Url.EndsWith("/fetch-json/rows.json", StringComparison.Ordinal))
            {
                await e.Request.RespondAsync(new ResponseData
                {
                    Status = System.Net.HttpStatusCode.OK,
                    ContentType = "application/json",
                    Body = """{"rows":[{"name":"intercepted","count":9}]}""",
                });

                return;
            }

            await e.Request.ContinueAsync();
        };

        await page.GoToAsync(lane.Url("fetch-json")).WaitAsync(Bound);

        await page.WaitForFunctionAsync("() => document.querySelectorAll('#rows li').length === 1").WaitAsync(Bound);

        (await page.EvaluateExpressionAsync<string>("document.querySelector('#rows li').textContent").WaitAsync(Bound))
            .Should().Be("intercepted (9)");

        lane.Server.Received.Should().NotContain(
            request => request.Path == "/fetch-json/rows.json",
            because: "a request the client answered never reaches the origin");

        await page.CloseAsync().WaitAsync(Bound);
        lane.Client.Disconnect();
    }

    /// <summary>Runs <paramref name="work"/> and answers what it threw, or <see langword="null"/>.</summary>
    private static async Task<Exception?> Catch(Func<Task> work)
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

    /// <summary>A server serving the course, a browser, a protocol server and a connected client.</summary>
    private sealed class ClientLane : IAsyncDisposable
    {
        private ClientLane(LoopbackServer server, global::Jint.Browser.Browser pages, DevToolsServer protocol, IBrowser client)
        {
            Server = server;
            Pages = pages;
            Protocol = protocol;
            Client = client;
        }

        internal LoopbackServer Server { get; }

        internal global::Jint.Browser.Browser Pages { get; }

        internal DevToolsServer Protocol { get; }

        internal IBrowser Client { get; }

        internal static async Task<ClientLane> OpenAsync(Action<LoopbackServer>? routes = null)
        {
            var server = FixtureOrigin.Serve(new LoopbackServer());
            routes?.Invoke(server);

            var pages = new global::Jint.Browser.Browser();
            await pages.NewContextAsync(new PageContextOptions { UrlFilter = server.Owns }).ConfigureAwait(false);

            var protocol = new DevToolsServer();
            await protocol.AddBrowser(pages).ConfigureAwait(false);
            await protocol.StartAsync().ConfigureAwait(false);

            var client = await Puppeteer.ConnectAsync(new ConnectOptions
            {
                BrowserWSEndpoint = protocol.BrowserWebSocketUrl,
            }).WaitAsync(Bound).ConfigureAwait(false);

            return new ClientLane(server, pages, protocol, client);
        }

        internal string Url(string fixture) => FixtureOrigin.Url(Server, fixture);

        internal async Task<IPage> NewPageAsync(string fixture)
        {
            var page = await Client.NewPageAsync().WaitAsync(Bound).ConfigureAwait(false);
            await page.GoToAsync(Url(fixture)).WaitAsync(Bound).ConfigureAwait(false);
            return page;
        }

        public async ValueTask DisposeAsync()
        {
            await Protocol.DisposeAsync().ConfigureAwait(false);
            await Pages.CloseAsync().ConfigureAwait(false);
            Server.Dispose();
        }
    }
}

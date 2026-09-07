using System.Collections.Concurrent;
using System.Text;
using Jint.Browser;
using Jint.Constraints;
using Jint.DevTools;
using Jint.Tests.Browser.Fixtures;
using Jint.Tests.Browser.Layout;
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
/// <b>Nothing here is forced, and that is the assertion.</b> Playwright's actionability check ends in
/// <c>if (style.visibility !== "visible") return false</c>, and until
/// <see href="https://github.com/sebastienros/jint/issues/3713">#3713</see> <c>getComputedStyle(el).visibility</c>
/// was the <b>empty string</b> for every element of every page — so Playwright believed the whole page was
/// hidden, every interaction in this suite passed <c>Force</c>, and <c>GetByRole</c> needed
/// <c>IncludeHidden</c>. <c>getComputedStyle</c> now answers a resolved value for the handful of properties
/// that decide actionability (<c>Jint.Browser/Dom/Views/ResolvedStyle</c>), so every wait below is a wait
/// for <i>visible</i>, every click goes through the whole actionability path, and
/// <see cref="PlaywrightSeesARenderedElementAsVisible"/> pins the check itself.
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
        // a bundle the page had to fetch — and the state waited for is `visible`, which is the actionability
        // check rather than mere existence.
        await page.Locator(".new-todo").WaitForAsync();

        foreach (var todo in new[] { "buy milk", "write the fixture", "read the standard" })
        {
            // Fill is Playwright's own: it selects everything and writes the value through the page's own
            // setter before firing `input`, which is why it reaches a controlled React field at all. It runs
            // the whole actionability wait first — visible, enabled, editable, stable.
            await page.Locator(".new-todo").FillAsync(todo);
            await page.Locator(".new-todo").PressAsync("Enter");
        }

        await page.Locator(".todo-list li").Nth(2).WaitForAsync();

        (await page.Locator(".todo-list li label").AllTextContentsAsync())
            .Should().Equal("buy milk", "write the fixture", "read the standard");

        // A locator re-resolves on every use, so this click is a fresh query, a scrollIntoViewIfNeeded, a
        // getContentQuads and a mouse event at the point it was told -- a path PuppeteerSharp never takes.
        await page.Locator(".todo-list li .toggle").Nth(1).ClickAsync();

        await page.Locator(".todo-list li.completed").WaitForAsync();
        (await page.Locator(".todo-count strong").TextContentAsync()).Should().Be("2");

        // A filter is an ordinary link to a fragment, so this also drives a same-document navigation.
        await page.Locator(".filters a[href='#/active']").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 2");

        await page.Locator(".filters a[href='#/']").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 3");

        // GetByRole goes through Playwright's *injected script*, not the Accessibility domain: its role engine
        // is JavaScript it evaluates in the page, so what this exercises is the DOM and HTML-AAM's implicit
        // roles rather than the tree Accessibility.getFullAXTree answers with. Both paths are covered -- the
        // PuppeteerSharp suite takes the other one through Accessibility.SnapshotAsync.
        //
        // No IncludeHidden: the role engine drops an element that is hidden for ARIA, and its hidden test is
        // the same visibility check the click path makes, so this finding the button is that check passing.
        var destroy = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "x" }).First;
        await destroy.ClickAsync();

        await page.WaitForFunctionAsync("() => document.querySelectorAll('.todo-list li').length === 2");
        (await page.Locator(".todo-list li label").AllTextContentsAsync())
            .Should().Equal("write the fixture", "read the standard");

        await page.CloseAsync();
    }

    [Test]
    public async Task PlaywrightLoadsMonacoThroughItsAmdCssPlugin()
    {
        await using var lane = await ClientLane.OpenAsync(
            server => FixtureRoutes.Monaco(server),
            new BrowserOptions { MaxTaskDuration = TimeSpan.FromSeconds(30) });
        var page = await lane.Context.NewPageAsync();
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Enqueue(message.Text);
            }
        };

        await page.GotoAsync(lane.Url("monaco-amd"));
        await page.WaitForFunctionAsync("() => document.getElementById('status').textContent === 'query { __typename }'");

        (await page.EvaluateAsync<string>("() => typeof monaco.editor.create")).Should().Be("function");
        (await page.EvaluateAsync<int>("() => callbackCount")).Should().Be(1);
        (await page.EvaluateAsync<bool>("() => callbackWasDeferred")).Should().BeTrue();
        (await page.EvaluateAsync<string>("() => resourceEvents.join(',')")).Should().Be("css:true:true");
        errors.Should().BeEmpty();
        foreach (var context in lane.Pages.Contexts)
        {
            foreach (var hostPage in context.Pages)
            {
                hostPage.Errors.Should().BeEmpty();
            }
        }

        await page.CloseAsync();
    }

    [Test]
    public async Task PlaywrightCreatesAFolderInATreeMountedWhileHidden()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("vue-folder-tree");
        await page.WaitForFunctionAsync("() => resizeHeights.length === 1 && resizeHeights[0] === 0");

        await page.Locator("#load").ClickAsync();
        await page.Locator("#children").WaitForAsync();
        await page.Locator("#create").ClickAsync();
        await page.Locator(".folder-name").Nth(1).WaitForAsync();
        (await page.Locator(".folder-name").AllTextContentsAsync()).Should().Equal("Files", "TestFolder");

        foreach (var context in lane.Pages.Contexts)
        {
            foreach (var hostPage in context.Pages)
            {
                hostPage.Errors.Should().BeEmpty();
            }
        }

        await page.CloseAsync();
    }

    [Test]
    public async Task PlaywrightUpdatesTheVueTreeAfterSeparateQueuedTasks()
    {
        var clock = new TaskBudgetClock();
        var options = new BrowserOptions().ConfigureEngine(options =>
        {
            // Only the page's host-owned deadline uses the fake clock; timers and promise waits do not.
            options.RemoveConstraints(static constraint => constraint is OperationDeadlineConstraint);
            options.AddConstraint(() => new OperationDeadlineConstraint(clock));
            options.Configure(engine =>
            {
                engine.SetValue("__checkTaskBudget", () => engine.Constraints.Check());
                engine.SetValue("__queueBudgetedTasks", () =>
                {
                    for (var i = 0; i < 2; i++)
                    {
                        engine.Tasks.Post(() =>
                        {
                            engine.Execute(
                                "Vue.nextTick(() => { __checkTaskBudget(); globalThis.__budgetedReactions++; });");
                            clock.Advance(TimeSpan.FromSeconds(4));
                        });
                    }
                });
            });
        });
        options.MaxTaskDuration.Should().Be(TimeSpan.FromSeconds(5));

        await using var lane = await ClientLane.OpenAsync(options: options);
        var page = await lane.NewPageAsync("vue-folder-tree");
        await page.EvaluateAsync(
            """
            () => {
              globalThis.__budgetedReactions = 0;
              for (const id of ['load', 'create']) {
                document.getElementById(id).addEventListener(
                  'click', () => __queueBudgetedTasks(), { capture: true, once: true });
              }
            }
            """);

        // Both tasks are queued by the actual input dispatch, before either can run. Each costs four
        // seconds, including its Vue reaction; only a drain-wide five-second deadline rejects the pair.
        // Advancing after Execute leaves the old FIFO's pending Vue nextTick jobs to discover the expiry.
        // The reaction checks explicitly rather than depending on the interpreter's amortized cadence.
        await page.Locator("#load").ClickAsync();
        await page.Locator("#children").WaitForAsync();
        await page.Locator("#create").ClickAsync();
        await page.Locator(".folder-name").Nth(1).WaitForAsync();

        (await page.Locator(".folder-name").AllTextContentsAsync()).Should().Equal("Files", "TestFolder");
        foreach (var context in lane.Pages.Contexts)
        {
            foreach (var hostPage in context.Pages)
            {
                hostPage.Errors.Should().BeEmpty();
            }
        }

        (await page.EvaluateAsync<int>("() => globalThis.__budgetedReactions")).Should().Be(4);
        (await page.EvaluateAsync<bool>("() => resizeHeights.some(height => height > 40)")).Should().BeTrue();
        await page.CloseAsync();
    }

    /// <summary>Role locators derive textbox names from HTML's label association.</summary>
    [Test]
    public async Task PlaywrightGetByRoleUsesAssociatedLabels()
    {
        await using var lane = await ClientLane.OpenAsync(server => server.MapHtml(
            "/associated-labels/index.html",
            """
            <!doctype html>
            <html><body>
            <label for="explicit">Explicit</label><input id="explicit">
            <label>Wrapped <input id="wrapped"></label>
            <label for="multiple">First</label>
            <label for="multiple" hidden>Hidden</label>
            <label for="multiple">Second</label>
            <input id="multiple">
            <label for="aria">Native</label><input id="aria" aria-label="ARIA">
            <span id="referenced" hidden>Referenced</span>
            <label for="labelledby">Native</label>
            <input id="labelledby" aria-label="ARIA" aria-labelledby="referenced">
            </body></html>
            """));
        var page = await lane.NewPageAsync("associated-labels");

        (await page.GetByRole(AriaRole.Textbox).CountAsync()).Should().Be(5);
        (await NamedTextboxCount("Explicit")).Should().Be(1);
        (await NamedTextboxCount("Wrapped")).Should().Be(1);
        (await NamedTextboxCount("First Hidden Second")).Should().Be(1);
        (await NamedTextboxCount("ARIA")).Should().Be(1);
        (await NamedTextboxCount("Referenced")).Should().Be(1);

        await page.CloseAsync();

        async Task<int> NamedTextboxCount(string name)
            => await page.GetByRole(
                AriaRole.Textbox,
                new PageGetByRoleOptions { Name = name, Exact = true }).CountAsync();
    }

    /// <summary>The router, and the emulated colour scheme the page reads through <c>matchMedia</c>.</summary>
    [Test]
    public async Task PlaywrightMovesThroughTheRouterAndEmulatesTheColourScheme()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("spa-router");

        (await page.Locator("#view").TextContentAsync()).Should().Be("the home view");

        await page.Locator(".route[href='/spa-router/about']").ClickAsync();
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

    /// <summary>A click that starts a form navigation does not answer before its redirected document loads.</summary>
    /// <remarks>
    /// <c>Input.dispatchMouseEvent</c> must publish <c>Page.frameRequestedNavigation</c> before it replies, or
    /// Playwright concludes that the action started no navigation and returns while the redirected document is
    /// still in flight. There is deliberately no explicit navigation or locator wait after the click.
    /// </remarks>
    [Test]
    public async Task PlaywrightClickWaitsForAPostRedirectNavigation()
    {
        string? body = null;
        string? method = null;

        await using var lane = await ClientLane.OpenAsync(
            server =>
            {
                server.ResponseDelay = request => request.Path == "/form-redirect/done.html"
                    ? TimeSpan.FromMilliseconds(500)
                    : TimeSpan.Zero;

                FixtureRoutes.FormRedirect(server, (seenMethod, seenBody) =>
                {
                    method = seenMethod;
                    body = seenBody;
                });
            });

        var page = await lane.NewPageAsync("form-redirect");
        await page.Locator("#place").ClickAsync();

        page.Url.Should().StartWith(lane.Server.Url("/form-redirect/done.html") + "?");
        (await page.EvaluateAsync<string>("() => document.querySelector('#method')?.textContent ?? ''"))
            .Should().Be("arrived by GET at /form-redirect/done.html");

        method.Should().Be("POST");
        body.Should().Be("item=a+widget&quantity=3&colour=blue&gift=yes&token=abc123&action=place");

        var redirected = lane.Server.Received.Single(request => request.Path == "/form-redirect/done.html");
        redirected.Method.Should().Be("GET");
        redirected.Body.Should().BeEmpty();

        await page.CloseAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PlaywrightClickCommitsTheParsedPostRedirectBeforeReturning(bool delegated)
    {
        using var release = new ManualResetEventSlim();
        var scriptRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var lane = await ClientLane.OpenAsync(server =>
        {
            server.MapHtml("/admin-post/index.html", """
                <!doctype html><html><body>
                <form method="post" action="/save" enctype="multipart/form-data" class="no-multisubmit">
                  <input name="GroupId" value="settings" type="hidden">
                  <label><input id="allow" name="AllowAnonymous" type="checkbox" value="true">Allow anonymous</label>
                  <button type="submit" class="btn save">Save</button>
                  <input name="__RequestVerificationToken" value="fixture-token" type="hidden">
                  <input name="AllowAnonymous" value="false" type="hidden">
                </form>
                <a id="enable" href="/save" data-url-af="UnsafeUrl">Enable</a>
                <script src="/vendor/jquery-3.7.1/jquery.min.js"></script>
                <script>
                  $("body").on("click", "a[data-url-af]", function () {
                    var form = $('<form method="post">').attr("action", this.href);
                    form.append($("input[name=__RequestVerificationToken]").first().clone());
                    form.css({ position: "absolute", left: "-9999em" });
                    $("body").append(form);
                    form.submit();
                    return false;
                  });
                  $("body").on("submit", "form.no-multisubmit", function (event) {
                    if ($(this).hasClass("submitting")) event.preventDefault();
                    $(this).addClass("submitting");
                  });
                </script>
                </body></html>
                """);
            server.Map("/save", _ => LoopbackResponse.Redirect(302, "/saved"));
            server.MapHtml("/saved", """
                <!doctype html><html><head><script src="/parse-gate.js"></script></head>
                <body><div class="message-success">Settings saved</div></body></html>
                """);
            server.Map("/parse-gate.js", _ =>
            {
                scriptRequested.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(30));
                return LoopbackResponse.Script("");
            });
        });
        var page = await lane.NewPageAsync("admin-post");
        if (!delegated)
        {
            await page.Locator("#allow").CheckAsync();
        }

        var clicking = page.Locator(delegated ? "#enable" : ".save").ClickAsync();
        try
        {
            await scriptRequested.Task.WaitAsync(TimeSpan.FromSeconds(30));
            (await page.EvaluateAsync<string>("() => document.readyState")).Should().Be("loading");
            clicking.IsCompleted.Should().BeFalse(
                "the redirected bytes arrived, but the parser has not committed an observable document");
        }
        finally
        {
            release.Set();
        }

        await clicking;
        page.Url.Should().Be(lane.Server.Url("/saved"));
        (await page.EvaluateAsync<string>("() => document.querySelector('.message-success')?.textContent"))
            .Should().Be("Settings saved");
        await page.WaitForLoadStateAsync(LoadState.Load);
        var post = lane.Server.Received.Single(request => request.Method == "POST");
        post.Body.Should().Contain("fixture-token");
        if (!delegated)
        {
            post.Header("Content-Type").Should().StartWith("multipart/form-data; boundary=");
            post.Body.Should().Contain("name=\"AllowAnonymous\"\r\n\r\ntrue")
                .And.Contain("name=\"AllowAnonymous\"\r\n\r\nfalse");
        }

        lane.Server.Received.Single(request => request.Path == "/saved").Method.Should().Be("GET");
        foreach (var host in lane.Pages.Contexts.SelectMany(context => context.Pages))
        {
            host.Errors.Should().BeEmpty();
        }
    }

    [Test]
    public async Task PlaywrightClickSubmitsAJQueryDelegatedHiddenForm()
    {
        string? method = null;
        string? body = null;
        await using var lane = await ClientLane.OpenAsync(
            server => FixtureRoutes.FormRedirect(server, (seenMethod, seenBody) =>
            {
                method = seenMethod;
                body = seenBody;
            }));
        var page = await lane.NewPageAsync("jquery-unsafe-url");

        (await page.Locator(".filter-option-inner-inner").TextContentAsync()).Should().Be("Choose a category");
        await page.Locator("#enable").ClickAsync();

        page.Url.Should().StartWith(lane.Server.Url("/form-redirect/done.html") + "?");
        (await page.Locator("#method").TextContentAsync()).Should().Be("arrived by GET at /form-redirect/done.html");
        method.Should().Be("POST");
        body.Should().Be("__RequestVerificationToken=test-token&feature=Example");
        lane.Server.Received.Count(request => request.Method == "POST").Should().Be(1);
        foreach (var hostPage in lane.Pages.Contexts.SelectMany(context => context.Pages))
        {
            hostPage.Errors.Should().BeEmpty();
        }

        await page.CloseAsync();
    }

    [Test]
    public async Task PlaywrightSavesANestedAdminFormWithoutRetryingThePost()
    {
        await using var lane = await ClientLane.OpenAsync(server => server.Map(
            "/admin-settings/index.html",
            request => LoopbackResponse.Html(AdminSettingsDocument.Create(saved: request.Method == "POST"))));
        var page = await lane.NewPageAsync("admin-settings");

        // No Force, extended timeout, explicit navigation wait or wait for the success marker.
        await page.Locator(".btn.save").ClickAsync();

        page.Url.Should().Be(lane.Url("admin-settings"));
        (await page.EvaluateAsync<string>("() => document.querySelector('#saved')?.textContent ?? ''"))
            .Should().Be("Settings saved");
        lane.Server.Received.Count(request => request.Method == "POST").Should().Be(1);
        foreach (var context in lane.Pages.Contexts)
        {
            foreach (var hostPage in context.Pages)
            {
                hostPage.Errors.Should().BeEmpty();
            }
        }

        await page.CloseAsync();
    }

    /// <summary>A refused navigation is not announced as one the click must wait for.</summary>
    [TestCase("http://127.0.0.1:1/blocked")]
    [TestCase("ftp://example.test/unsupported")]
    [TestCase("data:text/html;base64,%%%")]
    public async Task PlaywrightClickDoesNotWaitForARefusedNavigation(string target)
    {
        await using var lane = await ClientLane.OpenAsync(server => server.MapHtml(
            "/blocked-link/index.html",
            "<!doctype html><html><body><a id='blocked' href='" + target + "'>blocked</a></body></html>"));

        var page = await lane.NewPageAsync("blocked-link");

        await page.Locator("#blocked").ClickAsync(new LocatorClickOptions { Timeout = 2_000 });

        page.Url.Should().Be(lane.Url("blocked-link"));
        await page.CloseAsync();
    }

    /// <summary>Textual navigations commit and keep the original response, not the HTML text wrapper.</summary>
    [TestCase("application/json", 200)]
    [TestCase("Application/Json; charset=utf-8", 200)]
    [TestCase("text/json", 200)]
    [TestCase("application/vnd.api+json", 200)]
    [TestCase("application/problem+json", 400)]
    [TestCase("text/plain; charset=utf-8", 200)]
    public async Task PlaywrightNavigatesToATextDocumentAndReadsTheOriginalResponse(string contentType, int status)
    {
        const string body = """{"openapi":"3.0.0","info":{"title":"Repro","version":"1.0"}}""";
        await using var lane = await ClientLane.OpenAsync(server => server
            .Map("/swagger.json", _ => new LoopbackResponse
            {
                Status = status,
                Reason = status == 200 ? "OK" : "Bad Request",
                Body = body,
            }.With("Content-Type", contentType).With("X-Document", "original"))
            .MapHtml("/after.html", "<!doctype html><title>After JSON</title>"));
        var context = await lane.Client.NewContextAsync();
        var page = await context.NewPageAsync();
        var failed = new ConcurrentQueue<string>();
        page.RequestFailed += (_, request) => failed.Enqueue(request.Url);
        await page.AddInitScriptAsync(
            """
            window.lifecycle = [];
            window.addEventListener('DOMContentLoaded', () => lifecycle.push('DOMContentLoaded'));
            window.addEventListener('load', () => lifecycle.push('load'));
            """);

        var response = await page.GotoAsync(lane.Server.Url("/swagger.json"));

        response.Should().NotBeNull();
        response!.Status.Should().Be(status);
        response.Ok.Should().Be(status == 200);
        response.Url.Should().Be(lane.Server.Url("/swagger.json"));
        response.Headers["content-type"].Should().Be(contentType);
        response.Headers["x-document"].Should().Be("original");
        response.Headers["content-length"].Should().Be(Encoding.UTF8.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture));
        (await response.TextAsync()).Should().Be(body);
        (await response.BodyAsync()).Should().Equal(Encoding.UTF8.GetBytes(body));
        (await response.JsonAsync())!.Value.GetProperty("openapi").GetString().Should().Be("3.0.0");
        (await page.EvaluateAsync<string>("() => document.body.textContent")).Should().Be(body);
        (await page.EvaluateAsync<string>("() => document.readyState")).Should().Be("complete");
        (await page.EvaluateAsync<string[]>("() => lifecycle")).Should().Equal("DOMContentLoaded", "load");
        failed.Should().BeEmpty();

        await page.GotoAsync(lane.Server.Url("/after.html"));
        (await page.TitleAsync()).Should().Be("After JSON");
        await context.CloseAsync();
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
        await page.Locator("#sign-in").ClickAsync();
        await page.Locator("#welcome").WaitForAsync();

        (await page.Locator("#welcome").TextContentAsync()).Should().Be("welcome, ada");

        var cookies = await page.Context.CookiesAsync();
        cookies.Should().Contain(cookie => cookie.Name == "session" && cookie.Value == "ada-is-in");
        cookies.Should().Contain(cookie => cookie.Name == "theme");

        await page.CloseAsync();
    }

    /// <summary>
    /// The actionability check itself: a rendered element is visible, and a hidden one still is not.
    /// </summary>
    /// <remarks>
    /// <b>The inverse of what this test used to assert.</b> It pinned
    /// <see href="https://github.com/sebastienros/jint/issues/3713">#3713</see> — that
    /// <c>getComputedStyle(el).visibility</c> was the empty string, that Playwright's check is
    /// <c>style.visibility !== "visible"</c>, and that <c>IsVisibleAsync</c> was therefore
    /// <see langword="false"/> for an element with a real 1280×16 box. Each of those three lines now asserts
    /// the answer instead of the defect, and the fourth is what stops the fix from being a constant:
    /// <c>visibility: hidden</c> is still read as hidden, because the resolved value is a fallback the
    /// cascade wins over.
    /// </remarks>
    [Test]
    public async Task PlaywrightSeesARenderedElementAsVisible()
    {
        await using var lane = await ClientLane.OpenAsync();
        var page = await lane.NewPageAsync("todomvc-react");

        await page.Locator(".new-todo").WaitForAsync();

        (await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.new-todo')).visibility"))
            .Should().Be("visible", "nothing declares visibility, so it resolves to CSS's initial value");

        (await page.EvaluateAsync<string>(
            "() => { const r = document.querySelector('.new-todo').getBoundingClientRect(); return r.width + 'x' + r.height; }"))
            .Should().Be("1280x16", "the flat box model gives it a real box, and the check needs both halves");

        (await page.Locator(".new-todo").IsVisibleAsync())
            .Should().BeTrue("Playwright's check ends in style.visibility !== 'visible', and it is 'visible' now");

        // And a declared hidden still wins, so the resolved value is a fallback rather than a constant.
        await page.EvaluateAsync("() => document.querySelector('.new-todo').style.visibility = 'hidden'");

        (await page.Locator(".new-todo").IsVisibleAsync())
            .Should().BeFalse("an inline visibility: hidden is a declaration, and a declaration beats the resolved value");

        await page.CloseAsync();
    }

    /// <summary>
    /// <c>FillAsync</c> on an input whose CSS rule uses a percentage width, which is
    /// <see href="https://github.com/sebastienros/jint/issues/3730">#3730</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The failure was a protocol error read as a detached element.</b> The actionability check calls
    /// <c>getBoundingClientRect</c> in the utility world; the flat box model reads the cascade to decide
    /// which elements are rendered; AngleSharp.Css raised <c>ArgumentException</c> for <c>width: 100%</c>
    /// because the page's browsing context had no <c>IRenderDevice</c>; and a CLR exception escaping
    /// <c>Runtime.callFunctionOn</c> is <c>-32000 "A non null render device with a font size is required to
    /// calculate em or rem units."</c>. Playwright turns anything that is <em>not</em> an
    /// <c>exceptionDetails</c> answer into <c>error:notconnected</c>, which its retry loop prints as
    /// <c>element was detached from the DOM, retrying</c> — fourteen times, then a timeout. Changing the
    /// rule to <c>width: 50px</c> made the same program pass, which is what named the cause.
    /// </para>
    /// <para>
    /// The page is a route rather than a fixture because it is a reduction with no library in it: the
    /// obstacle course is for pages a framework drives.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PlaywrightFillsAnInputWhoseRuleUsesAPercentageWidth()
    {
        await using var lane = await ClientLane.OpenAsync(server => server.MapHtml(
            "/percentage-width/index.html",
            """
            <!doctype html>
            <html>
              <head><style>.form-control { width: 100%; display: block }</style></head>
              <body><input id="SiteName" class="form-control" type="text"></body>
            </html>
            """));

        var page = await lane.Context.NewPageAsync();
        await page.GotoAsync(lane.Url("percentage-width"));

        var siteName = page.Locator("#SiteName");
        (await siteName.CountAsync()).Should().Be(1);

        // Unforced, and with a short timeout: this is the whole actionability path — visible, enabled,
        // editable — and before the fix every attempt of it answered "detached" until the clock ran out.
        await siteName.FillAsync("Testing Blog", new LocatorFillOptions { Timeout = 10_000 });

        (await siteName.InputValueAsync()).Should().Be("Testing Blog");

        // And the box the check reads is a real one, computed against the viewport rather than refused.
        (await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.getElementById('SiteName')).width")).Should().Be("1280px");

        await page.CloseAsync();
    }

    /// <summary>Issue #3844: Playwright's standard DataTransfer path can select in-memory files.</summary>
    [Test]
    public async Task PlaywrightCanSetMultipleInputFiles()
    {
        await using var lane = await ClientLane.OpenAsync(server => server.MapHtml(
            "/file-input/index.html",
            """
            <form>
              <input id="upload" type="file" multiple>
            </form>
            <script>
              window.fileEvents = [];
              for (const type of ['input', 'change']) {
                document.body.addEventListener(type, event => {
                  window.fileEvents.push(type + ':' + event.target.id + ':' + event.bubbles);
                });
              }
            </script>
            """));
        var page = await lane.NewPageAsync("file-input");

        await page.Locator("#upload").SetInputFilesAsync(
        [
            new FilePayload
            {
                Name = "hello.txt",
                MimeType = "text/plain",
                Buffer = Encoding.UTF8.GetBytes("hello from Playwright"),
            },
            new FilePayload
            {
                Name = "data.json",
                MimeType = "application/json",
                Buffer = Encoding.UTF8.GetBytes("{\"answer\":42}"),
            },
        ]);

        var result = await page.EvaluateAsync<string>(
            """
            async () => {
              const files = document.getElementById('upload').files;
              const details = await Promise.all(Array.from(files, async file =>
                [file.name, file.type, await file.text()].join('|')));
              return [
                files instanceof FileList,
                files.length,
                files.item(0) === files[0],
                files.item(2) === null,
                details.join(';'),
                window.fileEvents.join(',')
              ].join('#');
            }
            """);

        result.Should().Be(
            "true#2#true#true#hello.txt|text/plain|hello from Playwright;"
            + "data.json|application/json|{\"answer\":42}#input:upload:true,change:upload:true");

        await page.CloseAsync();
    }

    private sealed class TaskBudgetClock : TimeProvider
    {
        private long _ticks = 1;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
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

        internal static async Task<ClientLane> OpenAsync(
            Action<LoopbackServer>? routes = null,
            BrowserOptions? options = null)
        {
            var server = FixtureOrigin.Serve(new LoopbackServer());
            routes?.Invoke(server);

            var pages = new global::Jint.Browser.Browser(options);
            await pages.NewContextAsync(new PageContextOptions { UrlFilter = server.Owns }).ConfigureAwait(false);

            var protocol = new DevToolsServer();
            await protocol.AddBrowser(pages).ConfigureAwait(false);
            await protocol.StartAsync().ConfigureAwait(false);

            var playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);

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

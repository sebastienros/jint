using System.Collections.Concurrent;
using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// <c>Page.RouteAsync</c> and <c>BrowserContext.RouteAsync</c>: a host answers a request in process — with a
/// response of its own, a failure, or the request sent on — over the same interception the <c>Fetch</c>
/// protocol domain answers through.
/// </summary>
public sealed class RequestRoutingTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    /// <summary>The smallest PNG <c>ImageHeader</c> reads: the signature and an <c>IHDR</c> naming 3×2.</summary>
    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, (byte) 'I', (byte) 'H', (byte) 'D', (byte) 'R',
        0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x02,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    [Test]
    public async Task TheIssueReproFulfilsAScriptNobodyServes()
    {
        await using var browser = new Browser();
        var context = await browser.NewContextAsync(new BrowserContextOptions());
        await context.RouteAsync(
            uri => uri.AbsolutePath.EndsWith("/track.js", StringComparison.Ordinal),
            request => request.FulfillAsync(status: 200, contentType: "application/javascript", body: "window.__stubbed = true"));
        var page = await context.NewPageAsync();

        await page.SetContentAsync("<!doctype html><script src=/track.js></script>", "http://127.0.0.1:9/");

        (await page.EvaluateAsync<string>("String(window.__stubbed)")).Should().Be("true");
        page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task ADocumentNavigationCanBeFulfilled()
    {
        await using var fixture = await LoopbackPage.CreateAsync();
        RoutedRequest? seen = null;
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/stub.html", request =>
        {
            seen = request;
            return request.FulfillAsync(200, [new("x-stub", "1")], "<title>Stubbed</title><p id=p>from the route</p>", "text/html");
        });

        var response = await fixture.Page.NavigateAsync(fixture.Url("/stub.html"));

        response!.Status.Should().Be(200);
        (await fixture.Page.TitleAsync()).Should().Be("Stubbed");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('p').textContent")).Should().Be("from the route");
        seen!.ResourceType.Should().Be(RequestResourceType.Document);
        seen.IsNavigationRequest.Should().BeTrue();
        seen.Method.Should().Be("GET");
        fixture.Server.Received.Should().BeEmpty("a fulfilled request opens no socket");
        fixture.Page.Requests.Should().Contain(request => request.Url == fixture.Url("/stub.html") && request.Status == 200);
    }

    [Test]
    public async Task AStylesheetAndAnImageCanBeFulfilled()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html",
            "<link rel=stylesheet href=/style.css><p id=p>x</p><img id=img src=/pic.png>"));
        var types = new ConcurrentQueue<RequestResourceType>();
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/style.css", request =>
        {
            types.Enqueue(request.ResourceType);
            return request.FulfillAsync(body: "#p { color: rgb(1, 2, 3) }", contentType: "text/css");
        });
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/pic.png", request =>
        {
            types.Enqueue(request.ResourceType);
            return request.FulfillAsync(200, null, Png, "image/png");
        });

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>("getComputedStyle(document.getElementById('p')).color")).Should().Be("rgb(1, 2, 3)");
        (await fixture.Page.WaitForAsync("document.getElementById('img').complete && document.getElementById('img').naturalWidth === 3", Wait))
            .Should().BeTrue();
        (await fixture.Page.EvaluateAsync<double>("document.getElementById('img').naturalHeight")).Should().Be(2);
        types.Should().BeEquivalentTo([RequestResourceType.Stylesheet, RequestResourceType.Image]);
        fixture.Server.Received.Select(request => request.Path).Should().Equal("/index.html");
    }

    [Test]
    public async Task FetchAndXhrCanBeFulfilledWithJson()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        var types = new ConcurrentQueue<RequestResourceType>();
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal), request =>
        {
            types.Enqueue(request.ResourceType);
            return request.FulfillAsync(
                body: "{\"method\":\"" + request.Method + "\",\"body\":\"" + request.PostData + "\"}",
                contentType: "application/json");
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            "(async () => { const r = await fetch('/api/a', { method: 'POST', body: 'hi' }); const j = await r.json(); return r.status + ':' + j.method + ':' + j.body; })()"))
            .Should().Be("200:POST:hi");

        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            "new Promise(resolve => { const x = new XMLHttpRequest(); x.open('GET', '/api/b'); x.onload = () => resolve(x.status + ':' + JSON.parse(x.responseText).method); x.send(); })"))
            .Should().Be("200:GET");

        types.Should().Equal(RequestResourceType.Fetch, RequestResourceType.Xhr);
        fixture.Server.Received.Select(request => request.Path).Should().Equal("/index.html");
    }

    [Test]
    public async Task AbortFailsFetchAScriptAndAnImage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/index.html", "<p>x</p>")
            .Map("/data", _ => LoopbackResponse.Text("served")));
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath is "/data" or "/blocked.js" or "/blocked.png",
            request => request.AbortAsync("connectionrefused"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>(
            "fetch('/data').then(() => 'resolved', e => e.name)")).Should().Be("TypeError");

        await fixture.Page.EvaluateAsync(
            "window.events = [];"
            + "const s = document.createElement('script'); s.src = '/blocked.js';"
            + "s.onerror = () => events.push('script error'); s.onload = () => events.push('script load'); document.head.append(s);"
            + "const i = new Image(); i.onerror = () => events.push('image error'); i.onload = () => events.push('image load'); i.src = '/blocked.png';");

        (await fixture.Page.WaitForAsync("events.length === 2", Wait)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("events.sort().join()")).Should().Be("image error,script error");
        fixture.Server.Received.Select(request => request.Path).Should().Equal("/index.html");
        fixture.Page.Requests.Should().Contain(request => request.Url == fixture.Url("/data") && request.Failed);
    }

    [Test]
    public async Task ContinuePassesARequestThroughAfterRecordingIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/index.html", "<script src=/app.js></script>")
            .Map("/app.js", _ => LoopbackResponse.Script("window.loaded = 'real'"))
            .Map("/echo", request => LoopbackResponse.Text(request.Method + ":" + request.Header("x-routed") + ":" + request.Body)));
        var recorded = new ConcurrentQueue<string>();
        await fixture.Context.RouteAsync(_ => true, async request =>
        {
            recorded.Enqueue(request.Url.AbsolutePath);
            await request.ContinueAsync();
        });

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        (await fixture.Page.EvaluateAsync<string>("window.loaded")).Should().Be("real");

        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/rewrite", request => request.ContinueAsync(new RouteContinueOverrides
        {
            Url = fixture.Url("/echo"),
            Method = "PUT",
            Headers = [.. request.Headers.Select(header => KeyValuePair.Create(header.Name, header.Value)), new("x-routed", "yes")],
            PostData = "rewritten"u8.ToArray(),
        }));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/rewrite', { method: 'POST', body: 'original' }).then(r => r.text())"))
            .Should().Be("PUT:yes:rewritten");

        recorded.Should().Equal("/index.html", "/app.js");
        fixture.Server.Received.Select(request => request.Path).Should().Equal("/index.html", "/app.js", "/echo");
    }

    [Test]
    public async Task AChildFrameDocumentAndItsScriptAreRouted()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<iframe id=f src=/frame.html></iframe>"));
        var types = new ConcurrentQueue<RequestResourceType>();
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/frame.html", request =>
        {
            types.Enqueue(request.ResourceType);
            return request.FulfillAsync(body: "<p id=inner>framed</p><script src=/frame.js></script>", contentType: "text/html");
        });
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/frame.js", request =>
        {
            types.Enqueue(request.ResourceType);
            return request.FulfillAsync(body: "document.body.dataset.ran = 'yes'", contentType: "text/javascript");
        });

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAsync<string>("document.getElementById('f').contentDocument.getElementById('inner').textContent"))
            .Should().Be("framed");
        (await fixture.Page.EvaluateAsync<string>("document.getElementById('f').contentDocument.body.dataset.ran")).Should().Be("yes");
        types.Should().Equal(RequestResourceType.Frame, RequestResourceType.Script);
    }

    [Test]
    public async Task ADedicatedWorkerScriptIsRouted()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        await fixture.Context.RouteAsync(uri => uri.AbsolutePath == "/worker.js",
            request => request.FulfillAsync(body: "postMessage('from a routed worker')", contentType: "text/javascript"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        await fixture.Page.EvaluateAsync("new Worker('/worker.js').onmessage = e => window.message = e.data;");

        (await fixture.Page.WaitForAsync("window.message === 'from a routed worker'", Wait)).Should().BeTrue();
        fixture.Server.Received.Select(request => request.Path).Should().Equal("/index.html");
    }

    [Test]
    public async Task PageRoutesComeFirstNewestFirstAndAnUndecidedHandlerFallsBack()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        var order = new ConcurrentQueue<string>();
        await fixture.Context.RouteAsync(_ => true, request =>
        {
            order.Enqueue("context");
            return request.Url.AbsolutePath == "/data" ? request.FulfillAsync(body: "context") : request.ContinueAsync();
        });
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/data", request =>
        {
            order.Enqueue("older page route");
            return request.FulfillAsync(body: "older");
        });
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/data", request =>
        {
            order.Enqueue("newest page route");
            return Task.CompletedTask;
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        order.Clear();

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("older");

        order.Should().Equal("newest page route", "older page route");
    }

    [Test]
    public async Task UnrouteStopsRoutingAndKeepsTheOtherRoutes()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/index.html", "<p>x</p>")
            .Map("/data", _ => LoopbackResponse.Text("network")));
        Func<Uri, bool> match = uri => uri.AbsolutePath == "/data";
        Func<RoutedRequest, Task> stub = request => request.FulfillAsync(body: "stub");
        Func<RoutedRequest, Task> other = request => request.FulfillAsync(body: "other");
        await fixture.Page.RouteAsync(match, other);
        await fixture.Page.RouteAsync(match, stub);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("stub");

        await fixture.Page.UnrouteAsync(match, stub);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("other");

        await fixture.Page.UnrouteAsync(match);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("network");

        await fixture.Context.RouteAsync(match, stub);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("stub");
        await fixture.Context.UnrouteAsync(match);
        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("network");
    }

    [Test]
    public async Task TheUrlFilterStillRunsFirst()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        var asked = 0;
        await fixture.Context.RouteAsync(_ => true, request =>
        {
            Interlocked.Increment(ref asked);
            return request.FulfillAsync(body: "stub");
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        Volatile.Write(ref asked, 0);

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('http://elsewhere.test/x').then(() => 'resolved', e => e.name)"))
            .Should().Be("TypeError");

        Volatile.Read(ref asked).Should().Be(0, "a URL the context's filter refuses never reaches a route");
    }

    [Test]
    public async Task AContinuedUrlIsReCheckedAgainstTheFilter()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/away",
            request => request.ContinueAsync(new RouteContinueOverrides { Url = "http://elsewhere.test/x" }));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/away').then(() => 'resolved', e => e.name)")).Should().Be("TypeError");
    }

    [Test]
    public async Task AThrowingHandlerFailsTheRequestAndADecisionIsTakenOnce()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        Exception? second = null;
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/twice", async request =>
        {
            await request.FulfillAsync(body: "first");
            second = await Caught.ExceptionAsync(() => request.AbortAsync());
            request.IsHandled.Should().BeTrue();
        });
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/throws",
            _ => throw new InvalidOperationException("the handler failed"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/twice').then(r => r.text())")).Should().Be("first");
        second.Should().BeOfType<InvalidOperationException>();

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/throws').then(() => 'resolved', e => e.name)")).Should().Be("TypeError");
    }

    [Test]
    public async Task AnUnknownAbortCodeIsRefused()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        Exception? refused = null;
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/data", async request =>
        {
            refused = await Caught.ExceptionAsync(() => request.AbortAsync("no-such-code"));
            await request.FulfillAsync(body: "after");
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("after");
        refused.Should().BeOfType<ArgumentException>();
    }

    [Test]
    public async Task ARouteRegisteredOnAContextReachesPagesOpenedLater()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        await fixture.Context.RouteAsync(uri => uri.AbsolutePath == "/data", request => request.FulfillAsync(body: "context"));
        var second = await fixture.NewPageAsync();
        await second.NavigateAsync(fixture.Url("/index.html"));

        (await second.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("context");
    }

    [Test]
    public async Task AHandlerThatBlocksDoesNotBlockThePageLoop()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var onLoop = true;
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/slow", request =>
        {
            onLoop = !Thread.CurrentThread.IsThreadPoolThread;
            entered.Set();
            release.Wait(Wait);
            return request.FulfillAsync(body: "released");
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        await fixture.Page.EvaluateAsync("window.result = 'pending'; fetch('/slow').then(r => r.text()).then(t => window.result = t);");
        entered.Wait(Wait).Should().BeTrue();

        // The handler is still blocked, and the page answers anyway.
        (await fixture.Page.EvaluateAsync<string>("window.result")).Should().Be("pending");
        release.Set();

        (await fixture.Page.WaitForAsync("window.result === 'released'", Wait)).Should().BeTrue();
        onLoop.Should().BeFalse();
    }

    [Test]
    public async Task ADecisionAfterTheHandlersTaskCompletedThrowsAndTheNextRouteDecides()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        var late = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.Context.RouteAsync(uri => uri.AbsolutePath == "/data", request => request.FulfillAsync(body: "context"));
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/data", request =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(50);
                late.SetResult(await Caught.ExceptionAsync(() => request.FulfillAsync(body: "too late")));
            });
            return Task.CompletedTask;
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("context");
        (await late.Task.WaitAsync(Wait)).Should().BeOfType<InvalidOperationException>();
    }

    [Test]
    public async Task OverridesThatCannotBeSentAreRefusedWhereTheyAreMade()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<p>x</p>"));
        var refusals = new ConcurrentQueue<string>();
        await fixture.Page.RouteAsync(uri => uri.AbsolutePath == "/data", async request =>
        {
            foreach (var attempt in new Func<Task>[]
            {
                () => request.ContinueAsync(new RouteContinueOverrides { Method = "GE T" }),
                () => request.ContinueAsync(new RouteContinueOverrides { Headers = [new("bad:name", "x")] }),
                () => request.ContinueAsync(new RouteContinueOverrides { Headers = [new("x-ok", "split\r\nheader")] }),
                () => request.ContinueAsync(new RouteContinueOverrides { Url = "ftp://example.test/" }),
                () => request.FulfillAsync(headers: [new("bad name", "x")]),
                () => request.FulfillAsync(status: 42),
            })
            {
                refusals.Enqueue((await Caught.ExceptionAsync(attempt))?.GetType().Name ?? "accepted");
            }

            ((ICollection<PageHeader>) request.Headers).IsReadOnly.Should().BeTrue();
            request.IsHandled.Should().BeFalse();
            await request.FulfillAsync(body: "valid");
        });
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        (await fixture.Page.EvaluateAndAwaitAsync<string>("fetch('/data').then(r => r.text())")).Should().Be("valid");
        refusals.Should().Equal("ArgumentException", "ArgumentException", "ArgumentException", "ArgumentException",
            "ArgumentException", "ArgumentOutOfRangeException");
    }
}

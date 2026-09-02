using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Workers;

/// <summary>
/// A worker is a thread the package starts, an engine the package builds and a module the page's own network
/// fetched — the engine never starts a thread of its own.
/// </summary>
public sealed class WorkerTests
{
    private const string EchoWorker =
        """
        self.addEventListener('message', function (e) {
          self.postMessage('echo:' + e.data);
        });
        """;

    [Test]
    public async Task AModuleWorkerLoadsOverThePagesNetworkAndEchoesAMessage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Bytes(EchoWorker, "text/javascript"))
            .MapHtml(
                "/index.html",
                """
                <title>workers</title>
                <script>
                  window.replies = [];
                  var worker = new Worker('/worker.js', { type: 'module' });
                  worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                  worker.postMessage('hello');
                  window.worker = worker;
                </script>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        await WaitFor(fixture, "window.replies.length > 0");

        (await fixture.Page.EvaluateAsync<string>("window.replies.join('|')")).Should().Be("echo:hello");
        fixture.Server.Received.Should().Contain(r => r.Path == "/worker.js");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task AWorkerIsCountedWhileItRunsAndTerminateStopsIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Bytes(EchoWorker, "text/javascript"))
            .MapHtml(
                "/index.html",
                """
                <script>
                  window.replies = [];
                  window.worker = new Worker('/worker.js', { type: 'module' });
                  window.worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                  window.worker.postMessage('one');
                </script>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "window.replies.length > 0");

        fixture.Page.Workers.Should().Be(1);

        await fixture.Page.EvaluateAsync("window.worker.terminate()");
        await WaitForWorkers(fixture, 0);

        fixture.Page.Workers.Should().Be(0);

        // A message posted to a terminated worker is dropped rather than answered.
        await fixture.Page.EvaluateAsync("window.worker.postMessage('two')");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(200));

        (await fixture.Page.EvaluateAsync<string>("window.replies.join('|')")).Should().Be("echo:one");
    }

    [Test]
    public async Task AWorkerCanFetchThroughThePagesOwnNetworkPosition()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map(
                "/worker.js",
                _ => LoopbackResponse.Bytes(
                    """
                    self.addEventListener('message', function () {
                      fetch('/api')
                        .then(function (r) { return r.text(); })
                        .then(function (t) { self.postMessage(t); })
                        .catch(function (e) { self.postMessage('failed: ' + e.message); });
                    });
                    """,
                    "text/javascript"))
            .Map("/api", _ => LoopbackResponse.Text("from the worker"))
            .MapHtml(
                "/index.html",
                """
                <script>
                  window.replies = [];
                  var worker = new Worker('/worker.js', { type: 'module' });
                  worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                  worker.postMessage('go');
                </script>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "window.replies.length > 0");

        (await fixture.Page.EvaluateAsync<string>("window.replies[0]")).Should().Be("from the worker");

        // The page's network log is what the page fetched, not what its document fetched: a worker's module
        // and its own requests are in it too.
        fixture.Page.Requests.Should().Contain(r => r.Url.EndsWith("/worker.js", StringComparison.Ordinal) && r.Status == 200);
        fixture.Page.Requests.Should().Contain(r => r.Url.EndsWith("/api", StringComparison.Ordinal) && r.Status == 200);
    }

    [Test]
    public async Task AWorkersModuleImportsResolveAgainstThePagesDocument()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/lib/helper.js", _ => LoopbackResponse.Bytes("export const greeting = 'from the module graph';", "text/javascript"))
            .Map(
                "/lib/worker.js",
                _ => LoopbackResponse.Bytes(
                    """
                    import { greeting } from './helper.js';
                    self.addEventListener('message', function () { self.postMessage(greeting); });
                    """,
                    "text/javascript"))
            .MapHtml(
                "/index.html",
                """
                <script>
                  window.replies = [];
                  var worker = new Worker('/lib/worker.js', { type: 'module' });
                  worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                  worker.postMessage('go');
                </script>
                """));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "window.replies.length > 0");

        (await fixture.Page.EvaluateAsync<string>("window.replies[0]")).Should().Be("from the module graph");
        fixture.Server.Received.Should().Contain(r => r.Path == "/lib/helper.js");
    }

    [Test]
    public async Task AWorkerReachesNothingThePagesFilterRefuses()
    {
        using var other = new LoopbackServer();
        other.Map("/secret.js", _ => LoopbackResponse.Bytes("self.postMessage('reached');", "text/javascript"));

        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml(
            "/index.html",
            "<script>window.failed = null;"
            + "var w = new Worker('" + other.Url("/secret.js") + "', { type: 'module' });"
            + "w.addEventListener('error', function (e) { window.failed = e.message || 'error'; });</script>"));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));

        // The worker was created and its module load failed, which is what proves the filter refused it
        // rather than the worker never having been asked for.
        await WaitFor(fixture, "window.failed !== null");

        other.Received.Should().BeEmpty("the page's URL filter bounds a worker's loads too");
    }

    [Test]
    public async Task NavigatingAwayStopsTheWorkersOfTheDocumentBeingLeft()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Bytes(EchoWorker, "text/javascript"))
            .MapHtml(
                "/index.html",
                """
                <script>
                  window.replies = [];
                  var worker = new Worker('/worker.js', { type: 'module' });
                  worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                  worker.postMessage('hello');
                </script>
                """)
            .MapHtml("/next.html", "<title>next</title>"));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "window.replies.length > 0");
        fixture.Page.Workers.Should().Be(1);

        await fixture.Page.NavigateAsync(fixture.Url("/next.html"));
        await WaitForWorkers(fixture, 0);

        fixture.Page.Workers.Should().Be(0, "disposing the engine a worker belonged to ends its connection");
    }

    [Test]
    public async Task ClosingThePageStopsEveryWorkerThread()
    {
        var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Bytes(EchoWorker, "text/javascript"))
            .MapHtml(
                "/index.html",
                """
                <script>
                  window.replies = [];
                  var worker = new Worker('/worker.js', { type: 'module' });
                  worker.addEventListener('message', function (e) { window.replies.push(e.data); });
                  worker.postMessage('hello');
                </script>
                """));

        try
        {
            await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
            await WaitFor(fixture, "window.replies.length > 0");

            var page = fixture.Page;
            page.Workers.Should().Be(1);

            await page.CloseAsync();

            // The provider refuses further workers and asked every live one to end; the count follows.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (page.Workers != 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            page.Workers.Should().Be(0);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Pumps the page until <paramref name="condition"/> answers true, or gives up — every wait bounded.
    /// </summary>
    /// <remarks>
    /// A worker runs on its own thread and answers through the page's event loop, so what makes a reply
    /// visible is a turn of the page's pump. The page is idle between turns, which is why this polls a
    /// bounded number of short waits rather than waiting for idle once.
    /// </remarks>
    private static async Task WaitFor(LoopbackPage fixture, string condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            if (await fixture.Page.EvaluateAsync<bool>(condition).ConfigureAwait(false))
            {
                return;
            }

            await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }

        Assert.Fail("The page never satisfied '" + condition + "'. Errors: " + string.Join("; ", fixture.Page.Errors));
    }

    private static async Task WaitForWorkers(LoopbackPage fixture, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (fixture.Page.Workers != count && DateTime.UtcNow < deadline)
        {
            await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
        }
    }
}

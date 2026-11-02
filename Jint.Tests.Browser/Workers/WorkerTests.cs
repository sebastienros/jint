using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.Workers;

/// <summary>
/// A worker is a thread the package starts, an engine the package builds and a module the page's own network
/// fetched — the engine never starts a thread of its own.
/// </summary>
public sealed class WorkerTests
{
    [TestCase("classic", false, false)]
    [TestCase("classic", true, false)]
    [TestCase("classic", false, true)]
    [TestCase("classic", true, true)]
    [TestCase("module", false, false)]
    [TestCase("module", true, false)]
    [TestCase("module", false, true)]
    [TestCase("module", true, true)]
    public async Task EndingAWorkerDuringExecutionDoesNotReportAScriptFailure(string type, bool navigate, bool task)
    {
        var script = task
            ? "onmessage=()=>{postMessage('running');while(true){}};"
            : "postMessage('running');while(true){}";
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Script(script))
            .MapHtml("/", "<!doctype html><title>worker cancellation</title>")
            .MapHtml("/next", "<!doctype html><title>next document</title>"),
            configureBrowser: options => options.MaxTaskDuration = Timeout.InfiniteTimeSpan);
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.EvaluateAsync("window.running=false;var worker=new Worker('/worker.js',{type:'"
            + type + "'});worker.onmessage=()=>running=true;worker.postMessage('go');");
        await WaitFor(fixture, "running");
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await fixture.Page.RunOnLoopAsync(engine =>
        {
            var worker = (Jint.WebApi.Workers.JsWorker) engine.GetValue("worker");
            worker.Link!.Worker.Disposed += (_, _) => disposed.TrySetResult();
            return true;
        });
        if (navigate) await fixture.Page.NavigateAsync(fixture.Url("/next"));
        else await fixture.Page.EvaluateAsync("worker.terminate()");
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("classic")]
    [TestCase("module")]
    public async Task BundledWorkerBootstrapUsesTheFinalLocationAndBrowserLocale(string type)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Redirect(302, "/assets/worker.js?bundle=1"))
            .Map("/assets/worker.js", _ => LoopbackResponse.Script("""
                self._VSCODE_FILE_ROOT = self.location.href;
                const language = navigator.language.toLowerCase();
                postMessage({
                    root:self._VSCODE_FILE_ROOT, language, href:location.href,
                    origin:location.origin, protocol:location.protocol, host:location.host,
                    hostname:location.hostname, port:location.port, pathname:location.pathname,
                    search:location.search, hash:location.hash, text:String(location),
                    brand:Object.prototype.toString.call(location),
                    instance:location instanceof WorkerLocation,
                    readonly:!Reflect.set(location,'href','https://invalid.example/'),
                    illegal:(() => {
                        try { Object.getOwnPropertyDescriptor(WorkerLocation.prototype,'href').get.call({}); }
                        catch(e) { return e.name; }
                    })()
                });
                """))
            .MapHtml("/", "<!doctype html><title>worker bootstrap</title>"));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.EvaluateAsync("window.bootstrap=null;var worker=new Worker('/worker.js',{type:'"
            + type + "'});worker.onmessage=e=>bootstrap=e.data");
        await WaitFor(fixture, "bootstrap !== null");
        (await fixture.Page.EvaluateAsync<string>("bootstrap.root")).Should().Be(fixture.Url("/assets/worker.js?bundle=1"));
        (await fixture.Page.EvaluateAsync<bool>("""
            bootstrap.language === navigator.language.toLowerCase() &&
            bootstrap.href === bootstrap.root && bootstrap.text === bootstrap.root &&
            bootstrap.origin === location.origin && bootstrap.protocol === location.protocol &&
            bootstrap.host === location.host && bootstrap.hostname === location.hostname &&
            bootstrap.port === location.port && bootstrap.pathname === '/assets/worker.js' &&
            bootstrap.search === '?bundle=1' && bootstrap.hash === '' &&
            bootstrap.brand === '[object WorkerLocation]' && bootstrap.instance &&
            bootstrap.readonly && bootstrap.illegal === 'TypeError'
            """)).Should().BeTrue();
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("classic", "", null, "en-US")]
    [TestCase("module", "fr-CA", null, "fr-CA")]
    [TestCase("classic", "fr-CA", "de-DE,de;q=0.8,en;q=0.5", "de-DE")]
    [TestCase("module", "fr-CA", "de-DE,de;q=0.8,en;q=0.5", "de-DE")]
    public async Task WorkersInheritBrowserLanguages(string type, string culture, string? acceptLanguage, string expected)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Script("""
                const language = navigator.language.toLowerCase();
                postMessage({
                    language:navigator.language, lower:language, languages:Array.from(navigator.languages),
                    frozen:Object.isFrozen(navigator.languages),
                    prototype:!Object.hasOwn(navigator,'language') && !Object.hasOwn(navigator,'languages'),
                    brand:(() => {
                        try { Object.getOwnPropertyDescriptor(Object.getPrototypeOf(navigator),'language').get.call({}); }
                        catch (e) { return e.name; }
                    })()
                });
                """))
            .MapHtml("/", "<!doctype html><title>worker locale</title>"),
            configureBrowser: options => options.ConfigureEngine(o => o.Culture = System.Globalization.CultureInfo.GetCultureInfo(culture)));
        await fixture.Page.NavigateAsync(fixture.Url("/"));
        await fixture.Page.RunOnLoopAsync(engine =>
        {
            global::Jint.Browser.Runtime.PageRuntime.Find(engine)!.Emulation.AcceptLanguage = acceptLanguage;
            return true;
        });
        await fixture.Page.EvaluateAsync("window.locale=null;var worker=new Worker('/worker.js',{type:'"
            + type + "'});worker.onmessage=e=>locale=e.data");
        await WaitFor(fixture, "locale !== null");
        (await fixture.Page.EvaluateAsync<string>("locale.language")).Should().Be(expected);
        (await fixture.Page.EvaluateAsync<bool>("""
            locale.lower === navigator.language.toLowerCase() &&
            locale.languages.join('|') === navigator.languages.join('|') &&
            locale.frozen && locale.prototype && locale.brand === 'TypeError'
            """)).Should().BeTrue();
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase(", { type: 'classic', name: 'bundle' }")]
    public async Task ClassicBundlesUseGlobalScriptSemanticsAndBufferEarlyMessages(string options)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/bundle.js", _ => LoopbackResponse.Bytes("""
                var bundleGlobal = 41;
                let lexical = 1;
                function topThis() { return this === self; }
                onmessage = event => postMessage([
                    event.data, self.bundleGlobal, typeof self.lexical,
                    lexical, topThis(), this === self, name
                ].join('|'));
                """, "text/javascript"))
            .MapHtml("/index.html", "<script>window.replies=[]; window.worker=new Worker('/bundle.js'"
                + options + "); worker.onmessage=e=>replies.push(e.data); worker.postMessage('ready');</script>"));

        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "replies.length === 1");

        (await fixture.Page.EvaluateAsync<string>("replies[0]"))
            .Should().Be("ready|41|undefined|1|true|true|" + (options.Length == 0 ? "" : "bundle"));
        fixture.Page.Errors.Should().BeEmpty();
        await fixture.Page.EvaluateAsync("worker.terminate()");
        await WaitForWorkers(fixture, 0);
    }

    [Test]
    public async Task ClassicImportsAndFetchUseTheRedirectedWorkerUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/bundle.js", _ => LoopbackResponse.Redirect(302, "/assets/bundle.js"))
            .Map("/assets/bundle.js", _ => LoopbackResponse.Bytes("""
                importScripts('helper.js');
                onmessage = () => fetch('answer').then(r=>r.text()).then(t=>postMessage(prefix+t));
                """, "text/javascript"))
            .Map("/assets/helper.js", _ => LoopbackResponse.Bytes("var prefix='bundle:';", "text/javascript"))
            .Map("/assets/answer", _ => LoopbackResponse.Text("ok"))
            .MapHtml("/index.html", """
                <script>
                window.reply=null;
                var worker=new Worker('/bundle.js');
                worker.onmessage=e=>reply=e.data;
                worker.postMessage('go');
                </script>
                """));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "reply !== null");
        (await fixture.Page.EvaluateAsync<string>("reply")).Should().Be("bundle:ok");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("/missing.js")]
    [TestCase("/syntax.js")]
    [TestCase("/module.js")]
    [TestCase("/html.js")]
    public async Task ClassicStartupFailuresFireAnErrorAndReleaseTheWorker(string source)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/syntax.js", _ => LoopbackResponse.Bytes("function {", "text/javascript"))
            .Map("/module.js", _ => LoopbackResponse.Bytes("export const value=1;", "text/javascript"))
            .MapHtml("/html.js", "<html>not JavaScript</html>")
            .MapHtml("/index.html", "<script>window.failed=false; var worker=new Worker('"
                + source + "'); worker.onerror=event=>{failed=event.constructor.name;event.preventDefault();};</script>"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "failed !== false");
        (await fixture.Page.EvaluateAsync<string>("failed")).Should().Be("Event");
        await WaitForWorkers(fixture, 0);
        fixture.Page.Workers.Should().Be(0);
    }

    [Test]
    public async Task ClassicEntryScriptsCannotRedirectToAnotherOrigin()
    {
        using var other = new LoopbackServer();
        other.Map("/worker.js", _ => LoopbackResponse.Bytes("postMessage('wrong-origin');", "text/javascript"));
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Redirect(302, other.Url("/worker.js")))
            .MapHtml("/index.html", """
                <script>window.failed=false;
                var worker=new Worker('/worker.js');
                worker.onerror=event=>{failed=true;event.preventDefault();};
                </script>
                """),
            configureContext: context => context.UrlFilter = uri => uri.IsLoopback);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "failed");
        other.Received.Should().BeEmpty();
        await WaitForWorkers(fixture, 0);
    }

    [Test]
    public async Task ClassicEntryScriptsRequireTheConstructorDocumentsOrigin()
    {
        using var other = new LoopbackServer();
        await using var fixture = await LoopbackPage.CreateAsync(server => server.MapHtml("/index.html", "<title>origin</title>"),
            configureContext: context => context.UrlFilter = uri => uri.IsLoopback);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        (await fixture.Page.EvaluateAsync<string>("try { new Worker('" + other.Url("/worker.js")
            + "'); 'unexpected'; } catch (error) { error.name; }")).Should().Be("SecurityError");
        other.Received.Should().BeEmpty();
        fixture.Page.Workers.Should().Be(0);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ClassicRuntimeErrorsDoNotDisableTheMessageQueue(bool handleInWorker)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Bytes((handleInWorker ? "onerror=()=>true;" : "") + """
                onmessage=e=>postMessage(e.data);
                throw new Error('The script execution was canceled.');
                """, "text/javascript"))
            .MapHtml("/index.html", """
                <script>window.reply=null; window.workerErrors=[];
                var worker=new Worker('/worker.js');
                worker.onerror=e=>{workerErrors.push(e.constructor.name);e.preventDefault();};
                worker.onmessage=e=>reply=e.data; worker.postMessage('alive');</script>
                """));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await WaitFor(fixture, "reply !== null");
        (await fixture.Page.EvaluateAsync<string>("reply")).Should().Be("alive");
        if (!handleInWorker) await WaitFor(fixture, "workerErrors.length === 1");
        (await fixture.Page.EvaluateAsync<string>("workerErrors.join('|')"))
            .Should().Be(handleInWorker ? "" : "ErrorEvent");
    }

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

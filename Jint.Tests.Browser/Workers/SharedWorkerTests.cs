using System.Net;
using System.Net.Http;
using Jint.Browser;
using Jint.Browser.Workers;
using Jint.Browser.Runtime;
using Jint.WebApi.Fetch;
using Jint.Tests.Browser.Navigation;
using Jint.WebApi;
using Jint.WebApi.Messaging;

namespace Jint.Tests.Browser.Workers;

public sealed class SharedWorkerTests
{
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);
    private const string Counter =
        """
        let count = 0;
        onconnect = e => {
          const port = e.ports[0];
          port.postMessage(++count);
          port.onmessage = m => port.postMessage(m.data);
        };
        """;

    private static Task<LoopbackPage> Fixture(string script = Counter, Action<BrowserOptions>? configure = null)
        => LoopbackPage.CreateAsync(server => server
            .Map("/worker.js", _ => LoopbackResponse.Bytes(script, "text/javascript"))
            .MapHtml("/index.html", "<title>shared workers</title>")
            .MapHtml("/next.html", "<title>next</title>"),
            configureBrowser: configure);

    private static async Task Connect(Page page, string options = "'counter'")
    {
        await page.EvaluateAsync(
            "globalThis.messages = []; globalThis.worker = new SharedWorker('/worker.js', " + options + ");"
            + "worker.port.onmessage = e => messages.push(e.data);");
        await page.WaitForAsync("messages.length === 1", _wait);
    }

    [TestCase("classic")]
    [TestCase("module")]
    public async Task ConnectAndStructuredCloneRoundTrip(string type)
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page, "{name:'counter', type:'" + type + "'}");
        await fixture.Page.EvaluateAsync(
            "const buffer = new Uint8Array([1,2,3]).buffer;"
            + "worker.port.postMessage({answer:42, buffer}, [buffer]);"
            + "globalThis.detached = buffer.byteLength === 0;");
        await fixture.Page.WaitForAsync("messages.length === 2", _wait);
        (await fixture.Page.EvaluateAsync<bool>(
            "detached && messages[1].answer === 42 && new Uint8Array(messages[1].buffer)[2] === 3")).Should().BeTrue();
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task TwoConnectionsOnOnePageShareGlobalState()
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await fixture.Page.EvaluateAsync(
            "globalThis.second = new SharedWorker('/worker.js', 'counter');"
            + "second.port.onmessage = e => messages.push(e.data);");
        await fixture.Page.WaitForAsync("messages.length === 2", _wait);
        (await fixture.Page.EvaluateAsync<string>("messages.join(',')")).Should().Be("1,2");
        fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
        fixture.Server.Received.Count(r => r.Path == "/worker.js").Should().Be(1);
    }

    [Test]
    public async Task TwoPagesShareAndTheFirstOwnerCanClose()
    {
        await using var fixture = await Fixture();
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await second.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await Connect(second);
        (await second.EvaluateAsync<int>("messages[0]")).Should().Be(2);
        await fixture.Page.CloseAsync();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
        await second.EvaluateAsync("worker.port.postMessage('still running')");
        await second.WaitForAsync("messages.length === 2", _wait);
        (await second.EvaluateAsync<string>("messages[1]")).Should().Be("still running");
        await second.CloseAsync();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    [TestCase("classic", "", true)]
    [TestCase("module", "", true)]
    [TestCase("classic", "no-referrer", false)]
    [TestCase("module", "unknown, origin, no-referrer", false)]
    public async Task ReusedWorkersOwnTheirNetworkLogAndReferrer(string type, string policy, bool sendsReferrer)
    {
        PageNetworkRecorder? workerLog = null;
        var workerClients = 0;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/first.html", "")
            .MapHtml("/later.html", "")
            .Map("/worker.js", _ => LoopbackResponse.Bytes(
                (type == "classic" ? "importScripts('/helper.js');" : "import '/helper.js';")
                + "let count=0; onconnect=e=>{const p=e.ports[0]; p.postMessage(++count);"
                + "p.onmessage=m=>fetch(m.data === 'denied' ? '/denied' : '/answer')"
                + ".then(r=>r.text()).then(t=>p.postMessage(t)).catch(()=>p.postMessage('blocked'));};",
                "text/javascript").With("Referrer-Policy", policy))
            .Map("/helper.js", _ => LoopbackResponse.Bytes("const imported = true;", "text/javascript"))
            .Map("/answer", _ => LoopbackResponse.Text("fetched")),
            configureContext: options =>
            {
                var allowed = options.UrlFilter!;
                options.UrlFilter = uri => allowed(uri) && uri.AbsolutePath != "/denied";
                options.HttpClientFactory = engine =>
                {
#pragma warning disable JINT0002 // Inspect the worker's engine-free network observer.
                    if (engine.Options.WebApi.Fetch.Observer is PageNetworkRecorder recorder
                        && engine.Options.Modules.ModuleLoader is PageModuleLoader)
                    {
                        workerLog = recorder;
                        Interlocked.Increment(ref workerClients);
                    }
#pragma warning restore JINT0002
                    return client;
                };
            },
            configureBrowser: options => options.MaxRecordedEvents = 3);
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/first.html"));
        await second.NavigateAsync(fixture.Url("/later.html"));
        await fixture.Page.EvaluateAsync("document.cookie='shared=present; Path=/'");
        var settings = "{name:'counter',type:'" + type + "'}";
        await Connect(fixture.Page, settings);
        await Connect(second, settings);
        workerClients.Should().Be(1, "the client factory must receive the independent worker engine");
        workerLog.Should().NotBeNull();
        workerLog!.Requests.Should().ContainSingle(r => r.Url == fixture.Url("/worker.js"));
        fixture.Server.Received.Single(r => r.Path == "/worker.js").Header("Referer")
            .Should().Be(fixture.Url("/first.html"), "startup uses the creator's outside settings");
        fixture.Page.Requests.Should().NotContain(r => r.Url == fixture.Url("/worker.js"));
        await fixture.Page.CloseAsync();
        var firstRequests = fixture.Page.Requests.Count;
        var third = await fixture.NewPageAsync();
        await third.NavigateAsync(fixture.Url("/later.html"));
        await Connect(third, settings);
        (await third.EvaluateAsync<int>("messages[0]")).Should().Be(3);
        for (var i = 0; i < 4; i++)
        {
            await third.EvaluateAsync("worker.port.postMessage('fetch')");
            await third.WaitForAsync("messages.length === " + (i + 2), _wait);
        }
        fixture.Server.Received.Count(r => r.Path == "/worker.js").Should().Be(1);
        foreach (var request in fixture.Server.Received.Where(r => r.Path is "/answer" or "/helper.js"))
        {
            request.Header("Referer").Should().Be(sendsReferrer ? fixture.Url("/worker.js") : null);
            request.Header("Cookie").Should().Contain("shared=present");
            request.Header("User-Agent").Should().NotBeNullOrEmpty();
        }
        await third.EvaluateAsync("worker.port.postMessage('denied')");
        await third.WaitForAsync("messages.length === 6", _wait);
        (await third.EvaluateAsync<string>("messages[5]")).Should().Be("blocked");
        fixture.Server.Received.Should().NotContain(r => r.Path == "/denied");
        workerLog.Requests.Should().HaveCount(3, "the independent log remains ring bounded");
        fixture.Page.Requests.Should().HaveCount(firstRequests);
        second.Requests.Should().NotContain(r => r.Url == fixture.Url("/answer"));
        third.Requests.Should().NotContain(r => r.Url == fixture.Url("/answer"));
        third.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task BudgetDiagnosticsFollowTheRemainingOwners()
    {
        await using var fixture = await Fixture(
            "onconnect=e=>{e.ports[0].postMessage('ready'); e.ports[0].onmessage=()=>{while(true){}};};",
            options => options.MaxTaskDuration = TimeSpan.FromMilliseconds(100));
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await second.NavigateAsync(fixture.Url("/next.html"));
        await Connect(fixture.Page);
        await Connect(second);
        await fixture.Page.CloseAsync();
        var firstErrors = fixture.Page.Errors.Count;
        await second.EvaluateAsync("worker.port.postMessage('spin')");
        var deadline = DateTime.UtcNow + _wait;
        while (!second.Errors.Any(e => e.Kind == PageErrorKind.WorkerError) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        second.Errors.Should().Contain(e => e.Kind == PageErrorKind.WorkerError
            && e.Message.Contains("time budget elapsed", StringComparison.Ordinal));
        fixture.Page.Errors.Should().HaveCount(firstErrors);
        await second.CloseAsync();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    [Test]
    public async Task ConcurrentPagesConnectToOneStartup()
    {
        await using var fixture = await Fixture();
        var pages = new[] { fixture.Page, await fixture.NewPageAsync(), await fixture.NewPageAsync() };
        await Task.WhenAll(pages.Select(p => p.NavigateAsync(fixture.Url("/index.html"))));
        await Task.WhenAll(pages.Select(p => Connect(p)));
        var replies = await Task.WhenAll(pages.Select(p => p.EvaluateAsync<int>("messages[0]")));
        replies.Order().Should().Equal(1, 2, 3);
        fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
        fixture.Server.Received.Count(r => r.Path == "/worker.js").Should().Be(1);
    }

    [Test]
    public async Task ContextsAndNamesAreIsolated()
    {
        await using var fixture = await Fixture();
        var isolated = await fixture.NewIsolatedPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await isolated.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await Connect(isolated);
        (await isolated.EvaluateAsync<int>("messages[0]")).Should().Be(1);
        await Connect(fixture.Page, "'other'");
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(1);
        fixture.Context.SharedWorkers.LiveCount.Should().Be(2);
    }

    [Test]
    public async Task NavigationReleasesOnlyTheOutgoingOwner()
    {
        await using var fixture = await Fixture();
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await second.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await Connect(second);
        await fixture.Page.NavigateAsync(fixture.Url("/next.html"));
        await second.EvaluateAsync("worker.port.postMessage('alive')");
        await second.WaitForAsync("messages.length === 2", _wait);
        (await second.EvaluateAsync<string>("messages[1]")).Should().Be("alive");
        await second.NavigateAsync(fixture.Url("/next.html"));
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(1);
    }

    [TestCase("classic")]
    [TestCase("module")]
    public async Task InterfacesAndConnectEventHaveTheSpecifiedShape(string type)
    {
        await using var fixture = await Fixture(
            """
            onconnect = e => e.ports[0].postMessage([
              self instanceof SharedWorkerGlobalScope, self instanceof WorkerGlobalScope,
              Object.getPrototypeOf(SharedWorkerGlobalScope) === WorkerGlobalScope,
              Object.prototype.toString.call(self), name, typeof postMessage,
              typeof DedicatedWorkerGlobalScope, typeof importScripts, typeof fetch,
              e instanceof MessageEvent, e.data, e.source === e.ports[0],
              Object.isFrozen(e.ports), e.ports.length, e.isTrusted, e.target === self
            ]);
            """);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page, "{name:'visible', type:'" + type + "'}");
        (await fixture.Page.EvaluateAsync<string>("JSON.stringify(messages[0])")).Should().Be(
            """[true,true,true,"[object SharedWorkerGlobalScope]","visible","undefined","undefined","function","function",true,"",true,true,1,true,true]""");
        (await fixture.Page.EvaluateAsync<bool>(
            """
            worker instanceof SharedWorker && worker instanceof EventTarget &&
            worker.port instanceof MessagePort && worker.port === worker.port &&
            worker.onerror === null && typeof SharedWorkerGlobalScope === 'undefined' &&
            Object.getPrototypeOf(SharedWorker) === EventTarget
            """)).Should().BeTrue();
        fixture.Page.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task CloseFinishesItsTurnAndDeliversAlreadyPostedMessages()
    {
        await using var fixture = await Fixture(
            """
            onconnect = e => {
              const p = e.ports[0];
              p.postMessage('before');
              close();
              p.postMessage('after');
              setTimeout(() => p.postMessage('timer'), 0);
            };
            """);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(100));
        (await fixture.Page.EvaluateAsync<string>("messages.join(',')")).Should().Be("before");
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<string>("messages.join(',')")).Should().Be("before");
    }

    [TestCase("classic", "/missing.js")]
    [TestCase("module", "/missing.js")]
    [TestCase("classic", "data:text/javascript,onconnect=()=>{}")]
    [TestCase("module", "data:text/javascript,onconnect=()=>{}")]
    public async Task LoadFailureFiresOnlyAPlainErrorAtTheWorkerObject(string type, string url)
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync(
            "globalThis.failures = []; globalThis.pageErrors = 0; onerror = () => pageErrors++;"
            + "const w = new SharedWorker('" + url + "', {type:'" + type + "'});"
            + "w.onerror = e => failures.push(e instanceof Event && !(e instanceof ErrorEvent) && e.target === w);");
        await fixture.Page.WaitForAsync("failures.length === 1", _wait);
        (await fixture.Page.EvaluateAsync<bool>("failures[0] && pageErrors === 0")).Should().BeTrue();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("{name:'counter',type:'module'}")]
    [TestCase("{name:'counter',credentials:'omit'}")]
    [TestCase("{name:'counter',extendedLifetime:true}")]
    public async Task OptionsMismatchFiresErrorWithoutConnecting(string options)
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await fixture.Page.EvaluateAsync(
            "globalThis.failed = false; const w = new SharedWorker('/worker.js'," + options + ");"
            + "w.onerror = e => { failed = e instanceof Event && !(e instanceof ErrorEvent); };");
        await fixture.Page.WaitForAsync("failed", _wait);
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(2);
        fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
    }

    [TestCase("classic")]
    [TestCase("module")]
    public async Task RuntimeErrorsStayInTheWorkerGlobal(string type)
    {
        await using var fixture = await Fixture(
            """
            let errors = [];
            onerror = (message, file, line, column, error) => { errors.push(error.message); return true; };
            onconnect = e => {
              e.ports[0].onmessage = m => {
                if (m.data === 'throw') throw new Error('callback');
                e.ports[0].postMessage(errors.join(','));
              };
            };
            throw new Error('initial');
            """);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync(
            "globalThis.messages = []; globalThis.ownerErrors = 0;"
            + "onerror = () => ownerErrors++;"
            + "const w = new SharedWorker('/worker.js',{type:'" + type + "'});"
            + "w.onerror = () => ownerErrors++; w.port.onmessage = e => messages.push(e.data);"
            + "w.port.postMessage('throw'); w.port.postMessage('report');");
        await fixture.Page.WaitForAsync("messages.length > 0", _wait);
        (await fixture.Page.EvaluateAsync<string>("messages[0]")).Should().Be("initial,callback");
        (await fixture.Page.EvaluateAsync<int>("ownerErrors")).Should().Be(0);
    }

    [Test]
    public async Task ClassicImportScriptsAndFetchResolveAgainstTheWorkerUrl()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/index.html", "<base href='/lib/'>")
            .Map("/lib/worker.js", _ => LoopbackResponse.Bytes(
                "importScripts('helper.js'); onconnect=e=>fetch('answer').then(r=>r.text()).then(t=>e.ports[0].postMessage(prefix+t));",
                "text/javascript"))
            .Map("/lib/helper.js", _ => LoopbackResponse.Bytes("const prefix = 'imported:';", "text/javascript"))
            .Map("/lib/answer", _ => LoopbackResponse.Text("fetched")));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync(
            "globalThis.reply = ''; const w = new SharedWorker('worker.js'); w.port.onmessage = e => reply = e.data;");
        await fixture.Page.WaitForAsync("reply !== ''", _wait);
        (await fixture.Page.EvaluateAsync<string>("reply")).Should().Be("imported:fetched");
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("classic")]
    [TestCase("module")]
    public async Task SyntaxErrorsFailStartup(string type)
    {
        await using var fixture = await Fixture("const = ;");
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync(
            "globalThis.failed = false; const w = new SharedWorker('/worker.js',{type:'" + type + "'});"
            + "w.onerror = e => failed = e.target === w;");
        await fixture.Page.WaitForAsync("failed", _wait);
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
        fixture.Page.Errors.Should().BeEmpty();
    }

    [TestCase("omit", false)]
    [TestCase("same-origin", true)]
    [TestCase("include", true)]
    public async Task ModuleImportsUseTheRequestedCredentials(string credentials, bool sendsCookie)
    {
        await using var fixture = await LoopbackPage.CreateAsync(server => server
            .MapHtml("/index.html", "")
            .Map("/worker.js", _ => LoopbackResponse.Bytes(
                "import {answer} from './helper.js'; onconnect=e=>{"
                + "try { importScripts('helper.js'); } catch(error) { e.ports[0].postMessage(answer+error.name); }};",
                "text/javascript"))
            .Map("/helper.js", _ => LoopbackResponse.Bytes("export const answer = 'imported:';", "text/javascript")));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("document.cookie = 'worker=present; Path=/';");
        await Connect(fixture.Page, "{type:'module',credentials:'" + credentials + "'}");
        (await fixture.Page.EvaluateAsync<string>("messages[0]")).Should().Be("imported:TypeError");
        var requests = fixture.Server.Received.Where(r => r.Path.EndsWith(".js", StringComparison.Ordinal)).ToArray();
        requests.Should().HaveCount(2);
        foreach (var request in requests)
        {
            request.Headers.ContainsKey("Cookie").Should().Be(sendsCookie);
        }
    }

    [Test]
    public async Task ListenerPortsWaitForStartAndTheirQueuesAreBounded()
    {
        await using var fixture = await Fixture(
            """
            let first = true;
            let refusal = '';
            onconnect = e => {
              const p = e.ports[0];
              if (first) {
                first = false;
                p.postMessage('buffered');
                try { p.postMessage('overflow'); } catch (error) { refusal = error.name; }
              } else {
                p.postMessage(refusal);
              }
            };
            """, options => options.ConfigureEngine(o => o.WebApi.Workers.MaxQueuedMessages = 1));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync(
            "globalThis.messages = []; globalThis.w = new SharedWorker('/worker.js');"
            + "w.port.addEventListener('message', e => messages.push(e.data));"
            + "globalThis.refusal = ''; const second = new SharedWorker('/worker.js');"
            + "second.port.onmessage = e => refusal = e.data;");
        await fixture.Page.WaitForAsync("refusal !== ''", _wait);
        (await fixture.Page.EvaluateAsync<string>("refusal")).Should().Be("QuotaExceededError");
        (await fixture.Page.EvaluateAsync<int>("messages.length")).Should().Be(0);
        await fixture.Page.EvaluateAsync("w.port.start()");
        await fixture.Page.WaitForAsync("messages.length === 1", _wait);
        (await fixture.Page.EvaluateAsync<string>("messages[0]")).Should().Be("buffered");
    }

    [Test]
    public async Task AConnectionCanTransferAnotherMessagePort()
    {
        await using var fixture = await Fixture(
            "onconnect=e=>e.ports[0].onmessage=m=>m.data.port.postMessage('through transferred port');");
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync(
            "globalThis.reply = ''; const channel = new MessageChannel();"
            + "channel.port1.onmessage = e => reply = e.data;"
            + "const w = new SharedWorker('/worker.js'); w.port.postMessage({port:channel.port2},[channel.port2]);");
        await fixture.Page.WaitForAsync("reply !== ''", _wait);
        (await fixture.Page.EvaluateAsync<string>("reply")).Should().Be("through transferred port");
    }

    [Test]
    public async Task ClosingAPortDoesNotReleaseItsDocument()
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await fixture.Page.EvaluateAsync("worker.port.close()");
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(2);
        await fixture.Page.CloseAsync();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    [Test]
    public async Task AConstructorRetainedByASnapshotReRegistersDocumentOwnership()
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        var snapshot = await fixture.Page.RunOnLoopAsync(engine =>
        {
            engine.Evaluate("SharedWorker");
            return engine.Advanced.CaptureGlobalSnapshot();
        });
        for (var cycle = 0; cycle < 2; cycle++)
        {
            await Connect(fixture.Page);
            fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
            await fixture.Page.RunOnLoopAsync(engine =>
            {
                engine.Advanced.RestoreGlobalSnapshot(snapshot);
                return true;
            });
            fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
        }
    }

    [Test]
    public async Task HostCancellationStillEndsARunningSharedWorker()
    {
        using var cancellation = new CancellationTokenSource();
        await using var fixture = await Fixture(
            "onconnect=e=>{e.ports[0].postMessage('ready'); e.ports[0].onmessage=()=>{while(true){}};};",
            options => options.ConfigureEngine(o => o.ObserveCancellation(cancellation.Token)));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await fixture.Page.EvaluateAsync("worker.port.postMessage('spin')");
        cancellation.Cancel();
        var deadline = DateTime.UtcNow + _wait;
        while (fixture.Context.SharedWorkers.LiveCount != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    [Test]
    public async Task ClosingTheLastOwnerCancelsAPendingScriptLoad()
    {
        using var handler = new PendingWorkerLoad();
        using var client = new HttpClient(handler);
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", ""),
            configureContext: options => options.HttpClient = client);
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("globalThis.w = new SharedWorker('/worker.js')");
        await handler.Started.Task.WaitAsync(_wait);
        await fixture.Page.CloseAsync();
        await handler.Cancelled.Task.WaitAsync(_wait);
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    [Test]
    public async Task ANewOwnerKeepsTheFirstOwnersPendingLoadAlive()
    {
        using var handler = new PendingWorkerLoad();
        using var client = new HttpClient(handler);
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/index.html", ""),
            configureContext: options => options.HttpClient = client);
        var second = await fixture.NewPageAsync();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await second.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("globalThis.w = new SharedWorker('/worker.js')");
        await handler.Started.Task.WaitAsync(_wait);
        await second.EvaluateAsync(
            "globalThis.messages = []; const w = new SharedWorker('/worker.js');"
            + "w.port.onmessage = e => messages.push(e.data);");
        await fixture.Page.CloseAsync();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Counter, System.Text.Encoding.UTF8, "text/javascript"),
        });
        await second.WaitForAsync("messages.length === 1", _wait);
        (await second.EvaluateAsync<int>("messages[0]")).Should().Be(1);
        handler.Cancelled.Task.IsCompleted.Should().BeFalse();
    }

    [Test]
    public void ClosingEntriesStillReleaseTheirOwnersWithoutRemovingAReplacement()
    {
        var registry = new SharedWorkerRegistry();
        var key = new SharedWorkerKey("https://example.test", "https://example.test/worker.js", "", null);
        var settings = new SharedWorkerSettings("", "classic", "same-origin", false);
        var firstOwner = new object();
        var first = new SharedWorkerClient(firstOwner, new MessagePortEndpoint(), new MessagePortEndpoint(), () => { });
        var entry = registry.Connect(key, settings, first, 2, out var created, out var quota)!;
        created.Should().BeTrue();
        quota.Should().BeFalse();
        registry.BeginClose(entry);
        registry.LiveCount.Should().Be(1);
        var secondOwner = new object();
        var second = new SharedWorkerClient(secondOwner, new MessagePortEndpoint(), new MessagePortEndpoint(), () => { });
        var replacement = registry.Connect(key, settings, second, 2, out created, out quota)!;
        created.Should().BeTrue();
        quota.Should().BeFalse();
        registry.Release(firstOwner);
        first.Outer.Closed.Should().BeTrue();
        registry.LiveCount.Should().Be(1);
        registry.End(entry, WorkerEndReason.ClosedByWorker);
        registry.LiveCount.Should().Be(1);
        registry.End(replacement, WorkerEndReason.ClosedByWorker);
        registry.LiveCount.Should().Be(0);
    }

    [Test]
    public async Task InvalidUrlsAndCrossOriginUrlsFailSynchronously()
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        (await fixture.Page.EvaluateAsync<string>(
            """
            ['http://[', 'https://other.invalid/worker.js'].map(url => {
              try { new SharedWorker(url); return 'missed'; } catch(e) { return e.name; }
            }).join(',')
            """)).Should().Be("SyntaxError,SecurityError");
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    [Test]
    public async Task AHostFetchOriginOverrideDoesNotChangeTheConstructorOrigin()
    {
        await using var fixture = await Fixture(configure: options =>
            options.ConfigureEngine(o => o.WebApi.Fetch.Origin = "https://other.invalid"));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        (await fixture.Page.EvaluateAsync<string>(
            "try { new SharedWorker('https://other.invalid/worker.js'); 'missed'; } catch(e) { e.name; }"))
            .Should().Be("SecurityError");
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(1);
    }

    [Test]
    public async Task ABlankPopupCanStartAWorkerAndShareItWithItsCreator()
    {
        await using var fixture = await Fixture();
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        var opened = new TaskCompletionSource<Page>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Page.Popup += (_, popup) => opened.TrySetResult(popup);
        await fixture.Page.EvaluateAsync("globalThis.popup = window.open()");
        var popup = await opened.Task.WaitAsync(_wait);
        await Connect(popup);
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(2);
        fixture.Context.SharedWorkers.LiveCount.Should().Be(1);
    }

    [Test]
    public async Task WorkerFlagControlsTheBrowserSharedWorkerGrant()
    {
        await using var fixture = await Fixture(configure: options =>
            options.ConfigureEngine(o => o.WebApi.Features &= ~WebApiFeatures.Workers));
        (await fixture.Page.EvaluateAsync<string>("typeof Worker + ',' + typeof SharedWorker"))
            .Should().Be("undefined,undefined");
    }

    [Test]
    public async Task TheWorkerLimitBoundsUniqueGlobalsButAllowsMoreConnections()
    {
        await using var fixture = await Fixture(configure: options =>
            options.ConfigureEngine(o => o.WebApi.Workers.MaxWorkers = 1));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await Connect(fixture.Page);
        await Connect(fixture.Page);
        (await fixture.Page.EvaluateAsync<int>("messages[0]")).Should().Be(2);
        (await fixture.Page.EvaluateAsync<string>(
            "try { new SharedWorker('/worker.js','other'); 'missed'; } catch(e) { e.name; }"))
            .Should().Be("QuotaExceededError");
    }

    [TestCase("while(true) {}")]
    [TestCase("onconnect = e => { while(true) {} };")]
    [TestCase("onconnect = e => { const spin=()=>Promise.resolve().then(spin); spin(); };")]
    public async Task SharedWorkerTasksAndMicrotasksRemainBounded(string script)
    {
        await using var fixture = await Fixture(script, options => options.MaxTaskDuration = TimeSpan.FromMilliseconds(100));
        await fixture.Page.NavigateAsync(fixture.Url("/index.html"));
        await fixture.Page.EvaluateAsync("globalThis.w = new SharedWorker('/worker.js')");
        var deadline = DateTime.UtcNow + _wait;
        while (!fixture.Page.Errors.Any(e => e.Kind == PageErrorKind.WorkerError) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        fixture.Page.Errors.Should().Contain(e => e.Kind == PageErrorKind.WorkerError
            && e.Message.Contains("time budget elapsed", StringComparison.Ordinal));
        await fixture.Page.CloseAsync();
        fixture.Context.SharedWorkers.LiveCount.Should().Be(0);
    }

    private sealed class PendingWorkerLoad() : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath != "/worker.js")
            {
                return await base.SendAsync(request, cancellationToken);
            }
            Started.TrySetResult();
            try
            {
                return await Response.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }
    }
}

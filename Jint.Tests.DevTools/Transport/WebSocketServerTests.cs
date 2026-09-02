using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using Jint.DevTools;

namespace Jint.Tests.DevTools.Transport;

/// <summary>
/// The server as a client actually reaches it: a real socket, a real HTTP upgrade, real frames.
/// </summary>
/// <remarks>
/// <para>
/// Every server here binds <c>127.0.0.1</c> port 0, so nothing collides with a port a developer is using and
/// nothing is reachable from off the machine. Every wait is bounded — see <see cref="DevToolsClient"/>.
/// </para>
/// <para>
/// These are the tests the in-process ones cannot replace. The envelope and the state machine are the same
/// code either way; the upgrade handshake, the frame handling and the single writer are not exercised at all
/// without a socket, and they are exactly where a protocol server goes wrong.
/// </para>
/// </remarks>
[NonParallelizable]
public class WebSocketServerTests
{
    [Test]
    public async Task VersionNamesTheBrowserEndpointAClientThenConnectsTo()
    {
        await using var server = await StartedAsync();

        var (status, body) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/json/version");

        status.Should().Be(200);

        using var document = JsonDocument.Parse(body);
        var version = document.RootElement;

        version.GetProperty("Browser").GetString().Should().StartWith("Jint/");
        version.GetProperty("Protocol-Version").GetString().Should().Be("1.3");
        version.GetProperty("User-Agent").GetString().Should().StartWith("Jint/");
        version.GetProperty("V8-Version").GetString().Should().NotBeNullOrEmpty();
        version.GetProperty("WebKit-Version").GetString().Should().Be("0");
        version.GetProperty("webSocketDebuggerUrl").GetString().Should().Be(server.BrowserWebSocketUrl);
    }

    [Test]
    public async Task ListNamesOneEntryPerTargetWithBothUrlsAClientReads()
    {
        await using var server = await StartedAsync();
        var target = new EngineTarget(new Engine(), new EngineTargetOptions { Title = "worker", Url = "app://worker" });
        server.AddTarget(target);

        foreach (var path in new[] { "/json", "/json/list" })
        {
            var (status, body) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}{path}");
            status.Should().Be(200);

            using var document = JsonDocument.Parse(body);
            var entries = document.RootElement.EnumerateArray().ToArray();

            entries.Should().HaveCount(1);
            entries[0].GetProperty("id").GetString().Should().Be(target.TargetId);
            entries[0].GetProperty("type").GetString().Should().Be("node");
            entries[0].GetProperty("title").GetString().Should().Be("worker");
            entries[0].GetProperty("url").GetString().Should().Be("app://worker");
            entries[0].GetProperty("description").GetString().Should().BeEmpty();
            entries[0].GetProperty("webSocketDebuggerUrl").GetString()
                .Should().Be(string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{server.BoundPort}/devtools/page/{target.TargetId}"));
            entries[0].GetProperty("devtoolsFrontendUrl").GetString()
                .Should().Contain("v8only=true").And.Contain($"ws=127.0.0.1:{server.BoundPort}/devtools/page/{target.TargetId}");
        }

        await target.DisposeAsync();
    }

    [Test]
    public async Task ProtocolDescribesWhatThisServerAnswersAndNothingElse()
    {
        await using var server = await StartedAsync();

        var (status, body) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/json/protocol");
        status.Should().Be(200);

        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("version").GetProperty("major").GetString().Should().Be("1");
        document.RootElement.GetProperty("version").GetProperty("minor").GetString().Should().Be("3");

        var domains = document.RootElement.GetProperty("domains").EnumerateArray().ToArray();
        domains.Select(domain => domain.GetProperty("domain").GetString()).Should().BeEquivalentTo(["Browser", "Console", "Debugger", "Log", "Profiler", "Runtime", "Schema", "Target"]);

        var runtime = domains.Single(domain => domain.GetProperty("domain").GetString() == "Runtime");
        runtime.GetProperty("commands").EnumerateArray().Select(command => command.GetProperty("name").GetString())
            .Should().Contain("evaluate").And.Contain("getProperties").And.NotContain("terminateExecution");
        runtime.GetProperty("events").EnumerateArray().Select(name => name.GetProperty("name").GetString())
            .Should().Contain("executionContextCreated");
    }

    [Test]
    public async Task NewRefusesWithoutAnEngineFactoryAndBuildsATargetWithOne()
    {
        await using var refusing = await StartedAsync();
        var (refused, _) = await DevToolsClient.GetAsync($"http://127.0.0.1:{refusing.BoundPort}/json/new");
        refused.Should().Be(501);

        await using var making = await StartedAsync(new DevToolsServerOptions { EngineFactory = () => new Engine() });
        var (status, body) = await DevToolsClient.GetAsync($"http://127.0.0.1:{making.BoundPort}/json/new");

        status.Should().Be(200);
        using var document = JsonDocument.Parse(body);
        making.Targets.Should().ContainSingle(target => target.TargetId == document.RootElement.GetProperty("id").GetString());
    }

    [Test]
    public async Task ActivateAndCloseAddressATargetByIdentifier()
    {
        await using var server = await StartedAsync();
        var target = new EngineTarget(new Engine());
        server.AddTarget(target);

        var (activated, activatedBody) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/json/activate/{target.TargetId}");
        activated.Should().Be(200);
        activatedBody.Should().Be("Target activated");

        var (missing, _) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/json/activate/NOPE");
        missing.Should().Be(404);

        var (closed, closedBody) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/json/close/{target.TargetId}");
        closed.Should().Be(200);
        closedBody.Should().Be("Target is closing");
        server.Targets.Should().BeEmpty();

        await target.DisposeAsync();
    }

    [Test]
    public async Task AnUnknownPathIsNotFound()
    {
        await using var server = await StartedAsync();

        var (status, _) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/nope");
        status.Should().Be(404);
    }

    [TestCase(ThreadMode.HostOwned)]
    [TestCase(ThreadMode.LibraryOwned)]
    public async Task AClientConnectsAutoAttachesAndEvaluates(ThreadMode mode)
    {
        await using var server = await StartedAsync();
        await using var target = new EngineTarget(new Engine(options => options.UseDevTools()), new EngineTargetOptions { ThreadMode = mode });
        server.AddTarget(target);

        using var pumping = new CancellationTokenSource();
        var pump = mode == ThreadMode.HostOwned ? Pump(target, pumping.Token) : Task.CompletedTask;

        try
        {
            await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

            var version = await client.ResultOfAsync("Browser.getVersion");
            version.GetProperty("product").GetString().Should().StartWith("Jint/");

            await client.ResultOfAsync("Target.setDiscoverTargets", """{"discover":true}""");
            await client.ResultOfAsync(
                "Target.setAutoAttach",
                """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true,"filter":[{"type":"page","exclude":true},{}]}""");

            var attached = await client.WaitForEventAsync("Target.attachedToTarget");
            var sessionId = attached.GetProperty("params").GetProperty("sessionId").GetString()!;
            attached.GetProperty("params").GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(target.TargetId);

            await client.ResultOfAsync("Runtime.runIfWaitingForDebugger", sessionId: sessionId);
            await client.ResultOfAsync("Runtime.enable", sessionId: sessionId);

            var context = await client.WaitForEventAsync("Runtime.executionContextCreated");
            context.GetProperty("sessionId").GetString().Should().Be(sessionId);

            var evaluated = await client.ResultOfAsync(
                "Runtime.evaluate",
                """{"expression":"6*7","returnByValue":true}""",
                sessionId);

            evaluated.GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);
        }
        finally
        {
            await pumping.CancelAsync();
            await pump;
        }
    }

    [Test]
    public async Task ADirectPageConnectionCarriesNoSessionIdentifier()
    {
        await using var server = await StartedAsync();
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        server.AddTarget(target);

        var url = string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{server.BoundPort}/devtools/page/{target.TargetId}");
        await using var client = await DevToolsClient.ConnectAsync(url);

        var evaluated = await client.ResultOfAsync("Runtime.evaluate", """{"expression":"1+1","returnByValue":true}""");
        evaluated.GetProperty("result").GetProperty("value").GetInt32().Should().Be(2);

        await client.ResultOfAsync("Runtime.enable");
        var context = await client.WaitForEventAsync("Runtime.executionContextCreated");
        context.TryGetProperty("sessionId", out _).Should().BeFalse("a direct connection is the session, so nothing addresses it");

        // The target tree is the browser endpoint's business; a direct connection is one engine and no more.
        var error = (await client.SendAsync("Target.getTargets")).GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32601);
    }

    [Test]
    public async Task TwoClientsAreServedAtOnceAndSeeTheirOwnSessions()
    {
        await using var server = await StartedAsync();
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        server.AddTarget(target);

        await using var first = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);
        await using var second = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

        var firstSession = (await first.ResultOfAsync("Target.attachToTarget", $$"""{"targetId":"{{target.TargetId}}","flatten":true}"""))
            .GetProperty("sessionId").GetString()!;
        var secondSession = (await second.ResultOfAsync("Target.attachToTarget", $$"""{"targetId":"{{target.TargetId}}","flatten":true}"""))
            .GetProperty("sessionId").GetString()!;

        secondSession.Should().NotBe(firstSession, "each conversation mints its own attachments");

        var evaluated = await second.ResultOfAsync("Runtime.evaluate", """{"expression":"1+2","returnByValue":true}""", secondSession);
        evaluated.GetProperty("result").GetProperty("value").GetInt32().Should().Be(3);

        // One client's session identifier is meaningless on the other's connection, which is what keeps two
        // clients from reaching into each other's attachments.
        var crossed = (await first.SendAsync("Runtime.evaluate", """{"expression":"1"}""", secondSession)).GetProperty("error");
        crossed.GetProperty("code").GetInt32().Should().Be(-32001);
    }

    [Test]
    public async Task AnUnknownEndpointIsRefusedBeforeTheUpgrade()
    {
        await using var server = await StartedAsync();

        var url = string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{server.BoundPort}/devtools/page/NOPE");

        var thrown = Assert.ThrowsAsync<WebSocketException>(async () => await DevToolsClient.ConnectAsync(url));
        thrown.Should().NotBeNull("a client that guessed a path is told so rather than left holding an open socket");
    }

    [Test]
    public async Task ABinaryFrameClosesTheConnection()
    {
        await using var server = await StartedAsync();
        await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

        await client.SendBinaryAsync();

        (await client.WaitForCloseAsync()).Should().Be(WebSocketCloseStatus.InvalidMessageType);
    }

    [Test]
    public async Task AMessageOverTheBoundClosesTheConnection()
    {
        await using var server = await StartedAsync(new DevToolsServerOptions { MaxMessageBytes = 1024 });
        await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

        var padding = new string('x', 4096);
        await client.SendRawAsync("{\"id\":1,\"method\":\"Browser.getVersion\",\"params\":{\"pad\":\"" + padding + "\"}}");

        (await client.WaitForCloseAsync()).Should().Be(WebSocketCloseStatus.MessageTooBig);
    }

    /// <summary>
    /// A client that goes away mid-command must not take the engine thread with it: the command was already
    /// on the mailbox, so it runs, and the connection ends around it.
    /// </summary>
    /// <remarks>
    /// The client closes its half of the stream rather than aborting it, which is what makes this a test
    /// rather than a race. An abort resets the connection, and a reset lets the peer's kernel discard
    /// whatever it had not read yet — including the very command whose effect is asserted below, which is
    /// how this failed on the ARM leg with <c>Expected number but got Undefined</c>. Closing sends the frame
    /// in order behind the command, so the server reads the command first, every time.
    /// </remarks>
    [Test]
    public async Task AClientClosingMidCommandLeavesTheEngineRunning()
    {
        await using var server = await StartedAsync();
        await using var target = new EngineTarget(new Engine(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        server.AddTarget(target);

        var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);
        var sessionId = (await client.ResultOfAsync("Target.attachToTarget", $$"""{"targetId":"{{target.TargetId}}","flatten":true}"""))
            .GetProperty("sessionId").GetString()!;

        await client.SendRawAsync($$"""{"id":99,"method":"Runtime.evaluate","params":{"expression":"globalThis.marker = 7"},"sessionId":"{{sessionId}}"}""");
        await client.DisposeAsync();

        var marker = await target.PostAsync(engine => engine.Evaluate("globalThis.marker").AsNumber()).WaitAsync(TimeSpan.FromSeconds(120));
        marker.Should().Be(7, "the command was already queued when the socket went, and the engine is still the host's");

        await using var second = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);
        (await second.ResultOfAsync("Browser.getVersion")).GetProperty("product").GetString().Should().StartWith("Jint/");
    }

    [Test]
    public async Task BrowserCloseEndsTheClientRatherThanTheHost()
    {
        await using var server = await StartedAsync();
        await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

        await client.ResultOfAsync("Browser.close");
        await client.WaitForCloseAsync();

        await using var second = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);
        (await second.ResultOfAsync("Browser.getVersion")).GetProperty("product").GetString().Should().StartWith("Jint/");
    }

    /// <summary>
    /// What the host called itself is what both a client and a browser-endpoint command are told, because
    /// the two are the same answer read twice.
    /// </summary>
    [Test]
    public async Task TheHostsOwnProductNameReachesBothAnswers()
    {
        await using var server = await StartedAsync(new DevToolsServerOptions { Product = "Contoso Workflow/3.1" });

        var (_, body) = await DevToolsClient.GetAsync($"http://127.0.0.1:{server.BoundPort}/json/version");
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("Browser").GetString().Should().Be("Contoso Workflow/3.1");
        document.RootElement.GetProperty("User-Agent").GetString().Should().Be("Contoso Workflow/3.1");

        await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);
        var version = await client.ResultOfAsync("Browser.getVersion");
        version.GetProperty("product").GetString().Should().Be("Contoso Workflow/3.1");
        version.GetProperty("jsVersion").GetString().Should().NotBe("Contoso Workflow/3.1", "the engine version is Jint's whatever the host calls itself");
    }

    /// <summary>
    /// A host whose whole purpose is the endpoint turns the disconnect off and decides for itself what
    /// <c>Browser.close</c> means; the command still succeeds, because a client that fails on the way out
    /// reports a failure it cannot act on.
    /// </summary>
    [Test]
    public async Task BrowserCloseLeavesTheConnectionOpenWhenTheHostSaysSo()
    {
        await using var server = await StartedAsync(new DevToolsServerOptions { CloseIsDisconnect = false });
        await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

        (await client.ResultOfAsync("Browser.close")).GetRawText().Should().Be("{}");

        var version = await client.ResultOfAsync("Browser.getVersion");
        version.GetProperty("product").GetString().Should().StartWith("Jint/");
    }

    /// <summary>
    /// Stopping the server ends the conversations it was holding, rather than leaving a client waiting on a
    /// socket nothing is reading any more.
    /// </summary>
    [Test]
    public async Task DisposingTheServerClosesTheClientsThatWereConnected()
    {
        var server = await StartedAsync();
        await using var client = await DevToolsClient.ConnectAsync(server.BrowserWebSocketUrl);

        await client.ResultOfAsync("Browser.getVersion");
        await server.DisposeAsync();

        await client.WaitForCloseAsync();
    }

    [Test]
    public async Task StartingTwiceIsRefusedAndAnUnstartedServerHasNoAddress()
    {
        await using var server = new DevToolsServer();

        Assert.Throws<InvalidOperationException>(() => _ = server.BrowserWebSocketUrl);
        server.BoundPort.Should().Be(0);

        server.Start();
        server.BoundPort.Should().BeGreaterThan(0);
        Assert.Throws<InvalidOperationException>(() => server.Start());
    }

    private static async Task<DevToolsServer> StartedAsync(DevToolsServerOptions? options = null)
    {
        var server = new DevToolsServer(options ?? new DevToolsServerOptions());
        await server.StartAsync();
        return server;
    }

    private static Task Pump(EngineTarget target, CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    target.Pump();
                    target.Engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(20));
                }
            },
            CancellationToken.None);
    }
}

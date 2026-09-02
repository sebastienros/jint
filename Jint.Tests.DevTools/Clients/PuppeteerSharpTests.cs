using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Jint.DevTools;
using Jint.DevTools.Protocol;
using PuppeteerSharp;

namespace Jint.Tests.DevTools.Clients;

/// <summary>
/// A real client library, over a real socket: PuppeteerSharp connects, lists the target, opens a session and
/// evaluates in it.
/// </summary>
/// <remarks>
/// <para>
/// This is the only test in the suite that can make a claim about <i>client compatibility</i>. Everything
/// else asserts what this server answers; this asserts that a library nobody here wrote, written against
/// Chrome, is satisfied by it. The version is the one the handshake in
/// <c>tools/devtools-protocol/handshakes/puppeteersharp-dotnet.json</c> was recorded with, so what it sends
/// here is what that file says it sends.
/// </para>
/// <para>
/// <b>No browser is downloaded and none is launched.</b> <c>ConnectAsync</c> speaks to an endpoint that
/// already exists, which is exactly what this server is; Puppeteer's browser-fetching machinery is never
/// touched.
/// </para>
/// <para>
/// A target is found by the location the host gave it and then confirmed by asking the engine for its own
/// identifier through <c>Runtime.getIsolateId</c>, rather than by reading an identifier off the client's
/// handle: what a client library exposes about a target type it has never heard of is the client's business,
/// and a test written against it would be testing the client.
/// </para>
/// <para>
/// What Puppeteer cannot do here is anything page-shaped — <c>NewPageAsync</c>, <c>GotoAsync</c>, <c>$</c> —
/// because an engine target is a Node-flavoured target with no document. That is not a gap in the transport
/// and is not closed by more of this package; it is what <c>Jint.Browser</c> is for.
/// </para>
/// </remarks>
[NonParallelizable]
public class PuppeteerSharpTests
{
    /// <summary>
    /// Generous on purpose: it is there to stop a hang, not to assert a speed. Each of these finishes in
    /// well under a second unloaded, and a two-core CI runner sharing the machine with four other test
    /// processes has been seen to make this suite two orders of magnitude slower.
    /// </summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(120);

    [Test]
    public async Task PuppeteerConnectsListsTheTargetOpensASessionAndEvaluates()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Title = "jint", Url = "jint://engine", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });

        browser.IsConnected.Should().BeTrue();
        browser.Targets().Should().NotBeEmpty("the client asks for the target list while connecting, and an engine is on it");

        var session = await SessionForAsync(browser, "jint://engine", target.TargetId);

        var evaluated = await session.SendAsync(
            "Runtime.evaluate",
            new { expression = "6 * 7", returnByValue = true }).WaitAsync(Bound);

        Value(evaluated).GetInt32().Should().Be(42);

        await session.DetachAsync().WaitAsync(Bound);

        browser.Disconnect();
        browser.IsConnected.Should().BeFalse();
    }

    /// <summary>
    /// The other entry a client takes: read <c>/json/version</c> for the endpoint, then connect to it. It is
    /// what <c>connect({ browserURL })</c> and <c>connectOverCDP(url)</c> both do, and it is the reason the
    /// discovery documents are served from the same port.
    /// </summary>
    [Test]
    public async Task PuppeteerConnectsThroughTheDiscoveryDocument()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Url = "jint://engine", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserURL = $"http://127.0.0.1:{server.BoundPort}" });

        browser.IsConnected.Should().BeTrue();

        var session = await SessionForAsync(browser, "jint://engine", target.TargetId);
        var evaluated = await session.SendAsync("Runtime.evaluate", new { expression = "1 + 1", returnByValue = true }).WaitAsync(Bound);
        Value(evaluated).GetInt32().Should().Be(2);

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// Two engines behind one endpoint, which is the shape a host with a pool of them is in. Each session
    /// reaches its own engine and no other.
    /// </summary>
    [Test]
    public async Task PuppeteerSeesEveryTargetAndEvaluatesInEachSeparately()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var first = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Title = "first", Url = "jint://first", ThreadMode = ThreadMode.LibraryOwned });
        await using var second = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Title = "second", Url = "jint://second", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(first);
        server.AddTarget(second);

        await first.PostAsync(engine => engine.SetValue("name", "first")).WaitAsync(Bound);
        await second.PostAsync(engine => engine.SetValue("name", "second")).WaitAsync(Bound);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });

        foreach (var (target, expected) in new[] { (first, "first"), (second, "second") })
        {
            var session = await SessionForAsync(browser, "jint://" + expected, target.TargetId);

            var evaluated = await session.SendAsync(
                "Runtime.evaluate",
                new { expression = "name", returnByValue = true }).WaitAsync(Bound);

            Value(evaluated).GetString().Should().Be(expected);

            await session.DetachAsync().WaitAsync(Bound);
        }
    }

    /// <summary>
    /// The handle path, end to end and over a socket: evaluate something that cannot be sent by value, ask
    /// what is inside it, then let it go.
    /// </summary>
    /// <remarks>
    /// It is the busiest path any recorded client takes —
    /// <c>tools/devtools-protocol/handshakes/matrix.md</c> counts <c>Runtime.releaseObject</c> 17 times and
    /// <c>Runtime.getProperties</c> twice for PuppeteerSharp alone — and the one where a handle that dangles
    /// or a handle that never dies shows up as a client bug rather than as a failed command.
    /// </remarks>
    [Test]
    public async Task PuppeteerWalksAnObjectHandleAndThenReleasesIt()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Url = "jint://handles", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://handles", target.TargetId);

        var evaluated = await session.SendAsync(
            "Runtime.evaluate",
            new { expression = "({ answer: 42, nested: { deep: 'yes' } })" }).WaitAsync(Bound);

        var objectId = evaluated!.Value.GetProperty("result").GetProperty("objectId").GetString();
        objectId.Should().NotBeNullOrEmpty();

        var properties = await session.SendAsync(
            "Runtime.getProperties",
            new { objectId, ownProperties = true }).WaitAsync(Bound);

        var listed = properties!.Value.GetProperty("result").EnumerateArray().ToArray();
        listed.Select(property => property.GetProperty("name").GetString()).Should().BeEquivalentTo(["answer", "nested"]);
        listed.Single(property => property.GetProperty("name").GetString() == "answer")
            .GetProperty("value").GetProperty("value").GetInt32().Should().Be(42);

        await session.SendAsync("Runtime.releaseObject", new { objectId }).WaitAsync(Bound);

        var afterRelease = async () => await session.SendAsync("Runtime.getProperties", new { objectId, ownProperties = true }).WaitAsync(Bound);
        (await afterRelease.Should().ThrowAsync<Exception>()).WithMessage("*Could not find object with given id*");

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// The binding path, which is how Puppeteer's <c>exposeFunction</c> gets an answer out of a page: the
    /// client installs a global, the script calls it, and the client hears about it as an event.
    /// </summary>
    [Test]
    public async Task PuppeteerInstallsABindingAndHearsScriptCallIt()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Url = "jint://bindings", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://bindings", target.TargetId);

        var called = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.MessageReceived += (_, message) =>
        {
            if (message.MessageID == "Runtime.bindingCalled")
            {
                called.TrySetResult(message.MessageData.GetProperty("payload").GetString()!);
            }
        };

        await session.SendAsync("Runtime.addBinding", new { name = "report" }).WaitAsync(Bound);
        await session.SendAsync("Runtime.evaluate", new { expression = "report('from the script')" }).WaitAsync(Bound);

        (await called.Task.WaitAsync(Bound)).Should().Be("from the script");

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// What a script logged, arriving at a real client as the event it listens for.
    /// </summary>
    /// <remarks>
    /// The console is opt-in, so the engine is built with <see cref="WebApiFeatures.Console"/> as a host
    /// that wants one builds it; an engine without it has no <c>console</c> object and nothing to report.
    /// </remarks>
    [Test]
    public async Task PuppeteerHearsWhatTheScriptLogged()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options =>
            {
                options.WebApi.Features |= WebApiFeatures.Console;
                options.UseDevTools();
            }),
            new EngineTargetOptions { Url = "jint://console", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://console", target.TargetId);

        var logged = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.MessageReceived += (_, message) =>
        {
            if (message.MessageID == "Runtime.consoleAPICalled")
            {
                logged.TrySetResult(message.MessageData.Clone());
            }
        };

        await session.SendAsync("Runtime.enable").WaitAsync(Bound);
        await session.SendAsync("Runtime.evaluate", new { expression = "console.warn('from the script', { a: 1 })" }).WaitAsync(Bound);

        var call = await logged.Task.WaitAsync(Bound);
        call.GetProperty("type").GetString().Should().Be("warning");

        var args = call.GetProperty("args").EnumerateArray().ToArray();
        args[0].GetProperty("value").GetString().Should().Be("from the script");
        args[1].GetProperty("preview").GetProperty("properties").EnumerateArray().Single()
            .GetProperty("name").GetString().Should().Be("a");

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// A breakpoint, a pause, and a resume, driven by a real client over a real socket.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one test that proves the pause loop against something nobody here wrote. The evaluation that hits
    /// the breakpoint is deliberately <b>not awaited</b>: it is what pauses, so awaiting it before resuming
    /// would be waiting for this test to answer itself — which is exactly the deadlock a client would meet if
    /// the pause loop stopped draining the mailbox.
    /// </para>
    /// <para>
    /// It also proves the part a client cannot see from an in-process test: the socket keeps writing while the
    /// engine thread is held inside the debugger, so the <c>Debugger.paused</c> event arrives and the
    /// <c>Debugger.resume</c> command is answered on a thread that is not the engine's.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PuppeteerSetsABreakpointHearsThePauseAndResumes()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Url = "jint://debugger", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await target.PostAsync(engine => engine.Execute(
            """
            function twice(n) {
                var doubled = n * 2;
                return doubled;
            }
            """,
            "twice.js")).WaitAsync(Bound);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://debugger", target.TargetId);

        var paused = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        session.MessageReceived += (_, message) =>
        {
            if (message.MessageID == "Debugger.paused")
            {
                paused.TrySetResult(message.MessageData.Clone());
            }
            else if (message.MessageID == "Debugger.resumed")
            {
                resumed.TrySetResult(true);
            }
        };

        await session.SendAsync("Runtime.enable").WaitAsync(Bound);
        await session.SendAsync("Debugger.enable").WaitAsync(Bound);

        var set = await session.SendAsync(
            "Debugger.setBreakpointByUrl",
            new { url = "twice.js", lineNumber = 2 }).WaitAsync(Bound);

        var breakpointId = set!.Value.GetProperty("breakpointId").GetString();
        set.Value.GetProperty("locations").GetArrayLength().Should().Be(1, "the script is already parsed, so the breakpoint is placed at once");

        var pending = session.SendAsync("Runtime.evaluate", new { expression = "twice(21)", returnByValue = true });

        var stop = await paused.Task.WaitAsync(Bound);
        stop.GetProperty("reason").GetString().Should().Be("other");
        stop.GetProperty("hitBreakpoints").EnumerateArray().Single().GetString().Should().Be(breakpointId);

        var frame = stop.GetProperty("callFrames").EnumerateArray().First();
        frame.GetProperty("functionName").GetString().Should().Be("twice");

        var read = await session.SendAsync(
            "Debugger.evaluateOnCallFrame",
            new { callFrameId = frame.GetProperty("callFrameId").GetString(), expression = "doubled", returnByValue = true }).WaitAsync(Bound);

        read!.Value.GetProperty("result").GetProperty("value").GetInt32().Should().Be(42);

        await session.SendAsync("Debugger.resume").WaitAsync(Bound);
        await resumed.Task.WaitAsync(Bound);

        Value(await pending.WaitAsync(Bound)).GetInt32().Should().Be(42);

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// The other way a front end stops an engine: not at a line it chose, but at a throw nothing catches.
    /// </summary>
    /// <remarks>
    /// <c>setPauseOnExceptions</c> is a command every recorded client sends while connecting, and until now
    /// it recorded a state and stopped on nothing. What this asserts is the whole round trip over a socket:
    /// the state reaches the engine, the throw stops it where it happened, the client is handed the thrown
    /// value and told nothing was waiting to catch it, and the resume lets the exception carry on out to the
    /// host that was running the script.
    /// </remarks>
    [Test]
    public async Task PuppeteerPausesOnAnUncaughtThrowAndResumes()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Url = "jint://throwing", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await target.PostAsync(engine => engine.Execute(
            """
            function fail(reason) {
                throw new Error(reason);
            }
            """,
            "fail.js")).WaitAsync(Bound);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://throwing", target.TargetId);

        var paused = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        session.MessageReceived += (_, message) =>
        {
            if (message.MessageID == "Debugger.paused")
            {
                paused.TrySetResult(message.MessageData.Clone());
            }
            else if (message.MessageID == "Debugger.resumed")
            {
                resumed.TrySetResult(true);
            }
        };

        await session.SendAsync("Runtime.enable").WaitAsync(Bound);
        await session.SendAsync("Debugger.enable").WaitAsync(Bound);
        await session.SendAsync("Debugger.setPauseOnExceptions", new { state = "uncaught" }).WaitAsync(Bound);

        // Host work rather than a command, so the throw reaches a host the way an embedder's own script does.
        var running = target.PostAsync(engine =>
        {
            try
            {
                engine.Evaluate("fail('over the socket')");
                return "returned";
            }
            catch (Jint.Runtime.JavaScriptException exception)
            {
                return exception.Message;
            }
        });

        var stop = await paused.Task.WaitAsync(Bound);

        stop.GetProperty("reason").GetString().Should().Be("exception");
        stop.GetProperty("hitBreakpoints").EnumerateArray().Should().BeEmpty();

        var data = stop.GetProperty("data");
        data.GetProperty("subtype").GetString().Should().Be("error");
        data.GetProperty("uncaught").GetBoolean().Should().BeTrue();
        data.GetProperty("description").GetString().Should().Contain("over the socket");

        stop.GetProperty("callFrames").EnumerateArray().First()
            .GetProperty("functionName").GetString().Should().Be("fail", "the engine stopped at the throw, not after it");

        await session.SendAsync("Debugger.resume").WaitAsync(Bound);
        await resumed.Task.WaitAsync(Bound);

        // Reporting a pause is not handling the throw: it carries on to the host that was running the script.
        (await running.WaitAsync(Bound)).Should().Be("over the socket");

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// A profile taken over a real socket, in the document a front end's Performance panel loads.
    /// </summary>
    /// <remarks>
    /// The reply is read back through the data transfer objects generated from the pinned
    /// <c>js_protocol.json</c>, whose <c>required</c> members make a missing one a failure rather than a
    /// default — so this asserts the shape a client would accept and not merely the shape this server meant
    /// to send.
    /// </remarks>
    [Test]
    public async Task PuppeteerRecordsAProfileOfWhatTheEngineRan()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools()),
            new EngineTargetOptions { Url = "jint://profiler", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await target.PostAsync(engine => engine.Execute(
            """
            function leaf(n) {
                return n * n;
            }

            function busy(times) {
                var total = 0;
                for (var i = 0; i < times; i++) {
                    total = total + leaf(i);
                }

                return total;
            }
            """,
            "busy.js")).WaitAsync(Bound);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://profiler", target.TargetId);

        await session.SendAsync("Profiler.enable").WaitAsync(Bound);
        await session.SendAsync("Profiler.setSamplingInterval", new { interval = 100 }).WaitAsync(Bound);
        await session.SendAsync("Profiler.start").WaitAsync(Bound);

        await session.SendAsync("Runtime.evaluate", new { expression = "busy(500)", returnByValue = true }).WaitAsync(Bound);

        var stopped = await session.SendAsync("Profiler.stop").WaitAsync(Bound);
        var profile = stopped!.Value.GetProperty("profile").Deserialize(ProtocolJsonContext.Default.ProfilerProfile);

        profile.Should().NotBeNull();
        profile!.Nodes.Select(node => node.CallFrame.FunctionName).Should().Contain(["(root)", "busy", "leaf"]);
        profile.Samples.Should().NotBeNullOrEmpty();
        profile.TimeDeltas!.Length.Should().Be(profile.Samples!.Length);
        profile.EndTime.Should().BeGreaterThan(profile.StartTime);

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// Which of the engine's code ran, taken over a real socket in the shape a front end's Coverage panel
    /// draws.
    /// </summary>
    /// <remarks>
    /// The assertion that matters is the second one. Coverage is a <i>covered set</i> — a function that
    /// never ran has no entry anywhere in the engine — so a client is told about it only because the domain
    /// derives the uncovered functions from the script's abstract syntax tree. A panel that shaded nothing
    /// red would look exactly like a passing test without it.
    /// </remarks>
    [Test]
    public async Task PuppeteerTakesCoverageAndIsToldWhatNeverRan()
    {
        await using var server = new DevToolsServer();
        await server.StartAsync();

        await using var target = new EngineTarget(
            new Engine(options => options.UseDevTools(devTools => devTools.Coverage = true)),
            new EngineTargetOptions { Url = "jint://coverage", ThreadMode = ThreadMode.LibraryOwned });

        server.AddTarget(target);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
        var session = await SessionForAsync(browser, "jint://coverage", target.TargetId);

        await session.SendAsync("Profiler.enable").WaitAsync(Bound);
        await session.SendAsync("Profiler.startPreciseCoverage", new { callCount = true, detailed = false }).WaitAsync(Bound);

        // Executed after the recording started, which is the order a client uses: what ran before it began
        // is not what the panel is asking about.
        await target.PostAsync(engine => engine.Execute(
            """
            function counted(n) {
                return n + 1;
            }

            function skipped(n) {
                return n - 1;
            }

            var total = counted(1);
            """,
            "coverage.js")).WaitAsync(Bound);

        var taken = await session.SendAsync("Profiler.takePreciseCoverage").WaitAsync(Bound);
        var scripts = taken!.Value.GetProperty("result").Deserialize(ProtocolJsonContext.Default.ProfilerScriptCoverageArray);

        scripts.Should().NotBeNull();
        var script = scripts!.Single(candidate => candidate.Url == "coverage.js");

        var functions = script.Functions.ToDictionary(function => function.FunctionName, function => function);
        functions.Should().ContainKeys("counted", "skipped");
        functions["counted"].Ranges.Single().Count.Should().Be(1);
        functions["skipped"].Ranges.Single().Count.Should().Be(0, "it is declared and never called, which is what the panel shades");

        await session.SendAsync("Profiler.stopPreciseCoverage").WaitAsync(Bound);
        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// The REPL's <c>--inspect</c>, from the outside: a separate process, its own port, and a client that
    /// reaches the engine that process ran its script in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything else in this suite builds its own server in this process, so nothing else would notice if
    /// the flag stopped serving a target — the wiring the flag does is a host's wiring, and a host is the
    /// one thing an in-process test cannot be. It also pins the two halves of a
    /// <see cref="ThreadMode.HostOwned"/> host that are easy to get wrong: the banner has to name the port
    /// the operating system actually chose, and the engine has to keep being pumped after the script it was
    /// given has finished, or every command a client sends times out.
    /// </para>
    /// <para>
    /// The script arrives on standard input rather than in a file, because a redirected standard input is
    /// what the REPL reads when it is not given <c>-f</c>, and a test that wrote a temporary file would be
    /// testing the file path instead of the flag.
    /// </para>
    /// </remarks>
    [Test]
    public async Task PuppeteerReachesTheEngineTheReplsInspectFlagServes()
    {
        using var repl = ReplProcess.Start("--inspect=0");

        var port = await repl.ListeningPortAsync();
        port.Should().BeGreaterThan(0, "port 0 asks the operating system for one, and the banner has to name what it chose");

        await repl.RunAsync("function twice(n) { return n * 2; }");
        await repl.Finished.WaitAsync(Bound);

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserURL = $"http://127.0.0.1:{port}" });
        var target = browser.Targets().Single(candidate => candidate.Url == "jint://repl");

        var session = await target.CreateCDPSessionAsync().WaitAsync(Bound);
        var evaluated = await session.SendAsync("Runtime.evaluate", new { expression = "twice(21)", returnByValue = true }).WaitAsync(Bound);

        Value(evaluated).GetInt32().Should().Be(42, "the client reached the engine that process ran its script in, and that engine is still being pumped");

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// <c>--inspect-brk</c>: the script the REPL was given does not run until a client has attached and said
    /// so.
    /// </summary>
    /// <remarks>
    /// The half of the flag that cannot be checked by seeing something happen, because what it promises is
    /// that nothing does. The negative is asserted with a bounded wait rather than a sleep and a poll: if the
    /// hold were broken the script would run within milliseconds of standard input closing, so a wait that
    /// times out is the assertion, and the positive half immediately after it is what stops that timeout from
    /// passing for a REPL that never got as far as reading its input at all.
    /// </remarks>
    [Test]
    public async Task TheReplHoldsItsScriptUntilAClientSaysToRunIt()
    {
        using var repl = ReplProcess.Start("--inspect-brk=0");

        var port = await repl.ListeningPortAsync();
        await repl.RunAsync("globalThis.ran = true;");

        var held = async () => await repl.Finished.WaitAsync(TimeSpan.FromSeconds(2));
        await held.Should().ThrowAsync<TimeoutException>("nothing the host queued runs until a client releases the target");

        await using var browser = await ConnectAsync(new ConnectOptions { BrowserURL = $"http://127.0.0.1:{port}" });
        var target = browser.Targets().Single(candidate => candidate.Url == "jint://repl");
        var session = await target.CreateCDPSessionAsync().WaitAsync(Bound);

        await session.SendAsync("Runtime.runIfWaitingForDebugger").WaitAsync(Bound);
        await repl.Finished.WaitAsync(Bound);

        var evaluated = await session.SendAsync("Runtime.evaluate", new { expression = "ran === true", returnByValue = true }).WaitAsync(Bound);
        Value(evaluated).GetBoolean().Should().BeTrue("the held script ran once the client released it, and it ran in the engine this session reaches");

        await session.DetachAsync().WaitAsync(Bound);
    }

    /// <summary>
    /// The REPL as a separate process, which is the only way an inspector flag can be checked: the wiring it
    /// does is a host's wiring, and a host is the one thing an in-process test cannot be.
    /// </summary>
    private sealed class ReplProcess : IDisposable
    {
        private const string Banner = "Debugger listening on ws://";

        private readonly Process _process = new();
        private readonly TaskCompletionSource<string> _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<string> _printed = [];

        private ReplProcess()
        {
        }

        /// <summary>Completes when the REPL says the script it was given has finished.</summary>
        internal Task Finished => _finished.Task;

        internal static ReplProcess Start(params string[] arguments)
        {
            var assembly = RepositoryPaths.ReplAssembly;
            assembly.Should().NotBeNull("Jint.Repl has to be built for this test; run `dotnet build -c Release Jint.Repl/Jint.Repl.csproj`");

            var repl = new ReplProcess();

            // A framework-dependent assembly is run by the .NET host, and the one running this test is the
            // host to use: it is the runtime this build was verified against rather than whichever `dotnet`
            // a PATH happens to name first.
            var host = Environment.ProcessPath is { } path && Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                ? path
                : "dotnet";

            repl._process.StartInfo = new ProcessStartInfo(host)
            {
                ArgumentList = { assembly!, "-t", "60" },
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = RepositoryPaths.Root,
            };

            foreach (var argument in arguments)
            {
                repl._process.StartInfo.ArgumentList.Add(argument);
            }

            repl._process.OutputDataReceived += repl.OnOutput;
            repl._process.ErrorDataReceived += repl.OnOutput;

            repl._process.Start();
            repl._process.BeginOutputReadLine();
            repl._process.BeginErrorReadLine();

            return repl;
        }

        /// <summary>The port the banner named, which is what port 0 makes worth reading.</summary>
        internal async Task<int> ListeningPortAsync()
        {
            var banner = await _listening.Task.WaitAsync(Bound);
            var authority = banner.Substring(Banner.Length).Split('/')[0];

            return int.Parse(authority.Split(':')[1], CultureInfo.InvariantCulture);
        }

        /// <summary>Hands the REPL a script on standard input, which is what it reads when given no file.</summary>
        internal async Task RunAsync(string script)
        {
            await _process.StandardInput.WriteAsync(script + "\n");
            _process.StandardInput.Close();
        }

        public void Dispose()
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit((int) Bound.TotalMilliseconds);
            }
            catch (InvalidOperationException)
            {
                // Already gone, which is the outcome the kill was for.
            }

            lock (_printed)
            {
                TestContext.Out.WriteLine(string.Join(Environment.NewLine, _printed));
            }

            _process.Dispose();
        }

        private void OnOutput(object sender, DataReceivedEventArgs line)
        {
            if (line.Data is not { } text)
            {
                return;
            }

            lock (_printed)
            {
                _printed.Add(text);
            }

            if (text.StartsWith(Banner, StringComparison.Ordinal))
            {
                _listening.TrySetResult(text);
            }
            else if (text.StartsWith("The script has finished", StringComparison.Ordinal))
            {
                _finished.TrySetResult(true);
            }
        }
    }

    /// <summary>The <c>result.value</c> of a <c>Runtime.evaluate</c> reply the client handed back.</summary>
    private static JsonElement Value(JsonElement? reply) => reply!.Value.GetProperty("result").GetProperty("value");

    private static Task<IBrowser> ConnectAsync(ConnectOptions options)
    {
        options.ProtocolTimeout = (int) Bound.TotalMilliseconds;
        return Puppeteer.ConnectAsync(options).WaitAsync(Bound);
    }

    /// <summary>
    /// Opens a session on the client's handle for one engine, found by the location the host gave it, and
    /// checks that it really is that engine before handing it back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bounded rather than immediate, because a client's target list is built from events it processes on
    /// its own schedule: <c>ConnectAsync</c> has returned by the time the discovery events are in its
    /// buffer, and reading the list in that window is a race the client owns and this test does not.
    /// </para>
    /// <para>
    /// The target is found by <c>Url</c> — which is what <see cref="EngineTargetOptions.Url"/> put there,
    /// so finding it is itself a check — and then confirmed by asking the engine for its own identifier.
    /// Probing by opening a session on each candidate would not do: detaching from the wrong one takes it
    /// out of the client's list of available targets, and the next search would not find it.
    /// </para>
    /// </remarks>
    private static async Task<ICDPSession> SessionForAsync(IBrowser browser, string url, string targetId)
    {
        var deadline = DateTime.UtcNow + Bound;
        var seen = 0;

        while (DateTime.UtcNow < deadline)
        {
            var targets = browser.Targets();
            seen = targets.Length;

            foreach (var candidate in targets)
            {
                if (candidate.Url != url)
                {
                    continue;
                }

                var session = await candidate.CreateCDPSessionAsync().WaitAsync(Bound);

                var isolate = await session.SendAsync("Runtime.getIsolateId").WaitAsync(Bound);
                isolate!.Value.GetProperty("id").GetString().Should().Be(targetId, "the session reaches the engine the client listed under '{0}'", url);

                return session;
            }

            await Task.Delay(25);
        }

        throw new InvalidOperationException($"the client listed {seen} target(s) within {Bound} and none of them was at '{url}'");
    }
}

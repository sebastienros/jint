using System.Text.Json;
using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// A target outlives its engine: what a client is told when the engine under it is replaced, and what stops
/// meaning anything when it is.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of the protocol layer a page needs and an engine target never exercises. Everything a
/// client holds — a handle, a script identifier, an execution-context identifier — names one engine, and a
/// navigation ends all of them at once; everything the client <i>asked for</i> — a binding, a breakpoint by
/// URL — is owed to the next document as much as it was to this one. Each test below is one half of that.
/// </para>
/// <para>
/// Every wait is bounded, and the engine is always pumped by a thread that is not the test's, which is the
/// arrangement a real client is in.
/// </para>
/// </remarks>
[NonParallelizable]
public class TargetRuntimeTests
{
    [Test]
    public async Task ReplacingTheEngineClearsTheContextsAndAnnouncesTheNextOne()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Runtime.enable", null, attachment);

        var first = await session.EventAsync("Runtime.executionContextCreated");
        first.GetProperty("context").GetProperty("id").GetInt32().Should().Be(1);
        first.GetProperty("context").GetProperty("auxData").GetProperty("isDefault").GetBoolean().Should().BeTrue();
        first.GetProperty("context").GetProperty("auxData").GetProperty("type").GetString().Should().Be("default");
        first.GetProperty("context").GetProperty("auxData").GetProperty("frameId").GetString().Should().Be(target.TargetId);

        await target.NavigateAsync(NewEngine);

        await session.EventAsync("Runtime.executionContextsCleared");

        var second = await session.EventAsync("Runtime.executionContextCreated", index: 1);
        second.GetProperty("context").GetProperty("id").GetInt32().Should().Be(
            2,
            "the counter is the target's, so the second document's default context is never 1 again");
        second.GetProperty("context").GetProperty("auxData").GetProperty("isDefault").GetBoolean().Should().BeTrue();

        // The order is Chrome's: the client is told everything it holds has gone before it is told what it
        // now has.
        var order = session.EventsOf("Runtime.executionContextsCleared").Count;
        order.Should().Be(1);
        Ordinal(session, "Runtime.executionContextsCleared").Should()
            .BeLessThan(Ordinal(session, "Runtime.executionContextCreated", 1), "cleared comes first");
    }

    [Test]
    public async Task ReplacingTheEngineReleasesEveryHandleTheClientHeld()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Runtime.enable", null, attachment);

        var evaluated = await session.ResultAsync(
            "Runtime.evaluate",
            """{"expression":"({ answer: 42 })"}""",
            attachment);

        var objectId = evaluated.GetProperty("result").GetProperty("objectId").GetString();
        objectId.Should().NotBeNullOrEmpty();

        await target.NavigateAsync(NewEngine);

        var error = await session.ErrorAsync(
            "Runtime.getProperties",
            $$"""{"objectId":"{{objectId}}"}""",
            attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be(
            "Could not find object with given id",
            "Chrome's wording, which a client matches on to decide a handle it holds has gone");
    }

    [Test]
    public async Task AContextIdentifierFromBeforeTheNavigationIsRefusedInChromesWording()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Runtime.enable", null, attachment);

        // The identifier the client read off the first executionContextCreated is good while it names the
        // document that minted it.
        await session.ResultAsync("Runtime.evaluate", """{"expression":"1","contextId":1}""", attachment);

        await target.NavigateAsync(NewEngine);

        var error = await session.ErrorAsync("Runtime.evaluate", """{"expression":"1","contextId":1}""", attachment);
        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Cannot find context with specified id");

        // The current one is answered, which is what tells the two failures apart.
        await session.ResultAsync("Runtime.evaluate", """{"expression":"1","contextId":2}""", attachment);
    }

    [Test]
    public async Task ABindingIsReinstalledIntoEveryNewEngine()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Runtime.enable", null, attachment);
        await session.ResultAsync("Runtime.addBinding", """{"name":"report"}""", attachment);

        await session.ResultAsync("Runtime.evaluate", """{"expression":"report('first')"}""", attachment);
        (await session.EventAsync("Runtime.bindingCalled")).GetProperty("payload").GetString().Should().Be("first");

        await target.NavigateAsync(NewEngine);

        // The binding is a global function of an engine that no longer exists, and the client never asked
        // for it again. It is owed to the document that is about to run.
        await session.ResultAsync("Runtime.evaluate", """{"expression":"report('second')"}""", attachment);

        var second = await session.EventAsync("Runtime.bindingCalled", index: 1);
        second.GetProperty("payload").GetString().Should().Be("second");
        second.GetProperty("executionContextId").GetInt32().Should().Be(2);
    }

    [Test]
    public async Task ABreakpointSetByUrlIsResolvedAgainstTheNextDocument()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Runtime.enable", null, attachment);
        await session.ResultAsync("Debugger.enable", null, attachment);

        var set = await session.ResultAsync(
            "Debugger.setBreakpointByUrl",
            """{"url":"app.js","lineNumber":1,"columnNumber":0}""",
            attachment);

        var breakpointId = set.GetProperty("breakpointId").GetString();

        _ = target.PostAsync(engine => engine.Execute(Source, "app.js"));

        var resolved = await session.EventAsync("Debugger.breakpointResolved");
        resolved.GetProperty("breakpointId").GetString().Should().Be(breakpointId);

        await session.EventAsync("Debugger.paused");
        await session.ResultAsync("Debugger.resume", null, attachment);

        await target.NavigateAsync(NewEngine);

        // A new engine, a new DebugHandler and an empty breakpoint collection: the request outlives all
        // three, and the client is told where it landed this time.
        _ = target.PostAsync(engine => engine.Execute(Source, "app.js"));

        var again = await session.EventAsync("Debugger.breakpointResolved", index: 1);
        again.GetProperty("breakpointId").GetString().Should().Be(breakpointId);

        var pausedAgain = await session.EventAsync("Debugger.paused", index: 1);
        pausedAgain.GetProperty("hitBreakpoints").EnumerateArray().Select(id => id.GetString()).Should().Equal(breakpointId);

        await session.ResultAsync("Debugger.resume", null, attachment);
    }

    [Test]
    public async Task AnIsolatedWorldIsAnAliasForTheDocumentsOwnRealm()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);
        var attachment = await session.AttachAsync(target);

        await session.ResultAsync("Runtime.enable", null, attachment);
        await session.ResultAsync("Runtime.evaluate", """{"expression":"var marker = 7"}""", attachment);

        var world = await target.PostAsync(_ => target.CreateWorldContext("utility"));

        var announced = await session.EventAsync("Runtime.executionContextCreated", index: 1);
        announced.GetProperty("context").GetProperty("id").GetInt32().Should().Be(world.Id);
        announced.GetProperty("context").GetProperty("name").GetString().Should().Be("utility");
        announced.GetProperty("context").GetProperty("auxData").GetProperty("isDefault").GetBoolean().Should().BeFalse();
        announced.GetProperty("context").GetProperty("auxData").GetProperty("type").GetString().Should().Be("isolated");

        // The documented divergence: there is one realm per document and a world is a name for it, so the
        // page's own global is visible in it.
        var evaluated = await session.ResultAsync(
            "Runtime.evaluate",
            $$"""{"expression":"marker","contextId":{{world.Id}},"returnByValue":true}""",
            attachment);

        evaluated.GetProperty("result").GetProperty("value").GetInt32().Should().Be(7);

        // The alias lasts as long as the realm it names.
        await target.NavigateAsync(NewEngine);

        var error = await session.ErrorAsync(
            "Runtime.evaluate",
            $$"""{"expression":"1","contextId":{{world.Id}}}""",
            attachment);

        error.GetProperty("message").GetString().Should().Be("Cannot find context with specified id");
    }

    [Test]
    public async Task AHostedTargetCreatedUnderWaitForDebuggerRunsNothingUntilItIsReleased()
    {
        await using var session = ProtocolSession.Create();

        var host = new StubTargetHost();
        session.Server.UseHost(host);

        await session.ResultOfAsync("""{"id":1,"method":"Target.setDiscoverTargets","params":{"discover":true}}""");
        await session.ResultOfAsync("""{"id":2,"method":"Target.setAutoAttach","params":{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}}""");

        var contextId = (await session.ResultOfAsync("""{"id":3,"method":"Target.createBrowserContext","params":{}}"""))
            .GetProperty("browserContextId").GetString();

        contextId.Should().NotBeNullOrEmpty();
        (await session.ResultOfAsync("""{"id":4,"method":"Target.getBrowserContexts"}"""))
            .GetProperty("browserContextIds").EnumerateArray().Select(id => id.GetString()).Should().Equal(contextId);

        await session.ResultOfAsync($$$"""{"id":5,"method":"Target.createTarget","params":{"url":"about:blank","browserContextId":"{{{contextId}}}"}}""");

        var attached = await session.EventAsync("Target.attachedToTarget");
        attached.GetProperty("waitingForDebugger").GetBoolean().Should().BeTrue();
        attached.GetProperty("targetInfo").GetProperty("browserContextId").GetString().Should().Be(contextId);
        attached.GetProperty("targetInfo").GetProperty("type").GetString().Should().Be("page");

        var attachment = attached.GetProperty("sessionId").GetString()!;
        var target = host.Created.Should().ContainSingle().Subject;

        // Host work is held while the wait is on: this is what "runs nothing" means.
        var ran = target.PostAsync(engine => engine.Evaluate("1 + 1").AsNumber());
        (await Task.WhenAny(ran, Task.Delay(200))).Should().NotBeSameAs(ran, "the target is holding everything posted to it");

        await session.ResultAsync("Runtime.runIfWaitingForDebugger", null, attachment);

        (await ran.WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(2);
        target.IsWaitingForDebugger.Should().BeFalse();
    }

    [Test]
    public async Task ATargetThatMovesTellsEveryDiscoveringClient()
    {
        await using var session = ProtocolSession.Create();

        var target = new NavigableTarget(NewEngine());
        session.Server.AddTarget(target);

        await session.ResultOfAsync("""{"id":1,"method":"Target.setDiscoverTargets","params":{"discover":true}}""");

        await target.PostAsync(_ => target.UpdateInfo(title: "Second page", url: "https://example.test/two"));

        var changed = await session.EventAsync("Target.targetInfoChanged");
        changed.GetProperty("targetInfo").GetProperty("title").GetString().Should().Be("Second page");
        changed.GetProperty("targetInfo").GetProperty("url").GetString().Should().Be("https://example.test/two");
    }

    /// <summary>Line 1 is the assignment the breakpoint lands on.</summary>
    private const string Source = """
        function work() {
            var value = 1;
            return value;
        }
        work();
        """;

    private static Engine NewEngine() => new(options => options.UseDevTools());

    /// <summary>Where one event sits among everything the connection has sent, for an ordering assertion.</summary>
    private static int Ordinal(ProtocolSession session, string method, int index = 0)
    {
        var seen = 0;
        for (var i = 0; i < session.Sent.Count; i++)
        {
            using var document = JsonDocument.Parse(session.Sent[i]);
            if (document.RootElement.TryGetProperty("method", out var name) && name.GetString() == method && seen++ == index)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    /// <summary>
    /// The smallest thing that is a host: it mints contexts and targets and remembers what it made.
    /// </summary>
    /// <remarks>
    /// <c>Jint.Browser</c> is the real one — a context is a cookie jar and a storage partition, a target is a
    /// page with a loop of its own. What this pins is the seam rather than the browser: that
    /// <c>Target.createTarget</c> reaches the host at all, that the <c>waitForDebuggerOnStart</c> a session
    /// asked for travels with the request, and that a hosted target is announced and attached like any other.
    /// </remarks>
    private sealed class StubTargetHost : ITargetHost
    {
        private readonly List<string> _contexts = [];

        internal List<NavigableTarget> Created { get; } = [];

        public IReadOnlyList<string> BrowserContextIds
        {
            get
            {
                lock (_contexts)
                {
                    return _contexts.ToArray();
                }
            }
        }

        public ValueTask<string> CreateBrowserContextAsync(CancellationToken cancellationToken)
        {
            var id = "context-" + Guid.NewGuid().ToString("N");
            lock (_contexts)
            {
                _contexts.Add(id);
            }

            return new ValueTask<string>(id);
        }

        public ValueTask DisposeBrowserContextAsync(string browserContextId, CancellationToken cancellationToken)
        {
            lock (_contexts)
            {
                _contexts.Remove(browserContextId);
            }

            return default;
        }

        public ValueTask<DevToolsTarget> CreateTargetAsync(TargetCreationRequest request, CancellationToken cancellationToken)
        {
            var target = new NavigableTarget(
                NewEngine(),
                request.Url ?? "about:blank",
                request.BrowserContextId,
                request.WaitForDebugger);

            Created.Add(target);
            return new ValueTask<DevToolsTarget>(target);
        }

        public ValueTask CloseTargetAsync(DevToolsTarget target, CancellationToken cancellationToken)
            => target.CloseAsync();

        /// <summary>A host with nothing of its own to answer about the browser, which is the ordinary case.</summary>
        public void RegisterBrowserDomains(Jint.DevTools.Session.DevToolsSession session)
        {
        }
    }
}

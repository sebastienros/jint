using System.Text.Json;
using Jint.DevTools;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// The <c>Target</c> state machine, which is what every client walks before it can evaluate anything.
/// </summary>
/// <remarks>
/// Written against the wire rather than against the domain, for the reason the rest of this suite is: what a
/// client acts on is the JSON, and the recorded handshakes in <c>tools/devtools-protocol/handshakes/</c> say
/// exactly which parts of it each one reads.
/// </remarks>
public class TargetSessionTests
{
    [Test]
    public async Task GetTargetsListsEveryPublishedEngine()
    {
        await using var session = ProtocolSession.Create();
        var first = session.AddTarget(new EngineTargetOptions { Title = "first" });
        var second = session.AddTarget(new EngineTargetOptions { Title = "second", Url = "app://second" });

        var reply = await session.SendAsync("Target.getTargets");
        var infos = reply.GetProperty("result").GetProperty("targetInfos").EnumerateArray().ToArray();

        infos.Select(info => info.GetProperty("targetId").GetString()).Should().Equal(first.TargetId, second.TargetId);
        infos.Select(info => info.GetProperty("type").GetString()).Should().AllBe("node");
        infos[1].GetProperty("title").GetString().Should().Be("second");
        infos[1].GetProperty("url").GetString().Should().Be("app://second");
        infos[0].GetProperty("attached").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task GetTargetInfoAnswersOneTargetAndRefusesAnUnknownIdentifier()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();

        var found = await session.SendAsync("Target.getTargetInfo", $$"""{"targetId":"{{target.TargetId}}"}""");
        found.GetProperty("result").GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(target.TargetId);

        var missing = await session.SendAsync("Target.getTargetInfo", """{"targetId":"NOPE"}""");
        missing.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32000);
        missing.GetProperty("error").GetProperty("message").GetString().Should().Be("No target with given id found");
    }

    /// <summary>
    /// <c>targetId</c> is optional, and on the browser session an omitted one names the browser itself.
    /// </summary>
    /// <remarks>
    /// The protocol says "if not specified, returns info about the target the session is attached to", and
    /// Playwright's <c>connectOverCDP</c> sends exactly that — no parameters at all — immediately after
    /// <c>Target.setAutoAttach</c> and before it will do anything else. Answering <c>-32000</c> refused every
    /// Playwright connection at the handshake, which is how this was found.
    /// </remarks>
    [Test]
    public async Task GetTargetInfoWithNoIdentifierAnswersTheBrowserItself()
    {
        await using var session = ProtocolSession.Create();
        session.AddTarget();

        var reply = await session.SendAsync("Target.getTargetInfo", "{}");
        var info = reply.GetProperty("result").GetProperty("targetInfo");

        info.GetProperty("type").GetString().Should().Be("browser");
        info.GetProperty("attached").GetBoolean().Should().BeTrue();
        info.GetProperty("targetId").GetString().Should().NotBeNullOrEmpty();
        info.GetProperty("url").GetString().Should().BeEmpty();
    }

    /// <summary>
    /// Discovery replays what already exists and then reports what arrives, which is what makes it usable by
    /// a client that connected after the targets did.
    /// </summary>
    [Test]
    public async Task SetDiscoverTargetsReplaysAndThenReports()
    {
        await using var session = ProtocolSession.Create();
        var existing = session.AddTarget();

        await session.SendAsync("Target.setDiscoverTargets", """{"discover":true}""");

        var created = session.EventsOf("Target.targetCreated");
        created.Should().HaveCount(1);
        created[0].GetProperty("params").GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(existing.TargetId);

        var later = session.AddTarget();
        session.EventsOf("Target.targetCreated").Should().HaveCount(2);

        session.Server.RemoveTarget(later).Should().BeTrue();
        var destroyed = session.EventsOf("Target.targetDestroyed");
        destroyed.Should().HaveCount(1);
        destroyed[0].GetProperty("params").GetProperty("targetId").GetString().Should().Be(later.TargetId);
    }

    [Test]
    public async Task ATargetIsNotReportedBeforeDiscoveryIsAskedFor()
    {
        await using var session = ProtocolSession.Create();
        session.AddTarget();

        session.EventsOf("Target.targetCreated").Should().BeEmpty("a client is told about targets when it asks to be, and not before");
    }

    /// <summary>
    /// Auto-attach is what Puppeteer uses instead of <c>attachToTarget</c>, and the filter it sends is the
    /// recorded one: exclude pages, include everything else.
    /// </summary>
    [Test]
    public async Task SetAutoAttachAttachesEveryExistingTargetThePuppeteerFilterAdmits()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget(new EngineTargetOptions { WaitForDebuggerOnStart = true });

        var reply = await session.SendAsync(
            "Target.setAutoAttach",
            """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true,"filter":[{"type":"page","exclude":true},{}]}""");

        reply.TryGetProperty("error", out _).Should().BeFalse();

        var attached = session.EventsOf("Target.attachedToTarget");
        attached.Should().HaveCount(1);

        var parameters = attached[0].GetProperty("params");
        parameters.GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(target.TargetId);
        parameters.GetProperty("targetInfo").GetProperty("attached").GetBoolean().Should().BeTrue();
        parameters.GetProperty("waitingForDebugger").GetBoolean().Should().BeTrue();
        parameters.GetProperty("sessionId").GetString().Should().NotBeNullOrEmpty();

        attached[0].TryGetProperty("sessionId", out _).Should().BeFalse("the attachment is announced on the conversation that made it, not inside it");
    }

    [Test]
    public async Task SetAutoAttachAttachesTargetsThatArriveLater()
    {
        await using var session = ProtocolSession.Create();
        await session.SendAsync("Target.setAutoAttach", """{"autoAttach":true,"waitForDebuggerOnStart":false,"flatten":true}""");

        session.EventsOf("Target.attachedToTarget").Should().BeEmpty();

        var target = session.AddTarget();
        var attached = session.EventsOf("Target.attachedToTarget");
        attached.Should().HaveCount(1);
        attached[0].GetProperty("params").GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(target.TargetId);
    }

    /// <summary>
    /// A filter that excludes everything attaches nothing, which is what makes the filter worth honouring
    /// rather than ignoring.
    /// </summary>
    [Test]
    public async Task AFilterThatExcludesTheTypeAttachesNothing()
    {
        await using var session = ProtocolSession.Create();
        session.AddTarget();

        await session.SendAsync(
            "Target.setAutoAttach",
            """{"autoAttach":true,"waitForDebuggerOnStart":false,"flatten":true,"filter":[{"type":"node","exclude":true},{}]}""");

        session.EventsOf("Target.attachedToTarget").Should().BeEmpty();
    }

    [TestCase("Target.setAutoAttach", """{"autoAttach":true,"waitForDebuggerOnStart":false,"flatten":false}""")]
    [TestCase("Target.attachToTarget", """{"targetId":"ANY","flatten":false}""")]
    public async Task TheWrappedSessionModelIsRefused(string method, string parameters)
    {
        await using var session = ProtocolSession.Create();
        var error = (await session.SendAsync(method, parameters)).GetProperty("error");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Only flatten protocol is supported");
    }

    [Test]
    public async Task AttachingTwiceAnswersTheSameSession()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();

        var first = await session.AttachAsync(target);
        var second = await session.AttachAsync(target);

        second.Should().Be(first, "a client that attaches twice has one attachment, and two would double every event");
        session.EventsOf("Target.attachedToTarget").Should().HaveCount(1);
    }

    [Test]
    public async Task DetachingReleasesTheSessionAndAnnouncesIt()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget(new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        var sessionId = await session.AttachAsync(target);

        (await session.SendAsync("Runtime.enable", sessionId: sessionId)).TryGetProperty("error", out _).Should().BeFalse();

        var detached = await session.SendAsync("Target.detachFromTarget", $$"""{"sessionId":"{{sessionId}}"}""");
        detached.TryGetProperty("error", out _).Should().BeFalse();

        var announced = session.EventsOf("Target.detachedFromTarget");
        announced.Should().HaveCount(1);
        announced[0].GetProperty("params").GetProperty("sessionId").GetString().Should().Be(sessionId);
        announced[0].GetProperty("params").GetProperty("targetId").GetString().Should().Be(target.TargetId);

        var after = await session.SendAsync("Runtime.enable", sessionId: sessionId);
        after.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32001);
        after.GetProperty("error").GetProperty("message").GetString().Should().Be("Session with given id not found.");
    }

    [Test]
    public async Task RemovingAnAttachedTargetDetachesFirst()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();
        var sessionId = await session.AttachAsync(target);

        await session.SendAsync("Target.setDiscoverTargets", """{"discover":true}""");
        session.Server.RemoveTarget(target).Should().BeTrue();

        session.EventsOf("Target.detachedFromTarget").Should().HaveCount(1);
        session.EventsOf("Target.targetDestroyed").Should().HaveCount(1);

        var after = await session.SendAsync("Runtime.enable", sessionId: sessionId);
        after.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32001);
    }

    /// <summary>
    /// Clients walk down the target tree by sending <c>setAutoAttach</c> on every session they are handed.
    /// An engine target has no children, so the answer is a success with nothing in it.
    /// </summary>
    [Test]
    public async Task SetAutoAttachOnAnAttachedSessionSucceedsAndAttachesNothing()
    {
        // Library-owned, because a command addressed to an attached session crosses to the engine thread
        // whatever the command is -- there is one rule about where a session's domains run, not one per
        // domain -- so an unpumped host-owned target would answer this only when somebody pumped it.
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget(new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        var sessionId = await session.AttachAsync(target);

        var reply = await session.SendAsync(
            "Target.setAutoAttach",
            """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""",
            sessionId);

        reply.TryGetProperty("error", out _).Should().BeFalse();
        reply.GetProperty("result").GetRawText().Should().Be("{}");
        session.EventsOf("Target.attachedToTarget").Should().HaveCount(1, "the nested call attaches nothing of its own");
    }

    [Test]
    public async Task BrowserContextsAreEmptyAndCannotBeMade()
    {
        await using var session = ProtocolSession.Create();

        var contexts = await session.SendAsync("Target.getBrowserContexts");
        contexts.GetProperty("result").GetProperty("browserContextIds").GetArrayLength().Should().Be(0);

        foreach (var method in new[] { "Target.createBrowserContext", "Target.disposeBrowserContext" })
        {
            var error = (await session.SendAsync(method, """{"browserContextId":"X"}""")).GetProperty("error");
            error.GetProperty("code").GetInt32().Should().Be(-32000);
            error.GetProperty("message").GetString().Should().Be("Browser contexts are not supported");
        }
    }

    [Test]
    public async Task CreateTargetRefusesWithoutAnEngineFactory()
    {
        await using var session = ProtocolSession.Create();
        var error = (await session.SendAsync("Target.createTarget", """{"url":"about:blank"}""")).GetProperty("error");

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("No engine factory is configured");
    }

    [Test]
    public async Task CreateTargetBuildsAndPublishesAnEngineWhenAFactoryIsConfigured()
    {
        await using var session = ProtocolSession.Create(options: new DevToolsServerOptions { EngineFactory = () => new Engine() });

        var created = await session.SendAsync("Target.createTarget", """{"url":"about:blank"}""");
        var targetId = created.GetProperty("result").GetProperty("targetId").GetString();

        session.Server.Targets.Should().ContainSingle(target => target.TargetId == targetId);
        session.Server.Targets[0].ThreadMode.Should().Be(
            ThreadMode.LibraryOwned,
            "no host thread ever agreed to pump a target a client asked for, so the package runs it");

        var closed = await session.SendAsync("Target.closeTarget", $$"""{"targetId":"{{targetId}}"}""");
        closed.GetProperty("result").GetProperty("success").GetBoolean().Should().BeTrue();
        session.Server.Targets.Should().BeEmpty();
    }

    [Test]
    public async Task ActivateTargetSucceedsForAKnownTargetAndRefusesAnUnknownOne()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();

        var activated = await session.SendAsync("Target.activateTarget", $$"""{"targetId":"{{target.TargetId}}"}""");
        activated.GetProperty("result").GetRawText().Should().Be("{}");

        var missing = await session.SendAsync("Target.activateTarget", """{"targetId":"NOPE"}""");
        missing.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32000);
    }

    /// <summary>
    /// The pre-flattened routing command, which stays unimplemented on purpose: no client recorded in the
    /// handshakes sends it, and answering it would mean a second routing path kept alive for nobody.
    /// </summary>
    [Test]
    public async Task SendMessageToTargetIsMethodNotFound()
    {
        await using var session = ProtocolSession.Create();
        var error = (await session.SendAsync("Target.sendMessageToTarget", """{"message":"{}","sessionId":"X"}""")).GetProperty("error");

        error.GetProperty("code").GetInt32().Should().Be(-32601);
        error.GetProperty("message").GetString().Should().Be("'Target.sendMessageToTarget' wasn't found");
    }

    [Test]
    public async Task ADetachedSessionsCommandsAreRefusedButTheConversationSurvives()
    {
        await using var session = ProtocolSession.Create();
        var target = session.AddTarget();
        var sessionId = await session.AttachAsync(target);

        await session.SendAsync("Target.detachFromTarget", $$"""{"sessionId":"{{sessionId}}"}""");

        var stale = await session.SendAsync("Runtime.getIsolateId", sessionId: sessionId);
        stale.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32001);

        var browserLevel = await session.SendAsync("Browser.getVersion");
        browserLevel.GetProperty("result").GetProperty("protocolVersion").GetString().Should().Be("1.3");
    }

    [Test]
    public async Task DetachingASessionThatIsNotThereIsSessionNotFound()
    {
        await using var session = ProtocolSession.Create();
        var error = (await session.SendAsync("Target.detachFromTarget", """{"sessionId":"NOPE"}""")).GetProperty("error");

        error.GetProperty("code").GetInt32().Should().Be(-32001);
    }
}

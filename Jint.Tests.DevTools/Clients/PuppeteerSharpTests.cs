using System.Text.Json;
using Jint.DevTools;
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

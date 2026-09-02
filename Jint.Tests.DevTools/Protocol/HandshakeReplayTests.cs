using System.Text.Json;
using Jint.DevTools;
using Jint.DevTools.Protocol;
using Jint.Tests.DevTools.Session;

namespace Jint.Tests.DevTools.Protocol;

/// <summary>
/// Every method a real client was recorded sending is sent at this server, and what comes back is checked
/// against what the manifest claims.
/// </summary>
/// <remarks>
/// <para>
/// The recordings in <c>tools/devtools-protocol/handshakes/</c> are what Puppeteer for .NET and the DevTools
/// front end actually put on the wire while driving one Chrome through one scenario — not what a
/// compatibility table says, and not what the protocol describes. Replaying them is the only test in this
/// suite that can fail because of something a client does that nobody here thought of.
/// </para>
/// <para>
/// Two properties, and both matter:
/// </para>
/// <list type="bullet">
/// <item>
/// A method the manifest names is <b>answered</b> — never <c>-32601</c>. Two of them answer a stated
/// refusal rather than a value (see <see cref="RefusedByDesign"/>), which is a different thing from not
/// being there: a client is told why.
/// </item>
/// <item>
/// Every other method answers <b>exactly <c>-32601</c></b>. Not <c>-32602</c>, which would tell a client it
/// called a command wrongly when the command does not exist here; and not an exception, which would reach
/// the client as <c>-32000</c> and read as a server that broke rather than one that does not implement it.
/// </item>
/// </list>
/// <para>
/// The second property needs the tolerance to be deliberate rather than blanket, so <see cref="Absent"/>
/// names every recorded method that is expected to answer <c>-32601</c> and says why. A recorded method that
/// is neither implemented nor listed there fails: a new client release, or a re-recording, then arrives as a
/// decision somebody makes rather than as a silently widened test.
/// </para>
/// </remarks>
public class HandshakeReplayTests
{
    /// <summary>
    /// Why each recorded method this server does not implement is expected to answer <c>-32601</c>.
    /// </summary>
    /// <remarks>
    /// Three reasons, and they are not interchangeable. A <b>page</b> entry belongs to a target that has a
    /// document — <c>Jint.Browser</c>, which is AngleSharp plus Jint — and would be wrong to answer here
    /// whatever the implementation state, because an engine target has no page to answer about. A
    /// <b>later</b> entry is engine-level and simply not written yet. A <b>none</b> entry is one Chrome
    /// itself answered <c>-32601</c> to in the very recording.
    /// </remarks>
    private static readonly Dictionary<string, string> Absent = new(StringComparer.Ordinal)
    {
        // Page-level: a document, its frames, its network, its input, its storage. Jint.Browser's, and an
        // engine target has nothing to answer about.
        ["Page.enable"] = "page",
        ["Page.getFrameTree"] = "page",
        ["Page.setLifecycleEventsEnabled"] = "page",
        ["Page.addScriptToEvaluateOnNewDocument"] = "page",
        ["Page.createIsolatedWorld"] = "page",
        ["Page.navigate"] = "page",
        ["Page.getNavigationHistory"] = "page",
        ["Page.navigateToHistoryEntry"] = "page",
        ["Page.captureScreenshot"] = "page",
        ["Page.printToPDF"] = "page",
        ["DOM.describeNode"] = "page",
        ["DOM.resolveNode"] = "page",
        ["Network.enable"] = "page",
        ["Network.setCacheDisabled"] = "page",
        ["Emulation.setDeviceMetricsOverride"] = "page",
        ["Emulation.setTouchEmulationEnabled"] = "page",
        ["Input.dispatchMouseEvent"] = "page",
        ["Input.dispatchKeyEvent"] = "page",
        ["Fetch.enable"] = "page",
        ["Fetch.disable"] = "page",
        ["Fetch.continueRequest"] = "page",
        ["Storage.getCookies"] = "page",
        ["Storage.setCookies"] = "page",
        ["IO.read"] = "page",
        ["IO.close"] = "page",
        ["Audits.enable"] = "page",
        ["Performance.enable"] = "page",
        ["Log.enable"] = "page",

        // Engine-level and not written yet. Each needs the remote-object table, the debugger seam or the
        // profiler seam, and half of any of those is worse than none.
        ["Runtime.getProperties"] = "later",
        ["Runtime.callFunctionOn"] = "later",
        ["Runtime.releaseObject"] = "later",
        ["Runtime.addBinding"] = "later",
        ["Debugger.enable"] = "later",
        ["Debugger.setPauseOnExceptions"] = "later",
        ["Debugger.setAsyncCallStackDepth"] = "later",
        ["Debugger.setBlackboxPatterns"] = "later",
        ["Profiler.enable"] = "later",

        // Chrome answered -32601 to these in the recording itself, so a client that sends one already
        // handles not getting it.
        ["WebMCP.enable"] = "none",
        ["Network.setAttachDebugStack"] = "none",
        ["Network.setBlockedURLs"] = "none",
        ["Network.emulateNetworkConditionsByRule"] = "none",
        ["Network.overrideNetworkState"] = "none",
        ["Network.clearAcceptedEncodingsOverride"] = "none",
    };

    /// <summary>
    /// The two implemented commands whose answer is a refusal with a reason, which is not the same as not
    /// being implemented: a client is told what is missing rather than that the command is unknown.
    /// </summary>
    private static readonly HashSet<string> RefusedByDesign = new(StringComparer.Ordinal)
    {
        "Target.createBrowserContext",
        "Target.disposeBrowserContext",
    };

    /// <summary>
    /// What one replay addresses: the target and attachment under test, plus a spare of each so that the
    /// two destructive commands do not pull the ground out from under the rest of the run.
    /// </summary>
    private sealed record Fixture(string TargetId, string SpareTargetId, string SpareSessionId);

    /// <summary>Parameters for the recorded methods that need them to get past deserialization.</summary>
    /// <remarks>
    /// Only for the implemented ones: an unimplemented command is method-not-found before its parameters are
    /// looked at, which is the very property the second assertion pins.
    /// </remarks>
    private static string? Parameters(string method, Fixture fixture) => method switch
    {
        "Target.getTargetInfo" or "Target.activateTarget" => $$"""{"targetId":"{{fixture.TargetId}}"}""",
        "Target.attachToTarget" => $$"""{"targetId":"{{fixture.TargetId}}","flatten":true}""",
        "Target.closeTarget" => $$"""{"targetId":"{{fixture.SpareTargetId}}"}""",
        "Target.detachFromTarget" => $$"""{"sessionId":"{{fixture.SpareSessionId}}"}""",
        "Target.setAutoAttach" => """{"autoAttach":false,"waitForDebuggerOnStart":false,"flatten":true}""",
        "Target.setDiscoverTargets" => """{"discover":false}""",
        "Target.createTarget" => """{"url":"about:blank"}""",
        "Target.disposeBrowserContext" => """{"browserContextId":"X"}""",
        "Runtime.evaluate" => """{"expression":"1"}""",
        _ => null,
    };

    [TestCase("puppeteersharp-dotnet")]
    [TestCase("devtools-frontend")]
    public async Task EveryRecordedMethodIsEitherAnsweredOrExactlyMethodNotFound(string client)
    {
        var methods = RecordedMethods(client);
        methods.Should().NotBeEmpty("the recording is the manifest these tests are written against");

        await using var session = ProtocolSession.Create(options: new DevToolsServerOptions
        {
            EngineFactory = () => new Engine(),
        });

        var target = session.AddTarget(
            new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned },
            new Engine(options => options.UseDevTools()));

        var attachment = await session.AttachAsync(target);

        var spare = session.AddTarget(new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });
        var fixture = new Fixture(target.TargetId, spare.TargetId, await session.AttachAsync(spare));

        var unexpected = new List<string>();

        foreach (var method in methods)
        {
            var reply = await BestAnswerAsync(session, method, fixture, attachment);
            var implemented = ProtocolManifest.ImplementedMethods.Contains(method, StringComparer.Ordinal);
            var error = reply.TryGetProperty("error", out var value) ? value : (JsonElement?) null;
            var code = error?.GetProperty("code").GetInt32();

            if (implemented)
            {
                if (RefusedByDesign.Contains(method))
                {
                    code.Should().Be(-32000, "'{0}' is implemented and refuses with a reason", method);
                    continue;
                }

                code.Should().BeNull("'{0}' is in the manifest, so a client that sends it is answered", method);
                continue;
            }

            code.Should().Be(-32601, "'{0}' is not implemented, and every other answer misleads the client", method);
            error!.Value.GetProperty("message").GetString().Should().Be($"'{method}' wasn't found");

            if (!Absent.ContainsKey(method))
            {
                unexpected.Add(method);
            }
        }

        Assert.That(
            unexpected.Count == 0,
            $"""
            {unexpected.Count} method(s) the '{client}' recording sends answer -32601 and are not accounted
            for in HandshakeReplayTests.Absent. Add each with the reason it is absent — a page-level command
            that belongs to Jint.Browser, an engine-level one that is not written yet, or one Chrome itself
            answered -32601 to — so that the tolerance stays a decision rather than a blanket:

            {string.Join(Environment.NewLine, unexpected.Select(method => "  " + method))}
            """);
    }

    /// <summary>
    /// The recording and the manifest agree about what a client that only ever spoke to an engine target
    /// would find missing.
    /// </summary>
    [Test]
    public void NothingIsExcusedThatIsAlreadyImplemented()
    {
        var stale = Absent.Keys.Where(method => ProtocolManifest.ImplementedMethods.Contains(method, StringComparer.Ordinal)).ToArray();

        Assert.That(
            stale.Length == 0,
            $"""
            {stale.Length} method(s) are listed in HandshakeReplayTests.Absent and are now implemented, so
            the excuse is stale and the list no longer says anything:

            {string.Join(Environment.NewLine, stale.Select(method => "  " + method))}
            """);
    }

    /// <summary>
    /// Sends <paramref name="method"/> where it belongs: the browser conversation first, and the attachment
    /// when the conversation does not carry that domain.
    /// </summary>
    /// <remarks>
    /// A real client knows which session each command belongs on, because the protocol tells it. A replay
    /// does not, so it tries both and keeps the better answer — which is exactly the question being asked:
    /// is this method reachable at all?
    /// </remarks>
    private static async Task<JsonElement> BestAnswerAsync(ProtocolSession session, string method, Fixture fixture, string attachment)
    {
        var parameters = Parameters(method, fixture);

        var onBrowser = await session.SendAsync(method, parameters).ConfigureAwait(false);
        if (!onBrowser.TryGetProperty("error", out _))
        {
            return onBrowser;
        }

        var onAttachment = await session.SendAsync(method, parameters, attachment).ConfigureAwait(false);
        return onAttachment.TryGetProperty("error", out _) ? onBrowser : onAttachment;
    }

    private static IReadOnlyList<string> RecordedMethods(string client)
    {
        var path = Path.Combine(RepositoryPaths.ProtocolDirectory, "handshakes", client + ".json");
        File.Exists(path).Should().BeTrue("the recorded handshakes are checked in at {0}", path);

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("allMethods").EnumerateArray().Select(method => method.GetString()!).ToArray();
    }
}

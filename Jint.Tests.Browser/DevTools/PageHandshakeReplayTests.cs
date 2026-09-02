using System.Text.Json;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// Every command a real client sends before its first script result, replayed against a real page.
/// </summary>
/// <remarks>
/// <para>
/// The recordings in <c>tools/devtools-protocol/handshakes/</c> are what Puppeteer, PuppeteerSharp,
/// Playwright and Playwright for .NET actually put on the wire while driving one Chrome through one scenario.
/// <c>matrix.md</c>'s "minimum must-answer set" is the first five steps of it — <c>connect</c>,
/// <c>newContext</c>, <c>newPage</c>, <c>goto</c>, the two evaluations — and this replays exactly those,
/// in the order each client sent them, on the session each belongs on.
/// </para>
/// <para>
/// <b>Two properties, and the second is the interesting one.</b> Nothing may answer <c>-32601</c> or
/// <c>-32602</c> except the methods <see cref="Absent"/> names with a reason; and the events the client then
/// waited on — <c>attachedToTarget</c>, <c>executionContextCreated</c>, <c>frameNavigated</c>,
/// <c>lifecycleEvent</c> for <c>load</c>, <c>loadEventFired</c> — have to have arrived, on the attachment
/// rather than on the browser conversation, in the recorded relative order.
/// </para>
/// <para>
/// A replay is not a client: it does not know a command's parameters, so <see cref="Parameters"/> supplies
/// the ones a command cannot be answered without and the rest go out empty. What it does know is what a real
/// client sent and in what order, which is the thing no compatibility table can tell you.
/// </para>
/// </remarks>
[NonParallelizable]
public class PageHandshakeReplayTests
{
    /// <summary>
    /// Why each recorded method this server does not answer is expected to fail, and how.
    /// </summary>
    /// <remarks>
    /// Every one of these is a campaign item rather than a decision, and each names it. <c>WebMCP.enable</c>
    /// is the exception: Chrome itself answered <c>-32601</c> to it in the very recording, so a client that
    /// sends it already handles not getting it.
    /// </remarks>
    private static readonly Dictionary<string, string> Absent = new(StringComparer.Ordinal)
    {
        ["WebMCP.enable"] = "Chrome answered -32601 in the recording itself",
        ["Storage.getCookies"] = "the Storage domain arrives with the network work",
        ["Storage.setCookies"] = "the Storage domain arrives with the network work",
        ["Fetch.continueRequest"] = "interception arrives with the network work",
    };

    /// <summary>The steps of the scenario that come before a client's first script result.</summary>
    private static readonly string[] MustAnswerSteps = ["connect", "newContext", "newPage", "goto", "evaluateTitle", "evaluateObject"];

    [TestCase("puppeteer-node")]
    [TestCase("puppeteersharp-dotnet")]
    [TestCase("playwright-node")]
    [TestCase("playwright-dotnet")]
    public async Task EveryMethodAClientSendsBeforeItsFirstScriptResultIsAnswered(string client)
    {
        var methods = RecordedMethods(client);
        methods.Should().NotBeEmpty("the recording is the specification these tests are written against");

        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Handshake</title></head><body><p>ready</p></body></html>");

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns });

        // The two commands the replay cannot make up, because everything after them addresses what they
        // answered: the context a page is created in, and the attachment every page-level command rides.
        var contextId = (await session.ResultAsync("Target.createBrowserContext", "{}"))
            .GetProperty("browserContextId").GetString()!;

        await session.ResultAsync("Target.setAutoAttach", """{"autoAttach":true,"waitForDebuggerOnStart":true,"flatten":true}""");
        await session.ResultAsync("Target.setDiscoverTargets", """{"discover":true}""");

        var targetId = (await session.ResultAsync("Target.createTarget", $$"""{"url":"about:blank","browserContextId":"{{contextId}}"}"""))
            .GetProperty("targetId").GetString()!;

        var attached = await session.EventAsync("Target.attachedToTarget");
        attached.GetProperty("targetInfo").GetProperty("targetId").GetString().Should().Be(targetId);

        var attachment = attached.GetProperty("sessionId").GetString()!;
        var refused = new List<string>();
        string? handle = null;

        foreach (var method in methods)
        {
            // The one command that cannot be sent without something the server minted moments earlier: a
            // client calls a function *on* a handle it was just given, so the replay takes one at the point
            // in the sequence the client would have had one.
            if (method is "Runtime.callFunctionOn" or "Runtime.getProperties" or "Runtime.releaseObject")
            {
                handle = await HandleAsync(session, attachment);
            }

            var reply = await BestAnswerAsync(session, method, attachment, targetId, contextId, server.Url("/page"), handle);
            if (!reply.TryGetProperty("error", out var error))
            {
                Absent.Should().NotContainKey(method, "'{0}' is answered now, so the reason it is excused is stale", method);
                continue;
            }

            var code = error.GetProperty("code").GetInt32();
            if (code != -32601 && code != -32602)
            {
                // A -32000 is a refusal with a reason, which is a different thing from not being there:
                // captureScreenshot and printToPDF are the two, and both say why in their message.
                continue;
            }

            if (!Absent.ContainsKey(method))
            {
                refused.Add($"{method} -> {code}: {error.GetProperty("message").GetString()}");
            }
        }

        Assert.That(
            refused.Count == 0,
            $"""
            {refused.Count} method(s) the '{client}' recording sends before its first script result are not
            answered. Every one of them is a command that client needs to get as far as evaluating anything,
            so each is either implemented or accounted for in PageHandshakeReplayTests.Absent with the reason:

            {string.Join(Environment.NewLine, refused.Select(entry => "  " + entry))}
            """);

        // And the events the client then waits on, on the attachment, in the recorded relative order.
        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        session.EventsOf("Runtime.executionContextCreated", attachment).Should().NotBeEmpty();
        session.EventsOf("Page.frameNavigated", attachment).Should().NotBeEmpty();

        session.EventsOf("Page.lifecycleEvent", attachment)
            .Select(e => e.GetProperty("params").GetProperty("name").GetString())
            .Should().Contain("load");

        session.Ordinal("Target.attachedToTarget").Should().BeLessThan(session.Ordinal("Page.frameNavigated"));
        session.Ordinal("Page.frameNavigated").Should().BeLessThan(session.Ordinal("Page.loadEventFired"));
    }

    /// <summary>
    /// Sends one method where it belongs: the browser conversation first, and the attachment when the
    /// conversation does not carry that domain.
    /// </summary>
    /// <remarks>
    /// A real client knows which session each command belongs on because the protocol tells it; a replay does
    /// not, so it tries both and keeps the better answer — which is exactly the question being asked: is this
    /// method reachable at all?
    /// </remarks>
    private static async Task<JsonElement> BestAnswerAsync(
        PageSession session,
        string method,
        string attachment,
        string targetId,
        string contextId,
        string url,
        string? handle)
    {
        var parameters = Parameters(method, targetId, contextId, url, handle);

        var onAttachment = await session.SendAsync(method, parameters, attachment).ConfigureAwait(false);
        if (!onAttachment.TryGetProperty("error", out _))
        {
            return onAttachment;
        }

        var onBrowser = await session.SendAsync(method, parameters).ConfigureAwait(false);
        return onBrowser.TryGetProperty("error", out _) ? onAttachment : onBrowser;
    }

    /// <summary>Evaluates one object on the attachment and hands back the handle the server minted for it.</summary>
    private static async Task<string> HandleAsync(PageSession session, string attachment)
    {
        var result = await session.ResultAsync(
            "Runtime.evaluate",
            """{"expression":"({ answer: 42 })"}""",
            attachment).ConfigureAwait(false);

        return result.GetProperty("result").GetProperty("objectId").GetString()!;
    }

    /// <summary>The parameters a command cannot be answered without.</summary>
    private static string? Parameters(string method, string targetId, string contextId, string url, string? handle) => method switch
    {
        "Runtime.callFunctionOn" => $$"""{"functionDeclaration":"function () { return this.answer; }","objectId":"{{handle}}","returnByValue":true}""",
        "Runtime.getProperties" => $$"""{"objectId":"{{handle}}"}""",
        "Runtime.releaseObject" => $$"""{"objectId":"{{handle}}"}""",
        "Page.navigate" => $$"""{"url":"{{url}}"}""",
        "Page.setLifecycleEventsEnabled" => """{"enabled":true}""",
        "Page.addScriptToEvaluateOnNewDocument" => """{"source":"void 0"}""",
        "Page.createIsolatedWorld" => $$"""{"frameId":"{{targetId}}","worldName":"utility"}""",
        "Page.setFontFamilies" => """{"fontFamilies":{}}""",
        "Page.setBypassCSP" => """{"enabled":true}""",
        "Page.setDocumentContent" => $$"""{"frameId":"{{targetId}}","html":"<html></html>"}""",
        "Page.handleJavaScriptDialog" => """{"accept":true}""",
        "Page.navigateToHistoryEntry" => """{"entryId":0}""",
        "Emulation.setDeviceMetricsOverride" => """{"width":800,"height":600,"deviceScaleFactor":1,"mobile":false}""",
        "Emulation.setTouchEmulationEnabled" => """{"enabled":true}""",
        "Emulation.setFocusEmulationEnabled" => """{"enabled":true}""",
        "Emulation.setUserAgentOverride" => """{"userAgent":"replay"}""",
        "Emulation.setEmulatedMedia" => """{"media":"screen"}""",
        "Network.setCacheDisabled" => """{"cacheDisabled":true}""",
        "Network.setExtraHTTPHeaders" => """{"headers":{}}""",
        "Browser.setDownloadBehavior" => """{"behavior":"deny"}""",
        "Browser.setWindowBounds" => """{"windowId":1,"bounds":{"width":800,"height":600}}""",
        "Browser.getWindowForTarget" => $$"""{"targetId":"{{targetId}}"}""",
        "Target.setAutoAttach" => """{"autoAttach":false,"waitForDebuggerOnStart":false,"flatten":true}""",
        "Target.setDiscoverTargets" => """{"discover":true}""",
        "Target.getTargetInfo" => $$"""{"targetId":"{{targetId}}"}""",
        "Target.attachToTarget" => $$"""{"targetId":"{{targetId}}","flatten":true}""",
        "Runtime.evaluate" => """{"expression":"document.title","returnByValue":true}""",
        "Runtime.addBinding" => """{"name":"__jintHandshakeBinding"}""",
        _ => null,
    };

    /// <summary>
    /// The methods one client sent in the steps that come before its first script result.
    /// </summary>
    /// <remarks>
    /// Taken from the recording's own per-step breakdown rather than from its whole method list, because the
    /// steps after these — <c>$</c>, <c>click</c>, <c>screenshot</c> — need the domains later campaign items
    /// bring, and a replay that included them would be asserting against work nobody has claimed is done.
    /// </remarks>
    private static IReadOnlyList<string> RecordedMethods(string client)
    {
        var path = Path.Combine(RepositoryPaths.Root, "tools", "devtools-protocol", "handshakes", client + ".json");
        File.Exists(path).Should().BeTrue("the recorded handshakes are checked in at {0}", path);

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var methods = new List<string>();

        foreach (var step in document.RootElement.GetProperty("scenarioSteps").EnumerateArray())
        {
            if (!Array.Exists(MustAnswerSteps, name => name == step.GetProperty("step").GetString()))
            {
                continue;
            }

            if (!step.TryGetProperty("methods", out var recorded))
            {
                continue;
            }

            foreach (var entry in recorded.EnumerateArray())
            {
                var method = entry.GetProperty("method").GetString()!;

                // The two the replay itself already sent, and which would take the ground out from under it:
                // a second createTarget is a second page, and a second createBrowserContext a second context.
                if (method is "Target.createTarget" or "Target.createBrowserContext" or "Target.closeTarget" or "Target.detachFromTarget")
                {
                    continue;
                }

                if (!methods.Contains(method, StringComparer.Ordinal))
                {
                    methods.Add(method);
                }
            }
        }

        return methods;
    }
}

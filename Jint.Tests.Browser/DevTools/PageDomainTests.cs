using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// What a client is told and answered while it drives a page over the protocol.
/// </summary>
/// <remarks>
/// <para>
/// Everything here runs against a real page — its own thread, its own engine, its own document — published on
/// a real server that simply never opened a socket. A command that touches the DOM crosses to the page loop
/// exactly as it would over a WebSocket, and a lifecycle event is raised by the same navigation a host's own
/// <c>NavigateAsync</c> raises.
/// </para>
/// <para>
/// Every wait is bounded and every one of these tests closes its browser, which is what stops a page thread
/// outliving the test that started it.
/// </para>
/// </remarks>
[NonParallelizable]
public class PageDomainTests
{
    [Test]
    public async Task ANavigationEmitsChromesEventsInChromesOrder()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><head><title>One</title></head><body><p>one</p></body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/one")}}"}""", attachment);

        await session.EventAsync("Page.frameStoppedLoading", sessionId: attachment);

        // The order the recordings show, which is what a client's own bookkeeping is written against.
        session.Ordinal("Page.frameStartedNavigating").Should().BeLessThan(session.Ordinal("Page.frameStartedLoading"));
        session.Ordinal("Page.frameStartedLoading").Should().BeLessThan(session.Ordinal("Page.frameNavigated"));
        session.Ordinal("Page.frameNavigated").Should().BeLessThan(session.Ordinal("Runtime.executionContextsCleared"));
        session.Ordinal("Runtime.executionContextsCleared").Should().BeLessThan(session.Ordinal("Page.domContentEventFired"));
        session.Ordinal("Page.domContentEventFired").Should().BeLessThan(session.Ordinal("Page.loadEventFired"));
        session.Ordinal("Page.loadEventFired").Should().BeLessThan(session.Ordinal("Page.frameStoppedLoading"));

        var navigated = await session.EventAsync("Page.frameNavigated", sessionId: attachment);
        navigated.GetProperty("frame").GetProperty("url").GetString().Should().Be(server.Url("/one"));
        navigated.GetProperty("type").GetString().Should().Be("Navigation");

        // Every lifecycle event of one document carries the loader identifier that document was given.
        var lifecycle = session.EventsOf("Page.lifecycleEvent", attachment)
            .Select(e => e.GetProperty("params"))
            .ToArray();

        var names = lifecycle.Select(p => p.GetProperty("name").GetString()).ToArray();
        names.Should().ContainInOrder("init", "commit", "DOMContentLoaded", "load");

        var loaderIds = lifecycle
            .Where(p => p.GetProperty("name").GetString() != "init")
            .Select(p => p.GetProperty("loaderId").GetString())
            .Distinct()
            .ToArray();

        loaderIds.Should().HaveCount(1, "every signal of one document carries one loader identifier");
    }

    [Test]
    public async Task NavigateAnswersAtTheCommitWithTheFrameAndTheLoader()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><body>one</body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        var result = await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/one")}}"}""", attachment);

        result.GetProperty("frameId").GetString().Should().Be(target.TargetId, "the page's main frame is named by its target");
        result.GetProperty("loaderId").GetString().Should().NotBeNullOrEmpty();
        result.TryGetProperty("errorText", out _).Should().BeFalse("the navigation produced a document");
    }

    [Test]
    public async Task ANavigationThatProducesNothingAnswersANetworkErrorString()
    {
        await using var session = await PageSession.CreateAsync(new BrowserContextOptions
        {
            // Everything is refused, which is the one failure a test can produce without depending on what
            // the machine's resolver does with a name nobody registered.
            UrlFilter = _ => false,
        });

        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        var result = await session.ResultAsync("Page.navigate", """{"url":"http://127.0.0.1:1/blocked"}""", attachment);
        result.GetProperty("errorText").GetString().Should().Be("net::ERR_BLOCKED_BY_CLIENT");
        result.GetProperty("frameId").GetString().Should().NotBeNullOrEmpty("a failed navigation still names the frame it was aimed at");
    }

    [Test]
    public async Task AScriptToEvaluateOnNewDocumentRunsBeforeTheDocumentsOwn()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><body><script>window.order = (window.order || '') + 'document';</script></body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        await session.ResultAsync(
            "Page.addScriptToEvaluateOnNewDocument",
            """{"source":"window.order = (window.order || '') + 'instrumentation:';"}""",
            attachment);

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/one")}}"}""", attachment);
        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        var order = await session.EvaluateAsync("window.order", attachment);
        order.GetProperty("value").GetString().Should().Be(
            "instrumentation:document",
            "a client's instrumentation is owed the document before the document's own script runs");
    }

    [Test]
    public async Task AnExecutionContextIdentifierFromBeforeTheNavigationIsRefused()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><body>one</body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        var first = await session.EventAsync("Runtime.executionContextCreated", sessionId: attachment);
        var stale = first.GetProperty("context").GetProperty("id").GetInt32();

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/one")}}"}""", attachment);
        await session.EventAsync("Runtime.executionContextsCleared", sessionId: attachment);

        var error = await session.ErrorAsync(
            "Runtime.evaluate",
            $$"""{"expression":"1","contextId":{{stale}}}""",
            attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Cannot find context with specified id");
    }

    [Test]
    public async Task SettingTheDeviceMetricsChangesWhatThePageBelievesItsWindowIs()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        (await session.EvaluateAsync("window.innerWidth", attachment)).GetProperty("value").GetInt32().Should().Be(1280);

        await session.ResultAsync(
            "Emulation.setDeviceMetricsOverride",
            """{"width":390,"height":844,"deviceScaleFactor":3,"mobile":true}""",
            attachment);

        (await session.EvaluateAsync("window.innerWidth", attachment)).GetProperty("value").GetInt32().Should().Be(390);
        (await session.EvaluateAsync("window.innerHeight", attachment)).GetProperty("value").GetInt32().Should().Be(844);
        (await session.EvaluateAsync("window.devicePixelRatio", attachment)).GetProperty("value").GetDouble().Should().Be(3);
        (await session.EvaluateAsync("matchMedia('(max-width: 500px)').matches", attachment)).GetProperty("value").GetBoolean().Should().BeTrue();

        // And the layout metrics answer from the same place, because there is no layout to answer from.
        var metrics = await session.ResultAsync("Page.getLayoutMetrics", null, attachment);
        metrics.GetProperty("cssContentSize").GetProperty("width").GetDouble().Should().Be(390);

        await session.ResultAsync("Emulation.clearDeviceMetricsOverride", null, attachment);
        (await session.EvaluateAsync("window.innerWidth", attachment)).GetProperty("value").GetInt32().Should().Be(1280);
    }

    [Test]
    public async Task TheNavigationHistoryIsWhatTheClientTravelsBackThrough()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><body>one</body></html>");
        server.MapHtml("/two", "<html><body>two</body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await page.NavigateAsync(server.Url("/one"));
        await page.NavigateAsync(server.Url("/two"));

        var history = await session.ResultAsync("Page.getNavigationHistory", null, attachment);
        var entries = history.GetProperty("entries").EnumerateArray().ToArray();

        // The initial about:blank is replaced by the first navigation rather than pushed past, which is
        // HTML's own rule and what makes history.length count what a browser counts.
        entries.Select(entry => entry.GetProperty("url").GetString())
            .Should().Equal([server.Url("/one"), server.Url("/two")]);

        history.GetProperty("currentIndex").GetInt32().Should().Be(1);

        var navigated = page.WaitForNavigationAsync(TimeSpan.FromSeconds(30));
        await session.ResultAsync("Page.navigateToHistoryEntry", """{"entryId":0}""", attachment);
        (await navigated).Should().BeTrue();

        page.Url.Should().Be(server.Url("/one"));

        var after = await session.ResultAsync("Page.getNavigationHistory", null, attachment);
        after.GetProperty("currentIndex").GetInt32().Should().Be(0);
    }

    [Test]
    public async Task TheFrameTreeIsOneFrameAndItIsTheTargets()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><head><title>One</title></head><body><iframe src='/two'></iframe></body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await page.NavigateAsync(server.Url("/one"));

        var tree = await session.ResultAsync("Page.getFrameTree", null, attachment);
        var frame = tree.GetProperty("frameTree").GetProperty("frame");

        frame.GetProperty("id").GetString().Should().Be(target.TargetId);
        frame.GetProperty("url").GetString().Should().Be(server.Url("/one"));
        frame.GetProperty("mimeType").GetString().Should().Be("text/html");
        frame.GetProperty("securityOrigin").GetString().Should().Be(server.Origin);

        tree.GetProperty("frameTree").TryGetProperty("childFrames", out _).Should()
            .BeFalse("an iframe is parsed and runs no script, so a client is not told about a frame it could never evaluate in");
    }

    [Test]
    public async Task ScreenshotsAndPdfsAreRefusedWithASentenceThatSaysWhy()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        foreach (var command in new[] { "Page.captureScreenshot", "Page.printToPDF" })
        {
            var error = await session.ErrorAsync(command, "{}", attachment);
            error.GetProperty("code").GetInt32().Should().Be(-32000);
            error.GetProperty("message").GetString().Should().Contain("renders no pixels");
            error.GetProperty("message").GetString().Should().Contain("Jint.getMarkdown");
        }
    }

    [Test]
    public async Task AnIsolatedWorldIsMintedAndEvaluatesInThePage()
    {
        await using var session = await PageSession.CreateAsync();
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await session.EvaluateAsync("window.marker = 11", attachment);

        var world = await session.ResultAsync(
            "Page.createIsolatedWorld",
            $$"""{"frameId":"{{target.TargetId}}","worldName":"utility"}""",
            attachment);

        var contextId = world.GetProperty("executionContextId").GetInt32();

        var evaluated = await session.ResultAsync(
            "Runtime.evaluate",
            $$"""{"expression":"window.marker","contextId":{{contextId}},"returnByValue":true}""",
            attachment);

        evaluated.GetProperty("result").GetProperty("value").GetInt32().Should().Be(
            11,
            "a world here is a second name for the document's own realm, which is the documented divergence");
    }

    /// <summary>
    /// A named world is made again over every document, which is what Chrome does and what every client
    /// relies on: Puppeteer and Playwright each create one utility world when they attach and then use it for
    /// the life of the page, so a world that ended with the first document leaves <c>$</c> and
    /// <c>waitForSelector</c> waiting for a context that never arrives.
    /// </summary>
    [Test]
    public async Task ANamedIsolatedWorldIsMadeAgainOverTheNextDocument()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><body>one</body></html>");
        server.MapHtml("/two", "<html><body>two</body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/one")}}"}""", attachment);
        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        var first = (await session.ResultAsync(
            "Page.createIsolatedWorld",
            $$"""{"frameId":"{{target.TargetId}}","worldName":"utility"}""",
            attachment)).GetProperty("executionContextId").GetInt32();

        // Asking twice for one name answers the world that exists rather than a second identifier over the
        // same realm, which is Chrome's behaviour and what stops a client accumulating one per attempt.
        (await session.ResultAsync(
            "Page.createIsolatedWorld",
            $$"""{"frameId":"{{target.TargetId}}","worldName":"utility"}""",
            attachment)).GetProperty("executionContextId").GetInt32().Should().Be(first);

        await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/two")}}"}""", attachment);
        await session.EventAsync("Page.loadEventFired", 1, attachment);

        var announced = session.EventsOf("Runtime.executionContextCreated", attachment)
            .Select(entry => entry.GetProperty("params").GetProperty("context"))
            .Where(context => context.GetProperty("name").GetString() == "utility")
            .ToArray();

        announced.Should().HaveCount(2, "the world is announced once per document, under the name that minted it");

        var second = announced[^1].GetProperty("id").GetInt32();
        second.Should().NotBe(first, "the realm it names is the new document's, so the identifier is a new one");

        // The identifier from the document before is refused rather than resolved against the one that
        // replaced it, and the new one evaluates in the page.
        var stale = await session.ErrorAsync(
            "Runtime.evaluate",
            $$"""{"expression":"1","contextId":{{first}}}""",
            attachment);

        stale.GetProperty("message").GetString().Should().Be("Cannot find context with specified id");

        (await session.ResultAsync(
            "Runtime.evaluate",
            $$"""{"expression":"document.body.textContent","contextId":{{second}},"returnByValue":true}""",
            attachment)).GetProperty("result").GetProperty("value").GetString().Should().Be("two");
    }

    [Test]
    public async Task APushStateIsReportedAsANavigationWithinTheDocument()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/one", "<html><body>one</body></html>");

        await using var session = await PageSession.CreateAsync(Options(server));

        // Discovery is what makes targetInfoChanged reach a client at all, and it is the first thing every
        // recorded client turns on.
        await session.ResultAsync("Target.setDiscoverTargets", """{"discover":true}""");

        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await page.NavigateAsync(server.Url("/one"));
        await session.EvaluateAsync("history.pushState({}, '', '/one/deeper')", attachment);

        var moved = await session.EventAsync("Page.navigatedWithinDocument", sessionId: attachment);
        moved.GetProperty("url").GetString().Should().Be(server.Url("/one/deeper"));
        moved.GetProperty("frameId").GetString().Should().Be(target.TargetId);

        // And the target's own information moves with it, which is what a client reads page.url() from.
        var changed = session.EventsOf("Target.targetInfoChanged");
        changed.Should().NotBeEmpty();
    }

    [Test]
    public async Task SetDocumentContentReplacesTheDocument()
    {
        await using var session = await PageSession.CreateAsync();
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await session.ResultAsync(
            "Page.setDocumentContent",
            $$"""{"frameId":"{{target.TargetId}}","html":"<html><head><title>Set</title></head><body><p id='x'>hello</p></body></html>"}""",
            attachment);

        var text = await session.EvaluateAsync("document.getElementById('x').textContent", attachment);
        text.GetProperty("value").GetString().Should().Be("hello");
    }

    [Test]
    public async Task ADialogIsReportedAndAnsweredFromTheStandingDecision()
    {
        await using var session = await PageSession.CreateAsync();
        var attachment = await session.OpenPageAsync();
        await session.EnablePageAsync(attachment);

        // The decision travels ahead of the dialog, because a dialog here does not wait for one.
        await session.ResultAsync("Page.handleJavaScriptDialog", """{"accept":true,"promptText":"answered"}""", attachment);

        var answered = await session.EvaluateAsync("prompt('who?', 'nobody')", attachment);
        answered.GetProperty("value").GetString().Should().Be("answered");

        var opening = await session.EventAsync("Page.javascriptDialogOpening", sessionId: attachment);
        opening.GetProperty("type").GetString().Should().Be("prompt");
        opening.GetProperty("message").GetString().Should().Be("who?");
        opening.GetProperty("defaultPrompt").GetString().Should().Be("nobody");

        var closed = await session.EventAsync("Page.javascriptDialogClosed", sessionId: attachment);
        closed.GetProperty("result").GetBoolean().Should().BeTrue();
        closed.GetProperty("userInput").GetString().Should().Be("answered");
    }

    [Test]
    public async Task ARunawayScriptInANavigatedDocumentIsBoundedAndThePageStaysDrivable()
    {
        using var server = new LoopbackServer();
        server.MapHtml(
            "/spin",
            "<html><body><p id='before'>seen</p><script>while (true) { }</script>"
            + "<p id='after'>also seen</p></body></html>");

        // The page's own turn budget, which is what bounds a script the protocol asked for exactly as it
        // bounds one a host asked for: a command that runs script crosses to the loop through the target's
        // mailbox, so it runs inside the bracket rather than beside it.
        await using var session = await PageSession.CreateAsync(
            Options(server),
            new BrowserOptions { MaxTaskDuration = TimeSpan.FromMilliseconds(250) });

        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        var result = await session.ResultAsync("Page.navigate", $$"""{"url":"{{server.Url("/spin")}}"}""", attachment);
        result.TryGetProperty("errorText", out _).Should().BeFalse("the script ran out of time; the navigation did not fail");

        await session.EventAsync("Page.loadEventFired", sessionId: attachment);

        page.Errors.Should().ContainSingle().Which.Kind.Should().Be(PageErrorKind.BudgetExceeded);

        // Still drivable, over the protocol, in the document the runaway script was in.
        var after = await session.EvaluateAsync("document.getElementById('after').textContent", attachment);
        after.GetProperty("value").GetString().Should().Be("also seen");

        (await session.EvaluateAsync("1 + 1", attachment)).GetProperty("value").GetInt32().Should().Be(2);
    }

    /// <summary>
    /// A title a script sets <b>after</b> <c>load</c> reaches the client, with no navigation to carry it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The page used to report its title only at the three navigation phases, and
    /// <c>Page.Navigation.cs</c>'s own remark admitted the gap — "a script that rewrites it does so before
    /// <c>load</c> far more often than after". Far more often is not always: a single-page application that
    /// sets <c>document.title</c> from its router does it after <c>load</c> every time, and until the page
    /// looked at the title at the end of each of its loop's turns, a client was told the old one until the
    /// next navigation.
    /// </para>
    /// <para>
    /// <c>Target.targetInfoChanged</c> is what a client reads <c>page.title()</c> from, and it needs
    /// <c>Target.setDiscoverTargets</c> — the first thing every recorded client turns on — to reach one at
    /// all. The event is deduped by the target, so what is asserted here is that one arrives carrying the new
    /// title, while the page is still showing the document it loaded.
    /// </para>
    /// </remarks>
    [Test]
    public async Task ATitleSetAfterLoadReachesTheClientWithoutANavigation()
    {
        using var server = new LoopbackServer();
        server.MapHtml(
            "/one",
            """
            <html><head><title>Before load</title></head><body>
            <script>addEventListener('load', () => setTimeout(() => { document.title = 'After load' }, 5))</script>
            </body></html>
            """);

        await using var session = await PageSession.CreateAsync(Options(server));

        // Discovery is what makes targetInfoChanged reach a client at all, and it is the first thing every
        // recorded client turns on.
        await session.ResultAsync("Target.setDiscoverTargets", """{"discover":true}""");

        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);
        await session.EnablePageAsync(attachment);

        await page.NavigateAsync(server.Url("/one"));

        // Nothing else is driven from here: the page's own loop turns, the timer fires in one of its drains,
        // and the end of that turn is what tells the target.
        var deadline = Environment.TickCount64 + 30_000L;
        string? announced = null;

        while (Environment.TickCount64 < deadline && announced is null)
        {
            announced = session.EventsOf("Target.targetInfoChanged")
                .Select(message => message.GetProperty("params").GetProperty("targetInfo").GetProperty("title").GetString())
                .LastOrDefault(title => string.Equals(title, "After load", StringComparison.Ordinal));

            if (announced is null)
            {
                await Task.Delay(20);
            }
        }

        announced.Should().Be("After load", "a watched page looks at its title at the end of every turn");

        // And it got there without a navigation: the document, and the loader identifier naming it, are the
        // ones the one navigation of this test produced.
        page.Url.Should().Be(server.Url("/one"));
        (await page.TitleAsync()).Should().Be("After load");
        page.Errors.Should().BeEmpty();
    }

    private static BrowserContextOptions Options(LoopbackServer server) => new() { UrlFilter = server.Owns };
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>Fetch</c> domain over a real page: a client is shown a request and answers it three ways.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interesting property is that the page keeps running while a request is paused.</b> The pause holds
/// the transport thread the request is being sent on, never the page loop — so the very commands that answer
/// it are answerable, which is what every one of these tests depends on and what a design that paused on the
/// loop would deadlock.
/// </para>
/// <para>
/// <b>Every wait is bounded.</b> A test that could hang on a request nobody answers is a continuous
/// integration leg that can hang.
/// </para>
/// </remarks>
[NonParallelizable]
public class FetchDomainTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    [Test]
    public async Task ContinuingAPausedRequestSendsIt()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Continued</title></head><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        var paused = await fixture.PausedAsync();
        paused.GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/page"));
        paused.GetProperty("resourceType").GetString().Should().Be("Document");
        paused.GetProperty("frameId").GetString().Should().Be(fixture.FrameId);

        // The Network identifier rides the pause, which is how a client pairs the two domains.
        paused.GetProperty("networkId").GetString().Should().NotBeNullOrEmpty();

        await fixture.ContinueAsync(paused);
        await navigation.WaitAsync(Bound);

        (await fixture.Page.TitleAsync()).Should().Be("Continued");
    }

    [Test]
    public async Task FulfillingAPausedRequestAnswersItWithoutASocket()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Origin</title></head><body>from the server</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        var paused = await fixture.PausedAsync();

        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes("<html><head><title>Fulfilled</title></head><body>from the client</body></html>"));
        await fixture.Session.ResultAsync(
            "Fetch.fulfillRequest",
            $$"""
            {"requestId":"{{paused.GetProperty("requestId").GetString()}}","responseCode":200,
             "responseHeaders":[{"name":"Content-Type","value":"text/html; charset=utf-8"}],
             "body":"{{body}}"}
            """,
            fixture.Attachment);

        await navigation.WaitAsync(Bound);

        (await fixture.Page.TitleAsync()).Should().Be("Fulfilled");
        (await fixture.Page.ContentAsync()).Should().Contain("from the client");
        server.Received.Should().BeEmpty("a fulfilled request never reaches the origin");
    }

    [Test]
    public async Task FailingAPausedRequestFailsTheNavigation()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigate = fixture.Session.SendAsync("Page.navigate", $$"""{"url":"{{server.Url("/page")}}"}""", fixture.Attachment);

        var paused = await fixture.PausedAsync();
        await fixture.Session.ResultAsync(
            "Fetch.failRequest",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}","errorReason":"AccessDenied"}""",
            fixture.Attachment);

        var reply = await navigate.WaitAsync(Bound);
        reply.GetProperty("result").GetProperty("errorText").GetString().Should().Be("net::ERR_ACCESS_DENIED");

        var failed = await fixture.Session.EventAsync("Network.loadingFailed", sessionId: fixture.Attachment, timeoutSeconds: 30);
        failed.GetProperty("errorText").GetString().Should().Be("net::ERR_ACCESS_DENIED");
    }

    [Test]
    public async Task ContinuingWithARewrittenRequestChangesWhatTheServerSees()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/original", "<html><head><title>Original</title></head><body>one</body></html>");
        server.MapHtml("/rewritten", "<html><head><title>Rewritten</title></head><body>two</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/original"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        var paused = await fixture.PausedAsync();

        await fixture.Session.ResultAsync(
            "Fetch.continueRequest",
            $$"""
            {"requestId":"{{paused.GetProperty("requestId").GetString()}}",
             "url":"{{server.Url("/rewritten")}}",
             "headers":[{"name":"X-Rewritten","value":"yes"}]}
            """,
            fixture.Attachment);

        await navigation.WaitAsync(Bound);

        (await fixture.Page.TitleAsync()).Should().Be("Rewritten");
        server.Received.Single().Header("X-Rewritten").Should().Be("yes");
    }

    [Test]
    public async Task APatternDecidesWhichRequestsArePaused()
    {
        using var server = new LoopbackServer();
        server.Map("/app.js", _ => LoopbackResponse.Script("globalThis.__ran = true;"));
        server.MapHtml("/page", "<html><head><script src=\"/app.js\"></script></head><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/app.js"}]}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        // The script matched and the document did not, so this is the only pause there will be — and the
        // parse is holding the page loop while it waits for it, which is exactly the case that proves the
        // loop still drains the protocol's own mailbox.
        var paused = await fixture.PausedAsync();
        paused.GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/app.js"));
        paused.GetProperty("resourceType").GetString().Should().Be("Script");

        await fixture.ContinueAsync(paused);
        await navigation.WaitAsync(Bound);

        (await fixture.Page.EvaluateAsync<bool>("globalThis.__ran === true")).Should().BeTrue(
            "the continued script really was fetched and really did run");

        fixture.Session.EventsOf("Fetch.requestPaused", fixture.Attachment).Should().HaveCount(1,
            "the document did not match the pattern and must not have been paused");
    }

    [Test]
    public async Task ThePageGoesOnRunningWhileARequestIsPaused()
    {
        using var server = new LoopbackServer();
        server.Map("/slow.json", _ => LoopbackResponse.Json("""{"ok":true}"""));
        server.MapHtml("/page", """
            <html><body><script>
              globalThis.__ticks = 0;
              setInterval(function () { globalThis.__ticks++; }, 5);
            </script></body></html>
            """);

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        await fixture.EnableAsync();
        await fixture.Page.EvaluateAsync("fetch('/slow.json')");

        var paused = await fixture.PausedAsync();

        // The pause holds the transport thread the request is on; the page's own loop is untouched, so its
        // timers go on firing and the command that releases the request is answerable at all.
        var before = await fixture.Page.EvaluateAsync<double>("globalThis.__ticks");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromMilliseconds(200));
        var after = await fixture.Page.EvaluateAsync<double>("globalThis.__ticks");

        after.Should().BeGreaterThan(before, "a paused request must not stop the page");

        await fixture.ContinueAsync(paused);
    }

    /// <summary>
    /// The one fetch the page loop blocks on rather than pumping through, and the command that releases it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>&lt;script src&gt;</c> a <i>running script</i> inserted is fetched with the loop held, because
    /// pumping from inside a running script would run the page's jobs in the middle of one
    /// (<c>Runtime/Parsing/AGENTS.md</c>). So the answer to a pause on that fetch cannot come from the loop,
    /// and until <c>PageTarget.RunsOffThread</c> it was queued on it: the client's <c>continueRequest</c> sat
    /// in the mailbox until the fetch gave up at <c>BrowserOptions.SubresourceTimeout</c>, by which point
    /// the pause it named was gone.
    /// </para>
    /// <para>
    /// The timeout is shortened so that a regression fails in seconds rather than in half a minute; the
    /// assertion is on the round trip, which is milliseconds when the command never reaches the loop at all.
    /// </para>
    /// </remarks>
    [Test]
    public async Task ContinuingTheOneFetchTheLoopBlocksOnIsAnsweredWhileItBlocks()
    {
        using var server = new LoopbackServer();
        server.Map("/inserted.js", _ => LoopbackResponse.Script("globalThis.__inserted = true;"));
        server.MapHtml("/page", """
            <html><head><title>Blocked</title><script>
              var el = document.createElement('script');
              el.src = '/inserted.js';
              document.head.appendChild(el);
            </script></head><body>ok</body></html>
            """);

        await using var fixture = await InterceptionFixture.OpenAsync(
            server,
            new BrowserOptions { SubresourceTimeout = TimeSpan.FromSeconds(8) });

        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/inserted.js"}]}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        var paused = await fixture.PausedAsync();
        paused.GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/inserted.js"));
        paused.GetProperty("resourceType").GetString().Should().Be("Script");

        var clock = Stopwatch.StartNew();
        var reply = await fixture.Session.SendAsync(
            "Fetch.continueRequest",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment);
        clock.Stop();

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3),
            "the command touches no engine state, so it is answered on the thread that read it rather than queued behind the fetch the loop is blocked on");

        reply.TryGetProperty("error", out var error).Should().BeFalse(
            "the pause was still there to be released, and it answered {0}", error);

        await navigation.WaitAsync(Bound);

        (await fixture.Page.EvaluateAsync<bool>("globalThis.__inserted === true")).Should().BeTrue(
            "the inserted script really was paused, really was continued, and really did run");
    }

    [Test]
    public async Task DetachingContinuesEverythingThatWasPaused()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Detached</title></head><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.PausedAsync();

        // The client walks away with the document's own request still paused. A page that stayed paused would
        // never load, so detaching lets everything go rather than failing it.
        await fixture.Session.ResultAsync("Target.detachFromTarget", $$"""{"sessionId":"{{fixture.Attachment}}"}""");

        await navigation.WaitAsync(Bound);
        (await fixture.Page.TitleAsync()).Should().Be("Detached");
    }

    [Test]
    public async Task DisablingTheDomainReleasesTheRequestsItWasHolding()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Released</title></head><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.PausedAsync();

        await fixture.Session.ResultAsync("Fetch.disable", null, fixture.Attachment);

        await navigation.WaitAsync(Bound);
        (await fixture.Page.TitleAsync()).Should().Be("Released");
    }

    [Test]
    public async Task AnUnknownInterceptionIdIsRefusedInChromesOwnWords()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var error = await fixture.Session.ErrorAsync(
            "Fetch.continueRequest",
            """{"requestId":"interception-job-999"}""",
            fixture.Attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Invalid InterceptionId.");
    }

    /// <summary>A page, its target, and an attachment ready to intercept.</summary>
    private sealed class InterceptionFixture : IAsyncDisposable
    {
        private InterceptionFixture(PageSession session, Page page, string attachment, string frameId)
        {
            Session = session;
            Page = page;
            Attachment = attachment;
            FrameId = frameId;
        }

        internal PageSession Session { get; }

        internal Page Page { get; }

        internal string Attachment { get; }

        internal string FrameId { get; }

        internal static async Task<InterceptionFixture> OpenAsync(LoopbackServer server, BrowserOptions? options = null)
        {
            var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns }, options);
            var page = await session.NewPageAsync();
            var target = await session.TargetForAsync(page);
            var attachment = await session.AttachAsync(target);

            await session.EnablePageAsync(attachment);
            await session.ResultAsync("Network.enable", "{}", attachment);

            return new InterceptionFixture(session, page, attachment, target.TargetId);
        }

        internal Task EnableAsync(string? parameters = null)
            => Session.ResultAsync("Fetch.enable", parameters ?? "{}", Attachment);

        /// <summary>The next request the client has been shown and has not yet answered.</summary>
        internal async Task<JsonElement> PausedAsync(int index = 0)
            => await Session.EventAsync("Fetch.requestPaused", index, Attachment, timeoutSeconds: 30);

        internal Task ContinueAsync(JsonElement paused)
            => Session.ResultAsync(
                "Fetch.continueRequest",
                $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
                Attachment);

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}

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

    // ---------------------------------------------------------------- authentication

    /// <summary>
    /// Maps a route that challenges the first request with <paramref name="challenge"/> and answers every
    /// later one, so a test reads the retry off <see cref="LoopbackServer.Received"/>.
    /// </summary>
    private static void MapChallenged(LoopbackServer server, string path, string challenge)
    {
        var served = 0;
        server.Map(path, _ =>
        {
            if (Interlocked.Increment(ref served) > 1)
            {
                return LoopbackResponse.Html("<html><head><title>Let in</title></head><body>ok</body></html>");
            }

            return new LoopbackResponse { Status = 401, Reason = "Unauthorized", Body = "no" }
                .With("WWW-Authenticate", challenge)
                .With("Content-Type", "text/html; charset=utf-8");
        });
    }

    /// <summary>
    /// The whole lane: a <c>401</c> pauses as <c>Fetch.authRequired</c>, the client answers with
    /// credentials, and the hop goes again carrying them —
    /// https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#method-continueWithAuth.
    /// </summary>
    /// <remarks>
    /// It is the gap <see href="https://github.com/sebastienros/jint/issues/3828">#3828</see>'s survey put
    /// first: both client families send <c>handleAuthRequests: true</c> unconditionally whenever they
    /// intercept, so before this the credentials a client configured were discarded with no error anywhere.
    /// </remarks>
    [Test]
    public async Task AChallengePausesAndContinueWithAuthSendsTheHopAgainWithCredentials()
    {
        using var server = new LoopbackServer();
        MapChallenged(server, "/private", "Basic realm=\"the vault\"");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"handleAuthRequests":true}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/private"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        // The request stage pauses first, because a default pattern is the request stage's.
        await fixture.ContinueAsync(await fixture.PausedAsync());

        var challenged = await fixture.Session.EventAsync("Fetch.authRequired", sessionId: fixture.Attachment, timeoutSeconds: 30);
        var challenge = challenged.GetProperty("authChallenge");
        challenge.GetProperty("source").GetString().Should().Be("Server");
        challenge.GetProperty("scheme").GetString().Should().Be("Basic");
        challenge.GetProperty("realm").GetString().Should().Be("the vault");
        challenge.GetProperty("origin").GetString().Should().Be(server.Origin);
        challenged.GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/private"));

        await fixture.Session.ResultAsync(
            "Fetch.continueWithAuth",
            $$$"""{"requestId":"{{{challenged.GetProperty("requestId").GetString()}}}","authChallengeResponse":{"response":"ProvideCredentials","username":"ada","password":"l0velace"}}""",
            fixture.Attachment);

        // The retry is a second hop of the same request, so it pauses at the request stage too.
        await fixture.ContinueAsync(await fixture.PausedAsync(1));

        await navigation.WaitAsync(Bound);
        (await fixture.Page.TitleAsync()).Should().Be("Let in");

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("ada:l0velace"));
        var attempts = server.Received.Where(received => received.Path == "/private").ToArray();
        attempts.Should().HaveCount(2);
        attempts[0].Header("Authorization").Should().BeNull();
        attempts[1].Header("Authorization").Should().Be(expected);
    }

    /// <summary>
    /// <c>CancelAuth</c> delivers the <c>401</c> to the page, which is what a browser does when the user
    /// dismisses the dialog.
    /// </summary>
    [Test]
    public async Task CancellingAChallengeDeliversTheFourHundredAndOne()
    {
        using var server = new LoopbackServer();
        MapChallenged(server, "/private", "Basic realm=\"the vault\"");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"handleAuthRequests":true}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/private"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.ContinueAsync(await fixture.PausedAsync());

        var challenged = await fixture.Session.EventAsync("Fetch.authRequired", sessionId: fixture.Attachment, timeoutSeconds: 30);
        await fixture.Session.ResultAsync(
            "Fetch.continueWithAuth",
            $$$"""{"requestId":"{{{challenged.GetProperty("requestId").GetString()}}}","authChallengeResponse":{"response":"CancelAuth"}}""",
            fixture.Attachment);

        await navigation.WaitAsync(Bound);
        server.Received.Count(received => received.Path == "/private").Should().Be(1, "a cancelled challenge sends nothing again");
    }

    /// <summary>
    /// A scheme this browser cannot answer is still reported, and <c>ProvideCredentials</c> for it is refused
    /// <b>by name</b> rather than accepted and quietly dropped.
    /// </summary>
    /// <remarks>
    /// Being asked is how a client tells "this browser does not do Digest" from "the server never
    /// challenged"; the error on the command is how it learns its credentials were understood and could not
    /// be used. An ask that cannot be honoured must fail visibly, because an ask that silently does nothing
    /// is the defect this lane exists to remove.
    /// </remarks>
    [Test]
    public async Task CredentialsForASchemeThatCannotBeAnsweredAreRefusedByName()
    {
        using var server = new LoopbackServer();
        MapChallenged(server, "/private", "Digest realm=\"the vault\", nonce=\"abc\"");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"handleAuthRequests":true}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/private"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.ContinueAsync(await fixture.PausedAsync());

        var challenged = await fixture.Session.EventAsync("Fetch.authRequired", sessionId: fixture.Attachment, timeoutSeconds: 30);
        challenged.GetProperty("authChallenge").GetProperty("scheme").GetString().Should().Be("Digest");

        var error = await fixture.Session.ErrorAsync(
            "Fetch.continueWithAuth",
            $$$"""{"requestId":"{{{challenged.GetProperty("requestId").GetString()}}}","authChallengeResponse":{"response":"ProvideCredentials","username":"ada","password":"l0velace"}}""",
            fixture.Attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Credentials cannot be provided for a 'Digest' challenge.");

        // Refused, and the request was still released rather than left hanging on the error.
        await navigation.WaitAsync(Bound);
        server.Received.Count(received => received.Path == "/private").Should().Be(1);
    }

    /// <summary>
    /// Without <c>handleAuthRequests</c> nothing is reported, which is the protocol's own rule and what keeps
    /// a client that never asked from being shown a pause it will not answer.
    /// </summary>
    [Test]
    public async Task AChallengeIsNotReportedToAClientThatDidNotAskForOne()
    {
        using var server = new LoopbackServer();
        MapChallenged(server, "/private", "Basic realm=\"the vault\"");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/private"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.ContinueAsync(await fixture.PausedAsync());

        await navigation.WaitAsync(Bound);
        server.Received.Count(received => received.Path == "/private").Should().Be(1);
    }

    /// <summary>A page, its target, and an attachment ready to intercept.</summary>
    // ---------------------------------------------------------------- the response stage

    /// <summary>
    /// A pattern asking for <c>requestStage: "Response"</c> pauses with the response's status and headers,
    /// and <c>Fetch.continueResponse</c> lets it through —
    /// https://chromedevtools.github.io/devtools-protocol/tot/Fetch/#method-continueResponse.
    /// </summary>
    /// <remarks>
    /// The whole stage was absent while <c>FetchObserver.OnResponse</c> was a notification an observer could
    /// not answer (<see href="https://github.com/sebastienros/jint/issues/3701">#3701</see> item 1).
    /// </remarks>
    [Test]
    public async Task AResponseStagePausePresentsTheStatusAndHeadersAndContinueResponseLetsItThrough()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Delivered</title></head><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*","requestStage":"Response"}]}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        var paused = await fixture.PausedAsync();
        paused.GetProperty("responseStatusCode").GetInt32().Should().Be(200);
        paused.GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/page"));
        paused.TryGetProperty("responseHeaders", out var headers).Should().BeTrue();
        headers.GetArrayLength().Should().BeGreaterThan(0);

        await fixture.Session.ResultAsync(
            "Fetch.continueResponse",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment);

        await navigation.WaitAsync(Bound);

        (await fixture.Page.TitleAsync()).Should().Be("Delivered");
        server.Received.Should().ContainSingle(received => received.Path == "/page");
    }

    /// <summary>
    /// And it rewrites: <c>continueResponse</c> with a status and headers changes what the response
    /// <i>says</i>, while the body the server sent still arrives.
    /// </summary>
    [Test]
    public async Task ContinueResponseCanRewriteTheStatusAndHeadersAndKeepTheBody()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("the real body"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        // The answer is parked on the page rather than awaited here: Page.EvaluateAsync converts what the
        // expression returned, and what this one returns is a promise.
        await fixture.Page.EvaluateAsync("""
            window.__answer = null;
            fetch('/data').then(r => r.text().then(t => {
              window.__answer = r.status + '|' + r.headers.get('x-rewritten') + '|' + t;
            }));
            """);

        var paused = await fixture.PausedAsync();
        await fixture.Session.ResultAsync(
            "Fetch.continueResponse",
            $$"""
            {"requestId":"{{paused.GetProperty("requestId").GetString()}}","responseCode":203,
             "responsePhrase":"Rewritten",
             "responseHeaders":[{"name":"content-type","value":"text/plain"},{"name":"x-rewritten","value":"yes"}]}
            """,
            fixture.Attachment);

        (await fixture.Page.WaitForAsync("window.__answer !== null", Bound)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("window.__answer")).Should().Be("203|yes|the real body");
    }

    /// <summary>
    /// <c>Fetch.fulfillRequest</c> answers a <i>response</i>-stage pause too: the bytes the server sent are
    /// discarded unread and the client's own response is what the page gets.
    /// </summary>
    [Test]
    public async Task FulfillRequestAnswersAResponseStagePause()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Origin</title></head><body>from the server</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*","requestStage":"Response"}]}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        var paused = await fixture.PausedAsync();

        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes("<html><head><title>Substituted</title></head><body>from the client</body></html>"));
        await fixture.Session.ResultAsync(
            "Fetch.fulfillRequest",
            $$"""
            {"requestId":"{{paused.GetProperty("requestId").GetString()}}","responseCode":200,
             "responseHeaders":[{"name":"Content-Type","value":"text/html; charset=utf-8"}],
             "body":"{{body}}"}
            """,
            fixture.Attachment);

        await navigation.WaitAsync(Bound);

        (await fixture.Page.TitleAsync()).Should().Be("Substituted");
        (await fixture.Page.ContentAsync()).Should().Contain("from the client");

        // Unlike a request-stage fulfil, the origin *was* reached: the pause is after the response came back.
        server.Received.Should().ContainSingle(received => received.Path == "/page");
    }

    /// <summary>And <c>Fetch.failRequest</c> fails one, which the page sees as a navigation failure.</summary>
    [Test]
    public async Task FailRequestAnswersAResponseStagePause()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*","requestStage":"Response"}]}""");

        var navigate = fixture.Session.SendAsync("Page.navigate", $$"""{"url":"{{server.Url("/page")}}"}""", fixture.Attachment);

        var paused = await fixture.PausedAsync();
        await fixture.Session.ResultAsync(
            "Fetch.failRequest",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}","errorReason":"AccessDenied"}""",
            fixture.Attachment);

        var reply = await navigate.WaitAsync(Bound);
        reply.GetProperty("result").GetProperty("errorText").GetString().Should().Be("net::ERR_ACCESS_DENIED");
    }

    /// <summary>
    /// A client that named no <c>requestStage</c> asked for the protocol's default, which is
    /// <c>Request</c> — so it is paused once, before the request goes out, and never again after it comes
    /// back. Pausing both stages for one pattern would double every pause every recorded client expects.
    /// </summary>
    [Test]
    public async Task ADefaultPatternPausesTheRequestStageOnly()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Once</title></head><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        var paused = await fixture.PausedAsync();
        paused.TryGetProperty("responseStatusCode", out var status).Should().BeFalse("a request-stage pause has no response yet");
        _ = status;

        await fixture.ContinueAsync(paused);
        await navigation.WaitAsync(Bound);

        fixture.Session.EventsOf("Fetch.requestPaused", fixture.Attachment).Should().HaveCount(
            1,
            "the default stage is Request, so the response is never paused at");
    }

    // ---------------------------------------------------------------- getResponseBody

    /// <summary>
    /// The whole body of a response the client is holding, base64 — and the page still receives every one of
    /// those bytes exactly once afterwards, which is the property the whole read/replay design exists for.
    /// </summary>
    [Test]
    public async Task GetResponseBodyAnswersTheWholeBodyAndThePageStillReceivesIt()
    {
        const string Payload = "the whole body, twice over: once to the client and once to the page";

        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text(Payload));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");

        var paused = await fixture.PausedAsync();
        var body = await fixture.GetResponseBodyAsync(paused);

        body.GetProperty("base64Encoded").GetBoolean().Should().BeTrue("bytes are always bytes here");
        Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("body").GetString()!)).Should().Be(Payload);

        await fixture.ContinueResponseAsync(paused);

        (await fixture.AnswerAsync()).Should().Be(Payload, "the page receives every original byte exactly once");
    }

    /// <summary>Every byte value, so nothing in the path is a charset in disguise.</summary>
    [Test]
    public async Task GetResponseBodyRoundTripsARealBinaryBody()
    {
        var payload = new byte[512];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte) (i % 256);
        }

        using var server = new LoopbackServer();
        server.Map("/blob", _ => LoopbackResponse.Raw(payload, "application/octet-stream"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/blob","requestStage":"Response"}]}""");

        await fixture.Page.EvaluateAsync("""
            window.__answer = null;
            fetch('/blob')
                .then(r => r.arrayBuffer())
                .then(b => { window.__answer = Array.from(new Uint8Array(b)).join(','); },
                      e => { window.__answer = 'rejected:' + e; });
            true
            """);

        var paused = await fixture.PausedAsync();
        var read = Convert.FromBase64String((await fixture.GetResponseBodyAsync(paused)).GetProperty("body").GetString()!);
        read.Should().Equal(payload);

        await fixture.ContinueResponseAsync(paused);

        (await fixture.AnswerAsync()).Should().Be(string.Join(",", payload), "the page got the same bytes");
    }

    /// <summary>Reading twice answers the same bytes rather than reaching for a socket that has moved on.</summary>
    [Test]
    public async Task RepeatedReadsOfOnePauseAnswerTheSameBody()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("read me twice"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        var first = (await fixture.GetResponseBodyAsync(paused)).GetProperty("body").GetString();
        var second = (await fixture.GetResponseBodyAsync(paused)).GetProperty("body").GetString();

        second.Should().Be(first);

        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be("read me twice");
    }

    /// <summary>
    /// Many replies of the same body do not accumulate: each reply's encoded copy is charged to the page's
    /// allowance and released once it has been written, so a client that asks twenty times is answered twenty
    /// times rather than refused on the way.
    /// </summary>
    [Test]
    public async Task RepeatedRepliesDoNotAccumulateEncodedCopies()
    {
        var payload = new string('r', 100);

        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text(payload));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(
            server,
            new BrowserOptions { MaxCapturedResponseBytes = 8192 });

        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        // Each reply reserves about five times the body for its base64 and the message carrying it. Twenty of
        // those outstanding at once would be far past the page's allowance; twenty in sequence are not.
        for (var i = 0; i < 20; i++)
        {
            (await fixture.GetResponseBodyAsync(paused)).GetProperty("body").GetString()
                .Should().Be(expected, "reply {0} was refused, so a reservation was never given back", i);
        }

        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be(payload);
    }

    /// <summary>A read then a substitution: the bytes read are discarded and the page sees the client's own.</summary>
    [Test]
    public async Task ReadingThenFulfillingDiscardsWhatWasRead()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("from the server"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        var read = Encoding.UTF8.GetString(Convert.FromBase64String((await fixture.GetResponseBodyAsync(paused)).GetProperty("body").GetString()!));
        read.Should().Be("from the server");

        await fixture.Session.ResultAsync(
            "Fetch.fulfillRequest",
            $$"""
            {"requestId":"{{paused.GetProperty("requestId").GetString()}}","responseCode":200,
             "responseHeaders":[{"name":"Content-Type","value":"text/plain"}],
             "body":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes("from the client"))}}"}
            """,
            fixture.Attachment);

        (await fixture.AnswerAsync()).Should().Be("from the client");
    }

    /// <summary>A read then a failure: the request fails, and the prefix is released rather than replayed.</summary>
    [Test]
    public async Task ReadingThenFailingStillFailsTheRequest()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("never delivered"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        await fixture.GetResponseBodyAsync(paused);

        await fixture.Session.ResultAsync(
            "Fetch.failRequest",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}","errorReason":"AccessDenied"}""",
            fixture.Attachment);

        (await fixture.AnswerAsync()).Should().StartWith("rejected:");
    }

    /// <summary>
    /// A body the page's allowance refuses is an error and <b>not</b> a resolved pause: the client can still
    /// answer it, and the page still gets every byte.
    /// </summary>
    [Test]
    public async Task ABodyOverThePagesAllowanceIsRefusedWithoutResolvingThePause()
    {
        var payload = new string('x', 4096);

        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text(payload));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(
            server,
            new BrowserOptions { MaxCapturedResponseBytes = 64 });

        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        var error = await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Contain("allowance");

        // Not resolved: the pause is still the client's to answer, and the page loses nothing.
        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be(payload, "a refusal never truncates what the page receives");
    }

    /// <summary>
    /// Two responses paused at once share one ledger: what the first read is holding is exactly what the
    /// second does not get, the first's bytes are never evicted to admit the second, and both pages are
    /// served in full regardless.
    /// </summary>
    [Test]
    public async Task TwoPausesShareOnePageAllowance()
    {
        var small = new string('a', 100);
        var large = new string('b', 6000);

        using var server = new LoopbackServer();
        server.Map("/small", _ => LoopbackResponse.Text(small));
        server.Map("/large", _ => LoopbackResponse.Text(large));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(
            server,
            new BrowserOptions { MaxCapturedResponseBytes = 8192 });

        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/small","requestStage":"Response"},{"urlPattern":"*/large","requestStage":"Response"}]}""");

        // Both in flight, so both are paused before either is answered.
        await fixture.Page.EvaluateAsync("""
            window.__small = null;
            window.__large = null;
            fetch('/small').then(r => r.text()).then(t => { window.__small = t; }, e => { window.__small = 'rejected:' + e; });
            fetch('/large').then(r => r.text()).then(t => { window.__large = t; }, e => { window.__large = 'rejected:' + e; });
            true
            """);

        var first = await fixture.PausedAsync(0);
        var second = await fixture.PausedAsync(1);

        var (little, big) = first.GetProperty("request").GetProperty("url").GetString()!.EndsWith("/small", StringComparison.Ordinal)
            ? (first, second)
            : (second, first);

        (await fixture.GetResponseBodyAsync(little)).GetProperty("body").GetString()
            .Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes(small)));

        (await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{big.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment)).GetProperty("code").GetInt32().Should().Be(
            -32000,
            "the ledger is one, and the smaller read is holding part of it");

        // The refusal did not evict what the first read is holding: it answers from the same bytes still.
        (await fixture.GetResponseBodyAsync(little)).GetProperty("body").GetString()
            .Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes(small)));

        await fixture.ContinueResponseAsync(little);
        await fixture.ContinueResponseAsync(big);

        (await fixture.AnswerAsync("__small")).Should().Be(small);
        (await fixture.AnswerAsync("__large")).Should().Be(large, "a refusal costs the page nothing");
    }

    /// <summary>
    /// A body read evicts completed <c>Network</c> captures to make room, which is the other half of one
    /// ledger: a finished capture is the page's cheapest thing to give up, and a paused body is not.
    /// </summary>
    [Test]
    public async Task AReadEvictsACompletedNetworkCaptureToMakeRoom()
    {
        var big = new string('b', 8000);

        using var server = new LoopbackServer();
        server.Map("/first", _ => LoopbackResponse.Text("kept until it is not"));
        server.Map("/second", _ => LoopbackResponse.Text(big));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(
            server,
            new BrowserOptions { MaxCapturedResponseBytes = 8192 });

        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        await fixture.FetchOnThePageAsync("/first");
        (await fixture.AnswerAsync()).Should().Be("kept until it is not");

        // The document's own request finished first, so the identifier is looked up by URL rather than by
        // order.
        var firstId = fixture.Session
            .EventsOf("Network.responseReceived", fixture.Attachment)
            .Select(e => e.GetProperty("params"))
            .Single(p => p.GetProperty("response").GetProperty("url").GetString()!.EndsWith("/first", StringComparison.Ordinal))
            .GetProperty("requestId").GetString();

        (await fixture.Session.ResultAsync("Network.getResponseBody", $$"""{"requestId":"{{firstId}}"}""", fixture.Attachment))
            .GetProperty("body").GetString().Should().Be("kept until it is not", "the capture is there to begin with");

        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/second","requestStage":"Response"}]}""");
        await fixture.FetchOnThePageAsync("/second");
        var paused = await fixture.PausedAsync();

        // Eight kilobytes of body cannot be admitted beside the capture, so the capture goes.
        await fixture.Session.SendAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment).WaitAsync(Bound);

        (await fixture.Session.ErrorAsync("Network.getResponseBody", $$"""{"requestId":"{{firstId}}"}""", fixture.Attachment))
            .GetProperty("code").GetInt32().Should().Be(-32000, "the completed capture was given up to admit the read");

        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be(big, "and the page is served whatever the ledger decided");
    }

    /// <summary>A zero allowance refuses every body, and the page is still served.</summary>
    [Test]
    public async Task AZeroAllowanceRefusesTheReadAndThePageIsStillServed()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("still delivered"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(
            server,
            new BrowserOptions { MaxCapturedResponseBytes = 0 });

        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        (await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment)).GetProperty("code").GetInt32().Should().Be(-32000);

        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be("still delivered");
    }

    /// <summary>
    /// A request-stage pause is a real identifier naming a moment at which there is no response, which is a
    /// different answer from an identifier naming nothing at all.
    /// </summary>
    [Test]
    public async Task GetResponseBodyIsRefusedAtTheRequestStage()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync();

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        var paused = await fixture.PausedAsync();

        var error = await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("Can only get response body on requests captured after headers received.");

        await fixture.ContinueAsync(paused);
        await navigation.WaitAsync(Bound);
    }

    /// <summary>An authentication pause names the same identifier space and has no response body either.</summary>
    [Test]
    public async Task GetResponseBodyIsRefusedAtAnAuthenticationPause()
    {
        using var server = new LoopbackServer();
        MapChallenged(server, "/private", "Digest realm=\"jint\", nonce=\"abc\"");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.EnableAsync("""{"handleAuthRequests":true}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/private"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.ContinueAsync(await fixture.PausedAsync());

        var challenged = await fixture.Session.EventAsync("Fetch.authRequired", sessionId: fixture.Attachment, timeoutSeconds: 30);

        var error = await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{challenged.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment);

        error.GetProperty("message").GetString().Should().Be("Can only get response body on requests captured after headers received.");

        await fixture.Session.ResultAsync(
            "Fetch.continueWithAuth",
            $$$"""{"requestId":"{{{challenged.GetProperty("requestId").GetString()}}}","authChallengeResponse":{"response":"CancelAuth"}}""",
            fixture.Attachment);

        await navigation.WaitAsync(Bound);
    }

    /// <summary>An identifier naming nothing, and one that named a response the client has already released.</summary>
    [Test]
    public async Task GetResponseBodyRefusesAnUnknownAndAStaleIdentifier()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("gone"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        (await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            """{"requestId":"interception-job-999"}""",
            fixture.Attachment)).GetProperty("message").GetString().Should().Be("Invalid InterceptionId.");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();

        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be("gone");

        (await fixture.Session.ErrorAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment)).GetProperty("message").GetString().Should().Be(
            "Invalid InterceptionId.",
            "a released pause is gone, whatever it was released with");
    }

    /// <summary>
    /// A terminal decision arriving while a read is in flight is refused rather than raced, and the read and
    /// the release both succeed once it is not.
    /// </summary>
    /// <remarks>
    /// The body is gated on the server side, so the read is genuinely outstanding when the second command
    /// arrives. Every wait is bounded, and the gate is released whatever the assertions do.
    /// </remarks>
    [Test]
    public async Task ATerminalCommandDuringAReadIsRefusedAndBothSucceedAfterwards()
    {
        var payload = Encoding.UTF8.GetBytes("gated body");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");
        server.Map("/gated", _ => new LoopbackResponse
        {
            RawBody = payload,
            WriteBodyAsync = async (stream, token) =>
            {
                await release.Task.WaitAsync(Bound, token).ConfigureAwait(false);
                await stream.WriteAsync(payload, token).ConfigureAwait(false);
            },
        }.With("Content-Type", "text/plain; charset=utf-8"));

        await using var fixture = await InterceptionFixture.OpenAsync(server);

        try
        {
            await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
            await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/gated","requestStage":"Response"}]}""");

            await fixture.FetchOnThePageAsync("/gated");
            var paused = await fixture.PausedAsync();
            var id = paused.GetProperty("requestId").GetString();

            // Started and deliberately not awaited: the body is gated, so this read is still in flight.
            var reading = fixture.Session.SendAsync("Fetch.getResponseBody", $$"""{"requestId":"{{id}}"}""", fixture.Attachment);

            var refused = await fixture.Session.SendAsync(
                "Fetch.continueResponse",
                $$"""{"requestId":"{{id}}"}""",
                fixture.Attachment).WaitAsync(Bound);

            refused.TryGetProperty("error", out var error).Should().BeTrue("a decision may not race a read");
            error.GetProperty("code").GetInt32().Should().Be(-32000);
            error.GetProperty("message").GetString().Should().Be("Invalid state for Fetch.continueResponse");

            // A second read is refused on the same terms: one read of a response at a time.
            (await fixture.Session.ErrorAsync(
                "Fetch.getResponseBody",
                $$"""{"requestId":"{{id}}"}""",
                fixture.Attachment).WaitAsync(Bound))
                .GetProperty("message").GetString().Should().Be("Invalid state for Fetch.getResponseBody");

            release.SetResult();

            var body = (await reading.WaitAsync(Bound)).GetProperty("result");
            Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("body").GetString()!)).Should().Be("gated body");

            await fixture.ContinueResponseAsync(paused);
            (await fixture.AnswerAsync()).Should().Be("gated body");
        }
        finally
        {
            release.TrySetResult();
        }
    }

    /// <summary>
    /// Disabling the domain while a read is in flight still ends the pause — deferred behind the read rather
    /// than racing it, which is the one thing the replay cannot survive.
    /// </summary>
    [Test]
    public async Task DisablingDuringAReadStillReleasesTheResponse()
    {
        var payload = Encoding.UTF8.GetBytes("released anyway");
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");
        server.Map("/gated", _ => new LoopbackResponse
        {
            RawBody = payload,
            WriteBodyAsync = async (stream, token) =>
            {
                await release.Task.WaitAsync(Bound, token).ConfigureAwait(false);
                await stream.WriteAsync(payload, token).ConfigureAwait(false);
            },
        }.With("Content-Type", "text/plain; charset=utf-8"));

        await using var fixture = await InterceptionFixture.OpenAsync(server);

        try
        {
            await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
            await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/gated","requestStage":"Response"}]}""");

            await fixture.FetchOnThePageAsync("/gated");
            var paused = await fixture.PausedAsync();

            var reading = fixture.Session.SendAsync(
                "Fetch.getResponseBody",
                $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
                fixture.Attachment);

            await fixture.Session.ResultAsync("Fetch.disable", "{}", fixture.Attachment).WaitAsync(Bound);

            release.SetResult();

            var reply = await reading.WaitAsync(Bound);
            Encoding.UTF8.GetString(Convert.FromBase64String(reply.GetProperty("result").GetProperty("body").GetString()!))
                .Should().Be("released anyway", "the read that was already under way finishes");

            (await fixture.AnswerAsync()).Should().Be("released anyway", "and the pause the disable ended delivers it");
        }
        finally
        {
            release.TrySetResult();
        }
    }

    /// <summary>
    /// The one fetch the page loop blocks on rather than pumping through, read while it blocks — the
    /// threading claim <c>Fetch.getResponseBody</c> makes by being named in <c>PageTarget.RunsOffThread</c>.
    /// </summary>
    [Test]
    public async Task TheOneFetchTheLoopBlocksOnIsReadableWhileItBlocks()
    {
        const string Source = "globalThis.__inserted = true;";

        using var server = new LoopbackServer();
        server.Map("/inserted.js", _ => LoopbackResponse.Script(Source));
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

        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/inserted.js","requestStage":"Response"}]}""");

        var navigation = fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        var paused = await fixture.PausedAsync();
        paused.GetProperty("responseStatusCode").GetInt32().Should().Be(200);

        var clock = Stopwatch.StartNew();
        var reply = await fixture.Session.SendAsync(
            "Fetch.getResponseBody",
            $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
            fixture.Attachment).WaitAsync(Bound);
        clock.Stop();

        reply.TryGetProperty("error", out var error).Should().BeFalse(
            "the read reaches no engine state, so it is answered on the thread that read it rather than queued behind the fetch the loop is blocked on; it answered {0}", error);

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));

        Encoding.UTF8.GetString(Convert.FromBase64String(reply.GetProperty("result").GetProperty("body").GetString()!))
            .Should().Be(Source);

        await fixture.ContinueResponseAsync(paused);
        await navigation.WaitAsync(Bound);

        (await fixture.Page.EvaluateAsync<bool>("globalThis.__inserted === true")).Should().BeTrue(
            "the script the client read really did run, from the very bytes it read");
    }

    /// <summary>
    /// Reading adds no second announcement of anything: the <c>Network</c> events a request produces are the
    /// same ones it produced before, because the read is the same transfer seen once.
    /// </summary>
    [Test]
    public async Task ReadingAPausedBodyRaisesNoDuplicateNetworkEvents()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("counted once"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();
        var networkId = paused.GetProperty("networkId").GetString();

        await fixture.GetResponseBodyAsync(paused);
        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be("counted once");

        await fixture.Session.EventAsync("Network.loadingFinished", sessionId: fixture.Attachment, timeoutSeconds: 30);

        Count("Network.responseReceived").Should().Be(1);
        Count("Network.loadingFinished").Should().Be(1);
        Count("Network.loadingFailed").Should().Be(0);

        // EventsOf answers whole envelopes, so the identifier is one level in.
        int Count(string method) => fixture.Session
            .EventsOf(method, fixture.Attachment)
            .Count(e => e.GetProperty("params").TryGetProperty("requestId", out var id) && id.GetString() == networkId);
    }

    /// <summary>
    /// Reading a paused body is not the <c>Network</c> domain's copy of it: that capture is still made, and
    /// the page's own capture limit is what it always was.
    /// </summary>
    [Test]
    public async Task APausedReadLeavesTheNetworkCaptureAsItWas()
    {
        using var server = new LoopbackServer();
        server.Map("/data", _ => LoopbackResponse.Text("read by the client"));
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await InterceptionFixture.OpenAsync(server);
        await fixture.Page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });
        await fixture.EnableAsync("""{"patterns":[{"urlPattern":"*/data","requestStage":"Response"}]}""");

        await fixture.FetchOnThePageAsync("/data");
        var paused = await fixture.PausedAsync();
        var networkId = paused.GetProperty("networkId").GetString();

        await fixture.GetResponseBodyAsync(paused);
        await fixture.ContinueResponseAsync(paused);
        (await fixture.AnswerAsync()).Should().Be("read by the client");

        await fixture.Session.EventAsync("Network.loadingFinished", sessionId: fixture.Attachment, timeoutSeconds: 30);

        var captured = await fixture.Session.ResultAsync(
            "Network.getResponseBody",
            $$"""{"requestId":"{{networkId}}"}""",
            fixture.Attachment);

        captured.GetProperty("body").GetString().Should().Be("read by the client");
    }

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

        internal Task ContinueResponseAsync(JsonElement paused)
            => Session.ResultAsync(
                "Fetch.continueResponse",
                $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
                Attachment);

        internal Task<JsonElement> GetResponseBodyAsync(JsonElement paused)
            => Session.ResultAsync(
                "Fetch.getResponseBody",
                $$"""{"requestId":"{{paused.GetProperty("requestId").GetString()}}"}""",
                Attachment);

        /// <summary>
        /// Starts a <c>fetch</c> on the page and parks its answer, because the expression's own value is a
        /// promise and the page is what has to await it.
        /// </summary>
        internal Task FetchOnThePageAsync(string path)
            => Page.EvaluateAsync($$"""
                window.__answer = null;
                fetch('{{path}}')
                    .then(r => r.text())
                    .then(t => { window.__answer = t; }, e => { window.__answer = 'rejected:' + e; });
                true
                """);

        /// <summary>What the page's own fetch settled to, waited for within the suite's bound.</summary>
        internal async Task<string> AnswerAsync(string slot = "__answer")
        {
            var deadline = Stopwatch.StartNew();

            while (deadline.Elapsed < Bound)
            {
                if (await Page.EvaluateAsync<string?>("window." + slot) is { } answer)
                {
                    return answer;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new TimeoutException("the page's fetch never settled");
        }

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}

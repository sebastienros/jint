using System.Text.Json;
using Jint.Browser;
using Jint.Tests.Browser.Navigation;

namespace Jint.Tests.Browser.DevTools;

/// <summary>
/// The <c>Network</c> domain over a real page and a real origin: what a client is told about every request.
/// </summary>
/// <remarks>
/// <para>
/// <b>A real socket, deliberately.</b> Everything here is about what the transport did — the order the events
/// came in, the redirect chain, the bytes the body turned out to be, the header the server received — and a
/// stub in front of <see cref="System.Net.Http.HttpClient"/> would be testing the stub.
/// </para>
/// <para>
/// <b>Every wait is bounded</b>, generously: the bound stops a hang rather than asserting a speed.
/// </para>
/// </remarks>
[NonParallelizable]
public class NetworkDomainTests
{
    [Test]
    public async Task EveryKindOfRequestIsReportedWithChromesOwnResourceType()
    {
        using var server = new LoopbackServer();
        server.Map("/style.css", _ => LoopbackResponse.Css("body { color: red }"));
        server.Map("/app.js", _ => LoopbackResponse.Script("globalThis.__ran = true;"));
        server.Map("/data.json", _ => LoopbackResponse.Json("""{"ok":true}"""));
        server.Map("/rows.txt", _ => LoopbackResponse.Text("one"));
        server.MapHtml("/page", """
            <html><head>
              <link rel="stylesheet" href="/style.css">
              <script src="/app.js"></script>
            </head><body>
              <script>
                fetch('/data.json');
                var request = new XMLHttpRequest();
                request.open('GET', '/rows.txt');
                request.send();
              </script>
            </body></html>
            """);

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/page");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(10));

        var sent = await fixture.WaitForCountAsync("Network.requestWillBeSent", 5);
        var byPath = sent.ToDictionary(
            entry => new Uri(entry.GetProperty("request").GetProperty("url").GetString()!).AbsolutePath,
            entry => entry,
            StringComparer.Ordinal);

        byPath["/page"].GetProperty("type").GetString().Should().Be("Document");
        byPath["/style.css"].GetProperty("type").GetString().Should().Be("Stylesheet");
        byPath["/app.js"].GetProperty("type").GetString().Should().Be("Script");
        byPath["/data.json"].GetProperty("type").GetString().Should().Be("Fetch");
        byPath["/rows.txt"].GetProperty("type").GetString().Should().Be("XHR");

        // The document is always first, and its own request is addressed by the loaderId — which is how a
        // client recognises the navigation among everything else the page fetched.
        var document = sent[0];
        document.GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/page"));
        document.GetProperty("requestId").GetString().Should().Be(document.GetProperty("loaderId").GetString());
        document.GetProperty("initiator").GetProperty("type").GetString().Should().Be("other");
        document.GetProperty("frameId").GetString().Should().Be(fixture.FrameId);

        // A resource the markup referenced is the parser's, and names the document it was found in.
        byPath["/style.css"].GetProperty("initiator").GetProperty("type").GetString().Should().Be("parser");
        byPath["/style.css"].GetProperty("initiator").GetProperty("url").GetString().Should().Be(server.Url("/page"));

        // And a fetch is the script's.
        byPath["/data.json"].GetProperty("initiator").GetProperty("type").GetString().Should().Be("script");
    }

    [Test]
    public async Task OneRequestIsSentThenAnsweredThenFinished()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><head><title>Order</title></head><body>hello</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/page");

        await fixture.WaitForCountAsync("Network.loadingFinished", 1);

        var requestId = fixture.Session.EventsOf("Network.requestWillBeSent", fixture.Attachment)[0]
            .GetProperty("params").GetProperty("requestId").GetString();

        fixture.Session.Ordinal("Network.requestWillBeSent")
            .Should().BeLessThan(fixture.Session.Ordinal("Network.responseReceived"));
        fixture.Session.Ordinal("Network.responseReceived")
            .Should().BeLessThan(fixture.Session.Ordinal("Network.loadingFinished"));

        // The document's requestWillBeSent precedes the frameNavigated that announces the same document, and
        // not by arrangement: the fetch happens off the page loop and the commit cannot run until it answers.
        fixture.Session.Ordinal("Network.requestWillBeSent")
            .Should().BeLessThan(fixture.Session.Ordinal("Page.frameNavigated"));

        var response = (await fixture.EventAsync("Network.responseReceived")).GetProperty("response");
        response.GetProperty("status").GetInt32().Should().Be(200);
        response.GetProperty("mimeType").GetString().Should().Be("text/html");
        response.GetProperty("charset").GetString().Should().Be("utf-8");
        response.GetProperty("url").GetString().Should().Be(server.Url("/page"));

        // Both extra-info halves arrive, because a client that waits for one of them otherwise hangs.
        fixture.Session.EventsOf("Network.requestWillBeSentExtraInfo", fixture.Attachment).Should().NotBeEmpty();
        fixture.Session.EventsOf("Network.responseReceivedExtraInfo", fixture.Attachment).Should().NotBeEmpty();

        var finished = await fixture.EventAsync("Network.loadingFinished");
        finished.GetProperty("requestId").GetString().Should().Be(requestId);
        finished.GetProperty("encodedDataLength").GetDouble().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task ARedirectIsOneRequestWithTheHopBeforeItAttached()
    {
        using var server = new LoopbackServer();
        server.Map("/start", _ => LoopbackResponse.Redirect(302, "/end"));
        server.MapHtml("/end", "<html><head><title>End</title></head><body>arrived</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/start");

        var sent = await fixture.WaitForCountAsync("Network.requestWillBeSent", 2);

        sent[0].GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/start"));
        sent[1].GetProperty("request").GetProperty("url").GetString().Should().Be(server.Url("/end"));

        // One request, two hops: the identifier is the same and the redirect rides the hop it caused.
        sent[1].GetProperty("requestId").GetString().Should().Be(sent[0].GetProperty("requestId").GetString());
        sent[1].GetProperty("redirectResponse").GetProperty("status").GetInt32().Should().Be(302);

        // And a redirect is never a responseReceived of its own, which is what Chrome does too.
        fixture.Session.EventsOf("Network.responseReceived", fixture.Attachment).Should().HaveCount(1);
    }

    [Test]
    public async Task AResponseBodyComesBackAsTextOrAsBase64()
    {
        using var server = new LoopbackServer();
        server.Map("/data.json", _ => LoopbackResponse.Json("""{"answer":42}"""));
        server.Map("/blob.bin", _ => LoopbackResponse.Raw([1, 2, 3, 250], "application/octet-stream"));
        server.MapHtml("/page", """
            <html><body><script>
              fetch('/data.json').then(r => r.text());
              fetch('/blob.bin').then(r => r.arrayBuffer());
            </script></body></html>
            """);

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/page");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(10));
        await fixture.WaitForCountAsync("Network.loadingFinished", 3);

        var json = await fixture.BodyAsync("/data.json");
        json.GetProperty("base64Encoded").GetBoolean().Should().BeFalse();
        json.GetProperty("body").GetString().Should().Be("""{"answer":42}""");

        var binary = await fixture.BodyAsync("/blob.bin");
        binary.GetProperty("base64Encoded").GetBoolean().Should().BeTrue();
        Convert.FromBase64String(binary.GetProperty("body").GetString()!).Should().Equal(new byte[] { 1, 2, 3, 250 });

        // And the document's own body, which is what a client's response.text() after a goto reads.
        var document = await fixture.BodyAsync("/page");
        document.GetProperty("body").GetString().Should().Contain("fetch('/data.json')");
    }

    [Test]
    public async Task ABodyLargerThanThePagesWholeCaptureBudgetIsNotKept()
    {
        using var server = new LoopbackServer();
        server.Map("/big.txt", _ => LoopbackResponse.Text(new string('x', 4096)));
        server.MapHtml("/page", "<html><body><script>fetch('/big.txt').then(r => r.text());</script></body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server, new BrowserOptions { MaxCapturedResponseBytes = 512 });
        await fixture.NavigateAsync("/page");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(10));
        await fixture.WaitForCountAsync("Network.loadingFinished", 2);

        var requestId = await fixture.RequestIdAsync("/big.txt");
        var error = await fixture.Session.ErrorAsync(
            "Network.getResponseBody",
            $$"""{"requestId":"{{requestId}}"}""",
            fixture.Attachment);

        error.GetProperty("code").GetInt32().Should().Be(-32000);
        error.GetProperty("message").GetString().Should().Be("No data found for resource with given identifier");
    }

    [Test]
    public async Task ABlockedUrlIsRefusedBeforeASocketIsOpened()
    {
        using var server = new LoopbackServer();
        server.Map("/tracker.js", _ => LoopbackResponse.Script("globalThis.__tracked = true;"));
        server.MapHtml("/page", "<html><head><script src=\"/tracker.js\"></script></head><body>ok</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.Session.ResultAsync("Network.setBlockedURLs", """{"urls":["*/tracker.js"]}""", fixture.Attachment);

        await fixture.NavigateAsync("/page");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(10));

        var failed = await fixture.EventAsync("Network.loadingFailed");
        failed.GetProperty("errorText").GetString().Should().Be("net::ERR_BLOCKED_BY_CLIENT");
        failed.GetProperty("blockedReason").GetString().Should().Be("inspector");
        failed.GetProperty("type").GetString().Should().Be("Script");

        server.Received.Should().NotContain(request => request.Path == "/tracker.js",
            because: "a blocked URL is refused before the transport opens a socket");
    }

    [Test]
    public async Task ExtraHeadersReachTheServerOnEveryRequest()
    {
        using var server = new LoopbackServer();
        server.Map("/probe.js", _ => LoopbackResponse.Script("globalThis.__probed = true;"));
        server.MapHtml("/page", "<html><head><script src=\"/probe.js\"></script></head><body>ok</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.Session.ResultAsync(
            "Network.setExtraHTTPHeaders",
            """{"headers":{"X-Jint":"present","Accept-Language":"fi-FI"}}""",
            fixture.Attachment);

        await fixture.NavigateAsync("/page");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(10));

        var document = server.Received.Single(request => request.Path == "/page");
        document.Header("X-Jint").Should().Be("present");
        document.Header("Accept-Language").Should().Be("fi-FI");

        var script = server.Received.Single(request => request.Path == "/probe.js");
        script.Header("X-Jint").Should().Be("present");
    }

    [Test]
    public async Task AUserAgentOverrideIsWhatTheServerSees()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.Session.ResultAsync(
            "Network.setUserAgentOverride",
            """{"userAgent":"JintProbe/1.0","acceptLanguage":"sv-SE","platform":"Linux x86_64"}""",
            fixture.Attachment);

        await fixture.NavigateAsync("/page");

        var document = server.Received.Single(request => request.Path == "/page");
        document.Header("User-Agent").Should().Be("JintProbe/1.0");
        document.Header("Accept-Language").Should().Be("sv-SE");
    }

    [Test]
    public async Task GoingOfflineFailsEveryRequestWithChromesOwnCode()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.Session.ResultAsync(
            "Network.emulateNetworkConditions",
            """{"offline":true,"latency":0,"downloadThroughput":-1,"uploadThroughput":-1}""",
            fixture.Attachment);

        var reply = await fixture.Session.ResultAsync(
            "Page.navigate",
            $$"""{"url":"{{server.Url("/page")}}"}""",
            fixture.Attachment);

        reply.GetProperty("errorText").GetString().Should().Be("net::ERR_INTERNET_DISCONNECTED");

        var failed = await fixture.EventAsync("Network.loadingFailed");
        failed.GetProperty("errorText").GetString().Should().Be("net::ERR_INTERNET_DISCONNECTED");

        server.Received.Should().BeEmpty("offline means no socket is opened at all");
    }

    [Test]
    public async Task CookiesRoundTripThroughTheContextsJar()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/page");

        var url = server.Url("/page");
        (await fixture.Session.ResultAsync(
            "Network.setCookie",
            $$"""{"name":"session","value":"abc","url":"{{url}}"}""",
            fixture.Attachment))
            .GetProperty("success").GetBoolean().Should().BeTrue();

        var cookies = (await fixture.Session.ResultAsync("Network.getCookies", "{}", fixture.Attachment)).GetProperty("cookies");
        cookies.EnumerateArray().Select(cookie => cookie.GetProperty("name").GetString()).Should().Contain("session");

        // The page sees the same jar, which is the whole point of the context owning it.
        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().Contain("session=abc");

        await fixture.Session.ResultAsync("Network.deleteCookies", $$"""{"name":"session","url":"{{url}}"}""", fixture.Attachment);
        (await fixture.Page.EvaluateAsync<string>("document.cookie")).Should().NotContain("session=abc");

        await fixture.Session.ResultAsync("Network.setCookie", $$"""{"name":"other","value":"x","url":"{{url}}"}""", fixture.Attachment);
        await fixture.Session.ResultAsync("Network.clearBrowserCookies", null, fixture.Attachment);

        (await fixture.Session.ResultAsync("Network.getAllCookies", null, fixture.Attachment))
            .GetProperty("cookies").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task StorageAnswersTheCookieCommandsEveryClientActuallySends()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/page");

        await fixture.Session.ResultAsync(
            "Storage.setCookies",
            $$"""{"cookies":[{"name":"visits","value":"3","url":"{{server.Url("/page")}}"}]}""",
            fixture.Attachment);

        var cookies = (await fixture.Session.ResultAsync("Storage.getCookies", "{}", fixture.Attachment)).GetProperty("cookies");
        cookies.EnumerateArray().Select(cookie => cookie.GetProperty("name").GetString()).Should().Contain("visits");

        await fixture.Session.ResultAsync("Storage.clearCookies", "{}", fixture.Attachment);
        (await fixture.Session.ResultAsync("Storage.getCookies", "{}", fixture.Attachment))
            .GetProperty("cookies").GetArrayLength().Should().Be(0);
    }

    [Test]
    public async Task ARequestBodyIsReadableBack()
    {
        using var server = new LoopbackServer();
        server.Map("/submit", _ => LoopbackResponse.Text("done"));
        server.MapHtml("/page", """
            <html><body><script>
              fetch('/submit', { method: 'POST', body: 'name=jint' });
            </script></body></html>
            """);

        await using var fixture = await NetworkFixture.OpenAsync(server);
        await fixture.NavigateAsync("/page");
        await fixture.Page.WaitForIdleAsync(TimeSpan.FromSeconds(10));
        await fixture.WaitForCountAsync("Network.requestWillBeSent", 2);

        var requestId = await fixture.RequestIdAsync("/submit");
        var post = await fixture.Session.ResultAsync(
            "Network.getRequestPostData",
            $$"""{"requestId":"{{requestId}}"}""",
            fixture.Attachment);

        post.GetProperty("postData").GetString().Should().Be("name=jint");
    }

    [Test]
    public async Task NothingIsReportedBeforeAClientEnablesTheDomain()
    {
        using var server = new LoopbackServer();
        server.MapHtml("/page", "<html><body>ok</body></html>");

        await using var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns });
        var page = await session.NewPageAsync();
        var target = await session.TargetForAsync(page);
        var attachment = await session.AttachAsync(target);

        await page.NavigateAsync(server.Url("/page"), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        session.EventsOf("Network.requestWillBeSent", attachment).Should().BeEmpty(
            "a domain that has not been enabled says nothing, which is what keeps a page nobody is driving free of the cost");
    }

    /// <summary>A page, its target, an attachment with <c>Network</c> enabled, and an origin to load from.</summary>
    private sealed class NetworkFixture : IAsyncDisposable
    {
        private readonly LoopbackServer _server;

        private NetworkFixture(LoopbackServer server, PageSession session, Page page, string attachment, string frameId)
        {
            _server = server;
            Session = session;
            Page = page;
            Attachment = attachment;
            FrameId = frameId;
        }

        internal PageSession Session { get; }

        internal Page Page { get; }

        internal string Attachment { get; }

        internal string FrameId { get; }

        internal static async Task<NetworkFixture> OpenAsync(LoopbackServer server, BrowserOptions? options = null)
        {
            var session = await PageSession.CreateAsync(new BrowserContextOptions { UrlFilter = server.Owns }, options);
            var page = await session.NewPageAsync();
            var target = await session.TargetForAsync(page);
            var attachment = await session.AttachAsync(target);

            await session.EnablePageAsync(attachment);
            await session.ResultAsync("Network.enable", "{}", attachment);

            return new NetworkFixture(server, session, page, attachment, target.TargetId);
        }

        internal Task NavigateAsync(string path)
            => Page.NavigateAsync(_server.Url(path), new NavigationOptions { WaitUntil = WaitUntilState.Load });

        internal Task<JsonElement> EventAsync(string method) => Session.EventAsync(method, sessionId: Attachment, timeoutSeconds: 30);

        /// <summary>Waits for at least <paramref name="count"/> of one event and answers their parameters.</summary>
        internal async Task<JsonElement[]> WaitForCountAsync(string method, int count)
        {
            var deadline = Environment.TickCount64 + 30_000L;

            while (Environment.TickCount64 < deadline)
            {
                var events = Session.EventsOf(method, Attachment);
                if (events.Count >= count)
                {
                    return [.. events.Select(entry => entry.GetProperty("params"))];
                }

                await Task.Delay(10);
            }

            Assert.Fail($"only {Session.EventsOf(method, Attachment).Count} of {count} '{method}' events arrived within 30 seconds.");
            return [];
        }

        /// <summary>The identifier the client would address the request for <paramref name="path"/> by.</summary>
        internal async Task<string> RequestIdAsync(string path)
        {
            var url = _server.Url(path);
            var deadline = Environment.TickCount64 + 30_000L;

            while (Environment.TickCount64 < deadline)
            {
                foreach (var entry in Session.EventsOf("Network.requestWillBeSent", Attachment))
                {
                    var parameters = entry.GetProperty("params");
                    if (string.Equals(parameters.GetProperty("request").GetProperty("url").GetString(), url, StringComparison.Ordinal))
                    {
                        return parameters.GetProperty("requestId").GetString()!;
                    }
                }

                await Task.Delay(10);
            }

            Assert.Fail($"no requestWillBeSent named '{url}' arrived within 30 seconds.");
            return "";
        }

        internal async Task<JsonElement> BodyAsync(string path)
        {
            var requestId = await RequestIdAsync(path);
            return await Session.ResultAsync("Network.getResponseBody", $$"""{"requestId":"{{requestId}}"}""", Attachment);
        }

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}

using System.Globalization;
using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

/// <summary>
/// The two streaming network APIs a page opens for itself — <c>WebSocket</c> and <c>EventSource</c> — and the
/// one it deliberately does not have.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every test here constructs the interface from the page</b>, which is the thing that was missing: the
/// engine has had both features for a long time, and a page never turned either of them on, so nothing in
/// this project had ever written <c>new WebSocket(…)</c> against a real document. A test that only asserted
/// the constructor is a function would have gone on passing if the feature were removed from the engine, so
/// each of these also puts a request on the wire and reads the server's copy of it.
/// </para>
/// <para>
/// <b>The context's <see cref="BrowserContextOptions.UrlFilter"/> is shown the <c>ws:</c> URL</b>, which is
/// why these tests widen it rather than reuse <see cref="LoopbackServer.Owns"/>: one policy covers the
/// document, its subresources, its <c>XMLHttpRequest</c>s, its workers <i>and</i> its sockets, and a filter
/// written for <c>http</c> alone refuses a socket to the same origin. That is the engine's rule
/// (<c>Options.FetchOptions.AllowedSchemes</c> admits <c>ws</c> wherever it admits <c>http</c>) surfacing at
/// the page, and it is worth a test of its own.
/// </para>
/// </remarks>
public sealed class PageSocketTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Admits loopback over <c>http</c> and over <c>ws</c>, which is the whole widening these tests need.
    /// </summary>
    /// <remarks>
    /// Loopback-only is what keeps a test off somebody's DNS, which is the property
    /// <see cref="LoopbackServer.Owns"/> is really there for; the port is not pinned because the point being
    /// made is about the <i>scheme</i>.
    /// </remarks>
    private static bool ReachesLoopback(Uri uri)
        => uri.IsLoopback
            && (string.Equals(uri.Scheme, "http", StringComparison.Ordinal) || string.Equals(uri.Scheme, "ws", StringComparison.Ordinal));

    private static string SocketUrl(LoopbackServer server, string path)
        => string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{server.Port}{path}");

    [Test]
    public async Task APageCanConstructAWebSocketAndItsHandshakeReachesTheOrigin()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "<html><body>ok</body></html>"),
            configureContext: options => options.UrlFilter = ReachesLoopback);

        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        (await fixture.Page.EvaluateAsync<string>("typeof WebSocket")).Should().Be("function");

        await fixture.Page.EvaluateAsync(
            $$"""
            window.__state = null;
            window.__socket = new WebSocket('{{SocketUrl(fixture.Server, "/socket")}}');
            window.__opened = window.__socket.readyState;
            window.__socket.addEventListener('close', function (e) { window.__state = e.code; });
            """);

        // https://websockets.spec.whatwg.org/#dom-websocket-connecting — a socket is CONNECTING the moment
        // the constructor returns, which is the observable proof that the constructor did more than exist.
        (await fixture.Page.EvaluateAsync<double>("window.__opened")).Should().Be(0);

        // The server answers an ordinary 200 rather than a 101, so the connection fails and the page is told
        // so. What is being asserted is the request: the socket went out over the page's own transport, to
        // the page's own origin, with the headers a WebSocket handshake carries.
        (await fixture.Page.WaitForAsync("window.__state !== null", Bound)).Should().BeTrue();

        var handshake = fixture.Server.Received.Single(received => received.Path == "/socket");
        handshake.Header("Upgrade").Should().Be("websocket");
        handshake.Header("Connection").Should().Contain("Upgrade");
        handshake.Header("Sec-WebSocket-Key").Should().NotBeNullOrEmpty();
        handshake.Header("Sec-WebSocket-Version").Should().Be("13");
    }

    [Test]
    public async Task APageWithAFilterForHttpAloneCannotOpenASocketToItsOwnOrigin()
    {
        // The default LoopbackPage filter is LoopbackServer.Owns, which requires the http scheme.
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "<html><body>ok</body></html>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        await fixture.Page.EvaluateAsync(
            $$"""
            window.__failed = false;
            var socket = new WebSocket('{{SocketUrl(fixture.Server, "/socket")}}');
            socket.addEventListener('error', function () { window.__failed = true; });
            """);

        (await fixture.Page.WaitForAsync("window.__failed === true", Bound)).Should().BeTrue();
        fixture.Server.Received.Should().NotContain(received => received.Path == "/socket");
    }

    [Test]
    public async Task APageCanOpenAnEventSourceAndReadAnEventFromIt()
    {
        await using var fixture = await LoopbackPage.CreateAsync(server =>
        {
            server.MapHtml("/page", "<html><body>ok</body></html>");
            server.Map("/events", _ => new LoopbackResponse { Body = "data: first\n\n" }
                .With("Content-Type", "text/event-stream")
                .With("Cache-Control", "no-store"));
        });

        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        (await fixture.Page.EvaluateAsync<string>("typeof EventSource")).Should().Be("function");

        await fixture.Page.EvaluateAsync(
            """
            window.__message = null;
            window.__source = new EventSource('/events');
            window.__source.addEventListener('message', function (e) {
                window.__message = e.data;
                // Closed from the handler so the stream's end cannot start a reconnection the test would
                // then have to wait out.
                window.__source.close();
            });
            """);

        (await fixture.Page.WaitForAsync("window.__message !== null", Bound)).Should().BeTrue();
        (await fixture.Page.EvaluateAsync<string>("window.__message")).Should().Be("first");

        var stream = fixture.Server.Received.First(received => received.Path == "/events");
        stream.Header("Accept").Should().Contain("text/event-stream");
    }

    /// <summary>
    /// <c>caches</c> is deliberately absent, and this pins the decision rather than the omission.
    /// </summary>
    /// <remarks>
    /// See <c>BrowserEngineFactory</c>, where the feature set is computed: the engine's default
    /// <c>CacheStorageProvider</c> is one per engine, and a page builds a new engine on every navigation, so
    /// a <c>caches</c> granted on the default would be emptied by every navigation — a scratchpad under a
    /// name that promises otherwise. Granting it needs a provider the browsing context owns and partitions
    /// by origin, the way <c>localStorage</c> already is, plus a quota; until that exists, absent is the
    /// honest answer and a page feature-detects its way past it exactly as it does on an insecure origin.
    /// </remarks>
    [Test]
    public async Task APageHasNoCacheStorage()
    {
        await using var fixture = await LoopbackPage.CreateAsync(
            server => server.MapHtml("/page", "<html><body>ok</body></html>"));

        await fixture.Page.NavigateAsync(fixture.Url("/page"));

        (await fixture.Page.EvaluateAsync<string>("typeof caches")).Should().Be("undefined");
    }
}

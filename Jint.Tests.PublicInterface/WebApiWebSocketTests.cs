#if NET8_0_OR_GREATER
#nullable enable

using Jint;
using Jint.Runtime;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// <c>WebSocket</c> seen from outside the assembly: what a host has to say to get it, what it costs when the
/// host says nothing, and the policy that decides which sockets may be opened at all.
/// </summary>
/// <remarks>
/// This project has no <c>InternalsVisibleTo</c>, so everything here is reachable by a third party — and
/// nothing here opens a socket: every test either refuses the URL before a connection exists, or stops at the
/// synchronous half of the interface. The socket layer itself is exercised against a scripted transport in
/// <c>Jint.Tests</c>, which is where the seam that makes that possible lives.
/// </remarks>
public class WebApiWebSocketTests
{
    /// <summary>
    /// An engine whose policy refuses every URL, so a socket can be constructed and observed without a
    /// network existing. The refusal is deliberately asynchronous — see
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol.
    /// </summary>
    private static Engine RefusingEngine()
        => new(options => options.UseWebSocket(net => net.UrlFilter = _ => false));

    [Test]
    public void ADefaultEngineHasNoWebSocket()
    {
        var engine = new Engine();

        engine.Evaluate("typeof WebSocket").AsString().Should().Be("undefined");
        engine.Evaluate("typeof CloseEvent").AsString().Should().Be("undefined");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("undefined");
    }

    /// <summary>
    /// Network egress is never inherited from asking for "the web APIs", which is the same rule
    /// <c>fetch</c> has.
    /// </summary>
    [Test]
    public void UseWebApisDoesNotBringWebSocket()
    {
        var engine = new Engine(options => options.UseWebApis());

        engine.Evaluate("typeof WebSocket").AsString().Should().Be("undefined");
        WebApiFeatures.Default.HasFlag(WebApiFeatures.WebSocket).Should().BeFalse();
    }

    [Test]
    public void UseWebSocketInstallsTheInterfacesItsEventsAreMadeOf()
    {
        var engine = new Engine(options => options.UseWebSocket());

        engine.Evaluate("typeof WebSocket").AsString().Should().Be("function");
        engine.Evaluate("typeof CloseEvent").AsString().Should().Be("function");
        engine.Evaluate("typeof MessageEvent").AsString().Should().Be("function");

        // ... and the two features its own surface is built out of.
        engine.Evaluate("typeof EventTarget").AsString().Should().Be("function");
        engine.Evaluate("typeof Blob").AsString().Should().Be("function");
    }

    [Test]
    public void EnablingWebSocketDoesNotEnableFetchAndTheOptionReadsBackWhatWasAsked()
    {
        var options = new Options().UseWebSocket();
        var engine = new Engine(options);

        engine.Evaluate("typeof fetch").AsString().Should().Be("undefined");
        options.WebApi.Features.Should().Be(WebApiFeatures.WebSocket, "the closure is computed when the engine is built");
    }

    [Test]
    public void EnablingFetchDoesNotEnableWebSocket()
    {
        var engine = new Engine(options => options.UseFetch());

        engine.Evaluate("typeof fetch").AsString().Should().Be("function");
        engine.Evaluate("typeof WebSocket").AsString().Should().Be("undefined");
    }

    [Test]
    public void AHostRegisteredGlobalWins()
    {
        var marker = new Jint.Native.JsString("the host's own WebSocket");

        var engine = new Engine(options => options
            .AddLazyGlobal("WebSocket", _ => marker)
            .UseWebSocket());

        // The host's configuration runs first and the install is non-clobbering, so a host that already
        // projects its own socket implementation keeps it.
        engine.Evaluate("WebSocket").Should().BeSameAs(marker);
    }

    [Test]
    public void UseWebSocketConfiguresTheSharedNetworkGroup()
    {
        var options = new Options().UseWebSocket(net =>
        {
            net.MaxConcurrentRequests = 3;
            net.AllowedSchemes.Clear();
            net.AllowedSchemes.Add("wss");
        });

        options.WebApi.Fetch.MaxConcurrentRequests.Should().Be(3);
        options.WebApi.Fetch.AllowedSchemes.Should().Equal("wss");
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-websocket steps 3 to 7 — the only failures the
    /// constructor has, and all of them <c>SyntaxError</c> <c>DOMException</c>s.
    /// </summary>
    [TestCase("ftp://example.org/")]
    [TestCase("relative/path")]
    [TestCase("wss://example.org/#fragment")]
    public void ABadUrlIsASyntaxErrorDomException(string url)
    {
        var engine = RefusingEngine();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"new WebSocket('{url}');"))!;

        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");
        engine.Evaluate($"(() => {{ try {{ new WebSocket('{url}'); }} catch (e) {{ return e instanceof DOMException; }} }})()")
            .AsBoolean().Should().BeTrue();
    }

    [Test]
    public void ABadSubprotocolIsASyntaxErrorDomException()
    {
        var engine = RefusingEngine();

        Assert.Throws<JavaScriptException>(() => engine.Execute("new WebSocket('wss://example.org/', ['a b']);"))!
            .Error.Get("name").AsString().Should().Be("SyntaxError");
        Assert.Throws<JavaScriptException>(() => engine.Execute("new WebSocket('wss://example.org/', ['x', 'x']);"))!
            .Error.Get("name").AsString().Should().Be("SyntaxError");
    }

    /// <summary>
    /// The host's filter is the last word, and refusing is not something a script can tell from a refused
    /// connection: the socket is born CONNECTING and fails on a later turn.
    /// </summary>
    [Test]
    public void ARefusedUrlOpensNothingAndFailsAsynchronously()
    {
        var engine = RefusingEngine();

        engine.Execute("""
            var log = [];
            var ws = new WebSocket('wss://example.org/');
            log.push('state:' + ws.readyState);
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push('close:' + e.code + ':' + e.wasClean);
            """);

        // The socket is born CONNECTING — read inside the script, before anything is pumped. Execute then
        // drains the loop, which is where the failure lands.
        engine.Evaluate("log.join('|')").AsString().Should().Be("state:0|error|close:1006:false");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// The filter is shown the <c>ws:</c> URL, which is what a host has to write its rules against.
    /// </summary>
    [Test]
    public void TheFilterIsShownTheWebSocketUrl()
    {
        var seen = new List<Uri>();
        var engine = new Engine(options => options.UseWebSocket(net => net.UrlFilter = uri =>
        {
            seen.Add(uri);
            return false;
        }));

        engine.Execute("new WebSocket('https://example.org/chat');");

        seen.Should().Equal(new Uri("wss://example.org/chat"));
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-send step 1: the one exception the method has.
    /// </summary>
    [Test]
    public void SendBeforeTheConnectionIsEstablishedIsAnInvalidStateError()
    {
        var engine = RefusingEngine();

        engine.Execute("var ws = new WebSocket('wss://example.org/');");

        // The engine has been pumped by Execute, so this socket is CLOSED rather than CONNECTING — the
        // interesting state is the one before that, which the same statement can still observe.
        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute("""
            var early = new WebSocket('wss://example.org/');
            early.send('too early');
            """))!;

        thrown.Error.Get("name").AsString().Should().Be("InvalidStateError");
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close steps 1 and 2.
    /// </summary>
    [Test]
    public void CloseValidatesItsArgumentsWhateverTheReadyStateIs()
    {
        var engine = RefusingEngine();

        engine.Execute("var ws = new WebSocket('wss://example.org/');");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(3, "the refused socket has already failed");

        Assert.Throws<JavaScriptException>(() => engine.Execute("ws.close(1001);"))!
            .Error.Get("name").AsString().Should().Be("InvalidAccessError");

        Assert.Throws<JavaScriptException>(() => engine.Execute("ws.close(1000, 'x'.repeat(124));"))!
            .Error.Get("name").AsString().Should().Be("SyntaxError");

        // A closed socket still accepts a well-formed close, and does nothing with it.
        engine.Execute("ws.close(3000, 'fine');");
    }

    /// <summary>
    /// The attributes a host can read before anything has happened, and the divergence it should know about:
    /// <c>binaryType</c> starts at <c>"arraybuffer"</c> here, where a browser starts at <c>"blob"</c>.
    /// </summary>
    [Test]
    public void TheAttributesReadBackBeforeAnythingHasHappened()
    {
        var engine = new Engine(options => options.UseWebSocket(net => net.UrlFilter = _ => false));

        engine.Execute("var ws = new WebSocket('wss://example.org/chat?x=1');");

        engine.Evaluate("ws.url").AsString().Should().Be("wss://example.org/chat?x=1");
        engine.Evaluate("ws.protocol").AsString().Should().BeEmpty();
        engine.Evaluate("ws.extensions").AsString().Should().BeEmpty();
        engine.Evaluate("ws.bufferedAmount").AsNumber().Should().Be(0);
        engine.Evaluate("ws.binaryType").AsString().Should().Be("arraybuffer");
        engine.Evaluate("ws instanceof EventTarget").AsBoolean().Should().BeTrue();
    }

    /// <summary>
    /// One <c>Options</c> instance serves any number of engines, and the socket registry is per engine.
    /// </summary>
    [Test]
    public void OneOptionsInstanceServesSeveralEnginesIndependently()
    {
        var options = new Options().UseWebSocket(net =>
        {
            net.UrlFilter = _ => false;
            net.MaxConcurrentRequests = 1;
        });

        var first = new Engine(options);
        var second = new Engine(options);

        first.Execute("var a = new WebSocket('wss://example.org/1');");
        second.Execute("var b = new WebSocket('wss://example.org/2');");

        first.Evaluate("a.readyState").AsNumber().Should().Be(3);
        second.Evaluate("b.readyState").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// The globals are never reached inside a shadow realm, which is the same conservative choice every other
    /// web API here makes.
    /// </summary>
    [Test]
    public void AShadowRealmHasNoWebSocket()
    {
        var engine = new Engine(options => options.UseWebSocket());

        engine.Evaluate("new ShadowRealm().evaluate('typeof WebSocket')").AsString().Should().Be("undefined");
    }
}
#endif

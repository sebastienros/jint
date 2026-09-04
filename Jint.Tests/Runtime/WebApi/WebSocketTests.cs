#if NET8_0_OR_GREATER
#nullable enable
#pragma warning disable JINT0002 // the WebSocket observer is a preview surface

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Jint.Runtime;
using Jint.WebApi.WebSockets;

namespace Jint.Tests.Runtime.WebApi;

/// <summary>
/// <c>WebSocket</c> against the WHATWG WebSockets Standard —
/// https://websockets.spec.whatwg.org/#the-websocket-interface — driven against a scripted transport so that
/// nothing here touches a network.
/// </summary>
/// <remarks>
/// <para>
/// The double replaces <see cref="IWebSocketConnection"/>, which is the seam the whole state machine sits on:
/// the handshake, the receive loop, the send queue, <c>bufferedAmount</c>, the closing handshake and every
/// event go through the real code, and only the socket itself is imaginary.
/// </para>
/// <para>
/// <b>Nothing here measures time.</b> Every assertion is structural; <see cref="Pump"/> turns the engine over
/// until the state a test is waiting for arrives, and its deadline exists only so that a hang fails the run
/// instead of hanging it.
/// </para>
/// <para>
/// <b>A test that waits for the send loop runs on <see cref="DedicatedThread.RunAsync"/>.</b> The operation's
/// outgoing queue is a <c>Channel</c>, which does not allow synchronous continuations, so everything past a
/// <c>send()</c> or a <c>close()</c> — the write itself, the <c>bufferedAmount</c> release, the Close frame,
/// and the <c>await sender</c> that has to finish before a <c>close</c> event is dispatched — resumes on a
/// thread-pool worker. Waiting for one from a body that is itself occupying a pool worker is the
/// resource inversion <see cref="DedicatedThread.RunAsync"/> exists for, and it is what turned fixed windows
/// elsewhere in this suite into flakes (sebastienros/jint#3201, #3213). Tests driven only by the test
/// thread's own hand-offs — a delivered message, a completed handshake, a refused URL — stay where they are,
/// because their continuations run inline on the thread that raised them.
/// </para>
/// </remarks>
public class WebSocketTests
{
    /// <summary>
    /// How long a test will wait for something the socket's own loops must produce. The claim is always that
    /// the write, or the event, happens at all — never how quickly — so this is a ceiling only a genuine
    /// failure can reach rather than a budget the pool has to beat.
    /// </summary>
    private static readonly TimeSpan TransportSignalCeiling = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A socket the test drives by hand. Its <see cref="ConnectAsync"/> answers a gate the test opens, its
    /// <see cref="ReceiveAsync"/> answers whatever the test has delivered, and everything it was asked to
    /// write is recorded.
    /// </summary>
    private sealed class FakeConnection : IWebSocketConnection
    {
        private readonly object _lock = new();
        private readonly Queue<object> _inbound = new();
        private readonly SemaphoreSlim _written = new(0);
        private TaskCompletionSource<WebSocketReceipt>? _waiting;
        private bool _aborted;

        internal FakeConnection(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent)
        {
            Url = url;
            Protocols = protocols;
            MaxMessageBytes = maxMessageBytes;
            UserAgent = userAgent;
        }

        internal Uri Url { get; }

        internal IReadOnlyList<string> Protocols { get; }

        internal long MaxMessageBytes { get; }

        /// <summary>The <c>User-Agent</c> the opening handshake was asked to carry.</summary>
        internal string? UserAgent { get; }

        /// <summary>Completed by the test to finish the handshake, or faulted to fail it.</summary>
        internal TaskCompletionSource Handshake { get; } = new();

        public string SubProtocol { get; set; } = string.Empty;

        /// <summary>What the "server" answered the opening handshake with, if anything.</summary>
        public int? HandshakeStatus { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<Jint.WebApi.Fetch.FetchHeader> HandshakeHeaders { get; set; } = [];

        /// <inheritdoc />
        public IReadOnlyList<Jint.WebApi.Fetch.FetchHeader> RequestHeaders { get; set; } =
            [new Jint.WebApi.Fetch.FetchHeader("user-agent", "Jint/test")];

        internal List<(byte[] Payload, bool IsText)> Sent { get; } = new();

        internal List<(int? Code, string Reason)> CloseFrames { get; } = new();

        /// <summary>Everything written, messages and Close frame alike, in the order it reached the socket.</summary>
        internal List<string> Writes { get; } = new();

        internal int Aborts { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(static state => ((TaskCompletionSource) state!).TrySetCanceled(), Handshake);
            return Handshake.Task;
        }

        public Task SendAsync(ReadOnlyMemory<byte> payload, bool isText, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_aborted)
                {
                    return Task.FromException(new IOException("the connection was aborted"));
                }

                Sent.Add((payload.ToArray(), isText));
                Writes.Add((isText ? "text:" : "binary:") + System.Text.Encoding.UTF8.GetString(payload.Span));
            }

            _written.Release();
            return Task.CompletedTask;
        }

        public Task<WebSocketReceipt> ReceiveAsync(CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_inbound.Count > 0)
                {
                    var next = _inbound.Dequeue();
                    return next is Exception failure
                        ? Task.FromException<WebSocketReceipt>(failure)
                        : Task.FromResult((WebSocketReceipt) next);
                }

                if (_aborted)
                {
                    return Task.FromCanceled<WebSocketReceipt>(new CancellationToken(canceled: true));
                }

                _waiting = new TaskCompletionSource<WebSocketReceipt>();
                cancellationToken.Register(static state => ((TaskCompletionSource<WebSocketReceipt>) state!).TrySetCanceled(), _waiting);
                return _waiting.Task;
            }
        }

        public Task CloseOutputAsync(int? code, string reason, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                CloseFrames.Add((code, reason));
                Writes.Add("close:" + (code?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"));
            }

            _written.Release();
            return Task.CompletedTask;
        }

        public void Abort()
        {
            TaskCompletionSource<WebSocketReceipt>? waiting;
            lock (_lock)
            {
                Aborts++;
                _aborted = true;
                waiting = _waiting;
                _waiting = null;
            }

            Handshake.TrySetCanceled();
            waiting?.TrySetCanceled();
        }

        public void Dispose()
        {
        }

        /// <summary>Hands the socket a message, or the peer's Close frame, from the test's thread.</summary>
        internal void Deliver(WebSocketReceipt receipt) => Deliver((object) receipt);

        /// <summary>Fails the next receive, which is how a dropped connection looks from in here.</summary>
        internal void Fail(Exception failure) => Deliver((object) failure);

        private void Deliver(object item)
        {
            TaskCompletionSource<WebSocketReceipt>? waiting;
            lock (_lock)
            {
                waiting = _waiting;
                _waiting = null;

                if (waiting is null)
                {
                    _inbound.Enqueue(item);
                    return;
                }
            }

            if (item is Exception failure)
            {
                waiting.SetException(failure);
            }
            else
            {
                waiting.SetResult((WebSocketReceipt) item);
            }
        }

        /// <summary>
        /// Waits for the send loop — which runs on a thread pool thread, deliberately, so that a socket write
        /// never blocks the engine — to have written <paramref name="count"/> things.
        /// </summary>
        /// <remarks>
        /// The bound is <see cref="TransportSignalCeiling"/>, not an interval the send loop is expected to
        /// beat: every caller of this hands its body to <see cref="DedicatedThread.RunAsync"/>, so the pool
        /// worker the loop needs is not the one this wait is holding, and only a write that never happens can
        /// reach the ceiling.
        /// </remarks>
        internal void WaitForWrites(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _written.Wait(TransportSignalCeiling).Should().BeTrue("the send loop should have written {0} time(s)", count);
            }
        }
    }

    private sealed class FakeConnections : IWebSocketConnectionFactory
    {
        internal List<FakeConnection> Created { get; } = new();

        internal FakeConnection Last => Created[^1];

        public IWebSocketConnection Create(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes, string? userAgent)
        {
            var connection = new FakeConnection(url, protocols, maxMessageBytes, userAgent);
            Created.Add(connection);
            return connection;
        }
    }

    private static (Engine Engine, FakeConnections Sockets) SocketEngine(Action<Options.FetchOptions>? configure = null)
    {
        var engine = new Engine(options => options.UseWebApis().UseWebSocket(configure));
        var sockets = new FakeConnections();
        engine._webApi!.WebSocketConnections = sockets;
        engine.Execute("var log = [];");
        return (engine, sockets);
    }

    /// <summary>
    /// Turns the engine over until <paramref name="until"/> holds. Nothing here is a measurement: the
    /// deadline only turns a hang into a failed assertion.
    /// </summary>
    private static void Pump(Engine engine, Func<bool> until)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            engine.Tasks.ProcessTasks();

            if (until())
            {
                return;
            }

            if (stopwatch.Elapsed > TransportSignalCeiling)
            {
                Assert.Fail("the engine never reached the state the test was waiting for");
            }

            Thread.Sleep(1);
        }
    }

    private static string Log(Engine engine) => engine.Evaluate("log.join('|')").AsString();

    private static void PumpUntilLogged(Engine engine, int entries)
        => Pump(engine, () => engine.Evaluate("log.length").AsNumber() >= entries);

    /// <summary>
    /// Opens a socket, finishes its handshake and returns once the <c>open</c> event has been dispatched.
    /// </summary>
    private static (Engine Engine, FakeConnections Sockets) OpenSocket(string script = "", string url = "wss://example.org/socket")
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute($$"""
            var ws = new WebSocket('{{url}}');
            ws.onopen = () => log.push('open:' + ws.readyState + ':' + ws.protocol);
            {{script}}
            """);

        sockets.Last.Handshake.SetResult();
        PumpUntilLogged(engine, 1);

        return (engine, sockets);
    }

    /// <summary>
    /// The four moments a <c>WebSocketObserver</c> is told about, in the order
    /// <see cref="Jint.WebApi.WebSockets.WebSocketObserver"/> fixes them:
    /// <c>OnCreated</c>, <c>OnHandshakeRequest</c>, <c>OnHandshakeResponse</c> and one <c>OnClosed</c>.
    /// </summary>
    /// <remarks>
    /// The seam is deliberately not <c>FetchObserver</c>'s — a socket's handshake never reaches the fetch
    /// transport and its frames are not a body — and deliberately carries an identifier of its own, so that a
    /// host waiting for its network to go quiet is not waiting on a socket that is meant to stay open
    /// (<see href="https://github.com/sebastienros/jint/issues/3701">#3701</see> item 2).
    /// </remarks>
    [Test]
    public void AnObserverIsToldAboutTheHandshakeAndTheClose()
    {
        var observer = new RecordingSocketObserver();
        var (engine, sockets) = SocketEngine(fetch => fetch.WebSocketObserver = observer);

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/socket', ['chat', 'v2']);
            ws.onopen = () => log.push('open');
            """);

        sockets.Last.HandshakeStatus = 101;
        sockets.Last.HandshakeHeaders = [new Jint.WebApi.Fetch.FetchHeader("sec-websocket-accept", "abc")];
        sockets.Last.SubProtocol = "chat";
        sockets.Last.Handshake.SetResult();

        // The wait is for `open`, not for the observer's third call, and the difference is the whole of
        // https://github.com/sebastienros/jint/issues/3701's CI failure: the response is reported to the
        // observer *before* the open task is queued, so a close() sent on the strength of that third event
        // can still land while the socket is CONNECTING - which the standard answers by failing the
        // connection, 1006 and not clean. AnObserverSeesAbnormalClosureWhenACloseFallsDuringTheHandshake
        // asserts that answer on purpose; this test is about the graceful one and has to be past it.
        PumpUntilLogged(engine, 1);

        observer.Events.Should().Equal(
            "created wss://example.org/socket",
            "handshake wss://example.org/socket protocols=chat,v2 headers=user-agent",
            "response 101 chat headers=sec-websocket-accept");

        engine.Execute("ws.close();");
        sockets.Last.Deliver(WebSocketReceipt.Closed(1000, "done"));
        Pump(engine, () => observer.Events.Count >= 4);

        observer.Events[3].Should().StartWith("closed 1000 done");

        // One socket, one identifier, and it is not a fetch request id.
        observer.Ids.Distinct().Should().HaveCount(1);
    }

    /// <summary>
    /// A URL the host's own filter refuses is still a socket the script holds: it is created, it never shakes
    /// hands, and it closes. An observer that paired every <c>created</c> with a <c>closed</c> would otherwise
    /// leak one per refusal.
    /// </summary>
    [Test]
    public void ARefusedUrlIsACreatedSocketThatClosesWithoutAHandshake()
    {
        var observer = new RecordingSocketObserver();
        var (engine, _) = SocketEngine(fetch =>
        {
            fetch.WebSocketObserver = observer;
            fetch.UrlFilter = _ => false;
        });

        engine.Execute("var ws = new WebSocket('wss://example.org/socket');");
        Pump(engine, () => observer.Events.Count >= 2);

        observer.Events[0].Should().Be("created wss://example.org/socket");
        observer.Events[1].Should().StartWith("closed 1006 ");
        observer.Events.Should().NotContain(entry => entry.StartsWith("handshake ", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>OnClosed</c> is terminal and fires once, however many ways one socket ends at the same moment.
    /// </summary>
    [Test]
    public void AnObserverIsToldOfACloseExactlyOnce()
    {
        var observer = new RecordingSocketObserver();
        var (engine, sockets) = SocketEngine(fetch => fetch.WebSocketObserver = observer);

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/socket');
            ws.onopen = () => log.push('open');
            """);

        sockets.Last.Handshake.SetResult();
        PumpUntilLogged(engine, 1);

        engine.Execute("ws.close(); ws.close();");
        sockets.Last.Deliver(WebSocketReceipt.Closed(1000, ""));
        Pump(engine, () => observer.Events.Any(entry => entry.StartsWith("closed ", StringComparison.Ordinal)));

        engine.Tasks.ProcessTasks();
        engine.Tasks.ProcessTasks();

        observer.Events.Count(entry => entry.StartsWith("closed ", StringComparison.Ordinal)).Should().Be(1);
    }

    /// <summary>
    /// A <c>close()</c> that falls while the socket is still <c>CONNECTING</c> is
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 3.2 — "fail the WebSocket connection" —
    /// so the observer is told <c>1006</c> with no reason and <c>wasClean</c> false, whatever code the script
    /// passed and whatever the peer sends afterwards.
    /// </summary>
    /// <remarks>
    /// <b>This is the answer the <c>linux</c>/<c>net10.0</c> leg was reporting</b>, and it was right: the
    /// test above had been waiting for the observer's handshake-response call, which the operation makes
    /// before the open task is queued, so on a leg that scheduled the pool differently the close arrived
    /// during CONNECTING. Nothing about the discriminator was the platform — it was which of the two
    /// moments the wait was for — so the fix was the wait, and this is the behaviour it used to reach by
    /// accident, reached on purpose.
    /// </remarks>
    [Test]
    public void AnObserverSeesAbnormalClosureWhenACloseFallsDuringTheHandshake()
    {
        var observer = new RecordingSocketObserver();
        var (engine, sockets) = SocketEngine(fetch => fetch.WebSocketObserver = observer);

        engine.Execute("var ws = new WebSocket('wss://example.org/socket');");

        // Deliberately not waiting for `open`: the handshake is still in flight, which is what makes this the
        // CONNECTING branch rather than a graceful close.
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(0, "the socket is CONNECTING");
        engine.Execute("ws.close(1000, 'please');");

        // Even the peer answering afterwards changes nothing: there was no connection to close politely.
        sockets.Last.Deliver(WebSocketReceipt.Closed(1000, "done"));
        Pump(engine, () => observer.Events.Any(entry => entry.StartsWith("closed ", StringComparison.Ordinal)));

        observer.Events.Should().ContainSingle(entry => entry.StartsWith("closed ", StringComparison.Ordinal))
            .Which.Should().Be("closed 1006  clean=False");
    }

    /// <summary>An observer whose every callback throws changes nothing about the socket.</summary>
    [Test]
    public void AnObserverThatThrowsIsIgnored()
    {
        var (engine, sockets) = SocketEngine(fetch => fetch.WebSocketObserver = new ThrowingSocketObserver());

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/socket');
            ws.onopen = () => log.push('open');
            ws.onclose = e => log.push('close:' + e.code);
            """);

        sockets.Last.Handshake.SetResult();
        PumpUntilLogged(engine, 1);

        sockets.Last.Deliver(WebSocketReceipt.Closed(1000, "done"));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open|close:1000");
    }

    private sealed class RecordingSocketObserver : Jint.WebApi.WebSockets.WebSocketObserver
    {
        internal List<string> Events { get; } = new();

        internal List<Jint.WebApi.WebSockets.WebSocketId> Ids { get; } = new();

        public override void OnCreated(Jint.WebApi.WebSockets.WebSocketId id, Uri url)
        {
            lock (Events)
            {
                Ids.Add(id);
                Events.Add("created " + url.AbsoluteUri);
            }
        }

        public override void OnHandshakeRequest(Jint.WebApi.WebSockets.ObservedWebSocketHandshake handshake)
        {
            lock (Events)
            {
                Ids.Add(handshake.Id);
                Events.Add($"handshake {handshake.Url.AbsoluteUri} protocols={string.Join(",", handshake.Protocols)} headers={string.Join(",", handshake.Headers.Select(h => h.Name))}");
            }
        }

        public override void OnHandshakeResponse(Jint.WebApi.WebSockets.ObservedWebSocketResponse response)
        {
            lock (Events)
            {
                Ids.Add(response.Id);
                Events.Add($"response {response.Status} {response.SubProtocol} headers={string.Join(",", response.Headers.Select(h => h.Name))}");
            }
        }

        public override void OnClosed(Jint.WebApi.WebSockets.WebSocketId id, int code, string reason, bool wasClean)
        {
            lock (Events)
            {
                Ids.Add(id);
                Events.Add($"closed {code} {reason} clean={wasClean}");
            }
        }
    }

    private sealed class ThrowingSocketObserver : Jint.WebApi.WebSockets.WebSocketObserver
    {
        public override void OnCreated(Jint.WebApi.WebSockets.WebSocketId id, Uri url) => throw new InvalidOperationException("created");

        public override void OnHandshakeRequest(Jint.WebApi.WebSockets.ObservedWebSocketHandshake handshake) => throw new InvalidOperationException("handshake");

        public override void OnHandshakeResponse(Jint.WebApi.WebSockets.ObservedWebSocketResponse response) => throw new InvalidOperationException("response");

        public override void OnClosed(Jint.WebApi.WebSockets.WebSocketId id, int code, string reason, bool wasClean) => throw new InvalidOperationException("closed");
    }

    /// <summary>
    /// The opening handshake is an HTTP request, so it carries the same <c>User-Agent</c> a <c>fetch</c>
    /// would — the engine's own token by default, and whatever the host named instead.
    /// </summary>
    /// <remarks>
    /// https://fetch.spec.whatwg.org/#default-user-agent-value. What the transport does with the value is
    /// two lines of <c>ClientWebSocketConnection</c>; what a host can get wrong is the wiring, which is what
    /// this reads.
    /// </remarks>
    [Test]
    public void TheHandshakeCarriesTheConfiguredUserAgent()
    {
        var (engine, sockets) = SocketEngine();
        engine.Execute("new WebSocket('wss://example.org/socket');");
        sockets.Last.UserAgent.Should().Be("Jint/" + typeof(Engine).Assembly.GetName().Version!.ToString(3));

        var (named, namedSockets) = SocketEngine(fetch => fetch.UserAgent = "Named/1.0");
        named.Execute("new WebSocket('wss://example.org/socket');");
        namedSockets.Last.UserAgent.Should().Be("Named/1.0");

        var (silent, silentSockets) = SocketEngine(fetch => fetch.UserAgent = null);
        silent.Execute("new WebSocket('wss://example.org/socket');");
        silentSockets.Last.UserAgent.Should().BeNull("a host that cleared the value sends no such header");
    }

    [Test]
    public void TheConstructorOpensTheConnectionItWasGiven()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("var ws = new WebSocket('wss://example.org/socket?x=1', ['chat', 'v2']);");

        sockets.Created.Should().HaveCount(1);
        sockets.Last.Url.Should().Be(new Uri("wss://example.org/socket?x=1"));
        sockets.Last.Protocols.Should().Equal("chat", "v2");

        // Nothing has been pumped, so the socket is still connecting and its url reads back serialized.
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(0);
        engine.Evaluate("ws.url").AsString().Should().Be("wss://example.org/socket?x=1");
        engine.Evaluate("ws.protocol").AsString().Should().BeEmpty();
        engine.Evaluate("ws.extensions").AsString().Should().BeEmpty();
        engine.Evaluate("ws.bufferedAmount").AsNumber().Should().Be(0);
        engine.Evaluate("ws.binaryType").AsString().Should().Be("arraybuffer");
    }

    [Test]
    public void AFinishedHandshakeFiresOpenAndPublishesTheSubprotocol()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/socket', 'chat');
            ws.onopen = e => log.push('open:' + ws.readyState + ':' + ws.protocol + ':' + e.type + ':' + e.isTrusted);
            """);

        sockets.Last.SubProtocol = "chat";
        Log(engine).Should().BeEmpty("nothing runs until the engine is pumped");

        sockets.Last.Handshake.SetResult();
        PumpUntilLogged(engine, 1);

        Log(engine).Should().Be("open:1:chat:open:true");
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-websocket steps 4 and 5 — the current text maps the
    /// two HTTP schemes rather than refusing them.
    /// </summary>
    [TestCase("http://example.org/s", "ws://example.org/s")]
    [TestCase("https://example.org/s", "wss://example.org/s")]
    [TestCase("ws://example.org/s", "ws://example.org/s")]
    [TestCase("wss://example.org/s", "wss://example.org/s")]
    public void MapsTheHttpSchemesOntoTheWebSocketOnes(string input, string expected)
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute($"var ws = new WebSocket('{input}');");

        engine.Evaluate("ws.url").AsString().Should().Be(expected);
        sockets.Last.Url.Scheme.Should().Be(new Uri(expected).Scheme);
    }

    /// <summary>
    /// Step 2 parses the URL against the API base URL, which for an embedded engine is
    /// <c>Options.WebApi.Fetch.BaseUrl</c> — the same setting <c>fetch</c> and <c>new Request()</c> resolve
    /// against, since it is one browsing position rather than one per interface.
    /// </summary>
    /// <remarks>
    /// https://websockets.spec.whatwg.org/#dom-websocket-websocket step 2, and
    /// https://url.spec.whatwg.org/#concept-basic-url-parser for what a base does. The scheme mapping of
    /// steps 4 and 5 runs afterwards, so an <c>https</c> base makes a relative URL a <c>wss</c> socket.
    /// </remarks>
    [TestCase("/socket", "wss://example.org/socket")]
    [TestCase("socket", "wss://example.org/app/socket")]
    [TestCase("./socket?x=1", "wss://example.org/app/socket?x=1")]
    [TestCase("//other.example/socket", "wss://other.example/socket")]
    [TestCase("ws://elsewhere.example/s", "ws://elsewhere.example/s")]
    public void ResolvesARelativeUrlAgainstTheApiBaseUrl(string input, string expected)
    {
        var (engine, sockets) = SocketEngine(fetch => fetch.BaseUrl = new Uri("https://example.org/app/page.html"));

        engine.Execute($"var ws = new WebSocket('{input}');");

        engine.Evaluate("ws.url").AsString().Should().Be(expected);
        sockets.Last.Url.Should().Be(new Uri(expected));
    }

    /// <summary>
    /// With no base URL a relative one still does not parse, which is the <c>SyntaxError</c> step 3 asks for
    /// and what every engine did before there was a base URL to resolve against.
    /// </summary>
    [Test]
    public void WithNoBaseUrlARelativeUrlIsStillASyntaxError()
    {
        var (engine, sockets) = SocketEngine();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute("new WebSocket('/socket');"))!;

        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");
        sockets.Created.Should().BeEmpty();
    }

    /// <summary>
    /// Steps 3, 6 and 7: an unparsable URL, a scheme that is not one of the four, and a fragment.
    /// </summary>
    [TestCase("not a url")]
    [TestCase("/relative")]
    [TestCase("ftp://example.org/")]
    [TestCase("file:///tmp/x")]
    [TestCase("wss://example.org/s#fragment")]
    [TestCase("ws://example.org/s#")]
    public void RefusesAUrlTheStandardRefuses(string url)
    {
        var (engine, sockets) = SocketEngine();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"new WebSocket('{url}');"))!;

        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");
        engine.Evaluate($"(() => {{ try {{ new WebSocket('{url}'); }} catch (e) {{ return e instanceof DOMException; }} }})()")
            .AsBoolean().Should().BeTrue();

        sockets.Created.Should().BeEmpty("a URL that fails the constructor never reaches a socket");
    }

    /// <summary>
    /// Step 10: every element must be a <c>Sec-WebSocket-Protocol</c> token and none may repeat.
    /// </summary>
    [TestCase("['']")]
    [TestCase("['a b']")]
    [TestCase("['a,b']")]
    [TestCase("['a;b']")]
    [TestCase("['a\\u00e9']")]
    [TestCase("['chat', 'chat']")]
    public void RefusesASubprotocolTheProtocolWouldRefuse(string protocols)
    {
        var (engine, sockets) = SocketEngine();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"new WebSocket('wss://example.org/', {protocols});"))!;

        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");
        sockets.Created.Should().BeEmpty();
    }

    [Test]
    public void AcceptsASingleStringProtocolAndAnIterableOfThem()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            new WebSocket('wss://example.org/', 'chat');
            new WebSocket('wss://example.org/', ['chat', 'superchat']);
            new WebSocket('wss://example.org/', new Set(['a', 'b']));
            new WebSocket('wss://example.org/');
            """);

        sockets.Created[0].Protocols.Should().Equal("chat");
        sockets.Created[1].Protocols.Should().Equal("chat", "superchat");
        sockets.Created[2].Protocols.Should().Equal("a", "b");
        sockets.Created[3].Protocols.Should().BeEmpty();
    }

    [Test]
    public void ATextMessageArrivesAsAStringOnATrustedMessageEvent()
    {
        var (engine, sockets) = OpenSocket("""
            ws.onmessage = e => log.push([typeof e.data, e.data, e.origin, e.isTrusted, e instanceof MessageEvent, e instanceof Event].join(','));
            """);

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: true, "héllo"u8.ToArray()));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|string,héllo,wss://example.org,true,true,true");
    }

    [Test]
    public void ABinaryMessageArrivesAsAnArrayBufferByDefault()
    {
        var (engine, sockets) = OpenSocket("""
            ws.onmessage = e => log.push(e.data.constructor.name + ':' + new Uint8Array(e.data).join('-'));
            """);

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: false, [1, 2, 3]));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|ArrayBuffer:1-2-3");
    }

    [Test]
    public void ABinaryMessageArrivesAsABlobWhenBinaryTypeSaysSo()
    {
        var (engine, sockets) = OpenSocket("""
            ws.binaryType = 'blob';
            ws.onmessage = e => log.push(e.data.constructor.name + ':' + e.data.size + ':' + JSON.stringify(e.data.type));
            """);

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: false, [1, 2, 3, 4]));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|Blob:4:\"\"");
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-enumeration — assigning something that is not one of the
    /// enumeration's values to an attribute of enumeration type is ignored, not refused.
    /// </summary>
    [Test]
    public void BinaryTypeIgnoresAValueThatIsNotOneOfTheTwo()
    {
        var (engine, _) = OpenSocket();

        engine.Evaluate("ws.binaryType = 'blob'; ws.binaryType").AsString().Should().Be("blob");
        engine.Evaluate("ws.binaryType = 'nodebuffer'; ws.binaryType").AsString().Should().Be("blob");
        engine.Evaluate("ws.binaryType = 'arraybuffer'; ws.binaryType").AsString().Should().Be("arraybuffer");
    }

    /// <summary>
    /// A message that arrives once the socket is no longer OPEN is dropped — step 1 of "when a WebSocket
    /// message has been received".
    /// </summary>
    [Test]
    public Task AMessageThatArrivesAfterTheCloseIsDropped() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket("""
            ws.onmessage = e => log.push('message:' + e.data);
            ws.onclose = e => log.push('close:' + e.code);
            """);

        var socket = sockets.Last;

        // The message is queued as a job while the socket is still OPEN, and close() then moves the ready
        // state to CLOSING before the engine turns over — Execute drains the loop when the script is done, so
        // the job runs against a socket that is no longer OPEN and step 1 drops it.
        socket.Deliver(WebSocketReceipt.Message(isText: true, "late"u8.ToArray()));
        engine.Execute("ws.close();");

        Log(engine).Should().Be("open:1:");

        socket.WaitForWrites(1);
        socket.Deliver(WebSocketReceipt.Closed(1000, "done"));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|close:1000");
    });

    [Test]
    public Task SendMarshalsOnTheEngineThreadAndBufferedAmountFallsWhenTheBytesGoOut() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();
        var socket = sockets.Last;

        engine.Execute("""
            var bytes = new Uint8Array([7, 8, 9]);
            ws.send('héllo');
            ws.send(bytes);
            bytes[0] = 99;

            // Read inside the script: six UTF-8 bytes for the string plus three for the view, counted by
            // send() itself. Nothing can have come off the count yet, because the release is an event-loop
            // job and no job runs while a script is running.
            log.push('buffered:' + ws.bufferedAmount);
            """);

        Log(engine).Should().Be("open:1:|buffered:9");

        socket.WaitForWrites(2);
        Pump(engine, () => engine.Evaluate("ws.bufferedAmount").AsNumber() == 0);

        socket.Sent.Should().HaveCount(2);
        socket.Sent[0].IsText.Should().BeTrue();
        socket.Sent[0].Payload.Should().Equal("héllo"u8.ToArray());

        socket.Sent[1].IsText.Should().BeFalse();
        socket.Sent[1].Payload.Should().Equal([7, 8, 9], "the bytes were copied when send() was called, not when they were written");
    });

    [Test]
    public Task SendAcceptsEveryArmOfTheUnion() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();
        var socket = sockets.Last;

        engine.Execute("""
            ws.send('text');
            ws.send(new Uint8Array([1, 2]).buffer);
            ws.send(new DataView(new Uint8Array([3, 4, 5]).buffer));
            ws.send(new Blob(['ab']));
            ws.send(42);
            """);

        socket.WaitForWrites(5);

        socket.Sent.Select(s => s.IsText).Should().Equal(true, false, false, false, true);
        socket.Sent[1].Payload.Should().Equal([1, 2]);
        socket.Sent[2].Payload.Should().Equal([3, 4, 5]);
        socket.Sent[3].Payload.Should().Equal("ab"u8.ToArray());
        socket.Sent[4].Payload.Should().Equal("42"u8.ToArray());
    });

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-send step 1.
    /// </summary>
    [Test]
    public void SendBeforeTheHandshakeFinishesIsAnInvalidStateError()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("var ws = new WebSocket('wss://example.org/');");

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute("ws.send('too early');"))!;
        thrown.Error.Get("name").AsString().Should().Be("InvalidStateError");
        engine.Evaluate("(() => { try { ws.send('x'); } catch (e) { return e instanceof DOMException; } })()")
            .AsBoolean().Should().BeTrue();

        engine.Evaluate("ws.bufferedAmount").AsNumber().Should().Be(0, "a call that throws queues nothing");
        sockets.Last.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// "If the WebSocket connection is closed, this attribute's value will only increase with each call to the
    /// send() method" — https://websockets.spec.whatwg.org/#dom-websocket-bufferedamount.
    /// </summary>
    [Test]
    public Task SendAfterTheCloseOnlyCountsBytesAndWritesNothing() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();
        var socket = sockets.Last;

        engine.Execute("ws.close();");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(2);

        engine.Execute("ws.send('abcd');");
        engine.Evaluate("ws.bufferedAmount").AsNumber().Should().Be(4);

        socket.WaitForWrites(1);
        socket.CloseFrames.Should().HaveCount(1);
        socket.Sent.Should().BeEmpty("a socket that is CLOSING transmits nothing");

        // ... and it never comes down again, because nothing will ever write those bytes.
        engine.Tasks.ProcessTasks();
        engine.Evaluate("ws.bufferedAmount").AsNumber().Should().Be(4);
    });

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 3.3, then the peer's answer.
    /// </summary>
    [Test]
    public Task CloseStartsTheHandshakeAndStaysClosingUntilThePeerAnswers() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket("""
            ws.onclose = e => log.push(['close', e.code, e.reason, e.wasClean, e.isTrusted, e instanceof CloseEvent].join(','));
            ws.onerror = () => log.push('error');
            """);

        var socket = sockets.Last;

        engine.Execute("ws.close(3000, 'bye');");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(2, "close() sets CLOSING synchronously");

        socket.WaitForWrites(1);
        socket.CloseFrames.Should().Equal((3000, "bye"));
        Log(engine).Should().Be("open:1:", "the socket is not closed until the peer answers");

        socket.Deliver(WebSocketReceipt.Closed(3000, "bye"));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|close,3000,bye,true,true,true");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(3);
    });

    [Test]
    public Task CloseWithNoArgumentsSendsAFrameWithNoBody() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute("ws.close();");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((null, string.Empty));
    });

    [Test]
    public Task QueuedMessagesAreWrittenBeforeTheCloseFrame() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();
        var socket = sockets.Last;

        engine.Execute("ws.send('first'); ws.send('second'); ws.close(1000);");
        socket.WaitForWrites(3);

        // The Close frame goes out last, which is what lets a script send and then close without losing the
        // message it just sent.
        socket.Writes.Should().Equal("text:first", "text:second", "close:1000");
    });

    /// <summary>
    /// The peer's Close frame: "when the WebSocket closing handshake is started" moves the ready state, and
    /// this endpoint answers with a Close frame of its own before the connection ends.
    /// </summary>
    [Test]
    public Task APeerInitiatedCloseIsEchoedAndReportedAsClean() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket("""
            ws.onclose = e => log.push(['close', e.code, e.reason, e.wasClean].join(','));
            ws.onerror = () => log.push('error');
            """);

        var socket = sockets.Last;
        socket.Deliver(WebSocketReceipt.Closed(1001, "going away"));

        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|close,1001,going away,true");
        socket.CloseFrames.Should().Equal((1001, string.Empty));
    });

    /// <summary>
    /// A Close frame with no status code is 1005, "no status received" —
    /// https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1 — and is answered with a body-less frame.
    /// </summary>
    [Test]
    public Task APeerCloseWithNoCodeIsReportedAs1005() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket("ws.onclose = e => log.push('close:' + e.code + ':' + e.wasClean);");

        sockets.Last.Deliver(WebSocketReceipt.Closed(1005, string.Empty));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|close:1005:true");
        sockets.Last.CloseFrames.Should().Equal((null, string.Empty));
    });

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 3.2: closing before the connection is
    /// established <i>fails</i> it, so the script sees the error and the 1006 every abnormal closure has.
    /// </summary>
    [Test]
    public void ClosingDuringTheHandshakeFailsTheConnection()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            ws.onopen = () => log.push('open');
            ws.onerror = e => log.push('error:' + e.type);
            ws.onclose = e => log.push(['close', e.code, e.wasClean].join(','));
            ws.close();
            log.push('closing:' + ws.readyState);
            """);

        sockets.Last.Aborts.Should().Be(1);

        PumpUntilLogged(engine, 3);

        // The ready state is CLOSING inside close() itself; the failure it started reaches the script only on
        // a later turn, and never as an open event.
        Log(engine).Should().Be("closing:2|error:error|close,1006,false");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(3);
    }

    /// <summary>
    /// The same close, one turn later: the handshake had already finished, so an <c>open</c> event was
    /// queued — and must not be dispatched into a socket the script has since closed.
    /// </summary>
    [Test]
    public Task AnOpenThatWasAlreadyQueuedIsSuppressedByAClose() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            ws.onopen = () => log.push('open');
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push('close:' + e.code);
            """);

        // The handshake finishes, which queues the open event; the script then closes the socket before the
        // engine turns over at all.
        sockets.Last.Handshake.SetResult();
        engine.Execute("ws.close();");

        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("error|close:1006");
        engine.Evaluate("ws.readyState").AsNumber().Should().Be(3);
    });

    /// <summary>
    /// A handshake that never succeeds ends in the same pair, which is what
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol requires: no failure may be told from
    /// another.
    /// </summary>
    [Test]
    public void AFailedHandshakeFiresErrorThenClose()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            ws.onopen = () => log.push('open');
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push(['close', e.code, e.reason, e.wasClean].join(','));
            """);

        sockets.Last.Handshake.SetException(new IOException("connection refused"));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("error|close,1006,,false");
    }

    [Test]
    public Task ADroppedConnectionFiresErrorThenClose() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket("""
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push(['close', e.code, e.wasClean].join(','));
            """);

        sockets.Last.Fail(new IOException("the connection was reset"));
        PumpUntilLogged(engine, 3);

        Log(engine).Should().Be("open:1:|error|close,1006,false");
    });

    /// <summary>
    /// A message larger than <c>Options.WebApi.Fetch.MaxResponseBytes</c> is the host's own limit rather than
    /// the network's, so unlike every other failure it names itself with RFC 6455's 1009.
    /// </summary>
    [Test]
    public Task AMessageOverTheCeilingClosesWith1009() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket("""
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push(['close', e.code, e.wasClean].join(','));
            """);

        sockets.Last.MaxMessageBytes.Should().Be(32 * 1024 * 1024, "the ceiling is the network group's own");
        sockets.Last.Fail(new WebSocketMessageTooLargeException("too large"));
        PumpUntilLogged(engine, 3);

        Log(engine).Should().Be("open:1:|error|close,1009,false");
        sockets.Last.Aborts.Should().Be(1);
    });

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close steps 1 and 2.
    /// </summary>
    [TestCase("999", "InvalidAccessError")]
    [TestCase("1001", "InvalidAccessError")]
    [TestCase("2999", "InvalidAccessError")]
    [TestCase("5000", "InvalidAccessError")]
    [TestCase("-1", "InvalidAccessError")]
    [TestCase("NaN", "InvalidAccessError")]
    [TestCase("1e10", "InvalidAccessError")]
    public void CloseRefusesACodeTheProtocolReserves(string code, string expected)
    {
        var (engine, sockets) = OpenSocket();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"ws.close({code});"))!;
        thrown.Error.Get("name").AsString().Should().Be(expected);

        engine.Evaluate("ws.readyState").AsNumber().Should().Be(1, "a refused close leaves the socket open");
        sockets.Last.CloseFrames.Should().BeEmpty();
    }

    [TestCase("1000")]
    [TestCase("3000")]
    [TestCase("4999")]
    public Task CloseAcceptsTheCodesAnApplicationMaySend(string code) => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute($"ws.close({code});");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((int.Parse(code, System.Globalization.CultureInfo.InvariantCulture), string.Empty));
    });

    [Test]
    public void CloseRefusesAReasonLongerThan123Utf8Bytes()
    {
        var (engine, _) = OpenSocket();

        engine.Execute("ws.close(1000, 'x'.repeat(123));");

        var (second, _) = OpenSocket();
        var thrown = Assert.Throws<JavaScriptException>(() => second.Execute("ws.close(1000, 'x'.repeat(124));"))!;
        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");

        // Bytes, not characters: 62 two-byte characters are 124 bytes.
        var (third, _) = OpenSocket();
        Assert.Throws<JavaScriptException>(() => third.Execute("ws.close(1000, 'é'.repeat(62));"))!
            .Error.Get("name").AsString().Should().Be("SyntaxError");
    }

    /// <summary>
    /// Both arguments are converted before either is validated, which is the order
    /// https://webidl.spec.whatwg.org/#js-operations puts them in.
    /// </summary>
    [Test]
    public void CloseConvertsBothArgumentsBeforeValidatingEither()
    {
        var (engine, _) = OpenSocket();

        Assert.Throws<JavaScriptException>(() => engine.Execute("""
            ws.close(999, { toString() { log.push('reason converted'); return 'r'; } });
            """));

        Log(engine).Should().Be("open:1:|reason converted");
    }

    [Test]
    public Task CloseIsIdempotentAndSendsOneFrame() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute("ws.close(1000, 'first'); ws.close(3000, 'second');");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((1000, "first"));
    });

    /// <summary>
    /// A reason with no code has nowhere to live in the protocol's Close frame, so it travels behind a normal
    /// closure rather than being dropped.
    /// </summary>
    [Test]
    public Task AReasonWithNoCodeTravelsAsANormalClosure() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute("ws.close(undefined, 'bye');");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((1000, "bye"));
    });

    /// <summary>
    /// The policy refuses the URL, and the script cannot tell that from a refused connection — which
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol requires.
    /// </summary>
    [Test]
    public void APolicyDenialLooksExactlyLikeARefusedConnection()
    {
        var (engine, sockets) = SocketEngine(net => net.UrlFilter = _ => false);

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            log.push('state:' + ws.readyState);
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push(['close', e.code, e.wasClean].join(','));
            """);

        sockets.Created.Should().BeEmpty("nothing is opened at all");

        PumpUntilLogged(engine, 3);

        // The constructor does not throw and the socket is born CONNECTING; only a later turn tells the
        // script anything, and what it tells it is what a refused connection would have.
        Log(engine).Should().Be("state:0|error|close,1006,false");
    }

    /// <summary>
    /// The scheme list is written in fetch's terms, and read in the socket's: <c>http</c> admits <c>ws</c> and
    /// <c>https</c> admits <c>wss</c>.
    /// </summary>
    [TestCase("ws://example.org/", "https", false)]
    [TestCase("ws://example.org/", "http", true)]
    [TestCase("ws://example.org/", "ws", true)]
    [TestCase("wss://example.org/", "http", false)]
    [TestCase("wss://example.org/", "https", true)]
    [TestCase("wss://example.org/", "wss", true)]
    public void TheSchemeListIsTranslatedIntoTheSocketsOwnSchemes(string url, string allowed, bool opens)
    {
        var (engine, sockets) = SocketEngine(net =>
        {
            net.AllowedSchemes.Clear();
            net.AllowedSchemes.Add(allowed);
        });

        engine.Execute($"var ws = new WebSocket('{url}');");

        sockets.Created.Should().HaveCount(opens ? 1 : 0);
    }

    [Test]
    public void TheFilterIsShownTheWebSocketUrl()
    {
        var seen = new List<Uri>();
        var (engine, _) = SocketEngine(net => net.UrlFilter = uri =>
        {
            seen.Add(uri);
            return true;
        });

        engine.Execute("new WebSocket('https://example.org/chat?x=1');");

        seen.Should().Equal(new Uri("wss://example.org/chat?x=1"));
    }

    /// <summary>
    /// <c>Options.WebApi.Fetch.MaxConcurrentRequests</c> bounds sockets too — separately from the requests in
    /// flight, since a socket is meant to be long-lived.
    /// </summary>
    [Test]
    public Task TooManyOpenSocketsIsAQuotaExceededError() => DedicatedThread.RunAsync(() =>
    {
        var (engine, sockets) = SocketEngine(net => net.MaxConcurrentRequests = 2);

        engine.Execute("var a = new WebSocket('wss://example.org/1'); var b = new WebSocket('wss://example.org/2');");

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute("new WebSocket('wss://example.org/3');"))!;
        thrown.Error.Get("name").AsString().Should().Be("QuotaExceededError");

        // https://webidl.spec.whatwg.org/#quotaexceedederror — the interface, carrying the ceiling and the
        // count the refused socket would have taken the engine to.
        engine.Evaluate("""
            (() => {
                try { new WebSocket('wss://example.org/3'); }
                catch (e) { return [e instanceof QuotaExceededError, e.code, e.quota, e.requested].join('|'); }
                return 'no error';
            })()
            """).AsString().Should().Be("true|22|2|3");

        // A closed socket frees its slot.
        sockets.Created[0].Handshake.SetResult();
        sockets.Created[1].Handshake.SetResult();
        Pump(engine, () => engine.Evaluate("a.readyState").AsNumber() == 1);

        engine.Execute("a.close();");
        sockets.Created[0].WaitForWrites(1);
        sockets.Created[0].Deliver(WebSocketReceipt.Closed(1000, string.Empty));
        Pump(engine, () => engine.Evaluate("a.readyState").AsNumber() == 3);

        engine.Execute("new WebSocket('wss://example.org/4');");
        sockets.Created.Should().HaveCount(3);
    });

    /// <summary>
    /// A restore ends the evaluation cycle, so the socket is dropped rather than left delivering into globals
    /// that no longer exist.
    /// </summary>
    [Test]
    public void ARestoreAbortsTheSocketAndDeliversNothingIntoTheRestoredEngine()
    {
        var (engine, sockets) = SocketEngine();
        var delivered = false;
        engine.SetValue("mark", new Action(() => delivered = true));

        var snapshot = engine.Advanced.CaptureGlobalSnapshot();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            ws.onopen = mark;
            ws.onclose = mark;
            ws.onerror = mark;
            """);

        var socket = sockets.Last;
        socket.Handshake.SetResult();

        engine.Advanced.RestoreGlobalSnapshot(snapshot);

        socket.Aborts.Should().Be(1, "the connection is dropped, not merely forgotten");

        socket.Deliver(WebSocketReceipt.Message(isText: true, "ignored"u8.ToArray()));
        for (var i = 0; i < 10; i++)
        {
            engine.Tasks.ProcessTasks();
        }

        delivered.Should().BeFalse();
        engine.Evaluate("typeof ws").AsString().Should().Be("undefined", "the binding belonged to the cycle that ended");
    }

    [Test]
    public void TheReadyStateConstantsAreOnBothTheInterfaceObjectAndThePrototype()
    {
        var engine = new Engine(options => options.UseWebSocket());

        engine.Evaluate("[WebSocket.CONNECTING, WebSocket.OPEN, WebSocket.CLOSING, WebSocket.CLOSED].join(',')")
            .AsString().Should().Be("0,1,2,3");
        engine.Evaluate("[WebSocket.prototype.CONNECTING, WebSocket.prototype.OPEN, WebSocket.prototype.CLOSING, WebSocket.prototype.CLOSED].join(',')")
            .AsString().Should().Be("0,1,2,3");

        // … and in the order the IDL declares them, which is the order that section defines them in and the
        // one a record conversion over the interface object reads.
        engine.Evaluate("Object.keys(WebSocket).join(',')").AsString()
            .Should().Be("CONNECTING,OPEN,CLOSING,CLOSED");

        // https://webidl.spec.whatwg.org/#es-constants — { writable: false, enumerable: true, configurable: false }
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(WebSocket, 'OPEN')");
        descriptor.Get("writable").AsBoolean().Should().BeFalse();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeFalse();
    }

    [Test]
    public void TheInterfaceInheritsFromEventTarget()
    {
        var (engine, _) = OpenSocket();

        engine.Evaluate("Object.getPrototypeOf(WebSocket) === EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.getPrototypeOf(WebSocket.prototype) === EventTarget.prototype").AsBoolean().Should().BeTrue();
        engine.Evaluate("ws instanceof EventTarget").AsBoolean().Should().BeTrue();
        engine.Evaluate("Object.prototype.toString.call(ws)").AsString().Should().Be("[object WebSocket]");
        engine.Evaluate("Object.getOwnPropertyNames(ws).length").AsNumber().Should().Be(0, "every attribute is an accessor on the prototype");
    }

    /// <summary>
    /// The attributes brand-check their receiver, so the prototype itself answers none of them.
    /// </summary>
    [Test]
    public void ThePrototypeIsNotASocket()
    {
        var engine = new Engine(options => options.UseWebSocket());

        foreach (var member in new[] { "url", "readyState", "bufferedAmount", "protocol", "extensions", "binaryType" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"WebSocket.prototype.{member}"))!
                .Error.Get("name").AsString().Should().Be("TypeError");
        }
    }

    /// <summary>
    /// An <c>addEventListener</c> registration and the handler attribute are the same list, in registration
    /// order — https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    [Test]
    public void ListenersAndHandlerAttributesShareOneList()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            ws.addEventListener('open', () => log.push('listener'));
            ws.onopen = () => log.push('handler');
            ws.addEventListener('open', () => log.push('second listener'));
            """);

        engine.Evaluate("typeof ws.onopen").AsString().Should().Be("function");

        sockets.Last.Handshake.SetResult();
        PumpUntilLogged(engine, 3);

        Log(engine).Should().Be("listener|handler|second listener");
    }

    [Test]
    public void AHandlerAttributeCanBeClearedAndReassigned()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("""
            var ws = new WebSocket('wss://example.org/');
            ws.onopen = () => log.push('first');
            ws.onopen = null;
            ws.onopen = () => log.push('second');
            """);

        sockets.Last.Handshake.SetResult();
        PumpUntilLogged(engine, 1);

        Log(engine).Should().Be("second");
        engine.Execute("ws.onopen = null;");
        engine.Evaluate("ws.onopen").IsNull().Should().BeTrue();
    }

    /// <summary>
    /// The rest of what an event handler IDL attribute is —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes — read on a
    /// <c>WebSocket</c>: the handler is <b>one entry of the object's own listener list</b> that keeps the
    /// position it first took, a non-object clears it (<c>[LegacyTreatNonObjectAsNull]</c>), and an object
    /// that is not callable is stored and read back but never invoked.
    /// </summary>
    [Test]
    public void AHandlerAttributeKeepsItsPositionAndTakesANonObjectAsNull()
    {
        var (engine, sockets) = OpenSocket("""
            ws.onmessage = () => log.push('handler');
            ws.addEventListener('message', () => log.push('listener'));
            ws.onmessage = () => log.push('replaced');
            """);

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: true, "a"u8.ToArray()));
        PumpUntilLogged(engine, 3);

        // Reassigning replaced the callback in place, so the handler still runs before a listener added after
        // it — a remove-and-add would have put it last.
        Log(engine).Should().Be("open:1:|replaced|listener");

        engine.Execute("ws.onmessage = 42;");
        engine.Evaluate("ws.onmessage").IsNull().Should().BeTrue();

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: true, "b"u8.ToArray()));
        PumpUntilLogged(engine, 4);
        Log(engine).Should().Be("open:1:|replaced|listener|listener");

        // An object that is not callable is kept and read back, and the dispatch simply passes it over.
        engine.Execute("var bag = {}; ws.onmessage = bag;");
        engine.Evaluate("ws.onmessage === bag").AsBoolean().Should().BeTrue();

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: true, "c"u8.ToArray()));
        PumpUntilLogged(engine, 5);
        Log(engine).Should().Be("open:1:|replaced|listener|listener|listener");
    }
}
#endif

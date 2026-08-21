#if NET8_0_OR_GREATER
#nullable enable

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
/// </remarks>
public class WebSocketTests
{
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

        internal FakeConnection(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes)
        {
            Url = url;
            Protocols = protocols;
            MaxMessageBytes = maxMessageBytes;
        }

        internal Uri Url { get; }

        internal IReadOnlyList<string> Protocols { get; }

        internal long MaxMessageBytes { get; }

        /// <summary>Completed by the test to finish the handshake, or faulted to fail it.</summary>
        internal TaskCompletionSource Handshake { get; } = new();

        public string SubProtocol { get; set; } = string.Empty;

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
        internal void WaitForWrites(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _written.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("the send loop should have written {0} time(s)", count);
            }
        }
    }

    private sealed class FakeConnections : IWebSocketConnectionFactory
    {
        internal List<FakeConnection> Created { get; } = new();

        internal FakeConnection Last => Created[^1];

        public IWebSocketConnection Create(Uri url, IReadOnlyList<string> protocols, long maxMessageBytes)
        {
            var connection = new FakeConnection(url, protocols, maxMessageBytes);
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
            engine.Advanced.ProcessTasks();

            if (until())
            {
                return;
            }

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(30))
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

    [Fact]
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

    [Fact]
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
    [Theory]
    [InlineData("http://example.org/s", "ws://example.org/s")]
    [InlineData("https://example.org/s", "wss://example.org/s")]
    [InlineData("ws://example.org/s", "ws://example.org/s")]
    [InlineData("wss://example.org/s", "wss://example.org/s")]
    public void MapsTheHttpSchemesOntoTheWebSocketOnes(string input, string expected)
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute($"var ws = new WebSocket('{input}');");

        engine.Evaluate("ws.url").AsString().Should().Be(expected);
        sockets.Last.Url.Scheme.Should().Be(new Uri(expected).Scheme);
    }

    /// <summary>
    /// Steps 3, 6 and 7: an unparsable URL, a scheme that is not one of the four, and a fragment.
    /// </summary>
    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative")]
    [InlineData("ftp://example.org/")]
    [InlineData("file:///tmp/x")]
    [InlineData("wss://example.org/s#fragment")]
    [InlineData("ws://example.org/s#")]
    public void RefusesAUrlTheStandardRefuses(string url)
    {
        var (engine, sockets) = SocketEngine();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"new WebSocket('{url}');"));

        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");
        engine.Evaluate($"(() => {{ try {{ new WebSocket('{url}'); }} catch (e) {{ return e instanceof DOMException; }} }})()")
            .AsBoolean().Should().BeTrue();

        sockets.Created.Should().BeEmpty("a URL that fails the constructor never reaches a socket");
    }

    /// <summary>
    /// Step 10: every element must be a <c>Sec-WebSocket-Protocol</c> token and none may repeat.
    /// </summary>
    [Theory]
    [InlineData("['']")]
    [InlineData("['a b']")]
    [InlineData("['a,b']")]
    [InlineData("['a;b']")]
    [InlineData("['a\\u00e9']")]
    [InlineData("['chat', 'chat']")]
    public void RefusesASubprotocolTheProtocolWouldRefuse(string protocols)
    {
        var (engine, sockets) = SocketEngine();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"new WebSocket('wss://example.org/', {protocols});"));

        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");
        sockets.Created.Should().BeEmpty();
    }

    [Fact]
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

    [Fact]
    public void ATextMessageArrivesAsAStringOnATrustedMessageEvent()
    {
        var (engine, sockets) = OpenSocket("""
            ws.onmessage = e => log.push([typeof e.data, e.data, e.origin, e.isTrusted, e instanceof MessageEvent, e instanceof Event].join(','));
            """);

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: true, "héllo"u8.ToArray()));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|string,héllo,wss://example.org,true,true,true");
    }

    [Fact]
    public void ABinaryMessageArrivesAsAnArrayBufferByDefault()
    {
        var (engine, sockets) = OpenSocket("""
            ws.onmessage = e => log.push(e.data.constructor.name + ':' + new Uint8Array(e.data).join('-'));
            """);

        sockets.Last.Deliver(WebSocketReceipt.Message(isText: false, [1, 2, 3]));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|ArrayBuffer:1-2-3");
    }

    [Fact]
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
    [Fact]
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
    [Fact]
    public void AMessageThatArrivesAfterTheCloseIsDropped()
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
    }

    [Fact]
    public void SendMarshalsOnTheEngineThreadAndBufferedAmountFallsWhenTheBytesGoOut()
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
    }

    [Fact]
    public void SendAcceptsEveryArmOfTheUnion()
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
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-send step 1.
    /// </summary>
    [Fact]
    public void SendBeforeTheHandshakeFinishesIsAnInvalidStateError()
    {
        var (engine, sockets) = SocketEngine();

        engine.Execute("var ws = new WebSocket('wss://example.org/');");

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute("ws.send('too early');"));
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
    [Fact]
    public void SendAfterTheCloseOnlyCountsBytesAndWritesNothing()
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
        engine.Advanced.ProcessTasks();
        engine.Evaluate("ws.bufferedAmount").AsNumber().Should().Be(4);
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 3.3, then the peer's answer.
    /// </summary>
    [Fact]
    public void CloseStartsTheHandshakeAndStaysClosingUntilThePeerAnswers()
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
    }

    [Fact]
    public void CloseWithNoArgumentsSendsAFrameWithNoBody()
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute("ws.close();");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((null, string.Empty));
    }

    [Fact]
    public void QueuedMessagesAreWrittenBeforeTheCloseFrame()
    {
        var (engine, sockets) = OpenSocket();
        var socket = sockets.Last;

        engine.Execute("ws.send('first'); ws.send('second'); ws.close(1000);");
        socket.WaitForWrites(3);

        // The Close frame goes out last, which is what lets a script send and then close without losing the
        // message it just sent.
        socket.Writes.Should().Equal("text:first", "text:second", "close:1000");
    }

    /// <summary>
    /// The peer's Close frame: "when the WebSocket closing handshake is started" moves the ready state, and
    /// this endpoint answers with a Close frame of its own before the connection ends.
    /// </summary>
    [Fact]
    public void APeerInitiatedCloseIsEchoedAndReportedAsClean()
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
    }

    /// <summary>
    /// A Close frame with no status code is 1005, "no status received" —
    /// https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1 — and is answered with a body-less frame.
    /// </summary>
    [Fact]
    public void APeerCloseWithNoCodeIsReportedAs1005()
    {
        var (engine, sockets) = OpenSocket("ws.onclose = e => log.push('close:' + e.code + ':' + e.wasClean);");

        sockets.Last.Deliver(WebSocketReceipt.Closed(1005, string.Empty));
        PumpUntilLogged(engine, 2);

        Log(engine).Should().Be("open:1:|close:1005:true");
        sockets.Last.CloseFrames.Should().Equal((null, string.Empty));
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 3.2: closing before the connection is
    /// established <i>fails</i> it, so the script sees the error and the 1006 every abnormal closure has.
    /// </summary>
    [Fact]
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
    [Fact]
    public void AnOpenThatWasAlreadyQueuedIsSuppressedByAClose()
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
    }

    /// <summary>
    /// A handshake that never succeeds ends in the same pair, which is what
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol requires: no failure may be told from
    /// another.
    /// </summary>
    [Fact]
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

    [Fact]
    public void ADroppedConnectionFiresErrorThenClose()
    {
        var (engine, sockets) = OpenSocket("""
            ws.onerror = () => log.push('error');
            ws.onclose = e => log.push(['close', e.code, e.wasClean].join(','));
            """);

        sockets.Last.Fail(new IOException("the connection was reset"));
        PumpUntilLogged(engine, 3);

        Log(engine).Should().Be("open:1:|error|close,1006,false");
    }

    /// <summary>
    /// A message larger than <c>Options.WebApi.Fetch.MaxResponseBytes</c> is the host's own limit rather than
    /// the network's, so unlike every other failure it names itself with RFC 6455's 1009.
    /// </summary>
    [Fact]
    public void AMessageOverTheCeilingClosesWith1009()
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
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close steps 1 and 2.
    /// </summary>
    [Theory]
    [InlineData("999", "InvalidAccessError")]
    [InlineData("1001", "InvalidAccessError")]
    [InlineData("2999", "InvalidAccessError")]
    [InlineData("5000", "InvalidAccessError")]
    [InlineData("-1", "InvalidAccessError")]
    [InlineData("NaN", "InvalidAccessError")]
    [InlineData("1e10", "InvalidAccessError")]
    public void CloseRefusesACodeTheProtocolReserves(string code, string expected)
    {
        var (engine, sockets) = OpenSocket();

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute($"ws.close({code});"));
        thrown.Error.Get("name").AsString().Should().Be(expected);

        engine.Evaluate("ws.readyState").AsNumber().Should().Be(1, "a refused close leaves the socket open");
        sockets.Last.CloseFrames.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1000")]
    [InlineData("3000")]
    [InlineData("4999")]
    public void CloseAcceptsTheCodesAnApplicationMaySend(string code)
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute($"ws.close({code});");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((int.Parse(code, System.Globalization.CultureInfo.InvariantCulture), string.Empty));
    }

    [Fact]
    public void CloseRefusesAReasonLongerThan123Utf8Bytes()
    {
        var (engine, _) = OpenSocket();

        engine.Execute("ws.close(1000, 'x'.repeat(123));");

        var (second, _) = OpenSocket();
        var thrown = Assert.Throws<JavaScriptException>(() => second.Execute("ws.close(1000, 'x'.repeat(124));"));
        thrown.Error.Get("name").AsString().Should().Be("SyntaxError");

        // Bytes, not characters: 62 two-byte characters are 124 bytes.
        var (third, _) = OpenSocket();
        Assert.Throws<JavaScriptException>(() => third.Execute("ws.close(1000, 'é'.repeat(62));"))
            .Error.Get("name").AsString().Should().Be("SyntaxError");
    }

    /// <summary>
    /// Both arguments are converted before either is validated, which is the order
    /// https://webidl.spec.whatwg.org/#js-operations puts them in.
    /// </summary>
    [Fact]
    public void CloseConvertsBothArgumentsBeforeValidatingEither()
    {
        var (engine, _) = OpenSocket();

        Assert.Throws<JavaScriptException>(() => engine.Execute("""
            ws.close(999, { toString() { log.push('reason converted'); return 'r'; } });
            """));

        Log(engine).Should().Be("open:1:|reason converted");
    }

    [Fact]
    public void CloseIsIdempotentAndSendsOneFrame()
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute("ws.close(1000, 'first'); ws.close(3000, 'second');");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((1000, "first"));
    }

    /// <summary>
    /// A reason with no code has nowhere to live in the protocol's Close frame, so it travels behind a normal
    /// closure rather than being dropped.
    /// </summary>
    [Fact]
    public void AReasonWithNoCodeTravelsAsANormalClosure()
    {
        var (engine, sockets) = OpenSocket();

        engine.Execute("ws.close(undefined, 'bye');");
        sockets.Last.WaitForWrites(1);

        sockets.Last.CloseFrames.Should().Equal((1000, "bye"));
    }

    /// <summary>
    /// The policy refuses the URL, and the script cannot tell that from a refused connection — which
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol requires.
    /// </summary>
    [Fact]
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
    [Theory]
    [InlineData("ws://example.org/", "https", false)]
    [InlineData("ws://example.org/", "http", true)]
    [InlineData("ws://example.org/", "ws", true)]
    [InlineData("wss://example.org/", "http", false)]
    [InlineData("wss://example.org/", "https", true)]
    [InlineData("wss://example.org/", "wss", true)]
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

    [Fact]
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
    [Fact]
    public void TooManyOpenSocketsIsAQuotaExceededError()
    {
        var (engine, sockets) = SocketEngine(net => net.MaxConcurrentRequests = 2);

        engine.Execute("var a = new WebSocket('wss://example.org/1'); var b = new WebSocket('wss://example.org/2');");

        var thrown = Assert.Throws<JavaScriptException>(() => engine.Execute("new WebSocket('wss://example.org/3');"));
        thrown.Error.Get("name").AsString().Should().Be("QuotaExceededError");

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
    }

    /// <summary>
    /// A restore ends the evaluation cycle, so the socket is dropped rather than left delivering into globals
    /// that no longer exist.
    /// </summary>
    [Fact]
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
            engine.Advanced.ProcessTasks();
        }

        delivered.Should().BeFalse();
        engine.Evaluate("typeof ws").AsString().Should().Be("undefined", "the binding belonged to the cycle that ended");
    }

    [Fact]
    public void TheReadyStateConstantsAreOnBothTheInterfaceObjectAndThePrototype()
    {
        var engine = new Engine(options => options.UseWebSocket());

        engine.Evaluate("[WebSocket.CONNECTING, WebSocket.OPEN, WebSocket.CLOSING, WebSocket.CLOSED].join(',')")
            .AsString().Should().Be("0,1,2,3");
        engine.Evaluate("[WebSocket.prototype.CONNECTING, WebSocket.prototype.OPEN, WebSocket.prototype.CLOSING, WebSocket.prototype.CLOSED].join(',')")
            .AsString().Should().Be("0,1,2,3");

        // https://webidl.spec.whatwg.org/#es-constants — { writable: false, enumerable: true, configurable: false }
        var descriptor = engine.Evaluate("Object.getOwnPropertyDescriptor(WebSocket, 'OPEN')");
        descriptor.Get("writable").AsBoolean().Should().BeFalse();
        descriptor.Get("enumerable").AsBoolean().Should().BeTrue();
        descriptor.Get("configurable").AsBoolean().Should().BeFalse();
    }

    [Fact]
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
    [Fact]
    public void ThePrototypeIsNotASocket()
    {
        var engine = new Engine(options => options.UseWebSocket());

        foreach (var member in new[] { "url", "readyState", "bufferedAmount", "protocol", "extensions", "binaryType" })
        {
            Assert.Throws<JavaScriptException>(() => engine.Evaluate($"WebSocket.prototype.{member}"))
                .Error.Get("name").AsString().Should().Be("TypeError");
        }
    }

    /// <summary>
    /// An <c>addEventListener</c> registration and the handler attribute are the same list, in registration
    /// order — https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    [Fact]
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

    [Fact]
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
}
#endif

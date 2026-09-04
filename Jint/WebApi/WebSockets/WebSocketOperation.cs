#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Runtime;

namespace Jint.WebApi.WebSockets;

/// <summary>
/// One <c>WebSocket</c>'s life off the engine thread: the handshake, the receive loop, the queue of outgoing
/// messages and the closing handshake.
/// <para>
/// https://websockets.spec.whatwg.org/#feedback-from-the-protocol
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>No JavaScript ever runs here.</b> Every one of the standard's "queue a task" steps is a
/// generation-stamped event-loop job, so an <c>open</c>, <c>message</c>, <c>error</c> or <c>close</c> listener
/// runs where every other continuation in Jint runs: inside a blocking <c>UnwrapIfPromise</c>, an
/// <c>await</c> of <c>EvaluateAsync</c>, or the host's own <c>engine.Tasks.ProcessTasks()</c> loop. Jint
/// never starts a thread to pump the engine, so an engine nobody pumps sees no events at all — while the
/// socket itself goes on draining, which is what keeps a peer from being throttled by a script that has
/// stopped listening.
/// </para>
/// <para>
/// <b>The realm and the event-loop generation are captured at construction</b>, exactly as
/// <c>FetchOperation</c> captures them, and for the same reason: a message that arrives after
/// <c>RestoreGlobalSnapshot</c> has ended the evaluation cycle must not reach the restored engine. The
/// registry in <c>WebApiEngineState</c> additionally aborts the socket at that point, so the connection is
/// dropped rather than merely ignored.
/// </para>
/// <para>
/// <b>The direction of every hand-over is one-way and lock-free.</b> Engine thread to socket: a
/// <see cref="Channel{T}"/> of already-marshalled byte payloads, and one <see cref="CloseIntent"/> published
/// by a compare-and-swap. Socket to engine thread: event-loop jobs. Nothing is shared mutable state, which is
/// why there is not a lock in the file.
/// </para>
/// </remarks>
internal sealed class WebSocketOperation
{
    /// <summary>https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1 — "normal closure".</summary>
    internal const int NormalClosure = 1000;

    /// <summary>
    /// https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1 — "the connection was closed abnormally". The one
    /// code every failure reports, which is what
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol requires so that a script cannot tell a
    /// refused connection from a DNS failure from a policy refusal.
    /// </summary>
    internal const int AbnormalClosure = 1006;

    /// <summary>https://www.rfc-editor.org/rfc/rfc6455#section-7.4.1 — "message is too big".</summary>
    private const int MessageTooBig = 1009;

    private readonly Engine _engine;

    /// <summary>
    /// The realm the socket was created in, captured now. A job running on a later event-loop turn would
    /// otherwise build its <c>MessageEvent</c> — and its <c>ArrayBuffer</c> — against whatever realm happened
    /// to be ambient then.
    /// </summary>
    private readonly Realm _realm;

    private readonly JsWebSocket _socket;

    /// <summary>
    /// The transport, or <see langword="null"/> when the host's policy refused the URL — in which case the
    /// only thing this operation ever does is queue the failure the standard's asynchronous "fail the
    /// WebSocket connection" amounts to.
    /// </summary>
    private readonly IWebSocketConnection? _connection;

    private readonly int _generation;

    /// <summary>
    /// The engine's own cancellation token, from <see cref="CancellationConstraint"/>. A socket torn down
    /// through it fires no events at all: a constraint that turned into an <c>error</c> event would no longer
    /// bound anything, since the script would handle it and carry on.
    /// </summary>
    private readonly CancellationToken _engineToken;

    private readonly CancellationTokenSource _cancellation;

    /// <summary>
    /// How long the opening handshake may take, and — once this endpoint has sent its Close frame — how long
    /// the peer has to answer it. From <c>Options.WebApi.Fetch.Timeout</c>.
    /// </summary>
    private readonly TimeSpan _handshakeTimeout;

    /// <summary>
    /// The messages <c>send()</c> has marshalled on the engine thread and the send loop has not yet written.
    /// Unbounded because the ceiling is <c>bufferedAmount</c>'s, which <c>send()</c> enforces itself by
    /// flagging the socket as full — the standard's own escape hatch for a buffer that cannot grow further.
    /// </summary>
    private readonly Channel<PendingSend> _outgoing = Channel.CreateUnbounded<PendingSend>(new UnboundedChannelOptions
    {
        SingleReader = true,

        // The engine thread writes and both it and the receive loop may complete the writer, so this is not a
        // single-writer channel however single-threaded the sends themselves are.
        SingleWriter = false,

        // The send loop must never run on the engine's thread: it does socket I/O.
        AllowSynchronousContinuations = false,
    });

    private CloseIntent? _closeIntent;
    private bool _abandoned;

    private readonly WebSocketObservation? _observation;
    private readonly IReadOnlyList<string> _protocols;

    internal WebSocketOperation(
        Engine engine,
        Realm realm,
        JsWebSocket socket,
        IWebSocketConnection? connection,
        TimeSpan handshakeTimeout,
        WebSocketObservation? observation = null,
        IReadOnlyList<string>? protocols = null)
    {
        _engine = engine;
        _realm = realm;
        _socket = socket;
        _connection = connection;
        _observation = observation;
        _protocols = protocols ?? [];
        _generation = engine.EventLoopGeneration;
        _handshakeTimeout = handshakeTimeout;
        _engineToken = engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(_engineToken);
    }

    /// <summary>
    /// Starts the run. Called from the constructor, on the engine thread; it returns at the transport's first
    /// await, so the <c>new WebSocket(...)</c> expression completes without waiting for a socket.
    /// </summary>
    internal void Start()
    {
        if (_connection is null)
        {
            // The policy refused the URL. The standard has no synchronous failure for this — its own
            // equivalent, a blocked request, is a network error inside "establish a WebSocket connection" —
            // so the socket is born CONNECTING and fails on the next turn, indistinguishably from a refused
            // connection. That indistinguishability is required:
            // https://websockets.spec.whatwg.org/#feedback-from-the-protocol.
            Finish(Abnormal());
            return;
        }

        _ = RunAsync(_connection);
    }

    /// <summary>
    /// Queues one marshalled message for the send loop. The bytes were taken on the engine thread, so nothing
    /// the script does afterwards can change what goes on the wire.
    /// </summary>
    /// <returns>Whether the message was queued, i.e. whether it will ever be written.</returns>
    internal bool Enqueue(ReadOnlyMemory<byte> payload, bool isText)
        => _outgoing.Writer.TryWrite(new PendingSend(payload, isText));

    /// <summary>
    /// https://websockets.spec.whatwg.org/#dom-websocket-close step 3.3, "start the WebSocket closing
    /// handshake". The frame goes out <i>after</i> everything already queued, which is what lets a script
    /// <c>send()</c> and then <c>close()</c> without losing the message.
    /// </summary>
    /// <remarks>
    /// The first caller wins: a script closing twice, or closing while the peer's own Close frame is being
    /// echoed, must not put two Close frames on the wire.
    /// </remarks>
    internal void RequestClose(int? code, string reason)
    {
        if (Interlocked.CompareExchange(ref _closeIntent, new CloseIntent(code, reason), null) is not null)
        {
            return;
        }

        _outgoing.Writer.TryComplete();
    }

    /// <summary>
    /// "Fail the WebSocket connection", https://www.rfc-editor.org/rfc/rfc6455#section-7.1.7 — drop it now,
    /// with no handshake. What <c>close()</c> before the connection is established does, and what a socket
    /// flagged as full does.
    /// </summary>
    internal void Fail()
    {
        _connection?.Abort();

        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run finished first and released it; there is nothing left to cancel.
        }
    }

    /// <summary>
    /// Abandons the socket because the evaluation cycle it belongs to has ended. The generation fence already
    /// stops a message reaching the restored engine; this is what stops the socket holding a connection open
    /// to deliver into a queue nobody will read.
    /// </summary>
    internal void Abandon()
    {
        Volatile.Write(ref _abandoned, true);
        _outgoing.Writer.TryComplete();
        Fail();
    }

    private async Task RunAsync(IWebSocketConnection connection)
    {
        try
        {
            using (var handshake = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token))
            {
                if (IsBounded(_handshakeTimeout))
                {
                    handshake.CancelAfter(_handshakeTimeout);
                }

                _observation?.HandshakeRequest(connection.RequestHeaders, _protocols);

                await connection.ConnectAsync(handshake.Token).ConfigureAwait(false);
            }

            ReportHandshakeResponse(connection);
        }
        catch
        {
            // The server may have answered and refused; a status collected before the throw is still worth
            // reporting, and there is nothing to report when the connection never got one.
            ReportHandshakeResponse(connection);
            // Every way the handshake can fail is one failure: a refused connection, a name that does not
            // resolve, a TLS error, a status that is not 101, a subprotocol the server did not accept, the
            // deadline, or a close() that fell during it. The standard requires exactly that — none of them
            // may be told apart by a script.
            Finish(Abnormal());
            return;
        }

        EnqueueOpen(connection.SubProtocol);

        var sender = SendLoopAsync(connection);
        var closure = await ReceiveLoopAsync(connection).ConfigureAwait(false);

        // The receive loop has ended, so nothing more will ever be written: completing the queue is what lets
        // the send loop finish its own work — including this endpoint's Close frame — and stop.
        _outgoing.Writer.TryComplete();
        await sender.ConfigureAwait(false);

        Finish(closure);
    }

    /// <summary>
    /// https://websockets.spec.whatwg.org/#feedback-from-the-protocol — "when a WebSocket message has been
    /// received" and "when the WebSocket closing handshake is started", until the connection ends.
    /// </summary>
    private async Task<WebSocketClosure> ReceiveLoopAsync(IWebSocketConnection connection)
    {
        while (true)
        {
            WebSocketReceipt receipt;
            try
            {
                receipt = await connection.ReceiveAsync(_cancellation.Token).ConfigureAwait(false);
            }
            catch (WebSocketMessageTooLargeException)
            {
                // The host's own ceiling, not the network's, so unlike every other failure this one names
                // itself: a script that cannot tell "your peer sent more than this host allows" from "the
                // connection dropped" cannot react to it. The connection is dropped rather than closed
                // politely — the peer is already sending more than the host agreed to buffer.
                connection.Abort();
                return new WebSocketClosure(MessageTooBig, string.Empty, WasClean: false);
            }
            catch
            {
                return Abnormal();
            }

            if (receipt.Kind == WebSocketReceiptKind.Close)
            {
                // "When the WebSocket closing handshake is started, queue a task to change the ready state to
                // CLOSING." Then answer it, unless this endpoint already sent its own Close frame — which is
                // exactly what RequestClose's compare-and-swap decides.
                EnqueueClosingHandshake();
                RequestClose(EchoCode(receipt.CloseCode), string.Empty);

                return new WebSocketClosure(receipt.CloseCode, receipt.CloseReason, WasClean: true);
            }

            EnqueueMessage(receipt.Kind == WebSocketReceiptKind.Text, receipt.Data);
        }
    }

    /// <summary>
    /// Writes what <c>send()</c> queued, then this endpoint's Close frame if one was asked for.
    /// </summary>
    /// <remarks>
    /// <c>bufferedAmount</c> comes down one message at a time, from an event-loop job posted after the write
    /// completed — which is the attribute's own definition: bytes "queued using send() but ... not yet
    /// transmitted to the network". A write that never happened, because the connection died first, therefore
    /// leaves its bytes counted, which is the truthful answer.
    /// </remarks>
    private async Task SendLoopAsync(IWebSocketConnection connection)
    {
        try
        {
            while (await _outgoing.Reader.WaitToReadAsync(_cancellation.Token).ConfigureAwait(false))
            {
                while (_outgoing.Reader.TryRead(out var pending))
                {
                    await connection.SendAsync(pending.Payload, pending.IsText, _cancellation.Token).ConfigureAwait(false);
                    EnqueueBufferedRelease(pending.Payload.Length);
                }
            }
        }
        catch
        {
            // The connection is gone. The receive loop is what reports that, with the one code every failure
            // gets, so there is nothing to do here but stop.
            return;
        }

        if (Volatile.Read(ref _closeIntent) is not { } intent)
        {
            return;
        }

        try
        {
            await connection.CloseOutputAsync(intent.Code, intent.Reason, _cancellation.Token).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        // The peer now owes us its half of the handshake. Bounding that wait is what stops a socket the
        // script has closed from living on because the peer never answered — the close then reports 1006 and
        // wasClean false, which is precisely what an unfinished handshake is.
        if (IsBounded(_handshakeTimeout))
        {
            try
            {
                _cancellation.CancelAfter(_handshakeTimeout);
            }
            catch (ObjectDisposedException)
            {
                // The run finished between the write and here; the wait is already over.
            }
        }
    }

    /// <summary>
    /// Releases the transport and queues the standard's "when the WebSocket connection is closed" task.
    /// </summary>
    private void Finish(WebSocketClosure closure)
    {
        _connection?.Dispose();
        _cancellation.Dispose();

        // Before the task that tells script, because the observer is not on the engine's queue and a socket
        // whose engine has gone away would otherwise never report its close at all.
        _observation?.Closed(closure.Code, closure.Reason, closure.WasClean);

        EnqueueClosed(closure);
    }

    /// <summary>
    /// Tells the observer what the server answered the handshake with, when there was an answer at all.
    /// </summary>
    private void ReportHandshakeResponse(IWebSocketConnection connection)
    {
        if (_observation is null || connection.HandshakeStatus is not { } status)
        {
            return;
        }

        _observation.HandshakeResponse(status, connection.HandshakeHeaders, connection.SubProtocol);
    }

    private void EnqueueOpen(string protocol) => Enqueue(() => _socket.ReportOpen(protocol));

    private void EnqueueMessage(bool isText, byte[] data) => Enqueue(() => _socket.ReportMessage(isText, data));

    private void EnqueueClosingHandshake() => Enqueue(_socket.ReportClosingHandshake);

    private void EnqueueClosed(WebSocketClosure closure)
        => Enqueue(() => _socket.ReportClosed(closure.Code, closure.Reason, closure.WasClean));

    private void EnqueueBufferedRelease(int bytes) => Enqueue(() => _socket.ReleaseBufferedAmount(bytes));

    /// <summary>
    /// Queues one of the standard's tasks, carrying the generation the socket was opened in.
    /// </summary>
    /// <remarks>
    /// Two things are checked before it is queued rather than after it is dequeued, both because a queued job
    /// would otherwise keep the socket — and the values it carries — alive for nothing: an abandoned socket,
    /// and an engine whose cancellation constraint has fired. The generation fence at dequeue is the
    /// authority either way.
    /// </remarks>
    private void Enqueue(Action task)
    {
        if (Volatile.Read(ref _abandoned) || _engineToken.IsCancellationRequested)
        {
            return;
        }

        _engine.AddToEventLoop(() => RunTask(task), _generation, EventLoopJobKind.Task);
    }

    /// <summary>
    /// On the engine thread, in the realm the socket was created in. An exception from a listener erupts out
    /// of whatever is pumping, which is the contract every other web-API callback in Jint has — see
    /// <c>JsEventTarget</c>'s remarks. The socket's own state has already been updated by then, so it stays
    /// usable.
    /// </summary>
    private void RunTask(Action task)
    {
        var entered = EnterRealm();
        try
        {
            task();
        }
        finally
        {
            LeaveRealm(entered);
        }
    }

    private bool EnterRealm()
    {
        if (ReferenceEquals(_engine.Realm, _realm))
        {
            return false;
        }

        _engine.EnterExecutionContext(_realm.GlobalEnv, _realm.GlobalEnv, _realm, privateEnvironment: null, strict: _engine.Options.Strict);
        return true;
    }

    private void LeaveRealm(bool entered)
    {
        if (entered)
        {
            _engine.LeaveExecutionContext();
        }
    }

    private static WebSocketClosure Abnormal() => new(AbnormalClosure, string.Empty, WasClean: false);

    private static bool IsBounded(TimeSpan timeout) => timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan;

    /// <summary>
    /// The status code to answer the peer's Close frame with —
    /// https://www.rfc-editor.org/rfc/rfc6455#section-5.5.1, "may send a Close frame … echoing the status
    /// code". A frame that carried no code is answered with none, and a code the framework would refuse to
    /// put on the wire is answered with 1000 rather than turning a clean close into a failure.
    /// </summary>
    private static int? EchoCode(int code)
    {
        if (code == WebSocketReceipt.NoStatusReceived)
        {
            return null;
        }

        var sendable = code is (>= 1000 and <= 1003) or (>= 1007 and <= 1011) or (>= 3000 and <= 4999);
        return sendable ? code : NormalClosure;
    }

    /// <summary>One message the engine thread marshalled and the send loop has yet to write.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct PendingSend(ReadOnlyMemory<byte> Payload, bool IsText);

    /// <summary>
    /// The close this endpoint asked for. A class rather than a struct so that it can be published — and read
    /// back as one whole — with a single interlocked operation.
    /// </summary>
    private sealed record CloseIntent(int? Code, string Reason);

    /// <summary>
    /// How the connection ended, as the <c>close</c> event's three attributes —
    /// https://websockets.spec.whatwg.org/#eventdef-websocket-close.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct WebSocketClosure(int Code, string Reason, bool WasClean);
}
#endif

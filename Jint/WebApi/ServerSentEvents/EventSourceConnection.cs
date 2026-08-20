#if NET8_0_OR_GREATER
using System.Buffers;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Constraints;
using Jint.Runtime;
using Jint.WebApi.Fetch;

namespace Jint.WebApi.ServerSentEvents;

/// <summary>
/// How one connection of an <c>EventSource</c> ended, and therefore which of the standard's three algorithms
/// the engine thread has to run.
/// </summary>
internal enum EventSourceOutcome
{
    /// <summary>
    /// The engine took the connection away — <c>close()</c>, a <c>RestoreGlobalSnapshot</c>, or the engine's
    /// own cancellation. Nothing is announced, nothing is fired and nothing reconnects.
    /// </summary>
    Abandoned,

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#fail-the-connection — and "once the user
    /// agent has failed the connection, it does not attempt to reconnect".
    /// </summary>
    Fail,

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#reestablish-the-connection.
    /// </summary>
    Reestablish,
}

/// <summary>
/// One connection of an <c>EventSource</c>: the HTTP request, the read loop that decodes its body, and the
/// generation-stamped jobs that carry what it found back to the engine thread.
/// <para>
/// https://html.spec.whatwg.org/multipage/server-sent-events.html#the-eventsource-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The same threading discipline as <c>FetchOperation</c>, which is not a coincidence.</b> The realm and
/// the event-loop generation are captured <i>here</i>, when the connection is created on the engine thread,
/// and every result crosses back as a job stamped with that generation — so a chunk that arrives after
/// <c>RestoreGlobalSnapshot</c> is discarded at dequeue rather than dispatched into the restored engine.
/// Nothing on the pool thread touches the engine: the read loop hands the parser bytes and the parser answers
/// with plain CLR records.
/// </para>
/// <para>
/// The differences from a fetch are the ones a stream forces. The response body is <b>not</b> buffered, so
/// <c>Options.WebApi.Fetch.MaxResponseBytes</c> cannot bound it and bounds a single event instead. The
/// per-request <c>Options.WebApi.Fetch.Timeout</c> is <b>not</b> armed, because a connection that is idle for
/// an hour is exactly what a server-sent event stream is for. And the operation does not settle once: it
/// delivers, possibly for as long as the engine lives, until something ends it.
/// </para>
/// </remarks>
internal sealed class EventSourceConnection
{
    /// <summary>The MIME type essence a response must carry, https://html.spec.whatwg.org/multipage/server-sent-events.html#text/event-stream.</summary>
    private const string EventStreamMimeType = "text/event-stream";

    private readonly JsEventSource _source;
    private readonly Engine _engine;

    /// <summary>
    /// The realm the connection was opened in, captured at creation for the same reason
    /// <c>FetchOperation</c> captures one: a job running on a later turn would otherwise build its
    /// <c>MessageEvent</c> against whatever realm happened to be ambient.
    /// </summary>
    private readonly Realm _realm;

    /// <summary>The evaluation cycle this connection belongs to; every job it queues carries it.</summary>
    private readonly int _generation;

    /// <summary>
    /// The engine's own cancellation token, from <see cref="CancellationConstraint"/>. A connection cancelled
    /// through it ends silently — a constraint that turned into an <c>error</c> event would let the script
    /// carry on, and reconnect, which is precisely what the constraint exists to stop.
    /// </summary>
    private readonly CancellationToken _engineToken;

    private readonly CancellationTokenSource _cancellation;

    /// <summary>
    /// <see cref="_cancellation"/>'s token, taken once and held. Deliberately not read from the source on
    /// each use: <see cref="Abandon"/> disposes it, and a token read afterwards throws
    /// <see cref="ObjectDisposedException"/> — so a loop that asked again would end in an exception rather
    /// than in the cancellation it was told about, and would do so only in the timing where the abandon
    /// happened to land between two reads.
    /// </summary>
    private readonly CancellationToken _token;

    private readonly EventStreamParser _parser;

    private int _finished;

    internal EventSourceConnection(JsEventSource source, Engine engine, Realm realm, long maxEventLength, string lastEventId)
    {
        _source = source;
        _engine = engine;
        _realm = realm;
        _generation = engine.EventLoopGeneration;
        _engineToken = engine.Constraints.Find<CancellationConstraint>()?.Token ?? CancellationToken.None;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(_engineToken);
        _token = _cancellation.Token;
        _parser = new EventStreamParser(maxEventLength, lastEventId);
    }

    /// <summary>
    /// Starts the request. Returns as soon as the transport goes asynchronous, so the constructor that
    /// ultimately called this does not wait for a socket.
    /// </summary>
    internal void Start(HttpClient client, FetchRequestSnapshot request, FetchPolicy policy)
    {
        // Fire and forget by design: every outcome the loop can reach, including a synchronous throw from a
        // host's own handler, is turned into an engine-thread job inside it.
        _ = RunAsync(client, request, policy);
    }

    private async Task RunAsync(HttpClient client, FetchRequestSnapshot request, FetchPolicy policy)
    {
        try
        {
            using var exchange = await FetchTransport
                .SendForStreamAsync(client, request, policy, _token)
                .ConfigureAwait(false);

            var response = exchange.Response;

            // Constructor step 15.iii: "if res's status is not 200, or if res's Content-Type is not
            // text/event-stream, then fail the connection" — a failure the standard deliberately does not
            // reconnect from, because retrying an answer the server meant would only repeat it.
            if ((int) response.StatusCode != 200 || !IsEventStream(response))
            {
                Finish(EventSourceOutcome.Fail);
                return;
            }

            var origin = exchange.Url.SerializeOrigin();
            Enqueue(() => _source.AnnounceConnection(this));

            await ReadAsync(response, origin).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // close(), a restore, or the engine's own cancellation: the only three things that cancel this
            // token. None of them announces anything, and none of them reconnects.
            Finish(EventSourceOutcome.Abandoned);
        }
        catch (FetchFailureException failure) when (failure.Kind is FetchFailureKind.ResponseTooLarge or FetchFailureKind.PolicyDenied or FetchFailureKind.RedirectLimit)
        {
            // The host's own limits, all three of which a reconnect would run straight back into.
            Finish(EventSourceOutcome.Fail);
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            // Constructor step 15.ii: a network error reestablishes the connection. A DNS failure, a refused
            // connection and a stream the server dropped are the ordinary life of a long-lived stream.
            Finish(EventSourceOutcome.Reestablish);
        }
    }

    /// <summary>
    /// The read loop: "announce the connection and interpret res's body line by line".
    /// </summary>
    private async Task ReadAsync(HttpResponseMessage response, string origin)
    {
        using var stream = await response.Content.ReadAsStreamAsync(_token).ConfigureAwait(false);

        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(), _token).ConfigureAwait(false);
                if (read == 0)
                {
                    // processResponseEndOfBody: "if res is not a network error, then reestablish the
                    // connection". A server closing the stream is how it asks to be polled again.
                    Finish(EventSourceOutcome.Reestablish);
                    return;
                }

                Deliver(buffer, read, origin);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Parses one chunk and queues whatever it produced. A separate method because a <see cref="Span{T}"/>
    /// cannot live in an <see langword="async"/> one, and the parser works in spans.
    /// </summary>
    private void Deliver(byte[] buffer, int count, string origin)
    {
        var messages = new List<EventStreamMessage>();
        _parser.Feed(buffer, count, messages);

        var reconnectionTime = _parser.TakeReconnectionTime();
        if (reconnectionTime is null && messages.Count == 0)
        {
            // A chunk that completed nothing — a comment keeping the connection alive, or half a line.
            return;
        }

        Enqueue(() => _source.DeliverMessages(this, reconnectionTime, messages, origin));
    }

    /// <summary>
    /// https://mimesniff.spec.whatwg.org/#mime-type-essence of the response's <c>Content-Type</c>, compared
    /// ASCII-case-insensitively — so <c>text/event-stream; charset=utf-8</c> is the same type as
    /// <c>text/event-stream</c>.
    /// </summary>
    /// <remarks>
    /// Read through <c>NonValidated</c> and cut by hand rather than through <c>Content.Headers.ContentType</c>,
    /// whose parser answers null for a value it dislikes — which would turn a malformed parameter into a
    /// failed connection. The last value wins, which is what
    /// https://fetch.spec.whatwg.org/#concept-header-extract-mime-type does.
    /// </remarks>
    private static bool IsEventStream(HttpResponseMessage response)
    {
        string? essence = null;

        foreach (var header in response.Content.Headers.NonValidated)
        {
            if (!string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in header.Value)
            {
                var semicolon = value.IndexOf(';');
                var candidate = (semicolon < 0 ? value : value.Substring(0, semicolon)).Trim();
                if (candidate.Length != 0)
                {
                    essence = candidate;
                }
            }
        }

        return essence is not null && string.Equals(essence, EventStreamMimeType, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Queues one engine-thread job carrying this connection's generation. A job whose cycle has ended is
    /// discarded at dequeue — the fence every cross-thread completion in Jint sits behind.
    /// </summary>
    private void Enqueue(Action job)
    {
        if (Volatile.Read(ref _finished) != 0)
        {
            return;
        }

        _engine.AddToEventLoop(() => Run(job), _generation);
    }

    /// <summary>
    /// Ends the connection exactly once, whichever of the terminal paths got here first.
    /// </summary>
    private void Finish(EventSourceOutcome outcome)
    {
        if (Interlocked.CompareExchange(ref _finished, 1, 0) != 0)
        {
            return;
        }

        _engine.AddToEventLoop(() => Complete(outcome), _generation);
    }

    /// <summary>
    /// On the engine thread, in the realm the connection was opened in: the bookkeeping first, so that a
    /// listener throwing out of the event that follows cannot leave the engine holding a connection that has
    /// already ended.
    /// </summary>
    private void Complete(EventSourceOutcome outcome)
    {
        Run(() =>
        {
            _engine._webApi?.UnregisterEventSource(this);
            _cancellation.Dispose();
            _source.OnConnectionEnded(this, outcome);
        });
    }

    /// <summary>
    /// Runs an engine-thread job in the connection's own realm.
    /// </summary>
    /// <remarks>
    /// A listener that throws erupts from whatever is pumping the event loop, which is the contract every
    /// event Jint fires already has — see <c>JsEventTarget</c>. There is no promise here to reject it into.
    /// </remarks>
    private void Run(Action job)
    {
        var entered = EnterRealm();
        try
        {
            job();
        }
        finally
        {
            LeaveRealm(entered);
        }
    }

    /// <summary>
    /// Ends the connection from the engine thread: <c>close()</c>, or the fence a
    /// <c>RestoreGlobalSnapshot</c> puts up. Cancels the request so the socket is freed at once, and settles
    /// nothing — a job already on its way is discarded by the generation fence, and nothing new is queued.
    /// </summary>
    internal void Abandon()
    {
        Interlocked.Exchange(ref _finished, 1);

        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Raced with a completion that already released it; there is nothing left to cancel.
        }

        _cancellation.Dispose();

        // Runs here rather than from a job, because a job is exactly what an abandoned connection may no
        // longer queue. The caller is on the engine thread either way — close() or the restore fence.
        _source.AbandonConnection(this);
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
}
#endif

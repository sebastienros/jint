#if NET8_0_OR_GREATER
using System.Buffers;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Fetch;

/// <summary>
/// A network response body, streamed: the HTTP content on one side and a readable <i>byte</i> stream on the
/// other, joined by demand rather than by a buffer.
/// <para>
/// https://fetch.spec.whatwg.org/#concept-response-body
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The stream is a byte stream</b>, as https://fetch.spec.whatwg.org/#concept-body requires of every
/// body ("set up with byte reading support"), so a consumer may read it BYOB and recycle one buffer across
/// the whole download. No <c>autoAllocateChunkSize</c> is set: the transport reads into a pooled array on
/// its own thread and cannot write into a script's buffer across that boundary, so a BYOB read is served
/// out of the controller's queue like any other. What a default reader sees is unchanged — one
/// <c>Uint8Array</c> per chunk the transport produced.
/// </para>
/// <para>
/// <b>The two halves never touch each other's state directly.</b> The engine half — the underlying source's
/// <c>pull</c> and <c>cancel</c>, and everything that reaches the controller — runs on the engine thread and
/// nowhere else. The transport half is an ordinary <c>async</c> loop on the thread pool that reads at most
/// <see cref="ChunkSize"/> bytes at a time and hands each chunk over as a <b>generation-stamped event-loop
/// job</b>, which is the same fence every other cross-thread completion in Jint sits behind: a chunk that
/// arrives after <c>RestoreGlobalSnapshot</c> is discarded at dequeue rather than enqueued into a stream the
/// restored engine can see.
/// </para>
/// <para>
/// <b>Backpressure is the point.</b> The loop reads nothing until the stream's <c>pull</c> asks, and
/// <c>pull</c>'s promise stays pending until that chunk has been enqueued — so the standard's own
/// <c>[[pulling]]</c> guard bounds the loop to one outstanding read, and a consumer that stops reading stops
/// the socket. The signal between them is a <see cref="SemaphoreSlim"/> released once per pull; nothing
/// larger is needed, because there is never more than one pull outstanding.
/// </para>
/// <para>
/// <b>The size cap is enforced per chunk</b>, on the running total of decompressed bytes, so a body that
/// exceeds <c>Options.WebApi.Fetch.MaxResponseBytes</c> errors the stream at the chunk that crossed the line
/// and drops the connection there. Unlike the buffered implementation this replaces, the cap can no longer
/// reject the <c>fetch</c> promise itself: by the time a single byte of the body has been read, the promise
/// has resolved with the response — which is what the standard prescribes and what a browser does. A
/// <c>Content-Length</c> that already exceeds the cap is still refused before the promise settles, because
/// that is known while the headers are being read.
/// </para>
/// </remarks>
internal sealed class FetchBodyStream : IDisposable
{
    /// <summary>
    /// The most bytes one read takes, and therefore the largest chunk a script sees. 64 KiB is what one pull
    /// answers with when the socket has that much ready; a slower server simply produces smaller chunks.
    /// </summary>
    private const int ChunkSize = 64 * 1024;

    // ---- transport half: touched on the thread pool, never by the engine ----

    private readonly HttpResponseMessage _message;
    private readonly Stream _content;
    private readonly long _maxBytes;

    /// <summary>
    /// Released once per <c>pull</c>. Never more than one release is outstanding, because the standard's own
    /// <c>[[pulling]]</c> guard admits one pull at a time.
    /// </summary>
    private readonly SemaphoreSlim _demand = new(0);

    private long _total;

    // ---- engine half: touched on the engine thread, never by the transport ----

    private Engine _engine = null!;
    private Realm _realm = null!;
    private int _generation;
    private JsReadableStream _stream = null!;
    private CancellationTokenSource _cancellation = null!;
    private PromiseCapability? _pull;
    private bool _finished;

    internal FetchBodyStream(HttpResponseMessage message, Stream content, long maxBytes)
    {
        _message = message;
        _content = content;
        _maxBytes = maxBytes;
    }

    /// <summary>
    /// Lets go of everything the transport half holds. Called for a body that will never be read — a response
    /// whose settle job the generation fence discarded, or one abandoned before its stream existed — and
    /// again from the read loop's own exit, which is why it must be safe to call twice.
    /// </summary>
    public void Dispose()
    {
        _message.Dispose();
        _demand.Dispose();
    }

    /// <summary>
    /// Builds the <c>ReadableStream</c> the response's <c>body</c> attribute answers with and starts the
    /// transport loop behind it. Runs on the engine thread, in the realm the fetch was started in.
    /// </summary>
    /// <param name="engine">The engine whose event loop the chunks are delivered on.</param>
    /// <param name="realm">The realm the fetch started in, which owns the stream and its promises.</param>
    /// <param name="cancellation">
    /// The body's own token source, linked to the request's <c>AbortSignal</c> and to the engine's
    /// cancellation constraint. Owned from here on: the read loop disposes it.
    /// </param>
    /// <param name="remainingTimeout">
    /// What is left of <c>Options.WebApi.Fetch.Timeout</c>, which the standard has no counterpart for and
    /// which is documented as covering the whole request "from the call to the last byte of the body". Kept
    /// CLR-side, like the header half of the deadline, so that an engine nobody pumps still lets go of its
    /// socket.
    /// </param>
    internal JsReadableStream Attach(
        Engine engine,
        Realm realm,
        CancellationTokenSource cancellation,
        TimeSpan? remainingTimeout)
    {
        _engine = engine;
        _realm = realm;
        _generation = engine.EventLoopGeneration;
        _cancellation = cancellation;

        if (remainingTimeout is { } remaining)
        {
            if (remaining <= TimeSpan.Zero)
            {
                _cancellation.Cancel();
            }
            else
            {
                _cancellation.CancelAfter(remaining);
            }
        }

        // A high water mark of zero means the transport is only asked for bytes once a consumer wants them:
        // nothing is read ahead into a queue the script may never drain. CreateReadableByteStream fixes that
        // mark at zero, which is what the default-controller predecessor asked for explicitly.
        _stream = ReadableStreamOperations.CreateReadableByteStream(
            engine, realm, static () => JsValue.Undefined, PullAlgorithm, CancelAlgorithm);

        engine._webApi?.RegisterBodyStream(this);

        // Task.Run rather than a bare call so the first read can never happen on the engine's thread, whatever
        // the demand count happens to be when this runs.
        _ = Task.Run(ReadLoopAsync);

        return _stream;
    }

    /// <summary>
    /// Ends the body because the evaluation cycle it belongs to has ended — see
    /// <c>WebApiEngineState.ResetTransientState</c>. Runs on the engine thread, from
    /// <c>RestoreGlobalSnapshot</c>.
    /// </summary>
    /// <remarks>
    /// The controller is <b>errored</b> rather than left pending: a host holding on to the response across a
    /// restore gets a stream that says it failed instead of one that never produces another byte. The
    /// reactions that erroring schedules carry the ending cycle's generation and are discarded when the
    /// restore bumps it, which is what keeps the previous cycle's continuations out of the restored engine.
    /// </remarks>
    internal void Abandon()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        CancelTransport();

        ReadableByteStreamControllerOperations.Error(
            _stream.ByteController,
            _realm.Intrinsics.TypeError.Construct("Failed to fetch: the engine's globals were restored while the response body was still streaming"));

        ResolvePull();
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#dictdef-underlyingsource — the pull algorithm. Its promise stays
    /// pending until the chunk it asked for has been enqueued, which is what makes the standard's
    /// <c>[[pulling]]</c> guard the backpressure valve for the socket.
    /// </summary>
    private JsPromise PullAlgorithm()
    {
        if (_finished)
        {
            return StreamPromises.ResolvedWithUndefined(_engine, _realm);
        }

        if (_pull is null)
        {
            _pull = StreamPromises.NewPromise(_engine, _realm);
            try
            {
                _demand.Release();
            }
            catch (ObjectDisposedException)
            {
                // The read loop has already exited and let go of the transport; the outcome it posted is on
                // its way through the event loop and will settle this pull.
            }
        }

        return StreamPromises.PromiseOf(_pull);
    }

    /// <summary>
    /// The cancel algorithm: <c>response.body.cancel()</c>, a reader's <c>cancel()</c>, or a pipe that gave
    /// up. The connection goes with it — there is nothing left to read it for.
    /// </summary>
    private JsPromise CancelAlgorithm(JsValue reason)
    {
        Finish();
        return StreamPromises.ResolvedWithUndefined(_engine, _realm);
    }

    /// <summary>
    /// The transport loop. Nothing here touches the engine: every outcome leaves as a generation-stamped
    /// job, and the engine half decides what it means.
    /// </summary>
    private async Task ReadLoopAsync()
    {
        var token = _cancellation.Token;
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);

        try
        {
            while (true)
            {
                await _demand.WaitAsync(token).ConfigureAwait(false);

                int read;
                try
                {
                    read = await _content.ReadAsync(buffer.AsMemory(0, ChunkSize), token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Post(null, new FetchFailureException(FetchFailureKind.Network, "Reading the response body failed: " + ex.Message, ex));
                    return;
                }

                if (read == 0)
                {
                    Post(null, null);
                    return;
                }

                // Counted before the chunk is handed over, so the cap is never overshot by a whole chunk and
                // the connection is dropped as soon as the limit is known to be broken.
                _total += read;
                if (_total > _maxBytes)
                {
                    Post(null, new FetchFailureException(
                        FetchFailureKind.ResponseTooLarge,
                        $"The response body exceeded the {_maxBytes} byte limit set by Options.WebApi.Fetch.MaxResponseBytes."));
                    return;
                }

                Post(buffer.AsSpan(0, read).ToArray(), null);
            }
        }
        catch (OperationCanceledException)
        {
            // The stream was cancelled, the request aborted, the deadline blown or the engine's cycle ended.
            // Each of those has already settled the engine half; there is nothing to report back.
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Dispose();
            _cancellation.Dispose();
        }
    }

    private void Post(byte[]? chunk, FetchFailureException? failure)
    {
        _engine.AddToEventLoop(() => Deliver(chunk, failure), _generation);
    }

    /// <summary>
    /// The engine-thread half of one transport outcome: a chunk, the end of the body, or a failure.
    /// </summary>
    private void Deliver(byte[]? chunk, FetchFailureException? failure)
    {
        if (_finished)
        {
            return;
        }

        if (failure is not null)
        {
            _finished = true;
            _engine._webApi?.UnregisterBodyStream(this);
            ReadableByteStreamControllerOperations.Error(_stream.ByteController, FetchOperation.NetworkError(_realm, failure));
            ResolvePull();
            return;
        }

        if (chunk is null)
        {
            _finished = true;
            _engine._webApi?.UnregisterBodyStream(this);
            ByteStreams.CloseAndReleasePendingByob(_stream.ByteController);
            ResolvePull();
            return;
        }

        // The array is the transport's own copy, taken out of the pooled buffer before it was posted, so
        // nothing else will ever read it and the byte controller can take it as it stands.
        ByteStreams.EnqueueOwnedBytes(_stream.ByteController, chunk);

        // Resolved after the enqueue, so the reaction that lets the controller pull again observes the chunk
        // already in the queue.
        ResolvePull();
    }

    /// <summary>
    /// Ends the body from the engine side, without touching the stream: the caller has already closed,
    /// errored or cancelled it.
    /// </summary>
    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _engine._webApi?.UnregisterBodyStream(this);
        CancelTransport();
        ResolvePull();
    }

    private void CancelTransport()
    {
        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The loop finished and disposed it first; there is nothing left to cancel.
        }
    }

    private void ResolvePull()
    {
        var pull = _pull;
        _pull = null;
        pull?.Resolve(JsValue.Undefined);
    }
}
#endif

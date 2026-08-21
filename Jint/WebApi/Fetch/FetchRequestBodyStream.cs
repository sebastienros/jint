#if NET8_0_OR_GREATER
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.Files;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Fetch;

/// <summary>
/// A request body that only exists as a <c>ReadableStream</c>, streamed to the wire rather than collected
/// first: the standard's <c>duplex: "half"</c>.
/// <para>
/// https://fetch.spec.whatwg.org/#dom-requestinit-duplex — "the user agent sends the entire request before
/// processing the response" — and https://fetch.spec.whatwg.org/#concept-request-body, whose transmission
/// steps are <c>processBodyChunk</c> / <c>processEndOfBody</c> / <c>processBodyError</c>.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// It is <see cref="FetchBodyStream"/> pointing the other way, and obeys the same rule: <b>the two halves
/// never touch each other's state directly</b>. The engine half reads the script's stream on the engine
/// thread, one chunk per read, through an ordinary read request — nothing else may read a
/// <c>ReadableStream</c>. The transport half is an <see cref="HttpContent"/> whose
/// <c>SerializeToStreamAsync</c> runs on whichever thread <see cref="HttpClient"/> gives it. Between them
/// sits a <see cref="Channel{T}"/> of <b>capacity one</b>, which is the whole of the synchronization: a
/// plain CLR object both threads may touch, carrying nothing but byte arrays.
/// </para>
/// <para>
/// <b>Backpressure falls out of that capacity.</b> The engine's write does not complete until the transport
/// has taken the previous chunk, and the next read of the script's stream is not issued until the write
/// completes — so a script writing into a <c>TransformStream</c> faster than the socket drains is stopped by
/// its own <c>desiredSize</c>, and a slow server slows the script rather than filling memory. The engine
/// never blocks waiting for that: the write's continuation comes back as a <b>generation-stamped event-loop
/// job</b>, the same fence every other cross-thread completion in Jint sits behind.
/// </para>
/// <para>
/// <b>Three reductions against a browser, all of them consequences of the transport being
/// <see cref="HttpClient"/>.</b>
/// </para>
/// <list type="number">
/// <item>
/// A body-preserving redirect fails the fetch. That is what the standard itself prescribes —
/// https://fetch.spec.whatwg.org/#http-redirect-fetch step 12: "If internalResponse's status is not 303,
/// request's body is non-null, and request's body's source is null, then return a network error" — because
/// the bytes are gone once sent and cannot be sent again. A 303 is fine: it drops the body with the method.
/// </item>
/// <item>
/// Nothing checks the negotiated HTTP version, where https://fetch.spec.whatwg.org/#concept-http-network-fetch
/// makes an HTTP/1.x connection carrying a null-source body a network error and browsers therefore require
/// HTTP/2. This streams over HTTP/1.1 as <c>Transfer-Encoding: chunked</c>, which is what every server-side
/// HTTP client does and what a server-side embedder wants; the browser rule exists to protect a shared
/// connection pool from a request that occupies it indefinitely, and is deliberately not mirrored.
/// </item>
/// <item>
/// The content can be serialized <b>once</b>. <see cref="HttpClient"/> resends a request in a few situations
/// of its own — a connection closed at exactly the wrong moment, an authentication challenge — and a second
/// serialization is refused rather than silently sending a truncated body. The standard anticipates this too
/// ("if the user agent needs to resend request, then instead return a network error"); a browser's 64 KiB
/// replay buffer is not reproduced.
/// </item>
/// </list>
/// <para>
/// A host handler that reads the content <i>synchronously</i> — <c>HttpContent.ReadAsStream()</c>,
/// <c>ReadAsByteArrayAsync()</c>'s synchronous cousins — gets a <see cref="NotSupportedException"/>, because
/// producing the bytes needs the engine thread to run event-loop turns and blocking the caller for them
/// would deadlock whenever the caller <i>is</i> that thread. The asynchronous members all work.
/// </para>
/// </remarks>
internal sealed class FetchRequestBodyStream : ReadRequest
{
    private readonly Engine _engine;
    private readonly EventLoopRegistration _registration;
    private readonly JsReadableStreamDefaultReader _reader;

    /// <summary>
    /// One chunk in flight, which is what makes the socket's pace the script's pace. Single reader and
    /// single writer: exactly one thread is ever on each end.
    /// </summary>
    private readonly Channel<byte[]> _chunks = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait,
    });

    /// <summary>
    /// Set by the transport half when it will take no more bytes — the request finished, failed, was
    /// aborted, or its message was disposed. Written from the transport's thread and read from the engine's,
    /// hence <c>volatile</c>; it is a hint that the engine half checks at its own next safe point, never a
    /// handshake.
    /// </summary>
    private volatile bool _stopped;

    private bool _reading;
    private bool _readAgain;
    private bool _finished;

    internal FetchRequestBodyStream(Engine engine, JsReadableStream stream)
    {
        _engine = engine;
        _registration = engine.CaptureEventLoopRegistration();

        // The fetch takes the body: the stream is locked and disturbed from here on, exactly as a buffered
        // upload's full read left it. The caller has already ruled out an unusable body.
        _reader = ReadableStreamOperations.AcquireDefaultReader(stream);

        Content = new RequestBodyContent(this);
    }

    /// <summary>
    /// What goes on the <see cref="HttpRequestMessage"/>. Plain CLR state: the transport touches this and
    /// the channel behind it, and no part of the engine.
    /// </summary>
    internal HttpContent Content { get; }

    /// <summary>
    /// Starts reading the script's stream. Called on the engine thread, before the request is handed to the
    /// transport, so that the first chunk is usually already waiting when the socket asks for it.
    /// </summary>
    internal void Start() => Run();

    /// <summary>
    /// Ends the body because the evaluation cycle it belongs to has ended — see
    /// <c>WebApiEngineState.ResetTransientState</c> — or because the fetch settled without the transport
    /// having consumed the body. Runs on the engine thread.
    /// </summary>
    internal void Abandon()
    {
        _finished = true;
        _chunks.Writer.TryComplete(new FetchFailureException(
            FetchFailureKind.Network,
            "The request body stream ended because the fetch was abandoned."));

        StopProducing();
    }

    /// <summary>
    /// Tells the engine half that nothing will read another byte. Safe from any thread and any number of
    /// times.
    /// </summary>
    /// <remarks>
    /// The flag alone is not enough: the engine half may be parked on a write into a channel that is full,
    /// and only a read makes room. So the queue is drained here — the chunk it holds is going nowhere — and
    /// the write that was waiting for that space completes, sees the flag, and lets the read loop end. The
    /// two orders are both fine: a write that arrives after the drain finds the channel empty and completes
    /// at once.
    /// </remarks>
    internal void StopProducing()
    {
        _stopped = true;

        while (_chunks.Reader.TryRead(out _))
        {
            // Discarded: there is no longer anything to send it to.
        }
    }

    /// <summary>
    /// The read loop. The standard writes it as recursion — each chunk's steps read again — which for a
    /// stream whose queue is already full would recurse once per queued chunk. This is
    /// <c>FetchBody.FullyReadRequest</c>'s loop with the same reentrancy pair: a chunk delivered
    /// <i>inside</i> the read sets <c>readAgain</c> and the loop goes round, while one delivered later
    /// starts a fresh loop of its own.
    /// </summary>
    private void Run()
    {
        do
        {
            _readAgain = false;
            _reading = true;
            try
            {
                ReadableStreamOperations.DefaultReaderRead(_reader, this);
            }
            finally
            {
                _reading = false;
            }
        }
        while (_readAgain && !_finished);
    }

    internal override void ChunkSteps(JsValue chunk)
    {
        if (_finished || _stopped)
        {
            _finished = true;
            return;
        }

        // "If chunk is not a Uint8Array object, terminate" — a stream carrying strings or plain objects is
        // not a body, and saying so beats coercing it into one.
        if (chunk is not JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 })
        {
            Fail("The request body stream produced a chunk that is not a Uint8Array.", releaseReader: true);
            return;
        }

        if (!FileApi.TryGetBufferSourceBytes(chunk, out var bytes) || bytes.IsEmpty)
        {
            // A zero-length chunk transmits nothing; reading on is the whole of its handling.
            ReadAgain();
            return;
        }

        // The array leaves the engine thread, so it is the engine's copy and nothing script holds.
        var write = _chunks.Writer.WriteAsync(bytes.ToArray());

        if (write.IsCompletedSuccessfully)
        {
            // The transport was already waiting, so the whole hand-over happened on this stack.
            OnWriteCompleted(succeeded: true);
            return;
        }

        _ = write.AsTask().ContinueWith(
            static (task, state) =>
            {
                var self = (FetchRequestBodyStream) state!;

                // The generation stamp is what stops a chunk written before Engine.Advanced
                // .RestoreGlobalSnapshot from resuming a read against the restored engine.
                self._engine.AddToEventLoop(
                    () => self.OnWriteCompleted(task.IsCompletedSuccessfully),
                    self._registration);
            },
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal override void CloseSteps()
    {
        // processEndOfBody: the body is complete, so the transport's loop ends and the request finishes.
        _finished = true;
        _chunks.Writer.TryComplete();
    }

    internal override void ErrorSteps(JsValue error)
    {
        // processBodyError: "terminate fetchParams's controller", which surfaces as a network error rather
        // than as the stream's own error — the fetch promise has the standard's one TypeError either way.
        // The error value itself is deliberately not stringified: doing that can run script.
        Fail("The request body stream errored.", releaseReader: false);
    }

    /// <summary>
    /// Called on the engine thread when the write the loop was parked on has settled. A cancelled or failed
    /// write means the transport has gone; there is nothing left to read the stream for.
    /// </summary>
    private void OnWriteCompleted(bool succeeded)
    {
        if (_finished)
        {
            return;
        }

        if (!succeeded || _stopped)
        {
            _finished = true;
            return;
        }

        ReadAgain();
    }

    private void ReadAgain()
    {
        if (_reading)
        {
            _readAgain = true;
            return;
        }

        Run();
    }

    private void Fail(string message, bool releaseReader)
    {
        _finished = true;

        if (releaseReader)
        {
            // The standard's failure steps for a non-Uint8Array chunk release the reader's lock before
            // reporting, so the stream is left usable rather than pinned by a reader nobody holds.
            ReadableStreamOperations.DefaultReaderRelease(_reader);
        }

        _chunks.Writer.TryComplete(new FetchFailureException(FetchFailureKind.Network, message));
    }

    /// <summary>
    /// The transport half: an <see cref="HttpContent"/> of unknown length whose bytes arrive from the
    /// channel. Unknown length is what makes <see cref="HttpClient"/> send
    /// <c>Transfer-Encoding: chunked</c> rather than wait for a <c>Content-Length</c> nobody can compute.
    /// </summary>
    private sealed class RequestBodyContent : HttpContent
    {
        private readonly FetchRequestBodyStream _owner;
        private int _serialized;

        internal RequestBodyContent(FetchRequestBodyStream owner)
        {
            _owner = owner;
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => WriteAsync(stream, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
            => WriteAsync(stream, cancellationToken);

        private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _serialized, 1) != 0)
            {
                // A resend. The bytes are gone, and half a body is worse than none.
                throw new FetchFailureException(
                    FetchFailureKind.Network,
                    "The request body is a ReadableStream and has already been sent, so the request cannot be sent again.");
            }

            var reader = _owner._chunks.Reader;

            // A channel the engine half completed with a reason raises it from here as the inner exception
            // of a ChannelClosedException, which the transport turns into a network error like any other
            // send failure — and which the host can still read off the error value.
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var chunk))
                {
                    await stream.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            // Every hop disposes its request message, so this runs once the body has been sent — and also
            // when the send failed, or when a handler answered without reading the body at all. In all three
            // the engine half has nothing left to produce for.
            _owner.StopProducing();
            base.Dispose(disposing);
        }
    }
}
#endif

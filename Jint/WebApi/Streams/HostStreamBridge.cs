#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.WebApi.Files;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Streams;

/// <summary>
/// The machinery every bridge between a WHATWG stream and a <see cref="Stream"/> shares: the cycle the
/// bridge belongs to, the cancellation source its I/O runs under, the one-shot release of the host's stream,
/// and the two conversions between a chunk and a byte sequence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two threads meet here and the split is always the same one.</b> Everything that touches the engine —
/// a controller, a promise, a typed array — happens on the engine's thread, from a generation-stamped
/// event-loop job. Everything that touches the host's <see cref="Stream"/> happens wherever the BCL's
/// asynchronous I/O completes, and produces nothing but plain CLR data. That is the same division
/// <c>FetchOperation</c> and <c>ModuleLoadCompletion</c> are built on, and for the same reason: the engine
/// is not thread-safe, and a stream chunk arriving on a thread-pool thread is exactly the shape that would
/// otherwise corrupt it.
/// </para>
/// <para>
/// <b>The generation is captured when the bridge is created, never read at completion time.</b> A chunk
/// whose read finished after <c>Engine.Advanced.RestoreGlobalSnapshot</c> ended the cycle is discarded at
/// dequeue rather than delivered into the restored engine — the fence every other cross-thread completion in
/// Jint sits behind. The restore additionally calls <see cref="Abandon"/>, so the host's stream is closed at
/// once instead of being held until a garbage collection notices.
/// </para>
/// <para>
/// <b>The host's stream is released exactly once.</b> A release must never happen while an asynchronous read
/// or write is still writing into a buffer, so the release is owed to whichever of the two racers observes
/// the other: <see cref="FinishBridge"/> takes it when nothing is in flight, and <see cref="EndOperation"/>
/// takes it when the bridge was finished while an operation ran.
/// </para>
/// </remarks>
internal abstract class HostStreamBridge
{
    /// <summary>No I/O is in flight and the bridge may start more.</summary>
    private const int Idle = 0;

    /// <summary>One read or write is in flight.</summary>
    private const int Busy = 1;

    /// <summary>The bridge was finished while an operation was in flight; that operation owes the release.</summary>
    private const int Releasing = 2;

    /// <summary>The host's stream has been released, or is being released by the caller that took it.</summary>
    private const int Released = 3;

    private int _state;
    private EventLoopRegistration _operationRegistration;

    protected HostStreamBridge(Engine engine, Realm realm, Stream stream, bool leaveOpen, CancellationToken hostCancellation)
    {
        Engine = engine;
        Realm = realm;
        HostStream = stream;
        LeaveOpen = leaveOpen;
        Generation = engine.EventLoopGeneration;
        Cancellation = CancellationTokenSource.CreateLinkedTokenSource(hostCancellation);
    }

    protected Engine Engine { get; }

    /// <summary>
    /// The realm the bridge was created in, captured rather than read back from the engine: every promise,
    /// every chunk and every error it mints belongs to that realm, and a job running on a later turn would
    /// otherwise build them against whichever realm happens to be ambient then.
    /// </summary>
    protected Realm Realm { get; }

    /// <summary>The host's stream. Touched only from asynchronous I/O and from the one release.</summary>
    protected Stream HostStream { get; }

    /// <summary>Whether the host keeps ownership of <see cref="HostStream"/> — see the options types.</summary>
    protected bool LeaveOpen { get; }

    /// <summary>The evaluation cycle this bridge belongs to. See the remarks on the class.</summary>
    protected int Generation { get; }

    protected EventLoopRegistration OperationRegistration => _operationRegistration;

    /// <summary>
    /// The token every read and write runs under. Cancelled by <see cref="FinishBridge"/>, so a stream that
    /// is blocked in a read stops being blocked when the script cancels or the engine is restored.
    /// </summary>
    protected CancellationTokenSource Cancellation { get; }

    /// <summary>
    /// Whether the bridge is done with the host's stream. Read on the engine thread only, to prune the
    /// engine's registry.
    /// </summary>
    internal bool IsReleased => Volatile.Read(ref _state) == Released;

    /// <summary>
    /// Claims the right to start one read or write. <see langword="false"/> means the bridge has been
    /// finished — cancelled, closed, or abandoned by a restore — and no further I/O may be started.
    /// </summary>
    protected bool TryBeginOperation()
    {
        if (Interlocked.CompareExchange(ref _state, Busy, Idle) != Idle)
        {
            return false;
        }

        _operationRegistration = Engine.CaptureEventLoopRegistration();
        return true;
    }

    /// <summary>
    /// Ends the in-flight read or write. Called on whichever thread the I/O completed on, <b>before</b> its
    /// result is enqueued for the engine: a job may be discarded by the generation fence, so a release owed
    /// to it would never be paid.
    /// </summary>
    /// <returns>Whether this call owes <see cref="ReleaseHostStream"/>.</returns>
    protected bool EndOperation()
    {
        if (Interlocked.CompareExchange(ref _state, Idle, Busy) == Busy)
        {
            return false;
        }

        // Only Releasing can be observed here: the bridge was finished while this operation ran, and the
        // finisher deliberately left the release to whoever was using the buffer.
        Volatile.Write(ref _state, Released);
        return true;
    }

    /// <summary>
    /// Refuses every later read and write and cancels any in flight.
    /// </summary>
    /// <returns>
    /// Whether this call owes <see cref="ReleaseHostStream"/>. <see langword="false"/> either because an
    /// operation is in flight — <see cref="EndOperation"/> will take it — or because the bridge was already
    /// finished.
    /// </returns>
    protected bool FinishBridge()
    {
        while (true)
        {
            var state = Volatile.Read(ref _state);
            if (state is Releasing or Released)
            {
                return false;
            }

            var next = state == Busy ? Releasing : Released;
            if (Interlocked.CompareExchange(ref _state, next, state) != state)
            {
                continue;
            }

            // Reached at most once per bridge, and always before the cancellation source is disposed: every
            // later FinishBridge returns above.
            Cancellation.Cancel();
            return next == Released;
        }
    }

    /// <summary>
    /// Releases what the bridge owns. Called exactly once, by whichever of the two racers
    /// <see cref="FinishBridge"/> and <see cref="EndOperation"/> was told it owes it, and therefore never
    /// while a read or write is still using the stream.
    /// </summary>
    /// <remarks>
    /// A failure to close is swallowed, deliberately: this is the path a cancellation, an error and an
    /// abandonment take, all of which already have their own outcome, and none of which has anywhere left to
    /// report a second one. The path where closing the stream <i>is</i> the outcome — a writable stream's
    /// <c>close()</c>, whose promise is the script's proof that the bytes reached the disk — does not come
    /// through here: <see cref="HostWritableStreamSink"/> flushes and disposes itself and settles that
    /// promise with whatever happened.
    /// </remarks>
    protected void ReleaseHostStream()
    {
        Cancellation.Dispose();

        if (LeaveOpen)
        {
            return;
        }

        try
        {
            HostStream.Dispose();
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            // Nothing to report it to. See the remarks.
        }
    }

    /// <summary>
    /// Abandons the bridge because the evaluation cycle it belongs to has ended. Runs on the engine thread,
    /// from <c>Engine.ResetTransientEvaluationState</c>.
    /// </summary>
    /// <remarks>
    /// The host's stream is closed at once rather than merely forgotten — the generation fence already stops
    /// a chunk reaching the restored engine, but forgetting the bridge would hold a file handle open until a
    /// finalizer noticed. Whatever promise the stream had outstanding stays pending forever, which is the
    /// contract every completion registered before a restore has.
    /// </remarks>
    internal virtual void Abandon()
    {
        if (FinishBridge())
        {
            ReleaseHostStream();
        }
    }

    /// <summary>
    /// Queues the engine-thread half of an I/O completion, carrying the cycle the bridge was created in.
    /// </summary>
    protected void Enqueue(Action job, EventLoopRegistration registration)
        => Engine.AddToEventLoop(job, registration);

    /// <summary>
    /// The chunk a host stream's bytes are delivered to script as: a fresh <c>Uint8Array</c> over a copy,
    /// which is what every WHATWG byte-producing stream hands a default reader.
    /// </summary>
    protected JsTypedArray CreateChunk(ReadOnlySpan<byte> bytes) => ByteStreams.NewUint8Array(Engine, Realm, bytes);

    /// <summary>
    /// The bytes a chunk written by script carries, or <see langword="false"/> when it is not something a
    /// byte sink can write at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Streams Standard says nothing about what a chunk is — <c>chunk</c> is <c>any</c>, and what a sink
    /// accepts is the sink's business (https://streams.spec.whatwg.org/#underlying-sink-write). This one
    /// accepts the three things that already <i>are</i> a byte sequence: a <c>BufferSource</c>, a
    /// <c>Blob</c>, and a string, which is UTF-8 encoded. That is the same union
    /// <c>FileSystemWritableFileStream</c> takes
    /// (https://fs.spec.whatwg.org/#typedefdef-filesystemwritechunktype).
    /// </para>
    /// <para>
    /// Everything else is refused rather than coerced. <c>ToString</c> on an arbitrary object would turn a
    /// programming mistake — writing the object instead of its <c>buffer</c> — into
    /// <c>[object Object]</c> silently appended to the host's file.
    /// </para>
    /// <para>
    /// A <c>SharedArrayBuffer</c> is refused with everything else, because
    /// <see cref="FileApi.TryGetBufferSourceBytes"/> implements the plain <c>BufferSource</c> typedef rather
    /// than <c>[AllowShared]</c>: another agent may write to it while this one is copying, and the bytes that
    /// reach the host would then be neither of the two states script could observe.
    /// </para>
    /// <para>
    /// The span is a window onto storage script still owns, so the caller must copy out of it before
    /// returning to the event loop. Every caller here does, into a pooled buffer.
    /// </para>
    /// </remarks>
    protected static bool TryGetChunkBytes(JsValue chunk, out ReadOnlySpan<byte> bytes)
    {
        if (FileApi.TryGetBufferSourceBytes(chunk, out bytes))
        {
            return true;
        }

        if (chunk is JsBlob blob)
        {
            bytes = blob.Data.Span;
            return true;
        }

        if (chunk is JsString text)
        {
            // Every unpaired surrogate becomes U+FFFD, which is what a UTF-8 encode of a USVString does.
            bytes = SystemEncoding.UTF8.GetBytes(text.ToString());
            return true;
        }

        bytes = default;
        return false;
    }

    /// <summary>
    /// Off the engine thread: what a finished I/O task failed with, or <see langword="null"/> for one that
    /// succeeded. Plain CLR data, because nothing here may touch the engine.
    /// </summary>
    protected static Exception? ClassifyFailure(Task task)
    {
        if (task.IsCanceled)
        {
            // The token is only ever cancelled by FinishBridge, so this is a stream the script cancelled or a
            // restore abandoned. The outcome is not wanted either way.
            return new OperationCanceledException();
        }

        if (!task.IsFaulted)
        {
            return null;
        }

        var exception = task.Exception;
        if (exception is null)
        {
            return new IOException("The host stream operation failed.");
        }

        // The AggregateException wrapper says nothing a host can use; the I/O failure itself is the single
        // inner exception in every ordinary case.
        return exception.InnerExceptions.Count == 1 ? exception.InnerExceptions[0] : exception;
    }

    /// <summary>
    /// The error value a CLR failure on the host's stream becomes: a <c>TypeError</c> whose message names the
    /// exception's type but not its text.
    /// </summary>
    /// <remarks>
    /// The message is deliberately thin for the same reason <c>fetch</c>'s network error is: a path, a
    /// share name or a permission message in an <see cref="IOException"/> tells a script things about the
    /// host it has no business learning. The originating exception rides the error <i>value</i>, so the host
    /// reads it with <c>JintException.TryGetClrException</c> while the script cannot see it at all.
    /// </remarks>
    protected JsValue HostStreamError(string operation, Exception failure)
    {
        var message = $"The host stream failed while {operation} ({failure.GetType().Name}).";
        return new JavaScriptException(Realm.Intrinsics.TypeError, message, failure).Error;
    }
}
#endif

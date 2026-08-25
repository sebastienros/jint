#if NET8_0_OR_GREATER
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Streams;

/// <summary>
/// Reads a script's <c>ReadableStream</c> to its end and writes every chunk to a host <see cref="Stream"/>:
/// the engine's side of <c>Engine.WebApi.StartReadableStreamCopy</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the direction with the awkward half</b>, and the awkwardness is who runs the turns. The chunks
/// come out of the engine, so every read is an engine-thread act driven by the event loop, while every write
/// leaves the engine entirely. The copy therefore never blocks and never pumps: it makes progress exactly
/// when the engine is given a turn, and the host either gives it turns itself
/// (<c>engine.Tasks.ProcessTasks()</c> plus <see cref="HostStreamCopyOperation.IsCompleted"/>) or lets
/// <c>Engine.WebApi.CopyReadableStreamAsync</c> do it while it awaits. That is the same contract, and the
/// same pair of entry points, as <c>Engine.Modules.StartImport</c> and <c>ImportAsync</c>.
/// </para>
/// <para>
/// <b>Exactly one read and one write are outstanding at a time</b>, and the next read is issued only once the
/// previous write has landed, which is the whole of the backpressure: a script producing faster than the disk
/// accepts sees its own stream's queue fill and its <c>pull()</c> stop being called. The next read is always
/// issued from a fresh event-loop job rather than from the write's completion, so a source whose chunks are
/// already queued and a destination that writes synchronously — a <see cref="MemoryStream"/> on both ends —
/// cannot turn the copy into unbounded recursion.
/// </para>
/// <para>
/// <b>The source is locked for the copy's duration</b>, exactly as <c>pipeTo</c> locks it, and the reader is
/// released however the copy ends. A copy that ends early also cancels the source unless
/// <c>HostStreamCopyOptions.PreventCancel</c> says otherwise.
/// </para>
/// </remarks>
internal sealed class HostStreamCopy : HostStreamBridge
{
    private readonly HostStreamCopyOperation _operation;
    private readonly PromiseCapability _capability;
    private readonly bool _preventCancel;
    private readonly EventLoopRegistration _registration;

    private JsReadableStreamDefaultReader? _reader;
    private CancellationTokenRegistration _cancellationRegistration;
    private bool _settled;

    private HostStreamCopy(
        Engine engine,
        Realm realm,
        Stream destination,
        HostStreamCopyOptions options,
        PromiseCapability capability,
        HostStreamCopyOperation operation,
        CancellationToken cancellationToken)
        : base(engine, realm, destination, options.LeaveOpen, cancellationToken)
    {
        _preventCancel = options.PreventCancel;
        _capability = capability;
        _operation = operation;
        _registration = engine.CaptureEventLoopRegistration();
    }

    /// <summary>
    /// Starts the copy. Runs on the engine thread and returns before the first chunk has been read.
    /// </summary>
    internal static HostStreamCopyOperation Start(
        Engine engine,
        Realm realm,
        JsReadableStream source,
        Stream destination,
        HostStreamCopyOptions options,
        CancellationToken cancellationToken)
    {
        var capability = StreamPromises.NewPromise(engine, realm);
        var promise = StreamPromises.PromiseOf(capability);

        // The operation object is the channel a host reads the outcome from, so an unobserved promise
        // rejection here is not a lost failure and must not be reported as one.
        StreamPromises.MarkHandled(promise);

        var operation = new HostStreamCopyOperation(engine, promise);
        var copy = new HostStreamCopy(engine, realm, destination, options, capability, operation, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            // Nothing is started at all: no reader is acquired, so the script's stream is not even locked,
            // and the destination is released exactly as it would be for a copy cancelled later.
            copy.FailWith(copy.AbortError("The stream copy was cancelled before it started."));
            return operation;
        }

        try
        {
            copy._reader = ReadableStreamOperations.AcquireDefaultReader(source);
        }
        catch (JavaScriptException exception)
        {
            // The stream is already locked — by another copy, by a `getReader()` the script still holds, or
            // by a `pipeTo` in progress. A rejection rather than a throw, so that host code written to the
            // poll-then-GetResult pattern does not have to guard the start call as well.
            copy.FailWith(exception.Error);
            return operation;
        }

        engine.RegisterHostStreamBridge(copy);

        // Registered after the reader, so the callback can never run against a half-built copy. It fires on
        // whichever thread cancels, and does nothing there but queue the engine-thread half.
        copy._cancellationRegistration = cancellationToken.Register(static state =>
        {
            var pending = (HostStreamCopy) state!;
            pending.Enqueue(pending.CancelFromToken, pending._registration);
        }, copy);

        copy.ReadNext();
        return operation;
    }

    /// <summary>
    /// Asks the source for one chunk. Engine thread.
    /// </summary>
    private void ReadNext()
    {
        if (_settled)
        {
            return;
        }

        ReadableStreamOperations.DefaultReaderRead(_reader!, new CopyReadRequest(this));
    }

    /// <summary>
    /// One chunk arrived. Engine thread.
    /// </summary>
    private void OnChunk(JsValue chunk)
    {
        if (_settled)
        {
            return;
        }

        if (!TryGetChunkBytes(chunk, out var bytes))
        {
            FailWith(Realm.Intrinsics.TypeError.Construct(
                $"A host stream can only be written a BufferSource, a Blob or a string, and this chunk is of type '{chunk.Type}'."));
            return;
        }

        if (bytes.IsEmpty)
        {
            // Nothing to write, and the loop still has to go round through the event loop: a stream of empty
            // chunks must not become recursion.
            Enqueue(ReadNext, Engine.CaptureEventLoopRegistration());
            return;
        }

        if (!TryBeginOperation(out var registration))
        {
            // Abandoned by a restore between the read and here.
            return;
        }

        // Copied out of script-visible storage before the write can start; see HostWritableStreamSink.Write.
        var count = bytes.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(count);
        bytes.CopyTo(buffer);

        ValueTask write;
        try
        {
            write = HostStream.WriteAsync(buffer.AsMemory(0, count), Cancellation.Token);
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            ArrayPool<byte>.Shared.Return(buffer);
            OnWriteSettled(exception, count);
            return;
        }

        if (write.IsCompleted)
        {
            Exception? failure = null;
            try
            {
                write.GetAwaiter().GetResult();
            }
            catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
            {
                failure = exception;
            }

            ArrayPool<byte>.Shared.Return(buffer);
            OnWriteSettled(failure, count);
            return;
        }

        _ = write.AsTask().ContinueWith(
            completed =>
            {
                ArrayPool<byte>.Shared.Return(buffer);
                var failure = ClassifyFailure(completed);
                if (EndOperation())
                {
                    ReleaseHostStream();
                    return;
                }

                Enqueue(
                    () => OnWriteSettled(failure, count, operationEnded: true),
                    registration);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// One write finished. Engine thread.
    /// </summary>
    private void OnWriteSettled(
        Exception? failure,
        int count,
        bool operationEnded = false)
    {
        if (!operationEnded && EndOperation())
        {
            // Abandoned while the write ran.
            ReleaseHostStream();
            return;
        }

        if (failure is not null)
        {
            FailWith(HostStreamError("writing", failure));
            return;
        }

        _operation.Advance(count);

        // Always a fresh job rather than a direct call: see the class remarks on recursion.
        Enqueue(ReadNext, Engine.CaptureEventLoopRegistration());
    }

    /// <summary>
    /// The source ran out of chunks. Engine thread: flush the destination and settle.
    /// </summary>
    private void OnSourceClosed()
    {
        if (_settled)
        {
            return;
        }

        if (!TryBeginOperation(out var registration))
        {
            return;
        }

        var task = FlushAndDisposeAsync();

        if (task.IsCompleted)
        {
            Exception? failure = null;
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
            {
                failure = exception;
            }

            OnFlushSettled(failure);
            return;
        }

        _ = task.AsTask().ContinueWith(
            completed =>
            {
                var failure = ClassifyFailure(completed);
                if (EndOperation())
                {
                    ReleaseHostStream();
                    return;
                }

                Enqueue(
                    () => OnFlushSettled(failure, operationEnded: true),
                    registration);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async ValueTask FlushAndDisposeAsync()
    {
        await HostStream.FlushAsync(Cancellation.Token).ConfigureAwait(false);

        if (!LeaveOpen)
        {
            await HostStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void OnFlushSettled(Exception? failure, bool operationEnded = false)
    {
        var owesRelease = !operationEnded && EndOperation();

        if (FinishBridge())
        {
            owesRelease = true;
        }

        if (owesRelease)
        {
            ReleaseHostStream();
        }

        if (failure is not null)
        {
            FailWith(HostStreamError("closing", failure), releaseDestination: false);
            return;
        }

        // The source is already closed, so there is nothing to cancel — only the lock to give back.
        Settle();
        ReleaseReader();
        _capability.Resolve(JsValue.Undefined);
        _operation.Fulfil();
    }

    /// <summary>
    /// The source errored. Engine thread.
    /// </summary>
    private void OnSourceErrored(JsValue error)
    {
        // The source cannot be cancelled: it is already errored, and ReadableStreamCancel on an errored
        // stream answers its stored error rather than running the underlying source's cancel.
        FailWith(error);
    }

    /// <summary>
    /// The host's cancellation token fired. Engine thread, from a generation-stamped job.
    /// </summary>
    private void CancelFromToken() => FailWith(AbortError("The stream copy was cancelled."));

    private JsDomException AbortError(string message)
        => Realm.Intrinsics.DomException.CreateException(DomExceptionNames.Abort, message);

    /// <summary>
    /// Ends the copy with a failure. Engine thread.
    /// </summary>
    private void FailWith(JsValue error, bool releaseDestination = true)
    {
        if (_settled)
        {
            return;
        }

        // Marked settled first, because cancelling the source below closes it, and closing a stream with an
        // outstanding read request runs that request's close steps — which are this copy's own, and would
        // otherwise try to finish the copy successfully on the way out of failing it.
        Settle();

        if (releaseDestination && FinishBridge())
        {
            // No flush: a copy that failed has no business pushing a partial buffer at the host.
            ReleaseHostStream();
        }

        CancelSource(error);
        ReleaseReader();

        _capability.Reject(error);
        _operation.Fail(error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-pipeTo — the <c>preventCancel</c> half of the piping rules: a
    /// destination that stopped reading tells the source so, unless the caller asked it not to.
    /// </summary>
    private void CancelSource(JsValue reason)
    {
        // Only a still-readable stream is worth cancelling. ReadableStreamCancel on a closed one is a no-op
        // and on an errored one answers a rejected promise nobody asked for — neither is a cancellation, and
        // the standard's own piping does not attempt them either.
        if (_preventCancel || _reader?.Stream is not { State: ReadableStreamState.Readable })
        {
            return;
        }

        var cancelled = ReadableStreamOperations.ReaderGenericCancel(_reader, reason);

        // Whatever the underlying source's cancel() answers is nobody's business here, and a rejection it
        // produces is not this copy's failure — the copy already has one.
        StreamPromises.MarkHandled(cancelled);
    }

    private void ReleaseReader()
    {
        if (_reader?.Stream is null)
        {
            return;
        }

        ReadableStreamOperations.DefaultReaderRelease(_reader);
    }

    private void Settle()
    {
        _settled = true;
        _cancellationRegistration.Dispose();
    }

    /// <inheritdoc />
    internal override void Abandon()
    {
        base.Abandon();

        // The operation reports itself completed-and-faulted from the engine's generation, so nothing has to
        // be pushed here; what does have to happen is that the token registration lets go, and that a job
        // enqueued before the fence went up cannot start another read if it somehow runs.
        Settle();
    }

    /// <summary>
    /// The read request the copy loop makes, one per chunk.
    /// </summary>
    private sealed class CopyReadRequest : ReadRequest
    {
        private readonly HostStreamCopy _copy;

        internal CopyReadRequest(HostStreamCopy copy)
        {
            _copy = copy;
        }

        internal override void ChunkSteps(JsValue chunk) => _copy.OnChunk(chunk);

        internal override void CloseSteps() => _copy.OnSourceClosed();

        internal override void ErrorSteps(JsValue error) => _copy.OnSourceErrored(error);
    }
}
#endif

#if NET8_0_OR_GREATER
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>WritableStream</c> whose underlying sink is a host <see cref="Stream"/>: the engine's side of
/// <c>Engine.WebApi.CreateWritableStream</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Backpressure is the write promise.</b> The standard's writable queue only advances when the sink's
/// <c>write()</c> promise settles (https://streams.spec.whatwg.org/#writable-stream-default-controller-advance-queue-if-needed),
/// and <c>writer.ready</c> is what a script awaits to feel it. So a script writing faster than the host's
/// disk simply waits on <c>ready</c>, and the queue never grows past
/// <c>HostWritableStreamOptions.HighWaterMark</c> chunks.
/// </para>
/// <para>
/// <b>The three sink algorithms never overlap</b> — the standard marks a write or a close in flight and
/// refuses to start anything else until it settles, and it will not run <c>abort()</c> while either is in
/// flight (https://streams.spec.whatwg.org/#writable-stream-finish-erroring). The one thing that <i>can</i>
/// arrive mid-write is a restore's <c>Abandon</c>, which is what the bridge's state machine is for.
/// </para>
/// <para>
/// <b><c>close()</c> is the script's proof the bytes landed.</b> Its promise settles only once the host's
/// stream has been flushed and — unless the host kept ownership — disposed, and a failure in either is what
/// the promise rejects with. That is the one path where a failure to close is reported rather than swallowed.
/// </para>
/// </remarks>
internal sealed class HostWritableStreamSink : HostStreamBridge
{
    private HostWritableStreamSink(Engine engine, Realm realm, Stream destination, HostWritableStreamOptions options)
        : base(engine, realm, destination, options.LeaveOpen, CancellationToken.None)
    {
    }

    /// <summary>
    /// Builds the stream. Runs on the engine thread; nothing is written until script writes.
    /// </summary>
    internal static JsWritableStream Create(Engine engine, Realm realm, Stream destination, HostWritableStreamOptions options)
    {
        var bridge = new HostWritableStreamSink(engine, realm, destination, options);

        var stream = WritableStreamOperations.CreateWritableStream(
            engine,
            realm,
            static () => JsValue.Undefined,
            bridge.Write,
            bridge.Close,
            _ => bridge.Abort(),
            options.HighWaterMark,
            static _ => 1);

        engine.RegisterHostStreamBridge(bridge);
        return stream;
    }

    /// <summary>
    /// The underlying sink's <c>write()</c>: https://streams.spec.whatwg.org/#dom-underlyingsink-write.
    /// </summary>
    private JsPromise Write(JsValue chunk)
    {
        var capability = StreamPromises.NewPromise(Engine, Realm);
        var promise = StreamPromises.PromiseOf(capability);

        if (!TryGetChunkBytes(chunk, out var bytes))
        {
            capability.Reject(Realm.Intrinsics.TypeError.Construct(
                $"A host stream can only be written a BufferSource, a Blob or a string, and this chunk is of type '{chunk.Type}'."));
            return promise;
        }

        if (!TryBeginOperation(out var registration))
        {
            // Only reachable for a bridge a restore has abandoned — the standard's own machinery stops a
            // write to a closed, closing or errored stream long before it reaches the sink.
            capability.Reject(Realm.Intrinsics.TypeError.Construct(
                "The host stream was released: Engine.Advanced.RestoreGlobalSnapshot ended the evaluation cycle it was created in."));
            return promise;
        }

        if (bytes.IsEmpty)
        {
            // Nothing to hand the host, and an empty WriteAsync is not free — it still costs a task and, for
            // a network stream, potentially a zero-length frame.
            EndOperationAndReleaseIfOwed();
            capability.Resolve(JsValue.Undefined);
            return promise;
        }

        // The chunk is copied out of script-visible storage here, on the engine thread, before anything can
        // observe the write. A buffer script keeps writing to while the host reads it would put bytes on the
        // disk that were never any state the script could see.
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
            SettleWrite(exception, capability);
            return promise;
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
            SettleWrite(failure, capability);
            return promise;
        }

        var task = write.AsTask();
        _ = task.ContinueWith(
            completed =>
            {
                // Returned here rather than from the engine-thread job: the job may be discarded by the
                // generation fence, and a buffer returned to the pool by nobody is a buffer the pool has
                // permanently lost.
                ArrayPool<byte>.Shared.Return(buffer);
                var failure = ClassifyFailure(completed);
                if (EndOperation())
                {
                    ReleaseHostStream();
                    return;
                }

                Enqueue(
                    () => SettleWrite(failure, capability, operationEnded: true),
                    registration);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return promise;
    }

    /// <summary>
    /// On the engine thread: end the in-flight write and settle its promise. A rejection is what errors the
    /// writable stream, which is the standard's own reaction to a sink whose <c>write()</c> failed.
    /// </summary>
    private void SettleWrite(
        Exception? failure,
        PromiseCapability capability,
        bool operationEnded = false)
    {
        var owesRelease = !operationEnded && EndOperation();

        if (failure is not null && FinishBridge())
        {
            owesRelease = true;
        }

        if (owesRelease)
        {
            ReleaseHostStream();
        }

        if (failure is null)
        {
            capability.Resolve(JsValue.Undefined);
            return;
        }

        capability.Reject(HostStreamError("writing", failure));
    }

    /// <summary>
    /// The underlying sink's <c>close()</c>: https://streams.spec.whatwg.org/#dom-underlyingsink-close.
    /// Flushes the host's stream and, unless the host kept ownership, disposes it — and the promise it
    /// answers with is what makes <c>await writer.close()</c> mean the bytes are on the disk.
    /// </summary>
    private JsPromise Close()
    {
        var capability = StreamPromises.NewPromise(Engine, Realm);
        var promise = StreamPromises.PromiseOf(capability);

        if (!TryBeginOperation(out var registration))
        {
            // Abandoned by a restore. Nothing is left to close, and nothing will observe this promise.
            capability.Resolve(JsValue.Undefined);
            return promise;
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

            SettleClose(failure, capability);
            return promise;
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
                    () => SettleClose(failure, capability, operationEnded: true),
                    registration);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return promise;
    }

    /// <summary>
    /// The underlying sink's <c>abort()</c>: https://streams.spec.whatwg.org/#dom-underlyingsink-abort.
    /// </summary>
    /// <remarks>
    /// An abort flushes nothing — the script has said the output is not wanted, and pushing a partial buffer
    /// to the disk on the way out is the opposite of what it asked for. The stream is still disposed unless
    /// the host kept ownership, because the handle has to go somewhere, and a failure to dispose is
    /// swallowed: the abort's own reason is the outcome the script is waiting on.
    /// </remarks>
    private JsPromise Abort()
    {
        if (FinishBridge())
        {
            ReleaseHostStream();
        }

        return StreamPromises.ResolvedWithUndefined(Engine, Realm);
    }

    /// <summary>
    /// Flushes and, unless the host kept ownership, disposes. Both asynchronously, so that a slow disk stalls
    /// the script's promise rather than the engine's thread.
    /// </summary>
    private async ValueTask FlushAndDisposeAsync()
    {
        await HostStream.FlushAsync(Cancellation.Token).ConfigureAwait(false);

        if (!LeaveOpen)
        {
            await HostStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// On the engine thread: the close is over, one way or the other, and the bridge is done with the host's
    /// stream whichever way it went.
    /// </summary>
    private void SettleClose(
        Exception? failure,
        PromiseCapability capability,
        bool operationEnded = false)
    {
        var owesRelease = !operationEnded && EndOperation();

        if (FinishBridge())
        {
            owesRelease = true;
        }

        if (owesRelease)
        {
            // Disposing twice is a no-op every Stream guarantees, so a close that already disposed and a
            // release that disposes again cannot collide; what this call is really for is the cancellation
            // source, and the LeaveOpen case where nothing was disposed at all.
            ReleaseHostStream();
        }

        if (failure is null)
        {
            capability.Resolve(JsValue.Undefined);
            return;
        }

        capability.Reject(HostStreamError("closing", failure));
    }

    private void EndOperationAndReleaseIfOwed()
    {
        if (EndOperation())
        {
            ReleaseHostStream();
        }
    }
}
#endif

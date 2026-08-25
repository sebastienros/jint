#if NET8_0_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// A <c>ReadableStream</c> whose underlying source is a host <see cref="Stream"/>: the engine's side of
/// <c>Engine.WebApi.CreateReadableStream</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pull-driven, one read at a time.</b> The specification's own reentrancy guard —
/// <c>[[pulling]]</c>/<c>[[pullAgain]]</c>, https://streams.spec.whatwg.org/#readable-stream-default-controller-call-pull-if-needed
/// — already promises that <c>pull()</c> is never re-entered while a previous call's promise is outstanding,
/// so one <see cref="Stream.ReadAsync(Memory{byte}, CancellationToken)"/> is in flight at a time and the
/// bridge needs a single buffer for its whole life rather than one per chunk. Backpressure is therefore the
/// standard's, not a second mechanism layered on top: the queue's desired size decides whether a pull
/// happens, and no byte leaves the host's stream until it does.
/// </para>
/// <para>
/// <b>A read that completes synchronously is delivered synchronously.</b> A <see cref="MemoryStream"/>, or a
/// file whose bytes are already in the page cache, answers <c>ReadAsync</c> without ever leaving the calling
/// thread — which here is the engine's, inside <c>pull()</c>, the one place the Streams Standard explicitly
/// allows an enqueue from. So such a stream costs no thread hop, no event-loop job and no pump: reading it
/// from script behaves exactly as a stream built by script does. It is the same window
/// <c>ModuleLoadCompletion</c> opens for a warm module cache, and for the same reason — the engine asked for
/// this answer and is at a known-safe point waiting for it.
/// </para>
/// <para>
/// Everything else goes the long way round: the read completes on a thread-pool thread, is classified there
/// into a byte count or an exception, and a generation-stamped job turns it into a chunk on the engine's
/// thread. <b>The engine must be pumped for that job to run</b> — a stream whose reads do not complete
/// synchronously makes no progress in an engine nobody is giving turns to.
/// </para>
/// </remarks>
internal sealed class HostReadableStreamSource : HostStreamBridge
{
    private readonly byte[] _buffer;

    private JsReadableStreamDefaultController _controller = null!;

    private HostReadableStreamSource(Engine engine, Realm realm, Stream source, HostReadableStreamOptions options)
        : base(engine, realm, source, options.LeaveOpen, CancellationToken.None)
    {
        // One buffer for the bridge's whole life rather than one per chunk: only one read is ever in flight,
        // and the bytes are copied into a fresh Uint8Array before the next read can start. Below the large
        // object heap's threshold at the default size, and the host's own choice above it.
        _buffer = new byte[options.ChunkSize];
    }

    /// <summary>
    /// Builds the stream. Runs on the engine thread, and returns before a single byte has been read: the
    /// first read is the first <c>pull()</c>, which the controller makes once <c>start()</c> has settled.
    /// </summary>
    internal static JsReadableStream Create(Engine engine, Realm realm, Stream source, HostReadableStreamOptions options)
    {
        var bridge = new HostReadableStreamSource(engine, realm, source, options);

        // The stream and its controller are built here rather than through
        // ReadableStreamOperations.CreateReadableStream so that the bridge holds the controller before
        // anything can pull. The controller is reachable from the stream the moment setup returns, but
        // reading it back afterwards would make this code depend on the fact that the first pull happens on
        // a later turn — true today, and not an invariant worth resting on.
        var stream = new JsReadableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStream.PrototypeObject,
        };

        var controller = new JsReadableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStreamDefaultController.PrototypeObject,
        };

        bridge._controller = controller;

        ReadableStreamOperations.SetUpDefaultController(
            stream,
            controller,
            static () => JsValue.Undefined,
            bridge.Pull,
            _ => bridge.Cancel(),
            options.HighWaterMark,
            static _ => 1);

        engine.RegisterHostStreamBridge(bridge);
        return stream;
    }

    /// <summary>
    /// The underlying source's <c>pull()</c>: read the next chunk, and settle when it has been enqueued.
    /// https://streams.spec.whatwg.org/#dom-underlyingsource-pull
    /// </summary>
    /// <remarks>
    /// The promise is resolved <i>after</i> the chunk is enqueued rather than when the read starts, which is
    /// what makes the standard's guard mean what it says: a pull that resolved early would let the controller
    /// start another one against a buffer the previous read still owns.
    /// </remarks>
    private JsPromise Pull()
    {
        var capability = StreamPromises.NewPromise(Engine, Realm);
        var promise = StreamPromises.PromiseOf(capability);

        if (!TryBeginOperation(out var registration))
        {
            // Only reachable for a bridge a restore has abandoned: a cancelled or closed stream is no longer
            // readable, and the controller does not pull one. The cycle this promise belongs to has ended,
            // so nothing will observe how it settles.
            capability.Resolve(JsValue.Undefined);
            return promise;
        }

        ValueTask<int> read;
        try
        {
            read = HostStream.ReadAsync(_buffer, Cancellation.Token);
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            // A stream that refuses synchronously — disposed, opened write-only, a host implementation that
            // validates before returning a task.
            Deliver(count: 0, exception, capability, operationEnded: false);
            return promise;
        }

        if (read.IsCompleted)
        {
            int count;
            try
            {
                count = read.GetAwaiter().GetResult();
            }
            catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
            {
                Deliver(count: 0, exception, capability, operationEnded: false);
                return promise;
            }

            Deliver(count, failure: null, capability, operationEnded: false);
            return promise;
        }

        var task = read.AsTask();
        _ = task.ContinueWith(
            completed => Complete(completed, capability, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return promise;
    }

    /// <summary>
    /// Off the engine thread: classify the read into plain CLR data and queue the engine-thread half.
    /// </summary>
    private void Complete(
        Task<int> task,
        PromiseCapability capability,
        EventLoopRegistration registration)
    {
        var failure = ClassifyFailure(task);
        var count = failure is null ? task.Result : 0;

        if (EndOperation())
        {
            ReleaseHostStream();
            return;
        }

        Enqueue(
            () => Deliver(count, failure, capability, operationEnded: true),
            registration);
    }

    /// <summary>
    /// On the engine thread: turn one finished read into a chunk, a close, or an error, and settle the pull.
    /// </summary>
    /// <remarks>
    /// The order matters. The in-flight operation is ended <i>before</i> the JavaScript work, so that the
    /// release the standard's close path owes is taken by <see cref="HostStreamBridge.FinishBridge"/> here
    /// rather than left to an operation that has already finished. The pull promise is resolved last, because
    /// resolving it is what lets the controller ask for the next chunk.
    /// </remarks>
    private void Deliver(
        int count,
        Exception? failure,
        PromiseCapability capability,
        bool operationEnded)
    {
        var owesRelease = !operationEnded && EndOperation();

        if (failure is not null)
        {
            if (FinishBridge())
            {
                owesRelease = true;
            }

            if (owesRelease)
            {
                ReleaseHostStream();
            }

            // A rejected pull() errors the stream, which is the standard's own reaction —
            // https://streams.spec.whatwg.org/#readable-stream-default-controller-call-pull-if-needed. Doing
            // it that way rather than erroring the controller directly keeps one path for every failure.
            capability.Reject(HostStreamError("reading", failure));
            return;
        }

        if (count == 0)
        {
            if (FinishBridge())
            {
                owesRelease = true;
            }

            if (owesRelease)
            {
                ReleaseHostStream();
            }

            ReadableStreamDefaultControllerOperations.Close(_controller);
            capability.Resolve(JsValue.Undefined);
            return;
        }

        if (owesRelease)
        {
            // Abandoned while this read ran. The chunk is dropped: the cycle it belongs to is over, and the
            // job that carried it here only ran at all because it was enqueued before the fence went up.
            ReleaseHostStream();
            capability.Resolve(JsValue.Undefined);
            return;
        }

        ReadableStreamDefaultControllerOperations.Enqueue(_controller, CreateChunk(_buffer.AsSpan(0, count)));
        capability.Resolve(JsValue.Undefined);
    }

    /// <summary>
    /// The underlying source's <c>cancel()</c>: stop reading and let go of the host's stream.
    /// https://streams.spec.whatwg.org/#dom-underlyingsource-cancel
    /// </summary>
    /// <remarks>
    /// Answers a resolved promise without waiting for the close, so <c>await stream.cancel()</c> does not
    /// depend on how long a file handle takes to release. A read still in flight is cancelled and releases
    /// the stream itself when it unwinds; the reason script gave is not passed on, because a host
    /// <see cref="Stream"/> has nowhere to put it.
    /// </remarks>
    private JsPromise Cancel()
    {
        if (FinishBridge())
        {
            ReleaseHostStream();
        }

        return StreamPromises.ResolvedWithUndefined(Engine, Realm);
    }
}
#endif

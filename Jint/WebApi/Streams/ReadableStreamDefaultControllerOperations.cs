#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableStreamDefaultController</c> abstract operations, plus its two internal methods
/// <c>[[PullSteps]]</c> and <c>[[CancelSteps]]</c>.
/// <para>
/// https://streams.spec.whatwg.org/#rs-default-controller-abstract-ops
/// </para>
/// </summary>
internal static class ReadableStreamDefaultControllerOperations
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-call-pull-if-needed
    /// </summary>
    /// <remarks>
    /// The <c>[[pulling]]</c>/<c>[[pullAgain]]</c> pair is the reentrancy guard the underlying source API
    /// promises: <c>pull()</c> is never called again while a previous call's promise is outstanding, and a
    /// request that arrives during one is remembered and served once it settles. That is what makes
    /// enqueueing from inside <c>pull()</c> safe.
    /// </remarks>
    internal static void CallPullIfNeeded(JsReadableStreamDefaultController controller)
    {
        if (!ShouldCallPull(controller))
        {
            return;
        }

        if (controller.Pulling)
        {
            controller.PullAgain = true;
            return;
        }

        controller.Pulling = true;

        // The algorithm is cleared only once the stream is closed or errored, and both of those make
        // ShouldCallPull answer false, so it is still present here.
        var pullPromise = controller.PullAlgorithm!();

        StreamPromises.UponPromise(
            controller.Engine,
            pullPromise,
            _ =>
            {
                controller.Pulling = false;
                if (controller.PullAgain)
                {
                    controller.PullAgain = false;
                    CallPullIfNeeded(controller);
                }
            },
            error => Error(controller, error));
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-should-call-pull
    /// </summary>
    internal static bool ShouldCallPull(JsReadableStreamDefaultController controller)
    {
        var stream = controller.Stream;

        if (!CanCloseOrEnqueue(controller))
        {
            return false;
        }

        // "This function will not be called until start() successfully completes."
        if (!controller.Started)
        {
            return false;
        }

        // A waiting consumer outranks the high water mark: a read against an empty queue pulls even when the
        // strategy would otherwise say the queue is full enough.
        if (ReadableStreamOperations.IsLocked(stream) && ReadableStreamOperations.GetNumReadRequests(stream) > 0)
        {
            return true;
        }

        var desiredSize = GetDesiredSize(controller);
        return desiredSize > 0;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-clear-algorithms
    /// </summary>
    /// <remarks>
    /// Called the moment the algorithms can no longer be needed, which is what stops a closed or errored
    /// stream from retaining the underlying source and everything it closed over.
    /// </remarks>
    internal static void ClearAlgorithms(JsReadableStreamDefaultController controller)
    {
        controller.PullAlgorithm = null;
        controller.CancelAlgorithm = null;
        controller.StrategySizeAlgorithm = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-close
    /// </summary>
    internal static void Close(JsReadableStreamDefaultController controller)
    {
        if (!CanCloseOrEnqueue(controller))
        {
            return;
        }

        var stream = controller.Stream;
        controller.CloseRequested = true;

        // Already-enqueued chunks stay readable; the stream only becomes closed once they have been read.
        if (controller.Queue.IsEmpty)
        {
            ClearAlgorithms(controller);
            ReadableStreamOperations.Close(stream);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-enqueue
    /// </summary>
    internal static void Enqueue(JsReadableStreamDefaultController controller, JsValue chunk)
    {
        if (!CanCloseOrEnqueue(controller))
        {
            return;
        }

        var stream = controller.Stream;

        if (ReadableStreamOperations.IsLocked(stream) && ReadableStreamOperations.GetNumReadRequests(stream) > 0)
        {
            // A waiting reader takes the chunk directly: it never enters the queue, so the strategy's
            // size() is not called for it either.
            ReadableStreamOperations.FulfillReadRequest(stream, chunk, done: false);
        }
        else
        {
            double chunkSize;
            try
            {
                chunkSize = controller.StrategySizeAlgorithm!(chunk);
            }
            catch (JavaScriptException e)
            {
                Error(controller, e.Error);
                throw;
            }

            try
            {
                controller.Queue.Enqueue(controller.Realm, chunk, chunkSize);
            }
            catch (JavaScriptException e)
            {
                Error(controller, e.Error);
                throw;
            }
        }

        CallPullIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-error
    /// </summary>
    internal static void Error(JsReadableStreamDefaultController controller, JsValue error)
    {
        var stream = controller.Stream;
        if (stream.State != ReadableStreamState.Readable)
        {
            return;
        }

        controller.Queue.Reset();
        ClearAlgorithms(controller);
        ReadableStreamOperations.Error(stream, error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-get-desired-size — null for an
    /// errored stream, which is why the getter's type is <c>unrestricted double?</c>.
    /// </summary>
    internal static double? GetDesiredSize(JsReadableStreamDefaultController controller)
    {
        return controller.Stream.State switch
        {
            ReadableStreamState.Errored => null,
            ReadableStreamState.Closed => 0,
            _ => controller.StrategyHighWaterMark - controller.Queue.TotalSize,
        };
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-has-backpressure — used by a
    /// transform stream to decide whether to stop accepting writes.
    /// </summary>
    internal static bool HasBackpressure(JsReadableStreamDefaultController controller) => !ShouldCallPull(controller);

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-controller-can-close-or-enqueue
    /// </summary>
    internal static bool CanCloseOrEnqueue(JsReadableStreamDefaultController controller)
        => !controller.CloseRequested && controller.Stream.State == ReadableStreamState.Readable;

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-default-controller-private-pull
    /// </summary>
    internal static void PullSteps(JsReadableStreamDefaultController controller, ReadRequest readRequest)
    {
        var stream = controller.Stream;

        if (!controller.Queue.IsEmpty)
        {
            var chunk = controller.Queue.Dequeue();

            if (controller.CloseRequested && controller.Queue.IsEmpty)
            {
                ClearAlgorithms(controller);
                ReadableStreamOperations.Close(stream);
            }
            else
            {
                CallPullIfNeeded(controller);
            }

            // The chunk steps run last, after the close or the pull: a consumer that reads the final chunk
            // sees the stream already closed.
            readRequest.ChunkSteps(chunk);
            return;
        }

        ReadableStreamOperations.AddReadRequest(stream, readRequest);
        CallPullIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rs-default-controller-private-cancel
    /// </summary>
    internal static JsPromise CancelSteps(JsReadableStreamDefaultController controller, JsValue reason)
    {
        controller.Queue.Reset();

        // Reachable only while the stream is readable, so the algorithm has not been cleared yet.
        var result = controller.CancelAlgorithm!(reason);
        ClearAlgorithms(controller);
        return result;
    }
}
#endif

#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>WritableStreamDefaultController</c> abstract operations, plus its two internal methods
/// <c>[[AbortSteps]]</c> and <c>[[ErrorSteps]]</c>.
/// <para>
/// https://streams.spec.whatwg.org/#ws-default-controller-abstract-ops
/// </para>
/// </summary>
internal static class WritableStreamDefaultControllerOperations
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#close-sentinel — "a unique value enqueued into <c>[[queue]]</c>, in
    /// lieu of a chunk, to signal that the stream is closed. It is only used internally, and is never
    /// exposed to web developers."
    /// </summary>
    /// <remarks>
    /// A symbol, and one nothing can reach: it is not in the global symbol registry, is never handed to a
    /// callback and is compared only by reference. Sharing one across engines is safe precisely because it
    /// is never exposed — a symbol identity that no script can obtain cannot leak between realms.
    /// </remarks>
    private static readonly JsSymbol _closeSentinel = new("[[close sentinel]]");

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-writable-stream-default-controller
    /// </summary>
    internal static void SetUp(
        JsWritableStream stream,
        JsWritableStreamDefaultController controller,
        Func<JsValue> startAlgorithm,
        Func<JsValue, JsPromise> writeAlgorithm,
        Func<JsPromise> closeAlgorithm,
        Func<JsValue, JsPromise> abortAlgorithm,
        double highWaterMark,
        Func<JsValue, double> sizeAlgorithm)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        controller.Stream = stream;
        stream.Controller = controller;
        controller.Queue.Reset();
        controller.Started = false;
        controller.StrategySizeAlgorithm = sizeAlgorithm;
        controller.StrategyHighWaterMark = highWaterMark;
        controller.WriteAlgorithm = writeAlgorithm;
        controller.CloseAlgorithm = closeAlgorithm;
        controller.AbortAlgorithm = abortAlgorithm;

        // Backpressure is computed before start() runs, so a stream whose high water mark is 0 applies it
        // from the very first moment and a writer acquired immediately is not ready.
        WritableStreamOperations.UpdateBackpressure(stream, GetBackpressure(controller));

        // The sink's start() has return type `any`, so an exception it raises propagates out of the
        // WritableStream constructor rather than becoming a rejection.
        var startResult = startAlgorithm();
        var startPromise = StreamPromises.ResolvedWith(engine, realm, startResult);

        StreamPromises.UponPromise(
            engine,
            startPromise,
            _ =>
            {
                controller.Started = true;
                AdvanceQueueIfNeeded(controller);
            },
            reason =>
            {
                // Started is set even on failure: the erroring machinery waits for start() to settle before
                // it may run the sink's abort(), and a start that failed has settled.
                controller.Started = true;
                WritableStreamOperations.DealWithRejection(stream, reason);
            });
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-writable-stream-default-controller-from-underlying-sink
    /// </summary>
    internal static void SetUpFromUnderlyingSink(
        JsWritableStream stream,
        JsValue underlyingSink,
        in StreamDictionaries.UnderlyingSinkRecord sink,
        double highWaterMark,
        Func<JsValue, double> sizeAlgorithm)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        var controller = new JsWritableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.WritableStreamDefaultController.PrototypeObject,
        };

        var start = sink.Start;
        var write = sink.Write;
        var close = sink.Close;
        var abort = sink.Abort;

        Func<JsValue> startAlgorithm = start is null
            ? static () => JsValue.Undefined
            : () => start.Call(underlyingSink, controller);

        Func<JsValue, JsPromise> writeAlgorithm = write is null
            ? _ => StreamPromises.ResolvedWithUndefined(engine, realm)
            : chunk => StreamPromises.PromiseCall(engine, realm, write, underlyingSink, [chunk, controller]);

        Func<JsPromise> closeAlgorithm = close is null
            ? () => StreamPromises.ResolvedWithUndefined(engine, realm)
            : () => StreamPromises.PromiseCall(engine, realm, close, underlyingSink, []);

        Func<JsValue, JsPromise> abortAlgorithm = abort is null
            ? _ => StreamPromises.ResolvedWithUndefined(engine, realm)
            : reason => StreamPromises.PromiseCall(engine, realm, abort, underlyingSink, [reason]);

        SetUp(stream, controller, startAlgorithm, writeAlgorithm, closeAlgorithm, abortAlgorithm, highWaterMark, sizeAlgorithm);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-advance-queue-if-needed
    /// </summary>
    internal static void AdvanceQueueIfNeeded(JsWritableStreamDefaultController controller)
    {
        var stream = controller.Stream;

        if (!controller.Started)
        {
            return;
        }

        // One sink operation at a time: nothing is dequeued while the sink is still working.
        if (stream.InFlightWriteRequest is not null)
        {
            return;
        }

        if (stream.State == WritableStreamState.Erroring)
        {
            WritableStreamOperations.FinishErroring(stream);
            return;
        }

        if (controller.Queue.IsEmpty)
        {
            return;
        }

        var value = controller.Queue.Peek();
        if (ReferenceEquals(value, _closeSentinel))
        {
            ProcessClose(controller);
        }
        else
        {
            ProcessWrite(controller, value);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-clear-algorithms
    /// </summary>
    internal static void ClearAlgorithms(JsWritableStreamDefaultController controller)
    {
        controller.WriteAlgorithm = null;
        controller.CloseAlgorithm = null;
        controller.AbortAlgorithm = null;
        controller.StrategySizeAlgorithm = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-close
    /// </summary>
    internal static void Close(JsWritableStreamDefaultController controller)
    {
        controller.Queue.Enqueue(controller.Realm, _closeSentinel, 0);
        AdvanceQueueIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-error
    /// </summary>
    internal static void Error(JsWritableStreamDefaultController controller, JsValue error)
    {
        ClearAlgorithms(controller);
        WritableStreamOperations.StartErroring(controller.Stream, error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-error-if-needed
    /// </summary>
    internal static void ErrorIfNeeded(JsWritableStreamDefaultController controller, JsValue error)
    {
        if (controller.Stream.State == WritableStreamState.Writable)
        {
            Error(controller, error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-get-backpressure
    /// </summary>
    internal static bool GetBackpressure(JsWritableStreamDefaultController controller) => GetDesiredSize(controller) <= 0;

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-get-chunk-size
    /// </summary>
    /// <remarks>
    /// A strategy whose <c>size()</c> throws errors the stream and the chunk is then measured as 1 — the
    /// write itself still goes on to fail, because the stream is no longer writable by the time
    /// <c>WritableStreamDefaultWriterWrite</c> re-reads its state.
    /// </remarks>
    internal static double GetChunkSize(JsWritableStreamDefaultController controller, JsValue chunk)
    {
        if (controller.StrategySizeAlgorithm is not { } sizeAlgorithm)
        {
            return 1;
        }

        try
        {
            return sizeAlgorithm(chunk);
        }
        catch (JavaScriptException e)
        {
            ErrorIfNeeded(controller, e.Error);
            return 1;
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-get-desired-size
    /// </summary>
    internal static double GetDesiredSize(JsWritableStreamDefaultController controller)
        => controller.StrategyHighWaterMark - controller.Queue.TotalSize;

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-process-close
    /// </summary>
    private static void ProcessClose(JsWritableStreamDefaultController controller)
    {
        var stream = controller.Stream;

        WritableStreamOperations.MarkCloseRequestInFlight(stream);
        controller.Queue.Dequeue();

        // Reachable only while the algorithms are present: they are cleared right after this call.
        var sinkClosePromise = controller.CloseAlgorithm!();
        ClearAlgorithms(controller);

        StreamPromises.UponPromise(
            controller.Engine,
            sinkClosePromise,
            _ => WritableStreamOperations.FinishInFlightClose(stream),
            reason => WritableStreamOperations.FinishInFlightCloseWithError(stream, reason));
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-process-write
    /// </summary>
    private static void ProcessWrite(JsWritableStreamDefaultController controller, JsValue chunk)
    {
        var stream = controller.Stream;

        WritableStreamOperations.MarkFirstWriteRequestInFlight(stream);

        var sinkWritePromise = controller.WriteAlgorithm!(chunk);

        StreamPromises.UponPromise(
            controller.Engine,
            sinkWritePromise,
            _ =>
            {
                WritableStreamOperations.FinishInFlightWrite(stream);

                var state = stream.State;

                // The chunk leaves the queue only once the sink has taken it, which is what makes
                // desiredSize describe what is still outstanding rather than what has been handed over.
                controller.Queue.Dequeue();

                if (!WritableStreamOperations.CloseQueuedOrInFlight(stream) && state == WritableStreamState.Writable)
                {
                    WritableStreamOperations.UpdateBackpressure(stream, GetBackpressure(controller));
                }

                AdvanceQueueIfNeeded(controller);
            },
            reason =>
            {
                if (stream.State == WritableStreamState.Writable)
                {
                    ClearAlgorithms(controller);
                }

                WritableStreamOperations.FinishInFlightWriteWithError(stream, reason);
            });
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-controller-write
    /// </summary>
    internal static void Write(JsWritableStreamDefaultController controller, JsValue chunk, double chunkSize)
    {
        try
        {
            controller.Queue.Enqueue(controller.Realm, chunk, chunkSize);
        }
        catch (JavaScriptException e)
        {
            // A size the queue refuses errors the stream rather than escaping to the caller: the caller is
            // writer.write(), whose promise is the write request already queued for this chunk.
            ErrorIfNeeded(controller, e.Error);
            return;
        }

        var stream = controller.Stream;
        if (!WritableStreamOperations.CloseQueuedOrInFlight(stream) && stream.State == WritableStreamState.Writable)
        {
            WritableStreamOperations.UpdateBackpressure(stream, GetBackpressure(controller));
        }

        AdvanceQueueIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-default-controller-private-abort
    /// </summary>
    internal static JsPromise AbortSteps(JsWritableStreamDefaultController controller, JsValue reason)
    {
        var result = controller.AbortAlgorithm!(reason);
        ClearAlgorithms(controller);
        return result;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#ws-default-controller-private-error
    /// </summary>
    internal static void ErrorSteps(JsWritableStreamDefaultController controller) => controller.Queue.Reset();
}
#endif

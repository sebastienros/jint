#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The transform-stream abstract operations: "working with transform streams", the default controller's
/// operations, and the default sink and source the two sides are built from.
/// <para>
/// https://streams.spec.whatwg.org/#ts-abstract-ops
/// </para>
/// </summary>
internal static class TransformStreamOperations
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#initialize-transform-stream
    /// </summary>
    internal static void Initialize(
        JsTransformStream stream,
        JsPromise startPromise,
        double writableHighWaterMark,
        Func<JsValue, double> writableSizeAlgorithm,
        double readableHighWaterMark,
        Func<JsValue, double> readableSizeAlgorithm)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        // Both sides share one start promise, so neither begins working before the transformer's start()
        // has settled.
        Func<JsValue> startAlgorithm = () => startPromise;

        stream.Writable = WritableStreamOperations.CreateWritableStream(
            engine,
            realm,
            startAlgorithm,
            chunk => SinkWriteAlgorithm(stream, chunk),
            () => SinkCloseAlgorithm(stream),
            reason => SinkAbortAlgorithm(stream, reason),
            writableHighWaterMark,
            writableSizeAlgorithm);

        stream.Readable = ReadableStreamOperations.CreateReadableStream(
            engine,
            realm,
            startAlgorithm,
            () => SourcePullAlgorithm(stream),
            reason => SourceCancelAlgorithm(stream, reason),
            readableHighWaterMark,
            readableSizeAlgorithm);

        // Backpressure starts on: nothing is transformed until the readable side is pulled from, which is
        // what keeps a transformer from running ahead of its consumer.
        stream.Backpressure = null;
        stream.BackpressureChangeCapability = null;
        SetBackpressure(stream, backpressure: true);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transformstream-set-up — the entry point for the transform streams
    /// <i>other</i> standards define (<c>TextEncoderStream</c>, <c>CompressionStream</c>, …) rather than ones
    /// a script constructs. It creates the stream too, which the specification leaves to the one line above
    /// every call site ("let transformStream be a new TransformStream").
    /// </summary>
    /// <remarks>
    /// The algorithms are engine code, not script callbacks, so they are <see cref="Action"/>s rather than
    /// functions returning a promise: the specification's wrappers exist to turn a thrown exception into a
    /// rejected promise and a non-promise return value into a resolved one, which is exactly what the
    /// <c>RunAlgorithm</c> pair below does. The high water marks are the specification's own defaults — one chunk
    /// of buffering on the way in, none on the way out — so a chunk is transformed only once the readable
    /// side is read from.
    /// </remarks>
    internal static JsTransformStream SetUp(
        Engine engine,
        Realm realm,
        Action<JsValue> transformAlgorithm,
        Action? flushAlgorithm = null,
        Action? cancelAlgorithm = null)
    {
        var stream = new JsTransformStream(engine, realm)
        {
            _prototype = realm.Intrinsics.TransformStream.PrototypeObject,
        };

        Initialize(
            stream,
            StreamPromises.ResolvedWithUndefined(engine, realm),
            writableHighWaterMark: 1,
            SizeOfOne,
            readableHighWaterMark: 0,
            SizeOfOne);

        var controller = new JsTransformStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.TransformStreamDefaultController.PrototypeObject,
        };

        SetUpController(
            stream,
            controller,
            chunk => RunAlgorithm(engine, realm, transformAlgorithm, chunk),
            () => RunAlgorithm(engine, realm, flushAlgorithm),
            _ => RunAlgorithm(engine, realm, cancelAlgorithm));

        return stream;
    }

    /// <summary>
    /// "Enqueue <paramref name="chunk"/> into <paramref name="stream"/>" —
    /// https://streams.spec.whatwg.org/#transformstream-enqueue.
    /// </summary>
    internal static void Enqueue(JsTransformStream stream, JsValue chunk)
        => ControllerEnqueue(stream.Controller, chunk);

    /// <summary>The size algorithm the set-up operation gives both sides: "an algorithm that returns 1".</summary>
    private static readonly Func<JsValue, double> SizeOfOne = static _ => 1;

    /// <summary>
    /// The specification's <c>transformAlgorithmWrapper</c>: run the algorithm, and report a JavaScript
    /// exception it raised as a rejected promise rather than letting it escape into the caller.
    /// </summary>
    private static JsPromise RunAlgorithm(Engine engine, Realm realm, Action<JsValue>? algorithm, JsValue chunk)
    {
        try
        {
            algorithm?.Invoke(chunk);
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(engine, realm, e.Error);
        }

        return StreamPromises.ResolvedWithUndefined(engine, realm);
    }

    /// <summary>The same wrapper for the two algorithms that take no argument.</summary>
    private static JsPromise RunAlgorithm(Engine engine, Realm realm, Action? algorithm)
    {
        try
        {
            algorithm?.Invoke();
        }
        catch (JavaScriptException e)
        {
            return StreamPromises.RejectedWith(engine, realm, e.Error);
        }

        return StreamPromises.ResolvedWithUndefined(engine, realm);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-error
    /// </summary>
    /// <remarks>
    /// Works when one or both sides are already errored, which is why the callers never check a state first.
    /// </remarks>
    internal static void Error(JsTransformStream stream, JsValue error)
    {
        ReadableStreamDefaultControllerOperations.Error(stream.Readable.Controller, error);
        ErrorWritableAndUnblockWrite(stream, error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-error-writable-and-unblock-write
    /// </summary>
    internal static void ErrorWritableAndUnblockWrite(JsTransformStream stream, JsValue error)
    {
        ClearAlgorithms(stream.Controller);
        WritableStreamDefaultControllerOperations.ErrorIfNeeded(stream.Writable.Controller, error);
        UnblockWrite(stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-unblock-write
    /// </summary>
    /// <remarks>
    /// A write parked on the backpressure latch would otherwise never resume once the readable side is gone,
    /// so releasing the latch is part of every failure path.
    /// </remarks>
    internal static void UnblockWrite(JsTransformStream stream)
    {
        if (stream.Backpressure == true)
        {
            SetBackpressure(stream, backpressure: false);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-set-backpressure
    /// </summary>
    internal static void SetBackpressure(JsTransformStream stream, bool backpressure)
    {
        stream.BackpressureChangeCapability?.Resolve(JsValue.Undefined);
        stream.BackpressureChangeCapability = StreamPromises.NewPromise(stream.Engine, stream.Realm);
        stream.Backpressure = backpressure;
    }

    // ---- Default controllers ----

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-transform-stream-default-controller
    /// </summary>
    private static void SetUpController(
        JsTransformStream stream,
        JsTransformStreamDefaultController controller,
        Func<JsValue, JsPromise> transformAlgorithm,
        Func<JsPromise> flushAlgorithm,
        Func<JsValue, JsPromise> cancelAlgorithm)
    {
        controller.Stream = stream;
        stream.Controller = controller;
        controller.TransformAlgorithm = transformAlgorithm;
        controller.FlushAlgorithm = flushAlgorithm;
        controller.CancelAlgorithm = cancelAlgorithm;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-transform-stream-default-controller-from-transformer
    /// </summary>
    internal static void SetUpControllerFromTransformer(
        JsTransformStream stream,
        JsValue transformer,
        in StreamDictionaries.TransformerRecord dictionary)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        var controller = new JsTransformStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.TransformStreamDefaultController.PrototypeObject,
        };

        var transform = dictionary.Transform;
        var flush = dictionary.Flush;
        var cancel = dictionary.Cancel;

        // With no transform() the stream is an identity transform: each chunk is enqueued as it arrives.
        JsPromise IdentityTransform(JsValue chunk)
        {
            try
            {
                ControllerEnqueue(controller, chunk);
                return StreamPromises.ResolvedWithUndefined(engine, realm);
            }
            catch (JavaScriptException e)
            {
                return StreamPromises.RejectedWith(engine, realm, e.Error);
            }
        }

        Func<JsValue, JsPromise> transformAlgorithm = transform is null
            ? IdentityTransform
            : chunk => StreamPromises.PromiseCall(engine, realm, transform, transformer, [chunk, controller]);

        Func<JsPromise> flushAlgorithm = flush is null
            ? () => StreamPromises.ResolvedWithUndefined(engine, realm)
            : () => StreamPromises.PromiseCall(engine, realm, flush, transformer, [controller]);

        Func<JsValue, JsPromise> cancelAlgorithm = cancel is null
            ? _ => StreamPromises.ResolvedWithUndefined(engine, realm)
            : reason => StreamPromises.PromiseCall(engine, realm, cancel, transformer, [reason]);

        SetUpController(stream, controller, transformAlgorithm, flushAlgorithm, cancelAlgorithm);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-controller-clear-algorithms
    /// </summary>
    internal static void ClearAlgorithms(JsTransformStreamDefaultController controller)
    {
        controller.TransformAlgorithm = null;
        controller.FlushAlgorithm = null;
        controller.CancelAlgorithm = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-controller-enqueue
    /// </summary>
    internal static void ControllerEnqueue(JsTransformStreamDefaultController controller, JsValue chunk)
    {
        var stream = controller.Stream;
        var readableController = stream.Readable.Controller;

        if (!ReadableStreamDefaultControllerOperations.CanCloseOrEnqueue(readableController))
        {
            Throw.TypeError(controller.Realm, "The readable side is not in a state that permits enqueue");
        }

        // Transform invocations are throttled by the readable side's backpressure, but an enqueue is still
        // accepted whenever the readable side can take one — a transformer may produce several chunks from
        // one.
        try
        {
            ReadableStreamDefaultControllerOperations.Enqueue(readableController, chunk);
        }
        catch (JavaScriptException e)
        {
            // This is the readable strategy's size() throwing. Both sides fail, and the caller is told the
            // readable side's stored error rather than the raw exception.
            ErrorWritableAndUnblockWrite(stream, e.Error);
            throw new JavaScriptException(stream.Readable.StoredError);
        }

        var backpressure = ReadableStreamDefaultControllerOperations.HasBackpressure(readableController);
        if (backpressure != stream.Backpressure)
        {
            SetBackpressure(stream, backpressure: true);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-controller-error
    /// </summary>
    internal static void ControllerError(JsTransformStreamDefaultController controller, JsValue error)
        => Error(controller.Stream, error);

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-controller-perform-transform
    /// </summary>
    private static JsPromise PerformTransform(JsTransformStreamDefaultController controller, JsValue chunk)
    {
        var engine = controller.Engine;
        var realm = controller.Realm;

        var transformPromise = controller.TransformAlgorithm!(chunk);

        return StreamPromises.TransformPromiseWith(
            engine,
            realm,
            transformPromise,
            onFulfilled: null,
            onRejected: reason =>
            {
                // The transformer failed, so both sides fail — and the rejection is passed on unchanged, so
                // the write that triggered the transform reports the transformer's own error.
                Error(controller.Stream, reason);
                throw new JavaScriptException(reason);
            });
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-controller-terminate
    /// </summary>
    internal static void ControllerTerminate(JsTransformStreamDefaultController controller)
    {
        var stream = controller.Stream;

        ReadableStreamDefaultControllerOperations.Close(stream.Readable.Controller);

        // The writable side is errored rather than closed: a producer still writing has to be told that its
        // chunks will never be transformed.
        var error = controller.Realm.Intrinsics.TypeError.Construct("The transform stream has been terminated");
        ErrorWritableAndUnblockWrite(stream, error);
    }

    // ---- Default sinks ----

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-sink-write-algorithm
    /// </summary>
    private static JsPromise SinkWriteAlgorithm(JsTransformStream stream, JsValue chunk)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;
        var controller = stream.Controller;

        if (stream.Backpressure == true)
        {
            var backpressureChangePromise = StreamPromises.PromiseOf(stream.BackpressureChangeCapability!);

            return StreamPromises.TransformPromiseWith(
                engine,
                realm,
                backpressureChangePromise,
                onFulfilled: _ =>
                {
                    var writable = stream.Writable;
                    if (writable.State == WritableStreamState.Erroring)
                    {
                        // The stream started failing while this write waited on the latch; the write reports
                        // that failure rather than being transformed.
                        throw new JavaScriptException(writable.StoredError);
                    }

                    return PerformTransform(controller, chunk);
                },
                onRejected: null);
        }

        return PerformTransform(controller, chunk);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-sink-abort-algorithm
    /// </summary>
    private static JsPromise SinkAbortAlgorithm(JsTransformStream stream, JsValue reason)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;
        var controller = stream.Controller;

        if (controller.FinishCapability is { } already)
        {
            return StreamPromises.PromiseOf(already);
        }

        // The readable side cannot change after construction, so caching it across a call into the
        // transformer is safe.
        var readable = stream.Readable;

        // Assigned before the transformer runs, so that a cancel() which reaches back into the stream —
        // readable.cancel(), say — does not start the algorithm a second time.
        var finish = StreamPromises.NewPromise(engine, realm);
        controller.FinishCapability = finish;

        var cancelPromise = controller.CancelAlgorithm!(reason);
        ClearAlgorithms(controller);

        StreamPromises.UponPromise(
            engine,
            cancelPromise,
            _ =>
            {
                if (readable.State == ReadableStreamState.Errored)
                {
                    finish.Reject(readable.StoredError);
                }
                else
                {
                    ReadableStreamDefaultControllerOperations.Error(readable.Controller, reason);
                    finish.Resolve(JsValue.Undefined);
                }
            },
            error =>
            {
                ReadableStreamDefaultControllerOperations.Error(readable.Controller, error);
                finish.Reject(error);
            });

        return StreamPromises.PromiseOf(finish);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-sink-close-algorithm
    /// </summary>
    private static JsPromise SinkCloseAlgorithm(JsTransformStream stream)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;
        var controller = stream.Controller;

        if (controller.FinishCapability is { } already)
        {
            return StreamPromises.PromiseOf(already);
        }

        var readable = stream.Readable;

        var finish = StreamPromises.NewPromise(engine, realm);
        controller.FinishCapability = finish;

        var flushPromise = controller.FlushAlgorithm!();
        ClearAlgorithms(controller);

        StreamPromises.UponPromise(
            engine,
            flushPromise,
            _ =>
            {
                if (readable.State == ReadableStreamState.Errored)
                {
                    finish.Reject(readable.StoredError);
                }
                else
                {
                    ReadableStreamDefaultControllerOperations.Close(readable.Controller);
                    finish.Resolve(JsValue.Undefined);
                }
            },
            error =>
            {
                ReadableStreamDefaultControllerOperations.Error(readable.Controller, error);
                finish.Reject(error);
            });

        return StreamPromises.PromiseOf(finish);
    }

    // ---- Default sources ----

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-source-pull-algorithm
    /// </summary>
    /// <remarks>
    /// The readable side's source does nothing but release the backpressure latch and then wait for it to be
    /// re-applied — which is what turns a pull on the readable side into permission for one more transform.
    /// </remarks>
    private static JsPromise SourcePullAlgorithm(JsTransformStream stream)
    {
        SetBackpressure(stream, backpressure: false);

        // Returning the new latch prevents another pull until backpressure re-appears.
        return StreamPromises.PromiseOf(stream.BackpressureChangeCapability!);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#transform-stream-default-source-cancel-algorithm
    /// </summary>
    private static JsPromise SourceCancelAlgorithm(JsTransformStream stream, JsValue reason)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;
        var controller = stream.Controller;

        if (controller.FinishCapability is { } already)
        {
            return StreamPromises.PromiseOf(already);
        }

        // The writable side cannot change after construction, so caching it across a call into the
        // transformer is safe.
        var writable = stream.Writable;

        var finish = StreamPromises.NewPromise(engine, realm);
        controller.FinishCapability = finish;

        var cancelPromise = controller.CancelAlgorithm!(reason);
        ClearAlgorithms(controller);

        StreamPromises.UponPromise(
            engine,
            cancelPromise,
            _ =>
            {
                if (writable.State == WritableStreamState.Errored)
                {
                    finish.Reject(writable.StoredError);
                }
                else
                {
                    WritableStreamDefaultControllerOperations.ErrorIfNeeded(writable.Controller, reason);
                    UnblockWrite(stream);
                    finish.Resolve(JsValue.Undefined);
                }
            },
            error =>
            {
                WritableStreamDefaultControllerOperations.ErrorIfNeeded(writable.Controller, error);
                UnblockWrite(stream);
                finish.Reject(error);
            });

        return StreamPromises.PromiseOf(finish);
    }
}
#endif

#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The writable-stream abstract operations: "working with writable streams", "interfacing with controllers"
/// and "writers".
/// <para>
/// https://streams.spec.whatwg.org/#ws-abstract-ops
/// </para>
/// </summary>
internal static class WritableStreamOperations
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#is-writable-stream-locked
    /// </summary>
    internal static bool IsLocked(JsWritableStream stream) => stream.Writer is not null;

    /// <summary>
    /// https://streams.spec.whatwg.org/#create-writable-stream — the entry point for streams the engine
    /// builds itself, which today means a transform stream's writable side.
    /// </summary>
    internal static JsWritableStream CreateWritableStream(
        Engine engine,
        Realm realm,
        Func<JsValue> startAlgorithm,
        Func<JsValue, JsPromise> writeAlgorithm,
        Func<JsPromise> closeAlgorithm,
        Func<JsValue, JsPromise> abortAlgorithm,
        double highWaterMark,
        Func<JsValue, double> sizeAlgorithm)
    {
        var stream = new JsWritableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.WritableStream.PrototypeObject,
        };

        var controller = new JsWritableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.WritableStreamDefaultController.PrototypeObject,
        };

        WritableStreamDefaultControllerOperations.SetUp(
            stream, controller, startAlgorithm, writeAlgorithm, closeAlgorithm, abortAlgorithm, highWaterMark, sizeAlgorithm);

        return stream;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-abort
    /// </summary>
    internal static JsPromise Abort(JsWritableStream stream, JsValue reason)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        if (stream.State is WritableStreamState.Closed or WritableStreamState.Errored)
        {
            return StreamPromises.ResolvedWithUndefined(engine, realm);
        }

        // The controller's signal is aborted first, so a sink that watches it can abandon an in-flight
        // write before any of the bookkeeping below runs — and before it is asked to abort.
        stream.Controller.SignalAbort(reason);

        // Aborting the signal runs author code (the signal's abort event listeners), which may have closed
        // or errored the stream in the meantime, so the state is re-read.
        var state = stream.State;
        if (state is WritableStreamState.Closed or WritableStreamState.Errored)
        {
            return StreamPromises.ResolvedWithUndefined(engine, realm);
        }

        if (stream.PendingAbortRequest is { } existing)
        {
            return StreamPromises.PromiseOf(existing.Capability);
        }

        var wasAlreadyErroring = state == WritableStreamState.Erroring;
        if (wasAlreadyErroring)
        {
            // The reason will not be used — the stream already has a stored error — so it is not retained.
            reason = JsValue.Undefined;
        }

        var capability = StreamPromises.NewPromise(engine, realm);
        stream.PendingAbortRequest = new PendingAbortRequest(capability, reason, wasAlreadyErroring);

        if (!wasAlreadyErroring)
        {
            StartErroring(stream, reason);
        }

        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-close
    /// </summary>
    internal static JsPromise Close(JsWritableStream stream)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;
        var state = stream.State;

        if (state is WritableStreamState.Closed or WritableStreamState.Errored)
        {
            return StreamPromises.RejectedWith(
                engine,
                realm,
                realm.Intrinsics.TypeError.Construct(
                    $"The stream (in {StateName(state)} state) is not in the writable state and cannot be closed"));
        }

        var capability = StreamPromises.NewPromise(engine, realm);
        stream.CloseRequest = capability;

        // A producer blocked on `ready` is released: nothing more can be written, so leaving it pending
        // would strand an `await writer.ready` forever.
        var writer = stream.Writer;
        if (writer is not null && stream.Backpressure && state == WritableStreamState.Writable)
        {
            writer.ReadyCapability.Resolve(JsValue.Undefined);
        }

        WritableStreamDefaultControllerOperations.Close(stream.Controller);

        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-add-write-request
    /// </summary>
    private static JsPromise AddWriteRequest(JsWritableStream stream)
    {
        var capability = StreamPromises.NewPromise(stream.Engine, stream.Realm);
        stream.WriteRequests.Enqueue(capability);
        return StreamPromises.PromiseOf(capability);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-close-queued-or-in-flight
    /// </summary>
    internal static bool CloseQueuedOrInFlight(JsWritableStream stream)
        => stream.CloseRequest is not null || stream.InFlightCloseRequest is not null;

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-deal-with-rejection
    /// </summary>
    internal static void DealWithRejection(JsWritableStream stream, JsValue error)
    {
        if (stream.State == WritableStreamState.Writable)
        {
            StartErroring(stream, error);
            return;
        }

        FinishErroring(stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-start-erroring
    /// </summary>
    internal static void StartErroring(JsWritableStream stream, JsValue reason)
    {
        var controller = stream.Controller;

        stream.State = WritableStreamState.Erroring;
        stream.StoredError = reason;

        if (stream.Writer is { } writer)
        {
            EnsureReadyPromiseRejected(writer, reason);
        }

        // The stream cannot finish erroring while the sink is mid-write or mid-close, nor before start()
        // has settled: either would run abort() out of order.
        if (!HasOperationMarkedInFlight(stream) && controller.Started)
        {
            FinishErroring(stream);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-finish-erroring
    /// </summary>
    internal static void FinishErroring(JsWritableStream stream)
    {
        var engine = stream.Engine;

        stream.State = WritableStreamState.Errored;
        WritableStreamDefaultControllerOperations.ErrorSteps(stream.Controller);

        var storedError = stream.StoredError;
        while (stream.WriteRequests.Count > 0)
        {
            stream.WriteRequests.Dequeue().Reject(storedError);
        }

        var abortRequest = stream.PendingAbortRequest;
        if (abortRequest is null)
        {
            RejectCloseAndClosedPromiseIfNeeded(stream);
            return;
        }

        stream.PendingAbortRequest = null;

        if (abortRequest.WasAlreadyErroring)
        {
            // The stream was already failing for another reason, so the abort merely observes it.
            abortRequest.Capability.Reject(storedError);
            RejectCloseAndClosedPromiseIfNeeded(stream);
            return;
        }

        var promise = WritableStreamDefaultControllerOperations.AbortSteps(stream.Controller, abortRequest.Reason);
        StreamPromises.UponPromise(
            engine,
            promise,
            _ =>
            {
                abortRequest.Capability.Resolve(JsValue.Undefined);
                RejectCloseAndClosedPromiseIfNeeded(stream);
            },
            reason =>
            {
                abortRequest.Capability.Reject(reason);
                RejectCloseAndClosedPromiseIfNeeded(stream);
            });
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-finish-in-flight-close
    /// </summary>
    internal static void FinishInFlightClose(JsWritableStream stream)
    {
        stream.InFlightCloseRequest!.Resolve(JsValue.Undefined);
        stream.InFlightCloseRequest = null;

        if (stream.State == WritableStreamState.Erroring)
        {
            // The error arrived too late to matter: the sink closed successfully, so it is discarded and any
            // abort waiting on it is told the stream shut down cleanly.
            stream.StoredError = JsValue.Undefined;
            if (stream.PendingAbortRequest is { } pending)
            {
                pending.Capability.Resolve(JsValue.Undefined);
                stream.PendingAbortRequest = null;
            }
        }

        stream.State = WritableStreamState.Closed;

        stream.Writer?.ClosedCapability.Resolve(JsValue.Undefined);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-finish-in-flight-close-with-error
    /// </summary>
    internal static void FinishInFlightCloseWithError(JsWritableStream stream, JsValue error)
    {
        stream.InFlightCloseRequest!.Reject(error);
        stream.InFlightCloseRequest = null;

        // The sink's abort() is never run after its close(): a close that failed has already told the sink
        // everything there is to tell.
        if (stream.PendingAbortRequest is { } pending)
        {
            pending.Capability.Reject(error);
            stream.PendingAbortRequest = null;
        }

        DealWithRejection(stream, error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-finish-in-flight-write
    /// </summary>
    internal static void FinishInFlightWrite(JsWritableStream stream)
    {
        stream.InFlightWriteRequest!.Resolve(JsValue.Undefined);
        stream.InFlightWriteRequest = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-finish-in-flight-write-with-error
    /// </summary>
    internal static void FinishInFlightWriteWithError(JsWritableStream stream, JsValue error)
    {
        stream.InFlightWriteRequest!.Reject(error);
        stream.InFlightWriteRequest = null;

        DealWithRejection(stream, error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-has-operation-marked-in-flight
    /// </summary>
    internal static bool HasOperationMarkedInFlight(JsWritableStream stream)
        => stream.InFlightWriteRequest is not null || stream.InFlightCloseRequest is not null;

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-mark-close-request-in-flight
    /// </summary>
    internal static void MarkCloseRequestInFlight(JsWritableStream stream)
    {
        stream.InFlightCloseRequest = stream.CloseRequest;
        stream.CloseRequest = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-mark-first-write-request-in-flight
    /// </summary>
    internal static void MarkFirstWriteRequestInFlight(JsWritableStream stream)
        => stream.InFlightWriteRequest = stream.WriteRequests.Dequeue();

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-reject-close-and-closed-promise-if-needed
    /// </summary>
    private static void RejectCloseAndClosedPromiseIfNeeded(JsWritableStream stream)
    {
        if (stream.CloseRequest is { } closeRequest)
        {
            closeRequest.Reject(stream.StoredError);
            stream.CloseRequest = null;
        }

        if (stream.Writer is { } writer)
        {
            writer.ClosedCapability.Reject(stream.StoredError);
            StreamPromises.MarkHandled(writer.ClosedPromise);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-update-backpressure
    /// </summary>
    internal static void UpdateBackpressure(JsWritableStream stream, bool backpressure)
    {
        var writer = stream.Writer;
        if (writer is not null && backpressure != stream.Backpressure)
        {
            if (backpressure)
            {
                // A fresh pending promise: `await writer.ready` from here on waits for the next transition.
                writer.ReadyCapability = StreamPromises.NewPromise(stream.Engine, stream.Realm);
            }
            else
            {
                writer.ReadyCapability.Resolve(JsValue.Undefined);
            }
        }

        stream.Backpressure = backpressure;
    }

    // ---- Writers ----

    /// <summary>
    /// https://streams.spec.whatwg.org/#acquire-writable-stream-default-writer
    /// </summary>
    internal static JsWritableStreamDefaultWriter AcquireDefaultWriter(JsWritableStream stream)
    {
        var writer = new JsWritableStreamDefaultWriter(stream.Engine, stream.Realm)
        {
            _prototype = stream.Realm.Intrinsics.WritableStreamDefaultWriter.PrototypeObject,
        };

        SetUpDefaultWriter(writer, stream);
        return writer;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-writable-stream-default-writer
    /// </summary>
    internal static void SetUpDefaultWriter(JsWritableStreamDefaultWriter writer, JsWritableStream stream)
    {
        var engine = writer.Engine;
        var realm = writer.Realm;

        if (IsLocked(stream))
        {
            Throw.TypeError(realm, "This writable stream is already locked for exclusive writing by another writer");
        }

        writer.Stream = stream;
        stream.Writer = writer;

        var ready = StreamPromises.NewPromise(engine, realm);
        var closed = StreamPromises.NewPromise(engine, realm);
        writer.ReadyCapability = ready;
        writer.ClosedCapability = closed;

        switch (stream.State)
        {
            case WritableStreamState.Writable:
                // A writer acquired while the stream is applying backpressure starts out not ready.
                if (CloseQueuedOrInFlight(stream) || !stream.Backpressure)
                {
                    ready.Resolve(JsValue.Undefined);
                }

                break;

            case WritableStreamState.Erroring:
                StreamPromises.MarkHandled(StreamPromises.PromiseOf(ready));
                ready.Reject(stream.StoredError);
                break;

            case WritableStreamState.Closed:
                ready.Resolve(JsValue.Undefined);
                closed.Resolve(JsValue.Undefined);
                break;

            default:
                StreamPromises.MarkHandled(StreamPromises.PromiseOf(ready));
                ready.Reject(stream.StoredError);
                StreamPromises.MarkHandled(StreamPromises.PromiseOf(closed));
                closed.Reject(stream.StoredError);
                break;
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-abort
    /// </summary>
    internal static JsPromise DefaultWriterAbort(JsWritableStreamDefaultWriter writer, JsValue reason)
        => Abort(writer.Stream!, reason);

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-close
    /// </summary>
    internal static JsPromise DefaultWriterClose(JsWritableStreamDefaultWriter writer) => Close(writer.Stream!);

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-close-with-error-propagation — what
    /// piping uses to close the destination: a stream that is already closing or closed is not closed again,
    /// and an errored one reports its error rather than a "cannot close" <c>TypeError</c>.
    /// </summary>
    internal static JsPromise DefaultWriterCloseWithErrorPropagation(JsWritableStreamDefaultWriter writer)
    {
        var stream = writer.Stream!;
        var state = stream.State;

        if (CloseQueuedOrInFlight(stream) || state == WritableStreamState.Closed)
        {
            return StreamPromises.ResolvedWithUndefined(stream.Engine, stream.Realm);
        }

        if (state == WritableStreamState.Errored)
        {
            return StreamPromises.RejectedWith(stream.Engine, stream.Realm, stream.StoredError);
        }

        return DefaultWriterClose(writer);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-ensure-closed-promise-rejected
    /// </summary>
    private static void EnsureClosedPromiseRejected(JsWritableStreamDefaultWriter writer, JsValue error)
    {
        if (writer.ClosedPromise.State != PromiseState.Pending)
        {
            writer.ClosedCapability = StreamPromises.NewPromise(writer.Engine, writer.Realm);
        }

        writer.ClosedCapability.Reject(error);
        StreamPromises.MarkHandled(writer.ClosedPromise);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-ensure-ready-promise-rejected
    /// </summary>
    private static void EnsureReadyPromiseRejected(JsWritableStreamDefaultWriter writer, JsValue error)
    {
        if (writer.ReadyPromise.State != PromiseState.Pending)
        {
            writer.ReadyCapability = StreamPromises.NewPromise(writer.Engine, writer.Realm);
        }

        writer.ReadyCapability.Reject(error);
        StreamPromises.MarkHandled(writer.ReadyPromise);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-get-desired-size
    /// </summary>
    internal static double? DefaultWriterGetDesiredSize(JsWritableStreamDefaultWriter writer)
    {
        var stream = writer.Stream!;

        return stream.State switch
        {
            WritableStreamState.Errored or WritableStreamState.Erroring => null,
            WritableStreamState.Closed => 0,
            _ => WritableStreamDefaultControllerOperations.GetDesiredSize(stream.Controller),
        };
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-release
    /// </summary>
    internal static void DefaultWriterRelease(JsWritableStreamDefaultWriter writer)
    {
        var stream = writer.Stream!;

        var released = writer.Realm.Intrinsics.TypeError.Construct(
            "Writer was released and can no longer be used to monitor the stream's closedness");

        EnsureReadyPromiseRejected(writer, released);

        // The state transitions to "errored" before the sink's abort() runs, but the writer's closed promise
        // is not rejected until afterwards, so testing the state would not be enough here.
        EnsureClosedPromiseRejected(writer, released);

        stream.Writer = null;
        writer.Stream = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#writable-stream-default-writer-write
    /// </summary>
    internal static JsPromise DefaultWriterWrite(JsWritableStreamDefaultWriter writer, JsValue chunk)
    {
        var stream = writer.Stream!;
        var engine = stream.Engine;
        var realm = stream.Realm;
        var controller = stream.Controller;

        // The strategy's size() runs first and can do anything, including release this very writer.
        var chunkSize = WritableStreamDefaultControllerOperations.GetChunkSize(controller, chunk);

        if (!ReferenceEquals(stream, writer.Stream))
        {
            return StreamPromises.RejectedWith(
                engine, realm, realm.Intrinsics.TypeError.Construct("Cannot write to a stream using a released writer"));
        }

        var state = stream.State;
        if (state == WritableStreamState.Errored)
        {
            return StreamPromises.RejectedWith(engine, realm, stream.StoredError);
        }

        if (CloseQueuedOrInFlight(stream) || state == WritableStreamState.Closed)
        {
            return StreamPromises.RejectedWith(
                engine, realm, realm.Intrinsics.TypeError.Construct("The stream is closing or closed and cannot be written to"));
        }

        if (state == WritableStreamState.Erroring)
        {
            return StreamPromises.RejectedWith(engine, realm, stream.StoredError);
        }

        var promise = AddWriteRequest(stream);
        WritableStreamDefaultControllerOperations.Write(controller, chunk, chunkSize);
        return promise;
    }

    /// <summary>The specification's own spelling of a state, for the messages that name one.</summary>
    private static string StateName(WritableStreamState state) => state switch
    {
        WritableStreamState.Writable => "writable",
        WritableStreamState.Erroring => "erroring",
        WritableStreamState.Closed => "closed",
        _ => "errored",
    };
}
#endif

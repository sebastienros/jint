#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The readable-stream abstract operations: "working with readable streams", "interfacing with controllers"
/// and "readers".
/// <para>
/// https://streams.spec.whatwg.org/#rs-abstract-ops
/// </para>
/// </summary>
internal static class ReadableStreamOperations
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#is-readable-stream-locked
    /// </summary>
    internal static bool IsLocked(JsReadableStream stream) => stream.Reader is not null;

    /// <summary>
    /// https://streams.spec.whatwg.org/#create-readable-stream — the entry point for streams the engine
    /// builds itself (tee's two branches, <c>ReadableStream.from</c>, a transform stream's readable side)
    /// rather than ones a script constructs.
    /// </summary>
    internal static JsReadableStream CreateReadableStream(
        Engine engine,
        Realm realm,
        Func<JsValue> startAlgorithm,
        Func<JsPromise> pullAlgorithm,
        Func<JsValue, JsPromise> cancelAlgorithm,
        double highWaterMark = 1,
        Func<JsValue, double>? sizeAlgorithm = null)
    {
        var stream = new JsReadableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStream.PrototypeObject,
        };

        var controller = new JsReadableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStreamDefaultController.PrototypeObject,
        };

        SetUpDefaultController(
            stream,
            controller,
            startAlgorithm,
            pullAlgorithm,
            cancelAlgorithm,
            highWaterMark,
            sizeAlgorithm ?? (static _ => 1));

        return stream;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#create-readable-byte-stream — the byte-stream counterpart of
    /// <see cref="CreateReadableStream"/>, for the streams the engine builds itself. The standard fixes its
    /// high water mark at 0 and gives it no automatic allocation: a stream created this way pulls only when
    /// a consumer asks.
    /// </summary>
    internal static JsReadableStream CreateReadableByteStream(
        Engine engine,
        Realm realm,
        Func<JsValue> startAlgorithm,
        Func<JsPromise> pullAlgorithm,
        Func<JsValue, JsPromise> cancelAlgorithm)
    {
        var stream = new JsReadableStream(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStream.PrototypeObject,
        };

        var controller = new JsReadableByteStreamController(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableByteStreamController.PrototypeObject,
        };

        ReadableByteStreamControllerOperations.SetUp(
            stream, controller, startAlgorithm, pullAlgorithm, cancelAlgorithm, highWaterMark: 0, autoAllocateChunkSize: null);

        return stream;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-tee — which of the two tee algorithms applies is
    /// decided by the kind of controller the stream has, and only here.
    /// </summary>
    internal static (JsReadableStream Branch1, JsReadableStream Branch2) Tee(JsReadableStream stream)
    {
        if (stream.Controller is JsReadableByteStreamController)
        {
            return ReadableByteStreamTee.Tee(stream);
        }

        return ReadableStreamTee.Tee(stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-cancel
    /// </summary>
    internal static JsPromise Cancel(JsReadableStream stream, JsValue reason)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        stream.Disturbed = true;

        if (stream.State == ReadableStreamState.Closed)
        {
            return StreamPromises.ResolvedWithUndefined(engine, realm);
        }

        if (stream.State == ReadableStreamState.Errored)
        {
            return StreamPromises.RejectedWith(engine, realm, stream.StoredError);
        }

        Close(stream);

        // A cancelled BYOB read does not get its memory back — its close steps receive undefined rather
        // than an empty view — which is the one place the two close paths differ.
        if (stream.Reader is JsReadableStreamBYOBReader byobReader)
        {
            var readIntoRequests = byobReader.ReadIntoRequests;
            while (readIntoRequests.Count > 0)
            {
                readIntoRequests.Dequeue().CloseSteps(JsValue.Undefined);
            }
        }

        var sourceCancelPromise = stream.Controller.CancelSteps(reason);

        // "Return the result of reacting to sourceCancelPromise with a fulfillment step that returns
        // undefined" — the underlying source's own fulfillment value is deliberately discarded, so
        // `await stream.cancel()` is always undefined.
        return StreamPromises.TransformPromiseWith(engine, realm, sourceCancelPromise, static _ => JsValue.Undefined, onRejected: null);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-close
    /// </summary>
    internal static void Close(JsReadableStream stream)
    {
        stream.State = ReadableStreamState.Closed;

        var reader = stream.Reader;
        if (reader is null)
        {
            return;
        }

        reader.ClosedCapability.Resolve(JsValue.Undefined);

        // Every outstanding read() resolves with { value: undefined, done: true }. The list is emptied
        // before the steps run, because a close step may start a fresh read. A BYOB reader's pending
        // read-into requests are deliberately not touched here: they are answered by the byte controller,
        // which has a buffer to hand back, or by ReadableStreamCancel, which has not.
        if (reader is not JsReadableStreamDefaultReader defaultReader)
        {
            return;
        }

        var readRequests = defaultReader.ReadRequests;
        while (readRequests.Count > 0)
        {
            readRequests.Dequeue().CloseSteps();
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-error
    /// </summary>
    internal static void Error(JsReadableStream stream, JsValue error)
    {
        stream.State = ReadableStreamState.Errored;
        stream.StoredError = error;

        var reader = stream.Reader;
        if (reader is null)
        {
            return;
        }

        reader.ClosedCapability.Reject(error);
        StreamPromises.MarkHandled(reader.ClosedPromise);

        if (reader is JsReadableStreamDefaultReader defaultReader)
        {
            DefaultReaderErrorReadRequests(defaultReader, error);
        }
        else
        {
            BYOBReaderErrorReadIntoRequests((JsReadableStreamBYOBReader) reader, error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-add-read-request
    /// </summary>
    internal static void AddReadRequest(JsReadableStream stream, ReadRequest readRequest)
        => ((JsReadableStreamDefaultReader) stream.Reader!).ReadRequests.Enqueue(readRequest);

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-add-read-into-request
    /// </summary>
    internal static void AddReadIntoRequest(JsReadableStream stream, ReadIntoRequest readIntoRequest)
        => ((JsReadableStreamBYOBReader) stream.Reader!).ReadIntoRequests.Enqueue(readIntoRequest);

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-fulfill-read-request
    /// </summary>
    internal static void FulfillReadRequest(JsReadableStream stream, JsValue chunk, bool done)
    {
        var readRequest = ((JsReadableStreamDefaultReader) stream.Reader!).ReadRequests.Dequeue();
        if (done)
        {
            readRequest.CloseSteps();
        }
        else
        {
            readRequest.ChunkSteps(chunk);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-fulfill-read-into-request — the close steps take the
    /// chunk, so a BYOB read of a closing stream still gets its buffer back.
    /// </summary>
    internal static void FulfillReadIntoRequest(JsReadableStream stream, JsValue chunk, bool done)
    {
        var readIntoRequest = ((JsReadableStreamBYOBReader) stream.Reader!).ReadIntoRequests.Dequeue();
        if (done)
        {
            readIntoRequest.CloseSteps(chunk);
        }
        else
        {
            readIntoRequest.ChunkSteps(chunk);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-get-num-read-requests
    /// </summary>
    internal static int GetNumReadRequests(JsReadableStream stream)
        => ((JsReadableStreamDefaultReader) stream.Reader!).ReadRequests.Count;

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-get-num-read-into-requests
    /// </summary>
    internal static int GetNumReadIntoRequests(JsReadableStream stream)
        => ((JsReadableStreamBYOBReader) stream.Reader!).ReadIntoRequests.Count;

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-has-default-reader
    /// </summary>
    internal static bool HasDefaultReader(JsReadableStream stream) => stream.Reader is JsReadableStreamDefaultReader;

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-has-byob-reader
    /// </summary>
    internal static bool HasBYOBReader(JsReadableStream stream) => stream.Reader is JsReadableStreamBYOBReader;

    /// <summary>
    /// https://streams.spec.whatwg.org/#acquire-readable-stream-reader
    /// </summary>
    internal static JsReadableStreamDefaultReader AcquireDefaultReader(JsReadableStream stream)
    {
        var reader = new JsReadableStreamDefaultReader(stream.Engine, stream.Realm)
        {
            _prototype = stream.Realm.Intrinsics.ReadableStreamDefaultReader.PrototypeObject,
        };

        SetUpDefaultReader(reader, stream);
        return reader;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#acquire-readable-stream-byob-reader
    /// </summary>
    internal static JsReadableStreamBYOBReader AcquireBYOBReader(JsReadableStream stream)
    {
        var reader = new JsReadableStreamBYOBReader(stream.Engine, stream.Realm)
        {
            _prototype = stream.Realm.Intrinsics.ReadableStreamBYOBReader.PrototypeObject,
        };

        SetUpBYOBReader(reader, stream);
        return reader;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-readable-stream-default-reader
    /// </summary>
    internal static void SetUpDefaultReader(JsReadableStreamDefaultReader reader, JsReadableStream stream)
    {
        if (IsLocked(stream))
        {
            Throw.TypeError(reader.Realm, "This readable stream is already locked for exclusive reading by another reader");
        }

        ReaderGenericInitialize(reader, stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-readable-stream-byob-reader — a BYOB reader can only be
    /// acquired from a stream whose controller is a byte controller, which is the whole of what makes
    /// getReader({ mode: "byob" }) refuse an ordinary stream.
    /// </summary>
    internal static void SetUpBYOBReader(JsReadableStreamBYOBReader reader, JsReadableStream stream)
    {
        if (IsLocked(stream))
        {
            Throw.TypeError(reader.Realm, "This readable stream is already locked for exclusive reading by another reader");
        }

        if (stream.Controller is not JsReadableByteStreamController)
        {
            Throw.TypeError(reader.Realm, "Cannot construct a ReadableStreamBYOBReader for a stream not constructed with a byte source");
        }

        ReaderGenericInitialize(reader, stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-reader-generic-initialize
    /// </summary>
    private static void ReaderGenericInitialize(JsReadableStreamReader reader, JsReadableStream stream)
    {
        var engine = reader.Engine;
        var realm = reader.Realm;

        reader.Stream = stream;
        stream.Reader = reader;

        var capability = StreamPromises.NewPromise(engine, realm);
        reader.ClosedCapability = capability;

        switch (stream.State)
        {
            case ReadableStreamState.Readable:
                break;

            case ReadableStreamState.Closed:
                capability.Resolve(JsValue.Undefined);
                break;

            default:
                StreamPromises.MarkHandled(StreamPromises.PromiseOf(capability));
                capability.Reject(stream.StoredError);
                break;
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-reader-generic-cancel
    /// </summary>
    internal static JsPromise ReaderGenericCancel(JsReadableStreamReader reader, JsValue reason)
        => Cancel(reader.Stream!, reason);

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-reader-generic-release
    /// </summary>
    private static void ReaderGenericRelease(JsReadableStreamReader reader)
    {
        var engine = reader.Engine;
        var realm = reader.Realm;
        var stream = reader.Stream!;

        var released = realm.Intrinsics.TypeError.Construct(
            "Reader was released and can no longer be used to monitor the stream's closedness");

        if (stream.State == ReadableStreamState.Readable)
        {
            reader.ClosedCapability.Reject(released);
        }
        else
        {
            // The closed promise has already settled, so it cannot be rejected: the reader is given a
            // freshly rejected one instead, which is what makes `reader.closed` observably a different
            // promise after releasing the lock on a closed stream.
            reader.ClosedCapability = StreamPromises.NewPromise(engine, realm);
            reader.ClosedCapability.Reject(released);
        }

        StreamPromises.MarkHandled(reader.ClosedPromise);

        // The default controller's [[ReleaseSteps]] do nothing; the byte controller's downgrades the
        // pull-into descriptor the underlying source may be writing into right now.
        stream.Controller.ReleaseSteps();

        stream.Reader = null;
        reader.Stream = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-default-reader-read
    /// </summary>
    internal static void DefaultReaderRead(JsReadableStreamDefaultReader reader, ReadRequest readRequest)
    {
        var stream = reader.Stream!;
        stream.Disturbed = true;

        switch (stream.State)
        {
            case ReadableStreamState.Closed:
                readRequest.CloseSteps();
                break;

            case ReadableStreamState.Errored:
                readRequest.ErrorSteps(stream.StoredError);
                break;

            default:
                stream.Controller.PullSteps(readRequest);
                break;
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-stream-byob-reader-read
    /// </summary>
    internal static void BYOBReaderRead(
        JsReadableStreamBYOBReader reader,
        in StreamBufferOperations.ArrayBufferViewInfo view,
        int min,
        ReadIntoRequest readIntoRequest)
    {
        var stream = reader.Stream!;
        stream.Disturbed = true;

        if (stream.State == ReadableStreamState.Errored)
        {
            readIntoRequest.ErrorSteps(stream.StoredError);
            return;
        }

        // Unlike a default read, a closed stream is not short-circuited here: the byte controller answers
        // it, because it is the one that can hand the caller's memory back as an empty view.
        ReadableByteStreamControllerOperations.PullInto(
            (JsReadableByteStreamController) stream.Controller, in view, min, readIntoRequest);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-readablestreamdefaultreaderrelease
    /// </summary>
    internal static void DefaultReaderRelease(JsReadableStreamDefaultReader reader)
    {
        var realm = reader.Realm;
        ReaderGenericRelease(reader);
        DefaultReaderErrorReadRequests(reader, realm.Intrinsics.TypeError.Construct("Reader was released"));
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-readablestreamdefaultreadererrorreadrequests
    /// </summary>
    private static void DefaultReaderErrorReadRequests(JsReadableStreamDefaultReader reader, JsValue error)
    {
        var readRequests = reader.ReadRequests;
        while (readRequests.Count > 0)
        {
            readRequests.Dequeue().ErrorSteps(error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-readablestreambyobreaderrelease
    /// </summary>
    internal static void BYOBReaderRelease(JsReadableStreamBYOBReader reader)
    {
        var realm = reader.Realm;
        ReaderGenericRelease(reader);
        BYOBReaderErrorReadIntoRequests(reader, realm.Intrinsics.TypeError.Construct("Reader was released"));
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#abstract-opdef-readablestreambyobreadererrorreadintorequests
    /// </summary>
    private static void BYOBReaderErrorReadIntoRequests(JsReadableStreamBYOBReader reader, JsValue error)
    {
        var readIntoRequests = reader.ReadIntoRequests;
        while (readIntoRequests.Count > 0)
        {
            readIntoRequests.Dequeue().ErrorSteps(error);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-readable-stream-default-controller
    /// </summary>
    internal static void SetUpDefaultController(
        JsReadableStream stream,
        JsReadableStreamDefaultController controller,
        Func<JsValue> startAlgorithm,
        Func<JsPromise> pullAlgorithm,
        Func<JsValue, JsPromise> cancelAlgorithm,
        double highWaterMark,
        Func<JsValue, double> sizeAlgorithm)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        controller.Stream = stream;
        controller.Queue.Reset();
        controller.Started = false;
        controller.CloseRequested = false;
        controller.PullAgain = false;
        controller.Pulling = false;
        controller.StrategySizeAlgorithm = sizeAlgorithm;
        controller.StrategyHighWaterMark = highWaterMark;
        controller.PullAlgorithm = pullAlgorithm;
        controller.CancelAlgorithm = cancelAlgorithm;
        stream.Controller = controller;

        // "Let startResult be the result of performing startAlgorithm. (This might throw an exception.)" —
        // the start callback's return type is `any`, so an exception it raises is not converted to a
        // rejection and propagates out of the ReadableStream constructor.
        var startResult = startAlgorithm();
        var startPromise = StreamPromises.ResolvedWith(realm, startResult);

        StreamPromises.UponPromise(
            engine,
            startPromise,
            _ =>
            {
                controller.Started = true;
                ReadableStreamDefaultControllerOperations.CallPullIfNeeded(controller);
            },
            reason => ReadableStreamDefaultControllerOperations.Error(controller, reason));
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-readable-stream-default-controller-from-underlying-source
    /// </summary>
    internal static void SetUpDefaultControllerFromUnderlyingSource(
        JsReadableStream stream,
        JsValue underlyingSource,
        in StreamDictionaries.UnderlyingSourceRecord source,
        double highWaterMark,
        Func<JsValue, double> sizeAlgorithm)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        var controller = new JsReadableStreamDefaultController(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableStreamDefaultController.PrototypeObject,
        };

        var start = source.Start;
        var pull = source.Pull;
        var cancel = source.Cancel;

        Func<JsValue> startAlgorithm = start is null
            ? static () => JsValue.Undefined
            : () => start.Call(underlyingSource, controller);

        Func<JsPromise> pullAlgorithm = pull is null
            ? () => StreamPromises.ResolvedWithUndefined(engine, realm)
            : () => StreamPromises.PromiseCall(engine, realm, pull, underlyingSource, [controller]);

        Func<JsValue, JsPromise> cancelAlgorithm = cancel is null
            ? _ => StreamPromises.ResolvedWithUndefined(engine, realm)
            : reason => StreamPromises.PromiseCall(engine, realm, cancel, underlyingSource, [reason]);

        SetUpDefaultController(stream, controller, startAlgorithm, pullAlgorithm, cancelAlgorithm, highWaterMark, sizeAlgorithm);
    }
}
#endif

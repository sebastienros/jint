#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The <c>ReadableByteStreamController</c> abstract operations, plus its three internal methods
/// <c>[[PullSteps]]</c>, <c>[[CancelSteps]]</c> and <c>[[ReleaseSteps]]</c>.
/// <para>
/// https://streams.spec.whatwg.org/#rbs-controller-abstract-ops
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The one thing to hold on to while reading this file is that <b>every buffer that crosses the boundary is
/// transferred, not shared</b>: a view handed to <c>enqueue()</c>, to a BYOB <c>read()</c> or to
/// <c>respondWithNewView()</c> has its <c>ArrayBuffer</c> detached and its bytes re-exposed through a new
/// buffer the other side owns. That is what makes a byte stream's zero-copy promise safe, and it is why the
/// caller's view is unusable the moment it is handed over.
/// </para>
/// <para>
/// The other is that a byte stream's queue and its pending pull-into descriptors are two halves of one
/// picture: bytes arrive into whichever is appropriate and are then moved between them —
/// <see cref="FillPullIntoDescriptorFromQueue"/> drains the queue into a waiting BYOB buffer, and
/// <see cref="EnqueueDetachedPullIntoToQueue"/> pushes an abandoned buffer's bytes back the other way.
/// </para>
/// </remarks>
internal static class ReadableByteStreamControllerOperations
{
    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-call-pull-if-needed
    /// </summary>
    internal static void CallPullIfNeeded(JsReadableByteStreamController controller)
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
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-should-call-pull
    /// </summary>
    private static bool ShouldCallPull(JsReadableByteStreamController controller)
    {
        var stream = controller.Stream;

        if (stream.State != ReadableStreamState.Readable || controller.CloseRequested || !controller.Started)
        {
            return false;
        }

        // A waiting consumer of either kind outranks the high water mark.
        if (ReadableStreamOperations.HasDefaultReader(stream) && ReadableStreamOperations.GetNumReadRequests(stream) > 0)
        {
            return true;
        }

        if (ReadableStreamOperations.HasBYOBReader(stream) && ReadableStreamOperations.GetNumReadIntoRequests(stream) > 0)
        {
            return true;
        }

        return GetDesiredSize(controller) > 0;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-clear-algorithms
    /// </summary>
    internal static void ClearAlgorithms(JsReadableByteStreamController controller)
    {
        controller.PullAlgorithm = null;
        controller.CancelAlgorithm = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-clear-pending-pull-intos
    /// </summary>
    internal static void ClearPendingPullIntos(JsReadableByteStreamController controller)
    {
        InvalidateByobRequest(controller);
        controller.PendingPullIntos.Clear();
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-close. Raises a <c>TypeError</c> —
    /// and errors the stream with it — when the head pull-into descriptor holds a partial element, because
    /// there is no way to hand back half of one.
    /// </summary>
    internal static void Close(JsReadableByteStreamController controller)
    {
        var stream = controller.Stream;

        if (controller.CloseRequested || stream.State != ReadableStreamState.Readable)
        {
            return;
        }

        // Already-enqueued bytes stay readable; the stream only becomes closed once they have been read.
        if (controller.Queue.TotalSize > 0)
        {
            controller.CloseRequested = true;
            return;
        }

        if (controller.PendingPullIntos.Count > 0)
        {
            var firstPendingPullInto = controller.PendingPullIntos.Peek();
            if (firstPendingPullInto.BytesFilled % firstPendingPullInto.ElementSize != 0)
            {
                var error = controller.Realm.Intrinsics.TypeError.Construct("Insufficient bytes to fill elements in the given buffer");
                Error(controller, error);
                ThrowValue(controller.Engine, error);
            }
        }

        ClearAlgorithms(controller);
        ReadableStreamOperations.Close(stream);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-commit-pull-into-descriptor
    /// </summary>
    private static void CommitPullIntoDescriptor(JsReadableStream stream, PullIntoDescriptor pullIntoDescriptor)
    {
        var done = stream.State == ReadableStreamState.Closed;
        var filledView = ConvertPullIntoDescriptor(stream.Realm, pullIntoDescriptor);

        if (pullIntoDescriptor.ReaderType == PullIntoReaderType.Default)
        {
            ReadableStreamOperations.FulfillReadRequest(stream, filledView, done);
        }
        else
        {
            ReadableStreamOperations.FulfillReadIntoRequest(stream, filledView, done);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-convert-pull-into-descriptor — the
    /// buffer is transferred one last time, so the view the consumer receives owns it and the controller
    /// can no longer write into it.
    /// </summary>
    private static ObjectInstance ConvertPullIntoDescriptor(Realm realm, PullIntoDescriptor pullIntoDescriptor)
    {
        var bytesFilled = pullIntoDescriptor.BytesFilled;
        var elementSize = pullIntoDescriptor.ElementSize;

        var buffer = StreamBufferOperations.TransferArrayBuffer(realm, pullIntoDescriptor.Buffer);

        return StreamBufferOperations.ConstructView(
            realm, pullIntoDescriptor.ViewElementType, buffer, pullIntoDescriptor.ByteOffset, bytesFilled / elementSize);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-enqueue
    /// </summary>
    internal static void Enqueue(JsReadableByteStreamController controller, in StreamBufferOperations.ArrayBufferViewInfo chunk)
    {
        var stream = controller.Stream;
        var realm = controller.Realm;

        if (controller.CloseRequested || stream.State != ReadableStreamState.Readable)
        {
            return;
        }

        if (chunk.Buffer.IsDetachedBuffer)
        {
            Throw.TypeError(realm, "The chunk's buffer is detached and so cannot be enqueued");
        }

        var byteOffset = chunk.ByteOffset;
        var byteLength = chunk.ByteLength;
        var transferredBuffer = StreamBufferOperations.TransferArrayBuffer(realm, chunk.Buffer);

        if (controller.PendingPullIntos.Count > 0)
        {
            var firstPendingPullInto = controller.PendingPullIntos.Peek();
            if (firstPendingPullInto.Buffer.IsDetachedBuffer)
            {
                Throw.TypeError(realm, "The BYOB request's buffer has been detached and so cannot be filled with an enqueued chunk");
            }

            InvalidateByobRequest(controller);
            firstPendingPullInto.Buffer = StreamBufferOperations.TransferArrayBuffer(realm, firstPendingPullInto.Buffer);

            if (firstPendingPullInto.ReaderType == PullIntoReaderType.None)
            {
                EnqueueDetachedPullIntoToQueue(controller, firstPendingPullInto);
            }
        }

        if (ReadableStreamOperations.HasDefaultReader(stream))
        {
            ProcessReadRequestsUsingQueue(controller);

            if (ReadableStreamOperations.GetNumReadRequests(stream) == 0)
            {
                controller.Queue.Enqueue(transferredBuffer, byteOffset, byteLength);
            }
            else
            {
                // A pull-into descriptor for a default reader (automatic allocation) is discarded: the
                // waiting read request is about to be served from this chunk instead.
                if (controller.PendingPullIntos.Count > 0)
                {
                    ShiftPendingPullInto(controller);
                }

                var transferredView = StreamBufferOperations.ConstructUint8Array(realm, transferredBuffer, byteOffset, byteLength);
                ReadableStreamOperations.FulfillReadRequest(stream, transferredView, done: false);
            }
        }
        else if (ReadableStreamOperations.HasBYOBReader(stream))
        {
            controller.Queue.Enqueue(transferredBuffer, byteOffset, byteLength);

            var filledPullIntos = ProcessPullIntoDescriptorsUsingQueue(controller);
            foreach (var filledPullInto in filledPullIntos)
            {
                CommitPullIntoDescriptor(stream, filledPullInto);
            }
        }
        else
        {
            controller.Queue.Enqueue(transferredBuffer, byteOffset, byteLength);
        }

        CallPullIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-enqueue-cloned-chunk-to-queue — a
    /// copy rather than a transfer, because the bytes being re-queued are in a buffer somebody else still
    /// owns.
    /// </summary>
    private static void EnqueueClonedChunkToQueue(JsReadableByteStreamController controller, JsArrayBuffer buffer, int byteOffset, int byteLength)
    {
        JsArrayBuffer clonedChunk;
        try
        {
            clonedChunk = StreamBufferOperations.CloneArrayBufferRegion(controller.Realm, buffer, byteOffset, byteLength);
        }
        catch (JavaScriptException e)
        {
            Error(controller, e.Error);
            throw;
        }

        controller.Queue.Enqueue(clonedChunk, 0, byteLength);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-enqueue-detached-pull-into-to-queue
    /// — what happens to the bytes already written into a buffer whose reader has gone away.
    /// </summary>
    private static void EnqueueDetachedPullIntoToQueue(JsReadableByteStreamController controller, PullIntoDescriptor firstDescriptor)
    {
        if (firstDescriptor.BytesFilled > 0)
        {
            EnqueueClonedChunkToQueue(controller, firstDescriptor.Buffer, firstDescriptor.ByteOffset, firstDescriptor.BytesFilled);
        }

        ShiftPendingPullInto(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-error
    /// </summary>
    internal static void Error(JsReadableByteStreamController controller, JsValue error)
    {
        var stream = controller.Stream;
        if (stream.State != ReadableStreamState.Readable)
        {
            return;
        }

        ClearPendingPullIntos(controller);
        controller.Queue.Reset();
        ClearAlgorithms(controller);
        ReadableStreamOperations.Error(stream, error);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-fill-pull-into-descriptor-from-queue
    /// </summary>
    /// <returns>Whether the descriptor is now filled to at least its minimum fill, and so ready to commit.</returns>
    private static bool FillPullIntoDescriptorFromQueue(JsReadableByteStreamController controller, PullIntoDescriptor pullIntoDescriptor)
    {
        var queue = controller.Queue;

        var maxBytesToCopy = (int) System.Math.Min(queue.TotalSize, pullIntoDescriptor.ByteLength - pullIntoDescriptor.BytesFilled);
        var maxBytesFilled = pullIntoDescriptor.BytesFilled + maxBytesToCopy;

        var totalBytesToCopyRemaining = maxBytesToCopy;
        var ready = false;

        var remainderBytes = maxBytesFilled % pullIntoDescriptor.ElementSize;
        var maxAlignedBytes = maxBytesFilled - remainderBytes;

        // A descriptor that cannot yet reach its minimum fill stays at the head of the queue, so the
        // underlying source can keep filling it — that is what makes read(view, { min }) wait.
        if (maxAlignedBytes >= pullIntoDescriptor.MinimumFill)
        {
            totalBytesToCopyRemaining = maxAlignedBytes - pullIntoDescriptor.BytesFilled;
            ready = true;
        }

        while (totalBytesToCopyRemaining > 0)
        {
            var headOfQueue = queue.Peek();
            var bytesToCopy = System.Math.Min(totalBytesToCopyRemaining, headOfQueue.ByteLength);
            var destStart = pullIntoDescriptor.ByteOffset + pullIntoDescriptor.BytesFilled;

            StreamBufferOperations.CopyDataBlockBytes(
                pullIntoDescriptor.Buffer, destStart, headOfQueue.Buffer, headOfQueue.ByteOffset, bytesToCopy);

            queue.ConsumeFromHead(bytesToCopy);
            pullIntoDescriptor.BytesFilled += bytesToCopy;
            totalBytesToCopyRemaining -= bytesToCopy;
        }

        return ready;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-fill-read-request-from-queue
    /// </summary>
    private static void FillReadRequestFromQueue(JsReadableByteStreamController controller, ReadRequest readRequest)
    {
        var entry = controller.Queue.Dequeue();
        HandleQueueDrain(controller);

        var view = StreamBufferOperations.ConstructUint8Array(controller.Realm, entry.Buffer, entry.ByteOffset, entry.ByteLength);
        readRequest.ChunkSteps(view);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-get-byob-request — the request is
    /// created lazily, on the first read of <c>controller.byobRequest</c>, and invalidated the moment the
    /// buffer behind it moves.
    /// </summary>
    internal static JsReadableStreamBYOBRequest? GetByobRequest(JsReadableByteStreamController controller)
    {
        if (controller.ByobRequest is null && controller.PendingPullIntos.Count > 0)
        {
            var firstDescriptor = controller.PendingPullIntos.Peek();
            var realm = controller.Realm;

            var view = StreamBufferOperations.ConstructUint8Array(
                realm,
                firstDescriptor.Buffer,
                firstDescriptor.ByteOffset + firstDescriptor.BytesFilled,
                firstDescriptor.ByteLength - firstDescriptor.BytesFilled);

            controller.ByobRequest = new JsReadableStreamBYOBRequest(controller.Engine, realm)
            {
                _prototype = realm.Intrinsics.ReadableStreamBYOBRequest.PrototypeObject,
                Controller = controller,
                View = view,
            };
        }

        return controller.ByobRequest;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-get-desired-size
    /// </summary>
    internal static double? GetDesiredSize(JsReadableByteStreamController controller)
    {
        return controller.Stream.State switch
        {
            ReadableStreamState.Errored => null,
            ReadableStreamState.Closed => 0,
            _ => controller.StrategyHighWaterMark - controller.Queue.TotalSize,
        };
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-handle-queue-drain
    /// </summary>
    private static void HandleQueueDrain(JsReadableByteStreamController controller)
    {
        if (controller.Queue.TotalSize == 0 && controller.CloseRequested)
        {
            ClearAlgorithms(controller);
            ReadableStreamOperations.Close(controller.Stream);
        }
        else
        {
            CallPullIfNeeded(controller);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-invalidate-byob-request — the
    /// request object survives in whatever script kept it, with a null <c>view</c> and a <c>respond()</c>
    /// that raises a <c>TypeError</c>.
    /// </summary>
    internal static void InvalidateByobRequest(JsReadableByteStreamController controller)
    {
        var byobRequest = controller.ByobRequest;
        if (byobRequest is null)
        {
            return;
        }

        byobRequest.Controller = null;
        byobRequest.View = null;
        controller.ByobRequest = null;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-process-pull-into-descriptors-using-queue
    /// </summary>
    private static List<PullIntoDescriptor> ProcessPullIntoDescriptorsUsingQueue(JsReadableByteStreamController controller)
    {
        var filledPullIntos = new List<PullIntoDescriptor>();

        while (controller.PendingPullIntos.Count > 0)
        {
            if (controller.Queue.TotalSize == 0)
            {
                break;
            }

            var pullIntoDescriptor = controller.PendingPullIntos.Peek();
            if (FillPullIntoDescriptorFromQueue(controller, pullIntoDescriptor))
            {
                ShiftPendingPullInto(controller);
                filledPullIntos.Add(pullIntoDescriptor);
            }
        }

        return filledPullIntos;
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-process-read-requests-using-queue
    /// </summary>
    private static void ProcessReadRequestsUsingQueue(JsReadableByteStreamController controller)
    {
        var reader = (JsReadableStreamDefaultReader) controller.Stream.Reader!;

        while (reader.ReadRequests.Count > 0)
        {
            if (controller.Queue.TotalSize == 0)
            {
                return;
            }

            var readRequest = reader.ReadRequests.Dequeue();
            FillReadRequestFromQueue(controller, readRequest);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-pull-into — a BYOB read's whole
    /// journey: transfer the caller's buffer in, then either fill it from the queue right away or park it
    /// as a pull-into descriptor for the underlying source to write into.
    /// </summary>
    internal static void PullInto(
        JsReadableByteStreamController controller,
        in StreamBufferOperations.ArrayBufferViewInfo view,
        int min,
        ReadIntoRequest readIntoRequest)
    {
        var stream = controller.Stream;
        var realm = controller.Realm;

        var elementSize = view.ElementSize;
        var minimumFill = min * elementSize;

        JsArrayBuffer buffer;
        try
        {
            buffer = StreamBufferOperations.TransferArrayBuffer(realm, view.Buffer);
        }
        catch (JavaScriptException e)
        {
            readIntoRequest.ErrorSteps(e.Error);
            return;
        }

        var pullIntoDescriptor = new PullIntoDescriptor
        {
            Buffer = buffer,
            BufferByteLength = buffer.ArrayBufferByteLength,
            ByteOffset = view.ByteOffset,
            ByteLength = view.ByteLength,
            BytesFilled = 0,
            MinimumFill = minimumFill,
            ElementSize = elementSize,
            ViewElementType = view.ElementType,
            ReaderType = PullIntoReaderType.Byob,
        };

        if (controller.PendingPullIntos.Count > 0)
        {
            // No CallPullIfNeeded: the desired size has not changed, and the source already knows there is
            // at least one pending read(view).
            controller.PendingPullIntos.Enqueue(pullIntoDescriptor);
            ReadableStreamOperations.AddReadIntoRequest(stream, readIntoRequest);
            return;
        }

        if (stream.State == ReadableStreamState.Closed)
        {
            // The memory is handed back rather than discarded: an empty view onto the very same buffer.
            var emptyView = StreamBufferOperations.ConstructView(realm, view.ElementType, buffer, view.ByteOffset, 0);
            readIntoRequest.CloseSteps(emptyView);
            return;
        }

        if (controller.Queue.TotalSize > 0)
        {
            if (FillPullIntoDescriptorFromQueue(controller, pullIntoDescriptor))
            {
                var filledView = ConvertPullIntoDescriptor(realm, pullIntoDescriptor);
                HandleQueueDrain(controller);
                readIntoRequest.ChunkSteps(filledView);
                return;
            }

            if (controller.CloseRequested)
            {
                var error = realm.Intrinsics.TypeError.Construct("Insufficient bytes to fill elements in the given buffer");
                Error(controller, error);
                readIntoRequest.ErrorSteps(error);
                return;
            }
        }

        controller.PendingPullIntos.Enqueue(pullIntoDescriptor);
        ReadableStreamOperations.AddReadIntoRequest(stream, readIntoRequest);
        CallPullIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-respond
    /// </summary>
    internal static void Respond(JsReadableByteStreamController controller, ulong bytesWritten)
    {
        var realm = controller.Realm;
        var firstDescriptor = controller.PendingPullIntos.Peek();
        var state = controller.Stream.State;

        if (state == ReadableStreamState.Closed)
        {
            if (bytesWritten != 0)
            {
                Throw.TypeError(realm, "bytesWritten must be 0 when calling respond() on a closed stream");
            }
        }
        else
        {
            if (bytesWritten == 0)
            {
                Throw.TypeError(realm, "bytesWritten must be greater than 0 when calling respond() on a readable stream");
            }

            if (bytesWritten > (ulong) (firstDescriptor.ByteLength - firstDescriptor.BytesFilled))
            {
                Throw.RangeError(realm, "bytesWritten out of range");
            }
        }

        firstDescriptor.Buffer = StreamBufferOperations.TransferArrayBuffer(realm, firstDescriptor.Buffer);

        RespondInternal(controller, (int) bytesWritten);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-respond-in-closed-state
    /// </summary>
    private static void RespondInClosedState(JsReadableByteStreamController controller, PullIntoDescriptor firstDescriptor)
    {
        if (firstDescriptor.ReaderType == PullIntoReaderType.None)
        {
            ShiftPendingPullInto(controller);
        }

        var stream = controller.Stream;
        if (!ReadableStreamOperations.HasBYOBReader(stream))
        {
            return;
        }

        var filledPullIntos = new List<PullIntoDescriptor>();
        while (filledPullIntos.Count < ReadableStreamOperations.GetNumReadIntoRequests(stream))
        {
            filledPullIntos.Add(ShiftPendingPullInto(controller));
        }

        foreach (var filledPullInto in filledPullIntos)
        {
            CommitPullIntoDescriptor(stream, filledPullInto);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-respond-in-readable-state
    /// </summary>
    private static void RespondInReadableState(JsReadableByteStreamController controller, int bytesWritten, PullIntoDescriptor pullIntoDescriptor)
    {
        pullIntoDescriptor.BytesFilled += bytesWritten;

        if (pullIntoDescriptor.ReaderType == PullIntoReaderType.None)
        {
            EnqueueDetachedPullIntoToQueue(controller, pullIntoDescriptor);

            foreach (var filledPullInto in ProcessPullIntoDescriptorsUsingQueue(controller))
            {
                CommitPullIntoDescriptor(controller.Stream, filledPullInto);
            }

            return;
        }

        // Not yet enough for the read to be answered: the descriptor stays at the head of the queue so the
        // underlying source can keep filling it.
        if (pullIntoDescriptor.BytesFilled < pullIntoDescriptor.MinimumFill)
        {
            return;
        }

        ShiftPendingPullInto(controller);

        // A trailing partial element cannot be handed over, so it is copied back into the stream's queue
        // and becomes the first bytes of the next chunk.
        var remainderSize = pullIntoDescriptor.BytesFilled % pullIntoDescriptor.ElementSize;
        if (remainderSize > 0)
        {
            var end = pullIntoDescriptor.ByteOffset + pullIntoDescriptor.BytesFilled;
            EnqueueClonedChunkToQueue(controller, pullIntoDescriptor.Buffer, end - remainderSize, remainderSize);
        }

        pullIntoDescriptor.BytesFilled -= remainderSize;
        var filledPullIntos = ProcessPullIntoDescriptorsUsingQueue(controller);

        CommitPullIntoDescriptor(controller.Stream, pullIntoDescriptor);
        foreach (var filledPullInto in filledPullIntos)
        {
            CommitPullIntoDescriptor(controller.Stream, filledPullInto);
        }
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-respond-internal
    /// </summary>
    private static void RespondInternal(JsReadableByteStreamController controller, int bytesWritten)
    {
        var firstDescriptor = controller.PendingPullIntos.Peek();

        InvalidateByobRequest(controller);

        if (controller.Stream.State == ReadableStreamState.Closed)
        {
            RespondInClosedState(controller, firstDescriptor);
        }
        else
        {
            RespondInReadableState(controller, bytesWritten, firstDescriptor);
        }

        CallPullIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-respond-with-new-view — the
    /// underlying source hands back a different view of the same memory, which is how a source that had to
    /// write elsewhere and copy still avoids a second copy.
    /// </summary>
    internal static void RespondWithNewView(JsReadableByteStreamController controller, in StreamBufferOperations.ArrayBufferViewInfo view)
    {
        var realm = controller.Realm;
        var firstDescriptor = controller.PendingPullIntos.Peek();
        var state = controller.Stream.State;

        if (state == ReadableStreamState.Closed)
        {
            if (view.ByteLength != 0)
            {
                Throw.TypeError(realm, "The view's length must be 0 when calling respondWithNewView() on a closed stream");
            }
        }
        else if (view.ByteLength == 0)
        {
            Throw.TypeError(realm, "The view's length must be greater than 0 when calling respondWithNewView() on a readable stream");
        }

        if (firstDescriptor.ByteOffset + firstDescriptor.BytesFilled != view.ByteOffset)
        {
            Throw.RangeError(realm, "The region specified by view does not match byobRequest");
        }

        if (firstDescriptor.BufferByteLength != view.Buffer.ArrayBufferByteLength)
        {
            Throw.RangeError(realm, "The buffer of view has different capacity than byobRequest");
        }

        if (firstDescriptor.BytesFilled + view.ByteLength > firstDescriptor.ByteLength)
        {
            Throw.RangeError(realm, "The region specified by view is larger than byobRequest");
        }

        var viewByteLength = view.ByteLength;
        firstDescriptor.Buffer = StreamBufferOperations.TransferArrayBuffer(realm, view.Buffer);

        RespondInternal(controller, viewByteLength);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-shift-pending-pull-into
    /// </summary>
    private static PullIntoDescriptor ShiftPendingPullInto(JsReadableByteStreamController controller)
        => controller.PendingPullIntos.Dequeue();

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-readable-byte-stream-controller
    /// </summary>
    internal static void SetUp(
        JsReadableStream stream,
        JsReadableByteStreamController controller,
        Func<JsValue> startAlgorithm,
        Func<JsPromise> pullAlgorithm,
        Func<JsValue, JsPromise> cancelAlgorithm,
        double highWaterMark,
        ulong? autoAllocateChunkSize)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        controller.Stream = stream;
        controller.PullAgain = false;
        controller.Pulling = false;
        controller.ByobRequest = null;
        controller.Queue.Reset();
        controller.CloseRequested = false;
        controller.Started = false;
        controller.StrategyHighWaterMark = highWaterMark;
        controller.PullAlgorithm = pullAlgorithm;
        controller.CancelAlgorithm = cancelAlgorithm;
        controller.AutoAllocateChunkSize = autoAllocateChunkSize;
        controller.PendingPullIntos.Clear();
        stream.Controller = controller;

        // As for a default controller: start()'s return type is `any`, so an exception it raises propagates
        // out of the ReadableStream constructor rather than becoming a rejection.
        var startResult = startAlgorithm();
        var startPromise = StreamPromises.ResolvedWith(engine, realm, startResult);

        StreamPromises.UponPromise(
            engine,
            startPromise,
            _ =>
            {
                controller.Started = true;
                CallPullIfNeeded(controller);
            },
            reason => Error(controller, reason));
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#set-up-readable-byte-stream-controller-from-underlying-source
    /// </summary>
    internal static void SetUpFromUnderlyingSource(
        JsReadableStream stream,
        JsValue underlyingSource,
        in StreamDictionaries.UnderlyingSourceRecord source,
        double highWaterMark)
    {
        var engine = stream.Engine;
        var realm = stream.Realm;

        var controller = new JsReadableByteStreamController(engine, realm)
        {
            _prototype = realm.Intrinsics.ReadableByteStreamController.PrototypeObject,
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

        var autoAllocateChunkSize = source.AutoAllocateChunkSize;
        if (autoAllocateChunkSize == 0)
        {
            Throw.TypeError(realm, "The underlying source's autoAllocateChunkSize must be greater than 0");
        }

        SetUp(stream, controller, startAlgorithm, pullAlgorithm, cancelAlgorithm, highWaterMark, autoAllocateChunkSize);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-private-pull — a default <c>read()</c> against a byte
    /// stream, which either takes a chunk out of the queue or, with automatic allocation configured, parks a
    /// controller-owned buffer for the underlying source to fill.
    /// </summary>
    internal static void PullSteps(JsReadableByteStreamController controller, ReadRequest readRequest)
    {
        var stream = controller.Stream;
        var realm = controller.Realm;

        if (controller.Queue.TotalSize > 0)
        {
            FillReadRequestFromQueue(controller, readRequest);
            return;
        }

        if (controller.AutoAllocateChunkSize is { } autoAllocateChunkSize)
        {
            JsArrayBuffer buffer;
            try
            {
                // "Let buffer be Construct(%ArrayBuffer%, « autoAllocateChunkSize »)" — a size no
                // ArrayBuffer can have fails here, as a RangeError this read reports, rather than having
                // been rejected by the constructor that never allocated anything.
                buffer = realm.Intrinsics.ArrayBuffer.AllocateArrayBuffer(realm.Intrinsics.ArrayBuffer, autoAllocateChunkSize);
            }
            catch (JavaScriptException e)
            {
                readRequest.ErrorSteps(e.Error);
                return;
            }

            // The allocation succeeded, so the size is a length an ArrayBuffer can have.
            var chunkSize = (int) autoAllocateChunkSize;

            controller.PendingPullIntos.Enqueue(new PullIntoDescriptor
            {
                Buffer = buffer,
                BufferByteLength = chunkSize,
                ByteOffset = 0,
                ByteLength = chunkSize,
                BytesFilled = 0,
                MinimumFill = 1,
                ElementSize = 1,
                ViewElementType = TypedArrayElementType.Uint8,
                ReaderType = PullIntoReaderType.Default,
            });
        }

        ReadableStreamOperations.AddReadRequest(stream, readRequest);
        CallPullIfNeeded(controller);
    }

    /// <summary>
    /// https://streams.spec.whatwg.org/#rbs-controller-private-cancel
    /// </summary>
    internal static JsPromise CancelSteps(JsReadableByteStreamController controller, JsValue reason)
    {
        ClearPendingPullIntos(controller);
        controller.Queue.Reset();

        // Reachable only while the stream is readable, so the algorithm has not been cleared yet.
        var result = controller.CancelAlgorithm!(reason);
        ClearAlgorithms(controller);
        return result;
    }

    [DoesNotReturn]
    private static void ThrowValue(Engine engine, JsValue error)
    {
        var location = engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(engine, error, in location);
    }
}
#endif

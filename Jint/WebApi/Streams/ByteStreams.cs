#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The three pieces every specification that hands bytes to a stream needs: a chunk, which is always a
/// <c>Uint8Array</c>; a way to push raw bytes into a byte stream's controller; and a stream over a byte
/// sequence that already exists in full.
/// </summary>
/// <remarks>
/// Used by <c>Blob.stream()</c> (https://w3c.github.io/FileAPI/#blob-get-stream) and by the <c>Body</c>
/// mixin's <c>body</c> attribute for a body that was extracted from bytes
/// (https://fetch.spec.whatwg.org/#concept-bodyinit-extract). Both specifications ask for a stream "set up
/// with byte reading support" — a readable <i>byte</i> stream — and both get one:
/// <see cref="CreateFromBytes"/> builds a <see cref="JsReadableByteStreamController"/>, so
/// <c>getReader({ mode: "byob" })</c> works on a blob's stream and on either kind of body, exactly as it
/// does in a browser. A default reader sees no difference at all: the chunk it takes out of the queue is
/// still one <c>Uint8Array</c>.
/// </remarks>
internal static class ByteStreams
{
    /// <summary>
    /// A fresh <c>Uint8Array</c> over a copy of <paramref name="bytes"/>. The copy is the point: what crosses
    /// into script is writable, and the source rarely is.
    /// </summary>
    internal static JsTypedArray NewUint8Array(Engine engine, Realm realm, ReadOnlySpan<byte> bytes)
    {
        var uint8Array = realm.Intrinsics.Uint8Array;
        var array = uint8Array.AllocateTypedArray(uint8Array, (uint) bytes.Length);
        TypedArrayConstructor.FillTypedArrayInstance(array, bytes);
        return array;
    }

    /// <summary>
    /// Enqueues a byte sequence the engine holds into a byte stream's controller —
    /// https://streams.spec.whatwg.org/#readable-byte-stream-controller-enqueue, whose argument is an
    /// <c>ArrayBufferView</c> rather than a chunk of any type.
    /// </summary>
    /// <remarks>
    /// The buffer is built here and handed straight over, so the transfer <c>enqueue</c> performs on it
    /// costs nothing: the data block moves to the new buffer and the one made here is detached without ever
    /// having been reachable from script. The copy of <paramref name="bytes"/> is the same one
    /// <see cref="NewUint8Array"/> makes and for the same reason — a blob, a body source or a pooled
    /// transport buffer must not be what a script ends up owning.
    /// </remarks>
    internal static void EnqueueBytes(JsReadableByteStreamController controller, ReadOnlySpan<byte> bytes)
        => EnqueueOwnedBytes(controller, bytes.ToArray());

    /// <summary>
    /// <see cref="EnqueueBytes"/> for a caller that already holds an array nothing else will read —
    /// a transport chunk copied out of a pooled buffer, say — and so needs no copy of its own.
    /// </summary>
    internal static void EnqueueOwnedBytes(JsReadableByteStreamController controller, byte[] bytes)
    {
        var realm = controller.Realm;

        var buffer = new JsArrayBuffer(controller.Engine, bytes)
        {
            _prototype = realm.Intrinsics.ArrayBuffer.PrototypeObject,
        };

        var view = new StreamBufferOperations.ArrayBufferViewInfo(
            buffer, ByteOffset: 0, bytes.Length, bytes.Length, ElementSize: 1, TypedArrayElementType.Uint8);

        ReadableByteStreamControllerOperations.Enqueue(controller, in view);
    }

    /// <summary>
    /// Ends a byte stream whose bytes arrive from outside the engine, the way
    /// https://streams.spec.whatwg.org/#example-rbs-pull ends one: close the controller, and then answer any
    /// outstanding BYOB request with zero bytes.
    /// </summary>
    /// <remarks>
    /// The second half is not optional and is easy to miss.
    /// <c>ReadableByteStreamControllerClose</c> closes the <i>stream</i>, and
    /// https://streams.spec.whatwg.org/#readable-stream-close resolves a default reader's pending
    /// <c>read()</c>s — but deliberately not a BYOB reader's pending read-<i>into</i> requests, which are
    /// still holding a buffer only the controller can hand back. A source that closes without responding
    /// leaves a <c>read(view)</c> pending for ever. Responding zero into a closed stream is exactly the
    /// "the source has no more bytes, here is your buffer" answer, and produces <c>{ done: true }</c> with
    /// an empty view onto the caller's own memory.
    /// </remarks>
    internal static void CloseAndReleasePendingByob(JsReadableByteStreamController controller)
    {
        ReadableByteStreamControllerOperations.Close(controller);

        // Close only *requests* a close while the queue still holds bytes, and a stream that is not closed
        // has nothing to hand back yet — the drain of those bytes will close it and answer the read.
        if (controller.Stream.State == ReadableStreamState.Closed && controller.PendingPullIntos.Count > 0)
        {
            ReadableByteStreamControllerOperations.Respond(controller, 0);
        }
    }

    /// <summary>
    /// A readable <i>byte</i> stream over a byte sequence that is already in memory: one chunk, then close.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole sequence is one chunk rather than the implementation-defined slices a browser hands out.
    /// That is observable to a default reader — one <c>read()</c> resolves with everything — and it is the
    /// honest shape here, because there is no I/O to interleave: the bytes exist, and slicing them would
    /// only manufacture event-loop turns. A BYOB reader is unaffected either way, since it is the
    /// <i>caller's</i> buffer that decides how much one read takes.
    /// </para>
    /// <para>
    /// An empty sequence enqueues nothing and closes immediately, so the first <c>read()</c> answers
    /// <c>{ value: undefined, done: true }</c>.
    /// </para>
    /// <para>
    /// No <c>autoAllocateChunkSize</c>: the bytes are already here, so there is nothing for the controller
    /// to allocate a buffer for. A BYOB read is served out of the queue instead.
    /// </para>
    /// </remarks>
    internal static JsReadableStream CreateFromBytes(Engine engine, Realm realm, ReadOnlyMemory<byte> bytes)
    {
        var stream = ReadableStreamOperations.CreateReadableByteStream(
            engine,
            realm,
            static () => JsValue.Undefined,
            () => StreamPromises.ResolvedWithUndefined(engine, realm),
            _ => StreamPromises.ResolvedWithUndefined(engine, realm));

        // Filled after the stream exists rather than from its start algorithm, which runs while the
        // controller is still being wired up and has no way to reach it.
        var controller = stream.ByteController;
        if (!bytes.IsEmpty)
        {
            EnqueueBytes(controller, bytes.Span);
        }

        ReadableByteStreamControllerOperations.Close(controller);
        return stream;
    }
}
#endif

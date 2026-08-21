#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.WebApi.Streams;

/// <summary>
/// The two pieces every specification that hands bytes to a stream needs: a chunk, which is always a
/// <c>Uint8Array</c>, and a stream over a byte sequence that already exists in full.
/// </summary>
/// <remarks>
/// Used by <c>Blob.stream()</c> (https://w3c.github.io/FileAPI/#blob-get-stream) and by the <c>Body</c>
/// mixin's <c>body</c> attribute for a body that was extracted from bytes
/// (https://fetch.spec.whatwg.org/#concept-bodyinit-extract). Both specifications ask for a stream "set up
/// with byte reading support" — a readable <i>byte</i> stream, which
/// <see cref="ReadableByteStreamControllerOperations"/> now implements — but neither caller has been moved
/// onto one yet, so what they get is still an ordinary stream carrying <c>Uint8Array</c> chunks. That is
/// what those algorithms reduce to once BYOB readers are out of the picture, and it is what every consumer
/// in this implementation reads; the difference a script can see is that <c>getReader({ mode: "byob" })</c>
/// on such a body refuses, where in a browser it would not.
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
    /// A <c>ReadableStream</c> over a byte sequence that is already in memory: one chunk, then close.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole sequence is one chunk rather than the implementation-defined slices a browser hands out.
    /// That is observable — a reader sees one <c>read()</c> resolve with everything — and it is the honest
    /// shape here, because there is no I/O to interleave: the bytes exist, and slicing them would only
    /// manufacture event-loop turns.
    /// </para>
    /// <para>
    /// An empty sequence enqueues nothing and closes immediately, so the first <c>read()</c> answers
    /// <c>{ value: undefined, done: true }</c>.
    /// </para>
    /// </remarks>
    internal static JsReadableStream CreateFromBytes(Engine engine, Realm realm, ReadOnlyMemory<byte> bytes)
    {
        var stream = ReadableStreamOperations.CreateReadableStream(
            engine,
            realm,
            static () => JsValue.Undefined,
            () => StreamPromises.ResolvedWithUndefined(engine, realm),
            _ => StreamPromises.ResolvedWithUndefined(engine, realm));

        // Filled after the stream exists rather than from its start algorithm, which runs while the
        // controller is still being wired up and has no way to reach it.
        var controller = stream.DefaultController;
        if (!bytes.IsEmpty)
        {
            ReadableStreamDefaultControllerOperations.Enqueue(controller, NewUint8Array(engine, realm, bytes.Span));
        }

        ReadableStreamDefaultControllerOperations.Close(controller);
        return stream;
    }
}
#endif

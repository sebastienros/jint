#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Encoding;

/// <summary>
/// A <c>TextDecoderStream</c> instance: the <c>TextDecoderCommon</c> mixin's state plus the transform
/// stream that runs it.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoderstream
/// </para>
/// </summary>
/// <remarks>
/// The decoding is the same object a <c>TextDecoder</c> uses, driven the same way a script drives one:
/// every chunk is a <c>decode(bytes, { stream: true })</c> and the flush is the closing <c>decode()</c>.
/// That is what makes a sequence split anywhere — mid sequence, mid surrogate pair, mid BOM — decode to
/// exactly the string the whole byte sequence would have.
/// </remarks>
internal sealed class JsTextDecoderStream : JsGenericTransformStream
{
    internal JsTextDecoderStream(Engine engine, Realm realm, TextDecoderCommon common) : base(engine, realm)
    {
        Common = common;
    }

    /// <summary>https://encoding.spec.whatwg.org/#textdecodercommon</summary>
    internal TextDecoderCommon Common { get; }

    /// <summary>
    /// "Decode and enqueue a chunk" — https://encoding.spec.whatwg.org/#decode-and-enqueue-a-chunk.
    /// </summary>
    /// <remarks>
    /// The chunk is an <c>AllowSharedBufferSource</c>, so anything else is the <c>TypeError</c> the WebIDL
    /// conversion raises — and a <c>TypeError</c> raised here errors both sides of the stream, which is what
    /// the interface's description promises for a decoder in <c>fatal</c> mode too.
    /// </remarks>
    internal void DecodeAndEnqueue(JsValue chunk)
    {
        if (!BufferSource.TryGetBytes(chunk, out var bytes))
        {
            Throw.TypeError(Realm, "TextDecoderStream: the chunk is not an ArrayBuffer or a view over one");
        }

        // The decoder is never flushed here: an incomplete sequence at the end of a chunk waits for the
        // next one, and only the flush algorithm below ends the stream.
        EnqueueIfNotEmpty(Common.Decode(Realm, bytes, stream: true));
    }

    /// <summary>
    /// "Flush and enqueue" — https://encoding.spec.whatwg.org/#flush-and-enqueue. This is the closing
    /// non-streaming decode, so a sequence left incomplete becomes U+FFFD, or a <c>TypeError</c> when the
    /// decoder is <c>fatal</c>.
    /// </summary>
    internal void FlushAndEnqueue() => EnqueueIfNotEmpty(Common.Decode(Realm, ReadOnlySpan<byte>.Empty, stream: false));

    /// <summary>"If outputChunk is not the empty string, then enqueue outputChunk".</summary>
    private void EnqueueIfNotEmpty(JsString outputChunk)
    {
        if (outputChunk.Length > 0)
        {
            Enqueue(outputChunk);
        }
    }
}
#endif

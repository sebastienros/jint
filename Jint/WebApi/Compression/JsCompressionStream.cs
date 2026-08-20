#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Encoding;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Compression;

/// <summary>
/// A <c>CompressionStream</c> instance.
/// <para>
/// https://compression.spec.whatwg.org/#compression-stream
/// </para>
/// </summary>
/// <remarks>
/// The writable side accepts any <c>BufferSource</c> and the readable side produces <c>Uint8Array</c>s, as
/// the standard's introduction specifies. Compression never fails, so the only error a script can see here
/// is the <c>TypeError</c> for a chunk that is not a buffer source.
/// </remarks>
internal sealed class JsCompressionStream : JsGenericTransformStream, IDisposable
{
    private readonly CompressionCodec _codec;

    internal JsCompressionStream(Engine engine, Realm realm, CompressionFormat format) : base(engine, realm)
    {
        Format = format;
        _codec = new CompressionCodec(format);
    }

    /// <summary>https://compression.spec.whatwg.org/#compressionstream-format</summary>
    internal CompressionFormat Format { get; }

    /// <summary>
    /// "Compress and enqueue a chunk" — https://compression.spec.whatwg.org/#compress-and-enqueue-a-chunk.
    /// </summary>
    internal void CompressAndEnqueue(JsValue chunk)
    {
        if (!BufferSource.TryGetBytes(chunk, out var bytes))
        {
            Throw.TypeError(Realm, "CompressionStream: the chunk is not an ArrayBuffer or a view over one");
        }

        EnqueueBytes(_codec.Compress(bytes));
    }

    /// <summary>
    /// "Compress flush and enqueue" — https://compression.spec.whatwg.org/#compress-flush-and-enqueue.
    /// </summary>
    internal void CompressFlushAndEnqueue() => EnqueueBytes(_codec.Finish());

    /// <summary>
    /// Releases the compression context of a stream that will never be finished. This is the transform's
    /// cancel algorithm — it runs when either side is shut down early, and it is invisible to script. The
    /// object stays perfectly usable afterwards; there is simply nothing left to compress into.
    /// </summary>
    public void Dispose() => _codec.Dispose();

    /// <summary>
    /// "Splitting buffer into one or more non-empty pieces and converting them into Uint8Arrays": one piece
    /// is a valid split, and the standard's "if buffer is empty, return" is the null case.
    /// </summary>
    private void EnqueueBytes(byte[]? output)
    {
        if (output is not null)
        {
            Enqueue(Realm.Intrinsics.Uint8Array.Construct(output));
        }
    }
}
#endif

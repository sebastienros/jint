#if NET8_0_OR_GREATER
using System.IO;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Encoding;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Compression;

/// <summary>
/// A <c>DecompressionStream</c> instance.
/// <para>
/// https://compression.spec.whatwg.org/#decompression-stream
/// </para>
/// </summary>
/// <remarks>
/// The writable side accepts any <c>BufferSource</c> and the readable side produces <c>Uint8Array</c>s.
/// Input the format rejects errors <b>both</b> sides with a <c>TypeError</c>, which is what the standard's
/// "throw a TypeError" inside the transform algorithm means for a transform stream; see
/// <see cref="DecompressionCodec"/> for the two truncation cases the BCL does not let us detect.
/// </remarks>
internal sealed class JsDecompressionStream : JsGenericTransformStream, IDisposable
{
    private readonly DecompressionCodec _codec;

    internal JsDecompressionStream(Engine engine, Realm realm, CompressionFormat format) : base(engine, realm)
    {
        Format = format;
        _codec = new DecompressionCodec(format);
    }

    /// <summary>https://compression.spec.whatwg.org/#decompressionstream-format</summary>
    internal CompressionFormat Format { get; }

    /// <summary>
    /// "Decompress and enqueue a chunk" — https://compression.spec.whatwg.org/#decompress-and-enqueue-a-chunk.
    /// </summary>
    internal void DecompressAndEnqueue(JsValue chunk)
    {
        if (!BufferSource.TryGetBytes(chunk, out var bytes))
        {
            Throw.TypeError(Realm, "DecompressionStream: the chunk is not an ArrayBuffer or a view over one");
        }

        try
        {
            EnqueueBytes(_codec.Decompress(bytes));
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            // "If this results in an error, then throw a TypeError." The BCL's own message names an archive
            // entry and a compression method, which would be a puzzle in a script's console.
            Throw.TypeError(Realm, "DecompressionStream: the input is not valid " + Name(Format) + " data");
        }
    }

    /// <summary>
    /// "Decompress flush and enqueue" — https://compression.spec.whatwg.org/#decompress-flush-and-enqueue.
    /// </summary>
    internal void DecompressFlushAndEnqueue()
    {
        try
        {
            EnqueueBytes(_codec.Finish());
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            Throw.TypeError(Realm, "DecompressionStream: the " + Name(Format) + " input ended before the compressed data was complete");
        }
    }

    /// <summary>
    /// Releases the decompression context of a stream that will never be finished. This is the transform's
    /// cancel algorithm — it runs when either side is shut down early, and it is invisible to script. The
    /// object stays perfectly usable afterwards; there is simply nothing left to decompress into.
    /// </summary>
    public void Dispose() => _codec.Dispose();

    private void EnqueueBytes(byte[]? output)
    {
        if (output is not null)
        {
            Enqueue(Realm.Intrinsics.Uint8Array.Construct(output));
        }
    }

    /// <summary>The format's own name, so a failure says which one the input did not match.</summary>
    private static string Name(CompressionFormat format) => format switch
    {
        CompressionFormat.Gzip => "gzip",
        CompressionFormat.Deflate => "deflate",
        _ => "deflate-raw",
    };
}
#endif

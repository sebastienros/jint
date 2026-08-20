#if NET8_0_OR_GREATER
using System.IO;
using System.IO.Compression;

namespace Jint.WebApi.Compression;

/// <summary>
/// The <c>CompressionStream</c>'s "compression context" — https://compression.spec.whatwg.org/#compression-context.
/// </summary>
/// <remarks>
/// <para>
/// The context is the BCL compressing stream for the format plus the buffer it writes into. Input is
/// written straight through and whatever the compressor has produced so far is taken out; the compressor
/// is deliberately <b>not</b> flushed per chunk, because a flush emits a sync-flush marker and would bloat
/// the output for no benefit. The specification anticipates exactly this — "if buffer is empty, return" —
/// so a chunk that produces nothing yet enqueues nothing, and the bulk of a small stream's output appears
/// when <see cref="Finish"/> ends the member.
/// </para>
/// <para>
/// Nothing here touches the engine or a thread: the compressor is a synchronous in-memory transform driven
/// from the transform stream's algorithms, which run on the engine's thread like every other stream
/// callback.
/// </para>
/// </remarks>
internal sealed class CompressionCodec : IDisposable
{
    /// <summary>
    /// The whole of an RFC 1951 stream whose payload is empty: BFINAL=1, BTYPE=01 (fixed Huffman) and the
    /// end-of-block symbol, which is ten bits and therefore these two bytes.
    /// </summary>
    private static ReadOnlySpan<byte> EmptyDeflateBlock => [0x03, 0x00];

    /// <summary>
    /// An RFC 1950 stream whose payload is empty: CMF (CM=8, CINFO=7) and FLG, whose FCHECK bits make
    /// 0x789C a multiple of 31; the empty block; and the ADLER32 of the empty byte sequence, which is 1.
    /// </summary>
    private static ReadOnlySpan<byte> EmptyZlibStream => [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01];

    /// <summary>
    /// An RFC 1952 member whose payload is empty: the magic number, CM=8, no flags, no MTIME, no extra
    /// flags, OS = 255 (unknown, rather than this machine's); the empty block; and a CRC32 and ISIZE of
    /// zero. Every field a <c>DecompressionStream</c> must ignore is at its "nothing to say" value.
    /// </summary>
    private static ReadOnlySpan<byte> EmptyGzipMember =>
    [
        0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
        0x03, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    ];

    private readonly MemoryStream _output = new();
    private readonly Stream _compressor;
    private readonly CompressionFormat _format;
    private bool _wroteBytes;
    private bool _disposed;

    internal CompressionCodec(CompressionFormat format)
    {
        _format = format;

        // See CompressionFormat: "deflate" is the ZLIB wrapper of RFC 1950, and only "deflate-raw" is the
        // bare RFC 1951 bit stream.
        _compressor = format switch
        {
            CompressionFormat.Gzip => new GZipStream(_output, CompressionMode.Compress, leaveOpen: true),
            CompressionFormat.Deflate => new ZLibStream(_output, CompressionMode.Compress, leaveOpen: true),
            _ => new DeflateStream(_output, CompressionMode.Compress, leaveOpen: true),
        };
    }

    /// <summary>
    /// "Compressing <paramref name="input"/> with the format and context", and taking whatever output that
    /// has made available. Returns <see langword="null"/> for the specification's empty buffer.
    /// </summary>
    internal byte[]? Compress(ReadOnlySpan<byte> input)
    {
        if (_disposed || input.Length == 0)
        {
            // A chunk of no bytes cannot change the compressor's state or produce output, and passing it
            // on would arm the BCL flag that <see cref="Finish"/> reasons about below.
            return null;
        }

        _wroteBytes = true;
        _compressor.Write(input);
        return TakeOutput();
    }

    /// <summary>
    /// "Compressing an empty input with the format and context, with the finish flag": the compressor is
    /// closed, which is what writes the format's trailer — a gzip CRC32 and ISIZE, a zlib ADLER32, the
    /// final DEFLATE block — and then everything left is taken out.
    /// </summary>
    /// <remarks>
    /// The empty case is the constant above rather than the compressor's own output, because the BCL will
    /// not produce one: a compressing <see cref="DeflateStream"/> that was never handed a byte writes
    /// <i>nothing at all</i> when it is closed — deliberately, because <c>ZipArchiveEntry</c> depends on
    /// zero output for zero input. On .NET 8 a zero-length write was enough to arm it and on .NET 10 it is
    /// not, so relying on that would make the same source produce different bytes per target framework.
    /// Zero bytes is not a valid stream in any of the three formats — our own <c>DecompressionStream</c>
    /// rejects it, and so does every other implementation — so the empty member is emitted explicitly.
    /// </remarks>
    internal byte[]? Finish()
    {
        if (_disposed)
        {
            return null;
        }

        if (!_wroteBytes)
        {
            Dispose();
            return _format switch
            {
                CompressionFormat.Gzip => EmptyGzipMember.ToArray(),
                CompressionFormat.Deflate => EmptyZlibStream.ToArray(),
                _ => EmptyDeflateBlock.ToArray(),
            };
        }

        _compressor.Dispose();
        var output = TakeOutput();
        _disposed = true;
        return output;
    }

    private byte[]? TakeOutput()
    {
        if (_output.Length == 0)
        {
            return null;
        }

        var bytes = _output.ToArray();

        // SetLength(0) also rewinds the position, so the buffer is reused rather than reallocated.
        _output.SetLength(0);
        return bytes;
    }

    /// <summary>
    /// Releases the native compression context of a stream that will never be finished — one whose
    /// readable side was cancelled or whose writable side was aborted. Ordinary completion goes through
    /// <see cref="Finish"/>, which disposes it too.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _compressor.Dispose();
            _disposed = true;
        }

        _output.Dispose();
    }
}
#endif

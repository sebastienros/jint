#if NET8_0_OR_GREATER
using System.IO;
using System.IO.Compression;

namespace Jint.WebApi.Compression;

/// <summary>
/// The <c>DecompressionStream</c>'s "compression context" — https://compression.spec.whatwg.org/#compression-context.
/// </summary>
/// <remarks>
/// <para>
/// The BCL's decompressing streams <i>pull</i> from a source stream, while a transform stream <i>pushes</i>
/// chunks at them, so the context owns a small push source: a chunk is copied into its queue and the
/// decompressor is then read until it asks for input the queue does not have. A source read that finds the
/// queue empty returns 0, which the decompressor treats as "no more for now" and recovers from when the
/// next chunk arrives — that is what makes a member split across arbitrarily many chunks decode
/// identically to the whole byte sequence.
/// </para>
/// <para>
/// The copy is deliberate: the queue may still hold the bytes when the algorithm returns to script, and
/// the specification asks for a synchronous copy of a buffer source precisely so a later mutation of the
/// caller's <c>ArrayBuffer</c> cannot be observed.
/// </para>
/// <para>
/// <b>Two documented divergences, both in the lenient direction, and they apply to every format.</b> The
/// standard makes it an error for a stream to end before its member is complete, and an error for anything
/// to follow the member (a second gzip "member" included). Detecting either needs to know how many of the
/// bytes handed over the decompressor actually consumed, and .NET exposes no incremental inflater or
/// decoder — only these pull streams, which buffer input internally and report neither figure. So a
/// truncated stream ends its readable side cleanly rather than erroring it, trailing bytes after a member
/// are ignored, and a multi-member gzip stream decodes every member. <see cref="BrotliStream"/> behaves
/// exactly the same way on both counts, so brotli shares the divergence rather than escaping it: the last
/// byte of a brotli stream may be dropped, or a junk byte appended, and the decoder still hands over the
/// payload it decoded and then reports "no more input for now". Everything the decompressor itself rejects
/// — a bad header, a failed CRC32/ADLER32, a malformed DEFLATE block, a brotli stream the decoder cannot
/// parse — is still an error, which is the case that matters for telling corrupt input from good. The one
/// truncation that <i>is</i> caught here is the empty stream: no compressed bytes at all cannot be a
/// complete member in any of the four formats.
/// </para>
/// </remarks>
internal sealed class DecompressionCodec : IDisposable
{
    private readonly PushSource _source = new();
    private readonly Stream _decompressor;
    private readonly byte[] _buffer = new byte[8192];
    private readonly bool _isBrotli;
    private long _inputBytes;
    private bool _disposed;

    internal DecompressionCodec(CompressionFormat format)
    {
        _isBrotli = format == CompressionFormat.Brotli;

        // See CompressionFormat: "deflate" is the ZLIB wrapper of RFC 1950, and only "deflate-raw" is the
        // bare RFC 1951 bit stream.
        _decompressor = format switch
        {
            CompressionFormat.Gzip => new GZipStream(_source, CompressionMode.Decompress, leaveOpen: true),
            CompressionFormat.Deflate => new ZLibStream(_source, CompressionMode.Decompress, leaveOpen: true),
            CompressionFormat.Brotli => new BrotliStream(_source, CompressionMode.Decompress, leaveOpen: true),
            _ => new DeflateStream(_source, CompressionMode.Decompress, leaveOpen: true),
        };
    }

    /// <summary>
    /// "Decompressing <paramref name="input"/> with the format and context". Returns
    /// <see langword="null"/> for the specification's empty buffer, and raises the BCL's
    /// <see cref="InvalidDataException"/> or <see cref="IOException"/> for input the format rejects — which
    /// the caller reports as the <c>TypeError</c> the standard asks for.
    /// </summary>
    internal byte[]? Decompress(ReadOnlySpan<byte> input)
    {
        if (_disposed)
        {
            return null;
        }

        _inputBytes += input.Length;
        _source.Push(input.ToArray());
        return Drain();
    }

    /// <summary>
    /// "Decompressing an empty input with the format and context, with the finish flag", followed by the
    /// standard's "if the end of the compressed input has not been reached, then throw a TypeError" — of
    /// which only the empty-input case is detectable here, as the class remarks explain.
    /// </summary>
    internal byte[]? Finish()
    {
        if (_disposed)
        {
            return null;
        }

        if (_inputBytes == 0)
        {
            throw new InvalidDataException("The compressed input ended before any member began.");
        }

        var output = Drain();
        Dispose();
        return output;
    }

    /// <summary>
    /// Reads the decompressor until it has produced everything the input so far allows. A read of 0 means
    /// it asked the push source for input that has not arrived, not that the stream ended.
    /// </summary>
    /// <remarks>
    /// The <see cref="InvalidOperationException"/> clause is <see cref="BrotliStream"/> alone, and it is a
    /// naming inconsistency in the BCL rather than a different condition: the deflate family reports data it
    /// cannot parse as <see cref="InvalidDataException"/>, while <c>BrotliStream</c> raises
    /// <c>InvalidOperationException("Decoder ran into invalid data.")</c> for the decoder's
    /// <c>OperationStatus.InvalidData</c>. Translating it here keeps this class's one promise to its caller —
    /// input the format rejects arrives as <see cref="InvalidDataException"/> or <see cref="IOException"/> —
    /// true for all four formats, so the two call sites in <c>JsDecompressionStream</c> stay a single
    /// <c>catch</c>. It is scoped to brotli deliberately: an <see cref="InvalidOperationException"/> out of
    /// the deflate family would be a defect on our side and must not be reported to script as bad input —
    /// and <see cref="ObjectDisposedException"/> is excluded for the same reason, being a derived type of it
    /// that <c>BrotliStream</c> raises for a use-after-dispose rather than for anything about the bytes.
    /// Both callers return early when this codec is disposed, so it cannot be reached today; leaving it in
    /// the clause would mean a future one arriving as the standard's "not valid brotli data" instead.
    /// </remarks>
    private byte[]? Drain()
    {
        MemoryStream? output = null;

        try
        {
            int read;
            while ((read = _decompressor.Read(_buffer, 0, _buffer.Length)) > 0)
            {
                (output ??= new MemoryStream()).Write(_buffer, 0, read);
            }
        }
        catch (InvalidOperationException e) when (_isBrotli && e is not ObjectDisposedException)
        {
            throw new InvalidDataException(e.Message, e);
        }

        return output?.ToArray();
    }

    /// <summary>
    /// Releases the native decompression context. Ordinary completion goes through <see cref="Finish"/>,
    /// which calls this; a cancelled or aborted stream calls it directly rather than waiting for a
    /// finalizer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _decompressor.Dispose();
        _source.Dispose();
    }

    /// <summary>
    /// The stream the decompressor pulls from: a queue of byte sequences that reports "nothing available"
    /// rather than a permanent end of stream, since more chunks may follow.
    /// </summary>
    private sealed class PushSource : Stream
    {
        private readonly Queue<byte[]> _queue = new();
        private byte[]? _current;
        private int _offset;

        internal void Push(byte[] bytes)
        {
            if (bytes.Length > 0)
            {
                _queue.Enqueue(bytes);
            }
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_current is null || _offset == _current.Length)
            {
                _current = _queue.Count > 0 ? _queue.Dequeue() : null;
                _offset = 0;
            }

            if (_current is null || buffer.Length == 0)
            {
                return 0;
            }

            var count = Math.Min(buffer.Length, _current.Length - _offset);
            _current.AsSpan(_offset, count).CopyTo(buffer);
            _offset += count;
            return count;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
#endif

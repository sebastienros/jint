#if NET11_0_OR_GREATER
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace Jint.WebApi.Compression;

/// <summary>
/// The streamless decompression context for https://compression.spec.whatwg.org/#compression-context.
/// </summary>
/// <remarks>
/// Completion and input consumption are tracked separately: the standard rejects both an incomplete
/// member at close and any bytes after its end, including a second gzip member.
/// </remarks>
internal sealed class DecompressionCodec : IDisposable
{
    private readonly CompressionFormat _format;
    private readonly GZipDecoder? _gzip;
    private readonly ZLibDecoder? _zlib;
    private readonly DeflateDecoder? _deflate;
    private BrotliDecoder _brotli;
    private readonly byte[] _buffer = new byte[8192];
    private bool _finished;
    private bool _disposed;

    internal DecompressionCodec(CompressionFormat format)
    {
        _format = format;
        switch (format)
        {
            case CompressionFormat.Gzip:
                _gzip = new GZipDecoder();
                break;
            case CompressionFormat.Deflate:
                _zlib = new ZLibDecoder();
                break;
            case CompressionFormat.DeflateRaw:
                _deflate = new DeflateDecoder();
                break;
            case CompressionFormat.Brotli:
                _brotli = new BrotliDecoder();
                break;
            default:
                throw new InvalidOperationException("Unhandled compression format: " + format);
        }
    }

    /// <summary>https://compression.spec.whatwg.org/#decompress-and-enqueue-a-chunk</summary>
    internal byte[]? Decompress(ReadOnlySpan<byte> input)
    {
        if (_disposed)
        {
            return null;
        }

        if (_finished)
        {
            if (!input.IsEmpty)
            {
                throw new InvalidDataException("The compressed input contains data after the end of the member.");
            }

            return null;
        }

        using var output = new MemoryStream();
        OperationStatus status;
        do
        {
            int consumed;
            int written;
            status = _format switch
            {
                CompressionFormat.Gzip => _gzip!.Decompress(input, _buffer, out consumed, out written),
                CompressionFormat.Deflate => _zlib!.Decompress(input, _buffer, out consumed, out written),
                CompressionFormat.DeflateRaw => _deflate!.Decompress(input, _buffer, out consumed, out written),
                CompressionFormat.Brotli => _brotli.Decompress(input, _buffer, out consumed, out written),
                _ => throw new InvalidOperationException("Unhandled compression format: " + _format),
            };
            input = input.Slice(consumed);
            output.Write(_buffer, 0, written);
            if (status == OperationStatus.InvalidData)
            {
                throw new InvalidDataException("The compressed input is invalid.");
            }
        }
        while (status == OperationStatus.DestinationTooSmall);

        if (!input.IsEmpty)
        {
            throw new InvalidDataException("The compressed input contains data after the end of the member.");
        }

        _finished = status == OperationStatus.Done;
        return output.Length == 0 ? null : output.ToArray();
    }

    /// <summary>https://compression.spec.whatwg.org/#decompress-flush-and-enqueue</summary>
    internal byte[]? Finish()
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            var output = Decompress([]);
            if (!_finished)
            {
                throw new InvalidDataException("The compressed input ended before the member was complete.");
            }

            return output;
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gzip?.Dispose();
        _zlib?.Dispose();
        _deflate?.Dispose();
        if (_format == CompressionFormat.Brotli)
        {
            _brotli.Dispose();
        }
    }
}
#endif

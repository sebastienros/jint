#if NET11_0_OR_GREATER
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace Jint.WebApi.Compression;

/// <summary>
/// The streamless compression context for https://compression.spec.whatwg.org/#compression-context.
/// </summary>
internal sealed class CompressionCodec : IDisposable
{
    private readonly CompressionFormat _format;
    private readonly GZipEncoder? _gzip;
    private readonly ZLibEncoder? _zlib;
    private readonly DeflateEncoder? _deflate;
    private BrotliEncoder _brotli;
    private readonly byte[] _buffer = new byte[8192];
    private bool _disposed;

    internal CompressionCodec(CompressionFormat format)
    {
        _format = format;
        switch (format)
        {
            case CompressionFormat.Gzip:
                _gzip = new GZipEncoder();
                break;
            case CompressionFormat.Deflate:
                _zlib = new ZLibEncoder();
                break;
            case CompressionFormat.DeflateRaw:
                _deflate = new DeflateEncoder();
                break;
            case CompressionFormat.Brotli:
                _brotli = new BrotliEncoder(quality: 4, window: 22);
                break;
            default:
                throw new InvalidOperationException("Unhandled compression format: " + format);
        }
    }

    internal byte[]? Compress(ReadOnlySpan<byte> input)
        => _disposed || input.IsEmpty ? null : Compress(input, isFinalBlock: false);

    internal byte[]? Finish()
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            return Compress([], isFinalBlock: true);
        }
        finally
        {
            Dispose();
        }
    }

    private byte[]? Compress(ReadOnlySpan<byte> input, bool isFinalBlock)
    {
        using var output = new MemoryStream();
        OperationStatus status;
        do
        {
            int consumed;
            int written;
            status = _format switch
            {
                CompressionFormat.Gzip => _gzip!.Compress(input, _buffer, out consumed, out written, isFinalBlock),
                CompressionFormat.Deflate => _zlib!.Compress(input, _buffer, out consumed, out written, isFinalBlock),
                CompressionFormat.DeflateRaw => _deflate!.Compress(input, _buffer, out consumed, out written, isFinalBlock),
                CompressionFormat.Brotli => _brotli.Compress(input, _buffer, out consumed, out written, isFinalBlock),
                _ => throw new InvalidOperationException("Unhandled compression format: " + _format),
            };
            input = input.Slice(consumed);
            output.Write(_buffer, 0, written);
            if (status == OperationStatus.InvalidData)
            {
                throw new InvalidDataException("The compression encoder rejected the input.");
            }
        }
        while (status == OperationStatus.DestinationTooSmall);

        if (!input.IsEmpty || isFinalBlock && status != OperationStatus.Done)
        {
            throw new InvalidDataException("The compression encoder did not finish the input.");
        }

        return output.Length == 0 ? null : output.ToArray();
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

using System.Buffers.Binary;
using System.IO.Compression;

namespace Jint.Browser.Dom.Canvas;

/// <summary>Encodes a transparent RGBA8 PNG without allocating a pixel buffer.</summary>
/// <remarks>https://html.spec.whatwg.org/multipage/canvas.html#serialising-bitmaps-to-a-file</remarks>
internal static class CanvasPng
{
    // This is a serialization limit, not an allocation proportional to the canvas size.
    internal const long MaxPixels = 16_777_216;

    internal static byte[]? Encode(CanvasRealm owner, double width, double height)
    {
        if (width <= 0 || height <= 0 || width > MaxPixels || height > MaxPixels || width * height > MaxPixels) return null;
        owner.Engine.Constraints.Check();
        using var png = new MemoryStream();
        png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint) width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint) height);
        header[8] = 8;
        header[9] = 6;
        Chunk(png, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            // Each row's filter byte and RGBA bytes are all zero; chunk boundaries need not be row boundaries.
            var remaining = ((long) width * 4 + 1) * (long) height;
            ReadOnlySpan<byte> zeros = new byte[16 * 1024];
            while (remaining > 0)
            {
                owner.Engine.Constraints.Check();
                owner.Dom.CancellationToken.ThrowIfCancellationRequested();
                var count = (int) Math.Min(remaining, zeros.Length);
                zlib.Write(zeros[..count]);
                remaining -= count;
            }
        }
        Chunk(png, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int) compressed.Length));
        Chunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void Chunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);
        output.Write(type);
        output.Write(data);
        var crc = UpdateCrc(UpdateCrc(uint.MaxValue, type), data) ^ uint.MaxValue;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
        output.Write(word);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xEDB88320u & (uint) -(int) (crc & 1));
        }
        return crc;
    }
}

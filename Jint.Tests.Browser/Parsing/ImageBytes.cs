using System.Text;

namespace Jint.Tests.Browser.Parsing;

/// <summary>
/// The smallest byte sequence of each container format that states a size, built here rather than vendored.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here is a real image and none of it needs to be.</b> <c>Jint.Browser/Media/ImageHeader</c>
/// reads a container header and never a pixel, so a file that is exactly its header is exactly what
/// exercises it — and a checked-in binary would be a test whose input nobody can read in a diff. Each method
/// is annotated with the byte offsets its format's specification gives, so a wrong answer is traceable to a
/// field rather than to a blob.
/// </para>
/// <para>
/// The one deliberate omission is a valid CRC or checksum anywhere: a header reader must not need one, and
/// requiring it would be a decoder.
/// </para>
/// </remarks>
internal static class ImageBytes
{
    /// <summary>The PNG signature and an <c>IHDR</c> chunk stating <paramref name="width"/> by <paramref name="height"/>.</summary>
    internal static byte[] Png(int width, int height)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        bytes.AddRange([0, 0, 0, 13]);
        bytes.AddRange("IHDR"u8.ToArray());
        bytes.AddRange(BigEndian(width));
        bytes.AddRange(BigEndian(height));
        bytes.AddRange([8, 6, 0, 0, 0]);
        bytes.AddRange([0, 0, 0, 0]);
        return [.. bytes];
    }

    /// <summary>
    /// A JPEG whose first marker is an <c>APP0</c> segment — so the walk has to skip one — followed by a
    /// baseline <c>SOF0</c> frame header.
    /// </summary>
    internal static byte[] Jpeg(int width, int height)
    {
        var bytes = new List<byte> { 0xFF, 0xD8 };

        // APP0: a JFIF header of nine payload bytes plus the two length bytes.
        bytes.AddRange([0xFF, 0xE0, 0x00, 0x0B]);
        bytes.AddRange("JFIF"u8.ToArray());
        bytes.AddRange([0, 1, 1, 0, 0]);

        // SOF0: length, sample precision, number of lines, samples per line, component count.
        bytes.AddRange([0xFF, 0xC0, 0x00, 0x0B, 8]);
        bytes.AddRange([(byte) (height >> 8), (byte) height]);
        bytes.AddRange([(byte) (width >> 8), (byte) width]);
        bytes.AddRange([1, 1, 0x11, 0]);
        return [.. bytes];
    }

    /// <summary>A GIF89a signature and the logical screen descriptor that follows it.</summary>
    internal static byte[] Gif(int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange("GIF89a"u8.ToArray());
        bytes.AddRange([(byte) width, (byte) (width >> 8), (byte) height, (byte) (height >> 8)]);
        bytes.AddRange([0, 0, 0]);
        return [.. bytes];
    }

    /// <summary>A RIFF/WEBP container holding a simple lossy <c>VP8 </c> frame.</summary>
    internal static byte[] WebPLossy(int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange("WEBPVP8 "u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);

        // The three-byte frame tag, then the sync code, then the two fourteen-bit dimensions.
        bytes.AddRange([0x30, 0x01, 0x00]);
        bytes.AddRange([0x9D, 0x01, 0x2A]);
        bytes.AddRange([(byte) width, (byte) (width >> 8), (byte) height, (byte) (height >> 8)]);
        return [.. bytes];
    }

    /// <summary>A RIFF/WEBP container holding a lossless <c>VP8L</c> frame, whose dimensions are one less than stated.</summary>
    internal static byte[] WebPLossless(int width, int height)
    {
        var packed = (uint) (width - 1) | ((uint) (height - 1) << 14);
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange("WEBPVP8L"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.Add(0x2F);
        bytes.AddRange([(byte) packed, (byte) (packed >> 8), (byte) (packed >> 16), (byte) (packed >> 24)]);
        return [.. bytes];
    }

    /// <summary>A RIFF/WEBP extended header, whose canvas dimensions are also stored one less than stated.</summary>
    internal static byte[] WebPExtended(int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange("WEBPVP8X"u8.ToArray());
        bytes.AddRange([10, 0, 0, 0]);
        bytes.AddRange([0, 0, 0, 0]);
        bytes.AddRange(LittleEndian24(width - 1));
        bytes.AddRange(LittleEndian24(height - 1));
        return [.. bytes];
    }

    /// <summary>A bitmap file header and a 40-byte <c>BITMAPINFOHEADER</c>; the height is negative, as a top-down bitmap's is.</summary>
    internal static byte[] Bmp(int width, int height)
    {
        var bytes = new List<byte>();
        bytes.AddRange("BM"u8.ToArray());
        bytes.AddRange([0, 0, 0, 0, 0, 0, 0, 0, 54, 0, 0, 0]);
        bytes.AddRange([40, 0, 0, 0]);
        bytes.AddRange(LittleEndian(width));
        bytes.AddRange(LittleEndian(-height));
        bytes.AddRange([1, 0, 24, 0]);
        return [.. bytes];
    }

    /// <summary>An icon directory of two entries, the second of them the larger.</summary>
    internal static byte[] Icon(int width, int height)
    {
        var bytes = new List<byte> { 0, 0, 1, 0, 2, 0 };
        bytes.AddRange([16, 16, 0, 0, 1, 0, 32, 0, 0, 0, 0, 0, 22, 0, 0, 0]);
        bytes.AddRange([(byte) width, (byte) height, 0, 0, 1, 0, 32, 0, 0, 0, 0, 0, 22, 0, 0, 0]);
        return [.. bytes];
    }

    /// <summary>An SVG document, optionally preceded by an XML declaration and a comment.</summary>
    internal static byte[] Svg(string rootAttributes, bool withProlog = false)
    {
        var prolog = withProlog ? "<?xml version=\"1.0\"?>\n<!-- a comment -->\n" : "";
        return Encoding.UTF8.GetBytes(prolog + "<svg xmlns=\"http://www.w3.org/2000/svg\" " + rootAttributes + "><rect/></svg>");
    }

    private static byte[] BigEndian(int value)
        => [(byte) (value >> 24), (byte) (value >> 16), (byte) (value >> 8), (byte) value];

    private static byte[] LittleEndian(int value)
        => [(byte) value, (byte) (value >> 8), (byte) (value >> 16), (byte) (value >> 24)];

    private static byte[] LittleEndian24(int value)
        => [(byte) value, (byte) (value >> 8), (byte) (value >> 16)];
}

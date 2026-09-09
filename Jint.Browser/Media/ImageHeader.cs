using System.Buffers.Binary;
using System.Text;

namespace Jint.Browser.Media;

/// <summary>
/// The intrinsic dimensions of an image, read out of its container header and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a decoder and must never become one.</b>
/// <a href="https://html.spec.whatwg.org/multipage/images.html#img-available">HTML §4.8.4.3</a> needs two
/// numbers from an image — its intrinsic width and height — and every container this browser supports states
/// both in a fixed-position header of at most thirty bytes. Decoding the pixels would buy nothing a page can
/// read: there is no rendering, no canvas read-back and no colour, so a pixel is a byte nobody could ever
/// ask about. What that costs is stated on <see cref="Jint.Browser.Media.PageImages"/> rather than hidden:
/// an animated GIF is its logical screen and has no frames, and an image whose header disagrees with its
/// pixel data is believed.
/// </para>
/// <para>
/// <b>The format is sniffed, never taken from <c>Content-Type</c>.</b> That is
/// <a href="https://mimesniff.spec.whatwg.org/#matching-an-image-type-pattern">MIME Sniffing §6.2</a>'s own
/// rule for an image, and it is what makes a PNG served as <c>application/octet-stream</c> — which is what a
/// blob store does by default — load rather than break.
/// </para>
/// <para>
/// <b>An unrecognised container is a refusal, not a zero.</b> HTML's update-the-image-data step 25 puts an
/// image whose data is "not in a supported file format" into the <i>broken</i> state and fires <c>error</c>,
/// so answering 0 × 0 and <c>complete</c> would be a lie a lazy-loading library acts on. AVIF, HEIC and TIFF
/// are the formats a real page meets that end up here: each keeps its dimensions in a box or directory walk
/// rather than a header, and none of them is worth one until something needs it.
/// </para>
/// </remarks>
internal static class ImageHeader
{
    /// <summary>The most bytes of an SVG document scanned for its root element.</summary>
    /// <remarks>
    /// A root start tag lives at the top of the document by definition, after at most an XML declaration, a
    /// doctype and comments. Scanning the whole of a megabyte of path data to find it would be work with no
    /// possible answer in it.
    /// </remarks>
    private const int SvgPrefixBytes = 4096;

    /// <summary>
    /// Whether <paramref name="mimeType"/> names a container <see cref="TryRead"/> can read a size out of.
    /// </summary>
    /// <remarks>
    /// <b>This is the declaration-side question, and only a declaration ever asks it.</b>
    /// <a href="https://html.spec.whatwg.org/multipage/images.html#update-the-source-set">HTML §4.8.4.3.6</a>
    /// step 3.7 drops a <c>&lt;source&gt;</c> whose <c>type</c> is not a supported image MIME type, which is
    /// how a page offers AVIF first and PNG as the fallback. The <i>bytes</i> are never asked: those are
    /// sniffed, because a server's <c>Content-Type</c> is not evidence about them.
    /// </remarks>
    internal static bool SupportsType(string? mimeType)
    {
        if (mimeType is null)
        {
            return false;
        }

        var semicolon = mimeType.IndexOf(';');
        var essence = (semicolon < 0 ? mimeType : mimeType.Substring(0, semicolon)).Trim();

        // A type attribute holds one MIME type, so anything the essence cannot contain -- a comma, a space,
        // a control character -- makes it not a MIME type at all rather than an unknown one.
        foreach (var character in essence)
        {
            if (character is ',' or ' ' or '\t' or '\n' or '\r' or '\f')
            {
                return false;
            }
        }

        return essence.ToLowerInvariant() switch
        {
            "image/png" or "image/apng" => true,
            "image/jpeg" or "image/jpg" => true,
            "image/gif" => true,
            "image/webp" => true,
            "image/bmp" or "image/x-bmp" or "image/x-ms-bmp" => true,
            "image/svg+xml" => true,
            "image/vnd.microsoft.icon" or "image/x-icon" or "image/ico" => true,
            _ => false,
        };
    }

    /// <summary>
    /// Reads the intrinsic size <paramref name="bytes"/> states, or answers <see langword="false"/> for a
    /// container this browser does not recognise.
    /// </summary>
    /// <remarks>
    /// A recognised container whose header states no size — an SVG with no <c>width</c>/<c>height</c>, which
    /// is the ordinary shape of an icon sprite — answers <see langword="true"/> with both dimensions zero.
    /// That is the difference between <i>available with no intrinsic size</i> and <i>broken</i>, and it is a
    /// difference a page can see: the first fires <c>load</c>.
    /// </remarks>
    internal static bool TryRead(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;

        return TryReadPng(bytes, ref width, ref height)
            || TryReadGif(bytes, ref width, ref height)
            || TryReadJpeg(bytes, ref width, ref height)
            || TryReadWebP(bytes, ref width, ref height)
            || TryReadBmp(bytes, ref width, ref height)
            || TryReadIcon(bytes, ref width, ref height)
            || TryReadSvg(bytes, ref width, ref height);
    }

    /// <summary>
    /// <a href="https://www.w3.org/TR/png-3/#11IHDR">PNG §11.2.2</a>: the signature, then an <c>IHDR</c>
    /// chunk whose first eight payload bytes are the two dimensions, big-endian.
    /// </summary>
    private static bool TryReadPng(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        if (bytes.Length < 24
            || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47
            || bytes[4] != 0x0D || bytes[5] != 0x0A || bytes[6] != 0x1A || bytes[7] != 0x0A
            || bytes[12] != (byte) 'I' || bytes[13] != (byte) 'H' || bytes[14] != (byte) 'D' || bytes[15] != (byte) 'R')
        {
            return false;
        }

        width = Clamp(BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4)));
        height = Clamp(BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4)));
        return true;
    }

    /// <summary>
    /// <a href="https://www.w3.org/Graphics/GIF/spec-gif89a.txt">GIF89a §18</a>'s logical screen descriptor,
    /// which follows the six-byte signature and states the screen in two little-endian shorts.
    /// </summary>
    /// <remarks>
    /// <b>The logical screen is the whole answer, and an animation has no frames here.</b> A GIF's frames are
    /// image descriptors inside the data stream, each with its own size and offset; reading them would be a
    /// decode, and nothing a page can ask would tell them apart.
    /// </remarks>
    private static bool TryReadGif(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        if (bytes.Length < 10
            || bytes[0] != (byte) 'G' || bytes[1] != (byte) 'I' || bytes[2] != (byte) 'F'
            || bytes[3] != (byte) '8' || (bytes[4] != (byte) '7' && bytes[4] != (byte) '9') || bytes[5] != (byte) 'a')
        {
            return false;
        }

        width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(6, 2));
        height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(8, 2));
        return true;
    }

    /// <summary>
    /// <a href="https://www.w3.org/Graphics/JPEG/itu-t81.pdf">ITU-T T.81 §B.2.2</a>: the frame header of the
    /// first start-of-frame marker states the number of lines and samples per line.
    /// </summary>
    /// <remarks>
    /// The markers before it are skipped by their own declared length, which is what makes an EXIF thumbnail
    /// (an <c>APP1</c> segment holding a whole second JPEG) invisible here — walking into it would answer the
    /// thumbnail's size for the image.
    /// </remarks>
    private static bool TryReadJpeg(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return false;
        }

        var index = 2;

        while (index + 3 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
            {
                // Not at a marker: the stream is entropy-coded data or truncated, and there is no frame
                // header left to find. The container is still a JPEG, so this is available with no size
                // rather than broken.
                return true;
            }

            var marker = bytes[index + 1];
            index += 2;

            // A fill byte, and the standalone markers that carry no segment: TEM and the eight RSTn.
            if (marker == 0xFF)
            {
                index--;
                continue;
            }

            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9))
            {
                continue;
            }

            if (index + 1 >= bytes.Length)
            {
                return true;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index, 2));

            if (IsStartOfFrame(marker))
            {
                if (index + 7 >= bytes.Length)
                {
                    return true;
                }

                height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(index + 5, 2));
                return true;
            }

            if (length < 2)
            {
                return true;
            }

            index += length;
        }

        return true;
    }

    /// <summary>
    /// The start-of-frame markers: <c>SOF0</c>–<c>SOF3</c>, <c>SOF5</c>–<c>SOF7</c>, <c>SOF9</c>–<c>SOF11</c>
    /// and <c>SOF13</c>–<c>SOF15</c>. The four gaps are <c>DHT</c>, <c>JPG</c>, <c>DAC</c> and the reserved
    /// one, none of which is a frame.
    /// </summary>
    private static bool IsStartOfFrame(byte marker)
        => marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);

    /// <summary>
    /// <a href="https://developers.google.com/speed/webp/docs/riff_container">The WebP RIFF container</a>, in
    /// its three shapes: a simple lossy <c>VP8 </c> frame, a simple lossless <c>VP8L</c> frame, and the
    /// extended <c>VP8X</c> header whose canvas size is what an animation or an alpha channel is sized by.
    /// </summary>
    private static bool TryReadWebP(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        if (bytes.Length < 16
            || bytes[0] != (byte) 'R' || bytes[1] != (byte) 'I' || bytes[2] != (byte) 'F' || bytes[3] != (byte) 'F'
            || bytes[8] != (byte) 'W' || bytes[9] != (byte) 'E' || bytes[10] != (byte) 'B' || bytes[11] != (byte) 'P')
        {
            return false;
        }

        var fourCc = bytes.Slice(12, 4);

        // The lossy bitstream: a three-byte frame tag, the three-byte sync code 9D 01 2A, then two
        // fourteen-bit dimensions. The top two bits of each are the horizontal and vertical scale, which
        // nothing here uses.
        if (fourCc[0] == 'V' && fourCc[1] == 'P' && fourCc[2] == '8' && fourCc[3] == ' ' && bytes.Length >= 30)
        {
            if (bytes[23] != 0x9D || bytes[24] != 0x01 || bytes[25] != 0x2A)
            {
                return true;
            }

            width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2)) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(28, 2)) & 0x3FFF;
            return true;
        }

        // The lossless bitstream: a 0x2F signature byte, then fourteen bits of width-1 and fourteen of
        // height-1 packed little-endian.
        if (fourCc[0] == 'V' && fourCc[1] == 'P' && fourCc[2] == '8' && fourCc[3] == 'L' && bytes.Length >= 25)
        {
            if (bytes[20] != 0x2F)
            {
                return true;
            }

            var packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(21, 4));
            width = (int) (packed & 0x3FFF) + 1;
            height = (int) ((packed >> 14) & 0x3FFF) + 1;
            return true;
        }

        // The extended header: one flags byte, three reserved, then two twenty-four-bit canvas dimensions,
        // each stored one less than its value.
        if (fourCc[0] == 'V' && fourCc[1] == 'P' && fourCc[2] == '8' && fourCc[3] == 'X' && bytes.Length >= 30)
        {
            width = (bytes[24] | (bytes[25] << 8) | (bytes[26] << 16)) + 1;
            height = (bytes[27] | (bytes[28] << 8) | (bytes[29] << 16)) + 1;
            return true;
        }

        return true;
    }

    /// <summary>
    /// The Windows bitmap file header, then the DIB header whose <i>size</i> field says which of the two
    /// dimension layouts follows: the twelve-byte <c>BITMAPCOREHEADER</c>'s signed shorts, or the signed
    /// integers every later version shares.
    /// </summary>
    /// <remarks>
    /// A negative height is a top-down bitmap rather than a negative size, so the magnitude is the answer.
    /// </remarks>
    private static bool TryReadBmp(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        if (bytes.Length < 26 || bytes[0] != (byte) 'B' || bytes[1] != (byte) 'M')
        {
            return false;
        }

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(14, 4));

        if (headerSize == 12)
        {
            width = Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(18, 2)));
            height = Math.Abs(BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(20, 2)));
            return true;
        }

        width = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(18, 4)));
        height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(22, 4)));
        return true;
    }

    /// <summary>
    /// The Windows icon directory: a reserved zero, the type, the image count, then one sixteen-byte entry
    /// per image whose first two bytes are its width and height — with zero meaning 256.
    /// </summary>
    /// <remarks>
    /// <b>The largest entry is the answer</b>, which is the entry a browser picks for an <c>&lt;img&gt;</c>
    /// with no size of its own. An <c>.ico</c> reached through <c>&lt;img src&gt;</c> rather than
    /// <c>&lt;link rel=icon&gt;</c> is rarer than the rest of this file's formats, and it is here because
    /// the alternative — the broken state — is a wrong answer rather than a missing one.
    /// </remarks>
    private static bool TryReadIcon(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        if (bytes.Length < 22 || bytes[0] != 0 || bytes[1] != 0 || bytes[3] != 0 || (bytes[2] != 1 && bytes[2] != 2))
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(4, 2));

        for (var entry = 0; entry < count; entry++)
        {
            var offset = 6 + (entry * 16);

            if (offset + 1 >= bytes.Length)
            {
                break;
            }

            var entryWidth = bytes[offset] == 0 ? 256 : bytes[offset];
            var entryHeight = bytes[offset + 1] == 0 ? 256 : bytes[offset + 1];

            if ((long) entryWidth * entryHeight > (long) width * height)
            {
                width = entryWidth;
                height = entryHeight;
            }
        }

        return true;
    }

    /// <summary>
    /// <a href="https://svgwg.org/specs/integration/#svg-css-sizing">SVG Integration §1.3</a>: a vector
    /// image's intrinsic dimensions are the root <c>&lt;svg&gt;</c>'s <c>width</c> and <c>height</c>, and it
    /// has none when either is a percentage or absent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a scan of the root start tag, not a parse.</b> The two attributes are on the document
    /// element, so finding that element is the whole of the work; running AngleSharp's XML parser over an
    /// arbitrary number of megabytes of path data to reach two attributes on its first tag would be a real
    /// cost for the same answer.
    /// </para>
    /// <para>
    /// <b>An SVG with no intrinsic size is available, not broken</b>, and <c>naturalWidth</c> then answers 0.
    /// A browser substitutes the 300 × 150 default sizing of a replaced element at layout time, which is a
    /// used value rather than an intrinsic one and is not something this browser has.
    /// </para>
    /// </remarks>
    private static bool TryReadSvg(ReadOnlySpan<byte> bytes, ref int width, ref int height)
    {
        var prefix = bytes.Length > SvgPrefixBytes ? bytes.Slice(0, SvgPrefixBytes) : bytes;
        string text;

        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
                .GetString(prefix.ToArray());
        }
        catch (ArgumentException)
        {
            return false;
        }

        var index = FindRootTag(text);

        if (index < 0)
        {
            return false;
        }

        var end = text.IndexOf('>', index);
        var tag = end < 0 ? text.Substring(index) : text.Substring(index, end - index);

        width = LengthOf(tag, "width");
        height = LengthOf(tag, "height");
        return true;
    }

    /// <summary>
    /// The index of the root <c>&lt;svg</c> start tag, skipping the XML declaration, comments, processing
    /// instructions and the doctype — or −1 when the first element is something else.
    /// </summary>
    private static int FindRootTag(string text)
    {
        var index = 0;

        // A byte order mark survives decoding as U+FEFF and is not white space.
        if (index < text.Length && text[index] == '\uFEFF')
        {
            index++;
        }

        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length || text[index] != '<')
            {
                return -1;
            }

            if (Follows(text, index, "<svg") && (index + 4 >= text.Length || !IsNameCharacter(text[index + 4])))
            {
                return index;
            }

            if (!Follows(text, index, "<?") && !Follows(text, index, "<!"))
            {
                return -1;
            }

            var end = text.IndexOf('>', index);

            if (end < 0)
            {
                return -1;
            }

            index = end + 1;
        }

        return -1;
    }

    private static bool Follows(string text, int index, string prefix)
        => index + prefix.Length <= text.Length
            && string.CompareOrdinal(text, index, prefix, 0, prefix.Length) == 0;

    private static bool IsNameCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':';

    /// <summary>
    /// The value of one attribute of the root start tag, as a whole number of CSS pixels — 0 for a
    /// percentage, a unit this browser has no conversion for, or an attribute that is not there.
    /// </summary>
    /// <remarks>
    /// <c>px</c> is the only unit accepted beside a bare number, because it is the only one whose value does
    /// not depend on a font or a viewport this image has neither of.
    /// </remarks>
    private static int LengthOf(string tag, string name)
    {
        var index = 0;

        while (true)
        {
            index = tag.IndexOf(name, index, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return 0;
            }

            var before = index == 0 || char.IsWhiteSpace(tag[index - 1]);
            var after = index + name.Length;

            while (after < tag.Length && char.IsWhiteSpace(tag[after]))
            {
                after++;
            }

            if (!before || after >= tag.Length || tag[after] != '=')
            {
                index += name.Length;
                continue;
            }

            after++;

            while (after < tag.Length && char.IsWhiteSpace(tag[after]))
            {
                after++;
            }

            if (after >= tag.Length || (tag[after] != '"' && tag[after] != '\''))
            {
                return 0;
            }

            var quote = tag[after];
            var close = tag.IndexOf(quote, after + 1);

            return close < 0 ? 0 : ParseLength(tag.AsSpan(after + 1, close - after - 1));
        }
    }

    private static int ParseLength(ReadOnlySpan<char> value)
    {
        value = value.Trim();

        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Slice(0, value.Length - 2).TrimEnd();
        }

        if (value.Length == 0
            || !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || double.IsNaN(number)
            || number <= 0)
        {
            return 0;
        }

        return number >= int.MaxValue ? int.MaxValue : (int) Math.Round(number);
    }

    /// <summary>A dimension a container states as an unsigned 32-bit value, held to what an <c>int</c> is.</summary>
    private static int Clamp(uint value) => value > int.MaxValue ? int.MaxValue : (int) value;
}

#if NET8_0_OR_GREATER
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SystemEncoding = System.Text.Encoding;
using Jint.Runtime;
using Jint.WebApi.Files;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One part of a parsed <c>multipart/form-data</c> body, as a plain CLR value.
/// </summary>
/// <param name="Name">The <c>name</c> parameter of the part's <c>Content-Disposition</c>, unescaped.</param>
/// <param name="FileName">
/// The <c>filename</c> parameter, unescaped, or <see langword="null"/> when the part carried none — which is
/// what decides whether the entry's value becomes a <c>File</c> or a string.
/// </param>
/// <param name="ContentType">The part's <c>Content-Type</c>, or <see langword="null"/> when it had none.</param>
/// <param name="Bytes">The part's body, verbatim.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct MultipartPart(string Name, string? FileName, string? ContentType, byte[] Bytes);

/// <summary>
/// The <c>multipart/form-data</c> writer and reader: the HTML Standard's
/// <see href="https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#multipart/form-data-encoding-algorithm">multipart/form-data
/// encoding algorithm</see>, which https://fetch.spec.whatwg.org/#concept-bodyinit-extract runs for a
/// <c>FormData</c> body, and the parser <c>formData()</c> needs to read one back.
/// </summary>
/// <remarks>
/// <para>
/// The HTML algorithm defines the escaping and then delegates the framing to RFC 7578 and, through it, to
/// RFC 2046 section 5.1; the WHATWG's own attempt at writing the two out step by step lives at
/// https://andreubotella.github.io/multipart-form-data/, which the Fetch Standard points at with "a more
/// detailed parsing specification is to be written". That draft is what this file implements — it is the only
/// text that states the parser at all, and its serializer, in its own words, "now matches the behavior of all
/// major browsers".
/// </para>
/// <para>
/// Deliberately engine-free apart from reading a <see cref="FormDataEntry"/>'s value: no <c>Realm</c>, no
/// promise, no property access. A failure is <see langword="null"/>, so the caller decides which realm's
/// <c>TypeError</c> to raise.
/// </para>
/// <para>
/// Three places where the parser is deliberately not the draft, each in the direction of the RFC:
/// </para>
/// <list type="bullet">
/// <item>
/// A part's body ends at the next <c>CRLF</c> <c>--</c> <i>boundary</i> — RFC 2046's <c>delimiter</c> — where
/// the draft scans for the boundary alone and then assumes the four bytes before it were that prefix. The two
/// agree on every well-formed body; on a body whose part contains the raw boundary text the draft mis-frames
/// and this keeps scanning.
/// </item>
/// <item>
/// HTTP tab and space after a delimiter are skipped, because RFC 2046's <c>transport-padding</c> says
/// receivers "MUST be able to handle padding added by message transports".
/// </item>
/// <item>
/// Anything after the close delimiter is discarded rather than being a failure, where the draft demands
/// exactly <c>--</c> <c>CRLF</c> and then end of input. RFC 2046 makes that trailing <c>CRLF</c> and its
/// epilogue optional, and a producer that omits it — Node's undici, for one — writes a body every other
/// implementation reads.
/// </item>
/// </list>
/// <para>
/// And two where it is deliberately strict, following the draft: a preamble before the first delimiter is a
/// failure, and a part's <c>Content-Disposition</c> must begin with exactly
/// <c>form-data; name="</c> — one space, lowercased, the value quoted. Both are what browsers accept, and
/// neither is a shape a conforming producer emits.
/// </para>
/// </remarks>
internal static class MultipartFormData
{
    /// <summary>The MIME type essence a <c>multipart/form-data</c> body carries.</summary>
    internal const string Essence = "multipart/form-data";

    /// <summary>
    /// What https://fetch.spec.whatwg.org/#concept-bodyinit-extract prepends to the generated boundary to make
    /// the <c>Content-Type</c>: "Set type to `multipart/form-data; boundary=`, followed by the
    /// multipart/form-data boundary string". The boundary is a token, so it is never quoted.
    /// </summary>
    internal const string ContentTypePrefix = "multipart/form-data; boundary=";

    /// <summary>RFC 7578 section 4.4: a part's <c>Content-Type</c> "defaults to text/plain".</summary>
    internal const string DefaultPartContentType = "text/plain";

    /// <summary>
    /// What a file part is labelled with when the <c>File</c> claims no type — RFC 7578 section 4.4's
    /// "or application/octet-stream", and what the serializer draft names explicitly.
    /// </summary>
    private const string UnknownFileContentType = "application/octet-stream";

    private const byte Cr = 0x0D;
    private const byte Lf = 0x0A;
    private const byte Quote = 0x22;
    private const byte Colon = 0x3A;
    private const byte Tab = 0x09;
    private const byte Space = 0x20;

    private const string ContentDispositionPrefix = "Content-Disposition: form-data; name=\"";
    private const string ContentTypePartPrefix = "Content-Type: ";
    private const string FileNamePrefix = "; filename=\"";

    private static readonly byte[] _crLf = [Cr, Lf];
    private static readonly byte[] _dashDash = [(byte) '-', (byte) '-'];
    private static readonly byte[] _contentDispositionName = SystemEncoding.ASCII.GetBytes("content-disposition");
    private static readonly byte[] _contentTypeName = SystemEncoding.ASCII.GetBytes("content-type");
    private static readonly byte[] _formDataName = SystemEncoding.ASCII.GetBytes("form-data; name=\"");
    private static readonly byte[] _fileName = SystemEncoding.ASCII.GetBytes(FileNamePrefix);

    /// <summary>
    /// The prefix every generated boundary carries. Recognizable in a capture, and — being all dashes and
    /// letters — a valid <c>bcharsnospace</c> run.
    /// </summary>
    private const string BoundaryPrefix = "----JintFormBoundary";

    private const int BoundaryRandomLength = 24;

    private static ReadOnlySpan<char> BoundaryAlphabet => "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>
    /// Generate a multipart/form-data boundary,
    /// https://andreubotella.github.io/multipart-form-data/#generate-a-multipart-form-data-boundary — a
    /// sequence of 27 to 70 bytes from the ASCII alphanumerics plus <c>'</c>, <c>-</c> and <c>_</c>, "such
    /// that part of it is randomly generated, with a minimum entropy of 95 bits".
    /// </summary>
    /// <remarks>
    /// 24 characters drawn from 62 is a shade under 143 bits, comfortably past the floor, and the whole
    /// boundary is 44 characters — inside RFC 2046's 70. The randomness is
    /// <see cref="RandomNumberGenerator"/>'s and not <c>Random</c>'s, and that is the point rather than a
    /// flourish: the older rule was that the boundary must not appear anywhere in the payload, which a writer
    /// that never inspects the payload can only honour by making the boundary unguessable. A predictable one
    /// would let a script that controls a field value close the part early and forge the rest of the body.
    /// </remarks>
    internal static string GenerateBoundary()
    {
        Span<char> boundary = stackalloc char[BoundaryPrefix.Length + BoundaryRandomLength];
        BoundaryPrefix.AsSpan().CopyTo(boundary);
        RandomNumberGenerator.GetItems(BoundaryAlphabet, boundary.Slice(BoundaryPrefix.Length));
        return new string(boundary);
    }

    /// <summary>
    /// The multipart/form-data encoding algorithm,
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#multipart/form-data-encoding-algorithm,
    /// stated as the chunk serializer of
    /// https://andreubotella.github.io/multipart-form-data/#multipart-form-data-chunk-serializer.
    /// </summary>
    /// <remarks>
    /// The encoding is UTF-8 throughout, which is the only one the Fetch Standard ever passes. Nothing here
    /// mutates the entry list: the algorithm's newline normalization is phrased as replacing text "in entry's
    /// name", but a <c>FormData</c> a script still holds must not have its entries rewritten by having been
    /// sent, so the normalized string is built on the way out.
    /// </remarks>
    internal static byte[] Serialize(List<FormDataEntry> entries, string boundary)
    {
        var writer = new ArrayBufferWriter<byte>();

        foreach (var entry in entries)
        {
            AppendUtf8(writer, "--");
            AppendUtf8(writer, boundary);
            writer.Write(_crLf);
            AppendUtf8(writer, ContentDispositionPrefix);
            AppendEscapedName(writer, entry.Name, isFileName: false);
            AppendUtf8(writer, "\"");

            if (entry.Value is JsFile file)
            {
                AppendUtf8(writer, FileNamePrefix);
                AppendEscapedName(writer, file.Name, isFileName: true);
                AppendUtf8(writer, "\"");
                writer.Write(_crLf);

                // The media type is ASCII by construction — every Blob and File normalizes it to the empty
                // string unless every code point is between U+0020 and U+007E — so encoding it as UTF-8 is
                // the isomorphic encoding the serializer asks for.
                AppendUtf8(writer, ContentTypePartPrefix);
                AppendUtf8(writer, file.MediaType.Length == 0 ? UnknownFileContentType : file.MediaType);
                writer.Write(_crLf);
                writer.Write(_crLf);
                writer.Write(file.Data.Span);
                writer.Write(_crLf);
                continue;
            }

            // A non-file field must not carry a Content-Type at all, which is the one thing the HTML
            // algorithm says about a part's headers beyond the Content-Disposition.
            writer.Write(_crLf);
            writer.Write(_crLf);
            AppendUtf8(writer, NormalizeNewlines(TypeConverter.ToString(entry.Value)));
            writer.Write(_crLf);
        }

        AppendUtf8(writer, "--");
        AppendUtf8(writer, boundary);
        AppendUtf8(writer, "--");
        writer.Write(_crLf);

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// The multipart/form-data parser,
    /// https://andreubotella.github.io/multipart-form-data/#multipart-form-data-parser — the parts, or
    /// <see langword="null"/> for the specification's failure, which <c>formData()</c> turns into a
    /// <c>TypeError</c>.
    /// </summary>
    internal static List<MultipartPart>? Parse(ReadOnlySpan<byte> input, string boundary)
    {
        if (!IsBoundary(boundary))
        {
            return null;
        }

        var dashBoundary = new byte[boundary.Length + 2];
        dashBoundary[0] = (byte) '-';
        dashBoundary[1] = (byte) '-';
        for (var i = 0; i < boundary.Length; i++)
        {
            dashBoundary[i + 2] = (byte) boundary[i];
        }

        // RFC 2046's delimiter: the CRLF that ends the previous part belongs to the delimiter, not to the
        // part's body, which is why an empty part body is possible at all.
        var delimiter = new byte[dashBoundary.Length + 2];
        delimiter[0] = Cr;
        delimiter[1] = Lf;
        dashBoundary.CopyTo(delimiter.AsSpan(2));

        var parts = new List<MultipartPart>();
        var position = 0;

        while (true)
        {
            if (!StartsWith(input, position, dashBoundary))
            {
                return null;
            }

            position += dashBoundary.Length;

            if (StartsWith(input, position, _dashDash))
            {
                // The close delimiter; whatever follows is transport padding and an epilogue, both discarded.
                return parts;
            }

            position = SkipTabOrSpace(input, position);
            if (!StartsWith(input, position, _crLf))
            {
                return null;
            }

            position += 2;

            if (!TryParseHeaders(input, ref position, out var name, out var fileName, out var contentType))
            {
                return null;
            }

            // Skip past the empty line that marks the end of the headers.
            position += 2;

            var end = IndexOf(input, position, delimiter);
            if (end < 0)
            {
                return null;
            }

            parts.Add(new MultipartPart(name, fileName, contentType, input.Slice(position, end - position).ToArray()));

            // Leave the position on the delimiter's dash-boundary, where the next iteration expects it.
            position = end + 2;
        }
    }

    /// <summary>
    /// Parse multipart/form-data headers,
    /// https://andreubotella.github.io/multipart-form-data/#parse-multipart-form-data-headers. On success
    /// <paramref name="position"/> is left on the empty line that ends the headers, which the caller skips.
    /// </summary>
    private static bool TryParseHeaders(
        ReadOnlySpan<byte> input,
        ref int position,
        [NotNullWhen(true)] out string? name,
        out string? fileName,
        out string? contentType)
    {
        name = null;
        fileName = null;
        contentType = null;

        while (true)
        {
            if (StartsWith(input, position, _crLf))
            {
                // A part with no Content-Disposition, or one naming no field, is a failure.
                return name is not null;
            }

            var start = position;
            while (position < input.Length && input[position] is not (Cr or Lf or Colon))
            {
                position++;
            }

            var headerName = TrimTabOrSpace(input.Slice(start, position - start));
            if (!IsFieldName(headerName))
            {
                return false;
            }

            if (position >= input.Length || input[position] != Colon)
            {
                return false;
            }

            position++;
            position = SkipTabOrSpace(input, position);

            if (EqualsIgnoreAsciiCase(headerName, _contentDispositionName))
            {
                // A second Content-Disposition discards what the first said, rather than merging.
                name = null;
                fileName = null;

                if (!StartsWith(input, position, _formDataName))
                {
                    return false;
                }

                position += _formDataName.Length;
                if (!TryParseName(input, ref position, out name))
                {
                    return false;
                }

                if (StartsWith(input, position, _fileName))
                {
                    position += _fileName.Length;
                    if (!TryParseName(input, ref position, out fileName))
                    {
                        return false;
                    }
                }
            }
            else if (EqualsIgnoreAsciiCase(headerName, _contentTypeName))
            {
                var valueStart = position;
                position = SkipToNewline(input, position);
                contentType = IsomorphicDecode(TrimEndTabOrSpace(input.Slice(valueStart, position - valueStart)));
            }
            else
            {
                // RFC 7578 section 4.8: header fields other than these "MUST be ignored".
                position = SkipToNewline(input, position);
            }

            if (!StartsWith(input, position, _crLf))
            {
                return false;
            }

            position += 2;
        }
    }

    /// <summary>
    /// Parse a multipart/form-data name,
    /// https://andreubotella.github.io/multipart-form-data/#parse-a-multipart-form-data-name — the quoted run
    /// after a <c>name="</c> or <c>filename="</c>, with the writer's three escapes undone.
    /// </summary>
    private static bool TryParseName(ReadOnlySpan<byte> input, ref int position, [NotNullWhen(true)] out string? value)
    {
        var start = position;
        while (position < input.Length && input[position] is not (Cr or Lf or Quote))
        {
            position++;
        }

        if (position >= input.Length || input[position] != Quote)
        {
            value = null;
            return false;
        }

        value = DecodeName(input.Slice(start, position - start));
        position++;
        return true;
    }

    /// <summary>
    /// Undoes the escaping the writer applied — <c>%0A</c>, <c>%0D</c> and <c>%22</c>, and nothing else,
    /// because "the user agent must not perform any other escapes" — and then UTF-8 decodes without stripping
    /// a BOM.
    /// </summary>
    /// <remarks>
    /// Only the uppercase hexadecimal spelling is recognized, which is the one the escape produces; a
    /// <c>%0a</c> in a name is a literal <c>%0a</c>. That keeps the round trip exact and keeps the unescaping
    /// from inventing a CRLF a producer never wrote.
    /// </remarks>
    private static string DecodeName(ReadOnlySpan<byte> name)
    {
        var percent = name.IndexOf((byte) '%');
        if (percent < 0)
        {
            return Utf8DecodeWithoutBom(name);
        }

        var decoded = new ArrayBufferWriter<byte>(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == (byte) '%' && i + 2 < name.Length)
            {
                var escaped = Unescape(name[i + 1], name[i + 2]);
                if (escaped != 0)
                {
                    decoded.GetSpan(1)[0] = escaped;
                    decoded.Advance(1);
                    i += 2;
                    continue;
                }
            }

            decoded.GetSpan(1)[0] = name[i];
            decoded.Advance(1);
        }

        return Utf8DecodeWithoutBom(decoded.WrittenSpan);
    }

    private static byte Unescape(byte first, byte second) => (first, second) switch
    {
        ((byte) '0', (byte) 'A') => Lf,
        ((byte) '0', (byte) 'D') => Cr,
        ((byte) '2', (byte) '2') => Quote,
        _ => 0,
    };

    /// <summary>
    /// https://encoding.spec.whatwg.org/#utf-8-decode-without-bom — no BOM is stripped, and an ill-formed
    /// sequence becomes U+FFFD rather than raising, which is what <see cref="SystemEncoding.UTF8"/> does.
    /// </summary>
    internal static string Utf8DecodeWithoutBom(ReadOnlySpan<byte> bytes) => SystemEncoding.UTF8.GetString(bytes);

    /// <summary>
    /// Escape a multipart/form-data name,
    /// https://andreubotella.github.io/multipart-form-data/#escape-a-multipart-form-data-name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the whole of the header-injection defence, and it is why a name is escaped after being encoded
    /// rather than before: what must not reach the header is a CR or LF <i>byte</i>. A name carrying
    /// <c>"\r\nContent-Type: text/html\r\n\r\n</c> becomes
    /// <c>%22%0D%0AContent-Type: text/html%0D%0A%0D%0A</c> — one header line, one field, exactly as the
    /// script asked.
    /// </para>
    /// <para>
    /// The newline normalization applies to an entry's name but not to a filename, which the algorithm only
    /// converts to a scalar value string — and that is what UTF-8 encoding already does to an unpaired
    /// surrogate. So a lone LF in a name is written <c>%0D%0A</c> and a lone LF in a filename <c>%0A</c>.
    /// </para>
    /// </remarks>
    private static void AppendEscapedName(ArrayBufferWriter<byte> writer, string name, bool isFileName)
    {
        var encoded = SystemEncoding.UTF8.GetBytes(isFileName ? name : NormalizeNewlines(name));

        var start = 0;
        for (var i = 0; i < encoded.Length; i++)
        {
            var escape = encoded[i] switch
            {
                Lf => "%0A",
                Cr => "%0D",
                Quote => "%22",
                _ => null,
            };

            if (escape is null)
            {
                continue;
            }

            writer.Write(encoded.AsSpan(start, i - start));
            AppendUtf8(writer, escape);
            start = i + 1;
        }

        writer.Write(encoded.AsSpan(start));
    }

    /// <summary>
    /// "Replace every occurrence of U+000D (CR) not followed by U+000A (LF), and every occurrence of U+000A
    /// (LF) not preceded by U+000D (CR), by a string consisting of U+000D (CR) and U+000A (LF)."
    /// </summary>
    private static string NormalizeNewlines(string value)
    {
        if (value.AsSpan().IndexOfAny('\r', '\n') < 0)
        {
            return value;
        }

        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c is not ('\r' or '\n'))
                {
                    builder.Append(c);
                    continue;
                }

                builder.Append('\r');
                builder.Append('\n');

                // A well-formed pair is one newline, not two.
                if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                {
                    i++;
                }
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void AppendUtf8(ArrayBufferWriter<byte> writer, string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var destination = writer.GetSpan(SystemEncoding.UTF8.GetByteCount(value));
        writer.Advance(SystemEncoding.UTF8.GetBytes(value.AsSpan(), destination));
    }

    /// <summary>
    /// RFC 2046 section 5.1.1's <c>boundary</c> production: 1 to 70 <c>bchars</c>, not ending with a space.
    /// </summary>
    /// <remarks>
    /// A boundary that is not this is refused rather than searched for. It is the only part of a
    /// <c>Content-Type</c> a hostile producer fully controls, and matching an arbitrary byte run — a lone
    /// <c>-</c>, or a run of CRLFs — would turn a body into whatever the sender wanted it to parse as. The
    /// draft parser accepts any byte sequence here; RFC 2046, which defines what a boundary <i>is</i>, does
    /// not.
    /// </remarks>
    private static bool IsBoundary(string boundary)
    {
        if (boundary.Length is 0 or > 70 || boundary[boundary.Length - 1] == ' ')
        {
            return false;
        }

        foreach (var c in boundary)
        {
            var valid = c is >= '0' and <= '9'
                or >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or '\'' or '(' or ')' or '+' or '_' or ',' or '-' or '.' or '/' or ':' or '=' or '?' or ' ';

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// RFC 9110's <c>field-name</c> token production — the same one a header name obeys, over bytes.
    /// </summary>
    private static bool IsFieldName(ReadOnlySpan<byte> name)
    {
        if (name.IsEmpty)
        {
            return false;
        }

        foreach (var b in name)
        {
            if (b > 0x7E || !HeaderList.IsTokenChar((char) b))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>https://infra.spec.whatwg.org/#isomorphic-decode — one byte, one code point.</summary>
    private static string IsomorphicDecode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var builder = new ValueStringBuilder(stackalloc char[64]);
        try
        {
            foreach (var b in bytes)
            {
                builder.Append((char) b);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static bool EqualsIgnoreAsciiCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> lowercased)
    {
        if (value.Length != lowercased.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if (b is >= (byte) 'A' and <= (byte) 'Z')
            {
                b += 0x20;
            }

            if (b != lowercased[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool StartsWith(ReadOnlySpan<byte> input, int position, ReadOnlySpan<byte> value)
        => (uint) position <= (uint) input.Length && input.Slice(position).StartsWith(value);

    private static int IndexOf(ReadOnlySpan<byte> input, int position, ReadOnlySpan<byte> value)
    {
        var index = input.Slice(position).IndexOf(value);
        return index < 0 ? -1 : position + index;
    }

    private static int SkipTabOrSpace(ReadOnlySpan<byte> input, int position)
    {
        while (position < input.Length && input[position] is Tab or Space)
        {
            position++;
        }

        return position;
    }

    private static int SkipToNewline(ReadOnlySpan<byte> input, int position)
    {
        while (position < input.Length && input[position] is not (Cr or Lf))
        {
            position++;
        }

        return position;
    }

    private static ReadOnlySpan<byte> TrimTabOrSpace(ReadOnlySpan<byte> value)
    {
        var start = 0;
        while (start < value.Length && value[start] is Tab or Space)
        {
            start++;
        }

        return TrimEndTabOrSpace(value.Slice(start));
    }

    private static ReadOnlySpan<byte> TrimEndTabOrSpace(ReadOnlySpan<byte> value)
    {
        var end = value.Length;
        while (end > 0 && value[end - 1] is Tab or Space)
        {
            end--;
        }

        return value.Slice(0, end);
    }
}
#endif

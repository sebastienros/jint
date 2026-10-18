#if NET8_0_OR_GREATER
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;
using SystemEncoding = System.Text.Encoding;
using Jint.Runtime;
using Jint.WebApi.Files;

namespace Jint.WebApi.Fetch;

/// <summary>
/// One part of a parsed <c>multipart/form-data</c> body, as a plain CLR value.
/// </summary>
/// <param name="Name">
/// The last <c>name</c> or valid <c>name*</c> parameter of the part's <c>Content-Disposition</c>, decoded.
/// </param>
/// <param name="FileName">
/// The <c>filename*</c> parameter decoded when the part carried a valid one, otherwise the <c>filename</c>
/// parameter unescaped, or <see langword="null"/> when it carried neither — which is what decides whether
/// the entry's value becomes a <c>File</c> or a string.
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
/// Four places where the parser is deliberately not the draft, each in the direction of the RFC:
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
/// <item>
/// A part's <c>Content-Disposition</c> is read as RFC 7578 section 4.2 defines it — RFC 2183's header, whose
/// parameter values are RFC 2045 section 5.1's <c>token / quoted-string</c> — where the draft accepts only
/// the one spelling HTML writes: <c>form-data; name="</c>, one space, lowercased, the value quoted, then an
/// optional <c>; filename="</c> and the end of the line. .NET's <c>MultipartFormDataContent</c> is a
/// mainstream producer, the one a host is likeliest to meet, and it does not write that spelling: it leaves a
/// <c>name</c> and a <c>filename</c> unquoted unless they need quoting, and appends an RFC 8187
/// <c>filename*</c> to every file name, which is the only place it writes a non-ASCII one properly. Node
/// reads all of it and prefers the decoded <c>filename*</c>, and so does this
/// (<see href="https://github.com/sebastienros/jint/issues/4269">sebastienros/jint#4269</see>), though
/// RFC 7578 tells senders not to use it. <see cref="TryParseContentDisposition"/> states the grammar and the
/// choices made inside it.
/// </item>
/// </list>
/// <para>
/// And one where it is deliberately strict, following the draft: a preamble before the first delimiter is a
/// failure. That is what browsers accept, and not a shape a conforming producer emits.
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
    private const byte Semicolon = 0x3B;
    private const byte EqualsSign = 0x3D;
    private const byte Tab = 0x09;
    private const byte Space = 0x20;

    private const string ContentDispositionPrefix = "Content-Disposition: form-data; name=\"";
    private const string ContentTypePartPrefix = "Content-Type: ";
    private const string FileNamePrefix = "; filename=\"";

    private static readonly byte[] _crLf = [Cr, Lf];
    private static readonly byte[] _dashDash = [(byte) '-', (byte) '-'];
    private static readonly byte[] _contentDispositionName = SystemEncoding.ASCII.GetBytes("content-disposition");
    private static readonly byte[] _contentTypeName = SystemEncoding.ASCII.GetBytes("content-type");
    private static readonly byte[] _formDataType = SystemEncoding.ASCII.GetBytes("form-data");
    private static readonly byte[] _nameParameter = SystemEncoding.ASCII.GetBytes("name");
    private static readonly byte[] _fileNameParameter = SystemEncoding.ASCII.GetBytes("filename");
    private static readonly byte[] _nameStarParameter = SystemEncoding.ASCII.GetBytes("name*");
    private static readonly byte[] _fileNameStarParameter = SystemEncoding.ASCII.GetBytes("filename*");
    private static readonly byte[] _utf8Charset = SystemEncoding.ASCII.GetBytes("utf-8");

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
                // A part with no Content-Disposition names no field, which is a failure; a Content-Disposition
                // that names none has failed already.
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
                // A second Content-Disposition discards what the first said, rather than merging: both outs
                // start again from null.
                if (!TryParseContentDisposition(input, ref position, out name, out fileName))
                {
                    return false;
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
    /// A part's <c>Content-Disposition</c> value, read as RFC 7578 section 4.2 defines it: RFC 2183's header,
    /// whose parameter values are RFC 2045 section 5.1's <c>value := token / quoted-string</c>. On entry
    /// <paramref name="position"/> is on the disposition type; on success it is left on the line break that
    /// ends the header, which the caller checks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grammar, with <c>OWS</c> being HTTP tab or space:
    /// </para>
    /// <code>
    /// value     = "form-data" *( OWS ";" OWS [ parameter ] ) OWS
    /// parameter = token OWS "=" OWS ( token / quoted-string )
    /// </code>
    /// <para>
    /// The disposition type and the parameter names are matched ASCII case-insensitively — RFC 2183's
    /// disposition-type "values are not case-sensitive", and RFC 2045's parameter names are not either. The
    /// separators are RFC 9110 section 5.6.6's <c>parameters</c>, whose <c>[ parameter ]</c> makes a trailing
    /// or doubled <c>;</c> an empty parameter rather than an error. The whitespace around <c>=</c> is not
    /// RFC 9110's, which allows none there; it is the linear whitespace RFC 822 lets a header put between any
    /// two lexical tokens, the lexical model RFC 2183 is written in. A token is a run of RFC 9110
    /// <c>tchar</c>, taken literally: no escape is undone in one, so <c>name=%22</c> is the three characters
    /// it says.
    /// </para>
    /// <para>
    /// A quoted-string is read the way the draft reads a name: every byte up to the next <c>"</c>, a line
    /// break being a failure, with the writer's <c>%22</c>, <c>%0D</c> and <c>%0A</c> undone by
    /// <see cref="DecodeName"/>. <b>A backslash is a literal byte, not the start of RFC 9110's
    /// <c>quoted-pair</c>.</b> HTML's writer — this file's, and every browser's — escapes a quote as
    /// <c>%22</c> and writes a backslash raw; .NET's <c>MultipartFormDataContent</c> refuses a quote inside a
    /// name outright and writes a backslash raw too. Reading <c>\</c> as an escape would turn
    /// <c>C:\dir\a.txt</c> into <c>C:dira.txt</c> and make a name that ends in a backslash unterminated. The
    /// draft, undici and Gecko read it literally; Chromium is the one that unescapes, and loses its own
    /// writer's backslashes doing so.
    /// </para>
    /// <para>
    /// <c>name</c> and <c>filename</c> are used, and so are their RFC 8187 extended forms <c>name*</c> and
    /// <c>filename*</c>; every other parameter is parsed and then skipped. A repeated parameter is
    /// <b>last-wins</b>: that is how undici, Chromium and Gecko all resolve it, and how a second
    /// <c>Content-Disposition</c> header already was.
    /// </para>
    /// <para>
    /// <b>A valid <c>filename*</c> wins over <c>filename</c>, whichever comes first.</b> RFC 7578 section
    /// 4.2's "MUST NOT be used" binds senders, and its section 5.1.3 tells parsers to "be aware of the variety
    /// of implementations". The implementation this reads most often is .NET's — <c>Response.formData()</c>
    /// against a .NET server, <c>Request.formData()</c> behind an ASP.NET host — and
    /// <c>MultipartFormDataContent</c> writes a non-ASCII file name properly only in <c>filename*</c>: its
    /// <c>filename</c> is an RFC 2047 encoded-word that nothing decodes. Undici, the runtime this API is most
    /// often compared with on a server, prefers the decoded <c>filename*</c> in just this way, and RFC 6266
    /// section 4.3 asks it of any recipient that sees both. <c>filename*</c> never supplies a <c>name</c>.
    /// <c>name*</c> is decoded because undici decodes it, and as in undici it takes its turn in last-wins
    /// beside <c>name</c> rather than winning over it, since RFC 6266 states that preference for
    /// <c>filename</c> alone.
    /// </para>
    /// <para>
    /// What counts as valid is <see cref="TryDecodeExtendedValue"/>'s to say: an RFC 8187 <c>ext-value</c>
    /// whose charset is UTF-8. Anything else — another charset, a malformed value, a bad percent-escape, bytes
    /// that are not UTF-8, a quoted-string — is ignored as though the parameter were absent, so the plain
    /// form stands, and it never fails the body. That is where this parts from undici, which rejects the whole
    /// <c>formData()</c> for each of them.
    /// </para>
    /// <para>
    /// Still a failure: a type other than <c>form-data</c>, a parameter with no <c>=</c>, an empty parameter
    /// name or token value, an unterminated quoted-string, anything between parameters that is not a
    /// <c>;</c>, and a header that carries no <c>name</c> — RFC 7578 section 4.2's "MUST also contain".
    /// </para>
    /// </remarks>
    private static bool TryParseContentDisposition(
        ReadOnlySpan<byte> input,
        ref int position,
        [NotNullWhen(true)] out string? name,
        out string? fileName)
    {
        name = null;
        fileName = null;
        string? extendedFileName = null;

        var typeStart = position;
        position = SkipToken(input, position);
        if (!EqualsIgnoreAsciiCase(input.Slice(typeStart, position - typeStart), _formDataType))
        {
            return false;
        }

        while (true)
        {
            position = SkipTabOrSpace(input, position);
            if (position >= input.Length || input[position] is Cr or Lf)
            {
                fileName = extendedFileName ?? fileName;
                return name is not null;
            }

            if (input[position] != Semicolon)
            {
                return false;
            }

            position = SkipTabOrSpace(input, position + 1);
            if (position >= input.Length || input[position] is Cr or Lf or Semicolon)
            {
                // RFC 9110's `[ parameter ]`: an empty one is no parameter at all.
                continue;
            }

            var parameterStart = position;
            position = SkipToken(input, position);
            var parameterName = input.Slice(parameterStart, position - parameterStart);
            if (parameterName.IsEmpty)
            {
                return false;
            }

            position = SkipTabOrSpace(input, position);
            if (position >= input.Length || input[position] != EqualsSign)
            {
                return false;
            }

            position = SkipTabOrSpace(input, position + 1);

            var quoted = position < input.Length && input[position] == Quote;
            ReadOnlySpan<byte> value;
            if (quoted)
            {
                if (!TryParseQuotedString(input, ref position, out value))
                {
                    return false;
                }
            }
            else
            {
                var valueStart = position;
                position = SkipToken(input, position);
                value = input.Slice(valueStart, position - valueStart);
                if (value.IsEmpty)
                {
                    return false;
                }
            }

            // Decoded only once it is known to be wanted, so a skipped parameter costs no string. An extended
            // value that is not a valid one is as though it were absent.
            if (EqualsIgnoreAsciiCase(parameterName, _nameParameter))
            {
                name = DecodeParameterValue(value, quoted);
            }
            else if (EqualsIgnoreAsciiCase(parameterName, _fileNameParameter))
            {
                fileName = DecodeParameterValue(value, quoted);
            }
            else if (EqualsIgnoreAsciiCase(parameterName, _nameStarParameter))
            {
                if (TryDecodeExtendedValue(value, quoted, out var decodedName))
                {
                    name = decodedName;
                }
            }
            else if (EqualsIgnoreAsciiCase(parameterName, _fileNameStarParameter))
            {
                if (TryDecodeExtendedValue(value, quoted, out var decodedFileName))
                {
                    extendedFileName = decodedFileName;
                }
            }
        }
    }

    /// <summary>
    /// An RFC 8187 section 3.2.1 <c>ext-value</c> — <c>charset "'" [ language ] "'" value-chars</c> —
    /// decoded, or <see langword="false"/> when <paramref name="value"/> is not one this reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The charset must be <c>UTF-8</c>, in any case. It is the one charset RFC 8187 requires; RFC 5987's
    /// ISO-8859-1 is down to an encouragement "for compatibility with legacy code", and undici refuses it, so
    /// it is refused here too. The language tag is checked only for its characters — letters, digits and
    /// <c>-</c> — and then dropped. Each value-char is an <c>attr-char</c>, "token except ( "*" / "'" / "%" )",
    /// or a <c>%</c> and two hexadecimal digits of either case, and the bytes they spell must be well-formed
    /// UTF-8.
    /// </para>
    /// <para>
    /// An <c>ext-value</c> "is a token", so a quoted-string is not one, which RFC 8187 calls invalid even
    /// though some parsers accept it.
    /// </para>
    /// </remarks>
    private static bool TryDecodeExtendedValue(ReadOnlySpan<byte> value, bool quoted, [NotNullWhen(true)] out string? decoded)
    {
        decoded = null;
        if (quoted)
        {
            return false;
        }

        var charsetEnd = value.IndexOf((byte) '\'');
        if (charsetEnd < 0 || !EqualsIgnoreAsciiCase(value.Slice(0, charsetEnd), _utf8Charset))
        {
            return false;
        }

        var rest = value.Slice(charsetEnd + 1);
        var languageEnd = rest.IndexOf((byte) '\'');
        if (languageEnd < 0)
        {
            return false;
        }

        foreach (var b in rest.Slice(0, languageEnd))
        {
            if (!char.IsAsciiLetterOrDigit((char) b) && b != (byte) '-')
            {
                return false;
            }
        }

        var valueChars = rest.Slice(languageEnd + 1);
        Span<byte> bytes = valueChars.Length <= 256 ? stackalloc byte[256] : new byte[valueChars.Length];
        var length = 0;
        for (var i = 0; i < valueChars.Length; i++)
        {
            var b = valueChars[i];
            if (b == (byte) '%')
            {
                if (i + 2 >= valueChars.Length)
                {
                    return false;
                }

                var high = FromHexDigit(valueChars[i + 1]);
                var low = FromHexDigit(valueChars[i + 2]);
                if (high < 0 || low < 0)
                {
                    return false;
                }

                bytes[length++] = (byte) ((high << 4) | low);
                i += 2;
            }
            else if (b is not ((byte) '*' or (byte) '\'') && HeaderList.IsTokenChar((char) b))
            {
                bytes[length++] = b;
            }
            else
            {
                return false;
            }
        }

        var written = bytes.Slice(0, length);
        if (!Utf8.IsValid(written))
        {
            return false;
        }

        decoded = Utf8DecodeWithoutBom(written);
        return true;
    }

    private static int FromHexDigit(byte b) => b switch
    {
        >= (byte) '0' and <= (byte) '9' => b - '0',
        >= (byte) 'A' and <= (byte) 'F' => b - 'A' + 10,
        >= (byte) 'a' and <= (byte) 'f' => b - 'a' + 10,
        _ => -1,
    };

    /// <summary>
    /// The scan half of the draft's parse a multipart/form-data name,
    /// https://andreubotella.github.io/multipart-form-data/#parse-a-multipart-form-data-name — every byte up
    /// to the next <c>"</c>, a CR or LF before it being a failure. On entry <paramref name="position"/> is on
    /// the opening quote; on success it is past the closing one and <paramref name="content"/> is what lay
    /// between, still escaped.
    /// </summary>
    private static bool TryParseQuotedString(ReadOnlySpan<byte> input, scoped ref int position, out ReadOnlySpan<byte> content)
    {
        var start = position + 1;
        var end = start;
        while (end < input.Length && input[end] is not (Cr or Lf or Quote))
        {
            end++;
        }

        if (end >= input.Length || input[end] != Quote)
        {
            content = default;
            return false;
        }

        content = input.Slice(start, end - start);
        position = end + 1;
        return true;
    }

    /// <summary>
    /// A quoted-string's content has the writer's escapes undone; a token is ASCII by construction and is
    /// taken as it stands.
    /// </summary>
    private static string DecodeParameterValue(ReadOnlySpan<byte> value, bool quoted)
        => quoted ? DecodeName(value) : Utf8DecodeWithoutBom(value);

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

    /// <summary>
    /// Past a run of RFC 9110 <c>tchar</c> — a <c>token</c>, or nothing when the run is empty, which each
    /// caller decides about.
    /// </summary>
    private static int SkipToken(ReadOnlySpan<byte> input, int position)
    {
        while (position < input.Length && HeaderList.IsTokenChar((char) input[position]))
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

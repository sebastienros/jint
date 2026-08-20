#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Text;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Url.Parsing;

/// <summary>
/// The eight percent-encode sets of https://url.spec.whatwg.org/#percent-encoded-bytes, as one bit each.
/// </summary>
/// <remarks>
/// The sets nest — fragment and query both extend C0 control, special-query and path extend query, userinfo
/// extends path, component extends userinfo, and form-urlencoded extends component — so a code point's row in
/// <see cref="PercentEncoding"/>'s table carries a bit for every set that contains it and a membership test is
/// one <c>AND</c>. The fragment set is the one that is not on that chain: it contains U+0060 (`) where the
/// query set does not, which is why the spec spells the two out separately rather than deriving one from the
/// other.
/// </remarks>
[Flags]
internal enum PercentEncodeSet : byte
{
    /// <summary>https://url.spec.whatwg.org/#c0-control-percent-encode-set</summary>
    C0Control = 1,

    /// <summary>https://url.spec.whatwg.org/#fragment-percent-encode-set</summary>
    Fragment = 2,

    /// <summary>https://url.spec.whatwg.org/#query-percent-encode-set</summary>
    Query = 4,

    /// <summary>https://url.spec.whatwg.org/#special-query-percent-encode-set</summary>
    SpecialQuery = 8,

    /// <summary>https://url.spec.whatwg.org/#path-percent-encode-set</summary>
    Path = 16,

    /// <summary>https://url.spec.whatwg.org/#userinfo-percent-encode-set</summary>
    Userinfo = 32,

    /// <summary>https://url.spec.whatwg.org/#component-percent-encode-set</summary>
    Component = 64,

    /// <summary>https://url.spec.whatwg.org/#application-x-www-form-urlencoded-percent-encode-set</summary>
    FormUrlEncoded = 128,
}

/// <summary>
/// Percent-encoding and percent-decoding, https://url.spec.whatwg.org/#percent-encoded-bytes.
/// </summary>
/// <remarks>
/// Everything here works on UTF-8, which is the only encoding this implementation supports. The spec's
/// <i>percent-encode after encoding</i> takes an encoding argument for HTML's sake, but its first step asserts
/// that anything other than UTF-8 is confined to the two query sets, and the URL and URLSearchParams APIs both
/// always pass UTF-8 — so the legacy-encoding branch, and with it the "%26%23…%3B" numeric-reference fallback,
/// cannot be reached from this surface at all.
/// </remarks>
internal static class PercentEncoding
{
    /// <summary>
    /// One byte per ASCII code point, holding a <see cref="PercentEncodeSet"/> bit for every set that contains
    /// it. Code points above U+007F are in the C0 control percent-encode set and therefore in all eight, and
    /// are handled without a lookup.
    /// </summary>
    /// <remarks>
    /// A <c>ReadOnlySpan&lt;byte&gt;</c> over a constant array initializer compiles to a direct reference to
    /// the assembly's data section, so this costs no static constructor and no allocation.
    /// </remarks>
    private static ReadOnlySpan<byte> SetMembership =>
    [
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,  // 0x00
        255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,  // 0x10
        254, 128, 254, 252, 192, 192, 192, 136, 128, 128,   0, 192, 192,   0,   0, 224,  // 0x20  SP ! " # $ % & ' ( ) * + , - . /
          0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 224, 224, 254, 224, 254, 240,  // 0x30  0-9 : ; < = > ?
        224,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  // 0x40  @ A-O
          0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 224, 224, 224, 240,   0,  // 0x50  P-Z [ \ ] ^ _
        242,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0,  // 0x60  ` a-o
          0,   0,   0,   0,   0,   0,   0,   0,   0,   0,   0, 240, 224, 240, 128, 255,  // 0x70  p-z { | } ~ DEL
    ];

    /// <summary>
    /// Whether <paramref name="c"/> is in <paramref name="set"/>. Every set contains every code point above
    /// U+007E, which is what the C0 control percent-encode set's second half says.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsInSet(char c, PercentEncodeSet set) => c > 0x7F || (SetMembership[c] & (byte) set) != 0;

    /// <summary>
    /// https://url.spec.whatwg.org/#string-utf-8-percent-encode — UTF-8 percent-encode a scalar value string
    /// using <paramref name="set"/>, appending to <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="spaceAsPlus"/> is the <c>spaceAsPlus</c> of
    /// https://url.spec.whatwg.org/#string-percent-encode-after-encoding, true exactly for the
    /// application/x-www-form-urlencoded set. An unpaired surrogate encodes as U+FFFD, which is what makes
    /// every one of these operations a USVString operation even for a caller that skipped the conversion.
    /// </remarks>
    internal static void Append(ref ValueStringBuilder builder, ReadOnlySpan<char> input, PercentEncodeSet set, bool spaceAsPlus = false)
    {
        var i = 0;
        while (i < input.Length)
        {
            var c = input[i];
            if (c < 0x80)
            {
                i++;

                if (spaceAsPlus && c == ' ')
                {
                    builder.Append('+');
                    continue;
                }

                if ((SetMembership[c] & (byte) set) == 0)
                {
                    builder.Append(c);
                    continue;
                }

                AppendPercentEncodedByte(ref builder, (byte) c);
                continue;
            }

            // Non-ASCII is in every set, so the whole scalar value is encoded byte by byte. Decoding it as a
            // Rune is what keeps a surrogate pair one code point rather than two lone halves.
            Rune.DecodeFromUtf16(input.Slice(i), out var rune, out var consumed);
            i += consumed;
            AppendUtf8PercentEncoded(ref builder, rune);
        }
    }

    /// <summary>
    /// <see cref="Append(ref ValueStringBuilder, ReadOnlySpan{char}, PercentEncodeSet, bool)"/> for a caller
    /// that wants the string.
    /// </summary>
    internal static string Encode(ReadOnlySpan<char> input, PercentEncodeSet set, bool spaceAsPlus = false)
    {
        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            Append(ref builder, input, set, spaceAsPlus);
            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// UTF-8 percent-encodes one scalar value, https://url.spec.whatwg.org/#utf-8-percent-encode.
    /// </summary>
    internal static void AppendUtf8PercentEncoded(ref ValueStringBuilder builder, Rune rune)
    {
        Span<byte> utf8 = stackalloc byte[4];
        var written = rune.EncodeToUtf8(utf8);
        for (var i = 0; i < written; i++)
        {
            AppendPercentEncodedByte(ref builder, utf8[i]);
        }
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#percent-encode — U+0025 (%) followed by two ASCII upper hex digits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendPercentEncodedByte(ref ValueStringBuilder builder, byte value)
    {
        builder.Append('%');
        builder.AppendHex(value);
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#percent-decode — percent-decodes a byte sequence.
    /// </summary>
    internal static byte[] Decode(ReadOnlySpan<byte> input)
    {
        var output = new byte[input.Length];
        var written = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var b = input[i];
            if (b != (byte) '%' || i + 2 >= input.Length || !TryHex(input[i + 1], out var high) || !TryHex(input[i + 2], out var low))
            {
                // A "%" that is not followed by two ASCII hex digits stays a literal "%": the spec appends the
                // byte and moves on rather than failing.
                output[written++] = b;
                continue;
            }

            output[written++] = (byte) ((high << 4) | low);
            i += 2;
        }

        return written == output.Length ? output : output.AsSpan(0, written).ToArray();
    }

    /// <summary>
    /// https://url.spec.whatwg.org/#string-percent-decode followed by UTF-8 decode without BOM: percent-decodes
    /// a scalar value string and reads the bytes back as UTF-8, with invalid sequences becoming U+FFFD.
    /// </summary>
    internal static string DecodeToString(string input)
    {
        if (input.IndexOf('%') < 0)
        {
            // Nothing to decode, so nothing to re-decode either: percent-decoding is the identity here and a
            // scalar value string is already valid UTF-16.
            return input;
        }

        var bytes = Decode(SystemEncoding.UTF8.GetBytes(input));
        return SystemEncoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Whether <paramref name="input"/> contains a percent-encoded byte,
    /// https://url.spec.whatwg.org/#percent-encoded-byte.
    /// </summary>
    internal static bool ContainsPercentEncodedByte(ReadOnlySpan<char> input)
    {
        for (var i = 0; i + 2 < input.Length; i++)
        {
            if (input[i] == '%' && UrlCharacters.IsAsciiHexDigit(input[i + 1]) && UrlCharacters.IsAsciiHexDigit(input[i + 2]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryHex(byte b, out int value)
    {
        if (b >= '0' && b <= '9')
        {
            value = b - '0';
            return true;
        }

        if (b >= 'A' && b <= 'F')
        {
            value = b - 'A' + 10;
            return true;
        }

        if (b >= 'a' && b <= 'f')
        {
            value = b - 'a' + 10;
            return true;
        }

        value = 0;
        return false;
    }
}
#endif

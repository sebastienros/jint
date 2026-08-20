#if NET8_0_OR_GREATER
using System.Text;
using Jint.WebApi.Url.Parsing;

namespace Jint.NodeCompat;

/// <summary>
/// The two string algorithms behind <c>node:querystring</c>: <c>querystring.escape</c> and
/// <c>querystring.unescape</c>, which <c>stringify</c> and <c>parse</c> are defined in terms of.
/// <para>
/// https://nodejs.org/api/querystring.html
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Node's escape set is <b>not</b> the WHATWG application/x-www-form-urlencoded set that
/// <see cref="FormUrlEncoded"/> serializes with — it is exactly <c>encodeURIComponent</c>'s: everything except
/// ASCII letters, digits and <c>- . _ ~ ! ' ( ) *</c> is escaped, and a space becomes <c>%20</c> rather than
/// <c>+</c>. That is the whole difference between the two modules, and the reason <c>querystring</c> "is more
/// performant but is not a standardized API" while <c>URLSearchParams</c> is the standard one. The percent
/// encoding and decoding themselves are the URL implementation's, so there is one of each in the assembly.
/// </para>
/// <para>
/// One deliberate deviation. Node's encoder throws <c>ERR_INVALID_URI</c> on a trailing unpaired surrogate and
/// silently mangles a leading one — its own comment says the branch "should never happen because all
/// URLSearchParams entries should already be converted to USVString". Jint performs that conversion instead,
/// so an unpaired surrogate encodes as U+FFFD, which is what every other percent-encoding path in the engine
/// does.
/// </para>
/// </remarks>
internal static class NodeQueryString
{
    /// <summary>
    /// One byte per ASCII code point: 1 for the characters <c>querystring.escape</c> leaves alone. Above
    /// U+007F everything is escaped, which needs no lookup.
    /// </summary>
    /// <remarks>
    /// A <c>ReadOnlySpan&lt;byte&gt;</c> over a constant array initializer compiles to a direct reference into
    /// the assembly's data section, so this costs no static constructor and no allocation.
    /// </remarks>
    private static ReadOnlySpan<byte> NoEscape =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 0x00
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 0x10
        0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1, 1, 0, // 0x20  SP ! " # $ % & ' ( ) * + , - . /
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, // 0x30  0-9 : ; < = > ?
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 0x40  @ A-O
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, // 0x50  P-Z [ \ ] ^ _
        0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 0x60  ` a-o
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 1, 0, // 0x70  p-z { | } ~ DEL
    ];

    /// <summary>
    /// <c>querystring.escape(str)</c>: "performs URL percent-encoding on the given <c>str</c> in a manner that
    /// is optimized for the specific requirements of URL query strings".
    /// <para>
    /// https://nodejs.org/api/querystring.html#querystringescapestr
    /// </para>
    /// </summary>
    internal static string Escape(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var builder = new ValueStringBuilder(stackalloc char[128]);
        try
        {
            var i = 0;
            while (i < value.Length)
            {
                var c = value[i];
                if (c < 0x80)
                {
                    i++;
                    if (NoEscape[c] == 1)
                    {
                        builder.Append(c);
                    }
                    else
                    {
                        PercentEncoding.AppendPercentEncodedByte(ref builder, (byte) c);
                    }

                    continue;
                }

                // Decoding the scalar value is what keeps a surrogate pair one code point rather than two
                // lone halves, and turns an unpaired half into U+FFFD.
                Rune.DecodeFromUtf16(value.AsSpan(i), out var rune, out var consumed);
                i += consumed;
                PercentEncoding.AppendUtf8PercentEncoded(ref builder, rune);
            }

            return builder.AsSpan().ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// <c>querystring.unescape(str)</c>: "performs decoding of URL percent-encoded characters", using
    /// <c>decodeURIComponent</c> and falling back to "a safer equivalent that does not throw on malformed
    /// URLs".
    /// <para>
    /// https://nodejs.org/api/querystring.html#querystringunescapestr
    /// </para>
    /// </summary>
    /// <param name="value">The string to decode.</param>
    /// <param name="decodeSpaces">
    /// Whether the fallback turns <c>+</c> into a space. Node's second parameter, which the exported function
    /// never sets and <c>parse</c> does — the fallback is the only place a <c>+</c> has not already been
    /// substituted.
    /// </param>
    /// <remarks>
    /// The fallback deviates from Node's on one point: Node truncates every UTF-16 code unit of the input to a
    /// single byte before percent-decoding, which mangles any non-ASCII text that got that far, while this
    /// percent-decodes the UTF-8 form and reads it back with U+FFFD for what is left invalid. It only ever
    /// runs for input <c>decodeURIComponent</c> already refused.
    /// </remarks>
    internal static string Unescape(string value, bool decodeSpaces)
    {
        if (NodeBuiltinHelpers.TryDecodeUriComponent(value, out var decoded))
        {
            return decoded;
        }

        var input = decodeSpaces ? value.Replace('+', ' ') : value;
        return PercentEncoding.DecodeToString(input);
    }
}
#endif

#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;

namespace Jint.WebApi.Encoding;

/// <summary>
/// The encodings Jint's <c>TextDecoder</c> understands and the labels that name them.
/// <para>
/// https://encoding.spec.whatwg.org/#names-and-labels
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every label the specification's table lists is here, and so is every encoding except the seven legacy
/// multi-byte ones (<c>Big5</c>, <c>EUC-JP</c>, <c>EUC-KR</c>, <c>GBK</c>, <c>gb18030</c>,
/// <c>ISO-2022-JP</c> and <c>Shift_JIS</c>). Those resolve to <see cref="EncodingKind.Unsupported"/>: the
/// label is recognized, so the failure says which encoding was asked for, but the constructor still raises
/// a <c>RangeError</c> rather than decoding as something else. That is Jint's one deviation from the
/// standard here, and it is deliberate — each of those encodings needs an index measured in tens of
/// thousands of entries.
/// </para>
/// <para>
/// The table itself, and the index tables the single-byte encodings decode through, are generated from the
/// Encoding Standard's own data files by <c>tools/whatwg-encoding/generate-encoding-tables.ps1</c>; see
/// <see cref="EncodingTables"/>.
/// </para>
/// </remarks>
internal static class EncodingLabels
{
    /// <summary>The name https://encoding.spec.whatwg.org/#utf-8 gives the encoding, already ASCII-lowercase.</summary>
    internal const string Utf8 = "utf-8";

    /// <summary>
    /// https://encoding.spec.whatwg.org/#utf-8 itself, for an algorithm that names the encoding rather than
    /// resolving a label — "set up decoder with UTF-8", https://w3c.github.io/FileAPI/#dom-blob-textstream
    /// step 3, is the one that does. Written out rather than looked up because UTF-8 is the generated
    /// table's one fixed point: it is not a single-byte encoding, so it carries no index.
    /// </summary>
    internal static readonly EncodingEntry Utf8Encoding = new(Utf8, EncodingKind.Utf8, SingleByteIndex.None);

    /// <summary>The name https://encoding.spec.whatwg.org/#utf-16le gives the encoding.</summary>
    internal const string Utf16Le = "utf-16le";

    /// <summary>The name https://encoding.spec.whatwg.org/#utf-16be gives the encoding.</summary>
    internal const string Utf16Be = "utf-16be";

    /// <summary>
    /// ASCII whitespace, https://infra.spec.whatwg.org/#ascii-whitespace — note that U+000B VT is not one
    /// of them, so a label padded with a vertical tab does not match.
    /// </summary>
    private static ReadOnlySpan<char> AsciiWhitespace => ['\t', '\n', '\f', '\r', ' '];

    /// <summary>
    /// "Get an encoding", https://encoding.spec.whatwg.org/#concept-encoding-get: strip leading and
    /// trailing ASCII whitespace, then match the rest ASCII case-insensitively against the label table.
    /// Returns <see langword="false"/> for the specification's "failure".
    /// </summary>
    /// <remarks>
    /// The match is ASCII case-insensitive and nothing more, which is why the label is lowercased here
    /// rather than handed to <see cref="StringComparison.OrdinalIgnoreCase"/>: that folds U+017F LATIN
    /// SMALL LETTER LONG S onto <c>s</c>, so <c>"cſunicode"</c> would be accepted as a label for
    /// <c>utf-16le</c>. Any non-ASCII code point in the trimmed label makes it fail here, since every
    /// label in the table is ASCII.
    /// </remarks>
    internal static bool TryLookup(string label, out EncodingEntry entry)
    {
        var trimmed = label.AsSpan().Trim(AsciiWhitespace);
        if (trimmed.Length is 0 or > EncodingTables.MaxLabelLength)
        {
            entry = default;
            return false;
        }

        Span<char> lowered = stackalloc char[EncodingTables.MaxLabelLength];
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (!char.IsAscii(c))
            {
                entry = default;
                return false;
            }

            lowered[i] = char.IsAsciiLetterUpper(c) ? (char) (c + ('a' - 'A')) : c;
        }

        return EncodingTables.TryMatch(lowered.Slice(0, trimmed.Length), out entry);
    }
}

/// <summary>
/// Which decoder an encoding uses, which is the only thing about an encoding the rest of this folder cares
/// about.
/// </summary>
internal enum EncodingKind
{
    /// <summary>https://encoding.spec.whatwg.org/#utf-8-decoder, run by the BCL.</summary>
    Utf8,

    /// <summary>https://encoding.spec.whatwg.org/#utf-16le, run by the shared UTF-16 decoder.</summary>
    Utf16Le,

    /// <summary>https://encoding.spec.whatwg.org/#utf-16be, run by the shared UTF-16 decoder.</summary>
    Utf16Be,

    /// <summary>
    /// https://encoding.spec.whatwg.org/#single-byte-decoder, run against the encoding's own index table.
    /// </summary>
    SingleByte,

    /// <summary>https://encoding.spec.whatwg.org/#x-user-defined-decoder, which is an algorithm, not a table.</summary>
    XUserDefined,

    /// <summary>
    /// https://encoding.spec.whatwg.org/#replacement. The <c>TextDecoder</c> constructor is required to
    /// refuse it, so its decoder is never reached from here.
    /// </summary>
    Replacement,

    /// <summary>
    /// One of the legacy multi-byte encodings, which Jint recognizes by label but does not decode.
    /// </summary>
    Unsupported,
}

/// <summary>
/// An encoding, as "get an encoding" returns it: the specification's name (already ASCII-lowercased, which
/// is what https://encoding.spec.whatwg.org/#dom-textdecoder-encoding reports) plus what it takes to decode.
/// </summary>
/// <param name="Name">The encoding's name, ASCII-lowercased.</param>
/// <param name="Kind">Which decoder the encoding uses.</param>
/// <param name="Index">
/// The index table a <see cref="EncodingKind.SingleByte"/> encoding decodes through, and
/// <see cref="SingleByteIndex.None"/> for every other kind. ISO-8859-8 and ISO-8859-8-I are two encodings
/// sharing one index, which is why this is not derivable from <see cref="Name"/>.
/// </param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EncodingEntry(string Name, EncodingKind Kind, SingleByteIndex Index);
#endif

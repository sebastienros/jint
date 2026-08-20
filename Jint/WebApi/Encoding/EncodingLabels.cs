#if NET8_0_OR_GREATER
namespace Jint.WebApi.Encoding;

/// <summary>
/// The encodings Jint's <c>TextDecoder</c> understands and the labels that name them.
/// <para>
/// https://encoding.spec.whatwg.org/#names-and-labels
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Three of the specification's encodings are implemented — <c>utf-8</c>, <c>utf-16le</c> and
/// <c>utf-16be</c> — with every label the table gives each of them. The legacy single-byte and
/// multi-byte encodings (<c>windows-1252</c>, <c>shift_jis</c>, <c>gbk</c>, …) are deliberately out of
/// scope: each needs its own index table, and a script reaching for one is not the case this surface
/// exists for. An unimplemented label is reported as a failure, so the constructor raises the
/// <c>RangeError</c> the specification asks for rather than silently decoding as something else.
/// </para>
/// <para>
/// The <c>replacement</c>, <c>x-user-defined</c> and <c>UTF-16</c>-BOM-sniffing behaviours that only
/// matter for those legacy encodings are out of scope with them.
/// </para>
/// </remarks>
internal static class EncodingLabels
{
    /// <summary>The name https://encoding.spec.whatwg.org/#utf-8 gives the encoding, already ASCII-lowercase.</summary>
    internal const string Utf8 = "utf-8";

    /// <summary>The name https://encoding.spec.whatwg.org/#utf-16le gives the encoding.</summary>
    internal const string Utf16Le = "utf-16le";

    /// <summary>The name https://encoding.spec.whatwg.org/#utf-16be gives the encoding.</summary>
    internal const string Utf16Be = "utf-16be";

    /// <summary>"unicode-1-1-utf-8", the longest label in the implemented subset.</summary>
    private const int MaxLabelLength = 17;

    /// <summary>
    /// ASCII whitespace, https://infra.spec.whatwg.org/#ascii-whitespace — note that U+000B VT is not one
    /// of them, so a label padded with a vertical tab does not match.
    /// </summary>
    private static ReadOnlySpan<char> AsciiWhitespace => ['\t', '\n', '\f', '\r', ' '];

    /// <summary>
    /// "Get an encoding", https://encoding.spec.whatwg.org/#concept-encoding-get: strip leading and
    /// trailing ASCII whitespace, then match the rest ASCII case-insensitively against the label table.
    /// Returns the encoding's name, or <see langword="null"/> for the specification's "failure".
    /// </summary>
    /// <remarks>
    /// The match is ASCII case-insensitive and nothing more, which is why the comparison is spelled out
    /// rather than handed to <see cref="StringComparison.OrdinalIgnoreCase"/>: that folds U+017F LATIN
    /// SMALL LETTER LONG S onto <c>s</c>, so <c>"cſunicode"</c> would be accepted as a label for
    /// <c>utf-16le</c>. Any non-ASCII code point in the trimmed label makes it fail here, since every
    /// label in the table is ASCII.
    /// </remarks>
    internal static string? Lookup(string label)
    {
        var trimmed = label.AsSpan().Trim(AsciiWhitespace);
        if (trimmed.Length is 0 or > MaxLabelLength)
        {
            return null;
        }

        Span<char> lowered = stackalloc char[MaxLabelLength];
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (!char.IsAscii(c))
            {
                return null;
            }

            lowered[i] = char.IsAsciiLetterUpper(c) ? (char) (c + ('a' - 'A')) : c;
        }

        return Match(lowered.Slice(0, trimmed.Length));
    }

    private static string? Match(ReadOnlySpan<char> label)
    {
        // https://encoding.spec.whatwg.org/#table-encoding-overrides, the UTF-8 row.
        if (label.SequenceEqual("utf-8")
            || label.SequenceEqual("utf8")
            || label.SequenceEqual("unicode-1-1-utf-8")
            || label.SequenceEqual("unicode11utf8")
            || label.SequenceEqual("unicode20utf8")
            || label.SequenceEqual("x-unicode20utf8"))
        {
            return Utf8;
        }

        // The UTF-16LE row. "utf-16" without a suffix is little-endian, which is the one label here that
        // regularly surprises people.
        if (label.SequenceEqual("utf-16le")
            || label.SequenceEqual("utf-16")
            || label.SequenceEqual("unicode")
            || label.SequenceEqual("unicodefeff")
            || label.SequenceEqual("csunicode")
            || label.SequenceEqual("iso-10646-ucs-2")
            || label.SequenceEqual("ucs-2"))
        {
            return Utf16Le;
        }

        // The UTF-16BE row.
        if (label.SequenceEqual("utf-16be") || label.SequenceEqual("unicodefffe"))
        {
            return Utf16Be;
        }

        return null;
    }
}
#endif

#if NET8_0_OR_GREATER
namespace Jint.WebApi.Encoding;

/// <summary>
/// HTML's "prescan a byte stream to determine its encoding", with "get an attribute" and "get an XML
/// encoding" beside it: the encoding a document declares for itself in its first bytes.
/// <para>
/// https://html.spec.whatwg.org/multipage/parsing.html#prescan-a-byte-stream-to-determine-its-encoding
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Engine-free and allocation-light on purpose</b>, like <see cref="EncodingLabels"/>: it reads bytes and
/// answers an <see cref="EncodingEntry"/>, so anything that decodes an HTML byte stream — the
/// <c>XMLHttpRequest</c> document response, or a browsing context's own document fetch — can ask it without an
/// engine.
/// An attribute's name and value are always one contiguous run of the input, so they are compared and
/// resolved where they lie rather than collected into strings; only a label handed to "get an encoding" is
/// materialized.
/// </para>
/// <para>
/// <b>The end condition is the first <see cref="EndCondition"/> bytes</b>, the number the encoding sniffing
/// algorithm encourages and the one https://xhr.spec.whatwg.org/#document-response names. Running out of
/// bytes, or into that end, anywhere in the scan — inside a comment, a tag or an attribute value — aborts it,
/// and the answer is then "get an XML encoding" over the same bytes. So a declaration the window cuts in two
/// is not found.
/// </para>
/// <para>
/// <b>It answers what "get an encoding" answers, decodable here or not.</b> A <c>&lt;meta charset&gt;</c>
/// naming one of the legacy multi-byte encodings returns that encoding; whether the caller can decode it is the
/// caller's question, because passing over it to the next declaration would make the scan disagree with the
/// standard about which declaration a document made.
/// </para>
/// </remarks>
internal static class HtmlEncodingPrescan
{
    /// <summary>
    /// How many bytes are prescanned — https://html.spec.whatwg.org/multipage/parsing.html#encoding-sniffing-algorithm:
    /// "User agents are encouraged to only prescan the first 1024 bytes."
    /// </summary>
    internal const int EndCondition = 1024;

    /// <summary>TAB, LF, FF, CR, SPACE or "&gt;" — what ends a tag name and an unquoted attribute value.</summary>
    private static readonly System.Buffers.SearchValues<byte> _spaceOrGreaterThan = System.Buffers.SearchValues.Create(" \t\n\f\r>"u8);

    /// <summary>ASCII whitespace or ";" — what ends an unquoted label in a <c>content</c> attribute.</summary>
    private static readonly System.Buffers.SearchValues<byte> _spaceOrSemicolon = System.Buffers.SearchValues.Create(" \t\n\f\r;"u8);

    /// <summary>
    /// Prescans <paramref name="bytes"/>, of which only the first <see cref="EndCondition"/> are read. Returns
    /// <see langword="false"/> for the algorithm's failure.
    /// </summary>
    internal static bool TryPrescan(ReadOnlySpan<byte> bytes, out EncodingEntry encoding)
    {
        var input = bytes.Length > EndCondition ? bytes.Slice(0, EndCondition) : bytes;

        // The scan either returns an encoding or is aborted — it has no other way out — and an abort answers
        // with "get an XML encoding applied to the same bytes".
        return TryScan(input, out encoding) || TryGetXmlEncoding(input, out encoding);
    }

    /// <summary>
    /// The prescan's own steps. <see langword="false"/> is an abort: the position ran past the input.
    /// </summary>
    private static bool TryScan(ReadOnlySpan<byte> input, out EncodingEntry encoding)
    {
        // Prescan for UTF-16 XML declarations: "<?x" in either endianness. Not mapped to UTF-8, unlike every
        // other answer, and the encoding name is not checked — both for historical reasons, the standard says.
        if (input is [0x3C, 0x00, 0x3F, 0x00, 0x78, 0x00, ..])
        {
            return EncodingLabels.TryLookup(EncodingLabels.Utf16Le, out encoding);
        }

        if (input is [0x00, 0x3C, 0x00, 0x3F, 0x00, 0x78, ..])
        {
            return EncodingLabels.TryLookup(EncodingLabels.Utf16Be, out encoding);
        }

        // Loop, and "next byte" at its foot.
        for (var position = 0; position < input.Length; position++)
        {
            var rest = input.Slice(position);
            if (rest.StartsWith("<!--"u8))
            {
                // The first > preceded by two - that comes after the <; the two may be the opener's own, which
                // is what makes "<!-->" a whole comment.
                var end = rest.Slice(2).IndexOf("-->"u8);
                if (end < 0)
                {
                    break;
                }

                position += 2 + end + 2;
            }
            else if (IsMetaStart(rest))
            {
                // Point at the space or slash that ended "<meta".
                position += 5;
                switch (ScanMeta(input, ref position, out encoding))
                {
                    case MetaOutcome.Found:
                        return true;
                    case MetaOutcome.OutOfBytes:
                        return false;
                }
            }
            else if (IsTagStart(rest))
            {
                // Any other tag: skip its name, then read its attributes until there are no more, so that an
                // attribute value is never mistaken for markup.
                var nameEnd = rest.IndexOfAny(_spaceOrGreaterThan);
                if (nameEnd < 0)
                {
                    break;
                }

                position += nameEnd;
                AttributeOutcome outcome;
                while ((outcome = GetAttribute(input, ref position, out _, out _)) == AttributeOutcome.Attribute)
                {
                }

                if (outcome == AttributeOutcome.OutOfBytes)
                {
                    break;
                }
            }
            else if (rest is [(byte) '<', (byte) '!' or (byte) '/' or (byte) '?', ..])
            {
                var end = rest.Slice(1).IndexOf((byte) '>');
                if (end < 0)
                {
                    break;
                }

                position += 1 + end;
            }

            // Any other byte: do nothing with it.
        }

        encoding = default;
        return false;
    }

    /// <summary>
    /// "&lt;meta" ASCII case-insensitively, followed by one of TAB, LF, FF, CR, SPACE or "/".
    /// </summary>
    private static bool IsMetaStart(ReadOnlySpan<byte> rest)
        => rest is [(byte) '<', (byte) 'm' or (byte) 'M', (byte) 'e' or (byte) 'E', (byte) 't' or (byte) 'T', (byte) 'a' or (byte) 'A', 0x09 or 0x0A or 0x0C or 0x0D or 0x20 or (byte) '/', ..];

    /// <summary>
    /// "&lt;", optionally "/", then an ASCII letter — the start of any other tag.
    /// </summary>
    private static bool IsTagStart(ReadOnlySpan<byte> rest)
        => rest is [(byte) '<', (byte) '/', var afterSlash, ..] ? IsAsciiLetter(afterSlash) : rest is [(byte) '<', var first, ..] && IsAsciiLetter(first);

    private static bool IsAsciiLetter(byte b) => (uint) ((b | 0x20) - 'a') <= 'z' - 'a';

    private static bool IsSpace(byte b) => b is 0x09 or 0x0A or 0x0C or 0x0D or 0x20;

    private enum MetaOutcome
    {
        Found,
        NextByte,
        OutOfBytes,
    }

    /// <summary>
    /// The <c>&lt;meta</c> branch, from "let attribute list be an empty list" to "return charset". Leaves
    /// <paramref name="position"/> where "next byte" moves on from.
    /// </summary>
    private static MetaOutcome ScanMeta(ReadOnlySpan<byte> input, ref int position, out EncodingEntry encoding)
    {
        // The attribute list only ever matters for the three names below, so it is three bits — which is the
        // shortcut the standard's own source suggests.
        const int HttpEquivSeen = 1, ContentSeen = 2, CharsetSeen = 4;
        var seen = 0;
        var gotPragma = false;
        bool? needPragma = null;
        var charset = CharsetState.Null;
        encoding = default;

        while (true)
        {
            var outcome = GetAttribute(input, ref position, out var name, out var value);
            if (outcome == AttributeOutcome.OutOfBytes)
            {
                return MetaOutcome.OutOfBytes;
            }

            if (outcome == AttributeOutcome.None)
            {
                break;
            }

            var nameBytes = input.Slice(name.Start, name.Length);
            var valueBytes = input.Slice(value.Start, value.Length);

            if (System.Text.Ascii.EqualsIgnoreCase(nameBytes, "http-equiv"u8))
            {
                if ((seen & HttpEquivSeen) == 0)
                {
                    seen |= HttpEquivSeen;
                    gotPragma |= System.Text.Ascii.EqualsIgnoreCase(valueBytes, "content-type"u8);
                }
            }
            else if (System.Text.Ascii.EqualsIgnoreCase(nameBytes, "content"u8))
            {
                if ((seen & ContentSeen) == 0)
                {
                    seen |= ContentSeen;

                    // "If a character encoding is returned" — failure is not one — "and if charset is still set
                    // to null".
                    if (charset == CharsetState.Null && TryExtractFromContent(valueBytes, out var fromContent))
                    {
                        encoding = fromContent;
                        charset = CharsetState.Encoding;
                        needPragma = true;
                    }
                }
            }
            else if (System.Text.Ascii.EqualsIgnoreCase(nameBytes, "charset"u8))
            {
                if ((seen & CharsetSeen) == 0)
                {
                    seen |= CharsetSeen;
                    charset = TryGetAnEncoding(valueBytes, out encoding) ? CharsetState.Encoding : CharsetState.Failure;
                    needPragma = false;
                }
            }
        }

        // Processing.
        if (needPragma is null || (needPragma == true && !gotPragma) || charset != CharsetState.Encoding)
        {
            return MetaOutcome.NextByte;
        }

        if (encoding.Kind is EncodingKind.Utf16Le or EncodingKind.Utf16Be)
        {
            encoding = EncodingLabels.Utf8Encoding;
        }
        else if (encoding.Kind == EncodingKind.XUserDefined)
        {
            EncodingLabels.TryLookup("windows-1252", out encoding);
        }

        return MetaOutcome.Found;
    }

    /// <summary>The prescan's <c>charset</c> variable: null, failure, or an encoding held beside it.</summary>
    private enum CharsetState
    {
        Null,
        Failure,
        Encoding,
    }

    private enum AttributeOutcome
    {
        Attribute,
        None,
        OutOfBytes,
    }

    /// <summary>Where an attribute's name or value lies in the input.</summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct Run(int Start, int Length);

    /// <summary>
    /// "Get an attribute", https://html.spec.whatwg.org/multipage/parsing.html#concept-get-attributes-when-sniffing.
    /// The standard lowercases A–Z as it collects; here the name and the value are left where they lie, and
    /// every comparison made on them is ASCII case-insensitive instead, which is the same test.
    /// </summary>
    private static AttributeOutcome GetAttribute(ReadOnlySpan<byte> input, ref int position, out Run name, out Run value)
    {
        name = default;
        value = default;

        // Skip whitespace and slashes; a > means there is no attribute.
        while (true)
        {
            if (position >= input.Length)
            {
                return AttributeOutcome.OutOfBytes;
            }

            var b = input[position];
            if (b == '>')
            {
                return AttributeOutcome.None;
            }

            if (!IsSpace(b) && b != '/')
            {
                break;
            }

            position++;
        }

        // The name: everything up to =, whitespace, / or > — where an = only ends a name that has begun.
        var nameStart = position;
        while (true)
        {
            if (position >= input.Length)
            {
                return AttributeOutcome.OutOfBytes;
            }

            var b = input[position];
            if (b == '=' && position > nameStart)
            {
                name = new Run(nameStart, position - nameStart);
                position++;
                return GetValue(input, ref position, ref value);
            }

            if (IsSpace(b))
            {
                name = new Run(nameStart, position - nameStart);
                break;
            }

            if (b is (byte) '/' or (byte) '>')
            {
                name = new Run(nameStart, position - nameStart);
                return AttributeOutcome.Attribute;
            }

            position++;
        }

        // Spaces: an = after them begins a value, anything else ends a valueless attribute.
        while (true)
        {
            if (position >= input.Length)
            {
                return AttributeOutcome.OutOfBytes;
            }

            if (!IsSpace(input[position]))
            {
                break;
            }

            position++;
        }

        if (input[position] != '=')
        {
            return AttributeOutcome.Attribute;
        }

        position++;
        return GetValue(input, ref position, ref value);
    }

    /// <summary>"Get an attribute"'s steps from the one labeled <i>value</i>.</summary>
    private static AttributeOutcome GetValue(ReadOnlySpan<byte> input, ref int position, ref Run value)
    {
        while (true)
        {
            if (position >= input.Length)
            {
                return AttributeOutcome.OutOfBytes;
            }

            if (!IsSpace(input[position]))
            {
                break;
            }

            position++;
        }

        var first = input[position];
        if (first is (byte) '"' or (byte) '\'')
        {
            var close = input.Slice(position + 1).IndexOf(first);
            if (close < 0)
            {
                return AttributeOutcome.OutOfBytes;
            }

            value = new Run(position + 1, close);
            position += close + 2;
            return AttributeOutcome.Attribute;
        }

        if (first == '>')
        {
            return AttributeOutcome.Attribute;
        }

        // Unquoted: up to whitespace or >, which is left for the caller.
        var start = position;
        var end = input.Slice(start).IndexOfAny(_spaceOrGreaterThan);
        if (end < 0)
        {
            return AttributeOutcome.OutOfBytes;
        }

        value = new Run(start, end);
        position = start + end;
        return AttributeOutcome.Attribute;
    }

    /// <summary>
    /// The algorithm for extracting a character encoding from a <c>meta</c> element, over a <c>content</c>
    /// attribute's value — https://html.spec.whatwg.org/multipage/urls-and-fetching.html#algorithm-for-extracting-a-character-encoding-from-a-meta-element.
    /// <see langword="false"/> is both of its other answers: "nothing", and a label "get an encoding" fails on.
    /// </summary>
    private static bool TryExtractFromContent(ReadOnlySpan<byte> s, out EncodingEntry encoding)
    {
        encoding = default;
        var position = 0;

        while (true)
        {
            // Loop: the next "charset", ASCII case-insensitively.
            var found = IndexOfIgnoreCase(s.Slice(position), "charset"u8);
            if (found < 0)
            {
                return false;
            }

            position += found + 7;
            while (position < s.Length && IsSpace(s[position]))
            {
                position++;
            }

            // Not an = : look again from that character.
            if (position < s.Length && s[position] == '=')
            {
                break;
            }
        }

        position++;
        while (position < s.Length && IsSpace(s[position]))
        {
            position++;
        }

        if (position >= s.Length)
        {
            return false;
        }

        var first = s[position];
        if (first is (byte) '"' or (byte) '\'')
        {
            var close = s.Slice(position + 1).IndexOf(first);
            return close >= 0 && TryGetAnEncoding(s.Slice(position + 1, close), out encoding);
        }

        var rest = s.Slice(position);
        var end = rest.IndexOfAny(_spaceOrSemicolon);
        return TryGetAnEncoding(end < 0 ? rest : rest.Slice(0, end), out encoding);
    }

    /// <summary>
    /// Get an XML encoding — the prescan's answer once it has been aborted: the <c>encoding</c> pseudo-attribute
    /// of an XML declaration at the very start of the input, with UTF-16 read as UTF-8.
    /// </summary>
    private static bool TryGetXmlEncoding(ReadOnlySpan<byte> input, out EncodingEntry encoding)
    {
        encoding = default;
        if (!input.StartsWith("<?xml"u8))
        {
            return false;
        }

        var declarationEnd = input.IndexOf((byte) '>');
        if (declarationEnd < 0)
        {
            return false;
        }

        var found = input.Slice(0, declarationEnd).IndexOf("encoding"u8);
        if (found < 0)
        {
            return false;
        }

        var position = found + 8;
        while (position < input.Length && input[position] <= 0x20)
        {
            position++;
        }

        if (position >= input.Length || input[position] != '=')
        {
            return false;
        }

        position++;
        while (position < input.Length && input[position] <= 0x20)
        {
            position++;
        }

        if (position >= input.Length || input[position] is not ((byte) '"' or (byte) '\''))
        {
            return false;
        }

        var quote = input[position];
        position++;
        var close = input.Slice(position).IndexOf(quote);
        if (close < 0)
        {
            return false;
        }

        var potential = input.Slice(position, close);
        if (potential.IndexOfAnyInRange((byte) 0x00, (byte) 0x20) >= 0 || !TryGetAnEncoding(potential, out encoding))
        {
            return false;
        }

        if (encoding.Kind is EncodingKind.Utf16Le or EncodingKind.Utf16Be)
        {
            encoding = EncodingLabels.Utf8Encoding;
        }

        return true;
    }

    /// <summary>
    /// "Get an encoding" over a run of bytes, each read as the code point of the same value — isomorphic decode,
    /// and what "get an attribute" does with a byte above ASCII. Such a label never matches, since every label is
    /// ASCII.
    /// </summary>
    private static bool TryGetAnEncoding(ReadOnlySpan<byte> label, out EncodingEntry encoding)
        => EncodingLabels.TryLookup(System.Text.Encoding.Latin1.GetString(label), out encoding);

    private static int IndexOfIgnoreCase(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> lowercaseNeedle)
    {
        for (var i = 0; i + lowercaseNeedle.Length <= haystack.Length; i++)
        {
            if (System.Text.Ascii.EqualsIgnoreCase(haystack.Slice(i, lowercaseNeedle.Length), lowercaseNeedle))
            {
                return i;
            }
        }

        return -1;
    }
}
#endif

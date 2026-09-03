namespace Jint.Browser.CustomElements;

/// <summary>
/// <a href="https://html.spec.whatwg.org/multipage/custom-elements.html#valid-custom-element-name">A valid
/// custom element name</a>, and the eight names SVG and MathML reserve.
/// </summary>
/// <remarks>
/// <para>
/// The grammar is <c>[a-z] PCENChar*</c> with at least one <c>-</c> in it, and it is written out here rather
/// than as a regular expression because it is asked twice on every element AngleSharp could not identify:
/// once by <see cref="Dom.DomManualInterfaces"/>, which is what makes an undefined <c>&lt;my-el&gt;</c> an
/// <c>HTMLElement</c> rather than an <c>HTMLUnknownElement</c>, and once by <c>define</c>. A character scan
/// with no allocation is what that first caller needs.
/// </para>
/// <para>
/// The astral half of <c>PCENChar</c> (<c>[#x10000-#xEFFFF]</c>) is checked as a surrogate pair, so a lone
/// surrogate is not a valid name character — which is the answer the grammar gives, since it names code
/// points rather than code units.
/// </para>
/// </remarks>
internal static class CustomElementNames
{
    /// <summary>
    /// The names SVG and MathML already use with a hyphen in them, which the specification reserves so that a
    /// page cannot redefine one.
    /// </summary>
    private static readonly string[] _reserved =
    [
        "annotation-xml",
        "color-profile",
        "font-face",
        "font-face-src",
        "font-face-uri",
        "font-face-format",
        "font-face-name",
        "missing-glyph",
    ];

    /// <summary>Whether <paramref name="name"/> is a valid custom element name.</summary>
    internal static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name![0] < 'a' || name[0] > 'z')
        {
            return false;
        }

        var hyphen = false;

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];

            if (c == '-')
            {
                hyphen = true;
                continue;
            }

            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= name.Length || !char.IsLowSurrogate(name[i + 1]) || char.ConvertToUtf32(c, name[i + 1]) > 0xEFFFF)
                {
                    return false;
                }

                i++;
                continue;
            }

            if (!IsNameChar(c))
            {
                return false;
            }
        }

        if (!hyphen)
        {
            return false;
        }

        foreach (var reserved in _reserved)
        {
            if (string.Equals(name, reserved, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// One <c>PCENChar</c> below the astral plane. The ranges are the grammar's, in the grammar's order.
    /// </summary>
    private static bool IsNameChar(char c)
        => c == '.'
        || c == '_'
        || (c >= '0' && c <= '9')
        || (c >= 'a' && c <= 'z')
        || c == '\u00B7'
        || (c >= '\u00C0' && c <= '\u00D6')
        || (c >= '\u00D8' && c <= '\u00F6')
        || (c >= '\u00F8' && c <= '\u037D')
        || (c >= '\u037F' && c <= '\u1FFF')
        || (c >= '\u200C' && c <= '\u200D')
        || (c >= '\u203F' && c <= '\u2040')
        || (c >= '\u2070' && c <= '\u218F')
        || (c >= '\u2C00' && c <= '\u2FEF')
        || (c >= '\u3001' && c <= '\uD7FF')
        || (c >= '\uF900' && c <= '\uFDCF')
        || (c >= '\uFDF0' && c <= '\uFFFD');
}

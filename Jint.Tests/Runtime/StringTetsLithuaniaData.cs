namespace Jint.Tests.Runtime;

public class StringTetsLithuaniaData
{
    // Contains the non-uppercased string that will be processed by the engine and the expected result.
    private readonly TheoryData<string, string> fullSetOfData = new TheoryData<string, string>();
    // From: https://github.com/tc39/test262/blob/main/test/intl402/String/prototype/toLocaleUpperCase/special_casing_Lithuanian.js
    private readonly string[] softDotted = [
        "\u0069", "\u006A",   // LATIN SMALL LETTER I..LATIN SMALL LETTER J
        "\u012F",             // LATIN SMALL LETTER I WITH OGONEK
        "\u0249",             // LATIN SMALL LETTER J WITH STROKE
        "\u0268",             // LATIN SMALL LETTER I WITH STROKE
        "\u029D",             // LATIN SMALL LETTER J WITH CROSSED-TAIL
        "\u02B2",             // MODIFIER LETTER SMALL J
        "\u03F3",             // GREEK LETTER YOT
        "\u0456",             // CYRILLIC SMALL LETTER BYELORUSSIAN-UKRAINIAN I
        "\u0458",             // CYRILLIC SMALL LETTER JE
        "\u1D62",             // LATIN SUBSCRIPT SMALL LETTER I
        "\u1D96",             // LATIN SMALL LETTER I WITH RETROFLEX HOOK
        "\u1DA4",             // MODIFIER LETTER SMALL I WITH STROKE
        "\u1DA8",             // MODIFIER LETTER SMALL J WITH CROSSED-TAIL
        "\u1E2D",             // LATIN SMALL LETTER I WITH TILDE BELOW
        "\u1ECB",             // LATIN SMALL LETTER I WITH DOT BELOW
        "\u2071",             // SUPERSCRIPT LATIN SMALL LETTER I
        "\u2148", "\u2149",   // DOUBLE-STRUCK ITALIC SMALL I..DOUBLE-STRUCK ITALIC SMALL J
        "\u2C7C",             // LATIN SUBSCRIPT SMALL LETTER J
        "\uD835\uDC22", "\uD835\uDC23",   // MATHEMATICAL BOLD SMALL I..MATHEMATICAL BOLD SMALL J
        "\uD835\uDC56", "\uD835\uDC57",   // MATHEMATICAL ITALIC SMALL I..MATHEMATICAL ITALIC SMALL J
        "\uD835\uDC8A", "\uD835\uDC8B",   // MATHEMATICAL BOLD ITALIC SMALL I..MATHEMATICAL BOLD ITALIC SMALL J
        "\uD835\uDCBE", "\uD835\uDCBF",   // MATHEMATICAL SCRIPT SMALL I..MATHEMATICAL SCRIPT SMALL J
        "\uD835\uDCF2", "\uD835\uDCF3",   // MATHEMATICAL BOLD SCRIPT SMALL I..MATHEMATICAL BOLD SCRIPT SMALL J
        "\uD835\uDD26", "\uD835\uDD27",   // MATHEMATICAL FRAKTUR SMALL I..MATHEMATICAL FRAKTUR SMALL J
        "\uD835\uDD5A", "\uD835\uDD5B",   // MATHEMATICAL DOUBLE-STRUCK SMALL I..MATHEMATICAL DOUBLE-STRUCK SMALL J
        "\uD835\uDD8E", "\uD835\uDD8F",   // MATHEMATICAL BOLD FRAKTUR SMALL I..MATHEMATICAL BOLD FRAKTUR SMALL J
        "\uD835\uDDC2", "\uD835\uDDC3",   // MATHEMATICAL SANS-SERIF SMALL I..MATHEMATICAL SANS-SERIF SMALL J
        "\uD835\uDDF6", "\uD835\uDDF7",   // MATHEMATICAL SANS-SERIF BOLD SMALL I..MATHEMATICAL SANS-SERIF BOLD SMALL J
        "\uD835\uDE2A", "\uD835\uDE2B",   // MATHEMATICAL SANS-SERIF ITALIC SMALL I..MATHEMATICAL SANS-SERIF ITALIC SMALL J
        "\uD835\uDE5E", "\uD835\uDE5F",   // MATHEMATICAL SANS-SERIF BOLD ITALIC SMALL I..MATHEMATICAL SANS-SERIF BOLD ITALIC SMALL J
        "\uD835\uDE92", "\uD835\uDE93",   // MATHEMATICAL MONOSPACE SMALL I..MATHEMATICAL MONOSPACE SMALL J
    ];

    // Results obtained from node -v 18.12.0.
    private readonly string[] softDottedUpperCased = [
        "I", "J", "Į", "Ɉ", "Ɨ", "Ʝ", "ʲ", "Ϳ", "І", "Ј",
        "ᵢ", "ᶖ", "ᶤ", "ᶨ", "Ḭ", "Ị", "ⁱ", "ⅈ", "ⅉ", "ⱼ",
        "𝐢", "𝐣", "𝑖", "𝑗", "𝒊", "𝒋", "𝒾", "𝒿", "𝓲", "𝓳",
        "𝔦", "𝔧", "𝕚", "𝕛", "𝖎", "𝖏", "𝗂", "𝗃", "𝗶", "𝗷",
        "𝘪", "𝘫", "𝙞", "𝙟", "𝚒", "𝚓",
    ];

    /// <summary>
    /// Creates and adds the data to <fullSetOfData> that will be used for the tests. Six cases:
    /// 1.- String with character at the beginning of the string.
    /// 2.- String with double character at the beginning of the string.
    /// 3.- String with character at the middle of the string.
    /// 4.- String with double character at the middle of the string.
    /// 5.- String with character at the end of the string.
    /// 6.- String with double character at the end of the string.
    /// </summary>
    private void AddStringsForChars(string nonCapChar, string toUpperChar)
    {
        fullSetOfData.Add($"{nonCapChar}lorem ipsum", $"{toUpperChar}LOREM IPSUM");
        fullSetOfData.Add($"{nonCapChar}{nonCapChar}lorem ipsum", $"{toUpperChar}{toUpperChar}LOREM IPSUM");
        fullSetOfData.Add($"lorem{nonCapChar}ipsum", $"LOREM{toUpperChar}IPSUM");
        fullSetOfData.Add($"lorem{nonCapChar}{nonCapChar}ipsum", $"LOREM{toUpperChar}{toUpperChar}IPSUM");
        fullSetOfData.Add($"lorem ipsum{nonCapChar}", $"LOREM IPSUM{toUpperChar}");
        fullSetOfData.Add($"lorem ipsum{nonCapChar}{nonCapChar}", $"LOREM IPSUM{toUpperChar}{toUpperChar}");
    }

    // All the cases from https://github.com/tc39/test262/blob/main/test/intl402/String/prototype/toLocaleUpperCase/special_casing_Lithuanian.js
    public TheoryData<string, string> TestData()
    {
        // COMBINING DOT ABOVE (U+0307) not removed when uppercasing capital I
        AddStringsForChars("İ", "İ");
        // COMBINING DOT ABOVE (U+0307) not removed when uppercasing capital J
        AddStringsForChars("J̇", "J̇");
        for (int i = 0; i < softDotted.Length; i++)
        {
            // COMBINING DOT ABOVE (U+0307) removed when preceded by Soft_Dotted.
            // Character directly preceded by Soft_Dotted.
            AddStringsForChars(softDotted[i] + "\u0307", softDottedUpperCased[i]);

            // COMBINING DOT ABOVE (U+0307) removed if preceded by Soft_Dotted.
            // Character not directly preceded by Soft_Dotted.
            // - COMBINING DOT BELOW (U+0323), combining class 220 (Below)
            AddStringsForChars(softDotted[i] + "\u0323\u0307", softDottedUpperCased[i] + "\u0323");

            // COMBINING DOT ABOVE removed if preceded by Soft_Dotted.
            // Character not directly preceded by Soft_Dotted.
            // - PHAISTOS DISC SIGN COMBINING OBLIQUE STROKE (U+101FD = D800 DDFD), combining class 220 (Below)
            AddStringsForChars(softDotted[i] + "\uD800\uDDFD\u0307", softDottedUpperCased[i] + "\uD800\uDDFD");
        }

        return fullSetOfData;
    }
}
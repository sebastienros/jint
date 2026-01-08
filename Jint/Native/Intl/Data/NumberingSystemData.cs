namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides digit mappings for numbering systems.
/// Based on https://tc39.es/ecma402/#table-numbering-system-digits
/// </summary>
internal static class NumberingSystemData
{
    /// <summary>
    /// Maps numbering system names to their 10-digit strings (0-9).
    /// </summary>
    public static readonly Dictionary<string, string> Digits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["adlm"] = "\U0001E950\U0001E951\U0001E952\U0001E953\U0001E954\U0001E955\U0001E956\U0001E957\U0001E958\U0001E959",
        ["ahom"] = "\U00011730\U00011731\U00011732\U00011733\U00011734\U00011735\U00011736\U00011737\U00011738\U00011739",
        ["arab"] = "\u0660\u0661\u0662\u0663\u0664\u0665\u0666\u0667\u0668\u0669",
        ["arabext"] = "\u06F0\u06F1\u06F2\u06F3\u06F4\u06F5\u06F6\u06F7\u06F8\u06F9",
        ["bali"] = "\u1B50\u1B51\u1B52\u1B53\u1B54\u1B55\u1B56\u1B57\u1B58\u1B59",
        ["beng"] = "\u09E6\u09E7\u09E8\u09E9\u09EA\u09EB\u09EC\u09ED\u09EE\u09EF",
        ["bhks"] = "\U00011C50\U00011C51\U00011C52\U00011C53\U00011C54\U00011C55\U00011C56\U00011C57\U00011C58\U00011C59",
        ["brah"] = "\U00011066\U00011067\U00011068\U00011069\U0001106A\U0001106B\U0001106C\U0001106D\U0001106E\U0001106F",
        ["cakm"] = "\U00011136\U00011137\U00011138\U00011139\U0001113A\U0001113B\U0001113C\U0001113D\U0001113E\U0001113F",
        ["cham"] = "\uAA50\uAA51\uAA52\uAA53\uAA54\uAA55\uAA56\uAA57\uAA58\uAA59",
        ["deva"] = "\u0966\u0967\u0968\u0969\u096A\u096B\u096C\u096D\u096E\u096F",
        ["diak"] = "\U00011950\U00011951\U00011952\U00011953\U00011954\U00011955\U00011956\U00011957\U00011958\U00011959",
        ["fullwide"] = "\uFF10\uFF11\uFF12\uFF13\uFF14\uFF15\uFF16\uFF17\uFF18\uFF19",
        ["gong"] = "\U00011DA0\U00011DA1\U00011DA2\U00011DA3\U00011DA4\U00011DA5\U00011DA6\U00011DA7\U00011DA8\U00011DA9",
        ["gonm"] = "\U00011D50\U00011D51\U00011D52\U00011D53\U00011D54\U00011D55\U00011D56\U00011D57\U00011D58\U00011D59",
        ["gujr"] = "\u0AE6\u0AE7\u0AE8\u0AE9\u0AEA\u0AEB\u0AEC\u0AED\u0AEE\u0AEF",
        ["guru"] = "\u0A66\u0A67\u0A68\u0A69\u0A6A\u0A6B\u0A6C\u0A6D\u0A6E\u0A6F",
        ["hanidec"] = "\u3007\u4E00\u4E8C\u4E09\u56DB\u4E94\u516D\u4E03\u516B\u4E5D",
        ["hmng"] = "\U00016B50\U00016B51\U00016B52\U00016B53\U00016B54\U00016B55\U00016B56\U00016B57\U00016B58\U00016B59",
        ["hmnp"] = "\U0001E140\U0001E141\U0001E142\U0001E143\U0001E144\U0001E145\U0001E146\U0001E147\U0001E148\U0001E149",
        ["java"] = "\uA9D0\uA9D1\uA9D2\uA9D3\uA9D4\uA9D5\uA9D6\uA9D7\uA9D8\uA9D9",
        ["kali"] = "\uA900\uA901\uA902\uA903\uA904\uA905\uA906\uA907\uA908\uA909",
        ["kawi"] = "\U00011F50\U00011F51\U00011F52\U00011F53\U00011F54\U00011F55\U00011F56\U00011F57\U00011F58\U00011F59",
        ["khmr"] = "\u17E0\u17E1\u17E2\u17E3\u17E4\u17E5\u17E6\u17E7\u17E8\u17E9",
        ["knda"] = "\u0CE6\u0CE7\u0CE8\u0CE9\u0CEA\u0CEB\u0CEC\u0CED\u0CEE\u0CEF",
        ["lana"] = "\u1A80\u1A81\u1A82\u1A83\u1A84\u1A85\u1A86\u1A87\u1A88\u1A89",
        ["lanatham"] = "\u1A90\u1A91\u1A92\u1A93\u1A94\u1A95\u1A96\u1A97\u1A98\u1A99",
        ["laoo"] = "\u0ED0\u0ED1\u0ED2\u0ED3\u0ED4\u0ED5\u0ED6\u0ED7\u0ED8\u0ED9",
        ["latn"] = "0123456789",
        ["lepc"] = "\u1C40\u1C41\u1C42\u1C43\u1C44\u1C45\u1C46\u1C47\u1C48\u1C49",
        ["limb"] = "\u1946\u1947\u1948\u1949\u194A\u194B\u194C\u194D\u194E\u194F",
        ["mathbold"] = "\U0001D7CE\U0001D7CF\U0001D7D0\U0001D7D1\U0001D7D2\U0001D7D3\U0001D7D4\U0001D7D5\U0001D7D6\U0001D7D7",
        ["mathdbl"] = "\U0001D7D8\U0001D7D9\U0001D7DA\U0001D7DB\U0001D7DC\U0001D7DD\U0001D7DE\U0001D7DF\U0001D7E0\U0001D7E1",
        ["mathmono"] = "\U0001D7F6\U0001D7F7\U0001D7F8\U0001D7F9\U0001D7FA\U0001D7FB\U0001D7FC\U0001D7FD\U0001D7FE\U0001D7FF",
        ["mathsanb"] = "\U0001D7EC\U0001D7ED\U0001D7EE\U0001D7EF\U0001D7F0\U0001D7F1\U0001D7F2\U0001D7F3\U0001D7F4\U0001D7F5",
        ["mathsans"] = "\U0001D7E2\U0001D7E3\U0001D7E4\U0001D7E5\U0001D7E6\U0001D7E7\U0001D7E8\U0001D7E9\U0001D7EA\U0001D7EB",
        ["mlym"] = "\u0D66\u0D67\u0D68\u0D69\u0D6A\u0D6B\u0D6C\u0D6D\u0D6E\u0D6F",
        ["modi"] = "\U00011650\U00011651\U00011652\U00011653\U00011654\U00011655\U00011656\U00011657\U00011658\U00011659",
        ["mong"] = "\u1810\u1811\u1812\u1813\u1814\u1815\u1816\u1817\u1818\u1819",
        ["mroo"] = "\U00016A60\U00016A61\U00016A62\U00016A63\U00016A64\U00016A65\U00016A66\U00016A67\U00016A68\U00016A69",
        ["mtei"] = "\uABF0\uABF1\uABF2\uABF3\uABF4\uABF5\uABF6\uABF7\uABF8\uABF9",
        ["mymr"] = "\u1040\u1041\u1042\u1043\u1044\u1045\u1046\u1047\u1048\u1049",
        ["mymrshan"] = "\u1090\u1091\u1092\u1093\u1094\u1095\u1096\u1097\u1098\u1099",
        ["mymrtlng"] = "\uA9F0\uA9F1\uA9F2\uA9F3\uA9F4\uA9F5\uA9F6\uA9F7\uA9F8\uA9F9",
        ["nagm"] = "\U0001E4F0\U0001E4F1\U0001E4F2\U0001E4F3\U0001E4F4\U0001E4F5\U0001E4F6\U0001E4F7\U0001E4F8\U0001E4F9",
        ["newa"] = "\U00011450\U00011451\U00011452\U00011453\U00011454\U00011455\U00011456\U00011457\U00011458\U00011459",
        ["nkoo"] = "\u07C0\u07C1\u07C2\u07C3\u07C4\u07C5\u07C6\u07C7\u07C8\u07C9",
        ["olck"] = "\u1C50\u1C51\u1C52\u1C53\u1C54\u1C55\u1C56\u1C57\u1C58\u1C59",
        ["orya"] = "\u0B66\u0B67\u0B68\u0B69\u0B6A\u0B6B\u0B6C\u0B6D\u0B6E\u0B6F",
        ["osma"] = "\U000104A0\U000104A1\U000104A2\U000104A3\U000104A4\U000104A5\U000104A6\U000104A7\U000104A8\U000104A9",
        ["rohg"] = "\U00010D30\U00010D31\U00010D32\U00010D33\U00010D34\U00010D35\U00010D36\U00010D37\U00010D38\U00010D39",
        ["saur"] = "\uA8D0\uA8D1\uA8D2\uA8D3\uA8D4\uA8D5\uA8D6\uA8D7\uA8D8\uA8D9",
        ["segment"] = "\U0001FBF0\U0001FBF1\U0001FBF2\U0001FBF3\U0001FBF4\U0001FBF5\U0001FBF6\U0001FBF7\U0001FBF8\U0001FBF9",
        ["shrd"] = "\U000111D0\U000111D1\U000111D2\U000111D3\U000111D4\U000111D5\U000111D6\U000111D7\U000111D8\U000111D9",
        ["sind"] = "\U000112F0\U000112F1\U000112F2\U000112F3\U000112F4\U000112F5\U000112F6\U000112F7\U000112F8\U000112F9",
        ["sinh"] = "\u0DE6\u0DE7\u0DE8\u0DE9\u0DEA\u0DEB\u0DEC\u0DED\u0DEE\u0DEF",
        ["sora"] = "\U000110F0\U000110F1\U000110F2\U000110F3\U000110F4\U000110F5\U000110F6\U000110F7\U000110F8\U000110F9",
        ["sund"] = "\u1BB0\u1BB1\u1BB2\u1BB3\u1BB4\u1BB5\u1BB6\u1BB7\u1BB8\u1BB9",
        ["takr"] = "\U000116C0\U000116C1\U000116C2\U000116C3\U000116C4\U000116C5\U000116C6\U000116C7\U000116C8\U000116C9",
        ["talu"] = "\u19D0\u19D1\u19D2\u19D3\u19D4\u19D5\u19D6\u19D7\u19D8\u19D9",
        ["tamldec"] = "\u0BE6\u0BE7\u0BE8\u0BE9\u0BEA\u0BEB\u0BEC\u0BED\u0BEE\u0BEF",
        ["telu"] = "\u0C66\u0C67\u0C68\u0C69\u0C6A\u0C6B\u0C6C\u0C6D\u0C6E\u0C6F",
        ["thai"] = "\u0E50\u0E51\u0E52\u0E53\u0E54\u0E55\u0E56\u0E57\u0E58\u0E59",
        ["tibt"] = "\u0F20\u0F21\u0F22\u0F23\u0F24\u0F25\u0F26\u0F27\u0F28\u0F29",
        ["tirh"] = "\U000114D0\U000114D1\U000114D2\U000114D3\U000114D4\U000114D5\U000114D6\U000114D7\U000114D8\U000114D9",
        ["tnsa"] = "\U00016AC0\U00016AC1\U00016AC2\U00016AC3\U00016AC4\U00016AC5\U00016AC6\U00016AC7\U00016AC8\U00016AC9",
        ["vaii"] = "\uA620\uA621\uA622\uA623\uA624\uA625\uA626\uA627\uA628\uA629",
        ["wara"] = "\U000118E0\U000118E1\U000118E2\U000118E3\U000118E4\U000118E5\U000118E6\U000118E7\U000118E8\U000118E9",
        ["wcho"] = "\U0001E2F0\U0001E2F1\U0001E2F2\U0001E2F3\U0001E2F4\U0001E2F5\U0001E2F6\U0001E2F7\U0001E2F8\U0001E2F9",
    };

    /// <summary>
    /// Maps numbering systems to their decimal separators (only for those that differ from '.').
    /// Based on CLDR data.
    /// </summary>
    private static readonly Dictionary<string, char> DecimalSeparators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arab"] = '\u066B', // Arabic decimal separator ٫
        ["arabext"] = '\u066B', // Arabic decimal separator ٫
    };

    /// <summary>
    /// Gets the decimal separator for a numbering system.
    /// Returns '.' for most systems, or the localized separator for systems like Arabic.
    /// </summary>
    public static char GetDecimalSeparator(string numberingSystem)
    {
        return DecimalSeparators.TryGetValue(numberingSystem, out var separator) ? separator : '.';
    }

    /// <summary>
    /// Transliterates Latin digits (0-9) and decimal separator in the input string to the specified numbering system.
    /// </summary>
    public static string TransliterateDigits(string input, string numberingSystem)
    {
        // "latn" is the default - no transliteration needed
        if (string.Equals(numberingSystem, "latn", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        if (!Digits.TryGetValue(numberingSystem, out var targetDigits))
        {
            // Unknown numbering system - return as-is
            return input;
        }

        // Get the decimal separator for this numbering system
        var decimalSeparator = GetDecimalSeparator(numberingSystem);

        // Use a StringBuilder for efficient character replacement
        var sb = new System.Text.StringBuilder(input.Length * 2); // Extra space for surrogate pairs

        foreach (var c in input)
        {
            if (c >= '0' && c <= '9')
            {
                // Replace Latin digit with target numbering system digit
                var digitIndex = c - '0';
                // Get the digit at the appropriate index (handling surrogate pairs)
                var digitStr = GetDigitAtIndex(targetDigits, digitIndex);
                sb.Append(digitStr);
            }
            else if (c == '.' && decimalSeparator != '.')
            {
                // Replace Latin decimal separator with target numbering system's separator
                sb.Append(decimalSeparator);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the digit character(s) at the specified index in a digit string.
    /// Handles surrogate pairs for characters outside the BMP.
    /// </summary>
    private static string GetDigitAtIndex(string digits, int index)
    {
        var currentIndex = 0;
        var i = 0;

        while (i < digits.Length)
        {
            if (currentIndex == index)
            {
                // Check if this is a surrogate pair
                if (char.IsHighSurrogate(digits[i]) && i + 1 < digits.Length && char.IsLowSurrogate(digits[i + 1]))
                {
                    return digits.Substring(i, 2);
                }
                return digits[i].ToString();
            }

            // Advance past this character (handling surrogate pairs)
            if (char.IsHighSurrogate(digits[i]) && i + 1 < digits.Length && char.IsLowSurrogate(digits[i + 1]))
            {
                i += 2;
            }
            else
            {
                i += 1;
            }
            currentIndex++;
        }

        // Should not reach here if index is valid (0-9)
        return index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

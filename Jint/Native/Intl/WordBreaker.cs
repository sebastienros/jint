using System.Runtime.CompilerServices;

namespace Jint.Native.Intl;

/// <summary>
/// Word boundaries of Unicode UAX #29, rules WB1 to WB999 (https://www.unicode.org/reports/tr29/#Word_Boundary_Rules), as
/// ICU's root rules tailor them (icu4c/source/data/brkitr/rules/word.txt), which is what ECMA-402 leaves to the
/// implementation for <c>granularity: "word"</c> (https://tc39.es/ecma402/#sec-intl.segmenter.prototype.segment), and the
/// rule status ICU gives each word, which is what <c>isWordLike</c> reports (https://tc39.es/ecma402/#sec-createsegmentdataobject).
/// </summary>
/// <remarks>
/// <para>
/// The tailoring is in the classes of the generated table (tools/text-segmentation/README.md) and in two rules ICU adds:
/// a run of Han, Hiragana and Katakana characters stays together, and so does a run of Hangul syllables. ICU then hands
/// such a run, and a run of Thai, Lao, Khmer or Myanmar letters, to a dictionary that splits it into words. Jint has no
/// such dictionary, so the run stays one word.
/// </para>
/// <para>
/// One call finds the end of one word and reads every code point once. WB6, WB7b and WB12 read ahead from a punctuation
/// mark past the Extend and Format characters after it to the next character, which the next step reads again, so
/// segmenting a string stays linear.
/// </para>
/// </remarks>
internal static partial class WordBreaker
{
    /// <summary>
    /// The Word_Break property (https://www.unicode.org/reports/tr29/#Word_Break_Property_Values) as ICU's root rules see
    /// it: <see cref="ALetter"/> is ICU's <c>$ALetterPlus</c>, without the Han, kana and Hangul letters and with the
    /// Complex_Context ones, <see cref="Extend"/> has no Han character, and <see cref="HangulSyllable"/> is ICU's own.
    /// The order is the generator's, and makes the groups the rules test ranges.
    /// </summary>
    internal enum WordClass : byte
    {
        Other,
        CR,
        LF,
        Newline,
        Extend,
        Format,
        ZWJ,
        ALetter,
        HebrewLetter,
        Numeric,
        Katakana,
        ExtendNumLet,
        MidLetter,
        MidNumLet,
        SingleQuote,
        MidNum,
        DoubleQuote,
        RegionalIndicator,
        WSegSpace,
        HangulSyllable,
    }

    private const int ClassMask = 0x1F;

    /// <summary>Extended_Pictographic, which WB3c joins to a ZWJ before it.</summary>
    internal const int ExtendedPictographic = 0x20;

    /// <summary>ICU's <c>$KanaKanji</c>: Han, Hiragana, and Word_Break Katakana.</summary>
    internal const int KanaKanji = 0x40;

    /// <summary>ICU's <c>$Hiragana</c> and <c>$Ideographic</c>, whose words have the status of kana and ideographs.</summary>
    internal const int IdeographicOrHiragana = 0x80;

    /// <summary>The properties of no character: a class no rule names, and no flag.</summary>
    private const int NoCharacter = ClassMask;

    /// <summary>
    /// The position after the word that starts at <paramref name="start"/>, which is less than the length of
    /// <paramref name="input"/>, and whether the word is word-like: whether ICU's rule status for it is a number, a
    /// letter, kana or an ideograph rather than none.
    /// </summary>
    internal static int NextBreak(string input, int start, out bool isWordLike)
    {
        var position = start;
        var current = PropertiesAt(input, ref position);

        // WB3 and WB3a: a line break is a word of its own, a CR LF pair one word.
        if (IsLineBreak(current))
        {
            if (ClassOf(current) == WordClass.CR && position < input.Length && input[position] == '\n')
            {
                position++;
            }

            isWordLike = false;
            return position;
        }

        // The last character the rules see, which is not one WB4 ignores, and the one before it, both in this word. A word
        // that starts with Extend, Format or ZWJ, after the start of the text or a line break, has none until WB3c gives it one.
        var last = IsIgnored(current) ? NoCharacter : current;
        var beforeLast = NoCharacter;

        // Whether an ignored character after the last one has the status of an ideograph of its own, which only
        // U+16FE4 KHITAN SMALL SCRIPT FILLER has (ICU's "$Ideographic $ExFm*" starts at it).
        var ignoredIdeograph = IsIgnored(current) && (current & IdeographicOrHiragana) != 0;
        var regionalIndicators = ClassOf(current) == WordClass.RegionalIndicator ? 1 : 0;

        while (position < input.Length)
        {
            var next = position;
            var following = PropertiesAt(input, ref next);

            // WB3b: a line break starts a word of its own.
            if (IsLineBreak(following))
            {
                break;
            }

            // WB4: X (Extend | Format | ZWJ)* → X
            if (IsIgnored(following))
            {
                ignoredIdeograph |= (following & IdeographicOrHiragana) != 0;
                current = following;
                position = next;
                continue;
            }

            var followingClass = ClassOf(following);
            if (!JoinsAdjacent(current, following) && !Joins(last, beforeLast, followingClass, input, next, regionalIndicators))
            {
                break;
            }

            if (followingClass == WordClass.RegionalIndicator)
            {
                regionalIndicators++;
            }

            beforeLast = last;
            last = following;
            current = following;
            ignoredIdeograph = false;
            position = next;
        }

        isWordLike = ignoredIdeograph || IsWordLike(last, beforeLast, IsIgnored(current));
        return position;
    }

    /// <summary>
    /// The rules that join <paramref name="following"/> only to the code point directly before it,
    /// <paramref name="previous"/>, with no Extend or Format character between them: WB3c, WB3d and ICU's two pair
    /// rules, which keep a run together for its dictionary.
    /// </summary>
    private static bool JoinsAdjacent(int previous, int following)
    {
        switch (ClassOf(previous))
        {
            case WordClass.ZWJ:
                // WB3c: ZWJ × \p{Extended_Pictographic}
                return (following & ExtendedPictographic) != 0;
            case WordClass.WSegSpace:
                // WB3d: WSegSpace × WSegSpace
                return ClassOf(following) == WordClass.WSegSpace;
            case WordClass.HangulSyllable:
                // ICU: "$HangulSyllable $HangulSyllable"
                return ClassOf(following) == WordClass.HangulSyllable;
            default:
                // ICU: "$KanaKanji $KanaKanji"
                return (previous & following & KanaKanji) != 0;
        }
    }

    /// <summary>
    /// WB5 to WB13b, WB15 and WB16 for the position before a character of class <paramref name="following"/>, whose
    /// code points end at <paramref name="afterFollowing"/>, after the character <paramref name="last"/> of the same word
    /// and the character <paramref name="beforeLast"/> before that (WB4 having set aside the Extend, Format and ZWJ
    /// characters between them). False is WB999.
    /// </summary>
    private static bool Joins(int last, int beforeLast, WordClass following, string input, int afterFollowing, int regionalIndicators)
    {
        var left = ClassOf(last);
        switch (following)
        {
            case WordClass.ALetter:
            case WordClass.HebrewLetter:
                // WB5: AHLetter × AHLetter, WB10: Numeric × AHLetter, WB13b: ExtendNumLet × AHLetter
                // WB7: AHLetter (MidLetter | MidNumLetQ) × AHLetter, WB7c: Hebrew_Letter Double_Quote × Hebrew_Letter
                return left is WordClass.ALetter or WordClass.HebrewLetter or WordClass.Numeric or WordClass.ExtendNumLet
                       || (IsMidLetterOrMidNumLetQ(left) && IsAHLetter(ClassOf(beforeLast)))
                       || (left == WordClass.DoubleQuote && following == WordClass.HebrewLetter && ClassOf(beforeLast) == WordClass.HebrewLetter);

            case WordClass.Numeric:
                // WB8: Numeric × Numeric, WB9: AHLetter × Numeric, WB13b: ExtendNumLet × Numeric
                // WB11: Numeric (MidNum | MidNumLetQ) × Numeric
                return left is WordClass.Numeric or WordClass.ALetter or WordClass.HebrewLetter or WordClass.ExtendNumLet
                       || (IsMidNumOrMidNumLetQ(left) && ClassOf(beforeLast) == WordClass.Numeric);

            case WordClass.Katakana:
                // WB13: Katakana × Katakana, WB13b: ExtendNumLet × Katakana
                return left is WordClass.Katakana or WordClass.ExtendNumLet;

            case WordClass.ExtendNumLet:
                // WB13a: (AHLetter | Numeric | Katakana | ExtendNumLet) × ExtendNumLet
                return left is WordClass.ALetter or WordClass.HebrewLetter or WordClass.Numeric or WordClass.Katakana or WordClass.ExtendNumLet;

            case WordClass.SingleQuote when left == WordClass.HebrewLetter:
                // WB7a: Hebrew_Letter × Single_Quote
                return true;

            case WordClass.MidLetter:
            case WordClass.MidNumLet:
            case WordClass.SingleQuote:
                // WB6: AHLetter × (MidLetter | MidNumLetQ) AHLetter
                if (IsAHLetter(left))
                {
                    return IsAHLetter(NextClass(input, afterFollowing));
                }

                // WB12: Numeric × (MidNum | MidNumLetQ) Numeric
                return left == WordClass.Numeric && following != WordClass.MidLetter && NextClass(input, afterFollowing) == WordClass.Numeric;

            case WordClass.MidNum:
                // WB12: Numeric × (MidNum | MidNumLetQ) Numeric
                return left == WordClass.Numeric && NextClass(input, afterFollowing) == WordClass.Numeric;

            case WordClass.DoubleQuote:
                // WB7b: Hebrew_Letter × Double_Quote Hebrew_Letter
                return left == WordClass.HebrewLetter && NextClass(input, afterFollowing) == WordClass.HebrewLetter;

            case WordClass.RegionalIndicator:
                // WB15 and WB16: an odd number of regional indicators before this one
                return left == WordClass.RegionalIndicator && (regionalIndicators & 1) == 1;

            default:
                return false;
        }
    }

    /// <summary>
    /// ICU's rule status of a word that ends with <paramref name="last"/> after <paramref name="beforeLast"/>: true for
    /// a number, a letter, kana or an ideograph. Most rules give the status of the last letter of a word and keep it
    /// over the Extend and Format characters after it; the others end at their last character, so those characters
    /// leave the word with no status.
    /// </summary>
    private static bool IsWordLike(int last, int beforeLast, bool endsWithIgnored)
    {
        // "$Numeric $ExFm* {100}", "$ALetterPlus $ExFm* {200}", "$Hebrew_Letter $ExFm* {200}",
        // "$Katakana $ExFm* {400}", "$Hiragana $ExFm* {400}", "$Ideographic $ExFm* {400}"
        var lastClass = ClassOf(last);
        if (lastClass is WordClass.Numeric or WordClass.ALetter or WordClass.HebrewLetter or WordClass.Katakana
            || (last & IdeographicOrHiragana) != 0)
        {
            return true;
        }

        if (endsWithIgnored)
        {
            return false;
        }

        return lastClass switch
        {
            // "$HangulSyllable {200}"
            WordClass.HangulSyllable => true,
            // WB7a, "$Hebrew_Letter $ExFm* $Single_Quote {200}"
            WordClass.SingleQuote => ClassOf(beforeLast) == WordClass.HebrewLetter,
            // WB13a, "(...) $ExFm* $ExtendNumLet {...}": nothing else joins an ExtendNumLet to the character before it
            WordClass.ExtendNumLet => beforeLast != NoCharacter,
            // "$KanaKanji $KanaKanji {400}", the only rule that joins a Han character that is not an ideograph
            _ => (last & beforeLast & KanaKanji) != 0,
        };
    }

    /// <summary>
    /// The class of the first character from <paramref name="index"/> on that WB4 does not ignore, for the look-ahead of
    /// WB6, WB7b and WB12; none at the end of the text.
    /// </summary>
    private static WordClass NextClass(string input, int index)
    {
        while (index < input.Length)
        {
            var properties = PropertiesAt(input, ref index);
            if (!IsIgnored(properties))
            {
                return ClassOf(properties);
            }
        }

        return (WordClass) NoCharacter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static WordClass ClassOf(int properties) => (WordClass) (properties & ClassMask);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLineBreak(int properties) => (uint) ((properties & ClassMask) - (int) WordClass.CR) <= WordClass.Newline - WordClass.CR;

    /// <summary>Extend, Format and ZWJ, which WB4 ignores.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIgnored(int properties) => (uint) ((properties & ClassMask) - (int) WordClass.Extend) <= WordClass.ZWJ - WordClass.Extend;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAHLetter(WordClass c) => c is WordClass.ALetter or WordClass.HebrewLetter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMidLetterOrMidNumLetQ(WordClass c) => c is WordClass.MidLetter or WordClass.MidNumLet or WordClass.SingleQuote;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMidNumOrMidNumLetQ(WordClass c) => c is WordClass.MidNum or WordClass.MidNumLet or WordClass.SingleQuote;

    /// <summary>
    /// The properties of the code point at <paramref name="position"/>, which moves past it: a lone surrogate is a code
    /// point of its own.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PropertiesAt(string input, ref int position)
    {
        var c = input[position];
        if (c < 0x80)
        {
            position++;
            return AsciiProperties[c];
        }

        return NonAsciiPropertiesAt(input, ref position);
    }

    private static int NonAsciiPropertiesAt(string input, ref int position)
    {
        var c = input[position];
        if (char.IsHighSurrogate(c) && position + 1 < input.Length && char.IsLowSurrogate(input[position + 1]))
        {
            var codePoint = char.ConvertToUtf32(c, input[position + 1]);
            position += 2;
            return PropertiesOfNonAscii(codePoint);
        }

        position++;
        return PropertiesOfNonAscii(c);
    }

    /// <summary>
    /// The properties of a code point: its <see cref="WordClass"/> in the low five bits and the flags above them.
    /// </summary>
    internal static int PropertiesOf(int codePoint) => codePoint < 0x80 ? AsciiProperties[codePoint] : PropertiesOfNonAscii(codePoint);

    /// <summary>
    /// A binary search of <see cref="_runs"/> for the last run that starts at or before the code point. The table is the
    /// Unicode Character Database's, not .NET's: the Unicode data of the runtime differs between the target frameworks
    /// Jint runs on, and a word must not.
    /// </summary>
    internal static int PropertiesOfNonAscii(int codePoint)
    {
        var runs = _runs;
        var low = 0;
        var high = runs.Length - 1;
        while (low < high)
        {
            var middle = (int) ((uint) (low + high + 1) >> 1);
            if ((runs[middle] >> 8) <= codePoint)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return runs[low] & 0xFF;
    }
}

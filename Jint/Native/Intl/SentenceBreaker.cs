using System.Globalization;
using System.Runtime.InteropServices;

namespace Jint.Native.Intl;

/// <summary>
/// Sentence boundaries of Unicode UAX #29, rules SB3 to SB11
/// (https://www.unicode.org/reports/tr29/#Sentence_Boundary_Rules), as ICU's root rules apply them, which is what
/// ECMA-402 leaves to the implementation for <c>granularity: "sentence"</c>
/// (https://tc39.es/ecma402/#sec-intl.segmenter.prototype.segment). No locale tailoring (ICU's sentence break suppressions for
/// abbreviations) is applied.
/// </summary>
/// <remarks>
/// One call finds the end of one sentence and reads every code point at most twice, so segmenting a string is
/// linear: the look-ahead of SB8 is the only part that reads ahead, it stops at the next letter or terminator, and
/// its answer is reused by every later position it covers.
/// </remarks>
internal static partial class SentenceBreaker
{
    internal enum SentenceClass : byte
    {
        Other,
        ParaSep,
        Extend,
        Sp,
        Lower,
        Upper,
        OLetter,
        Numeric,
        ATerm,
        SContinue,
        STerm,
        Close,
    }

    /// <summary>
    /// The position after the sentence that starts at <paramref name="start"/>, which is less than the length of <paramref name="input"/>.
    /// </summary>
    internal static int NextBreak(string input, int start)
    {
        var position = start;
        var current = ReadLogical(input, ref position);
        var previous = SentenceClass.Other;

        // The state that SB6 to SB11 share: a terminator (ATerm or STerm), then Close*, then Sp*.
        var inTerminator = false;
        var terminatorIsATerm = false;
        var terminatorStage = Stage.Terminator;
        var beforeTerminator = SentenceClass.Other;

        // The answer of the SB8 look-ahead, valid for every position up to lookaheadEnd.
        var lookaheadEnd = -1;
        var lookaheadFindsLower = false;

        while (true)
        {
            // SB4: a paragraph separator ends the sentence, SB3 has already joined a CR LF pair into one.
            if (current == SentenceClass.ParaSep)
            {
                return position;
            }

            if (current is SentenceClass.ATerm or SentenceClass.STerm)
            {
                inTerminator = true;
                terminatorIsATerm = current == SentenceClass.ATerm;
                terminatorStage = Stage.Terminator;
                beforeTerminator = previous;
            }
            else if (inTerminator)
            {
                if (current == SentenceClass.Close && terminatorStage != Stage.Spaces)
                {
                    terminatorStage = Stage.Closers;
                }
                else if (current == SentenceClass.Sp)
                {
                    terminatorStage = Stage.Spaces;
                }
                else
                {
                    inTerminator = false;
                }
            }

            // SB1 and SB2: the end of the text.
            if (position >= input.Length)
            {
                return position;
            }

            var next = position;
            var following = ReadLogical(input, ref next);

            if (inTerminator && IsBreak(input, position, following, terminatorIsATerm, terminatorStage, beforeTerminator, ref lookaheadEnd, ref lookaheadFindsLower))
            {
                return position;
            }

            previous = current;
            current = following;
            position = next;
        }
    }

    private enum Stage : byte
    {
        Terminator,
        Closers,
        Spaces,
    }

    /// <summary>
    /// SB6 to SB11 for the position before <paramref name="following"/>, which comes after a terminator and its Close* Sp*.
    /// </summary>
    private static bool IsBreak(
        string input,
        int position,
        SentenceClass following,
        bool terminatorIsATerm,
        Stage stage,
        SentenceClass beforeTerminator,
        ref int lookaheadEnd,
        ref bool lookaheadFindsLower)
    {
        if (terminatorIsATerm)
        {
            if (stage == Stage.Terminator)
            {
                // SB6: ATerm × Numeric
                if (following == SentenceClass.Numeric)
                {
                    return false;
                }

                // SB7: (Upper | Lower) ATerm × Upper
                if (following == SentenceClass.Upper && beforeTerminator is SentenceClass.Upper or SentenceClass.Lower)
                {
                    return false;
                }
            }

            // SB8: ATerm Close* Sp* × ( ¬(OLetter | Upper | Lower | ParaSep | SATerm) )* Lower
            if (position > lookaheadEnd)
            {
                lookaheadFindsLower = FindsLower(input, position, out lookaheadEnd);
            }

            if (lookaheadFindsLower)
            {
                return false;
            }
        }

        switch (following)
        {
            // SB8a: SATerm Close* Sp* × ( SContinue | SATerm )
            case SentenceClass.SContinue:
            case SentenceClass.ATerm:
            case SentenceClass.STerm:
            // SB9 and SB10: the paragraph separator that follows is part of the sentence it ends.
            case SentenceClass.ParaSep:
                return false;
            // SB9: SATerm Close* × ( Close | Sp ), SB10: SATerm Close* Sp* × Sp
            case SentenceClass.Close:
                return stage == Stage.Spaces;
            case SentenceClass.Sp:
                return false;
            // SB11: SATerm Close* Sp* ParaSep? ÷
            default:
                return true;
        }
    }

    /// <summary>
    /// Whether the code points from <paramref name="position"/> on are ( ¬(OLetter | Upper | Lower | ParaSep | SATerm) )* Lower,
    /// and where that run ends: every position up to there has the same answer.
    /// </summary>
    private static bool FindsLower(string input, int position, out int end)
    {
        var index = position;
        while (index < input.Length)
        {
            var codePoint = ReadCodePoint(input, index, out var length);
            switch (ClassOf(input, index, codePoint))
            {
                case SentenceClass.Lower:
                    end = index;
                    return true;
                case SentenceClass.OLetter:
                case SentenceClass.Upper:
                case SentenceClass.ParaSep:
                case SentenceClass.ATerm:
                case SentenceClass.STerm:
                    end = index;
                    return false;
            }

            index += length;
        }

        end = input.Length;
        return false;
    }

    /// <summary>
    /// Reads one character of SB5's view of the text: a code point and the Extend and Format characters that follow it,
    /// except after a paragraph separator, where they are ignored no longer. A CR LF pair is one character (SB3).
    /// </summary>
    private static SentenceClass ReadLogical(string input, ref int position)
    {
        var codePoint = ReadCodePoint(input, position, out var length);
        var result = ClassOf(input, position, codePoint);
        position += length;

        if (result == SentenceClass.ParaSep)
        {
            if (codePoint == '\r' && position < input.Length && input[position] == '\n')
            {
                position++;
            }

            return result;
        }

        while (position < input.Length)
        {
            var nextCodePoint = ReadCodePoint(input, position, out var nextLength);
            if (ClassOf(input, position, nextCodePoint) != SentenceClass.Extend)
            {
                break;
            }

            position += nextLength;
        }

        return result;
    }

    private static int ReadCodePoint(string input, int index, out int length)
    {
        var c = input[index];
        if (char.IsHighSurrogate(c) && index + 1 < input.Length && char.IsLowSurrogate(input[index + 1]))
        {
            length = 2;
            return char.ConvertToUtf32(c, input[index + 1]);
        }

        length = 1;
        return c;
    }

    internal static SentenceClass ClassOf(string input, int index, int codePoint)
    {
        if (codePoint < 0x80)
        {
            return ClassOfAscii((char) codePoint);
        }

        return ClassOfNonAscii(input, index, codePoint);
    }

    private static bool TryFindException(int codePoint, out SentenceClass result)
    {
        var exceptions = _exceptions;
        var low = 0;
        var high = exceptions.Length - 1;
        while (low <= high)
        {
            var middle = (int) ((uint) (low + high) >> 1);
            ref readonly var range = ref exceptions[middle];
            if (codePoint < range.Start)
            {
                high = middle - 1;
            }
            else if (codePoint > range.End)
            {
                low = middle + 1;
            }
            else
            {
                result = range.Class;
                return true;
            }
        }

        result = SentenceClass.Other;
        return false;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ClassRange(int Start, int End, SentenceClass Class);

    private static SentenceClass ClassOfAscii(char c)
    {
        switch (c)
        {
            case >= 'a' and <= 'z':
                return SentenceClass.Lower;
            case >= 'A' and <= 'Z':
                return SentenceClass.Upper;
            case >= '0' and <= '9':
                return SentenceClass.Numeric;
            case ' ':
            case '\t':
            case '\v':
            case '\f':
                return SentenceClass.Sp;
            case '\r':
            case '\n':
                return SentenceClass.ParaSep;
            case '.':
                return SentenceClass.ATerm;
            case '!':
            case '?':
                return SentenceClass.STerm;
            case ',':
            case '-':
            case ':':
            case ';':
                return SentenceClass.SContinue;
            case '"':
            case '\'':
            case '(':
            case ')':
            case '[':
            case ']':
            case '{':
            case '}':
                return SentenceClass.Close;
            default:
                return SentenceClass.Other;
        }
    }

    private static SentenceClass ClassOfNonAscii(string input, int index, int codePoint)
    {
        if (TryFindException(codePoint, out var exception))
        {
            return exception;
        }

        if (codePoint == 0x85)
        {
            return SentenceClass.ParaSep;
        }

        switch (CharUnicodeInfo.GetUnicodeCategory(input, index))
        {
            case UnicodeCategory.LowercaseLetter:
                return SentenceClass.Lower;
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
                return SentenceClass.Upper;
            case UnicodeCategory.OtherLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.LetterNumber:
                return SentenceClass.OLetter;
            case UnicodeCategory.DecimalDigitNumber:
                return SentenceClass.Numeric;
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.Format:
                return SentenceClass.Extend;
            case UnicodeCategory.SpaceSeparator:
                return SentenceClass.Sp;
            case UnicodeCategory.LineSeparator:
            case UnicodeCategory.ParagraphSeparator:
                return SentenceClass.ParaSep;
            case UnicodeCategory.OpenPunctuation:
            case UnicodeCategory.ClosePunctuation:
            case UnicodeCategory.InitialQuotePunctuation:
            case UnicodeCategory.FinalQuotePunctuation:
                return SentenceClass.Close;
            default:
                return SentenceClass.Other;
        }
    }
}

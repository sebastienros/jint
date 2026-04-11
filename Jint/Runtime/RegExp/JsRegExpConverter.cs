using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Jint.Runtime.RegExp;

/// <summary>
/// Lightweight JavaScript regex pattern to .NET Regex converter.
/// Handles common patterns by rewriting JS-specific escape sequences and anchors
/// to their .NET standard mode equivalents. Falls back (returns false) for patterns
/// that require the custom QuickJS engine (Unicode mode, named groups, etc.).
/// </summary>
internal static class JsRegExpConverter
{
    // JS whitespace characters for \s: TAB, LF, VT, FF, CR, SPACE, NBSP, and Unicode spaces
    private const string JsWhitespaceChars = "\t\n\v\f\r \u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff";

    // JS word boundary lookaround expansions
    private const string WordChars = "a-zA-Z0-9_";
    private const string WordBoundary = "(?:(?<=[" + WordChars + "])(?![" + WordChars + "])|(?<![" + WordChars + "])(?=[" + WordChars + "]))";
    private const string NonWordBoundary = "(?:(?<=[" + WordChars + "])(?=[" + WordChars + "])|(?<![" + WordChars + "])(?![" + WordChars + "]))";

    // JS line terminators for multiline ^/$
    private const string LineTerminators = "\n\r\u2028\u2029";

    /// <summary>
    /// Try to convert a JS regex pattern to a .NET Regex.
    /// Returns false if the pattern requires the custom engine.
    /// </summary>
    public static bool TryConvert(
        string pattern,
        string flags,
        TimeSpan regexTimeout,
        [NotNullWhen(true)] out Regex? regex,
        out int groupCount,
        bool compiled = false)
    {
        regex = null;
        groupCount = 0;

        // Unicode modes require the custom engine
        if (flags.Contains('u') || flags.Contains('v'))
        {
            return false;
        }

        // Parse flags
        var ignoreCase = flags.Contains('i');
        var dotAll = flags.Contains('s');
        var multiline = flags.Contains('m');

        var options = RegexOptions.CultureInvariant;
        if (ignoreCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (dotAll)
        {
            // Singleline makes . match everything including \n
            options |= RegexOptions.Singleline;
        }

        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        // Note: do NOT set RegexOptions.Multiline — we rewrite ^/$ ourselves

        var sb = new StringBuilder(pattern.Length + pattern.Length / 2);
        var groups = 0;
        var inCharClass = false;
        var lastCharClassItem = CharClassLastItem.None;
        var hasBackreference = false;
        var hasLookaround = false;
        // Track which groups are still open (not yet closed by ')').
        // Used to detect self-referencing backreferences like (abc\1).
        var maxClosedGroup = 0;
        // Stack to track nesting: true = capturing group, false = non-capturing/assertion
        var groupStack = new List<bool>(4);

        for (var i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];

            if (inCharClass)
            {
                if (!ConvertInsideCharClass(pattern, ref i, ch, sb, ref lastCharClassItem, ignoreCase))
                {
                    return false;
                }

                if (ch == ']')
                {
                    inCharClass = false;
                }

                continue;
            }

            switch (ch)
            {
                case '.':
                    if (dotAll)
                    {
                        sb.Append('.');
                    }
                    else
                    {
                        sb.Append("[^\n\r\u2028\u2029]");
                    }

                    break;

                case '^':
                    if (multiline)
                    {
                        sb.Append("(?:^|(?<=[").Append(LineTerminators).Append("]))");
                    }
                    else
                    {
                        sb.Append('^');
                    }

                    break;

                case '$':
                    if (multiline)
                    {
                        sb.Append("(?:\\z|(?=[").Append(LineTerminators).Append("]))");
                    }
                    else
                    {
                        // Standard .NET $ matches before \n at end; \z matches only at end
                        sb.Append("\\z");
                    }

                    break;

                case '[':
                    inCharClass = true;
                    lastCharClassItem = CharClassLastItem.None;
                    sb.Append('[');
                    // Handle negation
                    if (i + 1 < pattern.Length && pattern[i + 1] == '^')
                    {
                        sb.Append('^');
                        i++;
                    }
                    // Handle ] as first char in class (literal])
                    if (i + 1 < pattern.Length && pattern[i + 1] == ']')
                    {
                        sb.Append(']');
                        lastCharClassItem = CharClassLastItem.SingleChar;
                        i++;
                    }

                    break;

                case '\\':
                    if (!ConvertEscapeSequence(pattern, ref i, sb, ignoreCase, ref groups, ref hasBackreference, maxClosedGroup))
                    {
                        return false;
                    }

                    break;

                case '(':
                    if (i + 1 < pattern.Length && pattern[i + 1] == '?')
                    {
                        if (i + 2 >= pattern.Length)
                        {
                            return false;
                        }

                        var ch2 = pattern[i + 2];

                        switch (ch2)
                        {
                            // Non-capturing group (?:...) — pass through
                            case ':':
                                groupStack.Add(false);
                                sb.Append("(?");
                                i++;
                                break;

                            // Lookahead (?=...) and (?!...) — pass through but track
                            case '=' or '!':
                                hasLookaround = true;
                                groupStack.Add(false);
                                sb.Append("(?");
                                i++;
                                break;

                            // Lookbehind (?<=...) and (?<!...)
                            case '<' when i + 3 < pattern.Length && pattern[i + 3] is '=' or '!':
                                hasLookaround = true;
                                groupStack.Add(false);
                                sb.Append("(?");
                                i++;
                                break;

                            // Named groups (?<name>...) — bail (group numbering complexity)
                            case '<':
                                return false;

                            default:
                                // Scoped modifiers (?i:...), (?-m:...), or any other (?X — bail
                                return false;
                        }
                    }
                    else
                    {
                        // Capturing group
                        groups++;
                        groupStack.Add(true);
                        sb.Append('(');
                    }

                    break;

                case ')':
                    sb.Append(')');
                    if (groupStack.Count > 0)
                    {
                        var wasCapturing = groupStack[groupStack.Count - 1];
                        groupStack.RemoveAt(groupStack.Count - 1);
                        if (wasCapturing)
                        {
                            maxClosedGroup++;
                        }
                    }

                    break;

                default:
                    sb.Append(ch);
                    break;
            }
        }

        // .NET standard mode treats backreferences to uncaptured groups as failure.
        // JS treats them as matching empty. This matters when groups are defined inside
        // lookahead/lookbehind — after the assertion, those groups may be uncaptured.
        if (hasBackreference && hasLookaround)
        {
            return false;
        }

        groupCount = groups + 1; // +1 for group 0 (full match)

        try
        {
            regex = new Regex(sb.ToString(), options, regexTimeout);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Convert an escape sequence outside a character class.
    /// </summary>
    private static bool ConvertEscapeSequence(string pattern, ref int i, StringBuilder sb, bool ignoreCase, ref int groups, ref bool hasBackreference, int maxClosedGroup)
    {
        i++; // skip the backslash
        if (i >= pattern.Length)
        {
            sb.Append('\\');
            i--;
            return true;
        }

        var next = pattern[i];
        switch (next)
        {
            case 'd':
                sb.Append("[0-9]");
                break;
            case 'D':
                sb.Append("[^0-9]");
                break;
            case 'w':
                if (ignoreCase)
                {
                    return false; // case folding edge cases (U+017F, U+212A)
                }

                sb.Append("[" + WordChars + "]");
                break;
            case 'W':
                if (ignoreCase)
                {
                    return false;
                }

                sb.Append("[^" + WordChars + "]");
                break;
            case 's':
                sb.Append('[').Append(JsWhitespaceChars).Append(']');
                break;
            case 'S':
                sb.Append("[^").Append(JsWhitespaceChars).Append(']');
                break;
            case 'b':
                sb.Append(WordBoundary);
                break;
            case 'B':
                sb.Append(NonWordBoundary);
                break;
            case 'c':
                return false; // \c control escapes — Annex B divergence
            case 'k':
                // Named backreference \k<name>
                if (i + 1 < pattern.Length && pattern[i + 1] == '<')
                {
                    return false;
                }

                sb.Append("\\k");
                break;
            case 'p' or 'P':
                // Unicode property escape \p{...} / \P{...}
                if (i + 1 < pattern.Length && pattern[i + 1] == '{')
                {
                    return false;
                }

                sb.Append('\\').Append(next);
                break;
            case >= '1' and <= '9':
                // Parse the full backreference number
                var refStart = i;
                while (i + 1 < pattern.Length && pattern[i + 1] is >= '0' and <= '9')
                {
                    i++;
                }

                var refNumStr = pattern.AsSpan(refStart, i - refStart + 1);
                if (int.TryParse(refNumStr, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var refNum) && refNum > maxClosedGroup)
                {
                    // Backreference to a group that hasn't been fully closed yet
                    // (self-referencing like (abc\1)). In .NET standard mode,
                    // uncaptured backreferences fail instead of matching empty.
                    return false;
                }

                hasBackreference = true;
                sb.Append('\\');
                sb.Append(pattern, refStart, i - refStart + 1);
                break;
            default:
                // Pass through: \t, \n, \r, \f, \v, \0, \uXXXX, \xHH, \\, etc.
                sb.Append('\\').Append(next);
                break;
        }

        return true;
    }

    /// <summary>
    /// Convert a character inside a character class [...].
    /// </summary>
    private static bool ConvertInsideCharClass(string pattern, ref int i, char ch, StringBuilder sb, ref CharClassLastItem lastItem, bool ignoreCase)
    {
        switch (ch)
        {
            case ']':
                sb.Append(']');
                return true;

            case '\\':
                i++;
                if (i >= pattern.Length)
                {
                    return false; // unterminated escape
                }

                var next = pattern[i];
                switch (next)
                {
                    case 'd':
                        EmitCharClassShorthand(sb, "0-9", ref lastItem);
                        break;
                    case 'D' or 'W' or 'S':
                        return false; // Negated shorthand inside char class — bail
                    case 'w':
                        if (ignoreCase)
                        {
                            return false;
                        }

                        EmitCharClassShorthand(sb, WordChars, ref lastItem);
                        break;
                    case 's':
                        EmitCharClassShorthand(sb, JsWhitespaceChars, ref lastItem);
                        break;
                    case 'b':
                        // \b inside char class is backspace (\x08) in JS
                        sb.Append("\\x08");
                        lastItem = CharClassLastItem.SingleChar;
                        break;
                    case 'c':
                        return false; // \c control escape — bail
                    case 'p' or 'P':
                        if (i + 1 < pattern.Length && pattern[i + 1] == '{')
                        {
                            return false; // Unicode property escape
                        }

                        sb.Append('\\').Append(next);
                        lastItem = CharClassLastItem.SingleChar;
                        break;
                    default:
                        sb.Append('\\').Append(next);
                        lastItem = CharClassLastItem.SingleChar;
                        break;
                }

                break;

            case '-':
                if (lastItem == CharClassLastItem.ShorthandExpansion)
                {
                    // Dash after shorthand expansion (e.g., [\d-a]) — escape dash to avoid range
                    sb.Append("\\x2D");
                    lastItem = CharClassLastItem.SingleChar;
                }
                else
                {
                    sb.Append('-');
                    lastItem = CharClassLastItem.Dash;
                }

                break;

            default:
                sb.Append(ch);
                lastItem = CharClassLastItem.SingleChar;
                break;
        }

        return true;
    }

    /// <summary>
    /// Emit a shorthand expansion inside a character class, handling dash-before-shorthand edge case.
    /// </summary>
    private static void EmitCharClassShorthand(StringBuilder sb, string expansion, ref CharClassLastItem lastItem)
    {
        if (lastItem == CharClassLastItem.Dash)
        {
            // Previous was a dash, like [a-\d] — escape the dash we already emitted
            sb.Remove(sb.Length - 1, 1);
            sb.Append("\\x2D");
        }

        sb.Append(expansion);
        lastItem = CharClassLastItem.ShorthandExpansion;
    }

    private enum CharClassLastItem : byte
    {
        None,
        SingleChar,
        ShorthandExpansion,
        Dash,
    }
}

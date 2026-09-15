using AngleSharp.Dom;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// The three places DOM's <see href="https://dom.spec.whatwg.org/#scope-match-a-selectors-string">
/// scope-match a selectors string</see> and
/// <see href="https://drafts.csswg.org/css-syntax/">CSS Syntax</see> disagree with AngleSharp 1.8.1 about
/// what a selector <i>string</i> is, before the native parser sees it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A leading combinator.</b> Scope-match parses a complex selector list, not a <i>relative</i> one, so
/// <c>&gt;*</c> is a <c>SyntaxError</c>. AngleSharp accepts a relative selector in either context.
/// </para>
/// <para>
/// <b>An undeclared namespace prefix.</b> Scope-match parses "with the empty namespace-prefix map", so
/// <i>every</i> prefix is undeclared, and
/// <see href="https://drafts.csswg.org/selectors/#type-nmsp">Selectors §5.1</see> makes a type or attribute
/// selector naming one invalid. AngleSharp's <c>NamespaceSelector</c> resolves the prefix to <c>null</c>
/// through the public <c>ElementExtensions.MatchesCssNamespace</c> and merely fails to match, so
/// <c>ns|div</c> — and <c>:not(ns|div)</c>, and <c>[ns|attr]</c> — came back as a valid selector matching
/// nothing. The empty prefix (<c>|div</c>), the any-namespace prefix (<c>*|div</c>), the column combinator
/// (<c>a || b</c>) and the hyphen-separated attribute operator (<c>[lang|="en"]</c>) are all still handed to
/// the native parser.
/// </para>
/// <para>
/// <b>An EOF inside an open construct.</b> CSS Syntax §5.4's consume algorithms close every open block,
/// function, string and comment at EOF, so <c>#attr-value [align="center"</c> is a <b>valid</b> selector
/// that matches. AngleSharp's <c>CssSelectorConstructor</c> clears its <c>_ready</c> flag on <c>[</c> and
/// only a <c>]</c> restores it, so <c>ParseSelector</c> answers null and the caller raises a
/// <c>SyntaxError</c> instead. The walk closes those constructs itself, which is the one case where the
/// text handed to the native matcher is <b>not</b> the text the script passed — everywhere else it is
/// returned unchanged, and a selector that is already balanced allocates nothing.
/// </para>
/// </remarks>
internal static class DomSelectorText
{
    /// <summary>
    /// How deep the nesting whose closing delimiters are recorded may go, which is the width of
    /// <c>kinds</c>. Past it the text is returned untouched, so a selector nobody writes degrades to the
    /// refusal it already got rather than to a suffix that closes the wrong constructs.
    /// </summary>
    private const int MaxTrackedDepth = 64;

    internal static string Required(JsValue[] arguments, string member)
    {
        var text = DomConvert.RequiredText(arguments, 0, member);
        var depth = 0;
        var atStart = true;
        var quote = '\0';
        var comment = false;

        // True when the previous code point can end an identifier, which is what separates the undeclared
        // prefix of `ns|div` from the empty one of `|div` and the any-namespace one of `*|div`.
        var identifier = false;

        // Bit (depth - 1) records which delimiter opened that level: set for '[', clear for '('.
        var kinds = 0UL;

        // Nested functions (notably :has), attribute strings, comments and escapes belong to the native
        // parser; only the list starts and the namespace prefixes are read out of them here. This walk
        // allocates nothing for a selector whose constructs are all closed.
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i++;
                atStart = false;
                identifier = true;
                continue;
            }

            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    comment = true;
                    break;
                }
                i = end + 1;
                identifier = false;
                continue;
            }

            if (c is '\t' or '\n' or '\r' or '\f' or ' ')
            {
                identifier = false;
                continue;
            }

            // Selectors §5.1, at every depth: `:not(ns|div)` is nested and `[ns|attr]` is inside an
            // attribute selector, and both are as invalid as `ns|div` when the prefix map is empty. `||` is
            // the column combinator and `|=` the hyphen-separated attribute operator, so neither of those
            // is a prefix at all.
            if (c == '|' && identifier && (i + 1 >= text.Length || (text[i + 1] != '|' && text[i + 1] != '=')))
            {
                throw new DomException(DomError.Syntax);
            }

            if (depth == 0)
            {
                if (c == ',')
                {
                    atStart = true;
                    identifier = false;
                    continue;
                }

                if (atStart && (c is '>' or '+' or '~' || c == '|' && i + 1 < text.Length && text[i + 1] == '|'))
                {
                    throw new DomException(DomError.Syntax);
                }
                atStart = false;
            }

            if (c is '\'' or '"')
            {
                quote = c;
                identifier = false;
            }
            else if (c is '(' or '[')
            {
                if (depth < MaxTrackedDepth)
                {
                    kinds = c == '[' ? kinds | (1UL << depth) : kinds & ~(1UL << depth);
                }
                depth++;
                identifier = false;
            }
            else if (c is ')' or ']')
            {
                if (depth > 0)
                {
                    depth--;
                }
                identifier = false;
            }
            else
            {
                identifier = IsIdentifierEnd(c);
            }
        }

        if (quote == '\0' && !comment && depth == 0)
        {
            return text;
        }

        return ClosedAtEof(text, quote, comment, depth, kinds);
    }

    /// <summary>
    /// CSS Syntax §4.3's consume-a-string and consume-comments, and §5.4's consume-a-simple-block and
    /// consume-a-function: an EOF is a parse error and <i>returns</i> the construct it was reached in, so
    /// the comment, the string and every open block end there rather than invalidating the selector.
    /// </summary>
    private static string ClosedAtEof(string text, char quote, bool comment, int depth, ulong kinds)
    {
        if (depth > MaxTrackedDepth)
        {
            return text;
        }

        Span<char> tail = stackalloc char[MaxTrackedDepth + 2];
        var length = 0;
        if (comment)
        {
            tail[length++] = '*';
            tail[length++] = '/';
        }
        else if (quote != '\0')
        {
            tail[length++] = quote;
        }

        for (var level = depth - 1; level >= 0; level--)
        {
            tail[length++] = (kinds & (1UL << level)) != 0 ? ']' : ')';
        }

        return string.Concat(text.AsSpan(), tail[..length]);
    }

    /// <summary>
    /// <see href="https://drafts.csswg.org/css-syntax/#ident-code-point">An ident code point</see>: an ASCII
    /// letter or digit, <c>_</c>, <c>-</c>, or anything outside ASCII. Whether one may <i>start</i> an
    /// identifier is the native parser's decision; all this has to answer is whether a <c>|</c> follows one.
    /// </summary>
    private static bool IsIdentifierEnd(char c)
        => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' || c >= (char) 0x80;
}

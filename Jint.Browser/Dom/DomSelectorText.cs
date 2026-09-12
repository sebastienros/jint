using AngleSharp.Dom;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// DOM's https://dom.spec.whatwg.org/#scope-match-a-selectors-string parses a complex selector list,
/// not a relative selector list. AngleSharp accepts a leading combinator in either context.
/// </summary>
internal static class DomSelectorText
{
    internal static string Required(JsValue[] arguments, string member)
    {
        var text = DomConvert.RequiredText(arguments, 0, member);
        var depth = 0;
        var atStart = true;
        var quote = '\0';

        // Only inspect top-level list starts. Nested functions (notably :has), attribute strings,
        // comments and escapes belong to the native parser; none can introduce a top-level combinator.
        // This walk allocates nothing and never changes the selector passed to the native matcher.
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i++;
                atStart = false;
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
                    break;
                }
                i = end + 1;
                continue;
            }

            if (c is '\t' or '\n' or '\r' or '\f' or ' ')
            {
                continue;
            }

            if (depth == 0)
            {
                if (c == ',')
                {
                    atStart = true;
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
            }
            else if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth--;
            }
        }

        return text;
    }
}

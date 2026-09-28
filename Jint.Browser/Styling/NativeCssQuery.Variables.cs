using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    // A bounded textual convenience for automation, not the CSS Variables token-substitution engine.
    // Custom properties themselves remain declared text, including unresolved var() calls.
    private string? Substitute(Element element, string text, ref SelectorMatchWork matching, int depth = 0,
        HashSet<string>? active = null)
    {
        if (depth == 32) return null;
        active ??= new(StringComparer.Ordinal);
        var output = new StringBuilder();
        char quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            _work.Charge(1);
            if (output.Length > 1_000_000) return null;
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length)
            {
                output.Append(c).Append(text[++i]);
                continue;
            }
            if (quote != '\0')
            {
                output.Append(c);
                if (c == quote) quote = '\0';
                continue;
            }
            if (c is '\'' or '"') { quote = c; output.Append(c); continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                output.Append(c).Append(text[++i]);
                while (++i < text.Length)
                {
                    _work.Charge(1);
                    output.Append(text[i]);
                    if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    { output.Append(text[++i]); break; }
                }
                continue;
            }
            if (!text.AsSpan(i).StartsWith("var(", StringComparison.Ordinal) ||
                i > 0 && (char.IsLetterOrDigit(text[i - 1]) || text[i - 1] is '-' or '_'))
            { output.Append(c); continue; }
            var start = i + 4;
            var end = start;
            var nesting = 1;
            var comma = -1;
            char innerQuote = '\0';
            for (; end < text.Length; end++)
            {
                _work.Charge(1);
                c = text[end];
                if (c == '\\') { end++; continue; }
                if (innerQuote != '\0') { if (c == innerQuote) innerQuote = '\0'; continue; }
                if (c is '\'' or '"') { innerQuote = c; continue; }
                if (c == '/' && end + 1 < text.Length && text[end + 1] == '*')
                {
                    end += 2;
                    while (end + 1 < text.Length && !(text[end] == '*' && text[end + 1] == '/'))
                    { _work.Charge(1); end++; }
                    end++;
                    continue;
                }
                if (c == '(') nesting++;
                else if (c == ')' && --nesting == 0) break;
                else if (c == ',' && nesting == 1 && comma < 0) comma = end;
            }
            if (end >= text.Length) return null;
            var name = text[start..(comma < 0 ? end : comma)].Trim();
            if (!name.StartsWith("--", StringComparison.Ordinal) || name.Length == 2) return null;
            string? replacement = null;
            if (active.Add(name))
            {
                var value = GetProperty(element, name, ref matching);
                if (value.Text.Length != 0) replacement = Substitute(element, value.Text, ref matching, depth + 1, active);
                active.Remove(name);
            }
            if (replacement is null && comma >= 0)
                replacement = Substitute(element, text[(comma + 1)..end].Trim(), ref matching, depth + 1, active);
            if (replacement is null) return null;
            _work.Charge(replacement.Length);
            // Avoid exponential expansion even with short, depth-bounded chains.
            if (replacement.Length > 1_000_000 - output.Length) return null;
            output.Append(replacement);
            i = end;
        }
        _work.CheckCancellation();
        return output.Length > 1_000_000 ? null : output.ToString();
    }
}

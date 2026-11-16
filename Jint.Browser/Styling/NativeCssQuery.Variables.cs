using System.Text;
using Jint.HtmlParser;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.Browser.Styling;

internal sealed partial class NativeCssQuery
{
    private int _contextDependentExpansions;

    // A bounded textual convenience for automation, not the CSS Variables token-substitution engine.
    // Custom properties themselves remain declared text, including unresolved var() calls.
    private string? Substitute(Element element, SelectorPseudoElement? pseudo, string text, ref SelectorMatchWork matching, int depth = 0,
        HashSet<string>? active = null, Dictionary<string, string?>? expanded = null)
    {
        if (depth == 32)
        {
            _contextDependentExpansions++;
            return null;
        }
        active ??= new(StringComparer.Ordinal);
        expanded ??= new(StringComparer.Ordinal);
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
            var isEnv = text.AsSpan(i).StartsWith("env(", StringComparison.OrdinalIgnoreCase);
            if (!(isEnv || text.AsSpan(i).StartsWith("var(", StringComparison.Ordinal)) ||
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
            string? replacement = null;
            if (isEnv)
            {
                // CSS Environment Variables 1 §3: env() is substituted where var() is, from the user agent's values,
                // then from its fallback; an unknown name with none makes the declaration invalid at computed-value
                // time, as an undefined var() does.
                replacement = EnvironmentValue(name);
                if (replacement is null && comma >= 0)
                    replacement = Substitute(element, pseudo, text[(comma + 1)..end].Trim(), ref matching, depth + 1, active, expanded);
                if (replacement is null) return null;
                output.Append(replacement);
                i = end;
                continue;
            }
            if (!name.StartsWith("--", StringComparison.Ordinal) || name.Length == 2) return null;
            if (expanded.TryGetValue(name, out var known))
            {
                replacement = known;
            }
            else if (active.Add(name))
            {
                // A name referenced repeatedly (var(--a)var(--a)) expands once per read; without this, a chain
                // of such doublings costs 2^depth property reads before the output bound can reject it.
                var dependent = _contextDependentExpansions;
                var value = GetProperty(element, pseudo, name, ref matching);
                if (value.Text.Length != 0) replacement = Substitute(element, pseudo, value.Text, ref matching, depth + 1, active, expanded);
                active.Remove(name);
                // Only a result that met no cycle and no depth cut is independent of where it was reached.
                if (dependent == _contextDependentExpansions) expanded[name] = replacement;
            }
            else
            {
                _contextDependentExpansions++;
            }
            if (replacement is null && comma >= 0)
                replacement = Substitute(element, pseudo, text[(comma + 1)..end].Trim(), ref matching, depth + 1, active, expanded);
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

    /// <summary>
    /// The value of a user-agent environment variable (https://drafts.csswg.org/css-env-1/#safe-area-insets), or
    /// <see langword="null"/> for one this browser does not define. A headless page has a rectangular viewport with
    /// no notch, no rounded corners and no virtual keyboard, so every safe-area and keyboard inset is <c>0px</c>, as
    /// in a desktop Chromium. The title bar and viewport segment variables are not defined there either, and fall
    /// back.
    /// </summary>
    private static string? EnvironmentValue(string name)
    {
        var space = name.IndexOfAny([' ', '\t', '\n']);
        var identifier = (space < 0 ? name : name[..space]).ToLowerInvariant();
        return identifier switch
        {
            "safe-area-inset-top" or "safe-area-inset-right" or "safe-area-inset-bottom" or "safe-area-inset-left"
                or "safe-area-max-inset-top" or "safe-area-max-inset-right" or "safe-area-max-inset-bottom" or "safe-area-max-inset-left"
                or "keyboard-inset-top" or "keyboard-inset-right" or "keyboard-inset-bottom" or "keyboard-inset-left"
                or "keyboard-inset-width" or "keyboard-inset-height" => "0px",
            _ => null,
        };
    }
}

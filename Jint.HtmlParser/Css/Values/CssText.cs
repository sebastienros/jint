namespace Jint.HtmlParser.Css.Values;

// Shared bounded string operations for CSSOM names, values and query caches.
internal static class CssText
{
    internal static uint Hash(string value, CssValueWork work)
    {
        uint hash = 2166136261;
        foreach (var c in value)
        {
            work.Charge(1);
            hash = (hash ^ c) * 16777619;
        }
        return hash;
    }

    internal static bool Equals(string left, string right, CssValueWork work)
    {
        if (left.Length != right.Length) return false;
        for (var i = 0; i < left.Length; i++)
        {
            work.Charge(2);
            if (left[i] != right[i]) return false;
        }
        return true;
    }

    internal static bool IsWide(string value) =>
        value is "initial" or "inherit" or "unset" or "revert" or "revert-layer" or "revert-rule";

    internal static string[] Split(string value, CssValueWork work)
    {
        var parts = new List<string>();
        var start = -1;
        var depth = 0;
        var quote = '\0';
        var comment = false;
        for (var i = 0; i <= value.Length; i++)
        {
            work.Charge(1);
            if (i < value.Length && (depth != 0 || quote != '\0' || comment ||
                value[i] is not (' ' or '\t' or '\r' or '\n' or '\f')))
            {
                if (start < 0) start = i;
                var c = value[i];
                if (comment)
                {
                    if (c == '*' && i + 1 < value.Length && value[i + 1] == '/') { i++; comment = false; }
                }
                else if (c == '\\' && i + 1 < value.Length) { work.Charge(1); i++; }
                else if (quote != '\0') { if (c == quote) quote = '\0'; }
                else if (c is '"' or '\'') quote = c;
                else if (c == '/' && i + 1 < value.Length && value[i + 1] == '*') { i++; comment = true; }
                else if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}' && depth > 0) depth--;
            }
            else if (start >= 0)
            {
                parts.Add(value[start..i]);
                start = -1;
            }
        }
        return parts.ToArray();
    }
}

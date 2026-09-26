namespace Jint.HtmlParser.Css.Values;

internal enum CssWideKeyword
{
    None,
    Initial,
    Inherit,
    Unset,
    Revert,
    RevertLayer,
    RevertRule
}

internal static class CssWideKeywords
{
    // CSS Cascading and Inheritance Level 5, § 7.3; Editor's Draft, 2026-09-23.
    internal static CssWideKeyword Recognize(ReadOnlySpan<char> text)
    {
        if (text.Length is < 5 or > 12) return CssWideKeyword.None;
        foreach (var ch in text)
        {
            if (ch > 0x7f) return CssWideKeyword.None;
        }
        return text.Length switch
        {
            5 when text.Equals("unset", StringComparison.OrdinalIgnoreCase) => CssWideKeyword.Unset,
            6 when text.Equals("revert", StringComparison.OrdinalIgnoreCase) => CssWideKeyword.Revert,
            7 when text.Equals("initial", StringComparison.OrdinalIgnoreCase) => CssWideKeyword.Initial,
            7 when text.Equals("inherit", StringComparison.OrdinalIgnoreCase) => CssWideKeyword.Inherit,
            11 when text.Equals("revert-rule", StringComparison.OrdinalIgnoreCase) => CssWideKeyword.RevertRule,
            12 when text.Equals("revert-layer", StringComparison.OrdinalIgnoreCase) => CssWideKeyword.RevertLayer,
            _ => CssWideKeyword.None
        };
    }

    internal static string CanonicalSpelling(this CssWideKeyword keyword) => keyword switch
    {
        CssWideKeyword.Initial => "initial",
        CssWideKeyword.Inherit => "inherit",
        CssWideKeyword.Unset => "unset",
        CssWideKeyword.Revert => "revert",
        CssWideKeyword.RevertLayer => "revert-layer",
        CssWideKeyword.RevertRule => "revert-rule",
        _ => throw new InvalidOperationException("No CSS-wide keyword was selected.")
    };
}

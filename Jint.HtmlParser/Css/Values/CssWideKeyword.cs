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
        return CssWideKeywordLookup.Match(text);
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

namespace Jint.HtmlParser.Css.Values;

internal static class CssAllReset
{
    // CSS Cascading and Inheritance Level 5, § 3.1; Editor's Draft, 2026-09-23.
    internal static bool IsExcluded(string decodedPropertyName)
    {
        ArgumentNullException.ThrowIfNull(decodedPropertyName);
        return decodedPropertyName.StartsWith("--", StringComparison.Ordinal) ||
            decodedPropertyName.Equals("direction", StringComparison.OrdinalIgnoreCase) ||
            decodedPropertyName.Equals("unicode-bidi", StringComparison.OrdinalIgnoreCase);
    }
}

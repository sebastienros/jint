namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR list formatting patterns for locale-aware list formatting.
/// </summary>
internal static partial class ListPatternsData
{
    /// <summary>
    /// Gets list patterns for a locale.
    /// Returns patterns grouped by type_style (e.g., "conjunction_long", "unit_narrow").
    /// Falls back to English if locale not found.
    /// </summary>
    public static Dictionary<string, ListPatterns>? GetPatternsForLocale(string locale)
    {
        if (!string.IsNullOrEmpty(locale))
        {
            // Get language code from locale (e.g., "es" from "es-ES")
            var language = GetLanguageCode(locale);

            // Try to find patterns for this language
            if (_patterns.TryGetValue(language, out var data))
            {
                return data;
            }
        }

        // Fall back to English if not found and not already English
        if (!IsEnglish(locale))
        {
            if (_patterns.TryGetValue("en", out var enData))
            {
                return enData;
            }
        }

        return null;
    }

    private static string GetLanguageCode(string locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return "en";
        }

        // Extract language code (e.g., "es" from "es-ES")
        var dashIndex = locale.IndexOf('-');
        return dashIndex > 0 ? locale.Substring(0, dashIndex) : locale;
    }

    private static bool IsEnglish(string? locale)
    {
        if (string.IsNullOrEmpty(locale))
        {
            return false;
        }

        return locale!.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

}

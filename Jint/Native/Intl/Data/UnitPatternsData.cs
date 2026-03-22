namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR unit formatting patterns for locale-aware unit formatting.
/// </summary>
internal static class UnitPatternsData
{
    private static readonly Dictionary<string, Dictionary<string, string>> _patterns = new(5, StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(3, StringComparer.Ordinal)
        {
            ["kilometer-per-hour_short"] = "{0} km/h",
            ["kilometer-per-hour_narrow"] = "{0}km/h",
            ["kilometer-per-hour_long"] = "{0} kilometers per hour",
        },
        ["de"] = new(3, StringComparer.Ordinal)
        {
            ["kilometer-per-hour_short"] = "{0} km/h",
            ["kilometer-per-hour_narrow"] = "{0} km/h",
            ["kilometer-per-hour_long"] = "{0} Kilometer pro Stunde",
        },
        ["ja"] = new(3, StringComparer.Ordinal)
        {
            ["kilometer-per-hour_short"] = "{0} km/h",
            ["kilometer-per-hour_narrow"] = "{0}km/h",
            ["kilometer-per-hour_long"] = "時速 {0} キロメートル",
        },
        ["ko"] = new(3, StringComparer.Ordinal)
        {
            ["kilometer-per-hour_short"] = "{0}km/h",
            ["kilometer-per-hour_narrow"] = "{0}km/h",
            ["kilometer-per-hour_long"] = "시속 {0}킬로미터",
        },
        ["zh"] = new(3, StringComparer.Ordinal)
        {
            ["kilometer-per-hour_short"] = "{0} 公里/小時",
            ["kilometer-per-hour_narrow"] = "{0}公里/小時",
            ["kilometer-per-hour_long"] = "每小時 {0} 公里",
        },
    };

    /// <summary>
    /// Gets unit patterns for a locale.
    /// Returns patterns grouped by unit_displayStyle (e.g., "kilometer-per-hour_short").
    /// Falls back to English if locale not found.
    /// </summary>
    public static Dictionary<string, string>? GetPatternsForLocale(string locale)
    {
        if (!string.IsNullOrEmpty(locale))
        {
            // Get language code from locale (e.g., "zh" from "zh-TW")
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

        // Extract language code (e.g., "zh" from "zh-TW")
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

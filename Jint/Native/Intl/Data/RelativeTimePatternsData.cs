namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR relative time formatting patterns for locale-aware relative time formatting.
/// </summary>
internal static partial class RelativeTimePatternsData
{
    /// <summary>
    /// Gets relative time data for a locale.
    /// Falls back to English if locale not found.
    /// </summary>
    public static LocaleRelativeTimeData? GetDataForLocale(string locale)
    {
        if (!string.IsNullOrEmpty(locale))
        {
            // Get language code from locale (e.g., "es" from "es-ES")
            var language = GetLanguageCode(locale);

            // Try to find data for this language
            if (_data.TryGetValue(language, out var data))
            {
                return data;
            }
        }

        // Fall back to English if not found and not already English
        if (!IsEnglish(locale))
        {
            if (_data.TryGetValue("en", out var enData))
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

/// <summary>
/// Relative time data for a specific locale.
/// </summary>
internal sealed class LocaleRelativeTimeData
{
    /// <summary>
    /// Patterns organized by unit_style (e.g., "second_long", "day_short").
    /// </summary>
    public required Dictionary<string, UnitStylePatterns> Patterns { get; init; }
}

/// <summary>
/// Patterns for a specific unit and style combination.
/// </summary>
internal sealed class UnitStylePatterns
{
    /// <summary>
    /// Patterns organized by direction_pluralForm (e.g., "future_one", "past_other", "future_few").
    /// </summary>
    public required Dictionary<string, string> Patterns { get; init; }

    /// <summary>
    /// Gets the pattern for a specific direction and plural form.
    /// </summary>
    /// <param name="isPast">True for past direction, false for future.</param>
    /// <param name="pluralForm">Plural form (one, few, many, other).</param>
    /// <returns>The pattern string, or null if not found.</returns>
    public string? GetPattern(bool isPast, string pluralForm)
    {
        var key = (isPast ? "past_" : "future_") + pluralForm;
        return Patterns.TryGetValue(key, out var pattern) ? pattern : null;
    }
}

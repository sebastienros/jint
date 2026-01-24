using System.Reflection;

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR relative time formatting patterns for locale-aware relative time formatting.
/// Data is loaded from embedded RelativeTimePatternsData.txt resource.
/// </summary>
internal static class RelativeTimePatternsData
{
    private static readonly object _lock = new();
    private static Dictionary<string, LocaleRelativeTimeData>? _data;
    private static volatile bool _loaded;

    /// <summary>
    /// Gets relative time data for a locale.
    /// Falls back to English if locale not found.
    /// </summary>
    public static LocaleRelativeTimeData? GetDataForLocale(string locale)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(locale))
        {
            // Get language code from locale (e.g., "es" from "es-ES")
            var language = GetLanguageCode(locale);

            // Try to find data for this language
            if (_data!.TryGetValue(language, out var data))
            {
                return data;
            }
        }

        // Fall back to English if not found and not already English
        if (!IsEnglish(locale))
        {
            if (_data!.TryGetValue("en", out var enData))
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

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        lock (_lock)
        {
            if (_loaded)
            {
                return;
            }

            _data = new Dictionary<string, LocaleRelativeTimeData>(StringComparer.OrdinalIgnoreCase);

            var assembly = typeof(RelativeTimePatternsData).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jint.Native.Intl.Data.RelativeTimePatternsData.txt");
            if (stream is null)
            {
                _loaded = true;
                return;
            }

            using var reader = new StreamReader(stream);
            string? currentLocale = null;

            // Temporary storage for building patterns
            var tempPatterns = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    // Save previous locale data
                    if (currentLocale != null && tempPatterns.Count > 0)
                    {
                        var localeData = BuildLocaleDataFromTemp(tempPatterns);
                        _data[currentLocale] = localeData;
                        tempPatterns.Clear();
                    }

                    currentLocale = line.Substring(1, line.Length - 2);
                    continue;
                }

                if (currentLocale == null)
                {
                    continue;
                }

                var eqIndex = line.IndexOf('=');
                if (eqIndex < 0)
                {
                    continue;
                }

                var key = line.Substring(0, eqIndex);
                var value = line.Substring(eqIndex + 1);

                // Parse key: unit_style_direction_pluralForm
                // Example: "second_long_future_one" or "second_long_past_other"
                var parts = key.Split('_');
                if (parts.Length >= 4)
                {
                    var unit = parts[0]; // second, minute, hour, day, week, month, quarter, year
                    var style = parts[1]; // long, short, narrow
                    var direction = parts[2]; // future, past
                    var pluralForm = parts[3]; // one, few, many, other

                    var unitStyleKey = unit + "_" + style;
                    if (!tempPatterns.TryGetValue(unitStyleKey, out var directionPatterns))
                    {
                        directionPatterns = new Dictionary<string, string>(StringComparer.Ordinal);
                        tempPatterns[unitStyleKey] = directionPatterns;
                    }

                    var directionPluralKey = direction + "_" + pluralForm;
                    directionPatterns[directionPluralKey] = value;
                }
            }

            // Save last locale data
            if (currentLocale != null && tempPatterns.Count > 0)
            {
                var localeData = BuildLocaleDataFromTemp(tempPatterns);
                _data[currentLocale] = localeData;
            }

            _loaded = true;
        }
    }

    private static LocaleRelativeTimeData BuildLocaleDataFromTemp(Dictionary<string, Dictionary<string, string>> tempPatterns)
    {
        var patterns = new Dictionary<string, UnitStylePatterns>(StringComparer.Ordinal);

        foreach (var kvp in tempPatterns)
        {
            var unitStyleKey = kvp.Key;
            var directionPatterns = kvp.Value;

            var unitStylePatterns = new UnitStylePatterns
            {
                Patterns = new Dictionary<string, string>(directionPatterns, StringComparer.Ordinal)
            };

            patterns[unitStyleKey] = unitStylePatterns;
        }

        return new LocaleRelativeTimeData
        {
            Patterns = patterns
        };
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

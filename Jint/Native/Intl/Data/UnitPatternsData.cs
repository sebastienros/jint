using System.Threading;

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR unit formatting patterns for locale-aware unit formatting.
/// Data is loaded from embedded UnitPatternsData.txt resource.
/// </summary>
internal static class UnitPatternsData
{
    private static readonly Lock _lock = new();
    private static Dictionary<string, Dictionary<string, string>>? _patterns;
    private static volatile bool _loaded;

    /// <summary>
    /// Gets unit patterns for a locale.
    /// Returns patterns grouped by unit_displayStyle (e.g., "kilometer-per-hour_short").
    /// Falls back to English if locale not found.
    /// </summary>
    public static Dictionary<string, string>? GetPatternsForLocale(string locale)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(locale))
        {
            // Get language code from locale (e.g., "zh" from "zh-TW")
            var language = GetLanguageCode(locale);

            // Try to find patterns for this language
            if (_patterns!.TryGetValue(language, out var data))
            {
                return data;
            }
        }

        // Fall back to English if not found and not already English
        if (!IsEnglish(locale))
        {
            if (_patterns!.TryGetValue("en", out var enData))
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

            _patterns = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            var assembly = typeof(UnitPatternsData).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jint.Native.Intl.Data.UnitPatternsData.txt");
            if (stream is null)
            {
                _loaded = true;
                return;
            }

            using var reader = new StreamReader(stream);
            string? currentLocale = null;
            Dictionary<string, string>? currentPatterns = null;

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    // Save previous locale data
                    if (currentLocale != null && currentPatterns != null)
                    {
                        _patterns[currentLocale] = currentPatterns;
                    }

                    currentLocale = line.Substring(1, line.Length - 2);
                    currentPatterns = new Dictionary<string, string>(StringComparer.Ordinal);
                    continue;
                }

                if (currentLocale == null || currentPatterns == null)
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

                currentPatterns[key] = value;
            }

            // Save last locale data
            if (currentLocale != null && currentPatterns != null)
            {
                _patterns[currentLocale] = currentPatterns;
            }

            _loaded = true;
        }
    }
}

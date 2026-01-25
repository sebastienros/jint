namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR list formatting patterns for locale-aware list formatting.
/// Data is loaded from embedded ListPatternsData.txt resource.
/// </summary>
internal static class ListPatternsData
{
    private static readonly object _lock = new();
    private static Dictionary<string, Dictionary<string, ListPatterns>>? _patterns;
    private static volatile bool _loaded;

    /// <summary>
    /// Gets list patterns for a locale.
    /// Returns patterns grouped by type_style (e.g., "conjunction_long", "unit_narrow").
    /// Falls back to English if locale not found.
    /// </summary>
    public static Dictionary<string, ListPatterns>? GetPatternsForLocale(string locale)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(locale))
        {
            // Get language code from locale (e.g., "es" from "es-ES")
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

            _patterns = new Dictionary<string, Dictionary<string, ListPatterns>>(StringComparer.OrdinalIgnoreCase);

            var assembly = typeof(ListPatternsData).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jint.Native.Intl.Data.ListPatternsData.txt");
            if (stream is null)
            {
                _loaded = true;
                return;
            }

            using var reader = new StreamReader(stream);
            string? currentLocale = null;
            Dictionary<string, ListPatterns>? currentPatterns = null;

            // Temporary storage for building ListPatterns objects
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
                        currentPatterns = BuildListPatternsFromTemp(tempPatterns);
                        _patterns[currentLocale] = currentPatterns;
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

                // Parse key: type_style_position (e.g., "conjunction_long_end")
                // Store in temp dictionary for later assembly
                var parts = key.Split('_');
                if (parts.Length >= 3)
                {
                    var type = parts[0]; // conjunction, disjunction, unit
                    var style = parts[1]; // long, short, narrow
                    var position = parts[2]; // start, middle, end, two

                    var typeStyleKey = type + "_" + style;
                    if (!tempPatterns.TryGetValue(typeStyleKey, out var positionPatterns))
                    {
                        positionPatterns = new Dictionary<string, string>(StringComparer.Ordinal);
                        tempPatterns[typeStyleKey] = positionPatterns;
                    }

                    positionPatterns[position] = value;
                }
            }

            // Save last locale data
            if (currentLocale != null && tempPatterns.Count > 0)
            {
                currentPatterns = BuildListPatternsFromTemp(tempPatterns);
                _patterns[currentLocale] = currentPatterns;
            }

            _loaded = true;
        }
    }

    private static Dictionary<string, ListPatterns> BuildListPatternsFromTemp(Dictionary<string, Dictionary<string, string>> tempPatterns)
    {
        var result = new Dictionary<string, ListPatterns>(StringComparer.Ordinal);

        foreach (var kvp in tempPatterns)
        {
            var typeStyleKey = kvp.Key;
            var positionPatterns = kvp.Value;

            // Build ListPatterns object
            var listPatterns = new ListPatterns
            {
                Start = positionPatterns.TryGetValue("start", out var start) ? start : "{0}, {1}",
                Middle = positionPatterns.TryGetValue("middle", out var middle) ? middle : "{0}, {1}",
                End = positionPatterns.TryGetValue("end", out var end) ? end : "{0}, {1}",
                Two = positionPatterns.TryGetValue("two", out var two) ? two : "{0}, {1}"
            };

            result[typeStyleKey] = listPatterns;
        }

        return result;
    }
}

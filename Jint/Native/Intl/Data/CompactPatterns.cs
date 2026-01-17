using System.IO;

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR compact number patterns for locale-aware compact notation.
/// Data is loaded from embedded CompactPatterns.txt resource.
/// </summary>
internal static class CompactPatterns
{
    private static readonly object _lock = new object();
    private static Dictionary<string, LocaleCompactData>? _patterns;
    private static volatile bool _loaded;

    /// <summary>
    /// Gets compact patterns for a language code.
    /// Falls back to English if not found.
    /// </summary>
    public static LocaleCompactData GetPatterns(string? language)
    {
        EnsureLoaded();

        if (!string.IsNullOrEmpty(language))
        {
            // Try exact match first
            if (_patterns!.TryGetValue(language!, out var data))
            {
                return data;
            }

            // Handle Chinese script variants
            // zh-TW, zh-Hant-TW, zh-Hant, zh-HK, zh-MO → Traditional Chinese (zh-TW patterns)
            // zh-CN, zh-Hans-CN, zh-Hans, zh-SG → Simplified Chinese (zh patterns)
            if (language!.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                var isTraditional = language.Contains("-TW", StringComparison.OrdinalIgnoreCase) ||
                                    language.Contains("-HK", StringComparison.OrdinalIgnoreCase) ||
                                    language.Contains("-MO", StringComparison.OrdinalIgnoreCase) ||
                                    language.Contains("-Hant", StringComparison.OrdinalIgnoreCase);
                var zhKey = isTraditional ? "zh-TW" : "zh";
                if (_patterns.TryGetValue(zhKey, out data))
                {
                    return data;
                }
            }

            // Try base language (e.g., "de" from "de-DE")
            var dashIndex = language.IndexOf('-');
            if (dashIndex > 0)
            {
                var baseLang = language.Substring(0, dashIndex);
                if (_patterns.TryGetValue(baseLang, out data))
                {
                    return data;
                }
            }
        }

        // Fall back to English
        return _patterns!.TryGetValue("en", out var enData) ? enData : LocaleCompactData.Default;
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

            _patterns = new Dictionary<string, LocaleCompactData>(StringComparer.OrdinalIgnoreCase);

            var assembly = typeof(CompactPatterns).Assembly;
            using var stream = assembly.GetManifestResourceStream("Jint.Native.Intl.Data.CompactPatterns.txt");
            if (stream is null)
            {
                _loaded = true;
                return;
            }

            using var reader = new StreamReader(stream);
            string? currentLang = null;
            LocaleCompactData? currentData = null;

            while (reader.ReadLine() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    // Save previous locale data
                    if (currentLang != null && currentData != null)
                    {
                        _patterns[currentLang] = currentData;
                    }

                    currentLang = line.Substring(1, line.Length - 2);
                    currentData = new LocaleCompactData();
                    continue;
                }

                if (currentData == null)
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

                switch (key)
                {
                    case "short_thousand":
                        currentData.ShortThousand = value;
                        break;
                    case "short_million":
                        currentData.ShortMillion = value;
                        break;
                    case "short_billion":
                        currentData.ShortBillion = value;
                        break;
                    case "short_trillion":
                        currentData.ShortTrillion = value;
                        break;
                    case "long_thousand":
                        currentData.LongThousand = value;
                        break;
                    case "long_million":
                        currentData.LongMillion = value;
                        break;
                    case "long_billion":
                        currentData.LongBillion = value;
                        break;
                    case "long_trillion":
                        currentData.LongTrillion = value;
                        break;
                    case "threshold":
                        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out var threshold))
                        {
                            currentData.Threshold = threshold;
                        }
                        break;
                    case "threshold_long":
                        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out var thresholdLong))
                        {
                            currentData.ThresholdLong = thresholdLong;
                        }
                        break;
                    case "divisor_million":
                        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out var divMillion))
                        {
                            currentData.DivisorMillion = divMillion;
                        }
                        break;
                    case "divisor_billion":
                        if (long.TryParse(value, System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture, out var divBillion))
                        {
                            currentData.DivisorBillion = divBillion;
                        }
                        break;
                    case "short_space":
                        currentData.ShortSpace = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "long_space":
                        currentData.LongSpace = !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            // Save last locale data
            if (currentLang != null && currentData != null)
            {
                _patterns[currentLang] = currentData;
            }

            _loaded = true;
        }
    }

    internal sealed class LocaleCompactData
    {
        public string ShortThousand { get; set; } = "K";
        public string ShortMillion { get; set; } = "M";
        public string ShortBillion { get; set; } = "B";
        public string ShortTrillion { get; set; } = "T";
        public string LongThousand { get; set; } = "thousand";
        public string LongMillion { get; set; } = "million";
        public string LongBillion { get; set; } = "billion";
        public string LongTrillion { get; set; } = "trillion";
        public long Threshold { get; set; } = 1000;
        public long? ThresholdLong { get; set; }
        public long DivisorMillion { get; set; } = 1_000_000;
        public long DivisorBillion { get; set; } = 1_000_000_000;
        public bool ShortSpace { get; set; } = true; // Default to true (has space)
        public bool LongSpace { get; set; } = true; // Default to true (has space)

        /// <summary>
        /// Gets the threshold to use based on display mode.
        /// </summary>
        public long GetThreshold(bool isLong)
        {
            if (isLong && ThresholdLong.HasValue)
            {
                return ThresholdLong.Value;
            }
            return Threshold;
        }

        public static LocaleCompactData Default { get; } = new LocaleCompactData();
    }
}

namespace Jint.Native.Intl.Data;

/// <summary>
/// Provides CLDR compact number patterns for locale-aware compact notation.
/// </summary>
internal static partial class CompactPatterns
{
    /// <summary>
    /// Gets compact patterns for a language code.
    /// Falls back to English if not found.
    /// </summary>
    public static LocaleCompactData GetPatterns(string? language)
    {
        if (!string.IsNullOrEmpty(language))
        {
            // Try exact match first
            if (_patterns.TryGetValue(language!, out var data))
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
                if (_patterns!.TryGetValue(zhKey, out data))
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
        return _patterns.TryGetValue("en", out var enData) ? enData : LocaleCompactData.Default;
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

        /// <summary>
        /// The exponent https://tc39.es/ecma402/#sec-computeexponentformagnitude scales a number of the
        /// given <paramref name="magnitude"/> by, and the abbreviation written after the scaled number.
        /// </summary>
        /// <remarks>
        /// Zero, with no abbreviation, whenever this locale writes the number out instead: below its own
        /// threshold, and at a magnitude whose abbreviation the locale does not have — <c>de</c> has no
        /// short form for a thousand, so <c>12345</c> is <c>"12.345"</c> and not <c>"12,3 K"</c>. The
        /// magnitudes come from the divisors rather than from a fixed 3/6/9/12, because a locale can count
        /// in a different unit: <c>ja</c> abbreviates at 万 (10^4) and 億 (10^8).
        /// </remarks>
        public int CompactExponent(int magnitude, bool isLong, out string suffix)
        {
            suffix = "";

            if (magnitude < MagnitudeOf(GetThreshold(isLong)))
            {
                return 0;
            }

            int exponent;
            string abbreviation;
            if (magnitude >= 12)
            {
                exponent = 12;
                abbreviation = isLong ? LongTrillion : ShortTrillion;
            }
            else if (magnitude >= MagnitudeOf(DivisorBillion))
            {
                exponent = MagnitudeOf(DivisorBillion);
                abbreviation = isLong ? LongBillion : ShortBillion;
            }
            else if (magnitude >= MagnitudeOf(DivisorMillion))
            {
                exponent = MagnitudeOf(DivisorMillion);
                abbreviation = isLong ? LongMillion : ShortMillion;
            }
            else if (magnitude >= 3)
            {
                exponent = 3;
                abbreviation = isLong ? LongThousand : ShortThousand;
            }
            else
            {
                return 0;
            }

            if (string.IsNullOrEmpty(abbreviation))
            {
                return 0;
            }

            suffix = abbreviation;
            return exponent;
        }

        /// <summary>The power of ten a divisor is, which every one of them is.</summary>
        private static int MagnitudeOf(long powerOfTen)
        {
            var magnitude = 0;
            while (powerOfTen >= 10)
            {
                powerOfTen /= 10;
                magnitude++;
            }

            return magnitude;
        }

        public static LocaleCompactData Default { get; } = new LocaleCompactData();
    }
}

using System.Globalization;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-pluralrules-objects
/// Represents an Intl.PluralRules instance for locale-aware plural form selection.
/// </summary>
internal sealed class JsPluralRules : ObjectInstance
{
    internal JsPluralRules(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string pluralRuleType,
        string notation,
        int minimumIntegerDigits,
        int? minimumFractionDigits,
        int? maximumFractionDigits,
        int? minimumSignificantDigits,
        int? maximumSignificantDigits,
        string roundingMode,
        string roundingPriority,
        int roundingIncrement,
        string trailingZeroDisplay,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        PluralRuleType = pluralRuleType;
        Notation = notation;
        MinimumIntegerDigits = minimumIntegerDigits;
        MinimumFractionDigits = minimumFractionDigits;
        MaximumFractionDigits = maximumFractionDigits;
        MinimumSignificantDigits = minimumSignificantDigits;
        MaximumSignificantDigits = maximumSignificantDigits;
        RoundingMode = roundingMode;
        RoundingPriority = roundingPriority;
        RoundingIncrement = roundingIncrement;
        TrailingZeroDisplay = trailingZeroDisplay;
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// The locale used for plural rules.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The type of plural rules: "cardinal" or "ordinal".
    /// </summary>
    internal string PluralRuleType { get; }

    /// <summary>
    /// The notation style: "standard", "compact", "scientific", or "engineering".
    /// </summary>
    internal string Notation { get; }

    /// <summary>
    /// Minimum integer digits.
    /// </summary>
    internal int MinimumIntegerDigits { get; }

    /// <summary>
    /// Minimum fraction digits (null if significant digits are used).
    /// </summary>
    internal int? MinimumFractionDigits { get; }

    /// <summary>
    /// Maximum fraction digits (null if significant digits are used).
    /// </summary>
    internal int? MaximumFractionDigits { get; }

    /// <summary>
    /// Minimum significant digits (null if fraction digits are used).
    /// </summary>
    internal int? MinimumSignificantDigits { get; }

    /// <summary>
    /// Maximum significant digits (null if fraction digits are used).
    /// </summary>
    internal int? MaximumSignificantDigits { get; }

    /// <summary>
    /// The rounding mode.
    /// </summary>
    internal string RoundingMode { get; }

    /// <summary>
    /// The rounding priority.
    /// </summary>
    internal string RoundingPriority { get; }

    /// <summary>
    /// The rounding increment.
    /// </summary>
    internal int RoundingIncrement { get; }

    /// <summary>
    /// The trailing zero display mode.
    /// </summary>
    internal string TrailingZeroDisplay { get; }

    /// <summary>
    /// The .NET CultureInfo for the locale.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Selects the plural category for a number.
    /// Returns: "zero", "one", "two", "few", "many", or "other"
    /// </summary>
    internal string Select(double n)
    {
        if (string.Equals(PluralRuleType, "ordinal", StringComparison.Ordinal))
        {
            return SelectOrdinal(n);
        }

        return SelectCardinal(n);
    }

    /// <summary>
    /// Selects the plural category for cardinal numbers (counting).
    /// </summary>
    private string SelectCardinal(double n)
    {
        // Handle special values
        if (double.IsNaN(n) || double.IsInfinity(n))
        {
            return "other";
        }

        var absN = System.Math.Abs(n);
        var i = (long) System.Math.Floor(absN); // integer part
        var v = GetVisibleFractionDigitCount(n); // visible fraction digit count
        var f = GetFractionDigits(n, v); // visible fraction digits

        // Get language code from locale
        var lang = GetLanguageCode();

        // Simplified plural rules for common languages
        // A full implementation would use CLDR plural rules data
        return lang switch
        {
            // English, German, Dutch, etc. - "one" for 1, "other" for everything else
            "en" or "de" or "nl" or "sv" or "da" or "no" or "nb" or "nn" =>
                (i == 1 && v == 0) ? "one" : "other",

            // French - "one" for 0 and 1, "many" for multiples of 1 million with no fraction
            "fr" => SelectFrenchCardinal(i, v),

            // Portuguese, Persian - "one" for 0 and 1
            "pt" or "fa" => (i == 0 || i == 1) ? "one" : "other",

            // Spanish, Italian - "one" for 1
            "es" or "it" =>
                (i == 1 && v == 0) ? "one" : "other",

            // Manx (gv)
            "gv" => SelectManxCardinal(i, v),

            // Slovenian (sl)
            "sl" => SelectSlovenianCardinal(i, v),

            // Russian, Ukrainian, Polish - complex rules
            "ru" or "uk" => SelectSlavicCardinal(i, v),

            // Polish
            "pl" => SelectPolishCardinal(i, v),

            // Arabic - has six forms
            "ar" => SelectArabicCardinal(i),

            // Chinese, Japanese, Korean, Vietnamese - no plural forms
            "zh" or "ja" or "ko" or "vi" => "other",

            // Default to simple one/other distinction
            _ => (i == 1 && v == 0) ? "one" : "other"
        };
    }

    /// <summary>
    /// Selects the plural category for ordinal numbers (ordering: 1st, 2nd, 3rd).
    /// </summary>
    private string SelectOrdinal(double n)
    {
        if (double.IsNaN(n) || double.IsInfinity(n))
        {
            return "other";
        }

        var absN = System.Math.Abs(n);
        var i = (long) System.Math.Floor(absN);

        var lang = GetLanguageCode();

        return lang switch
        {
            // English ordinals: 1st, 2nd, 3rd, 4th, 11th, 12th, 13th, 21st, 22nd, 23rd, etc.
            "en" => SelectEnglishOrdinal(i),

            // Most other languages don't have distinct ordinal forms
            _ => "other"
        };
    }

    private static string SelectEnglishOrdinal(long n)
    {
        var mod10 = n % 10;
        var mod100 = n % 100;

        // 11th, 12th, 13th are exceptions
        if (mod100 >= 11 && mod100 <= 13)
        {
            return "other";
        }

        return mod10 switch
        {
            1 => "one",   // 1st, 21st, 31st, etc.
            2 => "two",   // 2nd, 22nd, 32nd, etc.
            3 => "few",   // 3rd, 23rd, 33rd, etc.
            _ => "other"  // 4th, 5th, ..., 11th, 12th, 13th, etc.
        };
    }

    private static string SelectFrenchCardinal(long i, int v)
    {
        // French cardinal rules (from CLDR):
        // one: i = 0,1 (integer part is 0 or 1)
        // many: e = 0 and i != 0 and i % 1000000 = 0 and v = 0 (integer is a non-zero multiple of 1 million with no fraction)
        // other: everything else
        if (i == 0 || i == 1)
        {
            return "one";
        }

        if (v == 0 && i != 0 && i % 1000000 == 0)
        {
            return "many";
        }

        return "other";
    }

    private static string SelectSlavicCardinal(long i, int v)
    {
        // Russian/Ukrainian cardinal rules
        if (v != 0)
        {
            return "other";
        }

        var mod10 = i % 10;
        var mod100 = i % 100;

        if (mod10 == 1 && mod100 != 11)
        {
            return "one";
        }

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
        {
            return "few";
        }

        return "other";
    }

    private static string SelectPolishCardinal(long i, int v)
    {
        if (v != 0)
        {
            return "other";
        }

        if (i == 1)
        {
            return "one";
        }

        var mod10 = i % 10;
        var mod100 = i % 100;

        if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14))
        {
            return "few";
        }

        return "other";
    }

    /// <summary>
    /// Manx (gv) cardinal plural rules from CLDR.
    /// one: v = 0 and i % 10 = 1
    /// two: v = 0 and i % 10 = 2
    /// few: v = 0 and i % 20 = 0
    /// many: v != 0
    /// other: everything else
    /// </summary>
    private static string SelectManxCardinal(long i, int v)
    {
        if (v != 0)
        {
            return "many";
        }

        var mod10 = i % 10;
        var mod20 = i % 20;

        if (mod10 == 1)
        {
            return "one";
        }

        if (mod10 == 2)
        {
            return "two";
        }

        if (mod20 == 0)
        {
            return "few";
        }

        return "other";
    }

    /// <summary>
    /// Slovenian (sl) cardinal plural rules from CLDR.
    /// one: v = 0 and i % 100 = 1
    /// two: v = 0 and i % 100 = 2
    /// few: v = 0 and i % 100 = 3..4 OR v != 0
    /// other: everything else
    /// </summary>
    private static string SelectSlovenianCardinal(long i, int v)
    {
        var mod100 = i % 100;

        if (v == 0 && mod100 == 1)
        {
            return "one";
        }

        if (v == 0 && mod100 == 2)
        {
            return "two";
        }

        if ((v == 0 && mod100 >= 3 && mod100 <= 4) || v != 0)
        {
            return "few";
        }

        return "other";
    }

    private static string SelectArabicCardinal(long i)
    {
        if (i == 0)
        {
            return "zero";
        }

        if (i == 1)
        {
            return "one";
        }

        if (i == 2)
        {
            return "two";
        }

        var mod100 = i % 100;
        if (mod100 >= 3 && mod100 <= 10)
        {
            return "few";
        }

        if (mod100 >= 11 && mod100 <= 99)
        {
            return "many";
        }

        return "other";
    }

    private string GetLanguageCode()
    {
        var locale = Locale;
        var dashIndex = locale.IndexOf('-');
        return dashIndex > 0 ? locale.Substring(0, dashIndex).ToLowerInvariant() : locale.ToLowerInvariant();
    }

    private static int GetVisibleFractionDigitCount(double n)
    {
        var str = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var dotIndex = str.IndexOf('.');
        if (dotIndex < 0)
        {
            return 0;
        }

        return str.Length - dotIndex - 1;
    }

    private static long GetFractionDigits(double n, int v)
    {
        if (v == 0)
        {
            return 0;
        }

        var str = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var dotIndex = str.IndexOf('.');
        if (dotIndex < 0)
        {
            return 0;
        }

        var fractionStr = str.Substring(dotIndex + 1);
        return long.TryParse(fractionStr, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    /// <summary>
    /// Returns the available plural categories for this locale.
    /// </summary>
    internal string[] GetPluralCategories()
    {
        var lang = GetLanguageCode();

        if (string.Equals(PluralRuleType, "ordinal", StringComparison.Ordinal))
        {
            return lang switch
            {
                "en" => new[] { "one", "two", "few", "other" },
                _ => new[] { "other" }
            };
        }

        // Cardinal plural categories by language
        // Based on CLDR plural rules
        return lang switch
        {
            "ar" => new[] { "zero", "one", "two", "few", "many", "other" },
            "gv" => new[] { "one", "two", "few", "many", "other" },
            "ru" or "uk" or "pl" => new[] { "one", "few", "many", "other" },
            "sl" => new[] { "one", "two", "few", "other" },
            // French and Portuguese have "many" for compact notation (large numbers)
            "fr" or "pt" => new[] { "one", "many", "other" },
            "zh" or "ja" or "ko" or "vi" => new[] { "other" },
            _ => new[] { "one", "other" }
        };
    }
}

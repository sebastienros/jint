using System.Globalization;
using System.Linq;
using Jint.Native.Intl.Data;

namespace Jint.Native.Intl;

/// <summary>
/// Default CLDR provider that uses embedded data files and hardcoded patterns.
/// Supports basic en-US/en-GB with English fallback for other locales.
/// </summary>
public sealed class DefaultCldrProvider : ICldrProvider
{
    /// <summary>
    /// Singleton instance of the default provider.
    /// </summary>
    public static readonly DefaultCldrProvider Instance = new();

    private DefaultCldrProvider()
    {
    }

    // === List Patterns ===

    public ListPatterns? GetListPatterns(string locale, string type, string style)
    {
        // Only provide English patterns in default provider
        if (!IsEnglish(locale))
        {
            return null;
        }

        // CLDR English list patterns
        if (string.Equals(type, "conjunction", StringComparison.Ordinal))
        {
            return style switch
            {
                "long" => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, and {1}", Two = "{0} and {1}" },
                "short" => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, & {1}", Two = "{0} & {1}" },
                "narrow" => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
                _ => null
            };
        }

        if (string.Equals(type, "disjunction", StringComparison.Ordinal))
        {
            return new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, or {1}", Two = "{0} or {1}" };
        }

        if (string.Equals(type, "unit", StringComparison.Ordinal))
        {
            return style switch
            {
                "long" => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
                "short" => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0}, {1}", Two = "{0}, {1}" },
                "narrow" => new ListPatterns { Start = "{0} {1}", Middle = "{0} {1}", End = "{0} {1}", Two = "{0} {1}" },
                _ => null
            };
        }

        return null;
    }

    // === Relative Time Patterns ===

    public RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style)
    {
        // Only provide English patterns
        if (!IsEnglish(locale))
        {
            return null;
        }

        var unitName = GetUnitName(unit, style, plural: false);
        var unitNamePlural = GetUnitName(unit, style, plural: true);

        return new RelativeTimePatterns
        {
            Future = $"in {{0}} {unitName}",
            Past = $"{{0}} {unitName} ago",
            FuturePlural = $"in {{0}} {unitNamePlural}",
            PastPlural = $"{{0}} {unitNamePlural} ago"
        };
    }

    public string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style)
    {
        // Only provide English phrases
        if (!IsEnglish(locale))
        {
            return null;
        }

        if (value == 0)
        {
            return unit switch
            {
                "second" => "now",
                "minute" => "this minute",
                "hour" => "this hour",
                "day" => "today",
                "week" => "this week",
                "month" => "this month",
                "quarter" => "this quarter",
                "year" => "this year",
                _ => null
            };
        }

        if (value == 1)
        {
            if (past)
            {
                return unit switch
                {
                    "day" => "yesterday",
                    "week" => "last week",
                    "month" => "last month",
                    "quarter" => "last quarter",
                    "year" => "last year",
                    _ => null
                };
            }
            return unit switch
            {
                "day" => "tomorrow",
                "week" => "next week",
                "month" => "next month",
                "quarter" => "next quarter",
                "year" => "next year",
                _ => null
            };
        }

        return null;
    }

    // === Number Formatting ===

    public string? GetNumberingSystemDigits(string numberingSystem)
    {
        return NumberingSystemData.Digits.TryGetValue(numberingSystem, out var digits) ? digits : null;
    }

    public CompactPatterns? GetCompactPatterns(string locale, string style)
    {
        // Use existing CompactPatterns data
        // The default provider returns English patterns only
        if (!IsEnglish(locale))
        {
            return null;
        }

        if (string.Equals(style, "short", StringComparison.Ordinal))
        {
            return new CompactPatterns
            {
                Patterns = new Dictionary<int, string>
                {
                    [3] = "{0}K",
                    [6] = "{0}M",
                    [9] = "{0}B",
                    [12] = "{0}T"
                }
            };
        }

        if (string.Equals(style, "long", StringComparison.Ordinal))
        {
            return new CompactPatterns
            {
                Patterns = new Dictionary<int, string>
                {
                    [3] = "{0} thousand",
                    [6] = "{0} million",
                    [9] = "{0} billion",
                    [12] = "{0} trillion"
                }
            };
        }

        return null;
    }

    public CurrencyData? GetCurrencyData(string locale, string currencyCode)
    {
        // Default provider returns basic currency data from .NET
        try
        {
            var culture = new CultureInfo(RemoveExtensions(locale));
            var region = new RegionInfo(culture.Name);

            // Common currency symbols
            var symbol = currencyCode switch
            {
                "USD" => "$",
                "EUR" => "\u20AC",
                "GBP" => "\u00A3",
                "JPY" => "\u00A5",
                "CNY" => "\u00A5",
                _ => currencyCode
            };

            return new CurrencyData
            {
                Symbol = symbol,
                NarrowSymbol = symbol,
                DisplayName = currencyCode
            };
        }
        catch
        {
            return null;
        }
    }

    public UnitPatterns? GetUnitPatterns(string locale, string unit, string style)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        var displayName = GetUnitDisplayName(unit, style);
        var one = $"{{0}} {GetUnitSingular(unit, style)}";
        var other = $"{{0}} {GetUnitPlural(unit, style)}";

        return new UnitPatterns
        {
            DisplayName = displayName,
            One = one,
            Other = other
        };
    }

    // === Date/Time Formatting ===

    public DateTimePatterns? GetDateTimePatterns(string locale, string? dateStyle, string? timeStyle)
    {
        // Default provider delegates to .NET's DateTimeFormatInfo
        return null;
    }

    public string[]? GetMonthNames(string locale, string style)
    {
        try
        {
            var culture = new CultureInfo(RemoveExtensions(locale));
            return style switch
            {
                "long" => culture.DateTimeFormat.MonthNames.Take(12).ToArray(),
                "short" => culture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray(),
                "narrow" => culture.DateTimeFormat.AbbreviatedMonthNames.Take(12).Select(m => m.Length > 0 ? m[0].ToString() : m).ToArray(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public string[]? GetWeekdayNames(string locale, string style)
    {
        try
        {
            var culture = new CultureInfo(RemoveExtensions(locale));
            return style switch
            {
                "long" => culture.DateTimeFormat.DayNames,
                "short" => culture.DateTimeFormat.AbbreviatedDayNames,
                "narrow" => culture.DateTimeFormat.ShortestDayNames,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public string[]? GetDayPeriods(string locale, string style)
    {
        try
        {
            var culture = new CultureInfo(RemoveExtensions(locale));
            return [culture.DateTimeFormat.AMDesignator, culture.DateTimeFormat.PMDesignator];
        }
        catch
        {
            return null;
        }
    }

    public string[]? GetEraNames(string locale, string style)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        return style switch
        {
            "long" => ["Before Christ", "Anno Domini"],
            "short" => ["BC", "AD"],
            "narrow" => ["B", "A"],
            _ => null
        };
    }

    // === Display Names ===

    public string? GetLanguageDisplayName(string locale, string code)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        try
        {
            var culture = new CultureInfo(code);
            return culture.EnglishName;
        }
        catch
        {
            return null;
        }
    }

    public string? GetRegionDisplayName(string locale, string code)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        try
        {
            var region = new RegionInfo(code);
            return region.EnglishName;
        }
        catch
        {
            return null;
        }
    }

    public string? GetScriptDisplayName(string locale, string code)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        // Common script names
        return code.ToUpperInvariant() switch
        {
            "LATN" => "Latin",
            "CYRL" => "Cyrillic",
            "ARAB" => "Arabic",
            "HANS" => "Simplified Chinese",
            "HANT" => "Traditional Chinese",
            "DEVA" => "Devanagari",
            "GREK" => "Greek",
            "HEBR" => "Hebrew",
            "JPAN" => "Japanese",
            "KORE" => "Korean",
            "THAI" => "Thai",
            _ => null
        };
    }

    public string? GetCurrencyDisplayName(string locale, string code)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        // Look up currency name from .NET culture data by finding a culture that uses this currency
        var upperCode = code.ToUpperInvariant();
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (string.Equals(region.ISOCurrencySymbol, upperCode, StringComparison.OrdinalIgnoreCase))
                {
                    return region.CurrencyEnglishName;
                }
            }
            catch
            {
                // Skip cultures without region info
            }
        }

        // Don't provide fallback - only return names for currencies we actually know about
        return null;
    }

    // === Locale Data ===

    public string? GetLikelySubtags(string locale)
    {
        return LikelySubtagsData.TryResolve(locale, out var result) ? result : null;
    }

    public WeekInfo? GetWeekInfo(string locale)
    {
        // Extract region from locale for week data lookup
        var region = ExtractRegion(locale);

        var firstDayNum = WeekData.GetFirstDayOfWeek(region);
        var minDays = WeekData.GetMinDays(region);

        // Convert CLDR day number (1=Monday, 7=Sunday) to DayOfWeek
        var firstDay = firstDayNum switch
        {
            1 => DayOfWeek.Monday,
            2 => DayOfWeek.Tuesday,
            3 => DayOfWeek.Wednesday,
            4 => DayOfWeek.Thursday,
            5 => DayOfWeek.Friday,
            6 => DayOfWeek.Saturday,
            7 => DayOfWeek.Sunday,
            _ => DayOfWeek.Monday
        };

        return new WeekInfo
        {
            FirstDay = firstDay,
            MinimalDays = minDays,
            Weekend = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }
        };
    }

    private static string? ExtractRegion(string locale)
    {
        // Locale format: language-Script-REGION-...
        var parts = locale.Split('-');
        foreach (var part in parts)
        {
            // Region is typically 2 uppercase letters or 3 digits
            if (part.Length == 2 && char.IsUpper(part[0]) && char.IsUpper(part[1]))
            {
                return part;
            }
            if (part.Length == 3 && char.IsDigit(part[0]) && char.IsDigit(part[1]) && char.IsDigit(part[2]))
            {
                return part;
            }
        }
        return null;
    }

    // === Supported Values ===

    public IReadOnlyCollection<string> GetSupportedCalendars()
    {
        // Only return calendars that are fully supported per ECMA-402 and Intl.Era-monthcode spec
        // Note: "islamic" and "islamic-rgsa" are excluded because they require specific
        // DateTimeFormat support that maps them back correctly (not aliased to islamic-civil)
        return new[]
        {
            "buddhist", "chinese", "coptic", "dangi", "ethioaa", "ethiopic",
            "gregory", "hebrew", "indian", "islamic-civil",
            "islamic-tbla", "islamic-umalqura", "iso8601",
            "japanese", "persian", "roc"
        };
    }

    public IReadOnlyCollection<string> GetSupportedCollations()
    {
        return new[]
        {
            "big5han", "compat", "dict", "direct", "ducet", "emoji", "eor",
            "gb2312", "phonebk", "phonetic", "pinyin", "reformed", "searchjl",
            "stroke", "trad", "unihan", "zhuyin"
        };
    }

    public IReadOnlyCollection<string> GetSupportedCurrencies()
    {
        var currencies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var currencyCode = region.ISOCurrencySymbol;

                // Filter out invalid currency codes (must be 3 uppercase ASCII letters)
                // Some cultures have placeholder codes like "¤¤" or "XXX"
                if (currencyCode.Length == 3 &&
                    IsUpperAsciiLetter(currencyCode[0]) &&
                    IsUpperAsciiLetter(currencyCode[1]) &&
                    IsUpperAsciiLetter(currencyCode[2]) &&
                    !string.Equals(currencyCode, "XXX", StringComparison.Ordinal)) // XXX is "no currency"
                {
                    currencies.Add(currencyCode);
                }
            }
            catch
            {
                // Skip cultures without region info
            }
        }
        return currencies.ToArray();
    }

    public IReadOnlyCollection<string> GetSupportedNumberingSystems()
    {
        return NumberingSystemData.Digits.Keys.ToArray();
    }

    public IReadOnlyCollection<string> GetSupportedTimeZones()
    {
        return TimeZoneData.GetAllTimeZones();
    }

    public IReadOnlyCollection<string> GetSupportedUnits()
    {
        return new[]
        {
            "acre", "bit", "byte", "celsius", "centimeter", "day", "degree",
            "fahrenheit", "fluid-ounce", "foot", "gallon", "gigabit", "gigabyte",
            "gram", "hectare", "hour", "inch", "kilobit", "kilobyte", "kilogram",
            "kilometer", "liter", "megabit", "megabyte", "meter", "microsecond",
            "mile", "mile-scandinavian", "milliliter", "millimeter", "millisecond",
            "minute", "month", "nanosecond", "ounce", "percent", "petabyte",
            "pound", "second", "stone", "terabit", "terabyte", "week", "yard", "year"
        };
    }

    // === Helper Methods ===

    private static bool IsEnglish(string locale)
    {
        return locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpperAsciiLetter(char c)
    {
        return c >= 'A' && c <= 'Z';
    }

    private static string RemoveExtensions(string locale)
    {
        var uIndex = locale.IndexOf("-u-", StringComparison.OrdinalIgnoreCase);
        return uIndex >= 0 ? locale.Substring(0, uIndex) : locale;
    }

    private static string GetUnitName(string unit, string style, bool plural)
    {
        if (string.Equals(style, "narrow", StringComparison.Ordinal))
        {
            return unit switch
            {
                "second" or "seconds" => "s",
                "minute" or "minutes" => "m",
                "hour" or "hours" => "h",
                "day" or "days" => "d",
                "week" or "weeks" => "w",
                "month" or "months" => "mo",
                "quarter" or "quarters" => "q",
                "year" or "years" => "y",
                _ => unit
            };
        }

        if (string.Equals(style, "short", StringComparison.Ordinal))
        {
            return unit switch
            {
                "second" or "seconds" => "sec.",
                "minute" or "minutes" => "min.",
                "hour" or "hours" => "hr.",
                "day" => plural ? "days" : "day",
                "days" => "days",
                "week" or "weeks" => "wk.",
                "month" or "months" => "mo.",
                "quarter" => plural ? "qtrs." : "qtr.",
                "quarters" => "qtrs.",
                "year" or "years" => "yr.",
                _ => unit
            };
        }

        // Long style
        return unit switch
        {
            "second" => plural ? "seconds" : "second",
            "seconds" => "seconds",
            "minute" => plural ? "minutes" : "minute",
            "minutes" => "minutes",
            "hour" => plural ? "hours" : "hour",
            "hours" => "hours",
            "day" => plural ? "days" : "day",
            "days" => "days",
            "week" => plural ? "weeks" : "week",
            "weeks" => "weeks",
            "month" => plural ? "months" : "month",
            "months" => "months",
            "quarter" => plural ? "quarters" : "quarter",
            "quarters" => "quarters",
            "year" => plural ? "years" : "year",
            "years" => "years",
            _ => unit
        };
    }

    private static string GetUnitDisplayName(string unit, string style)
    {
        return GetUnitName(unit, style, plural: true);
    }

    private static string GetUnitSingular(string unit, string style)
    {
        return GetUnitName(unit, style, plural: false);
    }

    private static string GetUnitPlural(string unit, string style)
    {
        return GetUnitName(unit, style, plural: true);
    }
}

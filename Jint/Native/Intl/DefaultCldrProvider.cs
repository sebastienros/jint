using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using Jint.Native.Intl.Data;

namespace Jint.Native.Intl;

/// <summary>
/// The locale data an engine uses unless the host replaces it: embedded CLDR files, with an English fallback.
/// </summary>
/// <remarks>
/// <para>
/// Every member is <c>virtual</c>, so changing one datum means deriving from this class and overriding
/// that one member. The other eighteen are inherited, and nothing has to be delegated by hand.
/// </para>
/// <para>
/// Install the derived instance on <see cref="Options.IntlOptions.CldrProvider"/>; leaving that property
/// alone keeps <see cref="Instance"/>, the shared singleton every unconfigured engine reads.
/// </para>
/// <para>
/// Every member has a caller: whatever this class answers is what <c>Intl</c> shows, and whatever a derived
/// class answers displaces it. Two narrow gaps are worth knowing about, both of them the engine's rather
/// than the interface's: <see cref="GetMonthNames"/> and <see cref="GetWeekdayNames"/> are not asked for
/// <c>"narrow"</c>, because <c>Intl.DateTimeFormat</c> writes the abbreviated name for a narrow style; and
/// <see cref="GetDayPeriods"/> reaches the component lane (<c>hour</c> with <c>hour12</c>) but not
/// <c>timeStyle</c>, which writes English AM/PM regardless of any locale data, .NET's included.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// sealed class GermanLists : DefaultCldrProvider
/// {
///     public override ListPatterns? GetListPatterns(string locale, string type, string style)
///         => new ListPatterns { Start = "{0}, {1}", Middle = "{0}, {1}", End = "{0} und {1}", Two = "{0} und {1}" };
/// }
///
/// options.Intl.CldrProvider = new GermanLists();
/// </code>
/// </example>
public class DefaultCldrProvider : ICldrProvider
{
    /// <summary>
    /// Singleton instance of the default provider.
    /// </summary>
    public static readonly DefaultCldrProvider Instance = new();

    // Caches for immutable locale-dependent data
    private static readonly ConcurrentDictionary<string, string[]?> _monthNameCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string[]?> _weekdayNameCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string[]?> _dayPeriodCache = new(StringComparer.Ordinal);
    private static readonly Lazy<string[]> _supportedCurrencies = new(BuildSupportedCurrencies);
    private static readonly Lazy<Dictionary<string, string>> _currencyDisplayNames = new(BuildCurrencyDisplayNames);

    /// <summary>
    /// Initializes a new instance a derived provider builds on; hosts wanting the data itself read <see cref="Instance"/>.
    /// </summary>
    protected DefaultCldrProvider()
    {
    }

    // === List Patterns ===

    /// <inheritdoc />
    public virtual ListPatterns? GetListPatterns(string locale, string type, string style)
    {
        // Try embedded CLDR data first
        var localePatterns = ListPatternsData.GetPatternsForLocale(locale);
        if (localePatterns != null)
        {
            var key = $"{type}_{style}";
            if (localePatterns.TryGetValue(key, out var patterns))
            {
                return patterns;
            }
        }

        // Fallback to English if not already English
        if (!IsEnglish(locale))
        {
            var enPatterns = ListPatternsData.GetPatternsForLocale("en");
            if (enPatterns != null)
            {
                var key = $"{type}_{style}";
                if (enPatterns.TryGetValue(key, out var patterns))
                {
                    return patterns;
                }
            }
        }

        return null;
    }

    // === Relative Time Patterns ===

    /// <inheritdoc />
    public virtual RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style)
    {
        // Try embedded CLDR data first
        var localeData = RelativeTimePatternsData.GetDataForLocale(locale);

        // Fallback to English if not found
        if (localeData == null && !IsEnglish(locale))
        {
            localeData = RelativeTimePatternsData.GetDataForLocale("en");
        }

        if (localeData == null)
        {
            return null;
        }

        var key = $"{unit}_{style}";
        if (!localeData.Patterns.TryGetValue(key, out var unitPatterns))
        {
            return null;
        }

        // Build RelativeTimePatterns with plural form support
        var futurePatterns = new Dictionary<string, string>(StringComparer.Ordinal);
        var pastPatterns = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kvp in unitPatterns.Patterns)
        {
            var patternKey = kvp.Key;
            var pattern = kvp.Value;

            if (patternKey.StartsWith("future_", StringComparison.Ordinal))
            {
                futurePatterns[patternKey.Substring(7)] = pattern; // "future_one" → "one"
            }
            else if (patternKey.StartsWith("past_", StringComparison.Ordinal))
            {
                pastPatterns[patternKey.Substring(5)] = pattern; // "past_one" → "one"
            }
        }

        // Legacy properties for backwards compatibility
        var futureOne = futurePatterns.TryGetValue("one", out var f1) ? f1 : "";
        var futureOther = futurePatterns.TryGetValue("other", out var f2) ? f2 : "";
        var pastOne = pastPatterns.TryGetValue("one", out var p1) ? p1 : "";
        var pastOther = pastPatterns.TryGetValue("other", out var p2) ? p2 : "";

        return new RelativeTimePatterns
        {
            Future = futureOne,
            FuturePlural = futureOther,
            Past = pastOne,
            PastPlural = pastOther,
            FuturePatterns = futurePatterns.Count > 0 ? futurePatterns : null,
            PastPatterns = pastPatterns.Count > 0 ? pastPatterns : null
        };
    }

    /// <inheritdoc />
    public virtual string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style)
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

    /// <inheritdoc />
    public virtual string? GetNumberingSystemDigits(string numberingSystem)
    {
        return NumberingSystemData.Digits.TryGetValue(numberingSystem, out var digits) ? digits : null;
    }

    /// <inheritdoc />
    public virtual string? GetDefaultNumberingSystem(string locale)
    {
        // Default provider has no per-locale CLDR data; caller falls back to "latn".
        return null;
    }

    /// <inheritdoc />
    public virtual CurrencyData? GetCurrencyData(string locale, string currencyCode)
    {
        return new CurrencyData
        {
            Symbol = LocaleAwareCurrencySymbol(locale, currencyCode),
            NarrowSymbol = NarrowCurrencySymbol(currencyCode),
            DisplayName = CurrencyAmountName(currencyCode)
        };
    }

    /// <summary>
    /// Some locales prefix a foreign currency with its country, so that "$" stays the local currency.
    /// </summary>
    private static string LocaleAwareCurrencySymbol(string locale, string currencyCode)
    {
        if (string.Equals(currencyCode, "USD", StringComparison.Ordinal))
        {
            var parts = locale.Split('-');
            var language = parts[0];
            var region = parts.Length > 1 ? parts[parts.Length - 1] : "";

            // Taiwan, Korea and the Chinese locales other than Hong Kong write USD as "US$"
            if (string.Equals(region, "TW", StringComparison.Ordinal) ||
                string.Equals(region, "KR", StringComparison.Ordinal) ||
                (string.Equals(language, "zh", StringComparison.Ordinal) && !string.Equals(region, "HK", StringComparison.Ordinal)))
            {
                return "US$";
            }
        }

        return CurrencySymbol(currencyCode);
    }

    private static string CurrencySymbol(string currencyCode)
    {
        return currencyCode switch
        {
            "USD" => "$",
            "EUR" => "\u20AC",
            "GBP" => "\u00A3",
            "JPY" => "\u00A5",
            "CNY" => "\u00A5",
            "KRW" => "\u20A9",
            "INR" => "\u20B9",
            "RUB" => "\u20BD",
            "BRL" => "R$",
            "CAD" => "CA$",
            "AUD" => "A$",
            "CHF" => "CHF",
            "HKD" => "HK$",
            "SGD" => "S$",
            "SEK" => "kr",
            "NOK" => "kr",
            "DKK" => "kr",
            "MXN" => "MX$",
            "NZD" => "NZ$",
            "ZAR" => "R",
            "TWD" => "NT$",
            "THB" => "\u0E3F",
            "PLN" => "z\u0142",
            "TRY" => "\u20BA",
            "ILS" => "\u20AA",
            "AED" => "\u062F.\u0625",
            "SAR" => "\uFDFC",
            "PHP" => "\u20B1",
            "MYR" => "RM",
            "IDR" => "Rp",
            "CZK" => "K\u010D",
            "HUF" => "Ft",
            _ => currencyCode
        };
    }

    private static string NarrowCurrencySymbol(string currencyCode)
    {
        // Most currencies narrow to their ordinary symbol; the exceptions are the ones whose
        // ordinary symbol carries a country prefix to disambiguate it from another dollar.
        return currencyCode switch
        {
            "CAD" or "AUD" or "HKD" or "SGD" or "NZD" or "MXN" or "TWD" => "$",
            _ => CurrencySymbol(currencyCode)
        };
    }

    private static string CurrencyAmountName(string currencyCode)
    {
        return currencyCode switch
        {
            "USD" => "US dollars",
            "EUR" => "euros",
            "GBP" => "British pounds",
            "JPY" => "Japanese yen",
            "CNY" => "Chinese yuan",
            "KRW" => "South Korean won",
            "INR" => "Indian rupees",
            "RUB" => "Russian rubles",
            "BRL" => "Brazilian reals",
            "CAD" => "Canadian dollars",
            "AUD" => "Australian dollars",
            "CHF" => "Swiss francs",
            _ => currencyCode
        };
    }

    /// <inheritdoc />
    public virtual UnitPatterns? GetUnitPatterns(string locale, string unit, string style)
    {
        // Try embedded CLDR data first
        var localePatterns = UnitPatternsData.GetPatternsForLocale(locale);
        if (localePatterns != null)
        {
            var key = $"{unit}_{style}";
            if (localePatterns.TryGetValue(key, out var pattern))
            {
                // For now, use the same pattern for both singular and plural
                // (CLDR unit patterns like "km/h" don't change based on plurality)
                return new UnitPatterns
                {
                    DisplayName = unit,
                    One = pattern,
                    Other = pattern
                };
            }
        }

        // Fallback to English if not already English
        if (!IsEnglish(locale))
        {
            var enPatterns = UnitPatternsData.GetPatternsForLocale("en");
            if (enPatterns != null)
            {
                var key = $"{unit}_{style}";
                if (enPatterns.TryGetValue(key, out var pattern))
                {
                    return new UnitPatterns
                    {
                        DisplayName = unit,
                        One = pattern,
                        Other = pattern
                    };
                }
            }
        }

        // Legacy fallback for units not in embedded data (English only)
        if (!IsEnglish(locale))
        {
            return null;
        }

        var displayName = GetUnitDisplayName(unit, style);
        var unitSingular = GetUnitSingular(unit, style);
        var unitPlural = GetUnitPlural(unit, style);

        // Special units like percent, celsius, fahrenheit don't have space before unit
        // Narrow style also doesn't have space between number and unit
        var needsSpace = !string.Equals(style, "narrow", StringComparison.Ordinal) &&
                        !string.Equals(unit, "percent", StringComparison.Ordinal) &&
                        !string.Equals(unit, "celsius", StringComparison.Ordinal) &&
                        !string.Equals(unit, "fahrenheit", StringComparison.Ordinal);

        var one = needsSpace ? $"{{0}} {unitSingular}" : $"{{0}}{unitSingular}";
        var other = needsSpace ? $"{{0}} {unitPlural}" : $"{{0}}{unitPlural}";

        return new UnitPatterns
        {
            DisplayName = displayName,
            One = one,
            Other = other
        };
    }

    // === Date/Time Formatting ===

    /// <inheritdoc />
    public virtual string[]? GetMonthNames(string locale, string style, string? calendar)
    {
        var cacheKey = string.Concat(locale, "_", style);
        return _monthNameCache.GetOrAdd(cacheKey, _ =>
        {
            var culture = IntlUtilities.GetCultureInfo(locale);
            if (culture is null)
            {
                return null;
            }

            return style switch
            {
                "long" => culture.DateTimeFormat.MonthNames.Take(12).ToArray(),
                "short" => culture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray(),
                "narrow" => culture.DateTimeFormat.AbbreviatedMonthNames.Take(12).Select(m => m.Length > 0 ? m[0].ToString() : m).ToArray(),
                _ => null
            };
        });
    }

    /// <inheritdoc />
    public virtual string[]? GetWeekdayNames(string locale, string style)
    {
        var cacheKey = string.Concat(locale, "_", style);
        return _weekdayNameCache.GetOrAdd(cacheKey, _ =>
        {
            var culture = IntlUtilities.GetCultureInfo(locale);
            if (culture is null)
            {
                return null;
            }

            return style switch
            {
                "long" => culture.DateTimeFormat.DayNames,
                "short" => culture.DateTimeFormat.AbbreviatedDayNames,
                "narrow" => culture.DateTimeFormat.ShortestDayNames,
                _ => null
            };
        });
    }

    /// <inheritdoc />
    public virtual string[]? GetDayPeriods(string locale, string style, string? calendar)
    {
        var cacheKey = string.Concat(locale, "_", style);
        return _dayPeriodCache.GetOrAdd(cacheKey, _ =>
        {
            var culture = IntlUtilities.GetCultureInfo(locale);
            if (culture is null)
            {
                return null;
            }

            return new[] { culture.DateTimeFormat.AMDesignator, culture.DateTimeFormat.PMDesignator };
        });
    }

    /// <inheritdoc />
    public virtual string[]? GetEraNames(string locale, string style, string? calendar)
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

    /// <inheritdoc />
    public virtual string? GetCurrencyDisplayName(string locale, string code)
    {
        if (!IsEnglish(locale))
        {
            return null;
        }

        var upperCode = code.ToUpperInvariant();
        return _currencyDisplayNames.Value.TryGetValue(upperCode, out var name) ? name : null;
    }

    // === Locale Data ===

    /// <inheritdoc />
    public virtual WeekInfo? GetWeekInfo(string locale)
    {
        // Extract region from locale for week data lookup
        var region = ExtractRegion(locale);

        var weekendNumbers = WeekData.GetWeekend(region);
        var weekend = new DayOfWeek[weekendNumbers.Length];
        for (var i = 0; i < weekendNumbers.Length; i++)
        {
            weekend[i] = IntlUtilities.CldrDayNumberToDayOfWeek(weekendNumbers[i]);
        }

        return new WeekInfo
        {
            FirstDay = IntlUtilities.CldrDayNumberToDayOfWeek(WeekData.GetFirstDayOfWeek(region)),
            Weekend = weekend
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

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedCalendars()
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

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedCollations()
    {
        // https://tc39.es/ecma402/#sec-availablecanonicalcollations wants the collations the
        // implementation provides Intl.Collator functionality for, which is the union of the one
        // [[co]] list every locale has - so it is read off that data rather than restated here.
        return CollatorConstructor.AvailableCanonicalCollations;
    }

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedCurrencies()
    {
        return _supportedCurrencies.Value;
    }

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedNumberingSystems()
    {
        return NumberingSystemData.Digits.Keys.ToArray();
    }

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedTimeZones()
    {
        // Return only canonical (primary) timezone identifiers for supportedValuesOf
        return TimeZoneData.GetCanonicalTimeZones();
    }

    /// <inheritdoc />
    public virtual IReadOnlyCollection<string> GetSupportedUnits()
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
                "millisecond" or "milliseconds" => "ms",
                "microsecond" or "microseconds" => "μs",
                "nanosecond" or "nanoseconds" => "ns",
                "meter" or "meters" => "m",
                "kilometer" or "kilometers" => "km",
                "centimeter" or "centimeters" => "cm",
                "millimeter" or "millimeters" => "mm",
                "mile" or "miles" => "mi",
                "foot" or "feet" => "ft",
                "inch" or "inches" => "in",
                "yard" or "yards" => "yd",
                "gram" or "grams" => "g",
                "kilogram" or "kilograms" => "kg",
                "pound" or "pounds" => "lb",
                "ounce" or "ounces" => "oz",
                "liter" or "liters" => "l",
                "milliliter" or "milliliters" => "mL",
                "gallon" or "gallons" => "gal",
                "byte" or "bytes" => "B",
                "kilobyte" or "kilobytes" => "kB",
                "megabyte" or "megabytes" => "MB",
                "gigabyte" or "gigabytes" => "GB",
                "percent" => "%",
                "celsius" => "°C",
                "fahrenheit" => "°F",
                _ => unit
            };
        }

        if (string.Equals(style, "short", StringComparison.Ordinal))
        {
            return unit switch
            {
                "second" or "seconds" => "sec",
                "minute" or "minutes" => "min",
                "hour" or "hours" => "hr",
                // "day" is special in English - short form has pluralization (day/days)
                "day" or "days" => plural ? "days" : "day",
                "week" or "weeks" => "wk",
                "month" or "months" => "mo",
                "quarter" or "quarters" => "qtr",
                "year" or "years" => "yr",
                "millisecond" or "milliseconds" => "ms",
                "microsecond" or "microseconds" => "μs",
                "nanosecond" or "nanoseconds" => "ns",
                "meter" or "meters" => "m",
                "kilometer" or "kilometers" => "km",
                "centimeter" or "centimeters" => "cm",
                "millimeter" or "millimeters" => "mm",
                "mile" or "miles" => "mi",
                "foot" or "feet" => "ft",
                "inch" or "inches" => "in",
                "yard" or "yards" => "yd",
                "gram" or "grams" => "g",
                "kilogram" or "kilograms" => "kg",
                "pound" or "pounds" => "lb",
                "ounce" or "ounces" => "oz",
                "liter" or "liters" => "L",
                "milliliter" or "milliliters" => "mL",
                "gallon" or "gallons" => "gal",
                "byte" or "bytes" => "B",
                "kilobyte" or "kilobytes" => "kB",
                "megabyte" or "megabytes" => "MB",
                "gigabyte" or "gigabytes" => "GB",
                "percent" => "%",
                "celsius" => "°C",
                "fahrenheit" => "°F",
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
            "millisecond" => plural ? "milliseconds" : "millisecond",
            "milliseconds" => "milliseconds",
            "microsecond" => plural ? "microseconds" : "microsecond",
            "microseconds" => "microseconds",
            "nanosecond" => plural ? "nanoseconds" : "nanosecond",
            "nanoseconds" => "nanoseconds",
            "meter" => plural ? "meters" : "meter",
            "meters" => "meters",
            "kilometer" => plural ? "kilometers" : "kilometer",
            "kilometers" => "kilometers",
            "centimeter" => plural ? "centimeters" : "centimeter",
            "centimeters" => "centimeters",
            "millimeter" => plural ? "millimeters" : "millimeter",
            "millimeters" => "millimeters",
            "mile" => plural ? "miles" : "mile",
            "miles" => "miles",
            "foot" => plural ? "feet" : "foot",
            "feet" => "feet",
            "inch" => plural ? "inches" : "inch",
            "inches" => "inches",
            "yard" => plural ? "yards" : "yard",
            "yards" => "yards",
            "gram" => plural ? "grams" : "gram",
            "grams" => "grams",
            "kilogram" => plural ? "kilograms" : "kilogram",
            "kilograms" => "kilograms",
            "pound" => plural ? "pounds" : "pound",
            "pounds" => "pounds",
            "ounce" => plural ? "ounces" : "ounce",
            "ounces" => "ounces",
            "liter" => plural ? "liters" : "liter",
            "liters" => "liters",
            "milliliter" => plural ? "milliliters" : "milliliter",
            "milliliters" => "milliliters",
            "gallon" => plural ? "gallons" : "gallon",
            "gallons" => "gallons",
            "byte" => plural ? "bytes" : "byte",
            "bytes" => "bytes",
            "kilobyte" => plural ? "kilobytes" : "kilobyte",
            "kilobytes" => "kilobytes",
            "megabyte" => plural ? "megabytes" : "megabyte",
            "megabytes" => "megabytes",
            "gigabyte" => plural ? "gigabytes" : "gigabyte",
            "gigabytes" => "gigabytes",
            "percent" => "percent",
            "celsius" => "degrees Celsius",
            "fahrenheit" => "degrees Fahrenheit",
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

    private static string[] BuildSupportedCurrencies()
    {
        var currencies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var culture in IntlUtilities.SpecificCultures.Value)
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var currencyCode = region.ISOCurrencySymbol;

                if (currencyCode.Length == 3 &&
                    char.IsAsciiLetterUpper(currencyCode[0]) &&
                    char.IsAsciiLetterUpper(currencyCode[1]) &&
                    char.IsAsciiLetterUpper(currencyCode[2]) &&
                    !string.Equals(currencyCode, "XXX", StringComparison.Ordinal))
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

    private static Dictionary<string, string> BuildCurrencyDisplayNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in IntlUtilities.SpecificCultures.Value)
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var code = region.ISOCurrencySymbol;
                if (!names.ContainsKey(code))
                {
                    names[code] = region.CurrencyEnglishName;
                }
            }
            catch
            {
                // Skip cultures without region info
            }
        }

        return names;
    }
}

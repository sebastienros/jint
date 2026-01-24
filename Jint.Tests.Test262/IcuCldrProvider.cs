#nullable enable

using ICU4N.Globalization;
using ICU4N.Impl;
using ICU4N.Text;
using ICU4N.Util;
using Jint.Native.Intl;

namespace Jint.Tests.Test262;

/// <summary>
/// CLDR provider implementation that combines ICU4N features with default provider fallback.
/// Uses ICU4N's PluralRules for plural category selection and ICUResourceBundle for
/// direct CLDR data access (unit patterns, list patterns, etc.).
/// Falls back to DefaultCldrProvider when ICU4N data is not available.
/// </summary>
public sealed class IcuCldrProvider : ICldrProvider
{
    private readonly ICldrProvider _fallback = DefaultCldrProvider.Instance;

    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly IcuCldrProvider Instance = new();

    private IcuCldrProvider()
    {
    }

    // === CLDR Resource Bundle Access ===

    private static UResourceBundle? GetBundle(string locale)
    {
        try
        {
            var culture = new UCultureInfo(locale);
            return UResourceBundle.GetBundleInstance(ICUData.IcuBaseName, culture);
        }
        catch
        {
            return null;
        }
    }

    private static UResourceBundle? GetBundleAt(UResourceBundle bundle, string path)
    {
        try
        {
            foreach (var segment in path.Split('/'))
            {
                bundle = bundle.Get(segment);
            }
            return bundle;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(UResourceBundle bundle, string key)
    {
        try
        {
            return bundle.GetString(key);
        }
        catch
        {
            return null;
        }
    }

    // === List Patterns ===
    // ICU4N doesn't have ListFormatter API, but we can access CLDR data directly

    public ListPatterns? GetListPatterns(string locale, string type, string style)
    {
        // Map Intl type/style to CLDR path
        var cldrType = (type, style) switch
        {
            ("conjunction", "long") => "standard",
            ("conjunction", "short") => "standard-short",
            ("conjunction", "narrow") => "standard-narrow",
            ("disjunction", "long") => "or",
            ("disjunction", "short") => "or-short",
            ("disjunction", "narrow") => "or-narrow",
            ("unit", "long") => "unit",
            ("unit", "short") => "unit-short",
            ("unit", "narrow") => "unit-narrow",
            _ => "standard"
        };

        var path = $"listPattern/{cldrType}";

        try
        {
            var bundle = GetBundle(locale);
            if (bundle == null)
            {
                return _fallback.GetListPatterns(locale, type, style);
            }

            var listBundle = GetBundleAt(bundle, path);
            if (listBundle == null)
            {
                return _fallback.GetListPatterns(locale, type, style);
            }

            var two = TryGetString(listBundle, "2");
            var start = TryGetString(listBundle, "start");
            var middle = TryGetString(listBundle, "middle");
            var end = TryGetString(listBundle, "end");

            // If we don't have the patterns, fall back
            if (two == null && start == null && end == null)
            {
                return _fallback.GetListPatterns(locale, type, style);
            }

            return new ListPatterns
            {
                Two = two ?? "{0}, {1}",
                Start = start ?? "{0}, {1}",
                Middle = middle ?? "{0}, {1}",
                End = end ?? "{0}, {1}"
            };
        }
        catch
        {
            return _fallback.GetListPatterns(locale, type, style);
        }
    }

    // === Relative Time Patterns ===
    // ICU4N doesn't have RelativeDateTimeFormatter, use fallback
    public RelativeTimePatterns? GetRelativeTimePatterns(string locale, string unit, string style)
        => _fallback.GetRelativeTimePatterns(locale, unit, style);

    public string? GetRelativeTimeSpecialPhrase(string locale, string unit, int value, bool past, string style)
        => _fallback.GetRelativeTimeSpecialPhrase(locale, unit, value, past, style);

    // === Number Formatting ===

    public string? GetNumberingSystemDigits(string numberingSystem)
    {
        // Use ICU's numbering system data
        try
        {
            var ns = NumberingSystem.GetInstanceByName(numberingSystem);
            if (ns != null && !ns.IsAlgorithmic)
            {
                return ns.Description; // Contains the digit characters
            }
            return _fallback.GetNumberingSystemDigits(numberingSystem);
        }
        catch
        {
            return _fallback.GetNumberingSystemDigits(numberingSystem);
        }
    }

    public CompactPatterns? GetCompactPatterns(string locale, string style)
        => _fallback.GetCompactPatterns(locale, style);

    public Jint.Native.Intl.CurrencyData? GetCurrencyData(string locale, string currencyCode)
        => _fallback.GetCurrencyData(locale, currencyCode);

    public UnitPatterns? GetUnitPatterns(string locale, string unit, string style)
    {
        // Map Intl unit names to CLDR paths
        var cldrUnit = MapToCldrUnit(unit);
        var path = $"units/{style}/{cldrUnit}";

        try
        {
            var bundle = GetBundle(locale);
            if (bundle == null)
            {
                return _fallback.GetUnitPatterns(locale, unit, style);
            }

            var unitBundle = GetBundleAt(bundle, path);
            if (unitBundle == null)
            {
                return _fallback.GetUnitPatterns(locale, unit, style);
            }

            var displayName = TryGetString(unitBundle, "displayName");

            // Get patterns for each plural category
            var other = TryGetString(unitBundle, "unitPattern-count-other");
            var one = TryGetString(unitBundle, "unitPattern-count-one");
            var zero = TryGetString(unitBundle, "unitPattern-count-zero");
            var two = TryGetString(unitBundle, "unitPattern-count-two");
            var few = TryGetString(unitBundle, "unitPattern-count-few");
            var many = TryGetString(unitBundle, "unitPattern-count-many");

            // If we don't have any patterns, fall back
            if (other == null && one == null)
            {
                return _fallback.GetUnitPatterns(locale, unit, style);
            }

            return new UnitPatterns
            {
                DisplayName = displayName ?? unit,
                Other = other ?? $"{{0}} {unit}",
                One = one,
                Zero = zero,
                Two = two,
                Few = few,
                Many = many
            };
        }
        catch
        {
            return _fallback.GetUnitPatterns(locale, unit, style);
        }
    }

    private static string MapToCldrUnit(string unit)
    {
        return unit switch
        {
            // Duration units
            "year" or "years" => "duration-year",
            "month" or "months" => "duration-month",
            "week" or "weeks" => "duration-week",
            "day" or "days" => "duration-day",
            "hour" or "hours" => "duration-hour",
            "minute" or "minutes" => "duration-minute",
            "second" or "seconds" => "duration-second",
            "millisecond" or "milliseconds" => "duration-millisecond",
            "microsecond" or "microseconds" => "duration-microsecond",
            "nanosecond" or "nanoseconds" => "duration-nanosecond",
            // Length units
            "meter" => "length-meter",
            "kilometer" => "length-kilometer",
            "centimeter" => "length-centimeter",
            "millimeter" => "length-millimeter",
            "inch" => "length-inch",
            "foot" => "length-foot",
            "yard" => "length-yard",
            "mile" => "length-mile",
            // Mass units
            "gram" => "mass-gram",
            "kilogram" => "mass-kilogram",
            "milligram" => "mass-milligram",
            "pound" => "mass-pound",
            "ounce" => "mass-ounce",
            // Other common units
            "liter" => "volume-liter",
            "milliliter" => "volume-milliliter",
            "gallon" => "volume-gallon",
            "celsius" => "temperature-celsius",
            "fahrenheit" => "temperature-fahrenheit",
            "percent" => "concentr-percent",
            "byte" => "digital-byte",
            "kilobyte" => "digital-kilobyte",
            "megabyte" => "digital-megabyte",
            "gigabyte" => "digital-gigabyte",
            "terabyte" => "digital-terabyte",
            _ => unit
        };
    }

    // === Date/Time Formatting ===
    // Note: ICU4N doesn't have DateFormatSymbols ported yet, so we use the fallback provider
    // which uses .NET's CultureInfo for basic date/time data.

    public DateTimePatterns? GetDateTimePatterns(string locale, string? dateStyle, string? timeStyle)
        => _fallback.GetDateTimePatterns(locale, dateStyle, timeStyle);

    public string[]? GetMonthNames(string locale, string style)
        => _fallback.GetMonthNames(locale, style);

    public string[]? GetWeekdayNames(string locale, string style)
        => _fallback.GetWeekdayNames(locale, style);

    public string[]? GetDayPeriods(string locale, string style)
        => _fallback.GetDayPeriods(locale, style);

    public string[]? GetEraNames(string locale, string style)
        => _fallback.GetEraNames(locale, style);

    // === Display Names ===

    public string? GetLanguageDisplayName(string locale, string code)
    {
        try
        {
            var displayLocale = new UCultureInfo(locale);
            var codeLocale = new UCultureInfo(code);
            var result = codeLocale.GetDisplayLanguage(displayLocale);
            return string.IsNullOrEmpty(result) ? _fallback.GetLanguageDisplayName(locale, code) : result;
        }
        catch
        {
            return _fallback.GetLanguageDisplayName(locale, code);
        }
    }

    public string? GetRegionDisplayName(string locale, string code)
    {
        try
        {
            var displayLocale = new UCultureInfo(locale);
            // Create a locale with just the region
            var codeLocale = new UCultureInfo("und-" + code);
            var result = codeLocale.GetDisplayCountry(displayLocale);
            return string.IsNullOrEmpty(result) ? _fallback.GetRegionDisplayName(locale, code) : result;
        }
        catch
        {
            return _fallback.GetRegionDisplayName(locale, code);
        }
    }

    public string? GetScriptDisplayName(string locale, string code)
    {
        try
        {
            var displayLocale = new UCultureInfo(locale);
            // Create a locale with just the script
            var codeLocale = new UCultureInfo("und-" + code);
            var result = codeLocale.GetDisplayScript(displayLocale);
            return string.IsNullOrEmpty(result) ? _fallback.GetScriptDisplayName(locale, code) : result;
        }
        catch
        {
            return _fallback.GetScriptDisplayName(locale, code);
        }
    }

    public string? GetCurrencyDisplayName(string locale, string code)
        => _fallback.GetCurrencyDisplayName(locale, code);

    // === Locale Data ===

    public string? GetLikelySubtags(string locale)
    {
        try
        {
            var icuLocale = new UCultureInfo(locale);
            var maximized = UCultureInfo.AddLikelySubtags(icuLocale);
            return maximized?.Name ?? _fallback.GetLikelySubtags(locale);
        }
        catch
        {
            return _fallback.GetLikelySubtags(locale);
        }
    }

    public WeekInfo? GetWeekInfo(string locale)
        => _fallback.GetWeekInfo(locale);

    // === Supported Values ===

    public IReadOnlyCollection<string> GetSupportedCalendars()
        => _fallback.GetSupportedCalendars();

    public IReadOnlyCollection<string> GetSupportedCollations()
        => _fallback.GetSupportedCollations();

    public IReadOnlyCollection<string> GetSupportedCurrencies()
        => _fallback.GetSupportedCurrencies();

    public IReadOnlyCollection<string> GetSupportedNumberingSystems()
    {
        // Use fallback - ICU4N's list may not include all required numbering systems
        // Our embedded NumberingSystemData has the complete ECMA-402 spec list
        return _fallback.GetSupportedNumberingSystems();
    }

    public IReadOnlyCollection<string> GetSupportedTimeZones()
        => _fallback.GetSupportedTimeZones();

    public IReadOnlyCollection<string> GetSupportedUnits()
        => _fallback.GetSupportedUnits();

    // === Plural Rules ===

    public string SelectPluralCategory(string locale, double value, string type)
    {
        try
        {
            var culture = new UCultureInfo(locale);
            var pluralType = string.Equals(type, "ordinal", StringComparison.Ordinal)
                ? PluralType.Ordinal
                : PluralType.Cardinal;

            var rules = PluralRules.GetInstance(culture, pluralType);
            var category = rules.Select(value);

            // ICU4N returns the category name (e.g., "one", "other", "few", "many", "zero", "two")
            return category ?? "other";
        }
        catch
        {
            // Fallback to default provider's English rules
            return _fallback.SelectPluralCategory(locale, value, type);
        }
    }
}

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

    public string? GetDefaultNumberingSystem(string locale)
    {
        try
        {
            var culture = new UCultureInfo(locale);
            var ns = NumberingSystem.GetInstance(culture);
            return ns?.Name;
        }
        catch
        {
            return _fallback.GetDefaultNumberingSystem(locale);
        }
    }

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

    // The default provider reads CLDR's own calendarPreferenceData, which is the table ICU would answer
    // from, so there is nothing ICU4N could add here.
    public string? GetDefaultCalendar(string locale)
        => _fallback.GetDefaultCalendar(locale);

    /// <summary>
    /// The month names of the calendar asked about. The fallback provider reads
    /// <c>CultureInfo.DateTimeFormat</c>, whose names are the twelve Gregorian months, and answers nothing
    /// for a calendar counting months of its own; CLDR is where those names live, and ICU4N ships the data
    /// even though it has no <c>DateFormatSymbols</c> to read it with.
    /// </summary>
    /// <remarks>
    /// The fallback goes first, so nothing a .NET culture already answers for changes hands.
    /// </remarks>
    public string[]? GetMonthNames(string locale, string style, string? calendar)
        => _fallback.GetMonthNames(locale, style, calendar) ?? CldrMonthNames(locale, style, calendar);

    /// <summary>
    /// The CLDR calendars to look one identifier's months up under, most specific first: several calendars
    /// share one table — the three tabular Islamic ones are all <c>islamic</c>, and <c>dangi</c> takes
    /// <c>chinese</c>'s names in every locale that has them.
    /// </summary>
    private static string[]? CldrCalendarKeys(string? calendar) => calendar switch
    {
        null or "gregory" or "iso8601" => ["gregorian"],
        "ethioaa" => ["ethiopic-amete-alem", "ethiopic"],
        "islamic-civil" or "islamic-tbla" or "islamic-umalqura" or "islamic-rgsa" or "islamic" => [calendar, "islamic"],
        "dangi" => ["dangi", "chinese"],
        "buddhist" or "japanese" or "roc" or "persian" or "hebrew" or "coptic" or "ethiopic"
            or "indian" or "chinese" => [calendar],
        _ => null
    };

    /// <summary>
    /// Reads <c>calendar/&lt;key&gt;/monthNames/format/&lt;width&gt;</c>, from the locale where it has one and
    /// from <c>root</c> otherwise — which is where CLDR keeps the English names of the calendars an <c>en</c>
    /// bundle carries no month table for, and what ICU itself resolves to for them.
    /// </summary>
    /// <remarks>
    /// The array is indexed by month number, so a thirteen-month calendar answers thirteen names. The Hebrew
    /// array holds fourteen, the last being the leap year's Adar II, which no caller here can pick out: a
    /// leap Adar II is month 7 and reads "Adar".
    /// </remarks>
    private static string[]? CldrMonthNames(string locale, string style, string? calendar)
    {
        var keys = CldrCalendarKeys(calendar);
        var width = style switch
        {
            "long" => "wide",
            "short" => "abbreviated",
            "narrow" => "narrow",
            _ => null
        };

        if (keys is null || width is null)
        {
            return null;
        }

        // "format" is the name a pattern writes, against "stand-alone" for a month named on its own.
        foreach (var source in new[] { locale, "root" })
        {
            var bundle = GetBundle(source);
            if (bundle is null)
            {
                continue;
            }

            foreach (var key in keys)
            {
                var months = GetBundleAt(bundle, $"calendar/{key}/monthNames/format/{width}");
                if (months is null)
                {
                    continue;
                }

                try
                {
                    if (months.GetStringArray() is { Length: > 0 } values)
                    {
                        return values;
                    }
                }
                catch
                {
                    // A resource that is not an array of strings is not month names.
                }
            }
        }

        return null;
    }

    public string[]? GetWeekdayNames(string locale, string style)
        => _fallback.GetWeekdayNames(locale, style);

    public string[]? GetDayPeriods(string locale, string style, string? calendar)
        => _fallback.GetDayPeriods(locale, style, calendar);

    public string[]? GetEraNames(string locale, string style, string? calendar)
        => _fallback.GetEraNames(locale, style, calendar);

    // === Display Names ===

    public string? GetCurrencyDisplayName(string locale, string code)
        => _fallback.GetCurrencyDisplayName(locale, code);

    // === Locale Data ===

    public WeekInfo? GetWeekInfo(string locale)
        => _fallback.GetWeekInfo(locale);

    // === Supported Values ===

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
}

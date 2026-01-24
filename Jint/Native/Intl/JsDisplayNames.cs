using System.Globalization;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-displaynames-objects
/// Represents an Intl.DisplayNames instance for locale-aware display name resolution.
/// </summary>
internal sealed class JsDisplayNames : ObjectInstance
{
    internal JsDisplayNames(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string type,
        string style,
        string fallback,
        string? languageDisplay,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        DisplayType = type;
        Style = style;
        Fallback = fallback;
        LanguageDisplay = languageDisplay;
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// The locale used for display names.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The type of display names: "language", "region", "script", "currency", "calendar", "dateTimeField".
    /// </summary>
    internal string DisplayType { get; }

    /// <summary>
    /// The style: "long", "short", or "narrow".
    /// </summary>
    internal string Style { get; }

    /// <summary>
    /// The fallback behavior: "code" or "none".
    /// </summary>
    internal string Fallback { get; }

    /// <summary>
    /// For language display names: "dialect" or "standard".
    /// </summary>
    internal string? LanguageDisplay { get; }

    /// <summary>
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Returns the display name for the given code.
    /// </summary>
    internal string? Of(string code)
    {
        return DisplayType switch
        {
            "language" => GetLanguageDisplayName(code),
            "region" => GetRegionDisplayName(code),
            "script" => GetScriptDisplayName(code),
            "currency" => GetCurrencyDisplayName(code),
            "calendar" => GetCalendarDisplayName(code),
            "dateTimeField" => GetDateTimeFieldDisplayName(code),
            _ => null
        };
    }

    private string? GetLanguageDisplayName(string code)
    {
        try
        {
            // Try to get CultureInfo for the language code
            var culture = CultureInfo.GetCultureInfo(code.Replace('_', '-'));

            // Use native name if available and locale-appropriate
            if (string.Equals(CultureInfo.TwoLetterISOLanguageName, culture.TwoLetterISOLanguageName, StringComparison.Ordinal))
            {
                return culture.NativeName;
            }

            // In English locale, use EnglishName; otherwise use DisplayName
            if (Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return culture.EnglishName;
            }

            return culture.DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return GetFallbackValue(code);
        }
    }

    private string? GetRegionDisplayName(string code)
    {
        try
        {
            // RegionInfo expects a country/region code
            var region = new RegionInfo(code);

            if (Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return region.EnglishName;
            }

            return region.DisplayName;
        }
        catch
        {
            return GetFallbackValue(code);
        }
    }

    private string? GetScriptDisplayName(string code)
    {
        // .NET doesn't have built-in script names, provide common ones
        var name = code.ToUpperInvariant() switch
        {
            "LATN" => "Latin",
            "CYRL" => "Cyrillic",
            "ARAB" => "Arabic",
            "HANS" => "Simplified Han",
            "HANT" => "Traditional Han",
            "DEVA" => "Devanagari",
            "GREK" => "Greek",
            "HEBR" => "Hebrew",
            "JPAN" => "Japanese",
            "KORE" => "Korean",
            "THAI" => "Thai",
            "BENG" => "Bengali",
            "GURU" => "Gurmukhi",
            "GUJR" => "Gujarati",
            "ORYA" => "Oriya",
            "TAML" => "Tamil",
            "TELU" => "Telugu",
            "KNDA" => "Kannada",
            "MLYM" => "Malayalam",
            "SINH" => "Sinhala",
            "MYMR" => "Myanmar",
            "GEOR" => "Georgian",
            "ARMN" => "Armenian",
            "ETHI" => "Ethiopic",
            "KHMR" => "Khmer",
            "TIBT" => "Tibetan",
            "MONG" => "Mongolian",
            _ => null
        };

        return name ?? GetFallbackValue(code);
    }

    private string? GetCurrencyDisplayName(string code)
    {
        // Use CLDR provider to get currency display name
        var cldrProvider = _engine.Options.Intl.CldrProvider;
        var name = cldrProvider.GetCurrencyDisplayName(Locale, code);

        return name ?? GetFallbackValue(code);
    }

    private string? GetCalendarDisplayName(string code)
    {
        var normalizedCode = code.ToLowerInvariant();

        // Only return display names for calendars we actually support
        // This ensures consistency with Intl.supportedValuesOf("calendar")
        var supportedCalendars = _engine.Options.Intl.CldrProvider.GetSupportedCalendars();
        var isSupported = false;
        foreach (var supported in supportedCalendars)
        {
            if (string.Equals(supported, normalizedCode, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(normalizedCode, "gregorian", StringComparison.Ordinal) && string.Equals(supported, "gregory", StringComparison.Ordinal)))
            {
                isSupported = true;
                break;
            }
        }

        if (!isSupported)
        {
            return GetFallbackValue(code);
        }

        var name = normalizedCode switch
        {
            "gregory" or "gregorian" => "Gregorian Calendar",
            "buddhist" => "Buddhist Calendar",
            "chinese" => "Chinese Calendar",
            "coptic" => "Coptic Calendar",
            "dangi" => "Dangi Calendar",
            "ethioaa" => "Ethiopic Amete Alem Calendar",
            "ethiopic" => "Ethiopic Calendar",
            "hebrew" => "Hebrew Calendar",
            "indian" => "Indian National Calendar",
            "islamic-umalqura" => "Islamic (Umm al-Qura) Calendar",
            "islamic-tbla" => "Islamic (tabular, Thursday epoch) Calendar",
            "islamic-civil" => "Islamic (civil) Calendar",
            "iso8601" => "ISO-8601 Calendar",
            "japanese" => "Japanese Calendar",
            "persian" => "Persian Calendar",
            "roc" => "Minguo Calendar",
            _ => null
        };

        return name ?? GetFallbackValue(code);
    }

    private string? GetDateTimeFieldDisplayName(string code)
    {
        var name = code.ToLowerInvariant() switch
        {
            "era" => "era",
            "year" => "year",
            "quarter" => "quarter",
            "month" => "month",
            "weekofyear" => "week",
            "weekday" => "day of the week",
            "day" => "day",
            "dayperiod" => "AM/PM",
            "hour" => "hour",
            "minute" => "minute",
            "second" => "second",
            "timezonename" => "time zone",
            _ => null
        };

        return name ?? GetFallbackValue(code);
    }

    private string? GetFallbackValue(string code)
    {
        return string.Equals(Fallback, "code", StringComparison.Ordinal) ? code : null;
    }
}

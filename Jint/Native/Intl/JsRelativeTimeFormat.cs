using System.Globalization;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-relativetimeformat-objects
/// Represents an Intl.RelativeTimeFormat instance for locale-aware relative time formatting.
/// </summary>
internal sealed class JsRelativeTimeFormat : ObjectInstance
{
    internal JsRelativeTimeFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string numberingSystem,
        string style,
        string numeric,
        CultureInfo cultureInfo,
        JsNumberFormat numberFormat) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        NumberingSystem = numberingSystem;
        Style = style;
        Numeric = numeric;
        CultureInfo = cultureInfo;
        NumberFormat = numberFormat;
    }

    /// <summary>
    /// The locale used for formatting.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The numbering system used for formatting digits.
    /// </summary>
    internal string NumberingSystem { get; }

    /// <summary>
    /// The style: "long", "short", or "narrow".
    /// </summary>
    internal string Style { get; }

    /// <summary>
    /// The numeric: "always" or "auto".
    /// </summary>
    internal string Numeric { get; }

    /// <summary>
    /// The NumberFormat instance for formatting numbers (per ECMA-402 17.1.1 step 24).
    /// </summary>
    internal JsNumberFormat NumberFormat { get; }

    /// <summary>
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    /// <summary>
    /// Formats a relative time value.
    /// </summary>
    internal string Format(double value, string unit)
    {
        var absValue = System.Math.Abs(value);
        // Handle negative zero: value < 0 is false for -0, so we check for negative infinity from 1/value
        var isPast = value < 0 || double.IsNegativeInfinity(1.0 / value);
        var intValue = (long) System.Math.Round(absValue);

        // Handle "auto" numeric - special phrases for -1, 0, 1
        if (string.Equals(Numeric, "auto", StringComparison.Ordinal))
        {
            var specialPhrase = GetSpecialPhrase(intValue, unit, isPast);
            if (specialPhrase != null)
            {
                return specialPhrase;
            }
        }

        // Format the number according to locale conventions using NumberFormat
        // Per ECMA-402 17.5.2 step 11: Use PartitionNumberPattern for formatting
        var formattedNumber = NumberFormat.Format(absValue);

        // Try to get patterns from CLDR provider
        var patterns = CldrProvider.GetRelativeTimePatterns(Locale, unit, Style);
        if (patterns != null)
        {
            string pattern;

            // Use plural rules if available
            if (patterns.FuturePatterns != null || patterns.PastPatterns != null)
            {
                // Get plural form using PluralRules logic
                var pluralForm = GetPluralForm(absValue);

                var patternsDict = isPast ? patterns.PastPatterns : patterns.FuturePatterns;
                if (patternsDict != null)
                {
                    // Try exact plural form, fallback to "other"
                    if (!patternsDict.TryGetValue(pluralForm, out pattern!))
                    {
                        pattern = patternsDict.TryGetValue("other", out var fallback) ? fallback : "";
                    }
                }
                else
                {
                    pattern = "";
                }
            }
            else
            {
                // Legacy: simple plural check (backwards compatibility)
                var plural = absValue != 1;
                pattern = isPast
                    ? (plural ? patterns.PastPlural : patterns.Past)
                    : (plural ? patterns.FuturePlural : patterns.Future);
            }

            var result = pattern.Replace("{0}", formattedNumber);
            return Data.NumberingSystemData.TransliterateDigits(result, NumberingSystem);
        }

        // Fallback to hardcoded patterns
        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var unitName = GetUnitName(unit, absValue != 1);

        if (isEnglish)
        {
            if (isPast)
            {
                return Data.NumberingSystemData.TransliterateDigits($"{formattedNumber} {unitName} ago", NumberingSystem);
            }
            return Data.NumberingSystemData.TransliterateDigits($"in {formattedNumber} {unitName}", NumberingSystem);
        }

        // For non-English, use a simple format
        if (isPast)
        {
            return Data.NumberingSystemData.TransliterateDigits($"-{formattedNumber} {unitName}", NumberingSystem);
        }
        return Data.NumberingSystemData.TransliterateDigits($"+{formattedNumber} {unitName}", NumberingSystem);
    }

    /// <summary>
    /// Formats a relative time value and returns parts.
    /// </summary>
    internal JsArray FormatToParts(Engine engine, double value, string unit)
    {
        var result = new JsArray(engine);
        uint index = 0;

        var absValue = System.Math.Abs(value);
        // Handle negative zero: value < 0 is false for -0, so we check for negative infinity from 1/value
        var isPast = value < 0 || double.IsNegativeInfinity(1.0 / value);
        var intValue = (long) System.Math.Round(absValue);

        // Handle "auto" numeric - special phrases
        if (string.Equals(Numeric, "auto", StringComparison.Ordinal))
        {
            var specialPhrase = GetSpecialPhrase(intValue, unit, isPast);
            if (specialPhrase != null)
            {
                var literalPart = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
                literalPart.Set("type", "literal");
                literalPart.Set("value", specialPhrase);
                result.SetIndexValue(index++, literalPart, updateLength: true);
                return result;
            }
        }

        // Try to get patterns from CLDR provider
        var patterns = CldrProvider.GetRelativeTimePatterns(Locale, unit, Style);
        if (patterns != null)
        {
            string pattern;

            // Use plural rules if available
            if (patterns.FuturePatterns != null || patterns.PastPatterns != null)
            {
                // Get plural form using PluralRules logic
                var pluralForm = GetPluralForm(absValue);

                var patternsDict = isPast ? patterns.PastPatterns : patterns.FuturePatterns;
                if (patternsDict != null)
                {
                    // Try exact plural form, fallback to "other"
                    if (!patternsDict.TryGetValue(pluralForm, out pattern!))
                    {
                        pattern = patternsDict.TryGetValue("other", out var fallback) ? fallback : "";
                    }
                }
                else
                {
                    pattern = "";
                }
            }
            else
            {
                // Legacy: simple plural check (backwards compatibility)
                var plural = absValue != 1;
                pattern = isPast
                    ? (plural ? patterns.PastPlural : patterns.Past)
                    : (plural ? patterns.FuturePlural : patterns.Future);
            }

            // Parse the pattern and create parts
            // Pattern format: "in {0} days" or "{0} days ago"
            FormatPatternToParts(engine, result, ref index, pattern, absValue, unit);
            return result;
        }

        // Fallback to hardcoded patterns
        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var unitName = GetUnitName(unit, absValue != 1);

        if (isEnglish)
        {
            if (isPast)
            {
                // "X units ago"
                AddNumberParts(engine, result, ref index, absValue, unit, CultureInfo, NumberingSystem);
                AddLiteralPart(engine, result, ref index, $" {unitName} ago");
            }
            else
            {
                // "in X units"
                AddLiteralPart(engine, result, ref index, "in ");
                AddNumberParts(engine, result, ref index, absValue, unit, CultureInfo, NumberingSystem);
                AddLiteralPart(engine, result, ref index, $" {unitName}");
            }
        }
        else
        {
            // Simple format for non-English
            if (isPast)
            {
                AddLiteralPart(engine, result, ref index, "-");
            }
            else
            {
                AddLiteralPart(engine, result, ref index, "+");
            }
            AddNumberParts(engine, result, ref index, absValue, unit, CultureInfo, NumberingSystem);
            AddLiteralPart(engine, result, ref index, $" {unitName}");
        }

        return result;
    }

    /// <summary>
    /// Formats a pattern like "in {0} days" or "{0} days ago" to parts.
    /// </summary>
    private void FormatPatternToParts(Engine engine, JsArray result, ref uint index, string pattern, double value, string unit)
    {
        const string placeholder = "{0}";
        var placeholderIndex = pattern.IndexOf(placeholder, StringComparison.Ordinal);

        if (placeholderIndex < 0)
        {
            // No placeholder found, treat as literal
            AddLiteralPart(engine, result, ref index, pattern);
            return;
        }

        // Add literal before placeholder
        if (placeholderIndex > 0)
        {
            AddLiteralPart(engine, result, ref index, pattern.Substring(0, placeholderIndex));
        }

        // Add number parts
        AddNumberParts(engine, result, ref index, value, unit, CultureInfo, NumberingSystem);

        // Add literal after placeholder
        var afterPlaceholder = placeholderIndex + placeholder.Length;
        if (afterPlaceholder < pattern.Length)
        {
            AddLiteralPart(engine, result, ref index, pattern.Substring(afterPlaceholder));
        }
    }

    private static void AddLiteralPart(Engine engine, JsArray result, ref uint index, string value)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "literal");
        part.Set("value", value);
        result.SetIndexValue(index++, part, updateLength: true);
    }

    /// <summary>
    /// Adds number parts using NumberFormat (per ECMA-402 17.5.2 step 11: use PartitionNumberPattern).
    /// </summary>
    private void AddNumberParts(Engine engine, JsArray result, ref uint index, double value, string unit, CultureInfo cultureInfo, string numberingSystem)
    {
        // Use NumberFormat to get properly formatted parts with grouping separators
        var numberParts = NumberFormat.FormatToParts(value);

        // Copy parts from NumberFormat, adding "unit" property to each
        foreach (var numberPart in numberParts)
        {
            var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            part.Set("type", numberPart.Type);
            part.Set("value", numberPart.Value);
            part.Set("unit", unit);
            result.SetIndexValue(index++, part, updateLength: true);
        }
    }

    private string? GetSpecialPhrase(long value, string unit, bool isPast)
    {
        // Try to get special phrase from CLDR provider
        var phrase = CldrProvider.GetRelativeTimeSpecialPhrase(Locale, unit, (int) value, isPast, Style);
        if (phrase != null)
        {
            return phrase;
        }

        // Fallback to hardcoded English phrases
        if (!Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // English special phrases per CLDR
        // second/minute/hour only have special form for 0
        // day/week/month/quarter/year have special forms for -1, 0, 1
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
            if (isPast)
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

    /// <summary>
    /// Gets the plural form for a value using simplified plural rules.
    /// This implements a subset of CLDR plural rules for the locales we support.
    /// </summary>
    private string GetPluralForm(double value)
    {
        // Get integer and fraction parts
        var i = (long) System.Math.Abs(System.Math.Truncate(value));
        var v = GetFractionDigitCount(value);

        // Extract language code from locale
        var language = Locale;
        var dashIndex = Locale.IndexOf('-');
        if (dashIndex > 0)
        {
            language = Locale.Substring(0, dashIndex).ToLowerInvariant();
        }
        else
        {
            language = Locale.ToLowerInvariant();
        }

        // Apply language-specific plural rules (cardinal)
        // Based on CLDR plural rules: https://cldr.unicode.org/index/cldr-spec/plural-rules
        switch (language)
        {
            case "pl": // Polish
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
                if (mod10 == 0 || mod10 == 1 || (mod10 >= 5 && mod10 <= 9) || (mod100 >= 12 && mod100 <= 14))
                {
                    return "many";
                }
                return "other";

            case "en": // English
            case "de": // German
            case "es": // Spanish
            default:
                // Simple rule: one if i == 1 and v == 0, otherwise other
                if (i == 1 && v == 0)
                {
                    return "one";
                }
                return "other";
        }
    }

    /// <summary>
    /// Gets the number of fraction digits in a value.
    /// </summary>
    private static int GetFractionDigitCount(double value)
    {
        var absValue = System.Math.Abs(value);
        var intPart = System.Math.Truncate(absValue);
        var fracPart = absValue - intPart;

        if (fracPart == 0)
        {
            return 0;
        }

        var fracStr = fracPart.ToString("0.###############", CultureInfo.InvariantCulture);
        if (fracStr.StartsWith("0.", StringComparison.Ordinal))
        {
            return fracStr.Length - 2; // Subtract "0."
        }

        return 0;
    }

    private string GetUnitName(string unit, bool plural)
    {
        var isShort = string.Equals(Style, "short", StringComparison.Ordinal);
        var isNarrow = string.Equals(Style, "narrow", StringComparison.Ordinal);

        if (isNarrow)
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

        if (isShort)
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
}

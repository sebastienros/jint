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
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        NumberingSystem = numberingSystem;
        Style = style;
        Numeric = numeric;
        CultureInfo = cultureInfo;
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

        // Format the number with grouping separators
        var formattedNumber = absValue.ToString("#,##0.###", CultureInfo);

        // Try to get patterns from CLDR provider
        var patterns = CldrProvider.GetRelativeTimePatterns(Locale, unit, Style);
        if (patterns != null)
        {
            var plural = absValue != 1;
            var pattern = isPast
                ? (plural ? patterns.PastPlural : patterns.Past)
                : (plural ? patterns.FuturePlural : patterns.Future);

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
            var plural = absValue != 1;
            var pattern = isPast
                ? (plural ? patterns.PastPlural : patterns.Past)
                : (plural ? patterns.FuturePlural : patterns.Future);

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
    /// Adds number parts decomposed into integer and group separator parts.
    /// </summary>
    private static void AddNumberParts(Engine engine, JsArray result, ref uint index, double value, string unit, CultureInfo cultureInfo, string numberingSystem)
    {
        var integerPart = (long) System.Math.Truncate(value);
        var fractionPart = value - integerPart;

        // Format integer part with grouping
        var intStr = integerPart.ToString(CultureInfo.InvariantCulture);
        var groupSeparator = cultureInfo.NumberFormat.NumberGroupSeparator;
        var groupSize = 3;

        if (intStr.Length <= groupSize)
        {
            // No grouping needed
            AddIntegerPart(engine, result, ref index, Data.NumberingSystemData.TransliterateDigits(intStr, numberingSystem), unit);
        }
        else
        {
            // Add grouped integer parts
            var position = intStr.Length % groupSize;
            if (position == 0) position = groupSize;

            var currentGroup = intStr.Substring(0, position);
            AddIntegerPart(engine, result, ref index, Data.NumberingSystemData.TransliterateDigits(currentGroup, numberingSystem), unit);

            while (position < intStr.Length)
            {
                AddGroupPart(engine, result, ref index, groupSeparator, unit);
                currentGroup = intStr.Substring(position, groupSize);
                AddIntegerPart(engine, result, ref index, Data.NumberingSystemData.TransliterateDigits(currentGroup, numberingSystem), unit);
                position += groupSize;
            }
        }

        // Handle fraction part if present
        if (fractionPart > 0)
        {
            var decimalSeparator = cultureInfo.NumberFormat.NumberDecimalSeparator;
            AddDecimalPart(engine, result, ref index, decimalSeparator, unit);

            // Format fraction digits (up to 3)
            var fractionStr = fractionPart.ToString("0.###", CultureInfo.InvariantCulture);
            if (fractionStr.StartsWith("0.", StringComparison.Ordinal))
            {
                fractionStr = fractionStr.Substring(2);
            }
            AddFractionPart(engine, result, ref index, Data.NumberingSystemData.TransliterateDigits(fractionStr, numberingSystem), unit);
        }
    }

    private static void AddIntegerPart(Engine engine, JsArray result, ref uint index, string value, string unit)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "integer");
        part.Set("value", value);
        part.Set("unit", unit);
        result.SetIndexValue(index++, part, updateLength: true);
    }

    private static void AddGroupPart(Engine engine, JsArray result, ref uint index, string value, string unit)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "group");
        part.Set("value", value);
        part.Set("unit", unit);
        result.SetIndexValue(index++, part, updateLength: true);
    }

    private static void AddDecimalPart(Engine engine, JsArray result, ref uint index, string value, string unit)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "decimal");
        part.Set("value", value);
        part.Set("unit", unit);
        result.SetIndexValue(index++, part, updateLength: true);
    }

    private static void AddFractionPart(Engine engine, JsArray result, ref uint index, string value, string unit)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "fraction");
        part.Set("value", value);
        part.Set("unit", unit);
        result.SetIndexValue(index++, part, updateLength: true);
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

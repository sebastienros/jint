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
        RelativeTimeFormatPrototype prototype,
        string locale,
        string style,
        string numeric,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Style = style;
        Numeric = numeric;
        CultureInfo = cultureInfo;
    }

    /// <summary>
    /// The locale used for formatting.
    /// </summary>
    internal string Locale { get; }

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
    /// Formats a relative time value.
    /// </summary>
    internal string Format(double value, string unit)
    {
        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var absValue = System.Math.Abs(value);
        var isPast = value < 0;
        var intValue = (long) System.Math.Round(absValue);

        // Handle "auto" numeric - special phrases for -1, 0, 1
        if (string.Equals(Numeric, "auto", StringComparison.Ordinal) && isEnglish)
        {
            var specialPhrase = GetSpecialPhrase(intValue, unit, isPast);
            if (specialPhrase != null)
            {
                return specialPhrase;
            }
        }

        // Format the number
        var formattedNumber = absValue.ToString(CultureInfo);

        // Get the unit name
        var unitName = GetUnitName(unit, absValue != 1);

        // Construct the relative time string
        if (isEnglish)
        {
            if (isPast)
            {
                return $"{formattedNumber} {unitName} ago";
            }
            return $"in {formattedNumber} {unitName}";
        }

        // For non-English, use a simple format
        if (isPast)
        {
            return $"-{formattedNumber} {unitName}";
        }
        return $"+{formattedNumber} {unitName}";
    }

    /// <summary>
    /// Formats a relative time value and returns parts.
    /// </summary>
    internal JsArray FormatToParts(Engine engine, double value, string unit)
    {
        var result = new JsArray(engine);
        uint index = 0;

        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var absValue = System.Math.Abs(value);
        var isPast = value < 0;
        var intValue = (long) System.Math.Round(absValue);

        // Handle "auto" numeric - special phrases
        if (string.Equals(Numeric, "auto", StringComparison.Ordinal) && isEnglish)
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

        var formattedNumber = absValue.ToString(CultureInfo);
        var unitName = GetUnitName(unit, absValue != 1);

        if (isEnglish)
        {
            if (isPast)
            {
                // "X units ago"
                AddIntegerPart(engine, result, ref index, formattedNumber, unit);
                AddLiteralPart(engine, result, ref index, " ");
                AddLiteralPart(engine, result, ref index, unitName);
                AddLiteralPart(engine, result, ref index, " ago");
            }
            else
            {
                // "in X units"
                AddLiteralPart(engine, result, ref index, "in ");
                AddIntegerPart(engine, result, ref index, formattedNumber, unit);
                AddLiteralPart(engine, result, ref index, " ");
                AddLiteralPart(engine, result, ref index, unitName);
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
            AddIntegerPart(engine, result, ref index, formattedNumber, unit);
            AddLiteralPart(engine, result, ref index, " ");
            AddLiteralPart(engine, result, ref index, unitName);
        }

        return result;
    }

    private static void AddLiteralPart(Engine engine, JsArray result, ref uint index, string value)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "literal");
        part.Set("value", value);
        result.SetIndexValue(index++, part, updateLength: true);
    }

    private static void AddIntegerPart(Engine engine, JsArray result, ref uint index, string value, string unit)
    {
        var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
        part.Set("type", "integer");
        part.Set("value", value);
        part.Set("unit", unit);
        result.SetIndexValue(index++, part, updateLength: true);
    }

    private static string? GetSpecialPhrase(long value, string unit, bool isPast)
    {
        // English special phrases for common cases
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
                    "second" => "1 second ago",
                    "minute" => "1 minute ago",
                    "hour" => "1 hour ago",
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
                "second" => "in 1 second",
                "minute" => "in 1 minute",
                "hour" => "in 1 hour",
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

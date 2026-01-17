using System.Globalization;
using System.Text;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/proposal-intl-duration-format/
/// Represents an Intl.DurationFormat instance for locale-aware duration formatting.
/// </summary>
internal sealed class JsDurationFormat : ObjectInstance
{
    internal JsDurationFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string style,
        string numberingSystem,
        CultureInfo cultureInfo,
        // Unit styles
        string yearsStyle,
        string monthsStyle,
        string weeksStyle,
        string daysStyle,
        string hoursStyle,
        string minutesStyle,
        string secondsStyle,
        string millisecondsStyle,
        string microsecondsStyle,
        string nanosecondsStyle,
        // Unit displays
        string yearsDisplay,
        string monthsDisplay,
        string weeksDisplay,
        string daysDisplay,
        string hoursDisplay,
        string minutesDisplay,
        string secondsDisplay,
        string millisecondsDisplay,
        string microsecondsDisplay,
        string nanosecondsDisplay,
        // Fractional digits
        int? fractionalDigits) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Style = style;
        NumberingSystem = numberingSystem;
        CultureInfo = cultureInfo;

        YearsStyle = yearsStyle;
        MonthsStyle = monthsStyle;
        WeeksStyle = weeksStyle;
        DaysStyle = daysStyle;
        HoursStyle = hoursStyle;
        MinutesStyle = minutesStyle;
        SecondsStyle = secondsStyle;
        MillisecondsStyle = millisecondsStyle;
        MicrosecondsStyle = microsecondsStyle;
        NanosecondsStyle = nanosecondsStyle;

        YearsDisplay = yearsDisplay;
        MonthsDisplay = monthsDisplay;
        WeeksDisplay = weeksDisplay;
        DaysDisplay = daysDisplay;
        HoursDisplay = hoursDisplay;
        MinutesDisplay = minutesDisplay;
        SecondsDisplay = secondsDisplay;
        MillisecondsDisplay = millisecondsDisplay;
        MicrosecondsDisplay = microsecondsDisplay;
        NanosecondsDisplay = nanosecondsDisplay;

        FractionalDigits = fractionalDigits;
    }

    /// <summary>
    /// The locale used for formatting.
    /// </summary>
    internal string Locale { get; }

    /// <summary>
    /// The style: "long", "short", "narrow", or "digital".
    /// </summary>
    internal string Style { get; }

    /// <summary>
    /// The numbering system.
    /// </summary>
    internal string NumberingSystem { get; }

    /// <summary>
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

    // Unit styles
    internal string YearsStyle { get; }
    internal string MonthsStyle { get; }
    internal string WeeksStyle { get; }
    internal string DaysStyle { get; }
    internal string HoursStyle { get; }
    internal string MinutesStyle { get; }
    internal string SecondsStyle { get; }
    internal string MillisecondsStyle { get; }
    internal string MicrosecondsStyle { get; }
    internal string NanosecondsStyle { get; }

    // Unit displays
    internal string YearsDisplay { get; }
    internal string MonthsDisplay { get; }
    internal string WeeksDisplay { get; }
    internal string DaysDisplay { get; }
    internal string HoursDisplay { get; }
    internal string MinutesDisplay { get; }
    internal string SecondsDisplay { get; }
    internal string MillisecondsDisplay { get; }
    internal string MicrosecondsDisplay { get; }
    internal string NanosecondsDisplay { get; }

    // Fractional digits for sub-second units
    internal int? FractionalDigits { get; }

    /// <summary>
    /// Formats a duration object.
    /// </summary>
    internal string Format(DurationRecord duration)
    {
        var isDigital = string.Equals(Style, "digital", StringComparison.Ordinal);

        if (isDigital)
        {
            return FormatDigital(duration);
        }

        return FormatNonDigital(duration);
    }

    private string FormatDigital(DurationRecord duration)
    {
        var parts = new List<string>();

        // Check if the duration is negative
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;
        var displayNegativeSign = isNegative;

        // For digital style, years/months/weeks/days are formatted with unit labels (per Table 1: "short" style)
        void AddDateUnitIfNeeded(long value, string display, string singularLong, string pluralLong, string shortForm)
        {
            if (value == 0 && !string.Equals(display, "always", StringComparison.Ordinal))
            {
                return;
            }

            var absValue = System.Math.Abs(value);
            var prefix = displayNegativeSign ? "-" : "";
            displayNegativeSign = false;

            // Digital style uses "short" for date units per Table 1
            parts.Add($"{prefix}{absValue} {shortForm}");
        }

        // Add date units with their labels
        AddDateUnitIfNeeded(duration.Years, YearsDisplay, "year", "years", "yr");
        AddDateUnitIfNeeded(duration.Months, MonthsDisplay, "month", "months", "mo");
        AddDateUnitIfNeeded(duration.Weeks, WeeksDisplay, "week", "weeks", "wk");
        // For days, short form is always "day" (not "days") per CLDR
        AddDateUnitIfNeeded(duration.Days, DaysDisplay, "day", "days", "day");

        // Now format the digital time part (HH:MM:SS)
        var sb = new StringBuilder();

        var hours = System.Math.Abs(duration.Hours);
        var minutes = System.Math.Abs(duration.Minutes);
        var seconds = System.Math.Abs(duration.Seconds);
        var milliseconds = System.Math.Abs(duration.Milliseconds);
        var microseconds = System.Math.Abs(duration.Microseconds);
        var nanoseconds = System.Math.Abs(duration.Nanoseconds);

        // Add negative sign if this is the first displayed element
        if (displayNegativeSign)
        {
            sb.Append('-');
            displayNegativeSign = false;
        }

        // Add hours if non-zero or if hoursDisplay is "always"
        var showHours = duration.Hours != 0 || string.Equals(HoursDisplay, "always", StringComparison.Ordinal);

        if (showHours)
        {
            sb.Append(hours);
            sb.Append(':');
        }

        // Minutes always 2-digit in digital style
        sb.Append(minutes.ToString("D2", CultureInfo.InvariantCulture));

        sb.Append(':');
        sb.Append(seconds.ToString("D2", CultureInfo.InvariantCulture));

        // Add fractional seconds if needed
        if (milliseconds > 0 || microseconds > 0 || nanoseconds > 0 || FractionalDigits.HasValue)
        {
            var totalNanos = milliseconds * 1_000_000 + microseconds * 1000 + nanoseconds;

            if (FractionalDigits.HasValue)
            {
                var digits = FractionalDigits.Value;
                if (digits > 0)
                {
                    var fraction = totalNanos.ToString("D9", CultureInfo.InvariantCulture);
                    sb.Append('.');
                    var len = System.Math.Min(digits, fraction.Length);
                    for (var i = 0; i < len; i++)
                    {
                        sb.Append(fraction[i]);
                    }
                }
            }
            else if (totalNanos > 0)
            {
                var fraction = totalNanos.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                if (fraction.Length > 0)
                {
                    sb.Append('.');
                    sb.Append(fraction);
                }
            }
        }

        parts.Add(sb.ToString());

        return string.Join(", ", parts);
    }

    private string FormatNonDigital(DurationRecord duration)
    {
        var parts = new List<string>();

        // Check if the duration is negative (any non-zero component is negative)
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;

        // Track whether we've shown the negative sign yet
        var displayNegativeSign = isNegative;

        void AddUnitIfNeeded(long value, string display, string singularLong, string pluralLong, string shortForm, string narrowForm, string unitStyle)
        {
            if (!ShouldShowUnit(value, display))
            {
                return;
            }

            // Use absolute value for the number, handle sign separately
            var absValue = System.Math.Abs(value);

            // If this is the first unit and duration is negative, show negative sign
            // Even if value is 0, we show -0 for the first unit of a negative duration
            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                if (value == 0)
                {
                    // Format as -0 (negative zero)
                    parts.Add(FormatUnitWithSign(absValue, singularLong, pluralLong, shortForm, narrowForm, unitStyle, isNegativeZero: true));
                }
                else
                {
                    // Format with the negative sign
                    parts.Add(FormatUnitWithSign(absValue, singularLong, pluralLong, shortForm, narrowForm, unitStyle, isNegative: true));
                }
            }
            else
            {
                // Subsequent units use absolute value without sign
                parts.Add(FormatUnit(absValue, singularLong, pluralLong, shortForm, narrowForm, unitStyle));
            }
        }

        void AddSubSecondUnitIfNeeded(long value, string display, string singular, string plural, string unitStyle)
        {
            if (!ShouldShowUnit(value, display))
            {
                return;
            }

            var absValue = System.Math.Abs(value);

            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                if (value == 0)
                {
                    parts.Add(FormatSubSecondUnitWithSign(absValue, singular, plural, unitStyle, isNegativeZero: true));
                }
                else
                {
                    parts.Add(FormatSubSecondUnitWithSign(absValue, singular, plural, unitStyle, isNegative: true));
                }
            }
            else
            {
                parts.Add(FormatSubSecondUnit(absValue, singular, plural, unitStyle));
            }
        }

        // Years
        AddUnitIfNeeded(duration.Years, YearsDisplay, "year", "years", "yr", "y", YearsStyle);

        // Months
        AddUnitIfNeeded(duration.Months, MonthsDisplay, "month", "months", "mo", "M", MonthsStyle);

        // Weeks
        AddUnitIfNeeded(duration.Weeks, WeeksDisplay, "week", "weeks", "wk", "w", WeeksStyle);

        // Days
        AddUnitIfNeeded(duration.Days, DaysDisplay, "day", "days", "day", "d", DaysStyle);

        // Hours
        AddUnitIfNeeded(duration.Hours, HoursDisplay, "hour", "hours", "hr", "h", HoursStyle);

        // Minutes - note: narrow form is also "min" not "m"
        AddUnitIfNeeded(duration.Minutes, MinutesDisplay, "minute", "minutes", "min", "min", MinutesStyle);

        // Seconds
        AddUnitIfNeeded(duration.Seconds, SecondsDisplay, "second", "seconds", "sec", "s", SecondsStyle);

        // Milliseconds - CLDR short form is same as long form for sub-seconds
        AddSubSecondUnitIfNeeded(duration.Milliseconds, MillisecondsDisplay, "millisecond", "milliseconds", MillisecondsStyle);

        // Microseconds
        AddSubSecondUnitIfNeeded(duration.Microseconds, MicrosecondsDisplay, "microsecond", "microseconds", MicrosecondsStyle);

        // Nanoseconds
        AddSubSecondUnitIfNeeded(duration.Nanoseconds, NanosecondsDisplay, "nanosecond", "nanoseconds", NanosecondsStyle);

        if (parts.Count == 0)
        {
            // If all units are zero and their display is "auto", return empty string
            // Only show "0 seconds" if at least one unit has display "always"
            if (HasAnyAlwaysDisplay())
            {
                return FormatUnit(0, "second", "seconds", "sec", "s", SecondsStyle);
            }
            return "";
        }

        // Join parts based on style
        if (string.Equals(Style, "narrow", StringComparison.Ordinal))
        {
            return string.Join(" ", parts);
        }

        // For long and short, use comma-separated
        // Note: The test harness expects comma separation, not "and" conjunction
        return string.Join(", ", parts);
    }

    private static bool ShouldShowUnit(long value, string display)
    {
        if (string.Equals(display, "always", StringComparison.Ordinal))
        {
            return true;
        }
        // "auto" - only show if non-zero
        return value != 0;
    }

    private bool HasAnyAlwaysDisplay()
    {
        return string.Equals(YearsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MonthsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(WeeksDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(DaysDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(HoursDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MinutesDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(SecondsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MillisecondsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(MicrosecondsDisplay, "always", StringComparison.Ordinal) ||
               string.Equals(NanosecondsDisplay, "always", StringComparison.Ordinal);
    }

    private static string FormatUnit(long value, string singularLong, string pluralLong, string shortForm, string narrowForm, string unitStyle)
    {
        var isPlural = System.Math.Abs(value) != 1;

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return value.ToString("D2", CultureInfo.InvariantCulture);
        }

        return unitStyle switch
        {
            "long" => $"{value} {(isPlural ? pluralLong : singularLong)}",
            "short" => $"{value} {shortForm}",
            "narrow" => $"{value}{narrowForm}",
            _ => $"{value} {shortForm}" // Default to short
        };
    }

    private static string FormatUnitWithSign(long absValue, string singularLong, string pluralLong, string shortForm, string narrowForm, string unitStyle, bool isNegative = false, bool isNegativeZero = false)
    {
        var isPlural = absValue != 1;
        var prefix = (isNegative || isNegativeZero) ? "-" : "";

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return $"{prefix}{absValue}";
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return $"{prefix}{absValue:D2}";
        }

        return unitStyle switch
        {
            "long" => $"{prefix}{absValue} {(isPlural ? pluralLong : singularLong)}",
            "short" => $"{prefix}{absValue} {shortForm}",
            "narrow" => $"{prefix}{absValue}{narrowForm}",
            _ => $"{prefix}{absValue} {shortForm}" // Default to short
        };
    }

    /// <summary>
    /// Formats sub-second units (milliseconds, microseconds, nanoseconds).
    /// CLDR uses singular form for short/narrow and proper plural for long.
    /// </summary>
    private static string FormatSubSecondUnit(long value, string singular, string plural, string unitStyle)
    {
        var isPlural = System.Math.Abs(value) != 1;

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return value.ToString("D2", CultureInfo.InvariantCulture);
        }

        // For sub-second units:
        // - long uses proper plural
        // - short/narrow use singular form always
        return unitStyle switch
        {
            "long" => $"{value} {(isPlural ? plural : singular)}",
            "short" => $"{value} {singular}",
            "narrow" => $"{value}{singular}", // narrow has no space
            _ => $"{value} {singular}" // Default to short (singular)
        };
    }

    private static string FormatSubSecondUnitWithSign(long absValue, string singular, string plural, string unitStyle, bool isNegative = false, bool isNegativeZero = false)
    {
        var isPlural = absValue != 1;
        var prefix = (isNegative || isNegativeZero) ? "-" : "";

        // Handle numeric and 2-digit styles
        if (string.Equals(unitStyle, "numeric", StringComparison.Ordinal))
        {
            return $"{prefix}{absValue}";
        }

        if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
        {
            return $"{prefix}{absValue:D2}";
        }

        // For sub-second units:
        // - long uses proper plural
        // - short/narrow use singular form always
        return unitStyle switch
        {
            "long" => $"{prefix}{absValue} {(isPlural ? plural : singular)}",
            "short" => $"{prefix}{absValue} {singular}",
            "narrow" => $"{prefix}{absValue}{singular}", // narrow has no space
            _ => $"{prefix}{absValue} {singular}" // Default to short (singular)
        };
    }

    /// <summary>
    /// Formats a duration object and returns parts.
    /// </summary>
    internal JsArray FormatToParts(Engine engine, DurationRecord duration)
    {
        var isDigital = string.Equals(Style, "digital", StringComparison.Ordinal);

        if (isDigital)
        {
            return FormatToPartsDigital(engine, duration);
        }

        return FormatToPartsNonDigital(engine, duration);
    }

    private JsArray FormatToPartsDigital(Engine engine, DurationRecord duration)
    {
        var result = new JsArray(engine);
        uint index = 0;

        // Check if the duration is negative
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;

        // Use absolute values for formatting
        var hours = System.Math.Abs(duration.Hours);
        var minutes = System.Math.Abs(duration.Minutes);
        var seconds = System.Math.Abs(duration.Seconds);

        void AddPart(string type, string value, string? unit = null)
        {
            var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            part.Set("type", type);
            part.Set("value", value);
            if (unit != null)
            {
                part.Set("unit", unit);
            }
            result.SetIndexValue(index++, part, updateLength: true);
        }

        // Add minus sign for negative durations
        if (isNegative)
        {
            AddPart("minusSign", "-", "hour");
        }

        // Hours
        var showHours = duration.Hours != 0 || string.Equals(HoursDisplay, "always", StringComparison.Ordinal);
        if (showHours)
        {
            AddPart("integer", hours.ToString(CultureInfo.InvariantCulture), "hour");
            AddPart("literal", ":");
        }

        // Minutes
        AddPart("integer", minutes.ToString("D2", CultureInfo.InvariantCulture), "minute");
        AddPart("literal", ":");

        // Seconds
        AddPart("integer", seconds.ToString("D2", CultureInfo.InvariantCulture), "second");

        return result;
    }

    private JsArray FormatToPartsNonDigital(Engine engine, DurationRecord duration)
    {
        var result = new JsArray(engine);
        uint index = 0;

        // Check if the duration is negative
        var isNegative = duration.Years < 0 || duration.Months < 0 || duration.Weeks < 0 ||
                        duration.Days < 0 || duration.Hours < 0 || duration.Minutes < 0 ||
                        duration.Seconds < 0 || duration.Milliseconds < 0 ||
                        duration.Microseconds < 0 || duration.Nanoseconds < 0;
        var displayNegativeSign = isNegative;

        void AddPart(string type, string value, string? unit = null)
        {
            var part = OrdinaryObjectCreate(engine, engine.Realm.Intrinsics.Object.PrototypeObject);
            part.Set("type", type);
            part.Set("value", value);
            if (unit != null)
            {
                part.Set("unit", unit);
            }
            result.SetIndexValue(index++, part, updateLength: true);
        }

        void AddUnitParts(long unitValue, string unitName, string display, string unitStyle)
        {
            if (!ShouldShowUnit(unitValue, display))
            {
                return;
            }

            // Add separator if not first
            if (index > 0)
            {
                // Narrow style uses space separator, others use comma-space
                var separator = string.Equals(Style, "narrow", StringComparison.Ordinal) ? " " : ", ";
                AddPart("literal", separator);
            }

            // Use absolute value for formatting
            var absValue = System.Math.Abs(unitValue);

            // Add minus sign for first displayed unit of negative duration
            if (displayNegativeSign)
            {
                displayNegativeSign = false;
                AddPart("minusSign", "-", unitName);
            }

            // Add integer part
            string valueStr;
            if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
            {
                valueStr = absValue.ToString("D2", CultureInfo.InvariantCulture);
            }
            else
            {
                valueStr = absValue.ToString(CultureInfo.InvariantCulture);
            }

            AddPart("integer", valueStr, unitName);

            // Add unit label for non-numeric styles
            if (!string.Equals(unitStyle, "numeric", StringComparison.Ordinal) &&
                !string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
            {
                // Narrow style: no space between number and unit
                // Long/short style: space between number and unit
                if (!string.Equals(unitStyle, "narrow", StringComparison.Ordinal))
                {
                    AddPart("literal", " ", unitName);
                }
                var label = GetUnitLabel(absValue, unitName, unitStyle);
                AddPart("unit", label, unitName);
            }
        }

        // Add parts for each unit
        AddUnitParts(duration.Years, "year", YearsDisplay, YearsStyle);
        AddUnitParts(duration.Months, "month", MonthsDisplay, MonthsStyle);
        AddUnitParts(duration.Weeks, "week", WeeksDisplay, WeeksStyle);
        AddUnitParts(duration.Days, "day", DaysDisplay, DaysStyle);
        AddUnitParts(duration.Hours, "hour", HoursDisplay, HoursStyle);
        AddUnitParts(duration.Minutes, "minute", MinutesDisplay, MinutesStyle);
        AddUnitParts(duration.Seconds, "second", SecondsDisplay, SecondsStyle);
        AddUnitParts(duration.Milliseconds, "millisecond", MillisecondsDisplay, MillisecondsStyle);
        AddUnitParts(duration.Microseconds, "microsecond", MicrosecondsDisplay, MicrosecondsStyle);
        AddUnitParts(duration.Nanoseconds, "nanosecond", NanosecondsDisplay, NanosecondsStyle);

        // If no parts, add zero seconds
        if (index == 0)
        {
            AddPart("integer", "0", "second");
            AddPart("literal", " ", "second");
            AddPart("unit", GetUnitLabel(0, "second", SecondsStyle), "second");
        }

        return result;
    }

    private static string GetUnitLabel(long value, string unitName, string style)
    {
        var isPlural = System.Math.Abs(value) != 1;
        var isLong = string.Equals(style, "long", StringComparison.Ordinal);
        var isShort = string.Equals(style, "short", StringComparison.Ordinal);
        var isNarrow = string.Equals(style, "narrow", StringComparison.Ordinal);

        if (string.Equals(unitName, "year", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "years" : "year";
            if (isShort) return "yr";
            if (isNarrow) return "y";
        }
        else if (string.Equals(unitName, "month", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "months" : "month";
            if (isShort) return "mo";
            if (isNarrow) return "M";
        }
        else if (string.Equals(unitName, "week", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "weeks" : "week";
            if (isShort) return "wk";
            if (isNarrow) return "w";
        }
        else if (string.Equals(unitName, "day", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "days" : "day";
            if (isShort) return "day";
            if (isNarrow) return "d";
        }
        else if (string.Equals(unitName, "hour", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "hours" : "hour";
            if (isShort) return "hr";
            if (isNarrow) return "h";
            return "hr";
        }
        else if (string.Equals(unitName, "minute", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "minutes" : "minute";
            // Both short and narrow use "min" for minute
            return "min";
        }
        else if (string.Equals(unitName, "second", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "seconds" : "second";
            if (isShort) return "sec";
            if (isNarrow) return "s";
            return "sec";
        }
        else if (string.Equals(unitName, "millisecond", StringComparison.Ordinal))
        {
            // long uses plural, short/narrow use singular
            if (isLong) return isPlural ? "milliseconds" : "millisecond";
            return "millisecond"; // short and narrow always singular
        }
        else if (string.Equals(unitName, "microsecond", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "microseconds" : "microsecond";
            return "microsecond";
        }
        else if (string.Equals(unitName, "nanosecond", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "nanoseconds" : "nanosecond";
            return "nanosecond";
        }

        return unitName;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    internal struct DurationRecord
    {
        public long Years;
        public long Months;
        public long Weeks;
        public long Days;
        public long Hours;
        public long Minutes;
        public long Seconds;
        public long Milliseconds;
        public long Microseconds;
        public long Nanoseconds;
    }
}

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
        DurationFormatPrototype prototype,
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
        var sb = new StringBuilder();

        // Format: [D:]HH:MM:SS[.mmm]
        if (duration.Days > 0)
        {
            sb.Append(duration.Days);
            sb.Append(':');
        }

        var hours = duration.Hours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;
        var milliseconds = duration.Milliseconds;
        var microseconds = duration.Microseconds;
        var nanoseconds = duration.Nanoseconds;

        // Add hours if we have days or hours, or if hoursDisplay is "always"
        var showHours = duration.Days > 0 || hours > 0 ||
                        string.Equals(HoursDisplay, "always", StringComparison.Ordinal);

        if (showHours)
        {
            if (duration.Days > 0 || string.Equals(HoursStyle, "2-digit", StringComparison.Ordinal))
            {
                sb.Append(hours.ToString("D2", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(hours);
            }
            sb.Append(':');
        }

        // Minutes (with leading zero if hours shown)
        if (showHours || string.Equals(MinutesStyle, "2-digit", StringComparison.Ordinal))
        {
            sb.Append(minutes.ToString("D2", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(minutes);
        }

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

        return sb.ToString();
    }

    private string FormatNonDigital(DurationRecord duration)
    {
        var parts = new List<string>();

        // Years
        if (ShouldShowUnit(duration.Years, YearsDisplay))
        {
            parts.Add(FormatUnit(duration.Years, "year", "years", "yr", "y", YearsStyle));
        }

        // Months
        if (ShouldShowUnit(duration.Months, MonthsDisplay))
        {
            parts.Add(FormatUnit(duration.Months, "month", "months", "mo", "mo", MonthsStyle));
        }

        // Weeks
        if (ShouldShowUnit(duration.Weeks, WeeksDisplay))
        {
            parts.Add(FormatUnit(duration.Weeks, "week", "weeks", "wk", "w", WeeksStyle));
        }

        // Days
        if (ShouldShowUnit(duration.Days, DaysDisplay))
        {
            parts.Add(FormatUnit(duration.Days, "day", "days", "day", "d", DaysStyle));
        }

        // Hours
        if (ShouldShowUnit(duration.Hours, HoursDisplay))
        {
            parts.Add(FormatUnit(duration.Hours, "hour", "hours", "hr", "h", HoursStyle));
        }

        // Minutes
        if (ShouldShowUnit(duration.Minutes, MinutesDisplay))
        {
            parts.Add(FormatUnit(duration.Minutes, "minute", "minutes", "min", "m", MinutesStyle));
        }

        // Seconds
        if (ShouldShowUnit(duration.Seconds, SecondsDisplay))
        {
            parts.Add(FormatUnit(duration.Seconds, "second", "seconds", "sec", "s", SecondsStyle));
        }

        // Milliseconds
        if (ShouldShowUnit(duration.Milliseconds, MillisecondsDisplay))
        {
            parts.Add(FormatUnit(duration.Milliseconds, "millisecond", "milliseconds", "ms", "ms", MillisecondsStyle));
        }

        // Microseconds
        if (ShouldShowUnit(duration.Microseconds, MicrosecondsDisplay))
        {
            parts.Add(FormatUnit(duration.Microseconds, "microsecond", "microseconds", "μs", "μs", MicrosecondsStyle));
        }

        // Nanoseconds
        if (ShouldShowUnit(duration.Nanoseconds, NanosecondsDisplay))
        {
            parts.Add(FormatUnit(duration.Nanoseconds, "nanosecond", "nanoseconds", "ns", "ns", NanosecondsStyle));
        }

        if (parts.Count == 0)
        {
            return FormatUnit(0, "second", "seconds", "sec", "s", SecondsStyle);
        }

        // Join parts based on style
        if (string.Equals(Style, "narrow", StringComparison.Ordinal))
        {
            return string.Join(" ", parts);
        }

        // For long and short, use comma-separated with "and" or ","
        if (parts.Count == 1)
        {
            return parts[0];
        }

        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        if (parts.Count == 2 && isEnglish && string.Equals(Style, "long", StringComparison.Ordinal))
        {
            return $"{parts[0]} and {parts[1]}";
        }

        // Use comma separation
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

    /// <summary>
    /// Formats a duration object and returns parts.
    /// </summary>
    internal JsArray FormatToParts(Engine engine, DurationRecord duration)
    {
        var result = new JsArray(engine);
        uint index = 0;

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
                AddPart("literal", " ");
            }

            // Add integer part
            string valueStr;
            if (string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
            {
                valueStr = unitValue.ToString("D2", CultureInfo.InvariantCulture);
            }
            else
            {
                valueStr = unitValue.ToString(CultureInfo.InvariantCulture);
            }

            AddPart("integer", valueStr, unitName);

            // Add unit label for non-numeric styles
            if (!string.Equals(unitStyle, "numeric", StringComparison.Ordinal) &&
                !string.Equals(unitStyle, "2-digit", StringComparison.Ordinal))
            {
                AddPart("literal", " ");
                var label = GetUnitLabel(unitValue, unitName, unitStyle);
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
            AddPart("literal", " ");
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
            return "mo"; // short and narrow
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
        }
        else if (string.Equals(unitName, "minute", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "minutes" : "minute";
            if (isShort) return "min";
            if (isNarrow) return "m";
        }
        else if (string.Equals(unitName, "second", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "seconds" : "second";
            if (isShort) return "sec";
            if (isNarrow) return "s";
        }
        else if (string.Equals(unitName, "millisecond", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "milliseconds" : "millisecond";
            return "ms";
        }
        else if (string.Equals(unitName, "microsecond", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "microseconds" : "microsecond";
            return "μs";
        }
        else if (string.Equals(unitName, "nanosecond", StringComparison.Ordinal))
        {
            if (isLong) return isPlural ? "nanoseconds" : "nanosecond";
            return "ns";
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

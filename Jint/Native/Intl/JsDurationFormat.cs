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
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Style = style;
        CultureInfo = cultureInfo;
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
    /// The .NET CultureInfo for locale-specific formatting.
    /// </summary>
    internal CultureInfo CultureInfo { get; }

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

    private static string FormatDigital(DurationRecord duration)
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

        // Add hours if we have days or hours
        if (duration.Days > 0 || hours > 0)
        {
            if (duration.Days > 0)
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
        if (duration.Days > 0 || hours > 0)
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
        if (milliseconds > 0 || microseconds > 0 || nanoseconds > 0)
        {
            var totalNanos = milliseconds * 1_000_000 + microseconds * 1000 + nanoseconds;
            var fraction = totalNanos.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
            if (fraction.Length > 0)
            {
                sb.Append('.');
                sb.Append(fraction);
            }
        }

        return sb.ToString();
    }

    private string FormatNonDigital(DurationRecord duration)
    {
        var parts = new List<string>();
        var isEnglish = Locale.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        // Years
        if (duration.Years != 0)
        {
            parts.Add(FormatUnit(duration.Years, "year", "years", "yr", "y"));
        }

        // Months
        if (duration.Months != 0)
        {
            parts.Add(FormatUnit(duration.Months, "month", "months", "mo", "mo"));
        }

        // Weeks
        if (duration.Weeks != 0)
        {
            parts.Add(FormatUnit(duration.Weeks, "week", "weeks", "wk", "w"));
        }

        // Days
        if (duration.Days != 0)
        {
            parts.Add(FormatUnit(duration.Days, "day", "days", "day", "d"));
        }

        // Hours
        if (duration.Hours != 0)
        {
            parts.Add(FormatUnit(duration.Hours, "hour", "hours", "hr", "h"));
        }

        // Minutes
        if (duration.Minutes != 0)
        {
            parts.Add(FormatUnit(duration.Minutes, "minute", "minutes", "min", "m"));
        }

        // Seconds
        if (duration.Seconds != 0)
        {
            parts.Add(FormatUnit(duration.Seconds, "second", "seconds", "sec", "s"));
        }

        // Milliseconds
        if (duration.Milliseconds != 0)
        {
            parts.Add(FormatUnit(duration.Milliseconds, "millisecond", "milliseconds", "ms", "ms"));
        }

        // Microseconds
        if (duration.Microseconds != 0)
        {
            parts.Add(FormatUnit(duration.Microseconds, "microsecond", "microseconds", "μs", "μs"));
        }

        // Nanoseconds
        if (duration.Nanoseconds != 0)
        {
            parts.Add(FormatUnit(duration.Nanoseconds, "nanosecond", "nanoseconds", "ns", "ns"));
        }

        if (parts.Count == 0)
        {
            return FormatUnit(0, "second", "seconds", "sec", "s");
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

        if (parts.Count == 2 && isEnglish && string.Equals(Style, "long", StringComparison.Ordinal))
        {
            return $"{parts[0]} and {parts[1]}";
        }

        // Use comma separation
        return string.Join(", ", parts);
    }

    private string FormatUnit(long value, string singularLong, string pluralLong, string shortForm, string narrowForm)
    {
        var isPlural = System.Math.Abs(value) != 1;

        return Style switch
        {
            "long" => $"{value} {(isPlural ? pluralLong : singularLong)}",
            "short" => $"{value} {shortForm}",
            "narrow" => $"{value}{narrowForm}",
            _ => $"{value} {(isPlural ? pluralLong : singularLong)}"
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

        // For simplicity, format the whole string and return as a single literal
        // A full implementation would break down into individual parts
        var formatted = Format(duration);
        AddPart("literal", formatted);

        return result;
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

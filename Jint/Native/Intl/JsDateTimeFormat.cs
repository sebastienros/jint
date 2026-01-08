using System.Globalization;
using System.Text;
using Jint.Native.Object;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-datetimeformat-objects
/// Represents an Intl.DateTimeFormat instance with locale-aware date/time formatting.
/// </summary>
internal sealed class JsDateTimeFormat : ObjectInstance
{
    internal JsDateTimeFormat(
        Engine engine,
        DateTimeFormatPrototype prototype,
        string locale,
        string? calendar,
        string? numberingSystem,
        string? timeZone,
        string? hourCycle,
        string? dateStyle,
        string? timeStyle,
        string? weekday,
        string? era,
        string? year,
        string? month,
        string? day,
        string? dayPeriod,
        string? hour,
        string? minute,
        string? second,
        int? fractionalSecondDigits,
        string? timeZoneName,
        DateTimeFormatInfo dateTimeFormatInfo,
        CultureInfo cultureInfo) : base(engine)
    {
        _prototype = prototype;
        Locale = locale;
        Calendar = calendar;
        NumberingSystem = numberingSystem;
        TimeZone = timeZone;
        HourCycle = hourCycle;
        DateStyle = dateStyle;
        TimeStyle = timeStyle;
        Weekday = weekday;
        Era = era;
        Year = year;
        Month = month;
        Day = day;
        DayPeriod = dayPeriod;
        Hour = hour;
        Minute = minute;
        Second = second;
        FractionalSecondDigits = fractionalSecondDigits;
        TimeZoneName = timeZoneName;
        DateTimeFormatInfo = dateTimeFormatInfo;
        CultureInfo = cultureInfo;
    }

    internal string Locale { get; }
    internal string? Calendar { get; }
    internal string? NumberingSystem { get; }
    internal string? TimeZone { get; }
    internal string? HourCycle { get; }
    internal string? DateStyle { get; }
    internal string? TimeStyle { get; }
    internal string? Weekday { get; }
    internal string? Era { get; }
    internal string? Year { get; }
    internal string? Month { get; }
    internal string? Day { get; }
    internal string? DayPeriod { get; }
    internal string? Hour { get; }
    internal string? Minute { get; }
    internal string? Second { get; }
    internal int? FractionalSecondDigits { get; }
    internal string? TimeZoneName { get; }
    internal DateTimeFormatInfo DateTimeFormatInfo { get; }
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Formats a date according to the formatter's locale and options.
    /// </summary>
    internal string Format(DateTime dateTime)
    {
        // If dateStyle or timeStyle is specified, use those
        if (DateStyle != null || TimeStyle != null)
        {
            return FormatWithStyles(dateTime);
        }

        // Otherwise build format from component options
        return FormatWithComponents(dateTime);
    }

    private string FormatWithStyles(DateTime dateTime)
    {
        // When both dateStyle and timeStyle are specified, combine them appropriately
        if (DateStyle != null && TimeStyle != null)
        {
            // Use "F" format for full date/time, "G" for mixed
            var dateIsLong = string.Equals(DateStyle, "full", StringComparison.Ordinal) ||
                             string.Equals(DateStyle, "long", StringComparison.Ordinal);
            var timeIsLong = string.Equals(TimeStyle, "full", StringComparison.Ordinal) ||
                             string.Equals(TimeStyle, "long", StringComparison.Ordinal) ||
                             string.Equals(TimeStyle, "medium", StringComparison.Ordinal);

            if (dateIsLong && timeIsLong)
            {
                // Full date + long time
                return dateTime.ToString("F", CultureInfo);
            }
            else if (dateIsLong)
            {
                // Full date + short time
                return dateTime.ToString("f", CultureInfo);
            }
            else
            {
                // Short date + time
                return dateTime.ToString("G", CultureInfo);
            }
        }

        if (DateStyle != null)
        {
            var format = DateStyle switch
            {
                "full" => "D",  // Full date pattern
                "long" => "D",  // Long date pattern
                "medium" => "d", // Short date pattern (medium in JS)
                "short" => "d",  // Short date pattern
                _ => "d"
            };
            return dateTime.ToString(format, CultureInfo);
        }

        if (TimeStyle != null)
        {
            var format = TimeStyle switch
            {
                "full" => "T",   // Long time pattern
                "long" => "T",   // Long time pattern
                "medium" => "T", // Long time pattern
                "short" => "t",  // Short time pattern
                _ => "t"
            };
            return dateTime.ToString(format, CultureInfo);
        }

        return dateTime.ToString("G", CultureInfo);
    }

    private string FormatWithComponents(DateTime dateTime)
    {
        // Build a custom format string based on component options
        var parts = new List<string>();

        // Weekday
        if (Weekday != null)
        {
            parts.Add(Weekday switch
            {
                "long" => "dddd",
                "short" => "ddd",
                "narrow" => "ddd",
                _ => "ddd"
            });
        }

        // Era (not well-supported in .NET, skip for now)

        // Year
        if (Year != null)
        {
            parts.Add(Year switch
            {
                "numeric" => "yyyy",
                "2-digit" => "yy",
                _ => "yyyy"
            });
        }

        // Month
        if (Month != null)
        {
            parts.Add(Month switch
            {
                "numeric" => "M",
                "2-digit" => "MM",
                "long" => "MMMM",
                "short" => "MMM",
                "narrow" => "MMM",
                _ => "MM"
            });
        }

        // Day
        if (Day != null)
        {
            parts.Add(Day switch
            {
                "numeric" => "d",
                "2-digit" => "dd",
                _ => "dd"
            });
        }

        // Hour
        if (Hour != null)
        {
            var use12Hour = string.Equals(GetHourFormat(), "h12", StringComparison.Ordinal);
            parts.Add(Hour switch
            {
                "numeric" => use12Hour ? "h" : "H",
                "2-digit" => use12Hour ? "hh" : "HH",
                _ => use12Hour ? "h" : "H"
            });
        }

        // Minute
        if (Minute != null)
        {
            parts.Add(Minute switch
            {
                "numeric" => "m",
                "2-digit" => "mm",
                _ => "mm"
            });
        }

        // Second
        if (Second != null)
        {
            parts.Add(Second switch
            {
                "numeric" => "s",
                "2-digit" => "ss",
                _ => "ss"
            });
        }

        // Fractional seconds
        if (FractionalSecondDigits.HasValue && FractionalSecondDigits.Value > 0)
        {
            parts.Add(new string('f', FractionalSecondDigits.Value));
        }

        // Day period (AM/PM)
        if (Hour != null && string.Equals(GetHourFormat(), "h12", StringComparison.Ordinal))
        {
            parts.Add("tt");
        }

        // Time zone name
        if (TimeZoneName != null)
        {
            parts.Add(TimeZoneName switch
            {
                "long" => "zzz",
                "short" => "zzz",
                "shortOffset" => "zzz",
                "longOffset" => "zzz",
                "shortGeneric" => "zzz",
                "longGeneric" => "zzz",
                _ => "zzz"
            });
        }

        if (parts.Count == 0)
        {
            // Default format if no components specified
            return dateTime.ToString("G", CultureInfo);
        }

        // Join parts with appropriate separators
        var formatString = BuildFormatString(parts);
        return dateTime.ToString(formatString, CultureInfo);
    }

    private string GetHourFormat()
    {
        if (HourCycle != null)
        {
            if (string.Equals(HourCycle, "h11", StringComparison.Ordinal) ||
                string.Equals(HourCycle, "h12", StringComparison.Ordinal))
            {
                return "h12";
            }
            if (string.Equals(HourCycle, "h23", StringComparison.Ordinal) ||
                string.Equals(HourCycle, "h24", StringComparison.Ordinal))
            {
                return "h24";
            }
            return "h12";
        }

        // Default based on locale
        return "h12";
    }

    private static string BuildFormatString(List<string> parts)
    {
        // Simple join - a more sophisticated implementation would use
        // locale-specific patterns
        var result = new ValueStringBuilder();
        var hasDate = false;
        var hasTime = false;

        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            var firstChar = part[0];

            if (result.Length > 0)
            {
                // Add separator based on what we're joining
                if (firstChar is 'h' or 'H' or 'm' or 's' or 'f' or 't')
                {
                    if (!hasTime)
                    {
                        if (hasDate)
                        {
                            result.Append(' ');
                        }
                        hasTime = true;
                    }
                    else if (firstChar is not 't' and not 'f')
                    {
                        result.Append(':');
                    }
                    else if (firstChar == 't')
                    {
                        result.Append(' ');
                    }
                    else if (firstChar == 'f')
                    {
                        result.Append('.');
                    }
                }
                else if (firstChar == 'z')
                {
                    result.Append(' ');
                }
                else
                {
                    if (!hasDate)
                    {
                        hasDate = true;
                    }
                    else
                    {
                        // Use locale-appropriate separator (simplified to space or /)
                        if ((firstChar is 'd' or 'D') && part.Length <= 2)
                        {
                            result.Append('/');
                        }
                        else if (firstChar is 'y' or 'Y')
                        {
                            result.Append('/');
                        }
                        else if (firstChar == 'M' && part.Length <= 2)
                        {
                            result.Append('/');
                        }
                        else
                        {
                            result.Append(' ');
                        }
                    }
                }
            }
            else
            {
                if (firstChar is 'h' or 'H')
                {
                    hasTime = true;
                }
                else if (firstChar is not 't' and not 'z')
                {
                    hasDate = true;
                }
            }

            result.Append(part);
        }

        return result.ToString();
    }
}

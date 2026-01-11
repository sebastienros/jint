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
        ObjectInstance prototype,
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

    /// <summary>
    /// Returns the formatted parts with their types for formatToParts.
    /// </summary>
    internal List<DateTimePart> FormatToParts(DateTime dateTime)
    {
        var result = new List<DateTimePart>();

        if (DateStyle != null || TimeStyle != null)
        {
            // For style-based formatting, use a simpler approach
            FormatStyleToParts(dateTime, result);
        }
        else
        {
            FormatComponentsToParts(dateTime, result);
        }

        return result;
    }

    private void FormatStyleToParts(DateTime dateTime, List<DateTimePart> result)
    {
        // For style-based formatting, parse the formatted output
        var formatted = FormatWithStyles(dateTime);

        // Simple approach: return the whole string as literal
        // A full implementation would parse locale patterns
        result.Add(new DateTimePart("literal", formatted));
    }

    private void FormatComponentsToParts(DateTime dateTime, List<DateTimePart> result)
    {
        var hasDate = false;
        var hasTime = false;
        var dateSeparator = DateTimeFormatInfo.DateSeparator;

        // Weekday (first, if present)
        if (Weekday != null)
        {
            var format = Weekday switch
            {
                "long" => "dddd",
                "short" => "ddd",
                "narrow" => "ddd",
                _ => "ddd"
            };
            result.Add(new DateTimePart("weekday", dateTime.ToString(format, CultureInfo)));
            hasDate = true;
        }

        // Year (before month/day to match FormatWithComponents order)
        if (Year != null)
        {
            if (result.Count > 0 && hasDate)
            {
                result.Add(new DateTimePart("literal", dateSeparator));
            }
            var yearValue = Year switch
            {
                "numeric" => dateTime.Year.ToString(CultureInfo),
                "2-digit" => (dateTime.Year % 100).ToString("00", CultureInfo),
                _ => dateTime.Year.ToString(CultureInfo)
            };
            result.Add(new DateTimePart("year", yearValue));
            hasDate = true;
        }

        // Month
        if (Month != null)
        {
            if (result.Count > 0 && hasDate)
            {
                result.Add(new DateTimePart("literal", dateSeparator));
            }
            var format = Month switch
            {
                "numeric" => "%M",
                "2-digit" => "MM",
                "long" => "MMMM",
                "short" => "MMM",
                "narrow" => "MMM",
                _ => "MM"
            };
            result.Add(new DateTimePart("month", dateTime.ToString(format, CultureInfo)));
            hasDate = true;
        }

        // Day
        if (Day != null)
        {
            if (result.Count > 0 && hasDate)
            {
                result.Add(new DateTimePart("literal", dateSeparator));
            }
            var format = Day switch
            {
                "numeric" => "%d",
                "2-digit" => "dd",
                _ => "dd"
            };
            result.Add(new DateTimePart("day", dateTime.ToString(format, CultureInfo)));
            hasDate = true;
        }

        // Era (after date components)
        if (Era != null)
        {
            if (result.Count > 0)
            {
                result.Add(new DateTimePart("literal", " "));
            }
            result.Add(new DateTimePart("era", dateTime.Year > 0 ? "AD" : "BC"));
        }

        // Hour
        if (Hour != null)
        {
            if (result.Count > 0)
            {
                result.Add(new DateTimePart("literal", hasDate ? ", " : ""));
            }
            var use12Hour = string.Equals(GetHourFormat(), "h12", StringComparison.Ordinal);
            var format = Hour switch
            {
                "numeric" => use12Hour ? "%h" : "%H",  // Use % for single character
                "2-digit" => use12Hour ? "hh" : "HH",
                _ => use12Hour ? "%h" : "%H"
            };
            result.Add(new DateTimePart("hour", dateTime.ToString(format, CultureInfo)));
            hasTime = true;
        }

        // Minute
        if (Minute != null)
        {
            if (result.Count > 0 && hasTime)
            {
                result.Add(new DateTimePart("literal", ":"));
            }
            var format = Minute switch
            {
                "numeric" => "%m",  // Use % for single character
                "2-digit" => "mm",
                _ => "mm"
            };
            result.Add(new DateTimePart("minute", dateTime.ToString(format, CultureInfo)));
            hasTime = true;
        }

        // Second
        if (Second != null)
        {
            if (result.Count > 0 && hasTime)
            {
                result.Add(new DateTimePart("literal", ":"));
            }
            var format = Second switch
            {
                "numeric" => "%s",  // Use % for single character
                "2-digit" => "ss",
                _ => "ss"
            };
            result.Add(new DateTimePart("second", dateTime.ToString(format, CultureInfo)));
            hasTime = true;
        }

        // Fractional seconds
        if (FractionalSecondDigits.HasValue && FractionalSecondDigits.Value > 0)
        {
            result.Add(new DateTimePart("literal", "."));
            var format = new string('f', FractionalSecondDigits.Value);
            result.Add(new DateTimePart("fractionalSecond", dateTime.ToString(format, CultureInfo)));
        }

        // Day period (AM/PM)
        if (Hour != null && string.Equals(GetHourFormat(), "h12", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("dayPeriod", dateTime.ToString("tt", CultureInfo)));
        }
        else if (DayPeriod != null)
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("dayPeriod", dateTime.ToString("tt", CultureInfo)));
        }

        // Time zone name
        if (TimeZoneName != null)
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("timeZoneName", dateTime.ToString("zzz", CultureInfo)));
        }

        // If no parts were added, use default format
        if (result.Count == 0)
        {
            var formatted = dateTime.ToString("G", CultureInfo);
            result.Add(new DateTimePart("literal", formatted));
        }
    }

    internal readonly struct DateTimePart
    {
        public DateTimePart(string type, string value)
        {
            Type = type;
            Value = value;
        }

        public string Type { get; }
        public string Value { get; }
    }
}

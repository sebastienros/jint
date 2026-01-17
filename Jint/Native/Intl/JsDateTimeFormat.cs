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
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    /// <summary>
    /// Formats a date according to the formatter's locale and options.
    /// </summary>
    internal string Format(DateTime dateTime)
    {
        // Convert to specified timezone if one was provided
        if (TimeZone != null)
        {
            dateTime = ConvertToTimeZone(dateTime, TimeZone);
        }

        // If dateStyle or timeStyle is specified, use those
        if (DateStyle != null || TimeStyle != null)
        {
            return FormatWithStyles(dateTime);
        }

        // Otherwise build format from component options
        return FormatWithComponents(dateTime);
    }

    private static DateTime ConvertToTimeZone(DateTime dateTime, string timeZoneId)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            // Convert to UTC
            if (dateTime.Kind == DateTimeKind.Local)
            {
                return dateTime.ToUniversalTime();
            }
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            if (dateTime.Kind == DateTimeKind.Local)
            {
                dateTime = dateTime.ToUniversalTime();
            }
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc), timeZone);
        }
        catch
        {
            // If timezone lookup fails, return as-is
            return dateTime;
        }
    }

    private string FormatWithStyles(DateTime dateTime)
    {
        // When both dateStyle and timeStyle are specified, combine them appropriately
        if (DateStyle != null && TimeStyle != null)
        {
            // Format date and time separately and combine with ", "
            var datePart = FormatDateStyleOnly(dateTime);
            var timePart = FormatTimeStyle(dateTime);
            return $"{datePart}, {timePart}";
        }

        if (DateStyle != null)
        {
            return FormatDateStyleOnly(dateTime);
        }

        if (TimeStyle != null)
        {
            return FormatTimeStyle(dateTime);
        }

        return dateTime.ToString("G", CultureInfo);
    }

    private string FormatDateStyleOnly(DateTime dateTime)
    {
        return DateStyle switch
        {
            "full" => dateTime.ToString("D", CultureInfo), // Full date pattern (includes weekday)
            "long" => FormatLongDate(dateTime),  // Long date without weekday
            "medium" => FormatMediumDate(dateTime), // Medium date (same as long for most locales)
            "short" => FormatShortDate(dateTime), // Short date (numeric)
            _ => dateTime.ToString("d", CultureInfo)
        };
    }

    /// <summary>
    /// Formats a date in long style (without weekday), e.g., "May 1, 1886"
    /// </summary>
    private string FormatLongDate(DateTime dateTime)
    {
        // Use MMMM d, yyyy for en-US style, or locale-appropriate pattern
        var lang = Locale.Split('-')[0].ToLowerInvariant();
        if (string.Equals(lang, "en", StringComparison.Ordinal))
        {
            return dateTime.ToString("MMMM d, yyyy", CultureInfo);
        }
        // For other locales, use the long date pattern without weekday
        var longPattern = CultureInfo.DateTimeFormat.LongDatePattern;
        // Remove weekday-related format specifiers manually
        var modifiedPattern = RemoveWeekdayFromPattern(longPattern);
        if (string.IsNullOrEmpty(modifiedPattern))
        {
            return dateTime.ToString("MMMM d, yyyy", CultureInfo);
        }
        return dateTime.ToString(modifiedPattern, CultureInfo);
    }

    private static string RemoveWeekdayFromPattern(string pattern)
    {
        // Remove dddd or ddd followed by optional comma/space
        var result = pattern;
        var weekdayPatterns = new[] { "dddd, ", "dddd,", "dddd ", "dddd", "ddd, ", "ddd,", "ddd ", "ddd" };
        foreach (var wp in weekdayPatterns)
        {
            var idx = result.IndexOf(wp, StringComparison.Ordinal);
            if (idx >= 0)
            {
                result = result.Remove(idx, wp.Length);
                break;
            }
        }
        return result.Trim().TrimStart(',').Trim();
    }

    /// <summary>
    /// Formats a date in medium style, e.g., "May 1, 1886"
    /// </summary>
    private string FormatMediumDate(DateTime dateTime)
    {
        // Medium is typically the same as long for most locales
        return FormatLongDate(dateTime);
    }

    /// <summary>
    /// Formats a date in short style, e.g., "5/1/86"
    /// </summary>
    private string FormatShortDate(DateTime dateTime)
    {
        // Use locale's short date pattern but with 2-digit year
        var lang = Locale.Split('-')[0].ToLowerInvariant();
        if (string.Equals(lang, "en", StringComparison.Ordinal))
        {
            // US style: M/d/yy with literal slash separator
            return dateTime.ToString("M'/'d'/'yy", CultureInfo);
        }
        // For other locales, use the short date pattern
        return dateTime.ToString("d", CultureInfo);
    }

    /// <summary>
    /// Formats time using timeStyle, respecting hourCycle
    /// </summary>
    private string FormatTimeStyle(DateTime dateTime)
    {
        // Determine if we should use 12 or 24 hour format
        bool use12Hour;
        if (HourCycle != null)
        {
            // Explicit hourCycle takes precedence
            use12Hour = string.Equals(HourCycle, "h11", StringComparison.Ordinal) ||
                       string.Equals(HourCycle, "h12", StringComparison.Ordinal);
        }
        else
        {
            // Derive from locale - English uses 12-hour, most others use 24-hour
            var lang = Locale.Split('-')[0].ToLowerInvariant();
            use12Hour = string.Equals(lang, "en", StringComparison.Ordinal);
        }

        var timeZoneSuffix = "";
        if (string.Equals(TimeStyle, "full", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(TimeZone) && string.Equals(TimeZone, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                timeZoneSuffix = " Coordinated Universal Time";
            }
            else
            {
                timeZoneSuffix = " " + TimeZoneInfo.Local.DisplayName;
            }
        }
        else if (string.Equals(TimeStyle, "long", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(TimeZone) && string.Equals(TimeZone, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                timeZoneSuffix = " UTC";
            }
        }

        return TimeStyle switch
        {
            "full" => use12Hour
                ? dateTime.ToString("h:mm:ss", CultureInfo) + " " + GetDayPeriod(dateTime.Hour) + timeZoneSuffix
                : dateTime.ToString("HH:mm:ss", CultureInfo) + timeZoneSuffix,
            "long" => use12Hour
                ? dateTime.ToString("h:mm:ss", CultureInfo) + " " + GetDayPeriod(dateTime.Hour) + timeZoneSuffix
                : dateTime.ToString("HH:mm:ss", CultureInfo) + timeZoneSuffix,
            "medium" => use12Hour
                ? dateTime.ToString("h:mm:ss", CultureInfo) + " " + GetDayPeriod(dateTime.Hour)
                : dateTime.ToString("HH:mm:ss", CultureInfo),
            "short" => use12Hour
                ? dateTime.ToString("h:mm", CultureInfo) + " " + GetDayPeriod(dateTime.Hour)
                : dateTime.ToString("HH:mm", CultureInfo),
            _ => use12Hour
                ? dateTime.ToString("h:mm", CultureInfo) + " " + GetDayPeriod(dateTime.Hour)
                : dateTime.ToString("HH:mm", CultureInfo),
        };
    }

    private static string GetDayPeriod(int hour)
    {
        return hour < 12 ? "AM" : "PM";
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

        // Minute - for time components, "numeric" typically uses 2-digit padding in most locales
        if (Minute != null)
        {
            parts.Add(Minute switch
            {
                "numeric" => "mm",
                "2-digit" => "mm",
                _ => "mm"
            });
        }

        // Second - for time components, "numeric" typically uses 2-digit padding in most locales
        if (Second != null)
        {
            parts.Add(Second switch
            {
                "numeric" => "ss",
                "2-digit" => "ss",
                _ => "ss"
            });
        }

        // Fractional seconds
        if (FractionalSecondDigits.HasValue && FractionalSecondDigits.Value > 0)
        {
            parts.Add(new string('f', FractionalSecondDigits.Value));
        }

        // Day period (AM/PM) - only add "tt" if using 12-hour format with hour specified
        // and DayPeriod is not explicitly specified (DayPeriod uses extended periods)
        var needsAmPm = Hour != null && string.Equals(GetHourFormat(), "h12", StringComparison.Ordinal) && DayPeriod == null;
        if (needsAmPm)
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

        // Handle DayPeriod option (extended day periods like "in the morning")
        if (DayPeriod != null)
        {
            // If only dayPeriod is specified (no other components), just return the day period
            if (parts.Count == 0)
            {
                return GetExtendedDayPeriod(dateTime.Hour);
            }

            // Otherwise, format with other components and append day period
            var formatString = BuildFormatString(parts);
            var formatted = dateTime.ToString(formatString, CultureInfo);
            return formatted + " " + GetExtendedDayPeriod(dateTime.Hour);
        }

        if (parts.Count == 0)
        {
            // Default format if no components specified
            return dateTime.ToString("G", CultureInfo);
        }

        // Join parts with appropriate separators
        var formatString2 = BuildFormatString(parts);
        return dateTime.ToString(formatString2, CultureInfo);
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
                if (firstChar is 'h' or 'H' or 'm' or 's' or 'f')
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

        var formatString = result.ToString();

        // In .NET, single character format strings are interpreted as standard format specifiers
        // We need to prefix with % to indicate it's a custom format
        if (formatString.Length == 1)
        {
            return "%" + formatString;
        }

        return formatString;
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
        // For style-based formatting, decompose into proper parts
        // Map styles to component options and use component-based parts generation
        var hasDate = DateStyle != null;
        var hasTime = TimeStyle != null;

        if (hasDate)
        {
            FormatDateStyleToParts(dateTime, result);
        }

        if (hasDate && hasTime)
        {
            // Add separator between date and time
            result.Add(new DateTimePart("literal", ", "));
        }

        if (hasTime)
        {
            FormatTimeStyleToParts(dateTime, result);
        }
    }

    private void FormatDateStyleToParts(DateTime dateTime, List<DateTimePart> result)
    {
        var style = DateStyle;

        // Full: weekday, month, day, year
        // Long: month, day, year
        // Medium: month, day, year (abbreviated)
        // Short: month/day/year (numeric)

        if (string.Equals(style, "full", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("weekday", dateTime.ToString("dddd", CultureInfo)));
            result.Add(new DateTimePart("literal", ", "));
        }

        if (string.Equals(style, "full", StringComparison.Ordinal) ||
            string.Equals(style, "long", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("month", dateTime.ToString("MMMM", CultureInfo)));
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("day", dateTime.Day.ToString(CultureInfo)));
            result.Add(new DateTimePart("literal", ", "));
            result.Add(new DateTimePart("year", dateTime.Year.ToString(CultureInfo)));
        }
        else if (string.Equals(style, "medium", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("month", dateTime.ToString("MMM", CultureInfo)));
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("day", dateTime.Day.ToString(CultureInfo)));
            result.Add(new DateTimePart("literal", ", "));
            result.Add(new DateTimePart("year", dateTime.Year.ToString(CultureInfo)));
        }
        else // short
        {
            result.Add(new DateTimePart("month", dateTime.Month.ToString(CultureInfo)));
            result.Add(new DateTimePart("literal", "/"));
            result.Add(new DateTimePart("day", dateTime.Day.ToString(CultureInfo)));
            result.Add(new DateTimePart("literal", "/"));
            result.Add(new DateTimePart("year", (dateTime.Year % 100).ToString("D2", CultureInfo)));
        }
    }

    private void FormatTimeStyleToParts(DateTime dateTime, List<DateTimePart> result)
    {
        var style = TimeStyle;
        var use12Hour = IsUsing12HourFormat();
        var hour = use12Hour ? (dateTime.Hour % 12 == 0 ? 12 : dateTime.Hour % 12) : dateTime.Hour;

        // Hour
        result.Add(new DateTimePart("hour", hour.ToString(CultureInfo)));

        // Minute (always for time styles)
        result.Add(new DateTimePart("literal", ":"));
        result.Add(new DateTimePart("minute", dateTime.Minute.ToString("D2", CultureInfo)));

        // Second (for medium, long, full)
        if (!string.Equals(style, "short", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("literal", ":"));
            result.Add(new DateTimePart("second", dateTime.Second.ToString("D2", CultureInfo)));
        }

        // Day period (AM/PM) for 12-hour format
        if (use12Hour)
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("dayPeriod", dateTime.Hour < 12 ? "AM" : "PM"));
        }

        // Time zone name (for long and full)
        if (string.Equals(style, "full", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("timeZoneName", GetTimeZoneDisplayName(true)));
        }
        else if (string.Equals(style, "long", StringComparison.Ordinal))
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("timeZoneName", GetTimeZoneDisplayName(false)));
        }
    }

    private bool IsUsing12HourFormat()
    {
        // Check hourCycle first
        if (HourCycle != null)
        {
            return string.Equals(HourCycle, "h11", StringComparison.Ordinal) ||
                   string.Equals(HourCycle, "h12", StringComparison.Ordinal);
        }

        // Default based on locale - US uses 12-hour, most others use 24-hour
        var lang = Locale.Split('-')[0].ToLowerInvariant();
        return string.Equals(lang, "en", StringComparison.Ordinal);
    }

    private string GetTimeZoneDisplayName(bool longName)
    {
        if (TimeZone != null)
        {
            if (string.Equals(TimeZone, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                return longName ? "Coordinated Universal Time" : "UTC";
            }
            // For other timezones, use the ID or a short form
            var parts = TimeZone.Split('/');
            return longName ? TimeZone : parts[parts.Length - 1];
        }
        return longName ? TimeZoneInfo.Local.DisplayName : TimeZoneInfo.Local.Id;
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

        // Minute - for time components, "numeric" typically uses 2-digit padding in most locales
        if (Minute != null)
        {
            if (result.Count > 0 && hasTime)
            {
                result.Add(new DateTimePart("literal", ":"));
            }
            // Per ECMA-402, minute and second use 2-digit format for both "numeric" and "2-digit"
            result.Add(new DateTimePart("minute", dateTime.Minute.ToString("D2", CultureInfo)));
            hasTime = true;
        }

        // Second - for time components, "numeric" typically uses 2-digit padding in most locales
        if (Second != null)
        {
            if (result.Count > 0 && hasTime)
            {
                result.Add(new DateTimePart("literal", ":"));
            }
            // Per ECMA-402, minute and second use 2-digit format for both "numeric" and "2-digit"
            result.Add(new DateTimePart("second", dateTime.Second.ToString("D2", CultureInfo)));
            hasTime = true;
        }

        // Fractional seconds
        if (FractionalSecondDigits.HasValue && FractionalSecondDigits.Value > 0)
        {
            result.Add(new DateTimePart("literal", "."));
            // Use % prefix for single-character format to prevent it being interpreted as standard format
            var format = FractionalSecondDigits.Value == 1 ? "%f" : new string('f', FractionalSecondDigits.Value);
            result.Add(new DateTimePart("fractionalSecond", dateTime.ToString(format, CultureInfo)));
        }

        // Day period (AM/PM or extended day periods)
        if (DayPeriod != null)
        {
            // Extended day periods like "in the morning", "noon", etc.
            if (result.Count > 0)
            {
                result.Add(new DateTimePart("literal", " "));
            }
            result.Add(new DateTimePart("dayPeriod", GetExtendedDayPeriod(dateTime.Hour)));
        }
        else if (Hour != null && string.Equals(GetHourFormat(), "h12", StringComparison.Ordinal))
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

    /// <summary>
    /// Gets the extended day period string based on the hour and dayPeriod style.
    /// CLDR defines: night1 (21:00-05:59), morning1 (06:00-11:59), noon (12:00),
    /// afternoon1 (12:01-17:59), evening1 (18:00-20:59)
    /// </summary>
    private string GetExtendedDayPeriod(int hour)
    {
        // For English locale (en), use CLDR day period names
        // Other locales would need locale-specific data
        var lang = Locale.Split('-')[0];

        if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase))
        {
            return DayPeriod switch
            {
                "long" => hour switch
                {
                    >= 0 and < 6 => "at night",
                    >= 6 and < 12 => "in the morning",
                    12 => "noon",
                    > 12 and < 18 => "in the afternoon",
                    >= 18 and < 21 => "in the evening",
                    _ => "at night"
                },
                "short" => hour switch
                {
                    >= 0 and < 6 => "at night",
                    >= 6 and < 12 => "in the morning",
                    12 => "noon",
                    > 12 and < 18 => "in the afternoon",
                    >= 18 and < 21 => "in the evening",
                    _ => "at night"
                },
                "narrow" => hour switch
                {
                    >= 0 and < 6 => "at night",
                    >= 6 and < 12 => "in the morning",
                    12 => "n",
                    > 12 and < 18 => "in the afternoon",
                    >= 18 and < 21 => "in the evening",
                    _ => "at night"
                },
                _ => hour < 12 ? "AM" : "PM"
            };
        }

        // Default: use AM/PM
        return hour < 12 ? "AM" : "PM";
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

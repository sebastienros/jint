using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Jint.Runtime;

namespace Jint.Native.Temporal;

/// <summary>
/// Shared helper functions for Temporal types.
/// </summary>
internal static class TemporalHelpers
{
    // Polyfill for Math.Clamp which doesn't exist in netstandard2.0
    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    // Constants for nanosecond calculations
    public const long NanosecondsPerMicrosecond = 1_000L;
    public const long NanosecondsPerMillisecond = 1_000_000L;
    public const long NanosecondsPerSecond = 1_000_000_000L;
    public const long NanosecondsPerMinute = 60_000_000_000L;
    public const long NanosecondsPerHour = 3_600_000_000_000L;
    public const long NanosecondsPerDay = 86_400_000_000_000L;

    // Valid Temporal unit names
    public static readonly string[] TemporalUnitNames =
    {
        "year", "month", "week", "day", "hour", "minute", "second", "millisecond", "microsecond", "nanosecond"
    };

    public static readonly string[] TemporalSingularUnits =
    {
        "year", "month", "week", "day", "hour", "minute", "second", "millisecond", "microsecond", "nanosecond"
    };

    public static readonly string[] TemporalPluralUnits =
    {
        "years", "months", "weeks", "days", "hours", "minutes", "seconds", "milliseconds", "microseconds", "nanoseconds"
    };

    // Regex patterns for ISO 8601 parsing
    // Note: Adding timeout for safety against ReDoS attacks
#pragma warning disable MA0023 // Use RegexOptions.ExplicitCapture - we need numbered capture groups
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    private static readonly Regex DatePattern = new(
        @"^([+-]?\d{4,6})-(\d{2})-(\d{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex TimePattern = new(
        @"^(\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,9}))?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex DateTimePattern = new(
        @"^([+-]?\d{4,6})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.(\d{1,9}))?)?(?:([Zz])|([+-])(\d{2}):?(\d{2})(?::?(\d{2})(?:\.(\d{1,9}))?)?)?(?:\[([^\]]+)\])?(?:\[u-ca=([^\]]+)\])?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly Regex DurationPattern = new(
        @"^([+-])?P(?:(\d+)Y)?(?:(\d+)M)?(?:(\d+)W)?(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)(?:\.(\d{1,9}))?S)?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        RegexTimeout);

    private static readonly Regex InstantPattern = new(
        @"^([+-]?\d{4,6})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,9}))?([Zz]|([+-])(\d{2}):?(\d{2})(?::?(\d{2})(?:\.(\d{1,9}))?)?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);
#pragma warning restore MA0023

    /// <summary>
    /// Formats an offset in nanoseconds as an ISO 8601 offset string.
    /// </summary>
    public static string FormatOffsetString(long offsetNanoseconds)
    {
        var sign = offsetNanoseconds >= 0 ? "+" : "-";
        offsetNanoseconds = System.Math.Abs(offsetNanoseconds);

        var hours = offsetNanoseconds / NanosecondsPerHour;
        offsetNanoseconds %= NanosecondsPerHour;
        var minutes = offsetNanoseconds / NanosecondsPerMinute;
        offsetNanoseconds %= NanosecondsPerMinute;
        var seconds = offsetNanoseconds / NanosecondsPerSecond;
        offsetNanoseconds %= NanosecondsPerSecond;

        if (seconds != 0 || offsetNanoseconds != 0)
        {
            var nanoseconds = offsetNanoseconds;
            if (nanoseconds != 0)
            {
                var fraction = nanoseconds.ToString(CultureInfo.InvariantCulture).PadLeft(9, '0').TrimEnd('0');
                return $"{sign}{hours:D2}:{minutes:D2}:{seconds:D2}.{fraction}";
            }
            return $"{sign}{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        return $"{sign}{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// Parses an ISO 8601 duration string.
    /// </summary>
    public static DurationRecord? ParseDuration(string input)
    {
        var match = DurationPattern.Match(input);
        if (!match.Success)
            return null;

        var sign = string.Equals(match.Groups[1].Value, "-", StringComparison.Ordinal) ? -1.0 : 1.0;
        var years = ParseDurationComponent(match.Groups[2].Value);
        var months = ParseDurationComponent(match.Groups[3].Value);
        var weeks = ParseDurationComponent(match.Groups[4].Value);
        var days = ParseDurationComponent(match.Groups[5].Value);
        var hours = ParseDurationComponent(match.Groups[6].Value);
        var minutes = ParseDurationComponent(match.Groups[7].Value);

        double seconds = 0;
        double milliseconds = 0;
        double microseconds = 0;
        double nanoseconds = 0;

        if (match.Groups[8].Success)
        {
            seconds = double.Parse(match.Groups[8].Value, CultureInfo.InvariantCulture);
            if (match.Groups[9].Success)
            {
                var fraction = match.Groups[9].Value.PadRight(9, '0');
#pragma warning disable CA1846 // Substring is used for cross-platform compatibility
                milliseconds = double.Parse(fraction.Substring(0, 3), CultureInfo.InvariantCulture);
                microseconds = double.Parse(fraction.Substring(3, 3), CultureInfo.InvariantCulture);
                nanoseconds = double.Parse(fraction.Substring(6, 3), CultureInfo.InvariantCulture);
#pragma warning restore CA1846
            }
        }

        return new DurationRecord(
            sign * years,
            sign * months,
            sign * weeks,
            sign * days,
            sign * hours,
            sign * minutes,
            sign * seconds,
            sign * milliseconds,
            sign * microseconds,
            sign * nanoseconds);
    }

    private static double ParseDurationComponent(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an ISO 8601 date string.
    /// </summary>
    public static IsoDate? ParseIsoDate(string input)
    {
        var match = DatePattern.Match(input);
        if (!match.Success)
            return null;

        var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        var date = new IsoDate(year, month, day);
        return date.IsValid() ? date : null;
    }

    /// <summary>
    /// Parses an ISO 8601 time string.
    /// </summary>
    public static IsoTime? ParseIsoTime(string input)
    {
        var match = TimePattern.Match(input);
        if (!match.Success)
            return null;

        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var second = match.Groups[3].Success
            ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : 0;

        int millisecond = 0, microsecond = 0, nanosecond = 0;
        if (match.Groups[4].Success)
        {
            var fraction = match.Groups[4].Value.PadRight(9, '0');
#pragma warning disable CA1846 // Substring is used for cross-platform compatibility
            millisecond = int.Parse(fraction.Substring(0, 3), CultureInfo.InvariantCulture);
            microsecond = int.Parse(fraction.Substring(3, 3), CultureInfo.InvariantCulture);
            nanosecond = int.Parse(fraction.Substring(6, 3), CultureInfo.InvariantCulture);
#pragma warning restore CA1846
        }

        var time = new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
        return time.IsValid() ? time : null;
    }

    /// <summary>
    /// Validates that a duration has valid components.
    /// </summary>
    public static bool IsValidDuration(DurationRecord duration)
    {
        // Check that all components have the same sign
        var sign = 0;
        double[] components =
        {
            duration.Years, duration.Months, duration.Weeks, duration.Days,
            duration.Hours, duration.Minutes, duration.Seconds,
            duration.Milliseconds, duration.Microseconds, duration.Nanoseconds
        };

        foreach (var component in components)
        {
            if (double.IsNaN(component) || double.IsInfinity(component))
                return false;

            if (component > 0)
            {
                if (sign < 0)
                    return false;
                sign = 1;
            }
            else if (component < 0)
            {
                if (sign > 0)
                    return false;
                sign = -1;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts a duration to total nanoseconds (excluding calendar units).
    /// Only valid for durations without years, months, or weeks.
    /// </summary>
    public static System.Numerics.BigInteger TotalDurationNanoseconds(DurationRecord duration)
    {
        const long nsPerDay = 86_400_000_000_000L;
        const long nsPerHour = 3_600_000_000_000L;
        const long nsPerMinute = 60_000_000_000L;
        const long nsPerSecond = 1_000_000_000L;
        const long nsPerMs = 1_000_000L;
        const long nsPerUs = 1_000L;

        var total = (System.Numerics.BigInteger) (duration.Days * nsPerDay);
        total += (System.Numerics.BigInteger) (duration.Hours * nsPerHour);
        total += (System.Numerics.BigInteger) (duration.Minutes * nsPerMinute);
        total += (System.Numerics.BigInteger) (duration.Seconds * nsPerSecond);
        total += (System.Numerics.BigInteger) (duration.Milliseconds * nsPerMs);
        total += (System.Numerics.BigInteger) (duration.Microseconds * nsPerUs);
        total += (System.Numerics.BigInteger) duration.Nanoseconds;

        return total;
    }

    /// <summary>
    /// Gets the singular form of a temporal unit name.
    /// </summary>
    public static string ToSingularUnit(string unit)
    {
        return unit switch
        {
            "years" => "year",
            "months" => "month",
            "weeks" => "week",
            "days" => "day",
            "hours" => "hour",
            "minutes" => "minute",
            "seconds" => "second",
            "milliseconds" => "millisecond",
            "microseconds" => "microsecond",
            "nanoseconds" => "nanosecond",
            _ => unit
        };
    }

    /// <summary>
    /// Gets the plural form of a temporal unit name.
    /// </summary>
    public static string ToPluralUnit(string unit)
    {
        return unit switch
        {
            "year" => "years",
            "month" => "months",
            "week" => "weeks",
            "day" => "days",
            "hour" => "hours",
            "minute" => "minutes",
            "second" => "seconds",
            "millisecond" => "milliseconds",
            "microsecond" => "microseconds",
            "nanosecond" => "nanoseconds",
            _ => unit
        };
    }

    /// <summary>
    /// Validates that a value is a valid ISO year.
    /// </summary>
    public static bool IsValidIsoYear(int year)
    {
        return year >= -271821 && year <= 275760;
    }

    /// <summary>
    /// Validates and regulates ISO date fields.
    /// </summary>
    public static IsoDate? RegulateIsoDate(int year, int month, int day, string overflow)
    {
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            month = Clamp(month, 1, 12);
            var daysInMonth = IsoDate.IsoDateInMonth(year, month);
            day = Clamp(day, 1, daysInMonth);
            return new IsoDate(year, month, day);
        }

        if (string.Equals(overflow, "reject", StringComparison.Ordinal))
        {
            var date = new IsoDate(year, month, day);
            return date.IsValid() ? date : null;
        }

        return null;
    }

    /// <summary>
    /// Validates and regulates ISO time fields.
    /// </summary>
    public static IsoTime? RegulateIsoTime(
        int hour, int minute, int second,
        int millisecond, int microsecond, int nanosecond,
        string overflow)
    {
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            hour = Clamp(hour, 0, 23);
            minute = Clamp(minute, 0, 59);
            second = Clamp(second, 0, 59);
            millisecond = Clamp(millisecond, 0, 999);
            microsecond = Clamp(microsecond, 0, 999);
            nanosecond = Clamp(nanosecond, 0, 999);
            return new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
        }

        if (string.Equals(overflow, "reject", StringComparison.Ordinal))
        {
            var time = new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond);
            return time.IsValid() ? time : null;
        }

        return null;
    }

    /// <summary>
    /// Adds a duration to an ISO date.
    /// </summary>
    public static IsoDate AddDurationToDate(IsoDate date, DurationRecord duration)
    {
        // Add years and months first
        var year = date.Year + (int) duration.Years;
        var month = date.Month + (int) duration.Months;

        // Normalize month
        while (month > 12)
        {
            month -= 12;
            year++;
        }
        while (month < 1)
        {
            month += 12;
            year--;
        }

        // Clamp day to valid range for new month
        var day = System.Math.Min(date.Day, IsoDate.IsoDateInMonth(year, month));

        // Convert to days and add weeks and days
        var totalDays = IsoDateToDays(year, month, day);
        totalDays += (long) (duration.Weeks * 7 + duration.Days);

        return DaysToIsoDate(totalDays);
    }

    /// <summary>
    /// Converts an ISO date to days since a reference point.
    /// </summary>
    public static long IsoDateToDays(int year, int month, int day)
    {
        // Adjust for months before March
        int a = (14 - month) / 12;
        int y = year - a;
        int m = month + 12 * a - 3;

        // Calculate Julian day number
        return day + (153 * m + 2) / 5 + 365L * y + y / 4 - y / 100 + y / 400 - 32045;
    }

    /// <summary>
    /// Converts days since reference to ISO date.
    /// </summary>
    public static IsoDate DaysToIsoDate(long days)
    {
        // Inverse of IsoDateToDays
        long a = days + 32044;
        long b = (4 * a + 3) / 146097;
        long c = a - 146097 * b / 4;
        long d = (4 * c + 3) / 1461;
        long e = c - 1461 * d / 4;
        long m = (5 * e + 2) / 153;

        var day = (int) (e - (153 * m + 2) / 5 + 1);
        var month = (int) (m + 3 - 12 * (m / 10));
        var year = (int) (100 * b + d - 4800 + m / 10);

        return new IsoDate(year, month, day);
    }

    /// <summary>
    /// Compares two ISO dates.
    /// </summary>
    public static int CompareIsoDates(IsoDate a, IsoDate b)
    {
        if (a.Year != b.Year) return a.Year.CompareTo(b.Year);
        if (a.Month != b.Month) return a.Month.CompareTo(b.Month);
        return a.Day.CompareTo(b.Day);
    }

    /// <summary>
    /// Compares two ISO times.
    /// </summary>
    public static int CompareIsoTimes(IsoTime a, IsoTime b)
    {
        if (a.Hour != b.Hour) return a.Hour.CompareTo(b.Hour);
        if (a.Minute != b.Minute) return a.Minute.CompareTo(b.Minute);
        if (a.Second != b.Second) return a.Second.CompareTo(b.Second);
        if (a.Millisecond != b.Millisecond) return a.Millisecond.CompareTo(b.Millisecond);
        if (a.Microsecond != b.Microsecond) return a.Microsecond.CompareTo(b.Microsecond);
        return a.Nanosecond.CompareTo(b.Nanosecond);
    }

    /// <summary>
    /// Compares two ISO date-times.
    /// </summary>
    public static int CompareIsoDateTimes(IsoDateTime a, IsoDateTime b)
    {
        var dateComparison = CompareIsoDates(a.Date, b.Date);
        if (dateComparison != 0) return dateComparison;
        return CompareIsoTimes(a.Time, b.Time);
    }

    /// <summary>
    /// Throws a RangeError for an invalid Temporal value.
    /// </summary>
    public static void ThrowRangeError(Realm realm, string message)
    {
        Throw.RangeError(realm, message);
    }

    /// <summary>
    /// Throws a TypeError for an invalid Temporal operation.
    /// </summary>
    public static void ThrowTypeError(Realm realm, string message)
    {
        Throw.TypeError(realm, message);
    }

    /// <summary>
    /// Formats an ISO date as a string.
    /// </summary>
    public static string FormatIsoDate(IsoDate date, string? calendarId = null)
    {
        var sb = new StringBuilder();

        if (date.Year < 0 || date.Year > 9999)
        {
            sb.Append(date.Year >= 0 ? '+' : '-');
            sb.Append(System.Math.Abs(date.Year).ToString("D6", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(date.Year.ToString("D4", CultureInfo.InvariantCulture));
        }

        sb.Append('-');
        sb.Append(date.Month.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('-');
        sb.Append(date.Day.ToString("D2", CultureInfo.InvariantCulture));

        if (calendarId is not null && !string.Equals(calendarId, "iso8601", StringComparison.Ordinal))
        {
            sb.Append("[u-ca=");
            sb.Append(calendarId);
            sb.Append(']');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats an ISO time as a string.
    /// </summary>
    public static string FormatIsoTime(IsoTime time, string? precision = null)
    {
        var sb = new StringBuilder();
        sb.Append(time.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(time.Minute.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(time.Second.ToString("D2", CultureInfo.InvariantCulture));

        var subSecond = time.Millisecond * 1_000_000L + time.Microsecond * 1_000L + time.Nanosecond;
        if (subSecond != 0)
        {
            sb.Append('.');
            var fraction = subSecond.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
            sb.Append(fraction);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats an ISO date-time as a string.
    /// </summary>
    public static string FormatIsoDateTime(IsoDateTime dateTime, string? calendarId = null)
    {
        return FormatIsoDate(dateTime.Date) + "T" + FormatIsoTime(dateTime.Time) +
               (calendarId is not null && !string.Equals(calendarId, "iso8601", StringComparison.Ordinal) ? $"[u-ca={calendarId}]" : "");
    }

    /// <summary>
    /// Formats a duration as an ISO 8601 duration string.
    /// </summary>
    public static string FormatDuration(DurationRecord duration)
    {
        var sb = new StringBuilder();

        if (duration.Sign() < 0)
        {
            sb.Append('-');
        }

        sb.Append('P');

        var absYears = System.Math.Abs(duration.Years);
        var absMonths = System.Math.Abs(duration.Months);
        var absWeeks = System.Math.Abs(duration.Weeks);
        var absDays = System.Math.Abs(duration.Days);
        var absHours = System.Math.Abs(duration.Hours);
        var absMinutes = System.Math.Abs(duration.Minutes);
        var absSeconds = System.Math.Abs(duration.Seconds);
        var absMilliseconds = System.Math.Abs(duration.Milliseconds);
        var absMicroseconds = System.Math.Abs(duration.Microseconds);
        var absNanoseconds = System.Math.Abs(duration.Nanoseconds);

        if (absYears != 0) sb.Append(absYears.ToString(CultureInfo.InvariantCulture)).Append('Y');
        if (absMonths != 0) sb.Append(absMonths.ToString(CultureInfo.InvariantCulture)).Append('M');
        if (absWeeks != 0) sb.Append(absWeeks.ToString(CultureInfo.InvariantCulture)).Append('W');
        if (absDays != 0) sb.Append(absDays.ToString(CultureInfo.InvariantCulture)).Append('D');

        var hasTime = absHours != 0 || absMinutes != 0 || absSeconds != 0 ||
                      absMilliseconds != 0 || absMicroseconds != 0 || absNanoseconds != 0;

        if (hasTime)
        {
            sb.Append('T');
            if (absHours != 0) sb.Append(absHours.ToString(CultureInfo.InvariantCulture)).Append('H');
            if (absMinutes != 0) sb.Append(absMinutes.ToString(CultureInfo.InvariantCulture)).Append('M');

            if (absSeconds != 0 || absMilliseconds != 0 || absMicroseconds != 0 || absNanoseconds != 0)
            {
                sb.Append(absSeconds.ToString(CultureInfo.InvariantCulture));
                var subSecond = absMilliseconds * 1_000_000 + absMicroseconds * 1_000 + absNanoseconds;
                if (subSecond != 0)
                {
                    sb.Append('.');
                    var fraction = ((long) subSecond).ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                    sb.Append(fraction);
                }
                sb.Append('S');
            }
        }

        // Handle zero duration
        if (sb.Length == 1)
        {
            sb.Append("T0S");
        }

        return sb.ToString();
    }
}

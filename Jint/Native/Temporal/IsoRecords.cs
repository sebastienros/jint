using System.Numerics;
using System.Runtime.InteropServices;

namespace Jint.Native.Temporal;

/// <summary>
/// Internal ISO date record for efficient storage.
/// https://tc39.es/proposal-temporal/#sec-temporal-iso-date-records
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct IsoDate(int Year, int Month, int Day)
{
    /// <summary>
    /// Validates that this date represents a valid ISO calendar date.
    /// </summary>
    public bool IsValid()
    {
        if (Month < 1 || Month > 12)
            return false;

        var daysInMonth = IsoDateInMonth(Year, Month);
        return Day >= 1 && Day <= daysInMonth;
    }

    /// <summary>
    /// Returns the number of days in the given month for the given year.
    /// </summary>
    public static int IsoDateInMonth(int year, int month)
    {
        return month switch
        {
            1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
            4 or 6 or 9 or 11 => 30,
            2 => IsLeapYear(year) ? 29 : 28,
            _ => 0
        };
    }

    /// <summary>
    /// Returns true if the given year is a leap year in the ISO calendar.
    /// </summary>
    public static bool IsLeapYear(int year)
    {
        return (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
    }

    /// <summary>
    /// Returns the day of the year (1-366).
    /// </summary>
    public int DayOfYear()
    {
        int[] daysBeforeMonth = IsLeapYear(Year)
            ? [0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335]
            : [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];

        return daysBeforeMonth[Month - 1] + Day;
    }

    /// <summary>
    /// Returns the day of the week (1 = Monday, 7 = Sunday).
    /// </summary>
    public int DayOfWeek()
    {
        // Using Zeller's congruence adapted for Monday = 1
        var y = Year;
        var m = Month;
        if (m < 3)
        {
            m += 12;
            y--;
        }
        var k = y % 100;
        var j = y / 100;
        var h = (Day + (13 * (m + 1)) / 5 + k + k / 4 + j / 4 - 2 * j) % 7;
        // Convert from Zeller (0 = Saturday) to ISO (1 = Monday)
        var dow = ((h + 5) % 7) + 1;
        return dow;
    }

    /// <summary>
    /// Returns the ISO week of the year (1-53).
    /// </summary>
    public int WeekOfYear()
    {
        var doy = DayOfYear();
        var dow = DayOfWeek();
        var week = (doy - dow + 10) / 7;

        if (week < 1)
        {
            // Last week of previous year
            return new IsoDate(Year - 1, 12, 31).WeekOfYear();
        }

        if (week > 52)
        {
            // Check if it's week 1 of next year
            var daysInYear = IsLeapYear(Year) ? 366 : 365;
            var remainingDays = daysInYear - doy;
            if (remainingDays < 4 - dow)
            {
                return 1;
            }
        }

        return week;
    }

    /// <summary>
    /// Returns the year for the ISO week (may differ from calendar year at year boundaries).
    /// </summary>
    public int YearOfWeek()
    {
        var doy = DayOfYear();
        var dow = DayOfWeek();
        var week = (doy - dow + 10) / 7;

        if (week < 1)
            return Year - 1;

        if (week > 52)
        {
            var daysInYear = IsLeapYear(Year) ? 366 : 365;
            var remainingDays = daysInYear - doy;
            if (remainingDays < 4 - dow)
                return Year + 1;
        }

        return Year;
    }

    /// <summary>
    /// Returns the number of days in the year.
    /// </summary>
    public int DaysInYear() => IsLeapYear(Year) ? 366 : 365;

    /// <summary>
    /// Returns the number of days in the month.
    /// </summary>
    public int DaysInMonth() => IsoDateInMonth(Year, Month);

    /// <summary>
    /// Returns the number of weeks in the year (52 or 53).
    /// </summary>
    public int WeeksInYear()
    {
        // A year has 53 weeks if:
        // - Jan 1 is Thursday, or
        // - Jan 1 is Wednesday and it's a leap year
        var jan1Dow = new IsoDate(Year, 1, 1).DayOfWeek();
        if (jan1Dow == 4)
            return 53;
        if (jan1Dow == 3 && IsLeapYear(Year))
            return 53;
        return 52;
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}-{Day:D2}";
}

/// <summary>
/// Internal ISO time record for efficient storage.
/// https://tc39.es/proposal-temporal/#sec-temporal-time-records
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct IsoTime(
    int Hour,
    int Minute,
    int Second,
    int Millisecond,
    int Microsecond,
    int Nanosecond)
{
    public static readonly IsoTime Midnight = new(0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Validates that this time represents a valid time of day.
    /// </summary>
    public bool IsValid()
    {
        return Hour >= 0 && Hour <= 23 &&
               Minute >= 0 && Minute <= 59 &&
               Second >= 0 && Second <= 59 &&
               Millisecond >= 0 && Millisecond <= 999 &&
               Microsecond >= 0 && Microsecond <= 999 &&
               Nanosecond >= 0 && Nanosecond <= 999;
    }

    /// <summary>
    /// Returns the total nanoseconds since midnight.
    /// </summary>
    public long TotalNanoseconds()
    {
        return (long)Hour * 3_600_000_000_000L +
               (long)Minute * 60_000_000_000L +
               (long)Second * 1_000_000_000L +
               (long)Millisecond * 1_000_000L +
               (long)Microsecond * 1_000L +
               Nanosecond;
    }

    /// <summary>
    /// Creates an IsoTime from total nanoseconds since midnight.
    /// </summary>
    public static IsoTime FromNanoseconds(long nanoseconds)
    {
        var ns = nanoseconds % 1_000;
        nanoseconds /= 1_000;
        var us = nanoseconds % 1_000;
        nanoseconds /= 1_000;
        var ms = nanoseconds % 1_000;
        nanoseconds /= 1_000;
        var s = nanoseconds % 60;
        nanoseconds /= 60;
        var m = nanoseconds % 60;
        var h = nanoseconds / 60;

        return new IsoTime((int)h, (int)m, (int)s, (int)ms, (int)us, (int)ns);
    }

    public override string ToString()
    {
        if (Nanosecond != 0)
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}.{Millisecond:D3}{Microsecond:D3}{Nanosecond:D3}";
        if (Microsecond != 0)
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}.{Millisecond:D3}{Microsecond:D3}";
        if (Millisecond != 0)
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}.{Millisecond:D3}";
        if (Second != 0)
            return $"{Hour:D2}:{Minute:D2}:{Second:D2}";
        return $"{Hour:D2}:{Minute:D2}";
    }
}

/// <summary>
/// Internal ISO date-time record combining date and time.
/// https://tc39.es/proposal-temporal/#sec-temporal-isodatetime-records
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct IsoDateTime(IsoDate Date, IsoTime Time)
{
    public int Year => Date.Year;
    public int Month => Date.Month;
    public int Day => Date.Day;
    public int Hour => Time.Hour;
    public int Minute => Time.Minute;
    public int Second => Time.Second;
    public int Millisecond => Time.Millisecond;
    public int Microsecond => Time.Microsecond;
    public int Nanosecond => Time.Nanosecond;

    public IsoDateTime(
        int year, int month, int day,
        int hour = 0, int minute = 0, int second = 0,
        int millisecond = 0, int microsecond = 0, int nanosecond = 0)
        : this(new IsoDate(year, month, day), new IsoTime(hour, minute, second, millisecond, microsecond, nanosecond))
    {
    }

    public bool IsValid() => Date.IsValid() && Time.IsValid();

    public override string ToString() => $"{Date}T{Time}";
}

/// <summary>
/// Internal duration record storing all duration components.
/// https://tc39.es/proposal-temporal/#sec-temporal-duration-records
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct DurationRecord(
    double Years,
    double Months,
    double Weeks,
    double Days,
    double Hours,
    double Minutes,
    double Seconds,
    double Milliseconds,
    double Microseconds,
    double Nanoseconds)
{
    public static readonly DurationRecord Zero = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Returns the sign of this duration (-1, 0, or 1).
    /// </summary>
    public int Sign()
    {
        if (Years < 0 || Months < 0 || Weeks < 0 || Days < 0 ||
            Hours < 0 || Minutes < 0 || Seconds < 0 ||
            Milliseconds < 0 || Microseconds < 0 || Nanoseconds < 0)
            return -1;

        if (Years > 0 || Months > 0 || Weeks > 0 || Days > 0 ||
            Hours > 0 || Minutes > 0 || Seconds > 0 ||
            Milliseconds > 0 || Microseconds > 0 || Nanoseconds > 0)
            return 1;

        return 0;
    }

    /// <summary>
    /// Returns true if all components are zero.
    /// </summary>
    public bool IsZero() => Sign() == 0;

    /// <summary>
    /// Returns a negated copy of this duration.
    /// </summary>
    public DurationRecord Negated() => new(
        Years == 0 ? 0 : -Years,
        Months == 0 ? 0 : -Months,
        Weeks == 0 ? 0 : -Weeks,
        Days == 0 ? 0 : -Days,
        Hours == 0 ? 0 : -Hours,
        Minutes == 0 ? 0 : -Minutes,
        Seconds == 0 ? 0 : -Seconds,
        Milliseconds == 0 ? 0 : -Milliseconds,
        Microseconds == 0 ? 0 : -Microseconds,
        Nanoseconds == 0 ? 0 : -Nanoseconds);

    /// <summary>
    /// Returns the absolute value of this duration.
    /// </summary>
    public DurationRecord Abs() => new(
        System.Math.Abs(Years),
        System.Math.Abs(Months),
        System.Math.Abs(Weeks),
        System.Math.Abs(Days),
        System.Math.Abs(Hours),
        System.Math.Abs(Minutes),
        System.Math.Abs(Seconds),
        System.Math.Abs(Milliseconds),
        System.Math.Abs(Microseconds),
        System.Math.Abs(Nanoseconds));
}

/// <summary>
/// Internal time duration record (no date components).
/// https://tc39.es/proposal-temporal/#sec-temporal-time-duration-records
/// </summary>
internal readonly record struct TimeDuration
{
    // Store as total nanoseconds using BigInteger for full precision
    private readonly BigInteger _nanoseconds;

    public TimeDuration(BigInteger nanoseconds)
    {
        _nanoseconds = nanoseconds;
    }

    public TimeDuration(double days, double hours, double minutes, double seconds,
        double milliseconds, double microseconds, double nanoseconds)
    {
        _nanoseconds = (BigInteger)(days * 86_400_000_000_000.0) +
                       (BigInteger)(hours * 3_600_000_000_000.0) +
                       (BigInteger)(minutes * 60_000_000_000.0) +
                       (BigInteger)(seconds * 1_000_000_000.0) +
                       (BigInteger)(milliseconds * 1_000_000.0) +
                       (BigInteger)(microseconds * 1_000.0) +
                       (BigInteger)nanoseconds;
    }

    public BigInteger TotalNanoseconds => _nanoseconds;

    public int Sign() => _nanoseconds.Sign;

    public TimeDuration Negated() => new(-_nanoseconds);

    public TimeDuration Abs() => new(BigInteger.Abs(_nanoseconds));

    public static TimeDuration operator +(TimeDuration a, TimeDuration b) =>
        new(a._nanoseconds + b._nanoseconds);

    public static TimeDuration operator -(TimeDuration a, TimeDuration b) =>
        new(a._nanoseconds - b._nanoseconds);
}

/// <summary>
/// Parsed ISO date-time result from parsing.
/// </summary>
internal readonly record struct ParsedIsoDateTime(
    IsoDate? Date,
    IsoTime? Time,
    string? TimeZone,
    string? TimeZoneOffsetString,
    int? OffsetMinutes,
    string? Calendar);

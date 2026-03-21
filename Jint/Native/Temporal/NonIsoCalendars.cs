using System.Globalization;
using System.Runtime.InteropServices;

namespace Jint.Native.Temporal;

/// <summary>
/// Provides calendar operations for non-ISO calendars using .NET's built-in Calendar classes.
/// Supports: Chinese, Dangi, Hebrew, Persian.
/// Gregorian-based calendars (iso8601, gregory, japanese, roc, buddhist) are handled
/// directly in TemporalHelpers using ISO arithmetic.
/// </summary>
internal static class NonIsoCalendars
{
    // Lazy calendar instances to avoid startup overhead
    private static ChineseLunisolarCalendar? _chineseCalendar;
    private static KoreanLunisolarCalendar? _koreanCalendar;
    private static HebrewCalendar? _hebrewCalendar;
    private static PersianCalendar? _persianCalendar;

    private static ChineseLunisolarCalendar ChineseCal => _chineseCalendar ??= new ChineseLunisolarCalendar();
    private static KoreanLunisolarCalendar DangiCal => _koreanCalendar ??= new KoreanLunisolarCalendar();
    private static HebrewCalendar HebrewCal => _hebrewCalendar ??= new HebrewCalendar();
    private static PersianCalendar PersianCal => _persianCalendar ??= new PersianCalendar();

    /// <summary>
    /// Result of converting an ISO date to a non-ISO calendar date.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct CalendarDate(
        int Year,
        int Month, // ordinal month (1-based, includes leap months)
        string MonthCode, // M01-M12 or M##L for leap months
        int Day,
        bool IsLeapMonth,
        int MonthsInYear,
        int DaysInMonth,
        int DaysInYear,
        bool InLeapYear);

    /// <summary>
    /// Returns true if the calendar is a non-ISO calendar supported by this adapter.
    /// </summary>
    internal static bool IsNonIsoCalendar(string calendar)
        => calendar is "chinese" or "dangi" or "hebrew" or "persian";

    /// <summary>
    /// Converts an ISO date to calendar-specific fields.
    /// </summary>
    internal static CalendarDate IsoToCalendarDate(string calendar, in IsoDate isoDate)
    {
        try
        {
            return calendar switch
            {
                "chinese" => LunisolarToCalendarDate(ChineseCal, isoDate),
                "dangi" => LunisolarToCalendarDate(DangiCal, isoDate),
                "hebrew" => HebrewToCalendarDate(isoDate),
                "persian" => PersianToCalendarDate(isoDate),
                _ => throw new NotSupportedException($"Calendar '{calendar}' not supported by NonIsoCalendars")
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            // Fall back to ISO-like fields when the ISO date is outside the .NET calendar's range
            var monthCode = $"M{isoDate.Month:D2}";
            var daysInMonth = IsoDate.IsoDateInMonth(isoDate.Year, isoDate.Month);
            var daysInYear = IsoDate.IsLeapYear(isoDate.Year) ? 366 : 365;
            return new CalendarDate(isoDate.Year, isoDate.Month, monthCode, isoDate.Day, false, 12, daysInMonth, daysInYear, IsoDate.IsLeapYear(isoDate.Year));
        }
    }

    /// <summary>
    /// Converts a calendar date to an ISO date. Returns null if the date is invalid with overflow "reject".
    /// </summary>
    internal static IsoDate? CalendarDateToIso(string calendar, int year, string? monthCode, int month, int day, string overflow)
    {
        var result = calendar switch
        {
            "chinese" => LunisolarDateToIso(ChineseCal, year, monthCode, month, day, overflow),
            "dangi" => LunisolarDateToIso(DangiCal, year, monthCode, month, day, overflow),
            "hebrew" => HebrewDateToIso(year, monthCode, month, day, overflow),
            "persian" => PersianDateToIso(year, monthCode, month, day, overflow),
            _ => throw new NotSupportedException($"Calendar '{calendar}' not supported by NonIsoCalendars")
        };

        // If calendar conversion failed (e.g., year out of .NET calendar range),
        // fall back to treating fields as ISO (best effort)
        if (result is null && !string.Equals(overflow, "reject", StringComparison.Ordinal))
        {
            // Don't fall back for fundamentally invalid monthCodes (display month > 12 or < 1)
            if (monthCode is not null)
            {
                var displayMonth = int.Parse(monthCode.AsSpan(1, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
                if (displayMonth < 1 || displayMonth > 12)
                {
                    return null;
                }
            }

            var isoMonth = month > 0 ? month : (monthCode is not null ? int.Parse(monthCode.AsSpan(1, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture) : 1);
            return TemporalHelpers.RegulateIsoDate(year, Clamp(isoMonth, 1, 12), day, overflow);
        }

        return result;
    }

    /// <summary>
    /// Adds years and months to an ISO date using calendar-specific reckoning.
    /// Years are added preserving monthCode semantics (not ordinal month).
    /// Months are added as ordinal month steps.
    /// </summary>
    internal static IsoDate CalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow)
    {
        var cal = GetCalendar(calendar);
        var calDate = IsoToCalendarDate(calendar, isoDate);
        var newYear = calDate.Year + years;
        int newOrdinalMonth;

        // When adding years, preserve monthCode (not ordinal month)
        if (years != 0 && calDate.IsLeapMonth)
        {
            // Was on a leap month - check if it exists in the new year
            var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, newYear);
            if (leapOrdinal > 0 && GetLeapDisplayMonth(calendar, leapOrdinal) == GetLeapDisplayMonth(calendar, GetLeapMonthOrdinal(calendar, cal, calDate.Year)))
            {
                // Same leap month exists in new year
                newOrdinalMonth = leapOrdinal;
            }
            else
            {
                // Leap month doesn't exist in new year
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain to the base month (non-leap version)
                newOrdinalMonth = MonthCodeToOrdinal(calendar, cal, newYear, calDate.MonthCode.Substring(0, 3), overflow);
            }
        }
        else if (years != 0)
        {
            // Not a leap month - resolve the same monthCode in the new year
            newOrdinalMonth = MonthCodeToOrdinal(calendar, cal, newYear, calDate.MonthCode, overflow);
        }
        else
        {
            newOrdinalMonth = calDate.Month;
        }

        // Add months (ordinal stepping)
        if (months != 0)
        {
            newOrdinalMonth += months;
            while (newOrdinalMonth > GetMonthsInYear(calendar, cal, newYear))
            {
                newOrdinalMonth -= GetMonthsInYear(calendar, cal, newYear);
                newYear++;
            }

            while (newOrdinalMonth < 1)
            {
                newYear--;
                newOrdinalMonth += GetMonthsInYear(calendar, cal, newYear);
            }
        }

        // Constrain day
        var maxDay = GetDaysInMonthCal(cal, newYear, newOrdinalMonth);
        var newDay = calDate.Day;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        // Convert back to ISO
        try
        {
            var dt = cal.ToDateTime(newYear, newOrdinalMonth, newDay, 0, 0, 0, 0);
            return new IsoDate(dt.Year, dt.Month, dt.Day);
        }
        catch (ArgumentOutOfRangeException)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            // Fallback: clamp to calendar's valid range
            return ClampToCalendarRange(cal, newYear, newOrdinalMonth, newDay);
        }
    }

    /// <summary>
    /// Computes the difference between two ISO dates using calendar-specific reckoning.
    /// </summary>
    internal static DurationRecord CalendarDateUntil(string calendar, in IsoDate one, in IsoDate two, string largestUnit)
    {
        var sign = TemporalHelpers.CompareIsoDates(one, two);
        if (sign == 0)
        {
            return new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // For day and week, use epoch day arithmetic (same as ISO)
        if (largestUnit is "day" or "week")
        {
            var epochOne = TemporalHelpers.IsoDateToDays(one.Year, one.Month, one.Day);
            var epochTwo = TemporalHelpers.IsoDateToDays(two.Year, two.Month, two.Day);
            var totalDays = (int) (epochTwo - epochOne);

            if (string.Equals(largestUnit, "week", StringComparison.Ordinal))
            {
                var w = totalDays / 7;
                var d = totalDays - w * 7;
                return new DurationRecord(0, 0, w, d, 0, 0, 0, 0, 0, 0);
            }

            return new DurationRecord(0, 0, 0, totalDays, 0, 0, 0, 0, 0, 0);
        }

        // Calendar-aware year/month difference
        sign = -sign; // negate per spec

        var calOne = IsoToCalendarDate(calendar, one);
        var calTwo = IsoToCalendarDate(calendar, two);

        int years = 0;
        if (string.Equals(largestUnit, "year", StringComparison.Ordinal))
        {
            years = calTwo.Year - calOne.Year;
            // Check if we overshot
            if (years != 0)
            {
                var check = CalendarDateAdd(calendar, one, years, 0, "constrain");
                if (sign > 0 && TemporalHelpers.CompareIsoDates(check, two) > 0 ||
                    sign < 0 && TemporalHelpers.CompareIsoDates(check, two) < 0)
                {
                    years -= sign;
                }
            }
        }

        int months = 0;
        if (largestUnit is "year" or "month")
        {
            if (string.Equals(largestUnit, "month", StringComparison.Ordinal))
            {
                // Estimate total months
                var cal = GetCalendar(calendar);
                var totalMonths = 0;
                if (calTwo.Year == calOne.Year)
                {
                    totalMonths = calTwo.Month - calOne.Month;
                }
                else
                {
                    // Count months between years
                    for (var y = calOne.Year; y < calTwo.Year; y++)
                    {
                        totalMonths += GetMonthsInYear(calendar, cal, y);
                    }

                    totalMonths += calTwo.Month - calOne.Month;
                }

                months = totalMonths;
                // Check if we overshot
                if (months != 0)
                {
                    var check = CalendarDateAdd(calendar, one, 0, months, "constrain");
                    if (sign > 0 && TemporalHelpers.CompareIsoDates(check, two) > 0 ||
                        sign < 0 && TemporalHelpers.CompareIsoDates(check, two) < 0)
                    {
                        months -= sign;
                    }
                }
            }
            else
            {
                // largestUnit is "year" - count remaining months after years
                var intermediate = CalendarDateAdd(calendar, one, years, 0, "constrain");
                var candidateMonths = sign;
                while (true)
                {
                    var check = CalendarDateAdd(calendar, one, years, candidateMonths, "constrain");
                    if (sign > 0 && TemporalHelpers.CompareIsoDates(check, two) > 0 ||
                        sign < 0 && TemporalHelpers.CompareIsoDates(check, two) < 0)
                    {
                        break;
                    }

                    months = candidateMonths;
                    candidateMonths += sign;
                }
            }
        }

        // Compute remaining days
        var afterYearsMonths = CalendarDateAdd(calendar, one, years, months, "constrain");
        var epochIntermediate = TemporalHelpers.IsoDateToDays(afterYearsMonths.Year, afterYearsMonths.Month, afterYearsMonths.Day);
        var epochTwoVal = TemporalHelpers.IsoDateToDays(two.Year, two.Month, two.Day);
        var remainingDays = (int) (epochTwoVal - epochIntermediate);

        return new DurationRecord(years, months, 0, remainingDays, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Returns the era string for a non-ISO calendar.
    /// </summary>
    internal static string? CalendarEra(string calendar, in CalendarDate calDate)
    {
        return calendar switch
        {
            "hebrew" => "am",
            "persian" => "ap",
            "chinese" or "dangi" => null, // no eras
            _ => null
        };
    }

    /// <summary>
    /// Returns the eraYear for a non-ISO calendar.
    /// </summary>
    internal static int? CalendarEraYear(string calendar, in CalendarDate calDate)
    {
        return calendar switch
        {
            "hebrew" => calDate.Year,
            "persian" => calDate.Year,
            "chinese" or "dangi" => null, // no eras
            _ => null
        };
    }

    /// <summary>
    /// Resolves a monthCode string to an ordinal month number for a given calendar year.
    /// Returns the ordinal month, or throws/constrains based on overflow.
    /// </summary>
    internal static int MonthCodeToOrdinal(string calendar, Calendar cal, int year, string monthCode, string overflow)
    {
        var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
        var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);

        if (calendar is "persian")
        {
            // Persian has no leap months
            if (isLeap)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain: use the base month
            }

            return Clamp(displayMonth, 1, 12);
        }

        var leapOrdinal = GetLeapMonthOrdinal(calendar, cal, year);

        if (isLeap)
        {
            if (leapOrdinal <= 0)
            {
                // No leap month this year
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain: use the base month
                return displayMonth < leapOrdinal || leapOrdinal <= 0 ? displayMonth : displayMonth + 1;
            }

            var leapDisplay = GetLeapDisplayMonth(calendar, leapOrdinal);
            if (displayMonth != leapDisplay)
            {
                // Wrong leap month
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain: use the base month
                return displayMonth < leapOrdinal ? displayMonth : displayMonth + 1;
            }

            return leapOrdinal;
        }

        // Non-leap month
        if (leapOrdinal > 0 && displayMonth >= leapOrdinal)
        {
            return displayMonth + 1;
        }

        return displayMonth;
    }

    /// <summary>
    /// Validates that month and monthCode are consistent for a non-ISO calendar.
    /// If only monthCode is provided, resolves it to an ordinal month.
    /// If only month is provided, uses the ordinal directly.
    /// If both are provided, validates consistency.
    /// </summary>
    internal static int ResolveMonthAndMonthCode(string calendar, int year, int month, string? monthCode, string overflow)
    {
        var cal = GetCalendar(calendar);

        if (monthCode is not null && month > 0)
        {
            // Both provided - validate consistency
            var resolvedOrdinal = MonthCodeToOrdinal(calendar, cal, year, monthCode, overflow);
            if (resolvedOrdinal != month)
            {
                throw new InvalidOperationException("reject"); // month and monthCode do not match
            }

            return resolvedOrdinal;
        }

        if (monthCode is not null)
        {
            return MonthCodeToOrdinal(calendar, cal, year, monthCode, overflow);
        }

        if (month > 0)
        {
            // Validate ordinal month is in range
            var maxMonths = GetMonthsInYear(calendar, cal, year);
            if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
            {
                return Clamp(month, 1, maxMonths);
            }

            if (month < 1 || month > maxMonths)
            {
                throw new InvalidOperationException("reject");
            }

            return month;
        }

        throw new InvalidOperationException("reject"); // neither month nor monthCode provided
    }

    /// <summary>
    /// Finds a calendar year where (year, monthCode, day) converts to an ISO date
    /// with the given ISO reference year. Used for PlainMonthDay without explicit year.
    /// </summary>
    internal static int FindCalendarReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day)
    {
        var cal = GetCalendar(calendar);

        // For lunisolar calendars, a calendar year spans parts of two ISO years
        // Try multiple calendar years around the ISO reference year
        var approxYear = IsoYearToCalendarYear(calendar, cal, isoReferenceYear);

        for (var y = approxYear - 2; y <= approxYear + 2; y++)
        {
            try
            {
                var ordinal = MonthCodeToOrdinal(calendar, cal, y, monthCode, "reject");
                var maxDay = GetDaysInMonthCal(cal, y, ordinal);
                var clampedDay = System.Math.Min(day, maxDay);
                var dt = cal.ToDateTime(y, ordinal, clampedDay, 0, 0, 0, 0);
                if (dt.Year == isoReferenceYear)
                {
                    return y;
                }
            }
            catch
            {
                // This year doesn't have the requested monthCode
            }
        }

        return approxYear;
    }

    #region Private Helpers

    private static int Clamp(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Determines if a Hebrew year is a leap year using the 19-year Metonic cycle.
    /// Works for any Hebrew year, including those outside .NET HebrewCalendar's range.
    /// In each 19-year cycle, years 3, 6, 8, 11, 14, 17, 19 are leap years.
    /// </summary>
    private static bool IsHebrewLeapYearAlgorithmic(int year)
    {
        return (7 * year + 1) % 19 < 7;
    }

    private static Calendar GetCalendar(string calendar)
    {
        return calendar switch
        {
            "chinese" => ChineseCal,
            "dangi" => DangiCal,
            "hebrew" => HebrewCal,
            "persian" => PersianCal,
            _ => throw new NotSupportedException($"Calendar '{calendar}' not supported")
        };
    }

    /// <summary>
    /// Converts an ISO year to an approximate calendar year.
    /// </summary>
    private static int IsoYearToCalendarYear(string calendar, Calendar cal, int isoYear)
    {
        try
        {
            // Use July 1 of the ISO year as a reference point
            var dt = new DateTime(System.Math.Max(1, System.Math.Min(isoYear, 9999)), 7, 1);
            if (dt >= cal.MinSupportedDateTime && dt <= cal.MaxSupportedDateTime)
            {
                return cal.GetYear(dt);
            }
        }
        catch
        {
            // Fall through
        }

        return isoYear;
    }

    /// <summary>
    /// Gets the ordinal position of the leap month in a given year.
    /// Returns 0 if the year has no leap month.
    /// </summary>
    private static int GetLeapMonthOrdinal(string calendar, Calendar cal, int year)
    {
        if (calendar is "persian")
        {
            return 0; // Persian never has leap months
        }

        if (calendar is "hebrew")
        {
            try
            {
                return HebrewCal.IsLeapYear(year) ? 6 : 0;
            }
            catch
            {
                // Out of .NET range: use algorithmic 19-year cycle
                // Years 3, 6, 8, 11, 14, 17, 19 of each 19-year cycle are leap years
                return IsHebrewLeapYearAlgorithmic(year) ? 6 : 0;
            }
        }

        // Chinese/Dangi: use EastAsianLunisolarCalendar.GetLeapMonth
        try
        {
            return ((EastAsianLunisolarCalendar) cal).GetLeapMonth(year);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets the display month number of the leap month at the given ordinal.
    /// For Chinese/Dangi: leapOrdinal 5 → display 4 (M04L).
    /// For Hebrew: leapOrdinal 6 → display 5 (M05L).
    /// </summary>
    private static int GetLeapDisplayMonth(string calendar, int leapOrdinal)
    {
        return leapOrdinal - 1;
    }

    /// <summary>
    /// Gets the number of months in a calendar year.
    /// </summary>
    private static int GetMonthsInYear(string calendar, Calendar cal, int year)
    {
        if (calendar is "persian")
        {
            return 12;
        }

        try
        {
            return cal.GetMonthsInYear(year);
        }
        catch
        {
            // For Hebrew out-of-range, use algorithmic leap year detection
            if (calendar is "hebrew")
            {
                return IsHebrewLeapYearAlgorithmic(year) ? 13 : 12;
            }

            return 12;
        }
    }

    /// <summary>
    /// Gets the number of days in a calendar month.
    /// </summary>
    private static int GetDaysInMonthCal(Calendar cal, int year, int month)
    {
        try
        {
            return cal.GetDaysInMonth(year, month);
        }
        catch
        {
            return 30; // fallback
        }
    }

    /// <summary>
    /// Clamps a date to the calendar's supported range.
    /// </summary>
    private static IsoDate ClampToCalendarRange(Calendar cal, int year, int month, int day)
    {
        var dt = cal.MaxSupportedDateTime;
        return new IsoDate(dt.Year, dt.Month, dt.Day);
    }

    #endregion

    #region Lunisolar (Chinese/Dangi) Calendar

    private static CalendarDate LunisolarToCalendarDate(EastAsianLunisolarCalendar cal, in IsoDate isoDate)
    {
        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        // If outside supported range, fall back to ISO-like fields
        if (dt < cal.MinSupportedDateTime || dt > cal.MaxSupportedDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(isoDate));
        }

        var year = cal.GetYear(dt);
        var ordinalMonth = cal.GetMonth(dt);
        var day = cal.GetDayOfMonth(dt);
        var leapMonth = cal.GetLeapMonth(year);

        bool isLeapMonth;
        string monthCode;

        if (leapMonth > 0 && ordinalMonth >= leapMonth)
        {
            if (ordinalMonth == leapMonth)
            {
                var displayMonth = leapMonth - 1;
                isLeapMonth = true;
                monthCode = $"M{displayMonth:D2}L";
            }
            else
            {
                var displayMonth = ordinalMonth - 1;
                isLeapMonth = false;
                monthCode = $"M{displayMonth:D2}";
            }
        }
        else
        {
            isLeapMonth = false;
            monthCode = $"M{ordinalMonth:D2}";
        }

        var monthsInYear = leapMonth > 0 ? 13 : 12;
        var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
        var daysInYear = cal.GetDaysInYear(year);
        var inLeapYear = leapMonth > 0;

        return new CalendarDate(year, ordinalMonth, monthCode, day, isLeapMonth, monthsInYear, daysInMonth, daysInYear, inLeapYear);
    }

    private static IsoDate? LunisolarDateToIso(EastAsianLunisolarCalendar cal, int year, string? monthCode, int month, int day, string overflow)
    {
        int ordinalMonth;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            // Display month must be 1-12 (no calendar has display month > 12)
            if (displayMonth < 1 || displayMonth > 12)
            {
                return null;
            }

            var leapMonth = 0;
            try
            {
                leapMonth = cal.GetLeapMonth(year);
            }
            catch
            {
                // Year out of range
            }

            if (isLeap)
            {
                if (leapMonth <= 0 || leapMonth - 1 != displayMonth)
                {
                    if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    // Constrain: use the non-leap version of the month
                    ordinalMonth = leapMonth > 0 && displayMonth >= leapMonth ? displayMonth + 1 : displayMonth;
                }
                else
                {
                    ordinalMonth = leapMonth;
                }
            }
            else
            {
                if (leapMonth > 0 && displayMonth >= leapMonth)
                {
                    ordinalMonth = displayMonth + 1;
                }
                else
                {
                    ordinalMonth = displayMonth;
                }
            }

            // Validate month matches ordinal if both provided
            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                // monthCode takes precedence
            }
        }
        else
        {
            ordinalMonth = month;
        }

        // Constrain ordinal month to valid range
        var maxMonths = 12;
        try
        {
            maxMonths = cal.GetLeapMonth(year) > 0 ? 13 : 12;
        }
        catch
        {
            // Year out of range
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = Clamp(ordinalMonth, 1, maxMonths);
        }
        else if (ordinalMonth < 1 || ordinalMonth > maxMonths)
        {
            return null;
        }

        // Constrain day
        int maxDay;
        try
        {
            maxDay = cal.GetDaysInMonth(year, ordinalMonth);
        }
        catch
        {
            maxDay = 30;
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        try
        {
            var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
            return new IsoDate(dt.Year, dt.Month, dt.Day);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Hebrew Calendar

    private static CalendarDate HebrewToCalendarDate(in IsoDate isoDate)
    {
        var cal = HebrewCal;
        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        if (dt < cal.MinSupportedDateTime || dt > cal.MaxSupportedDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(isoDate));
        }

        var year = cal.GetYear(dt);
        var ordinalMonth = cal.GetMonth(dt);
        var day = cal.GetDayOfMonth(dt);
        var isLeapYear = cal.IsLeapYear(year);

        bool isLeapMonth;
        string monthCode;

        if (isLeapYear)
        {
            // Leap year: 13 months
            // ordinals 1-5 → M01-M05
            // ordinal 6 → M05L (Adar I, the intercalary month)
            // ordinals 7-13 → M06-M12
            if (ordinalMonth <= 5)
            {
                monthCode = $"M{ordinalMonth:D2}";
                isLeapMonth = false;
            }
            else if (ordinalMonth == 6)
            {
                monthCode = "M05L";
                isLeapMonth = true;
            }
            else
            {
                monthCode = $"M{ordinalMonth - 1:D2}";
                isLeapMonth = false;
            }
        }
        else
        {
            // Non-leap year: 12 months, ordinals 1-12 → M01-M12
            monthCode = $"M{ordinalMonth:D2}";
            isLeapMonth = false;
        }

        var monthsInYear = isLeapYear ? 13 : 12;
        var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
        var daysInYear = cal.GetDaysInYear(year);

        return new CalendarDate(year, ordinalMonth, monthCode, day, isLeapMonth, monthsInYear, daysInMonth, daysInYear, isLeapYear);
    }

    private static IsoDate? HebrewDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var cal = HebrewCal;
        int ordinalMonth;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            // Display month must be 1-12
            if (displayMonth < 1 || displayMonth > 12)
            {
                return null;
            }

            bool yearIsLeap;
            try
            {
                yearIsLeap = cal.IsLeapYear(year);
            }
            catch
            {
                yearIsLeap = IsHebrewLeapYearAlgorithmic(year);
            }

            if (isLeap)
            {
                if (!yearIsLeap || displayMonth != 5)
                {
                    // M05L only exists in leap years
                    if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    // Constrain to M05 (non-leap version)
                    ordinalMonth = 5;
                }
                else
                {
                    ordinalMonth = 6; // M05L → ordinal 6
                }
            }
            else
            {
                if (yearIsLeap && displayMonth >= 6)
                {
                    ordinalMonth = displayMonth + 1; // M06 → ordinal 7, etc.
                }
                else
                {
                    ordinalMonth = displayMonth;
                }
            }

            // Validate month matches ordinal if both provided
            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }
        else
        {
            ordinalMonth = month;
        }

        // Constrain ordinal month
        int maxMonths;
        try
        {
            maxMonths = cal.IsLeapYear(year) ? 13 : 12;
        }
        catch
        {
            maxMonths = 12;
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = Clamp(ordinalMonth, 1, maxMonths);
        }
        else if (ordinalMonth < 1 || ordinalMonth > maxMonths)
        {
            return null;
        }

        // Constrain day
        int maxDay;
        try
        {
            maxDay = cal.GetDaysInMonth(year, ordinalMonth);
        }
        catch
        {
            maxDay = 30;
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        try
        {
            var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
            return new IsoDate(dt.Year, dt.Month, dt.Day);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Persian Calendar

    private static CalendarDate PersianToCalendarDate(in IsoDate isoDate)
    {
        var cal = PersianCal;
        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        if (dt < cal.MinSupportedDateTime || dt > cal.MaxSupportedDateTime)
        {
            throw new ArgumentOutOfRangeException(nameof(isoDate));
        }

        var year = cal.GetYear(dt);
        var ordinalMonth = cal.GetMonth(dt);
        var day = cal.GetDayOfMonth(dt);
        var isLeapYear = cal.IsLeapYear(year);

        var monthCode = $"M{ordinalMonth:D2}";
        var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
        var daysInYear = cal.GetDaysInYear(year);

        return new CalendarDate(year, ordinalMonth, monthCode, day, false, 12, daysInMonth, daysInYear, isLeapYear);
    }

    private static IsoDate? PersianDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var cal = PersianCal;
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                // Persian has no leap months
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                ordinalMonth = displayMonth;
            }

            if (month > 0 && month != ordinalMonth)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }
            }
        }

        // Constrain month
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            ordinalMonth = Clamp(ordinalMonth, 1, 12);
        }
        else if (ordinalMonth < 1 || ordinalMonth > 12)
        {
            return null;
        }

        // Constrain day
        int maxDay;
        try
        {
            maxDay = cal.GetDaysInMonth(year, ordinalMonth);
        }
        catch
        {
            // Fallback: Persian months 1-6=31, 7-11=30, 12=29/30
            maxDay = ordinalMonth <= 6 ? 31 : ordinalMonth <= 11 ? 30 : 29;
        }

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        try
        {
            var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
            return new IsoDate(dt.Year, dt.Month, dt.Day);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

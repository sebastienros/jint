using System.Globalization;
using System.Runtime.InteropServices;

namespace Jint.Native.Temporal;

/// <summary>
/// Provides calendar operations for non-ISO calendars using .NET's built-in Calendar classes.
/// Supports: Chinese, Dangi, Hebrew, Persian, Coptic, Ethiopic, EthioAA, Indian, Islamic-Umalqura.
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
    private static UmAlQuraCalendar? _umAlQuraCalendar;

    private static ChineseLunisolarCalendar ChineseCal => _chineseCalendar ??= new ChineseLunisolarCalendar();
    private static KoreanLunisolarCalendar DangiCal => _koreanCalendar ??= new KoreanLunisolarCalendar();
    private static HebrewCalendar HebrewCal => _hebrewCalendar ??= new HebrewCalendar();
    private static PersianCalendar PersianCal => _persianCalendar ??= new PersianCalendar();
    private static UmAlQuraCalendar UmAlQuraCal => _umAlQuraCalendar ??= new UmAlQuraCalendar();

    // Epoch day constants (days since Unix epoch 1970-01-01)
    // Coptic epoch: Coptic year 1, month 1, day 1 = proleptic Gregorian August 29, 284 CE
    // Verified: Coptic (1687, 1, 1) = ISO September 11, 1970
    private const long CopticEpochDays = -615558;

    // Ethiopic epoch: Ethiopic year 1, month 1, day 1 = proleptic Gregorian August 27, 8 CE
    // Verified: Ethiopic (1963, 1, 1) = ISO September 11, 1970
    private const long EthiopicEpochDays = -716367;

    // EthioAA epoch: EthioAA year 1, month 1, day 1
    // Verified: EthioAA (7463, 1, 1) = ISO September 11, 1970
    private const long EthioAAEpochDays = -2725242;

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
        => calendar is "chinese" or "dangi" or "hebrew" or "persian"
            or "coptic" or "ethiopic" or "ethioaa" or "indian"
            or "islamic-umalqura" or "islamic-civil" or "islamic-tbla";

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
                "coptic" => FixedEpochToCalendarDate(CopticEpochDays, isoDate),
                "ethiopic" => FixedEpochToCalendarDate(EthiopicEpochDays, isoDate),
                "ethioaa" => FixedEpochToCalendarDate(EthioAAEpochDays, isoDate),
                "indian" => IndianToCalendarDate(isoDate),
                "islamic-umalqura" => IslamicUmalquraToCalendarDate(isoDate),
                "islamic-civil" => IslamicCivilToCalendarDate(isoDate, 1948439L),
                "islamic-tbla" => IslamicCivilToCalendarDate(isoDate, 1948438L),
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
            "coptic" => FixedEpochDateToIso(CopticEpochDays, 13, year, monthCode, month, day, overflow),
            "ethiopic" => FixedEpochDateToIso(EthiopicEpochDays, 13, year, monthCode, month, day, overflow),
            "ethioaa" => FixedEpochDateToIso(EthioAAEpochDays, 13, year, monthCode, month, day, overflow),
            "indian" => IndianDateToIso(year, monthCode, month, day, overflow),
            "islamic-umalqura" => IslamicUmalquraDateToIso(year, monthCode, month, day, overflow),
            "islamic-civil" => IslamicCivilTabularDateToIso(year, monthCode, month, day, overflow, 1948439L),
            "islamic-tbla" => IslamicCivilTabularDateToIso(year, monthCode, month, day, overflow, 1948438L),
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
        // For calendars that use epoch-day arithmetic (coptic/ethiopic/ethioaa)
        // or Indian calendar, handle them directly
        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            return FixedEpochCalendarDateAdd(calendar, isoDate, years, months, overflow);
        }

        if (calendar is "indian")
        {
            return IndianCalendarDateAdd(isoDate, years, months, overflow);
        }

        if (calendar is "islamic-civil" or "islamic-tbla" or "islamic-umalqura")
        {
            return IslamicTabularCalendarDateAdd(calendar, isoDate, years, months, overflow);
        }

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
                Calendar? cal = calendar is "coptic" or "ethiopic" or "ethioaa" or "indian" or "islamic-civil" or "islamic-tbla" or "islamic-umalqura" ? null : GetCalendar(calendar);
                var totalMonths = 0;
                if (calTwo.Year == calOne.Year)
                {
                    totalMonths = calTwo.Month - calOne.Month;
                }
                else if (calTwo.Year > calOne.Year)
                {
                    // Forward: count months from calOne to calTwo
                    for (var y = calOne.Year; y < calTwo.Year; y++)
                    {
                        totalMonths += GetMonthsInYear(calendar, cal, y);
                    }

                    totalMonths += calTwo.Month - calOne.Month;
                }
                else
                {
                    // Backward: count months from calTwo to calOne (negative)
                    for (var y = calTwo.Year; y < calOne.Year; y++)
                    {
                        totalMonths -= GetMonthsInYear(calendar, cal, y);
                    }

                    totalMonths += calTwo.Month - calOne.Month;
                }

                months = totalMonths;
                // Check if we overshot (including day constraint cases)
                if (months != 0)
                {
                    var check = CalendarDateAdd(calendar, one, 0, months, "constrain");
                    var cmp = TemporalHelpers.CompareIsoDates(check, two);
                    if (sign > 0 && cmp > 0 || sign < 0 && cmp < 0)
                    {
                        months -= sign;
                    }
                    else if (cmp == 0)
                    {
                        // Intermediate equals target - check if day was constrained
                        // If adding months to calOne constrained the day down, we need to verify
                        // by checking remaining days: if they'd be in the wrong direction, reduce months
                        var checkCal = IsoToCalendarDate(calendar, check);
                        var oneCal = IsoToCalendarDate(calendar, one);
                        if (sign > 0 && checkCal.Day < oneCal.Day && checkCal.Day == IsoToCalendarDate(calendar, two).Day)
                        {
                            // Day was constrained down and matches target exactly - this is the wrapping case
                            // The month shouldn't count because we only got there via constraining
                            months -= sign;
                        }
                        else if (sign < 0 && checkCal.Day > oneCal.Day && checkCal.Day == IsoToCalendarDate(calendar, two).Day)
                        {
                            months -= sign;
                        }
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
                    var cmp = TemporalHelpers.CompareIsoDates(check, two);
                    if (sign > 0 && cmp > 0 || sign < 0 && cmp < 0)
                    {
                        break;
                    }

                    // Check for day constraining (wrapping at end of month)
                    if (cmp == 0)
                    {
                        var checkCal = IsoToCalendarDate(calendar, check);
                        var oneCal = IsoToCalendarDate(calendar, one);
                        if (sign > 0 && checkCal.Day < oneCal.Day ||
                            sign < 0 && checkCal.Day > oneCal.Day)
                        {
                            // Day constrained - don't count this month
                            break;
                        }
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
            "coptic" => calDate.Year >= 1 ? "am" : "bd",
            "ethiopic" => calDate.Year >= 1 ? "am" : "aa",
            "ethioaa" => "aa",
            "indian" => "shaka",
            "islamic-umalqura" or "islamic-civil" or "islamic-tbla" => calDate.Year >= 1 ? "ah" : "bh",
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
            "coptic" => calDate.Year >= 1 ? calDate.Year : 1 - calDate.Year,
            "ethiopic" => calDate.Year >= 1 ? calDate.Year : calDate.Year + 5500,
            "ethioaa" => calDate.Year,
            "indian" => calDate.Year,
            "islamic-umalqura" or "islamic-civil" or "islamic-tbla" => calDate.Year >= 1 ? calDate.Year : 1 - calDate.Year,
            _ => null
        };
    }

    /// <summary>
    /// Resolves a monthCode string to an ordinal month number for a given calendar year.
    /// Returns the ordinal month, or throws/constrains based on overflow.
    /// </summary>
    internal static int MonthCodeToOrdinal(string calendar, Calendar? cal, int year, string monthCode, string overflow)
    {
        var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
        var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);

        if (calendar is "persian" or "indian" or "islamic-umalqura" or "islamic-civil" or "islamic-tbla")
        {
            // These calendars have no leap months
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

        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            // 13-month calendars with no leap months
            if (isLeap)
            {
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("reject");
                }

                // Constrain: use the base month
            }

            return Clamp(displayMonth, 1, 13);
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
        Calendar? cal = calendar is "coptic" or "ethiopic" or "ethioaa" or "indian" ? null : GetCalendar(calendar);

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
        // For calendars that don't use .NET Calendar class
        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            return FindFixedEpochReferenceYear(calendar, isoReferenceYear, monthCode, day);
        }

        if (calendar is "indian")
        {
            return FindIndianReferenceYear(isoReferenceYear, monthCode, day);
        }

        if (calendar is "islamic-civil" or "islamic-tbla")
        {
            return FindIslamicTabularReferenceYear(calendar, isoReferenceYear, monthCode, day);
        }

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
            "islamic-umalqura" => UmAlQuraCal,
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
    private static int GetLeapMonthOrdinal(string calendar, Calendar? cal, int year)
    {
        if (calendar is "persian" or "coptic" or "ethiopic" or "ethioaa" or "indian" or "islamic-umalqura")
        {
            return 0; // These calendars never have leap months
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
        if (cal is null)
        {
            return 0;
        }

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
    private static int GetMonthsInYear(string calendar, Calendar? cal, int year)
    {
        if (calendar is "persian" or "indian" or "islamic-umalqura" or "islamic-civil" or "islamic-tbla")
        {
            return 12;
        }

        if (calendar is "coptic" or "ethiopic" or "ethioaa")
        {
            return 13;
        }

        if (cal is null)
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
    private static int GetDaysInMonthCal(Calendar? cal, int year, int month)
    {
        if (cal is null)
        {
            return 30; // fallback for calendars without .NET Calendar
        }

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

    #region Fixed-Epoch Calendars (Coptic/Ethiopic/EthioAA)

    /// <summary>
    /// Returns the number of leap years from year 1 through year n (inclusive)
    /// for calendars where year % 4 == 3 is a leap year.
    /// </summary>
    private static long FixedEpochLeapCount(long n)
    {
        if (n < 3) return 0;
        return (n - 3) / 4 + 1;
    }

    /// <summary>
    /// Returns true if a fixed-epoch calendar year is a leap year.
    /// Leap years are those where year % 4 == 3 (years 3, 7, 11, ...).
    /// </summary>
    private static bool IsFixedEpochLeapYear(int year)
    {
        // Handle negative years: need mathematical modulo (always non-negative)
        var mod = ((year % 4) + 4) % 4;
        return mod == 3;
    }

    /// <summary>
    /// Returns the number of days from the calendar epoch to the start of the given year.
    /// </summary>
    private static long FixedEpochDaysToYear(int year)
    {
        if (year >= 1)
        {
            return (long) (year - 1) * 365 + FixedEpochLeapCount(year - 1);
        }

        // For years <= 0 (proleptic): count backwards
        // Year 0 is 1 year before year 1, year -1 is 2 years before, etc.
        // Each year has 365 or 366 days. In the proleptic direction,
        // we need to handle the leap pattern correctly.
        // Year Y (Y <= 0): daysToYear(Y) = -(daysFromYear(Y) to year 1)
        // daysFromYear(Y) to year 1 = sum of days in years Y, Y+1, ..., 0
        // For simplicity, use the formula: (Y-1)*365 + leapCount
        // where leapCount handles negative years
        var y = (long) year - 1; // years elapsed (negative)
        // For negative y, floor(y/4) gives the correct negative leap count
        // But our leap rule is year%4==3, so for year Y:
        // Leap years below 1: -1 (mod4=3), -5 (mod4=3), -9 (mod4=3), ...
        // Count of leap years from Y to 0 inclusive where ((k%4)+4)%4 == 3:
        // k = -1, -5, -9, ..., Y (if Y matches pattern)
        // Total days = y * 365 + (negative leap count)

        // Use a different approach: convert to positive counting
        // Let p = 1 - year (p >= 1 for year <= 0)
        // The leap years in range [year, 0] are at positions where mod4==3
        // Equivalently, leap count = FixedEpochLeapCount(p) considering the shift
        // Actually, for proleptic years, the leap pattern mirrors:
        // Year -1: (-1+4)%4=3, leap. Year 0: 0%4=0, not leap.
        // Year -2: (-2+4)%4=2, not. Year -3: (-3+4)%4=1, not. Year -4: (-4+4)%4=0, not.
        // Year -5: (-5+8)%4=3, leap.
        // So leap years: -1, -5, -9, ... i.e., every 4 years starting from -1
        // Count of leap years from year to -1 (inclusive, for years year..0 that are leap):
        // = count of k in [year, 0] where ((k%4)+4)%4 == 3
        // = count of k in [year, -1] where ((k%4)+4)%4 == 3 (since year 0 is not leap)
        // The leap years are -1, -5, -9, ..., down to year
        // If year <= -1: count = floor((-year - 1) / 4) + 1 when ((-year-1)%4+4)%4... actually
        // count = floor((- year) / 4) ... let me just count directly:
        // Years from (year) to (-1) that are leap:
        // These are: -1, -5, -9, ..., last >= year
        // First (largest): -1. Step: -4.
        // Count = floor((-1 - year) / 4) + 1 when year <= -1

        // Days from epoch to year Y (Y <= 0):
        // = -(days from Y to 1) = -( sum_{k=Y}^{0} daysInYear(k) )
        // daysInYear(k) = 366 if leap(k), else 365
        // = -( (1-Y)*365 + leapsBetween(Y, 0) )
        long p = 1 - year; // number of years from Y to 0 inclusive
        long leapsBetween;
        if (year <= -1)
        {
            leapsBetween = (-1 - year) / 4 + 1;
        }
        else
        {
            // year == 0
            leapsBetween = 0;
        }

        return -(p * 365 + leapsBetween);
    }

    /// <summary>
    /// Returns the number of days in the given month of a fixed-epoch calendar year.
    /// Months 1-12 have 30 days each. Month 13 has 5 days (6 in leap year).
    /// </summary>
    private static int FixedEpochDaysInMonth(int year, int month)
    {
        if (month >= 1 && month <= 12) return 30;
        if (month == 13) return IsFixedEpochLeapYear(year) ? 6 : 5;
        return 30; // fallback
    }

    /// <summary>
    /// Converts a fixed-epoch calendar date to epoch days (days since Unix epoch).
    /// </summary>
    private static long FixedEpochToEpochDays(long calendarEpochDays, int year, int month, int day)
    {
        return calendarEpochDays + FixedEpochDaysToYear(year) + (month - 1) * 30 + day - 1;
    }

    /// <summary>
    /// Converts epoch days to a fixed-epoch calendar date.
    /// </summary>
    private static void EpochDaysToFixedEpoch(long calendarEpochDays, long epochDays, out int year, out int month, out int day)
    {
        var dfc = epochDays - calendarEpochDays; // days from calendar epoch

        // Approximate year
        if (dfc >= 0)
        {
            year = (int) (dfc / 365) + 1;
            // Adjust: we may have overshot
            while (FixedEpochDaysToYear(year) > dfc)
            {
                year--;
            }

            while (FixedEpochDaysToYear(year + 1) <= dfc)
            {
                year++;
            }
        }
        else
        {
            year = (int) (dfc / 366); // conservative (may undershoot)
            while (FixedEpochDaysToYear(year) > dfc)
            {
                year--;
            }

            while (FixedEpochDaysToYear(year + 1) <= dfc)
            {
                year++;
            }
        }

        var dayOfYear = (int) (dfc - FixedEpochDaysToYear(year)); // 0-based
        month = dayOfYear / 30 + 1;
        if (month > 13) month = 13;
        day = dayOfYear - (month - 1) * 30 + 1;
    }

    /// <summary>
    /// Converts an ISO date to a fixed-epoch calendar date (Coptic/Ethiopic/EthioAA).
    /// </summary>
    private static CalendarDate FixedEpochToCalendarDate(long calendarEpochDays, in IsoDate isoDate)
    {
        var epochDays = TemporalHelpers.IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
        EpochDaysToFixedEpoch(calendarEpochDays, epochDays, out var year, out var month, out var day);

        var monthCode = $"M{month:D2}";
        var isLeapYear = IsFixedEpochLeapYear(year);
        var daysInMonth = FixedEpochDaysInMonth(year, month);
        var daysInYear = isLeapYear ? 366 : 365;

        return new CalendarDate(year, month, monthCode, day, false, 13, daysInMonth, daysInYear, isLeapYear);
    }

    /// <summary>
    /// Converts a fixed-epoch calendar date to an ISO date.
    /// </summary>
    private static IsoDate? FixedEpochDateToIso(long calendarEpochDays, int maxMonth, int year, string? monthCode, int month, int day, string overflow)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                // These calendars have no leap months
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    return null;
                }

                ordinalMonth = displayMonth;
            }
            else
            {
                // Validate display month range (1-13 for coptic/ethiopic/ethioaa)
                if (displayMonth < 1 || displayMonth > maxMonth)
                {
                    if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    ordinalMonth = Clamp(displayMonth, 1, maxMonth);
                }
                else
                {
                    ordinalMonth = displayMonth;
                }
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
            ordinalMonth = Clamp(ordinalMonth, 1, maxMonth);
        }
        else if (ordinalMonth < 1 || ordinalMonth > maxMonth)
        {
            return null;
        }

        // Constrain day
        var maxDay = FixedEpochDaysInMonth(year, ordinalMonth);

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        var epochDays = FixedEpochToEpochDays(calendarEpochDays, year, ordinalMonth, day);
        return TemporalHelpers.DaysToIsoDate(epochDays);
    }

    /// <summary>
    /// CalendarDateAdd for fixed-epoch calendars (Coptic/Ethiopic/EthioAA).
    /// </summary>
    private static IsoDate FixedEpochCalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow)
    {
        var epochDays = GetCalendarEpochDays(calendar);
        var isoEpochDays = TemporalHelpers.IsoDateToDays(isoDate.Year, isoDate.Month, isoDate.Day);
        EpochDaysToFixedEpoch(epochDays, isoEpochDays, out var calYear, out var calMonth, out var calDay);

        var newYear = calYear + years;
        var newMonth = calMonth;

        // Add months (ordinal stepping through 13-month years)
        if (months != 0)
        {
            newMonth += months;
            while (newMonth > 13)
            {
                newMonth -= 13;
                newYear++;
            }

            while (newMonth < 1)
            {
                newYear--;
                newMonth += 13;
            }
        }

        // Constrain day
        var maxDay = FixedEpochDaysInMonth(newYear, newMonth);
        var newDay = calDay;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        var resultEpochDays = FixedEpochToEpochDays(epochDays, newYear, newMonth, newDay);
        return TemporalHelpers.DaysToIsoDate(resultEpochDays);
    }

    /// <summary>
    /// Finds a calendar reference year for a fixed-epoch calendar such that
    /// (calendarYear, monthCode, day) maps to an ISO date in isoReferenceYear.
    /// </summary>
    private static int FindFixedEpochReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day)
    {
        var epochDays = GetCalendarEpochDays(calendar);
        var monthNum = MonthCodeToMonthNumber(monthCode);

        // For M13 and days that require a leap year, search for a leap calendar year
        // that maps to an ISO year near the reference year.
        // Strategy: try ISO reference years starting from the target and going back,
        // preferring years where the month has the maximum number of days.
        var bestYear = 0;
        var bestMaxDay = 0;
        var bestIsoYear = 0;

        for (var isoOffset = 0; isoOffset <= 4; isoOffset++)
        {
            var targetIsoYear = isoReferenceYear - isoOffset;
            var midYearDays = TemporalHelpers.IsoDateToDays(targetIsoYear, 7, 1);
            EpochDaysToFixedEpoch(epochDays, midYearDays, out var approxYear, out _, out _);

            for (var y = approxYear - 2; y <= approxYear + 2; y++)
            {
                var maxDay = FixedEpochDaysInMonth(y, monthNum);
                var clampedDay = System.Math.Min(day, maxDay);
                var isoDate = FixedEpochDateToIso(epochDays, 13, y, monthCode, 0, clampedDay, "constrain");
                if (isoDate is null)
                {
                    continue;
                }

                // Prefer years where the month has more days (to maximize valid range)
                if (maxDay > bestMaxDay || (maxDay == bestMaxDay && System.Math.Abs(isoDate.Value.Year - isoReferenceYear) < System.Math.Abs(bestIsoYear - isoReferenceYear)))
                {
                    bestYear = y;
                    bestMaxDay = maxDay;
                    bestIsoYear = isoDate.Value.Year;
                }

                // If we found a perfect match (day fits and ISO year matches), use it
                if (day <= maxDay && isoDate.Value.Year == targetIsoYear)
                {
                    return y;
                }
            }
        }

        if (bestYear != 0)
        {
            return bestYear;
        }

        // Fallback: approximate
        var fallbackDays = TemporalHelpers.IsoDateToDays(isoReferenceYear, 7, 1);
        EpochDaysToFixedEpoch(epochDays, fallbackDays, out var fallbackYear, out _, out _);
        return fallbackYear;
    }

    private static long GetCalendarEpochDays(string calendar)
    {
        return calendar switch
        {
            "coptic" => CopticEpochDays,
            "ethiopic" => EthiopicEpochDays,
            "ethioaa" => EthioAAEpochDays,
            _ => throw new NotSupportedException($"Calendar '{calendar}' does not use fixed epoch")
        };
    }

    /// <summary>
    /// Extracts the numeric month from a monthCode string (e.g., "M05" -> 5, "M13" -> 13).
    /// </summary>
    private static int MonthCodeToMonthNumber(string monthCode)
    {
        return int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    #endregion

    #region Indian (Saka) Calendar

    /// <summary>
    /// Returns true if an Indian (Saka) calendar year is a leap year.
    /// An Indian year is a leap year if the corresponding Gregorian year (sakaYear + 78) is a leap year.
    /// </summary>
    private static bool IsIndianLeapYear(int sakaYear)
    {
        return IsoDate.IsLeapYear(sakaYear + 78);
    }

    /// <summary>
    /// Returns the number of days in the given month of an Indian calendar year.
    /// Month 1 (Chaitra): 30 days (31 in leap year).
    /// Months 2-6: 31 days each.
    /// Months 7-12: 30 days each.
    /// </summary>
    private static int IndianDaysInMonth(int sakaYear, int month)
    {
        if (month == 1) return IsIndianLeapYear(sakaYear) ? 31 : 30;
        if (month >= 2 && month <= 6) return 31;
        return 30; // months 7-12
    }

    /// <summary>
    /// Converts an ISO date to an Indian (Saka) calendar date.
    /// </summary>
    private static CalendarDate IndianToCalendarDate(in IsoDate isoDate)
    {
        var isoYear = isoDate.Year;

        // Day of year in ISO calendar (1-based)
        var isoDayOfYear = DayOfYear(isoYear, isoDate.Month, isoDate.Day);

        int sakaYear;
        int sakaDayOfYear; // 0-based within Saka year

        // Chaitra 1 is always ISO day 81:
        // Non-leap: Jan(31) + Feb(28) + Mar 22 = 81
        // Leap: Jan(31) + Feb(29) + Mar 21 = 81
        const int chaitraStartDoy = 81;

        if (isoDayOfYear >= chaitraStartDoy)
        {
            // We're in the current Saka year
            sakaYear = isoYear - 78;
            sakaDayOfYear = isoDayOfYear - chaitraStartDoy; // 0-based
        }
        else
        {
            // We're still in the previous Saka year (before Chaitra of current ISO year)
            sakaYear = isoYear - 79;
            // Day of year within previous Saka year
            var prevIsoYear = isoYear - 1;
            var prevYearDays = IsoDate.IsLeapYear(prevIsoYear) ? 366 : 365;
            sakaDayOfYear = (prevYearDays - chaitraStartDoy) + isoDayOfYear;
        }

        // Convert sakaDayOfYear (0-based) to month/day
        var isLeapYear = IsIndianLeapYear(sakaYear);
        var remaining = sakaDayOfYear;
        var month = 1;

        for (var m = 1; m <= 12; m++)
        {
            var dim = IndianDaysInMonth(sakaYear, m);
            if (remaining < dim)
            {
                month = m;
                break;
            }

            remaining -= dim;
            month = m + 1;
        }

        if (month > 12) month = 12; // safety clamp
        var day = remaining + 1;

        var monthCode = $"M{month:D2}";
        var daysInMonth = IndianDaysInMonth(sakaYear, month);
        var daysInYear = isLeapYear ? 366 : 365;

        return new CalendarDate(sakaYear, month, monthCode, day, false, 12, daysInMonth, daysInYear, isLeapYear);
    }

    /// <summary>
    /// Converts an Indian (Saka) calendar date to an ISO date.
    /// </summary>
    private static IsoDate? IndianDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
                // Indian has no leap months
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
        var maxDay = IndianDaysInMonth(year, ordinalMonth);

        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        // Convert to ISO: compute the day-of-year offset
        // Chaitra 1 of Saka year Y = March 22 (or 21) of ISO year Y + 78
        var isoYear = year + 78;
        var isLeapYear = IsoDate.IsLeapYear(isoYear);

        // Compute Saka day of year (0-based)
        var sakaDoy = 0;
        for (var m = 1; m < ordinalMonth; m++)
        {
            sakaDoy += IndianDaysInMonth(year, m);
        }

        sakaDoy += day - 1;

        // Chaitra 1 = March 22 (non-leap) or March 21 (leap) of isoYear
        // Day 81 of ISO year (1-based), in both cases
        var isoDoy = 81 + sakaDoy; // 1-based ISO day of year
        var totalDaysInIsoYear = isLeapYear ? 366 : 365;

        int resultIsoYear;
        int resultIsoDoy;

        if (isoDoy > totalDaysInIsoYear)
        {
            // Wraps to next ISO year
            resultIsoYear = isoYear + 1;
            resultIsoDoy = isoDoy - totalDaysInIsoYear;
        }
        else
        {
            resultIsoYear = isoYear;
            resultIsoDoy = isoDoy;
        }

        // Convert ISO day-of-year to month/day
        return DayOfYearToIsoDate(resultIsoYear, resultIsoDoy);
    }

    /// <summary>
    /// CalendarDateAdd for the Indian (Saka) calendar.
    /// </summary>
    private static IsoDate IndianCalendarDateAdd(in IsoDate isoDate, int years, int months, string overflow)
    {
        var calDate = IndianToCalendarDate(isoDate);
        var newYear = calDate.Year + years;
        var newMonth = calDate.Month;

        // Add months
        if (months != 0)
        {
            newMonth += months;
            while (newMonth > 12)
            {
                newMonth -= 12;
                newYear++;
            }

            while (newMonth < 1)
            {
                newYear--;
                newMonth += 12;
            }
        }

        // Constrain day
        var maxDay = IndianDaysInMonth(newYear, newMonth);
        var newDay = calDate.Day;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        var result = IndianDateToIso(newYear, null, newMonth, newDay, overflow);
        return result ?? isoDate; // fallback to input if conversion fails
    }

    /// <summary>
    /// Finds a reference year for the Indian calendar.
    /// </summary>
    private static int FindIndianReferenceYear(int isoReferenceYear, string monthCode, int day)
    {
        // Indian year ≈ ISO year - 78
        var approxYear = isoReferenceYear - 78;

        for (var y = approxYear - 1; y <= approxYear + 1; y++)
        {
            var ordinal = MonthCodeToMonthNumber(monthCode);
            var maxDay = IndianDaysInMonth(y, ordinal);
            var clampedDay = System.Math.Min(day, maxDay);
            var isoDate = IndianDateToIso(y, null, ordinal, clampedDay, "constrain");
            if (isoDate?.Year == isoReferenceYear)
            {
                return y;
            }
        }

        return approxYear;
    }

    /// <summary>
    /// Returns the day of year (1-based) for an ISO date.
    /// </summary>
    private static int DayOfYear(int year, int month, int day)
    {
        int[] monthDays = IsoDate.IsLeapYear(year)
            ? new[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335 }
            : new[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };

        return monthDays[month - 1] + day;
    }

    /// <summary>
    /// Converts an ISO day of year (1-based) to an ISO date.
    /// </summary>
    private static IsoDate DayOfYearToIsoDate(int year, int dayOfYear)
    {
        int[] monthDays = IsoDate.IsLeapYear(year)
            ? new[] { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 }
            : new[] { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };

        var remaining = dayOfYear;
        for (var m = 0; m < 12; m++)
        {
            if (remaining <= monthDays[m])
            {
                return new IsoDate(year, m + 1, remaining);
            }

            remaining -= monthDays[m];
        }

        return new IsoDate(year, 12, 31); // safety fallback
    }

    #endregion

    #region Islamic Umm al-Qura Calendar

    /// <summary>
    /// Converts an ISO date to an Islamic Umm al-Qura calendar date.
    /// Uses .NET's UmAlQuraCalendar when within range, falls back to islamic-civil algorithm otherwise.
    /// </summary>
    private static CalendarDate IslamicUmalquraToCalendarDate(in IsoDate isoDate)
    {
        var cal = UmAlQuraCal;
        var dt = new DateTime(isoDate.Year, isoDate.Month, isoDate.Day);

        if (dt >= cal.MinSupportedDateTime && dt <= cal.MaxSupportedDateTime)
        {
            var year = cal.GetYear(dt);
            var ordinalMonth = cal.GetMonth(dt);
            var day = cal.GetDayOfMonth(dt);
            var isLeapYear = cal.IsLeapYear(year);

            var monthCode = $"M{ordinalMonth:D2}";
            var daysInMonth = cal.GetDaysInMonth(year, ordinalMonth);
            var daysInYear = cal.GetDaysInYear(year);

            return new CalendarDate(year, ordinalMonth, monthCode, day, false, 12, daysInMonth, daysInYear, isLeapYear);
        }

        // Fall back to islamic-civil algorithm for dates outside UmAlQura range
        return IslamicCivilToCalendarDate(isoDate);
    }

    /// <summary>
    /// Islamic civil calendar conversion (fallback for out-of-range UmAlQura dates).
    /// </summary>
    private static CalendarDate IslamicCivilToCalendarDate(in IsoDate isoDate, long epochJdn = 1948439L)
    {
        // Convert ISO to Julian Day Number
        var jdn = IsoToJulianDay(isoDate.Year, isoDate.Month, isoDate.Day);

        // Forward formula: JDN = yearDays + monthDays + day + epoch (1-based day),
        // so JDN(1,1,1) = epoch + 1. To make inverse 0-based, subtract epoch + 1.
        var daysFromEpoch = jdn - epochJdn - 1;

        // 30-year cycle with 11 leap years
        var cycle30 = daysFromEpoch / 10631;
        var remaining = daysFromEpoch % 10631;
        if (remaining < 0)
        {
            cycle30--;
            remaining += 10631;
        }

        // Year within cycle
        var yearInCycle = 0;
        var daysSoFar = 0L;
        for (var y = 0; y < 30; y++)
        {
            var daysInYear = IsIslamicCivilLeapYear(y + 1) ? 355 : 354;
            if (daysSoFar + daysInYear > remaining)
            {
                yearInCycle = y;
                break;
            }

            daysSoFar += daysInYear;
        }

        var year = (int) (cycle30 * 30 + yearInCycle + 1);
        remaining -= daysSoFar;

        // Month within year - use ceil(29.5001 * n) to match forward formula IslamicToJdn
        var month = System.Math.Min(12, (int) System.Math.Ceiling((remaining + 0.5) / 29.5001));
        if (month < 1) month = 1;
        var monthOffset = (long) System.Math.Ceiling(29.5001 * (month - 1));
        var day = (int) (remaining - monthOffset) + 1;

        var isLeapYear = IsIslamicCivilLeapYear(year);
        var monthCode = $"M{month:D2}";
        var dim = IslamicCivilDaysInMonth(year, month);
        var diy = isLeapYear ? 355 : 354;

        return new CalendarDate(year, month, monthCode, day, false, 12, dim, diy, isLeapYear);
    }

    private static bool IsIslamicCivilLeapYear(int year)
    {
        // Positive mod: ((year % 30) + 30) % 30 to handle negative years
        var mod = ((year % 30) + 30) % 30;
        // Leap years in 30-year cycle: 2, 5, 7, 10, 13, 16, 18, 21, 24, 26, 29
        return mod is 2 or 5 or 7 or 10 or 13 or 16 or 18 or 21 or 24 or 26 or 29;
    }

    private static int IslamicCivilDaysInMonth(int year, int month)
    {
        // Odd months have 30 days, even months have 29 days
        // except month 12 in leap years has 30 days
        if (month % 2 == 1) return 30;
        if (month == 12 && IsIslamicCivilLeapYear(year)) return 30;
        return 29;
    }

    /// <summary>
    /// Converts an ISO date to Julian Day Number.
    /// </summary>
    private static long IsoToJulianDay(int year, int month, int day)
    {
        // Algorithm: ISO (proleptic Gregorian) to JDN
        long a = (14L - month) / 12;
        long y = year + 4800 - a;
        long m = month + 12 * a - 3;
        return day + (153 * m + 2) / 5 + 365 * y + y / 4 - y / 100 + y / 400 - 32045;
    }

    /// <summary>
    /// Converts an Islamic Umm al-Qura calendar date to an ISO date.
    /// Uses .NET's UmAlQuraCalendar when within range, falls back to islamic-civil algorithm otherwise.
    /// </summary>
    private static IsoDate? IslamicUmalquraDateToIso(int year, string? monthCode, int month, int day, string overflow)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
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

        // Try UmAlQura calendar first
        var cal = UmAlQuraCal;
        try
        {
            // Check if year is in UmAlQura's supported range
            var minYear = cal.GetYear(cal.MinSupportedDateTime);
            var maxYear = cal.GetYear(cal.MaxSupportedDateTime);

            if (year >= minYear && year <= maxYear)
            {
                var maxDay = cal.GetDaysInMonth(year, ordinalMonth);
                if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
                {
                    day = Clamp(day, 1, maxDay);
                }
                else if (day < 1 || day > maxDay)
                {
                    return null;
                }

                var dt = cal.ToDateTime(year, ordinalMonth, day, 0, 0, 0, 0);
                return new IsoDate(dt.Year, dt.Month, dt.Day);
            }
        }
        catch
        {
            // Fall through to islamic-civil
        }

        // Fall back to islamic-civil algorithm
        var maxDayCivil = IslamicCivilDaysInMonth(year, ordinalMonth);
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDayCivil);
        }
        else if (day < 1 || day > maxDayCivil)
        {
            return null;
        }

        return IslamicCivilDateToIso(year, ordinalMonth, day);
    }

    /// <summary>
    /// Converts an Islamic civil date to an ISO date using the tabular algorithm.
    /// </summary>
    private static IsoDate? IslamicCivilDateToIso(int year, int month, int day)
    {
        // Islamic civil uses Friday epoch
        var jdn = IslamicToJdn(year, month, day, 1948439L);
        return JdnToIso(jdn);
    }

    private static long IslamicToJdn(int year, int month, int day, long epoch)
    {
        var monthDays = (long) System.Math.Ceiling(29.5001 * (month - 1));
        var yearDays = (long) (year - 1) * 354L + (long) System.Math.Floor((3.0 + 11.0 * year) / 30.0);
        return monthDays + yearDays + day + epoch;
    }

    private static IsoDate? JdnToIso(long jdn)
    {
        var a = jdn + 32044L;
        var b = (4 * a + 3) / 146097;
        var c = a - 146097 * b / 4;
        var d = (4 * c + 3) / 1461;
        var e = c - 1461 * d / 4;
        var m = (5 * e + 2) / 153;

        var isoDay = (int) (e - (153 * m + 2) / 5 + 1);
        var isoMonth = (int) (m + 3 - 12 * (m / 10));
        var isoYear = (int) (100 * b + d - 4800 + m / 10);

        return new IsoDate(isoYear, isoMonth, isoDay);
    }

    #endregion

    #region Islamic Civil/Tbla Calendar

    /// <summary>
    /// Converts an Islamic tabular calendar date to an ISO date.
    /// islamic-civil uses Friday epoch (JDN 1948440), islamic-tbla uses Thursday epoch (JDN 1948439).
    /// </summary>
    private static IsoDate? IslamicCivilTabularDateToIso(int year, string? monthCode, int month, int day, string overflow, long epoch)
    {
        var ordinalMonth = month;

        if (monthCode is not null)
        {
            var isLeap = monthCode.Length == 4 && monthCode[3] == 'L';
            var displayMonth = int.Parse(monthCode.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture);

            if (isLeap)
            {
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
        var maxDay = IslamicCivilDaysInMonth(year, ordinalMonth);
        if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
        {
            day = Clamp(day, 1, maxDay);
        }
        else if (day < 1 || day > maxDay)
        {
            return null;
        }

        var jdn = IslamicToJdn(year, ordinalMonth, day, epoch);
        return JdnToIso(jdn);
    }

    /// <summary>
    /// CalendarDateAdd for Islamic calendars (islamic-civil/islamic-tbla/islamic-umalqura).
    /// </summary>
    private static IsoDate IslamicTabularCalendarDateAdd(string calendar, in IsoDate isoDate, int years, int months, string overflow)
    {
        var calDate = IsoToCalendarDate(calendar, isoDate);
        var newYear = calDate.Year + years;
        var newMonth = calDate.Month;

        // Add months
        if (months != 0)
        {
            newMonth += months;
            while (newMonth > 12)
            {
                newMonth -= 12;
                newYear++;
            }

            while (newMonth < 1)
            {
                newYear--;
                newMonth += 12;
            }
        }

        // Constrain day - use calendar-specific days in month
        int maxDay;
        if (calendar is "islamic-umalqura")
        {
            var cal = UmAlQuraCal;
            try
            {
                var minYear = cal.GetYear(cal.MinSupportedDateTime);
                var maxYear = cal.GetYear(cal.MaxSupportedDateTime);
                if (newYear >= minYear && newYear <= maxYear)
                {
                    maxDay = cal.GetDaysInMonth(newYear, newMonth);
                }
                else
                {
                    maxDay = IslamicCivilDaysInMonth(newYear, newMonth);
                }
            }
            catch
            {
                maxDay = IslamicCivilDaysInMonth(newYear, newMonth);
            }
        }
        else
        {
            maxDay = IslamicCivilDaysInMonth(newYear, newMonth);
        }

        var newDay = calDate.Day;
        if (newDay > maxDay)
        {
            if (string.Equals(overflow, "reject", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("reject");
            }

            newDay = maxDay;
        }

        // Convert back to ISO using the appropriate calendar
        var result = CalendarDateToIso(calendar, newYear, null, newMonth, newDay, overflow);
        return result ?? isoDate;
    }

    /// <summary>
    /// Finds a reference year for Islamic tabular calendars.
    /// </summary>
    private static int FindIslamicTabularReferenceYear(string calendar, int isoReferenceYear, string monthCode, int day)
    {
        long epoch = calendar is "islamic-tbla" ? 1948438L : 1948439L;
        var monthNum = MonthCodeToMonthNumber(monthCode);

        // Approximate Islamic year from ISO year: Islamic year ≈ (ISO - 622) * 33/32
        var approxYear = (int) ((isoReferenceYear - 622.0) * 33.0 / 32.0);

        for (var y = approxYear - 2; y <= approxYear + 2; y++)
        {
            var maxDay = IslamicCivilDaysInMonth(y, monthNum);
            var clampedDay = System.Math.Min(day, maxDay);
            var jdn = IslamicToJdn(y, monthNum, clampedDay, epoch);
            var isoDate = JdnToIso(jdn);
            if (isoDate?.Year == isoReferenceYear)
            {
                return y;
            }
        }

        return approxYear;
    }

    #endregion
}

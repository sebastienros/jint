using System.Globalization;

namespace Jint.Native.Intl;

/// <summary>
/// Helper class for Chinese and Dangi (Korean) calendar operations.
/// Provides conversion from Gregorian dates to Chinese/Dangi calendar dates
/// and computes the sexagenary cycle (干支 Gānzhī) year names.
/// </summary>
internal static class ChineseCalendarHelper
{
    // Lazy initialization to avoid startup overhead when Chinese calendar is not used
    private static ChineseLunisolarCalendar? _chineseCalendar;
    private static KoreanLunisolarCalendar? _koreanCalendar;

    private static ChineseLunisolarCalendar ChineseCalendar =>
        _chineseCalendar ??= new ChineseLunisolarCalendar();

    private static KoreanLunisolarCalendar KoreanCalendar =>
        _koreanCalendar ??= new KoreanLunisolarCalendar();

    /// <summary>
    /// The 10 Heavenly Stems (天干 Tiāngān) in Chinese characters.
    /// Used as part of the 60-year sexagenary cycle.
    /// </summary>
    private static readonly string[] HeavenlyStems =
    [
        "甲", // jiǎ
        "乙", // yǐ
        "丙", // bǐng
        "丁", // dīng
        "戊", // wù
        "己", // jǐ
        "庚", // gēng
        "辛", // xīn
        "壬", // rén
        "癸"  // guǐ
    ];

    /// <summary>
    /// The 12 Earthly Branches (地支 Dìzhī) in Chinese characters.
    /// Used as part of the 60-year sexagenary cycle.
    /// </summary>
    private static readonly string[] EarthlyBranches =
    [
        "子", // zǐ (Rat)
        "丑", // chǒu (Ox)
        "寅", // yín (Tiger)
        "卯", // mǎo (Rabbit)
        "辰", // chén (Dragon)
        "巳", // sì (Snake)
        "午", // wǔ (Horse)
        "未", // wèi (Goat)
        "申", // shēn (Monkey)
        "酉", // yǒu (Rooster)
        "戌", // xū (Dog)
        "亥"  // hài (Pig)
    ];

    /// <summary>
    /// Gets Chinese calendar date information for a given DateTime.
    /// </summary>
    /// <param name="dateTime">The Gregorian date to convert.</param>
    /// <returns>Chinese calendar date information including related year, year name, month, and day.</returns>
    public static ChineseCalendarDate GetChineseDate(DateTime dateTime)
    {
        return GetLunisolarDate(dateTime, ChineseCalendar, isDangi: false);
    }

    /// <summary>
    /// Gets Dangi (Korean lunisolar) calendar date information for a given DateTime.
    /// The Dangi calendar is essentially the same as the Chinese calendar.
    /// </summary>
    /// <param name="dateTime">The Gregorian date to convert.</param>
    /// <returns>Dangi calendar date information including related year, year name, month, and day.</returns>
    public static ChineseCalendarDate GetDangiDate(DateTime dateTime)
    {
        return GetLunisolarDate(dateTime, KoreanCalendar, isDangi: true);
    }

    private static ChineseCalendarDate GetLunisolarDate(DateTime dateTime, EastAsianLunisolarCalendar calendar, bool isDangi)
    {
        // Clamp to supported range
        var minDate = calendar.MinSupportedDateTime;
        var maxDate = calendar.MaxSupportedDateTime;

        if (dateTime < minDate)
        {
            dateTime = minDate;
        }
        else if (dateTime > maxDate)
        {
            dateTime = maxDate;
        }

        var year = calendar.GetYear(dateTime);
        var month = calendar.GetMonth(dateTime);
        var day = calendar.GetDayOfMonth(dateTime);

        // Check if this is a leap month
        var leapMonth = calendar.GetLeapMonth(year);
        var isLeapMonth = leapMonth > 0 && month == leapMonth;

        // Adjust month number for display (leap month has same number as previous month)
        var displayMonth = month;
        if (leapMonth > 0 && month >= leapMonth)
        {
            // If we're in or past the leap month, adjust the month number
            displayMonth = month - 1;
            if (month == leapMonth)
            {
                isLeapMonth = true;
            }
        }

        // The year from both ChineseLunisolarCalendar and KoreanLunisolarCalendar
        // is already the Gregorian-aligned "related year" (the Gregorian year that mostly
        // contains this lunar year). No offset adjustment is needed for either calendar.
        var relatedYear = year;

        // Get the sexagenary cycle year name (干支)
        var sexagenaryYear = calendar.GetSexagenaryYear(dateTime);
        var yearName = GetSexagenaryYearName(sexagenaryYear);

        return new ChineseCalendarDate(relatedYear, yearName, displayMonth, day, isLeapMonth);
    }

    /// <summary>
    /// Gets the sexagenary cycle year name (干支 Gānzhī) for a given sexagenary year number.
    /// </summary>
    /// <param name="sexagenaryYear">The sexagenary year number (1-60).</param>
    /// <returns>The two-character Chinese year name.</returns>
    private static string GetSexagenaryYearName(int sexagenaryYear)
    {
        // Sexagenary year is 1-60, need to convert to 0-59 for array indexing
        var index = sexagenaryYear - 1;

        // The sexagenary cycle combines 10 Heavenly Stems with 12 Earthly Branches
        var stemIndex = index % 10;
        var branchIndex = index % 12;

        return HeavenlyStems[stemIndex] + EarthlyBranches[branchIndex];
    }

    /// <summary>
    /// Represents a date in the Chinese or Dangi lunisolar calendar.
    /// </summary>
    internal readonly struct ChineseCalendarDate
    {
        /// <summary>
        /// Creates a new Chinese calendar date.
        /// </summary>
        public ChineseCalendarDate(int relatedYear, string yearName, int month, int day, bool isLeapMonth)
        {
            RelatedYear = relatedYear;
            YearName = yearName;
            Month = month;
            Day = day;
            IsLeapMonth = isLeapMonth;
        }

        /// <summary>
        /// The "related year" - the Gregorian year that mostly overlaps with this Chinese calendar year.
        /// For dates before Chinese New Year, this is the previous Gregorian year.
        /// </summary>
        public int RelatedYear { get; }

        /// <summary>
        /// The Chinese sexagenary cycle name (干支 Gānzhī) for the year.
        /// Example: "己亥" (jǐ hài) for the year 2019.
        /// </summary>
        public string YearName { get; }

        /// <summary>
        /// The month number in the Chinese calendar (1-12).
        /// Note: Leap months are indicated separately by IsLeapMonth.
        /// </summary>
        public int Month { get; }

        /// <summary>
        /// The day of the month in the Chinese calendar.
        /// </summary>
        public int Day { get; }

        /// <summary>
        /// Whether this date falls in a leap month.
        /// In lunisolar calendars, a leap month is an intercalary month
        /// inserted to keep the calendar aligned with the solar year.
        /// </summary>
        public bool IsLeapMonth { get; }
    }
}

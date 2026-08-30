using System.Globalization;
using Jint.Native.Temporal;

namespace Jint.Native.Intl;

/// <summary>
/// Helper class for Chinese and Dangi (Korean) calendar operations.
/// Provides conversion from Gregorian dates to Chinese/Dangi calendar dates
/// and computes the sexagenary cycle (干支 Gānzhī) year names.
/// </summary>
internal static class ChineseCalendarHelper
{
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
        return GetLunisolarDate(dateTime, "chinese");
    }

    /// <summary>
    /// Gets Dangi (Korean lunisolar) calendar date information for a given DateTime.
    /// The Dangi calendar is essentially the same as the Chinese calendar.
    /// </summary>
    /// <param name="dateTime">The Gregorian date to convert.</param>
    /// <returns>Dangi calendar date information including related year, year name, month, and day.</returns>
    public static ChineseCalendarDate GetDangiDate(DateTime dateTime)
    {
        return GetLunisolarDate(dateTime, "dangi");
    }

    /// <summary>
    /// The lunisolar fields of a date, read through the same conversion <c>Temporal</c> reads, so the two
    /// name the same day.
    /// </summary>
    /// <remarks>
    /// This used to read <c>ChineseLunisolarCalendar</c> and <c>KoreanLunisolarCalendar</c> directly, and
    /// to clamp a date outside their span to the table's own first or last date and report that instead —
    /// so 1800-01-01 formatted as the Chinese new year of 1901. Those tables are also not the same table
    /// on every target framework (https://github.com/sebastienros/jint/issues/3484).
    /// </remarks>
    private static ChineseCalendarDate GetLunisolarDate(DateTime dateTime, string calendar)
    {
        var fields = NonIsoCalendars.IsoToCalendarDate(calendar, new IsoDate(dateTime.Year, dateTime.Month, dateTime.Day));

        // The month code is M plus the display month, with an L for a leap month; a leap month carries the
        // display number of the month it follows, which is what Intl renders as "4bis".
        var displayMonth = int.Parse(
            fields.MonthCode.AsSpan(1, 2),
            NumberStyles.None,
            CultureInfo.InvariantCulture);

        return new ChineseCalendarDate(
            fields.Year,
            GetSexagenaryYearName(SexagenaryYearOf(fields.Year)),
            displayMonth,
            fields.Day,
            fields.IsLeapMonth);
    }

    /// <summary>
    /// The 1-to-60 position of a lunisolar year in the sexagenary cycle. 1984 is 甲子, position 1, which
    /// is the anchor <c>EastAsianLunisolarCalendar.GetSexagenaryYear</c> counts from too.
    /// </summary>
    private static int SexagenaryYearOf(int relatedYear)
    {
        var position = (relatedYear - 3) % 60;
        if (position <= 0)
        {
            position += 60;
        }

        return position;
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

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
        bool hasExplicitFormatComponents,
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
        HasExplicitFormatComponents = hasExplicitFormatComponents;
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
    internal bool HasExplicitFormatComponents { get; }
    internal DateTimeFormatInfo DateTimeFormatInfo { get; }
    internal CultureInfo CultureInfo { get; }

    /// <summary>
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    /// <summary>
    /// Formats a date according to the formatter's locale and options.
    /// </summary>
    /// <param name="dateTime">The .NET DateTime to format</param>
    /// <param name="originalYear">Optional original JavaScript year (for dates outside .NET DateTime range)</param>
    /// <param name="isPlain">If true, skip timezone conversion (for plain Temporal types)</param>
    internal string Format(DateTime dateTime, int? originalYear = null, bool isPlain = false)
    {
        // For Chinese and Dangi calendars, use FormatToParts to get consistent output
        // This ensures the special part types (relatedYear, yearName) are properly handled
        var isLunisolarCalendar = string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(Calendar, "dangi", StringComparison.OrdinalIgnoreCase);

        // For era formatting, use FormatToParts to ensure proper year formatting for BC dates
        // This is needed because .NET format strings don't handle proleptic Gregorian years correctly
        var hasEra = Era != null;

        if (isLunisolarCalendar || hasEra)
        {
            var parts = FormatToParts(dateTime, originalYear, isPlain);
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                sb.Append(part.Value);
            }
            return sb.ToString();
        }

        // Convert to specified timezone if one was provided
        // For plain Temporal types (isPlain=true), skip timezone conversion since
        // they represent wall-clock time, not an absolute point in time
        if (!isPlain)
        {
            if (TimeZone != null)
            {
                dateTime = ConvertToTimeZone(dateTime, TimeZone);
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                // No explicit timezone: convert UTC to engine's default timezone
                // (not system ToLocalTime which ignores engine's configured timezone)
                var defaultTz = _engine.Options.TimeSystem.DefaultTimeZone;
                dateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, defaultTz);
            }
        }

        string result;

        // If dateStyle or timeStyle is specified, use those
        if (DateStyle != null || TimeStyle != null)
        {
            result = FormatWithStyles(dateTime, isPlain);
        }
        else
        {
            // Otherwise build format from component options
            result = FormatWithComponents(dateTime, originalYear);
        }

        // Apply numbering system transliteration if not using Latin digits
        if (NumberingSystem != null && !string.Equals(NumberingSystem, "latn", StringComparison.OrdinalIgnoreCase))
        {
            result = Data.NumberingSystemData.TransliterateDigits(result, NumberingSystem);
        }

        return result;
    }

    private static DateTime ConvertToTimeZone(DateTime dateTime, string timeZoneId)
    {
        if (string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(timeZoneId, "+00:00", StringComparison.Ordinal))
        {
            // Convert to UTC
            if (dateTime.Kind == DateTimeKind.Local)
            {
                return dateTime.ToUniversalTime();
            }
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }

        // Check for offset timezone format like "+03:00", "-07:30"
        var offset = TryParseOffset(timeZoneId);
        if (offset.HasValue)
        {
            // Convert to UTC first
            if (dateTime.Kind == DateTimeKind.Local)
            {
                dateTime = dateTime.ToUniversalTime();
            }
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

            // Apply the offset
            return dateTime.Add(offset.Value);
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

    /// <summary>
    /// Parses an offset timezone string like "+03:00" or "-07:30" and returns the TimeSpan offset.
    /// </summary>
    private static TimeSpan? TryParseOffset(string timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId) || timeZoneId.Length != 6)
        {
            return null;
        }

        var sign = timeZoneId[0];
        if (sign != '+' && sign != '-')
        {
            return null;
        }

        if (timeZoneId[3] != ':')
        {
            return null;
        }

        // Parse hours and minutes using direct character parsing for compatibility
        if (!char.IsDigit(timeZoneId[1]) || !char.IsDigit(timeZoneId[2]) ||
            !char.IsDigit(timeZoneId[4]) || !char.IsDigit(timeZoneId[5]))
        {
            return null;
        }

        var hours = (timeZoneId[1] - '0') * 10 + (timeZoneId[2] - '0');
        var minutes = (timeZoneId[4] - '0') * 10 + (timeZoneId[5] - '0');

        var totalMinutes = hours * 60 + minutes;
        if (sign == '-')
        {
            totalMinutes = -totalMinutes;
        }

        return TimeSpan.FromMinutes(totalMinutes);
    }

    /// <summary>
    /// Gets the era name for a date based on the calendar and style.
    /// Returns null for calendars that don't have eras (chinese, dangi).
    /// </summary>
    /// <param name="dateTime">The .NET DateTime (may be clamped for dates outside .NET range)</param>
    /// <param name="calendar">The calendar type</param>
    /// <param name="style">The era style (long, short, narrow)</param>
    /// <param name="originalYear">The original JavaScript year (for dates outside .NET DateTime range)</param>
    private string? GetEraName(DateTime dateTime, string calendar, string style, int? originalYear = null)
    {
        // Chinese and Dangi calendars don't use eras
        if (string.Equals(calendar, "chinese", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(calendar, "dangi", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Get era names from CLDR provider if available
        var eraNames = CldrProvider.GetEraNames(Locale, style, calendar);

        // Use original year for era calculation if the date was clamped
        var effectiveYear = originalYear ?? dateTime.Year;

        return calendar.ToLowerInvariant() switch
        {
            "gregory" or "iso8601" => GetGregorianEra(effectiveYear, style, eraNames),
            "japanese" => GetJapaneseEra(dateTime, effectiveYear, style, eraNames),
            "roc" => GetRocEra(effectiveYear, style, eraNames),
            "buddhist" => GetBuddhistEra(style, eraNames),
            "hebrew" => GetHebrewEra(style, eraNames),
            "persian" => GetPersianEra(style, eraNames),
            "indian" => GetIndianEra(style, eraNames),
            "ethioaa" or "ethiopic" => GetEthiopicEra(style, eraNames),
            "coptic" => GetCopticEra(effectiveYear, style, eraNames),
            "islamic" or "islamic-civil" or "islamic-tbla" or "islamic-umalqura" => GetIslamicEra(style, eraNames),
            _ => GetGregorianEra(effectiveYear, style, eraNames) // Default to Gregorian
        };
    }

    private static string GetGregorianEra(int year, string style, string[]? eraNames)
    {
        var isAD = year > 0;
        if (eraNames != null && eraNames.Length >= 2)
        {
            return isAD ? eraNames[1] : eraNames[0];
        }
        // Fallback era names
        return style switch
        {
            "long" => isAD ? "Anno Domini" : "Before Christ",
            "short" => isAD ? "AD" : "BC",
            "narrow" => isAD ? "A" : "B",
            _ => isAD ? "AD" : "BC"
        };
    }

    private static string GetJapaneseEra(DateTime dateTime, int effectiveYear, string style, string[]? eraNames)
    {
        // Japanese era calculation
        // Reiwa: 2019-05-01 onwards
        // Heisei: 1989-01-08 to 2019-04-30
        // Showa: 1926-12-25 to 1989-01-07
        // Taisho: 1912-07-30 to 1926-12-24
        // Meiji: 1868-01-25 to 1912-07-29
        // Before Meiji

        // Determine era index and get name based on style
        // For Japanese eras, short and long use the same full name
        var isNarrow = string.Equals(style, "narrow", StringComparison.Ordinal);

        // Check for dates within .NET DateTime range using actual DateTime comparison
        if (effectiveYear >= 2019 && dateTime >= new DateTime(2019, 5, 1))
        {
            return isNarrow ? "R" : "Reiwa";
        }

        if (effectiveYear >= 1989 && dateTime >= new DateTime(1989, 1, 8))
        {
            return isNarrow ? "H" : "Heisei";
        }

        if (effectiveYear >= 1926 && dateTime >= new DateTime(1926, 12, 25))
        {
            return isNarrow ? "S" : "Shōwa";
        }

        if (effectiveYear >= 1912 && dateTime >= new DateTime(1912, 7, 30))
        {
            return isNarrow ? "T" : "Taishō";
        }

        if (effectiveYear >= 1868 && dateTime >= new DateTime(1868, 1, 25))
        {
            return isNarrow ? "M" : "Meiji";
        }

        // Before Meiji - use Gregorian era based on the effective year
        return effectiveYear > 0 ? "AD" : "BC";
    }

    private static string GetRocEra(int year, string style, string[]? eraNames)
    {
        // Republic of China calendar: year 1 = 1912 CE
        // Note: eraNames from CLDR are Gregorian, not ROC-specific, so we use hardcoded values
        var isAfter1912 = year >= 1912;
        return style switch
        {
            "long" => isAfter1912 ? "Minguo" : "Before R.O.C.",
            "short" => isAfter1912 ? "Minguo" : "Before R.O.C.",
            "narrow" => isAfter1912 ? "R.O.C." : "B.R.O.C.",
            _ => isAfter1912 ? "Minguo" : "Before R.O.C."
        };
    }

    private static string GetBuddhistEra(string style, string[]? eraNames)
    {
        // Buddhist calendar has single era (BE - Buddhist Era)
        // Note: eraNames from CLDR are Gregorian, not Buddhist-specific
        return style switch
        {
            "long" => "Buddhist Era",
            "short" => "BE",
            "narrow" => "BE",
            _ => "BE"
        };
    }

    private static string GetHebrewEra(string style, string[]? eraNames)
    {
        // Hebrew calendar has single era (AM - Anno Mundi)
        // Note: eraNames from CLDR are Gregorian, not Hebrew-specific
        return style switch
        {
            "long" => "Anno Mundi",
            "short" => "AM",
            "narrow" => "AM",
            _ => "AM"
        };
    }

    private static string GetPersianEra(string style, string[]? eraNames)
    {
        // Persian calendar has single era (AP - Anno Persico)
        // Note: eraNames from CLDR are Gregorian, not Persian-specific
        return style switch
        {
            "long" => "Anno Persico",
            "short" => "AP",
            "narrow" => "AP",
            _ => "AP"
        };
    }

    private static string GetIndianEra(string style, string[]? eraNames)
    {
        // Indian national calendar has single era (Saka)
        // Note: eraNames from CLDR are Gregorian, not Indian-specific
        return style switch
        {
            "long" => "Saka",
            "short" => "Saka",
            "narrow" => "Saka",
            _ => "Saka"
        };
    }

    private static string GetEthiopicEra(string style, string[]? eraNames)
    {
        // Ethiopic calendar uses Era of the Incarnation
        // Note: eraNames from CLDR are Gregorian, not Ethiopic-specific
        return style switch
        {
            "long" => "Era of the Incarnation",
            "short" => "ERA1",
            "narrow" => "ERA1",
            _ => "ERA1"
        };
    }

    private static string GetCopticEra(int year, string style, string[]? eraNames)
    {
        // Coptic calendar has two eras
        // Note: eraNames from CLDR are Gregorian, not Coptic-specific, so we use hardcoded values
        var isAfterEpoch = year >= 284; // Year 284 CE
        return style switch
        {
            "long" => isAfterEpoch ? "Era of the Martyrs" : "Before Era of the Martyrs",
            "short" => isAfterEpoch ? "AM" : "BAM",
            "narrow" => isAfterEpoch ? "AM" : "BAM",
            _ => isAfterEpoch ? "AM" : "BAM"
        };
    }

    private static string GetIslamicEra(string style, string[]? eraNames)
    {
        // Islamic calendar has single era (AH - Anno Hegirae)
        // Note: eraNames from CLDR are Gregorian, not Islamic-specific
        return style switch
        {
            "long" => "Anno Hegirae",
            "short" => "AH",
            "narrow" => "AH",
            _ => "AH"
        };
    }

    /// <summary>
    /// Holds locale-specific date format information.
    /// </summary>
    private readonly struct LocaleDateFormatInfo
    {
        public LocaleDateFormatInfo(string dateOrder, string dateSeparator, bool hasTextualMonth)
        {
            DateOrder = dateOrder;
            DateSeparator = dateSeparator;
            HasTextualMonth = hasTextualMonth;
        }

        /// <summary>Date component order as "Mdy", "dMy", or "yMd".</summary>
        public string DateOrder { get; }
        /// <summary>Separator between date components.</summary>
        public string DateSeparator { get; }
        /// <summary>Whether the month is textual (long, short, narrow) vs numeric.</summary>
        public bool HasTextualMonth { get; }
    }

    /// <summary>
    /// Determines the locale-specific date format order and separator.
    /// </summary>
    private LocaleDateFormatInfo GetLocaleDateFormat()
    {
        // Check if month is textual (long, short, narrow) vs numeric
        var hasTextualMonth = Month != null && Month is "long" or "short" or "narrow";

        // Get locale's short date pattern to determine order
        var lang = Locale.Split('-')[0].ToLowerInvariant();
        var region = Locale.Contains('-') ? Locale.Split('-')[1].ToUpperInvariant() : "";

        // Determine date component order based on locale
        // MDY: en-US, en-CA (Canada uses MDY in English), fil (Philippines)
        // DMY: Most of Europe, UK, Australia, most of world
        // YMD: East Asia (China, Japan, Korea), Lithuania, Hungary
        string dateOrder;
        if (string.Equals(lang, "en", StringComparison.Ordinal) &&
            (string.Equals(region, "US", StringComparison.Ordinal) ||
             string.Equals(region, "", StringComparison.Ordinal)))
        {
            // en-US and plain "en" use MDY
            dateOrder = "Mdy";
        }
        else if (string.Equals(lang, "zh", StringComparison.Ordinal) ||
                 string.Equals(lang, "ja", StringComparison.Ordinal) ||
                 string.Equals(lang, "ko", StringComparison.Ordinal) ||
                 string.Equals(lang, "hu", StringComparison.Ordinal) ||
                 string.Equals(lang, "lt", StringComparison.Ordinal))
        {
            // East Asian languages and Hungarian/Lithuanian use YMD
            dateOrder = "yMd";
        }
        else
        {
            // Most of the world uses DMY
            dateOrder = "dMy";
        }

        // Determine separator based on whether month is textual
        string dateSeparator;
        if (hasTextualMonth)
        {
            // Textual month uses space separators: "Jan 3, 2019"
            dateSeparator = " ";
        }
        else
        {
            // Numeric format uses "/" for en-US, varies by locale
            dateSeparator = "/";
        }

        return new LocaleDateFormatInfo(dateOrder, dateSeparator, hasTextualMonth);
    }

    private void AddMonthPart(DateTime dateTime, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth, ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null)
    {
        if (result.Count > 0 && hasDate)
        {
            result.Add(new DateTimePart("literal", separator));
        }

        string monthValue;
        if (lunisolarDate.HasValue)
        {
            // Use Chinese/Dangi calendar month
            var chineseMonth = lunisolarDate.Value.Month;
            monthValue = Month switch
            {
                "numeric" => chineseMonth.ToString(CultureInfo),
                "2-digit" => chineseMonth.ToString("D2", CultureInfo),
                // For textual months in lunisolar calendars, we still use numeric
                // as Chinese month names are not typically used in Intl formatting
                "long" or "short" or "narrow" => chineseMonth.ToString(CultureInfo),
                _ => chineseMonth.ToString("D2", CultureInfo)
            };
        }
        else
        {
            var format = Month switch
            {
                "numeric" => "%M",
                "2-digit" => "MM",
                "long" => "MMMM",
                "short" => "MMM",
                "narrow" => "MMM",
                _ => "MM"
            };
            monthValue = dateTime.ToString(format, CultureInfo);
        }

        result.Add(new DateTimePart("month", monthValue));
        hasDate = true;
    }

    private void AddDayPart(DateTime dateTime, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth, ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null)
    {
        if (result.Count > 0 && hasDate)
        {
            result.Add(new DateTimePart("literal", separator));
        }

        string dayValue;
        if (lunisolarDate.HasValue)
        {
            // Use Chinese/Dangi calendar day
            var chineseDay = lunisolarDate.Value.Day;
            dayValue = Day switch
            {
                "numeric" => chineseDay.ToString(CultureInfo),
                "2-digit" => chineseDay.ToString("D2", CultureInfo),
                _ => chineseDay.ToString("D2", CultureInfo)
            };
        }
        else
        {
            var format = Day switch
            {
                "numeric" => "%d",
                "2-digit" => "dd",
                _ => "dd"
            };
            dayValue = dateTime.ToString(format, CultureInfo);
        }

        result.Add(new DateTimePart("day", dayValue));
        hasDate = true;
    }

    private void AddYearPart(DateTime dateTime, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth, ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null, int? originalYear = null)
    {
        if (result.Count > 0 && hasDate)
        {
            // For textual month format, use ", " before year if it comes last
            var actualSeparator = hasTextualMonth ? ", " : separator;
            result.Add(new DateTimePart("literal", actualSeparator));
        }

        if (lunisolarDate.HasValue)
        {
            // For Chinese/Dangi calendars, output relatedYear and yearName instead of year
            var relatedYear = lunisolarDate.Value.RelatedYear;
            var yearName = lunisolarDate.Value.YearName;

            // Check locale for formatting - zh locale uses "年" suffix
            var lang = Locale.Split('-')[0].ToLowerInvariant();
            var isChineseLocale = string.Equals(lang, "zh", StringComparison.Ordinal);

            // Add relatedYear part
            var relatedYearValue = Year switch
            {
                "numeric" => relatedYear.ToString(CultureInfo),
                "2-digit" => (relatedYear % 100).ToString("00", CultureInfo),
                _ => relatedYear.ToString(CultureInfo)
            };
            result.Add(new DateTimePart("relatedYear", relatedYearValue));

            // Add yearName part (干支 sexagenary cycle name)
            result.Add(new DateTimePart("yearName", yearName));

            // For Chinese locale, add "年" (year) suffix
            if (isChineseLocale)
            {
                result.Add(new DateTimePart("literal", "年"));
            }
        }
        else
        {
            // Use original year if provided (for dates outside .NET DateTime range)
            var effectiveYear = originalYear ?? dateTime.Year;

            // For proleptic Gregorian calendar with era, convert negative years to positive BC years
            // Year 0 in astronomical notation = 1 BC, year -1 = 2 BC, etc.
            var displayYear = Era != null && effectiveYear <= 0 ? 1 - effectiveYear : System.Math.Abs(effectiveYear);

            var yearValue = Year switch
            {
                // For numeric with era, use plain number without leading zeros
                "numeric" => displayYear.ToString(CultureInfo),
                "2-digit" => (displayYear % 100).ToString("00", CultureInfo),
                _ => displayYear.ToString(CultureInfo)
            };
            result.Add(new DateTimePart("year", yearValue));
        }
        hasDate = true;
    }

    private string FormatWithStyles(DateTime dateTime, bool isPlain = false)
    {
        // When both dateStyle and timeStyle are specified, combine them appropriately
        if (DateStyle != null && TimeStyle != null)
        {
            // Format date and time separately and combine with ", "
            var datePart = FormatDateStyleOnly(dateTime);
            var timePart = FormatTimeStyle(dateTime, isPlain);
            return $"{datePart}, {timePart}";
        }

        if (DateStyle != null)
        {
            return FormatDateStyleOnly(dateTime);
        }

        if (TimeStyle != null)
        {
            return FormatTimeStyle(dateTime, isPlain);
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
    private string FormatTimeStyle(DateTime dateTime, bool isPlain = false)
    {
        // Use ComputeHourValue to handle all hour cycles correctly
        // Style-based formatting always pads 24-hour values (e.g., "05:00:00" not "5:00:00")
        ComputeHourValue(dateTime.Hour, out var hourStr, out var use12Hour, padByDefault: true);

        // Plain Temporal types should not show timeZoneName
        var timeZoneSuffix = "";
        if (!isPlain)
        {
            if (string.Equals(TimeStyle, "full", StringComparison.Ordinal))
            {
                timeZoneSuffix = " " + GetTimeZoneDisplayName(dateTime, longName: true, generic: false);
            }
            else if (string.Equals(TimeStyle, "long", StringComparison.Ordinal))
            {
                timeZoneSuffix = " " + GetTimeZoneDisplayName(dateTime, longName: false, generic: false);
            }
        }

        var minuteStr = dateTime.Minute.ToString("D2", CultureInfo.InvariantCulture);
        var secondStr = dateTime.Second.ToString("D2", CultureInfo.InvariantCulture);
        var dayPeriod = use12Hour ? " " + GetDayPeriod(dateTime.Hour) : "";

        return TimeStyle switch
        {
            "full" => hourStr + ":" + minuteStr + ":" + secondStr + dayPeriod + timeZoneSuffix,
            "long" => hourStr + ":" + minuteStr + ":" + secondStr + dayPeriod + timeZoneSuffix,
            "medium" => hourStr + ":" + minuteStr + ":" + secondStr + dayPeriod,
            "short" => hourStr + ":" + minuteStr + dayPeriod,
            _ => hourStr + ":" + minuteStr + dayPeriod,
        };
    }

    private static string GetDayPeriod(int hour)
    {
        return hour < 12 ? "AM" : "PM";
    }

    private string FormatWithComponents(DateTime dateTime, int? originalYear = null)
    {
        // Build a custom format string based on component options
        var parts = new List<string>();
        string? eraValue = null;

        // Get locale-specific date format info
        var formatInfo = GetLocaleDateFormat();

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

        // Era - get the era name but add it after formatting (since .NET doesn't support custom eras)
        if (Era != null)
        {
            eraValue = GetEraName(dateTime, Calendar ?? "gregory", Era, originalYear);
        }

        // Add date parts in locale-specific order
        foreach (var component in formatInfo.DateOrder)
        {
            switch (component)
            {
                case 'M' when Month != null:
                    parts.Add(Month switch
                    {
                        "numeric" => "M",
                        "2-digit" => "MM",
                        "long" => "MMMM",
                        "short" => "MMM",
                        "narrow" => "MMM",
                        _ => "MM"
                    });
                    break;
                case 'd' when Day != null:
                    parts.Add(Day switch
                    {
                        "numeric" => "d",
                        "2-digit" => "dd",
                        _ => "dd"
                    });
                    break;
                case 'y' when Year != null:
                    parts.Add(Year switch
                    {
                        "numeric" => "yyyy",
                        "2-digit" => "yy",
                        _ => "yyyy"
                    });
                    break;
            }
        }

        // Hour - use pre-computed value to handle all hour cycles (h11/h12/h23/h24)
        bool hourUse12Hour = false;
        if (Hour != null)
        {
            ComputeHourValue(dateTime.Hour, out var hourStr, out var use12Hr);
            hourUse12Hour = use12Hr;
            // Use escaped literal in format string so .NET outputs our pre-computed value
            parts.Add("'" + hourStr + "'");
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
        var needsAmPm = Hour != null && hourUse12Hour && DayPeriod == null;
        if (needsAmPm)
        {
            parts.Add("tt");
        }

        // Time zone name - compute the display name directly (not via .NET format specifier)
        string? timeZoneNameStr = null;
        if (TimeZoneName != null)
        {
            timeZoneNameStr = GetFormattedTimeZoneName(dateTime);
        }

        // Handle DayPeriod option (extended day periods like "in the morning")
        if (DayPeriod != null)
        {
            // If only dayPeriod is specified (no other components), just return the day period
            if (parts.Count == 0 && eraValue == null)
            {
                return GetExtendedDayPeriod(dateTime.Hour);
            }

            // Otherwise, format with other components and append day period
            string formatted;
            if (parts.Count > 0)
            {
                var formatString = BuildFormatString(parts);
                formatted = dateTime.ToString(formatString, CultureInfo);
            }
            else
            {
                formatted = "";
            }

            // Append era if specified
            if (eraValue != null)
            {
                if (formatted.Length > 0)
                {
                    formatted += " " + eraValue;
                }
                else
                {
                    formatted = eraValue;
                }
            }

            var dayPeriodResult = formatted + " " + GetExtendedDayPeriod(dateTime.Hour);
            if (timeZoneNameStr != null)
            {
                dayPeriodResult += " " + timeZoneNameStr;
            }
            return dayPeriodResult;
        }

        if (parts.Count == 0 && eraValue == null && timeZoneNameStr == null)
        {
            // Default format if no components specified
            return dateTime.ToString("G", CultureInfo);
        }

        // Join parts with appropriate separators
        string result;
        if (parts.Count > 0)
        {
            var formatString2 = BuildFormatString(parts);
            result = dateTime.ToString(formatString2, CultureInfo);
        }
        else
        {
            result = "";
        }

        // Append era if specified
        if (eraValue != null)
        {
            if (result.Length > 0)
            {
                result += " " + eraValue;
            }
            else
            {
                result = eraValue;
            }
        }

        // Append timezone name if specified
        if (timeZoneNameStr != null)
        {
            if (result.Length > 0)
            {
                result += " " + timeZoneNameStr;
            }
            else
            {
                result = timeZoneNameStr;
            }
        }

        return result;
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

        // Default based on locale's short time pattern
        // If pattern contains uppercase H, locale uses 24-hour; lowercase h means 12-hour
        var timePattern = CultureInfo.DateTimeFormat.ShortTimePattern;
        return timePattern.Contains('H') ? "h24" : "h12";
    }

    /// <summary>
    /// Computes the formatted hour string based on the hourCycle, hour option, and actual hour value.
    /// Returns the formatted hour string and whether AM/PM should be shown.
    /// Per ECMA-402: h11=0-11 (12hr), h12=1-12 (12hr), h23=0-23 (24hr), h24=1-24 (24hr).
    /// 24-hour formats always pad to 2 digits; 12-hour formats pad only for "2-digit" option.
    /// </summary>
    /// <summary>
    /// Computes the formatted hour value based on HourCycle and locale defaults.
    /// </summary>
    /// <param name="hour">The 0-23 hour value</param>
    /// <param name="hourStr">Output: formatted hour string</param>
    /// <param name="use12Hour">Output: whether 12-hour format is used (needs AM/PM)</param>
    /// <param name="padByDefault">If true, always pad h23/h24 hours (used by style-based formatting)</param>
    private void ComputeHourValue(int hour, out string hourStr, out bool use12Hour, bool padByDefault = false)
    {
        int hourValue;

        if (string.Equals(HourCycle, "h11", StringComparison.Ordinal))
        {
            hourValue = hour % 12; // 0-11
            use12Hour = true;
        }
        else if (string.Equals(HourCycle, "h24", StringComparison.Ordinal))
        {
            hourValue = hour == 0 ? 24 : hour; // 1-24
            use12Hour = false;
        }
        else if (string.Equals(HourCycle, "h23", StringComparison.Ordinal))
        {
            hourValue = hour; // 0-23
            use12Hour = false;
        }
        else if (string.Equals(HourCycle, "h12", StringComparison.Ordinal))
        {
            hourValue = hour % 12 == 0 ? 12 : hour % 12; // 1-12
            use12Hour = true;
        }
        else
        {
            // No explicit hourCycle - derive from locale using CLDR defaults
            // (not from .NET CultureInfo which may reflect system user overrides)
            var defaultHc = DateTimeFormatPrototype.GetDefaultHourCycle(Locale);
            if (string.Equals(defaultHc, "h11", StringComparison.Ordinal))
            {
                hourValue = hour % 12; // 0-11
                use12Hour = true;
            }
            else if (string.Equals(defaultHc, "h23", StringComparison.Ordinal))
            {
                hourValue = hour; // 0-23
                use12Hour = false;
            }
            else if (string.Equals(defaultHc, "h24", StringComparison.Ordinal))
            {
                hourValue = hour == 0 ? 24 : hour; // 1-24
                use12Hour = false;
            }
            else
            {
                // h12 default
                hourValue = hour % 12 == 0 ? 12 : hour % 12; // 1-12
                use12Hour = true;
            }
        }

        // Per ECMA-402: 24-hour formats (h23, h24) always pad to 2 digits.
        // 12-hour formats only pad when Hour option is "2-digit".
        var pad = !use12Hour || string.Equals(Hour, "2-digit", StringComparison.Ordinal);
        hourStr = pad ? hourValue.ToString("D2", CultureInfo.InvariantCulture) : hourValue.ToString(CultureInfo.InvariantCulture);
    }

    private string BuildFormatString(List<string> parts)
    {
        // Simple join - a more sophisticated implementation would use
        // locale-specific patterns
        var result = new ValueStringBuilder();
        var hasDate = false;
        var hasTime = false;

        // Check if this format uses a textual month (affects separator choice)
        var hasTextualMonth = Month is "short" or "long" or "narrow";

        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                continue;
            }

            var firstChar = part[0];
            // Escaped literals starting with ' are pre-computed hour values (treated as time component)
            var isHourLiteral = firstChar == '\'';

            if (result.Length > 0)
            {
                // Add separator based on what we're joining
                if (firstChar is 'h' or 'H' or 'm' or 's' or 'f' or 't' || isHourLiteral)
                {
                    if (!hasTime)
                    {
                        if (hasDate)
                        {
                            result.Append("', '"); // Literal ", " between date and time
                        }
                        hasTime = true;
                    }
                    else if (firstChar is not 't' and not 'f' and not '\'')
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
                        // Use appropriate separator based on format type
                        if (hasTextualMonth)
                        {
                            // Textual month format: "Jan 3, 2019"
                            // Use space after month, comma-space before year
                            if (firstChar is 'y' or 'Y')
                            {
                                result.Append("', '"); // Literal ", " before year
                            }
                            else
                            {
                                result.Append(' '); // Space between other parts
                            }
                        }
                        else
                        {
                            // Numeric format: "1/3/2019"
                            // Use literal '/' by escaping with single quotes to avoid .NET's culture-specific date separator
                            result.Append("'/'");
                        }
                    }
                }
            }
            else
            {
                if (firstChar is 'h' or 'H' or 'm' or 's' or 'f' || isHourLiteral)
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
    /// <param name="dateTime">The .NET DateTime to format</param>
    /// <param name="originalYear">Optional original JavaScript year (for dates outside .NET DateTime range)</param>
    /// <param name="isPlain">If true, skip timezone conversion (for plain Temporal types)</param>
    internal List<DateTimePart> FormatToParts(DateTime dateTime, int? originalYear = null, bool isPlain = false)
    {
        // Convert to specified timezone if one was provided
        // For plain Temporal types (isPlain=true), skip timezone conversion
        if (!isPlain)
        {
            if (TimeZone != null)
            {
                dateTime = ConvertToTimeZone(dateTime, TimeZone);
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                // No explicit timezone: convert UTC to engine's default timezone
                var defaultTz = _engine.Options.TimeSystem.DefaultTimeZone;
                dateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, defaultTz);
            }
        }

        var result = new List<DateTimePart>();

        if (DateStyle != null || TimeStyle != null)
        {
            // For style-based formatting, use a simpler approach
            FormatStyleToParts(dateTime, result, originalYear, isPlain);
        }
        else
        {
            FormatComponentsToParts(dateTime, result, originalYear);
        }

        // Apply numbering system transliteration if not using Latin digits
        if (NumberingSystem != null && !string.Equals(NumberingSystem, "latn", StringComparison.OrdinalIgnoreCase))
        {
            for (var i = 0; i < result.Count; i++)
            {
                var part = result[i];
                var transliterated = Data.NumberingSystemData.TransliterateDigits(part.Value, NumberingSystem);
                if (!ReferenceEquals(transliterated, part.Value))
                {
                    result[i] = new DateTimePart(part.Type, transliterated);
                }
            }
        }

        return result;
    }

    private void FormatStyleToParts(DateTime dateTime, List<DateTimePart> result, int? originalYear, bool isPlain = false)
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
            FormatTimeStyleToParts(dateTime, result, isPlain);
        }
    }

    private void FormatDateStyleToParts(DateTime dateTime, List<DateTimePart> result)
    {
        var style = DateStyle;

        // Check if using Chinese or Dangi calendar
        var isChineseCalendar = string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase);
        var isDangiCalendar = string.Equals(Calendar, "dangi", StringComparison.OrdinalIgnoreCase);
        var isLunisolarCalendar = isChineseCalendar || isDangiCalendar;

        // Get Chinese/Dangi calendar date if needed
        ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null;
        if (isLunisolarCalendar)
        {
            lunisolarDate = isChineseCalendar
                ? ChineseCalendarHelper.GetChineseDate(dateTime)
                : ChineseCalendarHelper.GetDangiDate(dateTime);
        }

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
            if (lunisolarDate.HasValue)
            {
                AddLunisolarDateParts(result, lunisolarDate.Value, textualMonth: true);
            }
            else
            {
                result.Add(new DateTimePart("month", dateTime.ToString("MMMM", CultureInfo)));
                result.Add(new DateTimePart("literal", " "));
                result.Add(new DateTimePart("day", dateTime.Day.ToString(CultureInfo)));
                result.Add(new DateTimePart("literal", ", "));
                result.Add(new DateTimePart("year", dateTime.Year.ToString(CultureInfo)));
            }
        }
        else if (string.Equals(style, "medium", StringComparison.Ordinal))
        {
            if (lunisolarDate.HasValue)
            {
                AddLunisolarDateParts(result, lunisolarDate.Value, textualMonth: false);
            }
            else
            {
                result.Add(new DateTimePart("month", dateTime.ToString("MMM", CultureInfo)));
                result.Add(new DateTimePart("literal", " "));
                result.Add(new DateTimePart("day", dateTime.Day.ToString(CultureInfo)));
                result.Add(new DateTimePart("literal", ", "));
                result.Add(new DateTimePart("year", dateTime.Year.ToString(CultureInfo)));
            }
        }
        else // short
        {
            if (lunisolarDate.HasValue)
            {
                AddLunisolarDateParts(result, lunisolarDate.Value, textualMonth: false, shortFormat: true);
            }
            else
            {
                result.Add(new DateTimePart("month", dateTime.Month.ToString(CultureInfo)));
                result.Add(new DateTimePart("literal", "/"));
                result.Add(new DateTimePart("day", dateTime.Day.ToString(CultureInfo)));
                result.Add(new DateTimePart("literal", "/"));
                result.Add(new DateTimePart("year", (dateTime.Year % 100).ToString("D2", CultureInfo)));
            }
        }
    }

    /// <summary>
    /// Adds date parts for Chinese/Dangi lunisolar calendars.
    /// </summary>
    private void AddLunisolarDateParts(List<DateTimePart> result, ChineseCalendarHelper.ChineseCalendarDate date, bool textualMonth, bool shortFormat = false)
    {
        var lang = Locale.Split('-')[0].ToLowerInvariant();
        var isChineseLocale = string.Equals(lang, "zh", StringComparison.Ordinal);

        // Month
        result.Add(new DateTimePart("month", date.Month.ToString(CultureInfo)));
        result.Add(new DateTimePart("literal", "/"));

        // Day
        result.Add(new DateTimePart("day", date.Day.ToString(CultureInfo)));
        result.Add(new DateTimePart("literal", "/"));

        // Year - use relatedYear and yearName for lunisolar calendars
        if (shortFormat)
        {
            result.Add(new DateTimePart("relatedYear", (date.RelatedYear % 100).ToString("D2", CultureInfo)));
        }
        else
        {
            result.Add(new DateTimePart("relatedYear", date.RelatedYear.ToString(CultureInfo)));
        }

        // Add yearName for Chinese locale
        if (isChineseLocale && !shortFormat)
        {
            result.Add(new DateTimePart("yearName", date.YearName));
            result.Add(new DateTimePart("literal", "年"));
        }
    }

    private void FormatTimeStyleToParts(DateTime dateTime, List<DateTimePart> result, bool isPlain = false)
    {
        var style = TimeStyle;
        ComputeHourValue(dateTime.Hour, out var hourStr, out var use12Hour, padByDefault: true);

        // Hour
        result.Add(new DateTimePart("hour", hourStr));

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

        // Time zone name (for long and full) - omit for plain Temporal types
        if (!isPlain)
        {
            if (string.Equals(style, "full", StringComparison.Ordinal))
            {
                result.Add(new DateTimePart("literal", " "));
                result.Add(new DateTimePart("timeZoneName", GetTimeZoneDisplayName(dateTime, longName: true, generic: false)));
            }
            else if (string.Equals(style, "long", StringComparison.Ordinal))
            {
                result.Add(new DateTimePart("literal", " "));
                result.Add(new DateTimePart("timeZoneName", GetTimeZoneDisplayName(dateTime, longName: false, generic: false)));
            }
        }
    }

    private string GetTimeZoneDisplayName(DateTime utcDateTime, bool longName, bool generic)
    {
        if (TimeZone != null)
        {
            if (string.Equals(TimeZone, "UTC", StringComparison.OrdinalIgnoreCase))
            {
                return longName ? "Coordinated Universal Time" : "UTC";
            }

            // Handle offset timezone format like "+00:00", "+03:00", "-07:30"
            var offset = TryParseOffset(TimeZone);
            if (offset.HasValue)
            {
                return FormatGmtOffset(offset.Value, longName);
            }

            // Try CLDR metazone data first (provides locale-correct names)
            var isDst = false;
            try
            {
                var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
                isDst = tzInfo.IsDaylightSavingTime(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
            }
            catch
            {
                // If timezone not found, isDst stays false
            }

            var cldrName = Data.MetaZoneData.GetDisplayName(TimeZone, isDst, longName, generic);
            if (cldrName != null)
            {
                return cldrName;
            }

            // Fallback to .NET TimeZoneInfo names
            try
            {
                var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
                if (longName)
                {
                    return isDst ? tzInfo.DaylightName : tzInfo.StandardName;
                }
                var parts = TimeZone.Split('/');
                return parts[parts.Length - 1].Replace('_', ' ');
            }
            catch
            {
                var parts = TimeZone.Split('/');
                return longName ? TimeZone : parts[parts.Length - 1];
            }
        }
        return longName ? TimeZoneInfo.Local.StandardName : TimeZoneInfo.Local.Id;
    }

    /// <summary>
    /// Formats a GMT offset display name. Short: "GMT+1", Long: "GMT+01:00".
    /// </summary>
    private static string FormatGmtOffset(TimeSpan offset, bool longName)
    {
        if (offset == TimeSpan.Zero)
        {
            return "GMT";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absOffset = offset < TimeSpan.Zero ? offset.Negate() : offset;

        if (longName)
        {
            return $"GMT{sign}{absOffset.Hours:D2}:{absOffset.Minutes:D2}";
        }

        if (absOffset.Minutes == 0)
        {
            return $"GMT{sign}{absOffset.Hours}";
        }

        return $"GMT{sign}{absOffset.Hours}:{absOffset.Minutes:D2}";
    }

    /// <summary>
    /// Gets the formatted timezone name based on the TimeZoneName option.
    /// </summary>
    private string GetFormattedTimeZoneName(DateTime dateTime)
    {
        if (string.Equals(TimeZoneName, "long", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: true, generic: false);
        }
        if (string.Equals(TimeZoneName, "longGeneric", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: true, generic: true);
        }
        if (string.Equals(TimeZoneName, "short", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: false, generic: false);
        }
        if (string.Equals(TimeZoneName, "shortGeneric", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: false, generic: true);
        }
        if (string.Equals(TimeZoneName, "longOffset", StringComparison.Ordinal))
        {
            return "GMT" + dateTime.ToString("zzz", CultureInfo);
        }
        if (string.Equals(TimeZoneName, "shortOffset", StringComparison.Ordinal))
        {
            return "GMT" + dateTime.ToString("zzz", CultureInfo);
        }
        return GetTimeZoneDisplayName(dateTime, longName: false, generic: false);
    }

    private void FormatComponentsToParts(DateTime dateTime, List<DateTimePart> result, int? originalYear = null)
    {
        var hasDate = false;
        var hasTime = false;

        // Check if using Chinese or Dangi calendar
        var isChineseCalendar = string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase);
        var isDangiCalendar = string.Equals(Calendar, "dangi", StringComparison.OrdinalIgnoreCase);
        var isLunisolarCalendar = isChineseCalendar || isDangiCalendar;

        // Get Chinese/Dangi calendar date if needed
        ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null;
        if (isLunisolarCalendar)
        {
            lunisolarDate = isChineseCalendar
                ? ChineseCalendarHelper.GetChineseDate(dateTime)
                : ChineseCalendarHelper.GetDangiDate(dateTime);
        }

        // Determine locale-specific date order and separators
        var formatInfo = GetLocaleDateFormat();
        var dateOrder = formatInfo.DateOrder;
        var dateSeparator = formatInfo.DateSeparator;
        var hasTextualMonth = formatInfo.HasTextualMonth;

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

        // Add date components in locale-specific order
        foreach (var component in dateOrder)
        {
            switch (component)
            {
                case 'M' when Month != null:
                    AddMonthPart(dateTime, result, ref hasDate, dateSeparator, hasTextualMonth, lunisolarDate);
                    break;
                case 'd' when Day != null:
                    AddDayPart(dateTime, result, ref hasDate, dateSeparator, hasTextualMonth, lunisolarDate);
                    break;
                case 'y' when Year != null:
                    AddYearPart(dateTime, result, ref hasDate, dateSeparator, hasTextualMonth, lunisolarDate, originalYear);
                    break;
            }
        }

        // Era (after date components)
        if (Era != null)
        {
            var eraName = GetEraName(dateTime, Calendar ?? "gregory", Era, originalYear);
            if (eraName != null)
            {
                if (result.Count > 0)
                {
                    result.Add(new DateTimePart("literal", " "));
                }
                result.Add(new DateTimePart("era", eraName));
            }
        }

        // Hour - use pre-computed value to handle all hour cycles (h11/h12/h23/h24)
        bool hourUse12Hour = false;
        if (Hour != null)
        {
            if (result.Count > 0)
            {
                result.Add(new DateTimePart("literal", hasDate ? ", " : ""));
            }
            ComputeHourValue(dateTime.Hour, out var hourStr, out var use12Hr);
            hourUse12Hour = use12Hr;
            result.Add(new DateTimePart("hour", hourStr));
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
            // Use the decimal separator for the numbering system (e.g., ٫ for Arabic)
            var decimalSeparator = NumberingSystem != null
                ? Data.NumberingSystemData.GetDecimalSeparator(NumberingSystem).ToString()
                : ".";
            result.Add(new DateTimePart("literal", decimalSeparator));
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
        else if (Hour != null && hourUse12Hour)
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("dayPeriod", dateTime.ToString("tt", CultureInfo)));
        }

        // Time zone name
        if (TimeZoneName != null)
        {
            result.Add(new DateTimePart("literal", " "));
            result.Add(new DateTimePart("timeZoneName", GetFormattedTimeZoneName(dateTime)));
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

    internal readonly record struct DateTimePart(string Type, string Value);
}

using System.Globalization;
using System.Text;
using Jint.Native.Object;
using Jint.Native.Temporal;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#datetimeformat-objects
/// Represents an Intl.DateTimeFormat instance with locale-aware date/time formatting.
/// </summary>
internal sealed class JsDateTimeFormat : ObjectInstance
{
    internal JsDateTimeFormat(
        Engine engine,
        ObjectInstance prototype,
        string locale,
        string? calendar,
        in Data.ResolvedNumberingSystem numberingSystem,
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
        _numberingSystem = numberingSystem;
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

    private readonly Data.ResolvedNumberingSystem _numberingSystem;

    /// <summary>The dateStyle pattern, split once: a formatter's style and locale never change.</summary>
    private List<PatternRun>? _dateStyleRuns;

    internal string Locale { get; }
    internal string? Calendar { get; }
    internal string NumberingSystem => _numberingSystem.Name;

    /// <summary>The numbering system resolved once at construction, digits and all.</summary>
    internal Data.ResolvedNumberingSystem ResolvedNumberingSystem => _numberingSystem;
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

        // For non-ISO non-Gregorian calendars, route through FormatToParts so that the year/
        // month/day overrides applied there (calendar-aware values) are reflected in format()
        // output too — otherwise format() prints the underlying ISO date (March 15, 2024)
        // while formatToParts() prints the calendar fields (Adar 5, 5784), and the test in
        // lunisolar-leap-months.js asserts they match.
        var isNonIsoCalendar = Calendar is not null
            && !string.Equals(Calendar, "iso8601", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Calendar, "gregory", StringComparison.OrdinalIgnoreCase);

        // A year no DateTime can hold arrives on a representative year with an override beside it, and
        // the parts lane is the one that knows what to do with a per-field override - AddYearPart reads
        // originalYear, FormatDateStyleToParts reads it, and neither .NET format strings nor the
        // literal-splicing FormatWithComponents used to do can express a year outside 1-9999 at all.
        // This is the same delegation era and the non-Gregorian calendars already take, and for the
        // same reason.
        // A dateStyle or a timeStyle formats through the locale's own pattern, and the parts lane is where
        // that pattern is split. https://tc39.es/ecma402/#sec-formatdatetime is the concatenation of the very
        // list https://tc39.es/ecma402/#sec-formatdatetimetoparts walks, so there is one decomposition here,
        // not two that drift.
        var hasStyle = DateStyle != null || TimeStyle != null;

        if (isLunisolarCalendar || hasEra || isNonIsoCalendar || originalYear.HasValue || hasStyle)
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

        // Everything that reaches here builds its format from the component options.
        var result = FormatWithComponents(dateTime, originalYear);

        // Write [[NumberingSystem]]'s digits, and only its digits. https://tc39.es/ecma402/#sec-formatdatetimepattern
        // splits the pattern with PartitionPattern and copies every "literal" through untouched; the
        // numbering system reaches a field's value only, through the FormatNumeric calls in the "numeric"
        // and "2-digit" branches. Those values are integers, so no field carries a decimal separator to
        // rewrite - and rewriting every full stop instead reached the ones de-DE's own date pattern owns,
        // turning 27.08.2026 into ٢٧٫٠٨٫٢٠٢٦.
        if (_numberingSystem.RewritesDigits)
        {
            result = _numberingSystem.TransliterateDigitsOnly(result);
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

            // Apply the offset, saturating rather than throwing. The last instant DateTime can hold has no
            // DateTime to land on once a positive offset is added, and Add answers that with an
            // ArgumentOutOfRangeException — a CLR exception, which leaves engine.Evaluate without ever
            // reaching a script try/catch. TimeZoneInfo.ConvertTimeFromUtc, the named-zone branch below,
            // clamps at MinValue/MaxValue in exactly this situation, so the two branches now agree.
            var shiftedTicks = dateTime.Ticks + offset.Value.Ticks;
            if (shiftedTicks < DateTime.MinValue.Ticks)
            {
                shiftedTicks = DateTime.MinValue.Ticks;
            }
            else if (shiftedTicks > DateTime.MaxValue.Ticks)
            {
                shiftedTicks = DateTime.MaxValue.Ticks;
            }

            return new DateTime(shiftedTicks, DateTimeKind.Utc);
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
            "ethiopic" => GetEthiopicEra(effectiveYear, style, eraNames),
            "ethioaa" => GetEthioAaEra(style, eraNames),
            "coptic" => GetCopticEra(effectiveYear, style, eraNames),
            "islamic" or "islamic-civil" or "islamic-tbla" or "islamic-umalqura" => GetIslamicEra(effectiveYear, dateTime, style, eraNames),
            _ => GetGregorianEra(effectiveYear, style, eraNames) // Default to Gregorian
        };
    }

    /// <summary>
    /// Whether the date <paramref name="year"/> names, taking its month and day from
    /// <paramref name="dateTime"/>, falls on or after the given proleptic Gregorian date.
    ///
    /// The year has to come in separately because <paramref name="dateTime"/> may be standing on a
    /// representative year — congruent to the real one mod 400, so its month, day and weekday are the
    /// real ones and its year is not. Every era boundary here is an absolute date, so comparing against
    /// the substitute's year answers about a year 275760 years away from the one asked about. It used to
    /// happen to work, because the clamp this replaces put such a value on year 9999 or year 1, which
    /// is on the right side of every one of these boundaries by accident.
    /// </summary>
    private static bool IsOnOrAfter(int year, DateTime dateTime, int boundaryYear, int boundaryMonth, int boundaryDay)
    {
        if (year != boundaryYear)
        {
            return year > boundaryYear;
        }

        // A year DateTime can hold is never substituted, so within the boundary year the month and day
        // beside it are the real ones.
        return dateTime.Month > boundaryMonth || (dateTime.Month == boundaryMonth && dateTime.Day >= boundaryDay);
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

        if (IsOnOrAfter(effectiveYear, dateTime, 2019, 5, 1))
        {
            return isNarrow ? "R" : "Reiwa";
        }

        if (IsOnOrAfter(effectiveYear, dateTime, 1989, 1, 8))
        {
            return isNarrow ? "H" : "Heisei";
        }

        if (IsOnOrAfter(effectiveYear, dateTime, 1926, 12, 25))
        {
            return isNarrow ? "S" : "Shōwa";
        }

        if (IsOnOrAfter(effectiveYear, dateTime, 1912, 7, 30))
        {
            return isNarrow ? "T" : "Taishō";
        }

        if (IsOnOrAfter(effectiveYear, dateTime, 1868, 1, 25))
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

    private static string GetEthiopicEra(int year, string style, string[]? eraNames)
    {
        // Ethiopic has two eras: Anno Mundi (AA) for Ethiopic years ≤ 0, Era of the Incarnation
        // (AM) for Ethiopic years ≥ 1. Ethiopic year 1 starts roughly ISO 8 CE; the easy
        // approximation that suffices here is "ISO year ≥ 8 → AM, otherwise AA".
        var isAm = year >= 8;
        return style switch
        {
            "long" => isAm ? "Era of the Incarnation" : "Anno Mundi",
            "short" => isAm ? "ERA1" : "ERA0",
            "narrow" => isAm ? "ERA1" : "ERA0",
            _ => isAm ? "ERA1" : "ERA0"
        };
    }

    private static string GetEthioAaEra(string style, string[]? eraNames)
    {
        // Ethio-AA (Amete Alem) — single era spanning all years.
        return style switch
        {
            "long" => "Anno Mundi",
            "short" => "ERA0",
            "narrow" => "ERA0",
            _ => "ERA0"
        };
    }

    private static string GetCopticEra(int year, string style, string[]? eraNames)
    {
        // Coptic has a single era (Anno Martyrum / Era of the Martyrs) per the spec; both
        // positive and negative Coptic years use the same era name.
        return style switch
        {
            "long" => "Era of the Martyrs",
            "short" => "AM",
            "narrow" => "AM",
            _ => "AM"
        };
    }

    private static string GetIslamicEra(int year, DateTime dateTime, string style, string[]? eraNames)
    {
        // Islamic has two eras: AH (Anno Hegirae) for dates ≥ 622-07-16 CE Gregorian (the Hijra
        // epoch) and BH (Before Hijra) for earlier dates.
        var isAh = IsOnOrAfter(year, dateTime, 622, 7, 16);
        return style switch
        {
            "long" => isAh ? "Anno Hegirae" : "Before Hijra",
            "short" => isAh ? "AH" : "BH",
            "narrow" => isAh ? "AH" : "BH",
            _ => isAh ? "AH" : "BH"
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
    /// Determines the locale-specific date format order and separator
    /// by parsing the ShortDatePattern from DateTimeFormatInfo.
    /// </summary>
    private LocaleDateFormatInfo GetLocaleDateFormat()
    {
        var hasTextualMonth = Month != null && Month is "long" or "short" or "narrow";

        // Derive date order and separator from the locale's ShortDatePattern (e.g., "dd-MM-yyyy", "M/d/yyyy")
        var pattern = CultureInfo.DateTimeFormat.ShortDatePattern;
        var dateOrder = ParseDateOrder(pattern);
        var dateSeparator = hasTextualMonth ? " " : CultureInfo.DateTimeFormat.DateSeparator;

        return new LocaleDateFormatInfo(dateOrder, dateSeparator, hasTextualMonth);
    }

    /// <summary>
    /// Parses a .NET ShortDatePattern to extract the date component order (e.g., "dMy", "Mdy", "yMd").
    /// </summary>
    private static string ParseDateOrder(string pattern)
    {
        var order = new StringBuilder(3);
        foreach (var c in pattern)
        {
            var component = char.ToLowerInvariant(c) switch
            {
                'd' => 'd',
                'm' => 'M',
                'y' => 'y',
                _ => '\0'
            };

            if (component != '\0' && (order.Length == 0 || order[order.Length - 1] != component))
            {
                order.Append(component);
                if (order.Length == 3)
                {
                    break;
                }
            }
        }

        return order.Length == 3 ? order.ToString() : "dMy"; // fallback to DMY
    }

    /// <summary>
    /// For non-ISO/non-Gregorian/non-lunisolar calendars, returns the calendar's view of the
    /// given DateTime via the three out parameters. Sets all three to null for ISO/Gregorian
    /// (the caller falls back to dateTime.Year/Month/Day) or for lunisolar calendars (which
    /// take a separate code path via ChineseCalendarHelper).
    /// </summary>
    private void ResolveCalendarFieldsForFormatting(DateTime dateTime, int? originalYear, out int? year, out int? month, out int? day)
    {
        year = null;
        month = null;
        day = null;

        if (Calendar is null) return;
        if (string.Equals(Calendar, "iso8601", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Calendar, "gregory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Lunisolar calendars are handled separately via ChineseCalendarHelper / lunisolarDate.
        if (string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Calendar, "dangi", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Simple offset calendars: cheap arithmetic without going through NonIsoCalendars.
        var sourceYear = originalYear ?? dateTime.Year;
        if (string.Equals(Calendar, "buddhist", StringComparison.OrdinalIgnoreCase))
        {
            year = sourceYear + 543;
            month = dateTime.Month;
            day = dateTime.Day;
            return;
        }
        if (string.Equals(Calendar, "roc", StringComparison.OrdinalIgnoreCase))
        {
            year = sourceYear - 1911;
            month = dateTime.Month;
            day = dateTime.Day;
            return;
        }
        if (string.Equals(Calendar, "japanese", StringComparison.OrdinalIgnoreCase))
        {
            // For Japanese, the displayed year is the era year of whichever era contains the
            // given date. Pre-Meiji dates fall back to the Gregorian year. The comparisons read
            // sourceYear rather than dateTime.Year for the reason IsOnOrAfter gives: a year outside
            // DateTime's range arrives on a substitute congruent mod 400, whose month and day are real
            // and whose year is not.
            var dt = dateTime;
            int? eraYear = null;
            if (IsOnOrAfter(sourceYear, dt, 2019, 5, 1))
                eraYear = sourceYear - 2018; // Reiwa
            else if (IsOnOrAfter(sourceYear, dt, 1989, 1, 8))
                eraYear = sourceYear - 1988; // Heisei
            else if (IsOnOrAfter(sourceYear, dt, 1926, 12, 25))
                eraYear = sourceYear - 1925; // Showa
            else if (IsOnOrAfter(sourceYear, dt, 1912, 7, 30))
                eraYear = sourceYear - 1911; // Taisho
            else if (IsOnOrAfter(sourceYear, dt, 1868, 1, 25))
                eraYear = sourceYear - 1867; // Meiji
            if (eraYear.HasValue)
            {
                year = eraYear;
                month = dt.Month;
                day = dt.Day;
            }
            return;
        }

        // Other non-ISO calendars go through the full IsoToCalendarDate machinery.
        try
        {
            // With the engine, so a calendar a host ICalendarProvider added is converted by the provider that
            // knows it rather than falling through to the catch below and printing the underlying ISO date.
            var isoDate = new IsoDate(originalYear ?? dateTime.Year, dateTime.Month, dateTime.Day);
            var calDate = NonIsoCalendars.IsoToCalendarDate(Calendar, in isoDate, _engine);
            year = calDate.Year;
            month = calDate.Month;
            day = calDate.Day;
        }
        catch
        {
            // ignore
        }
    }

    private void AddMonthPart(DateTime dateTime, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth, ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null, int? overrideMonth = null)
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
        else if (overrideMonth.HasValue)
        {
            // Calendar-aware override (e.g. coptic month for the underlying ISO date).
            // Textual month styles fall back to numeric since we don't have month-name data
            // for arbitrary non-ISO calendars.
            monthValue = Month switch
            {
                "numeric" => overrideMonth.Value.ToString(CultureInfo),
                "2-digit" => overrideMonth.Value.ToString("D2", CultureInfo),
                _ => overrideMonth.Value.ToString(CultureInfo)
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

    private void AddDayPart(DateTime dateTime, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth, ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null, int? overrideDay = null)
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
        else if (overrideDay.HasValue)
        {
            dayValue = Day switch
            {
                "numeric" => overrideDay.Value.ToString(CultureInfo),
                "2-digit" => overrideDay.Value.ToString("D2", CultureInfo),
                _ => overrideDay.Value.ToString("D2", CultureInfo)
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

    private void AddYearPart(DateTime dateTime, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth, ChineseCalendarHelper.ChineseCalendarDate? lunisolarDate = null, int? originalYear = null, int? overrideYear = null)
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
            var lang = IntlUtilities.GetLanguageSubtag(Locale).ToLowerInvariant();
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
            // Use override (calendar-aware) year if available, else the original year (for
            // dates outside .NET DateTime range), else the underlying ISO year.
            var effectiveYear = overrideYear ?? originalYear ?? dateTime.Year;

            // For proleptic Gregorian calendar with era, convert negative years to positive BC years
            // Year 0 in astronomical notation = 1 BC, year -1 = 2 BC, etc.
            int displayYear;
            if (Era != null && effectiveYear <= 0)
            {
                displayYear = 1 - effectiveYear;
            }
            else
            {
                displayYear = effectiveYear; // Keep sign for iso8601/gregorian without era
            }

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

    /// <summary>
    /// One run of a .NET custom date/time format pattern: either a repeated field letter, or a stretch of
    /// literal text, which is the split <see href="https://tc39.es/ecma402/#sec-partitionpattern">
    /// PartitionPattern</see> performs and whose literals it copies through untouched.
    /// </summary>
    private readonly record struct PatternRun(char Field, int Length, string? Literal)
    {
        /// <summary>Whether this run is literal text rather than a field to render.</summary>
        public bool IsLiteral => Field == '\0';
    }

    /// <summary>The letters .NET's custom date and time format strings reserve for fields.</summary>
    private static bool IsPatternField(char c)
        => c is 'd' or 'f' or 'F' or 'g' or 'h' or 'H' or 'K' or 'm' or 'M' or 's' or 't' or 'y' or 'z';

    /// <summary>
    /// Splits a .NET custom date/time format pattern into field runs and literal runs, unquoting
    /// <c>'...'</c> and <c>"..."</c> spans and resolving backslash escapes, so that one pattern can be both
    /// rendered as a string and partitioned into typed parts.
    /// </summary>
    private static List<PatternRun> SplitPattern(string pattern)
    {
        var runs = new List<PatternRun>();
        var literal = new StringBuilder();

        for (var i = 0; i < pattern.Length;)
        {
            var c = pattern[i];

            if (c is '\'' or '"')
            {
                i++;
                while (i < pattern.Length && pattern[i] != c)
                {
                    if (pattern[i] == '\\' && i + 1 < pattern.Length)
                    {
                        i++;
                    }

                    literal.Append(pattern[i]);
                    i++;
                }

                if (i < pattern.Length)
                {
                    i++;
                }

                continue;
            }

            if (c == '\\')
            {
                if (i + 1 < pattern.Length)
                {
                    literal.Append(pattern[i + 1]);
                }

                i += 2;
                continue;
            }

            // "%M" only tells .NET that the single letter is a custom format; it writes nothing itself.
            if (c == '%')
            {
                i++;
                continue;
            }

            if (!IsPatternField(c))
            {
                literal.Append(c);
                i++;
                continue;
            }

            var length = 1;
            while (i + length < pattern.Length && pattern[i + length] == c)
            {
                length++;
            }

            if (literal.Length > 0)
            {
                runs.Add(new PatternRun('\0', 0, literal.ToString()));
                literal.Clear();
            }

            runs.Add(new PatternRun(c, length, null));
            i += length;
        }

        if (literal.Length > 0)
        {
            runs.Add(new PatternRun('\0', 0, literal.ToString()));
        }

        return runs;
    }

    /// <summary>
    /// The ECMA-402 part type a pattern field writes, per the field table in
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern.
    /// </summary>
    private static string PartTypeOf(char field, int length) => field switch
    {
        'd' => length >= 3 ? "weekday" : "day",
        'M' => "month",
        'y' => "year",
        'g' => "era",
        'h' or 'H' => "hour",
        'm' => "minute",
        's' => "second",
        'f' or 'F' => "fractionalSecond",
        't' => "dayPeriod",
        'z' or 'K' => "timeZoneName",
        _ => "literal"
    };

    /// <summary>
    /// https://tc39.es/ecma402/#sec-date-time-style-format - the pattern a <c>dateStyle</c> formats with comes
    /// from the locale's own data, which on .NET is that culture's long or short date pattern.
    /// </summary>
    private List<PatternRun> GetDateStyleRuns()
    {
        var formatInfo = CultureInfo.DateTimeFormat;

        if (string.Equals(DateStyle, "short", StringComparison.Ordinal))
        {
            var shortRuns = SplitPattern(formatInfo.ShortDatePattern);
            if (shortRuns.Count == 0)
            {
                return SplitPattern("M'/'d'/'yy");
            }

            // .NET widens the two-digit year CLDR's short form asks for to four digits. English keeps
            // CLDR's, which is the "8/27/26" this lane has always written.
            if (string.Equals(IntlUtilities.GetLanguageSubtag(Locale), "en", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < shortRuns.Count; i++)
                {
                    if (shortRuns[i].Field == 'y')
                    {
                        shortRuns[i] = shortRuns[i] with { Length = 2 };
                    }
                }
            }

            return shortRuns;
        }

        var runs = SplitPattern(formatInfo.LongDatePattern);
        if (runs.Count == 0)
        {
            runs = SplitPattern("MMMM d, yyyy");
        }

        if (string.Equals(DateStyle, "full", StringComparison.Ordinal))
        {
            return runs;
        }

        // "long" and "medium" are the same pattern without the weekday .NET's long date pattern carries.
        RemoveWeekdayRun(runs);

        if (string.Equals(DateStyle, "medium", StringComparison.Ordinal))
        {
            // Medium is the abbreviated form: CLDR writes "MMM" where long writes "MMMM".
            for (var i = 0; i < runs.Count; i++)
            {
                if (runs[i].Field == 'M' && runs[i].Length >= 4)
                {
                    runs[i] = runs[i] with { Length = 3 };
                }
            }
        }

        return runs;
    }

    /// <summary>
    /// Removes the weekday run and the punctuation that was there only to separate it, leaving a neighbouring
    /// literal that is real text - Japanese day marks, Portuguese "de" - every character it had.
    /// </summary>
    private static void RemoveWeekdayRun(List<PatternRun> runs)
    {
        var index = -1;
        for (var i = 0; i < runs.Count; i++)
        {
            if (runs[i].Field == 'd' && runs[i].Length >= 3)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return;
        }

        runs.RemoveAt(index);

        if (index < runs.Count && runs[index].IsLiteral)
        {
            ReplaceLiteralRun(runs, index, TrimWeekdaySeparator(runs[index].Literal!, fromStart: true));
        }
        else if (index > 0 && runs[index - 1].IsLiteral)
        {
            ReplaceLiteralRun(runs, index - 1, TrimWeekdaySeparator(runs[index - 1].Literal!, fromStart: false));
        }
    }

    private static void ReplaceLiteralRun(List<PatternRun> runs, int index, string literal)
    {
        if (literal.Length == 0)
        {
            runs.RemoveAt(index);
        }
        else
        {
            runs[index] = new PatternRun('\0', 0, literal);
        }
    }

    /// <summary>
    /// Strips from a literal only the punctuation that set the weekday off - a comma and its spaces - so a
    /// full stop that terminates the day instead ("d., dddd" in Hungarian) stays where it was.
    /// </summary>
    private static string TrimWeekdaySeparator(string literal, bool fromStart)
    {
        var start = 0;
        var end = literal.Length;

        if (fromStart)
        {
            while (start < end && char.IsWhiteSpace(literal[start]))
            {
                start++;
            }

            if (start < end && IsListSeparator(literal[start]))
            {
                start++;
            }

            while (start < end && char.IsWhiteSpace(literal[start]))
            {
                start++;
            }
        }
        else
        {
            while (end > start && char.IsWhiteSpace(literal[end - 1]))
            {
                end--;
            }

            if (end > start && IsListSeparator(literal[end - 1]))
            {
                end--;
            }

            while (end > start && char.IsWhiteSpace(literal[end - 1]))
            {
                end--;
            }
        }

        return literal.Substring(start, end - start);
    }

    /// <summary>The marks a date pattern uses to set the weekday off from the rest of the date.</summary>
    private static bool IsListSeparator(char c)
        => c is ',' or ';' or '\u060C' or '\u3001' or '\u00B7';

    /// <summary>
    /// Renders one pattern into parts. A calendar .NET is not counting this date in contributes numeric
    /// year/month/day overrides, and <paramref name="originalYear"/> a year outside DateTime's range.
    /// </summary>
    private void AppendPatternParts(List<PatternRun> runs, DateTime dateTime, List<DateTimePart> result, int? originalYear)
    {
        ResolveCalendarFieldsForFormatting(dateTime, originalYear, out var calendarYear, out var calendarMonth, out var calendarDay);

        // A culture already counting in the requested calendar renders every field itself, month names
        // included; the numeric override is only for the calendars .NET is not reckoning this date in.
        if (calendarYear.HasValue && CultureCalendarAgrees(dateTime, calendarYear.Value, calendarMonth, calendarDay))
        {
            calendarMonth = null;
            calendarDay = null;
        }

        var yearOverride = calendarYear ?? originalYear;

        var hasNumericDay = false;
        foreach (var run in runs)
        {
            if (run.Field == 'd' && run.Length <= 2)
            {
                hasNumericDay = true;
                break;
            }
        }

        foreach (var run in runs)
        {
            if (run.IsLiteral)
            {
                result.Add(new DateTimePart("literal", run.Literal!));
                continue;
            }

            string? value = null;
            if (run.Field == 'y' && yearOverride.HasValue)
            {
                value = FormatOverride(run.Length == 2 ? yearOverride.Value % 100 : yearOverride.Value, run.Length);
            }
            else if (run.Field == 'M' && calendarMonth.HasValue)
            {
                value = FormatOverride(calendarMonth.Value, run.Length);
            }
            else if (run.Field == 'd' && run.Length <= 2 && calendarDay.HasValue)
            {
                value = FormatOverride(calendarDay.Value, run.Length);
            }
            else if (run.Field == 'M' && run.Length >= 4 && hasNumericDay)
            {
                value = GenitiveMonthName(dateTime);
            }

            if (value is null)
            {
                var specifier = run.Length == 1 ? "%" + run.Field : new string(run.Field, run.Length);
                value = dateTime.ToString(specifier, CultureInfo);
            }

            result.Add(new DateTimePart(PartTypeOf(run.Field, run.Length), value));
        }
    }

    /// <summary>
    /// The genitive month name a culture that has one writes beside a numeric day - Russian "27 августа" against
    /// a bare "август" - which .NET chooses from the whole pattern and a run rendered alone would lose.
    /// </summary>
    private string? GenitiveMonthName(DateTime dateTime)
    {
        var formatInfo = CultureInfo.DateTimeFormat;

        int month;
        try
        {
            month = formatInfo.Calendar.GetMonth(dateTime);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var genitive = formatInfo.MonthGenitiveNames;
        var nominative = formatInfo.MonthNames;
        if (month < 1 || month > genitive.Length || month > nominative.Length)
        {
            return null;
        }

        var name = genitive[month - 1];

        // A culture that writes one month name in every position gets nothing here, so the pattern letter
        // renders it and every calendar .NET writes as digits keeps doing so.
        return name.Length > 0 && !string.Equals(name, nominative[month - 1], StringComparison.Ordinal)
            ? name
            : null;
    }

    /// <summary>
    /// Writes an overridden field value. Only a two-letter run pads, because a calendar year is written as it
    /// is counted - Reiwa 8 is "8", not "0008" - and a textual month run has no name left to write.
    /// </summary>
    private string FormatOverride(int value, int length)
        => length == 2 ? value.ToString("D2", CultureInfo) : value.ToString(CultureInfo);

    /// <summary>
    /// Whether the culture this formatter renders through already counts the given date in the calendar the
    /// override was computed for, in which case its own month and weekday names are the right ones.
    /// </summary>
    private bool CultureCalendarAgrees(DateTime dateTime, int year, int? month, int? day)
    {
        try
        {
            var calendar = CultureInfo.DateTimeFormat.Calendar;
            return calendar.GetYear(dateTime) == year
                && (!month.HasValue || calendar.GetMonth(dateTime) == month.Value)
                && (!day.HasValue || calendar.GetDayOfMonth(dateTime) == day.Value);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern step 15.g — the <c>ampm</c> field of a pattern is
    /// "an ILD String representing post meridiem / ante meridiem", not those words, and Annex A lists
    /// "am/pm indicators" among the implementation- and locale-dependent behaviours. This is the same data the
    /// component lane's <c>tt</c> pattern letter renders through: the designators on this formatter's own
    /// <see cref="DateTimeFormatInfo"/>, which a host <see cref="ICldrProvider.GetDayPeriods"/> has already
    /// had its say over.
    /// </summary>
    private string GetDayPeriod(int hour)
    {
        return hour < 12 ? DateTimeFormatInfo.AMDesignator : DateTimeFormatInfo.PMDesignator;
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
                    // No originalYear branch here: Format routes a value carrying one through
                    // FormatToParts instead. Splicing the real year in as a quoted literal is what this
                    // used to do, and BuildFormatString reads a part beginning with an apostrophe as an
                    // hour - so it put the date/time separator in front of the year and then ran the
                    // real time fields together behind it: "12/31, 27576011:59:59 PM".
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
        // and DayPeriod is not explicitly specified (DayPeriod uses extended periods). An empty designator
        // takes its separator with it, so this lane and the parts lane cannot disagree for a host that
        // supplies one; no culture on any machine has one.
        var needsAmPm = Hour != null && hourUse12Hour && DayPeriod == null && GetDayPeriod(dateTime.Hour).Length > 0;
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
                        // The separator before a fractional second is this formatter's own, not a
                        // pattern's: no CLDR pattern supplies one, because fractionalSecondDigits is a
                        // component option and this method assembles the pattern around it. The parts
                        // lane writes the numbering system's decimal separator for it, so this lane
                        // writes the same character - quoted, so .NET copies it out verbatim.
                        result.Append('\'');
                        result.Append(_numberingSystem.DecimalSeparator);
                        result.Append('\'');
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
                            // Numeric format: use locale-specific date separator
                            var sep = CultureInfo.DateTimeFormat.DateSeparator;
                            result.Append('\'');
                            result.Append(sep);
                            result.Append('\'');
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
            var beforeConversion = dateTime;
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

            // A conversion can carry the representative date over a year boundary - an instant just
            // after midnight on 1 January in a zone behind UTC belongs to the previous year - and
            // originalYear names the year of the value that went in. Move it by what the substitute
            // moved by, so the printed year is the one the wall clock is actually in.
            if (originalYear.HasValue && dateTime.Year != beforeConversion.Year)
            {
                originalYear += dateTime.Year - beforeConversion.Year;
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

        // The digits of every part, and nothing else - the same rewrite Format applies to the assembled
        // string, one part at a time. A "literal" is pattern text, and the one separator this formatter
        // writes itself, before a fractional second, is already the numbering system's.
        if (_numberingSystem.RewritesDigits)
        {
            for (var i = 0; i < result.Count; i++)
            {
                var part = result[i];
                var transliterated = _numberingSystem.TransliterateDigitsOnly(part.Value);
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
            FormatDateStyleToParts(dateTime, result, originalYear);
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

    /// <summary>
    /// The date half of a dateStyle format. <c>originalYear</c> is the real year when
    /// <c>dateTime</c> stands on a representative one: FormatStyleToParts took it and dropped it, so a
    /// styled format of a date outside DateTime's range printed the substitute's year - 9999 - with
    /// nothing to say it had.
    /// </summary>
    private void FormatDateStyleToParts(DateTime dateTime, List<DateTimePart> result, int? originalYear = null)
    {
        var isChineseCalendar = string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase);
        var isDangiCalendar = string.Equals(Calendar, "dangi", StringComparison.OrdinalIgnoreCase);

        if (isChineseCalendar || isDangiCalendar)
        {
            // A lunisolar date is not a run of pattern fields: it writes relatedYear and yearName where a
            // pattern has a year, so it keeps the shape it had.
            var lunisolarDate = isChineseCalendar
                ? ChineseCalendarHelper.GetChineseDate(dateTime)
                : ChineseCalendarHelper.GetDangiDate(dateTime);

            var isFull = string.Equals(DateStyle, "full", StringComparison.Ordinal);
            if (isFull)
            {
                result.Add(new DateTimePart("weekday", dateTime.ToString("dddd", CultureInfo)));
                result.Add(new DateTimePart("literal", ", "));
            }

            AddLunisolarDateParts(
                result,
                lunisolarDate,
                textualMonth: isFull || string.Equals(DateStyle, "long", StringComparison.Ordinal),
                shortFormat: string.Equals(DateStyle, "short", StringComparison.Ordinal));
            return;
        }

        AppendPatternParts(_dateStyleRuns ??= GetDateStyleRuns(), dateTime, result, originalYear);
    }

    /// <summary>
    /// Adds date parts for Chinese/Dangi lunisolar calendars.
    /// </summary>
    private void AddLunisolarDateParts(List<DateTimePart> result, ChineseCalendarHelper.ChineseCalendarDate date, bool textualMonth, bool shortFormat = false)
    {
        var lang = IntlUtilities.GetLanguageSubtag(Locale).ToLowerInvariant();
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

        // Day period (AM/PM) for 12-hour format, from the same locale data the component lane's "tt" reads
        if (use12Hour)
        {
            var dayPeriodName = GetDayPeriod(dateTime.Hour);
            if (dayPeriodName.Length > 0)
            {
                result.Add(new DateTimePart("literal", " "));
                result.Add(new DateTimePart("dayPeriod", dayPeriodName));
            }
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
        // No explicit timezone: use the engine's default timezone
        var defaultTz = _engine.Options.TimeSystem.DefaultTimeZone;

        var defaultIsDst = false;
        try
        {
            defaultIsDst = defaultTz.IsDaylightSavingTime(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
        }
        catch
        {
            // If DST check fails, defaultIsDst stays false
        }

        // Try to get an IANA timezone ID for the CLDR lookup.
        // The IanaToMetaZone table also includes Windows timezone ID aliases for .NET Framework compatibility.
        var defaultTzId = defaultTz.Id;
#if NET8_0_OR_GREATER
        if (!defaultTz.HasIanaId && TimeZoneInfo.TryConvertWindowsIdToIanaId(defaultTzId, out var defaultIanaId))
        {
            defaultTzId = defaultIanaId;
        }
#endif

        var defaultCldrName = Data.MetaZoneData.GetDisplayName(defaultTzId, defaultIsDst, longName, generic);
        if (defaultCldrName != null)
        {
            return defaultCldrName;
        }

        // Fallback to .NET TimeZoneInfo names
        if (longName)
        {
            return defaultIsDst ? defaultTz.DaylightName : defaultTz.StandardName;
        }

        // For short names: try to parse an abbreviation from IANA-style IDs (e.g. "America/New_York" → "New York")
        // Windows timezone IDs don't contain '/', so fall back to DST-aware names
        if (defaultTzId.Contains('/'))
        {
            var defaultParts = defaultTzId.Split('/');
            return defaultParts[defaultParts.Length - 1].Replace('_', ' ');
        }

        return defaultIsDst ? defaultTz.DaylightName : defaultTz.StandardName;
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

        // For non-ISO non-lunisolar calendars, derive (calYear, calMonth, calDay) from the
        // calendar so the year/month/day components reflect the calendar — not the underlying
        // ISO date. Test262 (DateTimeFormat compare-to-temporal) verifies that DTF formats
        // values matching Temporal's calendar-aware year/month/day for buddhist/coptic/hebrew/
        // persian/etc.
        ResolveCalendarFieldsForFormatting(dateTime, originalYear, out var calOverrideYear, out var calOverrideMonth, out var calOverrideDay);

        // Add date components in locale-specific order
        foreach (var component in dateOrder)
        {
            switch (component)
            {
                case 'M' when Month != null:
                    AddMonthPart(dateTime, result, ref hasDate, dateSeparator, hasTextualMonth, lunisolarDate, calOverrideMonth);
                    break;
                case 'd' when Day != null:
                    AddDayPart(dateTime, result, ref hasDate, dateSeparator, hasTextualMonth, lunisolarDate, calOverrideDay);
                    break;
                case 'y' when Year != null:
                    AddYearPart(dateTime, result, ref hasDate, dateSeparator, hasTextualMonth, lunisolarDate, originalYear, calOverrideYear);
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
            var decimalSeparator = _numberingSystem.DecimalSeparator.ToString();
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
            var dayPeriodName = dateTime.ToString("tt", CultureInfo);
            if (dayPeriodName.Length > 0)
            {
                result.Add(new DateTimePart("literal", " "));
                result.Add(new DateTimePart("dayPeriod", dayPeriodName));
            }
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
        var lang = IntlUtilities.GetLanguageSubtag(Locale);

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
                _ => GetDayPeriod(hour)
            };
        }

        // No extended day-period data for this locale: fall back to its own AM/PM designators.
        return GetDayPeriod(hour);
    }

    internal readonly record struct DateTimePart(string Type, string Value);
}

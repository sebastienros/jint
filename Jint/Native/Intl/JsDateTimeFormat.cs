using System.Globalization;
using System.Text;
using Jint.Native.Object;
using Jint.Native.Temporal;

namespace Jint.Native.Intl;

/// <summary>
/// The date fields a <c>dateStyle</c>'s pattern may write, which is the <c>allowedOptions</c> of
/// https://tc39.es/proposal-temporal/#sec-adjustdatetimestyleformat.
/// </summary>
/// <remarks>
/// A <c>dateStyle</c> resolves to the locale's own date pattern, which carries every field a full date
/// has. A <c>Temporal.PlainYearMonth</c> and a <c>Temporal.PlainMonthDay</c> do not have all of them, and
/// the fields they lack are held as reference values that are no part of the value: writing them puts a
/// year of 1972 beside a month and a day. The format such a value is written with is that pattern narrowed
/// to the fields the type carries.
/// </remarks>
internal enum DateStyleFields
{
    /// <summary>Every field the locale's own pattern carries.</summary>
    All,

    /// <summary>Era, year and month - what a <c>Temporal.PlainYearMonth</c> has.</summary>
    YearMonth,

    /// <summary>Month and day - what a <c>Temporal.PlainMonthDay</c> has.</summary>
    MonthDay,
}

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
        CultureInfo cultureInfo,
        DateStyleFields dateStyleFields = DateStyleFields.All) : base(engine)
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
        _dateStyleFields = dateStyleFields;
    }

    private readonly Data.ResolvedNumberingSystem _numberingSystem;

    /// <summary>The dateStyle pattern, split once: a formatter's style and locale never change.</summary>
    private List<PatternRun>? _dateStyleRuns;

    /// <summary>The pattern the format matcher chose for a component bag, once it is first needed.</summary>
    private DateTimeFormatPattern? _componentPattern;

    /// <summary>The range patterns of the component bag's format record, once a range is first written.</summary>
    private DateTimeIntervalFormat? _intervalFormat;

    /// <summary>
    /// The runs <see cref="_hostNames"/> were read for, and a host provider's names per run of them; and the same for
    /// the range patterns, a few at most.
    /// </summary>
    private DateTimePatternRun[]? _hostNamesRuns;
    private string[]?[]? _hostNames;
    private List<(DateTimePatternRun[] Runs, string[]?[]? Names)>? _rangeHostNames;

    /// <summary>Which of that pattern's date fields the value this formatter writes actually has.</summary>
    private readonly DateStyleFields _dateStyleFields;

    internal string Locale { get; }
    internal string? Calendar { get; }
    internal string NumberingSystem => _numberingSystem.Name;

    /// <summary>The numbering system resolved once at construction, digits and all.</summary>
    internal Data.ResolvedNumberingSystem ResolvedNumberingSystem => _numberingSystem;
    internal string? TimeZone { get; }

    /// <summary>
    /// The hour cycle <c>hour12</c>, the <c>hourCycle</c> option or the <c>-u-hc-</c> keyword decided, or null
    /// when none of them did. A formatter derived from this one is handed this, not <see cref="ResolvedHourCycle"/>,
    /// so it reads the locale's default only if it writes an hour.
    /// </summary>
    internal string? HourCycle { get; }

    /// <summary>The locale's own hour cycle, looked up the first time a null <see cref="HourCycle"/> needs it.</summary>
    private string? _localeHourCycle;

    /// <summary>
    /// The hc of https://tc39.es/ecma402/#sec-createdatetimeformat: <see cref="HourCycle"/>, or when that is
    /// null, <c>resolvedLocaleData.[[hourCycle]]</c> — the CLDR provider's preferred cycle for the locale, which
    /// is <c>getHourCycles()[0]</c> of the same locale. <c>resolvedOptions()</c> reports it and every lane
    /// that writes an hour - format, formatToParts and formatRange - writes with it, so the two cannot disagree.
    /// </summary>
    /// <remarks>
    /// The provider is asked about the data locale, as the constructor asks it for <c>hour12</c>: <see cref="Locale"/>
    /// with the <c>-u-ca-</c> and <c>-u-nu-</c> keywords the formatter kept taken off again.
    /// </remarks>
    internal string ResolvedHourCycle
        => HourCycle ?? (_localeHourCycle ??= IntlUtilities.GetLocaleHourCycles(_engine, UnicodeExtension.RemoveSequence(Locale))[0]);

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
    /// The resolved calendar's own month names, one array per textual style, asked for once each and only by
    /// a formatter that writes one. An empty array is "asked, and there are none".
    /// </summary>
    private string[][]? _calendarMonthNames;

    /// <summary>
    /// The name this formatter writes for month <paramref name="month"/> of the calendar it resolved, or null
    /// when there is none and the month number stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// https://tc39.es/ecma402/#table-datetimeformat-components makes <c>"long"</c>, <c>"short"</c> and
    /// <c>"narrow"</c> textual, and a formatter is not free to answer one of them with a number. For a
    /// calendar counting the Gregorian months the name is the locale's own and no lookup happens here — the
    /// Gregorian lane writes it, host-seeded names and all. For a calendar counting months of its own the name
    /// has to come from data that knows that calendar, which is what
    /// <see cref="ICldrProvider.GetMonthNames"/> takes its <c>calendar</c> argument for.
    /// </para>
    /// <para>
    /// The array is indexed by the calendar's month number, so it is as long as that calendar's year: thirteen
    /// for Coptic, Ethiopic and a Hebrew or Chinese leap year. That is why the answer is read from the provider
    /// rather than out of <see cref="DateTimeFormatInfo"/>, whose month arrays hold twelve names and a trailing
    /// empty one. A month the array is too short for keeps its number, and so does an empty name.
    /// </para>
    /// <para>
    /// With no answer the number stands, which is what every release before this one wrote for all three
    /// styles, and what ICU itself writes for <c>"narrow"</c> on every one of these calendars.
    /// </para>
    /// </remarks>
    private string? CalendarMonthName(int month, string? style)
    {
        var index = style switch
        {
            "long" => 0,
            "short" => 1,
            "narrow" => 2,
            _ => -1
        };

        if (index < 0 || month < 1 || AvailableCalendars.UsesGregorianMonths(Calendar))
        {
            return null;
        }

        var cache = _calendarMonthNames ??= new string[3][];
        var names = cache[index];
        if (names is null)
        {
            names = CldrProvider.GetMonthNames(Locale, style!, Calendar) ?? [];
            cache[index] = names;
        }

        return month <= names.Length && names[month - 1].Length > 0 ? names[month - 1] : null;
    }

    /// <summary>
    /// Formats a date according to the formatter's locale and options.
    /// </summary>
    /// <param name="dateTime">The .NET DateTime to format</param>
    /// <param name="originalYear">Optional original JavaScript year (for dates outside .NET DateTime range)</param>
    /// <param name="isPlain">If true, skip timezone conversion (for plain Temporal types)</param>
    internal string Format(DateTime dateTime, int? originalYear = null, bool isPlain = false)
    {
        // https://tc39.es/ecma402/#sec-formatdatetime is the concatenation of the very list
        // https://tc39.es/ecma402/#sec-formatdatetimetoparts walks, so every lane - component bag, dateStyle and
        // timeStyle, the lunisolar calendars - writes its parts once and format() joins them: there is one
        // decomposition, not two that drift.
        var parts = FormatToParts(dateTime, originalYear, isPlain);
        if (parts.Count == 1)
        {
            return parts[0].Value;
        }

        var builder = new ValueStringBuilder(stackalloc char[64]);
        foreach (var part in parts)
        {
            builder.Append(part.Value);
        }

        return builder.ToString();
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
    /// by parsing the ShortDatePattern from DateTimeFormatInfo. Only the Chinese and Dangi lane reads it
    /// (<see cref="FormatLunisolarComponentsToParts"/>); every other component bag writes a CLDR pattern.
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

        // Simple offset calendars: cheap arithmetic without going through NonIsoCalendars. Only the year is
        // overridden, because only the year differs — https://tc39.es/ecma402/#table-datetimeformat-components
        // makes "long", "short" and "narrow" textual month formats, and a month override is what forced them
        // to a number. Leaving month and day to the Gregorian lane is what makes these calendars write the
        // very name gregory writes, in every style and through both the component and the pattern lane, which
        // is also what ICU does: buddhist, japanese and roc are the Gregorian months under another era.
        var sourceYear = originalYear ?? dateTime.Year;
        if (string.Equals(Calendar, "buddhist", StringComparison.OrdinalIgnoreCase))
        {
            year = sourceYear + 543;
            return;
        }
        if (string.Equals(Calendar, "roc", StringComparison.OrdinalIgnoreCase))
        {
            year = sourceYear - 1911;
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

    private void AddLunisolarMonthPart(ChineseCalendarHelper.ChineseCalendarDate lunisolarDate, List<DateTimePart> result, ref bool hasDate, string separator)
    {
        if (result.Count > 0 && hasDate)
        {
            result.Add(new DateTimePart("literal", separator));
        }

        var chineseMonth = lunisolarDate.Month;
        var monthValue = Month switch
        {
            "numeric" => chineseMonth.ToString(CultureInfo),
            "2-digit" => chineseMonth.ToString("D2", CultureInfo),
            // A lunisolar month has a name — ICU writes "Twelfth Month" — and Jint ships none, so the
            // number stands unless a host answers for the calendar.
            "long" or "short" or "narrow" => CalendarMonthName(chineseMonth, Month) ?? chineseMonth.ToString(CultureInfo),
            _ => chineseMonth.ToString("D2", CultureInfo)
        };

        result.Add(new DateTimePart("month", monthValue));
        hasDate = true;
    }

    private void AddLunisolarDayPart(ChineseCalendarHelper.ChineseCalendarDate lunisolarDate, List<DateTimePart> result, ref bool hasDate, string separator)
    {
        if (result.Count > 0 && hasDate)
        {
            result.Add(new DateTimePart("literal", separator));
        }

        var chineseDay = lunisolarDate.Day;
        var dayValue = Day switch
        {
            "numeric" => chineseDay.ToString(CultureInfo),
            _ => chineseDay.ToString("D2", CultureInfo)
        };

        result.Add(new DateTimePart("day", dayValue));
        hasDate = true;
    }

    private void AddLunisolarYearPart(ChineseCalendarHelper.ChineseCalendarDate lunisolarDate, List<DateTimePart> result, ref bool hasDate, string separator, bool hasTextualMonth)
    {
        if (result.Count > 0 && hasDate)
        {
            // For textual month format, use ", " before year if it comes last
            var actualSeparator = hasTextualMonth ? ", " : separator;
            result.Add(new DateTimePart("literal", actualSeparator));
        }

        // Chinese and Dangi dates write relatedYear and yearName instead of year
        var relatedYear = lunisolarDate.RelatedYear;

        // Check locale for formatting - zh locale uses "年" suffix
        var lang = IntlUtilities.GetLanguageSubtag(Locale).ToLowerInvariant();
        var isChineseLocale = string.Equals(lang, "zh", StringComparison.Ordinal);

        var relatedYearValue = Year switch
        {
            "2-digit" => (relatedYear % 100).ToString("00", CultureInfo),
            _ => relatedYear.ToString(CultureInfo)
        };
        result.Add(new DateTimePart("relatedYear", relatedYearValue));

        // The yearName part: the sexagenary cycle name (干支)
        result.Add(new DateTimePart("yearName", lunisolarDate.YearName));

        if (isChineseLocale)
        {
            result.Add(new DateTimePart("literal", "年"));
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
    /// The pattern this formatter's <c>dateStyle</c> writes: the locale's own styled pattern, narrowed to
    /// the fields the value being formatted has.
    /// </summary>
    private List<PatternRun> GetDateStyleRuns()
    {
        var runs = GetLocaleDateStyleRuns();
        NarrowToDateStyleFields(runs);
        return runs;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-date-time-style-format - the pattern a <c>dateStyle</c> formats with comes
    /// from the locale's own data, which on .NET is that culture's long or short date pattern.
    /// </summary>
    private List<PatternRun> GetLocaleDateStyleRuns()
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
    /// https://tc39.es/proposal-temporal/#sec-adjustdatetimestyleformat - drops from a styled pattern every
    /// field the value being formatted does not have, along with the punctuation that was only there to set
    /// that field off from what remains.
    /// </summary>
    /// <remarks>
    /// ICU reaches the same answer by re-deriving a pattern for the narrowed skeleton, which is data this
    /// engine has no equivalent of. Taking the fields out of the pattern the style already resolved to keeps
    /// the locale's own field widths and order, which is what the operation inherits from its base format
    /// either way: English "dddd, MMMM d, yyyy" becomes "MMMM yyyy" for a year-month and "MMMM d" for a
    /// month-day, its "M/d/yy" becomes "M/yy" and "M/d", and Japanese "yyyy年M月d日" becomes "yyyy年M月".
    /// </remarks>
    private void NarrowToDateStyleFields(List<PatternRun> runs)
    {
        if (_dateStyleFields == DateStyleFields.All)
        {
            return;
        }

        // Neither type has a weekday, and the weekday is the one field whose punctuation is already known.
        var startedWithField = runs.Count > 0 && !runs[0].IsLiteral;
        RemoveWeekdayRun(runs);

        var i = 0;
        while (i < runs.Count)
        {
            if (runs[i].IsLiteral || IsAllowedDateStyleField(runs[i]))
            {
                i++;
                continue;
            }

            RemoveFieldRun(runs, i);
        }

        TrimEdgeLiterals(runs, startedWithField);
    }

    /// <summary>Whether a pattern field is one the value being formatted has.</summary>
    private bool IsAllowedDateStyleField(in PatternRun run) => run.Field switch
    {
        // A run of three or more 'd' is the weekday, which neither type has.
        'd' => run.Length <= 2 && _dateStyleFields == DateStyleFields.MonthDay,
        'M' => true,
        'y' or 'g' => _dateStyleFields == DateStyleFields.YearMonth,
        _ => false,
    };

    /// <summary>
    /// Removes one field run together with the literal that was only there because of it, which is the one
    /// on the side the field was joined to the rest of the pattern from.
    /// </summary>
    private static void RemoveFieldRun(List<PatternRun> runs, int index)
    {
        runs.RemoveAt(index);

        // Text on the right went with the field it was written against, whether it separated that field from
        // what follows or marked the field itself - Japanese keeps the month's 月 and loses the day's 日.
        if (index < runs.Count && runs[index].IsLiteral)
        {
            runs.RemoveAt(index);
            return;
        }

        if (index > 0 && runs[index - 1].IsLiteral)
        {
            // Nothing follows, so the text before the field either separated it from what remains, and goes,
            // or is a mark the field before it is written with, and stays.
            var literal = runs[index - 1].Literal!;
            if (IsFieldSeparator(literal))
            {
                runs.RemoveAt(index - 1);
            }
            else
            {
                ReplaceLiteralRun(runs, index - 1, literal.TrimEnd());
            }
        }
    }

    /// <summary>
    /// Whether a literal only stood between two fields, rather than being text one of them is written with.
    /// </summary>
    private static bool IsFieldSeparator(string literal)
    {
        foreach (var c in literal)
        {
            if (char.IsLetter(c))
            {
                // A word set off by a space - Portuguese "d 'de' MMMM" - separates; a mark written straight
                // against a field is part of how that field reads.
                return char.IsWhiteSpace(literal[0]);
            }
        }

        return true;
    }

    /// <summary>Drops the separators a narrowed pattern is left starting or ending with.</summary>
    /// <remarks>
    /// A pattern that began with a field and now begins with text is beginning with what set that field off,
    /// whatever it is made of - Thai writes its weekday and the day as one phrase - so the whole of it goes.
    /// Nothing similar holds at the other end, where the text after the last field is that field's own mark.
    /// </remarks>
    private static void TrimEdgeLiterals(List<PatternRun> runs, bool startedWithField)
    {
        if (runs.Count > 0 && runs[0].IsLiteral && (startedWithField || IsFieldSeparator(runs[0].Literal!)))
        {
            runs.RemoveAt(0);
        }

        var last = runs.Count - 1;
        if (last < 0 || !runs[last].IsLiteral)
        {
            return;
        }

        var literal = runs[last].Literal!;
        if (IsFieldSeparator(literal))
        {
            runs.RemoveAt(last);
        }
        else
        {
            ReplaceLiteralRun(runs, last, literal.TrimEnd());
        }
    }

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
                // A three- or four-letter run is the pattern's textual month, and the calendar's own name is
                // what belongs in it — the same name the month option's "short" and "long" write, so the two
                // lanes agree. https://tc39.es/ecma402/#sec-formatdatetime is the concatenation of the parts
                // https://tc39.es/ecma402/#sec-formatdatetimetoparts walks, and both go through here.
                value = run.Length >= 3
                    ? CalendarMonthName(calendarMonth.Value, run.Length >= 4 ? "long" : "short")
                    : null;
                value ??= FormatOverride(calendarMonth.Value, run.Length);
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
    /// Writes an overridden field value as a number. Only a two-letter run pads, because a calendar year is
    /// written as it is counted - Reiwa 8 is "8", not "0008".
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

    /// <summary>
    /// Computes the formatted hour value based on <see cref="ResolvedHourCycle"/>, the cycle
    /// <c>resolvedOptions()</c> reports: h11=0-11 (12hr), h12=1-12 (12hr), h23=0-23 (24hr), h24=1-24 (24hr).
    /// 24-hour formats always pad to 2 digits; 12-hour formats pad only for the "2-digit" option.
    /// </summary>
    /// <param name="hour">The 0-23 hour value</param>
    /// <param name="hourStr">Output: formatted hour string</param>
    /// <param name="use12Hour">Output: whether 12-hour format is used (needs AM/PM)</param>
    /// <param name="padByDefault">If true, always pad h23/h24 hours (used by style-based formatting)</param>
    private void ComputeHourValue(int hour, out string hourStr, out bool use12Hour, bool padByDefault = false)
    {
        int hourValue;
        var hourCycle = ResolvedHourCycle;

        if (string.Equals(hourCycle, "h11", StringComparison.Ordinal))
        {
            hourValue = hour % 12; // 0-11
            use12Hour = true;
        }
        else if (string.Equals(hourCycle, "h24", StringComparison.Ordinal))
        {
            hourValue = hour == 0 ? 24 : hour; // 1-24
            use12Hour = false;
        }
        else if (string.Equals(hourCycle, "h23", StringComparison.Ordinal))
        {
            hourValue = hour; // 0-23
            use12Hour = false;
        }
        else
        {
            hourValue = hour % 12 == 0 ? 12 : hour % 12; // h12: 1-12
            use12Hour = true;
        }

        // Per ECMA-402: 24-hour formats (h23, h24) always pad to 2 digits.
        // 12-hour formats only pad when Hour option is "2-digit".
        var pad = !use12Hour || string.Equals(Hour, "2-digit", StringComparison.Ordinal);
        hourStr = pad ? hourValue.ToString("D2", CultureInfo.InvariantCulture) : hourValue.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the formatted parts with their types for formatToParts.
    /// </summary>
    /// <param name="dateTime">The .NET DateTime to format</param>
    /// <param name="originalYear">Optional original JavaScript year (for dates outside .NET DateTime range)</param>
    /// <param name="isPlain">If true, skip timezone conversion (for plain Temporal types)</param>
    internal List<DateTimePart> FormatToParts(DateTime dateTime, int? originalYear = null, bool isPlain = false)
    {
        // For plain Temporal types (isPlain=true), skip timezone conversion
        if (!isPlain)
        {
            dateTime = ToFormatterTimeZone(dateTime, ref originalYear);
        }

        return FormatLocalToParts(dateTime, originalYear, isPlain);
    }

    /// <summary>
    /// The wall-clock time of <paramref name="dateTime"/> in this formatter's time zone, or the engine's default one.
    /// </summary>
    private DateTime ToFormatterTimeZone(DateTime dateTime, ref int? originalYear)
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

        return dateTime;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatdatetimetoparts for a wall-clock time already in the formatter's time zone.
    /// </summary>
    private List<DateTimePart> FormatLocalToParts(DateTime dateTime, int? originalYear, bool isPlain)
    {
        List<DateTimePart> result;
        if (DateStyle != null || TimeStyle != null)
        {
            // For style-based formatting, use a simpler approach
            result = new List<DateTimePart>();
            FormatStyleToParts(dateTime, result, originalYear, isPlain);
        }
        else if (IsLunisolarCalendar)
        {
            result = new List<DateTimePart>();
            FormatLunisolarComponentsToParts(dateTime, result);
        }
        else
        {
            // One part per run of the pattern, exactly.
            result = new List<DateTimePart>(GetComponentPattern().Runs.Length);
            FormatComponentPatternToParts(dateTime, result, originalYear);
        }

        FinishParts(result);
        return result;
    }

    /// <summary>
    /// Writes [[NumberingSystem]]'s digits over every field, and nothing else: https://tc39.es/ecma402/#sec-formatdatetimepattern
    /// copies a "literal" through untouched, and the numbering system reaches a field's value only, through the
    /// FormatNumeric calls. The one separator this formatter writes itself, before a fractional second, is
    /// already the numbering system's. U+202F becomes a plain space in every part of every lane, so that
    /// format(), which is the concatenation of these parts, and formatToParts() never disagree.
    /// </summary>
    private void FinishParts(List<DateTimePart> result)
    {
        var rewritesDigits = _numberingSystem.RewritesDigits;
        for (var i = 0; i < result.Count; i++)
        {
            var part = result[i];
            var value = DateTimeFormatPattern.NormalizeSpaces(part.Value);
            if (rewritesDigits && !string.Equals(part.Type, "literal", StringComparison.Ordinal))
            {
                value = _numberingSystem.TransliterateDigitsOnly(value);
            }

            if (!ReferenceEquals(value, part.Value))
            {
                result[i] = new DateTimePart(part.Type, value);
            }
        }
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
            if (isFull && _dateStyleFields == DateStyleFields.All)
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

        // Day - a year-month has none, and writing one fills it from the reference value.
        if (_dateStyleFields != DateStyleFields.YearMonth)
        {
            result.Add(new DateTimePart("literal", "/"));
            result.Add(new DateTimePart("day", date.Day.ToString(CultureInfo)));
        }

        // Year - a month-day has none, for the same reason.
        if (_dateStyleFields == DateStyleFields.MonthDay)
        {
            return;
        }

        result.Add(new DateTimePart("literal", "/"));

        // Use relatedYear and yearName for lunisolar calendars
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
    /// Gets the formatted timezone name in the given style: the <c>timeZoneName</c> the format record holds.
    /// </summary>
    private string GetFormattedTimeZoneName(DateTime dateTime, string? style)
    {
        if (string.Equals(style, "long", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: true, generic: false);
        }
        if (string.Equals(style, "longGeneric", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: true, generic: true);
        }
        if (string.Equals(style, "short", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: false, generic: false);
        }
        if (string.Equals(style, "shortGeneric", StringComparison.Ordinal))
        {
            return GetTimeZoneDisplayName(dateTime, longName: false, generic: true);
        }
        if (string.Equals(style, "longOffset", StringComparison.Ordinal))
        {
            return "GMT" + dateTime.ToString("zzz", CultureInfo);
        }
        if (string.Equals(style, "shortOffset", StringComparison.Ordinal))
        {
            return "GMT" + dateTime.ToString("zzz", CultureInfo);
        }
        return GetTimeZoneDisplayName(dateTime, longName: false, generic: false);
    }

    /// <summary>
    /// Whether this formatter writes a Chinese or Dangi date, which has a related year and a year name where every
    /// other calendar has a year, and keeps the lane it had before the format matcher.
    /// </summary>
    private bool IsLunisolarCalendar => string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(Calendar, "dangi", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this formatter writes through the pattern <see cref="GetComponentPattern"/> resolves: a component bag
    /// on any calendar but the lunisolar ones.
    /// </summary>
    internal bool UsesComponentPattern => DateStyle is null && TimeStyle is null && !IsLunisolarCalendar;

    /// <summary>
    /// The format record of https://tc39.es/ecma402/#sec-createdatetimeformat for a component bag: the pattern the
    /// format matcher chose for the requested fields in this locale, resolved the first time it is needed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The matcher is <see cref="DateTimePatternGenerator"/>, over the locale's CLDR Gregorian data whatever the
    /// calendar: the other calendars' own patterns are not embedded, and a calendar that counts the Gregorian months
    /// writes them in the Gregorian shape. <c>formatMatcher</c> is read by the constructor and makes no difference;
    /// see <see cref="DateTimePatternGenerator"/> for why <c>"basic"</c> is answered by the best-fit matcher.
    /// </para>
    /// <para>
    /// The hour letter comes from <see cref="ResolvedHourCycle"/>, so the pattern and <c>resolvedOptions()</c> agree
    /// on the cycle; a bag without an hour never looks the locale's cycle up.
    /// </para>
    /// </remarks>
    internal DateTimeFormatPattern GetComponentPattern()
    {
        if (_componentPattern is not null)
        {
            return _componentPattern;
        }

        var hourCycle = Hour is null ? "h23" : ResolvedHourCycle;
        var skeleton = DateTimePatternGenerator.Skeleton.FromOptions(
            Weekday, Era, Year, Month, Day, DayPeriod, Hour, Minute, Second, FractionalSecondDigits, TimeZoneName, hourCycle);
        return _componentPattern = DateTimePatternGenerator.ForLocale(Locale).GetPattern(skeleton, hourCycle, _numberingSystem.DecimalSeparator);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-formatdatetimepattern for a component bag: each run of the chosen pattern, a
    /// literal as the pattern writes it and a field as the locale writes that field's value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The values are this formatter's calendar's (<see cref="ResolveCalendarFieldsForFormatting"/>) and the real year
    /// where the <see cref="DateTime"/> stands on a representative one. A year of zero or less is written as
    /// <c>1 - year</c>, which is step 15.f.ii of the operation and what an era beside it needs.
    /// </para>
    /// <para>
    /// The names — months in the format (<c>M</c>) and stand-alone (<c>L</c>) contexts, weekdays (<c>E</c>, <c>c</c>),
    /// the Gregorian eras and am/pm — are the locale's CLDR names, unless a host <see cref="ICldrProvider"/> answers
    /// differently from <see cref="DefaultCldrProvider.Instance"/> (<see cref="GetHostNames"/>). A calendar counting
    /// months of its own writes its month through <see cref="CalendarMonthName"/> and its era through
    /// <see cref="GetEraName"/>, as before.
    /// </para>
    /// </remarks>
    private void FormatComponentPatternToParts(DateTime dateTime, List<DateTimePart> result, int? originalYear)
    {
        var runs = GetComponentPattern().Runs;
        ResolveCalendarFieldsForFormatting(dateTime, originalYear, out var calendarYear, out var calendarMonth, out var calendarDay);
        var hostNames = GetHostNames(runs);
        for (var i = 0; i < runs.Length; i++)
        {
            AppendRun(result, in runs[i], hostNames?[i], dateTime, originalYear, calendarYear, calendarMonth, calendarDay);
        }
    }

    /// <summary>
    /// One run of a pattern (https://tc39.es/ecma402/#sec-formatdatetimepattern step 15): a literal as the pattern
    /// writes it, a field as the locale writes its value, with <paramref name="hostNames"/> in place of the run's CLDR
    /// names where a host provider has its own.
    /// </summary>
    private void AppendRun(
        List<DateTimePart> result,
        in DateTimePatternRun run,
        string[]? hostNames,
        DateTime dateTime,
        int? originalYear,
        int? calendarYear,
        int? calendarMonth,
        int? calendarDay)
    {
        if (run.IsLiteral)
        {
            result.Add(new DateTimePart("literal", run.Literal!));
            return;
        }

        var names = hostNames ?? run.Names;
        var length = run.Length;
        switch (run.Field)
        {
            case 'G':
                result.Add(new DateTimePart("era", FormatEra(dateTime, originalYear, length, names)));
                break;
            case 'y' or 'Y' or 'u' or 'r':
                var year = calendarYear ?? originalYear ?? dateTime.Year;
                if (year <= 0)
                {
                    year = 1 - year;
                }

                result.Add(new DateTimePart("year", length == 2 ? FormatTwoDigits(year % 100) : FormatPadded(year, length)));
                break;
            case 'M' or 'L':
                result.Add(new DateTimePart("month", FormatMonth(dateTime.Month, calendarMonth, length, names)));
                break;
            case 'd':
                result.Add(new DateTimePart("day", FormatPadded(calendarDay ?? dateTime.Day, length)));
                break;
            case 'E' or 'c' or 'e':
                result.Add(new DateTimePart("weekday", names![(int) dateTime.DayOfWeek]));
                break;
            case 'a' or 'b':
                result.Add(new DateTimePart("dayPeriod", names![dateTime.Hour < 12 ? 0 : 1]));
                break;
            case 'B':
                result.Add(new DateTimePart("dayPeriod", GetExtendedDayPeriod(dateTime.Hour, TextualStyle(length))));
                break;
            case 'h':
                result.Add(new DateTimePart("hour", FormatPadded(dateTime.Hour % 12 == 0 ? 12 : dateTime.Hour % 12, length)));
                break;
            case 'K':
                result.Add(new DateTimePart("hour", FormatPadded(dateTime.Hour % 12, length)));
                break;
            case 'H':
                result.Add(new DateTimePart("hour", FormatPadded(dateTime.Hour, length)));
                break;
            case 'k':
                result.Add(new DateTimePart("hour", FormatPadded(dateTime.Hour == 0 ? 24 : dateTime.Hour, length)));
                break;
            case 'm':
                result.Add(new DateTimePart("minute", FormatPadded(dateTime.Minute, length)));
                break;
            case 's':
                result.Add(new DateTimePart("second", FormatPadded(dateTime.Second, length)));
                break;
            case 'S':
                // floor(ms × 10^(digits - 3)), step 15.b; the fraction's own digits, zero-padded to its length.
                var fraction = length switch
                {
                    1 => dateTime.Millisecond / 100,
                    2 => dateTime.Millisecond / 10,
                    _ => dateTime.Millisecond,
                };
                result.Add(new DateTimePart("fractionalSecond", FormatPadded(fraction, System.Math.Min(length, 3))));
                break;
            case 'z' or 'Z' or 'O' or 'v' or 'V' or 'X' or 'x':
                result.Add(new DateTimePart("timeZoneName", GetFormattedTimeZoneName(dateTime, DateTimeFormatPattern.TimeZoneNameStyle(run.Field, length))));
                break;
            default:
                // A letter ECMA-402 has no part for; CLDR's Gregorian patterns write none.
                result.Add(new DateTimePart("unknown", new string(run.Field, length)));
                break;
        }
    }

    /// <summary>
    /// A month: its number, or for a text width its name — the calendar's own where it counts months of its own
    /// (the number where it has none), and otherwise the Gregorian name <paramref name="names"/> holds.
    /// </summary>
    private string FormatMonth(int gregorianMonth, int? calendarMonth, int length, string[]? names)
    {
        if (calendarMonth.HasValue)
        {
            var month = calendarMonth.Value;
            if (length >= 3)
            {
                return CalendarMonthName(month, TextualStyle(length)) ?? month.ToString(CultureInfo.InvariantCulture);
            }

            return FormatPadded(month, length);
        }

        return length >= 3 ? names![gregorianMonth - 1] : FormatPadded(gregorianMonth, length);
    }

    /// <summary>
    /// The era: CLDR's Gregorian era for <c>gregory</c> and <c>iso8601</c>, before the common era for a year of zero
    /// or less, and <see cref="GetEraName"/> for the other calendars, which have eras of their own.
    /// </summary>
    private string FormatEra(DateTime dateTime, int? originalYear, int length, string[]? names)
    {
        var calendar = Calendar ?? "gregory";
        if (string.Equals(calendar, "gregory", StringComparison.OrdinalIgnoreCase)
            || string.Equals(calendar, "iso8601", StringComparison.OrdinalIgnoreCase))
        {
            return names![(originalYear ?? dateTime.Year) <= 0 ? 0 : 1];
        }

        return GetEraName(dateTime, calendar, TextualStyle(length), originalYear) ?? "";
    }

    /// <summary>The ECMA-402 style a text field's pattern length stands for.</summary>
    private static string TextualStyle(int length) => length switch
    {
        4 => "long",
        5 => "narrow",
        _ => "short",
    };

    /// <summary>
    /// A number with at least <paramref name="length"/> digits, in ASCII: the numbering system's digits are written
    /// over every field afterwards.
    /// </summary>
    private static string FormatPadded(int value, int length) => length switch
    {
        <= 1 => value.ToString(CultureInfo.InvariantCulture),
        2 => value.ToString("D2", CultureInfo.InvariantCulture),
        3 => value.ToString("D3", CultureInfo.InvariantCulture),
        _ => value.ToString("D" + length.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
    };

    private static string FormatTwoDigits(int value) => value.ToString("D2", CultureInfo.InvariantCulture);

    /// <summary>
    /// The names a host <see cref="ICldrProvider"/> puts in place of the CLDR ones, per run of
    /// <paramref name="runs"/>, or null when the provider is the shipped one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A provider's answer is a host override only where it differs from what <see cref="DefaultCldrProvider.Instance"/>
    /// answers for the same arguments, and an override is written in both the format and the stand-alone context.
    /// That is what lets a provider that derives from the default and overrides something else — or implements the
    /// interface and delegates the names, as the test262 harness's does — leave the CLDR names alone: its answer is the
    /// default's, which reads .NET's culture data and has no format-context names at all.
    /// </para>
    /// <para>
    /// Asked once per formatter, for the widths its pattern writes (the pattern of a formatter never changes), and
    /// once for each range pattern it writes a range with.
    /// </para>
    /// </remarks>
    private string[]?[]? GetHostNames(DateTimePatternRun[] runs)
    {
        var provider = CldrProvider;
        if (ReferenceEquals(provider, DefaultCldrProvider.Instance))
        {
            return null;
        }

        if (ReferenceEquals(_hostNamesRuns, runs))
        {
            return _hostNames;
        }

        if (_rangeHostNames is not null)
        {
            foreach (var (rangeRuns, names) in _rangeHostNames)
            {
                if (ReferenceEquals(rangeRuns, runs))
                {
                    return names;
                }
            }
        }

        var shipped = DefaultCldrProvider.Instance;
        string[]?[]? overrides = null;
        for (var i = 0; i < runs.Length; i++)
        {
            var run = runs[i];
            if (run.Names is null)
            {
                continue;
            }

            string[]? host = null;
            var style = TextualStyle(run.Length);
            switch (run.Field)
            {
                case 'M' or 'L' when AvailableCalendars.UsesGregorianMonths(Calendar):
                    host = OverrideOf(provider.GetMonthNames(Locale, style, Calendar), shipped.GetMonthNames(Locale, style, Calendar), 12);
                    break;
                case 'E' or 'c' or 'e' when run.Length <= 5:
                    host = OverrideOf(provider.GetWeekdayNames(Locale, style), shipped.GetWeekdayNames(Locale, style), 7);
                    break;
                case 'G':
                    host = OverrideOf(provider.GetEraNames(Locale, style, Calendar), shipped.GetEraNames(Locale, style, Calendar), 2);
                    break;
                case 'a' or 'b':
                    host = OverrideOf(provider.GetDayPeriods(Locale, style, Calendar), shipped.GetDayPeriods(Locale, style, Calendar), 2);
                    break;
            }

            if (host is not null)
            {
                overrides ??= new string[]?[runs.Length];
                overrides[i] = host;
            }
        }

        if (_hostNamesRuns is null)
        {
            _hostNamesRuns = runs;
            _hostNames = overrides;
        }
        else
        {
            (_rangeHostNames ??= []).Add((runs, overrides));
        }

        return overrides;
    }

    /// <summary>
    /// A host's names when there are enough of them and they differ from the shipped provider's, and null otherwise.
    /// </summary>
    private static string[]? OverrideOf(string[]? host, string[]? shipped, int length)
    {
        if (host is null || host.Length < length)
        {
            return null;
        }

        if (shipped is not null && shipped.Length >= length)
        {
            var same = true;
            for (var i = 0; i < length; i++)
            {
                if (!string.Equals(host[i], shipped[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return null;
            }
        }

        var names = new string[length];
        for (var i = 0; i < length; i++)
        {
            names[i] = DateTimeFormatPattern.NormalizeSpaces(host[i]);
        }

        return names;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-partitiondatetimerangepattern for a component bag: the parts of a range and the
    /// source of each, from the range pattern of the largest calendar field in which the two dates differ.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The range patterns are <see cref="DateTimeIntervalFormat"/>'s, built from CLDR's intervalFormats the way ICU's
    /// DateIntervalFormat builds them. The fields are compared in the order of the DateTime Range Pattern Record
    /// (https://tc39.es/ecma402/#sec-datetimeformat-range-pattern-record): era, year, month and day in this formatter's
    /// calendar, then am/pm, hour, minute, second, and the fractional second at the digits the format writes. The day
    /// period that record lists after am/pm is read as the am/pm, as ICU reads it, so it never differs on its own.
    /// </para>
    /// <para>
    /// When no field differs, or the first that does is one the format does not show (a <c>{ month, day }</c> bag and
    /// two times on one day), the range is the start date alone, every part <c>shared</c>: the step that stops checking
    /// fields once no range pattern covers them. Otherwise a part is written from the start date if its source is
    /// <c>startRange</c> or <c>shared</c> and from the end date if it is <c>endRange</c>, and the literal text between
    /// two fields is one part.
    /// </para>
    /// </remarks>
    internal List<DateTimeRangePart> FormatRangeToParts(DateTime start, int? startYear, DateTime end, int? endYear, bool isPlain = false)
    {
        if (!isPlain)
        {
            start = ToFormatterTimeZone(start, ref startYear);
            end = ToFormatterTimeZone(end, ref endYear);
        }

        ResolveCalendarFieldsForFormatting(start, startYear, out var startCalendarYear, out var startCalendarMonth, out var startCalendarDay);
        ResolveCalendarFieldsForFormatting(end, endYear, out var endCalendarYear, out var endCalendarMonth, out var endCalendarDay);

        var field = -1;
        if (!string.Equals(EraKey(start, startYear), EraKey(end, endYear), StringComparison.Ordinal))
        {
            field = DateTimeIntervalFormat.Era;
        }
        else if ((startCalendarYear ?? startYear ?? start.Year) != (endCalendarYear ?? endYear ?? end.Year))
        {
            field = DateTimeIntervalFormat.Year;
        }
        else if ((startCalendarMonth ?? start.Month) != (endCalendarMonth ?? end.Month))
        {
            field = DateTimeIntervalFormat.Month;
        }
        else if ((startCalendarDay ?? start.Day) != (endCalendarDay ?? end.Day))
        {
            field = DateTimeIntervalFormat.Day;
        }
        else if (start.Hour < 12 != end.Hour < 12)
        {
            field = DateTimeIntervalFormat.AmPm;
        }
        else if (start.Hour != end.Hour)
        {
            field = DateTimeIntervalFormat.Hour;
        }
        else if (start.Minute != end.Minute)
        {
            field = DateTimeIntervalFormat.Minute;
        }
        else if (start.Second != end.Second)
        {
            field = DateTimeIntervalFormat.Second;
        }
        else
        {
            // floor(ms × 10^(fractionalSecondDigits - 3)), fractionalSecondDigits being 3 when the format writes none.
            var scale = GetComponentPattern().FractionalSecondDigits switch
            {
                1 => 100,
                2 => 10,
                _ => 1,
            };

            if (start.Millisecond / scale != end.Millisecond / scale)
            {
                field = DateTimeIntervalFormat.FractionalSecond;
            }
        }

        var range = field < 0 ? null : GetIntervalFormat().GetRangePattern(field);
        List<DateTimeRangePart> result;
        if (range is null)
        {
            var single = FormatLocalToParts(start, startYear, isPlain);
            result = new List<DateTimeRangePart>(single.Count);
            foreach (var part in single)
            {
                result.Add(new DateTimeRangePart(part.Type, part.Value, "shared"));
            }

            return result;
        }

        var runs = range.Runs;
        var sources = range.Sources;
        var hostNames = GetHostNames(runs);
        var parts = new List<DateTimePart>(runs.Length);
        for (var i = 0; i < runs.Length; i++)
        {
            if (sources[i] == DateTimeRangeSource.EndRange)
            {
                AppendRun(parts, in runs[i], hostNames?[i], end, endYear, endCalendarYear, endCalendarMonth, endCalendarDay);
            }
            else
            {
                AppendRun(parts, in runs[i], hostNames?[i], start, startYear, startCalendarYear, startCalendarMonth, startCalendarDay);
            }
        }

        FinishParts(parts);

        result = new List<DateTimeRangePart>(parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var source = sources[i] switch
            {
                DateTimeRangeSource.StartRange => "startRange",
                DateTimeRangeSource.EndRange => "endRange",
                _ => "shared",
            };

            var part = parts[i];
            var last = result.Count - 1;
            if (last >= 0
                && string.Equals(part.Type, "literal", StringComparison.Ordinal)
                && string.Equals(result[last].Type, "literal", StringComparison.Ordinal)
                && string.Equals(result[last].Source, source, StringComparison.Ordinal))
            {
                result[last] = new DateTimeRangePart("literal", result[last].Value + part.Value, source);
            }
            else
            {
                result.Add(new DateTimeRangePart(part.Type, part.Value, source));
            }
        }

        return result;
    }

    /// <summary>
    /// The range patterns of the component bag's format record, built for this locale, pattern and hour cycle once.
    /// </summary>
    private DateTimeIntervalFormat GetIntervalFormat()
    {
        return _intervalFormat ??= DateTimePatternGenerator.ForLocale(Locale).GetIntervalFormat(
            GetComponentPattern(),
            Hour is null ? "h23" : ResolvedHourCycle,
            _numberingSystem.DecimalSeparator);
    }

    /// <summary>
    /// What tells two dates' eras apart: before or in the common era for <c>gregory</c> and <c>iso8601</c>, and the
    /// era's name for a calendar with eras of its own.
    /// </summary>
    private string? EraKey(DateTime dateTime, int? originalYear)
    {
        var calendar = Calendar ?? "gregory";
        if (string.Equals(calendar, "gregory", StringComparison.OrdinalIgnoreCase)
            || string.Equals(calendar, "iso8601", StringComparison.OrdinalIgnoreCase))
        {
            return (originalYear ?? dateTime.Year) <= 0 ? "BC" : "AD";
        }

        return GetEraName(dateTime, calendar, "short", originalYear);
    }

    /// <summary>
    /// A Chinese or Dangi date's component bag, which writes a related year and a year name where a pattern has a
    /// year and so is not written through a CLDR pattern: the fields in the order the locale's .NET short date
    /// pattern puts them, and the time after them. Unchanged by the format matcher.
    /// </summary>
    private void FormatLunisolarComponentsToParts(DateTime dateTime, List<DateTimePart> result)
    {
        var hasDate = false;
        var hasTime = false;

        var lunisolarDate = string.Equals(Calendar, "chinese", StringComparison.OrdinalIgnoreCase)
            ? ChineseCalendarHelper.GetChineseDate(dateTime)
            : ChineseCalendarHelper.GetDangiDate(dateTime);

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
                _ => "ddd"
            };
            result.Add(new DateTimePart("weekday", dateTime.ToString(format, CultureInfo)));
            hasDate = true;
        }

        // Add date components in locale-specific order. A lunisolar calendar has no era.
        foreach (var component in dateOrder)
        {
            switch (component)
            {
                case 'M' when Month != null:
                    AddLunisolarMonthPart(lunisolarDate, result, ref hasDate, dateSeparator);
                    break;
                case 'd' when Day != null:
                    AddLunisolarDayPart(lunisolarDate, result, ref hasDate, dateSeparator);
                    break;
                case 'y' when Year != null:
                    AddLunisolarYearPart(lunisolarDate, result, ref hasDate, dateSeparator, hasTextualMonth);
                    break;
            }
        }

        // Hour - use pre-computed value to handle all hour cycles (h11/h12/h23/h24)
        var hourUse12Hour = false;
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
            result.Add(new DateTimePart("second", dateTime.Second.ToString("D2", CultureInfo)));
            hasTime = true;
        }

        // Fractional seconds
        if (FractionalSecondDigits.HasValue && FractionalSecondDigits.Value > 0)
        {
            // Use the decimal separator for the numbering system (e.g., ٫ for Arabic)
            result.Add(new DateTimePart("literal", _numberingSystem.DecimalSeparator.ToString()));
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
            result.Add(new DateTimePart("dayPeriod", GetExtendedDayPeriod(dateTime.Hour, DayPeriod)));
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
            result.Add(new DateTimePart("timeZoneName", GetFormattedTimeZoneName(dateTime, TimeZoneName)));
        }

        // If no parts were added, use default format
        if (result.Count == 0)
        {
            result.Add(new DateTimePart("literal", dateTime.ToString("G", CultureInfo)));
        }
    }

    /// <summary>
    /// Gets the extended day period string based on the hour and dayPeriod style.
    /// CLDR defines: night1 (21:00-05:59), morning1 (06:00-11:59), noon (12:00),
    /// afternoon1 (12:01-17:59), evening1 (18:00-20:59)
    /// </summary>
    private string GetExtendedDayPeriod(int hour, string? style)
    {
        // For English locale (en), use CLDR day period names
        // Other locales would need locale-specific data
        var lang = IntlUtilities.GetLanguageSubtag(Locale);

        if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase))
        {
            return style switch
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

    /// <summary>A part of a range, with the <c>source</c> <c>formatRangeToParts</c> reports.</summary>
    internal readonly record struct DateTimeRangePart(string Type, string Value, string Source);
}

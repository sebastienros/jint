using System.Numerics;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Native.Temporal;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-datetimeformat-prototype-object
/// </summary>
internal sealed class DateTimeFormatPrototype : Prototype
{
    private readonly DateTimeFormatConstructor _constructor;

    public DateTimeFormatPrototype(Engine engine,
        Realm realm,
        DateTimeFormatConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
            ["formatToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatToParts", FormatToParts, 1, LengthFlags), PropertyFlags),
            ["formatRange"] = new PropertyDescriptor(new ClrFunction(Engine, "formatRange", FormatRange, 2, LengthFlags), PropertyFlags),
            ["formatRangeToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatRangeToParts", FormatRangeToParts, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        // format is an accessor property - accessor properties don't have writable attribute
        SetAccessor("format", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get format", GetFormat, 0, LengthFlags),
            Undefined,
            PropertyFlag.Configurable));

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.DateTimeFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void SetAccessor(string name, GetSetPropertyDescriptor descriptor)
    {
        SetProperty(name, descriptor);
    }

    private JsDateTimeFormat ValidateDateTimeFormat(JsValue thisObject)
    {
        if (thisObject is JsDateTimeFormat dateTimeFormat)
        {
            return dateTimeFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.DateTimeFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.format
    /// </summary>
    private ClrFunction GetFormat(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        // Return a bound format function
        return new ClrFunction(Engine, "", (_, args) =>
        {
            var dateValue = args.At(0);
            var formattable = ToDateTimeFormattable(dateValue);

            if (IsTemporalObject(formattable))
            {
                var temporalDtf = GetTemporalFormatDtf(dateTimeFormat, formattable);
                var dateTime = ConvertTemporalToDateTime(formattable);
                var isPlain = formattable is not JsInstant;
                return temporalDtf.Format(dateTime, isPlain: isPlain);
            }
            else
            {
                var dateTime = HandleDateTimeNonTemporal(dateTimeFormat, formattable, out var originalYear);
                return dateTimeFormat.Format(dateTime, originalYear);
            }
        }, 1, PropertyFlag.Configurable);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.formattoparts
    /// </summary>
    private JsArray FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);
        var dateValue = arguments.At(0);
        var formattable = ToDateTimeFormattable(dateValue);

        List<JsDateTimeFormat.DateTimePart> parts;
        if (IsTemporalObject(formattable))
        {
            var temporalDtf = GetTemporalFormatDtf(dateTimeFormat, formattable);
            var dateTime = ConvertTemporalToDateTime(formattable);
            var isPlain = formattable is not JsInstant;
            parts = temporalDtf.FormatToParts(dateTime, isPlain: isPlain);
        }
        else
        {
            var dateTime = HandleDateTimeNonTemporal(dateTimeFormat, formattable, out var originalYear);
            parts = dateTimeFormat.FormatToParts(dateTime, originalYear);
        }
        var result = new JsArray(Engine, (uint) parts.Count);

        for (var i = 0; i < parts.Count; i++)
        {
            var partObj = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            partObj.Set("type", parts[i].Type);
            partObj.Set("value", parts[i].Value);
            result.SetIndexValue((uint) i, partObj, updateLength: true);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-todatetimeformattable
    /// Returns the Temporal object as-is if it is a Temporal object, otherwise converts to Number.
    /// </summary>
    private static JsValue ToDateTimeFormattable(JsValue value)
    {
        if (value.IsUndefined())
        {
            return value;
        }

        if (IsTemporalObject(value))
        {
            return value;
        }

        if (value is JsDate)
        {
            return value;
        }

        return JsNumber.Create(TypeConverter.ToNumber(value));
    }

    /// <summary>
    /// Returns true if the value is any Temporal type.
    /// </summary>
    private static bool IsTemporalObject(JsValue value)
    {
        return value is JsPlainDate or JsPlainDateTime or JsPlainTime
            or JsPlainYearMonth or JsPlainMonthDay
            or JsInstant or JsZonedDateTime or JsDuration;
    }

    /// <summary>
    /// Validates that range arguments are of the same Temporal type.
    /// Per spec: If IsTemporalObject(x) or IsTemporalObject(y), then SameTemporalType(x, y) must be true.
    /// </summary>
    private void ValidateTemporalRangeTypes(JsValue x, JsValue y)
    {
        var xIsTemporal = IsTemporalObject(x);
        var yIsTemporal = IsTemporalObject(y);

        if (!xIsTemporal && !yIsTemporal)
        {
            return;
        }

        // One is temporal and the other isn't, or they are different types
        if (!xIsTemporal || !yIsTemporal || x.GetType() != y.GetType())
        {
            Throw.TypeError(_realm, "Both arguments must be the same Temporal type");
        }
    }

    /// <summary>
    /// Handles a non-temporal formattable value (Date, Number, undefined).
    /// </summary>
    private DateTime HandleDateTimeNonTemporal(JsDateTimeFormat dateTimeFormat, JsValue formattable, out int? originalYear)
    {
        originalYear = null;
        return ToDateTimeWithOriginalYear(formattable, out originalYear);
    }

    /// <summary>
    /// Per spec HandleDateTimeValue + GetDateTimeFormat: computes the per-type format DTF
    /// for a Temporal object. Each Temporal type uses a specific format record with its own
    /// required/defaults, which may differ from the main DateTimeFormat record.
    /// </summary>
    private JsDateTimeFormat GetTemporalFormatDtf(JsDateTimeFormat dtf, JsValue temporal)
    {
        if (temporal is JsZonedDateTime)
        {
            Throw.TypeError(_realm, "Temporal.ZonedDateTime is not supported in DateTimeFormat.format(). Use toLocaleString() instead.");
            return null!;
        }

        // Plain types should not show timeZoneName
        var isPlain = temporal is not JsInstant;

        // Determine per-type required and defaults
        DateTimeRequired required;
        DateTimeDefaults defaults;
        switch (temporal)
        {
            case JsPlainDate:
                required = DateTimeRequired.Date;
                defaults = DateTimeDefaults.Date;
                break;
            case JsPlainDateTime:
                required = DateTimeRequired.Any;
                defaults = DateTimeDefaults.All;
                break;
            case JsPlainTime:
                required = DateTimeRequired.Time;
                defaults = DateTimeDefaults.Time;
                break;
            case JsPlainYearMonth:
                required = DateTimeRequired.YearMonth;
                defaults = DateTimeDefaults.YearMonth;
                break;
            case JsPlainMonthDay:
                required = DateTimeRequired.MonthDay;
                defaults = DateTimeDefaults.MonthDay;
                break;
            case JsInstant:
                required = DateTimeRequired.Any;
                defaults = DateTimeDefaults.All;
                break;
            default:
                Throw.TypeError(_realm, $"Unsupported Temporal type: {temporal.GetType().Name}");
                return null!;
        }

        // If the DTF uses dateStyle/timeStyle, validate restrictions and adjust for temporal type
        if (dtf.DateStyle != null || dtf.TimeStyle != null)
        {
            if ((required is DateTimeRequired.Date or DateTimeRequired.YearMonth or DateTimeRequired.MonthDay) && dtf.TimeStyle != null && dtf.DateStyle == null)
            {
                Throw.TypeError(_realm, "Cannot format a date-only Temporal type with timeStyle and no dateStyle");
            }
            if (required == DateTimeRequired.Time && dtf.DateStyle != null && dtf.TimeStyle == null)
            {
                Throw.TypeError(_realm, "Cannot format a time-only Temporal type with dateStyle and no timeStyle");
            }

            // For date-only types: strip timeStyle; for time-only types: strip dateStyle
            var effectiveDateStyle = dtf.DateStyle;
            var effectiveTimeStyle = dtf.TimeStyle;
            if (required is DateTimeRequired.Date or DateTimeRequired.YearMonth or DateTimeRequired.MonthDay)
            {
                effectiveTimeStyle = null;
            }
            else if (required == DateTimeRequired.Time)
            {
                effectiveDateStyle = null;
            }

            // For PlainYearMonth/PlainMonthDay, convert dateStyle to component-based formatting
            // because these types need specific fields omitted (day for YearMonth, year for MonthDay)
            if (effectiveDateStyle != null && required is DateTimeRequired.YearMonth or DateTimeRequired.MonthDay)
            {
                var isShort = string.Equals(effectiveDateStyle, "short", StringComparison.Ordinal);
                var isMedium = string.Equals(effectiveDateStyle, "medium", StringComparison.Ordinal);
                string? year = null, month = null, day = null;

                if (required == DateTimeRequired.YearMonth)
                {
                    year = "numeric";
                    month = isShort ? "numeric" : isMedium ? "short" : "long";
                }
                else // MonthDay
                {
                    month = isShort ? "numeric" : isMedium ? "short" : "long";
                    day = "numeric";
                }

                return new JsDateTimeFormat(
                    _engine, dtf._prototype!, dtf.Locale, dtf.Calendar, dtf.NumberingSystem,
                    dtf.TimeZone, dtf.HourCycle, null, null,
                    null, null, year, month, day, null, null, null, null, null,
                    null, false, dtf.DateTimeFormatInfo, dtf.CultureInfo);
            }

            // Return modified DTF if styles were stripped, otherwise return as-is
            if (!string.Equals(effectiveDateStyle, dtf.DateStyle, StringComparison.Ordinal)
                || !string.Equals(effectiveTimeStyle, dtf.TimeStyle, StringComparison.Ordinal))
            {
                return new JsDateTimeFormat(
                    _engine, dtf._prototype!, dtf.Locale, dtf.Calendar, dtf.NumberingSystem,
                    dtf.TimeZone, dtf.HourCycle, effectiveDateStyle, effectiveTimeStyle,
                    dtf.Weekday, dtf.Era, dtf.Year, dtf.Month, dtf.Day, dtf.DayPeriod,
                    dtf.Hour, dtf.Minute, dtf.Second, dtf.FractionalSecondDigits,
                    dtf.TimeZoneName, dtf.HasExplicitFormatComponents,
                    dtf.DateTimeFormatInfo, dtf.CultureInfo);
            }
            return dtf;
        }

        // Create per-type format DTF by filtering components relevant to the temporal type.
        // This implements GetDateTimeFormat with inherit=~relevant~.
        return CreatePerTypeFormatDtfFiltered(dtf, required, defaults, isPlain);
    }

    /// <summary>
    /// Creates a per-type format DTF by filtering components relevant to the temporal type.
    /// Implements GetDateTimeFormat with inherit=~relevant~.
    /// When HasExplicitFormatComponents is true: filter to relevant components, throw if no overlap.
    /// When HasExplicitFormatComponents is false: apply per-type defaults (e.g., era-only case).
    /// </summary>
    private JsDateTimeFormat CreatePerTypeFormatDtfFiltered(JsDateTimeFormat dtf, DateTimeRequired required, DateTimeDefaults defaults, bool isPlain = false)
    {
        // Plain types should not show timeZoneName (only Instant has timezone)

        // Copy era if required includes date-like components
        var era = (required is DateTimeRequired.Date or DateTimeRequired.YearMonth or DateTimeRequired.Any)
            ? dtf.Era : null;

        if (!dtf.HasExplicitFormatComponents)
        {
            // No explicit component options from user (e.g., only era, or nothing).
            // Apply per-type defaults - ignore construction defaults on the DTF.
            string? year = null, month = null, day = null;
            string? hour = null, minute = null, second = null;
            string? timeZoneName = null;

            switch (defaults)
            {
                case DateTimeDefaults.Date:
                    year = "numeric"; month = "numeric"; day = "numeric";
                    break;
                case DateTimeDefaults.Time:
                    hour = "numeric"; minute = "numeric"; second = "numeric";
                    break;
                case DateTimeDefaults.YearMonth:
                    year = "numeric"; month = "numeric";
                    break;
                case DateTimeDefaults.MonthDay:
                    month = "numeric"; day = "numeric";
                    break;
                case DateTimeDefaults.ZonedDateTime:
                case DateTimeDefaults.All:
                    year = "numeric"; month = "numeric"; day = "numeric";
                    hour = "numeric"; minute = "numeric"; second = "numeric";
                    if (defaults == DateTimeDefaults.ZonedDateTime && !isPlain)
                        timeZoneName = "short";
                    break;
            }

            return new JsDateTimeFormat(
                _engine, dtf._prototype!, dtf.Locale, dtf.Calendar, dtf.NumberingSystem,
                dtf.TimeZone, dtf.HourCycle, null, null,
                null, era, year, month, day, null, hour, minute, second, null,
                timeZoneName, false, dtf.DateTimeFormatInfo, dtf.CultureInfo);
        }

        // User specified explicit component options - filter to relevant components
        string? fWeekday = null, fYear = null, fMonth = null, fDay = null;
        string? fDayPeriod = null, fHour = null, fMinute = null, fSecond = null;
        int? fFractionalSecondDigits = null;
        string? fTimeZoneName = isPlain ? null : dtf.TimeZoneName;

        bool anyPresent = false;

        switch (required)
        {
            case DateTimeRequired.Date:
                fWeekday = dtf.Weekday; fYear = dtf.Year; fMonth = dtf.Month; fDay = dtf.Day;
                anyPresent = fWeekday != null || fYear != null || fMonth != null || fDay != null;
                break;
            case DateTimeRequired.Time:
                fDayPeriod = dtf.DayPeriod; fHour = dtf.Hour; fMinute = dtf.Minute; fSecond = dtf.Second;
                fFractionalSecondDigits = dtf.FractionalSecondDigits;
                anyPresent = fDayPeriod != null || fHour != null || fMinute != null || fSecond != null || fFractionalSecondDigits != null;
                break;
            case DateTimeRequired.YearMonth:
                fYear = dtf.Year; fMonth = dtf.Month;
                anyPresent = fYear != null || fMonth != null;
                break;
            case DateTimeRequired.MonthDay:
                fMonth = dtf.Month; fDay = dtf.Day;
                anyPresent = fMonth != null || fDay != null;
                break;
            case DateTimeRequired.Any:
                fWeekday = dtf.Weekday; fYear = dtf.Year; fMonth = dtf.Month; fDay = dtf.Day;
                fDayPeriod = dtf.DayPeriod; fHour = dtf.Hour; fMinute = dtf.Minute; fSecond = dtf.Second;
                fFractionalSecondDigits = dtf.FractionalSecondDigits;
                anyPresent = fWeekday != null || fYear != null || fMonth != null || fDay != null ||
                    fDayPeriod != null || fHour != null || fMinute != null || fSecond != null || fFractionalSecondDigits != null;
                break;
        }

        if (!anyPresent)
        {
            // User explicitly set component options that don't overlap with this temporal type
            Throw.TypeError(_realm, "DateTimeFormat options are incompatible with this Temporal type");
            return null!;
        }

        return new JsDateTimeFormat(
            _engine, dtf._prototype!, dtf.Locale, dtf.Calendar, dtf.NumberingSystem,
            dtf.TimeZone, dtf.HourCycle, null, null,
            fWeekday, era, fYear, fMonth, fDay, fDayPeriod, fHour, fMinute, fSecond, fFractionalSecondDigits,
            fTimeZoneName, true, dtf.DateTimeFormatInfo, dtf.CultureInfo);
    }

    /// <summary>
    /// Converts a Temporal object to a .NET DateTime for formatting.
    /// </summary>
    private static DateTime ConvertTemporalToDateTime(JsValue temporal)
    {
        return temporal switch
        {
            JsPlainDate pd => new DateTime(pd.IsoDate.Year, pd.IsoDate.Month, pd.IsoDate.Day, 12, 0, 0, DateTimeKind.Unspecified),
            JsPlainDateTime pdt => new DateTime(pdt.IsoDateTime.Date.Year, pdt.IsoDateTime.Date.Month, pdt.IsoDateTime.Date.Day,
                pdt.IsoDateTime.Time.Hour, pdt.IsoDateTime.Time.Minute, pdt.IsoDateTime.Time.Second,
                pdt.IsoDateTime.Time.Millisecond, DateTimeKind.Unspecified),
            JsPlainTime pt => new DateTime(1970, 1, 1, pt.IsoTime.Hour, pt.IsoTime.Minute, pt.IsoTime.Second,
                pt.IsoTime.Millisecond, DateTimeKind.Unspecified),
            JsPlainYearMonth ym => new DateTime(ym.IsoDate.Year, ym.IsoDate.Month, ym.IsoDate.Day, 12, 0, 0, DateTimeKind.Unspecified),
            JsPlainMonthDay md => new DateTime(md.IsoDate.Year, md.IsoDate.Month, md.IsoDate.Day, 12, 0, 0, DateTimeKind.Unspecified),
            JsInstant inst => EpochNanosecondsToDateTime(inst.EpochNanoseconds),
            _ => DateTime.Now
        };
    }

    /// <summary>
    /// Handles a formattable value for formatRange/formatRangeToParts.
    /// </summary>
    private DateTime HandleDateTimeTemporalOrOtherForRange(JsDateTimeFormat dateTimeFormat, JsValue formattable)
    {
        if (formattable is JsZonedDateTime)
        {
            Throw.TypeError(_realm, "Temporal.ZonedDateTime is not supported in DateTimeFormat.formatRange().");
            return default;
        }

        if (formattable is JsPlainDate plainDate)
        {
            ValidateTemporalStyleRestrictions(dateTimeFormat, isDateOnly: true);
            return IsoDateToDateTime(plainDate.IsoDate);
        }

        if (formattable is JsPlainDateTime plainDateTime)
        {
            return IsoDateTimeToDateTime(plainDateTime.IsoDateTime);
        }

        if (formattable is JsPlainTime plainTime)
        {
            ValidateTemporalStyleRestrictions(dateTimeFormat, isTimeOnly: true);
            return IsoTimeToDateTime(plainTime.IsoTime);
        }

        if (formattable is JsPlainYearMonth yearMonth)
        {
            ValidateTemporalStyleRestrictions(dateTimeFormat, isDateOnly: true);
            return IsoDateToDateTime(yearMonth.IsoDate);
        }

        if (formattable is JsPlainMonthDay monthDay)
        {
            ValidateTemporalStyleRestrictions(dateTimeFormat, isDateOnly: true);
            return IsoDateToDateTime(monthDay.IsoDate);
        }

        if (formattable is JsInstant instant)
        {
            return EpochNanosecondsToDateTime(instant.EpochNanoseconds);
        }

        return ToDateTimeForRange(formattable);
    }

    /// <summary>
    /// Validates style restrictions for Temporal "date-only" and "time-only" types.
    /// PlainDate/PlainYearMonth/PlainMonthDay: timeStyle without dateStyle → TypeError
    /// PlainTime: dateStyle without timeStyle → TypeError
    /// When both styles present: the "wrong" one is simply ignored by the formatting.
    /// </summary>
    private void ValidateTemporalStyleRestrictions(JsDateTimeFormat dateTimeFormat, bool isDateOnly = false, bool isTimeOnly = false)
    {
        if (isDateOnly && dateTimeFormat.TimeStyle != null && dateTimeFormat.DateStyle == null)
        {
            Throw.TypeError(_realm, "Cannot format a date-only Temporal type with timeStyle and no dateStyle");
        }

        if (isTimeOnly && dateTimeFormat.DateStyle != null && dateTimeFormat.TimeStyle == null)
        {
            Throw.TypeError(_realm, "Cannot format a time-only Temporal type with dateStyle and no timeStyle");
        }
    }

    /// <summary>
    /// Converts an IsoDate to DateTime (time set to midnight).
    /// </summary>
    private static DateTime IsoDateToDateTime(IsoDate isoDate)
    {
        // Clamp to .NET DateTime range
        if (isoDate.Year < 1 || isoDate.Year > 9999)
        {
            return isoDate.Year < 1 ? DateTime.MinValue : DateTime.MaxValue;
        }

        return new DateTime(isoDate.Year, isoDate.Month, isoDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Converts an IsoDateTime to DateTime.
    /// </summary>
    private static DateTime IsoDateTimeToDateTime(IsoDateTime isoDateTime)
    {
        var date = isoDateTime.Date;
        var time = isoDateTime.Time;

        // Clamp to .NET DateTime range
        if (date.Year < 1 || date.Year > 9999)
        {
            return date.Year < 1 ? DateTime.MinValue : DateTime.MaxValue;
        }

        return new DateTime(date.Year, date.Month, date.Day,
            time.Hour, time.Minute, time.Second,
            time.Millisecond, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Converts an IsoTime to DateTime using reference date 1970-01-01.
    /// </summary>
    private static DateTime IsoTimeToDateTime(IsoTime time)
    {
        return new DateTime(1970, 1, 1, time.Hour, time.Minute, time.Second,
            time.Millisecond, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Converts epoch nanoseconds (BigInteger) to DateTime in UTC.
    /// </summary>
    private static DateTime EpochNanosecondsToDateTime(BigInteger epochNanoseconds)
    {
        const long nsPerTick = 100; // 1 tick = 100 nanoseconds

        // Convert to ticks since Unix epoch
        var ticks = (long) (epochNanoseconds / nsPerTick);

        // Unix epoch in ticks
        var unixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var totalTicks = unixEpochTicks + ticks;

        // Clamp to valid DateTime range
        if (totalTicks < DateTime.MinValue.Ticks)
        {
            return DateTime.MinValue;
        }

        if (totalTicks > DateTime.MaxValue.Ticks)
        {
            return DateTime.MaxValue;
        }

        return new DateTime(totalTicks, DateTimeKind.Utc);
    }

    /// <summary>
    /// Converts a JavaScript value to DateTime, returning the original JavaScript year
    /// when the date is outside .NET DateTime range (for proper era formatting).
    /// </summary>
    private DateTime ToDateTimeWithOriginalYear(JsValue value, out int? originalYear)
    {
        if (value.IsUndefined())
        {
            originalYear = null;
            return DateTime.Now;
        }

        if (value is JsDate jsDate)
        {
            // Check if date is within .NET DateTime range
            if (!jsDate.DateTimeRangeValid)
            {
                // Extract the original JavaScript year from the date value
                originalYear = GetJavaScriptYear(jsDate.DateValue);
                // Date is outside .NET range - return min/max based on sign
                return jsDate.DateValue < 0 ? DateTime.MinValue : DateTime.MaxValue;
            }

            // ECMA-402 requires formatting in local time unless a specific timezone is provided
            var dateTime = jsDate.ToDateTime();
            if (dateTime.Kind == DateTimeKind.Utc || dateTime.Kind == DateTimeKind.Unspecified)
            {
                dateTime = dateTime.ToLocalTime();
            }
            originalYear = null;
            return dateTime;
        }

        var timeValue = TypeConverter.ToNumber(value);
        DatePresentation presentation = timeValue;
        presentation = presentation.TimeClip();

        if (presentation.IsNaN)
        {
            Throw.RangeError(_realm, "Invalid time value");
        }

        // Clamp to .NET DateTime range if necessary
        if (presentation.Value < JsDate.Min)
        {
            originalYear = GetJavaScriptYear(presentation.Value);
            return DateTime.MinValue;
        }

        if (presentation.Value > JsDate.Max)
        {
            originalYear = GetJavaScriptYear(presentation.Value);
            return DateTime.MaxValue;
        }

        originalYear = null;
        return presentation.ToDateTime().ToLocalTime();
    }

    /// <summary>
    /// Extracts the JavaScript year from a timestamp value using the ECMAScript algorithm.
    /// JavaScript Date uses milliseconds since Unix epoch (1970-01-01).
    /// </summary>
    private static int GetJavaScriptYear(double timeValue)
    {
        // ECMAScript YearFromTime algorithm
        // https://tc39.es/ecma262/#sec-yearfromtime
        const double msPerDay = 86400000;

        // Day number from epoch
        var day = System.Math.Floor(timeValue / msPerDay);

        // Estimate year (rough approximation to start binary search)
        var year = (int) (1970 + day / 365.2425);

        // Binary search refinement for exact year
        while (DayFromYear(year + 1) <= day)
        {
            year++;
        }
        while (DayFromYear(year) > day)
        {
            year--;
        }

        return year;
    }

    /// <summary>
    /// Returns the day number of the first day of a given year (ECMAScript algorithm).
    /// </summary>
    private static double DayFromYear(int year)
    {
        // https://tc39.es/ecma262/#sec-dayfromyear
        return 365.0 * (year - 1970)
               + System.Math.Floor((year - 1969) / 4.0)
               - System.Math.Floor((year - 1901) / 100.0)
               + System.Math.Floor((year - 1601) / 400.0);
    }

    /// <summary>
    /// Gets the default hour cycle for a locale.
    /// Most locales use h12, but some use h23 (24-hour format without leading zero for midnight).
    /// </summary>
    internal static string GetDefaultHourCycle(string locale)
    {
        // Most English-speaking locales use 12-hour format
        var lang = locale.Split('-')[0].ToLowerInvariant();

        // 24-hour format locales
        if (lang is "de" or "fr" or "it" or "es" or "pt" or "nl" or "ru" or "pl" or "sv" or "da" or "nb" or "fi")
        {
            return "h23";
        }

        // Japanese uses h11 for 12-hour format (0-11 instead of 1-12)
        if (string.Equals(lang, "ja", StringComparison.Ordinal))
        {
            return "h11";
        }

        // Default to 12-hour format h12 (1-12)
        return "h12";
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // Use CreateDataPropertyOrThrow to avoid prototype chain setters
        result.CreateDataPropertyOrThrow("locale", dateTimeFormat.Locale);

        result.CreateDataPropertyOrThrow("calendar", dateTimeFormat.Calendar ?? "gregory");

        result.CreateDataPropertyOrThrow("numberingSystem", dateTimeFormat.NumberingSystem ?? "latn");

        // timeZone is always present - use local timezone if not specified
        // Convert Windows timezone IDs to IANA format per ECMA-402
        string timeZoneId;
        if (dateTimeFormat.TimeZone != null)
        {
            // User explicitly specified timezone - preserve as-is
            timeZoneId = dateTimeFormat.TimeZone;
        }
        else
        {
            // Default timezone from system - canonicalize Etc/UTC variants to UTC
            timeZoneId = TimeZoneInfo.Local.Id;
            // On Linux, TimeZoneInfo.Local.Id often returns "Etc/UTC" which should be
            // canonicalized to "UTC" for default timezone (but preserved if user-specified)
            if (string.Equals(timeZoneId, "Etc/UTC", StringComparison.Ordinal) ||
                string.Equals(timeZoneId, "Etc/UCT", StringComparison.Ordinal) ||
                string.Equals(timeZoneId, "Etc/Universal", StringComparison.Ordinal) ||
                string.Equals(timeZoneId, "Etc/Zulu", StringComparison.Ordinal))
            {
                timeZoneId = "UTC";
            }
        }
        result.CreateDataPropertyOrThrow("timeZone", ToIanaTimeZoneId(timeZoneId));

        // hourCycle and hour12 should be returned if hour is present OR if timeStyle is set
        // Per ECMA-402, timeStyle implies hour formatting
        if (dateTimeFormat.Hour != null || dateTimeFormat.TimeStyle != null)
        {
            // Use provided hourCycle or derive default from locale
            var hourCycle = dateTimeFormat.HourCycle ?? GetDefaultHourCycle(dateTimeFormat.Locale);
            result.CreateDataPropertyOrThrow("hourCycle", hourCycle);
            result.CreateDataPropertyOrThrow("hour12", string.Equals(hourCycle, "h11", StringComparison.Ordinal) ||
                                 string.Equals(hourCycle, "h12", StringComparison.Ordinal));
        }

        if (dateTimeFormat.DateStyle != null)
        {
            result.CreateDataPropertyOrThrow("dateStyle", dateTimeFormat.DateStyle);
        }

        if (dateTimeFormat.TimeStyle != null)
        {
            result.CreateDataPropertyOrThrow("timeStyle", dateTimeFormat.TimeStyle);
        }

        // Component options
        if (dateTimeFormat.Weekday != null)
        {
            result.CreateDataPropertyOrThrow("weekday", dateTimeFormat.Weekday);
        }

        if (dateTimeFormat.Era != null)
        {
            result.CreateDataPropertyOrThrow("era", dateTimeFormat.Era);
        }

        if (dateTimeFormat.Year != null)
        {
            result.CreateDataPropertyOrThrow("year", dateTimeFormat.Year);
        }

        if (dateTimeFormat.Month != null)
        {
            result.CreateDataPropertyOrThrow("month", dateTimeFormat.Month);
        }

        if (dateTimeFormat.Day != null)
        {
            result.CreateDataPropertyOrThrow("day", dateTimeFormat.Day);
        }

        // dayPeriod comes after day and before hour per ECMA-402 spec order
        if (dateTimeFormat.DayPeriod != null)
        {
            result.CreateDataPropertyOrThrow("dayPeriod", dateTimeFormat.DayPeriod);
        }

        if (dateTimeFormat.Hour != null)
        {
            result.CreateDataPropertyOrThrow("hour", dateTimeFormat.Hour);
        }

        if (dateTimeFormat.Minute != null)
        {
            result.CreateDataPropertyOrThrow("minute", dateTimeFormat.Minute);
        }

        if (dateTimeFormat.Second != null)
        {
            result.CreateDataPropertyOrThrow("second", dateTimeFormat.Second);
        }

        if (dateTimeFormat.FractionalSecondDigits.HasValue)
        {
            result.CreateDataPropertyOrThrow("fractionalSecondDigits", dateTimeFormat.FractionalSecondDigits.Value);
        }

        if (dateTimeFormat.TimeZoneName != null)
        {
            result.CreateDataPropertyOrThrow("timeZoneName", dateTimeFormat.TimeZoneName);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.formatrange
    /// </summary>
    private JsValue FormatRange(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var startDate = arguments.At(0);
        var endDate = arguments.At(1);

        // Validate arguments
        if (startDate.IsUndefined() || endDate.IsUndefined())
        {
            Throw.TypeError(_realm, "startDate and endDate are required");
        }

        // Per spec: ToDateTimeFormattable on both before checking kinds
        var x = ToDateTimeFormattable(startDate);
        var y = ToDateTimeFormattable(endDate);

        // Check SameTemporalType
        ValidateTemporalRangeTypes(x, y);

        var start = HandleDateTimeTemporalOrOtherForRange(dateTimeFormat, x);
        var end = HandleDateTimeTemporalOrOtherForRange(dateTimeFormat, y);

        // For Temporal objects, use per-type DTF and isPlain flag
        var isTemporalInput = IsTemporalObject(x);
        var isPlain = isTemporalInput && x is not JsInstant;
        var effectiveDtf = isTemporalInput ? GetTemporalFormatDtf(dateTimeFormat, x) : dateTimeFormat;

        // Format both dates
        var startFormatted = effectiveDtf.Format(start, isPlain: isPlain);
        var endFormatted = effectiveDtf.Format(end, isPlain: isPlain);

        // If the dates are the same when formatted, return just one
        if (string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            return startFormatted;
        }

        // Get parts to determine shared prefix/suffix for collapsing
        var startParts = effectiveDtf.FormatToParts(start, isPlain: isPlain);
        var endParts = effectiveDtf.FormatToParts(end, isPlain: isPlain);

        var sharedPrefixEnd = FindNaturalBoundaryPrefix(startParts, endParts);
        var sharedSuffixLen = FindNaturalBoundarySuffix(startParts, endParts, sharedPrefixEnd);

        if (sharedPrefixEnd > 0 || sharedSuffixLen > 0)
        {
            var result = new System.Text.StringBuilder();
            var startUniqueEnd = startParts.Count - sharedSuffixLen;
            var endUniqueEnd = endParts.Count - sharedSuffixLen;

            // Shared prefix
            for (var i = 0; i < sharedPrefixEnd; i++)
            {
                result.Append(startParts[i].Value);
            }

            // Start range unique part
            for (var i = sharedPrefixEnd; i < startUniqueEnd; i++)
            {
                result.Append(startParts[i].Value);
            }

            result.Append(" \u2013 ");

            // End range unique part
            for (var i = sharedPrefixEnd; i < endUniqueEnd; i++)
            {
                result.Append(endParts[i].Value);
            }

            // Shared suffix
            for (var i = startUniqueEnd; i < startParts.Count; i++)
            {
                result.Append(startParts[i].Value);
            }

            return result.ToString();
        }

        // Return a range string without collapsing
        return $"{startFormatted} \u2013 {endFormatted}";
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.datetimeformat.prototype.formatrangetoparts
    /// </summary>
    private JsArray FormatRangeToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var dateTimeFormat = ValidateDateTimeFormat(thisObject);

        var startDate = arguments.At(0);
        var endDate = arguments.At(1);

        // Validate arguments
        if (startDate.IsUndefined() || endDate.IsUndefined())
        {
            Throw.TypeError(_realm, "startDate and endDate are required");
        }

        // Per spec: ToDateTimeFormattable on both before checking kinds
        var x = ToDateTimeFormattable(startDate);
        var y = ToDateTimeFormattable(endDate);

        // Check SameTemporalType
        ValidateTemporalRangeTypes(x, y);

        var start = HandleDateTimeTemporalOrOtherForRange(dateTimeFormat, x);
        var end = HandleDateTimeTemporalOrOtherForRange(dateTimeFormat, y);

        // For Temporal objects, use per-type DTF and isPlain flag
        var isTemporalInput = IsTemporalObject(x);
        var isPlain = isTemporalInput && x is not JsInstant;
        var effectiveDtf = isTemporalInput ? GetTemporalFormatDtf(dateTimeFormat, x) : dateTimeFormat;

        // Get parts for both dates
        var startParts = effectiveDtf.FormatToParts(start, isPlain: isPlain);
        var endParts = effectiveDtf.FormatToParts(end, isPlain: isPlain);

        // Check if dates are practically equal (same formatted output)
        var startFormatted = effectiveDtf.Format(start, isPlain: isPlain);
        var endFormatted = effectiveDtf.Format(end, isPlain: isPlain);

        var result = new JsArray(Engine);
        uint index = 0;

        if (string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            // Dates are practically equal - return parts with source "shared"
            foreach (var part in startParts)
            {
                AddPartToResult(result, ref index, part.Type, part.Value, "shared");
            }
        }
        else
        {
            // Find shared prefix and suffix at natural boundaries
            var sharedPrefixEnd = FindNaturalBoundaryPrefix(startParts, endParts);
            var sharedSuffixLen = FindNaturalBoundarySuffix(startParts, endParts, sharedPrefixEnd);

            if (sharedPrefixEnd > 0 || sharedSuffixLen > 0)
            {
                var startUniqueEnd = startParts.Count - sharedSuffixLen;
                var endUniqueEnd = endParts.Count - sharedSuffixLen;

                // Output shared prefix
                for (var i = 0; i < sharedPrefixEnd; i++)
                {
                    AddPartToResult(result, ref index, startParts[i].Type, startParts[i].Value, "shared");
                }

                // Output start range unique part
                for (var i = sharedPrefixEnd; i < startUniqueEnd; i++)
                {
                    AddPartToResult(result, ref index, startParts[i].Type, startParts[i].Value, "startRange");
                }

                // Separator
                AddPartToResult(result, ref index, "literal", " \u2013 ", "shared");

                // Output end range unique part
                for (var i = sharedPrefixEnd; i < endUniqueEnd; i++)
                {
                    AddPartToResult(result, ref index, endParts[i].Type, endParts[i].Value, "endRange");
                }

                // Output shared suffix
                for (var i = startUniqueEnd; i < startParts.Count; i++)
                {
                    AddPartToResult(result, ref index, startParts[i].Type, startParts[i].Value, "shared");
                }
            }
            else
            {
                // No collapsing - output full dates with separator
                foreach (var part in startParts)
                {
                    AddPartToResult(result, ref index, part.Type, part.Value, "startRange");
                }

                AddPartToResult(result, ref index, "literal", " \u2013 ", "shared");

                foreach (var part in endParts)
                {
                    AddPartToResult(result, ref index, part.Type, part.Value, "endRange");
                }
            }
        }

        return result;
    }

    private void AddPartToResult(JsArray result, ref uint index, string type, string value, string source)
    {
        var partObj = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
        partObj.Set("type", type);
        partObj.Set("value", value);
        partObj.Set("source", source);
        result.SetIndexValue(index++, partObj, updateLength: true);
    }

    /// <summary>
    /// Finds the length of the shared prefix between start and end parts,
    /// but only if the prefix ends at a natural boundary (e.g., the ", " literal
    /// separating date from time components, or a multi-char literal separator).
    /// Internal separators like "/" and ":" within date/time groups are not natural boundaries.
    /// Also ensures the remaining unique part has at least 2 non-literal components to avoid
    /// over-collapsing (e.g., collapsing "Mar 4, " when only year differs).
    /// Returns 0 if no valid shared prefix exists.
    /// </summary>
    private static int FindNaturalBoundaryPrefix(List<JsDateTimeFormat.DateTimePart> startParts, List<JsDateTimeFormat.DateTimePart> endParts)
    {
        var minLen = System.Math.Min(startParts.Count, endParts.Count);

        // Find how many parts match from the start
        var matchLen = 0;
        while (matchLen < minLen)
        {
            var sp = startParts[matchLen];
            var ep = endParts[matchLen];

            if (!string.Equals(sp.Type, ep.Type, StringComparison.Ordinal) ||
                !string.Equals(sp.Value, ep.Value, StringComparison.Ordinal))
            {
                break;
            }

            matchLen++;
        }

        // No shared prefix at all, or entire sequence matches (handled separately as "shared")
        if (matchLen == 0 || matchLen >= minLen)
        {
            return 0;
        }

        // Check if the prefix ends at a natural boundary.
        var lastPart = startParts[matchLen - 1];
        if (!string.Equals(lastPart.Type, "literal", StringComparison.Ordinal))
        {
            return 0;
        }

        var val = lastPart.Value;
        // Reject single-char internal separators
        if (val.Length == 1 && (val[0] == '/' || val[0] == ':' || val[0] == '.' || val[0] == '-'))
        {
            return 0;
        }

        // Ensure at least 2 non-literal parts remain in the unique section.
        // This prevents over-collapsing like "Mar 4, 2019 – 2020" when only years differ.
        var remainingNonLiteral = 0;
        for (var i = matchLen; i < startParts.Count; i++)
        {
            if (!string.Equals(startParts[i].Type, "literal", StringComparison.Ordinal))
            {
                remainingNonLiteral++;
            }
        }

        if (remainingNonLiteral < 2)
        {
            return 0;
        }

        return matchLen;
    }

    /// <summary>
    /// Finds the number of shared suffix parts between start and end parts,
    /// starting from the given offset (to avoid overlapping with a shared prefix).
    /// Only accepts suffixes that start at a natural boundary (multi-char literal).
    /// Returns 0 if no valid shared suffix exists.
    /// </summary>
    private static int FindNaturalBoundarySuffix(List<JsDateTimeFormat.DateTimePart> startParts, List<JsDateTimeFormat.DateTimePart> endParts, int prefixEnd)
    {
        var startLen = startParts.Count;
        var endLen = endParts.Count;

        // Find how many parts match from the end
        var matchLen = 0;
        while (matchLen < startLen - prefixEnd && matchLen < endLen - prefixEnd)
        {
            var sp = startParts[startLen - 1 - matchLen];
            var ep = endParts[endLen - 1 - matchLen];

            if (!string.Equals(sp.Type, ep.Type, StringComparison.Ordinal) ||
                !string.Equals(sp.Value, ep.Value, StringComparison.Ordinal))
            {
                break;
            }

            matchLen++;
        }

        if (matchLen == 0)
        {
            return 0;
        }

        // Check if the suffix starts at a natural boundary
        // The first suffix part (counting from the end) should be preceded by a natural boundary literal
        var firstSuffixIdx = startLen - matchLen;
        var boundaryPart = startParts[firstSuffixIdx];
        if (string.Equals(boundaryPart.Type, "literal", StringComparison.Ordinal))
        {
            var val = boundaryPart.Value;
            // Reject single-char internal separators
            if (val.Length == 1 && (val[0] == '/' || val[0] == ':' || val[0] == '.' || val[0] == '-'))
            {
                return 0;
            }

            // Accept multi-char literals like ", " as natural boundaries
            return matchLen;
        }

        return 0;
    }

    private DateTime ToDateTimeForRange(JsValue value)
    {
        if (value is JsDate jsDate)
        {
            // Check if date is within .NET DateTime range
            if (!jsDate.DateTimeRangeValid)
            {
                // Date is outside .NET range - return min/max based on sign
                return jsDate.DateValue < 0 ? DateTime.MinValue : DateTime.MaxValue;
            }

            var dt = jsDate.ToDateTime();
            if (dt == DateTime.MinValue)
            {
                // Invalid date
                Throw.RangeError(_realm, "Invalid time value");
            }
            // ECMA-402 requires formatting in local time unless a specific timezone is provided
            if (dt.Kind == DateTimeKind.Utc || dt.Kind == DateTimeKind.Unspecified)
            {
                dt = dt.ToLocalTime();
            }
            return dt;
        }

        var timeValue = TypeConverter.ToNumber(value);
        DatePresentation presentation = timeValue;
        presentation = presentation.TimeClip();

        if (presentation.IsNaN)
        {
            Throw.RangeError(_realm, "Invalid time value");
        }

        // Clamp to .NET DateTime range if necessary
        if (presentation.Value < JsDate.Min)
        {
            return DateTime.MinValue;
        }
        if (presentation.Value > JsDate.Max)
        {
            return DateTime.MaxValue;
        }

        return presentation.ToDateTime().ToLocalTime();
    }

    /// <summary>
    /// Converts a timezone ID to IANA format.
    /// Windows uses names like "FLE Standard Time" but ECMA-402 requires IANA names like "Europe/Helsinki".
    /// </summary>
    private static string ToIanaTimeZoneId(string timeZoneId)
    {
        // UTC is already valid
        if (string.Equals(timeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        // Offset timezones are already valid (e.g., "+03:00")
        if (timeZoneId.Length > 0 && (timeZoneId[0] == '+' || timeZoneId[0] == '-'))
        {
            return timeZoneId;
        }

#if NET6_0_OR_GREATER
        // Try to convert Windows ID to IANA
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId))
        {
            return ianaId;
        }
#endif

        // Check if it's already an IANA ID (contains '/')
        if (timeZoneId.Contains('/'))
        {
            return timeZoneId;
        }

        // Fallback: try to get the IANA ID from the TimeZoneInfo
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
#if NET6_0_OR_GREATER
            // On .NET 6+, check if HasIanaId is available
            if (tz.HasIanaId)
            {
                return tz.Id;
            }
            // Try conversion again with the found timezone
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out ianaId))
            {
                return ianaId;
            }
#endif
        }
        catch
        {
            // Ignore errors
        }

        // Last resort: return as-is (won't pass validation but better than crashing)
        return timeZoneId;
    }
}

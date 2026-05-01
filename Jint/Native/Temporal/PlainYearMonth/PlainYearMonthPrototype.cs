using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plainyearmonth-prototype-object
/// </summary>
[JsObject]
internal sealed partial class PlainYearMonthPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PlainYearMonthConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString PlainYearMonthToStringTag = new("Temporal.PlainYearMonth");

    internal PlainYearMonthPrototype(
        Engine engine,
        Realm realm,
        PlainYearMonthConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }


    private JsPlainYearMonth ValidatePlainYearMonth(JsValue thisObject)
    {
        if (thisObject is JsPlainYearMonth plainYearMonth)
            return plainYearMonth;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainYearMonth");
        return null!;
    }

    // Getters
    [JsAccessor("calendarId")]
    private JsString GetCalendarId(JsValue thisObject) => new JsString(ValidatePlainYearMonth(thisObject).Calendar);
    [JsAccessor("year")]
    private JsNumber GetYear(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return JsNumber.Create(TemporalHelpers.CalendarYear(ym.Calendar, ym.IsoDate, _engine));
    }
    [JsAccessor("month")]
    private JsNumber GetMonth(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return JsNumber.Create(TemporalHelpers.CalendarMonth(ym.Calendar, ym.IsoDate, _engine));
    }
    [JsAccessor("monthCode")]
    private JsString GetMonthCode(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return new JsString(TemporalHelpers.CalendarMonthCode(ym.Calendar, ym.IsoDate, _engine));
    }
    [JsAccessor("daysInMonth")]
    private JsNumber GetDaysInMonth(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return JsNumber.Create(TemporalHelpers.CalendarDaysInMonth(ym.Calendar, ym.IsoDate, _engine));
    }
    [JsAccessor("daysInYear")]
    private JsNumber GetDaysInYear(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return JsNumber.Create(TemporalHelpers.CalendarDaysInYear(ym.Calendar, ym.IsoDate, _engine));
    }
    [JsAccessor("monthsInYear")]
    private JsNumber GetMonthsInYear(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return JsNumber.Create(TemporalHelpers.CalendarMonthsInYear(ym.Calendar, ym.IsoDate, _engine));
    }
    [JsAccessor("inLeapYear")]
    private JsBoolean GetInLeapYear(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        return TemporalHelpers.CalendarInLeapYear(ym.Calendar, ym.IsoDate, _engine) ? JsBoolean.True : JsBoolean.False;
    }
    [JsAccessor("eraYear")]
    private JsValue GetEraYear(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var eraYear = TemporalHelpers.CalendarEraYear(ym.Calendar, ym.IsoDate);
        return eraYear.HasValue ? JsNumber.Create(eraYear.Value) : Undefined;
    }
    [JsAccessor("era")]
    private JsValue GetEra(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var era = TemporalHelpers.CalendarEra(ym.Calendar, ym.IsoDate);
        return era is not null ? new JsString(era) : Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.with
    /// </summary>
    [JsFunction(Length = 1)]
    private JsPlainYearMonth With(JsValue thisObject, JsValue temporalYearMonthLike, JsValue options)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        if (!temporalYearMonthLike.IsObject())
        {
            Throw.TypeError(_realm, "with argument must be an object");
        }

        var obj = temporalYearMonthLike.AsObject();

        // Reject Temporal objects (IsPartialTemporalObject step 2)
        // Only plain objects are allowed, not Temporal types
        if (obj is JsPlainDate or JsPlainDateTime or JsPlainMonthDay or JsPlainTime or JsPlainYearMonth or JsZonedDateTime or JsDuration or JsInstant)
        {
            Throw.TypeError(_realm, "with argument must be a plain object, not a Temporal object");
        }

        // RejectObjectWithCalendarOrTimeZone - check for calendar and timeZone properties first
        var calendarProperty = obj.Get("calendar");
        if (!calendarProperty.IsUndefined())
        {
            Throw.TypeError(_realm, "calendar property not supported");
        }

        var timeZoneProperty = obj.Get("timeZone");
        if (!timeZoneProperty.IsUndefined())
        {
            Throw.TypeError(_realm, "timeZone property not supported");
        }

        // Read and convert properties in alphabetical order with immediate conversion per spec
        // Order: month, monthCode, year
        var isNonIso8601 = ym.Calendar is not "iso8601" and not "gregory";

        var monthProp = obj.Get("month");
        int month = 0;
        var monthExplicit = !monthProp.IsUndefined();
        if (monthExplicit)
        {
            month = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthProp);
        }

        var monthCodeProp = obj.Get("monthCode");
        var monthCodeExplicit = !monthCodeProp.IsUndefined();
        string? monthCode = null;
        if (monthCodeExplicit)
        {
            // monthCode must be a string (per spec)
            // Handle objects specially: call ToPrimitive and ensure result is a string
            if (monthCodeProp.IsObject())
            {
                var primitive = TypeConverter.ToPrimitive(monthCodeProp, Types.String);
                if (!primitive.IsString())
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }
                monthCode = primitive.ToString();
            }
            else if (monthCodeProp.IsString())
            {
                monthCode = TypeConverter.ToString(monthCodeProp);
            }
            else
            {
                // Number, BigInt, Boolean, Null - reject; Symbol throws from ToString
                if (monthCodeProp.Type != Types.Symbol)
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }
                monthCode = TypeConverter.ToString(monthCodeProp);
            }
        }
        else if (isNonIso8601 && !monthExplicit)
        {
            // Only default monthCode from the calendar when month was not explicitly provided;
            // if the user set month explicitly, let month drive the conversion without monthCode
            monthCode = TemporalHelpers.CalendarMonthCode(ym.Calendar, ym.IsoDate, _engine);
        }

        // Default month from the existing date when neither month nor monthCode was explicitly
        // supplied. When monthCode IS explicitly supplied (without month), leave month=0 so
        // monthCode alone drives resolution — per NonIsoFieldKeysToIgnore, the existing date's
        // month must not be carried over and create a spurious mismatch.
        // For lunisolar calendars (chinese, dangi, hebrew) we similarly leave month=0 so the
        // defaulted monthCode drives, because the ordinal-to-monthCode mapping is year-dependent
        // (e.g. Hebrew M12 is ordinal 12 in non-leap years and ordinal 13 in leap years —
        // carrying the source ordinal across a year change to a year of different leap-status
        // would produce a spurious month/monthCode mismatch).
        var isLunisolar = ym.Calendar is "chinese" or "dangi" or "hebrew";
        if (!monthExplicit && !monthCodeExplicit && !isLunisolar)
        {
            month = isNonIso8601
                ? TemporalHelpers.CalendarMonth(ym.Calendar, ym.IsoDate, _engine)
                : ym.IsoDate.Month;
        }

        var yearProp = obj.Get("year");
        int year;
        if (!yearProp.IsUndefined())
        {
            year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearProp);
        }
        else
        {
            year = isNonIso8601
                ? TemporalHelpers.CalendarYear(ym.Calendar, ym.IsoDate, _engine)
                : ym.IsoDate.Year;
        }

        // Read era/eraYear only for calendars that support them
        var hasEraOrEraYear = false;
        if (TemporalHelpers.CalendarUsesEras(ym.Calendar))
        {
            var eraValue = obj.Get("era");
            var eraYearValue = obj.Get("eraYear");
            if (!eraValue.IsUndefined() && !eraYearValue.IsUndefined())
            {
                hasEraOrEraYear = true;
                var eraYear2 = TemporalHelpers.ReadEraFields(_realm, obj, ym.Calendar);
                if (eraYear2.HasValue)
                {
                    year = eraYear2.Value;
                }
            }
            else if (!eraValue.IsUndefined() || !eraYearValue.IsUndefined())
            {
                hasEraOrEraYear = true;
                Throw.TypeError(_realm, "Mismatching era/eraYear");
            }
        }

        // Validate that at least one temporal field was provided (IsPartialTemporalObject)
        if (!monthExplicit && !monthCodeExplicit && yearProp.IsUndefined()
            && !hasEraOrEraYear)
        {
            Throw.TypeError(_realm, "with argument must have at least one temporal property");
        }

        // Read options BEFORE any validation (per spec)
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        // Handle non-ISO8601 calendars via CalendarDateToISO
        if (isNonIso8601)
        {
            // Validate monthCode well-formedness if explicitly provided
            if (monthCodeExplicit && monthCode is not null)
            {
                var mc = TemporalHelpers.ParseMonthCode(_realm, monthCode);

                // Gregorian-based calendars don't support leap months
                if (TemporalHelpers.IsGregorianBasedCalendar(ym.Calendar))
                {
                    if (monthCode.Length == 4 && monthCode[3] == 'L')
                    {
                        Throw.RangeError(_realm, $"Leap months are not valid for calendar: {monthCode}");
                    }

                    if (mc < 1 || mc > 12)
                    {
                        Throw.RangeError(_realm, $"Month {mc} is not valid for calendar");
                    }

                    // month/monthCode consistency for Gregorian-based
                    if (monthExplicit && month != mc)
                    {
                        Throw.RangeError(_realm, "Mismatching month/monthCode");
                    }

                    month = mc;
                    monthCode = null; // let month drive for Gregorian-based
                }
            }

            var date = TemporalHelpers.CalendarDateToISO(_realm, ym.Calendar, year, month, 1, overflow, monthCode);
            if (date is null)
            {
                Throw.RangeError(_realm, "Invalid year-month");
            }

            if (!TemporalHelpers.ISOYearMonthWithinLimits(date.Value.Year, date.Value.Month))
            {
                Throw.RangeError(_realm, "Year-month is outside the representable range");
            }

            return _constructor.Construct(date.Value, ym.Calendar);
        }

        // ISO calendar path - validate monthCode
        var parsedMonthFromCode = TemporalHelpers.ValidateMonthCodeForNonLeapCalendar(
            _realm, monthCode, monthExplicit ? month : null);
        if (parsedMonthFromCode.HasValue)
        {
            month = parsedMonthFromCode.Value;
        }

        // Validate month range
        if (month < 1 || month > 12)
        {
            if (string.Equals(overflow, "constrain", StringComparison.Ordinal))
            {
                month = System.Math.Max(1, System.Math.Min(12, month));
            }
            else
            {
                Throw.RangeError(_realm, "Invalid year-month");
            }
        }

        // Validate year-month is within Temporal's representable range
        if (!TemporalHelpers.ISOYearMonthWithinLimits(year, month))
        {
            Throw.RangeError(_realm, "Year-month is outside the representable range");
        }

        return _constructor.Construct(new IsoDate(year, month, 1), ym.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.add
    /// </summary>
    [JsFunction(Length = 1)]
    private JsPlainYearMonth Add(JsValue thisObject, JsValue temporalDurationLike, JsValue options)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var duration = ToDurationRecord(temporalDurationLike);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        return AddDurationToYearMonth(ym, duration, overflow);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.subtract
    /// </summary>
    [JsFunction(Length = 1)]
    private JsPlainYearMonth Subtract(JsValue thisObject, JsValue temporalDurationLike, JsValue options)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var duration = ToDurationRecord(temporalDurationLike);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        // Negate the duration
        var negatedDuration = new DurationRecord(
            -duration.Years, -duration.Months, -duration.Weeks, -duration.Days,
            -duration.Hours, -duration.Minutes, -duration.Seconds,
            -duration.Milliseconds, -duration.Microseconds, -duration.Nanoseconds);

        return AddDurationToYearMonth(ym, negatedDuration, overflow);
    }

    private JsPlainYearMonth AddDurationToYearMonth(JsPlainYearMonth ym, DurationRecord duration, string overflow)
    {
        // Per spec: reject durations with non-zero weeks, days, or time components
        if (duration.Weeks != 0 || duration.Days != 0 ||
            duration.Hours != 0 || duration.Minutes != 0 || duration.Seconds != 0 ||
            duration.Milliseconds != 0 || duration.Microseconds != 0 || duration.Nanoseconds != 0)
        {
            Throw.RangeError(_realm, "Duration must not have weeks, days, or time components for PlainYearMonth");
        }

        // Step 8-9: Set day=1 (calendar day-1, not ISO day-1) and validate within range.
        // For non-ISO calendars, ym.IsoDate is the ISO anchor for calendar (year, month, day=1) at
        // construction but PYM may have been parsed from a string with a different anchor day.
        var intermediateDate = TemporalHelpers.IsoDateForCalendarFirstOfMonth(ym.Calendar, ym.IsoDate);
        TemporalHelpers.CheckISODaysRange(_realm, intermediateDate);

        // Use calendar-aware date addition
        var yearMonthDuration = new DurationRecord(duration.Years, duration.Months, 0, 0, 0, 0, 0, 0, 0, 0);
        var resultDate = TemporalHelpers.CalendarDateAdd(_realm, ym.Calendar, intermediateDate, yearMonthDuration, overflow);

        // Validate the result is within Temporal's representable range
        if (!TemporalHelpers.ISOYearMonthWithinLimits(resultDate.Year, resultDate.Month))
        {
            Throw.RangeError(_realm, "Year-month result is outside the representable range");
        }

        return _constructor.Construct(resultDate, ym.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.until
    /// </summary>
    [JsFunction(Length = 1)]
    private JsDuration Until(JsValue thisObject, JsValue other, JsValue options)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var otherYm = _constructor.ToTemporalYearMonth(other, "constrain");

        // Calendar equality check (before reading options per spec)
        if (!string.Equals(ym.Calendar, otherYm.Calendar, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "Calendars must match for year-month difference operations");
        }

        // Only year and month units allowed for PlainYearMonth
        var yearMonthUnits = new[] { "year", "month" };

        // GetDifferenceSettings reads options in correct order per spec
        var fallbackSmallestUnit = "month";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            options,
            "until",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            yearMonthUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "year")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "year");
        }

        return DifferenceYearMonth(ym, otherYm, largestUnit, settings.SmallestUnit, settings.RoundingMode, settings.RoundingIncrement, negate: false);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.since
    /// </summary>
    [JsFunction(Length = 1)]
    private JsDuration Since(JsValue thisObject, JsValue other, JsValue options)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var otherYm = _constructor.ToTemporalYearMonth(other, "constrain");

        // Calendar equality check (before reading options per spec)
        if (!string.Equals(ym.Calendar, otherYm.Calendar, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "Calendars must match for year-month difference operations");
        }

        // Only year and month units allowed for PlainYearMonth
        var yearMonthUnits = new[] { "year", "month" };

        // GetDifferenceSettings reads options in correct order per spec
        // and negates rounding mode for "since" operation
        var fallbackSmallestUnit = "month";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            options,
            "since",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            yearMonthUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "year")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "year");
        }

        return DifferenceYearMonth(ym, otherYm, largestUnit, settings.SmallestUnit, settings.RoundingMode, settings.RoundingIncrement, negate: true);
    }

    /// <summary>
    /// DifferenceTemporalPlainYearMonth ( operation, yearMonth, other, options )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencetemporalplainyearmonth
    /// </summary>
    private JsDuration DifferenceYearMonth(JsPlainYearMonth ym1, JsPlainYearMonth ym2, string largestUnit, string smallestUnit, string roundingMode, int roundingIncrement, bool negate)
    {
        // Step 6: If equal, return zero duration
        if (TemporalHelpers.CompareIsoDates(ym1.IsoDate, ym2.IsoDate) == 0)
        {
            var zeroDuration = new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return _engine.Realm.Intrinsics.TemporalDuration.Construct(zeroDuration);
        }

        // Step 7-10: Convert both PlainYearMonth to PlainDate with calendar day-1.
        // For non-ISO calendars, ISO month/day differs from calendar month/day, so naively
        // setting ISO day to 1 would produce a date in the wrong calendar month.
        var thisDate = TemporalHelpers.IsoDateForCalendarFirstOfMonth(ym1.Calendar, ym1.IsoDate);
        var otherDate = TemporalHelpers.IsoDateForCalendarFirstOfMonth(ym2.Calendar, ym2.IsoDate);

        // Step 9: CalendarDateFromFields validates the dates are within ISO range
        TemporalHelpers.CheckISODaysRange(_realm, thisDate);
        TemporalHelpers.CheckISODaysRange(_realm, otherDate);

        // Step 11: Get date difference using calendar
        var dateDifference = TemporalHelpers.CalendarDateUntil(ym1.Calendar, thisDate, otherDate, largestUnit);

        // Step 12: AdjustDateDurationRecord - zero out weeks and days (PlainYearMonth only has years/months)
        var yearsMonthsDifference = new DurationRecord(
            dateDifference.Years,
            dateDifference.Months,
            0, 0, 0, 0, 0, 0, 0, 0); // weeks=0, days=0

        // Step 13: Combine date and time duration
        var duration = TemporalHelpers.CombineDateAndTimeDuration(yearsMonthsDifference, 0);

        // Step 14-19: If rounding needed, use RoundRelativeDuration
        DurationRecord result;
        if (!string.Equals(smallestUnit, "month", StringComparison.Ordinal) || roundingIncrement != 1)
        {
            // Create ISO DateTime records for start and end
            var isoDateTime = new IsoDateTime(thisDate, IsoTime.Midnight);
            var isoDateTimeOther = new IsoDateTime(otherDate, IsoTime.Midnight);

            // Get epoch nanoseconds
            var originEpochNs = TemporalHelpers.GetUTCEpochNanoseconds(isoDateTime);
            var destEpochNs = TemporalHelpers.GetUTCEpochNanoseconds(isoDateTimeOther);

            // Round the duration
            var roundedDuration = TemporalHelpers.RoundRelativeDuration(
                _realm,
                null, // timeZoneProvider = null for PlainYearMonth
                duration,
                originEpochNs,
                destEpochNs,
                isoDateTime,
                null, // timeZone = ~unset~
                ym1.Calendar,
                largestUnit,
                roundingIncrement,
                smallestUnit,
                roundingMode);

            // Step 20: Convert from internal duration
            result = TemporalHelpers.TemporalDurationFromInternal(
                _realm,
                roundedDuration.Years,
                roundedDuration.Months,
                0, 0, 0, // weeks=0, days=0, time=0
                "day");
        }
        else
        {
            // No rounding needed, use date difference directly
            result = yearsMonthsDifference;
        }

        // Step 21: If operation is "since", negate the result
        // Use NoNegativeZero to ensure -0 becomes +0 (spec: CreateNegatedTemporalDuration)
        if (negate)
        {
            result = new DurationRecord(
                TemporalHelpers.NoNegativeZero(-result.Years),
                TemporalHelpers.NoNegativeZero(-result.Months),
                0, 0, 0, 0, 0, 0, 0, 0); // No weeks, days, or time components for PlainYearMonth
        }

        // Step 22: Return result
        return _engine.Realm.Intrinsics.TemporalDuration.Construct(result);
    }


    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.equals
    /// </summary>
    [JsFunction(Length = 1)]
    private JsBoolean Equals(JsValue thisObject, JsValue other)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var otherYm = _constructor.ToTemporalYearMonth(other, "constrain");

        return ym.IsoDate.Year == otherYm.IsoDate.Year &&
               ym.IsoDate.Month == otherYm.IsoDate.Month &&
               ym.IsoDate.Day == otherYm.IsoDate.Day &&
               string.Equals(ym.Calendar, otherYm.Calendar, StringComparison.Ordinal)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.tostring
    /// </summary>
    [JsFunction(Length = 0, Name = "toString")]
    private JsString ToTemporalString(JsValue thisObject, JsValue optionsValue)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var options = TemporalHelpers.GetOptionsObject(_realm, optionsValue);
        var showCalendar = GetCalendarNameOption(options);

        // For non-ISO calendars, always include the reference day (year-month alone is ambiguous)
        var isNonIsoCalendar = !string.Equals(ym.Calendar, "iso8601", StringComparison.Ordinal);

        // Include the calendar annotation based on the calendarName option
        var includeCalendar = string.Equals(showCalendar, "always", StringComparison.Ordinal) ||
                              string.Equals(showCalendar, "critical", StringComparison.Ordinal) ||
                              (string.Equals(showCalendar, "auto", StringComparison.Ordinal) && isNonIsoCalendar);

        // Include reference day for non-ISO calendar or when calendar annotation is shown
        var includeDay = isNonIsoCalendar || includeCalendar;

        var yearStr = TemporalHelpers.PadIsoYear(ym.IsoDate.Year);

        string result;
        if (includeDay)
        {
            if (includeCalendar)
            {
                // Add critical flag (!) if showCalendar is "critical"
                var criticalFlag = string.Equals(showCalendar, "critical", StringComparison.Ordinal) ? "!" : "";
                result = $"{yearStr}-{ym.IsoDate.Month:D2}-{ym.IsoDate.Day:D2}[{criticalFlag}u-ca={ym.Calendar}]";
            }
            else
            {
                result = $"{yearStr}-{ym.IsoDate.Month:D2}-{ym.IsoDate.Day:D2}";
            }
        }
        else
        {
            result = $"{yearStr}-{ym.IsoDate.Month:D2}";
        }

        return new JsString(result);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.tojson
    /// </summary>
    [JsFunction(Length = 0)]
    private JsString ToJSON(JsValue thisObject)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var yearStr = TemporalHelpers.PadIsoYear(ym.IsoDate.Year);
        // toJSON uses "auto" for calendarName: include day+calendar for non-ISO
        if (!string.Equals(ym.Calendar, "iso8601", StringComparison.Ordinal))
        {
            return new JsString($"{yearStr}-{ym.IsoDate.Month:D2}-{ym.IsoDate.Day:D2}[u-ca={ym.Calendar}]");
        }

        return new JsString($"{yearStr}-{ym.IsoDate.Month:D2}");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sup-temporal.plainyearmonth.prototype.tolocalestring
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue ToLocaleString(JsValue thisObject, JsValue locales, JsValue options)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        // Per spec: CreateDateTimeFormat with required=~date~, defaults=~date~
        // But for PlainYearMonth, we use year-month specific defaults (no day)
        var dtf = _realm.Intrinsics.DateTimeFormat.CreateDateTimeFormat(
            locales, options, required: Intl.DateTimeRequired.Date, defaults: Intl.DateTimeDefaults.YearMonth);

        // Calendar mismatch check: PlainYearMonth calendar must match DTF calendar exactly
        var cal = ym.Calendar;
        if (dtf.Calendar != null && !string.Equals(cal, dtf.Calendar, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, $"Calendar mismatch: PlainYearMonth uses '{cal}' but DateTimeFormat uses '{dtf.Calendar}'");
        }

        return dtf.Format(new DateTime(ym.IsoDate.Year, ym.IsoDate.Month, ym.IsoDate.Day, 12, 0, 0, DateTimeKind.Unspecified), isPlain: true);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.valueof
    /// </summary>
    [JsFunction(Length = 0)]
    private JsValue ValueOf(JsValue thisObject)
    {
        Throw.TypeError(_realm, "Temporal.PlainYearMonth cannot be converted to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.toplaindate
    /// </summary>
    [JsFunction(Length = 1)]
    private JsPlainDate ToPlainDate(JsValue thisObject, JsValue item)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        if (!item.IsObject())
        {
            Throw.TypeError(_realm, "toPlainDate requires an object argument");
        }

        var obj = item.AsObject();
        var dayProp = obj.Get("day");
        if (dayProp.IsUndefined())
        {
            Throw.TypeError(_realm, "day is required");
        }

        var day = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayProp);

        IsoDate? date;
        if (NonIsoCalendars.IsNonIsoCalendar(ym.Calendar))
        {
            // For non-ISO calendars, get the calendar year/month and combine with the provided day
            var calYear = TemporalHelpers.CalendarYear(ym.Calendar, ym.IsoDate, _engine);
            var calMonth = TemporalHelpers.CalendarMonth(ym.Calendar, ym.IsoDate, _engine);
            var calMonthCode = TemporalHelpers.CalendarMonthCode(ym.Calendar, ym.IsoDate, _engine);
            date = TemporalHelpers.CalendarDateToISO(_realm, ym.Calendar, calYear, calMonth, day, "constrain", calMonthCode);
        }
        else
        {
            date = TemporalHelpers.RegulateIsoDate(ym.IsoDate.Year, ym.IsoDate.Month, day, "constrain");
        }

        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        return _engine.Realm.Intrinsics.TemporalPlainDate.Construct(date.Value, ym.Calendar);
    }

    private DurationRecord ToDurationRecord(JsValue value)
    {
        if (value is JsDuration duration)
        {
            return duration.DurationRecord;
        }

        if (value.IsString())
        {
            var parsed = TemporalHelpers.ParseIsoDuration(value.ToString());
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid duration string");
            }
            return parsed.Value;
        }

        if (value.IsObject())
        {
            var obj = value.AsObject();
            // Read properties in alphabetical order per spec
            var hasAny = false;
            var days = GetDurationProperty(obj, "days", ref hasAny);
            var hours = GetDurationProperty(obj, "hours", ref hasAny);
            var microseconds = GetDurationProperty(obj, "microseconds", ref hasAny);
            var milliseconds = GetDurationProperty(obj, "milliseconds", ref hasAny);
            var minutes = GetDurationProperty(obj, "minutes", ref hasAny);
            var months = GetDurationProperty(obj, "months", ref hasAny);
            var nanoseconds = GetDurationProperty(obj, "nanoseconds", ref hasAny);
            var seconds = GetDurationProperty(obj, "seconds", ref hasAny);
            var weeks = GetDurationProperty(obj, "weeks", ref hasAny);
            var years = GetDurationProperty(obj, "years", ref hasAny);

            // At least one property must be defined
            if (!hasAny)
            {
                Throw.TypeError(_realm, "Duration object must have at least one temporal property");
            }

            var record = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);

            if (!TemporalHelpers.IsValidDuration(record))
            {
                Throw.RangeError(_realm, "Invalid duration");
            }

            return record;
        }

        Throw.TypeError(_realm, "Invalid duration");
        return default;
    }

    private double GetDurationProperty(ObjectInstance obj, string name, ref bool hasAny)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return 0;

        hasAny = true;
        return TemporalHelpers.ToIntegerIfIntegral(_realm, value);
    }

    private string GetCalendarNameOption(ObjectInstance? options)
    {
        if (options is null)
            return "auto";

        var calendarName = options.Get("calendarName");
        if (calendarName.IsUndefined())
            return "auto";

        var value = TypeConverter.ToString(calendarName);
        if (!TemporalHelpers.IsValidCalendarNameOption(value))
        {
            Throw.RangeError(_realm, $"Invalid calendarName option: {value}");
        }
        return value;
    }

}

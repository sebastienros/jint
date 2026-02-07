using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plainyearmonth-prototype-object
/// </summary>
internal sealed class PlainYearMonthPrototype : Prototype
{
    private readonly PlainYearMonthConstructor _constructor;

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
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(22, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["until"] = new(new ClrFunction(Engine, "until", Until, 1, LengthFlags), PropertyFlags),
            ["since"] = new(new ClrFunction(Engine, "since", Since, 1, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToTemporalString, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["toPlainDate"] = new(new ClrFunction(Engine, "toPlainDate", ToPlainDate, 1, LengthFlags), PropertyFlags),
            ["calendarId"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get calendarId", GetCalendarId, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["year"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get year", GetYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["month"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get month", GetMonth, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthCode"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthCode", GetMonthCode, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInMonth"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInMonth", GetDaysInMonth, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInYear", GetDaysInYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthsInYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthsInYear", GetMonthsInYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["inLeapYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get inLeapYear", GetInLeapYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["eraYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get eraYear", GetEraYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["era"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get era", GetEra, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainYearMonth", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsPlainYearMonth ValidatePlainYearMonth(JsValue thisObject)
    {
        if (thisObject is JsPlainYearMonth plainYearMonth)
            return plainYearMonth;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainYearMonth");
        return null!;
    }

    // Getters
    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainYearMonth(thisObject).Calendar);
    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainYearMonth(thisObject).IsoDate.Month:D2}");
    private JsNumber GetDaysInMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.DaysInMonth());
    private JsNumber GetDaysInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainYearMonth(thisObject).IsoDate.DaysInYear());
    private JsNumber GetMonthsInYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainYearMonth(thisObject);
        return JsNumber.Create(12);
    }
    private JsBoolean GetInLeapYear(JsValue thisObject, JsCallArguments arguments) => IsoDate.IsLeapYear(ValidatePlainYearMonth(thisObject).IsoDate.Year) ? JsBoolean.True : JsBoolean.False;
    private JsValue GetEraYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainYearMonth(thisObject);
        return Undefined; // ISO calendar has no era
    }
    private JsValue GetEra(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainYearMonth(thisObject);
        return Undefined; // ISO calendar has no era
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.with
    /// </summary>
    private JsPlainYearMonth With(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var temporalYearMonthLike = arguments.At(0);
        var options = arguments.At(1);

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
        var year = ym.IsoDate.Year;
        var month = ym.IsoDate.Month;

        var monthProp = obj.Get("month");
        if (!monthProp.IsUndefined())
        {
            month = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthProp);
        }

        var monthCodeProp = obj.Get("monthCode");
        string? monthCode = null;
        if (!monthCodeProp.IsUndefined())
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

        var yearProp = obj.Get("year");
        if (!yearProp.IsUndefined())
        {
            year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearProp);
        }

        // Validate that at least one temporal field was provided (IsPartialTemporalObject)
        if (monthProp.IsUndefined() && monthCodeProp.IsUndefined() && yearProp.IsUndefined())
        {
            Throw.TypeError(_realm, "with argument must have at least one temporal property");
        }

        // Read options BEFORE any validation (per spec)
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        // NOW validate monthCode (after options are read)
        int? parsedMonthFromCode = null;
        if (monthCode is not null)
        {
            // Use ParseMonthCode for proper validation (well-formedness and range)
            parsedMonthFromCode = TemporalHelpers.ParseMonthCode(_realm, monthCode);

            // For ISO 8601 calendar: validate monthCode is valid (01-12, no leap months)
            if (monthCode.Length == 4 && monthCode[3] == 'L')
            {
                Throw.RangeError(_realm, $"Leap months are not valid for ISO 8601 calendar: {monthCode}");
            }
            if (parsedMonthFromCode.Value < 1 || parsedMonthFromCode.Value > 12)
            {
                Throw.RangeError(_realm, $"Month {parsedMonthFromCode.Value} is not valid for ISO 8601 calendar");
            }
        }

        // Validate month/monthCode consistency
        if (parsedMonthFromCode.HasValue && !monthProp.IsUndefined() && month != parsedMonthFromCode.Value)
        {
            Throw.RangeError(_realm, "month and monthCode must match");
        }

        // Use monthCode if provided
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
    private JsPlainYearMonth Add(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var duration = ToDurationRecord(arguments.At(0));
        var options = arguments.At(1);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        return AddDurationToYearMonth(ym, duration, overflow);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.subtract
    /// </summary>
    private JsPlainYearMonth Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var duration = ToDurationRecord(arguments.At(0));
        var options = arguments.At(1);
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

        // Step 8-9: Set day=1 and validate the intermediate date is within range
        var intermediateDate = new IsoDate(ym.IsoDate.Year, ym.IsoDate.Month, 1);
        TemporalHelpers.CheckISODaysRange(_realm, intermediateDate);

        // Add years and months
        var currentYear = ym.IsoDate.Year + (int) duration.Years;
        var currentMonth = ym.IsoDate.Month + (int) duration.Months;

        // Normalize months
        while (currentMonth > 12)
        {
            currentMonth -= 12;
            currentYear++;
        }
        while (currentMonth < 1)
        {
            currentMonth += 12;
            currentYear--;
        }

        // Validate the result is within Temporal's representable range
        if (!TemporalHelpers.ISOYearMonthWithinLimits(currentYear, currentMonth))
        {
            Throw.RangeError(_realm, "Year-month result is outside the representable range");
        }

        return _constructor.Construct(new IsoDate(currentYear, currentMonth, 1), ym.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.until
    /// </summary>
    private JsDuration Until(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var other = _constructor.ToTemporalYearMonth(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // Only year and month units allowed for PlainYearMonth
        var yearMonthUnits = new[] { "year", "month" };

        // GetDifferenceSettings reads options in correct order per spec
        var fallbackSmallestUnit = "month";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
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

        return DifferenceYearMonth(ym, other, largestUnit, settings.SmallestUnit, settings.RoundingMode, settings.RoundingIncrement, negate: false);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.since
    /// </summary>
    private JsDuration Since(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var other = _constructor.ToTemporalYearMonth(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // Only year and month units allowed for PlainYearMonth
        var yearMonthUnits = new[] { "year", "month" };

        // GetDifferenceSettings reads options in correct order per spec
        // and negates rounding mode for "since" operation
        var fallbackSmallestUnit = "month";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
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

        return DifferenceYearMonth(ym, other, largestUnit, settings.SmallestUnit, settings.RoundingMode, settings.RoundingIncrement, negate: true);
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

        // Step 7-10: Convert both PlainYearMonth to PlainDate with day=1
        var thisDate = new IsoDate(ym1.IsoDate.Year, ym1.IsoDate.Month, 1);
        var otherDate = new IsoDate(ym2.IsoDate.Year, ym2.IsoDate.Month, 1);

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
    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var other = _constructor.ToTemporalYearMonth(arguments.At(0), "constrain");

        return ym.IsoDate.Year == other.IsoDate.Year &&
               ym.IsoDate.Month == other.IsoDate.Month &&
               ym.IsoDate.Day == other.IsoDate.Day &&
               string.Equals(ym.Calendar, other.Calendar, StringComparison.Ordinal)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.tostring
    /// </summary>
    private JsString ToTemporalString(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var optionsValue = arguments.At(0);
        var options = TemporalHelpers.GetOptionsObject(_realm, optionsValue);
        var showCalendar = GetCalendarNameOption(options);

        // Include the reference day when calendar is shown
        // Per spec: YYYY-MM-DD format with calendar annotation
        var includeDay = string.Equals(showCalendar, "always", StringComparison.Ordinal) ||
                         string.Equals(showCalendar, "critical", StringComparison.Ordinal) ||
                         (string.Equals(showCalendar, "auto", StringComparison.Ordinal) &&
                          !string.Equals(ym.Calendar, "iso8601", StringComparison.Ordinal));

        var year = ym.IsoDate.Year;
        var yearStr = (year < 0 || year > 9999)
            ? $"{(year >= 0 ? '+' : '-')}{System.Math.Abs(year):D6}"
            : $"{year:D4}";

        string result;
        if (includeDay)
        {
            // Add critical flag (!) if showCalendar is "critical"
            var criticalFlag = string.Equals(showCalendar, "critical", StringComparison.Ordinal) ? "!" : "";
            result = $"{yearStr}-{ym.IsoDate.Month:D2}-{ym.IsoDate.Day:D2}[{criticalFlag}u-ca={ym.Calendar}]";
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
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var year = ym.IsoDate.Year;
        var yearStr = (year < 0 || year > 9999)
            ? $"{(year >= 0 ? '+' : '-')}{System.Math.Abs(year):D6}"
            : $"{year:D4}";
        return new JsString($"{yearStr}-{ym.IsoDate.Month:D2}");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.tolocalestring
    /// </summary>
    private JsString ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        // For now, just return ISO format
        var year = ym.IsoDate.Year;
        var yearStr = (year < 0 || year > 9999)
            ? $"{(year >= 0 ? '+' : '-')}{System.Math.Abs(year):D6}"
            : $"{year:D4}";
        return new JsString($"{yearStr}-{ym.IsoDate.Month:D2}");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainYearMonth cannot be converted to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.prototype.toplaindate
    /// </summary>
    private JsPlainDate ToPlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        var ym = ValidatePlainYearMonth(thisObject);
        var item = arguments.At(0);

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
        var date = TemporalHelpers.RegulateIsoDate(ym.IsoDate.Year, ym.IsoDate.Month, day, "constrain");
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

using System.Globalization;
using System.Text;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindate-prototype-object
/// </summary>
internal sealed class PlainDatePrototype : Prototype
{
    private readonly PlainDateConstructor _constructor;

    internal PlainDatePrototype(
        Engine engine,
        Realm realm,
        PlainDateConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(32, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["withCalendar"] = new(new ClrFunction(Engine, "withCalendar", WithCalendar, 1, LengthFlags), PropertyFlags),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["until"] = new(new ClrFunction(Engine, "until", Until, 1, LengthFlags), PropertyFlags),
            ["since"] = new(new ClrFunction(Engine, "since", Since, 1, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToString, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["toPlainDateTime"] = new(new ClrFunction(Engine, "toPlainDateTime", ToPlainDateTime, 0, LengthFlags), PropertyFlags),
            ["toPlainYearMonth"] = new(new ClrFunction(Engine, "toPlainYearMonth", ToPlainYearMonth, 0, LengthFlags), PropertyFlags),
            ["toPlainMonthDay"] = new(new ClrFunction(Engine, "toPlainMonthDay", ToPlainMonthDay, 0, LengthFlags), PropertyFlags),
            ["toZonedDateTime"] = new(new ClrFunction(Engine, "toZonedDateTime", ToZonedDateTime, 1, LengthFlags), PropertyFlags),
            ["calendarId"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get calendarId", GetCalendarId, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["era"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get era", GetEra, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["eraYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get eraYear", GetEraYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["year"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get year", GetYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["month"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get month", GetMonth, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthCode"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthCode", GetMonthCode, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["day"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get day", GetDay, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["dayOfWeek"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get dayOfWeek", GetDayOfWeek, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["dayOfYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get dayOfYear", GetDayOfYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["weekOfYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get weekOfYear", GetWeekOfYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["yearOfWeek"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get yearOfWeek", GetYearOfWeek, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInWeek"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInWeek", GetDaysInWeek, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInMonth"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInMonth", GetDaysInMonth, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInYear", GetDaysInYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthsInYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthsInYear", GetMonthsInYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["inLeapYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get inLeapYear", GetInLeapYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainDate", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsPlainDate ValidatePlainDate(JsValue thisObject)
    {
        if (thisObject is JsPlainDate plainDate)
            return plainDate;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainDate");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainDate(thisObject).Calendar);

    private JsValue GetEra(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDate(thisObject);
        return Undefined;
    }

    private JsValue GetEraYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDate(thisObject);
        return Undefined;
    }

    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainDate(thisObject).IsoDate.Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.Day);
    private JsNumber GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DayOfWeek());
    private JsNumber GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DayOfYear());
    private JsNumber GetWeekOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.WeekOfYear());
    private JsNumber GetYearOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.YearOfWeek());

    private JsNumber GetDaysInWeek(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDate(thisObject);
        return JsNumber.Create(7);
    }

    private JsNumber GetDaysInMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DaysInMonth());
    private JsNumber GetDaysInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDate(thisObject).IsoDate.DaysInYear());

    private JsNumber GetMonthsInYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDate(thisObject);
        return JsNumber.Create(12);
    }

    private JsBoolean GetInLeapYear(JsValue thisObject, JsCallArguments arguments) => IsoDate.IsLeapYear(ValidatePlainDate(thisObject).IsoDate.Year) ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.with
    /// </summary>
    private JsPlainDate With(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var temporalDateLike = arguments.At(0);
        var optionsArg = arguments.At(1);

        if (!temporalDateLike.IsObject())
        {
            Throw.TypeError(_realm, "Temporal date-like must be an object");
        }

        var obj = temporalDateLike.AsObject();

        // RejectObjectWithCalendarOrTimeZone
        // Step 1: Check for Temporal types with internal slots
        if (obj is JsPlainDate or JsPlainDateTime or JsPlainMonthDay or JsPlainTime or JsPlainYearMonth or JsZonedDateTime)
        {
            Throw.TypeError(_realm, "Argument cannot be a Temporal object with calendar or time zone");
        }

        // Steps 2-3: Check for calendar property
        var calendarProp = obj.Get("calendar");
        if (!calendarProp.IsUndefined())
        {
            Throw.TypeError(_realm, "Temporal date-like must not have a calendar property");
        }

        // Steps 4-5: Check for timeZone property
        var timeZoneProp = obj.Get("timeZone");
        if (!timeZoneProp.IsUndefined())
        {
            Throw.TypeError(_realm, "Temporal date-like must not have a timeZone property");
        }

        // PrepareTemporalFields - read properties in alphabetical order: day, month, monthCode, year
        var dayValue = obj.Get("day");
        int? day = dayValue.IsUndefined() ? null : TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayValue);

        var monthValue = obj.Get("month");
        int? month = monthValue.IsUndefined() ? null : TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthValue);

        var monthCodeValue = obj.Get("monthCode");
        string? monthCode = null;
        if (!monthCodeValue.IsUndefined())
        {
            // monthCode must be a string (per spec)
            // Handle objects specially: call ToPrimitive and ensure result is a string
            if (monthCodeValue.IsObject())
            {
                var primitive = TypeConverter.ToPrimitive(monthCodeValue, Types.String);
                if (!primitive.IsString())
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }

                monthCode = primitive.ToString();
            }
            else if (monthCodeValue.IsString())
            {
                monthCode = TypeConverter.ToString(monthCodeValue);
            }
            else
            {
                // Number, BigInt, Boolean, Null - reject; Symbol throws from ToString
                if (monthCodeValue.Type != Types.Symbol)
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }

                monthCode = TypeConverter.ToString(monthCodeValue);
            }
        }

        var yearValue = obj.Get("year");
        int? year = yearValue.IsUndefined() ? null : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearValue);

        // At least one property must be present
        if (day is null && month is null && monthCode is null && year is null)
        {
            Throw.TypeError(_realm, "Temporal date-like object must have at least one temporal property");
        }

        // Read options BEFORE any validation (per spec: abstractops.html)
        // All options must be read before algorithmic validation
        var options = GetOptionsObject(optionsArg);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, (JsValue?) options ?? JsValue.Undefined);

        // Merge with existing date values
        var finalYear = year ?? plainDate.IsoDate.Year;
        var finalMonth = month ?? plainDate.IsoDate.Month;
        var finalDay = day ?? plainDate.IsoDate.Day;

        // Handle monthCode - if provided, validate and use it
        // Validation happens AFTER options are read
        int? parsedMonthCode = null;
        if (monthCode is not null)
        {
            // Use ParseMonthCode for proper validation (well-formedness and range)
            parsedMonthCode = TemporalHelpers.ParseMonthCode(_realm, monthCode);

            // For ISO 8601 calendar: validate monthCode is valid (01-12, no leap months)
            if (monthCode.Length == 4 && monthCode[3] == 'L')
            {
                Throw.RangeError(_realm, $"Leap months are not valid for ISO 8601 calendar: {monthCode}");
            }

            if (parsedMonthCode.Value < 1 || parsedMonthCode.Value > 12)
            {
                Throw.RangeError(_realm, $"Month {parsedMonthCode.Value} is not valid for ISO 8601 calendar");
            }
        }

        // Validate: both month and monthCode provided - they must match
        if (month.HasValue && parsedMonthCode.HasValue && month.Value != parsedMonthCode.Value)
        {
            Throw.RangeError(_realm, "month and monthCode must match");
        }

        // Use monthCode if provided, otherwise use month
        if (parsedMonthCode.HasValue)
        {
            finalMonth = parsedMonthCode.Value;
        }

        // Check for fundamentally invalid values
        // - Year out of supported range (-271821 to 275760)
        // - Month or day <= 0 (negative or zero)
        // These throw RangeError even with overflow="constrain"
        // But allow constrainable values like month=13 or day=32
        if (finalYear < -271821 || finalYear > 275760 || finalMonth < 1 || finalDay < 1)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        // Apply regulation with user's overflow option
        var date = TemporalHelpers.RegulateIsoDate(finalYear, finalMonth, finalDay, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        return _constructor.Construct(date.Value, plainDate.Calendar);
    }


    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.withcalendar
    /// </summary>
    private JsPlainDate WithCalendar(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var calendarArg = arguments.At(0);
        var calendar = ToTemporalCalendarIdentifier(calendarArg);

        if (!string.Equals(calendar, "iso8601", StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, $"Unsupported calendar: {calendar}");
        }

        return _constructor.Construct(plainDate.IsoDate, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.add
    /// </summary>
    private JsPlainDate Add(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var temporalDurationLike = arguments.At(0);
        var optionsArg = arguments.At(1);
        var duration = ToTemporalDurationRecord(temporalDurationLike);
        var options = GetOptionsObject(optionsArg);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, (JsValue?) options ?? JsValue.Undefined);
        return AddDurationToDate(plainDate, duration, overflow);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.subtract
    /// </summary>
    private JsPlainDate Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var temporalDurationLike = arguments.At(0);
        var optionsArg = arguments.At(1);
        var duration = ToTemporalDurationRecord(temporalDurationLike);
        var options = GetOptionsObject(optionsArg);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, (JsValue?) options ?? JsValue.Undefined);

        // Negate the duration
        var negated = new DurationRecord(
            -duration.Years, -duration.Months, -duration.Weeks, -duration.Days,
            -duration.Hours, -duration.Minutes, -duration.Seconds,
            -duration.Milliseconds, -duration.Microseconds, -duration.Nanoseconds);

        return AddDurationToDate(plainDate, negated, overflow);
    }

    private JsPlainDate AddDurationToDate(JsPlainDate plainDate, DurationRecord duration, string overflow)
    {
        var newDate = TemporalHelpers.AddDurationToDate(plainDate.IsoDate, duration);

        var regulated = TemporalHelpers.RegulateIsoDate(newDate.Year, newDate.Month, newDate.Day, overflow);
        if (regulated is null)
        {
            Throw.RangeError(_realm, "Invalid date after addition");
        }

        return _constructor.Construct(regulated.Value, plainDate.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.until
    /// </summary>
    private JsDuration Until(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var other = _constructor.ToTemporalDate(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // Date units for PlainDate operations (no time units allowed)
        var dateUnits = new[] { "year", "month", "week", "day" };

        // GetDifferenceSettings reads options in correct order per spec
        var fallbackSmallestUnit = "day";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            "until",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            dateUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "day")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "day");
        }

        ValidateUnitRange(largestUnit, settings.SmallestUnit);

        return DifferenceTemporalPlainDate(plainDate, other, largestUnit, settings.SmallestUnit, settings.RoundingIncrement, settings.RoundingMode, negate: false);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.since
    /// </summary>
    private JsDuration Since(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var other = _constructor.ToTemporalDate(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // Date units for PlainDate operations (no time units allowed)
        var dateUnits = new[] { "year", "month", "week", "day" };

        // GetDifferenceSettings reads options in correct order per spec
        // and negates rounding mode for "since" operation
        var fallbackSmallestUnit = "day";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            "since",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            dateUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "day")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "day");
        }

        ValidateUnitRange(largestUnit, settings.SmallestUnit);

        return DifferenceTemporalPlainDate(plainDate, other, largestUnit, settings.SmallestUnit, settings.RoundingIncrement, settings.RoundingMode, negate: true);
    }

    /// <summary>
    /// DifferenceTemporalPlainDate ( operation, temporalDate, other, options )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencetemporalplaindate
    /// </summary>
    private JsDuration DifferenceTemporalPlainDate(JsPlainDate temporalDate, JsPlainDate other, string largestUnit, string smallestUnit, double roundingIncrement, string roundingMode, bool negate)
    {
        // Step 5: If equal, return zero duration
        if (TemporalHelpers.CompareIsoDates(temporalDate.IsoDate, other.IsoDate) == 0)
        {
            return CreateDuration(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // Step 6: Get date difference using calendar
        var dateDifference = TemporalHelpers.CalendarDateUntil(temporalDate.Calendar, temporalDate.IsoDate, other.IsoDate, largestUnit);

        // Step 7-10: If rounding needed, use RoundRelativeDuration
        DurationRecord result;
        if (!string.Equals(smallestUnit, "day", StringComparison.Ordinal) || roundingIncrement != 1)
        {
            // Combine date duration with zero time duration
            var duration = TemporalHelpers.CombineDateAndTimeDuration(dateDifference, 0);

            // Create ISO DateTime records for start and end
            var isoDateTime = new IsoDateTime(temporalDate.IsoDate, IsoTime.Midnight);
            var isoDateTimeOther = new IsoDateTime(other.IsoDate, IsoTime.Midnight);

            // Get epoch nanoseconds
            var originEpochNs = TemporalHelpers.GetUTCEpochNanoseconds(isoDateTime);
            var destEpochNs = TemporalHelpers.GetUTCEpochNanoseconds(isoDateTimeOther);

            // Round the duration
            var roundedDuration = TemporalHelpers.RoundRelativeDuration(
                _realm,
                null, // timeZoneProvider = null for PlainDate
                duration,
                originEpochNs,
                destEpochNs,
                isoDateTime,
                null, // timeZone = ~unset~
                temporalDate.Calendar,
                largestUnit,
                (int) roundingIncrement,
                smallestUnit,
                roundingMode);

            // Convert from internal duration
            result = TemporalHelpers.TemporalDurationFromInternal(
                _realm,
                roundedDuration.Years,
                roundedDuration.Months,
                roundedDuration.Weeks,
                roundedDuration.Days,
                0, // no time component
                "day");
        }
        else
        {
            // No rounding needed, use date difference directly
            result = dateDifference;
        }

        // Step 11: If operation is "since", negate the result
        // Use NoNegativeZero to ensure -0 becomes +0 (spec: CreateNegatedTemporalDuration)
        if (negate)
        {
            result = new DurationRecord(
                TemporalHelpers.NoNegativeZero(-result.Years),
                TemporalHelpers.NoNegativeZero(-result.Months),
                TemporalHelpers.NoNegativeZero(-result.Weeks),
                TemporalHelpers.NoNegativeZero(-result.Days),
                0, 0, 0, 0, 0, 0); // No time components for PlainDate
        }

        return CreateDuration(result.Years, result.Months, result.Weeks, result.Days, 0, 0, 0, 0, 0, 0);
    }


    private JsDuration CreateDuration(double years, double months, double weeks, double days,
        double hours, double minutes, double seconds, double ms, double us, double ns)
    {
        var record = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, ms, us, ns);
        return new JsDuration(_engine, _realm.Intrinsics.TemporalDuration.PrototypeObject, record);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.equals
    /// </summary>
    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var other = _constructor.ToTemporalDate(arguments.At(0), "constrain");
        var result = TemporalHelpers.CompareIsoDates(plainDate.IsoDate, other.IsoDate) == 0 &&
                     string.Equals(plainDate.Calendar, other.Calendar, StringComparison.Ordinal);
        return result ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.tostring
    /// </summary>
    private JsString ToString(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var optionsValue = arguments.At(0);

        var calendarName = "auto";

        if (!optionsValue.IsUndefined())
        {
            var options = TemporalHelpers.GetOptionsObject(_realm, optionsValue)!;

            // Read calendarName option (in alphabetical order for order-of-operations)
            var calendarNameValue = options.Get("calendarName");
            if (!calendarNameValue.IsUndefined())
            {
                calendarName = TypeConverter.ToString(calendarNameValue);
                if (!TemporalHelpers.IsValidCalendarNameOption(calendarName))
                {
                    Throw.RangeError(_realm, $"Invalid calendarName option: {calendarName}");
                }
            }
        }

        return new JsString(FormatDate(plainDate.IsoDate, plainDate.Calendar, calendarName));
    }


    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.tojson
    /// </summary>
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        return new JsString(FormatDate(plainDate.IsoDate, plainDate.Calendar));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.tolocalestring
    /// </summary>
    private JsString ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        return new JsString(FormatDate(plainDate.IsoDate, plainDate.Calendar));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDate cannot be converted to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.toplaindatetime
    /// </summary>
    private JsPlainDateTime ToPlainDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var temporalTime = arguments.At(0);

        IsoTime time;
        if (temporalTime.IsUndefined())
        {
            time = new IsoTime(0, 0, 0, 0, 0, 0);
        }
        else
        {
            var plainTime = _realm.Intrinsics.TemporalPlainTime.ToTemporalTime(temporalTime, "constrain");
            time = plainTime.IsoTime;
        }

        var dateTime = new IsoDateTime(plainDate.IsoDate, time);

        if (!TemporalHelpers.IsValidIsoDateTime(
                dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Second,
                dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond))
        {
            Throw.RangeError(_realm, "Resulting PlainDateTime is outside the representable range");
        }

        return new JsPlainDateTime(_engine, _realm.Intrinsics.TemporalPlainDateTime.PrototypeObject, dateTime, plainDate.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.toplainyearmonth
    /// </summary>
    private JsPlainYearMonth ToPlainYearMonth(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        // Use the first day of month for the reference day
        var isoDate = new IsoDate(plainDate.IsoDate.Year, plainDate.IsoDate.Month, 1);
        return new JsPlainYearMonth(_engine, _realm.Intrinsics.TemporalPlainYearMonth.PrototypeObject,
            isoDate, plainDate.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.toplainmonthday
    /// </summary>
    private JsPlainMonthDay ToPlainMonthDay(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        // Use a reference year (1972 is a leap year, so it has Feb 29)
        var isoDate = new IsoDate(1972, plainDate.IsoDate.Month, plainDate.IsoDate.Day);
        return new JsPlainMonthDay(_engine, _realm.Intrinsics.TemporalPlainMonthDay.PrototypeObject,
            isoDate, plainDate.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.prototype.tozoneddatetime
    /// </summary>
    private JsZonedDateTime ToZonedDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDate = ValidatePlainDate(thisObject);
        var item = arguments.At(0);

        string timeZone;
        JsValue plainTimeValue;

        // If item is a string, it's the time zone and we use midnight
        if (item.IsString())
        {
            timeZone = TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, item);
            plainTimeValue = Undefined;
        }
        else if (item.IsObject())
        {
            var obj = item.AsObject();

            // Get timeZone first (per spec order)
            var timeZoneValue = obj.Get("timeZone");
            if (timeZoneValue.IsUndefined())
            {
                Throw.TypeError(_realm, "Missing required property: timeZone");
            }

            timeZone = TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, timeZoneValue);

            // Then get plainTime
            plainTimeValue = obj.Get("plainTime");
        }
        else
        {
            Throw.TypeError(_realm, "Invalid argument");
            return null!;
        }

        // Convert plainTime to IsoTime (or use midnight)
        IsoTime time;
        if (plainTimeValue.IsUndefined())
        {
            time = new IsoTime(0, 0, 0, 0, 0, 0);
        }
        else
        {
            var plainTime = _realm.Intrinsics.TemporalPlainTime.ToTemporalTime(plainTimeValue, "constrain");
            time = plainTime.IsoTime;
        }

        // Combine date and time
        var dateTime = new IsoDateTime(plainDate.IsoDate, time);

        // Get the epoch nanoseconds for this date-time in the given time zone
        var epochNs = GetEpochFromDateTime(dateTime, timeZone, "compatible");

        if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
        {
            Throw.RangeError(_realm, "Resulting ZonedDateTime is outside the valid range");
        }

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject,
            epochNs, timeZone, plainDate.Calendar);
    }

    private System.Numerics.BigInteger GetEpochFromDateTime(IsoDateTime dateTime, string timeZone, string disambiguation)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        var localNs = IsoDateTimeToNanoseconds(dateTime);

        var possibleInstants = provider.GetPossibleInstantsFor(timeZone,
            dateTime.Year, dateTime.Month, dateTime.Day,
            dateTime.Hour, dateTime.Minute, dateTime.Second,
            dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond);

        if (possibleInstants.Length == 1)
        {
            return possibleInstants[0];
        }

        if (possibleInstants.Length == 0)
        {
            // Gap - use disambiguation
            var before = provider.GetPossibleInstantsFor(timeZone,
                dateTime.Year, dateTime.Month, dateTime.Day,
                0, 0, 0, 0, 0, 0);
            if (before.Length > 0)
            {
                var instant = before[before.Length - 1];
                var offsetBefore = provider.GetOffsetNanosecondsFor(timeZone, instant);
                var offsetAfter = provider.GetOffsetNanosecondsFor(timeZone, instant + 1);

                switch (disambiguation)
                {
                    case "compatible":
                    case "later":
                        return localNs - offsetAfter;
                    case "earlier":
                        return localNs - offsetBefore;
                    case "reject":
                        Throw.RangeError(_realm, "No such time exists in this time zone (gap)");
                        break;
                }
            }

            // Fallback
            return localNs;
        }

        // Ambiguous (fold/overlap)
        switch (disambiguation)
        {
            case "compatible":
            case "earlier":
                return possibleInstants[0];
            case "later":
                return possibleInstants[possibleInstants.Length - 1];
            case "reject":
                Throw.RangeError(_realm, "Time is ambiguous in this time zone (fold)");
                break;
        }

        return possibleInstants[0];
    }

    private static System.Numerics.BigInteger IsoDateTimeToNanoseconds(IsoDateTime dateTime)
    {
        var days = TemporalHelpers.IsoDateToDays(dateTime.Year, dateTime.Month, dateTime.Day);
        var timeNs = dateTime.Time.TotalNanoseconds();
        return (System.Numerics.BigInteger) days * TemporalHelpers.NanosecondsPerDay + timeNs;
    }

    private static string FormatDate(IsoDate date, string calendar, string calendarName = "auto")
    {
        var sb = new ValueStringBuilder();

        if (date.Year < 0 || date.Year > 9999)
        {
            sb.Append(date.Year >= 0 ? '+' : '-');
            sb.Append(System.Math.Abs(date.Year).ToString("D6", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(date.Year.ToString("D4", CultureInfo.InvariantCulture));
        }

        sb.Append('-');
        sb.Append(date.Month.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('-');
        sb.Append(date.Day.ToString("D2", CultureInfo.InvariantCulture));

        // Add calendar annotation based on calendarName option
        var showCalendar = calendarName switch
        {
            "always" => true,
            "never" => false,
            "critical" => true,
            "auto" => !string.Equals(calendar, "iso8601", StringComparison.Ordinal),
            _ => !string.Equals(calendar, "iso8601", StringComparison.Ordinal)
        };

        if (showCalendar)
        {
            sb.Append('[');
            if (string.Equals(calendarName, "critical", StringComparison.Ordinal))
            {
                sb.Append('!');
            }

            sb.Append("u-ca=");
            sb.Append(calendar);
            sb.Append(']');
        }

        return sb.ToString();
    }

    private DurationRecord ToTemporalDurationRecord(JsValue value)
    {
        var duration = _realm.Intrinsics.TemporalDuration.ToTemporalDuration(value);
        return duration.DurationRecord;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-totemporalcalendaridentifier
    /// </summary>
    private string ToTemporalCalendarIdentifier(JsValue value)
    {
        // ToTemporalCalendarIdentifier only accepts strings
        if (!value.IsString())
        {
            // Check if it's a Temporal object with a calendarId property
            if (value.IsObject())
            {
                var obj = value.AsObject();
                // Check for Temporal types with calendar
                if (obj is JsPlainDate or JsPlainDateTime or JsPlainYearMonth or JsPlainMonthDay or JsZonedDateTime)
                {
                    var calendarId = obj.Get("calendarId");
                    if (calendarId.IsString())
                    {
                        return TypeConverter.ToString(calendarId);
                    }
                }
            }

            Throw.TypeError(_realm, "Calendar must be a string");
        }

        // Use the shared ToTemporalCalendarIdentifier which handles ISO string parsing
        return TemporalHelpers.ToTemporalCalendarIdentifier(_realm, value);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-getoptionsobject
    /// </summary>
    private ObjectInstance? GetOptionsObject(JsValue options)
    {
        if (options.IsUndefined())
            return null;
        if (options.IsObject())
            return options.AsObject();
        Throw.TypeError(_realm, "Options must be an object");
        return null!;
    }

    /// <summary>
    /// Gets a date unit option from the options object. Returns the singular unit name.
    /// Throws RangeError for invalid or non-date units.
    /// </summary>
    private string GetDateUnitOption(ObjectInstance? options, string name, string? defaultValue)
    {
        if (options is null)
        {
            if (defaultValue is null)
            {
                Throw.RangeError(_realm, $"{name} is required");
            }

            return defaultValue!;
        }

        var value = options.Get(name);
        if (value.IsUndefined())
        {
            if (defaultValue is null)
            {
                Throw.RangeError(_realm, $"{name} is required");
            }

            return defaultValue!;
        }

        var str = TypeConverter.ToString(value);
        var unit = TemporalHelpers.ToSingularUnit(str);

        if (!TemporalHelpers.IsValidTemporalUnit(unit))
        {
            Throw.RangeError(_realm, $"Invalid unit: {str}");
        }

        // For PlainDate, only date units are valid (year, month, week, day)
        if (TemporalHelpers.IsValidTimeUnit(unit))
        {
            Throw.RangeError(_realm, $"Invalid date unit: {str}");
        }

        return unit;
    }


    /// <summary>
    /// Validates that largestUnit >= smallestUnit in the temporal unit hierarchy.
    /// </summary>
    private void ValidateUnitRange(string largestUnit, string smallestUnit)
    {
        var largestRank = GetDateUnitRank(largestUnit);
        var smallestRank = GetDateUnitRank(smallestUnit);
        if (largestRank > smallestRank)
        {
            Throw.RangeError(_realm, "largestUnit must be larger than or equal to smallestUnit");
        }
    }

    private static int GetDateUnitRank(string unit)
    {
        return unit switch
        {
            "year" => 0,
            "month" => 1,
            "week" => 2,
            "day" => 3,
            _ => 4
        };
    }
}

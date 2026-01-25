using System.Globalization;
using System.Numerics;
using System.Text;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindatetime-prototype-object
/// </summary>
internal sealed class PlainDateTimePrototype : Prototype
{
    private readonly PlainDateTimeConstructor _constructor;

    internal PlainDateTimePrototype(
        Engine engine,
        Realm realm,
        PlainDateTimeConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(40, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["withPlainDate"] = new(new ClrFunction(Engine, "withPlainDate", WithPlainDate, 1, LengthFlags), PropertyFlags),
            ["withPlainTime"] = new(new ClrFunction(Engine, "withPlainTime", WithPlainTime, 0, LengthFlags), PropertyFlags),
            ["withCalendar"] = new(new ClrFunction(Engine, "withCalendar", WithCalendar, 1, LengthFlags), PropertyFlags),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["until"] = new(new ClrFunction(Engine, "until", Until, 1, LengthFlags), PropertyFlags),
            ["since"] = new(new ClrFunction(Engine, "since", Since, 1, LengthFlags), PropertyFlags),
            ["round"] = new(new ClrFunction(Engine, "round", Round, 1, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToString, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["toPlainDate"] = new(new ClrFunction(Engine, "toPlainDate", ToPlainDate, 0, LengthFlags), PropertyFlags),
            ["toPlainTime"] = new(new ClrFunction(Engine, "toPlainTime", ToPlainTime, 0, LengthFlags), PropertyFlags),
            ["toZonedDateTime"] = new(new ClrFunction(Engine, "toZonedDateTime", ToZonedDateTime, 1, LengthFlags), PropertyFlags),
            ["calendarId"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get calendarId", GetCalendarId, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["era"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get era", GetEra, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["eraYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get eraYear", GetEraYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["year"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get year", GetYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["month"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get month", GetMonth, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthCode"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthCode", GetMonthCode, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["day"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get day", GetDay, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["hour"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get hour", GetHour, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["minute"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get minute", GetMinute, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["second"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get second", GetSecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["millisecond"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get millisecond", GetMillisecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["microsecond"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get microsecond", GetMicrosecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["nanosecond"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get nanosecond", GetNanosecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
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
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainDateTime", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsPlainDateTime ValidatePlainDateTime(JsValue thisObject)
    {
        if (thisObject is JsPlainDateTime plainDateTime)
            return plainDateTime;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainDateTime");
        return null!;
    }

    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainDateTime(thisObject).Calendar);

    private JsValue GetEra(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDateTime(thisObject);
        return Undefined;
    }

    private JsValue GetEraYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDateTime(thisObject);
        return Undefined;
    }

    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Year);
    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Month);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainDateTime(thisObject).IsoDateTime.Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Day);
    private JsNumber GetHour(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Hour);
    private JsNumber GetMinute(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Minute);
    private JsNumber GetSecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Second);
    private JsNumber GetMillisecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Millisecond);
    private JsNumber GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Microsecond);
    private JsNumber GetNanosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Nanosecond);
    private JsNumber GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.DayOfWeek());
    private JsNumber GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.DayOfYear());
    private JsNumber GetWeekOfYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.WeekOfYear());
    private JsNumber GetYearOfWeek(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.YearOfWeek());

    private JsNumber GetDaysInWeek(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDateTime(thisObject);
        return JsNumber.Create(7);
    }

    private JsNumber GetDaysInMonth(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.DaysInMonth());
    private JsNumber GetDaysInYear(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainDateTime(thisObject).IsoDateTime.Date.DaysInYear());

    private JsNumber GetMonthsInYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidatePlainDateTime(thisObject);
        return JsNumber.Create(12);
    }

    private JsBoolean GetInLeapYear(JsValue thisObject, JsCallArguments arguments) => IsoDate.IsLeapYear(ValidatePlainDateTime(thisObject).IsoDateTime.Year) ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.with
    /// </summary>
    private JsPlainDateTime With(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var temporalDateTimeLike = arguments.At(0);
        var optionsArg = arguments.At(1);

        // IsPartialTemporalObject (spec: abstractops.html#sec-temporal-ispartialtemporalobject)
        // Step 1: If value is not an Object, return false
        if (!temporalDateTimeLike.IsObject())
        {
            Throw.TypeError(_realm, "Temporal date-time-like must be an object");
        }

        var obj = temporalDateTimeLike.AsObject();

        // Step 2: If value has Temporal internal slots, return false (reject Temporal objects)
        if (obj is JsPlainDate or JsPlainDateTime or JsPlainMonthDay or JsPlainTime or JsPlainYearMonth or JsZonedDateTime)
        {
            Throw.TypeError(_realm, "Cannot use Temporal object in with()");
        }

        // Step 3: Let calendarProperty be ? Get(value, "calendar")
        var calendarProperty = obj.Get("calendar");
        // Step 4: If calendarProperty is not undefined, return false
        if (!calendarProperty.IsUndefined())
        {
            Throw.TypeError(_realm, "calendar property not supported");
        }

        // Step 5: Let timeZoneProperty be ? Get(value, "timeZone")
        var timeZoneProperty = obj.Get("timeZone");
        // Step 6: If timeZoneProperty is not undefined, return false
        if (!timeZoneProperty.IsUndefined())
        {
            Throw.TypeError(_realm, "timeZone property not supported");
        }

        // PrepareTemporalFields: read and convert each field in alphabetical order
        // Per spec, each field is read (Get) then immediately converted (valueOf/toString)
        var any = false;
        JsValue v;

        v = obj.Get("day");
        int day;
        if (!v.IsUndefined())
        {
            any = true;
            day = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, v);
        }
        else { day = plainDateTime.IsoDateTime.Day; }

        v = obj.Get("hour");
        int hour;
        if (!v.IsUndefined())
        {
            any = true;
            hour = ConvertToInteger(v, "hour");
        }
        else { hour = plainDateTime.IsoDateTime.Hour; }

        v = obj.Get("microsecond");
        int microsecond;
        if (!v.IsUndefined())
        {
            any = true;
            microsecond = ConvertToInteger(v, "microsecond");
        }
        else { microsecond = plainDateTime.IsoDateTime.Microsecond; }

        v = obj.Get("millisecond");
        int millisecond;
        if (!v.IsUndefined())
        {
            any = true;
            millisecond = ConvertToInteger(v, "millisecond");
        }
        else { millisecond = plainDateTime.IsoDateTime.Millisecond; }

        v = obj.Get("minute");
        int minute;
        if (!v.IsUndefined())
        {
            any = true;
            minute = ConvertToInteger(v, "minute");
        }
        else { minute = plainDateTime.IsoDateTime.Minute; }

        v = obj.Get("month");
        int month;
        if (!v.IsUndefined())
        {
            any = true;
            month = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, v);
        }
        else { month = plainDateTime.IsoDateTime.Month; }

        v = obj.Get("monthCode");
        string? monthCode = null;
        if (!v.IsUndefined())
        {
            any = true;
            monthCode = TypeConverter.ToString(v);
        }

        v = obj.Get("nanosecond");
        int nanosecond;
        if (!v.IsUndefined())
        {
            any = true;
            nanosecond = ConvertToInteger(v, "nanosecond");
        }
        else { nanosecond = plainDateTime.IsoDateTime.Nanosecond; }

        v = obj.Get("second");
        int second;
        if (!v.IsUndefined())
        {
            any = true;
            second = ConvertToInteger(v, "second");
        }
        else { second = plainDateTime.IsoDateTime.Second; }

        v = obj.Get("year");
        int year;
        if (!v.IsUndefined())
        {
            any = true;
            year = ConvertToInteger(v, "year");
        }
        else { year = plainDateTime.IsoDateTime.Year; }

        if (!any)
        {
            Throw.TypeError(_realm, "Temporal date-time-like object must have at least one temporal property");
        }

        // Read options AFTER reading fields but BEFORE algorithmic validation
        // https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.with
        var overflow = TemporalHelpers.GetOverflowOption(_realm, optionsArg);

        // Process monthCode (after reading options, before algorithmic validation)
        int? monthFromCode = null;
        if (monthCode is not null)
        {
            if (monthCode.Length >= 2 && monthCode[0] == 'M')
            {
                if (int.TryParse(monthCode.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMonth))
                {
                    monthFromCode = parsedMonth;
                }
            }

            if (!monthFromCode.HasValue)
            {
                Throw.RangeError(_realm, $"Invalid monthCode: {monthCode}");
            }
        }

        // Validate month/monthCode consistency
        if (monthFromCode.HasValue && month != plainDateTime.IsoDateTime.Month && month != monthFromCode.Value)
        {
            Throw.RangeError(_realm, "month and monthCode must match");
        }

        // Use monthCode if provided
        if (monthFromCode.HasValue)
        {
            month = monthFromCode.Value;
        }

        var date = TemporalHelpers.RegulateIsoDate(year, month, day, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, overflow);
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        return _constructor.Construct(new IsoDateTime(date.Value, time.Value), plainDateTime.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.withplaindate
    /// </summary>
    private JsPlainDateTime WithPlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var plainDate = _realm.Intrinsics.TemporalPlainDate.ToTemporalDate(arguments.At(0), "constrain");
        return _constructor.Construct(new IsoDateTime(plainDate.IsoDate, plainDateTime.IsoDateTime.Time), plainDate.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.withplaintime
    /// </summary>
    private JsPlainDateTime WithPlainTime(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var timeArg = arguments.At(0);

        IsoTime time;
        if (timeArg.IsUndefined())
        {
            time = new IsoTime(0, 0, 0, 0, 0, 0);
        }
        else
        {
            var plainTime = _realm.Intrinsics.TemporalPlainTime.ToTemporalTime(timeArg, "constrain");
            time = plainTime.IsoTime;
        }

        return _constructor.Construct(new IsoDateTime(plainDateTime.IsoDateTime.Date, time), plainDateTime.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.withcalendar
    /// </summary>
    private JsPlainDateTime WithCalendar(JsValue thisObject, JsCallArguments arguments)
    {
        // https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.withcalendar
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var calendarArg = arguments.At(0);

        // Use ToTemporalCalendarIdentifier for spec-compliant conversion (handles Temporal objects)
        var calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarArg);

        if (!string.Equals(calendar, "iso8601", StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, $"Unsupported calendar: {calendar}");
        }

        return _constructor.Construct(plainDateTime.IsoDateTime, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.add
    /// </summary>
    private JsPlainDateTime Add(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var temporalDurationLike = arguments.At(0);
        var options = arguments.At(1);
        // Read duration fields first, then options (per spec order)
        var duration = ToDurationRecord(temporalDurationLike);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);
        return AddDurationToDateTime(plainDateTime, duration, overflow);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.subtract
    /// </summary>
    private JsPlainDateTime Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var temporalDurationLike = arguments.At(0);
        var options = arguments.At(1);
        // Read duration fields first, then options (per spec order)
        var duration = ToDurationRecord(temporalDurationLike);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        var negated = new DurationRecord(
            -duration.Years, -duration.Months, -duration.Weeks, -duration.Days,
            -duration.Hours, -duration.Minutes, -duration.Seconds,
            -duration.Milliseconds, -duration.Microseconds, -duration.Nanoseconds);

        return AddDurationToDateTime(plainDateTime, negated, overflow);
    }

    private JsPlainDateTime AddDurationToDateTime(JsPlainDateTime plainDateTime, DurationRecord duration, string overflow)
    {
        // Step 1: Compute time duration as BigInteger nanoseconds to avoid overflow
        var timeDuration = TemporalHelpers.TimeDurationFromComponents(duration);

        // Step 2: Add time duration to current time
        BigInteger timeNs = plainDateTime.IsoDateTime.Time.TotalNanoseconds() + timeDuration;

        // Step 3: Compute day overflow using floor division
        var dayOverflow = TemporalHelpers.FloorDivide(timeNs, TemporalHelpers.NanosecondsPerDay);
        timeNs -= dayOverflow * TemporalHelpers.NanosecondsPerDay;

        // Validate day overflow is within a reasonable range (final validation is in Construct)
        if (dayOverflow > 200_000_002 || dayOverflow < -200_000_002)
        {
            Throw.RangeError(_realm, "Date is outside the valid range");
        }

        // Step 4: Add date part with adjusted days
        var newDate = TemporalHelpers.AddDurationToDateCore(plainDateTime.IsoDateTime.Date, duration, (long) dayOverflow);

        // Step 5: Regulate and validate the result
        var regulated = TemporalHelpers.RegulateIsoDate(newDate.Year, newDate.Month, newDate.Day, overflow);
        if (regulated is null)
        {
            Throw.RangeError(_realm, "Invalid date after addition");
        }

        // Step 6: Construct validates ISODateTimeWithinLimits
        var newTime = IsoTime.FromNanoseconds((long) timeNs);
        return _constructor.Construct(new IsoDateTime(regulated.Value, newTime), plainDateTime.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.until
    /// </summary>
    private JsDuration Until(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var other = _constructor.ToTemporalDateTime(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // All temporal units allowed for PlainDateTime operations
        var allUnits = new[] { "year", "month", "week", "day", "hour", "minute", "second", "millisecond", "microsecond", "nanosecond" };

        // GetDifferenceSettings reads options in correct order per spec
        var fallbackSmallestUnit = "nanosecond";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            "until",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            allUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "day")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "day");
        }

        return DifferenceTemporalPlainDateTime(plainDateTime, other, largestUnit, settings.SmallestUnit, settings.RoundingIncrement, settings.RoundingMode, negate: false);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.since
    /// </summary>
    private JsDuration Since(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var other = _constructor.ToTemporalDateTime(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // All temporal units allowed for PlainDateTime operations
        var allUnits = new[] { "year", "month", "week", "day", "hour", "minute", "second", "millisecond", "microsecond", "nanosecond" };

        // GetDifferenceSettings reads options in correct order per spec
        var fallbackSmallestUnit = "nanosecond";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            "since",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            allUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "day")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "day");
        }

        return DifferenceTemporalPlainDateTime(plainDateTime, other, largestUnit, settings.SmallestUnit, settings.RoundingIncrement, settings.RoundingMode, negate: true);
    }

    /// <summary>
    /// DifferenceTemporalPlainDateTime ( operation, dateTime, other, options )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencetemporalplaindatetime
    /// </summary>
    private JsDuration DifferenceTemporalPlainDateTime(JsPlainDateTime dateTime, JsPlainDateTime other, string largestUnit, string smallestUnit, double roundingIncrement, string roundingMode, bool negate)
    {
        // Step 5: If equal, return zero duration
        if (TemporalHelpers.CompareIsoDateTimes(dateTime.IsoDateTime, other.IsoDateTime) == 0)
        {
            return CreateDuration(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        // Step 6: Get rounded difference
        // DifferencePlainDateTimeWithRounding returns an already-balanced DurationRecord
        var result = TemporalHelpers.DifferencePlainDateTimeWithRounding(
            _realm,
            dateTime.IsoDateTime,
            other.IsoDateTime,
            dateTime.Calendar,
            largestUnit,
            (int) roundingIncrement,
            smallestUnit,
            roundingMode);

        // Step 8: If operation is "since", negate the result
        // Use NoNegativeZero to ensure -0 becomes +0 (spec: CreateNegatedTemporalDuration)
        if (negate)
        {
            result = new DurationRecord(
                TemporalHelpers.NoNegativeZero(-result.Years),
                TemporalHelpers.NoNegativeZero(-result.Months),
                TemporalHelpers.NoNegativeZero(-result.Weeks),
                TemporalHelpers.NoNegativeZero(-result.Days),
                TemporalHelpers.NoNegativeZero(-result.Hours),
                TemporalHelpers.NoNegativeZero(-result.Minutes),
                TemporalHelpers.NoNegativeZero(-result.Seconds),
                TemporalHelpers.NoNegativeZero(-result.Milliseconds),
                TemporalHelpers.NoNegativeZero(-result.Microseconds),
                TemporalHelpers.NoNegativeZero(-result.Nanoseconds));
        }

        return CreateDuration(result.Years, result.Months, result.Weeks, result.Days, result.Hours, result.Minutes, result.Seconds, result.Milliseconds, result.Microseconds, result.Nanoseconds);
    }


    private JsDuration CreateDuration(double years, double months, double weeks, double days,
        double hours, double minutes, double seconds, double ms, double us, double ns)
    {
        var record = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, ms, us, ns);
        return new JsDuration(_engine, _realm.Intrinsics.TemporalDuration.PrototypeObject, record);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.round
    /// </summary>
    private JsPlainDateTime Round(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var roundTo = arguments.At(0);

        if (roundTo.IsUndefined())
        {
            Throw.TypeError(_realm, "Options required");
        }

        string smallestUnit;
        string roundingMode;
        int roundingIncrement;

        if (roundTo.IsString())
        {
            smallestUnit = TemporalHelpers.ToSingularUnit(roundTo.ToString());
            if (!TemporalHelpers.IsValidDateTimeUnit(smallestUnit))
            {
                Throw.RangeError(_realm, $"\"{roundTo}\" is not a valid value for smallest unit");
            }

            roundingMode = "halfExpand";
            roundingIncrement = 1;
        }
        else if (roundTo.IsObject())
        {
            // Read all options BEFORE algorithmic validation (in alphabetical order per spec)
            roundingIncrement = TemporalHelpers.GetRoundingIncrementOption(_realm, roundTo);
            roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, roundTo, "halfExpand");
            smallestUnit = GetSmallestUnitOption(roundTo, "");

            // Now validate after all options are read
            if (string.IsNullOrEmpty(smallestUnit))
            {
                Throw.RangeError(_realm, "smallestUnit is required");
            }

            if (!TemporalHelpers.IsValidDateTimeUnit(smallestUnit))
            {
                Throw.RangeError(_realm, $"\"{smallestUnit}\" is not a valid value for smallest unit");
            }
        }
        else
        {
            Throw.TypeError(_realm, "Options must be a string or object");
            return null!;
        }

        // Validate rounding increment against the unit's maximum
        // Spec steps 10-13: maximum from MaximumTemporalDurationRoundingIncrement, undefined → 1, inclusive = (maximum == 1)
        {
            var specMax = TemporalHelpers.MaximumTemporalDurationRoundingIncrement(smallestUnit);
            var maximum = specMax ?? 1;
            var inclusive = maximum == 1;
            TemporalHelpers.ValidateTemporalRoundingIncrement(_realm, roundingIncrement, maximum, inclusive);
        }

        // For day rounding, apply rounding mode to determine day adjustment
        if (string.Equals(smallestUnit, "day", StringComparison.Ordinal))
        {
            var ns = plainDateTime.IsoDateTime.Time.TotalNanoseconds();
            var dayIncrement = roundingMode switch
            {
                "ceil" => ns > 0 ? 1 : 0, // Round up if any time present
                "floor" or "trunc" => 0, // Always round down
                "expand" => ns > 0 ? (ns >= 0 ? 1 : -1) : 0, // Away from zero
                "halfExpand" => ns >= TemporalHelpers.NanosecondsPerDay / 2 ? 1 : 0, // Round half away from zero
                "halfCeil" => ns > TemporalHelpers.NanosecondsPerDay / 2 ||
                              (ns == TemporalHelpers.NanosecondsPerDay / 2)
                    ? 1
                    : 0, // Round half up
                "halfFloor" => ns > TemporalHelpers.NanosecondsPerDay / 2 ? 1 : 0, // Round half down
                "halfTrunc" => ns > TemporalHelpers.NanosecondsPerDay / 2 ? 1 : 0, // Round half toward zero
                "halfEven" => ns > TemporalHelpers.NanosecondsPerDay / 2 ||
                              (ns == TemporalHelpers.NanosecondsPerDay / 2 &&
                               ((plainDateTime.IsoDateTime.Day % 2) == 1))
                    ? 1
                    : 0, // Round half to even
                _ => 0
            };

            var newDate = TemporalHelpers.DaysToIsoDate(
                TemporalHelpers.IsoDateToDays(plainDateTime.IsoDateTime.Year, plainDateTime.IsoDateTime.Month, plainDateTime.IsoDateTime.Day) + dayIncrement);
            return _constructor.Construct(new IsoDateTime(newDate, new IsoTime(0, 0, 0, 0, 0, 0)), plainDateTime.Calendar);
        }

        // Time rounding
        var timeNs = plainDateTime.IsoDateTime.Time.TotalNanoseconds();
        var roundedNs = RoundTime(timeNs, smallestUnit, roundingMode, roundingIncrement);

        // Handle day overflow
        var dayOverflow = roundedNs / TemporalHelpers.NanosecondsPerDay;
        roundedNs %= TemporalHelpers.NanosecondsPerDay;

        var date = plainDateTime.IsoDateTime.Date;
        if (dayOverflow != 0)
        {
            var totalDays = TemporalHelpers.IsoDateToDays(date.Year, date.Month, date.Day) + dayOverflow;
            date = TemporalHelpers.DaysToIsoDate(totalDays);
        }

        return _constructor.Construct(new IsoDateTime(date, IsoTime.FromNanoseconds(roundedNs)), plainDateTime.Calendar);
    }

    private static long RoundTime(long nanoseconds, string smallestUnit, string roundingMode, int increment)
    {
        var unitNs = TemporalHelpers.GetUnitNanoseconds(smallestUnit);
        var incrementNs = unitNs * increment;
        return TemporalHelpers.RoundNumberToIncrement(nanoseconds, incrementNs, roundingMode);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.equals
    /// </summary>
    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var other = _constructor.ToTemporalDateTime(arguments.At(0), "constrain");
        var result = TemporalHelpers.CompareIsoDateTimes(plainDateTime.IsoDateTime, other.IsoDateTime) == 0 &&
                     string.Equals(plainDateTime.Calendar, other.Calendar, StringComparison.Ordinal);
        return result ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.tostring
    /// </summary>
    private JsString ToString(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var optionsArg = arguments.At(0);

        var calendarName = "auto";
        var precision = -1; // -1 means "auto"
        var roundingMode = "trunc";
        var isoDateTime = plainDateTime.IsoDateTime;

        if (!optionsArg.IsUndefined())
        {
            if (!optionsArg.IsObject())
            {
                Throw.TypeError(_realm, "Options must be an object");
            }

            var options = optionsArg.AsObject();

            // Read options in strict alphabetical order per spec:
            // calendarName, fractionalSecondDigits, roundingMode, smallestUnit

            // 1. calendarName
            var calendarNameValue = options.Get("calendarName");
            if (!calendarNameValue.IsUndefined())
            {
                calendarName = TypeConverter.ToString(calendarNameValue);
                if (!TemporalHelpers.IsValidCalendarNameOption(calendarName))
                {
                    Throw.RangeError(_realm, $"Invalid calendarName option: {calendarName}");
                }
            }

            // 2. fractionalSecondDigits
            var fsdValue = options.Get("fractionalSecondDigits");
            if (!fsdValue.IsUndefined())
            {
                if (fsdValue.IsNumber())
                {
                    var fsdNum = fsdValue.AsNumber();
                    if (double.IsNaN(fsdNum) || double.IsInfinity(fsdNum))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be a finite number");
                    }

                    var floored = (int) System.Math.Floor(fsdNum);
                    if (floored < 0 || floored > 9)
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be between 0 and 9");
                    }

                    precision = floored;
                }
                else
                {
                    var fsdStr = TypeConverter.ToString(fsdValue);
                    if (!string.Equals(fsdStr, "auto", StringComparison.Ordinal))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be 'auto' or a number 0-9");
                    }
                }
            }

            // 3. roundingMode
            roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, options, "trunc");

            // 4. smallestUnit (ALWAYS overrides fractionalSecondDigits when present)
            var smallestUnitValue = options.Get("smallestUnit");
            if (!smallestUnitValue.IsUndefined())
            {
                var str = TypeConverter.ToString(smallestUnitValue);
                var smallestUnit = TemporalHelpers.ToSingularUnit(str);
                if (!TemporalHelpers.IsValidTemporalUnit(smallestUnit))
                {
                    Throw.RangeError(_realm, $"Invalid unit: {str}");
                }

                // hour and larger units are not valid for toString
                if (smallestUnit is "year" or "month" or "week" or "day" or "hour")
                {
                    Throw.RangeError(_realm, $"Invalid smallest unit for toString: {str}");
                }

                // smallestUnit ALWAYS takes precedence over fractionalSecondDigits
                precision = smallestUnit switch
                {
                    "minute" => -2, // -2 means minute precision (omit seconds)
                    "second" => 0,
                    "millisecond" => 3,
                    "microsecond" => 6,
                    "nanosecond" => 9,
                    _ => -1
                };
            }

            // Apply rounding if needed
            // https://tc39.es/proposal-temporal/#sec-temporal-roundisodatetime
            if (precision >= 0 || precision == -2)
            {
                // Determine smallestUnit and increment from precision
                string smallestUnit;
                int increment;

                if (precision == -2)
                {
                    smallestUnit = "minute";
                    increment = 1;
                }
                else if (precision == 0)
                {
                    smallestUnit = "second";
                    increment = 1;
                }
                else if (precision > 0 && precision <= 9)
                {
                    // For precision 1-9, use nanosecond with appropriate increment
                    smallestUnit = "nanosecond";
                    increment = (int) System.Math.Pow(10, 9 - precision);
                }
                else
                {
                    smallestUnit = "nanosecond";
                    increment = 1;
                }

                var timeNs = TemporalHelpers.TimeToNanoseconds(isoDateTime.Time);
                var roundedNs = RoundTime(timeNs, smallestUnit, roundingMode, increment);

                // Handle day overflow
                var days = roundedNs / TemporalHelpers.NanosecondsPerDay;
                roundedNs = roundedNs % TemporalHelpers.NanosecondsPerDay;
                if (roundedNs < 0)
                {
                    roundedNs += TemporalHelpers.NanosecondsPerDay;
                    days--;
                }

                var roundedTime = TemporalHelpers.NanosecondsToTime(roundedNs);
                var adjustedDate = isoDateTime.Date;
                if (days != 0)
                {
                    var totalDays = TemporalHelpers.IsoDateToDays(adjustedDate.Year, adjustedDate.Month, adjustedDate.Day);
                    totalDays += (int) days;
                    adjustedDate = TemporalHelpers.DaysToIsoDate(totalDays);
                }

                isoDateTime = new IsoDateTime(adjustedDate, roundedTime);

                // Validate the rounded result is within representable limits
                if (!TemporalHelpers.IsValidIsoDateTime(
                        isoDateTime.Year, isoDateTime.Month, isoDateTime.Day,
                        isoDateTime.Hour, isoDateTime.Minute, isoDateTime.Second,
                        isoDateTime.Millisecond, isoDateTime.Microsecond, isoDateTime.Nanosecond))
                {
                    Throw.RangeError(_realm, "Rounded PlainDateTime is outside the representable range");
                }
            }
        }

        return new JsString(FormatDateTime(isoDateTime, plainDateTime.Calendar, calendarName, precision));
    }


    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.tojson
    /// </summary>
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        return new JsString(FormatDateTime(plainDateTime.IsoDateTime, plainDateTime.Calendar));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.tolocalestring
    /// </summary>
    private JsString ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        return new JsString(FormatDateTime(plainDateTime.IsoDateTime, plainDateTime.Calendar));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDateTime cannot be converted to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.toplaindate
    /// </summary>
    private JsPlainDate ToPlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        return new JsPlainDate(_engine, _realm.Intrinsics.TemporalPlainDate.PrototypeObject,
            plainDateTime.IsoDateTime.Date, plainDateTime.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.toplaintime
    /// </summary>
    private JsPlainTime ToPlainTime(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        return new JsPlainTime(_engine, _realm.Intrinsics.TemporalPlainTime.PrototypeObject,
            plainDateTime.IsoDateTime.Time);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.prototype.tozoneddatetime
    /// </summary>
    private JsZonedDateTime ToZonedDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        var plainDateTime = ValidatePlainDateTime(thisObject);
        var timeZoneLike = arguments.At(0);
        var optionsArg = arguments.At(1);

        // Validate time zone
        var timeZone = ToTemporalTimeZoneIdentifier(timeZoneLike);

        // Get disambiguation option
        var disambiguation = "compatible";
        if (!optionsArg.IsUndefined())
        {
            if (!optionsArg.IsObject())
            {
                Throw.TypeError(_realm, "Options must be an object");
            }

            var disValue = optionsArg.AsObject().Get("disambiguation");
            if (!disValue.IsUndefined())
            {
                disambiguation = TypeConverter.ToString(disValue);
                if (!IsValidDisambiguation(disambiguation))
                {
                    Throw.RangeError(_realm, $"Invalid disambiguation: {disambiguation}");
                }
            }
        }

        // Get the epoch nanoseconds for this date-time in the given time zone
        var epochNs = GetEpochFromDateTime(plainDateTime.IsoDateTime, timeZone, disambiguation);

        if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
        {
            Throw.RangeError(_realm, "Resulting ZonedDateTime is outside the valid range");
        }

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject,
            epochNs, timeZone, plainDateTime.Calendar);
    }

    private string ToTemporalTimeZoneIdentifier(JsValue timeZoneLike)
    {
        if (timeZoneLike.IsUndefined() || timeZoneLike.IsNull())
        {
            Throw.TypeError(_realm, "Time zone is required");
        }

        return TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, timeZoneLike);
    }

    private static bool IsValidDisambiguation(string value)
    {
        return string.Equals(value, "compatible", StringComparison.Ordinal) ||
               string.Equals(value, "earlier", StringComparison.Ordinal) ||
               string.Equals(value, "later", StringComparison.Ordinal) ||
               string.Equals(value, "reject", StringComparison.Ordinal);
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
                Throw.RangeError(_realm, "Time is ambiguous in this time zone (overlap)");
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

    private static string FormatDateTime(IsoDateTime dateTime, string calendar, string calendarName = "auto", int fractionalSecondDigits = -1)
    {
        var sb = new ValueStringBuilder();

        // Format date
        if (dateTime.Year < 0 || dateTime.Year > 9999)
        {
            sb.Append(dateTime.Year >= 0 ? '+' : '-');
            sb.Append(System.Math.Abs(dateTime.Year).ToString("D6", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(dateTime.Year.ToString("D4", CultureInfo.InvariantCulture));
        }

        sb.Append('-');
        sb.Append(dateTime.Month.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('-');
        sb.Append(dateTime.Day.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('T');

        // Format time
        sb.Append(dateTime.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(dateTime.Minute.ToString("D2", CultureInfo.InvariantCulture));

        // Include seconds unless precision is -2 (minute precision)
        if (fractionalSecondDigits != -2)
        {
            sb.Append(':');
            sb.Append(dateTime.Second.ToString("D2", CultureInfo.InvariantCulture));

            // Format fractional seconds
            var subSecond = dateTime.Millisecond * 1_000_000L + dateTime.Microsecond * 1_000L + dateTime.Nanosecond;
            if (fractionalSecondDigits == -1)
            {
                // Auto - include only if non-zero, trimming trailing zeros
                if (subSecond != 0)
                {
                    sb.Append('.');
                    var fraction = subSecond.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                    sb.Append(fraction);
                }
            }
            else if (fractionalSecondDigits > 0)
            {
                sb.Append('.');
                var fraction = subSecond.ToString("D9", CultureInfo.InvariantCulture);
                sb.Append(fraction.AsSpan(0, fractionalSecondDigits));
            }
            // else fractionalSecondDigits == 0, don't include any fractional seconds
        }

        // Format calendar annotation based on calendarName option
        var showCalendar = calendarName switch
        {
            "never" => false,
            "always" => true,
            "critical" => true,
            _ => !string.Equals(calendar, "iso8601", StringComparison.Ordinal) // "auto"
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

    private int ConvertToInteger(JsValue value, string fieldName)
    {
        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"DateTime {fieldName} must be a finite number");
        }

        return (int) System.Math.Truncate(number);
    }

    private int GetPropertyOrDefault(ObjectInstance obj, string name, int defaultValue, bool usePositiveInteger = false)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return defaultValue;

        // Use ToPositiveIntegerWithTruncation for fields that require positive values (day, month)
        // This throws RangeError for values < 1, per spec PrepareCalendarFields
        if (usePositiveInteger)
        {
            return TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, value);
        }

        return ConvertToInteger(value, name);
    }

    private DurationRecord ToDurationRecord(JsValue value)
    {
        if (value is JsDuration duration)
        {
            return duration.DurationRecord;
        }

        if (value.IsString())
        {
            var parsed = TemporalHelpers.ParseDuration(value.ToString());
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid duration string");
            }

            // Validate parsed duration is within valid range (years/months/weeks < 2^32, etc.)
            if (!TemporalHelpers.IsValidDuration(parsed.Value))
            {
                Throw.RangeError(_realm, "Invalid duration");
            }

            return parsed.Value;
        }

        if (value.IsObject())
        {
            var obj = value.AsObject();
            return GetDurationFromObject(obj);
        }

        Throw.TypeError(_realm, "Invalid duration");
        return default;
    }

    private DurationRecord GetDurationFromObject(ObjectInstance obj)
    {
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

    private double GetDurationProperty(ObjectInstance obj, string name, ref bool hasAny)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return 0;

        hasAny = true;

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be a finite number");
        }

        // Duration properties must be integers
        if (number != System.Math.Truncate(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be an integer");
        }

        return number;
    }

    private string GetLargestUnitOption(JsValue options, string defaultValue)
    {
        if (options.IsUndefined())
            return defaultValue;

        if (!options.IsObject())
        {
            Throw.TypeError(_realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var value = obj.Get("largestUnit");
        if (value.IsUndefined())
            return defaultValue;

        var unit = TypeConverter.ToString(value);
        return TemporalHelpers.ToSingularUnit(unit);
    }

    private string GetSmallestUnitOption(JsValue options, string defaultValue)
    {
        if (options.IsUndefined())
            return defaultValue;

        if (!options.IsObject())
        {
            Throw.TypeError(_realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var value = obj.Get("smallestUnit");
        if (value.IsUndefined())
            return defaultValue;

        var unit = TypeConverter.ToString(value);
        return TemporalHelpers.ToSingularUnit(unit);
    }
}

using System.Numerics;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-zoneddatetime-prototype-object
/// </summary>
internal sealed class ZonedDateTimePrototype : Prototype
{
    private readonly ZonedDateTimeConstructor _constructor;

    internal ZonedDateTimePrototype(
        Engine engine,
        Realm realm,
        ZonedDateTimeConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(53, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["withPlainTime"] = new(new ClrFunction(Engine, "withPlainTime", WithPlainTime, 0, LengthFlags), PropertyFlags),
            ["withTimeZone"] = new(new ClrFunction(Engine, "withTimeZone", WithTimeZone, 1, LengthFlags), PropertyFlags),
            ["withCalendar"] = new(new ClrFunction(Engine, "withCalendar", WithCalendar, 1, LengthFlags), PropertyFlags),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["until"] = new(new ClrFunction(Engine, "until", Until, 1, LengthFlags), PropertyFlags),
            ["since"] = new(new ClrFunction(Engine, "since", Since, 1, LengthFlags), PropertyFlags),
            ["round"] = new(new ClrFunction(Engine, "round", Round, 1, LengthFlags), PropertyFlags),
            ["startOfDay"] = new(new ClrFunction(Engine, "startOfDay", StartOfDay, 0, LengthFlags), PropertyFlags),
            ["getTimeZoneTransition"] = new(new ClrFunction(Engine, "getTimeZoneTransition", GetTimeZoneTransition, 1, LengthFlags), PropertyFlags),
            ["toInstant"] = new(new ClrFunction(Engine, "toInstant", ToInstant, 0, LengthFlags), PropertyFlags),
            ["toPlainDate"] = new(new ClrFunction(Engine, "toPlainDate", ToPlainDate, 0, LengthFlags), PropertyFlags),
            ["toPlainTime"] = new(new ClrFunction(Engine, "toPlainTime", ToPlainTime, 0, LengthFlags), PropertyFlags),
            ["toPlainDateTime"] = new(new ClrFunction(Engine, "toPlainDateTime", ToPlainDateTime, 0, LengthFlags), PropertyFlags),
            ["toPlainYearMonth"] = new(new ClrFunction(Engine, "toPlainYearMonth", ToPlainYearMonth, 0, LengthFlags), PropertyFlags),
            ["toPlainMonthDay"] = new(new ClrFunction(Engine, "toPlainMonthDay", ToPlainMonthDay, 0, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToStringMethod, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["calendarId"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get calendarId", GetCalendarId, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["timeZoneId"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get timeZoneId", GetTimeZoneId, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
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
            ["epochMilliseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get epochMilliseconds", GetEpochMilliseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["epochMicroseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get epochMicroseconds", GetEpochMicroseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["epochSeconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get epochSeconds", GetEpochSeconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["epochNanoseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get epochNanoseconds", GetEpochNanoseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["dayOfWeek"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get dayOfWeek", GetDayOfWeek, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["dayOfYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get dayOfYear", GetDayOfYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["weekOfYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get weekOfYear", GetWeekOfYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["yearOfWeek"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get yearOfWeek", GetYearOfWeek, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInWeek"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInWeek", GetDaysInWeek, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInMonth"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInMonth", GetDaysInMonth, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["daysInYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get daysInYear", GetDaysInYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthsInYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthsInYear", GetMonthsInYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["inLeapYear"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get inLeapYear", GetInLeapYear, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["hoursInDay"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get hoursInDay", GetHoursInDay, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["offset"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get offset", GetOffset, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["offsetNanoseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get offsetNanoseconds", GetOffsetNanoseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.ZonedDateTime", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsZonedDateTime ValidateZonedDateTime(JsValue thisObject)
    {
        if (thisObject is JsZonedDateTime zonedDateTime)
            return zonedDateTime;
        Throw.TypeError(_realm, "Value is not a Temporal.ZonedDateTime");
        return null!;
    }

    // Property accessors
    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) =>
        new JsString(ValidateZonedDateTime(thisObject).Calendar);

    private JsString GetTimeZoneId(JsValue thisObject, JsCallArguments arguments) =>
        new JsString(ValidateZonedDateTime(thisObject).TimeZone);

    private JsValue GetEra(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var isoDateTime = zdt.GetIsoDateTime();
        var era = TemporalHelpers.CalendarEra(zdt.Calendar, isoDateTime.Year);
        return era is not null ? new JsString(era) : Undefined;
    }

    private JsValue GetEraYear(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var isoDateTime = zdt.GetIsoDateTime();
        var eraYear = TemporalHelpers.CalendarEraYear(zdt.Calendar, isoDateTime.Year);
        return eraYear.HasValue ? JsNumber.Create(eraYear.Value) : Undefined;
    }

    private JsNumber GetYear(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var isoDateTime = zdt.GetIsoDateTime();
        return JsNumber.Create(TemporalHelpers.CalendarYear(zdt.Calendar, isoDateTime.Year));
    }

    private JsNumber GetMonth(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Month);

    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) =>
        new JsString($"M{ValidateZonedDateTime(thisObject).GetIsoDateTime().Month:D2}");

    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Day);

    private JsNumber GetHour(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Hour);

    private JsNumber GetMinute(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Minute);

    private JsNumber GetSecond(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Second);

    private JsNumber GetMillisecond(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Millisecond);

    private JsNumber GetMicrosecond(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Microsecond);

    private JsNumber GetNanosecond(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Nanosecond);

    private JsNumber GetEpochSeconds(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create((double) FloorDivide(ValidateZonedDateTime(thisObject).EpochNanoseconds, 1_000_000_000));

    private JsNumber GetEpochMilliseconds(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create((double) FloorDivide(ValidateZonedDateTime(thisObject).EpochNanoseconds, 1_000_000));

    private static BigInteger FloorDivide(BigInteger dividend, long divisor)
    {
        var result = dividend / divisor;
        if (dividend % divisor != 0 && (dividend < 0) != (divisor < 0))
        {
            result -= 1;
        }

        return result;
    }

    private JsBigInt GetEpochMicroseconds(JsValue thisObject, JsCallArguments arguments) =>
        JsBigInt.Create(ValidateZonedDateTime(thisObject).EpochNanoseconds / 1_000);

    private JsBigInt GetEpochNanoseconds(JsValue thisObject, JsCallArguments arguments) =>
        JsBigInt.Create(ValidateZonedDateTime(thisObject).EpochNanoseconds);

    private JsNumber GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Date.DayOfWeek());

    private JsNumber GetDayOfYear(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).GetIsoDateTime().Date.DayOfYear());

    private JsValue GetWeekOfYear(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        if (!string.Equals(zdt.Calendar, "iso8601", StringComparison.Ordinal))
        {
            return Undefined;
        }

        var date = zdt.GetIsoDateTime().Date;
        return JsNumber.Create(date.WeekOfYear());
    }

    private JsValue GetYearOfWeek(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        if (!string.Equals(zdt.Calendar, "iso8601", StringComparison.Ordinal))
        {
            return Undefined;
        }

        var date = zdt.GetIsoDateTime().Date;
        return JsNumber.Create(date.YearOfWeek());
    }

    private JsNumber GetDaysInWeek(JsValue thisObject, JsCallArguments arguments)
    {
        ValidateZonedDateTime(thisObject);
        return JsNumber.Create(7); // ISO 8601 always has 7 days in a week
    }

    private JsNumber GetDaysInMonth(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var date = zdt.GetIsoDateTime().Date;
        return JsNumber.Create(IsoDate.IsoDateInMonth(date.Year, date.Month));
    }

    private JsNumber GetDaysInYear(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var year = zdt.GetIsoDateTime().Year;
        var isLeap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
        return JsNumber.Create(isLeap ? 366 : 365);
    }

    private JsNumber GetMonthsInYear(JsValue thisObject, JsCallArguments arguments)
    {
        ValidateZonedDateTime(thisObject);
        return JsNumber.Create(12); // ISO 8601 always has 12 months
    }

    private JsBoolean GetInLeapYear(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var year = zdt.GetIsoDateTime().Year;
        var isLeap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
        return isLeap ? JsBoolean.True : JsBoolean.False;
    }

    private JsNumber GetHoursInDay(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var provider = _engine.Options.Temporal.TimeZoneProvider;

        // Get start of day
        var startOfDay = GetStartOfDayInstant(zdt, provider);
        if (!InstantConstructor.IsValidEpochNanoseconds(startOfDay))
        {
            Throw.RangeError(_realm, "Start of day is outside the valid range");
        }

        // Get start of next day using GetStartOfDay per spec
        var nextDay = AddDays(zdt.GetIsoDateTime().Date, 1);
        var startOfNextDay = TemporalHelpers.GetStartOfDay(_realm, provider, zdt.TimeZone, nextDay);

        if (!InstantConstructor.IsValidEpochNanoseconds(startOfNextDay))
        {
            Throw.RangeError(_realm, "Start of next day is outside the valid range");
        }

        var durationNs = startOfNextDay - startOfDay;
        var hours = (double) durationNs / TemporalHelpers.NanosecondsPerHour;
        return JsNumber.Create(hours);
    }

    private JsString GetOffset(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var offsetNs = zdt.OffsetNanoseconds;
        return new JsString(TemporalHelpers.FormatOffsetString(offsetNs));
    }

    private JsNumber GetOffsetNanoseconds(JsValue thisObject, JsCallArguments arguments) =>
        JsNumber.Create(ValidateZonedDateTime(thisObject).OffsetNanoseconds);

    // Methods
    private JsZonedDateTime With(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var temporalZonedDateTimeLike = arguments.At(0);
        var options = arguments.At(1);

        if (!temporalZonedDateTimeLike.IsObject())
        {
            Throw.TypeError(_realm, "with requires an object argument");
        }

        var obj = temporalZonedDateTimeLike.AsObject();
        var current = zdt.GetIsoDateTime();

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
            Throw.TypeError(_realm, "Temporal ZonedDateTime-like must not have a calendar property");
        }

        // Steps 4-5: Check for timeZone property
        var timeZoneProp = obj.Get("timeZone");
        if (!timeZoneProp.IsUndefined())
        {
            Throw.TypeError(_realm, "Temporal ZonedDateTime-like must not have a timeZone property");
        }

        // Read fields in strict alphabetical order: day, hour, microsecond, millisecond, minute, month, monthCode, nanosecond, offset, second, year
        // Each field is read and immediately converted, defaulting to current value if undefined
        // Track if at least one property is present

        var dayValue = obj.Get("day");
        var day = dayValue.IsUndefined() ? current.Day : GetIntegerFromValue(dayValue);

        var hourValue = obj.Get("hour");
        var hour = hourValue.IsUndefined() ? current.Hour : GetIntegerFromValue(hourValue);

        var microsecondValue = obj.Get("microsecond");
        var microsecond = microsecondValue.IsUndefined() ? current.Microsecond : GetIntegerFromValue(microsecondValue);

        var millisecondValue = obj.Get("millisecond");
        var millisecond = millisecondValue.IsUndefined() ? current.Millisecond : GetIntegerFromValue(millisecondValue);

        var minuteValue = obj.Get("minute");
        var minute = minuteValue.IsUndefined() ? current.Minute : GetIntegerFromValue(minuteValue);

        var monthValue = obj.Get("month");
        var month = monthValue.IsUndefined() ? current.Month : GetIntegerFromValue(monthValue);

        // monthCode - read in alphabetical order, convert to string but defer validation
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

        var nanosecondValue = obj.Get("nanosecond");
        var nanosecond = nanosecondValue.IsUndefined() ? current.Nanosecond : GetIntegerFromValue(nanosecondValue);

        // offset - read in alphabetical order, convert to string but defer parsing
        var offsetValue = obj.Get("offset");
        string? offsetStr = null;
        if (!offsetValue.IsUndefined())
        {
            offsetStr = TemporalHelpers.ToOffsetString(_realm, offsetValue);
        }

        var secondValue = obj.Get("second");
        var second = secondValue.IsUndefined() ? current.Second : GetIntegerFromValue(secondValue);

        var yearValue = obj.Get("year");
        var year = yearValue.IsUndefined() ? current.Year : GetIntegerFromValue(yearValue);

        // Check that at least one property is present
        if (dayValue.IsUndefined() && hourValue.IsUndefined() && microsecondValue.IsUndefined() &&
            millisecondValue.IsUndefined() && minuteValue.IsUndefined() && monthValue.IsUndefined() &&
            monthCodeValue.IsUndefined() && nanosecondValue.IsUndefined() && offsetValue.IsUndefined() &&
            secondValue.IsUndefined() && yearValue.IsUndefined())
        {
            Throw.TypeError(_realm, "with() requires at least one temporal property");
        }

        // Check for fundamentally invalid values BEFORE reading options
        // (so that RangeError is thrown before TypeError for wrong options type)
        if (year < -271821 || year > 275760 || month < 1 || day < 1 ||
            hour < 0 || minute < 0 || second < 0 ||
            millisecond < 0 || microsecond < 0 || nanosecond < 0)
        {
            Throw.RangeError(_realm, "Invalid datetime");
        }

        // Read options in alphabetical order per spec: disambiguation, offset, overflow
        // ALL options must be read BEFORE any other algorithmic validation (monthCode parsing, etc.)
        var disambiguation = GetDisambiguationOption(options, _realm);
        var offsetOption = GetOffsetOption(options, _realm);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        // NOW do remaining algorithmic validation(after all fields and options are read)

        // Parse and validate offset
        long? offsetNs = null;
        if (offsetStr is not null)
        {
            offsetNs = TemporalHelpers.ParseOffsetString(offsetStr);
            if (offsetNs is null)
            {
                Throw.RangeError(_realm, "Invalid offset string");
            }
        }

        // Parse and validate monthCode
        int? parsedMonthCode = null;
        if (monthCode is not null)
        {
            // Parse monthCode for well-formedness
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

            // Check if month property was explicitly provided (not just defaulted)
            // Use the monthValue that was already read earlier
            if (!monthValue.IsUndefined())
            {
                // Both month and monthCode provided - they must match
                if (month != parsedMonthCode.Value)
                {
                    Throw.RangeError(_realm, "month and monthCode must match");
                }
            }

            // Use monthCode if provided
            month = parsedMonthCode.Value;
        }

        // Regulate date and time with user's overflow option
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

        var newDateTime = new IsoDateTime(date.Value, time.Value);

        // Use the offset value already read and parsed earlier
        var epochNs = GetEpochFromIsoDateTime(newDateTime, zdt.TimeZone, disambiguation, offsetNs, offsetOption);

        // Validate the result instant is within valid range
        if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
        {
            Throw.RangeError(_realm, "Resulting epoch nanoseconds are outside valid Temporal range");
        }

        return _constructor.Construct(epochNs, zdt.TimeZone, zdt.Calendar);
    }

    private JsZonedDateTime WithPlainTime(JsValue thisObject, JsCallArguments arguments)
    {
        // https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime.prototype.withplaintime
        var zdt = ValidateZonedDateTime(thisObject);
        var plainTimeLike = arguments.At(0);
        var provider = _engine.Options.Temporal.TimeZoneProvider;

        BigInteger epochNs;
        if (plainTimeLike.IsUndefined())
        {
            // Step 7: If plainTimeLike is undefined, let epochNs be GetStartOfDay
            var current = zdt.GetIsoDateTime();
            epochNs = TemporalHelpers.GetStartOfDay(_realm, provider, zdt.TimeZone, current.Date);
        }
        else
        {
            // Step 8-9: Let plainTime be ToTemporalTime, then GetEpochNanosecondsFor with compatible
            var plainTime = _realm.Intrinsics.TemporalPlainTime.ToTemporalTime(plainTimeLike, "constrain");
            var current = zdt.GetIsoDateTime();
            var newDateTime = new IsoDateTime(current.Date, plainTime.IsoTime);
            epochNs = GetInstantFor(provider, zdt.TimeZone, newDateTime, "compatible");
        }

        // Validate result is within valid Temporal range
        if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
        {
            Throw.RangeError(_realm, "Resulting epoch nanoseconds are outside valid Temporal range");
        }

        return _constructor.Construct(epochNs, zdt.TimeZone, zdt.Calendar);
    }

    private JsZonedDateTime WithTimeZone(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var timeZoneLike = arguments.At(0);

        var timeZone = ToTemporalTimeZoneIdentifier(timeZoneLike);
        return _constructor.Construct(zdt.EpochNanoseconds, timeZone, zdt.Calendar);
    }

    private JsZonedDateTime WithCalendar(JsValue thisObject, JsCallArguments arguments)
    {
        // https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime.prototype.withcalendar
        // Step 1-2: Validate this value
        var zdt = ValidateZonedDateTime(thisObject);
        var calendarLike = arguments.At(0);

        // Step 3: Let calendar be ? ToTemporalCalendarIdentifier(calendarLike)
        var calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarLike);

        // Step 4: Return ! CreateTemporalZonedDateTime(zonedDateTime.[[EpochNanoseconds]], zonedDateTime.[[TimeZone]], calendar)
        return _constructor.Construct(zdt.EpochNanoseconds, zdt.TimeZone, calendar);
    }

    private JsZonedDateTime Add(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var temporalDurationLike = arguments.At(0);
        var options = arguments.At(1);

        var duration = ToDurationRecord(temporalDurationLike);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        var newEpochNs = AddDurationToZonedDateTime(zdt, duration, overflow);
        if (!InstantConstructor.IsValidEpochNanoseconds(newEpochNs))
        {
            Throw.RangeError(_realm, "Resulting ZonedDateTime is outside the valid range");
        }

        return _constructor.Construct(newEpochNs, zdt.TimeZone, zdt.Calendar);
    }

    private JsZonedDateTime Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var temporalDurationLike = arguments.At(0);
        var options = arguments.At(1);

        var duration = ToDurationRecord(temporalDurationLike);
        var negatedDuration = duration.Negated();
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        var newEpochNs = AddDurationToZonedDateTime(zdt, negatedDuration, overflow);
        if (!InstantConstructor.IsValidEpochNanoseconds(newEpochNs))
        {
            Throw.RangeError(_realm, "Resulting ZonedDateTime is outside the valid range");
        }

        return _constructor.Construct(newEpochNs, zdt.TimeZone, zdt.Calendar);
    }

    private JsDuration Until(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var other = arguments.At(0);
        var options = arguments.At(1);

        var otherZdt = _constructor.ToTemporalZonedDateTime(other, Undefined);
        return DifferenceZonedDateTime(zdt, otherZdt, options, "until");
    }

    private JsDuration Since(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var other = arguments.At(0);
        var options = arguments.At(1);

        var otherZdt = _constructor.ToTemporalZonedDateTime(other, Undefined);
        return DifferenceZonedDateTime(zdt, otherZdt, options, "since");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime.prototype.round
    /// </summary>
    private JsValue Round(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var roundTo = arguments.At(0);

        if (roundTo.IsUndefined())
        {
            Throw.TypeError(_realm, "roundTo is required");
        }

        string smallestUnit;
        string roundingMode;
        int roundingIncrement;

        if (roundTo.IsString())
        {
            smallestUnit = roundTo.ToString();
            roundingMode = "halfExpand";
            roundingIncrement = 1;
        }
        else if (roundTo.IsObject())
        {
            var obj = roundTo.AsObject();

            // Read options in alphabetical order per spec
            // 1. roundingIncrement
            roundingIncrement = TemporalHelpers.GetRoundingIncrementOption(_realm, obj);

            // 2. roundingMode
            roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, obj, "halfExpand");

            // 3. smallestUnit
            var unitProp = obj.Get("smallestUnit");
            if (unitProp.IsUndefined())
            {
                Throw.RangeError(_realm, "smallestUnit is required");
            }

            smallestUnit = TypeConverter.ToString(unitProp);
        }
        else
        {
            Throw.TypeError(_realm, "roundTo must be a string or object");
            return Undefined;
        }

        smallestUnit = TemporalHelpers.ToSingularUnit(smallestUnit);

        if (!TemporalHelpers.IsValidDateTimeUnit(smallestUnit))
        {
            Throw.RangeError(_realm, $"\"{smallestUnit}\" is not a valid value for smallest unit");
        }

        // Validate rounding increment against the unit's maximum
        // Spec steps 10-13: maximum from MaximumTemporalDurationRoundingIncrement, undefined → 1, inclusive = (maximum == 1)
        {
            var specMax = TemporalHelpers.MaximumTemporalDurationRoundingIncrement(smallestUnit);
            var maximum = specMax ?? 1;
            var inclusive = maximum == 1;
            TemporalHelpers.ValidateTemporalRoundingIncrement(_realm, roundingIncrement, maximum, inclusive);
        }

        // If rounding to nanosecond with increment 1, return a copy
        if (string.Equals(smallestUnit, "nanosecond", StringComparison.Ordinal) && roundingIncrement == 1)
        {
            return _constructor.Construct(zdt.EpochNanoseconds, zdt.TimeZone, zdt.Calendar);
        }

        var thisNs = zdt.EpochNanoseconds;
        var timeZone = zdt.TimeZone;
        var calendar = zdt.Calendar;
        var provider = _engine.Options.Temporal.TimeZoneProvider;

        // Get ISO date-time in the timezone
        var isoDateTime = zdt.GetIsoDateTime();

        BigInteger epochNanoseconds;

        if (string.Equals(smallestUnit, "day", StringComparison.Ordinal))
        {
            // Special handling for day rounding (accounts for DST)
            var dateStart = isoDateTime.Date;
            var dateEnd = AddDays(dateStart, 1);

            // Get start of current day and next day using GetStartOfDay per spec
            var startNs = TemporalHelpers.GetStartOfDay(_realm, provider, timeZone, dateStart);
            var endNs = TemporalHelpers.GetStartOfDay(_realm, provider, timeZone, dateEnd);

            // Calculate day length (may not be exactly 24 hours due to DST)
            var dayLengthNs = (long) (endNs - startNs);

            // Calculate progress through the day
            var dayProgressNs = (long) (thisNs - startNs);

            // Round the progress
            var roundedDayNs = TemporalHelpers.RoundNumberToIncrement(dayProgressNs, dayLengthNs, roundingMode);

            // Add back to start of day
            epochNanoseconds = startNs + roundedDayNs;
        }
        else
        {
            // Round the time component
            var timeNs = TemporalHelpers.TimeToNanoseconds(isoDateTime.Time);
            var unitNs = TemporalHelpers.GetUnitNanoseconds(smallestUnit);
            var incrementNs = unitNs * roundingIncrement;
            var roundedTimeNs = TemporalHelpers.RoundNumberToIncrement(timeNs, incrementNs, roundingMode);

            // Handle day overflow
            var days = roundedTimeNs / TemporalHelpers.NanosecondsPerDay;
            roundedTimeNs = roundedTimeNs % TemporalHelpers.NanosecondsPerDay;
            if (roundedTimeNs < 0)
            {
                roundedTimeNs += TemporalHelpers.NanosecondsPerDay;
                days--;
            }

            // Adjust date if needed
            var newDate = days != 0 ? AddDays(isoDateTime.Date, (int) days) : isoDateTime.Date;

            // Convert rounded nanoseconds back to time
            var newTime = TemporalHelpers.NanosecondsToTime(roundedTimeNs);

            // Get epoch nanoseconds for the rounded date-time, preserving the offset
            var roundedDateTime = new IsoDateTime(newDate, newTime);
            var localNs = IsoDateTimeToNanoseconds(roundedDateTime);
            var offsetNanoseconds = zdt.OffsetNanoseconds;
            epochNanoseconds = localNs - offsetNanoseconds;
        }

        // Validate that the rounded result is within valid range
        if (!InstantConstructor.IsValidEpochNanoseconds(epochNanoseconds))
        {
            Throw.RangeError(_realm, "Rounded ZonedDateTime is outside the valid range");
        }

        return _constructor.Construct(epochNanoseconds, timeZone, calendar);
    }

    private JsZonedDateTime StartOfDay(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        var startOfDayNs = GetStartOfDayInstant(zdt, provider);

        if (!InstantConstructor.IsValidEpochNanoseconds(startOfDayNs))
        {
            Throw.RangeError(_realm, "Start of day is outside the valid range");
        }

        return _constructor.Construct(startOfDayNs, zdt.TimeZone, zdt.Calendar);
    }

    private JsValue GetTimeZoneTransition(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var directionArg = arguments.At(0);

        if (directionArg.IsUndefined())
        {
            Throw.TypeError(_realm, "direction is required");
        }

        // If directionArg is a String, use it directly. Else, call GetOptionsObject (which throws TypeError for non-objects).
        string direction;
        if (directionArg.IsString())
        {
            direction = directionArg.ToString();
        }
        else
        {
            // Per spec: GetOptionsObject throws TypeError for non-object, non-undefined values
            if (!directionArg.IsObject())
            {
                Throw.TypeError(_realm, "direction must be a string or an object");
            }

            var obj = directionArg.AsObject();
            var directionProp = obj.Get("direction");
            direction = TypeConverter.ToString(directionProp);
        }

        if (!string.Equals(direction, "next", StringComparison.Ordinal) &&
            !string.Equals(direction, "previous", StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "direction must be 'next' or 'previous'");
        }

        var provider = _engine.Options.Temporal.TimeZoneProvider;
        BigInteger? transitionNs;

        if (string.Equals(direction, "next", StringComparison.Ordinal))
        {
            transitionNs = provider.GetNextTransition(zdt.TimeZone, zdt.EpochNanoseconds);
        }
        else
        {
            transitionNs = provider.GetPreviousTransition(zdt.TimeZone, zdt.EpochNanoseconds);
        }

        if (transitionNs is null)
        {
            return Null;
        }

        return _constructor.Construct(transitionNs.Value, zdt.TimeZone, zdt.Calendar);
    }

    private JsInstant ToInstant(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        return _realm.Intrinsics.TemporalInstant.Construct(zdt.EpochNanoseconds);
    }

    private JsPlainDate ToPlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var dt = zdt.GetIsoDateTime();
        return _realm.Intrinsics.TemporalPlainDate.Construct(dt.Date, zdt.Calendar);
    }

    private JsPlainTime ToPlainTime(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var dt = zdt.GetIsoDateTime();
        return _realm.Intrinsics.TemporalPlainTime.Construct(dt.Time);
    }

    private JsPlainDateTime ToPlainDateTime(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var dt = zdt.GetIsoDateTime();
        return _realm.Intrinsics.TemporalPlainDateTime.Construct(dt, zdt.Calendar);
    }

    private JsPlainYearMonth ToPlainYearMonth(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var dt = zdt.GetIsoDateTime();
        return _realm.Intrinsics.TemporalPlainYearMonth.Construct(dt.Date, zdt.Calendar);
    }

    private JsPlainMonthDay ToPlainMonthDay(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var dt = zdt.GetIsoDateTime();
        return _realm.Intrinsics.TemporalPlainMonthDay.Construct(dt.Date, zdt.Calendar);
    }

    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var other = arguments.At(0);

        var otherZdt = _constructor.ToTemporalZonedDateTime(other, Undefined);

        var sameEpoch = zdt.EpochNanoseconds == otherZdt.EpochNanoseconds;
        var sameTimeZone = TemporalHelpers.TimeZoneEquals(_engine, zdt.TimeZone, otherZdt.TimeZone);
        var sameCalendar = string.Equals(zdt.Calendar, otherZdt.Calendar, StringComparison.Ordinal);

        return sameEpoch && sameTimeZone && sameCalendar ? JsBoolean.True : JsBoolean.False;
    }

    private JsString ToStringMethod(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var options = arguments.At(0);

        // Validate options parameter
        if (!options.IsUndefined() && !options.IsObject())
        {
            Throw.TypeError(_realm, "Options must be an object");
        }

        var showCalendar = "auto";
        var showTimeZone = "auto";
        var showOffset = "auto";
        string? precision = null;
        var roundingMode = "trunc";

        if (options.IsObject())
        {
            var obj = options.AsObject();

            // Read options in alphabetical order per spec
            // 1. calendarName
            var calendarProp = obj.Get("calendarName");
            if (!calendarProp.IsUndefined())
            {
                showCalendar = TypeConverter.ToString(calendarProp);
                if (!TemporalHelpers.IsValidCalendarNameOption(showCalendar))
                {
                    Throw.RangeError(_realm, $"Invalid calendarName option: {showCalendar}");
                }
            }

            // 2. fractionalSecondDigits - GetTemporalFractionalSecondDigitsOption
            var precisionProp = obj.Get("fractionalSecondDigits");
            if (!precisionProp.IsUndefined())
            {
                if (precisionProp is not JsNumber)
                {
                    // Not a Number type: ToString then check for "auto"
                    var str = TypeConverter.ToString(precisionProp);
                    if (!string.Equals(str, "auto", StringComparison.Ordinal))
                    {
                        Throw.RangeError(_realm, $"Invalid fractionalSecondDigits value: {str}");
                    }

                    precision = "auto";
                }
                else
                {
                    var num = ((JsNumber) precisionProp)._value;
                    if (double.IsNaN(num) || double.IsInfinity(num))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be finite");
                    }

                    var digits = (int) System.Math.Floor(num);
                    if (digits < 0 || digits > 9)
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be between 0 and 9");
                    }

                    precision = digits.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // 3. offset
            var offsetProp = obj.Get("offset");
            if (!offsetProp.IsUndefined())
            {
                showOffset = TypeConverter.ToString(offsetProp);
                if (!string.Equals(showOffset, "auto", StringComparison.Ordinal) &&
                    !string.Equals(showOffset, "never", StringComparison.Ordinal))
                {
                    Throw.RangeError(_realm, $"Invalid offset option: {showOffset}");
                }
            }

            // 4. roundingMode
            roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, obj, "trunc");

            // 5. smallestUnit - Read first, validate later
            var smallestUnitProp = obj.Get("smallestUnit");
            string? smallestUnitStr = null;
            if (!smallestUnitProp.IsUndefined())
            {
                smallestUnitStr = TemporalHelpers.ToSingularUnit(TypeConverter.ToString(smallestUnitProp));
            }

            // 6. timeZoneName - Read before validation
            var timeZoneProp = obj.Get("timeZoneName");
            if (!timeZoneProp.IsUndefined())
            {
                showTimeZone = TypeConverter.ToString(timeZoneProp);
            }

            // Now validate and process options after all have been read
            if (smallestUnitStr != null)
            {
                // Valid smallestUnit values for toString: minute, second, millisecond, microsecond, nanosecond
                if (smallestUnitStr is not ("minute" or "second" or "millisecond" or "microsecond" or "nanosecond"))
                {
                    Throw.RangeError(_realm, $"\"{smallestUnitStr}\" is not a valid value for smallest unit");
                }

                // smallestUnit ALWAYS takes precedence over fractionalSecondDigits
                precision = smallestUnitStr switch
                {
                    "minute" => "-2", // -2 means minute precision (omit seconds)
                    "second" => "0",
                    "millisecond" => "3",
                    "microsecond" => "6",
                    "nanosecond" => "9",
                    _ => null
                };
            }

            // Validate timeZoneName after reading all options
            if (!string.Equals(showTimeZone, "auto", StringComparison.Ordinal) &&
                !IsValidTimeZoneNameOption(showTimeZone))
            {
                Throw.RangeError(_realm, $"Invalid timeZoneName option: {showTimeZone}");
            }
        }

        // Apply rounding if smallestUnit is specified
        // Skip rounding if precision is "auto" (handled during formatting)
        if (precision != null && !string.Equals(precision, "auto", StringComparison.Ordinal))
        {
            // Parse precision
            int precisionValue = int.Parse(precision, System.Globalization.CultureInfo.InvariantCulture);

            // Determine smallestUnit and increment from precision
            string smallestUnit;
            int increment;

            if (precisionValue == -2)
            {
                smallestUnit = "minute";
                increment = 1;
            }
            else if (precisionValue == 0)
            {
                smallestUnit = "second";
                increment = 1;
            }
            else if (precisionValue > 0 && precisionValue <= 9)
            {
                // For precision 1-9, use nanosecond with appropriate increment
                smallestUnit = "nanosecond";
                increment = (int) System.Math.Pow(10, 9 - precisionValue);
            }
            else
            {
                smallestUnit = "nanosecond";
                increment = 1;
            }

            // Round the ZonedDateTime's epoch nanoseconds
            var timeZone = zdt.TimeZone;
            var calendar = zdt.Calendar;
            var provider = _engine.Options.Temporal.TimeZoneProvider;

            // Get ISO date-time components
            var dt = zdt.GetIsoDateTime();

            // Round the time component
            var timeNs = TemporalHelpers.TimeToNanoseconds(dt.Time);
            var roundedNs = RoundTime(timeNs, smallestUnit, roundingMode, increment);

            // Handle day overflow
            var days = roundedNs / TemporalHelpers.NanosecondsPerDay;
            roundedNs = roundedNs % TemporalHelpers.NanosecondsPerDay;
            if (roundedNs < 0)
            {
                roundedNs += TemporalHelpers.NanosecondsPerDay;
                days--;
            }

            // Create new date with day adjustment (use AddDaysToISODate for proper overflow handling)
            var newDate = days != 0 ? TemporalHelpers.AddDaysToISODate(dt.Date, days) : dt.Date;

            // Convert rounded nanoseconds back to time
            var newTime = TemporalHelpers.NanosecondsToTime(roundedNs);

            // Get epoch nanoseconds for the rounded date-time
            var roundedDateTime = new IsoDateTime(newDate, newTime);
            var roundedEpochNs = TemporalHelpers.GetEpochNanosecondsFor(_realm, provider, timeZone, roundedDateTime, "compatible");

            // Create new ZonedDateTime with rounded epoch
            zdt = _constructor.Construct(roundedEpochNs, timeZone, calendar);
        }

        return new JsString(FormatZonedDateTime(zdt, showCalendar, showTimeZone, showOffset, precision));
    }

    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        return new JsString(FormatZonedDateTime(zdt, "auto", "auto", "auto", null));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sup-temporal.zoneddatetime.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Per spec: CreateDateTimeFormat with required=~any~, defaults=~all~, toLocaleStringTimeZone=ZDT.[[TimeZone]]
        // Uses ZonedDateTime defaults which include timeZoneName: "short"
        // This will throw TypeError if user passes a timeZone option
        var dtf = _realm.Intrinsics.DateTimeFormat.CreateDateTimeFormat(
            locales, options, required: Intl.DateTimeRequired.Any, defaults: Intl.DateTimeDefaults.ZonedDateTime,
            toLocaleStringTimeZone: zdt.TimeZone);

        // Calendar mismatch check per spec
        var cal = zdt.Calendar;
        if (!string.Equals(cal, "iso8601", StringComparison.Ordinal) &&
            dtf.Calendar != null && !string.Equals(cal, dtf.Calendar, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, $"Calendar mismatch: ZonedDateTime uses '{cal}' but DateTimeFormat uses '{dtf.Calendar}'");
        }

        // Per spec: create instant from ZDT's epoch nanoseconds and format it
        // The DTF was created with ZDT's timezone, so the instant will be formatted in that timezone
        const long nsPerTick = 100;
        var ticks = (long) (zdt.EpochNanoseconds / nsPerTick);
        var unixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var totalTicks = unixEpochTicks + ticks;
        var dateTime = new DateTime(totalTicks, DateTimeKind.Utc);

        return dtf.Format(dateTime);
    }

    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "ZonedDateTime.prototype.valueOf is not allowed");
        return Undefined;
    }

    // Helper methods

    private static long RoundTime(long nanoseconds, string smallestUnit, string roundingMode, int increment)
    {
        var unitNs = TemporalHelpers.GetUnitNanoseconds(smallestUnit);
        var incrementNs = unitNs * increment;
        return TemporalHelpers.RoundNumberToIncrement(nanoseconds, incrementNs, roundingMode);
    }

    private static string FormatZonedDateTime(JsZonedDateTime zdt, string showCalendar, string showTimeZone, string showOffset, string? precision)
    {
        var dt = zdt.GetIsoDateTime();
        var dateStr = TemporalHelpers.FormatIsoDate(dt.Date);
        var timeStr = TemporalHelpers.FormatIsoTime(dt.Time, precision);

        var result = $"{dateStr}T{timeStr}";

        // Add offset
        if (!string.Equals(showOffset, "never", StringComparison.Ordinal))
        {
            var offsetStr = TemporalHelpers.FormatOffsetRounded(zdt.OffsetNanoseconds);
            result += offsetStr;
        }

        // Add time zone
        if (!string.Equals(showTimeZone, "never", StringComparison.Ordinal))
        {
            var tzPrefix = string.Equals(showTimeZone, "critical", StringComparison.Ordinal) ? "!" : "";
            result += $"[{tzPrefix}{zdt.TimeZone}]";
        }

        // Add calendar
        if (string.Equals(showCalendar, "always", StringComparison.Ordinal) ||
            (string.Equals(showCalendar, "auto", StringComparison.Ordinal) && !string.Equals(zdt.Calendar, "iso8601", StringComparison.Ordinal)) ||
            string.Equals(showCalendar, "critical", StringComparison.Ordinal))
        {
            var prefix = string.Equals(showCalendar, "critical", StringComparison.Ordinal) ? "!" : "";
            result += $"[{prefix}u-ca={zdt.Calendar}]";
        }

        return result;
    }

    private BigInteger GetStartOfDayInstant(JsZonedDateTime zdt, ITimeZoneProvider provider)
    {
        var dt = zdt.GetIsoDateTime();
        return TemporalHelpers.GetStartOfDay(_realm, provider, zdt.TimeZone, dt.Date);
    }

    private static IsoDate AddDays(IsoDate date, int days)
    {
        var epochDays = TemporalHelpers.IsoDateToDays(date.Year, date.Month, date.Day) + days;
        return TemporalHelpers.DaysToIsoDate(epochDays);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-addzoneddatetime
    /// </summary>
    private BigInteger AddDurationToZonedDateTime(JsZonedDateTime zdt, DurationRecord duration, string overflow)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;

        // If only time units, add directly to epoch nanoseconds
        if (duration.Years == 0 && duration.Months == 0 && duration.Weeks == 0 && duration.Days == 0)
        {
            var timeNs = TemporalHelpers.TotalDurationNanoseconds(duration);
            return zdt.EpochNanoseconds + timeNs;
        }

        // Otherwise, we need to work with calendar dates
        // Per spec: CalendarDateAdd is called with overflow parameter
        var dt = zdt.GetIsoDateTime();

        // Add years and months (CalendarDateAdd step 1-2)
        var year = dt.Date.Year + (int) duration.Years;
        var month = dt.Date.Month + (int) duration.Months;

        // Normalize month
        while (month > 12)
        {
            month -= 12;
            year++;
        }

        while (month < 1)
        {
            month += 12;
            year--;
        }

        // RegulateISODate per spec (CalendarDateAdd step 3)
        // This will throw RangeError if overflow is "reject" and date is invalid
        var regulated = TemporalHelpers.RegulateIsoDate(year, month, dt.Date.Day, overflow);
        if (regulated is null)
        {
            Throw.RangeError(_realm, "Invalid date after adding duration");
        }

        // Add weeks and days (CalendarDateAdd step 4-5)
        var totalDays = duration.Weeks * 7 + duration.Days;
        var newDate = totalDays != 0 ? AddDays(regulated.Value, (int) totalDays) : regulated.Value;

        // Add time components using BigInteger to avoid overflow
        BigInteger totalNs = dt.Time.TotalNanoseconds() + TemporalHelpers.TimeDurationFromComponents(duration);

        // Handle overflow using floor division
        var dayOverflow = TemporalHelpers.FloorDivide(totalNs, TemporalHelpers.NanosecondsPerDay);
        totalNs -= dayOverflow * TemporalHelpers.NanosecondsPerDay;

        if (dayOverflow != 0)
        {
            // Validate day overflow is within representable range
            if (dayOverflow > 200_000_000 || dayOverflow < -200_000_000)
            {
                Throw.RangeError(_realm, "Date is outside the valid range");
            }

            newDate = AddDays(newDate, (int) dayOverflow);
        }

        var newTime = IsoTime.FromNanoseconds((long) totalNs);
        var newDateTime = new IsoDateTime(newDate, newTime);

        return GetInstantFor(provider, zdt.TimeZone, newDateTime, "compatible");
    }

    /// <summary>
    /// DifferenceTemporalZonedDateTime ( operation, zonedDateTime, other, options )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencetemporalzoneddatetime
    /// </summary>
    private JsDuration DifferenceZonedDateTime(JsZonedDateTime zonedDateTime, JsZonedDateTime other, JsValue optionsArg, string operation)
    {
        // Step 2-3: Check calendars match
        if (!string.Equals(zonedDateTime.Calendar, other.Calendar, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "Calendars must match for ZonedDateTime difference");
        }

        // All temporal units allowed for ZonedDateTime operations
        var allUnits = new[] { "year", "month", "week", "day", "hour", "minute", "second", "millisecond", "microsecond", "nanosecond" };

        // Step 4-5: GetDifferenceSettings reads options in correct order per spec
        // and negates rounding mode for "since" operation
        var fallbackSmallestUnit = "nanosecond";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            operation,
            fallbackSmallestUnit,
            fallbackLargestUnit,
            allUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "hour")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "hour");
        }

        // Step 6-10: If largestUnit is a time unit, use fast path with DifferenceInstant
        if (TemporalHelpers.IsTimeUnit(largestUnit))
        {
            var result = TemporalHelpers.DifferenceInstant(
                zonedDateTime.EpochNanoseconds,
                other.EpochNanoseconds,
                (int) settings.RoundingIncrement,
                settings.SmallestUnit,
                settings.RoundingMode,
                largestUnit);

            // Step 9: If operation is ~since~, negate the result
            if (string.Equals(operation, "since", StringComparison.Ordinal))
            {
                result = new DurationRecord(
                    0, 0, 0, 0, // No date components
                    TemporalHelpers.NoNegativeZero(-result.Hours),
                    TemporalHelpers.NoNegativeZero(-result.Minutes),
                    TemporalHelpers.NoNegativeZero(-result.Seconds),
                    TemporalHelpers.NoNegativeZero(-result.Milliseconds),
                    TemporalHelpers.NoNegativeZero(-result.Microseconds),
                    TemporalHelpers.NoNegativeZero(-result.Nanoseconds));
            }

            // Step 10: Return result
            return _realm.Intrinsics.TemporalDuration.Construct(result);
        }

        // Step 12-13: For date units, timezones must match
        if (!TemporalHelpers.TimeZoneEquals(_engine, zonedDateTime.TimeZone, other.TimeZone))
        {
            Throw.RangeError(_realm, "Time zones must match for ZonedDateTime difference with date units");
        }

        // Step 14-15: If equal, return zero duration
        if (zonedDateTime.EpochNanoseconds == other.EpochNanoseconds)
        {
            var zeroDuration = new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            return _realm.Intrinsics.TemporalDuration.Construct(zeroDuration);
        }

        // Step 16: Call DifferenceZonedDateTimeWithRounding for calendar-aware difference
        var timeZoneProvider = _engine.Options.Temporal.TimeZoneProvider;
        var finalResult = TemporalHelpers.DifferenceZonedDateTimeWithRounding(
            _realm,
            timeZoneProvider,
            zonedDateTime.EpochNanoseconds,
            other.EpochNanoseconds,
            zonedDateTime.TimeZone,
            zonedDateTime.Calendar,
            largestUnit,
            (int) settings.RoundingIncrement,
            settings.SmallestUnit,
            settings.RoundingMode);

        // Step 18: If operation is ~since~, negate the result
        if (string.Equals(operation, "since", StringComparison.Ordinal))
        {
            finalResult = new DurationRecord(
                TemporalHelpers.NoNegativeZero(-finalResult.Years),
                TemporalHelpers.NoNegativeZero(-finalResult.Months),
                TemporalHelpers.NoNegativeZero(-finalResult.Weeks),
                TemporalHelpers.NoNegativeZero(-finalResult.Days),
                TemporalHelpers.NoNegativeZero(-finalResult.Hours),
                TemporalHelpers.NoNegativeZero(-finalResult.Minutes),
                TemporalHelpers.NoNegativeZero(-finalResult.Seconds),
                TemporalHelpers.NoNegativeZero(-finalResult.Milliseconds),
                TemporalHelpers.NoNegativeZero(-finalResult.Microseconds),
                TemporalHelpers.NoNegativeZero(-finalResult.Nanoseconds));
        }

        // Step 19: Return result
        return _realm.Intrinsics.TemporalDuration.Construct(finalResult);
    }

    private DurationRecord ToDurationRecord(JsValue value)
    {
        if (value is JsDuration dur)
        {
            return dur.DurationRecord;
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
            // Read properties in alphabetical order per spec
            var hasAny = false;
            var days = GetDoubleProperty(obj, "days", 0, ref hasAny);
            var hours = GetDoubleProperty(obj, "hours", 0, ref hasAny);
            var microseconds = GetDoubleProperty(obj, "microseconds", 0, ref hasAny);
            var milliseconds = GetDoubleProperty(obj, "milliseconds", 0, ref hasAny);
            var minutes = GetDoubleProperty(obj, "minutes", 0, ref hasAny);
            var months = GetDoubleProperty(obj, "months", 0, ref hasAny);
            var nanoseconds = GetDoubleProperty(obj, "nanoseconds", 0, ref hasAny);
            var seconds = GetDoubleProperty(obj, "seconds", 0, ref hasAny);
            var weeks = GetDoubleProperty(obj, "weeks", 0, ref hasAny);
            var years = GetDoubleProperty(obj, "years", 0, ref hasAny);

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

    private double GetDoubleProperty(ObjectInstance obj, string name, double defaultValue, ref bool hasAny)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return defaultValue;
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

    private int GetIntegerPropertyOrDefault(ObjectInstance obj, string name, int defaultValue)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return defaultValue;
        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
            return defaultValue;
        if (double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"{name} must be finite");
            return defaultValue; // Unreachable
        }

        return (int) System.Math.Truncate(number);
    }

    private int GetIntegerFromValue(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
            return 0;
        if (double.IsInfinity(number))
        {
            Throw.RangeError(_realm, "Value must be finite");
            return 0; // Unreachable
        }

        return (int) System.Math.Truncate(number);
    }

    private static long GetSmallestUnitDivisor(string unit)
    {
        return unit switch
        {
            "day" => TemporalHelpers.NanosecondsPerDay,
            "hour" => TemporalHelpers.NanosecondsPerHour,
            "minute" => TemporalHelpers.NanosecondsPerMinute,
            "second" => TemporalHelpers.NanosecondsPerSecond,
            "millisecond" => TemporalHelpers.NanosecondsPerMillisecond,
            "microsecond" => TemporalHelpers.NanosecondsPerMicrosecond,
            "nanosecond" => 1,
            _ => 1
        };
    }


    private string ToTemporalTimeZoneIdentifier(JsValue timeZoneLike)
    {
        if (timeZoneLike.IsUndefined())
        {
            Throw.TypeError(_realm, "Time zone is required");
        }

        return TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, timeZoneLike);
    }

    private BigInteger GetEpochFromIsoDateTime(IsoDateTime dateTime, string timeZone, string disambiguation, long? offsetNs, string offsetOption)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        var localNs = IsoDateTimeToNanoseconds(dateTime);

        if (offsetNs.HasValue && string.Equals(offsetOption, "use", StringComparison.Ordinal))
        {
            // Use the offset directly
            var epochNs = localNs - offsetNs.Value;

            // Validate the result instant is within valid range
            if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
            {
                Throw.RangeError(_realm, "Resulting instant is outside the valid range");
            }

            return epochNs;
        }

        if (offsetNs.HasValue && string.Equals(offsetOption, "reject", StringComparison.Ordinal))
        {
            // In "reject" mode, the provided offset must exactly match the actual offset
            var possibleInstants = provider.GetPossibleInstantsFor(
                timeZone,
                dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Second,
                dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond);

            foreach (var instant in possibleInstants)
            {
                var actualOffset = provider.GetOffsetNanosecondsFor(timeZone, instant);
                if (actualOffset == offsetNs.Value)
                {
                    return instant;
                }
            }

            Throw.RangeError(_realm, "Offset does not match any possible offset for this wall time");
        }

        if (offsetNs.HasValue && string.Equals(offsetOption, "prefer", StringComparison.Ordinal))
        {
            // In "prefer" mode, use the provided offset if it matches, otherwise disambiguate
            var possibleInstants = provider.GetPossibleInstantsFor(
                timeZone,
                dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Second,
                dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond);

            foreach (var instant in possibleInstants)
            {
                var actualOffset = provider.GetOffsetNanosecondsFor(timeZone, instant);
                if (actualOffset == offsetNs.Value)
                {
                    return instant;
                }
            }

            // No match - fall through to normal disambiguation
        }

        return GetInstantFor(provider, timeZone, dateTime, disambiguation);
    }

    private BigInteger GetInstantFor(ITimeZoneProvider provider, string timeZone, IsoDateTime dateTime, string disambiguation)
    {
        return TemporalHelpers.GetEpochNanosecondsFor(_realm, provider, timeZone, dateTime, disambiguation);
    }

    private static BigInteger IsoDateTimeToNanoseconds(IsoDateTime dateTime)
    {
        var days = TemporalHelpers.IsoDateToDays(dateTime.Year, dateTime.Month, dateTime.Day);
        BigInteger ns = days;
        ns *= TemporalHelpers.NanosecondsPerDay;
        ns += dateTime.Time.TotalNanoseconds();
        return ns;
    }

    private static string GetDisambiguationOption(JsValue options, Realm realm)
    {
        if (options.IsUndefined())
            return "compatible";

        if (!options.IsObject())
        {
            Throw.TypeError(realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var value = obj.Get("disambiguation");
        if (value.IsUndefined())
            return "compatible";

        var str = TypeConverter.ToString(value);

        // Validate disambiguation value
        if (!string.Equals(str, "compatible", StringComparison.Ordinal) &&
            !string.Equals(str, "earlier", StringComparison.Ordinal) &&
            !string.Equals(str, "later", StringComparison.Ordinal) &&
            !string.Equals(str, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(realm, $"Invalid disambiguation option: {str}");
        }

        return str;
    }

    private static string GetOffsetOption(JsValue options, Realm realm)
    {
        if (options.IsUndefined())
            return "prefer";

        if (!options.IsObject())
        {
            Throw.TypeError(realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var value = obj.Get("offset");
        if (value.IsUndefined())
            return "prefer";

        var str = TypeConverter.ToString(value);

        // Validate offset value
        if (!string.Equals(str, "prefer", StringComparison.Ordinal) &&
            !string.Equals(str, "use", StringComparison.Ordinal) &&
            !string.Equals(str, "ignore", StringComparison.Ordinal) &&
            !string.Equals(str, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(realm, $"Invalid offset option: {str}");
        }

        return str;
    }


    private static bool IsValidTimeZoneNameOption(string value)
    {
        return string.Equals(value, "auto", StringComparison.Ordinal) ||
               string.Equals(value, "never", StringComparison.Ordinal) ||
               string.Equals(value, "critical", StringComparison.Ordinal);
    }
}

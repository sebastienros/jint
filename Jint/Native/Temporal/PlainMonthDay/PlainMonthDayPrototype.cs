using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plainmonthday-prototype-object
/// </summary>
internal sealed class PlainMonthDayPrototype : Prototype
{
    private readonly PlainMonthDayConstructor _constructor;

    internal PlainMonthDayPrototype(
        Engine engine,
        Realm realm,
        PlainMonthDayConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(11, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToTemporalString, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["toPlainDate"] = new(new ClrFunction(Engine, "toPlainDate", ToPlainDate, 1, LengthFlags), PropertyFlags),
            ["calendarId"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get calendarId", GetCalendarId, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["monthCode"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get monthCode", GetMonthCode, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["day"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get day", GetDay, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainMonthDay", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsPlainMonthDay ValidatePlainMonthDay(JsValue thisObject)
    {
        if (thisObject is JsPlainMonthDay plainMonthDay)
            return plainMonthDay;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainMonthDay");
        return null!;
    }

    // Getters
    private JsString GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainMonthDay(thisObject).Calendar);
    private JsString GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainMonthDay(thisObject).IsoDate.Month:D2}");
    private JsNumber GetDay(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainMonthDay(thisObject).IsoDate.Day);

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.with
    /// </summary>
    private JsPlainMonthDay With(JsValue thisObject, JsCallArguments arguments)
    {
        var md = ValidatePlainMonthDay(thisObject);
        var temporalMonthDayLike = arguments.At(0);
        var optionsArg = arguments.At(1);

        if (!temporalMonthDayLike.IsObject())
        {
            Throw.TypeError(_realm, "with argument must be an object");
        }

        var obj = temporalMonthDayLike.AsObject();

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

        // Read and convert properties in strict alphabetical order per spec: day, month, monthCode, year
        // 1. day
        var dayProp = obj.Get("day");
        var day = dayProp.IsUndefined() ? md.IsoDate.Day : TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayProp);

        // 2. month
        var monthProp = obj.Get("month");
        var month = monthProp.IsUndefined() ? md.IsoDate.Month : TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthProp);

        // 3. monthCode - read but don't validate yet
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

        // 4. year (read but use reference year for PlainMonthDay)
        var yearProp = obj.Get("year");
        var year = yearProp.IsUndefined() ? md.IsoDate.Year : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearProp);

        // Validate that at least one temporal field was provided (IsPartialTemporalObject)
        if (dayProp.IsUndefined() && monthProp.IsUndefined() && monthCodeProp.IsUndefined() && yearProp.IsUndefined())
        {
            Throw.TypeError(_realm, "with argument must have at least one temporal property");
        }

        // Read options BEFORE any validation (per spec)
        var overflow = TemporalHelpers.GetOverflowOption(_realm, optionsArg);

        // For non-ISO calendars, monthCode is required when month is provided
        if (!string.Equals(md.Calendar, "iso8601", StringComparison.Ordinal) &&
            !monthProp.IsUndefined() && monthCodeProp.IsUndefined())
        {
            Throw.TypeError(_realm, "monthCode is required for non-ISO calendars");
        }

        // NOW validate monthCode (after options are read)
        int? monthFromCode = null;
        if (monthCode is not null)
        {
            monthFromCode = TemporalHelpers.ParseMonthCode(_realm, monthCode);

            // For ISO 8601 calendar: validate monthCode is valid (01-12, no leap months)
            if (monthCode.Length == 4 && monthCode[3] == 'L')
            {
                Throw.RangeError(_realm, $"Leap months are not valid for ISO 8601 calendar: {monthCode}");
            }

            if (monthFromCode.Value < 1 || monthFromCode.Value > 12)
            {
                Throw.RangeError(_realm, $"Month {monthFromCode.Value} is not valid for ISO 8601 calendar");
            }
        }

        // Validate month/monthCode consistency
        if (monthFromCode.HasValue && !monthProp.IsUndefined() && month != monthFromCode.Value)
        {
            Throw.RangeError(_realm, "month and monthCode must match");
        }

        // Use monthCode if provided
        if (monthFromCode.HasValue)
        {
            month = monthFromCode.Value;
        }

        // Validate using the provided year (important for leap day validation)
        var date = TemporalHelpers.RegulateIsoDate(year, month, day, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid month-day");
        }

        // Choose reference year for result (per spec: not the same as the validation year)
        // Use 1972 (a leap year) to ensure Feb 29 is always valid
        var referenceYear = 1972;
        var resultDate = new IsoDate(referenceYear, date.Value.Month, date.Value.Day);

        return _constructor.Construct(resultDate, md.Calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.equals
    /// </summary>
    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var md = ValidatePlainMonthDay(thisObject);
        var other = _constructor.ToTemporalMonthDay(arguments.At(0), "constrain");

        return md.IsoDate.Year == other.IsoDate.Year &&
               md.IsoDate.Month == other.IsoDate.Month &&
               md.IsoDate.Day == other.IsoDate.Day &&
               string.Equals(md.Calendar, other.Calendar, StringComparison.Ordinal)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.tostring
    /// </summary>
    private JsString ToTemporalString(JsValue thisObject, JsCallArguments arguments)
    {
        var md = ValidatePlainMonthDay(thisObject);
        var optionsValue = arguments.At(0);
        var options = TemporalHelpers.GetOptionsObject(_realm, optionsValue);
        var showCalendar = GetCalendarNameOption(options);

        // For non-ISO calendars, always include the year (month-day alone is ambiguous)
        // The year is needed regardless of calendarName option
        var isNonIsoCalendar = !string.Equals(md.Calendar, "iso8601", StringComparison.Ordinal);

        // Include the calendar annotation based on the calendarName option
        var includeCalendar = string.Equals(showCalendar, "always", StringComparison.Ordinal) ||
                              string.Equals(showCalendar, "critical", StringComparison.Ordinal) ||
                              (string.Equals(showCalendar, "auto", StringComparison.Ordinal) && isNonIsoCalendar);

        // Include year for non-ISO calendar or when calendar annotation is shown
        var includeYear = isNonIsoCalendar || includeCalendar;

        string result;
        if (includeYear)
        {
            var yearStr = TemporalHelpers.PadIsoYear(md.IsoDate.Year);
            if (includeCalendar)
            {
                // Add critical flag (!) if showCalendar is "critical"
                var criticalFlag = string.Equals(showCalendar, "critical", StringComparison.Ordinal) ? "!" : "";
                result = $"{yearStr}-{md.IsoDate.Month:D2}-{md.IsoDate.Day:D2}[{criticalFlag}u-ca={md.Calendar}]";
            }
            else
            {
                result = $"{yearStr}-{md.IsoDate.Month:D2}-{md.IsoDate.Day:D2}";
            }
        }
        else
        {
            result = $"{md.IsoDate.Month:D2}-{md.IsoDate.Day:D2}";
        }

        return new JsString(result);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.tojson
    /// </summary>
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var md = ValidatePlainMonthDay(thisObject);
        // toJSON uses "auto" for calendarName: include year+calendar for non-ISO
        if (!string.Equals(md.Calendar, "iso8601", StringComparison.Ordinal))
        {
            var yearStr = TemporalHelpers.PadIsoYear(md.IsoDate.Year);
            return new JsString($"{yearStr}-{md.IsoDate.Month:D2}-{md.IsoDate.Day:D2}[u-ca={md.Calendar}]");
        }

        return new JsString($"{md.IsoDate.Month:D2}-{md.IsoDate.Day:D2}");
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sup-temporal.plainmonthday.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var md = ValidatePlainMonthDay(thisObject);
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Per spec: CreateDateTimeFormat with required=~date~, defaults=~date~
        // But for PlainMonthDay, we use month-day specific defaults (no year)
        var dtf = _realm.Intrinsics.DateTimeFormat.CreateDateTimeFormat(
            locales, options, required: Intl.DateTimeRequired.Date, defaults: Intl.DateTimeDefaults.MonthDay);

        // Calendar mismatch check: PlainMonthDay calendar must match DTF calendar exactly
        var cal = md.Calendar;
        if (dtf.Calendar != null && !string.Equals(cal, dtf.Calendar, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, $"Calendar mismatch: PlainMonthDay uses '{cal}' but DateTimeFormat uses '{dtf.Calendar}'");
        }

        return dtf.Format(new DateTime(md.IsoDate.Year, md.IsoDate.Month, md.IsoDate.Day, 12, 0, 0, DateTimeKind.Unspecified), isPlain: true);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainMonthDay cannot be converted to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.prototype.toplaindate
    /// </summary>
    private JsPlainDate ToPlainDate(JsValue thisObject, JsCallArguments arguments)
    {
        var md = ValidatePlainMonthDay(thisObject);
        var item = arguments.At(0);

        if (!item.IsObject())
        {
            Throw.TypeError(_realm, "toPlainDate requires an object argument");
        }

        var obj = item.AsObject();

        // Read era/eraYear for era-supporting calendars (alphabetically before year)
        var eraYear = TemporalHelpers.ReadEraFields(_realm, obj, md.Calendar);

        int year;
        if (eraYear.HasValue)
        {
            year = eraYear.Value;
            obj.Get("year");
        }
        else
        {
            var yearProp = obj.Get("year");
            if (yearProp.IsUndefined())
            {
                Throw.TypeError(_realm, "year is required");
            }

            year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearProp);
        }

        var date = TemporalHelpers.RegulateIsoDate(year, md.IsoDate.Month, md.IsoDate.Day, "constrain");
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        return _engine.Realm.Intrinsics.TemporalPlainDate.Construct(date.Value, md.Calendar);
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

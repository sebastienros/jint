using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.plaindate
/// </summary>
internal sealed class PlainDateConstructor : Constructor
{
    private static readonly JsString _functionName = new("PlainDate");

    internal PlainDateConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PlainDatePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(3), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PlainDatePrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(2, checkExistingKeys: false)
        {
            ["from"] = new(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags),
            ["compare"] = new(new ClrFunction(Engine, "compare", Compare, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.from
    /// </summary>
    private JsPlainDate From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        var optionsValue = arguments.At(1);

        // For PlainDate/PlainDateTime/ZonedDateTime, validate options first (for observable side effects) then convert
        if (item is JsPlainDate || item is JsPlainDateTime || item is JsZonedDateTime)
        {
            // Read options first (per spec, options are accessed before conversion)
            var overflow = "constrain"; // Default for these types, options not actually used
            if (!optionsValue.IsUndefined())
            {
                overflow = TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            // Now perform the conversion (overflow is ignored for these types)
            return ToTemporalDate(item, "constrain");
        }

        // For strings, parse first (fail fast if invalid), then read options
        if (item.IsString())
        {
            // Parse string first - this will throw if string is invalid
            var result = ToTemporalDate(item, "constrain");
            // Only read options if parsing succeeded
            if (!optionsValue.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            return result;
        }

        // For object, read fields first, then options (per spec order)
        if (item.IsObject())
        {
            return ToTemporalDateFromObjectWithOptions(item.AsObject(), optionsValue);
        }

        Throw.TypeError(_realm, "Invalid date");
        return null!;
    }

    private JsPlainDate ToTemporalDateFromObjectWithOptions(ObjectInstance obj, JsValue options)
    {
        // Read and convert properties in alphabetical order per spec: calendar, day, era, eraYear, month, monthCode, year
        // Each property must be fully read and converted before moving to the next

        // 1. calendar
        var calendarValue = obj.Get("calendar");
        string calendar = "iso8601";
        if (!calendarValue.IsUndefined())
        {
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            // This handles Temporal objects (fast path) and string calendars
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarValue);
        }

        // 2. day - read and convert immediately
        var dayValue = obj.Get("day");
        if (dayValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Missing required property: day");
        }

        var day = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayValue);

        // 2.5. era/eraYear - read for era-supporting calendars (alphabetically between day and month)
        var eraYear = TemporalHelpers.ReadEraFields(_realm, obj, calendar);

        // 3. month - read and convert immediately
        var monthValue = obj.Get("month");
        int month = 0;
        if (!monthValue.IsUndefined())
        {
            month = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthValue);
        }

        // 4. monthCode - read and convert immediately, validate well-formedness
        var monthCodeValue = obj.Get("monthCode");
        string? monthCodeStr = null;
        int monthFromCode = 0;
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

                monthCodeStr = primitive.ToString();
            }
            else if (monthCodeValue.IsString())
            {
                // String primitive - use directly (but call ToString for any side effects)
                monthCodeStr = TypeConverter.ToString(monthCodeValue);
            }
            else
            {
                // Number, BigInt, Boolean, Null - reject
                // Symbol - will throw TypeError from ToString
                if (monthCodeValue.Type != Types.Symbol)
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }

                // Let ToString throw for Symbol
                monthCodeStr = TypeConverter.ToString(monthCodeValue);
            }

            // Validate well-formedness (format) - this happens before year type validation
            monthFromCode = TemporalHelpers.ParseMonthCode(_realm, monthCodeStr);

            // If both month and monthCode are provided, they must match
            if (month != 0 && month != monthFromCode)
            {
                Throw.RangeError(_realm, "month and monthCode do not match");
            }

            month = monthFromCode;
        }

        // 5. year - use eraYear if computed, otherwise read from property
        int year;
        if (eraYear.HasValue)
        {
            // Year was already computed from era/eraYear
            year = eraYear.Value;
            // Still read the year property for observable side effects, but don't require it
            obj.Get("year");
        }
        else
        {
            var yearValue = obj.Get("year");
            if (yearValue.IsUndefined())
            {
                Throw.TypeError(_realm, "Missing required property: year");
            }

            year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearValue);
        }

        // 6. Read options AFTER all fields (but BEFORE algorithmic validation)
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        // Validate monthCode suitability - only for ISO/Gregorian calendars
        if (monthCodeStr is not null && TemporalHelpers.IsGregorianBasedCalendar(calendar))
        {
            // For ISO 8601 calendar: validate monthCode is valid (01-12, no leap months)
            if (monthCodeStr.Length == 4 && monthCodeStr[3] == 'L')
            {
                Throw.RangeError(_realm, $"Leap months are not valid for ISO 8601 calendar: {monthCodeStr}");
            }

            if (monthFromCode < 1 || monthFromCode > 12)
            {
                Throw.RangeError(_realm, $"Month {monthFromCode} is not valid for ISO 8601 calendar");
            }
        }

        if (month == 0)
        {
            Throw.TypeError(_realm, "month or monthCode is required");
        }

        var date = TemporalHelpers.CalendarDateToISO(_realm, calendar, year, month, day, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        return Construct(date.Value, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalDate(arguments.At(0), "constrain");
        var two = ToTemporalDate(arguments.At(1), "constrain");
        return JsNumber.Create(TemporalHelpers.CompareIsoDates(one.IsoDate, two.IsoDate));
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDate cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindate
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(0));
        var month = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(1));
        var day = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(2));
        var calendarArg = arguments.At(3);

        string calendar;
        if (calendarArg.IsUndefined())
        {
            calendar = "iso8601";
        }
        else
        {
            // Calendar argument must be a calendar ID, not an ISO string
            // Check the original input before canonicalization
            if (calendarArg.IsString())
            {
                var calendarStr = calendarArg.ToString();
                if (TemporalHelpers.LooksLikeIsoDateString(calendarStr))
                {
                    Throw.RangeError(_realm, $"Unsupported calendar: {calendarStr}");
                }
            }

            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            // This handles Temporal objects (fast path) and string calendars
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarArg);
        }

        var date = TemporalHelpers.RegulateIsoDate(year, month, day, "reject");
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        return Construct(date.Value, calendar, newTarget);
    }

    internal JsPlainDate Construct(IsoDate isoDate, string calendar = "iso8601", JsValue? newTarget = null)
    {
        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalPlainDate.PrototypeObject);

        return new JsPlainDate(_engine, proto, isoDate, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporaldate
    /// </summary>
    internal JsPlainDate ToTemporalDate(JsValue item, string overflow)
    {
        if (item is JsPlainDate plainDate)
        {
            // Create a copy rather than returning the same object
            return Construct(plainDate.IsoDate, plainDate.Calendar);
        }

        if (item is JsPlainDateTime plainDateTime)
        {
            return Construct(plainDateTime.IsoDateTime.Date, plainDateTime.Calendar);
        }

        if (item is JsZonedDateTime zonedDateTime)
        {
            var dt = zonedDateTime.GetIsoDateTime();
            return Construct(dt.Date, zonedDateTime.Calendar);
        }

        if (item.IsString())
        {
            var str = item.ToString();
            var parsed = ParseDateString(str);
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid date string");
            }

            return Construct(parsed.Value.Date, parsed.Value.Calendar ?? "iso8601");
        }

        if (item.IsObject())
        {
            var obj = item.AsObject();
            return ToTemporalDateFromFields(obj, overflow);
        }

        Throw.TypeError(_realm, "Invalid date");
        return null!;
    }

    private JsPlainDate ToTemporalDateFromFields(ObjectInstance obj, string overflow)
    {
        // Read and convert properties in alphabetical order per spec: calendar, day, month, monthCode, year
        // Each property must be fully read and converted before moving to the next

        // 1. calendar
        var calendarValue = obj.Get("calendar");
        string calendar = "iso8601";
        if (!calendarValue.IsUndefined())
        {
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            // This handles Temporal objects (fast path) and string calendars
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarValue);
        }

        // 2. day - read and convert immediately
        var dayValue = obj.Get("day");
        if (dayValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Missing required property: day");
        }

        var day = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayValue);

        // 2.5. era/eraYear - read for era-supporting calendars
        var eraYear = TemporalHelpers.ReadEraFields(_realm, obj, calendar);

        // 3. month - read and convert immediately
        var monthValue = obj.Get("month");
        int month = 0;
        if (!monthValue.IsUndefined())
        {
            month = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthValue);
        }

        // 4. monthCode - read and convert immediately
        var monthCodeValue = obj.Get("monthCode");
        int? monthFromCode = null;
        string? code = null;
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

                code = primitive.ToString();
            }
            else if (monthCodeValue.IsString())
            {
                code = TypeConverter.ToString(monthCodeValue);
            }
            else
            {
                // Number, BigInt, Boolean, Null - reject; Symbol throws from ToString
                if (monthCodeValue.Type != Types.Symbol)
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }

                code = TypeConverter.ToString(monthCodeValue);
            }

            if (code.Length >= 2 && code[0] == 'M')
            {
                if (int.TryParse(code.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMonth))
                {
                    monthFromCode = parsedMonth;
                }
            }

            // If we couldn't parse monthCode, it's invalid
            if (!monthFromCode.HasValue)
            {
                Throw.RangeError(_realm, $"Invalid monthCode: {code}");
            }
        }

        // 5. year - use eraYear if computed, otherwise read from property
        int year;
        if (eraYear.HasValue)
        {
            year = eraYear.Value;
            obj.Get("year");
        }
        else
        {
            var yearValue = obj.Get("year");
            if (yearValue.IsUndefined())
            {
                Throw.TypeError(_realm, "Missing required property: year");
            }

            year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearValue);
        }

        // Validate: both month and monthCode provided - they must match
        if (month != 0 && monthFromCode.HasValue && month != monthFromCode.Value)
        {
            Throw.RangeError(_realm, "month and monthCode must match");
        }

        // Use whichever is provided
        if (monthFromCode.HasValue)
        {
            month = monthFromCode.Value;
        }

        if (month == 0)
        {
            Throw.TypeError(_realm, "month or monthCode is required");
        }

        var date = TemporalHelpers.CalendarDateToISO(_realm, calendar, year, month, day, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        return Construct(date.Value, calendar);
    }

    private static ParsedDateResult? ParseDateString(string input)
    {
        // Empty strings are invalid
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        // Strip annotations and extract calendar if present
        var error = TemporalHelpers.StripAnnotations(input, out var coreString, out var calendar);
        if (error is not null)
        {
            // Annotation parsing error - return null to let caller throw the error
            // (or we could return an error in ParsedDateResult, but null is simpler)
            return null;
        }

        // Check for negative zero year
        var firstDash = coreString.IndexOf('-', 1);
        if (firstDash > 0)
        {
            var yearStr = coreString.Substring(0, firstDash);
            if (TemporalHelpers.IsNegativeZeroYear(yearStr))
            {
                return null;
            }
        }

        // Check if there's a UTC offset or Z without a time component
        // UTC offsets are only valid when there's a time component (T, t, or space)
        // Per spec: date-only strings like "2022-09-15Z" or "2022-09-15+00:00" are invalid
        // But "2000-05-02T00Z" or "2000-05-02T00+00:00" are valid (time present, offset allowed)
        var hasTimeSeparator = coreString.Contains('T') || coreString.Contains('t') || coreString.Contains(' ');
        if (!hasTimeSeparator)
        {
            // For date-only strings (no time component), reject UTC designators and offsets
            // Standard date: "YYYY-MM-DD" (2 dashes)
            // Extended year: "±YYYYYY-MM-DD" (3 dashes: sign + 2 separators)
            // Date with offset: "YYYY-MM-DD±HH:MM" (3+ dashes) - INVALID

            var isExtendedYear = coreString.Length > 0 && (coreString[0] == '+' || coreString[0] == '-');
            var expectedDashes = isExtendedYear ? 3 : 2;
            var dashCount = 0;

            for (var i = 0; i < coreString.Length; i++)
            {
                var c = coreString[i];

                // Z is always invalid for date-only strings
                if (c == 'Z' || c == 'z')
                {
                    return null;
                }

                // + at any position except 0 (extended year sign) is an offset sign
                if (c == '+' && i > 0)
                {
                    return null;
                }

                // Count dashes
                if (c == '-')
                {
                    dashCount++;
                    // More dashes than expected for a valid date means there's an offset
                    if (dashCount > expectedDashes)
                    {
                        return null;
                    }
                }
            }
        }

        // Try parsing as simple date
        var parsed = TemporalHelpers.ParseIsoDate(coreString);
        if (parsed is not null)
        {
            // Validate date limits
            if (!TemporalHelpers.IsValidIsoDateTime(parsed.Value.Year, parsed.Value.Month, parsed.Value.Day))
            {
                return null;
            }

            return new ParsedDateResult(parsed.Value, calendar ?? "iso8601");
        }

        // Try parsing date-time string and extract date part
        var tIndex = coreString.IndexOf('T');
        if (tIndex < 0)
            tIndex = coreString.IndexOf('t');
        if (tIndex < 0)
            tIndex = coreString.IndexOf(' ');
        if (tIndex > 0)
        {
            var dateString = coreString.Substring(0, tIndex);
            parsed = TemporalHelpers.ParseIsoDate(dateString);
            if (parsed is not null)
            {
                // Validate that the time component (after T) is well-formed
                // Extract everything after the date: time, offset, etc.
                var timeAndOffset = coreString.Substring(tIndex + 1);
                if (!TemporalHelpers.IsValidTimeWithOffset(timeAndOffset))
                {
                    return null;
                }

                // PlainDate must not have Z designator (UTC indicator)
                // Per spec, PlainDate represents a calendar date without time zone information
                if (timeAndOffset.Contains('Z') || timeAndOffset.Contains('z'))
                {
                    return null;
                }

                // Validate date limits
                if (!TemporalHelpers.IsValidIsoDateTime(parsed.Value.Year, parsed.Value.Month, parsed.Value.Day))
                {
                    return null;
                }

                return new ParsedDateResult(parsed.Value, calendar ?? "iso8601");
            }
        }

        return null;
    }

    private readonly record struct ParsedDateResult(IsoDate Date, string? Calendar);
}

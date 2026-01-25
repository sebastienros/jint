using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday
/// </summary>
internal sealed class PlainMonthDayConstructor : Constructor
{
    private static readonly JsString _functionName = new("PlainMonthDay");
    private static readonly char[] TimeSuffixChars = { 'T', ' ', '[' };

    internal PlainMonthDayConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PlainMonthDayPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PlainMonthDayPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["from"] = new(new ClrFunction(Engine, "from", From, 1, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday.from
    /// </summary>
    private JsPlainMonthDay From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        var optionsValue = arguments.At(1);

        // For existing PlainMonthDay (cloning), validate options first then convert
        if (item is JsPlainMonthDay)
        {
            // Read options first (per spec, options are accessed before conversion)
            if (!optionsValue.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            return ToTemporalMonthDay(item, "constrain");
        }

        // For strings, parse first (fail fast if invalid), then read options
        if (item.IsString())
        {
            // Parse string first - this will throw if string is invalid
            var result = ToTemporalMonthDay(item, "constrain");
            // Only read options if parsing succeeded
            if (!optionsValue.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            return result;
        }

        // For objects, read fields first, then options (order matters for observable side effects)
        if (item.IsObject())
        {
            var obj = item.AsObject();
            return ToTemporalMonthDayFromFields(obj, optionsValue);
        }

        Throw.TypeError(_realm, "Invalid month-day");
        return null!;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainMonthDay cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainmonthday
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var month = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(0));
        var day = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(1));
        var calendarArg = arguments.At(2);
        var referenceYear = arguments.At(3);

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
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarArg);
        }

        // Use a reference year - 1972 is a leap year so Feb 29 is valid
        var year = referenceYear.IsUndefined() ? 1972 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, referenceYear);

        var date = TemporalHelpers.RegulateIsoDate(year, month, day, "reject");
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid month-day");
        }

        return Construct(date.Value, calendar, newTarget);
    }

    internal JsPlainMonthDay Construct(IsoDate isoDate, string calendar = "iso8601", JsValue? newTarget = null)
    {
        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalPlainMonthDay.PrototypeObject);

        return new JsPlainMonthDay(_engine, proto, isoDate, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalmonthday
    /// </summary>
    internal JsPlainMonthDay ToTemporalMonthDay(JsValue item, string overflow)
    {
        if (item is JsPlainMonthDay plainMonthDay)
        {
            // Return a copy, not the same object
            return Construct(plainMonthDay.IsoDate, plainMonthDay.Calendar);
        }

        if (item.IsString())
        {
            var str = item.ToString();
            var parsed = ParseMonthDayString(str);
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid month-day string");
            }

            return Construct(parsed.Value, "iso8601");
        }

        if (item.IsObject())
        {
            // For internal callers that pass objects, use the overflow parameter
            var obj = item.AsObject();
            return ToTemporalMonthDayFromFields(obj, Undefined);
        }

        Throw.TypeError(_realm, "Invalid month-day");
        return null!;
    }

    private JsPlainMonthDay ToTemporalMonthDayFromFields(ObjectInstance obj, JsValue optionsValue)
    {
        // Read and process properties in alphabetical order per spec: calendar, day, month, monthCode, year
        // Each property must be read and converted before moving to the next

        // 1. calendar - read and process immediately
        var calendarValue = obj.Get("calendar");
        string calendar = "iso8601";
        if (!calendarValue.IsUndefined())
        {
            // Calendar must be a string, not other types
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarValue);
        }

        // 2. day - read and convert immediately
        var dayValue = obj.Get("day");
        if (dayValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Missing required property: day");
        }

        var day = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayValue);

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
        int? monthFromCode = null;
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
                monthCodeStr = TypeConverter.ToString(monthCodeValue);
            }
            else
            {
                // Number, BigInt, Boolean, Null - reject; Symbol throws from ToString
                if (monthCodeValue.Type != Types.Symbol)
                {
                    Throw.TypeError(_realm, "monthCode must be a string");
                }

                monthCodeStr = TypeConverter.ToString(monthCodeValue);
            }

            // Validate well-formedness (format) - this happens before year type validation
            monthFromCode = TemporalHelpers.ParseMonthCode(_realm, monthCodeStr);
        }

        // 5. year - read and convert immediately (TYPE validation happens here)
        var yearValue = obj.Get("year");
        var year = yearValue.IsUndefined() ? 1972 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, yearValue);

        // 6. Read options.overflow AFTER all fields (but BEFORE algorithmic validation)
        var overflow = optionsValue.IsUndefined() ? "constrain" : TemporalHelpers.GetOverflowOption(_realm, optionsValue);

        // NOW validate monthCode suitability (ISO calendar checks) - after year type validation AND options reading
        if (monthCodeStr is not null)
        {
            // For ISO 8601 calendar: validate monthCode is valid (01-12, no leap months)
            if (monthCodeStr.Length == 4 && monthCodeStr[3] == 'L')
            {
                Throw.RangeError(_realm, $"Leap months are not valid for ISO 8601 calendar: {monthCodeStr}");
            }

            if (monthFromCode!.Value < 1 || monthFromCode.Value > 12)
            {
                Throw.RangeError(_realm, $"Month {monthFromCode.Value} is not valid for ISO 8601 calendar");
            }
        }

        // Now validate and combine month/monthCode
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

        // Use input year for validation, but always use 1972 as reference year in result
        // Per spec (calendar.html CalendarMonthDayToISOReferenceDate):
        // "The reference year is always 1972"
        var date = TemporalHelpers.RegulateIsoDate(year, month, day, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid month-day");
        }

        // Store with reference year 1972 (the type represents just month+day)
        return Construct(new IsoDate(1972, date.Value.Month, date.Value.Day), calendar);
    }

    private static IsoDate? ParseMonthDayString(string input)
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
            // Annotation parsing error
            return null;
        }
        // Note: calendar is ignored for PlainMonthDay (always uses iso8601)

        // PlainMonthDay cannot have UTC designator
        if (coreString.Contains('Z'))
        {
            return null;
        }

        // Check if there's a UTC offset or Z without a time component
        // UTC offsets are only valid when there's a time component (T, t, or space)
        var hasTimeSeparator = coreString.Contains('T') || coreString.Contains('t') || coreString.Contains(' ');
        if (!hasTimeSeparator)
        {
            // Check for Z or offset after initial portion (position 2+ for --MM-DD or MM-DD formats)
            for (var i = 2; i < coreString.Length; i++)
            {
                var c = coreString[i];
                if (c == 'Z' || c == 'z' || c == '+')
                {
                    // Found Z or + offset without time component - invalid
                    return null;
                }
            }
        }

        // Try parsing as --MM-DD or --MMDD format (ISO 8601 month-day)
        if (coreString.Length >= 6 && coreString.StartsWith("--", StringComparison.Ordinal))
        {
            var rest = coreString.Substring(2);
            var dashIndex = rest.IndexOf('-');
            if (dashIndex >= 0)
            {
                var monthStr = rest.Substring(0, dashIndex);
                var dayStr = rest.Substring(dashIndex + 1);

                // Remove any suffix
                var suffixIndex = dayStr.IndexOfAny(TimeSuffixChars);
                if (suffixIndex >= 0)
                {
                    dayStr = dayStr.Substring(0, suffixIndex);
                }

                if (int.TryParse(monthStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var month) &&
                    int.TryParse(dayStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var day))
                {
                    // Use 1972 as reference year (leap year)
                    var date = TemporalHelpers.RegulateIsoDate(1972, month, day, "reject");
                    if (date is not null)
                    {
                        return date.Value;
                    }
                }
            }

            // Try parsing as --MMDD format (compact ISO month-day)
            if (dashIndex < 0 && rest.Length == 4 && int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out var compactValue))
            {
                var month = compactValue / 100;
                var day = compactValue % 100;
                var date = TemporalHelpers.RegulateIsoDate(1972, month, day, "reject");
                if (date is not null)
                {
                    return date.Value;
                }
            }
        }

        // Try parsing as MMDD format (4-digit compact month-day without dashes)
        if (coreString.Length == 4 && !coreString.Contains('-') && !hasTimeSeparator &&
            int.TryParse(coreString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mmdd))
        {
            var month = mmdd / 100;
            var day = mmdd % 100;
            var date = TemporalHelpers.RegulateIsoDate(1972, month, day, "reject");
            if (date is not null)
            {
                return date.Value;
            }
        }

        // Try parsing as MM-DD format (but not full dates like YYYY-MM-DD)
        if (coreString.Length >= 5 && coreString.Length <= 10)
        {
            var dashIndex = coreString.IndexOf('-');
            if (dashIndex == 2)
            {
                var monthStr = coreString.Substring(0, dashIndex);
                var rest = coreString.Substring(dashIndex + 1);

                // Check if this looks like a month-day (not a full date)
                var nextDash = rest.IndexOf('-');
                if (nextDash < 0)
                {
                    var dayStr = rest;
                    var suffixIndex = dayStr.IndexOfAny(TimeSuffixChars);
                    if (suffixIndex >= 0)
                    {
                        dayStr = dayStr.Substring(0, suffixIndex);
                    }

                    if (int.TryParse(monthStr, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var month) &&
                        int.TryParse(dayStr, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var day))
                    {
                        var date = TemporalHelpers.RegulateIsoDate(1972, month, day, "reject");
                        if (date is not null)
                        {
                            return date.Value;
                        }
                    }
                }
            }
        }

        // Try parsing as full date and extract month-day
        return TryParseFullDateAsMonthDay(coreString);
    }

    private static IsoDate? TryParseFullDateAsMonthDay(string input)
    {
        var parsed = TemporalHelpers.ParseIsoDate(input);
        if (parsed is not null)
        {
            // For PlainMonthDay, we only extract month and day — year range validation is not needed
            // Use 1972 as reference year (leap year, so Feb 29 is valid)
            return new IsoDate(1972, parsed.Value.Month, parsed.Value.Day);
        }

        // Try parsing date-time string
        var tIndex = input.IndexOf('T');
        if (tIndex < 0)
        {
            tIndex = input.IndexOf('t');
        }

        if (tIndex < 0)
        {
            tIndex = input.IndexOf(' ');
        }

        if (tIndex > 0)
        {
            var dateString = input.Substring(0, tIndex);
            parsed = TemporalHelpers.ParseIsoDate(dateString);
            if (parsed is not null)
            {
                // Validate that the time component (after T) is well-formed
                // Extract everything after the date: time, offset, etc.
                var timeAndOffset = input.Substring(tIndex + 1);
                if (!TemporalHelpers.IsValidTimeWithOffset(timeAndOffset))
                {
                    return null;
                }

                // Validate the original date is within limits
                if (!TemporalHelpers.IsValidIsoDateTime(parsed.Value.Year, parsed.Value.Month, parsed.Value.Day))
                {
                    return null;
                }

                return new IsoDate(1972, parsed.Value.Month, parsed.Value.Day);
            }
        }

        return null;
    }
}

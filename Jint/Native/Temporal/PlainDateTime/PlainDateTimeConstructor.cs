using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime
/// </summary>
internal sealed class PlainDateTimeConstructor : Constructor
{
    private static readonly JsString _functionName = new("PlainDateTime");

    internal PlainDateTimeConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PlainDateTimePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(3), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PlainDateTimePrototype PrototypeObject { get; }

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
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.from
    /// </summary>
    private JsPlainDateTime From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        var optionsValue = arguments.At(1);

        // For existing Temporal types (cloning), validate options first then convert
        if (item is JsPlainDateTime || item is JsPlainDate || item is JsZonedDateTime)
        {
            // Read options first (per spec, options are accessed before conversion)
            if (!optionsValue.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            return ToTemporalDateTime(item, "constrain");
        }

        // For strings, parse first (fail fast if invalid), then read options
        if (item.IsString())
        {
            // Parse string first - this will throw if string is invalid
            var result = ToTemporalDateTime(item, "constrain");
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
            return ToTemporalDateTimeFromFields(obj, optionsValue);
        }

        Throw.TypeError(_realm, "Invalid datetime");
        return null!;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalDateTime(arguments.At(0), "constrain");
        var two = ToTemporalDateTime(arguments.At(1), "constrain");
        return JsNumber.Create(TemporalHelpers.CompareIsoDateTimes(one.IsoDateTime, two.IsoDateTime));
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainDateTime cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaindatetime
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(0));
        var month = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(1));
        var day = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(2));
        var hour = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(3), 0);
        var minute = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(4), 0);
        var second = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(5), 0);
        var millisecond = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(6), 0);
        var microsecond = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(7), 0);
        var nanosecond = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(8), 0);
        var calendarArg = arguments.At(9);

        string calendar;
        if (calendarArg.IsUndefined())
        {
            calendar = "iso8601";
        }
        else
        {
            // Calendar must be a string, not other types
            if (!calendarArg.IsString())
            {
                Throw.TypeError(_realm, "calendar must be a string");
            }

            var calendarStr = TypeConverter.ToString(calendarArg);

            // Calendar argument must be a calendar ID, not an ISO string
            if (TemporalHelpers.LooksLikeIsoDateString(calendarStr))
            {
                Throw.RangeError(_realm, $"Unsupported calendar: {calendarStr}");
            }

            var canonical = TemporalHelpers.CanonicalizeCalendar(calendarStr);
            if (canonical is null)
            {
                Throw.RangeError(_realm, $"Unsupported calendar: {calendarStr}");
            }

            calendar = canonical;
        }

        // Validate ISO date limits
        if (!TemporalHelpers.IsValidIsoDateTime(year, month, day))
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        var date = TemporalHelpers.RegulateIsoDate(year, month, day, "reject");
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, "reject");
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        var dateTime = new IsoDateTime(date.Value, time.Value);
        return Construct(dateTime, calendar, newTarget);
    }

    internal JsPlainDateTime Construct(IsoDateTime isoDateTime, string calendar = "iso8601", JsValue? newTarget = null)
    {
        // ISODateTimeWithinLimits check
        if (!TemporalHelpers.ISODateTimeWithinLimits(isoDateTime))
        {
            Throw.RangeError(_realm, "DateTime is outside the representable range");
        }

        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalPlainDateTime.PrototypeObject);

        return new JsPlainDateTime(_engine, proto, isoDateTime, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporaldatetime
    /// </summary>
    internal JsPlainDateTime ToTemporalDateTime(JsValue item, string overflow)
    {
        if (item is JsPlainDateTime plainDateTime)
        {
            // Return a COPY, not the original instance (per spec: from() always creates a new object)
            return Construct(plainDateTime.IsoDateTime, plainDateTime.Calendar);
        }

        if (item is JsPlainDate plainDate)
        {
            var time = new IsoTime(0, 0, 0, 0, 0, 0);
            var dateTime = new IsoDateTime(plainDate.IsoDate, time);
            return Construct(dateTime, plainDate.Calendar);
        }

        if (item is JsZonedDateTime zonedDateTime)
        {
            return Construct(zonedDateTime.GetIsoDateTime(), zonedDateTime.Calendar);
        }

        if (item.IsString())
        {
            var str = item.ToString();
            var parsed = ParseDateTimeString(str);
            if (parsed.Error is not null)
            {
                Throw.RangeError(_realm, parsed.Error);
            }

            if (parsed.DateTime is null)
            {
                Throw.RangeError(_realm, "Invalid date-time string");
            }

            return Construct(parsed.DateTime.Value, TemporalHelpers.ExtractCalendarIdentifierFromString(str));
        }

        if (item.IsObject())
        {
            // For internal callers that pass objects, use the overflow parameter
            var obj = item.AsObject();
            return ToTemporalDateTimeFromFields(obj, Undefined);
        }

        Throw.TypeError(_realm, "Invalid date-time");
        return null!;
    }

    private JsPlainDateTime ToTemporalDateTimeFromFields(ObjectInstance obj, JsValue optionsValue)
    {
        // Read and process properties in alphabetical order per spec
        // Each property must be read and converted before moving to the next
        // Order: calendar, day, hour, microsecond, millisecond, minute, month, monthCode, nanosecond, second, year

        // 1. calendar - read and process immediately
        var calendarProp = obj.Get("calendar");
        string calendar = "iso8601";
        if (!calendarProp.IsUndefined())
        {
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            // This handles Temporal objects (fast path) and string calendars
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarProp);
        }

        // 2. day - read and convert immediately
        var dayValue = obj.Get("day");
        if (dayValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Missing required property: day");
        }

        var day = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, dayValue);

        // 2.5. era/eraYear - read for era-supporting calendars (alphabetically between day and hour)
        var eraYear = TemporalHelpers.ReadEraFields(_realm, obj, calendar);

        // 3. hour - read and convert immediately
        var hourValue = obj.Get("hour");
        var hour = hourValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, hourValue);

        // 4. microsecond - read and convert immediately
        var microsecondValue = obj.Get("microsecond");
        var microsecond = microsecondValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, microsecondValue);

        // 5. millisecond - read and convert immediately
        var millisecondValue = obj.Get("millisecond");
        var millisecond = millisecondValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, millisecondValue);

        // 6. minute - read and convert immediately
        var minuteValue = obj.Get("minute");
        var minute = minuteValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, minuteValue);

        // 7. month - read and convert immediately
        var monthValue = obj.Get("month");
        int month = 0;
        if (!monthValue.IsUndefined())
        {
            month = TemporalHelpers.ToPositiveIntegerWithTruncation(_realm, monthValue);
        }

        // 8. monthCode - read and convert immediately, validate well-formedness
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

        // 9. nanosecond - read and convert immediately
        var nanosecondValue = obj.Get("nanosecond");
        var nanosecond = nanosecondValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, nanosecondValue);

        // 10. second - read and convert immediately
        var secondValue = obj.Get("second");
        var second = secondValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, secondValue);

        // 11. year - use eraYear if computed, otherwise read from property
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

        // 12. Read options.overflow AFTER all fields (but BEFORE algorithmic validation)
        var overflow = optionsValue.IsUndefined() ? "constrain" : TemporalHelpers.GetOverflowOption(_realm, optionsValue);

        // NOW validate monthCode suitability (ISO calendar checks) - after year type validation AND options reading
        if (monthCodeStr is not null && string.Equals(calendar, "iso8601", StringComparison.Ordinal))
        {
            // Check for leap month suffix (not allowed in ISO calendar)
            if (monthCodeStr.Length == 4 && monthCodeStr[3] == 'L')
            {
                Throw.RangeError(_realm, $"Leap months are not valid for ISO 8601 calendar: {monthCodeStr}");
            }

            // Check month is in range 01-12 (not 00, 13, etc.)
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

        // Validate year range only (month/day will be validated by RegulateIsoDate based on overflow)
        if (year < -271821 || year > 275760)
        {
            Throw.RangeError(_realm, "Date is outside valid range");
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

        return Construct(new IsoDateTime(date.Value, time.Value), calendar);
    }

    /// <summary>
    /// Parses a PlainDateTime string according to the Temporal spec.
    /// Returns null for invalid strings, but may also throw for specific violations.
    /// </summary>
    private static ParsedDateTimeResult ParseDateTimeString(string input)
    {
        // Empty strings are invalid
        if (string.IsNullOrEmpty(input))
        {
            return default;
        }

        // Try to parse as ISO date-time
        // Accept both 'T' (uppercase), 't' (lowercase), and space as time separators
        var tIndex = input.IndexOf('T');
        if (tIndex < 0)
            tIndex = input.IndexOf('t');
        if (tIndex < 0)
            tIndex = input.IndexOf(' ');

        string dateString;
        string timeString;

        if (tIndex < 0)
        {
            // No time part, try parsing as date only
            dateString = input;
            timeString = "";

            // Check for negative zero year
            if (TemporalHelpers.IsNegativeZeroYear(ExtractYearString(dateString)))
            {
                return new ParsedDateTimeResult(null, "Negative zero year is not allowed");
            }

            // Date-only strings (no time component) cannot have UTC designators or offsets
            // Per spec: "2022-09-15Z" or "2022-09-15+00:00" are invalid for PlainDateTime
            // Same validation as PlainDate - reject Z, +, or excess dashes indicating offsets
            var isExtendedYear = dateString.Length > 0 && (dateString[0] == '+' || dateString[0] == '-');
            var expectedDashes = isExtendedYear ? 3 : 2;
            var dashCount = 0;

            for (var i = 0; i < dateString.Length; i++)
            {
                var c = dateString[i];

                // Z is invalid for date-only strings
                if (c == 'Z' || c == 'z')
                {
                    return new ParsedDateTimeResult(null, "UTC designator not valid for date-only string");
                }

                // + at any position except 0 (extended year sign) is an offset sign
                if (c == '+' && i > 0)
                {
                    return new ParsedDateTimeResult(null, "UTC offset not valid for date-only string");
                }

                // Count dashes - more than expected means there's an offset
                if (c == '-')
                {
                    dashCount++;
                    if (dashCount > expectedDashes)
                    {
                        return new ParsedDateTimeResult(null, "UTC offset not valid for date-only string");
                    }
                }
            }

            var dateOnly = TemporalHelpers.ParseIsoDate(dateString);
            if (dateOnly is null)
            {
                return new ParsedDateTimeResult(null, "Invalid date string");
            }

            // Check date limits - validate full date-time with midnight time
            if (!TemporalHelpers.IsValidIsoDateTime(dateOnly.Value.Year, dateOnly.Value.Month, dateOnly.Value.Day, 0, 0, 0, 0, 0, 0))
            {
                return new ParsedDateTimeResult(null, "Date is outside valid range");
            }

            return new ParsedDateTimeResult(new IsoDateTime(dateOnly.Value, new IsoTime(0, 0, 0, 0, 0, 0)), null);
        }

        dateString = input.Substring(0, tIndex);
        var remainder = input.Substring(tIndex + 1);

        // Check for negative zero year
        if (TemporalHelpers.IsNegativeZeroYear(ExtractYearString(dateString)))
        {
            return new ParsedDateTimeResult(null, "Negative zero year is not allowed");
        }

        // Parse time and annotations
        var timeZoneCount = 0;
        var calendarCount = 0;
        var hasCriticalCalendar = false;
        var hasUtcDesignator = false;

        // Find where time ends and annotations begin
        var timeEnd = remainder.Length;

        // Check for Z or offset before brackets
        for (var i = 0; i < remainder.Length; i++)
        {
            var c = remainder[i];

            if (c == 'Z' || c == 'z')
            {
                hasUtcDesignator = true;
                timeEnd = System.Math.Min(timeEnd, i);
                break;
            }

            if ((c == '+' || c == '-') && i > 0)
            {
                // Check if this could be an offset (not just a negative sign in ISO date-time)
                // Offset should follow time: HH:MM[:SS[.sss...]][+-]HH:MM
                timeEnd = System.Math.Min(timeEnd, i);
                break;
            }

            if (c == '[')
            {
                timeEnd = i;
                break;
            }
        }

        // PlainDateTime cannot have UTC designator
        if (hasUtcDesignator)
        {
            return new ParsedDateTimeResult(null, "UTC designator Z is not allowed for PlainDateTime");
        }

        // PlainDateTime CAN have numeric UTC offset (it will be ignored)
        // Per spec: offsets in PlainDateTime strings are allowed but have no effect
        // Example: "1976-11-18T15:23:30.1+00:00" is valid, offset is simply ignored

        timeString = remainder.Substring(0, timeEnd);

        // Parse annotations in brackets
        var pos = timeEnd;
        // Validate and skip offset portion (+HH:MM or +HH:MM:SS or similar)
        if (pos < remainder.Length && (remainder[pos] == '+' || remainder[pos] == '-'))
        {
            var offsetStart = pos;
            pos++;
            // Consume valid offset characters (digits, colons, dots/commas for fractions)
            while (pos < remainder.Length && (char.IsDigit(remainder[pos]) || remainder[pos] == ':' || remainder[pos] == '.' || remainder[pos] == ','))
            {
                pos++;
            }

            // Validate the offset format
            var offsetStr = remainder.Substring(offsetStart + 1, pos - offsetStart - 1);
            if (!IsValidOffsetFormat(offsetStr))
            {
                return new ParsedDateTimeResult(null, "Invalid UTC offset format");
            }
        }

        while (pos < remainder.Length)
        {
            if (remainder[pos] == '[')
            {
                var endBracket = remainder.IndexOf(']', pos);
                if (endBracket < 0)
                {
                    return new ParsedDateTimeResult(null, "Unclosed bracket annotation");
                }

                var annotation = remainder.Substring(pos + 1, endBracket - pos - 1);

                // Check for critical flag (!) - strip it for checking annotation type
                var isCritical = annotation.Length > 0 && annotation[0] == '!';
                var annotationContent = isCritical ? annotation.Substring(1) : annotation;

                // Check for key-value annotations (contains '=')
                var equalsIdx = annotationContent.IndexOf('=');
                if (equalsIdx >= 0)
                {
                    // Key-value annotation - validate that key is lowercase only
                    var key = annotationContent.Substring(0, equalsIdx);
                    if (!TemporalHelpers.IsLowercaseAnnotationKey(key))
                    {
                        return new ParsedDateTimeResult(null, "Annotation keys must be lowercase");
                    }

                    // Check if it's a calendar annotation (u-ca=...)
                    if (StartsWithOrdinal(annotationContent, "u-ca="))
                    {
                        // Calendar annotation
                        calendarCount++;
                        if (isCritical)
                        {
                            hasCriticalCalendar = true;
                        }
                    }
                    else
                    {
                        // Unknown key annotation - accepted unless critical
                        if (isCritical)
                        {
                            return new ParsedDateTimeResult(null, "Critical unknown annotation");
                        }
                        // Non-critical unknown annotations are accepted (just ignored)
                    }
                }
                else
                {
                    // Time zone annotation (no '=' sign) - critical flag is ignored
                    timeZoneCount++;
                }

                pos = endBracket + 1;
            }
            else
            {
                // Trailing junk after annotations or offset
                return new ParsedDateTimeResult(null, "Trailing characters after annotations");
            }
        }

        // Check for multiple time zones
        if (timeZoneCount > 1)
        {
            return new ParsedDateTimeResult(null, "Multiple time zone annotations");
        }

        // Validate: Multiple calendar annotations with any critical flag is invalid
        if (calendarCount > 1 && hasCriticalCalendar)
        {
            return new ParsedDateTimeResult(null, "Multiple calendar annotations with critical flag");
        }

        var date = TemporalHelpers.ParseIsoDate(dateString);
        if (date is null)
        {
            return new ParsedDateTimeResult(null, "Invalid date string");
        }

        // A trailing T with no time digits is invalid (e.g., "2020-01-01T")
        if (string.IsNullOrEmpty(timeString))
        {
            return new ParsedDateTimeResult(null, "Invalid time string");
        }

        var time = TemporalHelpers.ParseIsoTime(timeString);
        if (time is null)
        {
            return new ParsedDateTimeResult(null, "Invalid time string");
        }

        var resultTime = time ?? new IsoTime(0, 0, 0, 0, 0, 0);

        // Check date-time limits with full time components
        if (!TemporalHelpers.IsValidIsoDateTime(date.Value.Year, date.Value.Month, date.Value.Day,
                resultTime.Hour, resultTime.Minute, resultTime.Second,
                resultTime.Millisecond, resultTime.Microsecond, resultTime.Nanosecond))
        {
            return new ParsedDateTimeResult(null, "Date-time is outside valid range");
        }

        return new ParsedDateTimeResult(new IsoDateTime(date.Value, resultTime), null);
    }

    private static bool StartsWithOrdinal(string str, string prefix)
    {
        if (str.Length < prefix.Length)
            return false;
        for (var i = 0; i < prefix.Length; i++)
        {
            if (str[i] != prefix[i])
                return false;
        }

        return true;
    }

    private static string ExtractYearString(string dateString)
    {
        // Extract the year portion from a date string
        // Handles both YYYY-MM-DD and [+-]YYYYYY-MM-DD formats
        var startIndex = (dateString.Length > 0 && (dateString[0] == '-' || dateString[0] == '+')) ? 1 : 0;
        var firstDash = dateString.IndexOf('-', startIndex);
        if (firstDash < 0)
            return dateString;
        return dateString.Substring(0, firstDash);
    }


    /// <summary>
    /// Validates offset format: HH, HHMM, HH:MM, HHMMSS, HH:MM:SS (with optional fractional seconds).
    /// Separator style must be consistent (all colons or none).
    /// </summary>
    private static bool IsValidOffsetFormat(string offset)
    {
        if (offset.Length < 2)
            return false;

        // Must start with 2 digits (hours)
        if (!char.IsDigit(offset[0]) || !char.IsDigit(offset[1]))
            return false;

        if (offset.Length == 2)
            return true; // Just HH

        var hasColon = offset[2] == ':';

        if (hasColon)
        {
            // Extended format: HH:MM or HH:MM:SS[.fff...]
            if (offset.Length < 5 || !char.IsDigit(offset[3]) || !char.IsDigit(offset[4]))
                return false;
            if (offset.Length == 5)
                return true; // HH:MM
            if (offset[5] != ':')
                return false; // Must have colon before seconds
            if (offset.Length < 8 || !char.IsDigit(offset[6]) || !char.IsDigit(offset[7]))
                return false;
            if (offset.Length == 8)
                return true; // HH:MM:SS
            // Fractional seconds
            return offset.Length > 8 && (offset[8] == '.' || offset[8] == ',');
        }
        else
        {
            // Basic format: HHMM or HHMMSS[.fff...]
            if (!char.IsDigit(offset[2]) || !char.IsDigit(offset[3]))
                return false;
            if (offset.Length == 4)
                return true; // HHMM
            if (!char.IsDigit(offset[4]) || !char.IsDigit(offset[5]))
                return false;
            if (offset.Length == 6)
                return true; // HHMMSS
            // Fractional seconds
            return offset.Length > 6 && (offset[6] == '.' || offset[6] == ',');
        }
    }

    private readonly record struct ParsedDateTimeResult(IsoDateTime? DateTime, string? Error);
}

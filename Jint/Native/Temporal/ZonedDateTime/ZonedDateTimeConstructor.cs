using System.Numerics;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime
/// </summary>
internal sealed class ZonedDateTimeConstructor : Constructor
{
    private static readonly JsString _functionName = new("ZonedDateTime");

    internal ZonedDateTimeConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new ZonedDateTimePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public ZonedDateTimePrototype PrototypeObject { get; }

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
    /// https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime.from
    /// </summary>
    private JsZonedDateTime From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        var options = arguments.At(1);
        return ToTemporalZonedDateTime(item, options);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalZonedDateTime(arguments.At(0), Undefined);
        var two = ToTemporalZonedDateTime(arguments.At(1), Undefined);

        var cmp = one.EpochNanoseconds.CompareTo(two.EpochNanoseconds);
        return JsNumber.Create(cmp < 0 ? -1 : cmp > 0 ? 1 : 0);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.ZonedDateTime cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.zoneddatetime
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var epochNanosecondsArg = arguments.At(0);
        var timeZoneArg = arguments.At(1);
        var calendarArg = arguments.At(2);

        // Get epochNanoseconds
        var epochNanoseconds = ToBigInt(epochNanosecondsArg);

        // Validate epoch nanoseconds range
        if (!InstantConstructor.IsValidEpochNanoseconds(epochNanoseconds))
        {
            Throw.RangeError(_realm, "epochNanoseconds is outside the valid range");
        }

        // Get time zone - per spec step 4, only IANA names are valid from ISO string bracket annotations
        if (!timeZoneArg.IsString())
        {
            Throw.TypeError(_realm, "Time zone must be a string");
        }

        var timeZone = ParseConstructorTimeZone(timeZoneArg.ToString());

        // Get calendar - per spec step 7, use CanonicalizeCalendar directly (not ToTemporalCalendarIdentifier)
        string calendar;
        if (calendarArg.IsUndefined())
        {
            calendar = "iso8601";
        }
        else if (calendarArg.IsString())
        {
            var canonical = TemporalHelpers.CanonicalizeCalendar(calendarArg.ToString());
            if (canonical is null)
            {
                Throw.RangeError(_realm, $"Invalid calendar: {calendarArg}");
            }

            calendar = canonical;
        }
        else
        {
            Throw.TypeError(_realm, "calendar must be a string");
            calendar = null!;
        }

        return Construct(epochNanoseconds, timeZone, calendar, newTarget);
    }

    internal JsZonedDateTime Construct(BigInteger epochNanoseconds, string timeZone, string calendar = "iso8601", JsValue? newTarget = null)
    {
        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalZonedDateTime.PrototypeObject);

        return new JsZonedDateTime(_engine, proto, epochNanoseconds, timeZone, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalzoneddatetime
    /// </summary>
    internal JsZonedDateTime ToTemporalZonedDateTime(JsValue item, JsValue options)
    {
        if (item is JsZonedDateTime zonedDateTime)
        {
            // Validate options even though we don't use them for existing ZonedDateTime
            // Read in alphabetical order per spec: disambiguation, offset, overflow
            if (!options.IsUndefined())
            {
                GetDisambiguationOption(options);
                GetOffsetOption(options);
                TemporalHelpers.GetOverflowOption(_realm, options);
            }

            // Return a COPY, not the original instance (per spec: from() always creates a new object)
            return Construct(zonedDateTime.EpochNanoseconds, zonedDateTime.TimeZone, zonedDateTime.Calendar);
        }

        if (item.IsString())
        {
            var str = item.ToString();

            // Parse the string first (to throw RangeError if invalid BEFORE checking options type)
            // Only validate string format, do NOT compute epoch nanoseconds yet
            ValidateZonedDateTimeString(str);

            // Now read options (alphabetical order: disambiguation, offset, overflow)
            // This will throw TypeError if options is not an object
            var disambiguation = GetDisambiguationOption(options);
            var offsetOption = GetOffsetOption(options);
            var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

            // Parse and interpret the string with the specified options
            var result = ParseZonedDateTimeString(str, overflow, disambiguation, offsetOption);
            return result;
        }

        if (item.IsObject())
        {
            var obj = item.AsObject();
            return ToZonedDateTimeFromFieldsWithOptions(obj, options);
        }

        Throw.TypeError(_realm, "Invalid ZonedDateTime");
        return null!;
    }

    private JsZonedDateTime ToZonedDateTimeFromFieldsWithOptions(ObjectInstance obj, JsValue options)
    {
        // Read and convert properties in strict alphabetical order per spec:
        // calendar, day, hour, microsecond, millisecond, minute, month, monthCode, nanosecond, offset, second, timeZone, year
        // Each property must be fully read and converted before moving to the next

        // 1. calendar
        var calendarProp = obj.Get("calendar");
        string calendar = "iso8601";
        if (!calendarProp.IsUndefined())
        {
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion (handles Temporal objects)
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

            // If both month and monthCode are provided, they must match
            if (month != 0 && month != monthFromCode)
            {
                Throw.RangeError(_realm, "month and monthCode do not match");
            }

            month = monthFromCode;
        }

        // 9. nanosecond - read and convert immediately
        var nanosecondValue = obj.Get("nanosecond");
        var nanosecond = nanosecondValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, nanosecondValue);

        // 10. offset - read and convert immediately
        var offsetProp = obj.Get("offset");
        long? offsetNs = null;
        if (!offsetProp.IsUndefined())
        {
            var offsetStr = TemporalHelpers.ToOffsetString(_realm, offsetProp);
            offsetNs = TemporalHelpers.ParseOffsetString(offsetStr);
            if (offsetNs is null)
            {
                Throw.RangeError(_realm, "Invalid offset string");
            }
        }

        // 11. second - read and convert immediately
        var secondValue = obj.Get("second");
        var second = secondValue.IsUndefined() ? 0 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, secondValue);

        // 12. timeZone - read and convert immediately (required)
        var timeZoneProp = obj.Get("timeZone");
        if (timeZoneProp.IsUndefined())
        {
            Throw.TypeError(_realm, "Missing required property: timeZone");
        }

        var timeZone = ToTemporalTimeZoneIdentifier(timeZoneProp);

        // 13. year - use eraYear if computed, otherwise read from property
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

        // 14-16. Read options AFTER all fields(but BEFORE algorithmic validation)
        // Alphabetical order: disambiguation, offset, overflow
        var disambiguation = GetDisambiguationOption(options);
        var offsetOption = GetOffsetOption(options);
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

        // Regulate date (with calendar conversion for non-ISO calendars)
        var date = TemporalHelpers.CalendarDateToISO(_realm, calendar, year, month, day, overflow);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid date");
        }

        // Regulate time
        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, overflow);
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        var isoDateTime = new IsoDateTime(date.Value, time.Value);

        // Convert local date-time to instant
        // For object input, hasUtcDesignator is false (only strings can have Z)
        var epochNs = GetEpochFromIsoDateTime(isoDateTime, timeZone, disambiguation, offsetNs, false, offsetOption);

        return Construct(epochNs, timeZone, calendar);
    }

    /// <summary>
    /// Validates that a string is a syntactically valid ZonedDateTime string
    /// without computing epoch nanoseconds. Used to throw RangeError before reading options.
    /// </summary>
    private void ValidateZonedDateTimeString(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Throw.RangeError(_realm, "Empty string is not a valid ZonedDateTime");
        }

        if (TemporalHelpers.HasNegativeZeroYear(input))
        {
            Throw.RangeError(_realm, "Negative zero year is not allowed");
        }

        var result = ParseZonedDateTimeStringInternal(input);

        if (result.Error is not null)
        {
            Throw.RangeError(_realm, result.Error);
        }

        if (result.DateTime is null || result.TimeZone is null)
        {
            Throw.RangeError(_realm, "Invalid ZonedDateTime string");
        }
    }

    private JsZonedDateTime ParseZonedDateTimeString(string input, string overflow, string disambiguation, string offsetOption)
    {
        // Empty strings are invalid
        if (string.IsNullOrEmpty(input))
        {
            Throw.RangeError(_realm, "Empty string is not a valid ZonedDateTime");
        }

        // Check for negative zero year
        if (TemporalHelpers.HasNegativeZeroYear(input))
        {
            Throw.RangeError(_realm, "Negative zero year is not allowed");
        }

        // Parse the string
        var result = ParseZonedDateTimeStringInternal(input);

        if (result.Error is not null)
        {
            Throw.RangeError(_realm, result.Error);
        }

        if (result.DateTime is null || result.TimeZone is null)
        {
            Throw.RangeError(_realm, "Invalid ZonedDateTime string");
        }

        var timeZone = ToTemporalTimeZoneIdentifier(new JsString(result.TimeZone));

        // Per spec ToTemporalZonedDateTime steps 15-21:
        // Determine offsetBehaviour
        string offsetBehaviour;
        if (result.HasUtcDesignator)
        {
            offsetBehaviour = "exact";
        }
        else if (result.OffsetNanoseconds.HasValue)
        {
            offsetBehaviour = "option";
        }
        else
        {
            offsetBehaviour = "wall";
        }

        // Per spec step 3/12: For strings, matchBehaviour = ~match-minutes~
        // Unless offset has sub-minute precision (seconds), then ~match-exactly~
        var matchBehaviour = result.OffsetHasSubMinutePrecision ? "match-exactly" : "match-minutes";

        var offsetNs = result.OffsetNanoseconds ?? 0;

        var provider = _engine.Options.Temporal.TimeZoneProvider;
        var isoDateTime = result.DateTime.Value;

        BigInteger epochNs;
        if (result.TimeIsStartOfDay)
        {
            // Per spec InterpretISODateTimeOffset step 1: time is ~start-of-day~
            epochNs = TemporalHelpers.GetStartOfDay(_realm, provider, timeZone, isoDateTime.Date);
        }
        else
        {
            // Convert to epoch nanoseconds using the spec's InterpretISODateTimeOffset
            epochNs = TemporalHelpers.InterpretISODateTimeOffset(
                _realm,
                provider,
                isoDateTime.Date,
                isoDateTime.Time,
                offsetBehaviour,
                offsetNs,
                timeZone,
                disambiguation,
                offsetOption,
                matchBehaviour);
        }

        var calendarId = result.Calendar ?? "iso8601";
        var canonicalCalendar = TemporalHelpers.CanonicalizeCalendar(calendarId) ?? calendarId;
        return Construct(epochNs, timeZone, canonicalCalendar);
    }

    private BigInteger GetEpochFromIsoDateTime(IsoDateTime dateTime, string timeZone, string disambiguation, long? offsetNs, bool hasUtcDesignator, string offsetOption)
    {
        var provider = _engine.Options.Temporal.TimeZoneProvider;

        // Calculate local date-time as nanoseconds
        var localNs = IsoDateTimeToNanoseconds(dateTime);

        // Determine offsetBehaviour per spec
        // If hasUtcDesignator (Z present), offsetBehaviour = "exact"
        // Else if offsetNs has value, offsetBehaviour = "option"
        // Else offsetBehaviour = "wall"

        // If offsetBehaviour is "exact" (has Z), use the offset directly (offset is 0 for Z)
        if (hasUtcDesignator)
        {
            // Z means UTC, so offset is 0
            var epochNs = localNs - (offsetNs ?? 0);

            // Validate the result instant is within valid range
            if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
            {
                Throw.RangeError(_realm, "Resulting instant is outside the valid range");
            }

            return epochNs;
        }

        // If we have an offset, use it based on offsetOption (offsetBehaviour = "option")
        if (offsetNs.HasValue)
        {
            if (string.Equals(offsetOption, "use", StringComparison.Ordinal))
            {
                // Use the offset directly
                // https://tc39.es/proposal-temporal/#sec-temporal-interpretisodatetimeoffset step 18-19
                var epochNs = localNs - offsetNs.Value;

                // Validate the result instant is within valid range
                if (!InstantConstructor.IsValidEpochNanoseconds(epochNs))
                {
                    Throw.RangeError(_realm, "Resulting instant is outside the valid range");
                }

                return epochNs;
            }

            if (string.Equals(offsetOption, "ignore", StringComparison.Ordinal))
            {
                // Ignore the offset, use disambiguation
                return GetInstantFor(provider, timeZone, dateTime, disambiguation);
            }

            if (string.Equals(offsetOption, "prefer", StringComparison.Ordinal))
            {
                // For prefer/reject, must check the wall-clock datetime is within valid range first
                // https://tc39.es/proposal-temporal/#sec-temporal-interpretisodatetimeoffset step 26
                TemporalHelpers.CheckISODaysRange(_realm, dateTime.Date);

                // Try to use the offset if it matches a possible instant
                var possibleInstants = provider.GetPossibleInstantsFor(timeZone,
                    dateTime.Year, dateTime.Month, dateTime.Day,
                    dateTime.Hour, dateTime.Minute, dateTime.Second,
                    dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond);

                // Validate each possible instant is within valid range
                foreach (var instant in possibleInstants)
                {
                    if (!InstantConstructor.IsValidEpochNanoseconds(instant))
                    {
                        Throw.RangeError(_realm, "Instant is outside the valid range");
                    }
                }

                var preferredEpochNs = localNs - offsetNs.Value;
                foreach (var instant in possibleInstants)
                {
                    if (instant == preferredEpochNs)
                    {
                        return instant;
                    }
                }

                // Offset doesn't match any possible instant, fall back to disambiguation
                return GetInstantFor(provider, timeZone, dateTime, disambiguation);
            }

            if (string.Equals(offsetOption, "reject", StringComparison.Ordinal))
            {
                // For prefer/reject, must check the wall-clock datetime is within valid range first
                // https://tc39.es/proposal-temporal/#sec-temporal-interpretisodatetimeoffset step 26
                TemporalHelpers.CheckISODaysRange(_realm, dateTime.Date);

                // The offset must match exactly
                var possibleInstants = provider.GetPossibleInstantsFor(timeZone,
                    dateTime.Year, dateTime.Month, dateTime.Day,
                    dateTime.Hour, dateTime.Minute, dateTime.Second,
                    dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond);

                // Validate each possible instant is within valid range
                foreach (var instant in possibleInstants)
                {
                    if (!InstantConstructor.IsValidEpochNanoseconds(instant))
                    {
                        Throw.RangeError(_realm, "Instant is outside the valid range");
                    }
                }

                var preferredEpochNs = localNs - offsetNs.Value;
                foreach (var instant in possibleInstants)
                {
                    if (instant == preferredEpochNs)
                    {
                        return instant;
                    }
                }

                Throw.RangeError(_realm, "Offset does not match any possible instant for the given date-time in the time zone");
            }
        }

        // No offset provided, use disambiguation
        return GetInstantFor(provider, timeZone, dateTime, disambiguation);
    }

    private BigInteger GetInstantFor(ITimeZoneProvider provider, string timeZone, IsoDateTime dateTime, string disambiguation)
    {
        return TemporalHelpers.GetEpochNanosecondsFor(_realm, provider, timeZone, dateTime, disambiguation);
    }

    private static IsoDateTime AddHoursToIsoDateTime(IsoDateTime dateTime, int hours)
    {
        var totalHours = dateTime.Hour + hours;
        var newHour = totalHours % 24;
        var dayOverflow = totalHours / 24;

        var newDate = dateTime.Date;
        if (dayOverflow > 0)
        {
            var days = TemporalHelpers.IsoDateToDays(dateTime.Year, dateTime.Month, dateTime.Day) + dayOverflow;
            newDate = TemporalHelpers.DaysToIsoDate(days);
        }

        return new IsoDateTime(newDate, new IsoTime(newHour, dateTime.Minute, dateTime.Second,
            dateTime.Millisecond, dateTime.Microsecond, dateTime.Nanosecond));
    }

    private static BigInteger IsoDateTimeToNanoseconds(IsoDateTime dateTime)
    {
        var days = TemporalHelpers.IsoDateToDays(dateTime.Year, dateTime.Month, dateTime.Day);
        BigInteger ns = days;
        ns *= TemporalHelpers.NanosecondsPerDay;
        ns += dateTime.Time.TotalNanoseconds();
        return ns;
    }

    private static ParsedZonedDateTimeResult ParseZonedDateTimeStringInternal(string input)
    {
        // Parse ISO 8601 date-time with time zone
        // Format: YYYY-MM-DDTHH:MM:SS[.sss][±HH:MM][TimeZone][u-ca=calendar]

        // Find the first time separator (T, t, or space)
        // Need to check all three and use the earliest one
        // Only search before the first '[' to avoid matching 'T' inside timezone annotations like [UTC]
        var searchLimit = input.IndexOf('[');
        if (searchLimit < 0) searchLimit = input.Length;
        var searchPart = input.Substring(0, searchLimit);

        var tIndex = searchPart.IndexOf('T');
        var tIndexLower = searchPart.IndexOf('t');
        var spaceIndex = searchPart.IndexOf(' ');

        // Find the minimum valid index
        if (tIndexLower >= 0 && (tIndex < 0 || tIndexLower < tIndex))
            tIndex = tIndexLower;
        if (spaceIndex >= 0 && (tIndex < 0 || spaceIndex < tIndex))
            tIndex = spaceIndex;

        if (tIndex < 0)
        {
            // No time component - treat as date-only with implicit 00:00:00 time
            // This is valid when there's a timezone annotation (makes it a ZonedDateTime at start of day)
            var datePart = input;
            var bracketIndex = input.IndexOf('[');
            if (bracketIndex >= 0)
            {
                datePart = input.Substring(0, bracketIndex);
            }

            // Parse just the date part
            var dateResult = TemporalHelpers.ParseIsoDate(datePart);
            if (dateResult is null)
            {
                return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Invalid date");
            }

            // Default time to 00:00:00
            var defaultTime = new IsoTime(0, 0, 0, 0, 0, 0);
            var parsedDateTime = new IsoDateTime(dateResult.Value, defaultTime);

            // Extract timezone and calendar from annotations
            string? parsedTimeZone = null;
            string? parsedCalendar = null;
            int lastBracketEnd = -1;

            bracketIndex = input.IndexOf('[');
            while (bracketIndex >= 0 && bracketIndex < input.Length)
            {
                var bracketEnd = input.IndexOf(']', bracketIndex);
                if (bracketEnd < 0) break;

                lastBracketEnd = bracketEnd;

                var annotation = input.Substring(bracketIndex + 1, bracketEnd - bracketIndex - 1);
                // Strip critical flag if present
                if (annotation.Length > 0 && annotation[0] == '!')
                {
                    annotation = annotation.Substring(1);
                }

                if (annotation.StartsWith("u-ca=", StringComparison.Ordinal))
                {
                    parsedCalendar = annotation.Substring(5);
                }
                else if (!annotation.Contains('='))
                {
                    // No '=' means it's a timezone identifier
                    parsedTimeZone = annotation;
                }

                bracketIndex = input.IndexOf('[', bracketEnd + 1);
            }

            // Validate: No junk after the last bracket annotation
            if (lastBracketEnd >= 0 && lastBracketEnd + 1 < input.Length)
            {
                return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Unexpected characters after valid content");
            }

            if (parsedTimeZone is null)
            {
                return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "ZonedDateTime string must include timezone annotation");
            }

            return new ParsedZonedDateTimeResult(parsedDateTime, parsedTimeZone, parsedCalendar, null, false, false, true, null);
        }

        var dateString = input.Substring(0, tIndex);
        var remainder = input.Substring(tIndex + 1);

        var date = TemporalHelpers.ParseIsoDate(dateString);
        if (date is null)
        {
            return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Invalid date");
        }

        // Parse time and extract offset/time zone/calendar
        var timeEnd = remainder.Length;
        var offsetStart = -1;
        var hasUtcDesignator = false;
        string? timeZone = null;
        string? calendar = null;
        long? offsetNs = null;
        var offsetHasSubMinutePrecision = false;

        // Find where time ends
        for (var i = 0; i < remainder.Length; i++)
        {
            var c = remainder[i];
            if (c == 'Z' || c == 'z')
            {
                hasUtcDesignator = true;
                timeEnd = i;
                break;
            }

            if ((c == '+' || c == '-') && i > 0)
            {
                offsetStart = i;
                timeEnd = i;
                break;
            }

            if (c == '[')
            {
                timeEnd = i;
                break;
            }
        }

        var timeString = remainder.Substring(0, timeEnd);

        // Handle leap second
        timeString = timeString.Replace(":60", ":59");

        var time = TemporalHelpers.ParseIsoTime(timeString);
        if (time is null)
        {
            return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Invalid time");
        }

        // Parse offset if present
        // Note: For ZonedDateTime, Z only sets the offset - the time zone must come from a bracketed annotation
        if (hasUtcDesignator)
        {
            offsetNs = 0;
            // Don't set timeZone here - ZonedDateTime requires a bracketed time zone annotation
        }
        else if (offsetStart >= 0)
        {
            var offsetEnd = remainder.Length;
            for (var i = offsetStart + 1; i < remainder.Length; i++)
            {
                if (remainder[i] == '[')
                {
                    offsetEnd = i;
                    break;
                }
            }

            var offsetStr = remainder.Substring(offsetStart, offsetEnd - offsetStart);
            offsetNs = TemporalHelpers.ParseOffsetString(offsetStr);
            if (offsetNs is null)
            {
                return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Invalid offset");
            }

            // Per spec: detect sub-minute precision in offset string
            // UTCOffset[+SubMinutePrecision] with more than one MinuteSecond means it has seconds
            // Format: ±HH:MM (6 chars) = minute precision, ±HH:MM:SS or longer = sub-minute precision
            // Also handle ±HHMM (5 chars) vs ±HHMMSS (7 chars)
            offsetHasSubMinutePrecision = HasOffsetSubMinutePrecision(offsetStr);
        }

        // Parse bracket annotations
        var pos = hasUtcDesignator ? timeEnd + 1 : (offsetStart >= 0 ? remainder.IndexOf('[', offsetStart) : remainder.IndexOf('['));
        if (pos < 0) pos = remainder.Length;

        var timeZoneCount = 0;
        var calendarCount = 0;
        var hasCriticalCalendar = false;

        while (pos < remainder.Length && remainder[pos] == '[')
        {
            var endBracket = remainder.IndexOf(']', pos);
            if (endBracket < 0)
            {
                return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Unclosed bracket");
            }

            var annotation = remainder.Substring(pos + 1, endBracket - pos - 1);
            var isCritical = annotation.Length > 0 && annotation[0] == '!';
            var content = isCritical ? annotation.Substring(1) : annotation;

            // Check if it's a key-value annotation
            var equalsIndex = content.IndexOf('=');
            if (equalsIndex >= 0)
            {
                // Validate that annotation key is lowercase only
                var key = content.Substring(0, equalsIndex);
                if (!TemporalHelpers.IsLowercaseAnnotationKey(key))
                {
                    return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Annotation keys must be lowercase");
                }

                if (content.StartsWith("u-ca=", StringComparison.Ordinal))
                {
                    calendarCount++;
                    if (isCritical)
                    {
                        hasCriticalCalendar = true;
                    }

                    // Use the first calendar annotation, ignore subsequent ones
                    if (calendar is null)
                    {
                        calendar = content.Substring(5);
                    }
                }
                else if (isCritical)
                {
                    // Unknown key annotation with critical flag
                    return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Critical unknown annotation");
                }
                // Non-critical unknown annotations are accepted but ignored
            }
            else
            {
                // Time zone annotation
                timeZoneCount++;
                if (timeZoneCount > 1)
                {
                    return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Multiple time zone annotations");
                }

                timeZone = content;
            }

            pos = endBracket + 1;
        }

        // Validate: Multiple calendar annotations with any critical flag is invalid
        if (calendarCount > 1 && hasCriticalCalendar)
        {
            return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Multiple calendar annotations with critical flag");
        }

        // Validate: No junk after the last bracket annotation
        if (pos < remainder.Length)
        {
            return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "Unexpected characters after valid content");
        }

        // ZonedDateTime requires a time zone
        if (timeZone is null)
        {
            return new ParsedZonedDateTimeResult(null, null, null, null, false, false, false, "ZonedDateTime string must include a time zone");
        }

        var dateTime = new IsoDateTime(date.Value, time.Value);

        // Note: Do NOT validate date limits here during parsing
        // The validation happens later in GetEpochFromIsoDateTime based on offsetBehaviour/offsetOption
        // For offset="use", only the resulting instant needs to be valid
        // For offset="prefer"/"reject", the wall-clock datetime needs to be valid (checked via CheckISODaysRange)

        return new ParsedZonedDateTimeResult(dateTime, timeZone, calendar, offsetNs, hasUtcDesignator, offsetHasSubMinutePrecision, false, null);
    }

    /// <summary>
    /// Constructor-specific timezone parsing per spec step 4.
    /// Unlike ToTemporalTimeZoneIdentifier, this rejects ISO datetime strings where the
    /// bracket annotation contains only a UTC offset (not an IANA name).
    /// </summary>
    private string ParseConstructorTimeZone(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Throw.RangeError(_realm, "Invalid time zone string");
            return null!;
        }

        // Step 4a: ParseTemporalTimeZoneString
        // Step 1 of ParseTemporalTimeZoneString: try parsing as a plain TimeZoneIdentifier
        // If the whole string is just a timezone identifier (IANA name or UTC offset), accept it
        var bracketStart = input.IndexOf('[');
        var hasDateTimeSeparator = false;
        if (input.Length > 4 && char.IsDigit(input[0]))
        {
            for (var i = 4; i < input.Length; i++)
            {
                if (input[i] == 'T' || input[i] == 't')
                {
                    hasDateTimeSeparator = true;
                    break;
                }
            }
        }

        if (!hasDateTimeSeparator && bracketStart < 0)
        {
            // Plain identifier (not an ISO string) — use normal path
            return TemporalHelpers.ParseTemporalTimeZoneString(_engine, _realm, input);
        }

        // It's an ISO datetime string — extract bracket annotation
        // Per spec: if the bracket annotation is a UTC offset, [[TimeZoneIANAName]] is empty → reject
        if (bracketStart >= 0)
        {
            var bracketEnd = input.IndexOf(']', bracketStart);
            if (bracketEnd > bracketStart)
            {
                var annotation = input.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                // Skip calendar annotations
                if (!annotation.StartsWith("u-ca=", StringComparison.Ordinal))
                {
                    // Strip critical flag
                    if (annotation.Length > 0 && annotation[0] == '!')
                    {
                        annotation = annotation.Substring(1);
                    }

                    // Per spec step 4c: if TimeZoneIANAName is empty, throw RangeError
                    // UTC offsets start with +/-/−, IANA names don't
                    if (TemporalHelpers.IsOffsetString(annotation))
                    {
                        Throw.RangeError(_realm, "UTC offset time zones from ISO strings are not valid for the ZonedDateTime constructor");
                        return null!;
                    }

                    // It's an IANA name — validate and return
                    return TemporalHelpers.ParseTemporalTimeZoneString(_engine, _realm, input);
                }
            }
        }

        // No valid timezone annotation found
        Throw.RangeError(_realm, $"Invalid time zone: {input}");
        return null!;
    }

    private string ToTemporalTimeZoneIdentifier(JsValue timeZoneLike)
    {
        if (timeZoneLike.IsUndefined())
        {
            Throw.TypeError(_realm, "Time zone is required");
        }

        return TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, timeZoneLike);
    }

    private BigInteger ToBigInt(JsValue value)
    {
        if (value is JsBigInt bigInt)
        {
            return bigInt._value;
        }

        // undefined is not allowed - throw TypeError per spec
        if (value.IsUndefined())
        {
            Throw.TypeError(_realm, "epochNanoseconds is required");
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(_realm, "epochNanoseconds must be a finite number");
        }

        if (number != System.Math.Truncate(number))
        {
            Throw.RangeError(_realm, "epochNanoseconds must be an integer");
        }

        return new BigInteger(number);
    }


    private string GetDisambiguationOption(JsValue options)
    {
        if (options.IsUndefined() || options.IsNull())
            return "compatible";

        if (!options.IsObject())
        {
            Throw.TypeError(_realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var value = obj.Get("disambiguation");
        if (value.IsUndefined())
            return "compatible";

        var str = TypeConverter.ToString(value);
        if (!string.Equals(str, "compatible", StringComparison.Ordinal) &&
            !string.Equals(str, "earlier", StringComparison.Ordinal) &&
            !string.Equals(str, "later", StringComparison.Ordinal) &&
            !string.Equals(str, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "Invalid disambiguation option");
        }

        return str;
    }

    private string GetOffsetOption(JsValue options)
    {
        if (options.IsUndefined() || options.IsNull())
            return "reject";

        if (!options.IsObject())
        {
            Throw.TypeError(_realm, "Options must be an object");
        }

        var obj = options.AsObject();
        var value = obj.Get("offset");
        if (value.IsUndefined())
            return "reject";

        var str = TypeConverter.ToString(value);
        if (!string.Equals(str, "use", StringComparison.Ordinal) &&
            !string.Equals(str, "prefer", StringComparison.Ordinal) &&
            !string.Equals(str, "ignore", StringComparison.Ordinal) &&
            !string.Equals(str, "reject", StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "Invalid offset option");
        }

        return str;
    }

    /// <summary>
    /// Per spec: determines if an offset string has sub-minute precision (contains seconds).
    /// Used to set matchBehaviour to ~match-exactly~ vs ~match-minutes~.
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalzoneddatetime step 23-24
    /// </summary>
    private static bool HasOffsetSubMinutePrecision(string offsetStr)
    {
        // ±HH (3 chars) or ±HH:MM (6 chars) or ±HHMM (5 chars) = minute precision
        // ±HH:MM:SS (9+ chars) or ±HHMMSS (7 chars) = sub-minute precision
        if (offsetStr.Length <= 3)
            return false; // ±HH

        if (offsetStr[3] == ':')
        {
            // Colon format: ±HH:MM = 6 chars, ±HH:MM:SS = 9+ chars
            return offsetStr.Length > 6;
        }

        // No-colon format: ±HHMM = 5 chars, ±HHMMSS = 7 chars
        return offsetStr.Length > 5;
    }

    private readonly record struct ParsedZonedDateTimeResult(IsoDateTime? DateTime, string? TimeZone, string? Calendar, long? OffsetNanoseconds, bool HasUtcDesignator, bool OffsetHasSubMinutePrecision, bool TimeIsStartOfDay, string? Error);
}

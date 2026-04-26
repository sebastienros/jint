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
            var parsed = ParseMonthDayString(str, out var parsedCalendar);
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid month-day string");
            }

            return Construct(parsed.Value, parsedCalendar);
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

        TemporalHelpers.RejectTemporalUnsupportedCalendar(_realm, calendar);

        // 2. day - read and convert immediately
        var dayValue = obj.Get("day");
        if (dayValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Missing day");
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

        // 5. year - use eraYear if computed, otherwise read from property. PMD allows year to
        // be omitted (uses 1972 as the reference year), so requireYear is false.
        var year = TemporalHelpers.ResolveYearFromEraOrYear(
            _realm, obj, eraYear, requireYear: false, out var yearExplicitlyProvided, defaultYear: 1972);

        // 6. Read options.overflow AFTER all fields (but BEFORE algorithmic validation)
        var overflow = optionsValue.IsUndefined() ? "constrain" : TemporalHelpers.GetOverflowOption(_realm, optionsValue);

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

        // Required-field checks (TypeError) MUST come before mismatch checks (RangeError).

        // For non-ISO calendars, monthCode is required unless year is explicitly provided
        // (month alone is ambiguous in calendars with leap months)
        if (!string.Equals(calendar, "iso8601", StringComparison.Ordinal) && monthCodeStr is null && !yearExplicitlyProvided)
        {
            Throw.TypeError(_realm, "monthCode is required for non-ISO calendars when year is not provided");
        }

        // For non-ISO calendars, when an explicit ordinal `month` is supplied, year is needed
        // because the ordinal-to-monthCode mapping is year-dependent (leap months shift it).
        if (NonIsoCalendars.IsNonIsoCalendar(calendar) && month != 0 && !yearExplicitlyProvided)
        {
            Throw.TypeError(_realm, "year is required when month is provided for a non-ISO calendar");
        }

        if (month == 0 && monthCodeStr is null)
        {
            Throw.TypeError(_realm, "Missing month/monthCode");
        }

        // Fundamental monthCode validity for non-ISO calendars: out-of-range display number,
        // or leap variant on a calendar without leap months → RangeError regardless of overflow.
        if (monthCodeStr is not null && NonIsoCalendars.IsNonIsoCalendar(calendar)
            && !NonIsoCalendars.TryValidateMonthCode(calendar, monthCodeStr, out var displayMonth))
        {
            var max = NonIsoCalendars.MaxDisplayMonth(calendar) ?? 12;
            Throw.RangeError(_realm, $"Invalid month: {displayMonth}; must be between 1-{max}");
        }

        // Range validation: month/monthCode mismatch — must come AFTER required-field checks.
        month = TemporalHelpers.ValidateMonthAndMonthCode(_realm, calendar, year, month, monthCodeStr, monthFromCode);

        // Bail out early when an explicit year is outside the Temporal-supported envelope
        // (year ∈ [-271821, +275760]). Without this, `PlainMonthDay.from({ year: ±999999, ... })`
        // would silently produce ISO dates that no other Temporal type would accept.
        // iso8601 PMD doesn't have an "explicit year" notion in the same way and is intentionally
        // allowed any year value (matches existing skipRangeCheck behaviour for that path).
        if (yearExplicitlyProvided
            && !string.Equals(calendar, "iso8601", StringComparison.Ordinal)
            && (year < -271821 || year > 275760))
        {
            Throw.RangeError(_realm, "year is outside the supported range for PlainMonthDay");
        }

        // For non-ISO calendars, convert calendar year/month/day to ISO.
        if (!TemporalHelpers.IsGregorianBasedCalendar(calendar))
        {
            // When year IS explicitly provided, the spec uses it for VALIDATION (date must exist
            // for that year, possibly with overflow), but the STORED reference year is still the
            // canonical 1972-anchored one (per CalendarMonthDayToISOReferenceDate). We therefore
            // first round-trip through the user's year to obtain the constrained day, then ask
            // FindCalendarReferenceYear for the canonical reference year using that constrained
            // day. Without this re-anchoring, PMD.from({year: 5781, monthCode: "M02", day: 30,
            // calendar: "hebrew"}) with constrain would store ISO 2020-11-16 instead of 1972-…
            int actualDay = day;
            string finalOverflow = overflow;
            string? effectiveMonthCode = monthCodeStr;
            if (yearExplicitlyProvided)
            {
                var validated = TemporalHelpers.CalendarDateToISO(_realm, calendar, year, month, day, overflow, monthCodeStr);
                if (validated is null)
                {
                    Throw.RangeError(_realm, "Invalid month-day");
                }

                // Extract the constrained day AND monthCode by reading back via the calendar so
                // the canonical refYear lookup uses the validated/constrained day. Also fills in
                // monthCode when the user passed numeric month without monthCode (FindCalendar-
                // ReferenceYear's non-iso path requires monthCode for the lookup).
                var calendarFields = NonIsoCalendars.IsoToCalendarDate(calendar, validated.Value);
                actualDay = calendarFields.Day;
                effectiveMonthCode ??= calendarFields.MonthCode;
                // Day already validated and constrained — the second conversion below should
                // never need to throw on its own.
                finalOverflow = "constrain";
            }

            // For Chinese/Dangi leap monthCodes that NEVER appear with the requested day in any
            // calendar year (e.g. M02L D30 — M02L max ≈29 days in every year of the supported
            // range), the spec falls the request back to the corresponding regular month with
            // the day preserved. Detect this BEFORE the canonical lookup by probing the leap
            // month's max day across the search window; if no year admits the requested day,
            // strip the L and proceed with the regular monthCode.
            if (!yearExplicitlyProvided
                && effectiveMonthCode is { Length: 4 } leapMc
                && leapMc[3] == 'L'
                && (calendar is "chinese" or "dangi")
                && !NonIsoCalendars.IsLeapMonthRepresentableForDay(calendar, leapMc, actualDay))
            {
                effectiveMonthCode = leapMc.Substring(0, 3);
                if (string.Equals(overflow, "reject", StringComparison.Ordinal))
                {
                    // Reject mode: per spec, if the user requested a leap monthCode and the
                    // requested day is not representable in any year for that leap monthCode,
                    // throw rather than silently fall back to the regular month.
                    Throw.RangeError(_realm, "Invalid month-day");
                }
            }

            var calendarYear = TemporalHelpers.FindCalendarReferenceYear(calendar, 1972, month, actualDay, effectiveMonthCode);

            var calDate = TemporalHelpers.CalendarDateToISO(_realm, calendar, calendarYear, month, actualDay, finalOverflow, effectiveMonthCode);
            if (calDate is null)
            {
                Throw.RangeError(_realm, "Invalid month-day");
            }

            // The converted ISO date becomes the reference date
            return Construct(calDate.Value, calendar);
        }

        // Use input year for validation, but always use 1972 as reference year in result
        // Per spec (calendar.html CalendarMonthDayToISOReferenceDate):
        // "The reference year is always 1972"
        // For iso8601 calendar, the year is only used for overflow, not range-checked
        var date = TemporalHelpers.RegulateIsoDate(year, month, day, overflow, skipRangeCheck: true);
        if (date is null)
        {
            Throw.RangeError(_realm, "Invalid month-day");
        }

        // Store with reference year 1972 (the type represents just month+day)
        return Construct(new IsoDate(1972, date.Value.Month, date.Value.Day), calendar);
    }

    private static IsoDate? ParseMonthDayString(string input, out string parsedCalendar)
    {
        parsedCalendar = "iso8601";

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
        // Canonicalize calendar annotation if present
        var hasNonIsoCalendar = false;
        if (calendar is not null)
        {
            var canonical = TemporalHelpers.CanonicalizeCalendar(calendar);
            if (canonical is null)
            {
                return null; // Invalid calendar
            }
            parsedCalendar = canonical;
            hasNonIsoCalendar = !string.Equals(canonical, "iso8601", StringComparison.Ordinal);
        }

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
                        if (hasNonIsoCalendar) return null;
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
                    if (hasNonIsoCalendar) return null;
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
                if (hasNonIsoCalendar) return null;
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
                            if (hasNonIsoCalendar) return null;
                            return date.Value;
                        }
                    }
                }
            }
        }

        // Try parsing as full date and extract month-day
        return TryParseFullDateAsMonthDay(coreString, hasNonIsoCalendar, hasNonIsoCalendar ? parsedCalendar : null);
    }

    private static IsoDate? TryParseFullDateAsMonthDay(string input, bool hasNonIsoCalendar, string? calendar = null)
    {
        var parsed = TemporalHelpers.ParseIsoDate(input);
        if (parsed is not null)
        {
            // For non-iso8601 calendars, the parsed year is used for calendar conversion
            // so it must be within valid ISO limits
            if (hasNonIsoCalendar && !TemporalHelpers.IsValidIsoDateTime(parsed.Value.Year, parsed.Value.Month, parsed.Value.Day))
            {
                return null;
            }

            if (hasNonIsoCalendar && calendar is not null)
            {
                // For non-ISO calendars, the parsed (calendarYear, monthCode, day) — derived
                // from the parsed ISO date — is what determines the PMD's identity. Re-anchor
                // by finding a canonical reference year (≈1972) where the same monthCode+day
                // round-trip back. Otherwise we'd lose the year info: e.g. ISO 2023-01-01 in
                // hebrew is M04 D08 (Tevet 8 of 5783), but ISO 1972-01-01 in hebrew is M04 D14
                // (Tevet 14 of 5732), so simply replacing the year flips the calendar day.
                var calFields = NonIsoCalendars.IsoToCalendarDate(calendar, parsed.Value);
                var canonicalYear = TemporalHelpers.FindCalendarReferenceYear(calendar, 1972, calFields.Month, calFields.Day, calFields.MonthCode);
                var anchored = TemporalHelpers.CalendarDateToISO(null!, calendar, canonicalYear, calFields.Month, calFields.Day, "constrain", calFields.MonthCode);
                if (anchored is not null)
                {
                    return anchored.Value;
                }
                // Fallback: keep the parsed ISO date as-is.
                return parsed.Value;
            }

            // For PlainMonthDay, we only extract month and day — year range validation is not needed for iso8601
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

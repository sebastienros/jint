using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth
/// </summary>
internal sealed class PlainYearMonthConstructor : Constructor
{
    private static readonly JsString _functionName = new("PlainYearMonth");
    private static readonly char[] DateTimeSeparators = { '-', 'T', ' ', '[' };

    internal PlainYearMonthConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PlainYearMonthPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PlainYearMonthPrototype PrototypeObject { get; }

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
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.from
    /// </summary>
    private JsPlainYearMonth From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        var optionsValue = arguments.At(1);

        // For PlainYearMonth, validate options first (for observable side effects) then convert
        if (item is JsPlainYearMonth)
        {
            // Read options first (per spec, options are accessed before conversion)
            var overflow = "constrain"; // Default, options not actually used for these types
            if (!optionsValue.IsUndefined())
            {
                overflow = TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            // Now perform the conversion (overflow is ignored for these types)
            return ToTemporalYearMonth(item, "constrain");
        }

        // For strings, parse first (fail fast if invalid), then read options
        if (item.IsString())
        {
            // Parse string first - this will throw if string is invalid
            var result = ToTemporalYearMonth(item, "constrain");
            // Only read options if parsing succeeded
            if (!optionsValue.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, optionsValue);
            }

            return result;
        }

        // For objects, pass options value and let ToTemporalYearMonthFromFields read it after fields
        if (item.IsObject())
        {
            return ToTemporalYearMonthFromObjectWithOptions(item.AsObject(), optionsValue);
        }

        Throw.TypeError(_realm, "Invalid year-month");
        return null!;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalYearMonth(arguments.At(0), "constrain");
        var two = ToTemporalYearMonth(arguments.At(1), "constrain");
        return JsNumber.Create(CompareIsoYearMonth(one.IsoDate, two.IsoDate));
    }

    private static int CompareIsoYearMonth(IsoDate one, IsoDate two)
    {
        if (one.Year != two.Year)
            return one.Year < two.Year ? -1 : 1;
        if (one.Month != two.Month)
            return one.Month < two.Month ? -1 : 1;
        if (one.Day != two.Day)
            return one.Day < two.Day ? -1 : 1;
        return 0;
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainYearMonth cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plainyearmonth
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var year = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(0));
        var month = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(1));
        var calendarArg = arguments.At(2);
        var referenceDay = arguments.At(3);

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

        var day = referenceDay.IsUndefined() ? 1 : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, referenceDay);

        // Validate the date is a valid calendar date (month 1-12, day 1-daysInMonth)
        var isoDate = new IsoDate(year, month, day);
        if (!isoDate.IsValid())
        {
            Throw.RangeError(_realm, "Invalid year-month");
        }

        // Validate year-month is within Temporal's representable range
        if (!TemporalHelpers.ISOYearMonthWithinLimits(year, month))
        {
            Throw.RangeError(_realm, "Year-month is outside the representable range");
        }

        return Construct(isoDate, calendar, newTarget);
    }

    internal JsPlainYearMonth Construct(IsoDate isoDate, string calendar = "iso8601", JsValue? newTarget = null)
    {
        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalPlainYearMonth.PrototypeObject);

        return new JsPlainYearMonth(_engine, proto, isoDate, calendar);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalyearmonth
    /// </summary>
    internal JsPlainYearMonth ToTemporalYearMonth(JsValue item, string overflow)
    {
        if (item is JsPlainYearMonth plainYearMonth)
        {
            // Return a copy, not the same object
            return Construct(plainYearMonth.IsoDate, plainYearMonth.Calendar);
        }

        if (item.IsString())
        {
            var str = item.ToString();
            var parsed = ParseYearMonthString(str, out var parsedCalendar);
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid year-month string");
            }

            if (!TemporalHelpers.ISOYearMonthWithinLimits(parsed.Value.Year, parsed.Value.Month))
            {
                Throw.RangeError(_realm, "Year-month is outside the representable range");
            }

            // Per spec: ISOYearMonthFromFields uses day=1 for the ISO calendar reference day.
            // Non-iso8601 calendars preserve the parsed day as the reference ISO day.
            var refDay = string.Equals(parsedCalendar, "iso8601", StringComparison.Ordinal) ? 1 : parsed.Value.Day;
            return Construct(new IsoDate(parsed.Value.Year, parsed.Value.Month, refDay), parsedCalendar);
        }

        if (item.IsObject())
        {
            var obj = item.AsObject();
            return ToTemporalYearMonthFromFields(obj, overflow);
        }

        Throw.TypeError(_realm, "Invalid year-month");
        return null!;
    }

    private JsPlainYearMonth ToTemporalYearMonthFromObjectWithOptions(ObjectInstance obj, JsValue options)
    {
        // Read and convert properties in alphabetical order per spec: calendar, month, monthCode, year
        // Each property must be fully read and converted before moving to the next

        // 1. calendar
        var calendarValue = obj.Get("calendar");
        string calendar;
        if (calendarValue.IsUndefined())
        {
            calendar = "iso8601";
        }
        else
        {
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarValue);
        }

        // 2. era/eraYear - read for era-supporting calendars (alphabetically between calendar and month)
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

            // If both month and monthCode are provided, they must match
            if (month != 0 && month != monthFromCode.Value)
            {
                Throw.RangeError(_realm, "month and monthCode must match");
            }

            month = monthFromCode.Value;
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

            if (monthFromCode!.Value < 1 || monthFromCode.Value > 12)
            {
                Throw.RangeError(_realm, $"Month {monthFromCode.Value} is not valid for ISO 8601 calendar");
            }
        }

        // At least one of month or monthCode is required
        if (month == 0)
        {
            Throw.TypeError(_realm, "month or monthCode is required");
        }

        // For non-ISO calendars, convert calendar year/month to ISO
        if (!TemporalHelpers.IsGregorianBasedCalendar(calendar))
        {
            var date = TemporalHelpers.CalendarDateToISO(_realm, calendar, year, month, 1, overflow);
            if (date is null)
            {
                Throw.RangeError(_realm, "Invalid year-month");
            }

            return Construct(date.Value, calendar);
        }

        // Validate month range (ISO calendars)
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

        return Construct(new IsoDate(year, month, 1), calendar);
    }

    private JsPlainYearMonth ToTemporalYearMonthFromFields(ObjectInstance obj, string overflow)
    {
        // Read and convert properties in alphabetical order per spec: calendar, month, monthCode, year
        // Each property must be fully read and converted before moving to the next

        // 1. calendar
        var calendarValue = obj.Get("calendar");
        string calendar;
        if (calendarValue.IsUndefined())
        {
            calendar = "iso8601";
        }
        else
        {
            // Use ToTemporalCalendarIdentifier for spec-compliant conversion
            calendar = TemporalHelpers.ToTemporalCalendarIdentifier(_realm, calendarValue);
        }

        // 2. era/eraYear - read for era-supporting calendars (alphabetically between calendar and month)
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

            // If both month and monthCode are provided, they must match
            if (month != 0 && month != monthFromCode.Value)
            {
                Throw.RangeError(_realm, "month and monthCode must match");
            }

            month = monthFromCode.Value;
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

        // Validate monthCode suitability - only for ISO/Gregorian calendars
        if (monthCodeStr is not null && TemporalHelpers.IsGregorianBasedCalendar(calendar))
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

        // At least one of month or monthCode is required
        if (month == 0)
        {
            Throw.TypeError(_realm, "month or monthCode is required");
        }

        // Note: overflow option is already read in From() method before calling this method, per spec

        // For non-ISO calendars, convert calendar year/month to ISO
        if (!TemporalHelpers.IsGregorianBasedCalendar(calendar))
        {
            var date = TemporalHelpers.CalendarDateToISO(_realm, calendar, year, month, 1, overflow);
            if (date is null)
            {
                Throw.RangeError(_realm, "Invalid year-month");
            }

            return Construct(date.Value, calendar);
        }

        // Validate month range (ISO calendars)
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

        return Construct(new IsoDate(year, month, 1), calendar);
    }

    private static IsoDate? ParseYearMonthString(string input, out string parsedCalendar)
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
        if (calendar is not null)
        {
            var canonical = TemporalHelpers.CanonicalizeCalendar(calendar);
            if (canonical is null)
            {
                return null; // Invalid calendar
            }
            parsedCalendar = canonical;
        }

        // Per spec: DateSpecYearMonth (YYYY-MM) with non-iso8601 calendar annotation is invalid.
        // Only full date formats (AnnotatedDateTime) support non-iso8601 calendars.
        var hasNonIsoCalendar = calendar is not null && !string.Equals(parsedCalendar, "iso8601", StringComparison.Ordinal);

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

        // PlainYearMonth cannot have UTC designator
        if (coreString.Contains('Z'))
        {
            return null;
        }

        // Check if there's a UTC offset or Z without a time component
        // UTC offsets are only valid when there's a time component (T, t, or space)
        var hasTimeSeparator = coreString.Contains('T') || coreString.Contains('t') || coreString.Contains(' ');
        if (!hasTimeSeparator)
        {
            // Check for Z or offset after year portion (position 4+)
            for (var i = 4; i < coreString.Length; i++)
            {
                var c = coreString[i];
                if (c == 'Z' || c == 'z' || c == '+')
                {
                    // Found Z or + offset without time component - invalid
                    return null;
                }
            }
        }

        // Try parsing as YYYY-MM format
        if (coreString.Length >= 7)
        {
            var dashIndex = coreString.IndexOf('-', 1); // Start at 1 to skip potential leading minus for negative years
            if (dashIndex >= 4)
            {
                var yearStr = coreString.Substring(0, dashIndex);
                var rest = coreString.Substring(dashIndex + 1);

                // Handle possible day or time suffix
                var endIndex = rest.IndexOfAny(DateTimeSeparators);
                var monthStr = endIndex >= 0 ? rest.Substring(0, endIndex) : rest;

                if (int.TryParse(yearStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var year) &&
                    int.TryParse(monthStr, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var month))
                {
                    // If there's a suffix, check what follows
                    if (endIndex >= 0 && endIndex < rest.Length)
                    {
                        var suffix = rest.Substring(endIndex);
                        // If it starts with '-', there's a day component - don't match here, let full date parsing handle it
                        if (suffix.Length > 0 && suffix[0] == '-')
                        {
                            // Fall through to full date parsing
                        }
                        // If it starts with 'T' or space (time separator), validate the time component
                        else if (suffix.Length > 0 && (suffix[0] == 'T' || suffix[0] == 't' || suffix[0] == ' '))
                        {
                            var timeAndOffset = suffix.Substring(1);
                            if (!TemporalHelpers.IsValidTimeWithOffset(timeAndOffset))
                            {
                                return null;
                            }

                            if (hasNonIsoCalendar) return null;
                            if (month >= 1 && month <= 12)
                            {
                                return new IsoDate(year, month, 1);
                            }
                        }
                        // Other suffix (like '[' for annotations) - also valid
                        else
                        {
                            if (hasNonIsoCalendar) return null;
                            if (month >= 1 && month <= 12)
                            {
                                return new IsoDate(year, month, 1);
                            }
                        }
                    }
                    else
                    {
                        // No suffix - just YYYY-MM
                        if (hasNonIsoCalendar) return null;
                        if (month >= 1 && month <= 12)
                        {
                            return new IsoDate(year, month, 1);
                        }
                    }
                }
            }
        }

        // Try parsing compact YYYYMM format (6 digits) or extended year +YYYYYYYMMDD (8+ digits)
        if (coreString.Length >= 6 && !coreString.Contains('-') && !hasTimeSeparator)
        {
            // Check if it's all digits (possibly with leading +/-)
            var startIdx = (coreString[0] == '+' || coreString[0] == '-') ? 1 : 0;
            var isAllDigits = true;
            for (var i = startIdx; i < coreString.Length; i++)
            {
                if (!char.IsDigit(coreString[i]))
                {
                    isAllDigits = false;
                    break;
                }
            }

            if (isAllDigits)
            {
                // For extended year: +YYYYYYMMDD or -YYYYYYMMDD (sign + 6 year digits + 2 month digits)
                // For normal year: YYYYMMDD (4 year digits + 2 month + 2 day) or YYYYMM (4 year + 2 month)
                if (startIdx > 0 && coreString.Length >= 8)
                {
                    // Extended year format: sign + at least 6 digits for year + 2 for month
                    var yearStr = coreString.Substring(0, coreString.Length - 2);
                    var monthStr = coreString.Substring(coreString.Length - 2, 2);

                    if (int.TryParse(yearStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) &&
                        int.TryParse(monthStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) &&
                        month >= 1 && month <= 12)
                    {
                        if (hasNonIsoCalendar) return null;
                        return new IsoDate(year, month, 1);
                    }
                }
                else if (startIdx == 0 && coreString.Length == 6)
                {
                    // YYYYMM format (4 year digits + 2 month digits)
                    var yearStr = coreString.Substring(0, 4);
                    var monthStr = coreString.Substring(4, 2);

                    if (int.TryParse(yearStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) &&
                        int.TryParse(monthStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) &&
                        month >= 1 && month <= 12)
                    {
                        if (hasNonIsoCalendar) return null;
                        return new IsoDate(year, month, 1);
                    }
                }
            }
        }

        // Try parsing as full date and extract year/month (preserve day as reference day per spec)
        var parsed = TemporalHelpers.ParseIsoDate(coreString);
        if (parsed is not null)
        {
            return parsed.Value;
        }

        // Try parsing date-time string
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
                var timeAndOffset = coreString.Substring(tIndex + 1);
                if (!TemporalHelpers.IsValidTimeWithOffset(timeAndOffset))
                {
                    return null;
                }

                return parsed.Value;
            }
        }

        return null;
    }
}

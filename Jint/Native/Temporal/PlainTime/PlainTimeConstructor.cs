using System.Globalization;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.plaintime
/// </summary>
internal sealed class PlainTimeConstructor : Constructor
{
    private static readonly JsString _functionName = new("PlainTime");

    internal PlainTimeConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PlainTimePrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public PlainTimePrototype PrototypeObject { get; }

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
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.from
    /// </summary>
    private JsPlainTime From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        var options = arguments.At(1);

        // For existing Temporal types (cloning), validate options first then convert
        if (item is JsPlainTime || item is JsPlainDateTime || item is JsZonedDateTime)
        {
            // Read options first (per spec, options are accessed before conversion)
            if (!options.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, options);
            }

            return ToTemporalTime(item, "constrain");
        }

        // For strings, parse first (fail fast if invalid), then read options
        if (item.IsString())
        {
            // Parse string first - this will throw if string is invalid
            var result = ToTemporalTime(item, "constrain");
            // Only read options if parsing succeeded
            if (!options.IsUndefined())
            {
                TemporalHelpers.GetOverflowOption(_realm, options);
            }

            return result;
        }

        // For property bags, read fields first, then options (per spec order)
        // First, check if item is an object (non-primitives)
        if (!item.IsObject())
        {
            // Let ToTemporalTime handle the type error for non-objects
            return ToTemporalTime(item, "constrain");
        }

        return ToTemporalTimeFromObject(item.AsObject(), options);
    }

    private JsPlainTime ToTemporalTimeFromObject(ObjectInstance obj, JsValue options)
    {
        // Read time fields first in alphabetical order per spec
        var hasAny = false;
        var hour = GetTimeProperty(obj, "hour", 0, ref hasAny);
        var microsecond = GetTimeProperty(obj, "microsecond", 0, ref hasAny);
        var millisecond = GetTimeProperty(obj, "millisecond", 0, ref hasAny);
        var minute = GetTimeProperty(obj, "minute", 0, ref hasAny);
        var nanosecond = GetTimeProperty(obj, "nanosecond", 0, ref hasAny);
        var second = GetTimeProperty(obj, "second", 0, ref hasAny);

        if (!hasAny)
        {
            Throw.TypeError(_realm, "Temporal time-like object must have at least one temporal property");
        }

        // Read options AFTER fields per spec
        var overflow = TemporalHelpers.GetOverflowOption(_realm, options);

        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, overflow);
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        return Construct(time.Value);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalTime(arguments.At(0), "constrain");
        var two = ToTemporalTime(arguments.At(1), "constrain");
        return JsNumber.Create(TemporalHelpers.CompareIsoTimes(one.IsoTime, two.IsoTime));
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainTime cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var hour = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(0), 0);
        var minute = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(1), 0);
        var second = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(2), 0);
        var millisecond = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(3), 0);
        var microsecond = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(4), 0);
        var nanosecond = TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, arguments.At(5), 0);

        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, "reject");
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        return Construct(time.Value, newTarget);
    }

    internal JsPlainTime Construct(IsoTime isoTime, JsValue? newTarget = null)
    {
        // OrdinaryCreateFromConstructor for subclassing support
        var proto = newTarget is null
            ? PrototypeObject
            : _realm.Intrinsics.Function.GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.TemporalPlainTime.PrototypeObject);

        return new JsPlainTime(_engine, proto, isoTime);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporaltime
    /// </summary>
    internal JsPlainTime ToTemporalTime(JsValue item, string overflow)
    {
        if (item is JsPlainTime plainTime)
        {
            // Return a COPY, not the original instance (per spec: from() always creates a new object)
            return Construct(plainTime.IsoTime);
        }

        if (item is JsPlainDateTime plainDateTime)
        {
            return Construct(plainDateTime.IsoDateTime.Time);
        }

        if (item is JsZonedDateTime zonedDateTime)
        {
            // Get the time part from zoned date time
            var time = zonedDateTime.GetIsoDateTime().Time;
            return Construct(time);
        }

        if (item.IsString())
        {
            var str = item.ToString();

            // Check for UTC designator - not allowed in PlainTime
            if (HasUtcDesignator(str))
            {
                Throw.RangeError(_realm, "UTC designator Z is not allowed for PlainTime");
            }

            // Try parsing as a time string (with potential annotations)
            var parseResult = ParseTimeString(str);
            if (parseResult.Error is not null)
            {
                Throw.RangeError(_realm, parseResult.Error);
            }

            if (parseResult.Time is not null)
            {
                return Construct(parseResult.Time.Value);
            }

            // Try parsing as date-time string
            var dtParsed = ParseTimeFromDateTimeString(str);
            if (dtParsed.Error is not null)
            {
                Throw.RangeError(_realm, dtParsed.Error);
            }

            if (dtParsed.Time is null)
            {
                Throw.RangeError(_realm, "Invalid time string");
            }

            return Construct(dtParsed.Time.Value);
        }

        if (item.IsObject())
        {
            var obj = item.AsObject();
            return ToTemporalTimeFromFields(obj, overflow);
        }

        Throw.TypeError(_realm, "Invalid time");
        return null!;
    }

    private JsPlainTime ToTemporalTimeFromFields(ObjectInstance obj, string overflow)
    {
        // Read time fields in alphabetical order per spec (hour, microsecond, millisecond, minute, nanosecond, second)
        // Note: PlainTime does NOT read calendar per ToTemporalTimeRecord spec
        var hasAny = false;
        var hour = GetTimeProperty(obj, "hour", 0, ref hasAny);
        var microsecond = GetTimeProperty(obj, "microsecond", 0, ref hasAny);
        var millisecond = GetTimeProperty(obj, "millisecond", 0, ref hasAny);
        var minute = GetTimeProperty(obj, "minute", 0, ref hasAny);
        var nanosecond = GetTimeProperty(obj, "nanosecond", 0, ref hasAny);
        var second = GetTimeProperty(obj, "second", 0, ref hasAny);

        // At least one temporal property must be present
        if (!hasAny)
        {
            Throw.TypeError(_realm, "Temporal time-like object must have at least one temporal property");
        }

        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, overflow);
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        return Construct(time.Value);
    }

    private int GetTimeProperty(ObjectInstance obj, string name, int defaultValue, ref bool hasAny)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return defaultValue;

        hasAny = true;

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"Time {name} must be a finite number");
        }

        // Truncate to integer per spec (ToIntegerWithTruncation behavior)
        return (int) System.Math.Truncate(number);
    }

    /// <summary>
    /// Parses a pure time string (with optional annotations) according to the Temporal spec.
    /// </summary>
    private static ParsedTimeResult ParseTimeString(string input)
    {
        // Empty strings are invalid
        if (string.IsNullOrEmpty(input))
        {
            return new ParsedTimeResult(null, "Empty string");
        }

        // Reject non-ASCII minus sign (U+2212)
        if (TemporalHelpers.ContainsInvalidMinusSign(input))
        {
            return new ParsedTimeResult(null, "Invalid minus sign");
        }

        // Check for T prefix - required for ambiguous strings
        var hasTimeDesignator = input.Length > 0 && (input[0] == 'T' || input[0] == 't');
        var str = hasTimeDesignator ? input.Substring(1) : input;

        // If no T prefix, check if the string is ambiguous
        if (!hasTimeDesignator && IsAmbiguousTimeString(str))
        {
            return new ParsedTimeResult(null, "Ambiguous ISO string requires 'T' time designator");
        }

        // Find where time ends and annotations/offset begin
        // Per spec grammar: Time DateTimeUTCOffset[~Z]? TimeZoneAnnotation? Annotations?
        // Strategy: Try parsing greedily - attempt longest match first

        // First, look for annotation bracket - that definitely marks end of time+offset
        var bracketPos = str.IndexOf('[');
        var searchEnd = bracketPos >= 0 ? bracketPos : str.Length;

        // Try to find offset marker within the time portion
        // We'll try parsing the whole thing first, then if that fails, look for splits
        var timeString = str.Substring(0, searchEnd);

        // Parse annotations
        var pos = searchEnd;
        var calendarCount = 0;
        var hasCalendarCritical = false;
        var timeZoneCount = 0;

        // Skip offset portion (+HH:MM or similar)
        if (pos < str.Length && (str[pos] == '+' || str[pos] == '-'))
        {
            pos++;
            while (pos < str.Length && (char.IsDigit(str[pos]) || str[pos] == ':' || str[pos] == '.' || str[pos] == ','))
            {
                pos++;
            }
        }

        while (pos < str.Length)
        {
            if (str[pos] == '[')
            {
                var endBracket = str.IndexOf(']', pos);
                if (endBracket < 0)
                {
                    return new ParsedTimeResult(null, "Unclosed bracket annotation");
                }

                var annotation = str.Substring(pos + 1, endBracket - pos - 1);

                // Check for critical flag (!)
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
                        return new ParsedTimeResult(null, "Annotation keys must be lowercase");
                    }

                    // Calendar annotations (u-ca=...) are tracked
                    if (StartsWithOrdinal(annotationContent, "u-ca="))
                    {
                        calendarCount++;
                        if (isCritical)
                        {
                            hasCalendarCritical = true;
                        }
                    }
                    else if (isCritical)
                    {
                        // Unknown key annotations are accepted unless critical
                        return new ParsedTimeResult(null, "Critical unknown annotation");
                    }
                }
                else
                {
                    // Time zone annotation (no '=')
                    timeZoneCount++;
                }

                pos = endBracket + 1;
            }
            else
            {
                // Trailing junk after annotations or offset
                return new ParsedTimeResult(null, "Trailing characters after annotations");
            }
        }

        // Check for invalid multiple calendar annotations with critical flag
        if (calendarCount > 1 && hasCalendarCritical)
        {
            return new ParsedTimeResult(null, "Multiple calendar annotations with critical flag");
        }

        // Check for multiple time zone annotations
        if (timeZoneCount > 1)
        {
            return new ParsedTimeResult(null, "Multiple time zone annotations");
        }

        // Try to parse the time string - attempt with offsets if needed
        // Per spec: Time DateTimeUTCOffset[~Z]?
        // Try parsing the whole string first (greedy)
        var time = TemporalHelpers.ParseIsoTime(timeString);

        if (time is null && timeString.Length > 0)
        {
            // Parsing failed - might have an offset. Try splitting at valid offset positions
            // Scan from the end looking for +/- that could mark start of offset
            // Must handle fractional times like HH:MM:SS.fff+offset

            // Find all potential split positions (where +/- appears), scanning from end
            for (var splitPos = timeString.Length - 3; splitPos >= 2; splitPos--)
            {
                var c = timeString[splitPos];
                if (c == '+' || c == '-')
                {
                    // Check for mixed separators before the split point (invalid)
                    var timePart = timeString.Substring(0, splitPos);
                    var hasColon = timePart.Contains(':');
                    var hasHyphen = timePart.Contains('-');
                    if (hasColon && hasHyphen)
                        continue; // Mixed separators - invalid

                    // Check if what follows is a valid UTC offset format
                    // Valid offset formats: +HH, +HH:MM, +HHMM, +HH:MM:SS, +HHMMSS
                    var offsetPart = timeString.Substring(splitPos + 1);
                    if (offsetPart.Length < 2)
                        continue; // Offset must have at least HH (2 digits)

                    // Quick validation: offset should be digits and optional colons only
                    var validOffsetChars = true;
                    var hasColonInOffset = false;
                    var colonPositions = new List<int>();
                    for (var i = 0; i < offsetPart.Length; i++)
                    {
                        var ch = offsetPart[i];
                        if (!char.IsDigit(ch) && ch != ':')
                        {
                            validOffsetChars = false;
                            break;
                        }

                        if (ch == ':')
                        {
                            hasColonInOffset = true;
                            colonPositions.Add(i);
                        }
                    }

                    if (!validOffsetChars)
                        continue;

                    // Offset must be one of these lengths: 2 (HH), 4 (HHMM), 5 (HH:MM), 6 (HHMMSS), 8 (HH:MM:SS)
                    // Note: 7 characters is NOT a valid offset format
                    if (offsetPart.Length != 2 && offsetPart.Length != 4 && offsetPart.Length != 5 &&
                        offsetPart.Length != 6 && offsetPart.Length != 8)
                        continue;

                    // Check separator consistency in offset
                    // Valid patterns: HH, HHMM, HH:MM, HHMMSS, HH:MM:SS
                    // Invalid: HH:MMSS, HHMM:SS (mixed separators)
                    if (hasColonInOffset)
                    {
                        // With colons, must be HH:MM or HH:MM:SS format
                        // First colon at position 2, second colon (if any) at position 5
                        if (colonPositions.Count > 0 && colonPositions[0] != 2)
                            continue; // First colon must be after HH
                        if (colonPositions.Count > 1 && colonPositions[1] != 5)
                            continue; // Second colon must be after MM
                        if (colonPositions.Count > 2)
                            continue; // At most 2 colons
                    }

                    // Validate offset hour (first 2 digits must be 00-23)
                    if (!int.TryParse(offsetPart.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var offsetHour) ||
                        offsetHour > 23)
                        continue;

                    // Validate offset minute if present (for HH:MM format)
                    if (offsetPart.Length >= 5 && offsetPart[2] == ':')
                    {
                        if (!int.TryParse(offsetPart.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var offsetMinute) ||
                            offsetMinute > 59)
                            continue;
                    }
                    // Validate offset minute for HHMM format
                    else if (offsetPart.Length >= 4 && char.IsDigit(offsetPart[2]) && char.IsDigit(offsetPart[3]))
                    {
                        if (!int.TryParse(offsetPart.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var offsetMinute) ||
                            offsetMinute > 59)
                            continue;
                    }

                    // Try parsing just the time part
                    time = TemporalHelpers.ParseIsoTime(timePart);
                    if (time is not null)
                    {
                        // Successfully parsed time part
                        // Note: For PlainTime, offset is ignored per spec (we just validate format if needed)
                        break;
                    }
                }
            }
        }

        if (time is null)
        {
            // Return null to indicate this isn't a pure time string (might be a date-time)
            return new ParsedTimeResult(null, null);
        }

        return new ParsedTimeResult(time, null);
    }

    /// <summary>
    /// Parses a PlainTime string from a date-time string according to the Temporal spec.
    /// Returns null for invalid strings, but may also return error for specific violations.
    /// </summary>
    private static ParsedTimeResult ParseTimeFromDateTimeString(string input)
    {
        // Try to parse as ISO date-time and extract the time part
        // Accept both 'T' (uppercase), 't' (lowercase), and space as time separators
        var tIndex = input.IndexOf('T');
        if (tIndex < 0)
            tIndex = input.IndexOf('t');

        var isSpaceSeparator = false;
        if (tIndex < 0)
        {
            tIndex = input.IndexOf(' ');
            isSpaceSeparator = tIndex >= 0;
        }

        if (tIndex < 0)
            return new ParsedTimeResult(null, null);

        // Space at the beginning is not a valid time designator
        if (isSpaceSeparator && tIndex == 0)
        {
            return new ParsedTimeResult(null, "Space is not accepted as a substitute for T prefix");
        }

        // Check the date portion for negative zero year
        var datePortion = input.Substring(0, tIndex);

        // Only check for negative zero year if datePortion is long enough
        if (datePortion.Length > 1)
        {
            var firstDash = datePortion.IndexOf('-', 1); // Start from index 1 to skip leading sign
            if (firstDash > 0)
            {
                var yearStr = datePortion.Substring(0, firstDash);
                if (TemporalHelpers.IsNegativeZeroYear(yearStr))
                {
                    return new ParsedTimeResult(null, "Negative zero year is not allowed");
                }
            }
        }

        var remainder = input.Substring(tIndex + 1);

        // Find where time ends and offset/annotations begin
        var timeEnd = remainder.Length;
        var hasUtcDesignator = false;

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

        // PlainTime cannot have UTC designator
        if (hasUtcDesignator)
        {
            return new ParsedTimeResult(null, "UTC designator Z is not allowed for PlainTime");
        }

        var timeString = remainder.Substring(0, timeEnd);

        // Parse annotations in brackets
        var pos = timeEnd;
        var calendarCount = 0;
        var hasCalendarCritical = false;
        var timeZoneCount = 0;
        while (pos < remainder.Length)
        {
            // Skip offset
            if (remainder[pos] == '+' || remainder[pos] == '-')
            {
                pos++;
                while (pos < remainder.Length && remainder[pos] != '[')
                {
                    pos++;
                }

                continue;
            }

            if (remainder[pos] == '[')
            {
                var endBracket = remainder.IndexOf(']', pos);
                if (endBracket < 0)
                {
                    return new ParsedTimeResult(null, "Unclosed bracket annotation");
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
                        return new ParsedTimeResult(null, "Annotation keys must be lowercase");
                    }

                    // Check if it's a calendar annotation (u-ca=...)
                    if (StartsWithOrdinal(annotationContent, "u-ca="))
                    {
                        calendarCount++;
                        if (isCritical)
                        {
                            hasCalendarCritical = true;
                        }
                    }
                    else if (isCritical)
                    {
                        // Unknown key annotation - accepted unless critical
                        return new ParsedTimeResult(null, "Critical unknown annotation");
                    }
                }
                else
                {
                    // Time zone annotation (no '=' sign)
                    timeZoneCount++;
                }

                pos = endBracket + 1;
            }
            else
            {
                pos++;
            }
        }

        // Check for invalid multiple calendar annotations with critical flag
        if (calendarCount > 1 && hasCalendarCritical)
        {
            return new ParsedTimeResult(null, "Multiple calendar annotations with critical flag");
        }

        // Check for multiple time zone annotations
        if (timeZoneCount > 1)
        {
            return new ParsedTimeResult(null, "Multiple time zone annotations");
        }

        // Handle leap second - normalize :60 to :59
        timeString = timeString.Replace(":60", ":59");

        var time = TemporalHelpers.ParseIsoTime(timeString);
        return new ParsedTimeResult(time, null);
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

    /// <summary>
    /// Checks if the string contains a UTC designator (Z) that is not inside a bracket annotation.
    /// </summary>
    private static bool HasUtcDesignator(string input)
    {
        var bracketDepth = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '[')
            {
                bracketDepth++;
            }
            else if (c == ']')
            {
                bracketDepth--;
            }
            else if ((c == 'Z' || c == 'z') && bracketDepth == 0)
            {
                return true;
            }
        }

        return false;
    }


    private readonly record struct ParsedTimeResult(IsoTime? Time, string? Error);

    /// <summary>
    /// Checks if a time string (without T prefix) is ambiguous with date formats.
    /// Ambiguous strings could be valid dates and require a "T" prefix for PlainTime.
    /// Based on TC39 Temporal spec requirement for disambiguation.
    /// </summary>
    private static bool IsAmbiguousTimeString(string str)
    {
        // Remove annotations to get the core string
        var coreString = str;
        var bracketIndex = str.IndexOf('[');
        if (bracketIndex >= 0)
        {
            coreString = str.Substring(0, bracketIndex);
        }

        // Strings with colons (in the time portion, not annotations) are unambiguous time (HH:MM:SS format)
        if (coreString.Contains(':'))
        {
            return false; // Has colon in time portion → unambiguous time
        }

        // Remove offset suffix (+/-HH:MM) to get the core date/time part
        for (var i = coreString.Length - 1; i >= 0; i--)
        {
            if (coreString[i] == '+' || coreString[i] == '-')
            {
                // Make sure this is an offset, not a date separator (must be after position 4)
                if (i > 4)
                {
                    coreString = coreString.Substring(0, i).TrimEnd();
                    break;
                }
            }
        }

        coreString = coreString.Trim();

        // Now check if the remaining string (without colons, annotations, or offsets)
        // could be a valid date format
        // YYYY-MM format (7 chars): could be year-month (PlainYearMonth)
        if (coreString.Length == 7 && coreString[4] == '-' &&
            char.IsDigit(coreString[0]) && char.IsDigit(coreString[5]))
        {
            if (int.TryParse(coreString.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
                month >= 1 && month <= 12)
            {
                return true; // Ambiguous: could be valid year-month
            }
        }

        // YYYYMM format (6 digits): could be year-month or time (HHMMSS)
        if (coreString.Length == 6 && char.IsDigit(coreString[0]) && char.IsDigit(coreString[5]))
        {
            if (int.TryParse(coreString.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
                month >= 1 && month <= 12)
            {
                return true; // Ambiguous: could be valid year-month
            }
        }

        // MM-DD format (5 chars): could be month-day (PlainMonthDay)
        if (coreString.Length == 5 && coreString[2] == '-' &&
            char.IsDigit(coreString[0]) && char.IsDigit(coreString[3]))
        {
            if (int.TryParse(coreString.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
                int.TryParse(coreString.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day) &&
                IsValidMonthDay(month, day))
            {
                return true; // Ambiguous: could be valid month-day
            }
        }

        // MMDD format (4 digits): could be month-day
        if (coreString.Length == 4 && char.IsDigit(coreString[0]) && char.IsDigit(coreString[3]))
        {
            if (int.TryParse(coreString.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
                int.TryParse(coreString.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day) &&
                IsValidMonthDay(month, day))
            {
                return true; // Ambiguous: could be valid month-day
            }
        }

        // Note: Per spec, 2-digit strings (HH) are NOT ambiguous with DateSpecMonthDay
        // because DateSpecMonthDay requires both month AND day components (MM-DD or MMDD)
        // So "15" is unambiguous (can only be hour), not ambiguous with day

        return false; // Unambiguous - can be parsed as time without T prefix
    }

    /// <summary>
    /// Checks if a month-day combination is valid (exists in at least some years).
    /// Used to determine if a string like "0230" is ambiguous (could be a date) or unambiguous (can only be time).
    /// Per spec: "0229" is ambiguous (Feb 29 exists in leap years), but "0230" is unambiguous (Feb 30 never exists).
    /// </summary>
    private static bool IsValidMonthDay(int month, int day)
    {
        // Month must be 1-12
        if (month < 1 || month > 12)
            return false;

        // Day must be at least 1
        if (day < 1)
            return false;

        // Check maximum days for each month
        // Use maximum possible days (assume leap year for February)
        var maxDays = month switch
        {
            2 => 29, // February (leap year)
            4 or 6 or 9 or 11 => 30, // April, June, September, November
            _ => 31 // January, March, May, July, August, October, December
        };

        return day <= maxDays;
    }
}

using System.Numerics;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-duration-prototype-object
/// </summary>
internal sealed class DurationPrototype : Prototype
{
    private readonly DurationConstructor _constructor;

    internal DurationPrototype(
        Engine engine,
        Realm realm,
        DurationConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(24, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["negated"] = new(new ClrFunction(Engine, "negated", Negated, 0, LengthFlags), PropertyFlags),
            ["abs"] = new(new ClrFunction(Engine, "abs", Abs, 0, LengthFlags), PropertyFlags),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["round"] = new(new ClrFunction(Engine, "round", Round, 1, LengthFlags), PropertyFlags),
            ["total"] = new(new ClrFunction(Engine, "total", Total, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToStringMethod, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["years"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get years", GetYears, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["months"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get months", GetMonths, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["weeks"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get weeks", GetWeeks, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["days"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get days", GetDays, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["hours"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get hours", GetHours, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["minutes"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get minutes", GetMinutes, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["seconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get seconds", GetSeconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["milliseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get milliseconds", GetMilliseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["microseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get microseconds", GetMicroseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["nanoseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get nanoseconds", GetNanoseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["sign"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get sign", GetSign, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["blank"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get blank", GetBlank, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.Duration", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsDuration ValidateDuration(JsValue thisObject)
    {
        if (thisObject is JsDuration duration)
            return duration;
        Throw.TypeError(_realm, "Value is not a Temporal.Duration");
        return null!;
    }

    private JsNumber GetYears(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Years);
    private JsNumber GetMonths(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Months);
    private JsNumber GetWeeks(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Weeks);
    private JsNumber GetDays(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Days);
    private JsNumber GetHours(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Hours);
    private JsNumber GetMinutes(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Minutes);
    private JsNumber GetSeconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Seconds);
    private JsNumber GetMilliseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Milliseconds);
    private JsNumber GetMicroseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Microseconds);
    private JsNumber GetNanoseconds(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).Nanoseconds);
    private JsNumber GetSign(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidateDuration(thisObject).DurationRecord.Sign());
    private JsBoolean GetBlank(JsValue thisObject, JsCallArguments arguments) => ValidateDuration(thisObject).DurationRecord.IsZero() ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.with
    /// </summary>
    private JsDuration With(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var temporalDurationLike = arguments.At(0);

        if (!temporalDurationLike.IsObject())
        {
            Throw.TypeError(_realm, "Duration-like object expected");
        }

        var obj = temporalDurationLike.AsObject();
        var record = duration.DurationRecord;

        // ToTemporalPartialDurationRecord: read properties in alphabetical order
        // Track if at least one property is defined
        var anyDefined = false;

        var days = GetOptionalDurationProperty(obj, "days", record.Days, ref anyDefined);
        var hours = GetOptionalDurationProperty(obj, "hours", record.Hours, ref anyDefined);
        var microseconds = GetOptionalDurationProperty(obj, "microseconds", record.Microseconds, ref anyDefined);
        var milliseconds = GetOptionalDurationProperty(obj, "milliseconds", record.Milliseconds, ref anyDefined);
        var minutes = GetOptionalDurationProperty(obj, "minutes", record.Minutes, ref anyDefined);
        var months = GetOptionalDurationProperty(obj, "months", record.Months, ref anyDefined);
        var nanoseconds = GetOptionalDurationProperty(obj, "nanoseconds", record.Nanoseconds, ref anyDefined);
        var seconds = GetOptionalDurationProperty(obj, "seconds", record.Seconds, ref anyDefined);
        var weeks = GetOptionalDurationProperty(obj, "weeks", record.Weeks, ref anyDefined);
        var years = GetOptionalDurationProperty(obj, "years", record.Years, ref anyDefined);

        if (!anyDefined)
        {
            Throw.TypeError(_realm, "Duration-like object must have at least one temporal property");
        }

        var newRecord = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);

        if (!TemporalHelpers.IsValidDuration(newRecord))
        {
            Throw.RangeError(_realm, "Invalid duration");
        }

        return _constructor.Construct(newRecord);
    }

    private double GetOptionalDurationProperty(ObjectInstance obj, string name, double defaultValue, ref bool anyDefined)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return defaultValue;

        anyDefined = true;

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be a finite number");
        }

        if (number != System.Math.Truncate(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be an integer");
        }

        return number;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.negated
    /// </summary>
    private JsDuration Negated(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        return _constructor.Construct(duration.DurationRecord.Negated());
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.abs
    /// </summary>
    private JsDuration Abs(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        return _constructor.Construct(duration.DurationRecord.Abs());
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.add
    /// https://tc39.es/proposal-temporal/#sec-temporal-adddurations
    /// </summary>
    private JsDuration Add(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var other = arguments.At(0);
        return AddDurations(1, duration, other);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.subtract
    /// https://tc39.es/proposal-temporal/#sec-temporal-adddurations
    /// </summary>
    private JsDuration Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var other = arguments.At(0);
        return AddDurations(-1, duration, other);
    }

    /// <summary>
    /// Implements the AddDurations abstract operation.
    /// https://tc39.es/proposal-temporal/#sec-temporal-adddurations
    /// </summary>
    private JsDuration AddDurations(int operation, JsDuration duration, JsValue otherValue)
    {
        // Step 1: ToTemporalDuration
        var other = _constructor.ToTemporalDuration(otherValue);

        // Step 2: If subtract, negate
        if (operation == -1)
        {
            other = _constructor.Construct(other.DurationRecord.Negated());
        }

        // Step 3-4: Get largest units
        var largestUnit1 = TemporalHelpers.DefaultTemporalLargestUnit(duration.DurationRecord);
        var largestUnit2 = TemporalHelpers.DefaultTemporalLargestUnit(other.DurationRecord);

        // Step 5: Get larger of two units
        var largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(largestUnit1, largestUnit2);

        // Step 6: If calendar unit, throw RangeError
        if (TemporalHelpers.IsCalendarUnit(largestUnit))
        {
            Throw.RangeError(_realm, "Cannot add durations with calendar units (years, months, or weeks) without relativeTo");
        }

        // Step 7-8: ToInternalDurationRecordWith24HourDays
        // Convert days to nanoseconds and combine with time
        var d1 = duration.DurationRecord;
        var d2 = other.DurationRecord;

        var time1 = TemporalHelpers.TimeDurationFromComponents(d1);
        time1 += new System.Numerics.BigInteger(d1.Days) * TemporalHelpers.NanosecondsPerDay;

        var time2 = TemporalHelpers.TimeDurationFromComponents(d2);
        time2 += new System.Numerics.BigInteger(d2.Days) * TemporalHelpers.NanosecondsPerDay;

        // Step 9: AddTimeDuration with range check
        var timeResult = TemporalHelpers.AddTimeDuration(_realm, time1, time2);

        // Step 10-11: CombineDateAndTimeDuration with ZeroDateDuration, then TemporalDurationFromInternal
        var result = TemporalHelpers.TemporalDurationFromInternal(_realm, 0, 0, 0, 0, timeResult, largestUnit);

        return _constructor.Construct(result);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.round
    /// </summary>
    private JsDuration Round(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var options = arguments.At(0);

        if (options.IsUndefined())
        {
            Throw.TypeError(_realm, "Options argument is required");
        }

        string? smallestUnit = null;
        string? largestUnit = null;
        string roundingMode = "halfExpand";
        double roundingIncrement = 1;
        TemporalHelpers.RelativeToResult relativeToResult = default;

        if (options.IsString())
        {
            smallestUnit = TemporalHelpers.ToSingularUnit(options.ToString());
            if (!TemporalHelpers.IsValidTemporalUnit(smallestUnit))
            {
                Throw.RangeError(_realm, $"Invalid smallest unit: {smallestUnit}");
            }
        }
        else if (options.IsObject())
        {
            var obj = options.AsObject();

            // Read options in alphabetical order per spec (see note at step 404 of spec)
            // 1. largestUnit
            var largestUnitValue = obj.Get("largestUnit");
            if (!largestUnitValue.IsUndefined())
            {
                var luStr = TypeConverter.ToString(largestUnitValue);
                largestUnit = TemporalHelpers.ToSingularUnit(luStr);
                // "auto" is a valid value for largestUnit in round()
                if (!string.Equals(largestUnit, "auto", StringComparison.Ordinal) &&
                    !TemporalHelpers.IsValidTemporalUnit(largestUnit))
                {
                    Throw.RangeError(_realm, $"Invalid largest unit: {largestUnit}");
                }
            }

            // 2. relativeTo - process immediately via GetTemporalRelativeToOption
            relativeToResult = TemporalHelpers.GetTemporalRelativeToOption(_engine, _realm, obj);

            // 3. roundingIncrement
            var roundingIncrementValue = obj.Get("roundingIncrement");
            if (!roundingIncrementValue.IsUndefined())
            {
                roundingIncrement = TypeConverter.ToNumber(roundingIncrementValue);
                if (double.IsNaN(roundingIncrement) || double.IsInfinity(roundingIncrement))
                {
                    Throw.RangeError(_realm, "roundingIncrement must be a finite number");
                }

                roundingIncrement = System.Math.Truncate(roundingIncrement); // spec: ToIntegerWithTruncation
                // Step 4: If integerIncrement < 1 or integerIncrement > 10^9, throw RangeError
                if (roundingIncrement < 1 || roundingIncrement > 1_000_000_000)
                {
                    Throw.RangeError(_realm, "roundingIncrement must be between 1 and 1000000000");
                }
            }

            // 4. roundingMode
            roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, obj, roundingMode);

            // 5. smallestUnit
            var smallestUnitValue = obj.Get("smallestUnit");
            if (!smallestUnitValue.IsUndefined())
            {
                var suStr = TypeConverter.ToString(smallestUnitValue);
                smallestUnit = TemporalHelpers.ToSingularUnit(suStr);
                if (!TemporalHelpers.IsValidTemporalUnit(smallestUnit))
                {
                    Throw.RangeError(_realm, $"Invalid smallest unit: {smallestUnit}");
                }
            }
        }
        else
        {
            Throw.TypeError(_realm, "Options must be a string or object");
        }

        if (smallestUnit is null && largestUnit is null)
        {
            Throw.RangeError(_realm, "At least one of smallestUnit or largestUnit is required");
        }

        var d = duration.DurationRecord;
        var existingLargestUnit = TemporalHelpers.DefaultTemporalLargestUnit(d);

        // Default values
        smallestUnit ??= "nanosecond";
        var defaultLargestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(existingLargestUnit, smallestUnit);

        // Handle "auto" - replace with default
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = defaultLargestUnit;
        }

        largestUnit ??= defaultLargestUnit;

        // Validate: largestUnit >= smallestUnit
        if (TemporalHelpers.TemporalUnitIndex(largestUnit) > TemporalHelpers.TemporalUnitIndex(smallestUnit))
        {
            Throw.RangeError(_realm, "largestUnit must be larger than or equal to smallestUnit");
        }

        // Validate: rounding increment must divide evenly into the next highest unit
        if (roundingIncrement > 1)
        {
            var maxIncrement = GetMaximumRoundingIncrement(smallestUnit);
            if (maxIncrement > 0)
            {
                if (roundingIncrement >= maxIncrement)
                {
                    Throw.RangeError(_realm, $"roundingIncrement must be less than {maxIncrement} for {smallestUnit}");
                }

                if (maxIncrement % roundingIncrement != 0)
                {
                    Throw.RangeError(_realm, $"roundingIncrement must divide evenly into {maxIncrement} for {smallestUnit}");
                }
            }
        }

        // Validate: Cannot round to increment > 1 of calendar units while also balancing
        if (roundingIncrement > 1)
        {
            var isSmallestCalendar = TemporalHelpers.IsCalendarUnit(smallestUnit) ||
                                     string.Equals(smallestUnit, "day", StringComparison.Ordinal);
            if (isSmallestCalendar && !string.Equals(largestUnit, smallestUnit, StringComparison.Ordinal))
            {
                Throw.RangeError(_realm, $"Cannot round to an increment of {smallestUnit} while also balancing to {largestUnit}");
            }
        }

        // Per spec (line 429-447): check zonedRelativeTo first, then plainRelativeTo
        var hasZonedRelativeTo = relativeToResult.ZonedRelativeTo != null;
        var hasPlainRelativeTo = relativeToResult.PlainRelativeTo != null;
        var hasRelativeTo = hasZonedRelativeTo || hasPlainRelativeTo;

        // Calendar units (years, months, weeks) require relativeTo
        var hasCalendarUnits = d.Years != 0 || d.Months != 0 || d.Weeks != 0;
        var needsCalendar = TemporalHelpers.IsCalendarUnit(largestUnit) || TemporalHelpers.IsCalendarUnit(smallestUnit);

        if ((hasCalendarUnits || needsCalendar) && !hasRelativeTo)
        {
            Throw.RangeError(_realm, "A relativeTo option is required for rounding durations with calendar units");
        }

        // Per spec: If relativeTo is provided, always use the relativeTo-aware path
        // The spec structure is: if zonedRelativeTo → zoned path, else if plainRelativeTo → plain path, else simple path
        if (hasRelativeTo)
        {
            var result = RoundDurationWithRelativeTo(_engine, _realm, d, smallestUnit, largestUnit, roundingIncrement, roundingMode, relativeToResult);

            if (!TemporalHelpers.IsValidDuration(result))
            {
                Throw.RangeError(_realm, "Invalid duration result");
            }

            return _constructor.Construct(result);
        }

        // Simple case: no calendar units, just time-based rounding
        var totalNs = TemporalHelpers.TotalDurationNanoseconds(d);

        // Round to the smallest unit
        var nsPerUnit = GetNanosecondsPerUnit(smallestUnit);
        var roundedNs = TemporalHelpers.RoundBigIntegerToIncrement(totalNs, (BigInteger) nsPerUnit * (long) roundingIncrement, roundingMode);

        // Balance based on largest unit
        var result2 = TemporalHelpers.BalanceTimeDuration(roundedNs, largestUnit);

        if (!TemporalHelpers.IsValidDuration(result2))
        {
            Throw.RangeError(_realm, "Invalid duration result");
        }

        return _constructor.Construct(result2);
    }

    private static DurationRecord RoundDurationWithRelativeTo(
        Engine engine,
        Realm realm,
        DurationRecord duration,
        string smallestUnit,
        string largestUnit,
        double roundingIncrement,
        string roundingMode,
        TemporalHelpers.RelativeToResult relativeTo)
    {
        if (relativeTo.PlainRelativeTo is not null)
        {
            return RoundDurationWithPlainDate(realm, duration, smallestUnit, largestUnit, roundingIncrement, roundingMode, relativeTo.PlainRelativeTo);
        }

        if (relativeTo.ZonedRelativeTo is not null)
        {
            return RoundDurationWithZonedDateTime(engine, realm, duration, smallestUnit, largestUnit, roundingIncrement, roundingMode, relativeTo.ZonedRelativeTo);
        }

        // Should not reach here
        return duration;
    }

    private static DurationRecord RoundDurationWithPlainDate(
        Realm realm,
        DurationRecord duration,
        string smallestUnit,
        string largestUnit,
        double roundingIncrement,
        string roundingMode,
        JsPlainDate plainRelativeTo)
    {
        // Spec: Duration.prototype.round lines 438-447
        // https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.round

        // Range check: if duration has too many days, throw before trying to process
        // Per spec ISODateTimeWithinLimits, days should be <= 10^8 + 1 from epoch
        var totalDays = System.Math.Abs(duration.Days + duration.Weeks * 7);
        if (totalDays > 100_000_001)
        {
            Throw.RangeError(realm, "Duration days out of valid range");
        }

        var relativeDateStart = plainRelativeTo.IsoDate;

        // Step 439: Convert to internal duration with 24-hour days
        // ToInternalDurationRecordWith24HourDays moves duration.Days into the time nanoseconds
        var timeDuration = TemporalHelpers.TimeDurationFromComponents(duration);
        timeDuration = TemporalHelpers.Add24HourDaysToTimeDurationChecked(realm, timeDuration, duration.Days);

        // Step 440: AddTime(MidnightTimeRecord(), internalDuration.[[Time]])
        // This may overflow past 24 hours, giving us additional days
        var totalNanos = timeDuration;
        var targetTimeDays = (long) (totalNanos / TemporalHelpers.NanosecondsPerDay);
        var remainderNanos = totalNanos % TemporalHelpers.NanosecondsPerDay;

        // Handle negative time - per spec, AddTime can produce negative days
        if (remainderNanos < 0 && targetTimeDays >= 0)
        {
            targetTimeDays--;
            remainderNanos += TemporalHelpers.NanosecondsPerDay;
        }
        else if (remainderNanos > 0 && targetTimeDays < 0)
        {
            // No adjustment needed
        }

        // Balance the remainder to time components
        var targetTimeBalance = TemporalHelpers.BalanceTimeDuration(remainderNanos, "hour");

        // Step 442: dateDuration = AdjustDateDurationRecord(internalDuration.[[Date]], targetTime.[[Days]])
        // internalDuration.[[Date]] has years, months, weeks, and 0 days (per ToInternalDurationRecordWith24HourDays)
        // We add targetTimeDays to get the adjusted date duration
        var adjustedDays = targetTimeDays;

        // Step 443: targetDate = CalendarDateAdd(calendar, relativeTo, dateDuration)
        var adjustedDuration = new DurationRecord(duration.Years, duration.Months, duration.Weeks, adjustedDays, 0, 0, 0, 0, 0, 0);
        var targetDate = TemporalHelpers.CalendarDateAdd(realm, "iso8601", relativeDateStart, adjustedDuration, "constrain");

        // Step 444-445: Combine isoDateTime and targetDateTime
        var isoDateTime = new IsoDateTime(relativeDateStart, IsoTime.Midnight);
        var targetDateTime = new IsoDateTime(targetDate, new IsoTime(
            (int) targetTimeBalance.Hours,
            (int) targetTimeBalance.Minutes,
            (int) targetTimeBalance.Seconds,
            (int) targetTimeBalance.Milliseconds,
            (int) targetTimeBalance.Microseconds,
            (int) targetTimeBalance.Nanoseconds));

        // Step 446: Call DifferencePlainDateTimeWithRounding
        return TemporalHelpers.DifferencePlainDateTimeWithRounding(
            realm,
            isoDateTime,
            targetDateTime,
            "iso8601", // calendar
            largestUnit,
            (int) roundingIncrement,
            smallestUnit,
            roundingMode);
    }

    private static DurationRecord RoundDurationWithZonedDateTime(
        Engine engine,
        Realm realm,
        DurationRecord duration,
        string smallestUnit,
        string largestUnit,
        double roundingIncrement,
        string roundingMode,
        JsZonedDateTime zonedRelativeTo)
    {
        // Per spec: Duration.prototype.round lines 429-437
        // https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.round

        // Step 431-432: Get timezone and calendar
        var timeZone = zonedRelativeTo.TimeZone;
        var calendar = zonedRelativeTo.Calendar;
        var provider = engine.Options.Temporal.TimeZoneProvider;

        // Step 433: Get relative epoch nanoseconds
        var relativeEpochNs = zonedRelativeTo.EpochNanoseconds;

        // Step 434: AddZonedDateTime to get target epoch nanoseconds
        var targetEpochNs = TemporalHelpers.AddZonedDateTime(
            realm,
            provider,
            relativeEpochNs,
            timeZone,
            calendar,
            duration,
            "constrain");

        // Step 435: DifferenceZonedDateTimeWithRounding
        return TemporalHelpers.DifferenceZonedDateTimeWithRounding(
            realm,
            provider,
            relativeEpochNs,
            targetEpochNs,
            timeZone,
            calendar,
            largestUnit,
            (int) roundingIncrement,
            smallestUnit,
            roundingMode);
    }

    private static DurationRecord BalanceDurationToUnit(IsoDate startDate, long totalDays, DurationRecord duration, string largestUnit)
    {
        // Balance duration components to the largest unit
        switch (largestUnit)
        {
            case "year":
                return BalanceToYears(startDate, totalDays, duration);
            case "month":
                return BalanceToMonths(startDate, totalDays, duration);
            case "week":
                return BalanceToWeeks(totalDays, duration);
            case "day":
                return new DurationRecord(0, 0, 0, totalDays, duration.Hours, duration.Minutes, duration.Seconds,
                    duration.Milliseconds, duration.Microseconds, duration.Nanoseconds);
            default:
                // For time units, convert days to hours
                var totalHours = totalDays * 24 + duration.Hours;
                return new DurationRecord(0, 0, 0, 0, totalHours, duration.Minutes, duration.Seconds,
                    duration.Milliseconds, duration.Microseconds, duration.Nanoseconds);
        }
    }

    private static DurationRecord BalanceToYears(IsoDate startDate, long totalDays, DurationRecord duration)
    {
        var years = 0;
        var months = 0;
        var days = totalDays;
        var currentDate = startDate;

        // Determine direction
        var isNegative = days < 0;

        if (isNegative)
        {
            // For negative durations, count backwards
            while (days < 0)
            {
                // Calculate days in the previous year
                var prevYear = new IsoDate(currentDate.Year - 1, currentDate.Month, currentDate.Day);
                if (!prevYear.IsValid())
                {
                    // Handle leap year edge case (Feb 29)
                    prevYear = new IsoDate(currentDate.Year - 1, currentDate.Month, 28);
                }

                var prevYearDays = TemporalHelpers.IsoDateToDays(prevYear.Year, prevYear.Month, prevYear.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var yearDays = prevYearDays - currentDays; // This will be negative

                if (days <= yearDays)
                {
                    years--;
                    days -= yearDays;
                    currentDate = prevYear;
                }
                else
                {
                    break;
                }
            }

            // Count complete months backwards in remaining days
            while (days < 0)
            {
                var prevMonth = AddMonths(currentDate, -1);
                var prevMonthDays = TemporalHelpers.IsoDateToDays(prevMonth.Year, prevMonth.Month, prevMonth.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var monthDays = prevMonthDays - currentDays; // This will be negative

                if (days <= monthDays)
                {
                    months--;
                    days -= monthDays;
                    currentDate = prevMonth;
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            // Count complete years forward
            while (days > 0)
            {
                var nextYear = new IsoDate(currentDate.Year + 1, currentDate.Month, currentDate.Day);
                if (!nextYear.IsValid())
                {
                    // Handle leap year edge case (Feb 29)
                    nextYear = new IsoDate(currentDate.Year + 1, currentDate.Month, 28);
                }

                var nextYearDays = TemporalHelpers.IsoDateToDays(nextYear.Year, nextYear.Month, nextYear.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var yearDays = nextYearDays - currentDays;

                if (days >= yearDays)
                {
                    years++;
                    days -= yearDays;
                    currentDate = nextYear;
                }
                else
                {
                    break;
                }
            }

            // Count complete months in remaining days
            while (days > 0)
            {
                var nextMonth = AddMonths(currentDate, 1);
                var nextMonthDays = TemporalHelpers.IsoDateToDays(nextMonth.Year, nextMonth.Month, nextMonth.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var monthDays = nextMonthDays - currentDays;

                if (days >= monthDays)
                {
                    months++;
                    days -= monthDays;
                    currentDate = nextMonth;
                }
                else
                {
                    break;
                }
            }
        }

        return new DurationRecord(years, months, 0, days, duration.Hours, duration.Minutes, duration.Seconds,
            duration.Milliseconds, duration.Microseconds, duration.Nanoseconds);
    }

    private static DurationRecord BalanceToMonths(IsoDate startDate, long totalDays, DurationRecord duration)
    {
        var months = 0;
        var days = totalDays;
        var currentDate = startDate;

        if (days < 0)
        {
            // For negative durations, count backwards
            while (days < 0)
            {
                var prevMonth = AddMonths(currentDate, -1);
                var prevMonthDays = TemporalHelpers.IsoDateToDays(prevMonth.Year, prevMonth.Month, prevMonth.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var monthDays = prevMonthDays - currentDays; // This will be negative

                if (days <= monthDays)
                {
                    months--;
                    days -= monthDays;
                    currentDate = prevMonth;
                }
                else
                {
                    break;
                }
            }
        }
        else
        {
            // For positive durations, count forward
            while (days > 0)
            {
                var nextMonth = AddMonths(currentDate, 1);
                var nextMonthDays = TemporalHelpers.IsoDateToDays(nextMonth.Year, nextMonth.Month, nextMonth.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var monthDays = nextMonthDays - currentDays;

                if (days >= monthDays)
                {
                    months++;
                    days -= monthDays;
                    currentDate = nextMonth;
                }
                else
                {
                    break;
                }
            }
        }

        return new DurationRecord(0, months, 0, days, duration.Hours, duration.Minutes, duration.Seconds,
            duration.Milliseconds, duration.Microseconds, duration.Nanoseconds);
    }

    private static DurationRecord BalanceToWeeks(long totalDays, DurationRecord duration)
    {
        var weeks = totalDays / 7;
        var days = totalDays % 7;

        return new DurationRecord(0, 0, weeks, days, duration.Hours, duration.Minutes, duration.Seconds,
            duration.Milliseconds, duration.Microseconds, duration.Nanoseconds);
    }

    private static IsoDate AddMonths(IsoDate date, int months)
    {
        var newMonth = date.Month + months;
        var newYear = date.Year;

        while (newMonth > 12)
        {
            newMonth -= 12;
            newYear++;
        }

        while (newMonth < 1)
        {
            newMonth += 12;
            newYear--;
        }

        var newDay = System.Math.Min(date.Day, IsoDate.IsoDateInMonth(newYear, newMonth));
        return new IsoDate(newYear, newMonth, newDay);
    }

    private static DurationRecord RoundBalancedDuration(DurationRecord duration, string smallestUnit, double increment, string roundingMode)
    {
        // Convert duration to the smallest unit and apply rounding
        // https://tc39.es/proposal-temporal/#sec-temporal-roundduration

        double valueToRound = 0;

        // Extract the value to round based on smallestUnit
        switch (smallestUnit)
        {
            case "year":
                // Convert entire duration to fractional years for rounding
                // years + months/12 + (approximate days/365)
                valueToRound = duration.Years + duration.Months / 12.0 + duration.Days / 365.0;
                break;
            case "month":
                // Convert years to months and add fractional months from days
                valueToRound = duration.Years * 12 + duration.Months + duration.Days / 30.0;
                break;
            case "week":
                valueToRound = duration.Weeks;
                break;
            case "day":
                // Include fractional days from time components
                var totalNs = TemporalHelpers.TotalDurationNanoseconds(new DurationRecord(
                    0, 0, 0, 0,
                    duration.Hours, duration.Minutes, duration.Seconds,
                    duration.Milliseconds, duration.Microseconds, duration.Nanoseconds));
                valueToRound = duration.Days + (double) totalNs / TemporalHelpers.NanosecondsPerDay;
                break;
            case "hour":
                var nsHour = TemporalHelpers.TotalDurationNanoseconds(new DurationRecord(
                    0, 0, 0, 0,
                    0, duration.Minutes, duration.Seconds,
                    duration.Milliseconds, duration.Microseconds, duration.Nanoseconds));
                valueToRound = duration.Hours + (double) nsHour / TemporalHelpers.NanosecondsPerHour;
                break;
            case "minute":
                var nsMinute = TemporalHelpers.TotalDurationNanoseconds(new DurationRecord(
                    0, 0, 0, 0,
                    0, 0, duration.Seconds,
                    duration.Milliseconds, duration.Microseconds, duration.Nanoseconds));
                valueToRound = duration.Minutes + (double) nsMinute / TemporalHelpers.NanosecondsPerMinute;
                break;
            case "second":
                var nsSecond = TemporalHelpers.TotalDurationNanoseconds(new DurationRecord(
                    0, 0, 0, 0,
                    0, 0, 0,
                    duration.Milliseconds, duration.Microseconds, duration.Nanoseconds));
                valueToRound = duration.Seconds + (double) nsSecond / TemporalHelpers.NanosecondsPerSecond;
                break;
            case "millisecond":
                var nsMilli = TemporalHelpers.TotalDurationNanoseconds(new DurationRecord(
                    0, 0, 0, 0,
                    0, 0, 0,
                    0, duration.Microseconds, duration.Nanoseconds));
                valueToRound = duration.Milliseconds + (double) nsMilli / TemporalHelpers.NanosecondsPerMillisecond;
                break;
            case "microsecond":
                var nsMicro = TemporalHelpers.TotalDurationNanoseconds(new DurationRecord(
                    0, 0, 0, 0,
                    0, 0, 0,
                    0, 0, duration.Nanoseconds));
                valueToRound = duration.Microseconds + (double) nsMicro / TemporalHelpers.NanosecondsPerMicrosecond;
                break;
            default:
                return duration; // nanosecond or unknown
        }

        // Apply rounding increment
        var rounded = ApplyRounding(valueToRound, increment, roundingMode);

        // Reconstruct the duration with rounded value and zero out smaller units
        return smallestUnit switch
        {
            "year" => new DurationRecord(rounded, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            "month" => new DurationRecord(duration.Years, rounded, 0, 0, 0, 0, 0, 0, 0, 0),
            "week" => new DurationRecord(duration.Years, duration.Months, rounded, 0, 0, 0, 0, 0, 0, 0),
            "day" => new DurationRecord(duration.Years, duration.Months, duration.Weeks, rounded, 0, 0, 0, 0, 0, 0),
            "hour" => new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, rounded, 0, 0, 0, 0, 0),
            "minute" => new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, duration.Hours, rounded, 0, 0, 0, 0),
            "second" => new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, duration.Hours, duration.Minutes, rounded, 0, 0, 0),
            "millisecond" => new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, duration.Hours, duration.Minutes, duration.Seconds, rounded, 0, 0),
            "microsecond" => new DurationRecord(duration.Years, duration.Months, duration.Weeks, duration.Days, duration.Hours, duration.Minutes, duration.Seconds, duration.Milliseconds, rounded, 0),
            _ => duration
        };
    }

    private static double ApplyRounding(double value, double increment, string roundingMode)
    {
        return TemporalHelpers.RoundNumberToIncrement(value, increment, roundingMode);
    }

    private static double RoundTowardZero(double value)
    {
        // Round towards zero (equivalent to MidpointRounding.ToZero in newer frameworks)
        var abs = System.Math.Abs(value);
        var rounded = System.Math.Floor(abs + 0.5);
        var fraction = abs - System.Math.Floor(abs);

        // If exactly at midpoint (0.5), round toward zero
        if (System.Math.Abs(fraction - 0.5) < double.Epsilon)
        {
            rounded = System.Math.Floor(abs);
        }

        return value >= 0 ? rounded : -rounded;
    }

    private static System.Numerics.BigInteger RoundBigIntToIncrement(System.Numerics.BigInteger value, System.Numerics.BigInteger increment, string roundingMode)
    {
        if (increment <= 1)
            return value;

        var quotient = (double) value / (double) increment;

        double rounded = roundingMode switch
        {
            "ceil" => System.Math.Ceiling(quotient),
            "floor" => System.Math.Floor(quotient),
            "expand" => quotient >= 0 ? System.Math.Ceiling(quotient) : System.Math.Floor(quotient),
            "trunc" => System.Math.Truncate(quotient),
            "halfExpand" => System.Math.Round(quotient, MidpointRounding.AwayFromZero),
            "halfCeil" => quotient >= 0
                ? System.Math.Round(quotient, MidpointRounding.AwayFromZero)
                : RoundTowardZero(quotient),
            "halfFloor" => quotient >= 0
                ? RoundTowardZero(quotient)
                : System.Math.Round(quotient, MidpointRounding.AwayFromZero),
            "halfTrunc" => RoundTowardZero(quotient),
            "halfEven" => System.Math.Round(quotient, MidpointRounding.ToEven),
            _ => quotient
        };

        return new System.Numerics.BigInteger(rounded) * increment;
    }

    private static double TotalDurationWithRelativeTo(
        Engine engine,
        Realm realm,
        DurationRecord duration,
        string unit,
        TemporalHelpers.RelativeToResult relativeTo)
    {
        if (relativeTo.PlainRelativeTo is not null)
        {
            return TotalDurationWithPlainDate(realm, duration, unit, relativeTo.PlainRelativeTo);
        }

        if (relativeTo.ZonedRelativeTo is not null)
        {
            return TotalDurationWithZonedDateTime(engine, realm, duration, unit, relativeTo.ZonedRelativeTo);
        }

        return 0;
    }

    private static double TotalDurationWithPlainDate(
        Realm realm,
        DurationRecord duration,
        string unit,
        JsPlainDate plainRelativeTo)
    {
        // Per spec: Duration.prototype.total lines 489-497
        // https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.total

        // Step 490: ToInternalDurationRecordWith24HourDays
        var timeDuration = TemporalHelpers.TimeDurationFromComponents(duration);
        timeDuration = TemporalHelpers.Add24HourDaysToTimeDurationChecked(realm, timeDuration, duration.Days);

        // Step 491: AddTime(MidnightTimeRecord(), internalDuration.Time)
        var targetTimeDays = (long) (timeDuration / TemporalHelpers.NanosecondsPerDay);
        var remainderNanos = timeDuration % TemporalHelpers.NanosecondsPerDay;

        // Handle negative time
        if (remainderNanos < 0)
        {
            targetTimeDays--;
            remainderNanos += TemporalHelpers.NanosecondsPerDay;
        }

        // Balance the remainder to time components
        var targetTimeBalance = TemporalHelpers.BalanceTimeDuration(remainderNanos, "hour");

        // Step 493: AdjustDateDurationRecord
        var adjustedDuration = new DurationRecord(duration.Years, duration.Months, duration.Weeks, targetTimeDays, 0, 0, 0, 0, 0, 0);

        // Step 494: CalendarDateAdd to get targetDate
        var calendar = plainRelativeTo.Calendar;
        var targetDate = TemporalHelpers.CalendarDateAdd(realm, calendar, plainRelativeTo.IsoDate, adjustedDuration, "constrain");

        // Step 495-496: Combine start and target as IsoDateTime
        var isoDateTime = new IsoDateTime(plainRelativeTo.IsoDate, IsoTime.Midnight);
        var targetDateTime = new IsoDateTime(targetDate, new IsoTime(
            (int) targetTimeBalance.Hours,
            (int) targetTimeBalance.Minutes,
            (int) targetTimeBalance.Seconds,
            (int) targetTimeBalance.Milliseconds,
            (int) targetTimeBalance.Microseconds,
            (int) targetTimeBalance.Nanoseconds));

        // Step 497: Call DifferencePlainDateTimeWithTotal
        return TemporalHelpers.DifferencePlainDateTimeWithTotal(
            realm,
            isoDateTime,
            targetDateTime,
            calendar,
            unit);
    }

    private static double TotalDurationWithZonedDateTime(
        Engine engine,
        Realm realm,
        DurationRecord duration,
        string unit,
        JsZonedDateTime zonedRelativeTo)
    {
        // Per spec: Duration.prototype.total lines 482-488
        // https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.total

        // Get starting epoch nanoseconds
        var ns1 = zonedRelativeTo.EpochNanoseconds;
        var timeZone = zonedRelativeTo.TimeZone;
        var calendar = zonedRelativeTo.Calendar;

        // Get timezone provider from engine
        var provider = engine.Options.Temporal.TimeZoneProvider;

        // Step 487: Add duration to get target epoch nanoseconds
        // Uses AddZonedDateTime which handles calendar-aware date addition + timezone-aware time addition
        var ns2 = TemporalHelpers.AddZonedDateTime(
            realm,
            provider,
            ns1,
            timeZone,
            calendar,
            duration,
            "constrain");

        // Step 488: Compute total using DifferenceZonedDateTimeWithTotal
        // This operation handles both calendar units and time units correctly
        return TemporalHelpers.DifferenceZonedDateTimeWithTotal(
            realm,
            provider,
            ns1,
            ns2,
            timeZone,
            calendar,
            unit);
    }

    private static double TotalInYears(IsoDate startDate, IsoDate endDate, DurationRecord duration)
    {
        var startDays = TemporalHelpers.IsoDateToDays(startDate.Year, startDate.Month, startDate.Day);
        var endDays = TemporalHelpers.IsoDateToDays(endDate.Year, endDate.Month, endDate.Day);
        var totalDays = endDays - startDays;

        // Handle negative durations
        if (totalDays < 0)
        {
            // For negative durations, count years in the negative direction
            var years = 0;
            var currentDate = startDate;
            var remainingDays = totalDays;

            while (true)
            {
                var prevYear = new IsoDate(currentDate.Year - 1, currentDate.Month, currentDate.Day);
                if (!prevYear.IsValid())
                {
                    prevYear = new IsoDate(currentDate.Year - 1, currentDate.Month, 28);
                }

                var prevYearDays = TemporalHelpers.IsoDateToDays(prevYear.Year, prevYear.Month, prevYear.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var yearDays = currentDays - prevYearDays; // positive

                if (remainingDays <= -yearDays)
                {
                    years--;
                    remainingDays += yearDays;
                    currentDate = prevYear;
                }
                else
                {
                    break;
                }
            }

            // Calculate fractional year from remaining days
            var prevYear2 = new IsoDate(currentDate.Year - 1, currentDate.Month, currentDate.Day);
            if (!prevYear2.IsValid())
            {
                prevYear2 = new IsoDate(currentDate.Year - 1, currentDate.Month, 28);
            }

            var daysInYear = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day) -
                             TemporalHelpers.IsoDateToDays(prevYear2.Year, prevYear2.Month, prevYear2.Day);

            return years + (double) remainingDays / daysInYear;
        }

        // Count complete years
        var positiveYears = 0;
        var currentDatePositive = startDate;

        while (true)
        {
            var nextYear = new IsoDate(currentDatePositive.Year + 1, currentDatePositive.Month, currentDatePositive.Day);
            if (!nextYear.IsValid())
            {
                nextYear = new IsoDate(currentDatePositive.Year + 1, currentDatePositive.Month, 28);
            }

            var nextYearDays = TemporalHelpers.IsoDateToDays(nextYear.Year, nextYear.Month, nextYear.Day);
            var currentDays = TemporalHelpers.IsoDateToDays(currentDatePositive.Year, currentDatePositive.Month, currentDatePositive.Day);
            var yearDays = nextYearDays - currentDays;

            if (totalDays >= yearDays)
            {
                positiveYears++;
                totalDays -= yearDays;
                currentDatePositive = nextYear;
            }
            else
            {
                break;
            }
        }

        // Calculate fractional year from remaining days
        var nextYear2 = new IsoDate(currentDatePositive.Year + 1, currentDatePositive.Month, currentDatePositive.Day);
        if (!nextYear2.IsValid())
        {
            nextYear2 = new IsoDate(currentDatePositive.Year + 1, currentDatePositive.Month, 28);
        }

        var daysInYearPositive = TemporalHelpers.IsoDateToDays(nextYear2.Year, nextYear2.Month, nextYear2.Day) -
                                 TemporalHelpers.IsoDateToDays(currentDatePositive.Year, currentDatePositive.Month, currentDatePositive.Day);

        return positiveYears + (double) totalDays / daysInYearPositive;
    }

    private static double TotalInMonths(IsoDate startDate, IsoDate endDate, DurationRecord duration)
    {
        var startDays = TemporalHelpers.IsoDateToDays(startDate.Year, startDate.Month, startDate.Day);
        var endDays = TemporalHelpers.IsoDateToDays(endDate.Year, endDate.Month, endDate.Day);
        var totalDays = endDays - startDays;

        // Handle negative durations
        if (totalDays < 0)
        {
            var months = 0;
            var currentDate = startDate;
            var remainingDays = totalDays;

            while (true)
            {
                var prevMonth = AddMonths(currentDate, -1);
                var prevMonthDays = TemporalHelpers.IsoDateToDays(prevMonth.Year, prevMonth.Month, prevMonth.Day);
                var currentDays = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day);
                var monthDays = currentDays - prevMonthDays; // positive

                if (remainingDays <= -monthDays)
                {
                    months--;
                    remainingDays += monthDays;
                    currentDate = prevMonth;
                }
                else
                {
                    break;
                }
            }

            // Calculate fractional month from remaining days
            var prevMonth2 = AddMonths(currentDate, -1);
            var daysInMonth = TemporalHelpers.IsoDateToDays(currentDate.Year, currentDate.Month, currentDate.Day) -
                              TemporalHelpers.IsoDateToDays(prevMonth2.Year, prevMonth2.Month, prevMonth2.Day);

            return months + (double) remainingDays / daysInMonth;
        }

        var positiveMonths = 0;
        var currentDatePositive = startDate;

        while (true)
        {
            var nextMonth = AddMonths(currentDatePositive, 1);
            var nextMonthDays = TemporalHelpers.IsoDateToDays(nextMonth.Year, nextMonth.Month, nextMonth.Day);
            var currentDays = TemporalHelpers.IsoDateToDays(currentDatePositive.Year, currentDatePositive.Month, currentDatePositive.Day);
            var monthDays = nextMonthDays - currentDays;

            if (totalDays >= monthDays)
            {
                positiveMonths++;
                totalDays -= monthDays;
                currentDatePositive = nextMonth;
            }
            else
            {
                break;
            }
        }

        // Calculate fractional month from remaining days
        var nextMonth2 = AddMonths(currentDatePositive, 1);
        var daysInMonthPositive = TemporalHelpers.IsoDateToDays(nextMonth2.Year, nextMonth2.Month, nextMonth2.Day) -
                                  TemporalHelpers.IsoDateToDays(currentDatePositive.Year, currentDatePositive.Month, currentDatePositive.Day);

        return positiveMonths + (double) totalDays / daysInMonthPositive;
    }

    private static double TotalInWeeks(IsoDate startDate, IsoDate endDate, DurationRecord duration)
    {
        var startDays = TemporalHelpers.IsoDateToDays(startDate.Year, startDate.Month, startDate.Day);
        var endDays = TemporalHelpers.IsoDateToDays(endDate.Year, endDate.Month, endDate.Day);
        var totalDays = endDays - startDays;

        return (double) totalDays / 7.0;
    }

    private static double TotalInDays(IsoDate startDate, IsoDate endDate, DurationRecord duration)
    {
        var startDays = TemporalHelpers.IsoDateToDays(startDate.Year, startDate.Month, startDate.Day);
        var endDays = TemporalHelpers.IsoDateToDays(endDate.Year, endDate.Month, endDate.Day);
        return endDays - startDays;
    }

    private static double ConvertDaysToTimeUnit(double totalDays, string unit, DurationRecord duration)
    {
        // Convert days to nanoseconds, add time components, then convert to target unit
        var daysNs = (System.Numerics.BigInteger) (totalDays * 86_400_000_000_000L);
        var timeNs = new System.Numerics.BigInteger(duration.Hours) * new System.Numerics.BigInteger(TemporalHelpers.NanosecondsPerHour)
                     + new System.Numerics.BigInteger(duration.Minutes) * new System.Numerics.BigInteger(TemporalHelpers.NanosecondsPerMinute)
                     + new System.Numerics.BigInteger(duration.Seconds) * new System.Numerics.BigInteger(TemporalHelpers.NanosecondsPerSecond)
                     + new System.Numerics.BigInteger(duration.Milliseconds) * new System.Numerics.BigInteger(TemporalHelpers.NanosecondsPerMillisecond)
                     + new System.Numerics.BigInteger(duration.Microseconds) * new System.Numerics.BigInteger(TemporalHelpers.NanosecondsPerMicrosecond)
                     + new System.Numerics.BigInteger(duration.Nanoseconds);

        var totalNs = daysNs + timeNs;
        var nsPerUnit = GetNanosecondsPerUnit(unit);
        return (double) totalNs / nsPerUnit;
    }

    private static long GetNanosecondsPerUnit(string unit)
    {
        return unit switch
        {
            "day" => 86_400_000_000_000L,
            "hour" => 3_600_000_000_000L,
            "minute" => 60_000_000_000L,
            "second" => 1_000_000_000L,
            "millisecond" => 1_000_000L,
            "microsecond" => 1_000L,
            "nanosecond" => 1L,
            _ => 0
        };
    }

    private static int GetMaximumRoundingIncrement(string unit)
    {
        return unit switch
        {
            "hour" => 24,
            "minute" => 60,
            "second" => 60,
            "millisecond" => 1000,
            "microsecond" => 1000,
            "nanosecond" => 1000,
            _ => 0 // No validation for day/week/month/year
        };
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.total
    /// </summary>
    private JsNumber Total(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var options = arguments.At(0);

        if (options.IsUndefined())
        {
            Throw.TypeError(_realm, "Options argument is required");
        }

        string? unit = null;
        TemporalHelpers.RelativeToResult relativeToResult = default;

        if (options.IsString())
        {
            unit = options.ToString();
        }
        else if (options.IsObject())
        {
            var obj = options.AsObject();

            // Read options in alphabetical order per spec (see note at step 476 of spec)
            // 1. relativeTo - process immediately via GetTemporalRelativeToOption
            relativeToResult = TemporalHelpers.GetTemporalRelativeToOption(_engine, _realm, obj);

            // 2. unit
            var unitValue = obj.Get("unit");
            if (!unitValue.IsUndefined())
            {
                unit = TypeConverter.ToString(unitValue);
            }
        }
        else
        {
            Throw.TypeError(_realm, "Options must be a string or object");
        }

        if (unit is null)
        {
            Throw.RangeError(_realm, "Unit option is required");
        }

        unit = TemporalHelpers.ToSingularUnit(unit);

        if (!TemporalHelpers.IsValidTemporalUnit(unit))
        {
            Throw.RangeError(_realm, $"Invalid unit: {unit}");
        }

        var d = duration.DurationRecord;

        // Determine if this duration has calendar units (years, months, weeks, days)
        var hasCalendarUnits = d.Years != 0 || d.Months != 0 || d.Weeks != 0;
        var calendarUnitsPresent = hasCalendarUnits || d.Days != 0;

        // Calendar units (year, month, week) always require relativeTo
        var isCalendarUnit = TemporalHelpers.IsCalendarUnit(unit);

        // Check if relativeTo is required
        var needsRelativeTo = isCalendarUnit || hasCalendarUnits;
        var hasRelativeTo = relativeToResult.PlainRelativeTo != null || relativeToResult.ZonedRelativeTo != null;

        if (needsRelativeTo && !hasRelativeTo)
        {
            Throw.RangeError(_realm, "A relativeTo option is required for total with calendar units");
        }

        // Per spec step 19: If zonedRelativeTo is not undefined, ALWAYS use ZDT path
        if (relativeToResult.ZonedRelativeTo != null)
        {
            var total = TotalDurationWithZonedDateTime(_engine, _realm, d, unit, relativeToResult.ZonedRelativeTo);
            return JsNumber.Create(total);
        }

        // Per spec step 20: If plainRelativeTo is not undefined, ALWAYS use PlainDate path
        if (relativeToResult.PlainRelativeTo != null)
        {
            var total = TotalDurationWithPlainDate(_realm, d, unit, relativeToResult.PlainRelativeTo);
            return JsNumber.Create(total);
        }

        // Per spec step 21: Simple case - no relativeTo
        // Step 21a-b: DefaultTemporalLargestUnit and validate no calendar units
        var largestUnit = TemporalHelpers.DefaultTemporalLargestUnit(d);
        if (TemporalHelpers.IsCalendarUnit(largestUnit) || isCalendarUnit)
        {
            Throw.RangeError(_realm, "A relativeTo option is required for total with calendar units");
        }

        // Step 21c: ToInternalDurationRecordWith24HourDays → days are converted to 24-hour nanoseconds
        var timeDuration = TemporalHelpers.TimeDurationFromComponents(d);
        timeDuration = TemporalHelpers.Add24HourDaysToTimeDuration(timeDuration, d.Days);

        // Step 21d: TotalTimeDuration
        return JsNumber.Create(TemporalHelpers.TotalTimeDuration(timeDuration, unit));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.tostring
    /// </summary>
    private JsString ToStringMethod(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var options = arguments.At(0);

        // Default: auto precision, trunc rounding, no rounding needed
        int precision = -1; // -1 means "auto"
        string roundingMode = "trunc";
        long increment = 1;

        if (!options.IsUndefined())
        {
            if (!options.IsObject())
            {
                Throw.TypeError(_realm, "Options must be an object");
            }

            var obj = options.AsObject();

            // Read options in alphabetical order: fractionalSecondDigits, roundingMode, smallestUnit
            // fractionalSecondDigits - uses GetStringOrNumberOption semantics
            var fsdValue = obj.Get("fractionalSecondDigits");
            int? fractionalSecondDigits = null;
            if (!fsdValue.IsUndefined())
            {
                if (fsdValue.IsNumber())
                {
                    var fsdNum = fsdValue.AsNumber();
                    if (double.IsNaN(fsdNum) || double.IsInfinity(fsdNum))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be 'auto' or a number 0-9");
                    }

                    var floored = (int) System.Math.Floor(fsdNum);
                    if (floored < 0 || floored > 9)
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be 'auto' or a number 0-9");
                    }

                    fractionalSecondDigits = floored;
                }
                else
                {
                    // ToString coercion per spec (triggers observable ToString on objects)
                    var fsdStr = TypeConverter.ToString(fsdValue);
                    if (!string.Equals(fsdStr, "auto", StringComparison.Ordinal))
                    {
                        Throw.RangeError(_realm, $"fractionalSecondDigits must be 'auto' or a number 0-9, got '{fsdStr}'");
                    }
                }
            }

            // roundingMode
            var rmValue = obj.Get("roundingMode");
            if (!rmValue.IsUndefined())
            {
                roundingMode = TypeConverter.ToString(rmValue);
                if (!TemporalHelpers.IsValidRoundingMode(roundingMode))
                {
                    Throw.RangeError(_realm, $"Invalid rounding mode: {roundingMode}");
                }
            }

            // smallestUnit
            string? smallestUnit = null;
            var suValue = obj.Get("smallestUnit");
            if (!suValue.IsUndefined())
            {
                var suStr = TypeConverter.ToString(suValue);
                smallestUnit = TemporalHelpers.ToSingularUnit(suStr);

                // Only second, millisecond, microsecond, nanosecond are valid for toString
                if (smallestUnit is not ("second" or "millisecond" or "microsecond" or "nanosecond"))
                {
                    Throw.RangeError(_realm, $"Invalid smallest unit for Duration.toString: {suStr}");
                }
            }

            // Determine precision from smallestUnit or fractionalSecondDigits
            if (smallestUnit is not null)
            {
                // smallestUnit takes priority
                switch (smallestUnit)
                {
                    case "second":
                        precision = 0;
                        increment = TemporalHelpers.NanosecondsPerSecond;
                        break;
                    case "millisecond":
                        precision = 3;
                        increment = TemporalHelpers.NanosecondsPerMillisecond;
                        break;
                    case "microsecond":
                        precision = 6;
                        increment = TemporalHelpers.NanosecondsPerMicrosecond;
                        break;
                    case "nanosecond":
                        precision = 9;
                        increment = 1L;
                        break;
                    default:
                        precision = -1;
                        increment = 1L;
                        break;
                }
            }
            else if (fractionalSecondDigits is not null)
            {
                var digits = fractionalSecondDigits.Value;
                precision = digits;
                increment = digits switch
                {
                    0 => TemporalHelpers.NanosecondsPerSecond,
                    1 => 100_000_000L,
                    2 => 10_000_000L,
                    3 => TemporalHelpers.NanosecondsPerMillisecond,
                    4 => 100_000L,
                    5 => 10_000L,
                    6 => TemporalHelpers.NanosecondsPerMicrosecond,
                    7 => 100L,
                    8 => 10L,
                    9 => 1L,
                    _ => 1L
                };
            }
        }

        var d = duration.DurationRecord;

        // If we need to round the time component
        if (increment > 1 || (precision >= 0 && precision < 9))
        {
            var origLargestUnit = TemporalHelpers.DefaultTemporalLargestUnit(d);
            var timeNs = TemporalHelpers.TimeDurationFromComponents(d);
            var roundedTimeNs = TemporalHelpers.RoundBigIntegerToIncrement(timeNs, increment, roundingMode);

            // Balance back to the original largest time unit
            // If the duration has calendar units or days, balance to "hour" and handle day overflow
            string balanceTarget;
            bool canOverflowToDays;
            if (TemporalHelpers.IsCalendarUnit(origLargestUnit) || string.Equals(origLargestUnit, "day", StringComparison.Ordinal))
            {
                balanceTarget = "hour";
                canOverflowToDays = true;
            }
            else
            {
                // Preserve original largest time unit (hour/minute/second/etc.)
                balanceTarget = origLargestUnit;
                canOverflowToDays = false;
            }

            var balanced = TemporalHelpers.BalanceTimeDuration(roundedTimeNs, balanceTarget);

            double newDays = d.Days;
            double newHours = balanced.Hours;

            if (canOverflowToDays && System.Math.Abs(newHours) >= 24)
            {
                var extraDays = System.Math.Truncate(newHours / 24);
                newDays += extraDays;
                newHours -= extraDays * 24;
            }

            d = new DurationRecord(d.Years, d.Months, d.Weeks, newDays,
                newHours, balanced.Minutes, balanced.Seconds,
                balanced.Milliseconds, balanced.Microseconds, balanced.Nanoseconds);

            if (!TemporalHelpers.IsValidDuration(d))
            {
                Throw.RangeError(_realm, "Rounded duration is out of range");
            }
        }

        return new JsString(TemporalHelpers.FormatDuration(d, precision));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.tojson
    /// </summary>
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        return new JsString(TemporalHelpers.FormatDuration(duration.DurationRecord));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Per spec: new Intl.DurationFormat(locales, options).format(this)
        var durationFormat = (Intl.JsDurationFormat) _realm.Intrinsics.DurationFormat.Construct([locales, options], Undefined);
        // Convert Temporal DurationRecord to Intl DurationRecord
        var dr = duration.DurationRecord;
        var intlRecord = new Intl.JsDurationFormat.DurationRecord
        {
            Years = dr.Years,
            Months = dr.Months,
            Weeks = dr.Weeks,
            Days = dr.Days,
            Hours = dr.Hours,
            Minutes = dr.Minutes,
            Seconds = dr.Seconds,
            Milliseconds = dr.Milliseconds,
            Microseconds = dr.Microseconds,
            Nanoseconds = dr.Nanoseconds,
        };
        return durationFormat.Format(intlRecord);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Cannot convert Temporal.Duration to a primitive value");
        return Undefined;
    }
}

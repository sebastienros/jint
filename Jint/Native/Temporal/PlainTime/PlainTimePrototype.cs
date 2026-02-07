using System.Globalization;
using System.Numerics;
using System.Text;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaintime-prototype-object
/// </summary>
internal sealed class PlainTimePrototype : Prototype
{
    private readonly PlainTimeConstructor _constructor;

    internal PlainTimePrototype(
        Engine engine,
        Realm realm,
        PlainTimeConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(18, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["with"] = new(new ClrFunction(Engine, "with", With, 1, LengthFlags), PropertyFlags),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["until"] = new(new ClrFunction(Engine, "until", Until, 1, LengthFlags), PropertyFlags),
            ["since"] = new(new ClrFunction(Engine, "since", Since, 1, LengthFlags), PropertyFlags),
            ["round"] = new(new ClrFunction(Engine, "round", Round, 1, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToString, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["hour"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get hour", GetHour, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["minute"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get minute", GetMinute, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["second"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get second", GetSecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["millisecond"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get millisecond", GetMillisecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["microsecond"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get microsecond", GetMicrosecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["nanosecond"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get nanosecond", GetNanosecond, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainTime", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsPlainTime ValidatePlainTime(JsValue thisObject)
    {
        if (thisObject is JsPlainTime plainTime)
            return plainTime;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainTime");
        return null!;
    }

    private JsNumber GetHour(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Hour);
    private JsNumber GetMinute(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Minute);
    private JsNumber GetSecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Second);
    private JsNumber GetMillisecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Millisecond);
    private JsNumber GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Microsecond);
    private JsNumber GetNanosecond(JsValue thisObject, JsCallArguments arguments) => JsNumber.Create(ValidatePlainTime(thisObject).IsoTime.Nanosecond);

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.with
    /// </summary>
    private JsPlainTime With(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var temporalTimeLike = arguments.At(0);
        var optionsArg = arguments.At(1);

        if (!temporalTimeLike.IsObject())
        {
            Throw.TypeError(_realm, "Temporal time-like must be an object");
        }

        var obj = temporalTimeLike.AsObject();

        // RejectObjectWithCalendarOrTimeZone
        RejectObjectWithCalendarOrTimeZone(obj);

        // Reject other Temporal types (Step 4 per spec)
        if (obj is JsPlainDate or JsPlainDateTime or JsPlainMonthDay or JsPlainTime or
            JsPlainYearMonth or JsZonedDateTime or JsInstant or JsDuration)
        {
            Throw.TypeError(_realm, "Temporal time-like object must be a plain property bag, not another Temporal type");
        }

        // Read and convert properties in alphabetical order with immediate conversion per spec
        // Order: hour, microsecond, millisecond, minute, nanosecond, second
        var hourValue = obj.Get("hour");
        var hour = hourValue.IsUndefined() ? plainTime.IsoTime.Hour : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, hourValue);

        var microsecondValue = obj.Get("microsecond");
        var microsecond = microsecondValue.IsUndefined() ? plainTime.IsoTime.Microsecond : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, microsecondValue);

        var millisecondValue = obj.Get("millisecond");
        var millisecond = millisecondValue.IsUndefined() ? plainTime.IsoTime.Millisecond : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, millisecondValue);

        var minuteValue = obj.Get("minute");
        var minute = minuteValue.IsUndefined() ? plainTime.IsoTime.Minute : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, minuteValue);

        var nanosecondValue = obj.Get("nanosecond");
        var nanosecond = nanosecondValue.IsUndefined() ? plainTime.IsoTime.Nanosecond : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, nanosecondValue);

        var secondValue = obj.Get("second");
        var second = secondValue.IsUndefined() ? plainTime.IsoTime.Second : TemporalHelpers.ToIntegerWithTruncationAsInt(_realm, secondValue);

        // At least one property must be present
        if (hourValue.IsUndefined() && microsecondValue.IsUndefined() && millisecondValue.IsUndefined() &&
            minuteValue.IsUndefined() && nanosecondValue.IsUndefined() && secondValue.IsUndefined())
        {
            Throw.TypeError(_realm, "Temporal time-like object must have at least one temporal property");
        }

        // Check for fundamentally invalid values (negative, would never be valid)
        // This ensures RangeError is thrown before TypeError for options
        if (hour < 0 || minute < 0 || second < 0 ||
            millisecond < 0 || microsecond < 0 || nanosecond < 0)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        // Get options via GetOptionsObject (after validating partial values)
        var options = GetOptionsObject(optionsArg);
        var overflow = TemporalHelpers.GetOverflowOption(_realm, (JsValue?) options ?? Undefined);

        var time = TemporalHelpers.RegulateIsoTime(hour, minute, second, millisecond, microsecond, nanosecond, overflow);
        if (time is null)
        {
            Throw.RangeError(_realm, "Invalid time");
        }

        return _constructor.Construct(time.Value);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.add
    /// </summary>
    private JsPlainTime Add(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var temporalDurationLike = arguments.At(0);
        var duration = ToTemporalDurationRecord(temporalDurationLike);
        return AddDurationToTime(plainTime.IsoTime, duration, 1);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.subtract
    /// </summary>
    private JsPlainTime Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var temporalDurationLike = arguments.At(0);
        var duration = ToTemporalDurationRecord(temporalDurationLike);
        return AddDurationToTime(plainTime.IsoTime, duration, -1);
    }

    private JsPlainTime AddDurationToTime(IsoTime time, DurationRecord duration, int sign)
    {
        // Convert current time to nanoseconds
        BigInteger totalNanoseconds = time.TotalNanoseconds();

        // Compute duration nanoseconds using BigInteger to avoid overflow
        var durationNanoseconds = sign * TemporalHelpers.TimeDurationFromComponents(duration);

        totalNanoseconds += durationNanoseconds;

        // Balance to get time within a day (wrap around)
        const long nsPerDay = TemporalHelpers.NanosecondsPerDay;
        totalNanoseconds = ((totalNanoseconds % nsPerDay) + nsPerDay) % nsPerDay;

        return _constructor.Construct(NanosecondsToTime((long) totalNanoseconds));
    }

    private static IsoTime NanosecondsToTime(long nanoseconds)
    {
        return TemporalHelpers.NanosecondsToTime(nanoseconds);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.until
    /// </summary>
    private JsDuration Until(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var other = _constructor.ToTemporalTime(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // Time units for PlainTime operations
        var timeUnits = new[] { "hour", "minute", "second", "millisecond", "microsecond", "nanosecond" };

        // GetDifferenceSettings reads options in correct order per spec
        var fallbackSmallestUnit = "nanosecond";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            "until",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            timeUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "hour")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "hour");
        }

        ValidateUnitRange(largestUnit, settings.SmallestUnit);
        ValidateRoundingIncrement(settings.SmallestUnit, settings.RoundingIncrement);

        return DifferenceTemporalPlainTime(plainTime, other, largestUnit, settings.SmallestUnit, settings.RoundingMode, settings.RoundingIncrement, negate: false);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.since
    /// </summary>
    private JsDuration Since(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var other = _constructor.ToTemporalTime(arguments.At(0), "constrain");
        var optionsArg = arguments.At(1);

        // Time units for PlainTime operations
        var timeUnits = new[] { "hour", "minute", "second", "millisecond", "microsecond", "nanosecond" };

        // GetDifferenceSettings reads options in correct order per spec
        // and negates rounding mode for "since" operation
        var fallbackSmallestUnit = "nanosecond";
        var fallbackLargestUnit = "auto"; // Will be resolved after reading smallestUnit

        var settings = TemporalHelpers.GetDifferenceSettings(
            _realm,
            optionsArg,
            "since",
            fallbackSmallestUnit,
            fallbackLargestUnit,
            timeUnits);

        // Resolve "auto" largestUnit to LargerOfTwoTemporalUnits(smallestUnit, "hour")
        var largestUnit = settings.LargestUnit;
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits(settings.SmallestUnit, "hour");
        }

        ValidateUnitRange(largestUnit, settings.SmallestUnit);
        ValidateRoundingIncrement(settings.SmallestUnit, settings.RoundingIncrement);

        return DifferenceTemporalPlainTime(plainTime, other, largestUnit, settings.SmallestUnit, settings.RoundingMode, settings.RoundingIncrement, negate: true);
    }

    /// <summary>
    /// DifferenceTemporalPlainTime ( operation, temporalTime, other, options )
    /// https://tc39.es/proposal-temporal/#sec-temporal-differencetemporalplaintime
    /// </summary>
    private JsDuration DifferenceTemporalPlainTime(JsPlainTime temporalTime, JsPlainTime other, string largestUnit, string smallestUnit, string roundingMode, int roundingIncrement, bool negate)
    {
        // Step 4: DifferenceTime(temporalTime.Time, other.Time)
        var ns1 = TimeToNanoseconds(temporalTime.IsoTime);
        var ns2 = TimeToNanoseconds(other.IsoTime);
        var timeDuration = ns2 - ns1;

        // Step 5: RoundTimeDuration
        timeDuration = RoundTimeDuration(timeDuration, smallestUnit, roundingMode, roundingIncrement);

        // Step 6: CombineDateAndTimeDuration(ZeroDateDuration(), timeDuration)
        var duration = TemporalHelpers.CombineDateAndTimeDuration(
            new DurationRecord(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            timeDuration);

        // Step 7: TemporalDurationFromInternal
        var result = TemporalHelpers.TemporalDurationFromInternal(
            _realm,
            0, 0, 0, 0, // date components are zero
            timeDuration,
            largestUnit);

        // Step 8: If operation is ~since~, negate the result
        // Use NoNegativeZero to ensure -0 becomes +0 (spec: CreateNegatedTemporalDuration)
        if (negate)
        {
            result = new DurationRecord(
                0, 0, 0, 0, // No date components for PlainTime
                TemporalHelpers.NoNegativeZero(-result.Hours),
                TemporalHelpers.NoNegativeZero(-result.Minutes),
                TemporalHelpers.NoNegativeZero(-result.Seconds),
                TemporalHelpers.NoNegativeZero(-result.Milliseconds),
                TemporalHelpers.NoNegativeZero(-result.Microseconds),
                TemporalHelpers.NoNegativeZero(-result.Nanoseconds));
        }

        // Step 9: Return result
        return _engine.Realm.Intrinsics.TemporalDuration.Construct(result);
    }

    private static long TimeToNanoseconds(IsoTime time)
    {
        return TemporalHelpers.TimeToNanoseconds(time);
    }

    private static long RoundTimeDuration(long nanoseconds, string smallestUnit, string roundingMode, int increment)
    {
        var unitNs = TemporalHelpers.GetUnitNanoseconds(smallestUnit);
        var incrementNs = unitNs * increment;

        if (incrementNs == 0)
            return nanoseconds;

        return TemporalHelpers.RoundNumberToIncrement(nanoseconds, incrementNs, roundingMode);
    }

    private static long RoundToIncrement(long nanoseconds, long incrementNs, string roundingMode)
    {
        if (incrementNs <= 1)
            return nanoseconds;

        return TemporalHelpers.RoundNumberToIncrement(nanoseconds, incrementNs, roundingMode);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.round
    /// </summary>
    private JsPlainTime Round(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var roundTo = arguments.At(0);

        if (roundTo.IsUndefined())
        {
            Throw.TypeError(_realm, "Options required");
        }

        string smallestUnit;
        string roundingMode;
        int roundingIncrement;

        if (roundTo.IsString())
        {
            smallestUnit = TemporalHelpers.ToSingularUnit(roundTo.ToString());
            if (!TemporalHelpers.IsValidTimeUnit(smallestUnit))
            {
                Throw.RangeError(_realm, $"Invalid unit for time rounding: {roundTo}");
            }

            roundingMode = "halfExpand";
            roundingIncrement = 1;
        }
        else
        {
            var options = GetOptionsObject(roundTo);
            if (options is null)
            {
                Throw.TypeError(_realm, "Options required");
                return null!;
            }

            // Read options in alphabetical order per spec (roundingIncrement, roundingMode, smallestUnit)
            roundingIncrement = TemporalHelpers.GetRoundingIncrementOption(_realm, options);
            roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, options, "halfExpand");
            smallestUnit = GetTimeUnitOption(options, "smallestUnit", null!);

            // Validation happens AFTER all options are read
            if (smallestUnit is null)
            {
                Throw.RangeError(_realm, "smallestUnit is required");
            }

            ValidateRoundingIncrement(smallestUnit, roundingIncrement);
        }

        var nanoseconds = TimeToNanoseconds(plainTime.IsoTime);
        var roundedNanoseconds = RoundTimeDuration(nanoseconds, smallestUnit, roundingMode, roundingIncrement);

        // Constrain to valid time range (0 to 24 hours)
        roundedNanoseconds = ((roundedNanoseconds % TemporalHelpers.NanosecondsPerDay) + TemporalHelpers.NanosecondsPerDay) % TemporalHelpers.NanosecondsPerDay;

        return _constructor.Construct(NanosecondsToTime(roundedNanoseconds));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.equals
    /// </summary>
    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var other = _constructor.ToTemporalTime(arguments.At(0), "constrain");
        var result = TemporalHelpers.CompareIsoTimes(plainTime.IsoTime, other.IsoTime) == 0;
        return result ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.tostring
    /// </summary>
    private JsString ToString(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        var options = GetOptionsObject(arguments.At(0));

        // Default precision: auto (show subsecond digits as needed)
        var precision = -1; // -1 means auto

        if (options is not null)
        {
            // Read properties in spec order: fractionalSecondDigits, roundingMode, smallestUnit
            // 1. Read fractionalSecondDigits first
            var fsdValue = options.Get("fractionalSecondDigits");
            if (!fsdValue.IsUndefined())
            {
                if (fsdValue.IsNumber())
                {
                    var fsdNum = fsdValue.AsNumber();
                    if (double.IsNaN(fsdNum) || double.IsInfinity(fsdNum))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be a finite number");
                    }

                    // Floor first, then validate range (per spec GetStringOrNumberOption)
                    var floored = (int) System.Math.Floor(fsdNum);
                    if (floored < 0 || floored > 9)
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be between 0 and 9");
                    }

                    precision = floored;
                }
                else
                {
                    var fsdStr = TypeConverter.ToString(fsdValue);
                    if (!string.Equals(fsdStr, "auto", StringComparison.Ordinal))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be 'auto' or a number 0-9");
                    }
                    // auto is the default (-1)
                }
            }

            // 2. Read roundingMode
            var roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, options, "trunc");

            // 3. Read smallestUnit (ALWAYS overrides fractionalSecondDigits when present)
            var smallestUnitValue = options.Get("smallestUnit");
            if (!smallestUnitValue.IsUndefined())
            {
                var str = TypeConverter.ToString(smallestUnitValue);
                var smallestUnit = TemporalHelpers.ToSingularUnit(str);
                if (!TemporalHelpers.IsValidTemporalUnit(smallestUnit))
                {
                    Throw.RangeError(_realm, $"Invalid unit: {str}");
                }

                // hour and calendar units are not valid for toString
                if (smallestUnit is "year" or "month" or "week" or "day" or "hour")
                {
                    Throw.RangeError(_realm, $"Invalid smallest unit for toString: {str}");
                }

                // smallestUnit ALWAYS takes precedence over fractionalSecondDigits
                precision = smallestUnit switch
                {
                    "minute" => -2, // -2 means minute precision (omit seconds)
                    "second" => 0,
                    "millisecond" => 3,
                    "microsecond" => 6,
                    "nanosecond" => 9,
                    _ => -1
                };
            }

            // If we have rounding to apply, round the time
            if (precision >= 0 || precision == -2)
            {
                var ns = TimeToNanoseconds(plainTime.IsoTime);
                // Calculate the rounding increment based on precision
                // precision -2 (minute): round to 60_000_000_000 ns
                // precision 0: round to 1_000_000_000 ns (1 second)
                // precision 1: round to 100_000_000 ns
                // precision 2: round to 10_000_000 ns
                // precision 3: round to 1_000_000 ns (1 millisecond)
                // etc.
                long incrementNs;
                if (precision == -2)
                {
                    incrementNs = TemporalHelpers.NanosecondsPerMinute;
                }
                else
                {
                    // For precision N, we round to 10^(9-N) nanoseconds
                    incrementNs = (long) System.Math.Pow(10, 9 - precision);
                }

                var rounded = RoundToIncrement(ns, incrementNs, roundingMode);
                // Wrap around day boundary
                rounded = ((rounded % TemporalHelpers.NanosecondsPerDay) + TemporalHelpers.NanosecondsPerDay) % TemporalHelpers.NanosecondsPerDay;
                plainTime = _constructor.Construct(NanosecondsToTime(rounded));
            }
        }

        return new JsString(FormatTime(plainTime.IsoTime, precision));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.tojson
    /// </summary>
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        return new JsString(FormatTime(plainTime.IsoTime, -1));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.tolocalestring
    /// </summary>
    private JsString ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var plainTime = ValidatePlainTime(thisObject);
        return new JsString(FormatTime(plainTime.IsoTime, -1));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.plaintime.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.PlainTime cannot be converted to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// Formats a time as ISO 8601 string with the specified precision.
    /// precision: -1 = auto (trim trailing zeros), -2 = minute (omit seconds), 0-9 = exact fractional digits
    /// </summary>
    private static string FormatTime(IsoTime time, int precision)
    {
        var sb = new ValueStringBuilder();
        sb.Append(time.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(time.Minute.ToString("D2", CultureInfo.InvariantCulture));

        if (precision == -2)
        {
            // Minute precision - omit seconds
            return sb.ToString();
        }

        sb.Append(':');
        sb.Append(time.Second.ToString("D2", CultureInfo.InvariantCulture));

        var subSecondNs = time.Millisecond * 1_000_000L + time.Microsecond * 1_000L + time.Nanosecond;

        if (precision == -1)
        {
            // Auto: show subsecond digits only if non-zero, trimming trailing zeros
            if (subSecondNs != 0)
            {
                sb.Append('.');
                var fraction = subSecondNs.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                sb.Append(fraction);
            }
        }
        else if (precision > 0)
        {
            // Show exactly N fractional digits
            sb.Append('.');
            var fraction = subSecondNs.ToString("D9", CultureInfo.InvariantCulture);
            sb.Append(fraction.AsSpan(0, precision));
        }
        // precision == 0: no fractional part

        return sb.ToString();
    }

    /// <summary>
    /// Use the central ToTemporalDuration to properly validate duration arguments.
    /// </summary>
    private DurationRecord ToTemporalDurationRecord(JsValue value)
    {
        var duration = _realm.Intrinsics.TemporalDuration.ToTemporalDuration(value);
        return duration.DurationRecord;
    }

    private void RejectObjectWithCalendarOrTimeZone(ObjectInstance obj)
    {
        var calendar = obj.Get("calendar");
        if (!calendar.IsUndefined())
        {
            Throw.TypeError(_realm, "Temporal time-like must not have a calendar property");
        }

        var timeZone = obj.Get("timeZone");
        if (!timeZone.IsUndefined())
        {
            Throw.TypeError(_realm, "Temporal time-like must not have a timeZone property");
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-getoptionsobject
    /// </summary>
    private ObjectInstance? GetOptionsObject(JsValue options)
    {
        if (options.IsUndefined())
            return null;
        if (options.IsObject())
            return options.AsObject();
        Throw.TypeError(_realm, "Options must be an object");
        return null!;
    }

    /// <summary>
    /// Gets a time unit option from the options object. Returns the singular unit name.
    /// Throws RangeError for invalid or non-time units.
    /// </summary>
    private string GetTimeUnitOption(ObjectInstance? options, string name, string? defaultValue)
    {
        if (options is null)
        {
            if (defaultValue is null)
            {
                Throw.RangeError(_realm, $"{name} is required");
            }

            return defaultValue!;
        }

        var value = options.Get(name);
        if (value.IsUndefined())
        {
            if (defaultValue is null)
            {
                Throw.RangeError(_realm, $"{name} is required");
            }

            return defaultValue!;
        }

        var str = TypeConverter.ToString(value);
        var unit = TemporalHelpers.ToSingularUnit(str);

        if (!TemporalHelpers.IsValidTemporalUnit(unit))
        {
            Throw.RangeError(_realm, $"Invalid unit: {str}");
        }

        if (!TemporalHelpers.IsValidTimeUnit(unit))
        {
            Throw.RangeError(_realm, $"Invalid time unit: {str}");
        }

        return unit;
    }


    /// <summary>
    /// Validates that largestUnit >= smallestUnit in the temporal unit hierarchy.
    /// </summary>
    private void ValidateUnitRange(string largestUnit, string smallestUnit)
    {
        var largestRank = GetTimeUnitRank(largestUnit);
        var smallestRank = GetTimeUnitRank(smallestUnit);
        if (largestRank > smallestRank)
        {
            Throw.RangeError(_realm, "largestUnit must be larger than or equal to smallestUnit");
        }
    }

    /// <summary>
    /// Validates that roundingIncrement divides evenly into the unit's maximum value.
    /// </summary>
    private void ValidateRoundingIncrement(string unit, int increment)
    {
        if (increment == 1)
            return;

        var maxValue = unit switch
        {
            "hour" => 24,
            "minute" => 60,
            "second" => 60,
            "millisecond" => 1000,
            "microsecond" => 1000,
            "nanosecond" => 1000,
            _ => 1
        };

        if (increment >= maxValue)
        {
            Throw.RangeError(_realm, $"roundingIncrement must be less than {maxValue} for {unit}");
        }

        if (maxValue % increment != 0)
        {
            Throw.RangeError(_realm, $"roundingIncrement must divide evenly into {maxValue} for {unit}");
        }
    }

    private static int GetTimeUnitRank(string unit)
    {
        return unit switch
        {
            "hour" => 0,
            "minute" => 1,
            "second" => 2,
            "millisecond" => 3,
            "microsecond" => 4,
            "nanosecond" => 5,
            _ => 6
        };
    }
}

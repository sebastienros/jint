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
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-instant-prototype-object
/// </summary>
internal sealed class InstantPrototype : Prototype
{
    private readonly InstantConstructor _constructor;

    internal InstantPrototype(
        Engine engine,
        Realm realm,
        InstantConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;

        var properties = new PropertyDictionary(14, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["add"] = new(new ClrFunction(Engine, "add", Add, 1, LengthFlags), PropertyFlags),
            ["subtract"] = new(new ClrFunction(Engine, "subtract", Subtract, 1, LengthFlags), PropertyFlags),
            ["until"] = new(new ClrFunction(Engine, "until", Until, 1, LengthFlags), PropertyFlags),
            ["since"] = new(new ClrFunction(Engine, "since", Since, 1, LengthFlags), PropertyFlags),
            ["round"] = new(new ClrFunction(Engine, "round", Round, 1, LengthFlags), PropertyFlags),
            ["equals"] = new(new ClrFunction(Engine, "equals", Equals, 1, LengthFlags), PropertyFlags),
            ["toString"] = new(new ClrFunction(Engine, "toString", ToStringMethod, 0, LengthFlags), PropertyFlags),
            ["toJSON"] = new(new ClrFunction(Engine, "toJSON", ToJSON, 0, LengthFlags), PropertyFlags),
            ["toLocaleString"] = new(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, LengthFlags), PropertyFlags),
            ["valueOf"] = new(new ClrFunction(Engine, "valueOf", ValueOf, 0, LengthFlags), PropertyFlags),
            ["toZonedDateTimeISO"] = new(new ClrFunction(Engine, "toZonedDateTimeISO", ToZonedDateTimeISO, 1, LengthFlags), PropertyFlags),
            ["epochMilliseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get epochMilliseconds", GetEpochMilliseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
            ["epochNanoseconds"] = new GetSetPropertyDescriptor(new ClrFunction(Engine, "get epochNanoseconds", GetEpochNanoseconds, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.Instant", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsInstant ValidateInstant(JsValue thisObject)
    {
        if (thisObject is JsInstant instant)
            return instant;
        Throw.TypeError(_realm, "Value is not a Temporal.Instant");
        return null!;
    }

    private JsNumber GetEpochMilliseconds(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        // Floor division for negative numbers (spec requires floor, not truncation)
        var ns = instant.EpochNanoseconds;
        var milliseconds = FloorDivide(ns, 1_000_000);
        return JsNumber.Create((double) milliseconds);
    }

    private static BigInteger FloorDivide(BigInteger dividend, long divisor)
    {
        var result = dividend / divisor;
        // Adjust for floor division: if remainder is non-zero and signs differ, subtract 1
        if (dividend % divisor != 0 && (dividend < 0) != (divisor < 0))
        {
            result -= 1;
        }
        return result;
    }

    private JsBigInt GetEpochNanoseconds(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        return JsBigInt.Create(instant.EpochNanoseconds);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.add
    /// </summary>
    private JsInstant Add(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var temporalDurationLike = arguments.At(0);

        var duration = _realm.Intrinsics.TemporalDuration.ToTemporalDuration(temporalDurationLike);
        var d = duration.DurationRecord;

        // Instant can only add time components
        if (d.Years != 0 || d.Months != 0 || d.Weeks != 0 || d.Days != 0)
        {
            Throw.RangeError(_realm, "Instant.add cannot use years, months, weeks, or days");
        }

        var ns = TemporalHelpers.TotalDurationNanoseconds(d);
        var result = instant.EpochNanoseconds + ns;

        if (!InstantConstructor.IsValidEpochNanoseconds(result))
        {
            Throw.RangeError(_realm, "Instant outside of valid range");
        }

        return _constructor.Construct(result);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.subtract
    /// </summary>
    private JsInstant Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var temporalDurationLike = arguments.At(0);

        var duration = _realm.Intrinsics.TemporalDuration.ToTemporalDuration(temporalDurationLike);
        var d = duration.DurationRecord;

        // Instant can only subtract time components
        if (d.Years != 0 || d.Months != 0 || d.Weeks != 0 || d.Days != 0)
        {
            Throw.RangeError(_realm, "Instant.subtract cannot use years, months, weeks, or days");
        }

        var ns = TemporalHelpers.TotalDurationNanoseconds(d);
        var result = instant.EpochNanoseconds - ns;

        if (!InstantConstructor.IsValidEpochNanoseconds(result))
        {
            Throw.RangeError(_realm, "Instant outside of valid range");
        }

        return _constructor.Construct(result);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.until
    /// </summary>
    private JsDuration Until(JsValue thisObject, JsCallArguments arguments)
    {
        return DifferenceTemporalInstant(thisObject, arguments, isSince: false);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.since
    /// </summary>
    private JsDuration Since(JsValue thisObject, JsCallArguments arguments)
    {
        return DifferenceTemporalInstant(thisObject, arguments, isSince: true);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-differencetemporalinstant
    /// </summary>
    private JsDuration DifferenceTemporalInstant(JsValue thisObject, JsCallArguments arguments, bool isSince)
    {
        var instant = ValidateInstant(thisObject);
        var other = _constructor.ToTemporalInstant(arguments.At(0));

        var options = arguments.At(1);

        // GetOptionsObject: undefined → use defaults, Object → use it, else → TypeError
        ObjectInstance? optionsObj = null;
        if (!options.IsUndefined())
        {
            if (options.IsObject())
            {
                optionsObj = options.AsObject();
            }
            else
            {
                Throw.TypeError(_realm, "Options must be an object");
                return null!;
            }
        }

        // Read options in alphabetical order (spec requirement)
        // largestUnit
        var largestUnit = "auto";
        if (optionsObj is not null)
        {
            var largestUnitValue = optionsObj.Get("largestUnit");
            if (!largestUnitValue.IsUndefined())
            {
                largestUnit = TemporalHelpers.ToSingularUnit(TypeConverter.ToString(largestUnitValue));
            }
        }

        // roundingIncrement
        var roundingIncrement = 1;
        if (optionsObj is not null)
        {
            var incValue = optionsObj.Get("roundingIncrement");
            if (!incValue.IsUndefined())
            {
                var inc = TypeConverter.ToNumber(incValue);
                if (double.IsNaN(inc) || double.IsInfinity(inc))
                {
                    Throw.RangeError(_realm, "roundingIncrement must be finite");
                }

                roundingIncrement = (int) System.Math.Truncate(inc);
                if (roundingIncrement < 1 || roundingIncrement > 1_000_000_000)
                {
                    Throw.RangeError(_realm, "roundingIncrement is out of range");
                }
            }
        }

        // roundingMode
        var roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, optionsObj ?? Undefined, "trunc");

        // smallestUnit
        var smallestUnit = "nanosecond";
        if (optionsObj is not null)
        {
            var unitValue = optionsObj.Get("smallestUnit");
            if (!unitValue.IsUndefined())
            {
                smallestUnit = TemporalHelpers.ToSingularUnit(TypeConverter.ToString(unitValue));
            }
        }

        // Validate smallestUnit is a TIME unit
        if (!TemporalHelpers.IsValidTimeUnit(smallestUnit))
        {
            Throw.RangeError(_realm, $"Invalid smallest unit: {smallestUnit}");
        }

        // Validate largestUnit is a TIME unit or "auto"
        if (!string.Equals(largestUnit, "auto", StringComparison.Ordinal) && !TemporalHelpers.IsValidTimeUnit(largestUnit))
        {
            Throw.RangeError(_realm, $"Invalid largest unit: {largestUnit}");
        }

        // Default largestUnit: LargerOfTwoTemporalUnits(fallbackLargestUnit="second", smallestUnit)
        if (string.Equals(largestUnit, "auto", StringComparison.Ordinal))
        {
            largestUnit = TemporalHelpers.LargerOfTwoTemporalUnits("second", smallestUnit);
        }

        // Validate largestUnit >= smallestUnit
        if (!string.Equals(TemporalHelpers.LargerOfTwoTemporalUnits(largestUnit, smallestUnit), largestUnit, StringComparison.Ordinal))
        {
            Throw.RangeError(_realm, "largestUnit must be greater than or equal to smallestUnit");
        }

        // ValidateTemporalRoundingIncrement (inclusive=false for duration)
        var maximum = MaximumTemporalDurationRoundingIncrement(smallestUnit);
        if (maximum > 0)
        {
            var maxAllowed = maximum - 1;
            if (roundingIncrement > maxAllowed || maximum % roundingIncrement != 0)
            {
                Throw.RangeError(_realm, "roundingIncrement is invalid for the smallest unit");
            }
        }

        // Compute diff: for "until" other - instant, for "since" instant - other
        // This is equivalent to the spec's approach of negating roundingMode and result for "since"
        var diff = isSince
            ? instant.EpochNanoseconds - other.EpochNanoseconds
            : other.EpochNanoseconds - instant.EpochNanoseconds;

        // Round
        var roundedNs = RoundTemporalDurationNanoseconds(diff, smallestUnit, roundingIncrement, roundingMode);

        var duration = BalanceTimeDuration(roundedNs, largestUnit);

        return _realm.Intrinsics.TemporalDuration.Construct(duration);
    }

    private static BigInteger RoundTemporalDurationNanoseconds(BigInteger totalNs, string smallestUnit, int increment, string roundingMode)
    {
        var unitNs = GetNanosecondsPerUnit(smallestUnit);
        var roundingIncrement = unitNs * increment;
        if (roundingIncrement <= 1)
        {
            return totalNs;
        }

        return RoundToIncrement(totalNs, roundingIncrement, roundingMode);
    }

    private static DurationRecord BalanceTimeDuration(BigInteger totalNs, string largestUnit)
    {
        return TemporalHelpers.BalanceTimeDuration(totalNs, largestUnit);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.round
    /// </summary>
    private JsInstant Round(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var roundTo = arguments.At(0);

        // Step 3: If roundTo is undefined, throw TypeError
        if (roundTo.IsUndefined())
        {
            Throw.TypeError(_realm, "Options argument is required");
        }

        ObjectInstance roundToObj;

        // Step 4: If roundTo is a string, treat as { smallestUnit: roundTo }
        if (roundTo.IsString())
        {
            roundToObj = new JsObject(_engine);
            roundToObj.Set("smallestUnit", roundTo);
        }
        // Step 5: Else, GetOptionsObject (must be an object)
        else if (roundTo.IsObject())
        {
            roundToObj = roundTo.AsObject();
        }
        else
        {
            Throw.TypeError(_realm, "Options must be an object or a string");
            return null!;
        }

        // Read all options in alphabetical order before algorithmic validation
        // Step: roundingIncrement
        var roundingIncrementValue = roundToObj.Get("roundingIncrement");
        int roundingIncrement = 1;
        if (!roundingIncrementValue.IsUndefined())
        {
            var inc = TypeConverter.ToNumber(roundingIncrementValue);
            if (double.IsNaN(inc) || double.IsInfinity(inc))
            {
                Throw.RangeError(_realm, "roundingIncrement must be finite");
            }

            roundingIncrement = (int) System.Math.Truncate(inc);
            if (roundingIncrement < 1 || roundingIncrement > 1_000_000_000)
            {
                Throw.RangeError(_realm, "roundingIncrement is out of range");
            }
        }

        // Step: roundingMode
        var roundingModeValue = roundToObj.Get("roundingMode");
        var roundingMode = "halfExpand";
        if (!roundingModeValue.IsUndefined())
        {
            roundingMode = TypeConverter.ToString(roundingModeValue);
            if (!TemporalHelpers.IsValidRoundingMode(roundingMode))
            {
                Throw.RangeError(_realm, $"Invalid rounding mode: {roundingMode}");
            }
        }

        // Step: smallestUnit
        var smallestUnitValue = roundToObj.Get("smallestUnit");
        if (smallestUnitValue.IsUndefined())
        {
            Throw.RangeError(_realm, "smallestUnit is required");
        }

        var smallestUnit = TemporalHelpers.ToSingularUnit(TypeConverter.ToString(smallestUnitValue));

        // Validate unit is a TIME unit (hour, minute, second, millisecond, microsecond, nanosecond)
        if (!TemporalHelpers.IsValidTimeUnit(smallestUnit))
        {
            Throw.RangeError(_realm, $"Invalid smallest unit for Instant.round: {smallestUnit}");
        }

        // Compute maximum (number of smallest units per day)
        long maximum = smallestUnit switch
        {
            "hour" => 24,
            "minute" => 1440,
            "second" => 86_400,
            "millisecond" => 86_400_000,
            "microsecond" => 86_400_000_000L,
            "nanosecond" => 86_400_000_000_000L,
            _ => 1
        };

        // ValidateTemporalRoundingIncrement
        if (roundingIncrement > maximum || maximum % roundingIncrement != 0)
        {
            Throw.RangeError(_realm, "roundingIncrement does not divide evenly into the maximum for the smallest unit");
        }

        // Do the rounding using RoundNumberToIncrementAsIfPositive
        var nsPerUnit = GetNanosecondsPerUnit(smallestUnit);
        var totalIncrement = nsPerUnit * roundingIncrement;
        var rounded = RoundToIncrementAsIfPositive(instant.EpochNanoseconds, totalIncrement, roundingMode);

        if (!InstantConstructor.IsValidEpochNanoseconds(rounded))
        {
            Throw.RangeError(_realm, "Instant outside of valid range");
        }

        return _constructor.Construct(rounded);
    }

    private static int MaximumTemporalDurationRoundingIncrement(string unit)
    {
        return unit switch
        {
            "hour" => 24,
            "minute" or "second" => 60,
            "millisecond" or "microsecond" or "nanosecond" => 1000,
            _ => 0 // UNSET for year/month/week/day
        };
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

    /// <summary>
    /// RoundNumberToIncrementAsIfPositive - uses floor division and treats value as positive for rounding mode.
    /// Used by Instant.round and toString per the Temporal spec.
    /// https://tc39.es/proposal-temporal/#sec-roundnumbertoincrementasifpositive
    /// </summary>
    private static BigInteger RoundToIncrementAsIfPositive(BigInteger value, long increment, string mode)
    {
        var bigIncrement = (BigInteger) increment;

        // Floor division: r1 = floor(value / increment) * increment
        var quotient = BigInteger.DivRem(value, bigIncrement, out var remainder);
        if (remainder < 0)
        {
            quotient -= 1;
            remainder += bigIncrement;
        }

        if (remainder == 0)
            return value;

        var r1 = quotient * bigIncrement;

        // Compare remainder against half of increment using multiplication to avoid truncation
        var doubleRemainder = 2 * remainder;
        var isAboveHalf = doubleRemainder > bigIncrement;
        var isHalfway = doubleRemainder == bigIncrement;

        // Unsigned rounding mode with isNegative=false (AsIfPositive)
        // INFINITY = toward r2 (upper), ZERO = toward r1 (lower)
        bool roundUp = mode switch
        {
            "ceil" => true,       // INFINITY
            "expand" => true,     // INFINITY
            "floor" => false,     // ZERO
            "trunc" => false,     // ZERO
            "halfCeil" => isAboveHalf || isHalfway,     // HALF-INFINITY
            "halfExpand" => isAboveHalf || isHalfway,   // HALF-INFINITY
            "halfFloor" => isAboveHalf,                 // HALF-ZERO
            "halfTrunc" => isAboveHalf,                 // HALF-ZERO
            "halfEven" => isAboveHalf || (isHalfway && quotient % 2 != 0), // HALF-EVEN
            _ => isAboveHalf || isHalfway
        };

        return roundUp ? r1 + bigIncrement : r1;
    }

    /// <summary>
    /// RoundNumberToIncrement - sign-aware rounding used by Until/Since.
    /// https://tc39.es/proposal-temporal/#sec-roundnumbertoincrement
    /// </summary>
    private static BigInteger RoundToIncrement(BigInteger value, long increment, string mode)
    {
        var bigIncrement = (BigInteger) increment;

        // Truncating division (toward zero)
        var quotient = BigInteger.DivRem(value, bigIncrement, out var remainder);

        if (remainder == 0)
            return value;

        var isNegative = value < 0;
        var absRemainder = BigInteger.Abs(remainder);

        // Compare using multiplication to avoid truncation issues
        var doubleAbsRemainder = 2 * absRemainder;
        var isAboveHalf = doubleAbsRemainder > bigIncrement;
        var isHalfway = doubleAbsRemainder == bigIncrement;

        // GetUnsignedRoundingMode with actual sign, then apply
        bool roundAwayFromZero = mode switch
        {
            "ceil" => !isNegative,
            "floor" => isNegative,
            "expand" => true,
            "trunc" => false,
            "halfCeil" => isAboveHalf || (isHalfway && !isNegative),
            "halfFloor" => isAboveHalf || (isHalfway && isNegative),
            "halfExpand" => isAboveHalf || isHalfway,
            "halfTrunc" => isAboveHalf,
            "halfEven" => isAboveHalf || (isHalfway && BigInteger.Abs(quotient) % 2 != 0),
            _ => isAboveHalf || isHalfway
        };

        if (roundAwayFromZero)
        {
            return isNegative ? (quotient - 1) * bigIncrement : (quotient + 1) * bigIncrement;
        }

        return quotient * bigIncrement;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.equals
    /// </summary>
    private JsBoolean Equals(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var other = _constructor.ToTemporalInstant(arguments.At(0));

        return instant.EpochNanoseconds == other.EpochNanoseconds ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.tostring
    /// </summary>
    private JsString ToStringMethod(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var options = arguments.At(0);

        // Step 2: GetOptionsObject
        ObjectInstance? optionsObj = null;
        if (!options.IsUndefined())
        {
            if (!options.IsObject())
            {
                Throw.TypeError(_realm, "Options must be an object");
                return null!;
            }
            optionsObj = options.AsObject();
        }

        // Read options in alphabetical order per spec

        // Step 4: GetTemporalFractionalSecondDigitsOption
        var fractionalSecondDigits = -1; // -1 = auto
        if (optionsObj is not null)
        {
            var digitsValue = optionsObj.Get("fractionalSecondDigits");
            if (!digitsValue.IsUndefined())
            {
                if (digitsValue is not JsNumber)
                {
                    // Not a Number type: ToString then check for "auto"
                    var str = TypeConverter.ToString(digitsValue);
                    if (!string.Equals(str, "auto", StringComparison.Ordinal))
                    {
                        Throw.RangeError(_realm, $"Invalid fractionalSecondDigits value: {str}");
                    }
                }
                else
                {
                    var num = ((JsNumber) digitsValue)._value;
                    if (double.IsNaN(num) || double.IsInfinity(num))
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be finite");
                    }
                    fractionalSecondDigits = (int) System.Math.Floor(num);
                    if (fractionalSecondDigits < 0 || fractionalSecondDigits > 9)
                    {
                        Throw.RangeError(_realm, "fractionalSecondDigits must be between 0 and 9");
                    }
                }
            }
        }

        // Step 5: GetRoundingModeOption
        var roundingMode = TemporalHelpers.GetRoundingModeOption(_realm, optionsObj ?? Undefined, "trunc");

        // Step 6: Read smallestUnit (ToString + singular normalization only, defer validation)
        string? smallestUnit = null;
        if (optionsObj is not null)
        {
            var unitValue = optionsObj.Get("smallestUnit");
            if (!unitValue.IsUndefined())
            {
                smallestUnit = TemporalHelpers.ToSingularUnit(TypeConverter.ToString(unitValue));
            }
        }

        // Step 8: Read timeZone (before algorithmic validation per spec)
        var timeZone = "UTC";
        var timeZoneExplicitlySet = false;
        JsValue? timeZoneValue = null;
        if (optionsObj is not null)
        {
            timeZoneValue = optionsObj.Get("timeZone");
        }

        // Now perform algorithmic validation
        // Step 7: Validate smallestUnit
        if (smallestUnit is not null)
        {
            if (!TemporalHelpers.IsValidTimeUnit(smallestUnit))
            {
                Throw.RangeError(_realm, $"Invalid smallest unit: {smallestUnit}");
            }
            if (string.Equals(smallestUnit, "hour", StringComparison.Ordinal))
            {
                Throw.RangeError(_realm, "hour is not a valid smallest unit for Instant.toString");
            }
        }

        // Step 9: Validate and parse timeZone
        if (timeZoneValue is not null && !timeZoneValue.IsUndefined())
        {
            timeZone = TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, timeZoneValue);
            timeZoneExplicitlySet = true;
        }

        // Step 10: ToSecondsStringPrecisionRecord
        int precision;
        long roundingIncrement;
        if (smallestUnit is not null)
        {
            switch (smallestUnit)
            {
                case "minute":
                    precision = -2;
                    roundingIncrement = 60_000_000_000L;
                    break;
                case "second":
                    precision = 0;
                    roundingIncrement = 1_000_000_000L;
                    break;
                case "millisecond":
                    precision = 3;
                    roundingIncrement = 1_000_000L;
                    break;
                case "microsecond":
                    precision = 6;
                    roundingIncrement = 1_000L;
                    break;
                case "nanosecond":
                    precision = 9;
                    roundingIncrement = 1L;
                    break;
                default:
                    precision = -1;
                    roundingIncrement = 1L;
                    break;
            }
        }
        else if (fractionalSecondDigits >= 0)
        {
            precision = fractionalSecondDigits;
            switch (fractionalSecondDigits)
            {
                case 0: roundingIncrement = 1_000_000_000L; break;
                case 1: roundingIncrement = 100_000_000L; break;
                case 2: roundingIncrement = 10_000_000L; break;
                case 3: roundingIncrement = 1_000_000L; break;
                case 4: roundingIncrement = 100_000L; break;
                case 5: roundingIncrement = 10_000L; break;
                case 6: roundingIncrement = 1_000L; break;
                case 7: roundingIncrement = 100L; break;
                case 8: roundingIncrement = 10L; break;
                case 9: roundingIncrement = 1L; break;
                default: roundingIncrement = 1L; break;
            }
        }
        else
        {
            precision = -1; // auto
            roundingIncrement = 1L;
        }

        // Step 11: RoundTemporalInstant
        var epochNs = instant.EpochNanoseconds;
        if (roundingIncrement > 1)
        {
            epochNs = RoundToIncrementAsIfPositive(epochNs, roundingIncrement, roundingMode);
        }

        return new JsString(FormatInstant(epochNs, timeZone, precision, timeZoneExplicitlySet));
    }

    private string FormatInstant(BigInteger epochNs, string timeZone, int fractionalSecondDigits, bool timeZoneExplicitlySet = false)
    {
        // Get offset for the time zone
        var provider = _engine.Options.Temporal.TimeZoneProvider;
        var offsetNs = provider.GetOffsetNanosecondsFor(timeZone, epochNs);
        var localNs = epochNs + offsetNs;

        // Convert to date-time components
        var dateTime = TemporalHelpers.EpochNanosecondsToIsoDateTime(localNs);

        // Format the date-time
        var sb = new ValueStringBuilder();
        FormatIsoYear(ref sb, dateTime.Year);
        sb.Append('-');
        sb.Append(dateTime.Month.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('-');
        sb.Append(dateTime.Day.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append('T');
        sb.Append(dateTime.Hour.ToString("D2", CultureInfo.InvariantCulture));
        sb.Append(':');
        sb.Append(dateTime.Minute.ToString("D2", CultureInfo.InvariantCulture));

        // For minute precision, don't include seconds
        if (fractionalSecondDigits != -2)
        {
            sb.Append(':');
            sb.Append(dateTime.Second.ToString("D2", CultureInfo.InvariantCulture));

            // Format fractional seconds
            var subSecondNs = (long) dateTime.Millisecond * 1_000_000 +
                              (long) dateTime.Microsecond * 1_000 +
                              dateTime.Nanosecond;

            if (fractionalSecondDigits == -1)
            {
                // Auto: show minimum needed
                if (subSecondNs != 0)
                {
                    var fraction = subSecondNs.ToString("D9", CultureInfo.InvariantCulture).TrimEnd('0');
                    sb.Append('.');
                    sb.Append(fraction);
                }
            }
            else if (fractionalSecondDigits > 0)
            {
                var fraction = subSecondNs.ToString("D9", CultureInfo.InvariantCulture);
                sb.Append('.');
                sb.Append(fraction.AsSpan(0, fractionalSecondDigits));
            }
        }

        // Format offset
        // Use Z only when timeZone was not explicitly set and offset is 0
        if (!timeZoneExplicitlySet && offsetNs == 0)
        {
            sb.Append('Z');
        }
        else
        {
            FormatOffset(ref sb, offsetNs);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats an ISO year. Negative years and years > 9999 use 6-digit expanded format with sign.
    /// </summary>
    private static void FormatIsoYear(ref ValueStringBuilder sb, int year)
    {
        if (year < 0 || year > 9999)
        {
            sb.Append(year < 0 ? '-' : '+');
            sb.Append(System.Math.Abs(year).ToString("D6", CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append(year.ToString("D4", CultureInfo.InvariantCulture));
        }
    }

    private static void FormatOffset(ref ValueStringBuilder sb, long offsetNs)
    {
        // Use FormatDateTimeUTCOffsetRounded: round to nearest minute, format as ±HH:MM
        sb.Append(TemporalHelpers.FormatOffsetRounded(offsetNs));
    }


    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.tojson
    /// </summary>
    private JsString ToJSON(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        return new JsString(FormatInstant(instant.EpochNanoseconds, "UTC", -1));
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sup-temporal.instant.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var locales = arguments.At(0);
        var options = arguments.At(1);

        // Per spec: CreateDateTimeFormat with required=~any~, defaults=~all~
        var dtf = _realm.Intrinsics.DateTimeFormat.CreateDateTimeFormat(
            locales, options, required: Intl.DateTimeRequired.Any, defaults: Intl.DateTimeDefaults.All);

        // Convert epoch nanoseconds to UTC DateTime (DTF will convert to its timezone)
        const long nsPerTick = 100;
        var ticks = (long) (instant.EpochNanoseconds / nsPerTick);
        var unixEpochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var totalTicks = unixEpochTicks + ticks;
        var dateTime = new DateTime(totalTicks, DateTimeKind.Utc);

        return dtf.Format(dateTime);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.valueof
    /// </summary>
    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Cannot convert Temporal.Instant to a primitive value");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.instant.prototype.tozoneddatetimeiso
    /// </summary>
    private JsZonedDateTime ToZonedDateTimeISO(JsValue thisObject, JsCallArguments arguments)
    {
        var instant = ValidateInstant(thisObject);
        var timeZoneId = TemporalHelpers.ToTemporalTimeZoneIdentifier(_engine, _realm, arguments.At(0));

        return new JsZonedDateTime(_engine, _realm.Intrinsics.TemporalZonedDateTime.PrototypeObject,
            instant.EpochNanoseconds, timeZoneId, "iso8601");
    }
}

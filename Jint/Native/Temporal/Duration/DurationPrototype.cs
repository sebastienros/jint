using System.Globalization;
using System.Text;
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

        var properties = new PropertyDictionary(14, checkExistingKeys: false)
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
        };
        SetProperties(properties);

        // Getter properties
        DefineAccessor("years", GetYears);
        DefineAccessor("months", GetMonths);
        DefineAccessor("weeks", GetWeeks);
        DefineAccessor("days", GetDays);
        DefineAccessor("hours", GetHours);
        DefineAccessor("minutes", GetMinutes);
        DefineAccessor("seconds", GetSeconds);
        DefineAccessor("milliseconds", GetMilliseconds);
        DefineAccessor("microseconds", GetMicroseconds);
        DefineAccessor("nanoseconds", GetNanoseconds);
        DefineAccessor("sign", GetSign);
        DefineAccessor("blank", GetBlank);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.Duration", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void DefineAccessor(string name, Func<JsValue, JsCallArguments, JsValue> getter)
    {
        SetProperty(name, new GetSetPropertyDescriptor(
            new ClrFunction(Engine, $"get {name}", getter, 0, PropertyFlag.Configurable),
            Undefined,
            PropertyFlag.Configurable));
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

        var years = GetOptionalDurationProperty(obj, "years", record.Years);
        var months = GetOptionalDurationProperty(obj, "months", record.Months);
        var weeks = GetOptionalDurationProperty(obj, "weeks", record.Weeks);
        var days = GetOptionalDurationProperty(obj, "days", record.Days);
        var hours = GetOptionalDurationProperty(obj, "hours", record.Hours);
        var minutes = GetOptionalDurationProperty(obj, "minutes", record.Minutes);
        var seconds = GetOptionalDurationProperty(obj, "seconds", record.Seconds);
        var milliseconds = GetOptionalDurationProperty(obj, "milliseconds", record.Milliseconds);
        var microseconds = GetOptionalDurationProperty(obj, "microseconds", record.Microseconds);
        var nanoseconds = GetOptionalDurationProperty(obj, "nanoseconds", record.Nanoseconds);

        var newRecord = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);

        if (!TemporalHelpers.IsValidDuration(newRecord))
        {
            Throw.RangeError(_realm, "Invalid duration");
        }

        return _constructor.Construct(newRecord);
    }

    private double GetOptionalDurationProperty(ObjectInstance obj, string name, double defaultValue)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return defaultValue;

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
    /// </summary>
    private JsDuration Add(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var other = _constructor.ToTemporalDuration(arguments.At(0));

        var d1 = duration.DurationRecord;
        var d2 = other.DurationRecord;

        // Check for calendar units that require relativeTo
        if (d1.Years != 0 || d1.Months != 0 || d1.Weeks != 0 ||
            d2.Years != 0 || d2.Months != 0 || d2.Weeks != 0)
        {
            var options = arguments.At(1);
            if (options.IsUndefined() || options.IsNull() || !options.IsObject() || !options.AsObject().HasProperty("relativeTo"))
            {
                Throw.RangeError(_realm, "A relativeTo option is required for adding durations with calendar units");
            }
            // TODO: Implement relativeTo addition
            Throw.TypeError(_realm, "Duration addition with calendar units is not yet fully implemented");
        }

        var result = new DurationRecord(
            d1.Years + d2.Years,
            d1.Months + d2.Months,
            d1.Weeks + d2.Weeks,
            d1.Days + d2.Days,
            d1.Hours + d2.Hours,
            d1.Minutes + d2.Minutes,
            d1.Seconds + d2.Seconds,
            d1.Milliseconds + d2.Milliseconds,
            d1.Microseconds + d2.Microseconds,
            d1.Nanoseconds + d2.Nanoseconds);

        if (!TemporalHelpers.IsValidDuration(result))
        {
            Throw.RangeError(_realm, "Invalid duration result");
        }

        return _constructor.Construct(result);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.subtract
    /// </summary>
    private JsDuration Subtract(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var other = _constructor.ToTemporalDuration(arguments.At(0));

        var d1 = duration.DurationRecord;
        var d2 = other.DurationRecord;

        // Check for calendar units that require relativeTo
        if (d1.Years != 0 || d1.Months != 0 || d1.Weeks != 0 ||
            d2.Years != 0 || d2.Months != 0 || d2.Weeks != 0)
        {
            var options = arguments.At(1);
            if (options.IsUndefined() || options.IsNull() || !options.IsObject() || !options.AsObject().HasProperty("relativeTo"))
            {
                Throw.RangeError(_realm, "A relativeTo option is required for subtracting durations with calendar units");
            }
            // TODO: Implement relativeTo subtraction
            Throw.TypeError(_realm, "Duration subtraction with calendar units is not yet fully implemented");
        }

        var result = new DurationRecord(
            d1.Years - d2.Years,
            d1.Months - d2.Months,
            d1.Weeks - d2.Weeks,
            d1.Days - d2.Days,
            d1.Hours - d2.Hours,
            d1.Minutes - d2.Minutes,
            d1.Seconds - d2.Seconds,
            d1.Milliseconds - d2.Milliseconds,
            d1.Microseconds - d2.Microseconds,
            d1.Nanoseconds - d2.Nanoseconds);

        if (!TemporalHelpers.IsValidDuration(result))
        {
            Throw.RangeError(_realm, "Invalid duration result");
        }

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

        if (options.IsString())
        {
            smallestUnit = options.ToString();
        }
        else if (options.IsObject())
        {
            var obj = options.AsObject();

            var smallestUnitValue = obj.Get("smallestUnit");
            if (!smallestUnitValue.IsUndefined())
            {
                smallestUnit = TemporalHelpers.ToSingularUnit(smallestUnitValue.ToString());
            }

            var largestUnitValue = obj.Get("largestUnit");
            if (!largestUnitValue.IsUndefined())
            {
                largestUnit = TemporalHelpers.ToSingularUnit(largestUnitValue.ToString());
            }

            var roundingModeValue = obj.Get("roundingMode");
            if (!roundingModeValue.IsUndefined())
            {
                roundingMode = roundingModeValue.ToString();
            }

            var roundingIncrementValue = obj.Get("roundingIncrement");
            if (!roundingIncrementValue.IsUndefined())
            {
                roundingIncrement = TypeConverter.ToNumber(roundingIncrementValue);
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

        // Check for calendar units that require relativeTo
        var d = duration.DurationRecord;
        if (d.Years != 0 || d.Months != 0 || d.Weeks != 0)
        {
            Throw.RangeError(_realm, "A relativeTo option is required for rounding durations with calendar units");
        }

        // For now, implement basic rounding for time-only durations
        var totalNs = TemporalHelpers.TotalDurationNanoseconds(d);

        smallestUnit ??= "nanosecond";
        largestUnit ??= GetDefaultLargestUnit(d);

        var nsPerUnit = GetNanosecondsPerUnit(smallestUnit);
        if (nsPerUnit == 0)
        {
            Throw.RangeError(_realm, $"Invalid smallest unit: {smallestUnit}");
        }

        // Round to the smallest unit
        var roundedNs = RoundToIncrement((double) totalNs, nsPerUnit * roundingIncrement, roundingMode);
        var result = BalanceDuration((System.Numerics.BigInteger) roundedNs, largestUnit);

        return _constructor.Construct(result);
    }

    private static string GetDefaultLargestUnit(DurationRecord d)
    {
        if (d.Years != 0) return "year";
        if (d.Months != 0) return "month";
        if (d.Weeks != 0) return "week";
        if (d.Days != 0) return "day";
        if (d.Hours != 0) return "hour";
        if (d.Minutes != 0) return "minute";
        if (d.Seconds != 0) return "second";
        if (d.Milliseconds != 0) return "millisecond";
        if (d.Microseconds != 0) return "microsecond";
        return "nanosecond";
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

    private static double RoundToIncrement(double value, double increment, string mode)
    {
        var quotient = value / increment;

        var rounded = mode switch
        {
            "ceil" => System.Math.Ceiling(quotient),
            "floor" => System.Math.Floor(quotient),
            "trunc" => System.Math.Truncate(quotient),
            "expand" => quotient >= 0 ? System.Math.Ceiling(quotient) : System.Math.Floor(quotient),
            "halfCeil" => System.Math.Floor(quotient + 0.5),
            "halfFloor" => System.Math.Ceiling(quotient - 0.5),
            "halfTrunc" => quotient >= 0 ? System.Math.Ceiling(quotient - 0.5) : System.Math.Floor(quotient + 0.5),
            "halfExpand" => System.Math.Round(quotient, MidpointRounding.AwayFromZero),
            "halfEven" => System.Math.Round(quotient, MidpointRounding.ToEven),
            _ => System.Math.Round(quotient, MidpointRounding.AwayFromZero)
        };

        return rounded * increment;
    }

    private static DurationRecord BalanceDuration(System.Numerics.BigInteger totalNs, string largestUnit)
    {
        const long nsPerDay = 86_400_000_000_000L;
        const long nsPerHour = 3_600_000_000_000L;
        const long nsPerMinute = 60_000_000_000L;
        const long nsPerSecond = 1_000_000_000L;
        const long nsPerMs = 1_000_000L;
        const long nsPerUs = 1_000L;

        double days = 0, hours = 0, minutes = 0, seconds = 0, milliseconds = 0, microseconds = 0, nanoseconds = 0;
        var remaining = totalNs;

        if (largestUnit is "day")
        {
            days = (double) (remaining / nsPerDay);
            remaining %= nsPerDay;
        }

        if (largestUnit is "day" or "hour")
        {
            hours = (double) (remaining / nsPerHour);
            remaining %= nsPerHour;
        }

        if (largestUnit is "day" or "hour" or "minute")
        {
            minutes = (double) (remaining / nsPerMinute);
            remaining %= nsPerMinute;
        }

        if (largestUnit is "day" or "hour" or "minute" or "second")
        {
            seconds = (double) (remaining / nsPerSecond);
            remaining %= nsPerSecond;
        }

        if (largestUnit is "day" or "hour" or "minute" or "second" or "millisecond")
        {
            milliseconds = (double) (remaining / nsPerMs);
            remaining %= nsPerMs;
        }

        if (largestUnit is "day" or "hour" or "minute" or "second" or "millisecond" or "microsecond")
        {
            microseconds = (double) (remaining / nsPerUs);
            remaining %= nsPerUs;
        }

        nanoseconds = (double) remaining;

        return new DurationRecord(0, 0, 0, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.total
    /// </summary>
    private JsNumber Total(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        var options = arguments.At(0);

        string? unit = null;

        if (options.IsString())
        {
            unit = options.ToString();
        }
        else if (options.IsObject())
        {
            var unitValue = options.AsObject().Get("unit");
            if (!unitValue.IsUndefined())
            {
                unit = unitValue.ToString();
            }
        }
        else if (!options.IsUndefined())
        {
            Throw.TypeError(_realm, "Options must be a string or object");
        }

        if (unit is null)
        {
            Throw.RangeError(_realm, "Unit option is required");
        }

        unit = TemporalHelpers.ToSingularUnit(unit);

        var d = duration.DurationRecord;

        // Check for calendar units that require relativeTo
        if (d.Years != 0 || d.Months != 0 || d.Weeks != 0)
        {
            if (unit is "year" or "month" or "week")
            {
                Throw.RangeError(_realm, "A relativeTo option is required for total with calendar units");
            }
        }

        var totalNs = TemporalHelpers.TotalDurationNanoseconds(d);
        var nsPerUnit = GetNanosecondsPerUnit(unit);

        if (nsPerUnit == 0)
        {
            // Calendar units
            Throw.RangeError(_realm, $"Cannot compute total for unit '{unit}' without relativeTo");
        }

        return JsNumber.Create((double) totalNs / nsPerUnit);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.prototype.tostring
    /// </summary>
    private JsString ToStringMethod(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        return new JsString(TemporalHelpers.FormatDuration(duration.DurationRecord));
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
    private JsString ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        var duration = ValidateDuration(thisObject);
        // For now, just return the ISO string representation
        // Full Intl.DurationFormat integration would require more work
        return new JsString(TemporalHelpers.FormatDuration(duration.DurationRecord));
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

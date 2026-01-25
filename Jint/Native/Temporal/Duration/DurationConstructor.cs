using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-temporal.duration
/// </summary>
internal sealed class DurationConstructor : Constructor
{
    private static readonly JsString _functionName = new("Duration");

    internal DurationConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype) : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new DurationPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public DurationPrototype PrototypeObject { get; }

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
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.from
    /// </summary>
    private JsDuration From(JsValue thisObject, JsCallArguments arguments)
    {
        var item = arguments.At(0);
        return ToTemporalDuration(item);
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration.compare
    /// </summary>
    private JsNumber Compare(JsValue thisObject, JsCallArguments arguments)
    {
        var one = ToTemporalDuration(arguments.At(0));
        var two = ToTemporalDuration(arguments.At(1));

        // For durations without calendar units, we can compare directly
        // by converting to total nanoseconds
        var d1 = one.DurationRecord;
        var d2 = two.DurationRecord;

        // Check if either duration has calendar units (years, months, weeks)
        if (d1.Years != 0 || d1.Months != 0 || d1.Weeks != 0 ||
            d2.Years != 0 || d2.Months != 0 || d2.Weeks != 0)
        {
            // Comparison with calendar units requires a relativeTo argument
            var options = arguments.At(2);
            if (options.IsUndefined() || options.IsNull())
            {
                Throw.RangeError(_realm, "A relativeTo option is required for comparing durations with calendar units");
            }
            // TODO: Implement relativeTo comparison
            Throw.TypeError(_realm, "Duration comparison with calendar units is not yet fully implemented");
        }

        // Compare time-only durations
        var ns1 = TemporalHelpers.TotalDurationNanoseconds(d1);
        var ns2 = TemporalHelpers.TotalDurationNanoseconds(d2);

        return JsNumber.Create(ns1.CompareTo(ns2));
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Temporal.Duration cannot be called as a function");
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-temporal/#sec-temporal.duration
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var years = ToIntegerIfIntegral(arguments.At(0), "years");
        var months = ToIntegerIfIntegral(arguments.At(1), "months");
        var weeks = ToIntegerIfIntegral(arguments.At(2), "weeks");
        var days = ToIntegerIfIntegral(arguments.At(3), "days");
        var hours = ToIntegerIfIntegral(arguments.At(4), "hours");
        var minutes = ToIntegerIfIntegral(arguments.At(5), "minutes");
        var seconds = ToIntegerIfIntegral(arguments.At(6), "seconds");
        var milliseconds = ToIntegerIfIntegral(arguments.At(7), "milliseconds");
        var microseconds = ToIntegerIfIntegral(arguments.At(8), "microseconds");
        var nanoseconds = ToIntegerIfIntegral(arguments.At(9), "nanoseconds");

        var record = new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);

        if (!TemporalHelpers.IsValidDuration(record))
        {
            Throw.RangeError(_realm, "Invalid duration");
        }

        return Construct(record);
    }

    private double ToIntegerIfIntegral(JsValue value, string name)
    {
        if (value.IsUndefined())
            return 0;

        var number = TypeConverter.ToNumber(value);

        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, $"Duration {name} must be a finite number");
        }

        if (double.IsInfinity(number))
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
    /// https://tc39.es/proposal-temporal/#sec-temporal-totemporalduration
    /// </summary>
    internal JsDuration ToTemporalDuration(JsValue item)
    {
        if (item is JsDuration duration)
        {
            return duration;
        }

        if (item.IsString())
        {
            var parsed = TemporalHelpers.ParseDuration(item.ToString());
            if (parsed is null)
            {
                Throw.RangeError(_realm, "Invalid duration string");
            }
            return Construct(parsed.Value);
        }

        if (item.IsObject())
        {
            var obj = item.AsObject();
            var record = ToDurationRecord(obj);
            if (!TemporalHelpers.IsValidDuration(record))
            {
                Throw.RangeError(_realm, "Invalid duration");
            }
            return Construct(record);
        }

        Throw.TypeError(_realm, "Invalid duration");
        return null!;
    }

    private DurationRecord ToDurationRecord(ObjectInstance obj)
    {
        var years = GetDurationProperty(obj, "years");
        var months = GetDurationProperty(obj, "months");
        var weeks = GetDurationProperty(obj, "weeks");
        var days = GetDurationProperty(obj, "days");
        var hours = GetDurationProperty(obj, "hours");
        var minutes = GetDurationProperty(obj, "minutes");
        var seconds = GetDurationProperty(obj, "seconds");
        var milliseconds = GetDurationProperty(obj, "milliseconds");
        var microseconds = GetDurationProperty(obj, "microseconds");
        var nanoseconds = GetDurationProperty(obj, "nanoseconds");

        // At least one property must be defined
        if (years == 0 && months == 0 && weeks == 0 && days == 0 &&
            hours == 0 && minutes == 0 && seconds == 0 &&
            milliseconds == 0 && microseconds == 0 && nanoseconds == 0)
        {
            // Check if any property was actually present
            var hasAny = obj.HasProperty("years") || obj.HasProperty("months") ||
                         obj.HasProperty("weeks") || obj.HasProperty("days") ||
                         obj.HasProperty("hours") || obj.HasProperty("minutes") ||
                         obj.HasProperty("seconds") || obj.HasProperty("milliseconds") ||
                         obj.HasProperty("microseconds") || obj.HasProperty("nanoseconds");
            if (!hasAny)
            {
                Throw.TypeError(_realm, "Duration object must have at least one temporal property");
            }
        }

        return new DurationRecord(years, months, weeks, days, hours, minutes, seconds, milliseconds, microseconds, nanoseconds);
    }

    private double GetDurationProperty(ObjectInstance obj, string name)
    {
        var value = obj.Get(name);
        if (value.IsUndefined())
            return 0;

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

    internal JsDuration Construct(DurationRecord duration)
    {
        return new JsDuration(_engine, PrototypeObject, duration);
    }
}

using System.Numerics;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/proposal-intl-duration-format/
/// </summary>
internal sealed class DurationFormatPrototype : Prototype
{
    private readonly DurationFormatConstructor _constructor;

    public DurationFormatPrototype(
        Engine engine,
        Realm realm,
        DurationFormatConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["format"] = new PropertyDescriptor(new ClrFunction(Engine, "format", Format, 1, LengthFlags), PropertyFlags),
            ["formatToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatToParts", FormatToParts, 1, LengthFlags), PropertyFlags),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.DurationFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsDurationFormat ValidateDurationFormat(JsValue thisObject)
    {
        if (thisObject is JsDurationFormat durationFormat)
        {
            return durationFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.DurationFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat.prototype.format
    /// </summary>
    private JsValue Format(JsValue thisObject, JsCallArguments arguments)
    {
        var durationFormat = ValidateDurationFormat(thisObject);
        var duration = arguments.At(0);

        var durationRecord = ToDurationRecord(duration);
        return durationFormat.Format(durationRecord);
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat.prototype.formattoparts
    /// </summary>
    private JsArray FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var durationFormat = ValidateDurationFormat(thisObject);
        var duration = arguments.At(0);

        var durationRecord = ToDurationRecord(duration);
        return durationFormat.FormatToParts(_engine, durationRecord);
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var durationFormat = ValidateDurationFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.CreateDataPropertyOrThrow("locale", durationFormat.Locale);
        result.CreateDataPropertyOrThrow("numberingSystem", durationFormat.NumberingSystem);
        result.CreateDataPropertyOrThrow("style", durationFormat.Style);

        // Unit styles
        result.CreateDataPropertyOrThrow("years", durationFormat.YearsStyle);
        result.CreateDataPropertyOrThrow("yearsDisplay", durationFormat.YearsDisplay);
        result.CreateDataPropertyOrThrow("months", durationFormat.MonthsStyle);
        result.CreateDataPropertyOrThrow("monthsDisplay", durationFormat.MonthsDisplay);
        result.CreateDataPropertyOrThrow("weeks", durationFormat.WeeksStyle);
        result.CreateDataPropertyOrThrow("weeksDisplay", durationFormat.WeeksDisplay);
        result.CreateDataPropertyOrThrow("days", durationFormat.DaysStyle);
        result.CreateDataPropertyOrThrow("daysDisplay", durationFormat.DaysDisplay);
        result.CreateDataPropertyOrThrow("hours", durationFormat.HoursStyle);
        result.CreateDataPropertyOrThrow("hoursDisplay", durationFormat.HoursDisplay);
        result.CreateDataPropertyOrThrow("minutes", durationFormat.MinutesStyle);
        result.CreateDataPropertyOrThrow("minutesDisplay", durationFormat.MinutesDisplay);
        result.CreateDataPropertyOrThrow("seconds", durationFormat.SecondsStyle);
        result.CreateDataPropertyOrThrow("secondsDisplay", durationFormat.SecondsDisplay);
        result.CreateDataPropertyOrThrow("milliseconds", durationFormat.MillisecondsStyle);
        result.CreateDataPropertyOrThrow("millisecondsDisplay", durationFormat.MillisecondsDisplay);
        result.CreateDataPropertyOrThrow("microseconds", durationFormat.MicrosecondsStyle);
        result.CreateDataPropertyOrThrow("microsecondsDisplay", durationFormat.MicrosecondsDisplay);
        result.CreateDataPropertyOrThrow("nanoseconds", durationFormat.NanosecondsStyle);
        result.CreateDataPropertyOrThrow("nanosecondsDisplay", durationFormat.NanosecondsDisplay);

        // Fractional digits
        if (durationFormat.FractionalDigits.HasValue)
        {
            result.CreateDataPropertyOrThrow("fractionalDigits", durationFormat.FractionalDigits.Value);
        }

        return result;
    }

    private static readonly string[] DurationProperties =
    [
        "years", "months", "weeks", "days", "hours", "minutes",
        "seconds", "milliseconds", "microseconds", "nanoseconds"
    ];

    private JsDurationFormat.DurationRecord ToDurationRecord(JsValue value)
    {
        // Per spec: if input is a string, try to parse it as a duration
        if (value.IsString())
        {
            // String durations not yet supported - throw RangeError
            Throw.RangeError(_realm, "Duration string parsing is not supported");
        }

        if (!value.IsObject())
        {
            Throw.TypeError(_realm, "Duration must be an object");
        }

        var obj = value.AsObject();

        // Check if at least one duration property is defined and not undefined
        var hasDefinedProperty = false;
        foreach (var prop in DurationProperties)
        {
            var propValue = obj.Get(prop);
            if (!propValue.IsUndefined())
            {
                hasDefinedProperty = true;
                break;
            }
        }

        if (!hasDefinedProperty)
        {
            Throw.TypeError(_realm, "Duration must have at least one duration property defined");
        }

        var record = new JsDurationFormat.DurationRecord();

        record.Years = GetDurationComponent(obj, "years");
        record.Months = GetDurationComponent(obj, "months");
        record.Weeks = GetDurationComponent(obj, "weeks");
        record.Days = GetDurationComponent(obj, "days");
        record.Hours = GetDurationComponent(obj, "hours");
        record.Minutes = GetDurationComponent(obj, "minutes");
        record.Seconds = GetDurationComponent(obj, "seconds");
        record.Milliseconds = GetDurationComponent(obj, "milliseconds");
        record.Microseconds = GetDurationComponent(obj, "microseconds");
        record.Nanoseconds = GetDurationComponent(obj, "nanoseconds");

        // Validate the duration record per spec (IsValidDurationRecord)
        ValidateDurationRecord(record);

        return record;
    }

    private void ValidateDurationRecord(JsDurationFormat.DurationRecord record)
    {
        const double MaxYearsMonthsWeeks = 4294967296.0; // 2^32

        // Check if years, months, weeks are in valid range
        if (System.Math.Abs(record.Years) >= MaxYearsMonthsWeeks)
        {
            Throw.RangeError(_realm, "years value out of range");
        }
        if (System.Math.Abs(record.Months) >= MaxYearsMonthsWeeks)
        {
            Throw.RangeError(_realm, "months value out of range");
        }
        if (System.Math.Abs(record.Weeks) >= MaxYearsMonthsWeeks)
        {
            Throw.RangeError(_realm, "weeks value out of range");
        }

        // Per spec: normalizedSeconds = days × 86400 + hours × 3600 + minutes × 60 + seconds +
        //   milliseconds × 10^-3 + microseconds × 10^-6 + nanoseconds × 10^-9
        // If abs(normalizedSeconds) >= 2^53, throw RangeError
        // Use BigInteger arithmetic to avoid double precision loss.
        // Compute totalNanoseconds = normalizedSeconds × 10^9 (exact integer arithmetic)
        var totalNanoseconds =
            new BigInteger(record.Days) * 86_400_000_000_000 +
            new BigInteger(record.Hours) * 3_600_000_000_000 +
            new BigInteger(record.Minutes) * 60_000_000_000 +
            new BigInteger(record.Seconds) * 1_000_000_000 +
            new BigInteger(record.Milliseconds) * 1_000_000 +
            new BigInteger(record.Microseconds) * 1_000 +
            new BigInteger(record.Nanoseconds);

        // maxTimeDuration = 2^53 × 10^9 - 1 (the maximum valid total nanoseconds)
        // abs(totalNanoseconds) >= 2^53 × 10^9 means normalizedSeconds >= 2^53
        BigInteger maxTimeDuration = ((BigInteger) 1 << 53) * 1_000_000_000;
        if (BigInteger.Abs(totalNanoseconds) >= maxTimeDuration)
        {
            Throw.RangeError(_realm, "Duration time values out of range");
        }

        // Check for mixed positive and negative values
        var hasPositive = record.Years > 0 || record.Months > 0 || record.Weeks > 0 ||
                         record.Days > 0 || record.Hours > 0 || record.Minutes > 0 ||
                         record.Seconds > 0 || record.Milliseconds > 0 ||
                         record.Microseconds > 0 || record.Nanoseconds > 0;

        var hasNegative = record.Years < 0 || record.Months < 0 || record.Weeks < 0 ||
                         record.Days < 0 || record.Hours < 0 || record.Minutes < 0 ||
                         record.Seconds < 0 || record.Milliseconds < 0 ||
                         record.Microseconds < 0 || record.Nanoseconds < 0;

        if (hasPositive && hasNegative)
        {
            Throw.RangeError(_realm, "Duration cannot have mixed positive and negative values");
        }
    }

    private double GetDurationComponent(ObjectInstance obj, string property)
    {
        var value = obj.Get(property);
        if (value.IsUndefined())
        {
            return 0;
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            Throw.RangeError(_realm, $"Invalid value for {property}");
        }

        // Per spec: duration properties must be integers
        var truncated = System.Math.Truncate(number);
        if (number != truncated)
        {
            Throw.RangeError(_realm, $"Duration property {property} must be an integer");
        }

        return truncated;
    }
}

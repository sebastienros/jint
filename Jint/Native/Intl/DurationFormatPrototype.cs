#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- prototype methods return JsValue

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
    private JsValue FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var durationFormat = ValidateDurationFormat(thisObject);
        var duration = arguments.At(0);

        var durationRecord = ToDurationRecord(duration);
        return durationFormat.FormatToParts(_engine, durationRecord);
    }

    /// <summary>
    /// https://tc39.es/proposal-intl-duration-format/#sec-intl.durationformat.prototype.resolvedoptions
    /// </summary>
    private JsValue ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var durationFormat = ValidateDurationFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.Set("locale", durationFormat.Locale);
        result.Set("numberingSystem", durationFormat.NumberingSystem);
        result.Set("style", durationFormat.Style);

        // Unit styles
        result.Set("years", durationFormat.YearsStyle);
        result.Set("yearsDisplay", durationFormat.YearsDisplay);
        result.Set("months", durationFormat.MonthsStyle);
        result.Set("monthsDisplay", durationFormat.MonthsDisplay);
        result.Set("weeks", durationFormat.WeeksStyle);
        result.Set("weeksDisplay", durationFormat.WeeksDisplay);
        result.Set("days", durationFormat.DaysStyle);
        result.Set("daysDisplay", durationFormat.DaysDisplay);
        result.Set("hours", durationFormat.HoursStyle);
        result.Set("hoursDisplay", durationFormat.HoursDisplay);
        result.Set("minutes", durationFormat.MinutesStyle);
        result.Set("minutesDisplay", durationFormat.MinutesDisplay);
        result.Set("seconds", durationFormat.SecondsStyle);
        result.Set("secondsDisplay", durationFormat.SecondsDisplay);
        result.Set("milliseconds", durationFormat.MillisecondsStyle);
        result.Set("millisecondsDisplay", durationFormat.MillisecondsDisplay);
        result.Set("microseconds", durationFormat.MicrosecondsStyle);
        result.Set("microsecondsDisplay", durationFormat.MicrosecondsDisplay);
        result.Set("nanoseconds", durationFormat.NanosecondsStyle);
        result.Set("nanosecondsDisplay", durationFormat.NanosecondsDisplay);

        // Fractional digits
        if (durationFormat.FractionalDigits.HasValue)
        {
            result.Set("fractionalDigits", durationFormat.FractionalDigits.Value);
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
        const long MaxYearsMonthsWeeks = 4294967296L; // 2^32

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

    private long GetDurationComponent(ObjectInstance obj, string property)
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

        return (long) System.Math.Truncate(number);
    }
}

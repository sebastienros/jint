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
        result.Set("style", durationFormat.Style);
        result.Set("numberingSystem", "latn");

        return result;
    }

    private JsDurationFormat.DurationRecord ToDurationRecord(JsValue value)
    {
        if (!value.IsObject())
        {
            Throw.TypeError(_realm, "Duration must be an object");
        }

        var obj = value.AsObject();
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

        // Validate that not all components are zero or that the duration is valid
        // (For now we allow any combination)

        return record;
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

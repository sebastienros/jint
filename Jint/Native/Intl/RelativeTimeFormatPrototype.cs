using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-relativetimeformat-prototype-object
/// </summary>
internal sealed class RelativeTimeFormatPrototype : Prototype
{
    private static readonly string[] ValidUnits = ["second", "seconds", "minute", "minutes", "hour", "hours", "day", "days", "week", "weeks", "month", "months", "quarter", "quarters", "year", "years"];

    private readonly RelativeTimeFormatConstructor _constructor;

    public RelativeTimeFormatPrototype(
        Engine engine,
        Realm realm,
        RelativeTimeFormatConstructor constructor,
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
            ["format"] = new PropertyDescriptor(new ClrFunction(Engine, "format", Format, 2, LengthFlags), PropertyFlags),
            ["formatToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatToParts", FormatToParts, 2, LengthFlags), PropertyFlags),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.RelativeTimeFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsRelativeTimeFormat ValidateRelativeTimeFormat(JsValue thisObject)
    {
        if (thisObject is JsRelativeTimeFormat relativeTimeFormat)
        {
            return relativeTimeFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.RelativeTimeFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.relativetimeformat.prototype.format
    /// </summary>
    private JsValue Format(JsValue thisObject, JsCallArguments arguments)
    {
        var relativeTimeFormat = ValidateRelativeTimeFormat(thisObject);
        var value = arguments.At(0);
        var unit = arguments.At(1);

        var numericValue = TypeConverter.ToNumber(value);
        if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
        {
            Throw.RangeError(_realm, "Invalid value");
        }

        var unitString = TypeConverter.ToString(unit);
        var normalizedUnit = NormalizeUnit(unitString);
        if (normalizedUnit == null)
        {
            Throw.RangeError(_realm, $"Invalid unit: {unitString}");
        }

        return relativeTimeFormat.Format(numericValue, normalizedUnit);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.relativetimeformat.prototype.formattoparts
    /// </summary>
    private JsArray FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var relativeTimeFormat = ValidateRelativeTimeFormat(thisObject);
        var value = arguments.At(0);
        var unit = arguments.At(1);

        var numericValue = TypeConverter.ToNumber(value);
        if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
        {
            Throw.RangeError(_realm, "Invalid value");
        }

        var unitString = TypeConverter.ToString(unit);
        var normalizedUnit = NormalizeUnit(unitString);
        if (normalizedUnit == null)
        {
            Throw.RangeError(_realm, $"Invalid unit: {unitString}");
        }

        return relativeTimeFormat.FormatToParts(_engine, numericValue, normalizedUnit);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.relativetimeformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var relativeTimeFormat = ValidateRelativeTimeFormat(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.CreateDataPropertyOrThrow("locale", relativeTimeFormat.Locale);
        result.CreateDataPropertyOrThrow("style", relativeTimeFormat.Style);
        result.CreateDataPropertyOrThrow("numeric", relativeTimeFormat.Numeric);
        result.CreateDataPropertyOrThrow("numberingSystem", relativeTimeFormat.NumberingSystem);

        return result;
    }

    private static string? NormalizeUnit(string unit)
    {
        // Check if it's a valid unit
        foreach (var validUnit in ValidUnits)
        {
            if (string.Equals(unit, validUnit, StringComparison.Ordinal))
            {
                // Normalize to singular form
                return unit switch
                {
                    "seconds" => "second",
                    "minutes" => "minute",
                    "hours" => "hour",
                    "days" => "day",
                    "weeks" => "week",
                    "months" => "month",
                    "quarters" => "quarter",
                    "years" => "year",
                    _ => unit
                };
            }
        }

        return null;
    }
}

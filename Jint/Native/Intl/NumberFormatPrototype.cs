using Jint.Native.BigInt;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-numberformat-prototype-object
/// </summary>
internal sealed class NumberFormatPrototype : Prototype
{
    private readonly NumberFormatConstructor _constructor;

    public NumberFormatPrototype(
        Engine engine,
        Realm realm,
        NumberFormatConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        const PropertyFlag LengthFlags = PropertyFlag.Configurable;
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(5, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
            ["formatToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatToParts", FormatToParts, 1, LengthFlags), PropertyFlags),
            ["formatRange"] = new PropertyDescriptor(new ClrFunction(Engine, "formatRange", FormatRange, 2, LengthFlags), PropertyFlags),
            ["formatRangeToParts"] = new PropertyDescriptor(new ClrFunction(Engine, "formatRangeToParts", FormatRangeToParts, 2, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        // format is an accessor property - accessor properties don't have writable attribute
        SetAccessor("format", new GetSetPropertyDescriptor(
            new ClrFunction(Engine, "get format", GetFormat, 0, LengthFlags),
            Undefined,
            PropertyFlag.Configurable));

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.NumberFormat", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void SetAccessor(string name, GetSetPropertyDescriptor descriptor)
    {
        SetProperty(name, descriptor);
    }

    private JsNumberFormat ValidateNumberFormat(JsValue thisObject)
    {
        if (thisObject is JsNumberFormat numberFormat)
        {
            return numberFormat;
        }

        Throw.TypeError(_realm, "Value is not an Intl.NumberFormat");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.format
    /// </summary>
    private ClrFunction GetFormat(JsValue thisObject, JsCallArguments arguments)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        // Return a bound format function
        return new ClrFunction(Engine, "", (_, args) =>
        {
            var value = args.At(0);

            // Handle BigInt values separately to preserve precision
            if (value is JsBigInt bigInt)
            {
                return numberFormat.Format(bigInt._value);
            }

            if (value is BigIntInstance bigIntInstance)
            {
                return numberFormat.Format(bigIntInstance.BigIntData._value);
            }

            return numberFormat.Format(TypeConverter.ToNumber(value));
        }, 1, PropertyFlag.Configurable);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        var result = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        result.Set("locale", numberFormat.Locale);
        result.Set("numberingSystem", "latn"); // Default to Latin numerals
        result.Set("style", numberFormat.Style);

        if (string.Equals(numberFormat.Style, "currency", StringComparison.Ordinal))
        {
            result.Set("currency", numberFormat.Currency ?? "");
            result.Set("currencyDisplay", numberFormat.CurrencyDisplay ?? "symbol");
            result.Set("currencySign", numberFormat.CurrencySign ?? "standard");
        }

        if (string.Equals(numberFormat.Style, "unit", StringComparison.Ordinal))
        {
            result.Set("unit", numberFormat.Unit ?? "");
            result.Set("unitDisplay", numberFormat.UnitDisplay ?? "short");
        }

        result.Set("minimumIntegerDigits", numberFormat.MinimumIntegerDigits);
        result.Set("minimumFractionDigits", numberFormat.MinimumFractionDigits);
        result.Set("maximumFractionDigits", numberFormat.MaximumFractionDigits);

        // Include significant digits options if they were specified
        if (numberFormat.MinimumSignificantDigits.HasValue)
        {
            result.Set("minimumSignificantDigits", numberFormat.MinimumSignificantDigits.Value);
        }
        if (numberFormat.MaximumSignificantDigits.HasValue)
        {
            result.Set("maximumSignificantDigits", numberFormat.MaximumSignificantDigits.Value);
        }

        // Per spec, useGrouping can be "auto", "always", "min2", or false (boolean)
        if (string.Equals(numberFormat.UseGrouping, "false", StringComparison.Ordinal))
        {
            result.Set("useGrouping", false);
        }
        else
        {
            result.Set("useGrouping", numberFormat.UseGrouping);
        }
        result.Set("notation", numberFormat.Notation);

        // compactDisplay is only included when notation is "compact"
        if (string.Equals(numberFormat.Notation, "compact", StringComparison.Ordinal))
        {
            result.Set("compactDisplay", numberFormat.CompactDisplay);
        }

        result.Set("signDisplay", numberFormat.SignDisplay);
        result.Set("roundingMode", numberFormat.RoundingMode);
        result.Set("roundingPriority", numberFormat.RoundingPriority);
        result.Set("roundingIncrement", numberFormat.RoundingIncrement);
        result.Set("trailingZeroDisplay", numberFormat.TrailingZeroDisplay);

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.formattoparts
    /// </summary>
    private JsArray FormatToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var numberFormat = ValidateNumberFormat(thisObject);
        var value = arguments.At(0);

        double number;
        if (value.IsUndefined())
        {
            number = double.NaN;
        }
        else
        {
            number = TypeConverter.ToNumber(value);
        }

        // Get parts from the number format
        var parts = numberFormat.FormatToParts(number);

        // Convert to JsArray of objects
        var result = new JsArray(Engine, (uint) parts.Count);
        for (var i = 0; i < parts.Count; i++)
        {
            var partObj = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            partObj.Set("type", parts[i].Type);
            partObj.Set("value", parts[i].Value);
            result.SetIndexValue((uint) i, partObj, updateLength: true);
        }

        return result;
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.formatrange
    /// </summary>
    private JsValue FormatRange(JsValue thisObject, JsCallArguments arguments)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        var start = arguments.At(0);
        var end = arguments.At(1);

        // Validate arguments
        if (start.IsUndefined() || end.IsUndefined())
        {
            Throw.TypeError(_realm, "start and end are required");
        }

        var startNum = ToRangeNumber(start);
        var endNum = ToRangeNumber(end);

        // Format both numbers
        var startFormatted = numberFormat.Format(startNum);
        var endFormatted = numberFormat.Format(endNum);

        // If the numbers are the same when formatted, return just one
        if (string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            return startFormatted;
        }

        // Return a range string with locale-appropriate separator
        return $"{startFormatted} – {endFormatted}";
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.numberformat.prototype.formatrangetoparts
    /// </summary>
    private JsArray FormatRangeToParts(JsValue thisObject, JsCallArguments arguments)
    {
        var numberFormat = ValidateNumberFormat(thisObject);

        var start = arguments.At(0);
        var end = arguments.At(1);

        // Validate arguments
        if (start.IsUndefined() || end.IsUndefined())
        {
            Throw.TypeError(_realm, "start and end are required");
        }

        var startNum = ToRangeNumber(start);
        var endNum = ToRangeNumber(end);

        // Format both numbers
        var startFormatted = numberFormat.Format(startNum);
        var endFormatted = numberFormat.Format(endNum);

        var result = new JsArray(Engine);
        uint index = 0;

        // Add start number parts with source "startRange"
        var startPart = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
        startPart.Set("type", "integer");
        startPart.Set("value", startFormatted);
        startPart.Set("source", "startRange");
        result.SetIndexValue(index++, startPart, updateLength: true);

        // If numbers are different, add separator and end number
        if (!string.Equals(startFormatted, endFormatted, StringComparison.Ordinal))
        {
            // Add separator
            var separator = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            separator.Set("type", "literal");
            separator.Set("value", " – ");
            separator.Set("source", "shared");
            result.SetIndexValue(index++, separator, updateLength: true);

            // Add end number parts with source "endRange"
            var endPart = ObjectInstance.OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);
            endPart.Set("type", "integer");
            endPart.Set("value", endFormatted);
            endPart.Set("source", "endRange");
            result.SetIndexValue(index++, endPart, updateLength: true);
        }

        return result;
    }

    private double ToRangeNumber(JsValue value)
    {
        // Handle BigInt values - convert to double
        if (value is JsBigInt bigInt)
        {
            return (double) bigInt._value;
        }

        if (value is BigIntInstance bigIntInstance)
        {
            return (double) bigIntInstance.BigIntData._value;
        }

        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            Throw.RangeError(_realm, "Invalid number value");
        }

        return number;
    }
}

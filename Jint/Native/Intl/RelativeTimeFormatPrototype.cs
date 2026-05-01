using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-relativetimeformat-prototype-object
/// </summary>
[JsObject]
internal sealed partial class RelativeTimeFormatPrototype : Prototype
{
    private static readonly string[] ValidUnits = ["second", "seconds", "minute", "minutes", "hour", "hours", "day", "days", "week", "weeks", "month", "months", "quarter", "quarters", "year", "years"];

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly RelativeTimeFormatConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString RelativeTimeFormatToStringTag = new("Intl.RelativeTimeFormat");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
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
    [JsFunction(Length = 2)]
    private JsValue Format(JsValue thisObject, JsValue value, JsValue unit)
    {
        var relativeTimeFormat = ValidateRelativeTimeFormat(thisObject);

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
    [JsFunction(Length = 2)]
    private JsArray FormatToParts(JsValue thisObject, JsValue value, JsValue unit)
    {
        var relativeTimeFormat = ValidateRelativeTimeFormat(thisObject);

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
    [JsFunction(Length = 0)]
    private JsObject ResolvedOptions(JsValue thisObject)
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

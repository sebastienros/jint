using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-pluralrules-prototype-object
/// </summary>
internal sealed class PluralRulesPrototype : Prototype
{
    private readonly PluralRulesConstructor _constructor;

    public PluralRulesPrototype(
        Engine engine,
        Realm realm,
        PluralRulesConstructor constructor,
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
            ["select"] = new PropertyDescriptor(new ClrFunction(Engine, "select", Select, 1, LengthFlags), PropertyFlags),
            ["selectRange"] = new PropertyDescriptor(new ClrFunction(Engine, "selectRange", SelectRange, 2, LengthFlags), PropertyFlags),
            ["resolvedOptions"] = new PropertyDescriptor(new ClrFunction(Engine, "resolvedOptions", ResolvedOptions, 0, LengthFlags), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl.PluralRules", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsPluralRules ValidatePluralRules(JsValue thisObject)
    {
        if (thisObject is JsPluralRules pluralRules)
        {
            return pluralRules;
        }

        Throw.TypeError(_realm, "Value is not an Intl.PluralRules");
        return null!; // Never reached
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.pluralrules.prototype.select
    /// </summary>
    private JsValue Select(JsValue thisObject, JsCallArguments arguments)
    {
        var pluralRules = ValidatePluralRules(thisObject);
        var n = arguments.At(0);

        var x = TypeConverter.ToNumber(n);
        return pluralRules.Select(x);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.pluralrules.prototype.selectrange
    /// </summary>
    private JsValue SelectRange(JsValue thisObject, JsCallArguments arguments)
    {
        var pluralRules = ValidatePluralRules(thisObject);
        var start = arguments.At(0);
        var end = arguments.At(1);

        if (start.IsUndefined())
        {
            Throw.TypeError(_realm, "start is undefined");
        }

        if (end.IsUndefined())
        {
            Throw.TypeError(_realm, "end is undefined");
        }

        var x = TypeConverter.ToNumber(start);
        var y = TypeConverter.ToNumber(end);

        if (double.IsNaN(x) || double.IsNaN(y))
        {
            Throw.RangeError(_realm, "Invalid number");
        }

        // For range, we typically use the end value's plural category
        // This is a simplification - full CLDR data would have specific range rules
        return pluralRules.Select(y);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.pluralrules.prototype.resolvedoptions
    /// </summary>
    private JsObject ResolvedOptions(JsValue thisObject, JsCallArguments arguments)
    {
        var pluralRules = ValidatePluralRules(thisObject);

        var result = OrdinaryObjectCreate(Engine, Engine.Realm.Intrinsics.Object.PrototypeObject);

        // Properties in spec-defined order:
        // locale, type, notation, minimumIntegerDigits,
        // then either (minimumFractionDigits, maximumFractionDigits) OR (minimumSignificantDigits, maximumSignificantDigits),
        // then pluralCategories
        result.CreateDataPropertyOrThrow("locale", pluralRules.Locale);
        result.CreateDataPropertyOrThrow("type", pluralRules.PluralRuleType);
        result.CreateDataPropertyOrThrow("notation", pluralRules.Notation);
        result.CreateDataPropertyOrThrow("minimumIntegerDigits", pluralRules.MinimumIntegerDigits);

        // Include either fraction digits or significant digits (not both)
        if (pluralRules.MinimumSignificantDigits.HasValue)
        {
            result.CreateDataPropertyOrThrow("minimumSignificantDigits", pluralRules.MinimumSignificantDigits.Value);
            result.CreateDataPropertyOrThrow("maximumSignificantDigits", pluralRules.MaximumSignificantDigits!.Value);
        }
        else
        {
            result.CreateDataPropertyOrThrow("minimumFractionDigits", pluralRules.MinimumFractionDigits!.Value);
            result.CreateDataPropertyOrThrow("maximumFractionDigits", pluralRules.MaximumFractionDigits!.Value);
        }

        // Return plural categories
        var categories = pluralRules.GetPluralCategories();
        var categoriesArray = new JsArray(_engine, (uint) categories.Length);
        for (var i = 0; i < categories.Length; i++)
        {
            categoriesArray.SetIndexValue((uint) i, categories[i], updateLength: true);
        }
        result.CreateDataPropertyOrThrow("pluralCategories", categoriesArray);

        return result;
    }
}

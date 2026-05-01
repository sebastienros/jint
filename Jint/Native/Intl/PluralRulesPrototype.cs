using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#sec-properties-of-intl-pluralrules-prototype-object
/// </summary>
[JsObject]
internal sealed partial class PluralRulesPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PluralRulesConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString PluralRulesToStringTag = new("Intl.PluralRules");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();
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
    [JsFunction(Length = 1)]
    private JsValue Select(JsValue thisObject, JsValue n)
    {
        var pluralRules = ValidatePluralRules(thisObject);
        var x = TypeConverter.ToNumber(n);
        return pluralRules.Select(x);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.pluralrules.prototype.selectrange
    /// </summary>
    [JsFunction(Length = 2)]
    private JsValue SelectRange(JsValue thisObject, JsValue start, JsValue end)
    {
        var pluralRules = ValidatePluralRules(thisObject);

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
    [JsFunction(Length = 0)]
    private JsObject ResolvedOptions(JsValue thisObject)
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

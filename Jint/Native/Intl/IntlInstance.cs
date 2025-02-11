#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#intl-object
/// </summary>
internal sealed class IntlInstance : ObjectInstance
{
    private readonly Realm _realm;

    internal IntlInstance(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype) : base(engine)
    {
        _realm = realm;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        // TODO check length
        var properties = new PropertyDictionary(10, checkExistingKeys: false)
        {
            ["Collator"] = new(_realm.Intrinsics.Collator, false, false, true),
            ["DateTimeFormat"] = new(_realm.Intrinsics.DateTimeFormat, false, false, true),
            ["DisplayNames"] = new(_realm.Intrinsics.DisplayNames, false, false, true),
            ["ListFormat"] = new(_realm.Intrinsics.ListFormat, false, false, true),
            ["Locale"] = new(_realm.Intrinsics.Locale, false, false, true),
            ["NumberFormat"] = new(_realm.Intrinsics.NumberFormat, false, false, true),
            ["PluralRules"] = new(_realm.Intrinsics.PluralRules, false, false, true),
            ["RelativeTimeFormat"] = new(_realm.Intrinsics.RelativeTimeFormat, false, false, true),
            ["Segmenter"] = new(_realm.Intrinsics.Segmenter, false, false, true),
            ["getCanonicalLocales "] = new(new ClrFunction(Engine, "getCanonicalLocales ", GetCanonicalLocales , 1, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private JsValue GetCanonicalLocales(JsValue thisObject, JsCallArguments arguments)
    {
        return new JsArray(_engine);
    }
}

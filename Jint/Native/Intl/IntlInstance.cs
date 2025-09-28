#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

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
            ["getCanonicalLocales"] = new(new ClrFunction(Engine, "getCanonicalLocales", GetCanonicalLocales, 1, PropertyFlag.Configurable), true, false, true),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    // CanonicalizeLocaleList(locales)
    // Spec: https://tc39.es/ecma402/#sec-canonicalizelocalelist
    // Behavior mirrors WebKit’s implementation: if `locales` is undefined -> empty list.
    // Otherwise iterate array-like `O`, accept String or Intl.Locale objects, validate
    // BCP-47 via ICU (uloc_forLanguageTag), then canonicalize (uloc_toLanguageTag).
    private List<string> CanonicalizeLocaleList(JsValue locales)
    {
        var seen = new List<string>();

        // 1. If locales is undefined, return empty list
        if (locales.IsUndefined())
            return seen;

        // 3–4. Build O:
        // If locales is a String or an Intl.Locale, behave like [ locales ].
        // We special-case String; for Intl.Locale we don’t have the brand slot here,
        // so we treat other objects via ToObject (spec 4).
        bool treatAsSingle = locales.IsString();

        ObjectInstance O;
        if (treatAsSingle)
        {
            var arr = _realm.Intrinsics.Array.Construct(Arguments.Empty);
            arr.SetIndexValue(0, locales, updateLength: true);
            O = arr;
        }
        else
        {
            O = TypeConverter.ToObject(_realm, locales);
        }

        // 5. Let len be LengthOfArrayLike(O)
        var lenValue = O.Get("length");
        var len = TypeConverter.ToLength(lenValue);

        var dedupe = new HashSet<string>(System.StringComparer.Ordinal);

        // 6–7. Iterate k
        for (ulong k = 0; k < len; k++)
        {
            var pk = TypeConverter.ToString(k); // ToString(𝔽(k))
            bool kPresent = O.HasProperty(pk);
            if (!kPresent)
                continue;

            var kValue = O.Get(pk);

            // 7.c.ii: must be String or Object
            if (!kValue.IsString() && !kValue.IsObject())
            {
                Throw.TypeError(_realm, "locale value must be a string or object");
            }

            // 7.c.iii/iv: tag from Locale.[[Locale]] or ToString(kValue)
            // We don’t have direct [[InitializedLocale]] plumbing here, so use ToString unless it’s a JS string.
            string tag = kValue.IsString()
                ? kValue.AsString().ToString()
                : TypeConverter.ToString(kValue);

            // 7.c.v–vii: Validate & canonicalize; throw RangeError if invalid
            string canonical = IcuHelpers.CanonicalizeUnicodeLocaleIdOrThrow(_realm, tag);

            if (dedupe.Add(canonical))
                seen.Add(canonical);
        }

        // 8. Return seen
        return seen;
    }

    // Intl.getCanonicalLocales(locales)
    // https://tc39.es/ecma402/#sec-intl.getcanonicallocales
    private JsValue GetCanonicalLocales(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        var list = CanonicalizeLocaleList(locales);

        var arr = new JsArray(_engine);
        arr.Prototype = _realm.Intrinsics.Array.PrototypeObject;

        for (uint i = 0; i < list.Count; i++)
        {
            arr.SetIndexValue(i, list[(int) i], updateLength: true);
        }

        return arr;
    }
}

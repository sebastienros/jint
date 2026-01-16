using System.Globalization;
using System.Linq;
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
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(13, checkExistingKeys: false)
        {
            ["Collator"] = new(_realm.Intrinsics.Collator, PropertyFlags),
            ["DateTimeFormat"] = new(_realm.Intrinsics.DateTimeFormat, PropertyFlags),
            ["DisplayNames"] = new(_realm.Intrinsics.DisplayNames, PropertyFlags),
            ["DurationFormat"] = new(_realm.Intrinsics.DurationFormat, PropertyFlags),
            ["ListFormat"] = new(_realm.Intrinsics.ListFormat, PropertyFlags),
            ["Locale"] = new(_realm.Intrinsics.Locale, PropertyFlags),
            ["NumberFormat"] = new(_realm.Intrinsics.NumberFormat, PropertyFlags),
            ["PluralRules"] = new(_realm.Intrinsics.PluralRules, PropertyFlags),
            ["RelativeTimeFormat"] = new(_realm.Intrinsics.RelativeTimeFormat, PropertyFlags),
            ["Segmenter"] = new(_realm.Intrinsics.Segmenter, PropertyFlags),
            ["getCanonicalLocales"] = new(new ClrFunction(Engine, "getCanonicalLocales", GetCanonicalLocales, 1, PropertyFlag.Configurable), PropertyFlags),
            ["supportedValuesOf"] = new(new ClrFunction(Engine, "supportedValuesOf", SupportedValuesOf, 1, PropertyFlag.Configurable), PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Intl", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.getcanonicallocales
    /// </summary>
    private JsArray GetCanonicalLocales(JsValue thisObject, JsCallArguments arguments)
    {
        var locales = arguments.At(0);
        return new JsArray(_engine, IntlUtilities.CanonicalizeLocaleList(_engine, locales).Select(x => new JsString(x)).ToArray<JsValue>());
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.supportedvaluesof
    /// </summary>
    private JsValue SupportedValuesOf(JsValue thisObject, JsCallArguments arguments)
    {
        var key = TypeConverter.ToString(arguments.At(0));

        string[] values;
        switch (key)
        {
            case "calendar":
                values = GetSupportedCalendars();
                break;
            case "collation":
                values = GetSupportedCollations();
                break;
            case "currency":
                values = GetSupportedCurrencies();
                break;
            case "numberingSystem":
                values = GetSupportedNumberingSystems();
                break;
            case "timeZone":
                values = GetSupportedTimeZones();
                break;
            case "unit":
                values = GetSupportedUnits();
                break;
            default:
                Throw.RangeError(_realm, $"Invalid key: {key}");
                return Undefined;
        }

        // Sort values alphabetically
        System.Array.Sort(values, StringComparer.Ordinal);

        var result = new JsArray(_engine, (uint) values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            result.SetIndexValue((uint) i, values[i], updateLength: true);
        }

        return result;
    }

    private static string[] GetSupportedCalendars()
    {
        // Return calendar types that DateTimeFormat supports
        // https://tc39.es/ecma402/#sec-availablecalendars
        // Must include all from CLDR that we support in DateTimeFormat
        return new[]
        {
            "buddhist",
            "chinese",
            "coptic",
            "dangi",
            "ethioaa",
            "ethiopic",
            "gregory",
            "hebrew",
            "indian",
            "islamic",
            "islamic-civil",
            "islamic-rgsa",
            "islamic-tbla",
            "islamic-umalqura",
            "iso8601",
            "japanese",
            "persian",
            "roc"
        };
    }

    private static string[] GetSupportedCollations()
    {
        // Return commonly supported collation types
        // https://tc39.es/ecma402/#sec-availablecollations
        // Note: "standard" and "search" are special values and must NOT be included
        return new[]
        {
            "big5han",
            "compat",
            "dict",
            "direct",
            "ducet",
            "emoji",
            "eor",
            "gb2312",
            "phonebk",
            "phonetic",
            "pinyin",
            "reformed",
            "searchjl",
            "stroke",
            "trad",
            "unihan",
            "zhuyin"
        };
    }

    private static string[] GetSupportedCurrencies()
    {
        // Return ISO 4217 currency codes
        var currencies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                currencies.Add(region.ISOCurrencySymbol);
            }
            catch
            {
                // Skip cultures without region info
            }
        }

        var result = new string[currencies.Count];
        currencies.CopyTo(result);
        return result;
    }

    private static string[] GetSupportedNumberingSystems()
    {
        // Return all numbering systems with simple digit mappings from our data
        // https://tc39.es/ecma402/#sec-availablenumberingsystems
        var digits = Data.NumberingSystemData.Digits;
        var result = new string[digits.Count];
        var index = 0;
        foreach (var key in digits.Keys)
        {
            result[index++] = key;
        }
        return result;
    }

    private static string[] GetSupportedTimeZones()
    {
        // Return IANA time zone names from our comprehensive database
        var allZones = Data.TimeZoneData.GetAllTimeZones();
        var result = new string[allZones.Count];
        for (var i = 0; i < allZones.Count; i++)
        {
            result[i] = allZones[i];
        }
        return result;
    }

    private static string[] GetSupportedUnits()
    {
        // Return sanctioned simple unit identifiers
        // https://tc39.es/ecma402/#table-sanctioned-single-unit-identifiers
        return new[]
        {
            "acre",
            "bit",
            "byte",
            "celsius",
            "centimeter",
            "day",
            "degree",
            "fahrenheit",
            "fluid-ounce",
            "foot",
            "gallon",
            "gigabit",
            "gigabyte",
            "gram",
            "hectare",
            "hour",
            "inch",
            "kilobit",
            "kilobyte",
            "kilogram",
            "kilometer",
            "liter",
            "megabit",
            "megabyte",
            "meter",
            "microsecond",
            "mile",
            "mile-scandinavian",
            "milliliter",
            "millimeter",
            "millisecond",
            "minute",
            "month",
            "nanosecond",
            "ounce",
            "percent",
            "petabyte",
            "pound",
            "second",
            "stone",
            "terabit",
            "terabyte",
            "week",
            "yard",
            "year"
        };
    }
}

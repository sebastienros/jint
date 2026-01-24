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

    /// <summary>
    /// Gets the CLDR provider from engine options.
    /// </summary>
    private ICldrProvider CldrProvider => _engine.Options.Intl.CldrProvider;

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

        var properties = new PropertyDictionary(4, checkExistingKeys: false)
        {
            ["Collator"] = new(_realm.Intrinsics.Collator, PropertyFlags),
            ["Locale"] = new(_realm.Intrinsics.Locale, PropertyFlags),
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

    private string[] GetSupportedCalendars()
    {
        // Use CLDR provider for supported calendars
        var calendars = CldrProvider.GetSupportedCalendars();
        var result = new string[calendars.Count];
        var i = 0;
        foreach (var calendar in calendars)
        {
            result[i++] = calendar;
        }
        return result;
    }

    private string[] GetSupportedCollations()
    {
        // Use CLDR provider for supported collations
        var collations = CldrProvider.GetSupportedCollations();
        var result = new string[collations.Count];
        var i = 0;
        foreach (var collation in collations)
        {
            result[i++] = collation;
        }
        return result;
    }

    private string[] GetSupportedCurrencies()
    {
        // Use CLDR provider for supported currencies
        var currencies = CldrProvider.GetSupportedCurrencies();
        var result = new string[currencies.Count];
        var i = 0;
        foreach (var currency in currencies)
        {
            result[i++] = currency;
        }
        return result;
    }

    private string[] GetSupportedNumberingSystems()
    {
        // Use CLDR provider for supported numbering systems
        var systems = CldrProvider.GetSupportedNumberingSystems();
        var result = new string[systems.Count];
        var i = 0;
        foreach (var system in systems)
        {
            result[i++] = system;
        }
        return result;
    }

    private string[] GetSupportedTimeZones()
    {
        // Use CLDR provider for supported time zones
        var zones = CldrProvider.GetSupportedTimeZones();
        var result = new string[zones.Count];
        var i = 0;
        foreach (var zone in zones)
        {
            result[i++] = zone;
        }
        return result;
    }

    private string[] GetSupportedUnits()
    {
        // Use CLDR provider for supported units
        var units = CldrProvider.GetSupportedUnits();
        var result = new string[units.Count];
        var i = 0;
        foreach (var unit in units)
        {
            result[i++] = unit;
        }
        return result;
    }
}

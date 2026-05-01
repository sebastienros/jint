using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#intl-object
/// </summary>
// ExtraCapacity = 10 covers the post-Initialize SetProperty calls below for the 10 sub-namespace
// constructor refs (Collator/DateTimeFormat/.../Segmenter), which can't be expressed via [JsProperty]
// because they read from _realm.Intrinsics.X (not generator-friendly).
[JsObject(ExtraCapacity = 10)]
internal sealed partial class IntlInstance : ObjectInstance
{
    private readonly Realm _realm;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString IntlToStringTag = new("Intl");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Constructor references aren't generator-friendly (they pull from _realm.Intrinsics);
        // register them after generated properties to avoid a separate generator feature.
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        SetProperty("Collator", new PropertyDescriptor(_realm.Intrinsics.Collator, PropertyFlags));
        SetProperty("DateTimeFormat", new PropertyDescriptor(_realm.Intrinsics.DateTimeFormat, PropertyFlags));
        SetProperty("DisplayNames", new PropertyDescriptor(_realm.Intrinsics.DisplayNames, PropertyFlags));
        SetProperty("DurationFormat", new PropertyDescriptor(_realm.Intrinsics.DurationFormat, PropertyFlags));
        SetProperty("ListFormat", new PropertyDescriptor(_realm.Intrinsics.ListFormat, PropertyFlags));
        SetProperty("Locale", new PropertyDescriptor(_realm.Intrinsics.Locale, PropertyFlags));
        SetProperty("NumberFormat", new PropertyDescriptor(_realm.Intrinsics.NumberFormat, PropertyFlags));
        SetProperty("PluralRules", new PropertyDescriptor(_realm.Intrinsics.PluralRules, PropertyFlags));
        SetProperty("RelativeTimeFormat", new PropertyDescriptor(_realm.Intrinsics.RelativeTimeFormat, PropertyFlags));
        SetProperty("Segmenter", new PropertyDescriptor(_realm.Intrinsics.Segmenter, PropertyFlags));
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.getcanonicallocales
    /// </summary>
    [JsFunction(Length = 1)]
    private JsArray GetCanonicalLocales(JsValue thisObject, JsValue locales)
    {
        return new JsArray(_engine, IntlUtilities.CanonicalizeLocaleList(_engine, locales).Select(x => new JsString(x)).ToArray<JsValue>());
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.supportedvaluesof
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue SupportedValuesOf(JsValue thisObject, JsValue keyArg)
    {
        var key = TypeConverter.ToString(keyArg);

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

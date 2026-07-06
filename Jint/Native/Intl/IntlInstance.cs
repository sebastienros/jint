using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Intl;

/// <summary>
/// https://tc39.es/ecma402/#intl-object
/// </summary>
// The 10 sub-namespace constructor references (Collator/DateTimeFormat/.../Segmenter) are lazy
// realm-intrinsic references, emitted by the generator exactly like globalThis's constructor
// properties (see GlobalObject.Properties.cs): each resolves _realm.Intrinsics.X on first read.
[JsIntrinsicReference("Collator")]
[JsIntrinsicReference("DateTimeFormat")]
[JsIntrinsicReference("DisplayNames")]
[JsIntrinsicReference("DurationFormat")]
[JsIntrinsicReference("ListFormat")]
[JsIntrinsicReference("Locale")]
[JsIntrinsicReference("NumberFormat")]
[JsIntrinsicReference("PluralRules")]
[JsIntrinsicReference("RelativeTimeFormat")]
[JsIntrinsicReference("Segmenter")]
[JsObject(UseShape = true)]
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
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.getcanonicallocales
    /// </summary>
    [JsFunction]
    private JsArray GetCanonicalLocales(JsValue thisObject, JsValue locales)
    {
        return new JsArray(_engine, IntlUtilities.CanonicalizeLocaleList(_engine, locales).Select(x => new JsString(x)).ToArray<JsValue>());
    }

    /// <summary>
    /// https://tc39.es/ecma402/#sec-intl.supportedvaluesof
    /// </summary>
    [JsFunction]
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

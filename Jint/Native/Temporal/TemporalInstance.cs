using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Temporal;

/// <summary>
/// The Temporal namespace object.
/// https://tc39.es/proposal-temporal/#sec-temporal-objects
/// </summary>
internal sealed class TemporalInstance : ObjectInstance
{
    private readonly Realm _realm;

    internal TemporalInstance(
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

        var properties = new PropertyDictionary(10, checkExistingKeys: false)
        {
            ["Duration"] = new(_realm.Intrinsics.TemporalDuration, PropertyFlags),
            ["Instant"] = new(_realm.Intrinsics.TemporalInstant, PropertyFlags),
            ["PlainDate"] = new(_realm.Intrinsics.TemporalPlainDate, PropertyFlags),
            ["PlainDateTime"] = new(_realm.Intrinsics.TemporalPlainDateTime, PropertyFlags),
            ["PlainMonthDay"] = new(_realm.Intrinsics.TemporalPlainMonthDay, PropertyFlags),
            ["PlainTime"] = new(_realm.Intrinsics.TemporalPlainTime, PropertyFlags),
            ["PlainYearMonth"] = new(_realm.Intrinsics.TemporalPlainYearMonth, PropertyFlags),
            ["ZonedDateTime"] = new(_realm.Intrinsics.TemporalZonedDateTime, PropertyFlags),
            ["Now"] = new(_realm.Intrinsics.TemporalNow, PropertyFlags),
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }
}

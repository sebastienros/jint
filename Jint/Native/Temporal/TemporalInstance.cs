using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Temporal;

/// <summary>
/// The Temporal namespace object.
/// https://tc39.es/proposal-temporal/#sec-temporal-objects
/// </summary>
// ExtraCapacity = 9 covers the post-Initialize SetProperty calls below for Duration/Instant/PlainDate/
// PlainDateTime/PlainMonthDay/PlainTime/PlainYearMonth/ZonedDateTime/Now (cross-realm references).
[JsObject(ExtraCapacity = 9)]
internal sealed partial class TemporalInstance : ObjectInstance
{
    private readonly Realm _realm;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString TemporalToStringTag = new("Temporal");

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
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Constructor references aren't generator-friendly (they pull from _realm.Intrinsics);
        // wire them after generated symbols.
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        SetProperty("Duration", new PropertyDescriptor(_realm.Intrinsics.TemporalDuration, PropertyFlags));
        SetProperty("Instant", new PropertyDescriptor(_realm.Intrinsics.TemporalInstant, PropertyFlags));
        SetProperty("PlainDate", new PropertyDescriptor(_realm.Intrinsics.TemporalPlainDate, PropertyFlags));
        SetProperty("PlainDateTime", new PropertyDescriptor(_realm.Intrinsics.TemporalPlainDateTime, PropertyFlags));
        SetProperty("PlainMonthDay", new PropertyDescriptor(_realm.Intrinsics.TemporalPlainMonthDay, PropertyFlags));
        SetProperty("PlainTime", new PropertyDescriptor(_realm.Intrinsics.TemporalPlainTime, PropertyFlags));
        SetProperty("PlainYearMonth", new PropertyDescriptor(_realm.Intrinsics.TemporalPlainYearMonth, PropertyFlags));
        SetProperty("ZonedDateTime", new PropertyDescriptor(_realm.Intrinsics.TemporalZonedDateTime, PropertyFlags));
        SetProperty("Now", new PropertyDescriptor(_realm.Intrinsics.TemporalNow, PropertyFlags));
    }
}

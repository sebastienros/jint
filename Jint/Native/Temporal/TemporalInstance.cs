using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Temporal;

/// <summary>
/// The Temporal namespace object.
/// https://tc39.es/proposal-temporal/#sec-temporal-objects
/// </summary>
// The 9 sub-namespace references (Duration/Instant/.../ZonedDateTime/Now) are lazy realm-intrinsic
// references, emitted by the generator like globalThis's constructor properties (see
// GlobalObject.Properties.cs). IntrinsicMember maps each JS name to its Temporal*-prefixed intrinsic.
[JsIntrinsicReference("Duration", IntrinsicMember = "TemporalDuration")]
[JsIntrinsicReference("Instant", IntrinsicMember = "TemporalInstant")]
[JsIntrinsicReference("Now", IntrinsicMember = "TemporalNow")]
[JsIntrinsicReference("PlainDate", IntrinsicMember = "TemporalPlainDate")]
[JsIntrinsicReference("PlainDateTime", IntrinsicMember = "TemporalPlainDateTime")]
[JsIntrinsicReference("PlainMonthDay", IntrinsicMember = "TemporalPlainMonthDay")]
[JsIntrinsicReference("PlainTime", IntrinsicMember = "TemporalPlainTime")]
[JsIntrinsicReference("PlainYearMonth", IntrinsicMember = "TemporalPlainYearMonth")]
[JsIntrinsicReference("ZonedDateTime", IntrinsicMember = "TemporalZonedDateTime")]
[JsObject]
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
    }
}

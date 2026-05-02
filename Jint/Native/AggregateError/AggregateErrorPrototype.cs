using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.AggregateError;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-aggregate-error-prototype-objects
/// </summary>
[JsObject]
internal sealed partial class AggregateErrorPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly AggregateErrorConstructor _constructor;

    [JsProperty(Name = "message", Flags = PropertyFlag.NonEnumerable)]
    private static readonly JsString MessageDefault = JsString.Empty;

    [JsProperty(Name = "name", Flags = PropertyFlag.NonEnumerable)]
    private static readonly JsString NameValue = new("AggregateError");

    internal AggregateErrorPrototype(
        Engine engine,
        Realm realm,
        AggregateErrorConstructor constructor,
        ObjectInstance prototype)
        : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = prototype;
    }

    protected override void Initialize() => CreateProperties_Generated();
}

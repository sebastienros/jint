using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.SuppressedError;

[JsObject]
internal sealed partial class SuppressedErrorPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly SuppressedErrorConstructor _constructor;

    [JsProperty(Name = "message", Flags = PropertyFlag.NonEnumerable)]
    private static readonly JsString MessageDefault = JsString.Empty;

    [JsProperty(Name = "name", Flags = PropertyFlag.NonEnumerable)]
    private static readonly JsString NameValue = new("SuppressedError");

    internal SuppressedErrorPrototype(
        Engine engine,
        Realm realm,
        SuppressedErrorConstructor constructor,
        ObjectInstance prototype)
        : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = prototype;
    }

    protected override void Initialize() => CreateProperties_Generated();
}

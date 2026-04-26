using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakRef;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-weak-ref-prototype-object
/// </summary>
[JsObject]
internal sealed partial class WeakRefPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WeakRefConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString WeakRefToStringTag = new("WeakRef");

    internal WeakRefPrototype(
        Engine engine,
        Realm realm,
        WeakRefConstructor constructor,
        ObjectPrototype prototype) : base(engine, realm)
    {
        _prototype = prototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsFunction(Length = 0)]
    private JsValue Deref(JsValue thisObject)
    {
        if (thisObject is JsWeakRef weakRef)
        {
            return weakRef.WeakRefDeref();
        }

        Throw.TypeError(_realm, "object must be a WeakRef");
        return default;
    }
}

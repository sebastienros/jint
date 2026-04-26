#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakMap;

/// <summary>
/// https://tc39.es/ecma262/#sec-weakmap-objects
/// </summary>
[JsObject]
internal sealed partial class WeakMapPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WeakMapConstructor _constructor;

    // Pre-existing quirk: TC39 spec does not require a `length` property on WeakMap.prototype
    // (length lives on the constructor). Preserved here verbatim from the pre-source-gen Initialize body.
    [JsProperty(Name = "length", Flags = PropertyFlag.Configurable)] private static readonly JsNumber WeakMapLength = JsNumber.PositiveZero;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString WeakMapToStringTag = new("WeakMap");

    internal WeakMapPrototype(
        Engine engine,
        Realm realm,
        WeakMapConstructor constructor,
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

    [JsFunction(Length = 1, Name = "get")]
    private JsValue MapGet(JsValue thisObject, JsValue key)
    {
        var map = AssertWeakMapInstance(thisObject);
        return map.WeakMapGet(key);
    }

    [JsFunction(Length = 2)]
    private JsValue GetOrInsert(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertWeakMapInstance(thisObject);
        var checkedKey = AssertCanBeHeldWeakly(key);
        return map.GetOrInsert(checkedKey, value);
    }

    [JsFunction(Length = 2)]
    private JsValue GetOrInsertComputed(JsValue thisObject, JsValue key, JsValue callbackfn)
    {
        var map = AssertWeakMapInstance(thisObject);
        var checkedKey = AssertCanBeHeldWeakly(key);
        var callable = callbackfn.GetCallable(_realm);
        return map.GetOrInsertComputed(checkedKey, callable);
    }

    private JsValue AssertCanBeHeldWeakly(JsValue key)
    {
        if (!key.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            Throw.TypeError(_realm, "Invalid value used as weak map key");
        }

        return key;
    }

    [JsFunction(Length = 1)]
    private JsValue Delete(JsValue thisObject, JsValue key)
    {
        var map = AssertWeakMapInstance(thisObject);
        return map.WeakMapDelete(key) ? JsBoolean.True : JsBoolean.False;
    }

    [JsFunction(Length = 2, Name = "set")]
    private JsValue MapSet(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertWeakMapInstance(thisObject);
        map.WeakMapSet(key, value);
        return thisObject;
    }

    [JsFunction(Length = 1)]
    private JsValue Has(JsValue thisObject, JsValue key)
    {
        var map = AssertWeakMapInstance(thisObject);
        return map.WeakMapHas(key) ? JsBoolean.True : JsBoolean.False;
    }

    private JsWeakMap AssertWeakMapInstance(JsValue thisObject)
    {
        if (thisObject is JsWeakMap map)
        {
            return map;
        }

        Throw.TypeError(_realm, "object must be a WeakMap");
        return default;
    }
}

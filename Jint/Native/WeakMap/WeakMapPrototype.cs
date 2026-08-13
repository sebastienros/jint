#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.WeakMap;

/// <summary>
/// https://tc39.es/ecma262/#sec-weakmap-objects
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class WeakMapPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WeakMapConstructor _constructor;

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

    [JsFunction(Name = "get")]
    private JsValue MapGet(JsValue thisObject, JsValue key)
    {
        var map = AssertWeakMapInstance(thisObject);
        return map.WeakMapGet(key);
    }

    [JsFunction]
    private JsValue GetOrInsert(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertWeakMapInstance(thisObject);
        var checkedKey = AssertCanBeHeldWeakly(key);
        return map.GetOrInsert(checkedKey, value);
    }

    [JsFunction]
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

    [JsFunction(FastCall = true)]
    private JsValue Delete(JsValue thisObject, JsValue key)
    {
        var map = AssertWeakMapInstance(thisObject);
        return map.WeakMapDelete(key) ? JsBoolean.True : JsBoolean.False;
    }

    [JsFunction(Name = "set", FastCall = true)]
    private JsValue MapSet(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertWeakMapInstance(thisObject);
        map.WeakMapSet(key, value);
        return thisObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-weakmap.prototype.has
    /// </summary>
    /// <remarks>
    /// Register lane only, unlike its <c>Map.prototype</c> namesake, and deliberately so — the same
    /// goes for <c>WeakSet.prototype</c>. Two things would have to change first. A <c>WeakMap</c>
    /// receiver has no <c>InternalTypes</c> bit, and giving it one (plus one for <c>WeakSet</c>) would
    /// spend the enum's remaining headroom on a far colder path than a Map lookup. And it would buy
    /// only half the family: <c>set</c> and <c>add</c> raise a TypeError for a key that cannot be held
    /// weakly, which is a property of the argument that no <c>FastCallGuard</c> expresses, so those
    /// two would stay framed however the receiver were guarded.
    /// </remarks>
    [JsFunction(FastCall = true)]
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

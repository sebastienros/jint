#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Map;

/// <summary>
/// https://tc39.es/ecma262/#sec-map-objects
/// </summary>
[JsObject]
internal sealed partial class MapPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly MapConstructor _mapConstructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString MapToStringTag = new("Map");

    internal MapPrototype(
        Engine engine,
        Realm realm,
        MapConstructor mapConstructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _mapConstructor = mapConstructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Spec requires Map.prototype[@@iterator] to be the same function object as Map.prototype.entries
        // (function identity, observable via ===). Alias the descriptor here so the @@iterator slot
        // shares the same materialized function as `entries` rather than emitting a separate dispatcher.
        SetProperty(GlobalSymbolRegistry.Iterator, GetOwnProperty("entries"));
    }

    [JsAccessor("size")]
    private JsValue Size(JsValue thisObject)
    {
        AssertMapInstance(thisObject);
        return JsNumber.Create(0);
    }

    [JsFunction(Length = 1, Name = "get")]
    private JsValue MapGet(JsValue thisObject, JsValue key)
    {
        var map = AssertMapInstance(thisObject);
        return map.Get(key);
    }

    [JsFunction(Length = 2)]
    private JsValue GetOrInsert(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertMapInstance(thisObject);
        var checkedKey = key.CanonicalizeKeyedCollectionKey();
        return map.GetOrInsert(checkedKey, value);
    }

    [JsFunction(Length = 2)]
    private JsValue GetOrInsertComputed(JsValue thisObject, JsValue key, JsValue callbackfn)
    {
        var map = AssertMapInstance(thisObject);
        var checkedKey = key.CanonicalizeKeyedCollectionKey();
        var callable = callbackfn.GetCallable(_realm);
        return map.GetOrInsertComputed(checkedKey, callable);
    }

    [JsFunction(Length = 0)]
    private JsValue Clear(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        map.Clear();
        return Undefined;
    }

    [JsFunction(Length = 1)]
    private JsValue Delete(JsValue thisObject, JsValue key)
    {
        var map = AssertMapInstance(thisObject);
        return map.Remove(key)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    [JsFunction(Length = 2, Name = "set")]
    private JsValue MapSet(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertMapInstance(thisObject);
        map.Set(key, value);
        return thisObject;
    }

    [JsFunction(Length = 1)]
    private JsValue Has(JsValue thisObject, JsValue key)
    {
        var map = AssertMapInstance(thisObject);
        return map.Has(key)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    [JsFunction(Length = 1)]
    private JsValue ForEach(JsValue thisObject, JsValue callbackfn, JsValue thisArg)
    {
        var map = AssertMapInstance(thisObject);
        var callable = GetCallable(callbackfn);

        map.ForEach(callable, thisArg);

        return Undefined;
    }

    [JsFunction(Length = 0)]
    private ObjectInstance Entries(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        return map.Iterator();
    }

    [JsFunction(Length = 0)]
    private ObjectInstance Keys(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        return map.Keys();
    }

    [JsFunction(Length = 0)]
    private ObjectInstance Values(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        return map.Values();
    }

    private JsMap AssertMapInstance(JsValue thisObject, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
    {
        if (thisObject is JsMap map)
        {
            return map;
        }

        Throw.TypeError(_realm, $"Method Map.prototype.{MapMethodName(methodName)} called on incompatible receiver {thisObject}");
        return default;
    }

    private static string MapMethodName(string callerName) => callerName switch
    {
        "Size" => "get size",
        "MapGet" => "get",
        "MapSet" => "set",
        _ => char.ToLowerInvariant(callerName[0]) + callerName.Substring(1)
    };
}

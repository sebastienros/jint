#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Map;

/// <summary>
/// https://tc39.es/ecma262/#sec-map-objects
/// </summary>
// Spec requires Map.prototype[@@iterator] to be the same function object as Map.prototype.entries
// (function identity, observable via ===); [JsSymbolAlias] shares the materialized `entries` function.
[JsSymbolAlias("Iterator", "entries")]
[JsObject(UseShape = true)]
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
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-map.prototype.size
    /// </summary>
    [JsAccessor("size")]
    private JsValue Size(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        return JsNumber.Create(map.Size);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-map.prototype.get
    /// </summary>
    /// <remarks>
    /// The first of the four keyed-collection lookups that claim <c>Leaf</c>. What makes them leaf is
    /// that a Map key is only ever hashed and compared with SameValueZero, which converts nothing — so
    /// unlike a <c>String.prototype</c> argument, no key can reach a user <c>valueOf</c> inside the
    /// frameless window, whatever its type. That is what <c>LeafArg0 = AnyValue</c> declares, and it is
    /// the whole reason these can be leaf for the object keys a Map exists for.
    /// <para>
    /// The one hazard left is the receiver: <see cref="AssertMapInstance"/> raises a TypeError for
    /// anything that is not a <c>JsMap</c>, and <c>LeafReceiver</c> is what keeps that call framed.
    /// Note the guard can only ever fail on a call that was going to throw anyway — a warm site's
    /// callee IS <c>Map.prototype.get</c>, so a receiver failing the brand test has no non-throwing
    /// outcome to reach.
    /// </para>
    /// <para>
    /// The remaining edge is a wrapped CLR object used as a key, whose <c>Equals</c>/<c>GetHashCode</c>
    /// the comparer calls: that is host code, not interpreted code, and it runs identically on the
    /// framed path. A host implementation that re-entered the engine would push its own frames and be
    /// charged for them; only this built-in's own frame would be missing from the stack it reads.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "get", FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Map, LeafArg0 = FastCallGuard.AnyValue)]
    private JsValue MapGet(JsValue thisObject, JsValue key)
    {
        var map = AssertMapInstance(thisObject);
        return map.Get(key);
    }

    [JsFunction]
    private JsValue GetOrInsert(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertMapInstance(thisObject);
        var checkedKey = key.CanonicalizeKeyedCollectionKey();
        return map.GetOrInsert(checkedKey, value);
    }

    [JsFunction]
    private JsValue GetOrInsertComputed(JsValue thisObject, JsValue key, JsValue callbackfn)
    {
        var map = AssertMapInstance(thisObject);
        var checkedKey = key.CanonicalizeKeyedCollectionKey();
        var callable = callbackfn.GetCallable(_realm);
        return map.GetOrInsertComputed(checkedKey, callable);
    }

    [JsFunction]
    private JsValue Clear(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        map.Clear();
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-map.prototype.delete
    /// </summary>
    /// <remarks>Leaf on the same reasoning as <see cref="MapGet"/>.</remarks>
    [JsFunction(FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Map, LeafArg0 = FastCallGuard.AnyValue)]
    private JsValue Delete(JsValue thisObject, JsValue key)
    {
        var map = AssertMapInstance(thisObject);
        return map.Remove(key)
            ? JsBoolean.True
            : JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-map.prototype.set
    /// </summary>
    /// <remarks>
    /// Leaf on the same reasoning as <see cref="MapGet"/>, and mutating is no obstacle: the frame the
    /// lane elides carries the recursion charge and the stack a JS error would report, and nothing
    /// else — no version counter, no iteration state hangs off it. Map iteration walks the ordering
    /// list by index and <c>size</c> is answered from the live count by the <see cref="Size"/>
    /// accessor, so an entry added through the frameless path is as visible as one added through the
    /// framed path. The stored value is never converted either, hence <c>LeafArg1 = AnyValue</c>.
    /// </remarks>
    [JsFunction(Name = "set", FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Map, LeafArg0 = FastCallGuard.AnyValue, LeafArg1 = FastCallGuard.AnyValue)]
    private JsValue MapSet(JsValue thisObject, JsValue key, JsValue value)
    {
        var map = AssertMapInstance(thisObject);
        map.Set(key, value);
        return thisObject;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-map.prototype.has
    /// </summary>
    /// <remarks>Leaf on the same reasoning as <see cref="MapGet"/>.</remarks>
    [JsFunction(FastCall = true, Leaf = true,
        LeafReceiver = FastCallGuard.Map, LeafArg0 = FastCallGuard.AnyValue)]
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

    [JsFunction]
    private ObjectInstance Entries(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        return map.Iterator();
    }

    [JsFunction]
    private ObjectInstance Keys(JsValue thisObject)
    {
        var map = AssertMapInstance(thisObject);
        return map.Keys();
    }

    [JsFunction]
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

        Throw.TypeError(_realm, $"Method Map.prototype.{MapMethodName(methodName)} called on incompatible receiver");
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

#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of prototype methods return JsValue

using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Map;

/// <summary>
/// https://tc39.es/ecma262/#sec-map-objects
/// </summary>
internal sealed class MapPrototype : Prototype
{
    private readonly MapConstructor _mapConstructor;

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
        const PropertyFlag PropertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
        var properties = new PropertyDictionary(14, checkExistingKeys: false)
        {
            ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
            ["constructor"] = new PropertyDescriptor(_mapConstructor, PropertyFlag.NonEnumerable),
            ["clear"] = new PropertyDescriptor(new ClrFunction(Engine, "clear", Clear, 0, PropertyFlag.Configurable), PropertyFlags),
            ["delete"] = new PropertyDescriptor(new ClrFunction(Engine, "delete", Delete, 1, PropertyFlag.Configurable), PropertyFlags),
            ["entries"] = new PropertyDescriptor(new ClrFunction(Engine, "entries", Entries, 0, PropertyFlag.Configurable), PropertyFlags),
            ["forEach"] = new PropertyDescriptor(new ClrFunction(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), PropertyFlags),
            ["get"] = new PropertyDescriptor(new ClrFunction(Engine, "get", Get, 1, PropertyFlag.Configurable), PropertyFlags),
            ["getOrInsert"] = new PropertyDescriptor(new ClrFunction(Engine, "getOrInsert", GetOrInsert, 2, PropertyFlag.Configurable), PropertyFlags),
            ["getOrInsertComputed"] = new PropertyDescriptor(new ClrFunction(Engine, "getOrInsertComputed", GetOrInsertComputed, 2, PropertyFlag.Configurable), PropertyFlags),
            ["has"] = new PropertyDescriptor(new ClrFunction(Engine, "has", Has, 1, PropertyFlag.Configurable), PropertyFlags),
            ["keys"] = new PropertyDescriptor(new ClrFunction(Engine, "keys", Keys, 0, PropertyFlag.Configurable), PropertyFlags),
            ["set"] = new PropertyDescriptor(new ClrFunction(Engine, "set", Set, 2, PropertyFlag.Configurable), PropertyFlags),
            ["values"] = new PropertyDescriptor(new ClrFunction(Engine, "values", Values, 0, PropertyFlag.Configurable), PropertyFlags),
            ["size"] = new GetSetPropertyDescriptor(get: new ClrFunction(Engine, "get size", Size, 0, PropertyFlag.Configurable), set: null, PropertyFlag.Configurable)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(2)
        {
            [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(new ClrFunction(Engine, "iterator", Entries, 1, PropertyFlag.Configurable), PropertyFlags),
            [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("Map", false, false, true),
        };
        SetSymbols(symbols);
    }

    private JsValue Size(JsValue thisObject, JsCallArguments arguments)
    {
        AssertMapInstance(thisObject);
        return JsNumber.Create(0);
    }

    private JsValue Get(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        return map.Get(arguments.At(0));
    }

    private JsValue GetOrInsert(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        var key = arguments.At(0).CanonicalizeKeyedCollectionKey();
        var value = arguments.At(1);
        return map.GetOrInsert(key, value);
    }

    private JsValue GetOrInsertComputed(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        var key = arguments.At(0).CanonicalizeKeyedCollectionKey();
        var callbackfn = arguments.At(1).GetCallable(_realm);
        return map.GetOrInsertComputed(key, callbackfn);
    }

    private JsValue Clear(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        map.Clear();
        return Undefined;
    }

    private JsValue Delete(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        return map.Remove(arguments.At(0))
            ? JsBoolean.True
            : JsBoolean.False;
    }

    private JsValue Set(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        map.Set(arguments.At(0), arguments.At(1));
        return thisObject;
    }

    private JsValue Has(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        return map.Has(arguments.At(0))
            ? JsBoolean.True
            : JsBoolean.False;
    }

    private JsValue ForEach(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        var callbackfn = arguments.At(0);
        var thisArg = arguments.At(1);

        var callable = GetCallable(callbackfn);

        map.ForEach(callable, thisArg);

        return Undefined;
    }

    private ObjectInstance Entries(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        return map.Iterator();
    }

    private ObjectInstance Keys(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        return map.Keys();
    }

    private ObjectInstance Values(JsValue thisObject, JsCallArguments arguments)
    {
        var map = AssertMapInstance(thisObject);
        return map.Values();
    }

    private JsMap AssertMapInstance(JsValue thisObject)
    {
        if (thisObject is JsMap map)
        {
            return map;
        }

        Throw.TypeError(_realm, "object must be a Map");
        return default;
    }
}

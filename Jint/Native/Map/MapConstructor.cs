#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Map;

public sealed class MapConstructor : Constructor
{
    private static readonly JsString _functionName = new("Map");

    internal MapConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new MapPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(0, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    private MapPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
        var properties = new PropertyDictionary(1, checkExistingKeys: false) { ["groupBy"] = new(new ClrFunction(Engine, "groupBy", GroupBy, 2, PropertyFlag.Configurable), PropertyFlags), };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1) { [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunction(_engine, "get [Symbol.species]", Species, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable) };
        SetSymbols(symbols);
    }

    public JsMap Construct() => ConstructMap(this);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-map-iterable
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var map = ConstructMap(newTarget);

        if (arguments.Length > 0 && !arguments[0].IsNullOrUndefined())
        {
            var adder = ((ObjectInstance) map).Get("set");
            var iterator = arguments.At(0).GetIterator(_realm);

            IteratorProtocol.AddEntriesFromIterable(map, iterator, adder);
        }

        return map;
    }

    private JsMap ConstructMap(JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var map = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Map.PrototypeObject,
            static (Engine engine, Realm realm, object? _) => new JsMap(engine, realm));

        return map;
    }


    /// <summary>
    /// https://tc39.es/proposal-array-grouping/#sec-map.groupby
    /// </summary>
    private JsValue GroupBy(JsValue thisObject, JsCallArguments arguments)
    {
        var items = arguments.At(0);
        var callbackfn = arguments.At(1);
        var grouping = GroupByHelper.GroupBy(_engine, _realm, items, callbackfn, mapMode: true);
        var map = (JsMap) Construct(_realm.Intrinsics.Map);
        foreach (var pair in grouping)
        {
            map.Set(pair.Key, pair.Value);
        }

        return map;
    }

    private static JsValue Species(JsValue thisObject, JsCallArguments arguments)
    {
        return thisObject;
    }
}

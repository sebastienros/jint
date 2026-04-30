#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using Jint.Native.Function;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Map;

[JsObject]
public sealed partial class MapConstructor : Constructor
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
        CreateProperties_Generated();
        CreateSymbols_Generated();
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
            Throw.TypeError(_realm, $"Constructor {_nameDescriptor?.Value} requires 'new'");
        }

        if (ReferenceEquals(newTarget, this))
        {
            return new JsMap(_engine, _realm)
            {
                _prototype = PrototypeObject
            };
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
    [JsFunction(Length = 2)]
    private JsValue GroupBy(JsValue thisObject, JsValue items, JsValue callbackfn)
    {
        var grouping = GroupByHelper.GroupBy(_engine, _realm, items, callbackfn, mapMode: true);
        var map = (JsMap) Construct(_realm.Intrinsics.Map);
        foreach (var pair in grouping)
        {
            map.Set(pair.Key, pair.Value);
        }

        return map;
    }

    [JsSymbolAccessor("Species")]
    private static JsValue Species(JsValue thisObject)
    {
        return thisObject;
    }
}

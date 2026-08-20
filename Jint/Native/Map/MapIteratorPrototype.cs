using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Map;

/// <summary>
/// https://tc39.es/ecma262/#sec-%mapiteratorprototype%-object
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class MapIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString MapIteratorToStringTag = new("Map Iterator");

    internal MapIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    [JsFunction(Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal IteratorInstance ConstructEntryIterator(JsMap map)
    {
        var instance = new MapIterator(Engine, map, MapIteratorKind.KeyAndValue)
        {
            _prototype = this
        };

        return instance;
    }

    internal IteratorInstance ConstructKeyIterator(JsMap map)
    {
        var instance = new MapIterator(Engine, map, MapIteratorKind.Key)
        {
            _prototype = this
        };

        return instance;
    }

    internal IteratorInstance ConstructValueIterator(JsMap map)
    {
        var instance = new MapIterator(Engine, map, MapIteratorKind.Value)
        {
            _prototype = this
        };

        return instance;
    }

    private enum MapIteratorKind
    {
        Key,
        Value,
        KeyAndValue,
    }

    /// <summary>
    /// The closure of https://tc39.es/ecma262/#sec-createmapiterator, which walks [[MapData]] by index
    /// and re-reads its length after every yield. <see cref="KeyedCollectionCursor"/> is that index:
    /// deleted entries are tombstones, so a mutation between two steps cannot move the resume point.
    /// </summary>
    private sealed class MapIterator : IteratorInstance
    {
        private readonly KeyedCollectionData _data;
        private readonly MapIteratorKind _kind;
        private KeyedCollectionCursor _cursor;
        private bool _done;

        public MapIterator(Engine engine, JsMap map, MapIteratorKind kind) : base(engine)
        {
            _data = map._data;
            _kind = kind;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (!_done)
            {
                var slot = _data.Next(ref _cursor);
                if (slot >= 0)
                {
                    nextItem = _kind switch
                    {
                        MapIteratorKind.Key => IteratorResult.CreateValueIteratorPosition(_engine, _data.KeyAt(slot)!),
                        MapIteratorKind.Value => IteratorResult.CreateValueIteratorPosition(_engine, _data.ValueAt(slot)),
                        _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, _data.KeyAt(slot)!, _data.ValueAt(slot)),
                    };
                    return true;
                }

                _done = true;
            }

            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}

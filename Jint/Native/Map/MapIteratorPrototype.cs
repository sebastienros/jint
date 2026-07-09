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

    private sealed class MapIterator : IteratorInstance
    {
        private readonly JintOrderedDictionary<JsValue, JsValue> _map;
        private readonly MapIteratorKind _kind;
        private int _position;
        private JsValue? _lastKey;
        private bool _done;

        public MapIterator(Engine engine, JsMap map, MapIteratorKind kind) : base(engine)
        {
            _map = map._map;
            _kind = kind;
            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_done)
            {
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
                return false;
            }

            // Adjust position for mutations since last step
            if (_lastKey is not null)
            {
                if (_position > 0 && _position - 1 < _map.Count && ReferenceEquals(_map.GetKey(_position - 1), _lastKey))
                {
                    // Common fast path: lastKey still at expected position
                }
                else if (_map.ContainsKey(_lastKey))
                {
                    var newIndex = _map.IndexOf(_lastKey);
                    if (newIndex < _position - 1)
                    {
                        // Key moved backward (entries before it were deleted)
                        _position = newIndex + 1;
                    }
                    // else: key was deleted and re-added at end, keep position
                }
                else
                {
                    // Key deleted, entries shifted left
                    _position = System.Math.Max(0, _position - 1);
                }
            }

            if (_position < _map.Count)
            {
                var key = _map.GetKey(_position);
                _lastKey = key;
                _position++;
                nextItem = _kind switch
                {
                    MapIteratorKind.Key => IteratorResult.CreateValueIteratorPosition(_engine, key),
                    MapIteratorKind.Value => IteratorResult.CreateValueIteratorPosition(_engine, _map[key]),
                    _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, key, _map[key]),
                };
                return true;
            }

            _done = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}

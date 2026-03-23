using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Map;

/// <summary>
/// https://tc39.es/ecma262/#sec-%mapiteratorprototype%-object
/// </summary>
internal sealed class MapIteratorPrototype : IteratorPrototype
{
    internal MapIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype iteratorPrototype) : base(engine, realm, iteratorPrototype)
    {
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new(new ClrFunction(Engine, "next", Next, 0, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Map Iterator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    internal IteratorInstance ConstructEntryIterator(JsMap map)
    {
        var instance = new MapIterator(Engine, map)
        {
            _prototype = this
        };

        return instance;
    }

    internal IteratorInstance ConstructKeyIterator(JsMap map)
    {
        var instance = new IteratorInstance.EnumerableIterator(Engine, map._map.Keys)
        {
            _prototype = this
        };

        return instance;
    }

    internal IteratorInstance ConstructValueIterator(JsMap map)
    {
        var instance = new IteratorInstance.EnumerableIterator(Engine, map._map.Values)
        {
            _prototype = this
        };

        return instance;
    }

    private sealed class MapIterator : IteratorInstance
    {
        private readonly JintOrderedDictionary<JsValue, JsValue> _map;
        private int _position;
        private JsValue? _lastKey;
        private bool _done;

        public MapIterator(Engine engine, JsMap map) : base(engine)
        {
            _map = map._map;
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
                var value = _map[key];
                _lastKey = key;
                _position++;
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine, key, value);
                return true;
            }

            _done = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}

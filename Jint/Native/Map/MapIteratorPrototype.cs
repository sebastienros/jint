using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Map
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-%mapiteratorprototype%-object
    /// </summary>
    internal sealed class MapIteratorPrototype : IteratorPrototype
    {
        internal MapIteratorPrototype(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype) : base(engine, realm, "Map Iterator", objectPrototype)
        {
        }

        internal IteratorInstance ConstructEntryIterator(MapInstance map)
        {
            var instance = new MapIterator(Engine, map)
            {
                _prototype = this
            };

            return instance;
        }

        internal IteratorInstance ConstructKeyIterator(MapInstance map)
        {
            var instance = new IteratorInstance(Engine, map._map.Keys)
            {
                _prototype = this
            };

            return instance;
        }

        internal IteratorInstance ConstructValueIterator(MapInstance map)
        {
            var instance = new IteratorInstance(Engine, map._map.Values)
            {
                _prototype = this
            };

            return instance;
        }

        private sealed class MapIterator : IteratorInstance
        {
            private readonly MapInstance _map;

            private int _position;

            public MapIterator(Engine engine, MapInstance map) : base(engine)
            {
                _map = map;
                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_position < _map.GetSize())
                {
                    var key = _map._map.GetKey(_position);
                    var value = _map._map[key];

                    _position++;
                    nextItem = new KeyValueIteratorPosition(_engine, key, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done(_engine);
                return false;
            }
        }
    }
}
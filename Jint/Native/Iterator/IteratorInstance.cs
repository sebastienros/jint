using System.Collections.Generic;
using System.Linq;
using Jint.Native.Array;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator
{
    public class IteratorInstance : ObjectInstance, IIterator
    {
        private readonly IEnumerator<JsValue> _enumerable;

        public IteratorInstance(Engine engine)
            : this(engine, Enumerable.Empty<JsValue>())
        {
        }

        public IteratorInstance(
            Engine engine,
            IEnumerable<JsValue> enumerable) : base(engine, "Iterator")
        {
            _enumerable = enumerable.GetEnumerator();
        }

        public override object ToObject()
        {
            throw new System.NotImplementedException();
        }

        public override bool Equals(JsValue other)
        {
            return false;
        }

        public virtual ObjectInstance Next()
        {
            if (_enumerable.MoveNext())
            {
                return new ValueIteratorPosition(_engine, _enumerable.Current);
            }

            return ValueIteratorPosition.Done;
        }

        private class KeyValueIteratorPosition : ObjectInstance
        {
            internal static readonly ObjectInstance Done = new KeyValueIteratorPosition(null, null, null);

            public KeyValueIteratorPosition(Engine engine, JsValue key, JsValue value) : base(engine)
            {
                var done = ReferenceEquals(null, key) && ReferenceEquals(null, value);
                if (!done)
                {
                    var arrayInstance = engine.Array.ConstructFast(2);
                    arrayInstance.SetIndexValue(0, key, false);
                    arrayInstance.SetIndexValue(1, value, false);
                    SetOwnProperty("value", new PropertyDescriptor(arrayInstance, PropertyFlag.AllForbidden));
                }
                SetOwnProperty("done", new PropertyDescriptor(done, PropertyFlag.AllForbidden));
            }
        }

        private class ValueIteratorPosition : ObjectInstance
        {
            internal static readonly ObjectInstance Done = new KeyValueIteratorPosition(null, null, null);

            public ValueIteratorPosition(Engine engine, JsValue value) : base(engine)
            {
                var done = ReferenceEquals(null, value);
                if (!done)
                {
                    SetOwnProperty("value", new PropertyDescriptor(value, PropertyFlag.AllForbidden));
                }
                SetOwnProperty("done", new PropertyDescriptor(done, PropertyFlag.AllForbidden));
            }
        }

        public class MapIterator : IteratorInstance
        {
            private readonly MapInstance _map;
            private int _position;

            public MapIterator(Engine engine, MapInstance map) : base(engine)
            {
                _map = map;
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                if (_position < _map.GetSize())
                {
                    var entry = _map.GetEntry(_position);
                    _position++;
                    return new  KeyValueIteratorPosition(_engine, entry.Key, entry.Value);
                }

                return KeyValueIteratorPosition.Done;
            }
        }

        public class ArrayIterator : IteratorInstance
        {
            private readonly ArrayInstance _array;
            private uint _position;

            public ArrayIterator(Engine engine, ArrayInstance array) : base(engine)
            {
                _array = array;
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                if (_position < _array.GetLength())
                {
                    _array.TryGetValue(_position, out var value);
                    _position++;
                    return new  ValueIteratorPosition(_engine, value);
                }

                return KeyValueIteratorPosition.Done;
            }
        }
    }
}
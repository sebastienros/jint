using System.Collections.Generic;
using System.Linq;
using Jint.Native.Array;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Native.Set;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator
{
    internal class IteratorInstance : ObjectInstance, IIterator
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

        public void Return()
        {
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
                SetOwnProperty(
                    "done",
                    done ? PropertyDescriptor.AllForbiddenDescriptor.BooleanTrue : PropertyDescriptor.AllForbiddenDescriptor.BooleanFalse);
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
                    var key  = _map._map.GetKey(_position);
                    var value = _map._map[key];

                    _position++;
                    return new KeyValueIteratorPosition(_engine, key, value);
                }

                return KeyValueIteratorPosition.Done;
            }
        }

        public class ArrayLikeIterator : IteratorInstance
        {
            private readonly ArrayPrototype.ArrayOperations _array;
            private uint? _end;
            private uint _position;

            public ArrayLikeIterator(Engine engine, JsValue target) : base(engine)
            {
                if (!(target is ObjectInstance objectInstance))
                {
                    ExceptionHelper.ThrowTypeError(engine, "Target must be an object");
                    return;
                }

                _array = ArrayPrototype.ArrayOperations.For(objectInstance);
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                if (_end == null)
                {
                    _end = _array.GetLength();
                }

                if (_position < _end.Value)
                {
                    _array.TryGetValue(_position, out var value);
                    return new KeyValueIteratorPosition(_engine, _position++, value);
                }

                return KeyValueIteratorPosition.Done;
            }
        }

        public class SetIterator : IteratorInstance
        {
            private readonly SetInstance _set;
            private int _position;

            public SetIterator(Engine engine, SetInstance set) : base(engine)
            {
                _set = set;
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                if (_position < _set._set._list.Count)
                {
                    var value = _set._set[_position];
                    _position++;
                    return new  ValueIteratorPosition(_engine, value);
                }

                return KeyValueIteratorPosition.Done;
            }
        }

        public class SetEntryIterator : IteratorInstance
        {
            private readonly SetInstance _set;
            private int _position;

            public SetEntryIterator(Engine engine, SetInstance set) : base(engine)
            {
                _set = set;
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                if (_position < _set._set._list.Count)
                {
                    var value = _set._set[_position];
                    _position++;
                    return new  KeyValueIteratorPosition(_engine, value, value);
                }

                return KeyValueIteratorPosition.Done;
            }
        }

        public class ListIterator : IteratorInstance
        {
            private readonly List<JsValue> _values;
            private int _position;
            private bool _closed;

            public ListIterator(Engine engine, List<JsValue> values) : base(engine)
            {
                _values = values;
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                if (!_closed && _position < _values.Count)
                {
                    var value = _values[_position];
                    _position++;
                    return new  ValueIteratorPosition(_engine, value);
                }

                _closed = true;
                return ValueIteratorPosition.Done;
            }
        } 

        public class ArrayLikeKeyIterator : IteratorInstance
        {
            private readonly ArrayPrototype.ArrayOperations _operations;
            private uint _position;
            private bool _closed;

            public ArrayLikeKeyIterator(Engine engine, ObjectInstance objectInstance) : base(engine)
            {
                _operations = ArrayPrototype.ArrayOperations.For(objectInstance);
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                var length = _operations.GetLength();
                if (!_closed && _position < length)
                {
                    return new  ValueIteratorPosition(_engine, _position++);
                }

                _closed = true;
                return ValueIteratorPosition.Done;
            }
        }

        public class ArrayLikeValueIterator : IteratorInstance
        {
            private readonly ArrayPrototype.ArrayOperations _operations;
            private uint _position;
            private bool _closed;

            public ArrayLikeValueIterator(Engine engine, ObjectInstance objectInstance) : base(engine)
            {
                _operations = ArrayPrototype.ArrayOperations.For(objectInstance);
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                var length = _operations.GetLength();
                if (!_closed && _position < length)
                {
                    _operations.TryGetValue(_position++, out var value);
                    return new ValueIteratorPosition(_engine, value);
                }

                _closed = true;
                return ValueIteratorPosition.Done;
            }
        }

        internal class ObjectWrapper : IIterator
        {
            private readonly ObjectInstance _target;
            private readonly ICallable _callable;

            public ObjectWrapper(ObjectInstance target)
            {
                _target = target;
                _callable = (ICallable) target.Get("next");
            }

            public ObjectInstance Next()
            {
                return (ObjectInstance) _callable.Call(_target, Arguments.Empty);
            }

            public void Return()
            {
                if (_target.TryGetValue("return", out var func))
                {
                    ((ICallable) func).Call(_target, Arguments.Empty);
                }
            }
        }

        internal class StringIterator : IteratorInstance
        {
            private readonly string _str;
            private int _position;
            private bool _closed;

            public StringIterator(Engine engine, string str) : base(engine)
            {
                _str = str;
                _position = 0;
            }

            public override ObjectInstance Next()
            {
                var length = _str.Length;
                if (!_closed && _position < length)
                {
                    return new ValueIteratorPosition(_engine, _str[_position++]);
                }

                _closed = true;
                return ValueIteratorPosition.Done;
            }
        }
    }
}
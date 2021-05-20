using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Jint.Native.Array;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Set;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator
{
    public class IteratorInstance : ObjectInstance, IIterator
    {
        private readonly IEnumerator<JsValue> _enumerable;

        protected IteratorInstance(Engine engine)
            : this(engine, Enumerable.Empty<JsValue>())
        {
        }

        public IteratorInstance(
            Engine engine,
            IEnumerable<JsValue> enumerable) : base(engine, ObjectClass.Iterator)
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

        public virtual bool TryIteratorStep(out ObjectInstance nextItem)
        {
            if (_enumerable.MoveNext())
            {
                nextItem = new ValueIteratorPosition(_engine, _enumerable.Current);
                return true;
            }

            nextItem = ValueIteratorPosition.Done;
            return false;
        }

        public void Close(CompletionType completion)
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
                    SetProperty("value", new PropertyDescriptor(arrayInstance, PropertyFlag.AllForbidden));
                }
                SetProperty("done", done ? PropertyDescriptor.AllForbiddenDescriptor.BooleanTrue : PropertyDescriptor.AllForbiddenDescriptor.BooleanFalse);
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
                    SetProperty("value", new PropertyDescriptor(value, PropertyFlag.AllForbidden));
                }
                SetProperty("done", new PropertyDescriptor(done, PropertyFlag.AllForbidden));
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

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_position < _map.GetSize())
                {
                    var key  = _map._map.GetKey(_position);
                    var value = _map._map[key];

                    _position++;
                    nextItem = new KeyValueIteratorPosition(_engine, key, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        public class ArrayLikeIterator : IteratorInstance
        {
            private readonly ArrayOperations _array;
            private uint? _end;
            private uint _position;

            public ArrayLikeIterator(Engine engine, JsValue target) : base(engine)
            {
                if (!(target is ObjectInstance objectInstance))
                {
                    ExceptionHelper.ThrowTypeError(engine, "Target must be an object");
                    return;
                }

                _array = ArrayOperations.For(objectInstance);
                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_end == null)
                {
                    _end = _array.GetLength();
                }

                if (_position < _end.Value)
                {
                    _array.TryGetValue(_position, out var value);
                    nextItem = new KeyValueIteratorPosition(_engine, _position++, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done;
                return false;
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

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_position < _set._set._list.Count)
                {
                    var value = _set._set[_position];
                    _position++;
                    nextItem = new  ValueIteratorPosition(_engine, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done;
                return false;
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

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_position < _set._set._list.Count)
                {
                    var value = _set._set[_position];
                    _position++;
                    nextItem = new  KeyValueIteratorPosition(_engine, value, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done;
                return false;
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

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (!_closed && _position < _values.Count)
                {
                    var value = _values[_position];
                    _position++;
                    nextItem = new  ValueIteratorPosition(_engine, value);
                    return true;
                }

                _closed = true;
                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        } 

        public class ArrayLikeKeyIterator : IteratorInstance
        {
            private readonly ArrayOperations _operations;
            private uint _position;
            private bool _closed;

            public ArrayLikeKeyIterator(Engine engine, ObjectInstance objectInstance) : base(engine)
            {
                _operations = ArrayOperations.For(objectInstance);
                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                var length = _operations.GetLength();
                if (!_closed && _position < length)
                {
                    nextItem = new  ValueIteratorPosition(_engine, _position++);
                    return true;
                }

                _closed = true;
                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        public class ArrayLikeValueIterator : IteratorInstance
        {
            private readonly ArrayOperations _operations;
            private uint _position;
            private bool _closed;

            public ArrayLikeValueIterator(Engine engine, ObjectInstance objectInstance) : base(engine)
            {
                _operations = ArrayOperations.For(objectInstance);
                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                var length = _operations.GetLength();
                if (!_closed && _position < length)
                {
                    _operations.TryGetValue(_position++, out var value);
                    nextItem = new ValueIteratorPosition(_engine, value);
                    return true;
                }

                _closed = true;
                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        internal class ObjectIterator : IIterator
        {
            private readonly ObjectInstance _target;
            private readonly ICallable _nextMethod;

            public ObjectIterator(ObjectInstance target)
            {
                _target = target;
                _nextMethod = target.Get(CommonProperties.Next, target) as ICallable
                            ?? ExceptionHelper.ThrowTypeError<ICallable>(target.Engine);
            }

            public bool TryIteratorStep(out ObjectInstance result)
            {
                result = IteratorNext();

                var done = result.Get(CommonProperties.Done);
                if (!done.IsUndefined() && TypeConverter.ToBoolean(done))
                {
                    return false;
                }

                return true;
            }

            private ObjectInstance IteratorNext()
            {
                var jsValue = _nextMethod.Call(_target, Arguments.Empty);
                return jsValue as ObjectInstance ?? ExceptionHelper.ThrowTypeError<ObjectInstance>(_target.Engine, "Iterator result " + jsValue + " is not an object");
            }

            public void Close(CompletionType completion)
            {
                if (!_target.TryGetValue(CommonProperties.Return, out var func)
                    || func.IsNullOrUndefined())
                {
                    return;
                }

                var callable = func as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(_target.Engine, func + " is not a function");

                var innerResult = Undefined;
                try
                {
                    innerResult = callable.Call(_target, Arguments.Empty);
                }
                catch
                {
                    if (completion != CompletionType.Throw)
                    {
                        throw;
                    }
                }
                if (completion != CompletionType.Throw && !innerResult.IsObject())
                {
                    ExceptionHelper.ThrowTypeError(_target.Engine);
                }
            }
        }

        internal class StringIterator : IteratorInstance
        {
            private readonly TextElementEnumerator _iterator;

            public StringIterator(Engine engine, string str) : base(engine)
            {
                _iterator = StringInfo.GetTextElementEnumerator(str);
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_iterator.MoveNext())
                {
                    nextItem = new ValueIteratorPosition(_engine, (string) _iterator.Current);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }
        
        internal class RegExpStringIterator : IteratorInstance
        {
            private readonly RegExpInstance _iteratingRegExp;
            private readonly string _s;
            private readonly bool _global;
            private readonly bool _unicode;

            private bool _done;

            public RegExpStringIterator(Engine engine, ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode) : base(engine)
            {
                if (!(iteratingRegExp is RegExpInstance r))
                {
                    ExceptionHelper.ThrowTypeError(engine);
                    return;
                }
                
                _iteratingRegExp = r;
                _s = iteratedString;
                _global = global;
                _unicode = unicode;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_done)
                {
                    nextItem = new IteratorResult(_engine, Undefined, JsBoolean.True);
                    return false;
                }
                
                var match  = RegExpPrototype.RegExpExec(_iteratingRegExp, _s);
                if (match.IsNull())
                {
                    _done = true;
                    nextItem = new IteratorResult(_engine, Undefined, JsBoolean.True);
                    return false;
                }

                if (_global)
                {
                    var macthStr = TypeConverter.ToString(match.Get(JsString.NumberZeroString));
                    if (macthStr == "")
                    {
                        var thisIndex = TypeConverter.ToLength(_iteratingRegExp.Get(RegExpInstance.PropertyLastIndex));
                        var nextIndex = thisIndex + 1;
                        _iteratingRegExp.Set(RegExpInstance.PropertyLastIndex, nextIndex, true);
                    }
                }
                else
                {
                    _done = true;
                }

                nextItem = new IteratorResult(_engine, match, JsBoolean.False);
                return false;
            }
        }
    }
}
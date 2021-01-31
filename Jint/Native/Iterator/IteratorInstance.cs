using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Jint.Native.Array;
using Jint.Native.Map;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Set;
using Jint.Native.TypedArray;
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

        private ObjectInstance CreateIterResultObject(JsValue value, bool done)
        {
            var obj = _engine.Realm.Intrinsics.Object.Construct(2);
            obj.SetDataProperty("value", value);
            obj.SetDataProperty("done", done);
            return obj;
        }

        private sealed class KeyValueIteratorPosition : ObjectInstance
        {
            internal static readonly ObjectInstance Done = new KeyValueIteratorPosition(null, null, null);

            public KeyValueIteratorPosition(Engine engine, JsValue key, JsValue value) : base(engine)
            {
                var done = ReferenceEquals(null, key) && ReferenceEquals(null, value);
                if (!done)
                {
                    var arrayInstance = engine.Realm.Intrinsics.Array.ConstructFast(2);
                    arrayInstance.SetIndexValue(0, key, false);
                    arrayInstance.SetIndexValue(1, value, false);
                    SetProperty("value", new PropertyDescriptor(arrayInstance, PropertyFlag.AllForbidden));
                }
                SetProperty("done", done ? PropertyDescriptor.AllForbiddenDescriptor.BooleanTrue : PropertyDescriptor.AllForbiddenDescriptor.BooleanFalse);
            }
        }

        private sealed class ValueIteratorPosition : ObjectInstance
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

        public sealed class MapIterator : IteratorInstance
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

                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        public sealed class SetEntryIterator : IteratorInstance
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
                    nextItem = new KeyValueIteratorPosition(_engine, value, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        public sealed class ListIterator : IteratorInstance
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
                    nextItem = new ValueIteratorPosition(_engine, value);
                    return true;
                }

                _closed = true;
                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        internal sealed class ArrayLikeIterator : IteratorInstance
        {
            private readonly ArrayIteratorType _kind;
            private readonly TypedArrayInstance _typedArray;
            private readonly ArrayOperations _operations;
            private uint _position;
            private bool _closed;

            public ArrayLikeIterator(Engine engine, ObjectInstance objectInstance, ArrayIteratorType kind) : base(engine)
            {
                _kind = kind;
                _typedArray = objectInstance as TypedArrayInstance;
                if (_typedArray is null)
                {
                    _operations = ArrayOperations.For(objectInstance);
                }
                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                uint len;
                if (_typedArray is not null)
                {
                    _typedArray._viewedArrayBuffer.AssertNotDetached();
                    len = _typedArray.Length;
                }
                else
                {
                    len = _operations.GetLength();
                }

                if (!_closed && _position < len)
                {
                    JsValue value;
                    if (_typedArray is not null)
                    {
                        nextItem = _kind switch
                        {
                            ArrayIteratorType.Key => new ValueIteratorPosition(_engine, _position),
                            ArrayIteratorType.Value => new ValueIteratorPosition(_engine, _typedArray[(int) _position]),
                            _ => new KeyValueIteratorPosition(_engine, _position, _typedArray[(int) _position])
                        };
                    }
                    else
                    {
                        _operations.TryGetValue(_position, out value);
                        if (_kind == ArrayIteratorType.Key)
                        {
                            nextItem = new ValueIteratorPosition(_engine, _position);
                        }
                        else if (_kind == ArrayIteratorType.Value)
                        {
                            nextItem = new ValueIteratorPosition(_engine, value);
                        }
                        else
                        {
                            nextItem = new KeyValueIteratorPosition(_engine, _position, value);
                        }
                    }

                    _position++;
                    return true;
                }

                _closed = true;
                nextItem = KeyValueIteratorPosition.Done;
                return false;
            }
        }

        internal sealed class ObjectIterator : IIterator
        {
            private readonly ObjectInstance _target;
            private readonly ICallable _nextMethod;

            public ObjectIterator(ObjectInstance target)
            {
                _target = target;
                _nextMethod = target.Get(CommonProperties.Next, target) as ICallable;
                if (_nextMethod is null)
                {
                    ExceptionHelper.ThrowTypeError(target.Engine.Realm);
                }
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
                var instance = jsValue as ObjectInstance;
                if (instance is null)
                {
                    ExceptionHelper.ThrowTypeError(_target.Engine.Realm, "Iterator result " + jsValue + " is not an object");
                }

                return instance;
            }

            public void Close(CompletionType completion)
            {
                if (!_target.TryGetValue(CommonProperties.Return, out var func)
                    || func.IsNullOrUndefined())
                {
                    return;
                }

                var callable = func as ICallable;
                if (callable is null)
                {
                    ExceptionHelper.ThrowTypeError(_target.Engine.Realm, func + " is not a function");
                }

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
                    ExceptionHelper.ThrowTypeError(_target.Engine.Realm);
                }
            }
        }

        internal sealed class StringIterator : IteratorInstance
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

        internal sealed class RegExpStringIterator : IteratorInstance
        {
            private readonly RegExpInstance _iteratingRegExp;
            private readonly string _s;
            private readonly bool _global;
            private readonly bool _unicode;

            private bool _done;

            public RegExpStringIterator(Engine engine, ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode) : base(engine)
            {
                var r = iteratingRegExp as RegExpInstance;
                if (r is null)
                {
                    ExceptionHelper.ThrowTypeError(engine.Realm);
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
                    nextItem = CreateIterResultObject(Undefined, true);
                    return false;
                }

                var match = RegExpPrototype.RegExpExec(_iteratingRegExp, _s);
                if (match.IsNull())
                {
                    _done = true;
                    nextItem = CreateIterResultObject(Undefined, true);
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

                nextItem = CreateIterResultObject(match, false);
                return false;
            }
        }
    }
}

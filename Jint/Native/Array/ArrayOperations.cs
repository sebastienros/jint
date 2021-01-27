using System.Collections;
using System.Collections.Generic;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Array
{
    internal abstract class ArrayOperations : IEnumerable<JsValue>
    {
        protected internal const ulong MaxArrayLength = 4294967295;
        protected internal const ulong MaxArrayLikeLength = NumberConstructor.MaxSafeInteger;

        public static ArrayOperations For(ObjectInstance instance)
        {
            if (instance is ArrayInstance arrayInstance)
            {
                return new ArrayInstanceOperations(arrayInstance);
            }

            return new ObjectInstanceOperations(instance);
        }

        public static ArrayOperations For(Engine engine, JsValue thisObj)
        {
            var instance = TypeConverter.ToObject(engine, thisObj);
            return For(instance);
        }

        public abstract ObjectInstance Target { get; }

        public abstract ulong GetSmallestIndex(ulong length);

        public abstract uint GetLength();

        public abstract ulong GetLongLength();

        public abstract void SetLength(ulong length);

        public abstract void EnsureCapacity(ulong capacity);

        public abstract JsValue Get(ulong index);

        public virtual JsValue[] GetAll(Types elementTypes)
        {
            var n = (int) GetLength();
            var jsValues = new JsValue[n];
            for (uint i = 0; i < (uint) jsValues.Length; i++)
            {
                var jsValue = Get(i);
                if ((jsValue.Type & elementTypes) == 0)
                {
                    ExceptionHelper.ThrowTypeErrorNoEngine<object>("invalid type");
                }

                jsValues[i] = jsValue;
            }

            return jsValues;
        }

        public abstract bool TryGetValue(ulong index, out JsValue value);

        public bool HasProperty(ulong index) => Target.HasProperty(index);

        public abstract void CreateDataPropertyOrThrow(ulong index, JsValue value);

        public abstract void Set(ulong index, JsValue value, bool updateLength, bool throwOnError);

        public abstract void DeletePropertyOrThrow(ulong index);

        internal ArrayLikeIterator GetEnumerator() => new ArrayLikeIterator(this);

        IEnumerator<JsValue> IEnumerable<JsValue>.GetEnumerator() => new ArrayLikeIterator(this);

        IEnumerator IEnumerable.GetEnumerator() => new ArrayLikeIterator(this);

        internal sealed class ArrayLikeIterator : IEnumerator<JsValue>
        {
            private readonly ArrayOperations _obj;
            private ulong _current;
            private bool _initialized;
            private readonly uint _length;

            public ArrayLikeIterator(ArrayOperations obj)
            {
                _obj = obj;
                _length = obj.GetLength();

                Reset();
            }

            public JsValue Current 
            {
                get 
                {
                    if (!_initialized)
                    {
                        return null;
                    }
                    else
                    {
                        return _obj.TryGetValue(_current, out var temp) ? temp : null;
                    }
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (!_initialized)
                {
                    _initialized = true;
                }
                else
                {
                    _current++;
                }

                return _current < _length;
            }

            public void Reset()
            {
                _initialized = false;
                _current = 0;
            }
        }

        private sealed class ObjectInstanceOperations : ArrayOperations<ObjectInstance>
        {
            public ObjectInstanceOperations(ObjectInstance target) : base(target)
            {
            }

            private double GetIntegerLength()
            {
                var descValue = _target.Get(CommonProperties.Length, _target);
                if (!ReferenceEquals(descValue, null))
                {
                    return TypeConverter.ToInteger(descValue);
                }

                return 0;
            }

            public override ulong GetSmallestIndex(ulong length)
            {
                // there are some evil tests that iterate a lot with unshift..
                if (_target.Properties == null)
                {
                    return 0;
                }

                var min = length;
                foreach (var entry in _target.Properties)
                {
                    if (ulong.TryParse(entry.Key.ToString(), out var index))
                    {
                        min = System.Math.Min(index, min);
                    }
                }

                if (_target.Prototype?.Properties != null)
                {
                    foreach (var entry in _target.Prototype.Properties)
                    {
                        if (ulong.TryParse(entry.Key.ToString(), out var index))
                        {
                            min = System.Math.Min(index, min);
                        }
                    }
                }

                return min;
            }

            public override uint GetLength()
            {
                var integerLength = GetIntegerLength();
                return (uint) (integerLength >= 0 ? integerLength : 0);
            }

            public override ulong GetLongLength()
            {
                var integerLength = GetIntegerLength();
                if (integerLength <= 0)
                {
                    return 0;
                }

                return (ulong) System.Math.Min(integerLength, MaxArrayLikeLength);
            }

            public override void SetLength(ulong length)
            {
                _target.Set(CommonProperties.Length, length, true);
            }

            public override void EnsureCapacity(ulong capacity)
            {
            }

            public override JsValue Get(ulong index)
            {
                return _target.Get(JsString.Create(index), _target);
            }

            public override bool TryGetValue(ulong index, out JsValue value)
            {
                var propertyName = JsString.Create(index);
                var property = _target.GetProperty(propertyName);
                var kPresent = property != PropertyDescriptor.Undefined;
                value = kPresent ? _target.UnwrapJsValue(property) : JsValue.Undefined;
                return kPresent;
            }

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            {
                _target.CreateDataPropertyOrThrow(JsString.Create(index), value);
            }

            public override void Set(ulong index, JsValue value, bool updateLength, bool throwOnError)
            {
                _target.Set(JsString.Create(index), value, throwOnError);
            }

            public override void DeletePropertyOrThrow(ulong index)
            {
                _target.DeletePropertyOrThrow(JsString.Create(index));
            }
        }

        private sealed class ArrayInstanceOperations : ArrayOperations<ArrayInstance>
        {
            public ArrayInstanceOperations(ArrayInstance target) : base(target)
            {
            }

            public override ulong GetSmallestIndex(ulong length)
            {
                return _target.GetSmallestIndex();
            }

            public override uint GetLength()
            {
                return (uint) ((JsNumber) _target._length._value)._value;
            }

            public override ulong GetLongLength()
            {
                return (ulong) ((JsNumber) _target._length._value)._value;
            }

            public override void SetLength(ulong length)
            {
                _target.Set(CommonProperties.Length, length, true);
            }

            public override void EnsureCapacity(ulong capacity)
            {
                _target.EnsureCapacity((uint) capacity);
            }

            public override bool TryGetValue(ulong index, out JsValue value)
            {
                // array max size is uint
                return _target.TryGetValue((uint) index, out value);
            }

            public override JsValue Get(ulong index)
            {
                return _target.Get((uint) index);
            }

            public override JsValue[] GetAll(Types elementTypes)
            {
                var n = _target.GetLength();

                if (_target._dense == null || _target._dense.Length < n)
                {
                    return base.GetAll(elementTypes);
                }

                // optimized
                var jsValues = new JsValue[n];
                for (uint i = 0; i < (uint) jsValues.Length; i++)
                {
                    var prop = _target._dense[i] ?? PropertyDescriptor.Undefined;
                    if (prop == PropertyDescriptor.Undefined)
                    {
                        prop = _target.Prototype?.GetProperty(i) ?? PropertyDescriptor.Undefined;
                    }

                    var jsValue = _target.UnwrapJsValue(prop);
                    if ((jsValue.Type & elementTypes) == 0)
                    {
                        ExceptionHelper.ThrowTypeErrorNoEngine<object>("invalid type");
                    }

                    jsValues[i] = jsValue;
                }

                return jsValues;
            }

            public override void DeletePropertyOrThrow(ulong index)
            {
                _target.DeletePropertyOrThrow((uint) index);
            }

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            {
                _target.SetIndexValue((uint) index, value, updateLength: false);
            }

            public override void Set(ulong index, JsValue value, bool updateLength, bool throwOnError)
            {
                _target.SetIndexValue((uint) index, value, updateLength);
            }
        }
    }

    /// <summary>
    ///     Adapter to use optimized array operations when possible.
    ///     Gaps the difference between ArgumentsInstance and ArrayInstance.
    /// </summary>
    internal abstract class ArrayOperations<T> : ArrayOperations where T : ObjectInstance
    {
        protected readonly T _target;

        protected ArrayOperations(T target)
        {
            _target = target;
        }

        public override ObjectInstance Target => _target;
    }
}
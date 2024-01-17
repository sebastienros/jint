using System.Collections;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;

namespace Jint.Native.Array
{
    internal abstract class ArrayOperations : IEnumerable<JsValue>
    {
        protected internal const ulong MaxArrayLength = 4294967295;
        protected internal const ulong MaxArrayLikeLength = NumberConstructor.MaxSafeInteger;

        public static ArrayOperations For(Realm realm, JsValue value, bool forWrite)
        {
            if (!forWrite)
            {
                if (value.IsString())
                {
                    return new JsStringOperations(realm, (JsString) value);
                }

                if (value is StringInstance stringInstance)
                {
                    return new JsStringOperations(realm, stringInstance);
                }

                if (value is JsArray { CanUseFastAccess: true } array && array.Length <= (array._dense?.Length ?? -1))
                {
                    return new ArrayReadOperations(array);
                }
            }

            return For(TypeConverter.ToObject(realm, value), forWrite);
        }

        public static ArrayOperations For(ObjectInstance instance, bool forWrite)
        {
            if (instance is JsArray { CanUseFastAccess: true } arrayInstance)
            {
                return new JsArrayOperations(arrayInstance);
            }

            if (instance is JsTypedArray typedArrayInstance)
            {
                return new JsTypedArrayOperations(typedArrayInstance);
            }

            return new ObjectOperations(instance);
        }

        public abstract ObjectInstance Target { get; }

        public abstract ulong GetSmallestIndex(ulong length);

        public abstract uint GetLength();

        public abstract ulong GetLongLength();

        public abstract void SetLength(ulong length);

        public abstract void EnsureCapacity(ulong capacity);

        public abstract JsValue Get(ulong index);

        public virtual JsValue[] GetAll(
            Types elementTypes = Types.Undefined | Types.Null | Types.Boolean | Types.String | Types.Symbol | Types.Number | Types.Object,
            bool skipHoles = false)
        {
            uint writeIndex = 0;
            var n = (int) GetLength();
            var jsValues = new JsValue[n];
            for (uint i = 0; i < (uint) jsValues.Length; i++)
            {
                var jsValue = skipHoles && !HasProperty(i) ? JsValue.Undefined : Get(i);
                if ((jsValue.Type & elementTypes) == Types.Empty)
                {
                    ExceptionHelper.ThrowTypeErrorNoEngine("invalid type");
                }

                jsValues[writeIndex++] = jsValue;
            }

            return jsValues;
        }

        public abstract bool TryGetValue(ulong index, out JsValue value);

        public abstract bool HasProperty(ulong index);

        public abstract void CreateDataPropertyOrThrow(ulong index, JsValue value);

        public abstract void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true);

        public abstract void DeletePropertyOrThrow(ulong index);

        public ArrayLikeIterator GetEnumerator() => new ArrayLikeIterator(this);

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
                    return !_initialized
                        ? JsValue.Undefined
                        : _obj.TryGetValue(_current, out var temp) ? temp : JsValue.Undefined;
                }
            }

            object? IEnumerator.Current => Current;

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

        private sealed class ObjectOperations : ArrayOperations<ObjectInstance>
        {
            public ObjectOperations(ObjectInstance target) : base(target)
            {
            }

            private double GetIntegerLength()
            {
                var descValue = _target.Get(CommonProperties.Length);
                if (!ReferenceEquals(descValue, null))
                {
                    return TypeConverter.ToInteger(descValue);
                }

                return 0;
            }

            public override ulong GetSmallestIndex(ulong length)
            {
                return _target.GetSmallestIndex(length);
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
                => _target.Set(CommonProperties.Length, length, true);

            public override void EnsureCapacity(ulong capacity)
            {
            }

            public override JsValue Get(ulong index)
                => _target.Get(JsString.Create(index));

            public override bool TryGetValue(ulong index, out JsValue value)
            {
                var propertyName = JsString.Create(index);
                var kPresent = _target.HasProperty(propertyName);
                value = kPresent ? _target.Get(propertyName) : JsValue.Undefined;
                return kPresent;
            }

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
                => _target.CreateDataPropertyOrThrow(JsString.Create(index), value);

            public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
                => _target.Set(JsString.Create(index), value, throwOnError);

            public override void DeletePropertyOrThrow(ulong index)
                => _target.DeletePropertyOrThrow(JsString.Create(index));

            public override bool HasProperty(ulong index) => Target.HasProperty(index);
        }

        private sealed class JsArrayOperations : ArrayOperations<JsArray>
        {
            public JsArrayOperations(JsArray target) : base(target)
            {
            }

            public override ulong GetSmallestIndex(ulong length)
                => _target.GetSmallestIndex();

            public override uint GetLength()
                => (uint) ((JsNumber) _target._length!._value!)._value;

            public override ulong GetLongLength()
                => (ulong) ((JsNumber) _target._length!._value!)._value;

            public override void SetLength(ulong length)
                => _target.SetLength(length);

            public override void EnsureCapacity(ulong capacity)
                => _target.EnsureCapacity((uint) capacity);

            public override bool TryGetValue(ulong index, out JsValue value)
                // array max size is uint
                => _target.TryGetValue((uint) index, out value);

            public override JsValue Get(ulong index) => _target.Get((uint) index);

            public override JsValue[] GetAll(Types elementTypes = Types.Undefined | Types.Null | Types.Boolean | Types.String | Types.Symbol | Types.Number | Types.Object, bool skipHoles = false)
            {
                var n = _target.GetLength();

                if (_target._dense == null || _target._dense.Length < n)
                {
                    return base.GetAll(elementTypes);
                }

                // optimized
                uint writeIndex = 0;
                var jsValues = new JsValue[n];
                for (uint i = 0; i < (uint) jsValues.Length; i++)
                {
                    var value = _target._dense[i];
                    if (value is null)
                    {
                        value = _target.Prototype?.Get(i) ?? JsValue.Undefined;
                    }

                    if ((value.Type & elementTypes) == Types.Empty)
                    {
                        ExceptionHelper.ThrowTypeErrorNoEngine("invalid type");
                    }

                    jsValues[writeIndex++] = (JsValue?) value ?? JsValue.Undefined;
                }

                return jsValues;
            }

            public override void DeletePropertyOrThrow(ulong index)
                => _target.DeletePropertyOrThrow((uint) index);

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
                => _target.SetIndexValue((uint) index, value, updateLength: false);

            public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
                => _target.SetIndexValue((uint) index, value, updateLength);

            public override bool HasProperty(ulong index) => _target.HasProperty(index);
        }

        private sealed class JsTypedArrayOperations : ArrayOperations
        {
            private readonly JsTypedArray _target;

            public JsTypedArrayOperations(JsTypedArray target)
            {
                _target = target;
            }

            public override ObjectInstance Target => _target;

            public override ulong GetSmallestIndex(ulong length) => 0;

            public override uint GetLength()
            {
                if (!_target.IsConcatSpreadable)
                {
                    return _target.GetLength();
                }

                var descValue = _target.Get(CommonProperties.Length);
                if (!ReferenceEquals(descValue, null))
                {
                    return (uint) TypeConverter.ToInteger(descValue);
                }

                return 0;
            }

            public override ulong GetLongLength() => GetLength();

            public override void SetLength(ulong length)
            {
            }

            public override void EnsureCapacity(ulong capacity)
            {
            }

            public override JsValue Get(ulong index) => _target[(int) index];

            public override bool TryGetValue(ulong index, out JsValue value)
            {
                if (index < _target.GetLength())
                {
                    value = _target[(int) index];
                    return true;
                }

                value = JsValue.Undefined;
                return false;
            }

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
                => _target.CreateDataPropertyOrThrow(index, value);

            public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
                => _target[(int) index] = value;

            public override void DeletePropertyOrThrow(ulong index)
                => _target.DeletePropertyOrThrow(index);

            public override bool HasProperty(ulong index) => _target.HasProperty(index);
        }

                private sealed class JsStringOperations : ArrayOperations
        {
            private readonly Realm _realm;
            private readonly JsString _target;
            private ObjectInstance? _wrappedTarget;

            public JsStringOperations(Realm realm, JsString target)
            {
                _realm = realm;
                _target = target;
            }

            public JsStringOperations(Realm realm, StringInstance stringInstance) : this(realm, stringInstance.StringData)
            {
                _wrappedTarget = stringInstance;
            }

            public override ObjectInstance Target => _wrappedTarget ??= _realm.Intrinsics.String.Construct(_target);

            public override ulong GetSmallestIndex(ulong length) => 0;

            public override uint GetLength() => (uint) _target.Length;

            public override ulong GetLongLength() => GetLength();

            public override void SetLength(ulong length) => throw new NotSupportedException();

            public override void EnsureCapacity(ulong capacity)
            {
            }

            public override JsValue Get(ulong index) => index < (ulong) _target.Length ? _target[(int) index] : JsValue.Undefined;

            public override bool TryGetValue(ulong index, out JsValue value)
            {
                if (index < (ulong) _target.Length)
                {
                    value = _target[(int) index];
                    return true;
                }

                value = JsValue.Undefined;
                return false;
            }

            public override bool HasProperty(ulong index) => index < (ulong) _target.Length;

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value) => throw new NotSupportedException();

            public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true) => throw new NotSupportedException();

            public override void DeletePropertyOrThrow(ulong index) => throw new NotSupportedException();
        }

        private sealed class ArrayReadOperations : ArrayOperations
        {
            private readonly JsArray _target;
            private readonly JsValue?[] _data;
            private readonly uint _length;

            public ArrayReadOperations(JsArray target)
            {
                _target = target;
                _data = target._dense ?? System.Array.Empty<JsValue>();
                _length = target.Length;
            }

            public override ObjectInstance Target => _target;

            public override ulong GetSmallestIndex(ulong length) => 0;

            public override uint GetLength() => _length;

            public override ulong GetLongLength() => _length;

            public override void SetLength(ulong length) => throw new NotSupportedException();

            public override void EnsureCapacity(ulong capacity)
            {
            }

            public override JsValue Get(ulong index) => (index < (ulong) _data.Length ? _data[(int) index] : JsValue.Undefined) ?? JsValue.Undefined;

            public override bool TryGetValue(ulong index, out JsValue value)
            {
                if (index < _length)
                {
                    value = _data[(int) index]!;
                    return value is not null;
                }

                value = JsValue.Undefined;
                return false;
            }

            public override bool HasProperty(ulong index) => index < _length && _data[index] is not null;

            public override void CreateDataPropertyOrThrow(ulong index, JsValue value) => throw new NotSupportedException();

            public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true) => throw new NotSupportedException();

            public override void DeletePropertyOrThrow(ulong index) => throw new NotSupportedException();
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

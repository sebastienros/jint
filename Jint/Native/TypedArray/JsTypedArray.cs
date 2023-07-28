using System.Runtime.CompilerServices;
using Esprima;
using Jint.Native.ArrayBuffer;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray
{
    public sealed class JsTypedArray : ObjectInstance
    {
        internal readonly TypedArrayContentType _contentType;
        internal readonly TypedArrayElementType _arrayElementType;
        internal JsArrayBuffer _viewedArrayBuffer;
        internal uint _byteLength;
        internal int _byteOffset;
        private readonly Intrinsics _intrinsics;
        internal uint _arrayLength;

        internal JsTypedArray(
            Engine engine,
            Intrinsics intrinsics,
            TypedArrayElementType type,
            uint length) : base(engine)
        {
            _intrinsics = intrinsics;
            _viewedArrayBuffer = new JsArrayBuffer(engine, 0);

            _arrayElementType = type;
            _contentType = type != TypedArrayElementType.BigInt64 && type != TypedArrayElementType.BigUint64
                ? TypedArrayContentType.Number
                : TypedArrayContentType.BigInt;

            _arrayLength = length;
        }

        public JsValue this[int index]
        {
            get => IntegerIndexedElementGet(index);
            set => IntegerIndexedElementSet(index, value);
        }

        public JsValue this[uint index]
        {
            get => IntegerIndexedElementGet(index);
            set => IntegerIndexedElementSet(index, value);
        }

        public override uint Length => _viewedArrayBuffer.IsDetachedBuffer ? 0 : _arrayLength;

        internal override bool IsIntegerIndexedArray => true;

        /// <summary>
        /// https://tc39.es/ecma262/#sec-allocatetypedarraybuffer
        /// </summary>
        internal void AllocateTypedArrayBuffer(ulong len)
        {
            var elementSize = _arrayElementType.GetElementSize();
            var byteLength = elementSize * len;

            var data = _intrinsics.ArrayBuffer.AllocateArrayBuffer(_intrinsics.ArrayBuffer, byteLength);

            _byteLength = (uint) byteLength;
            _arrayLength = (uint) len;
            _viewedArrayBuffer = data;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HasProperty(long numericIndex)
        {
            return IsValidIntegerIndex(numericIndex);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-hasproperty-p
        /// </summary>
        public override bool HasProperty(JsValue property)
        {
            if (property.IsString())
            {
                var numericIndex = TypeConverter.CanonicalNumericIndexString(property);
                if (numericIndex is not null)
                {
                    return IsValidIntegerIndex(numericIndex.Value);
                }
            }

            return base.HasProperty(property);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-getownproperty-p
        /// </summary>
        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            var numericIndex = TypeConverter.CanonicalNumericIndexString(property);
            if (numericIndex is not null)
            {
                var value = IntegerIndexedElementGet(numericIndex.Value);
                if (value.IsUndefined())
                {
                    return PropertyDescriptor.Undefined;
                }

                return new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
            }

            return base.GetOwnProperty(property);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-get-p-receiver
        /// </summary>
        public override JsValue Get(JsValue property, JsValue receiver)
        {
            var numericIndex = TypeConverter.CanonicalNumericIndexString(property);
            if (numericIndex is not null)
            {
                return IntegerIndexedElementGet(numericIndex.Value);
            }

            return base.Get(property, receiver);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-set-p-v-receiver
        /// </summary>
        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            var numericIndex = TypeConverter.CanonicalNumericIndexString(property);
            if (numericIndex is not null)
            {
                IntegerIndexedElementSet(numericIndex.Value, value);
                return true;
            }

            return base.Set(property, value, receiver);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-defineownproperty-p-desc
        /// </summary>
        public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (property.IsString())
            {
                var numericIndex = TypeConverter.CanonicalNumericIndexString(property);
                if (numericIndex is not null)
                {
                    if (!IsValidIntegerIndex(numericIndex.Value))
                    {
                        return false;
                    }

                    if (desc.ConfigurableSet && !desc.Configurable)
                    {
                        return false;
                    }

                    if (desc.EnumerableSet && !desc.Enumerable)
                    {
                        return false;
                    }

                    if (desc.IsAccessorDescriptor())
                    {
                        return false;
                    }

                    if (desc.WritableSet && !desc.Writable)
                    {
                        return false;
                    }

                    IntegerIndexedElementSet(numericIndex.Value, desc.Value);
                    return true;
                }
            }

            return base.DefineOwnProperty(property, desc);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-ownpropertykeys
        /// </summary>
        public override List<JsValue> GetOwnPropertyKeys(Types types = Types.None | Types.String | Types.Symbol)
        {
            var keys = new List<JsValue>();
            if (!_viewedArrayBuffer.IsDetachedBuffer)
            {
                var length = Length;
                for (uint i = 0; i < length; ++i)
                {
                    keys.Add(JsString.Create(i));
                }
            }

            if (_properties is not null)
            {
                foreach (var pair in _properties)
                {
                    keys.Add(pair.Key.Name);
                }
            }

            if (_symbols is not null)
            {
                foreach (var pair in _symbols)
                {
                    keys.Add(pair.Key);
                }
            }

            return keys;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-delete-p
        /// </summary>
        public override bool Delete(JsValue property)
        {
            var numericIndex = TypeConverter.CanonicalNumericIndexString(property);
            if (numericIndex is not null)
            {
                return !IsValidIntegerIndex(numericIndex.Value);
            }

            return base.Delete(property);
        }

        // helper to prevent floating points
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue IntegerIndexedElementGet(int index)
        {
            if (!IsValidIntegerIndex(index))
            {
                return Undefined;
            }

            return DoIntegerIndexedElementGet(index);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integerindexedelementget
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private JsValue IntegerIndexedElementGet(double index)
        {
            if (!IsValidIntegerIndex(index))
            {
                return Undefined;
            }

            return DoIntegerIndexedElementGet((int) index);
        }

        private JsValue DoIntegerIndexedElementGet(int index)
        {
            var offset = _byteOffset;
            var elementType = _arrayElementType;
            var elementSize = elementType.GetElementSize();
            var indexedPosition = index * elementSize + offset;
            var value = _viewedArrayBuffer.GetValueFromBuffer(indexedPosition, elementType, true, ArrayBufferOrder.Unordered);
            if (value.Type == Types.Number)
            {
                return _arrayElementType.FitsInt32()
                    ? JsNumber.Create((int) value.DoubleValue)
                    : JsNumber.Create(value.DoubleValue);
            }

            return JsBigInt.Create(value.BigInteger);
        }

        // helper tot prevent floating point
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void IntegerIndexedElementSet(int index, JsValue value)
        {
            TypedArrayValue numValue = _contentType != TypedArrayContentType.BigInt
                ? TypeConverter.ToNumber(value)
                : value.ToBigInteger(_engine);

            if (IsValidIntegerIndex(index))
            {
                DoIntegerIndexedElementSet(index, numValue);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integerindexedelementset
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IntegerIndexedElementSet(double index, JsValue value)
        {
            if (_contentType != TypedArrayContentType.BigInt)
            {
                var numValue = TypeConverter.ToNumber(value);
                if (IsValidIntegerIndex(index))
                {
                    DoIntegerIndexedElementSet((int) index, numValue);
                }
            }
            else
            {
                try
                {
                    var numValue = TypeConverter.ToBigInt(value);
                    if (IsValidIntegerIndex(index))
                    {
                        DoIntegerIndexedElementSet((int) index, numValue);
                    }
                }
                catch (ParserException ex)
                {
                    ExceptionHelper.ThrowSyntaxError(_engine.Realm, ex.Message);
                }
            }
        }

        internal void DoIntegerIndexedElementSet(int index, TypedArrayValue numValue)
        {
            var offset = _byteOffset;
            var elementType = _arrayElementType;
            var elementSize = elementType.GetElementSize();
            var indexedPosition = index * elementSize + offset;
            _viewedArrayBuffer.SetValueInBuffer(indexedPosition, elementType, numValue, true, ArrayBufferOrder.Unordered);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-isvalidintegerindex
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsValidIntegerIndex(double index)
        {
            if (_viewedArrayBuffer.IsDetachedBuffer)
            {
                return false;
            }

            if (!TypeConverter.IsIntegralNumber(index))
            {
                return false;
            }

            if (NumberInstance.IsNegativeZero(index))
            {
                return false;
            }

            if (index < 0 || index >= _arrayLength)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-isvalidintegerindex
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsValidIntegerIndex(int index)
        {
            return !_viewedArrayBuffer.IsDetachedBuffer && (uint) index < _arrayLength;
        }

        internal T[] ToNativeArray<T>()
        {
            var conversionType = typeof(T);
            var elementSize = _arrayElementType.GetElementSize();
            var byteOffset = _byteOffset;
            var buffer = _viewedArrayBuffer;

            var array = new T[Length];
            for (var i = 0; i < array.Length; ++i)
            {
                var indexedPosition = i * elementSize + byteOffset;
                var value = buffer.RawBytesToNumeric(_arrayElementType, indexedPosition, BitConverter.IsLittleEndian);
                array[i] = (T) Convert.ChangeType(value, conversionType);
            }

            return array;
        }
    }
}

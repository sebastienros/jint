using System.Collections;
using System.Collections.Generic;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.TypedArray
{
    public sealed class TypedArrayInstance : ObjectInstance, IEnumerable<JsNumber>
    {
        internal TypedArrayContentType _contentType;
        internal readonly TypedArrayElementType _arrayElementType;
        internal ArrayBufferInstance _viewedArrayBuffer;
        internal uint _byteLength;
        internal uint _byteOffset;
        private readonly Intrinsics _intrinsics;
        private PropertyDescriptor _length;

        private TypedArrayInstance(
            Engine engine,
            Intrinsics intrinsics) : base(engine)
        {
            _intrinsics = intrinsics;
        }

        internal TypedArrayInstance(
            Engine engine,
            Intrinsics intrinsics,
            TypedArrayElementType type,
            int length) : this(engine, intrinsics)
        {
            _arrayElementType = type;
            _length = new PropertyDescriptor(JsNumber.Create(length), PropertyFlag.OnlyWritable);
            AllocateTypedArrayBuffer(length);
        }

        internal JsValue this[uint index]
        {
            get => IntegerIndexedElementGet(index);
            set => IntegerIndexedElementSet(index, value);
        }

        public override uint Length
        {
            get
            {
                if (_length is null)
                {
                    return 0;
                }

                return (uint) ((JsNumber) _length._value)._value;
            }
        }

        internal override bool IsIntegerIndexedArray => true;

        internal void AllocateTypedArrayBuffer(int len)
        {
            var elementSize = _arrayElementType.GetElementSize();
            _byteLength = (uint) (elementSize * len);
            _length._value = len;
            var data = _intrinsics.ArrayBuffer.AllocateArrayBuffer(_intrinsics.ArrayBuffer, _byteLength);
            _viewedArrayBuffer = data;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-hasproperty-p
        /// </summary>
        public override bool HasProperty(JsValue property)
        {
            if (ArrayInstance.IsArrayIndex(property, out var numericIndex))
            {
                return IsValidIntegerIndex(numericIndex);
            }

            return base.HasProperty(property);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-getownproperty-p
        /// </summary>
        public override PropertyDescriptor GetOwnProperty(JsValue property)
        {
            if (property == CommonProperties.Length)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            if (ArrayInstance.IsArrayIndex(property, out var numericIndex))
            {
                var value = IntegerIndexedElementGet(numericIndex);
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
            if (ArrayInstance.IsArrayIndex(property, out var numericIndex))
            {
                return IntegerIndexedElementGet(numericIndex);
            }

            return base.Get(property, receiver);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-set-p-v-receiver
        /// </summary>
        public override bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            if (ArrayInstance.IsArrayIndex(property, out var numericIndex))
            {
                IntegerIndexedElementSet(numericIndex, value);
                return true;
            }

            return base.Set(property, value, receiver);
        }

        protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (property == CommonProperties.Length)
            {
                _length = desc;
            }
            else
            {
                base.SetOwnProperty(property, desc);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-defineownproperty-p-desc
        /// </summary>
        public override bool DefineOwnProperty(JsValue property, PropertyDescriptor desc)
        {
            if (ArrayInstance.IsArrayIndex(property, out var numericIndex))
            {
                if (!IsValidIntegerIndex(numericIndex))
                {
                    return false;
                }

                if (!desc.Configurable)
                {
                    return false;
                }

                if (!desc.Enumerable)
                {
                    return false;
                }

                if (desc.IsAccessorDescriptor())
                {
                    return false;
                }

                if (!desc.Writable)
                {
                    return false;
                }

                IntegerIndexedElementSet(numericIndex, desc.Value);
                return true;
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

            foreach (var pair in _properties)
            {
                keys.Add(pair.Key.Name);
            }

            foreach (var pair in _symbols)
            {
                keys.Add(pair.Key);
            }

            return keys;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integer-indexed-exotic-objects-delete-p
        /// </summary>
        public override bool Delete(JsValue property)
        {
            if (ArrayInstance.IsArrayIndex(property, out var numericIndex))
            {
                return !IsValidIntegerIndex(numericIndex);
            }

            return base.Delete(property);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integerindexedelementget
        /// </summary>
        private JsValue IntegerIndexedElementGet(long index)
        {
            if (!IsValidIntegerIndex(index))
            {
                return Undefined;
            }

            var offset = _byteOffset;
            var elementType = _arrayElementType;
            var elementSize = elementType.GetElementSize();
            var indexedPosition = index * elementSize + offset;
            return _viewedArrayBuffer.GetValueFromBuffer((uint) indexedPosition, elementType, true, ArrayBufferOrder.Unordered);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-integerindexedelementset
        /// </summary>
        private void IntegerIndexedElementSet(uint index, JsValue value)
        {
            // 2. If O.[[ContentType]] is BigInt, let numValue be ? ToBigInt(value).
            var numValue = TypeConverter.ToNumber(value);
            if (IsValidIntegerIndex(index))
            {
                var offset = _byteOffset;
                var elementType = _arrayElementType;
                var elementSize = elementType.GetElementSize();
                var indexedPosition = index * elementSize + offset;
                _viewedArrayBuffer.SetValueInBuffer((uint) indexedPosition, elementType, numValue, true, ArrayBufferOrder.Unordered);
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-isvalidintegerindex
        /// </summary>
        private bool IsValidIntegerIndex(long index)
        {
            if (_viewedArrayBuffer.IsDetachedBuffer)
            {
                return false;
            }

            /*
            if (!IsIntegralNumber(index))
            {
                return false;
            }
            */

            if (index < 0 || index >= ((JsNumber) _length._value)._value)
            {
                return false;
            }

            return true;
        }

        public IEnumerator<JsNumber> GetEnumerator()
        {
            var length = Length;
            for (uint i = 0; i < length; i++)
            {
                yield return (JsNumber) this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
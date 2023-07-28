using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.Native.ArrayBuffer
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-arraybuffer-objects
    /// </summary>
    public sealed class JsArrayBuffer : ObjectInstance
    {
        // so that we don't need to allocate while or reading setting values
        private readonly byte[] _workBuffer = new byte[8];

        private byte[]? _arrayBufferData;
        private readonly JsValue _arrayBufferDetachKey = Undefined;

        internal JsArrayBuffer(
            Engine engine,
            ulong byteLength) : base(engine)
        {
            var block = byteLength > 0 ? CreateByteDataBlock(byteLength) : System.Array.Empty<byte>();
            _arrayBufferData = block;
        }

        private byte[] CreateByteDataBlock(ulong byteLength)
        {
            if (byteLength > int.MaxValue)
            {
                ExceptionHelper.ThrowRangeError(_engine.Realm, "Array buffer allocation failed");
            }

            return new byte[byteLength];
        }

        internal int ArrayBufferByteLength => _arrayBufferData?.Length ?? 0;
        internal byte[]? ArrayBufferData => _arrayBufferData;

        internal bool IsDetachedBuffer => _arrayBufferData is null;
        internal bool IsSharedArrayBuffer => false; // TODO SharedArrayBuffer

        /// <summary>
        /// https://tc39.es/ecma262/#sec-detacharraybuffer
        /// </summary>
        internal void DetachArrayBuffer(JsValue? key = null)
        {
            key ??= Undefined;

            if (!SameValue(_arrayBufferDetachKey, key))
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm);
            }

            _arrayBufferData = null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-clonearraybuffer
        /// </summary>
        internal JsArrayBuffer CloneArrayBuffer(
            ArrayBufferConstructor constructor,
            int srcByteOffset,
            uint srcLength)
        {
            var targetBuffer = constructor.AllocateArrayBuffer(_engine.Realm.Intrinsics.ArrayBuffer, srcLength);
            AssertNotDetached();

            var srcBlock = _arrayBufferData!;
            var targetBlock = targetBuffer.ArrayBufferData!;

            // TODO SharedArrayBuffer would use this
            //CopyDataBlockBytes(targetBlock, 0, srcBlock, srcByteOffset, srcLength).

            System.Array.Copy(srcBlock, srcByteOffset, targetBlock, 0, srcLength);

            return targetBuffer;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-getvaluefrombuffer
        /// </summary>
        internal TypedArrayValue GetValueFromBuffer(
            int byteIndex,
            TypedArrayElementType type,
            bool isTypedArray,
            ArrayBufferOrder order,
            bool? isLittleEndian = null)
        {
            if (!IsSharedArrayBuffer)
            {
                // If isLittleEndian is not present, set isLittleEndian to the value of the [[LittleEndian]] field of the surrounding agent's Agent Record.
                return RawBytesToNumeric(type, byteIndex, isLittleEndian ?? BitConverter.IsLittleEndian);
            }

            /*
                Let execution be the [[CandidateExecution]] field of the surrounding agent's Agent EsprimaExtensions.Record.
                b. Let eventList be the [[EventList]] field of the element in execution.[[EventsRecords]] whose [[AgentSignifier]] is AgentSignifier().
                c. If isTypedArray is true and IsNoTearConfiguration(type, order) is true, let noTear be true; otherwise let noTear be false.
                d. Let rawValue be a List of length elementSize whose elements are nondeterministically chosen byte values.
                e. NOTE: In implementations, rawValue is the result of a non-atomic or atomic read instruction on the underlying hardware. The nondeterminism is a semantic prescription of the memory model to describe observable behaviour of hardware with weak consistency.
                f. Let readEvent be ReadSharedMemory { [[Order]]: order, [[NoTear]]: noTear, [[Block]]: block, [[ByteIndex]]: byteIndex, [[ElementSize]]: elementSize }.
                g. Append readEvent to eventList.
                h. Append Chosen Value EsprimaExtensions.Record { [[Event]]: readEvent, [[ChosenValue]]: rawValue } to execution.[[ChosenValues]].
            */
            ExceptionHelper.ThrowNotImplementedException("SharedArrayBuffer not implemented");
            return default;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-rawbytestonumeric
        /// </summary>
        internal TypedArrayValue RawBytesToNumeric(TypedArrayElementType type, int byteIndex, bool isLittleEndian)
        {
            var elementSize = type.GetElementSize();
            var rawBytes = _arrayBufferData!;

            // 8 byte values require a little more at the moment
            var needsReverse = !isLittleEndian
                               && elementSize > 1
                               && type is TypedArrayElementType.Float32 or TypedArrayElementType.Float64 or TypedArrayElementType.BigInt64 or TypedArrayElementType.BigUint64;

            if (needsReverse)
            {
                System.Array.Copy(rawBytes, byteIndex, _workBuffer, 0, elementSize);
                byteIndex = 0;
                System.Array.Reverse(_workBuffer, 0, elementSize);
                rawBytes = _workBuffer;
            }

            if (type == TypedArrayElementType.Float32)
            {
                // rawBytes concatenated and interpreted as a little-endian bit string encoding of an IEEE 754-2019 binary32 value.
                var value = BitConverter.ToSingle(rawBytes, byteIndex);

                // If value is an IEEE 754-2019 binary32 NaN value, return the NaN Number value.
                if (float.IsNaN(value))
                {
                    return double.NaN;
                }

                return value;
            }

            if (type == TypedArrayElementType.Float64)
            {
                // rawBytes concatenated and interpreted as a little-endian bit string encoding of an IEEE 754-2019 binary64 value.
                var value = BitConverter.ToDouble(rawBytes, byteIndex);
                return value;
            }

            if (type == TypedArrayElementType.BigUint64)
            {
                var value = BitConverter.ToUInt64(rawBytes, byteIndex);
                return value;
            }

            if (type == TypedArrayElementType.BigInt64)
            {
                var value = BitConverter.ToInt64(rawBytes, byteIndex);
                return value;
            }

            TypedArrayValue? arrayValue = type switch
            {
                TypedArrayElementType.Int8 => ((sbyte) rawBytes[byteIndex]),
                TypedArrayElementType.Uint8 => (rawBytes[byteIndex]),
                TypedArrayElementType.Uint8C =>(rawBytes[byteIndex]),
                TypedArrayElementType.Int16 => (isLittleEndian
                    ? (short) (rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8))
                    : (short) (rawBytes[byteIndex + 1] | (rawBytes[byteIndex] << 8))
                ),
                TypedArrayElementType.Uint16 => (isLittleEndian
                    ? (ushort) (rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8))
                    : (ushort) (rawBytes[byteIndex + 1] | (rawBytes[byteIndex] << 8))
                ),
                TypedArrayElementType.Int32 => (isLittleEndian
                    ? rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8) | (rawBytes[byteIndex + 2] << 16) | (rawBytes[byteIndex + 3] << 24)
                    : rawBytes[byteIndex + 3] | (rawBytes[byteIndex + 2] << 8) | (rawBytes[byteIndex + 1] << 16) | (rawBytes[byteIndex + 0] << 24)
                ),
                TypedArrayElementType.Uint32 => (isLittleEndian
                    ? (uint) (rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8) | (rawBytes[byteIndex + 2] << 16) | (rawBytes[byteIndex + 3] << 24))
                    : (uint) (rawBytes[byteIndex + 3] | (rawBytes[byteIndex + 2] << 8) | (rawBytes[byteIndex + 1] << 16) | (rawBytes[byteIndex] << 24))
                ),
                _ => null
            };

            if (arrayValue is null)
            {
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(type), type.ToString());
            }

            return arrayValue.Value;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-setvalueinbuffer
        /// </summary>
        internal void SetValueInBuffer(
            int byteIndex,
            TypedArrayElementType type,
            TypedArrayValue value,
            bool isTypedArray,
            ArrayBufferOrder order,
            bool? isLittleEndian = null)
        {
            var block = _arrayBufferData!;
            if (!IsSharedArrayBuffer)
            {
                // If isLittleEndian is not present, set isLittleEndian to the value of the [[LittleEndian]] field of the surrounding agent's Agent Record.
                var rawBytes = NumericToRawBytes(type, value, isLittleEndian ?? BitConverter.IsLittleEndian);
                System.Array.Copy(rawBytes, 0, block,  byteIndex, type.GetElementSize());
            }
            else
            {
                /*
                    a. Let execution be the [[CandidateExecution]] field of the surrounding agent's Agent Record.
                    b. Let eventList be the [[EventList]] field of the element in execution.[[EventsRecords]] whose [[AgentSignifier]] is AgentSignifier().
                    c. If isTypedArray is true and IsNoTearConfiguration(type, order) is true, let noTear be true; otherwise let noTear be false.
                    d. Append WriteSharedMemory { [[Order]]: order, [[NoTear]]: noTear, [[Block]]: block, [[ByteIndex]]: byteIndex, [[ElementSize]]: elementSize, [[Payload]]: rawBytes } to eventList.
                */
                ExceptionHelper.ThrowNotImplementedException("SharedArrayBuffer not implemented");
            }
        }

        private byte[] NumericToRawBytes(TypedArrayElementType type, TypedArrayValue value, bool isLittleEndian)
        {
            byte[] rawBytes;
            if (type == TypedArrayElementType.Float32)
            {
                // Let rawBytes be a List whose elements are the 4 bytes that are the result of converting value to IEEE 754-2019 binary32 format using roundTiesToEven mode. If isLittleEndian is false, the bytes are arranged in big endian order. Otherwise, the bytes are arranged in little endian order. If value is NaN, rawBytes may be set to any implementation chosen IEEE 754-2019 binary32 format Not-a-Number encoding. An implementation must always choose the same encoding for each implementation distinguishable NaN value.
                rawBytes = BitConverter.GetBytes((float) value.DoubleValue);
            }
            else if (type == TypedArrayElementType.Float64)
            {
                // Let rawBytes be a List whose elements are the 8 bytes that are the IEEE 754-2019 binary64 format encoding of value. If isLittleEndian is false, the bytes are arranged in big endian order. Otherwise, the bytes are arranged in little endian order. If value is NaN, rawBytes may be set to any implementation chosen IEEE 754-2019 binary64 format Not-a-Number encoding. An implementation must always choose the same encoding for each implementation distinguishable NaN value.
                rawBytes = BitConverter.GetBytes(value.DoubleValue);
            }
            else if (type == TypedArrayElementType.BigInt64)
            {
                rawBytes = BitConverter.GetBytes(TypeConverter.ToBigInt64(value.BigInteger));
            }
            else if (type == TypedArrayElementType.BigUint64)
            {
                rawBytes = BitConverter.GetBytes(TypeConverter.ToBigUint64(value.BigInteger));
            }
            else
            {
                // inlined conversion for faster speed instead of getting the method in spec
                var doubleValue  = value.DoubleValue;
                var intValue = double.IsNaN(doubleValue) || doubleValue == 0 || double.IsInfinity(doubleValue)
                    ? 0
                    : (long) doubleValue;

                rawBytes = _workBuffer;
                switch (type)
                {
                    case TypedArrayElementType.Int8:
                        rawBytes[0] = (byte) (sbyte) intValue;
                        break;
                    case TypedArrayElementType.Uint8:
                        rawBytes[0] = (byte) intValue;
                        break;
                    case TypedArrayElementType.Uint8C:
                        rawBytes[0] = (byte) TypeConverter.ToUint8Clamp(value.DoubleValue);
                        break;
                    case TypedArrayElementType.Int16:
#if !NETSTANDARD2_1
                        rawBytes = BitConverter.GetBytes((short) intValue);
#else
                        BitConverter.TryWriteBytes(rawBytes, (short) intValue);
#endif
                        break;
                    case TypedArrayElementType.Uint16:
#if !NETSTANDARD2_1
                        rawBytes = BitConverter.GetBytes((ushort) intValue);
#else
                        BitConverter.TryWriteBytes(rawBytes, (ushort) intValue);
#endif
                        break;
                    case TypedArrayElementType.Int32:
#if !NETSTANDARD2_1
                        rawBytes = BitConverter.GetBytes((uint) intValue);
#else
                        BitConverter.TryWriteBytes(rawBytes, (uint) intValue);
#endif
                        break;
                    case TypedArrayElementType.Uint32:
#if !NETSTANDARD2_1
                        rawBytes = BitConverter.GetBytes((uint) intValue);
#else
                        BitConverter.TryWriteBytes(rawBytes, (uint) intValue);
#endif
                        break;
                    default:
                        ExceptionHelper.ThrowArgumentOutOfRangeException();
                        return null;
                }
            }

            var elementSize = type.GetElementSize();
            if (!isLittleEndian && elementSize > 1)
            {
                System.Array.Reverse(rawBytes, 0, elementSize);
            }

            return rawBytes;
        }

        internal void AssertNotDetached()
        {
            if (IsDetachedBuffer)
            {
                ExceptionHelper.ThrowTypeError(_engine.Realm, "ArrayBuffer has been detached");
            }
        }
    }
}

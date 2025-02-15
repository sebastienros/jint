using Jint.Native.ArrayBuffer;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-arraybuffer-objects
/// </summary>
public class JsArrayBuffer : ObjectInstance
{
    // so that we don't need to allocate while or reading setting values
    private readonly byte[] _workBuffer = new byte[8];

    internal byte[]? _arrayBufferData;
    internal readonly int? _arrayBufferMaxByteLength;

    internal readonly JsValue _arrayBufferDetachKey = Undefined;

    internal JsArrayBuffer(
        Engine engine,
        byte[] data,
        uint? arrayBufferMaxByteLength = null) : base(engine)
    {
        if (arrayBufferMaxByteLength is > int.MaxValue)
        {
            ExceptionHelper.ThrowRangeError(engine.Realm, "arrayBufferMaxByteLength cannot be larger than int32.MaxValue");
        }

        _prototype = engine.Intrinsics.ArrayBuffer.PrototypeObject;
        _arrayBufferData = data;
        _arrayBufferMaxByteLength = (int?) arrayBufferMaxByteLength;
    }

    internal static byte[] CreateByteDataBlock(Realm realm, ulong byteLength)
    {
        if (byteLength > int.MaxValue)
        {
            ExceptionHelper.ThrowRangeError(realm, "Array buffer allocation failed");
        }

        return new byte[byteLength];
    }

    internal virtual int ArrayBufferByteLength => _arrayBufferData?.Length ?? 0;
    internal byte[]? ArrayBufferData => _arrayBufferData;

    internal bool IsDetachedBuffer => _arrayBufferData is null;

    internal bool IsFixedLengthArrayBuffer => _arrayBufferMaxByteLength is null;

    internal virtual bool IsSharedArrayBuffer => false;

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
        return RawBytesToNumeric(type, byteIndex, isLittleEndian ?? BitConverter.IsLittleEndian);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-rawbytestonumeric
    /// </summary>
    internal TypedArrayValue RawBytesToNumeric(TypedArrayElementType type, int byteIndex, bool isLittleEndian)
    {
        if (type is TypedArrayElementType.Uint8 or TypedArrayElementType.Uint8C)
        {
            return new TypedArrayValue(Types.Number, _arrayBufferData![byteIndex], default);
        }

        var elementSize = type.GetElementSize();
        var rawBytes = _arrayBufferData!;

        // 8 byte values require a little more at the moment
        var needsReverse = !isLittleEndian
                           && elementSize > 1
                           && type is TypedArrayElementType.Float16 or TypedArrayElementType.Float32 or TypedArrayElementType.Float64 or TypedArrayElementType.BigInt64 or TypedArrayElementType.BigUint64;

        if (needsReverse)
        {
            System.Array.Copy(rawBytes, byteIndex, _workBuffer, 0, elementSize);
            byteIndex = 0;
            System.Array.Reverse(_workBuffer, 0, elementSize);
            rawBytes = _workBuffer;
        }

        if (type == TypedArrayElementType.Float16)
        {
#if SUPPORTS_HALF
            // rawBytes concatenated and interpreted as a little-endian bit string encoding of an IEEE 754-2019 binary32 value.
            var value = BitConverter.ToHalf(rawBytes, byteIndex);

            // If value is an IEEE 754-2019 binary32 NaN value, return the NaN Number value.
            if (Half.IsNaN(value))
            {
                return double.NaN;
            }

            return value;
#else
            ExceptionHelper.ThrowNotImplementedException("Float16/Half type is not supported in this build");
            return default;
#endif

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
            TypedArrayElementType.Int8 => (sbyte) rawBytes[byteIndex],
            TypedArrayElementType.Int16 => isLittleEndian
                ? (short) (rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8))
                : (short) (rawBytes[byteIndex + 1] | (rawBytes[byteIndex] << 8)),
            TypedArrayElementType.Uint16 => isLittleEndian
                ? (ushort) (rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8))
                : (ushort) (rawBytes[byteIndex + 1] | (rawBytes[byteIndex] << 8)),
            TypedArrayElementType.Int32 => isLittleEndian
                ? rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8) | (rawBytes[byteIndex + 2] << 16) | (rawBytes[byteIndex + 3] << 24)
                : rawBytes[byteIndex + 3] | (rawBytes[byteIndex + 2] << 8) | (rawBytes[byteIndex + 1] << 16) | (rawBytes[byteIndex + 0] << 24),
            TypedArrayElementType.Uint32 => isLittleEndian
                ? (uint) (rawBytes[byteIndex] | (rawBytes[byteIndex + 1] << 8) | (rawBytes[byteIndex + 2] << 16) | (rawBytes[byteIndex + 3] << 24))
                : (uint) (rawBytes[byteIndex + 3] | (rawBytes[byteIndex + 2] << 8) | (rawBytes[byteIndex + 1] << 16) | (rawBytes[byteIndex] << 24)),
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
        if (type is TypedArrayElementType.Uint8)
        {
            var doubleValue = value.DoubleValue;
            var intValue = double.IsNaN(doubleValue) || doubleValue == 0 || double.IsInfinity(doubleValue)
                ? 0
                : (long) doubleValue;

            _arrayBufferData![byteIndex] = (byte) intValue;
            return;
        }

        var block = _arrayBufferData!;
        // If isLittleEndian is not present, set isLittleEndian to the value of the [[LittleEndian]] field of the surrounding agent's Agent Record.
        var rawBytes = NumericToRawBytes(type, value, isLittleEndian ?? BitConverter.IsLittleEndian);
        System.Array.Copy(rawBytes, 0, block, byteIndex, type.GetElementSize());
    }

    private byte[] NumericToRawBytes(TypedArrayElementType type, TypedArrayValue value, bool isLittleEndian)
    {
        byte[] rawBytes;
        if (type == TypedArrayElementType.Float16)
        {
#if SUPPORTS_HALF
            rawBytes = BitConverter.GetBytes((Half) value.DoubleValue);
#else
            ExceptionHelper.ThrowNotImplementedException("Float16/Half type is not supported in this build");
            return default!;
#endif
        }
        else if (type == TypedArrayElementType.Float32)
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
                    rawBytes[0] = TypeConverter.ToUint8Clamp(value.DoubleValue);
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

    internal void Resize(uint newByteLength)
    {
        if (_arrayBufferMaxByteLength is null)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm);
        }

        if (newByteLength > _arrayBufferMaxByteLength)
        {
            ExceptionHelper.ThrowRangeError(_engine.Realm);
        }

        var oldBlock = _arrayBufferData ?? [];
        var newBlock = CreateByteDataBlock(_engine.Realm, newByteLength);
        var copyLength = System.Math.Min(newByteLength, ArrayBufferByteLength);

        System.Array.Copy(oldBlock, newBlock, copyLength);
        _arrayBufferData = newBlock;
    }

    internal void AssertNotDetached()
    {
        if (IsDetachedBuffer)
        {
            ExceptionHelper.ThrowTypeError(_engine.Realm, "ArrayBuffer has been detached");
        }
    }
}

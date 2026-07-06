using System.Buffers.Binary;
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
    // scratch space for byte-order-reversing a big-endian floating point element before decoding it,
    // so the read path doesn't allocate (integer reads/writes go straight through BinaryPrimitives)
    private readonly byte[] _workBuffer = new byte[8];

    internal byte[]? _arrayBufferData;
    internal readonly int? _arrayBufferMaxByteLength;
    internal bool _isImmutable;

    internal readonly JsValue _arrayBufferDetachKey = Undefined;

    internal JsArrayBuffer(
        Engine engine,
        byte[] data,
        uint? arrayBufferMaxByteLength = null) : base(engine)
    {
        if (arrayBufferMaxByteLength is > int.MaxValue)
        {
            Throw.RangeError(engine.Realm, "arrayBufferMaxByteLength cannot be larger than int32.MaxValue");
        }

        _prototype = engine.Intrinsics.ArrayBuffer.PrototypeObject;
        _arrayBufferData = data;
        _arrayBufferMaxByteLength = (int?) arrayBufferMaxByteLength;
    }

    internal static byte[] CreateByteDataBlock(Realm realm, ulong byteLength)
    {
        if (byteLength > int.MaxValue)
        {
            Throw.RangeError(realm, "Array buffer allocation failed");
        }

        return new byte[byteLength];
    }

    internal virtual int ArrayBufferByteLength => _arrayBufferData?.Length ?? 0;
    internal byte[]? ArrayBufferData => _arrayBufferData;

    internal bool IsDetachedBuffer => _arrayBufferData is null;

    internal bool IsFixedLengthArrayBuffer => _arrayBufferMaxByteLength is null;

    internal virtual bool IsSharedArrayBuffer => false;

    /// <summary>
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-isimmutablebuffer
    /// </summary>
    internal bool IsImmutableBuffer => _isImmutable;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-detacharraybuffer
    /// </summary>
    internal void DetachArrayBuffer(JsValue? key = null)
    {
        key ??= Undefined;

        if (!SameValue(_arrayBufferDetachKey, key))
        {
            Throw.TypeError(_engine.Realm);
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
        var block = _arrayBufferData!;

        // Integer element types read straight from the buffer via BinaryPrimitives: no scratch buffer,
        // no manual bit assembly, and endianness-correct regardless of the host's byte order.
        switch (type)
        {
            case TypedArrayElementType.Uint8:
            case TypedArrayElementType.Uint8C:
                return new TypedArrayValue(Types.Number, block[byteIndex], default);
            case TypedArrayElementType.Int8:
                return (sbyte) block[byteIndex];
        }

        var src = block.AsSpan(byteIndex);
        switch (type)
        {
            case TypedArrayElementType.Int16:
                return isLittleEndian ? BinaryPrimitives.ReadInt16LittleEndian(src) : BinaryPrimitives.ReadInt16BigEndian(src);
            case TypedArrayElementType.Uint16:
                return isLittleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(src) : BinaryPrimitives.ReadUInt16BigEndian(src);
            case TypedArrayElementType.Int32:
                return isLittleEndian ? BinaryPrimitives.ReadInt32LittleEndian(src) : BinaryPrimitives.ReadInt32BigEndian(src);
            case TypedArrayElementType.Uint32:
                return isLittleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(src) : BinaryPrimitives.ReadUInt32BigEndian(src);
            case TypedArrayElementType.BigInt64:
                return isLittleEndian ? BinaryPrimitives.ReadInt64LittleEndian(src) : BinaryPrimitives.ReadInt64BigEndian(src);
            case TypedArrayElementType.BigUint64:
                return isLittleEndian ? BinaryPrimitives.ReadUInt64LittleEndian(src) : BinaryPrimitives.ReadUInt64BigEndian(src);
        }

        // Floating point: BitConverter interprets in host-endian order, so reverse into the scratch
        // buffer when the requested order differs. A NaN read canonicalizes to the NaN Number value.
        var elementSize = type.GetElementSize();
        var rawBytes = block;
        if (!isLittleEndian && elementSize > 1)
        {
            System.Array.Copy(block, byteIndex, _workBuffer, 0, elementSize);
            byteIndex = 0;
            System.Array.Reverse(_workBuffer, 0, elementSize);
            rawBytes = _workBuffer;
        }

        switch (type)
        {
            case TypedArrayElementType.Float16:
                return ReadFloat16(rawBytes, byteIndex);
            case TypedArrayElementType.Float32:
                // A NaN read canonicalizes to the NaN Number value.
                var single = BitConverter.ToSingle(rawBytes, byteIndex);
                return float.IsNaN(single) ? double.NaN : single;
            case TypedArrayElementType.Float64:
                return BitConverter.ToDouble(rawBytes, byteIndex);
            default:
                Throw.ArgumentOutOfRangeException(nameof(type), type.ToString());
                return default;
        }
    }

    private static TypedArrayValue ReadFloat16(byte[] rawBytes, int byteIndex)
    {
#if SUPPORTS_HALF
        // A NaN read canonicalizes to the NaN Number value.
        var value = BitConverter.ToHalf(rawBytes, byteIndex);
        if (Half.IsNaN(value))
        {
            return double.NaN;
        }
        return value;
#else
        Throw.NotImplementedException("Float16/Half type is not supported in this build");
        return default;
#endif
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
        // If isLittleEndian is not present, use the [[LittleEndian]] field of the surrounding agent's Agent Record.
        var littleEndian = isLittleEndian ?? BitConverter.IsLittleEndian;

        // Integer element types write straight into the buffer via BinaryPrimitives: no per-element
        // byte[] allocation, no manual endian reversal. (short)/(ushort) and (int)/(uint) share the
        // same bit pattern, so the signed overload serves the unsigned type too.
        switch (type)
        {
            case TypedArrayElementType.Int8:
                block[byteIndex] = (byte) (sbyte) DoubleToInt64(value.DoubleValue);
                return;
            case TypedArrayElementType.Uint8:
                block[byteIndex] = (byte) DoubleToInt64(value.DoubleValue);
                return;
            case TypedArrayElementType.Uint8C:
                block[byteIndex] = TypeConverter.ToUint8Clamp(value.DoubleValue);
                return;
            case TypedArrayElementType.Int16:
            case TypedArrayElementType.Uint16:
                {
                    var v = (short) DoubleToInt64(value.DoubleValue);
                    var dest = block.AsSpan(byteIndex);
                    if (littleEndian)
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(dest, v);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt16BigEndian(dest, v);
                    }
                    return;
                }
            case TypedArrayElementType.Int32:
            case TypedArrayElementType.Uint32:
                {
                    var v = (int) DoubleToInt64(value.DoubleValue);
                    var dest = block.AsSpan(byteIndex);
                    if (littleEndian)
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(dest, v);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt32BigEndian(dest, v);
                    }
                    return;
                }
            case TypedArrayElementType.BigInt64:
                {
                    var v = TypeConverter.ToBigInt64(value.BigInteger);
                    var dest = block.AsSpan(byteIndex);
                    if (littleEndian)
                    {
                        BinaryPrimitives.WriteInt64LittleEndian(dest, v);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt64BigEndian(dest, v);
                    }
                    return;
                }
            case TypedArrayElementType.BigUint64:
                {
                    var v = TypeConverter.ToBigUint64(value.BigInteger);
                    var dest = block.AsSpan(byteIndex);
                    if (littleEndian)
                    {
                        BinaryPrimitives.WriteUInt64LittleEndian(dest, v);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt64BigEndian(dest, v);
                    }
                    return;
                }
        }

        // Floating point: encode via BitConverter (NaN encoding is implementation-defined per spec) and
        // copy into the buffer, reversing for big-endian order.
        var rawBytes = FloatToRawBytes(type, value);
        if (!littleEndian && rawBytes.Length > 1)
        {
            System.Array.Reverse(rawBytes);
        }
        System.Array.Copy(rawBytes, 0, block, byteIndex, rawBytes.Length);
    }

    // ℝ(value) truncated toward zero, with NaN, ±0 and ±∞ mapping to 0 — the integer conversion the
    // spec's SetValueInBuffer applies before encoding an integer element type.
    private static long DoubleToInt64(double doubleValue)
        => double.IsNaN(doubleValue) || doubleValue == 0 || double.IsInfinity(doubleValue) ? 0 : (long) doubleValue;

    private static byte[] FloatToRawBytes(TypedArrayElementType type, TypedArrayValue value)
    {
        switch (type)
        {
            case TypedArrayElementType.Float16:
#if SUPPORTS_HALF
                return BitConverter.GetBytes((Half) value.DoubleValue);
#else
                Throw.NotImplementedException("Float16/Half type is not supported in this build");
                return default!;
#endif
            case TypedArrayElementType.Float32:
                return BitConverter.GetBytes((float) value.DoubleValue);
            case TypedArrayElementType.Float64:
                return BitConverter.GetBytes(value.DoubleValue);
            default:
                Throw.ArgumentOutOfRangeException(nameof(type), type.ToString());
                return null!;
        }
    }

    internal void Resize(uint newByteLength)
    {
        if (_arrayBufferMaxByteLength is null)
        {
            Throw.TypeError(_engine.Realm);
        }

        if (newByteLength > _arrayBufferMaxByteLength)
        {
            Throw.RangeError(_engine.Realm);
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
            Throw.TypeError(_engine.Realm, "ArrayBuffer has been detached");
        }
    }

    /// <summary>
    /// https://tc39.es/proposal-immutable-arraybuffer/#sec-isimmutablebuffer
    /// </summary>
    internal void AssertNotImmutable()
    {
        if (IsImmutableBuffer)
        {
            Throw.TypeError(_engine.Realm, "Cannot modify an immutable ArrayBuffer");
        }
    }
}

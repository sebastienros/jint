using System.Numerics;
using System.Runtime.InteropServices;
using Jint.Runtime;

namespace Jint.Native.TypedArray;

/// <summary>
/// Container for either double or BigInteger.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TypedArrayValue(Types Type, double DoubleValue, BigInteger BigInteger) : IConvertible
{
#if SUPPORTS_HALF
    public static implicit operator TypedArrayValue(Half value)
    {
        return new TypedArrayValue(Types.Number, (double) value, default);
    }
#endif

    public static implicit operator TypedArrayValue(double value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(byte value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(int value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(ushort value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(short value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(uint value)
    {
        return new TypedArrayValue(Types.Number, value, default);
    }

    public static implicit operator TypedArrayValue(BigInteger value)
    {
        return new TypedArrayValue(Types.BigInt, default, value);
    }

    public static implicit operator TypedArrayValue(ulong value)
    {
        return new TypedArrayValue(Types.BigInt, default, value);
    }

    public static implicit operator TypedArrayValue(long value)
    {
        return new TypedArrayValue(Types.BigInt, default, value);
    }

    public JsValue ToJsValue()
    {
        return Type == Types.Number
            ? JsNumber.Create(DoubleValue)
            : JsBigInt.Create(BigInteger);
    }

    public TypeCode GetTypeCode()
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public bool ToBoolean(IFormatProvider? provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public char ToChar(IFormatProvider? provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public sbyte ToSByte(IFormatProvider? provider)
    {
        return (sbyte) DoubleValue;
    }

    public byte ToByte(IFormatProvider? provider)
    {
        return (byte) DoubleValue;
    }

    public short ToInt16(IFormatProvider? provider)
    {
        return (short) DoubleValue;
    }

    public ushort ToUInt16(IFormatProvider? provider)
    {
        return (ushort) DoubleValue;
    }

    public int ToInt32(IFormatProvider? provider)
    {
        return (int) DoubleValue;
    }

    public uint ToUInt32(IFormatProvider? provider)
    {
        return (uint) DoubleValue;
    }

    public long ToInt64(IFormatProvider? provider)
    {
        return (long) BigInteger;
    }

    public ulong ToUInt64(IFormatProvider? provider)
    {
        return (ulong) BigInteger;
    }

    public float ToSingle(IFormatProvider? provider)
    {
        return (float) DoubleValue;
    }

    public double ToDouble(IFormatProvider? provider)
    {
        return DoubleValue;
    }

    public decimal ToDecimal(IFormatProvider? provider)
    {
        return (decimal) DoubleValue;
    }

    public DateTime ToDateTime(IFormatProvider? provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public string ToString(IFormatProvider? provider)
    {
        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }

    public object ToType(Type conversionType, IFormatProvider? provider)
    {
        if (conversionType == typeof(BigInteger) && Type == Types.BigInt)
        {
            return BigInteger;
        }

#if SUPPORTS_HALF
        if (conversionType == typeof(Half) && Type == Types.Number)
        {
            return (Half) DoubleValue;
        }
#endif

        ExceptionHelper.ThrowNotImplementedException();
        return default;
    }
}

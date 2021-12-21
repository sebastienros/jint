using System;
using System.Numerics;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsBigInt : JsValue, IEquatable<JsBigInt>
{
    internal readonly BigInteger _value;

    public static readonly JsBigInt Zero = new(0);
    public static readonly JsBigInt One = new(1);

    private static readonly JsBigInt[] _bigIntegerToJsValue;

    static JsBigInt()
    {
        var bigIntegers = new JsBigInt[1024];
        for (uint i = 0; i < bigIntegers.Length; i++)
        {
            bigIntegers[i] = new JsBigInt(i);
        }
        _bigIntegerToJsValue = bigIntegers;
    }

    public JsBigInt(BigInteger value) : base(Types.BigInt)
    {
        _value = value;
    }

    internal static JsBigInt Create(BigInteger bigInt)
    {
        var temp = _bigIntegerToJsValue;
        if (bigInt >= 0 && bigInt < (uint) temp.Length)
        {
            return temp[(int) bigInt];
        }

        return new JsBigInt(bigInt);
    }

    internal static JsBigInt Create(JsValue value)
    {
        return value is JsBigInt jsBigInt
            ? jsBigInt
            : Create(TypeConverter.ToBigInt(value));
    }


    public override object ToObject()
    {
        return _value;
    }

    public static bool operator ==(JsBigInt a, double b)
    {
        return a is not null && TypeConverter.IsIntegralNumber(b) && a._value == (long) b;
    }

    public static bool operator !=(JsBigInt a, double b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        return TypeConverter.ToString(_value);
    }

    public override bool IsLooselyEqual(JsValue value)
    {
        if (value is JsBigInt bigInt)
        {
            return Equals(bigInt);
        }

        return value is JsNumber jsNumber && TypeConverter.IsIntegralNumber(jsNumber._value) && _value == new BigInteger(jsNumber._value)
               || value is JsString jsString && TypeConverter.TryStringToBigInt(jsString.ToString(), out var temp) && temp == _value;
    }

    public override bool Equals(object other)
    {
        return Equals(other as JsBigInt);
    }

    public override bool Equals(JsValue other)
    {
        return Equals(other as JsBigInt);
    }

    public bool Equals(JsBigInt other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        return ReferenceEquals(this, other) || _value.Equals(other._value);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
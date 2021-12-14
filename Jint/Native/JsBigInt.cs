using System;
using System.Numerics;
using Jint.Runtime;

namespace Jint.Native;

public sealed class JsBigInt : JsValue, IEquatable<JsBigInt>
{
    internal readonly BigInteger _value;

    public static readonly JsBigInt Zero = new(0);
    public static readonly JsBigInt One = new(1);

    public JsBigInt(BigInteger value) : base(Types.BigInt)
    {
        _value = value;
    }

    public static JsBigInt Create(BigInteger bigInt)
    {
        return new JsBigInt(bigInt);
    }

    public override object ToObject()
    {
        return _value;
    }

    public static bool operator ==(JsBigInt a, double b)
    {
        return a is not null && !JsNumber.HasFractionalPart(b) && a._value == (long) b;
    }

    public static bool operator !=(JsBigInt a, double b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        return TypeConverter.ToString(_value);
    }

    public override bool NonStrictEquals(JsValue value)
    {
        return Equals(value) || value.IsNumber() && this == TypeConverter.ToNumber(value);
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

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _value.Equals(other._value);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
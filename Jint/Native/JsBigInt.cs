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
        return value as JsBigInt ?? Create(TypeConverter.ToBigInt(value));
    }

    public override object ToObject() => _value;

    internal override bool ToBoolean() => _value != 0;

    public static bool operator ==(JsBigInt a, double b)
    {
        return TypeConverter.IsIntegralNumber(b) && a._value == (long) b;
    }

    public static bool operator !=(JsBigInt a, double b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        return TypeConverter.ToString(_value);
    }

    protected internal override bool IsLooselyEqual(JsValue value)
    {
        if (value is JsBigInt bigInt)
        {
            return Equals(bigInt);
        }

        if (value is JsNumber number && TypeConverter.IsIntegralNumber(number._value) && _value == new BigInteger(number._value))
        {
            return true;
        }

        if (value is JsBoolean b)
        {
            return b._value && _value == BigInteger.One || !b._value && _value == BigInteger.Zero;
        }

        if (value is JsString s && TypeConverter.TryStringToBigInt(s.ToString(), out var temp) && temp == _value)
        {
            return true;
        }

        if (value.IsObject())
        {
            return IsLooselyEqual(TypeConverter.ToPrimitive(value, Types.Number));
        }

        return false;
    }

    public override bool Equals(object? obj) => Equals(obj as JsBigInt);

    public override bool Equals(JsValue? other) => Equals(other as JsBigInt);

    public bool Equals(JsBigInt? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        return ReferenceEquals(this, other) || _value == other._value;
    }

    public override int GetHashCode() => _value.GetHashCode();
}

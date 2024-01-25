using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Native.Number;
using Jint.Runtime;

namespace Jint.Native;

[DebuggerDisplay("{_value}", Type = "string")]
public sealed class JsNumber : JsValue, IEquatable<JsNumber>
{
    // .NET double epsilon and JS epsilon have different values
    internal const double JavaScriptEpsilon = 2.2204460492503130808472633361816E-16;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal readonly double _value;

    // how many decimals to check when determining if double is actually an int
    internal const double DoubleIsIntegerTolerance = double.Epsilon * 100;

    internal static readonly long NegativeZeroBits = BitConverter.DoubleToInt64Bits(-0.0);

    // we can cache most common values, doubles are used in indexing too at times so we also cache
    // integer values converted to doubles
    private const int NumbersMax = 1024 * 10;
    private static readonly JsNumber[] _intToJsValue;

    internal static readonly JsNumber DoubleNaN = new JsNumber(double.NaN);
    internal static readonly JsNumber DoubleNegativeOne = new JsNumber((double) -1);
    internal static readonly JsNumber DoublePositiveInfinity = new JsNumber(double.PositiveInfinity);
    internal static readonly JsNumber DoubleNegativeInfinity = new JsNumber(double.NegativeInfinity);
    internal static readonly JsNumber IntegerNegativeOne = new JsNumber(-1);
    internal static readonly JsNumber NegativeZero = new JsNumber(-0d);
    internal static readonly JsNumber PositiveZero = new JsNumber(+0);
    internal static readonly JsNumber PositiveOne = new JsNumber(1);
    internal static readonly JsNumber PositiveTwo = new JsNumber(2);
    internal static readonly JsNumber PositiveThree = new JsNumber(3);

    internal static readonly JsNumber PI = new JsNumber(System.Math.PI);

    static JsNumber()
    {
        var integers = new JsNumber[NumbersMax];
        for (uint i = 0; i < (uint) integers.Length; i++)
        {
            integers[i] = new JsNumber(i);
        }
        _intToJsValue = integers;
    }

    public JsNumber(double value) : base(Types.Number)
    {
        _value = value;
    }

    public JsNumber(int value) : base(InternalTypes.Integer)
    {
        _value = value;
    }

    public JsNumber(uint value) : base(value <= int.MaxValue ? InternalTypes.Integer : InternalTypes.Number)
    {
        _value = value;
    }

    public JsNumber(ulong value) : base(value <= int.MaxValue ? InternalTypes.Integer : InternalTypes.Number)
    {
        _value = value;
    }

    public JsNumber(long value) : base(value is <= int.MaxValue and >= int.MinValue ? InternalTypes.Integer : InternalTypes.Number)
    {
        _value = value;
    }

    public override object ToObject() => _value;

    internal override bool ToBoolean()
    {
        if (_type == InternalTypes.Integer)
        {
            return (int) _value != 0;
        }

        return _value != 0 && !double.IsNaN(_value);
    }

    internal static JsNumber Create(object value)
    {
        var underlyingType = System.Type.GetTypeCode(value.GetType());
        return underlyingType switch
        {
            TypeCode.Int64 => Create(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            TypeCode.UInt32 => Create(Convert.ToUInt64(value, CultureInfo.InvariantCulture)),
            TypeCode.UInt64 => Create(Convert.ToUInt64(value, CultureInfo.InvariantCulture)),
            _ => Create(Convert.ToInt32(value, CultureInfo.InvariantCulture))
        };
    }

    public static JsNumber Create(decimal value)
    {
        return Create((double) value);
    }

    public static JsNumber Create(double value)
    {
        // we expect zero to be on the fast path of integer mostly
        if (TypeConverter.IsIntegralNumber(value) && value is > long.MinValue and < long.MaxValue && !NumberInstance.IsNegativeZero(value))
        {
            var longValue = (long) value;
            return longValue == -1 ? IntegerNegativeOne : Create(longValue);
        }

        return CreateNumberUnlikely(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsNumber CreateNumberUnlikely(double value)
    {
        if (value is <= double.MaxValue and >= double.MinValue)
        {
            return new JsNumber(value);
        }

        if (double.IsNegativeInfinity(value))
        {
            return DoubleNegativeInfinity;
        }

        if (double.IsPositiveInfinity(value ))
        {
            return DoublePositiveInfinity;
        }

        if (double.IsNaN(value))
        {
            return DoubleNaN;
        }

        return new JsNumber(value);
    }

    public static JsNumber Create(byte value)
    {
        return _intToJsValue[value];
    }

    internal static JsNumber Create(sbyte value)
    {
        var temp = _intToJsValue;
        if ((uint) value < (uint) temp.Length)
        {
            return temp[value];
        }
        return new JsNumber(value);
    }

    public static JsNumber Create(int value)
    {
        var temp = _intToJsValue;
        if ((uint) value < (uint) temp.Length)
        {
            return temp[value];
        }

        if (value == -1)
        {
            return IntegerNegativeOne;
        }

        return new JsNumber(value);
    }

    internal static JsNumber Create(uint value)
    {
        var temp = _intToJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return new JsNumber(value);
    }

    internal static JsNumber Create(ulong value)
    {
        if (value < (ulong) _intToJsValue.Length)
        {
            return _intToJsValue[value];
        }

        return new JsNumber(value);
    }

    public static JsNumber Create(long value)
    {
        if ((ulong) value < (ulong) _intToJsValue.Length)
        {
            return _intToJsValue[value];
        }

        return new JsNumber(value);
    }

    public static JsNumber Create(JsValue jsValue)
    {
        if (jsValue is JsNumber number)
        {
            return number;
        }

        return Create(TypeConverter.ToNumber(jsValue));
    }

    public override string ToString()
    {
        return TypeConverter.ToString(_value);
    }

    internal bool IsNaN()
    {
        return double.IsNaN(_value);
    }

    /// <summary>
    /// Either positive or negative zero.
    /// </summary>
    internal bool IsZero()
    {
        return IsNegativeZero() || IsPositiveZero();
    }

    internal bool IsNegativeZero()
    {
        return NumberInstance.IsNegativeZero(_value);
    }

    internal bool IsPositiveZero()
    {
        return NumberInstance.IsPositiveZero(_value);
    }

    internal bool IsPositiveInfinity()
    {
        return double.IsPositiveInfinity(_value);
    }

    internal bool IsNegativeInfinity()
    {
        return double.IsNegativeInfinity(_value);
    }

    protected internal override bool IsLooselyEqual(JsValue value)
    {
        if (value is JsNumber jsNumber)
        {
            return Equals(jsNumber);
        }

        if (value.IsBigInt())
        {
            return TypeConverter.IsIntegralNumber(_value) && new BigInteger(_value) == value.AsBigInt();
        }

        return base.IsLooselyEqual(value);
    }

    public override bool Equals(object? obj) => Equals(obj as JsNumber);

    public override bool Equals(JsValue? other) => Equals(other as JsNumber);

    public bool Equals(JsNumber? other)
    {
        if (other is null)
        {
            return false;
        }

        if (double.IsNaN(_value) || double.IsNaN(other._value))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _value == other._value;
    }

    public override int GetHashCode() => _value.GetHashCode();
}

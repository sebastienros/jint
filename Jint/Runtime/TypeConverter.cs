using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Runtime.Interop;
using Jint.Extensions;

namespace Jint.Runtime;

public static class TypeConverter
{
    // how many decimals to check when determining if double is actually an int
    private const double DoubleIsIntegerTolerance = double.Epsilon * 100;

    private static readonly string[] intToString = new string[1024];
    private static readonly string[] charToString = new string[256];

    static TypeConverter()
    {
        for (var i = 0; i < intToString.Length; ++i)
        {
            intToString[i] = i.ToString(CultureInfo.InvariantCulture);
        }

        for (var i = 0; i < charToString.Length; ++i)
        {
            var c = (char) i;
            charToString[i] = c.ToString();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-toprimitive
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsValue ToPrimitive(JsValue input, Types preferredType = Types.Empty)
    {
        return input is not ObjectInstance oi
            ? input
            : ToPrimitiveObjectInstance(oi, preferredType);
    }

    private static JsValue ToPrimitiveObjectInstance(ObjectInstance oi, Types preferredType)
    {
        var exoticToPrim = oi.GetMethod(GlobalSymbolRegistry.ToPrimitive);
        if (exoticToPrim is not null)
        {
            var hint = preferredType switch
            {
                Types.String => JsString.StringString,
                Types.Number => JsString.NumberString,
                _ => JsString.DefaultString
            };

            var str = exoticToPrim.Call(oi, hint);
            if (str.IsPrimitive())
            {
                return str;
            }

            if (str.IsObject())
            {
                ExceptionHelper.ThrowTypeError(oi.Engine.Realm, "Cannot convert object to primitive value");
            }
        }

        return OrdinaryToPrimitive(oi, preferredType == Types.Empty ? Types.Number : preferredType);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarytoprimitive
    /// </summary>
    internal static JsValue OrdinaryToPrimitive(ObjectInstance input, Types hint = Types.Empty)
    {
        JsString property1;
        JsString property2;

        if (hint == Types.String)
        {
            property1 = (JsString) "toString";
            property2 = (JsString) "valueOf";
        }
        else if (hint == Types.Number)
        {
            property1 = (JsString) "valueOf";
            property2 = (JsString) "toString";
        }
        else
        {
            ExceptionHelper.ThrowTypeError(input.Engine.Realm);
            return null;
        }

        if (input.Get(property1) is ICallable method1)
        {
            var val = method1.Call(input, Arguments.Empty);
            if (val.IsPrimitive())
            {
                return val;
            }
        }

        if (input.Get(property2) is ICallable method2)
        {
            var val = method2.Call(input, Arguments.Empty);
            if (val.IsPrimitive())
            {
                return val;
            }
        }

        ExceptionHelper.ThrowTypeError(input.Engine.Realm);
        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-toboolean
    /// </summary>
    public static bool ToBoolean(JsValue o) => o.ToBoolean();

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tonumeric
    /// </summary>
    public static JsValue ToNumeric(JsValue value)
    {
        if (value.IsNumber() || value.IsBigInt())
        {
            return value;
        }

        var primValue = ToPrimitive(value, Types.Number);
        if (primValue.IsBigInt())
        {
            return primValue;
        }

        return ToNumber(primValue);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tonumber
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToNumber(JsValue o)
    {
        return o.IsNumber()
            ? ((JsNumber) o)._value
            : ToNumberUnlikely(o);
    }

    private static double ToNumberUnlikely(JsValue o)
    {
        var type = o._type & ~InternalTypes.InternalFlags;

        switch (type)
        {
            case InternalTypes.Undefined:
                return double.NaN;
            case InternalTypes.Null:
                return 0;
            case InternalTypes.Boolean:
                return ((JsBoolean) o)._value ? 1 : 0;
            case InternalTypes.String:
                return ToNumber(o.ToString());
            case InternalTypes.Symbol:
            case InternalTypes.BigInt:
            case InternalTypes.Empty:
                // TODO proper TypeError would require Engine instance and a lot of API changes
                ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a " + type + " value to a number");
                return 0;
            default:
                return ToNumber(ToPrimitive(o, Types.Number));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsNumber ToJsNumber(JsValue o)
    {
        return o.IsNumber() ? (JsNumber) o : ToJsNumberUnlikely(o);
    }

    private static JsNumber ToJsNumberUnlikely(JsValue o)
    {
        var type = o._type & ~InternalTypes.InternalFlags;

        switch (type)
        {
            case InternalTypes.Undefined:
                return JsNumber.DoubleNaN;
            case InternalTypes.Null:
                return JsNumber.PositiveZero;
            case InternalTypes.Boolean:
                return ((JsBoolean) o)._value ? JsNumber.PositiveOne : JsNumber.PositiveZero;
            case InternalTypes.String:
                return new JsNumber(ToNumber(o.ToString()));
            case InternalTypes.Symbol:
            case InternalTypes.BigInt:
            case InternalTypes.Empty:
                // TODO proper TypeError would require Engine instance and a lot of API changes
                ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a " + type + " value to a number");
                return JsNumber.PositiveZero;
            default:
                return new JsNumber(ToNumber(ToPrimitive(o, Types.Number)));
        }
    }

    private static double ToNumber(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        var firstChar = input[0];
        if (input.Length == 1)
        {
            return firstChar is >= '0' and <= '9' ? firstChar - '0' : double.NaN;
        }

        input = StringPrototype.TrimEx(input);
        firstChar = input[0];

        const NumberStyles NumberStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                                          NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                                          NumberStyles.AllowExponent;

        if (long.TryParse(input, NumberStyles, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue == 0 && firstChar == '-' ? -0.0 : longValue;
        }

        if (input.Length is 8 or 9)
        {
            switch (input)
            {
                case "+Infinity":
                case "Infinity":
                    return double.PositiveInfinity;
                case "-Infinity":
                    return double.NegativeInfinity;
            }

            if (input.EndsWith("infinity", StringComparison.OrdinalIgnoreCase))
            {
                // we don't accept other that case-sensitive
                return double.NaN;
            }
        }

        if (input.Length > 2 && firstChar == '0' && char.IsLetter(input[1]))
        {
            var fromBase = input[1] switch
            {
                'x' or 'X' => 16,
                'o' or 'O' => 8,
                'b' or 'B' => 2,
                _ => 0
            };

            if (fromBase > 0)
            {
                try
                {
                    return Convert.ToInt32(input.Substring(2), fromBase);
                }
                catch
                {
                    return double.NaN;
                }
            }
        }

#if NETFRAMEWORK
        // if we are on full framework, one extra check for whether it was actually over the bounds of double
        // in modern NET parsing was fixed to be IEEE 754 compliant, full framework is not and cannot detect positive infinity
        try
        {
            var targetString = firstChar == '-' ? input.Substring(1) : input;
            var n = double.Parse(targetString, NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture);

            if (n == 0 && firstChar == '-')
            {
                return -0.0;
            }

            return firstChar == '-' ? - 1 * n : n;
        }
        catch (Exception e) when (e is OverflowException)
        {
            return firstChar == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        }
        catch
        {
            return double.NaN;
        }
#else
        if (double.TryParse(input, NumberStyles, CultureInfo.InvariantCulture, out var n))
        {
            return n == 0 && firstChar == '-' ? -0.0 : n;
        }

        return double.NaN;
#endif
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tolength
    /// </summary>
    public static ulong ToLength(JsValue o)
    {
        var len = ToInteger(o);
        if (len <= 0)
        {
            return 0;
        }

        return (ulong) Math.Min(len, NumberConstructor.MaxSafeInteger);
    }


    /// <summary>
    /// https://tc39.es/ecma262/#sec-tointegerorinfinity
    /// </summary>
    public static double ToIntegerOrInfinity(JsValue argument)
    {
        var number = ToNumber(argument);
        if (double.IsNaN(number) || number == 0)
        {
            return 0;
        }

        if (double.IsInfinity(number))
        {
            return number;
        }

        var integer = (long) Math.Floor(Math.Abs(number));
        if (number < 0)
        {
            integer *= -1;
        }

        return integer;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tointeger
    /// </summary>
    public static double ToInteger(JsValue o)
    {
        return ToInteger(ToNumber(o));
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tointeger
    /// </summary>
    internal static double ToInteger(double number)
    {
        if (double.IsNaN(number))
        {
            return 0;
        }

        if (number == 0 || double.IsInfinity(number))
        {
            return number;
        }

        if (number is >= long.MinValue and <= long.MaxValue)
        {
            return (long) number;
        }

        var integer = Math.Floor(Math.Abs(number));
        if (number < 0)
        {
            integer *= -1;
        }

        return integer;
    }

    internal static int DoubleToInt32Slow(double o)
    {
        // Computes the integral value of the number mod 2^32.

        var doubleBits = BitConverter.DoubleToInt64Bits(o);
        var sign = (int) (doubleBits >> 63); // 0 if positive, -1 if negative
        var exponent = (int) ((doubleBits >> 52) & 0x7FF) - 1023;

        if ((uint) exponent >= 84)
        {
            // Anything with an exponent that is negative or >= 84 will convert to zero.
            // This includes infinities and NaNs, which have exponent = 1024
            // The 84 comes from 52 (bits in double mantissa) + 32 (bits in integer)
            return 0;
        }

        var mantissa = (doubleBits & 0xFFFFFFFFFFFFFL) | 0x10000000000000L;
        var int32Value = exponent >= 52 ? (int) (mantissa << (exponent - 52)) : (int) (mantissa >> (52 - exponent));

        return (int32Value + sign) ^ sign;
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
    /// </summary>
    public static int ToInt32(JsValue o)
    {
        if (o._type == InternalTypes.Integer)
        {
            return o.AsInteger();
        }

        var doubleVal = ToNumber(o);
        if (doubleVal >= -(double) int.MinValue && doubleVal <= int.MaxValue)
        {
            // Double-to-int cast is correct in this range
            return (int) doubleVal;
        }

        return DoubleToInt32Slow(doubleVal);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-touint32
    /// </summary>
    public static uint ToUint32(JsValue o)
    {
        if (o._type == InternalTypes.Integer)
        {
            return (uint) o.AsInteger();
        }

        var doubleVal = ToNumber(o);
        if (doubleVal is >= 0.0 and <= uint.MaxValue)
        {
            // Double-to-uint cast is correct in this range
            return (uint) doubleVal;
        }

        return (uint) DoubleToInt32Slow(doubleVal);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-touint16
    /// </summary>
    public static ushort ToUint16(JsValue o)
    {
        if (o._type == InternalTypes.Integer)
        {
            var integer = o.AsInteger();
            if (integer is >= 0 and <= ushort.MaxValue)
            {
                return (ushort) integer;
            }
        }

        var number = ToNumber(o);
        if (double.IsNaN(number) || number == 0 || double.IsInfinity(number))
        {
            return 0;
        }

        var intValue = Math.Floor(Math.Abs(number));
        if (number < 0)
        {
            intValue *= -1;
        }

        var int16Bit = intValue % 65_536; // 2^16
        return (ushort) int16Bit;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-toint16
    /// </summary>
    internal static double ToInt16(JsValue o)
    {
        return o._type == InternalTypes.Integer
            ? (short) o.AsInteger()
            : (short) (long) ToNumber(o);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-toint8
    /// </summary>
    internal static double ToInt8(JsValue o)
    {
        return o._type == InternalTypes.Integer
            ? (sbyte) o.AsInteger()
            : (sbyte) (long) ToNumber(o);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-touint8
    /// </summary>
    internal static double ToUint8(JsValue o)
    {
        return o._type == InternalTypes.Integer
            ? (byte) o.AsInteger()
            : (byte) (long) ToNumber(o);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-touint8clamp
    /// </summary>
    internal static byte ToUint8Clamp(JsValue o)
    {
        if (o._type == InternalTypes.Integer)
        {
            var intValue = o.AsInteger();
            if (intValue is > -1 and < 256)
            {
                return (byte) intValue;
            }
        }

        return ToUint8ClampUnlikely(o);
    }

    private static byte ToUint8ClampUnlikely(JsValue o)
    {
        var number = ToNumber(o);
        if (double.IsNaN(number))
        {
            return 0;
        }

        if (number <= 0)
        {
            return 0;
        }

        if (number >= 255)
        {
            return 255;
        }

        var f = Math.Floor(number);
        if (f + 0.5 < number)
        {
            return (byte) (f + 1);
        }

        if (number < f + 0.5)
        {
            return (byte) f;
        }

        if (f % 2 != 0)
        {
            return (byte) (f + 1);
        }

        return (byte) f;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tobigint
    /// </summary>
    public static BigInteger ToBigInt(JsValue value)
    {
        return value is JsBigInt bigInt
            ? bigInt._value
            : ToBigIntUnlikely(value);
    }

    private static BigInteger ToBigIntUnlikely(JsValue value)
    {
        var prim = ToPrimitive(value, Types.Number);
        switch (prim.Type)
        {
            case Types.BigInt:
                return ((JsBigInt) prim)._value;
            case Types.Boolean:
                return ((JsBoolean) prim)._value ? BigInteger.One : BigInteger.Zero;
            case Types.String:
                return StringToBigInt(prim.ToString());
            default:
                ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a " + prim.Type + " to a BigInt");
                return BigInteger.One;
        }
    }

    public static JsBigInt ToJsBigInt(JsValue value)
    {
        return value as JsBigInt ?? ToJsBigIntUnlikely(value);
    }

    private static JsBigInt ToJsBigIntUnlikely(JsValue value)
    {
        var prim = ToPrimitive(value, Types.Number);
        switch (prim.Type)
        {
            case Types.BigInt:
                return (JsBigInt) prim;
            case Types.Boolean:
                return ((JsBoolean) prim)._value ? JsBigInt.One : JsBigInt.Zero;
            case Types.String:
                return new JsBigInt(StringToBigInt(prim.ToString()));
            default:
                ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a " + prim.Type + " to a BigInt");
                return JsBigInt.One;
        }
    }

    internal static BigInteger StringToBigInt(string str)
    {
        if (!TryStringToBigInt(str, out var result))
        {
            // TODO: this doesn't seem a JS syntax error, use a dedicated exception type?
            throw new SyntaxError("CannotConvertToBigInt", " Cannot convert " + str + " to a BigInt").ToException();
        }

        return result;
    }

    internal static bool TryStringToBigInt(string str, out BigInteger result)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            result = BigInteger.Zero;
            return true;
        }

        str = str.Trim();

        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (!char.IsDigit(c))
            {
                if (i == 0 && (c == '-' || Character.IsDecimalDigit(c)))
                {
                    // ok
                    continue;
                }

                if (i != 1 && Character.IsHexDigit(c))
                {
                    // ok
                    continue;
                }

                if (i == 1 && (Character.IsDecimalDigit(c) || c is 'x' or 'X' or 'b' or 'B' or 'o' or 'O'))
                {
                    // allowed, can be probably parsed
                    continue;
                }

                result = default;
                return false;
            }
        }

        // check if we can get by using plain parsing
        if (BigInteger.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        if (str.Length > 2)
        {
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // we get better precision if we don't hit floating point parsing that is performed by Esprima
#if SUPPORTS_SPAN_PARSE
                    var source = str.AsSpan(2);
#else
                var source = str.Substring(2);
#endif

                var c = source[0];
                if (c > 7 && Character.IsHexDigit(c))
                {
                    // ensure we get positive number
                    source = "0" + source.ToString();
                }

                if (BigInteger.TryParse(source, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                {
                    return true;
                }
            }
            else if (str.StartsWith("0o", StringComparison.OrdinalIgnoreCase) && Character.IsOctalDigit(str[2]))
            {
                // try parse large octal
                var bigInteger = new BigInteger();
                for (var i = 2; i < str.Length; i++)
                {
                    var c = str[i];
                    if (!Character.IsHexDigit(c))
                    {
                        return false;
                    }

                    bigInteger = bigInteger * 8 + c - '0';
                }

                result = bigInteger;
                return true;
            }
            else if (str.StartsWith("0b", StringComparison.OrdinalIgnoreCase) && Character.IsDecimalDigit(str[2]))
            {
                // try parse large binary
                var bigInteger = new BigInteger();
                for (var i = 2; i < str.Length; i++)
                {
                    var c = str[i];

                    if (c != '0' && c != '1')
                    {
                        // not good
                        return false;
                    }

                    bigInteger <<= 1;
                    bigInteger += c == '1' ? 1 : 0;
                }

                result = bigInteger;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tobigint64
    /// </summary>
    internal static long ToBigInt64(BigInteger value)
    {
        var int64bit = BigIntegerModulo(value, BigInteger.Pow(2, 64));
        if (int64bit >= BigInteger.Pow(2, 63))
        {
            return (long) (int64bit - BigInteger.Pow(2, 64));
        }

        return (long) int64bit;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tobiguint64
    /// </summary>
    internal static ulong ToBigUint64(BigInteger value)
    {
        return (ulong) BigIntegerModulo(value, BigInteger.Pow(2, 64));
    }

    /// <summary>
    /// Implements the JS spec modulo operation as expected.
    /// </summary>
    internal static BigInteger BigIntegerModulo(BigInteger a, BigInteger n)
    {
        return (a %= n) < 0 && n > 0 || a > 0 && n < 0 ? a + n : a;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-canonicalnumericindexstring
    /// </summary>
    internal static double? CanonicalNumericIndexString(JsValue value)
    {
        if (value is JsNumber jsNumber)
        {
            return jsNumber._value;
        }

        if (value is JsString jsString)
        {
            if (string.Equals(jsString.ToString(), "-0", StringComparison.Ordinal))
            {
                return JsNumber.NegativeZero._value;
            }

            var n = ToNumber(value);
            if (!JsValue.SameValue(ToString(n), value))
            {
                return null;
            }

            return n;
        }

        return null;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-toindex
    /// </summary>
    public static uint ToIndex(Realm realm, JsValue value)
    {
        if (value.IsUndefined())
        {
            return 0;
        }

        var integerIndex = ToIntegerOrInfinity(value);
        if (integerIndex < 0)
        {
            ExceptionHelper.ThrowRangeError(realm);
        }

        var index = ToLength(integerIndex);
        if (integerIndex != index)
        {
            ExceptionHelper.ThrowRangeError(realm, "Invalid index");
        }

        return (uint) Math.Min(uint.MaxValue, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(long i)
    {
        var temp = intToString;
        return (ulong) i < (ulong) temp.Length
            ? temp[i]
            : i.ToString(CultureInfo.InvariantCulture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(int i)
    {
        var temp = intToString;
        return (uint) i < (uint) temp.Length
            ? temp[i]
            : i.ToString(CultureInfo.InvariantCulture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(uint i)
    {
        var temp = intToString;
        return i < (uint) temp.Length
            ? temp[i]
            : i.ToString(CultureInfo.InvariantCulture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(char c)
    {
        var temp = charToString;
        return (uint) c < (uint) temp.Length
            ? temp[c]
            : c.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(ulong i)
    {
        var temp = intToString;
        return i < (ulong) temp.Length
            ? temp[i]
            : i.ToString(CultureInfo.InvariantCulture);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(double d)
    {
        if (CanBeStringifiedAsLong(d))
        {
            // we are dealing with integer that can be cached
            return ToString((long) d);
        }

        return NumberPrototype.ToNumberString(d);
    }

    /// <summary>
    /// Returns true if <see cref="ToString(long)"/> can be used for the
    /// provided value <paramref name="d"/>, otherwise false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanBeStringifiedAsLong(double d)
    {
        return d > long.MinValue && d < long.MaxValue && Math.Abs(d % 1) <= DoubleIsIntegerTolerance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToString(BigInteger bigInteger)
    {
        return bigInteger.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/6.0/#sec-topropertykey
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsValue ToPropertyKey(JsValue o)
    {
        const InternalTypes PropertyKeys = InternalTypes.String | InternalTypes.Symbol | InternalTypes.PrivateName;
        return (o._type & PropertyKeys) != InternalTypes.Empty
            ? o
            : ToPropertyKeyNonString(o);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsValue ToPropertyKeyNonString(JsValue o)
    {
        const InternalTypes PropertyKeys = InternalTypes.String | InternalTypes.Symbol | InternalTypes.PrivateName;
        var primitive = ToPrimitive(o, Types.String);
        return (primitive._type & PropertyKeys) != InternalTypes.Empty
            ? primitive
            : ToStringNonString(primitive);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-tostring
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToString(JsValue o)
    {
        return o.IsString() ? o.ToString() : ToStringNonString(o);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsString ToJsString(JsValue o)
    {
        if (o is JsString s)
        {
            return s;
        }

        return JsString.Create(ToStringNonString(o));
    }

    private static string ToStringNonString(JsValue o)
    {
        var type = o._type & ~InternalTypes.InternalFlags;
        switch (type)
        {
            case InternalTypes.Boolean:
                return ((JsBoolean) o)._value ? "true" : "false";
            case InternalTypes.Integer:
                return ToString((int) ((JsNumber) o)._value);
            case InternalTypes.Number:
                return ToString(((JsNumber) o)._value);
            case InternalTypes.BigInt:
                return ToString(((JsBigInt) o)._value);
            case InternalTypes.Symbol:
                ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a Symbol value to a string");
                return null;
            case InternalTypes.Undefined:
                return "undefined";
            case InternalTypes.Null:
                return "null";
            case InternalTypes.PrivateName:
                return o.ToString();
            case InternalTypes.Object when o is IObjectWrapper p:
                return p.Target?.ToString()!;
            default:
                return ToString(ToPrimitive(o, Types.String));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ObjectInstance ToObject(Realm realm, JsValue value)
    {
        if (value is ObjectInstance oi)
        {
            return oi;
        }

        return ToObjectNonObject(realm, value);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-isintegralnumber
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsIntegralNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value % 1 == 0;
    }

    private static ObjectInstance ToObjectNonObject(Realm realm, JsValue value)
    {
        var type = value._type & ~InternalTypes.InternalFlags;
        var intrinsics = realm.Intrinsics;
        switch (type)
        {
            case InternalTypes.Boolean:
                return intrinsics.Boolean.Construct((JsBoolean) value);
            case InternalTypes.Number:
            case InternalTypes.Integer:
                return intrinsics.Number.Construct((JsNumber) value);
            case InternalTypes.BigInt:
                return intrinsics.BigInt.Construct((JsBigInt) value);
            case InternalTypes.String:
                return intrinsics.String.Construct(value as JsString ?? JsString.Create(value.ToString()));
            case InternalTypes.Symbol:
                return intrinsics.Symbol.Construct((JsSymbol) value);
            case InternalTypes.Null:
            case InternalTypes.Undefined:
                ExceptionHelper.ThrowTypeError(realm, "Cannot convert undefined or null to object");
                return null;
            default:
                ExceptionHelper.ThrowTypeError(realm, "Cannot convert given item to object");
                return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void CheckObjectCoercible(
        Engine engine,
        JsValue o,
        Node sourceNode,
        string referenceName)
    {
        if (!engine._referenceResolver.CheckCoercible(o))
        {
            ThrowMemberNullOrUndefinedError(engine, o, sourceNode, referenceName);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMemberNullOrUndefinedError(
        Engine engine,
        JsValue o,
        Node sourceNode,
        string referencedName)
    {
        referencedName ??= "unknown";
        var message = $"Cannot read property '{referencedName}' of {o}";
        throw new JavaScriptException(engine.Realm.Intrinsics.TypeError, message)
            .SetJavaScriptCallstack(engine, sourceNode.Location, overwriteExisting: true);
    }

    [Obsolete("Use TypeConverter.RequireObjectCoercible")]
    public static void CheckObjectCoercible(Engine engine, JsValue o) => RequireObjectCoercible(engine, o);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-requireobjectcoercible
    /// </summary>
    public static void RequireObjectCoercible(Engine engine, JsValue o)
    {
        if (o._type < InternalTypes.Boolean)
        {
            ExceptionHelper.ThrowTypeError(engine.Realm, $"Cannot call method on {o}");
        }
    }
}

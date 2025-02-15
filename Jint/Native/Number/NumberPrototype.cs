using System.Diagnostics;
using System.Globalization;
using System.Text;
using Jint.Collections;
using Jint.Native.Number.Dtoa;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Number;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-the-number-prototype-object
/// </summary>
internal sealed class NumberPrototype : NumberInstance
{
    private const int SmallDtoaLength = FastDtoa.KFastDtoaMaximalLength + 8;
    private const int LargeDtoaLength = 101;

    private readonly Realm _realm;
    private readonly NumberConstructor _constructor;

    internal NumberPrototype(
        Engine engine,
        Realm realm,
        NumberConstructor constructor,
        ObjectPrototype objectPrototype)
        : base(engine, InternalTypes.Object | InternalTypes.PlainObject)
    {
        _prototype = objectPrototype;
        _realm = realm;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(8, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, true, false, true),
            ["toString"] = new PropertyDescriptor(new ClrFunction(Engine, "toString", ToNumberString, 1, PropertyFlag.Configurable), true, false, true),
            ["toLocaleString"] = new PropertyDescriptor(new ClrFunction(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), true, false, true),
            ["valueOf"] = new PropertyDescriptor(new ClrFunction(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true),
            ["toFixed"] = new PropertyDescriptor(new ClrFunction(Engine, "toFixed", ToFixed, 1, PropertyFlag.Configurable), true, false, true),
            ["toExponential"] = new PropertyDescriptor(new ClrFunction(Engine, "toExponential", ToExponential, 1, PropertyFlag.Configurable), true, false, true),
            ["toPrecision"] = new PropertyDescriptor(new ClrFunction(Engine, "toPrecision", ToPrecision, 1, PropertyFlag.Configurable), true, false, true)
        };
        SetProperties(properties);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-number.prototype.tolocalestring
    /// </summary>
    private JsValue ToLocaleString(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsNumber() && thisObject is not NumberInstance)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var m = TypeConverter.ToNumber(thisObject);

        if (double.IsNaN(m))
        {
            return "NaN";
        }

        if (m == 0)
        {
            return JsString.NumberZeroString;
        }

        if (m < 0)
        {
            return "-" + ToLocaleString(-m, arguments);
        }

        if (double.IsPositiveInfinity(m) || m >= double.MaxValue)
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(m) || m <= -double.MaxValue)
        {
            return "-Infinity";
        }

        var numberFormat = (NumberFormatInfo) Engine.Options.Culture.NumberFormat.Clone();

        try
        {
            if (arguments.Length > 0 && arguments[0].IsString())
            {
                var cultureArgument = arguments[0].ToString();
                numberFormat = (NumberFormatInfo) CultureInfo.GetCultureInfo(cultureArgument).NumberFormat.Clone();
            }

            int decDigitCount = NumberIntlHelper.GetDecimalDigitCount(m);
            numberFormat.NumberDecimalDigits = decDigitCount;
        }
        catch (CultureNotFoundException)
        {
            ExceptionHelper.ThrowRangeError(_realm, "Incorrect locale information provided");
        }

        return m.ToString("n", numberFormat);
    }

    private JsValue ValueOf(JsValue thisObject, JsCallArguments arguments)
    {
        if (thisObject is NumberInstance ni)
        {
            return ni.NumberData;
        }

        if (thisObject is JsNumber)
        {
            return thisObject;
        }

        ExceptionHelper.ThrowTypeError(_realm);
        return null;
    }

    private const double Ten21 = 1e21;

    private JsValue ToFixed(JsValue thisObject, JsCallArguments arguments)
    {
        var f = (int) TypeConverter.ToInteger(arguments.At(0, 0));
        if (f < 0 || f > 100)
        {
            ExceptionHelper.ThrowRangeError(_realm, "fractionDigits argument must be between 0 and 100");
        }

        // limitation with .NET, max is 99
        if (f == 100)
        {
            ExceptionHelper.ThrowRangeError(_realm, "100 fraction digits is not supported due to .NET format specifier limitation");
        }

        var x = TypeConverter.ToNumber(thisObject);

        if (double.IsNaN(x))
        {
            return "NaN";
        }

        if (x >= Ten21)
        {
            return ToNumberString(x);
        }

        // handle non-decimal with greater precision
        if (System.Math.Abs(x - (long) x) < JsNumber.DoubleIsIntegerTolerance)
        {
            return ((long) x).ToString("f" + f, CultureInfo.InvariantCulture);
        }

        return x.ToString("f" + f, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-number.prototype.toexponential
    /// </summary>
    private JsValue ToExponential(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsNumber() && ReferenceEquals(thisObject.TryCast<NumberInstance>(), null))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var x = TypeConverter.ToNumber(thisObject);
        var fractionDigits = arguments.At(0);
        if (fractionDigits.IsUndefined())
        {
            fractionDigits = JsNumber.PositiveZero;
        }

        var f = (int) TypeConverter.ToInteger(fractionDigits);

        if (double.IsNaN(x))
        {
            return "NaN";
        }

        if (double.IsInfinity(x))
        {
            return thisObject.ToString();
        }

        if (f < 0 || f > 100)
        {
            ExceptionHelper.ThrowRangeError(_realm, "fractionDigits argument must be between 0 and 100");
        }

        if (arguments.At(0).IsUndefined())
        {
            f = -1;
        }

        bool negative = false;
        if (x < 0)
        {
            x = -x;
            negative = true;
        }

        int decimalPoint;
        var dtoaBuilder = new DtoaBuilder(stackalloc char[f == -1 ? SmallDtoaLength : LargeDtoaLength]);

        if (f == -1)
        {
            DtoaNumberFormatter.DoubleToAscii(
                ref dtoaBuilder,
                x,
                DtoaMode.Shortest,
                requested_digits: 0,
                out _,
                out decimalPoint);
            f = dtoaBuilder.Length - 1;
        }
        else
        {
            DtoaNumberFormatter.DoubleToAscii(
                ref dtoaBuilder,
                x,
                DtoaMode.Precision,
                requested_digits: f + 1,
                out _,
                out decimalPoint);
        }

        Debug.Assert(dtoaBuilder.Length > 0);
        Debug.Assert(dtoaBuilder.Length <= f + 1);

        int exponent = decimalPoint - 1;
        var result = CreateExponentialRepresentation(ref dtoaBuilder, exponent, negative, f+1);
        return result;
    }

    private JsValue ToPrecision(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsNumber() && ReferenceEquals(thisObject.TryCast<NumberInstance>(), null))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var x = TypeConverter.ToNumber(thisObject);
        var precisionArgument = arguments.At(0);

        if (precisionArgument.IsUndefined())
        {
            return TypeConverter.ToString(x);
        }

        var p = (int) TypeConverter.ToInteger(precisionArgument);

        if (double.IsNaN(x))
        {
            return "NaN";
        }

        if (double.IsInfinity(x))
        {
            return thisObject.ToString();
        }

        if (p < 1 || p > 100)
        {
            ExceptionHelper.ThrowRangeError(_realm, "precision must be between 1 and 100");
        }

        var dtoaBuilder = new DtoaBuilder(stackalloc char[LargeDtoaLength]);
        DtoaNumberFormatter.DoubleToAscii(
            ref dtoaBuilder,
            x,
            DtoaMode.Precision,
            p,
            out var negative,
            out var decimalPoint);


        int exponent = decimalPoint - 1;
        if (exponent < -6 || exponent >= p)
        {
            return CreateExponentialRepresentation(ref dtoaBuilder, exponent, negative, p);
        }

        var sb = new ValueStringBuilder(stackalloc char[128]);

        // Use fixed notation.
        if (negative)
        {
            sb.Append('-');
        }

        if (decimalPoint <= 0)
        {
            sb.Append("0.");
            sb.Append('0', -decimalPoint);
            sb.Append(dtoaBuilder._chars.Slice(0, dtoaBuilder.Length));
            sb.Append('0', p - dtoaBuilder.Length);
        }
        else
        {
            int m = System.Math.Min(dtoaBuilder.Length, decimalPoint);
            sb.Append(dtoaBuilder._chars.Slice(0, m));
            sb.Append('0', System.Math.Max(0, decimalPoint - dtoaBuilder.Length));
            if (decimalPoint < p)
            {
                sb.Append('.');
                var extra = negative ? 2 : 1;
                if (dtoaBuilder.Length > decimalPoint)
                {
                    int len = dtoaBuilder.Length - decimalPoint;
                    int n = System.Math.Min(len, p - (sb.Length - extra));
                    sb.Append(dtoaBuilder._chars.Slice(decimalPoint, n));
                }

                sb.Append('0', System.Math.Max(0, extra + (p - sb.Length)));
            }
        }

        return sb.ToString();
    }

    private static string CreateExponentialRepresentation(
        ref  DtoaBuilder buffer,
        int exponent,
        bool negative,
        int significantDigits)
    {
        bool negativeExponent = false;
        if (exponent < 0)
        {
            negativeExponent = true;
            exponent = -exponent;
        }

        var sb = new ValueStringBuilder(stackalloc char[128]);
        if (negative)
        {
            sb.Append('-');
        }
        sb.Append(buffer[0]);
        if (significantDigits != 1)
        {
            sb.Append('.');
            sb.Append(buffer.Slice(1, buffer.Length - 1));
            int length = buffer.Length;
            sb.Append('0', significantDigits - length);
        }

        sb.Append('e');
        sb.Append(negativeExponent ? '-' : '+');
        sb.Append(exponent);

        return sb.ToString();
    }

    private JsValue ToNumberString(JsValue thisObject, JsCallArguments arguments)
    {
        if (!thisObject.IsNumber() && (ReferenceEquals(thisObject.TryCast<NumberInstance>(), null)))
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        var radix = arguments.At(0).IsUndefined()
            ? 10
            : (int) TypeConverter.ToInteger(arguments.At(0));

        if (radix < 2 || radix > 36)
        {
            ExceptionHelper.ThrowRangeError(_realm, "radix must be between 2 and 36");
        }

        var x = TypeConverter.ToNumber(thisObject);

        if (double.IsNaN(x))
        {
            return "NaN";
        }

        if (x == 0)
        {
            return JsString.NumberZeroString;
        }

        if (double.IsPositiveInfinity(x) || x >= double.MaxValue)
        {
            return "Infinity";
        }

        if (x < 0)
        {
            return "-" + ToNumberString(-x, arguments);
        }

        if (radix == 10)
        {
            return ToNumberString(x);
        }

        var integer = (long) x;
        var fraction = x -  integer;

        string result = NumberPrototype.ToBase(integer, radix);
        if (fraction != 0)
        {
            result += "." + NumberPrototype.ToFractionBase(fraction, radix);
        }

        return result;
    }

    internal static string ToBase(long n, int radix)
    {
        const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (n == 0)
        {
            return "0";
        }

        var sb = new ValueStringBuilder(stackalloc char[64]);
        while (n > 0)
        {
            var digit = (int) (n % radix);
            n /= radix;
            sb.Append(Digits[digit]);
        }
        sb.Reverse();
        return sb.ToString();
    }

    internal static string ToFractionBase(double n, int radix)
    {
        // based on the repeated multiplication method
        // http://www.mathpath.org/concepts/Num/frac.htm

        const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        if (n == 0)
        {
            return "0";
        }

        var result = new ValueStringBuilder(stackalloc char[64]);
        while (n > 0 && result.Length < 50) // arbitrary limit
        {
            var c = n*radix;
            var d = (int) c;
            n = c - d;

            result.Append(Digits[d]);
        }

        return result.ToString();
    }

    internal static string ToNumberString(double m)
    {
        if (double.IsNaN(m))
        {
            return "NaN";
        }

        if (m == 0)
        {
            return "0";
        }

        if (double.IsInfinity(m))
        {
            return double.IsNegativeInfinity(m) ? "-Infinity" : "Infinity";
        }

        var builder = new DtoaBuilder(stackalloc char[SmallDtoaLength]);

        DtoaNumberFormatter.DoubleToAscii(
            ref builder,
            m,
            DtoaMode.Shortest,
            0,
            out var negative,
            out var decimal_point);


        var stringBuilder = new ValueStringBuilder(stackalloc char[64]);
        if (negative)
        {
            stringBuilder.Append('-');
        }

        if (builder.Length <= decimal_point && decimal_point <= 21)
        {
            // ECMA-262 section 9.8.1 step 6.
            stringBuilder.Append(builder._chars.Slice(0, builder.Length));
            stringBuilder.Append('0', decimal_point - builder.Length);
        }
        else if (0 < decimal_point && decimal_point <= 21)
        {
            // ECMA-262 section 9.8.1 step 7.
            stringBuilder.Append(builder._chars.Slice(0, decimal_point));
            stringBuilder.Append('.');
            stringBuilder.Append(builder._chars.Slice(decimal_point, builder.Length - decimal_point));
        }
        else if (decimal_point <= 0 && decimal_point > -6)
        {
            // ECMA-262 section 9.8.1 step 8.
            stringBuilder.Append("0.");
            stringBuilder.Append('0', -decimal_point);
            stringBuilder.Append(builder._chars.Slice(0, builder.Length));
        }
        else
        {
            // ECMA-262 section 9.8.1 step 9 and 10 combined.
            stringBuilder.Append(builder._chars[0]);
            if (builder.Length != 1)
            {
                stringBuilder.Append('.');
                stringBuilder.Append(builder._chars.Slice(1, builder.Length - 1));
            }

            stringBuilder.Append('e');
            stringBuilder.Append((decimal_point >= 0) ? '+' : '-');
            int exponent = decimal_point - 1;
            if (exponent < 0)
            {
                exponent = -exponent;
            }

            stringBuilder.Append(exponent);
        }

        return stringBuilder.ToString();
    }
}

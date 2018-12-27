using System;
using System.Globalization;
using Jint.Native.Number.Dtoa;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Number
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.4
    /// </summary>
    public sealed class NumberPrototype : NumberInstance
    {
        private NumberPrototype(Engine engine)
            : base(engine)
        {
        }

        public static NumberPrototype CreatePrototypeObject(Engine engine, NumberConstructor numberConstructor)
        {
            var obj = new NumberPrototype(engine)
            {
                Prototype = engine.Object.PrototypeObject,
                NumberData = JsNumber.Create(0),
                Extensible = true
            };

            obj.FastAddProperty("constructor", numberConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToNumberString, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("toFixed", new ClrFunctionInstance(Engine, "toFixed", ToFixed, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("toExponential", new ClrFunctionInstance(Engine, "toExponential", ToExponential, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("toPrecision", new ClrFunctionInstance(Engine, "toPrecision", ToPrecision, 1, PropertyFlag.Configurable), true, false, true);
        }

        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            if (!thisObject.IsNumber() && ReferenceEquals(thisObject.TryCast<NumberInstance>(), null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var m = TypeConverter.ToNumber(thisObject);

            if (double.IsNaN(m))
            {
                return "NaN";
            }

            if (m == 0)
            {
                return "0";
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

            return m.ToString("n", Engine.Options._Culture);
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is NumberInstance ni)
            {
                return ni.NumberData;
            }

            if (thisObj is JsNumber)
            {
                return thisObj;
            }

            return ExceptionHelper.ThrowTypeError<JsValue>(Engine);
        }

        private const double Ten21 = 1e21;

        private JsValue ToFixed(JsValue thisObj, JsValue[] arguments)
        {
            var f = (int)TypeConverter.ToInteger(arguments.At(0, 0));
            if (f < 0 || f > 100)
            {
                ExceptionHelper.ThrowRangeError(_engine, "fractionDigits argument must be between 0 and 100");
            }

            var x = TypeConverter.ToNumber(thisObj);

            if (double.IsNaN(x))
            {
                return "NaN";
            }

            if (x >= Ten21)
            {
                return ToNumberString(x);
            }

            // handle non-decimal with greater precision
            if (x - (long) x < JsNumber.DoubleIsIntegerTolerance)
            {
                return ((long) x).ToString("f" + f, CultureInfo.InvariantCulture);
            }

            return x.ToString("f" + f, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// https://www.ecma-international.org/ecma-262/6.0/#sec-number.prototype.toexponential
        /// </summary>
        private JsValue ToExponential(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsNumber() && ReferenceEquals(thisObj.TryCast<NumberInstance>(), null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var x = TypeConverter.ToNumber(thisObj);
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

            bool negative = false;
            if (x < 0)
            {
                negative = true;
                x = -x;
            }

            if (double.IsPositiveInfinity(x))
            {
                return thisObj.ToString();
            }

            if (f < 0 || f > 100)
            {
                ExceptionHelper.ThrowRangeError(_engine, "fractionDigits argument must be between 0 and 100");
            }

            string format = string.Concat("#.", new string('0', f), "e+0");

            // handle non-decimal with greater precision
            string formatted;
            if (x - (long) x < JsNumber.DoubleIsIntegerTolerance)
            {
                formatted = ((long) x).ToString(format, CultureInfo.InvariantCulture);
            }
            else
            {
                formatted = x.ToString(format, CultureInfo.InvariantCulture);
            }

            return negative ? "-" + formatted : formatted;
        }

        private JsValue ToPrecision(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsNumber() && ReferenceEquals(thisObj.TryCast<NumberInstance>(), null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            var x = TypeConverter.ToNumber(thisObj);
            var precisionArgument = arguments.At(0);

            if (precisionArgument.IsUndefined())
            {
                return TypeConverter.ToString(x);
            }

            var p = (int) TypeConverter.ToInteger(precisionArgument);

            if (double.IsInfinity(x) || double.IsNaN(x))
            {
                return TypeConverter.ToString(x);
            }

            if (p < 1 || p > 100)
            {
                ExceptionHelper.ThrowRangeError(_engine, "precision must be between 1 and 100");
            }

            // Get the number of decimals
            bool negative = false;
            if (x < 0)
            {
                negative = true;
                x = -x;
            }

            var decimalRep = FastDtoa.NumberToString(
                x,
                FastDtoa.FastDtoaMode.Precision,
                p,
                out var decimal_rep_length,
                out var decimal_point);

            if (decimalRep == null)
            {
                p -= GetNumberOfDigits(x);
                p = p < 0 ? 0 : p;

                p += GetNumberOfDecimals(x);
                var formatted = x.ToString("f" + p, CultureInfo.InvariantCulture);
                return negative ? "-" + formatted : formatted;
            }

            int exponent = decimal_point - 1;

            if (exponent < -6 || exponent >= p)
            {
                return CreateExponentialRepresentation(decimalRep, exponent, negative, p);
            }

            using (var builder = StringBuilderPool.GetInstance())
            {
                // Use fixed notation.
                if (negative)
                {
                    builder.Builder.Append('-');
                }

                if (decimal_point <= 0)
                {
                    builder.Builder.Append("0.");
                    builder.Builder.Append('0', -decimal_point);
                    builder.Builder.Append(decimalRep);
                    builder.Builder.Append('0', p - decimal_rep_length);
                }
                else
                {
                    int m = System.Math.Min(decimal_rep_length, decimal_point);
                    builder.Builder.Append(decimalRep, 0, m);
                    builder.Builder.Append('0', System.Math.Max(0, decimal_point - decimal_rep_length));
                    if (decimal_point < p)
                    {
                        builder.Builder.Append('.');
                        var extra = negative ? 2 : 1;
                        if (decimal_rep_length > decimal_point)
                        {
                            int len = decimalRep.Length - decimal_point;
                            int n = System.Math.Min(len, p - (builder.Length - 1 - extra));
                            builder.Builder.Append(decimalRep, decimal_point, n);
                        }

                        builder.Builder.Append('0', System.Math.Max(0, extra + (p - builder.Length - 1)));
                    }
                }

                return builder.ToString();
            }
        }

        private static int GetNumberOfDigits(double d)
        {
            var abs = System.Math.Abs(d);
            return abs < 1 ? 1 : (int)(System.Math.Log10(abs) + 1);
        }

        private static int GetNumberOfDecimals(double d)
        {
            return BitConverter.GetBytes(decimal.GetBits((decimal) d)[3])[2];
        }

        private static string CreateExponentialRepresentation(
            string decimalRep,
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

            var builder = StringBuilderPool.GetInstance();

            if (negative)
            {
                builder.Builder.Append('-');
            }
            builder.Builder.Append(decimalRep[0]);
            if (significantDigits != 1)
            {
                builder.Builder.Append('.');
                builder.Builder.Append(decimalRep, 1, decimalRep.Length - 1);
                int length = decimalRep.Length;
                builder.Builder.Append('0', significantDigits - length);
            }

            builder.Builder.Append('e');
            builder.Builder.Append(negativeExponent ? '-' : '+');
            builder.Builder.Append(exponent);
            return builder.ToString();
        }

        private JsValue ToNumberString(JsValue thisObject, JsValue[] arguments)
        {
            if (!thisObject.IsNumber() && (ReferenceEquals(thisObject.TryCast<NumberInstance>(), null)))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            var radix = arguments.At(0).IsUndefined()
                ? 10
                : (int) TypeConverter.ToInteger(arguments.At(0));

            if (radix < 2 || radix > 36)
            {
                ExceptionHelper.ThrowRangeError(_engine, "radix must be between 2 and 36");
            }

            var x = TypeConverter.ToNumber(thisObject);

            if (double.IsNaN(x))
            {
                return "NaN";
            }

            if (x == 0)
            {
                return "0";
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

            string result = ToBase(integer, radix);
            if (fraction != 0)
            {
                result += "." + ToFractionBase(fraction, radix);
            }

            return result;
        }

        public static string ToBase(long n, int radix)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (n == 0)
            {
                return "0";
            }

            using (var result = StringBuilderPool.GetInstance())
            {
                while (n > 0)
                {
                    var digit = (int) (n % radix);
                    n = n / radix;
                    result.Builder.Insert(0, digits[digit]);
                }

                return result.ToString();
            }
        }

        public static string ToFractionBase(double n, int radix)
        {
            // based on the repeated multiplication method
            // http://www.mathpath.org/concepts/Num/frac.htm

            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (n == 0)
            {
                return "0";
            }

            using (var result = StringBuilderPool.GetInstance())
            {
                while (n > 0 && result.Length < 50) // arbitrary limit
                {
                    var c = n*radix;
                    var d = (int) c;
                    n = c - d;

                    result.Builder.Append(digits[d]);
                }

                return result.ToString();
            }
        }

        public static string ToNumberString(double m)
        {
            if (double.IsNaN(m))
            {
                return "NaN";
            }

            if (m == 0)
            {
                return "0";
            }

            if (double.IsPositiveInfinity(m) || m >= double.MaxValue)
            {
                return "Infinity";
            }

            if (m < 0)
            {
                return "-" + ToNumberString(-m);
            }

            // V8 FastDtoa can't convert all numbers, so try it first but
            // fall back to old DToA in case it fails
            var result = FastDtoa.NumberToString(
                m,
                FastDtoa.FastDtoaMode.Shortest,
                0,
                out var length,
                out var decimalPoint);

            if (result != null)
            {
                return result;
            }

            return CreateFallbackString(m);
        }

        private static string CreateFallbackString(double m)
        {
            // s is all digits (significand)
            // k number of digits of s
            // n total of digits in fraction s*10^n-k=m
            // 123.4 s=1234, k=4, n=3
            // 1234000 s = 1234, k=4, n=7
            string s = null;
            var rFormat = m.ToString("r", CultureInfo.InvariantCulture);
            if (rFormat.IndexOf("e", StringComparison.OrdinalIgnoreCase) == -1)
            {
                s = rFormat.Replace(".", "").TrimStart('0').TrimEnd('0');
            }

            const string format = "0.00000000000000000e0";
            var parts = m.ToString(format, CultureInfo.InvariantCulture).Split('e');
            if (s == null)
            {
                s = parts[0].TrimEnd('0').Replace(".", "");
            }

            var n = int.Parse(parts[1]) + 1;
            var k = s.Length;

            if (k <= n && n <= 21)
            {
                return s + new string('0', n - k);
            }

            if (0 < n && n <= 21)
            {
                return s.Substring(0, n) + '.' + s.Substring(n);
            }

            if (-6 < n && n <= 0)
            {
                return "0." + new string('0', -n) + s;
            }

            if (k == 1)
            {
                return s + "e" + (n - 1 < 0 ? "-" : "+") + System.Math.Abs(n - 1);
            }

            return s.Substring(0, 1) + "." + s.Substring(1) + "e" + (n - 1 < 0 ? "-" : "+") + System.Math.Abs(n - 1);
        }
    }
}

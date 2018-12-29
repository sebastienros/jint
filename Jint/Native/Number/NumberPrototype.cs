using System;
using System.Diagnostics;
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
        // share buffer to reduce memory usage
        private readonly DtoaBuilder cachedBuffer = new DtoaBuilder();


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
            if (System.Math.Abs(x - (long) x) < JsNumber.DoubleIsIntegerTolerance)
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

            if (double.IsInfinity(x))
            {
                return thisObj.ToString();
            }

            if (f < 0 || f > 100)
            {
                ExceptionHelper.ThrowRangeError(_engine, "fractionDigits argument must be between 0 and 100");
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
            if (f == -1)
            {
                DtoaNumberFormatter.DoubleToAscii(
                    cachedBuffer,
                    x,
                    DtoaMode.Shortest,
                    requested_digits: 0,
                    out _,
                    out decimalPoint);
                f = cachedBuffer.Length - 1;
            }
            else
            {
                DtoaNumberFormatter.DoubleToAscii(
                    cachedBuffer,
                    x,
                    DtoaMode.Precision,
                    requested_digits: f + 1,
                    out _,
                    out decimalPoint);
            }

            Debug.Assert(cachedBuffer.Length > 0);
            Debug.Assert(cachedBuffer.Length <= f + 1);

            int exponent = decimalPoint - 1;
            var result = CreateExponentialRepresentation(cachedBuffer, exponent, negative, f+1);
            return result;
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

            DtoaNumberFormatter.DoubleToAscii(
                cachedBuffer,
                x,
                DtoaMode.Precision,
                p,
                out var negative,
                out var decimalPoint);


            int exponent = decimalPoint - 1;
            if (exponent < -6 || exponent >= p)
            {
                return CreateExponentialRepresentation(cachedBuffer, exponent, negative, p);
            }

            using (var builder = StringBuilderPool.GetInstance())
            {
                // Use fixed notation.
                if (negative)
                {
                    builder.Builder.Append('-');
                }

                if (decimalPoint <= 0)
                {
                    builder.Builder.Append("0.");
                    builder.Builder.Append('0', -decimalPoint);
                    builder.Builder.Append(cachedBuffer._chars, 0, cachedBuffer.Length);
                    builder.Builder.Append('0', p - cachedBuffer.Length);
                }
                else
                {
                    int m = System.Math.Min(cachedBuffer.Length, decimalPoint);
                    builder.Builder.Append(cachedBuffer._chars, 0, m);
                    builder.Builder.Append('0', System.Math.Max(0, decimalPoint - cachedBuffer.Length));
                    if (decimalPoint < p)
                    {
                        builder.Builder.Append('.');
                        var extra = negative ? 2 : 1;
                        if (cachedBuffer.Length > decimalPoint)
                        {
                            int len = cachedBuffer.Length - decimalPoint;
                            int n = System.Math.Min(len, p - (builder.Builder.Length - extra));
                            builder.Builder.Append(cachedBuffer._chars, decimalPoint, n);
                        }

                        builder.Builder.Append('0', System.Math.Max(0, extra + (p - builder.Builder.Length)));
                    }
                }

                return builder.ToString();
            }
        }

        private static string CreateExponentialRepresentation(
            DtoaBuilder buffer,
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
            builder.Builder.Append(buffer._chars[0]);
            if (significantDigits != 1)
            {
                builder.Builder.Append('.');
                builder.Builder.Append(buffer._chars, 1, buffer.Length - 1);
                int length = buffer.Length;
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

        private string ToNumberString(double m)
        {
            return NumberToString(m, cachedBuffer);
        }

        internal static string NumberToString(double m, DtoaBuilder builder)
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
                return "-" + NumberToString(-m, builder);
            }

            DtoaNumberFormatter.DoubleToAscii(
                builder,
                m,
                DtoaMode.Shortest,
                0,
                out _,
                out _);

            // check for minus sign
            int firstDigit = builder._chars[0] == '-' ? 1 : 0;
            int decPoint = builder.Point - firstDigit;
            return decPoint < -5 || decPoint > 21
                ? ToExponentialFormat(builder, firstDigit, decPoint)
                : ToFixedFormat(builder, firstDigit, decPoint);
        }

        private static string ToFixedFormat(DtoaBuilder builder, int firstDigit, int decPoint)
        {
            void Fill(char[] array, int fromIndex, int toIndex, char val)
            {
                for (int i = fromIndex; i < toIndex; i++)
                {
                    array[i] = val;
                }
            }

            if (builder.Point < builder.Length)
            {
                // insert decimal point
                if (decPoint > 0)
                {
                    // >= 1, split decimals and insert point
                    System.Array.Copy(builder._chars, builder.Point,builder. _chars, builder.Point + 1, builder.Length - builder.Point);
                    builder._chars[builder.Point] = '.';
                    builder.Length++;
                }
                else
                {
                    // < 1,
                    int target = firstDigit + 2 - decPoint;
                    System.Array.Copy(builder._chars, firstDigit, builder._chars, target, builder.Length - firstDigit);
                    builder._chars[firstDigit] = '0';
                    builder._chars[firstDigit + 1] = '.';
                    if (decPoint < 0)
                    {
                        Fill(builder._chars, firstDigit + 2, target, '0');
                    }
                    builder.Length += 2 - decPoint;
                }
            }
            else if (builder.Point > builder.Length)
            {
                // large integer, add trailing zeroes
                Fill(builder._chars, builder.Length, builder.Point, '0');
                builder.Length += builder. Point - builder.Length;
            }

            return new string(builder._chars, 0, builder.Length);
        }

        private static string ToExponentialFormat(DtoaBuilder builder, int firstDigit, int decPoint)
        {
            if (builder.Length - firstDigit > 1)
            {
                // insert decimal point if more than one digit was produced
                int dot = firstDigit + 1;
                System.Array.Copy(builder._chars, dot, builder._chars, dot + 1, builder.Length - dot);
                builder._chars[dot] = '.';
                builder.Length++;
            }
            builder._chars[builder.Length++] = 'e';
            char sign = '+';
            int exp = decPoint - 1;
            if (exp < 0)
            {
                sign = '-';
                exp = -exp;
            }
            builder._chars[builder.Length++] = sign;

            int charPos = exp > 99 ? builder.Length + 2 : exp > 9 ? builder.Length + 1 : builder.Length;
            builder.Length = charPos + 1;

            for (;;)
            {
                int r = exp%10;
                builder._chars[charPos--] = (char) ('0' + r);
                exp = exp/10;
                if (exp == 0) break;
            }

            return new string(builder._chars, 0, builder.Length);
        }
    }
}

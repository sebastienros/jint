using System;
using System.Globalization;
using System.Text;
using Jint.Native.Number.Dtoa;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Number
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.7.4
    /// </summary>
    public sealed class NumberPrototype : NumberInstance
    {
        private static readonly char[] _numberSeparators = {'.', 'e'};

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
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToNumberString), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, "valueOf", ValueOf), true, false, true);
            FastAddProperty("toFixed", new ClrFunctionInstance(Engine, "toFixed", ToFixed, 1), true, false, true);
            FastAddProperty("toExponential", new ClrFunctionInstance(Engine, "toExponential", ToExponential), true, false, true);
            FastAddProperty("toPrecision", new ClrFunctionInstance(Engine, "toPrecision", ToPrecision), true, false, true);
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
            var number = thisObj.TryCast<NumberInstance>();
            if (ReferenceEquals(number, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return number.NumberData;
        }

        private const double Ten21 = 1e21;

        private JsValue ToFixed(JsValue thisObj, JsValue[] arguments)
        {
            var f = (int)TypeConverter.ToInteger(arguments.At(0, 0));
            if (f < 0 || f > 20)
            {
                ExceptionHelper.ThrowRangeError(_engine, "fractionDigits argument must be between 0 and 20");
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

            return x.ToString("f" + f, CultureInfo.InvariantCulture);
        }

        private JsValue ToExponential(JsValue thisObj, JsValue[] arguments)
        {
            var f = (int)TypeConverter.ToInteger(arguments.At(0, 16));
            if (f < 0 || f > 20)
            {
                ExceptionHelper.ThrowRangeError(_engine, "fractionDigits argument must be between 0 and 20");
            }

            var x = TypeConverter.ToNumber(thisObj);

            if (double.IsNaN(x))
            {
                return "NaN";
            }

            string format = string.Concat("#.", new string('0', f), "e+0");
            return x.ToString(format, CultureInfo.InvariantCulture);
        }

        private JsValue ToPrecision(JsValue thisObj, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(thisObj);

            if (arguments.At(0).IsUndefined())
            {
                return TypeConverter.ToString(x);
            }

            var p = TypeConverter.ToInteger(arguments.At(0));

            if (double.IsInfinity(x) || double.IsNaN(x))
            {
                return TypeConverter.ToString(x);
            }

            if (p < 1 || p > 21)
            {
                ExceptionHelper.ThrowRangeError(_engine, "precision must be between 1 and 21");
            }

            // Get the number of decimals
            string str = x.ToString("e23", CultureInfo.InvariantCulture);
            int decimals = str.IndexOfAny(_numberSeparators);
            decimals = decimals == -1 ? str.Length : decimals;

            p -= decimals;
            p = p < 1 ? 1 : p;

            return x.ToString("f" + p, CultureInfo.InvariantCulture);
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

            var result = new StringBuilder();
            while (n > 0)
            {
                var digit = (int)(n % radix);
                n = n / radix;
                result.Insert(0, digits[digit]);
            }

            return result.ToString();
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

            var result = new StringBuilder();
            while (n > 0 && result.Length < 50) // arbitrary limit
            {
                var c = n*radix;
                var d = (int) c;
                n = c - d;

                result.Append(digits[d]);
            }

            return result.ToString();
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
            var result = FastDtoa.NumberToString(m);
            if (result != null)
            {
                return result;
            }

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

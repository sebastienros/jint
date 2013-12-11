using System;
using Jint.Runtime;
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
            var obj = new NumberPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.PrimitiveValue = 0;
            obj.Extensible = true;

            obj.FastAddProperty("constructor", numberConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToNumberString), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, ToLocaleString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, ValueOf), true, false, true);
            FastAddProperty("toFixed", new ClrFunctionInstance(Engine, ToFixed, 1), true, false, true);
            FastAddProperty("toExponential", new ClrFunctionInstance(Engine, ToExponential), true, false, true);
            FastAddProperty("toPrecision", new ClrFunctionInstance(Engine, ToPrecision), true, false, true);
        }

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private JsValue ValueOf(JsValue thisObj, JsValue[] arguments)
        {
            var number = thisObj.TryCast<NumberInstance>();
            if (number == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return number.PrimitiveValue;
        }

        private JsValue ToFixed(JsValue thisObj, JsValue[] arguments)
        {
            var f = (int)TypeConverter.ToInteger(arguments.At(0, 0));
            if (f < 0 || f > 20)
            {
                throw new JavaScriptException(Engine.RangeError, "fractionDigits argument must be between 0 and 20");
            }

            var x = TypeConverter.ToNumber(thisObj);

            if (double.IsNaN(x))
            {
                return "NaN";
            }

            var sign = "";
            if (x < 0)
            {
                sign = "-";
                x = -x;
            }

            string m = "";
            if (x >= System.Math.Pow(10, 21))
            {
                m = TypeConverter.ToString(x);
            }
            else
            {
                var d = (Decimal)x;
                var significants = GetSignificantDigitCount(d);
                var s = significants.Item1;
                var kdigits = (int)System.Math.Floor(System.Math.Log10((double)s) + 1);
                var n = kdigits - significants.Item2;

                if (n == 0)
                {
                    m = "0";
                }
                else
                {
                    m = (System.Math.Round(x, f) * System.Math.Pow(10,f)).ToString();
                }

                if (f != 0)
                {
                    var k = m.Length;
                    if (k <= f)
                    {
                        var z = new System.String('0', f + 1 - k);
                        m += z;
                        k = f + 1;
                    }
                    var a = m.Substring(0, k - f);
                    var b = m.Substring(k - f);
                    m = a + "." + b;
                }
            }

            return sign + m;
        }

        private JsValue ToExponential(JsValue thisObj, JsValue[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private JsValue ToPrecision(JsValue thisObj, JsValue[] arguments)
        {
            throw new System.NotImplementedException();
        }

        private JsValue ToNumberString(JsValue thisObject, JsValue[] arguments)
        {
            if (!thisObject.IsNumber() && (thisObject.TryCast<NumberInstance>() == null))
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            return ToNumberString(TypeConverter.ToNumber(thisObject));
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

            if (m < 0)
            {
                return "-" + ToNumberString(-m);
            }

            if (double.IsPositiveInfinity(m) || m >= double.MaxValue)
            {
                return "Infinity";
            }

            if (double.IsNegativeInfinity(m) || m <= -double.MaxValue)
            {
                return "-Infinity";
            }

            // s is all digits (significand or mantissa)
            // k number of digits of s
            // n total of digits in fraction s*10^n-k=m
            // 123.4 s=1234, k=4, n=3
            // 1234000 s = 1234, k=4, n=7
            var d = (Decimal)m;
            var significants = GetSignificantDigitCount(d);
            var s = significants.Item1;
            var k = (int)System.Math.Floor(System.Math.Log10((double)s) + 1);
            var n = k - significants.Item2;
            if (m < 1 && m > -1)
            {
                n++;
            }

            while (s % 10 == 0)
            {
                s = s / 10;
                k--;
            }


            if (k <= n && n <= 21)
            {
                return s + new string('0', n - k);
            }

            if (0 < n && n <= 21)
            {
                return s.ToString().Substring(0, n) + '.' + s.ToString().Substring(n);
            }

            if (-6 < n && n <= 0)
            {
                return "0." + new string('0', -n) + s;
            }

            if (k == 1)
            {
                return s + "e" + (n - 1 < 0 ? "-" : "+") + System.Math.Abs(n - 1);
            }

            return s.ToString().Substring(0, 1) + "." + s.ToString().Substring(1) + "e" + (n - 1 < 0 ? "-" : "+") + System.Math.Abs(n - 1);
        }

        public static Tuple<decimal, int> GetSignificantDigitCount(decimal value)
        {
            /* So, the decimal type is basically represented as a fraction of two
             * integers: a numerator that can be anything, and a denominator that is 
             * some power of 10.
             * 
             * For example, the following numbers are represented by
             * the corresponding fractions:
             * 
             * VALUE    NUMERATOR   DENOMINATOR
             * 1        1           1
             * 1.0      10          10
             * 1.012    1012        1000
             * 0.04     4           100
             * 12.01    1201        100
             * 
             * So basically, if the magnitude is greater than or equal to one,
             * the number of digits is the number of digits in the numerator.
             * If it's less than one, the number of digits is the number of     digits
             * in the denominator.
             */

            int[] bits = decimal.GetBits(value);
            int scalePart = bits[3];
            int highPart = bits[2];
            int middlePart = bits[1];
            int lowPart = bits[0];

            if (value >= 1M || value <= -1M)
            {

                var num = new decimal(lowPart, middlePart, highPart, false, 0);

                return new Tuple<decimal, int>(num, (scalePart >> 16) & 0x7fff);
            }
            else
            {

                // Accoring to MSDN, the exponent is represented by
                // bits 16-23 (the 2nd word):
                // http://msdn.microsoft.com/en-us/library/system.decimal.getbits.aspx
                int exponent = (scalePart & 0x00FF0000) >> 16;

                return new Tuple<decimal, int>(lowPart, exponent + 1);
            }
        }

    }
}

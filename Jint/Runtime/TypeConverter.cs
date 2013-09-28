using System;
using System.Globalization;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime
{
    public enum Types
    {
        None,
        Undefined,
        Null,
        Boolean,
        String,
        Number,
        Object
    }

    public class TypeConverter
    {
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.1
        /// </summary>
        /// <param name="input"></param>
        /// <param name="preferredType"></param>
        /// <returns></returns>
        public static object ToPrimitive(object input, Types preferredType = Types.None)
        {
            if (input == Null.Instance || input == Undefined.Instance)
            {
                return input;
            }

            var primitive = input as IPrimitiveType;
            if (primitive != null)
            {
                return primitive.PrimitiveValue;
            }

            var o = input as ObjectInstance;

            if (o == null)
            {
                return input;
            }

            return o.DefaultValue(preferredType);
        }

        public static bool IsPrimitiveValue(object o)
        {
            return o is string
                   || o is double
                   || o is bool
                   || o is Undefined
                   || o is Null;
        }
    
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.2
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static bool ToBoolean(object o)
        {
            if (o is bool)
            {
                return (bool)o;
            }
            
            if (o == Undefined.Instance || o == Null.Instance)
            {
                return false;
            }


            var p = o as IPrimitiveType;
            if (p != null)
            {
                o = ToBoolean(p.PrimitiveValue);
            }

            if (o is double)
            {
                var n = (double) o;
                if (n == 0 || double.IsNaN(n))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            var s = o as string;
            if (s != null)
            {
                if (String.IsNullOrEmpty(s))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            if (o is int)
            {
                return ToBoolean((double)(int)o);
            }

            if (o is uint)
            {
                return ToBoolean((double)(uint)o);
            }

            return true;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static double ToNumber(object o)
        {
            if (o is double)
            {
                return (double)o;
            }

            if (o is int)
            {
                return (int) o;
            }

            if (o is uint)
            {
                return (uint)o;
            }

            if (o is DateTime)
            {
                return ((DateTime)o).Ticks;
            }

            if (o == Undefined.Instance)
            {
                return double.NaN;
            }

            if (o == Null.Instance)
            {
                return 0;
            }

            if (o is bool)
            {
                return (bool)o ? 1 : 0;
            }

            var s = o as string;
            if (s != null)
            {
                double n;
                s = s.Trim();

                if (String.IsNullOrEmpty(s))
                {
                    return 0;
                }

                if ("+Infinity".Equals(s) || "Infinity".Equals(s))
                {
                    return double.PositiveInfinity;
                }

                if ("-Infinity".Equals(s))
                {
                    return double.NegativeInfinity;
                }

                // todo: use a common implementation with JavascriptParser
                try
                {
                    if (!s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        var start = s[0];
                        if (start != '+' && start != '-' && start != '.' && !char.IsDigit(start))
                        {
                            return double.NaN;
                        }

                        n = Double.Parse(s,
                                         NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                                         NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                                         NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
                        if (s.StartsWith("-") && n == 0)
                        {
                            return -0.0;
                        }

                        return n;
                    }

                    int i = int.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                 
                    return i;
                }
                catch (OverflowException)
                {
                    return s.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
                }
                catch
                {
                    return double.NaN;
                }

                return double.NaN;
            }

            return ToNumber(ToPrimitive(o, Types.Number));
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.4
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static double ToInteger(object o)
        {
            var number = ToNumber(o);

            if (double.IsNaN(number))
            {
                return 0;
            }
            
            if (number == 0 || number == double.NegativeInfinity || number == double.PositiveInfinity)
            {
                return number;
            }

            return (long)number;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static int ToInt32(object o)
        {
            if (o is int)
            {
                return (int)o;
            }
                
            return (int)(uint)ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint ToUint32(object o)
        {
            if (o is uint)
            {
                return (uint)o;
            }
                
            return (uint)ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static ushort ToUint16(object o)
        {
            return (ushort)(uint)ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.8
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string ToString(object o)
        {
            var str = o as string;
            if (str != null)
            {
                return str;
            }

            if (o == Undefined.Instance)
            {
                return "undefined";
            }

            if (o == Null.Instance)
            {
                return "null";
            }

            var p = o as IPrimitiveType;
            if (p != null)
            {
                o = p.PrimitiveValue;
            }

            if (o is bool)
            {
                return (bool) o ? "true" : "false";
            }

            if (o is double)
            {
                var m = (double) o;
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
                    return "-" + ToString(-m);
                }

                if (m == double.PositiveInfinity)
                {
                    return "Infinity";
                }

                if (m == double.NegativeInfinity)
                {
                    return "-Infinity";
                }

                // s is all digits (significand or mantissa)
                // k number of digits of s
                // n total of digits in fraction s*10^n-k=m
                // 123.4 s=1234, k=4, n=3
                // 1234000 s = 1234, k=4, n=7
                var d = (Decimal) m;
                var significants = GetSignificantDigitCount(d);
                var s = significants.Item1;
                var k = (int)Math.Floor(Math.Log10((double) s)+1);
                var n = k - significants.Item2;
                if (m < 1 && m > -1)
                {
                    n++;
                }

                while (s%10 == 0)
                {
                    s = s/10;
                    k--;
                }

                
                if (k <= n && n <= 21)
                {
                    return s + new string('0', (int)(n - k));
                }

                if (0 < n && n <= 21)
                {
                    return s.ToString().Substring(0, (int)n) + '.' + s.ToString().Substring((int)n);
                }

                if (-6 < n && n <= 0)
                {
                    return "0." + new string('0', -n) + s;
                }

                if (k == 1)
                {
                    return s + "e" + (n - 1 < 0 ? "-" : "+") + Math.Abs(n - 1);
                }

                return s.ToString().Substring(0, 1) + "." + s.ToString().Substring(1) + "e" + (n - 1 < 0 ? "-" : "+") +
                       Math.Abs(n - 1);
            }

            if (o is int)
            {
                return ToString((double)(int)o);
            }

            if (o is uint)
            {
                return ToString((double)(uint)o);
            }

            if (o is DateTime)
            {
                return o.ToString();
            }

            return ToString(ToPrimitive(o, Types.String));
        }

        public static ObjectInstance ToObject(Engine engine, object value)
        {
            var o = value as ObjectInstance;
            if (o != null)
            {
                return o;
            }

            if (value == Undefined.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }

            if (value == Null.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }

            if (value is bool)
            {
                return engine.Boolean.Construct((bool) value);
            }

            if (value is int)
            {
                return engine.Number.Construct((int) value);
            }

            if (value is uint)
            {
                return engine.Number.Construct((uint) value);
            }

            if (value is DateTime)
            {
                return engine.Date.Construct((DateTime)value);
            }

            if (value is double)
            {
                return engine.Number.Construct((double) value);
            }

            var s = value as string;
            if (s != null)
            {
                return engine.String.Construct(s);
            }

            throw new JavaScriptException(engine.TypeError);
        }

        public static Types GetType(object value)
        {
            if (value == null || value == Null.Instance)
            {
                return Types.Null;
            }

            if (value == Undefined.Instance)
            {
                return Types.Undefined;
            }

            if (value is string)
            {
                return Types.String;
            }

            if (value is double || value is int || value is uint || value is ushort)
            {
                return Types.Number;
            }

            if (value is bool)
            {
                return Types.Boolean;
            }

            return Types.Object;
        }

        public static void CheckObjectCoercible(Engine engine, object o)
        {
            if (o == Undefined.Instance || o == Null.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }
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

using System;
using System.Globalization;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime
{
    public class TypeConverter
    {
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.1
        /// </summary>
        /// <param name="input"></param>
        /// <param name="preferredType"></param>
        /// <returns></returns>
        public static object ToPrimitive(object input, TypeCode preferredType = TypeCode.Empty)
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
                o = p.PrimitiveValue;
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
                if (double.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out n))
                {
                    return n;
                }

                return double.NaN;
            }

            return ToNumber(ToPrimitive(o, TypeCode.Double));
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.4
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static int ToInteger(object o)
        {
            if (o is int)
            {
                return (int) o;
            }

            if (o is uint)
            {
                return (int) (uint) o;
            }

            var number = ToNumber(o);
            if (double.IsNaN(number))
            {
                return 0;
            }
            
            return (int) number;
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

            if (o is uint)
            {
                return (int) (uint) o;
            }

            var n = ToNumber(o);
            if (double.IsNaN(n) || double.IsInfinity(n) || n == 0)
            {
                return 0;
            }

            return Convert.ToInt32(n);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint ToUint32(object o)
        {
            var number = ToNumber(o);
            if (double.IsNaN(number) || double.IsInfinity(number) || number == 0)
            {
                return 0;
            }

            var posInt = (long) number;
            return (uint)(posInt % 4294967296);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static ushort ToUint16(object o)
        {
            var n = ToNumber(o);
            if (double.IsNaN(n) || double.IsInfinity(n) || n == 0)
            {
                return 0;
            }

            return Convert.ToUInt16(n);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.8
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string ToString(object o)
        {
            var s = o as string;
            if (s != null)
            {
                return s;
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
                o = p;
            }

            if (o is bool)
            {
                return (bool) o ? "true" : "false";
            }

            if (o is double)
            {
                var n = (double) o;
                if (double.IsNaN(n))
                {
                    return "NaN";
                }    

                if (double.IsInfinity(n))
                {
                    return "Infinity";
                }

                return n.ToString();
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

            return ToString(ToPrimitive(o, TypeCode.String));
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

        public static TypeCode GetType(object value)
        {
            if (value == null || value == Undefined.Instance || value == Null.Instance)
            {
                return TypeCode.Empty;
            }

            if (value is string)
            {
                return TypeCode.String;
            }

            if (value is double || value is int || value is uint || value is ushort)
            {
                return TypeCode.Double;
            }

            if (value is bool)
            {
                return TypeCode.Boolean;
            }

            return TypeCode.Object;
        }

        public static void CheckObjectCoercible(Engine engine, object o)
        {
            if (o == Undefined.Instance || o == Null.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }
        }
    }
}

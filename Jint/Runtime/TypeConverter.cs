using System;
using Jint.Native;
using Jint.Native.Errors;
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
        public static object ToPrimitive(object input, TypeCode preferredType)
        {
            return Undefined.Instance;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.2
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static bool ToBoolean(object o)
        {
            return (bool) o;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static double ToNumber(object o)
        {
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

            if (o is double)
            {
                return (double)o;
            }

            var s = o as string;
            if (s != null)
            {
                return double.Parse(s);
            }

            return ToNumber(ToPrimitive(o, TypeCode.Double));
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.4
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public int ToInteger(object o)
        {
            return (int) o;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public int ToInt32(object o)
        {
            return (int)o;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public uint ToUint32(object o)
        {
            return (uint)o;
        }


        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public ushort ToUint16(object o)
        {
            return (ushort)o;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.8
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public string ToString(object o)
        {
            return (string)o;
        }

        public ObjectInstance ToObject(Engine engine, object value)
        {
            var o = value as ObjectInstance;
            if (o != null)
            {
                return o;
            }

            if (value == Undefined.Instance)
            {
                throw new TypeError();
            }

            if (value == Null.Instance)
            {
                throw new TypeError();
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

            if (value is double)
            {
                return engine.Number.Construct((double) value);
            }
            if (value is string)
            {
                return engine.String.Construct((string) value);
            }

            throw new TypeError();
        }
    }
}

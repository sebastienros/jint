using System;
using System.Globalization;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Native.Global
{
    public sealed class GlobalObject : ObjectInstance
    {
        private GlobalObject(Engine engine) : base(engine)
        {
        }

        public static GlobalObject CreateGlobalObject(Engine engine)
        {
            var global = new GlobalObject(engine);
            global.Prototype = null;
            global.Extensible = true;
            
            // Global object properties
            global.DefineOwnProperty("NaN", new DataDescriptor(double.NaN), false);
            global.DefineOwnProperty("Infinity", new DataDescriptor(double.PositiveInfinity), false);
            global.DefineOwnProperty("undefined", new DataDescriptor(Undefined.Instance), false);

            // Global object functions
            global.DefineOwnProperty("parseInt", new ClrDataDescriptor<object, object>(engine, ParseInt), false);
            global.DefineOwnProperty("parseFloat", new ClrDataDescriptor<object, object>(engine, ParseFloat), false);
            global.DefineOwnProperty("isNaN", new ClrDataDescriptor<object, bool>(engine, IsNaN), false);
            global.DefineOwnProperty("isFinite", new ClrDataDescriptor<object, bool>(engine, IsFinite), false);
            global.DefineOwnProperty("decodeURI", new ClrDataDescriptor<object, string>(engine, DecodeUri), false);
            global.DefineOwnProperty("decodeURIComponent", new ClrDataDescriptor<object, string>(engine, DecodeUriComponent), false);
            global.DefineOwnProperty("encodeURI", new ClrDataDescriptor<object, string>(engine, EncodeUri), false);
            global.DefineOwnProperty("encodeURIComponent", new ClrDataDescriptor<object, string>(engine, EncodeUriComponent), false);

            return global;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.2
        /// </summary>
        public static object ParseInt(object thisObject, object[] arguments)
        {
            int radix = arguments.Length > 1 ? TypeConverter.ToInt32(arguments[1]) : 10;
            object v = arguments[0];

            if (radix == 0)
            {
                radix = 10;
            }

            if (radix < 2 || radix > 36)
            {
                return double.NaN;
            }

            try
            {
                var s = TypeConverter.ToString(v);
                return Convert.ToInt32(s.TrimStart(), radix);
            }
            catch
            {
                return double.NaN;
            }

        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.3
        /// </summary>
        public static object ParseFloat(object thisObject, object[] arguments)
        {
            object v = arguments[0];

            var s = TypeConverter.ToString(v);
            double n;
            if (double.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }

            return double.NaN;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.4
        /// </summary>
        public static bool IsNaN(object thisObject, object[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments[0]);
            return double.IsNaN(x);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.5
        /// </summary>
        public static bool IsFinite(object thisObject, object[] arguments)
        {
            if (arguments.Length != 1)
            {
                return false;
            }

            var n = TypeConverter.ToNumber(arguments[0]);
            if (n == double.NaN || n == double.PositiveInfinity || n == double.NegativeInfinity)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.1
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static string DecodeUri(object thisObject, object[] arguments)
        {
            if (arguments.Length < 1 || arguments[0] == Undefined.Instance)
            {
                return "";
            }

            return Uri.UnescapeDataString(arguments[0].ToString().Replace("+", " "));
        }

        private static readonly char[] ReservedEncoded = new [] { ';', ',', '/', '?', ':', '@', '&', '=', '+', '$', '#' };
        private static readonly char[] ReservedEncodedComponent = new [] { '-', '_', '.', '!', '~', '*', '\'', '(', ')', '[', ']' };

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.2
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static string EncodeUri(object thisObject, object[] arguments)
        {
            if (arguments.Length < 1 || arguments[0] == Undefined.Instance)
            {
                return "";
            }

            string encoded = Uri.EscapeDataString(arguments[0].ToString());

            foreach (char c in ReservedEncoded)
            {
                encoded = encoded.Replace(Uri.EscapeDataString(c.ToString()), c.ToString());
            }

            foreach (char c in ReservedEncodedComponent)
            {
                encoded = encoded.Replace(Uri.EscapeDataString(c.ToString()), c.ToString());
            }

            return encoded.ToUpper();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.3
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static string DecodeUriComponent(object thisObject, object[] arguments)
        {
            if (arguments.Length < 1 || arguments[0] == Undefined.Instance)
            {
                return "";
            }

            return Uri.UnescapeDataString(arguments[0].ToString().Replace("+", " "));
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.4
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static string EncodeUriComponent(object thisObject, object[] arguments)
        {
            if (arguments.Length < 1 || arguments[0] == Undefined.Instance)
            {
                return "";
            }

            string encoded = Uri.EscapeDataString(arguments[0].ToString());

            foreach (char c in ReservedEncodedComponent)
            {
                encoded = encoded.Replace(Uri.EscapeDataString(c.ToString()), c.ToString().ToUpper());
            }

            return encoded;
        }


    }
}

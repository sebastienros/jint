using System;
using System.Globalization;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

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
            
            return global;
        }

        public void Configure()
        {
            // this is implementation dependent, and only to pass some unit tests
            Prototype = Engine.Object.PrototypeObject;

            // Global object properties
            FastAddProperty("Object", Engine.Object, true, false, true);
            FastAddProperty("Function", Engine.Function, true, false, true);
            FastAddProperty("Array", Engine.Array, true, false, true);
            FastAddProperty("String", Engine.String, true, false, true);
            FastAddProperty("RegExp", Engine.RegExp, true, false, true);
            FastAddProperty("Number", Engine.Number, true, false, true);
            FastAddProperty("Boolean", Engine.Boolean, true, false, true);
            FastAddProperty("Date", Engine.Date, true, false, true);
            FastAddProperty("Math", Engine.Math, true, false, true);
            FastAddProperty("JSON", Engine.Json, true, false, true);

            FastAddProperty("Error", Engine.Error, true, false, true);
            FastAddProperty("EvalError", Engine.EvalError, true, false, true);
            FastAddProperty("RangeError", Engine.RangeError, true, false, true);
            FastAddProperty("ReferenceError", Engine.ReferenceError, true, false, true);
            FastAddProperty("SyntaxError", Engine.SyntaxError, true, false, true);
            FastAddProperty("TypeError", Engine.TypeError, true, false, true);
            FastAddProperty("URIError", Engine.UriError, true, false, true);

            FastAddProperty("NaN", double.NaN, false, false, false);
            FastAddProperty("Infinity", double.PositiveInfinity, false, false, false);
            FastAddProperty("undefined", Undefined.Instance, false, false, false);

            // Global object functions
            FastAddProperty("parseInt", new ClrFunctionInstance<object, object>(Engine, ParseInt, 2), false, false, false);
            FastAddProperty("parseFloat", new ClrFunctionInstance<object, object>(Engine, ParseFloat), false, false, false);
            FastAddProperty("isNaN", new ClrFunctionInstance<object, bool>(Engine, IsNaN), false, false, false);
            FastAddProperty("isFinite", new ClrFunctionInstance<object, bool>(Engine, IsFinite), false, false, false);
            FastAddProperty("decodeURI", new ClrFunctionInstance<object, string>(Engine, DecodeUri), false, false, false);
            FastAddProperty("decodeURIComponent", new ClrFunctionInstance<object, string>(Engine, DecodeUriComponent), false, false, false);
            FastAddProperty("encodeURI", new ClrFunctionInstance<object, string>(Engine, EncodeUri), false, false, false);
            FastAddProperty("encodeURIComponent", new ClrFunctionInstance<object, string>(Engine, EncodeUriComponent), false, false, false);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.2
        /// </summary>
        public static object ParseInt(object thisObject, object[] arguments)
        {
            string inputString = TypeConverter.ToString(arguments.At(0));
            var s = inputString.Trim();

            var sign = 1;
            if (!System.String.IsNullOrEmpty(s))
            {
                if (s[0] == '-')
                {
                    sign = -1;
                }
                
                if (s[0] == '-' || s[0] == '+')
                {
                    s = s.Substring(1);
                }
            }

            var stripPrefix = true;

            int radix = arguments.Length > 1 ? TypeConverter.ToInt32(arguments[1]) : 0;

            if (radix == 0)
            {
                if (s.Length >= 2 && s.StartsWith("0x") || s.StartsWith("0X"))
                {
                    radix = 16;
                }
                else
                {
                    radix = 10;
                }
            }
            else if (radix < 2 || radix > 36)
            {
                return double.NaN;
            }
            else if(radix != 16)
            {
                stripPrefix = false;
            }

            if (stripPrefix && s.Length >= 2 && s.StartsWith("0x") || s.StartsWith("0X"))
            {
                s = s.Substring(2);
            }

            try
            {
                return sign * Parse(s, radix);
            }
            catch
            {
                return double.NaN;
            }

        }

        private static double Parse(string number, int radix)
        {
            if (number == "")
            {
                return double.NaN;
            }

            double result = 0;
            double pow = 1;
            for (int i = number.Length - 1; i >= 0 ; i--)
            {
                double index = double.NaN;
                char digit = number[i];

                if (digit >= '0' && digit <= '9')
                {
                    index = digit - '0';
                }
                else if (digit >= 'a' && digit <= 'z')
                {
                    index = digit - 'a' + 10;
                }
                else if (digit >= 'A' && digit <= 'Z')
                {
                    index = digit - 'A' + 10;
                }

                if (double.IsNaN(index) || index >= radix)
                {
                    return Parse(number.Substring(0, i), radix);
                }

                result += index*pow;
                pow = pow * radix;
            }

            return result;
            return 0;
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
            if (double.IsNaN(n) || double.IsInfinity(n))
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

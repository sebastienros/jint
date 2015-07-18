using System;
using System.Linq;
using System.Text;
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
            var global = new GlobalObject(engine) {Prototype = null, Extensible = true};

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
            FastAddProperty("parseInt", new ClrFunctionInstance(Engine, ParseInt, 2), true, false, true);
            FastAddProperty("parseFloat", new ClrFunctionInstance(Engine, ParseFloat, 1), true, false, true);
            FastAddProperty("isNaN", new ClrFunctionInstance(Engine, IsNaN, 1), true, false, true);
            FastAddProperty("isFinite", new ClrFunctionInstance(Engine, IsFinite, 1), true, false, true);
            FastAddProperty("decodeURI", new ClrFunctionInstance(Engine, DecodeUri, 1), true, false, true);
            FastAddProperty("decodeURIComponent", new ClrFunctionInstance(Engine, DecodeUriComponent, 1), true, false, true);
            FastAddProperty("encodeURI", new ClrFunctionInstance(Engine, EncodeUri, 1), true, false, true);
            FastAddProperty("encodeURIComponent", new ClrFunctionInstance(Engine, EncodeUriComponent, 1), true, false, true);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.2
        /// </summary>
        public static JsValue ParseInt(JsValue thisObject, JsValue[] arguments)
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
                return sign * Parse(s, radix).AsNumber();
            }
            catch
            {
                return double.NaN;
            }

        }

        private static JsValue Parse(string number, int radix)
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
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.3
        /// </summary>
        public static JsValue ParseFloat(JsValue thisObject, JsValue[] arguments)
        {
            var inputString = TypeConverter.ToString(arguments.At(0));
            var trimmedString = inputString.TrimStart();

            var sign = 1;
            if (trimmedString.Length > 0 )
            {
                if (trimmedString[0] == '-')
                {
                    sign = -1;
                    trimmedString = trimmedString.Substring(1);
                }
                else if (trimmedString[0] == '+')
                {
                    trimmedString = trimmedString.Substring(1);
                }
            }

            if (trimmedString.StartsWith("Infinity"))
            {
                return sign*double.PositiveInfinity;
            }

            if (trimmedString.StartsWith("NaN"))
            {
                return double.NaN;
            }

            var separator = (char) 0;

            bool isNan = true;
            decimal number = 0;
            var i = 0;
            for (; i < trimmedString.Length; i++)
            {
                var c = trimmedString[i];
                if (c == '.')
                {
                    i++;
                    separator = '.';
                    break;
                }
                
                if(c == 'e' || c == 'E')
                {
                    i++;
                    separator = 'e';
                    break;
                }

                var digit = c - '0';

                if (digit >= 0 && digit <= 9)
                {
                    isNan = false;
                    number = number * 10 + digit;
                }
                else
                {
                    break;
                }
            }

            decimal pow = 0.1m;

            if (separator == '.')
            {
                for (; i < trimmedString.Length; i++)
                {
                    var c = trimmedString[i];

                    var digit = c - '0';

                    if (digit >= 0 && digit <= 9)
                    {
                        isNan = false;
                        number += digit * pow;
                        pow *= 0.1m;
                    }
                    else if (c == 'e' || c == 'E')
                    {
                        i++;
                        separator = 'e';
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            var exp = 0;
            var expSign = 1;

            if (separator == 'e')
            {
                if (i < trimmedString.Length)
                {
                    if (trimmedString[i] == '-')
                    {
                        expSign = -1;
                        i++;
                    }
                    else if (trimmedString[i] == '+')
                    {
                        i++;
                    }
                }

                for (; i < trimmedString.Length; i++)
                {
                    var c = trimmedString[i];

                    var digit = c - '0';

                    if (digit >= 0 && digit <= 9)
                    {
                        exp = exp * 10 + digit;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (isNan)
            {
                return double.NaN;
            }

            for (var k = 1; k <= exp; k++)
            {
                if (expSign > 0)
                {
                    number *= 10;
                }
                else
                {
                    number /= 10;
                }
            }
            
            return (double) (sign * number);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.4
        /// </summary>
        public static JsValue IsNaN(JsValue thisObject, JsValue[] arguments)
        {
            var x = TypeConverter.ToNumber(arguments.At(0));
            return double.IsNaN(x);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.5
        /// </summary>
        public static JsValue IsFinite(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length != 1)
            {
                return false;
            }

            var n = TypeConverter.ToNumber(arguments.At(0));
            if (double.IsNaN(n) || double.IsInfinity(n))
            {
                return false;
            }

            return true;
        }

        private static readonly char[] UriReserved = {';', '/', '?', ':', '@', '&', '=', '+', '$', ','};

        private static readonly char[] UriUnescaped =
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
            'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '_', '.', '!',
            '~', '*', '\'', '(', ')'
        };

        private const string HexaMap = "0123456789ABCDEF";

        private static bool IsValidHexaChar(char c)
        {
            return
                c >= '0' && c <= '9' ||
                c >= 'a' && c <= 'f' ||
                c >= 'A' && c <= 'F';
        }
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.2
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public JsValue EncodeUri(JsValue thisObject, JsValue[] arguments)
        {
            var uriString = TypeConverter.ToString(arguments.At(0));
            var unescapedUriSet = UriReserved.Concat(UriUnescaped).Concat(new [] {'#'}).ToArray();

            return Encode(uriString, unescapedUriSet);
        }


        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.4
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public JsValue EncodeUriComponent(JsValue thisObject, JsValue[] arguments)
        {
            var uriString = TypeConverter.ToString(arguments.At(0));

            return Encode(uriString, UriUnescaped);
        }

        private string Encode(string uriString, char[] unescapedUriSet)
        {
            var strLen = uriString.Length;
            var r = new StringBuilder(uriString.Length);
            for (var k = 0; k< strLen; k++)
            {
                var c = uriString[k];
                if (System.Array.IndexOf(unescapedUriSet, c) != -1)
                {
                    r.Append(c);
                }
                else
                {
                    if (c >= 0xDC00 && c <= 0xDBFF)
                    {
                        throw new JavaScriptException(Engine.UriError);
                    }

                    int v;
                    if (c < 0xD800 || c > 0xDBFF)
                    {
                        v = c;
                    }
                    else
                    {
                        k++;
                        if (k == strLen)
                        {
                            throw new JavaScriptException(Engine.UriError);
                        }

                        var kChar = (int) uriString[k];
                        if (kChar < 0xDC00 || kChar > 0xDFFF)
                        {
                            throw new JavaScriptException(Engine.UriError);
                        }

                        v = (c - 0xD800) * 0x400 + (kChar - 0xDC00) + 0x10000;
                    }

                    byte[] octets;

                    if (v >= 0 && v <= 0x007F)
                    {
                        // 00000000 0zzzzzzz -> 0zzzzzzz
                        octets = new[] { (byte)v };
                    }
                    else if (v <= 0x07FF)
                    {
                        // 00000yyy yyzzzzzz ->	110yyyyy ; 10zzzzzz
                        octets = new[]
                        {
                            (byte)(0xC0 | (v >> 6)), 
                            (byte)(0x80 | (v & 0x3F))
                        };
                    }
                    else if (v <= 0xD7FF)
                    {
                        // xxxxyyyy yyzzzzzz -> 1110xxxx; 10yyyyyy; 10zzzzzz	
                        octets = new[]
                        {
                            (byte)(0xE0 | (v >> 12)), 
                            (byte)(0x80 | ((v >> 6) & 0x3F)), 
                            (byte)(0x80 | (v & 0x3F))
                        };
                    }
                    else if (v <= 0xDFFF)
                    {
                        throw new JavaScriptException(Engine.UriError);
                    }
                    else if (v <= 0xFFFF)
                    {
                        octets = new[]
                        {
                            (byte) (0xE0 | (v >> 12)),
                            (byte) (0x80 | ((v >> 6) & 0x3F)),
                            (byte) (0x80 | (v & 0x3F))
                        };
                    }
                    else
                    {
                        octets = new[]
                        {
                            (byte) (0xF0 | (v >> 18)),
                            (byte) (0x80 | (v >> 12 & 0x3F)),
                            (byte) (0x80 | (v >> 6 & 0x3F)),
                            (byte) (0x80 | (v >> 0 & 0x3F))
                        };
                    }

                    for (var j = 0; j < octets.Length; j++)
                    {
                        var jOctet = octets[j];
                        var x1 = HexaMap[jOctet / 16];
                        var x2 = HexaMap[jOctet % 16];
                        r.Append('%').Append(x1).Append(x2);
                    }
                }
            }

            return r.ToString();
        }

        public JsValue DecodeUri(JsValue thisObject, JsValue[] arguments)
        {
            var uriString = TypeConverter.ToString(arguments.At(0));
            var reservedUriSet = UriReserved.Concat(new[] { '#' }).ToArray();

            return Decode(uriString, reservedUriSet);
        }

        public JsValue DecodeUriComponent(JsValue thisObject, JsValue[] arguments)
        {
            var componentString = TypeConverter.ToString(arguments.At(0));
            var reservedUriComponentSet = new char[0];

            return Decode(componentString, reservedUriComponentSet);
        }

        public string Decode(string uriString, char[] reservedSet)
        {
            var strLen = uriString.Length;
            var R = new StringBuilder(strLen);
            for (var k = 0; k < strLen; k++)
            {
                var C = uriString[k];
                if (C != '%')
                {
                    R.Append(C);
                }
                else
                {
                    var start = k;
                    if (k + 2 >= strLen)
                    {
                        throw new JavaScriptException(Engine.UriError);
                    }

                    if (!IsValidHexaChar(uriString[k + 1]) || !IsValidHexaChar(uriString[k + 2]))
                    {
                        throw new JavaScriptException(Engine.UriError);    
                    }

                    var B = Convert.ToByte(uriString[k + 1].ToString() + uriString[k + 2], 16);

                    k += 2;
                    if ((B & 0x80) == 0)
                    {
                        C = (char) B;
                        if (System.Array.IndexOf(reservedSet, C) == -1)
                        {
                            R.Append(C);
                        }
                        else
                        {
                            R.Append(uriString.Substring(start, k - start + 1));
                        }
                    }
                    else
                    {
                        var n = 0;
                        for (; ((B << n) & 0x80) != 0; n++) ;

                        if (n == 1 || n > 4)
                        {
                            throw new JavaScriptException(Engine.UriError);
                        }
                        
                        var Octets = new byte[n];
                        Octets[0] = B;
                        
                        if (k + (3*(n - 1)) >= strLen)
                        {
                            throw new JavaScriptException(Engine.UriError);
                        }
                        
                        for(var j=1; j <n; j++)
                        {
                            k++;
                            if (uriString[k] != '%')
                            {
                                throw new JavaScriptException(Engine.UriError);
                            }

                            if (!IsValidHexaChar(uriString[k + 1]) || !IsValidHexaChar(uriString[k + 2]))
                            {
                                throw new JavaScriptException(Engine.UriError);
                            }

                            B = Convert.ToByte(uriString[k + 1].ToString() + uriString[k + 2], 16);

                            // B & 11000000 != 10000000 
                            if ((B & 0xC0) != 0x80)
                            {
                                throw new JavaScriptException(Engine.UriError);
                            }

                            k += 2;

                            Octets[j] = B;
                        }

                        R.Append(Encoding.UTF8.GetString(Octets, 0, Octets.Length));
                    }
                }
            }
            
            return R.ToString();
        }

    }
}

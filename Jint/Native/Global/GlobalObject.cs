using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Esprima;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Global
{
    public sealed partial class GlobalObject : ObjectInstance
    {
        private readonly Realm _realm;
        private readonly StringBuilder _stringBuilder = new();

        internal GlobalObject(
            Engine engine,
            Realm realm) : base(engine, ObjectClass.Object, InternalTypes.Object | InternalTypes.PlainObject)
        {
            _realm = realm;
        }

        private JsValue ToStringString(JsValue thisObject, JsValue[] arguments)
        {
            return _realm.Intrinsics.Object.PrototypeObject.ToObjectString(thisObject, Arguments.Empty);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-parseint-string-radix
        /// </summary>
        public static JsValue ParseInt(JsValue thisObject, JsValue[] arguments)
        {
            var inputString = TypeConverter.ToString(arguments.At(0));
            var trimmed = StringPrototype.TrimEx(inputString);
            var s = trimmed.AsSpan();

            var radix = arguments.Length > 1 ? TypeConverter.ToInt32(arguments[1]) : 0;
            var hexStart = s.Length > 1 && trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase);

            var stripPrefix = true;
            if (radix == 0)
            {
                radix = hexStart ? 16 : 10;
            }
            else if (radix < 2 || radix > 36)
            {
                return JsNumber.DoubleNaN;
            }
            else if (radix != 16)
            {
                stripPrefix = false;
            }

            // check fast case
            if (radix == 10 && int.TryParse(trimmed, out var number))
            {
                return JsNumber.Create(number);
            }

            var sign = 1;
            if (s.Length > 0)
            {
                var c = s[0];
                if (c == '-')
                {
                    sign = -1;
                }

                if (c is '-' or '+')
                {
                    s = s.Slice(1);
                }
            }

            if (stripPrefix && hexStart)
            {
                s = s.Slice(2);
            }

            if (s.Length == 0)
            {
                return double.NaN;
            }

            var hasResult = false;
            double result = 0;
            double pow = 1;
            for (var i = s.Length - 1; i >= 0; i--)
            {
                var digit = s[i];

                var index = digit switch
                {
                    >= '0' and <= '9' => digit - '0',
                    >= 'a' and <= 'z' => digit - 'a' + 10,
                    >= 'A' and <= 'Z' => digit - 'A' + 10,
                    _ => -1
                };

                if (index == -1 || index >= radix)
                {
                    // reset
                    hasResult = false;
                    result = 0;
                    pow = 1;
                    continue;
                }

                hasResult = true;
                result += index * pow;
                pow *= radix;
            }

            return hasResult ? JsNumber.Create(sign  * result) : JsNumber.DoubleNaN;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-parsefloat-string
        /// </summary>
        public static JsValue ParseFloat(JsValue thisObject, JsValue[] arguments)
        {
            var inputString = TypeConverter.ToString(arguments.At(0));
            var trimmedString = StringPrototype.TrimStartEx(inputString);

            if (string.IsNullOrWhiteSpace(trimmedString))
            {
                return JsNumber.DoubleNaN;
            }

            // start of string processing
            var i = 0;

            // check known string constants
            if (!char.IsDigit(trimmedString[0]))
            {
                if (trimmedString[0] == '-')
                {
                    i++;
                    if (trimmedString.Length > 1 && trimmedString[1] == 'I' && trimmedString.StartsWith("-Infinity"))
                    {
                        return JsNumber.DoubleNegativeInfinity;
                    }
                }

                if (trimmedString[0] == '+')
                {
                    i++;
                    if (trimmedString.Length > 1 && trimmedString[1] == 'I' && trimmedString.StartsWith("+Infinity"))
                    {
                        return JsNumber.DoublePositiveInfinity;
                    }
                }

                if (trimmedString.StartsWith("Infinity"))
                {
                    return JsNumber.DoublePositiveInfinity;
                }

                if (trimmedString.StartsWith("NaN"))
                {
                    return JsNumber.DoubleNaN;
                }
            }

            // find the starting part of string  that is still acceptable JS number

            var dotFound = false;
            var exponentFound = false;
            while (i < trimmedString.Length)
            {
                var c = trimmedString[i];

                if (Character.IsDecimalDigit(c))
                {
                    i++;
                    continue;
                }

                if (c == '.')
                {
                    if (dotFound)
                    {
                        // does not look right
                        break;
                    }

                    i++;
                    dotFound = true;
                    continue;
                }

                if (c is 'e' or 'E')
                {
                    if (exponentFound)
                    {
                        // does not look right
                        break;
                    }

                    i++;
                    exponentFound = true;
                    continue;
                }

                if (c is '+' or '-' && trimmedString[i - 1] is 'e' or 'E')
                {
                    // ok
                    i++;
                    continue;
                }

                break;
            }

            while (exponentFound && i > 0 && !Character.IsDecimalDigit(trimmedString[i - 1]))
            {
                // we are missing required exponent number part info
                i--;
            }

            // we should now have proper input part

#if SUPPORTS_SPAN_PARSE
            var substring = trimmedString.AsSpan(0, i);
#else
            var substring = trimmedString.Substring(0, i);
#endif

            const NumberStyles Styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign;
            if (double.TryParse(substring, Styles, CultureInfo.InvariantCulture, out var d))
            {
                return d;
            }

            return JsNumber.DoubleNaN;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.2.4
        /// </summary>
        public static JsValue IsNaN(JsValue thisObject, JsValue[] arguments)
        {
            var value = arguments.At(0);

            if (ReferenceEquals(value, JsNumber.DoubleNaN))
            {
                return true;
            }

            var x = TypeConverter.ToNumber(value);
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

        private static readonly HashSet<char> UriReserved = new HashSet<char>
        {
            ';', '/', '?', ':', '@', '&', '=', '+', '$', ','
        };

        private static readonly HashSet<char> UriUnescaped = new HashSet<char>
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
            'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '_', '.', '!',
            '~', '*', '\'', '(', ')'
        };

        private static readonly HashSet<char> UnescapedUriSet = new HashSet<char>(UriReserved.Concat(UriUnescaped).Concat(new[] { '#' }));
        private static readonly HashSet<char> ReservedUriSet = new HashSet<char>(UriReserved.Concat(new[] { '#' }));

        private const string HexaMap = "0123456789ABCDEF";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsValidHexaChar(char c) => Uri.IsHexDigit(c);

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.1.3.2
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public JsValue EncodeUri(JsValue thisObject, JsValue[] arguments)
        {
            var uriString = TypeConverter.ToString(arguments.At(0));

            return Encode(uriString, UnescapedUriSet);
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

        private string Encode(string uriString, HashSet<char> unescapedUriSet)
        {
            var strLen = uriString.Length;

            _stringBuilder.EnsureCapacity(uriString.Length);
            _stringBuilder.Clear();

            for (var k = 0; k < strLen; k++)
            {
                var c = uriString[k];
                if (unescapedUriSet != null && unescapedUriSet.Contains(c))
                {
                    _stringBuilder.Append(c);
                }
                else
                {
                    if (c >= 0xDC00 && c <= 0xDBFF)
                    {
                        ExceptionHelper.ThrowUriError(_realm);
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
                            ExceptionHelper.ThrowUriError(_realm);
                        }

                        var kChar = (int)uriString[k];
                        if (kChar < 0xDC00 || kChar > 0xDFFF)
                        {
                            ExceptionHelper.ThrowUriError(_realm);
                        }

                        v = (c - 0xD800) * 0x400 + (kChar - 0xDC00) + 0x10000;
                    }

                    byte[] octets = System.Array.Empty<byte>();

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
                        ExceptionHelper.ThrowUriError(_realm);
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

                    foreach (var octet in octets)
                    {
                        var x1 = HexaMap[octet / 16];
                        var x2 = HexaMap[octet % 16];
                        _stringBuilder.Append('%').Append(x1).Append(x2);
                    }
                }
            }

            return _stringBuilder.ToString();
        }

        public JsValue DecodeUri(JsValue thisObject, JsValue[] arguments)
        {
            var uriString = TypeConverter.ToString(arguments.At(0));

            return Decode(uriString, ReservedUriSet);
        }

        public JsValue DecodeUriComponent(JsValue thisObject, JsValue[] arguments)
        {
            var componentString = TypeConverter.ToString(arguments.At(0));

            return Decode(componentString, null);
        }

        private string Decode(string uriString, HashSet<char>? reservedSet)
        {
            var strLen = uriString.Length;

            _stringBuilder.EnsureCapacity(strLen);
            _stringBuilder.Clear();

            var octets = System.Array.Empty<byte>();

            for (var k = 0; k < strLen; k++)
            {
                var C = uriString[k];
                if (C != '%')
                {
                    _stringBuilder.Append(C);
                }
                else
                {
                    var start = k;
                    if (k + 2 >= strLen)
                    {
                        ExceptionHelper.ThrowUriError(_realm);
                    }

                    if (!IsValidHexaChar(uriString[k + 1]) || !IsValidHexaChar(uriString[k + 2]))
                    {
                        ExceptionHelper.ThrowUriError(_realm);
                    }

                    var B = Convert.ToByte(uriString[k + 1].ToString() + uriString[k + 2], 16);

                    k += 2;
                    if ((B & 0x80) == 0)
                    {
                        C = (char)B;
                        if (reservedSet == null || !reservedSet.Contains(C))
                        {
                            _stringBuilder.Append(C);
                        }
                        else
                        {
                            _stringBuilder.Append(uriString, start, k - start + 1);
                        }
                    }
                    else
                    {
                        var n = 0;
                        for (; ((B << n) & 0x80) != 0; n++) ;

                        if (n == 1 || n > 4)
                        {
                            ExceptionHelper.ThrowUriError(_realm);
                        }

                        octets = octets.Length == n
                            ? octets
                            : new byte[n];

                        octets[0] = B;

                        if (k + (3 * (n - 1)) >= strLen)
                        {
                            ExceptionHelper.ThrowUriError(_realm);
                        }

                        for (var j = 1; j < n; j++)
                        {
                            k++;
                            if (uriString[k] != '%')
                            {
                                ExceptionHelper.ThrowUriError(_realm);
                            }

                            if (!IsValidHexaChar(uriString[k + 1]) || !IsValidHexaChar(uriString[k + 2]))
                            {
                                ExceptionHelper.ThrowUriError(_realm);
                            }

                            B = Convert.ToByte(uriString[k + 1].ToString() + uriString[k + 2], 16);

                            // B & 11000000 != 10000000
                            if ((B & 0xC0) != 0x80)
                            {
                                ExceptionHelper.ThrowUriError(_realm);
                            }

                            k += 2;

                            octets[j] = B;
                        }

                        _stringBuilder.Append(Encoding.UTF8.GetString(octets, 0, octets.Length));
                    }
                }
            }

            return _stringBuilder.ToString();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-B.2.1
        /// </summary>
        public JsValue Escape(JsValue thisObject, JsValue[] arguments)
        {
            const string whiteList = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@*_ + -./";
            var uriString = TypeConverter.ToString(arguments.At(0));

            var strLen = uriString.Length;

            _stringBuilder.EnsureCapacity(strLen);
            _stringBuilder.Clear();

            for (var k = 0; k < strLen; k++)
            {
                var c = uriString[k];
                if (whiteList.IndexOf(c) != -1)
                {
                    _stringBuilder.Append(c);
                }
                else if (c < 256)
                {
                    _stringBuilder.Append($"%{((int) c):X2}");
                }
                else
                {
                    _stringBuilder.Append($"%u{((int) c):X4}");
                }
            }

            return _stringBuilder.ToString();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-B.2.2
        /// </summary>
        public JsValue Unescape(JsValue thisObject, JsValue[] arguments)
        {
            var uriString = TypeConverter.ToString(arguments.At(0));

            var strLen = uriString.Length;

            _stringBuilder.EnsureCapacity(strLen);
            _stringBuilder.Clear();

            for (var k = 0; k < strLen; k++)
            {
                var c = uriString[k];
                if (c == '%')
                {
                    if (k <= strLen - 6
                        && uriString[k + 1] == 'u'
                        && uriString.Skip(k + 2).Take(4).All(IsValidHexaChar))
                    {
                        c = (char)int.Parse(
                            string.Join(string.Empty, uriString.Skip(k + 2).Take(4)),
                            NumberStyles.AllowHexSpecifier);

                        k += 5;
                    }
                    else if (k <= strLen - 3
                        && uriString.Skip(k + 1).Take(2).All(IsValidHexaChar))
                    {
                        c = (char)int.Parse(
                            string.Join(string.Empty, uriString.Skip(k + 1).Take(2)),
                            NumberStyles.AllowHexSpecifier);

                        k += 2;
                    }
                }
                _stringBuilder.Append(c);
            }

            return _stringBuilder.ToString();
        }

        // optimized versions with string parameter and without virtual dispatch for global environment usage

        internal bool HasProperty(Key property)
        {
            return GetOwnProperty(property) != PropertyDescriptor.Undefined;
        }

        internal PropertyDescriptor GetProperty(Key property) => GetOwnProperty(property);

        internal bool DefinePropertyOrThrow(Key property, PropertyDescriptor desc)
        {
            if (!DefineOwnProperty(property, desc))
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return true;
        }

        internal bool DefineOwnProperty(Key property, PropertyDescriptor desc)
        {
            var current = GetOwnProperty(property);
            if (current == desc)
            {
                return true;
            }

            // check fast path
            if ((current._flags & PropertyFlag.MutableBinding) != 0)
            {
                current._value = desc.Value;
                return true;
            }

            return ValidateAndApplyPropertyDescriptor(this, new JsString(property), true, desc, current);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PropertyDescriptor GetOwnProperty(Key property)
        {
            Properties!.TryGetValue(property, out var descriptor);
            return descriptor ?? PropertyDescriptor.Undefined;
        }

        internal bool SetFromMutableBinding(Key property, JsValue value, bool strict)
        {
            // here we are called only from global environment record context
            // we can take some shortcuts to be faster

            if (!_properties!.TryGetValue(property, out var existingDescriptor))
            {
                if (strict)
                {
                    ExceptionHelper.ThrowReferenceNameError(_realm, property.Name);
                }
                _properties[property] = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding);
                return true;
            }

            if (existingDescriptor.IsDataDescriptor())
            {
                if (!existingDescriptor.Writable || existingDescriptor.IsAccessorDescriptor())
                {
                    return false;
                }

                // check fast path
                if ((existingDescriptor._flags & PropertyFlag.MutableBinding) != 0)
                {
                    existingDescriptor._value = value;
                    return true;
                }

                // slow path
                return DefineOwnProperty(property, new PropertyDescriptor(value, PropertyFlag.None));
            }

            if (existingDescriptor.Set is not ICallable setter)
            {
                return false;
            }

            setter.Call(this, new[] {value});

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetOwnProperty(Key property, PropertyDescriptor desc)
        {
            SetProperty(property, desc);
        }
    }
}

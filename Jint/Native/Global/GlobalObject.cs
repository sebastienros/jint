using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Extensions;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Global;

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

    private JsValue ToStringString(JsValue thisObject, JsCallArguments arguments)
    {
        return _realm.Intrinsics.Object.PrototypeObject.ToObjectString(thisObject, Arguments.Empty);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-parseint-string-radix
    /// </summary>
    internal static JsValue ParseInt(JsValue thisObject, JsCallArguments arguments)
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
        if (radix == 10 && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
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
    internal static JsValue ParseFloat(JsValue thisObject, JsCallArguments arguments)
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
                if (trimmedString.Length > 1 && trimmedString[1] == 'I' && trimmedString.StartsWith("-Infinity", StringComparison.Ordinal))
                {
                    return JsNumber.DoubleNegativeInfinity;
                }
            }

            if (trimmedString[0] == '+')
            {
                i++;
                if (trimmedString.Length > 1 && trimmedString[1] == 'I' && trimmedString.StartsWith("+Infinity", StringComparison.Ordinal))
                {
                    return JsNumber.DoublePositiveInfinity;
                }
            }

            if (trimmedString.StartsWith("Infinity", StringComparison.Ordinal))
            {
                return JsNumber.DoublePositiveInfinity;
            }

            if (trimmedString.StartsWith("NaN", StringComparison.Ordinal))
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
    private static JsValue IsNaN(JsValue thisObject, JsCallArguments arguments)
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
    private static JsValue IsFinite(JsValue thisObject, JsCallArguments arguments)
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

    private const string UriReservedString = ";/?:@&=+$,";
    private const string UriUnescapedString = "-.!~*'()";
    private static readonly SearchValues<char> UriUnescaped = SearchValues.Create(Character.AsciiWordCharacters + UriUnescapedString);
    private static readonly SearchValues<char> UnescapedUriSet = SearchValues.Create(Character.AsciiWordCharacters + UriReservedString + UriUnescapedString + '#');
    private static readonly SearchValues<char> ReservedUriSet = SearchValues.Create(UriReservedString + '#');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidHexaChar(char c) => Uri.IsHexDigit(c);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-encodeuri-uri
    /// </summary>
    private JsValue EncodeUri(JsValue thisObject, JsCallArguments arguments)
    {
        var uriString = TypeConverter.ToString(arguments.At(0));
        return Encode(uriString, UnescapedUriSet);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-encodeuricomponent-uricomponent
    /// </summary>
    private JsValue EncodeUriComponent(JsValue thisObject, JsCallArguments arguments)
    {
        var uriString = TypeConverter.ToString(arguments.At(0));

        return Encode(uriString, UriUnescaped);
    }

    private JsValue Encode(string uriString, SearchValues<char> allowedCharacters)
    {
        var strLen = uriString.Length;
        var builder = new ValueStringBuilder(uriString.Length);
        Span<byte> buffer = stackalloc byte[4];

        for (var k = 0; k < strLen; k++)
        {
            var c = uriString[k];
            if (allowedCharacters.Contains(c))
            {
                builder.Append(c);
            }
            else
            {
                if (c >= 0xDC00 && c <= 0xDBFF)
                {
                    goto uriError;
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
                        goto uriError;
                    }

                    var kChar = (int) uriString[k];
                    if (kChar is < 0xDC00 or > 0xDFFF)
                    {
                        goto uriError;
                    }

                    v = (c - 0xD800) * 0x400 + (kChar - 0xDC00) + 0x10000;
                }

                var length = 1;
                switch (v)
                {
                    case >= 0 and <= 0x007F:
                        // 00000000 0zzzzzzz -> 0zzzzzzz
                        buffer[0] = (byte) v;
                        break;
                    case <= 0x07FF:
                        // 00000yyy yyzzzzzz ->	110yyyyy ; 10zzzzzz
                        length = 2;
                        buffer[0] = (byte) (0xC0 | (v >> 6));
                        buffer[1] = (byte) (0x80 | (v & 0x3F));
                        break;
                    case <= 0xD7FF:
                        // xxxxyyyy yyzzzzzz -> 1110xxxx; 10yyyyyy; 10zzzzzz
                        length = 3;
                        buffer[0] = (byte) (0xE0 | (v >> 12));
                        buffer[1] = (byte) (0x80 | ((v >> 6) & 0x3F));
                        buffer[2] = (byte) (0x80 | (v & 0x3F));
                        break;
                    case <= 0xDFFF:
                        goto uriError;
                    case <= 0xFFFF:
                        length = 3;
                        buffer[0] = (byte) (0xE0 | (v >> 12));
                        buffer[1] = (byte) (0x80 | ((v >> 6) & 0x3F));
                        buffer[2] = (byte) (0x80 | (v & 0x3F));
                        break;
                    default:
                        length = 4;
                        buffer[0] = (byte) (0xF0 | (v >> 18));
                        buffer[1] = (byte) (0x80 | (v >> 12 & 0x3F));
                        buffer[2] = (byte) (0x80 | (v >> 6 & 0x3F));
                        buffer[3] = (byte) (0x80 | (v >> 0 & 0x3F));
                        break;
                }

                for (var i = 0; i < length; i++)
                {
                    builder.Append('%');
                    builder.AppendHex(buffer[i]);
                }
            }
        }

        return builder.ToString();

        uriError:
        _engine.SignalError(ExceptionHelper.CreateUriError(_realm, "URI malformed"));
        return JsEmpty.Instance;
    }

    private JsValue DecodeUri(JsValue thisObject, JsCallArguments arguments)
    {
        var uriString = TypeConverter.ToString(arguments.At(0));

        return Decode(uriString, ReservedUriSet);
    }

    private JsValue DecodeUriComponent(JsValue thisObject, JsCallArguments arguments)
    {
        var componentString = TypeConverter.ToString(arguments.At(0));

        return Decode(componentString, null);
    }

    private JsValue Decode(string uriString, SearchValues<char>? reservedSet)
    {
        var strLen = uriString.Length;

        _stringBuilder.EnsureCapacity(strLen);
        _stringBuilder.Clear();

#if SUPPORTS_SPAN_PARSE
        Span<byte> octets = stackalloc byte[4];
#else
        var octets = new byte[4];
#endif

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
                    goto uriError;
                }

                var c1 = uriString[k + 1];
                var c2 = uriString[k + 2];
                if (!IsValidHexaChar(c1) || !IsValidHexaChar(c2))
                {
                    goto uriError;
                }

                var B = StringToIntBase16(uriString.AsSpan(k + 1, 2));

                k += 2;
                if ((B & 0x80) == 0)
                {
                    C = (char)B;
#pragma warning disable CA2249
                    if (reservedSet == null || !reservedSet.Contains(C))
#pragma warning restore CA2249
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
                    for (; ((B << n) & 0x80) != 0; n++)
                    {
                    }

                    if (n == 1 || n > 4)
                    {
                        goto uriError;
                    }

                    octets[0] = B;

                    if (k + (3 * (n - 1)) >= strLen)
                    {
                        goto uriError;
                    }

                    for (var j = 1; j < n; j++)
                    {
                        k++;
                        if (uriString[k] != '%')
                        {
                            goto uriError;
                        }

                        c1 = uriString[k + 1];
                        c2 = uriString[k + 2];
                        if (!IsValidHexaChar(c1) || !IsValidHexaChar(c2))
                        {
                            goto uriError;
                        }

                        B = StringToIntBase16(uriString.AsSpan(k + 1, 2));

                        // B & 11000000 != 10000000
                        if ((B & 0xC0) != 0x80)
                        {
                            goto uriError;
                        }

                        k += 2;

                        octets[j] = B;
                    }

#if SUPPORTS_SPAN_PARSE
                        _stringBuilder.Append(Encoding.UTF8.GetString(octets.Slice(0, n)));
#else
                    _stringBuilder.Append(Encoding.UTF8.GetString(octets, 0, n));
#endif
                }
            }
        }

        return _stringBuilder.ToString();

        uriError:
        _engine.SignalError(ExceptionHelper.CreateUriError(_realm, "URI malformed"));
        return JsEmpty.Instance;
    }

    private static byte StringToIntBase16(ReadOnlySpan<char> s)
    {
        var i = 0;
        var length = s.Length;

        if (s[i] == '+')
        {
            i++;
        }

        if (i + 1 < length && s[i] == '0')
        {
            if (s[i + 1] == 'x' || s[i + 1] == 'X')
            {
                i += 2;
            }
        }

        uint result = 0;
        while (i < s.Length && IsDigit(s[i], 16, out var value))
        {
            result = result * 16 + (uint) value;
            i++;
        }

        return (byte) (int) result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDigit(char c, int radix, out int result)
    {
        int tmp;
        if ((uint)(c - '0') <= 9)
        {
            result = tmp = c - '0';
        }
        else if ((uint)(c - 'A') <= 'Z' - 'A')
        {
            result = tmp = c - 'A' + 10;
        }
        else if ((uint)(c - 'a') <= 'z' - 'a')
        {
            result = tmp = c - 'a' + 10;
        }
        else
        {
            result = -1;
            return false;
        }

        return tmp < radix;
    }

    private static readonly SearchValues<char> EscapeAllowList = SearchValues.Create(Character.AsciiWordCharacters + "@*+-./");

    /// <summary>
    /// https://tc39.es/ecma262/#sec-escape-string
    /// </summary>
    private JsValue Escape(JsValue thisObject, JsCallArguments arguments)
    {
        var uriString = TypeConverter.ToString(arguments.At(0));

        var builder = new ValueStringBuilder(uriString.Length);

        foreach (var c in uriString)
        {
            if (EscapeAllowList.Contains(c))
            {
                builder.Append(c);
            }
            else if (c < 256)
            {
                builder.Append('%');
                builder.AppendHex((byte) c);
            }
            else
            {
                builder.Append("%u");
                builder.Append(((int) c).ToString("X4", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-B.2.2
    /// </summary>
    private JsValue Unescape(JsValue thisObject, JsCallArguments arguments)
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
                    && AreValidHexChars(uriString.AsSpan(k + 2, 4)))
                {
                    c = ParseHexString(uriString.AsSpan(k + 2, 4));
                    k += 5;
                }
                else if (k <= strLen - 3 && AreValidHexChars(uriString.AsSpan(k + 1, 2)))
                {
                    c = ParseHexString(uriString.AsSpan(k + 1, 2));
                    k += 2;
                }
            }
            _stringBuilder.Append(c);
        }

        return _stringBuilder.ToString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool AreValidHexChars(ReadOnlySpan<char> input)
        {
            foreach (var c in input)
            {
                if (!IsValidHexaChar(c))
                {
                    return false;
                }
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static char ParseHexString(ReadOnlySpan<char> input)
        {
#if NET6_0_OR_GREATER
            return (char) int.Parse(input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
#else
            return (char) int.Parse(input.ToString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
#endif
        }
    }

    // optimized versions with string parameter and without virtual dispatch for global environment usage

    internal bool HasProperty(Key property)
    {
        return GetOwnProperty(property) != PropertyDescriptor.Undefined;
    }

    private bool DefineOwnProperty(Key property, PropertyDescriptor desc)
    {
        var current = GetOwnProperty(property);
        if (current == desc)
        {
            return true;
        }

        // check fast path
        if ((current._flags & PropertyFlag.MutableBinding) != PropertyFlag.None)
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
            if ((existingDescriptor._flags & PropertyFlag.MutableBinding) != PropertyFlag.None)
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

        setter.Call(this, value);

        return true;
    }
}

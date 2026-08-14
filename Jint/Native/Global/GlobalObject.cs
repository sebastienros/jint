using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Jint.Extensions;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Global;

[JsObject(UseShape = true)]
public sealed partial class GlobalObject : ObjectInstance
{
    private readonly Realm _realm;
    private ErrorDispatchInfo? _uriError;
    private ErrorDispatchInfo UriError => _uriError ??= Throw.CreateUriError(_realm, "URI malformed");

    internal GlobalObject(
        Engine engine,
        Realm realm) : base(engine, ObjectClass.Object, InternalTypes.Object | InternalTypes.PlainObject)
    {
        _realm = realm;
    }

    [JsFunction(Name = "toString", Length = 1)]
    private JsValue ToStringString(JsValue thisObject)
    {
        return _realm.Intrinsics.Object.PrototypeObject.ToObjectString(thisObject);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-parseint-string-radix
    /// </summary>
    internal static JsValue ParseInt(JsValue value, JsValue radixValue)
    {
        var inputString = TypeConverter.ToString(value);
        var trimmed = StringPrototype.TrimEx(inputString);
        var s = trimmed.AsSpan();

        // Steps 6-8: the sign is recorded and removed before anything else reads the string, because
        // the "0x" test in step 9 is against the string the sign has already left.
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

        // Steps 2-3 and 10. stripPrefix captures step 9's "radixMV is 0 or 16" before step 10
        // defaults it to 10; the spec validates the range at step 3, before trimming, which is a
        // reordering nothing can observe because no user code runs in between. An omitted radix
        // arrives as undefined, whose ToInt32 is the 0 the absent case produced.
        var radix = TypeConverter.ToInt32(radixValue);
        var stripPrefix = true;
        if (radix == 0)
        {
            radix = 10;
        }
        else if (radix < 2 || radix > 36)
        {
            return JsNumber.DoubleNaN;
        }
        else if (radix != 16)
        {
            stripPrefix = false;
        }

        // Step 9. Only 'x' and 'X' fold onto each other under bit 0x20, so the two comparisons are
        // the whole of the spec's "0x" or "0X" test.
        if (stripPrefix && s.Length > 1 && s[0] == '0' && (s[1] | 0x20) == 'x')
        {
            s = s.Slice(2);
            radix = 16;
        }

        // check fast case
        if (radix == 10 && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            // Step 15: zero keeps a recorded minus sign, and the cached integers are all positive.
            return number == 0 && sign == -1 ? JsNumber.NegativeZero : JsNumber.Create(number);
        }

        // Step 5, deferred to here: an input that is empty, or empty once the sign and prefix are
        // gone, has no digits to read.
        if (s.Length == 0)
        {
            return JsNumber.DoubleNaN;
        }

        // Steps 11-14: numberString is the longest radix-R digit prefix. Accumulating from the end
        // and resetting on every non-digit leaves exactly that prefix standing at index 0.
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

        // Steps 15-16: sign * 0 is already -0 in IEEE 754, and JsNumber.Create keeps that sign.
        return hasResult ? JsNumber.Create(sign * result) : JsNumber.DoubleNaN;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-parsefloat-string
    /// </summary>
    internal static JsValue ParseFloat(JsValue value)
    {
        var inputString = TypeConverter.ToString(value);
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

        var substring = trimmedString.AsSpan(0, i);

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
    // The body is one ToNumber, so the guard is the set of values that conversion answers without
    // asking the value anything: a number is itself, a string is parsed — an unparseable one is NaN,
    // not a throw — and undefined is NaN. Symbol and BigInt are the two primitives ToNumber raises a
    // TypeError for and an object reaches user code through valueOf, so both keep the frame. The
    // receiver is not read.
    [JsFunction(Leaf = true, LeafArg0 = FastCallGuard.Number | FastCallGuard.String | FastCallGuard.Undefined)]
    private static JsValue IsNaN(JsValue thisObject, JsValue value)
    {
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
    // Same single ToNumber, same guard.
    [JsFunction(Leaf = true, LeafArg0 = FastCallGuard.Number | FastCallGuard.String | FastCallGuard.Undefined)]
    private static JsValue IsFinite(JsValue thisObject, JsValue value)
    {
        var n = TypeConverter.ToNumber(value);
        return double.IsFinite(n);
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
    [JsFunction(Name = "encodeURI")]
    private JsValue EncodeUri(JsValue thisObject, [ToString] string uri)
    {
        return Encode(uri, UnescapedUriSet);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-encodeuricomponent-uricomponent
    /// </summary>
    [JsFunction(Name = "encodeURIComponent")]
    private JsValue EncodeUriComponent(JsValue thisObject, [ToString] string uri)
    {
        return Encode(uri, UriUnescaped);
    }

    [MethodImpl(512)]
    private JsValue Encode(string uri, SearchValues<char> allowedCharacters)
    {
        var strLen = uri.Length;

        // Nothing needs escaping: hand the input straight back rather than rebuilding it character by
        // character into a fresh buffer. This is the shape Decode already has for a '%'-free input, and
        // the overwhelmingly common one — most strings handed to encodeURI are already URI-safe.
        var k = IndexOfFirstDisallowed(uri.AsSpan(), allowedCharacters);
        if (k < 0)
        {
            return uri;
        }

        var builder = new ValueStringBuilder(uri.Length);
        builder.Append(uri.AsSpan(0, k));
        Span<byte> buffer = stackalloc byte[4];

        for (; k < strLen; k++)
        {
            var c = uri[k];
            if (allowedCharacters.Contains(c))
            {
                // Copy the whole clean run at once instead of one character per loop turn. The run
                // cannot be empty (c is allowed), so k always advances.
                var remaining = uri.AsSpan(k);
                var next = IndexOfFirstDisallowed(remaining, allowedCharacters);
                var runLength = next < 0 ? remaining.Length : next;
                builder.Append(remaining.Slice(0, runLength));
                k += runLength - 1;
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

                    var kChar = (int) uri[k];
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
        builder.Dispose();
        _engine.SignalError(UriError);
        return JsEmpty.Instance;
    }

    /// <summary>
    /// Index of the first character <paramref name="allowed"/> does not cover, or -1 when it covers every
    /// one of them. Vectorized where the runtime provides it, and a character-at-a-time scan through the
    /// polyfill everywhere else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfFirstDisallowed(ReadOnlySpan<char> value, SearchValues<char> allowed)
    {
        return value.IndexOfAnyExcept(allowed);
    }

    [JsFunction(Name = "decodeURI")]
    private JsValue DecodeUri(JsValue thisObject, [ToString] string uri)
    {
        return Decode(uri, ReservedUriSet);
    }

    [JsFunction(Name = "decodeURIComponent")]
    private JsValue DecodeUriComponent(JsValue thisObject, [ToString] string uri)
    {
        return Decode(uri, null);
    }

    [MethodImpl(512)]
    private JsValue Decode(string uri, SearchValues<char>? reservedSet)
    {
        var strLen = uri.Length;

        if (!uri.Contains('%'))
        {
            return uri;
        }

        var builder = new ValueStringBuilder(stackalloc char[256]);
        builder.EnsureCapacity(strLen);

        Span<byte> octets = stackalloc byte[4];
        for (var k = 0; k < strLen; k++)
        {
            var C = uri[k];
            if (C != '%')
            {
                builder.Append(C);
            }
            else
            {
                var start = k;
                if (k + 2 >= strLen)
                {
                    goto uriError;
                }

                var c1 = uri[k + 1];
                var c2 = uri[k + 2];
                if (!IsValidHexaChar(c1) || !IsValidHexaChar(c2))
                {
                    goto uriError;
                }

                var B = HexToByteUnchecked(c1, c2);

                k += 2;
                if ((B & 0x80) == 0)
                {
                    C = (char) B;
#pragma warning disable CA2249
                    if (reservedSet == null || !reservedSet.Contains(C))
#pragma warning restore CA2249
                    {
                        builder.Append(C);
                    }
                    else
                    {
                        builder.Append(uri.AsSpan(start, k - start + 1));
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
                        if (uri[k] != '%')
                        {
                            goto uriError;
                        }

                        c1 = uri[k + 1];
                        c2 = uri[k + 2];
                        if (!IsValidHexaChar(c1) || !IsValidHexaChar(c2))
                        {
                            goto uriError;
                        }

                        B = HexToByteUnchecked(c1, c2);

                        // B & 11000000 != 10000000
                        if ((B & 0xC0) != 0x80)
                        {
                            goto uriError;
                        }

                        k += 2;

                        octets[j] = B;
                    }

                    switch (n)
                    {
                        case 2:
                            {
                                // Overlong encoding check for 2-byte sequences
                                var x = octets[0] & 0x1F;
                                var y = octets[1] & 0x3F;
                                var codepoint = (x << 6) | y;

                                if (codepoint < 0x80) // 2-byte should be ≥ 0x80
                                {
                                    goto uriError;
                                }

                                builder.Append((char) codepoint);
                                break;
                            }
                        case 3:
                            {
                                // Reserved surrogate pair (U+D800-DFFF)
                                var x = octets[0] & 0x0F;
                                var y = octets[1] & 0x3F;
                                var z = octets[2] & 0x3F;
                                var codepoint = (x << 12) | (y << 6) | z;

                                if (codepoint is >= 0xD800 and <= 0xDFFF)
                                {
                                    goto uriError;
                                }

                                builder.Append((char) codepoint);
                                break;
                            }
                        case 4:
                            {
                                var x = octets[0] & 0x07;
                                var y = octets[1] & 0x3F;
                                var z = octets[2] & 0x3F;
                                var w = octets[3] & 0x3F;
                                var codepoint = (x << 18) | (y << 12) | (z << 6) | w;

                                if (codepoint > 0x10FFFF)
                                {
                                    goto uriError;
                                }

                                // Convert to UTF-16 surrogate pair
                                var offset = codepoint - 0x10000;
                                var highSurrogate = (char) (0xD800 + (offset >> 10));
                                var lowSurrogate = (char) (0xDC00 + (offset & 0x3FF));
                                builder.Append(highSurrogate);
                                builder.Append(lowSurrogate);
                                break;
                            }
                    }
                }
            }
        }

        return builder.ToString();

uriError:
        builder.Dispose();
        _engine.SignalError(UriError);
        return JsEmpty.Instance;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HexToByteUnchecked(char c1, char c2)
    {
        // Fast 2-char hex to byte conversion for %XX percent-encoded sequences
        // Assumes c1 and c2 are valid hex digits (already validated by IsValidHexaChar)
        return (byte) ((HexValue(c1) << 4) | HexValue(c2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexValue(char c)
    {
        // Branch-free hex digit conversion
        // '0'-'9' (0x30-0x39) -> 0-9
        // 'A'-'F' (0x41-0x46) -> 10-15
        // 'a'-'f' (0x61-0x66) -> 10-15
        if (c <= '9') return c - '0';
        if (c <= 'F') return c - 'A' + 10;
        return c - 'a' + 10;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDigit(char c, int radix, out int result)
    {
        int tmp;
        if ((uint) (c - '0') <= 9)
        {
            result = tmp = c - '0';
        }
        else if ((uint) (c - 'A') <= 'Z' - 'A')
        {
            result = tmp = c - 'A' + 10;
        }
        else if ((uint) (c - 'a') <= 'z' - 'a')
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
    [JsFunction]
    private static JsValue Escape(JsValue thisObject, [ToString] string uri)
    {
        var builder = new ValueStringBuilder(uri.Length);

        foreach (var c in uri)
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
    [JsFunction]
    private static JsValue Unescape(JsValue thisObject, [ToString] string uri)
    {
        if (!uri.Contains('%'))
        {
            return uri;
        }

        var strLen = uri.Length;
        var builder = new ValueStringBuilder(stackalloc char[256]);
        builder.EnsureCapacity(strLen);

        for (var k = 0; k < strLen; k++)
        {
            var c = uri[k];
            if (c == '%')
            {
                if (k <= strLen - 6
                    && uri[k + 1] == 'u'
                    && AreValidHexChars(uri.AsSpan(k + 2, 4)))
                {
                    c = ParseHexString(uri.AsSpan(k + 2, 4));
                    k += 5;
                }
                else if (k <= strLen - 3 && AreValidHexChars(uri.AsSpan(k + 1, 2)))
                {
                    c = ParseHexString(uri.AsSpan(k + 1, 2));
                    k += 2;
                }
            }
            builder.Append(c);
        }

        return builder.ToString();

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
            return (char) int.Parse(input, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        }
    }

    // optimized versions with string parameter and without virtual dispatch for global environment usage

    internal bool HasOwnProperty(Key property)
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

        // the validate/apply protocol stores through the property dictionary
        EnsureDictionaryProperties();
        return ValidateAndApplyPropertyDescriptor(this, new JsString(property), true, desc, current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PropertyDescriptor GetOwnProperty(Key property)
    {
        if (_properties is not null)
        {
            _properties.TryGetValue(property, out var descriptor);
            if (descriptor is not null || (_type & InternalTypes.BuiltinShapeMode) == InternalTypes.Empty)
            {
                return descriptor ?? PropertyDescriptor.Undefined;
            }
            // hybrid: side dictionary miss on a still-shaped global falls through to the shape
        }

        return GetOwnPropertyFromShape(property);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private PropertyDescriptor GetOwnPropertyFromShape(Key property)
    {
        // builtin-shape mode (no dictionary until something deopts the global): misses cost an
        // index probe, hits materialize the slot's descriptor once with stable identity.
        EnsureInitialized();
        var shaped = (IBuiltinShaped) this;
        if (shaped.BuiltinDescriptors is not null && shaped.BuiltinShape.Index.TryGetValue(property.Name, out var slot))
        {
            return MaterializeBuiltinSlot(shaped, slot);
        }

        // EnsureInitialized may have deopted or filled the dictionary meanwhile
        if (_properties is not null && _properties.TryGetValue(property, out var descriptor))
        {
            return descriptor ?? PropertyDescriptor.Undefined;
        }

        return PropertyDescriptor.Undefined;
    }

    internal bool SetFromMutableBinding(Key property, JsValue value, bool strict)
    {
        // here we are called only from global environment record context
        // we can take some shortcuts to be faster

        PropertyDescriptor? existingDescriptor = null;
        _properties?.TryGetValue(property, out existingDescriptor);
        if (existingDescriptor is null && (_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty)
        {
            existingDescriptor = GetOwnPropertyFromShape(property);
            if (existingDescriptor == PropertyDescriptor.Undefined)
            {
                existingDescriptor = null;
            }
        }

        if (existingDescriptor is null)
        {
            if (_prototype is not null && TrySetThroughPrototype(property, value, out var setResult))
            {
                return setResult;
            }
            if (strict)
            {
                Throw.ReferenceNameError(_realm, property.Name);
            }
            var fresh = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable | PropertyFlag.MutableBinding);
            // binding names are identifiers, so a still-shaped global takes the hybrid side dictionary
            if ((_type & InternalTypes.BuiltinShapeMode) != InternalTypes.Empty && TryHybridAddToShapedHost(property, fresh))
            {
                return true;
            }
            _properties ??= new PropertyDictionary();
            _properties[property] = fresh;
            unchecked { _propertiesVersion++; }
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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-environment-records-setmutablebinding-n-v-s — a name
    /// that exists on the global's prototype chain assigns through [[Set]] with the global as
    /// receiver (running inherited setters, respecting inherited non-writable data) instead of
    /// blindly creating an own property.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrySetThroughPrototype(Key property, JsValue value, out bool result)
    {
        var jsName = JsString.Create(property.Name);
        if (!_prototype!.HasProperty(jsName))
        {
            result = false;
            return false;
        }

        result = Set(jsName, value, this);

        if (result)
        {
            MarkBindingPropertyCreatedBySet(property);
        }

        return true;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-putvalue step 2.c — a sloppy assignment to a name that resolves
    /// nowhere at all has an unresolvable reference, so PutValue never reaches the global environment
    /// record and performs Set(globalObj, V.[[ReferencedName]], W, false) here instead. What that
    /// creates is a global variable-like binding all the same, so it is marked like every other route
    /// that creates one and comes out indistinguishable from an eval-scoped <c>var</c>'s global.
    /// </summary>
    /// <remarks>
    /// <para>Unresolvability establishes what <see cref="MarkBindingPropertyCreatedBySet"/> needs: the
    /// name resolved on no environment of the chain, which for the last of them means neither an own
    /// property of the global nor anything on its prototype chain, so whatever own property is there
    /// once [[Set]] returns was created by it. A right-hand side that reached <c>globalThis</c> between
    /// the reference and the assignment is the one way that is not literally true, and it is covered
    /// anyway: only the exact descriptor shape CreateDataProperty produces is ever marked, which is a
    /// plain writable global data property either way.</para>
    /// <para>The property name of an unresolvable reference is always an identifier name — only
    /// Engine.GetIdentifierReference produces the unresolvable sentinel, and it always carries a
    /// <see cref="JsString"/>.</para>
    /// </remarks>
    internal void SetFromUnresolvableAssignment(JsString property, JsValue value)
    {
        // [[Set]] can still decline - a non-extensible global has nowhere to put the property, and the
        // sloppy assignment is a silent no-op - in which case there is nothing to mark.
        if (Set(property, value))
        {
            MarkBindingPropertyCreatedBySet(property.ToString());
        }
    }

    /// <summary>
    /// Where a global binding is created by ordinary [[Set]] rather than by the binding machinery's own
    /// helper, the property comes out with the right attributes but without
    /// <see cref="PropertyFlag.MutableBinding"/>, the marker telling the two stores above they may write
    /// the descriptor's value in place. So a property the binding machinery had itself just created sent
    /// every later write of that name down the validate-and-apply path instead — allocating a descriptor
    /// and a key each time, and for the rest of the engine's life, since nothing ever puts the marker on
    /// afterwards. Every other way a global binding comes into being marks it: CreateGlobalVarBinding for
    /// a <c>var</c> declaration, and the branch above for a binding that resolves to nothing on the
    /// prototype either.
    /// </summary>
    /// <remarks>
    /// Only the exact descriptor CreateDataProperty leaves behind is marked. Each caller has already
    /// established that the global had no own property of this name before the [[Set]], so whatever is
    /// there now was created by it — but an inherited setter is free to have defined something of its
    /// own shape during the call, and that is left alone.
    /// </remarks>
    private void MarkBindingPropertyCreatedBySet(Key property)
    {
        var created = GetOwnProperty(property);
        if (created != PropertyDescriptor.Undefined
            && created._flags == PropertyFlag.ConfigurableEnumerableWritable)
        {
            created._flags |= PropertyFlag.MutableBinding;
        }
    }
}

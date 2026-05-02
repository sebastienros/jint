using System.Buffers;
using System.Globalization;
using Jint.Extensions;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.TypedArray;

[JsObject]
public sealed partial class Uint8ArrayConstructor : TypedArrayConstructor
{
    // Uint8 element size is fixed at 1; the base class also tracks BYTES_PER_ELEMENT but its
    // generator-emitted CreateProperties_Generated() is private, so this subclass redeclares the
    // entry locally. Duplicate, but cheap (single static field) and avoids fragile base.Initialize
    // chaining (SetProperties replaces _properties wholesale).
    [JsProperty(Name = "BYTES_PER_ELEMENT", Flags = PropertyFlag.AllForbidden)]
    private static readonly JsNumber BytesPerElementValue = JsNumber.PositiveOne;

    internal Uint8ArrayConstructor(
        Engine engine,
        Realm realm,
        IntrinsicTypedArrayConstructor functionPrototype,
        IntrinsicTypedArrayPrototype objectPrototype) : base(engine, realm, functionPrototype, objectPrototype, TypedArrayElementType.Uint8)
    {
    }

    protected override void Initialize() => CreateProperties_Generated();

    public JsTypedArray Construct(ReadOnlySpan<byte> values)
    {
        var array = (JsTypedArray) base.Construct([values.Length], this);
        values.CopyTo(array._viewedArrayBuffer._arrayBufferData.AsSpan());
        return array;
    }

    [JsFunction(Length = 1)]
    private JsTypedArray FromBase64(JsValue thisObject, JsValue s, JsValue options)
    {
        if (!s.IsString())
        {
            Throw.TypeError(_realm, "fromBase64 must be called with a string");
        }

        var opts = GetOptionsObject(_engine, options);
        var alphabet = GetAndValidateAlphabetOption(_engine, opts);
        var lastChunkHandling = GetAndValidateLastChunkHandling(_engine, opts);

        var result = FromBase64(_engine, s.ToString(), alphabet.ToString(), lastChunkHandling.ToString());
        if (result.Error is not null)
        {
            throw result.Error;
        }

        var ta = _realm.Intrinsics.Uint8Array.Construct(new JsArrayBuffer(_engine, result.Bytes));
        return ta;
    }

    internal static JsString GetAndValidateLastChunkHandling(Engine engine, ObjectInstance opts)
    {
        var lastChunkHandling = opts.Get("lastChunkHandling");
        if (lastChunkHandling.IsUndefined())
        {
            lastChunkHandling = "loose";
        }

        if (lastChunkHandling is not JsString s || (s != "loose" && s != "strict" && s != "stop-before-partial"))
        {
            Throw.TypeError(engine.Realm, "lastChunkHandling must be either 'loose', 'strict' or 'stop-before-partial'");
            return default;
        }

        return s;
    }

    internal static JsString GetAndValidateAlphabetOption(Engine engine, ObjectInstance opts)
    {
        var alphabet = opts.Get("alphabet");
        if (alphabet.IsUndefined())
        {
            alphabet = "base64";
        }

        if (alphabet is not JsString s || (s != "base64" && s != "base64url"))
        {
            Throw.TypeError(engine.Realm, "alphabet must be either 'base64' or 'base64url'");
            return default;
        }

        return s;
    }

    internal readonly record struct FromEncodingResult(byte[] Bytes, JavaScriptException? Error, int Read);

    private static readonly SearchValues<char> Base64Alphabet = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");

    internal static FromEncodingResult FromBase64(Engine engine, string input, string alphabet, string lastChunkHandling, uint maxLength = uint.MaxValue)
    {
        if (maxLength == 0)
        {
            return new FromEncodingResult([], Error: null, 0);
        }

        var read = 0;
        var bytes = new List<byte>();
        var chunk = new char[4];
        var chunkLength = 0;
        var index = 0;
        var length = input.Length;

        var stopBeforePartial = string.Equals(lastChunkHandling, "stop-before-partial", StringComparison.Ordinal);
        var loose = string.Equals(lastChunkHandling, "loose", StringComparison.Ordinal);
        var base64Url = string.Equals(alphabet, "base64url", StringComparison.Ordinal);
        var throwOnExtraBits = string.Equals(lastChunkHandling, "strict", StringComparison.Ordinal);

        while (true)
        {
            index = SkipAsciiWhitespace(input, index);
            if (index == length)
            {
                if (chunkLength > 0)
                {
                    if (stopBeforePartial)
                    {
                        return new FromEncodingResult(bytes.ToArray(), Error: null, read);
                    }

                    if (loose)
                    {
                        if (chunkLength == 1)
                        {
                            return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid base64 chunk length."), read);
                        }

                        DecodeBase64Chunk(engine, bytes, chunk, chunkLength, throwOnExtraBits: false);
                    }
                    else // strict
                    {
                        return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid base64 chunk length in strict mode."), read);
                    }
                }

                return new FromEncodingResult(bytes.ToArray(), Error: null, length);
            }

            char currentChar = input[index];
            index++;

            if (currentChar == '=')
            {
                if (chunkLength < 2)
                {
                    return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid '=' placement in base64 string."), read);
                }

                index = SkipAsciiWhitespace(input, index);
                if (chunkLength == 2)
                {
                    if (index == length)
                    {
                        if (stopBeforePartial)
                        {
                            return new FromEncodingResult(bytes.ToArray(), Error: null, read);
                        }

                        return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid base64 string termination."), read);
                    }

                    currentChar = input[index];
                    if (currentChar != '=')
                    {
                        return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Expected '=' in base64 string."), read);
                    }

                    index = SkipAsciiWhitespace(input, index + 1);
                }

                if (index < length)
                {
                    return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Extra characters after base64 string."), read);
                }

                DecodeBase64Chunk(engine, bytes, chunk, chunkLength, throwOnExtraBits);
                return new FromEncodingResult(bytes.ToArray(), Error: null, length);
            }

            if (base64Url)
            {
                if (currentChar is '+' or '/')
                {
                    return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid character in base64url string."), read);
                }

                if (currentChar == '-')
                {
                    currentChar = '+';
                }

                if (currentChar == '_')
                {
                    currentChar = '/';
                }
            }

            if (!Base64Alphabet.Contains(currentChar))
            {
                return new FromEncodingResult(bytes.ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid base64 character."), read);
            }

            ulong remaining = maxLength - (ulong) bytes.Count;
            if ((remaining == 1 && chunkLength == 2) || (remaining == 2 && chunkLength == 3))
            {
                return new FromEncodingResult(bytes.ToArray(), Error: null, read);
            }

            chunk[chunkLength] = currentChar;
            chunkLength++;

            if (chunkLength == 4)
            {
                DecodeBase64Chunk(engine, bytes, chunk, chunkLength, throwOnExtraBits: false);
                chunkLength = 0;
                read = index;
                if (bytes.Count == maxLength)
                {
                    return new FromEncodingResult(bytes.ToArray(), Error: null, read);
                }
            }
        }
    }

    private static int SkipAsciiWhitespace(string input, int index)
    {
        while (index < input.Length)
        {
            var c = input[index];
            if (c != 0x0009 && c != 0x000A && c != 0x000C && c != 0x000D && c != 0x0020)
            {
                return index;
            }

            index++;
        }

        return index;
    }

    private static void DecodeBase64Chunk(
        Engine engine,
        List<byte> into,
        char[] chunk,
        int chunkLength,
        bool throwOnExtraBits = false)
    {
        if (chunkLength == 2)
        {
            chunk[2] = 'A';
            chunk[3] = 'A';
        }
        else if (chunkLength == 3)
        {
            chunk[3] = 'A';
        }

        var bytes = WebEncoders.Base64UrlDecode(chunk);

        if (chunkLength == 2)
        {
            if (throwOnExtraBits && bytes[1] != 0)
            {
                Throw.SyntaxError(engine.Realm, "Invalid padding in base64 chunk.");
            }
            into.Add(bytes[0]);
            return;
        }

        if (chunkLength == 3)
        {
            if (throwOnExtraBits && bytes[2] != 0)
            {
                Throw.SyntaxError(engine.Realm, "Invalid padding in base64 chunk.");
            }
            into.Add(bytes[0]);
            into.Add(bytes[1]);
            return;
        }

        into.AddRange(bytes);
    }

    [JsFunction]
    private JsTypedArray FromHex(JsValue thisObject, JsValue s)
    {
        if (!s.IsString())
        {
            Throw.TypeError(_realm, "fromHex must be called with a string");
        }

        var result = FromHex(_engine, s.ToString());
        if (result.Error is not null)
        {
            throw result.Error;
        }

        var ta = _realm.Intrinsics.Uint8Array.Construct(new JsArrayBuffer(_engine, result.Bytes));
        ta._viewedArrayBuffer._arrayBufferData = result.Bytes;
        return ta;
    }

    internal static FromEncodingResult FromHex(Engine engine, string s, uint maxLength = int.MaxValue)
    {
        var length = s.Length;
        var bytes = new byte[System.Math.Min(maxLength, length / 2)];
        var read = 0;

        if (length % 2 != 0)
        {
            return new FromEncodingResult([], Throw.CreateSyntaxError(engine.Realm, "Invalid hex string"), read);
        }

        var byteIndex = 0;
        while (read < length && byteIndex < maxLength)
        {
            var hexits = s.AsSpan(read, 2);
            if (!hexits[0].IsHexDigit() || !hexits[1].IsHexDigit())
            {
                return new FromEncodingResult(bytes.AsSpan(0, byteIndex).ToArray(), Throw.CreateSyntaxError(engine.Realm, "Invalid hex value"), read);
            }

#if SUPPORTS_SPAN_PARSE
            var b = byte.Parse(hexits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#else
            var b = byte.Parse(hexits.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
#endif
            bytes[byteIndex++] = b;
            read += 2;
        }

        return new FromEncodingResult(bytes, Error: null, read);
    }

    internal static ObjectInstance GetOptionsObject(Engine engine, JsValue options)
    {
        if (options.IsUndefined())
        {
            return new JsObject(engine);
        }

        if (options.IsObject())
        {
            return options.AsObject();
        }

        Throw.TypeError(engine.Realm, "Invalid options");
        return default;
    }
}

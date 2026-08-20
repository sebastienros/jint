#if NET8_0_OR_GREATER
using System.Buffers;
using SystemEncoding = System.Text.Encoding;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.WebApi.Files;

/// <summary>
/// The pieces of the File API that several of its interfaces share: the byte-sequence builder behind the
/// <c>Blob</c> and <c>File</c> constructors, the media-type normalization both of them and
/// <c>Blob.slice</c> perform, and the WebIDL scalar conversions their arguments declare.
/// <para>
/// https://w3c.github.io/FileAPI/
/// </para>
/// </summary>
internal static class FileApi
{
    /// <summary>
    /// The name a bare <c>Blob</c> is given when <c>FormData</c> wraps it into a <c>File</c>.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/form-control-infrastructure.html#create-an-entry
    /// </para>
    /// </summary>
    internal const string DefaultBlobFileName = "blob";

    private const string EndingsTransparent = "transparent";
    private const string EndingsNative = "native";

    /// <summary>
    /// Processing blob parts, https://w3c.github.io/FileAPI/#process-blob-parts, fused with the WebIDL
    /// conversion of the <c>sequence&lt;BlobPart&gt;</c> argument that feeds it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two are fused because the conversion is what makes each element a <c>BufferSource</c>, a
    /// <c>Blob</c> or a <c>USVString</c>, and that is observable: an element that is none of the first two
    /// reaches <c>toString</c>, and it does so while the sequence is being drained — before the options
    /// dictionary's members are read. Doing it in one pass keeps that order without materializing the
    /// intermediate sequence.
    /// </para>
    /// <para>
    /// A <c>SharedArrayBuffer</c> is deliberately not a <c>BufferSource</c> — the typedef covers
    /// <c>ArrayBuffer</c> and the views, and sharing is only admitted where <c>[AllowShared]</c> says so,
    /// which <c>BlobPart</c> does not. It therefore falls through to the <c>USVString</c> arm and is
    /// stringified, exactly as in a browser.
    /// </para>
    /// </remarks>
    internal static byte[] ProcessBlobParts(Realm realm, JsValue parts)
    {
        // WebIDL sequence conversion: anything that is not an object cannot be one, and that includes a
        // bare string — `new Blob("abc")` is a TypeError, not a one-character-per-element blob.
        if (parts is not ObjectInstance)
        {
            Throw.TypeError(realm, "The provided value cannot be converted to a sequence");
        }

        var writer = new ArrayBufferWriter<byte>();
        var iterator = parts.GetIterator(realm);
        while (iterator.TryIteratorStepValue(out var element))
        {
            AppendBlobPart(writer, element);
        }

        return writer.WrittenSpan.ToArray();
    }

    private static void AppendBlobPart(ArrayBufferWriter<byte> writer, JsValue element)
    {
        switch (element)
        {
            // A detached buffer holds no bytes to copy, so it contributes none. Nothing here can observe
            // the difference between that and an empty buffer.
            case JsArrayBuffer { IsSharedArrayBuffer: false } buffer:
                Append(writer, buffer.ArrayBufferData.AsSpan());
                return;

            case JsTypedArray typedArray:
                Append(writer, ViewBytes(typedArray));
                return;

            case JsDataView dataView:
                Append(writer, ViewBytes(dataView));
                return;

            case JsBlob blob:
                Append(writer, blob.Data.Span);
                return;

            default:
                AppendUtf8(writer, TypeConverter.ToString(element));
                return;
        }
    }

    private static ReadOnlySpan<byte> ViewBytes(JsTypedArray typedArray)
    {
        var data = typedArray._viewedArrayBuffer.ArrayBufferData;
        if (data is null)
        {
            return default;
        }

        // Length answers 0 for a view that has gone out of bounds over a resizable buffer, but the offset
        // it was created with can still be past the end of the shrunken block, so clamp both.
        var offset = System.Math.Min(typedArray._byteOffset, data.Length);
        var byteLength = System.Math.Min((int) typedArray.Length * typedArray._arrayElementType.GetElementSize(), data.Length - offset);
        return data.AsSpan(offset, byteLength);
    }

    private static ReadOnlySpan<byte> ViewBytes(JsDataView dataView)
    {
        var buffer = dataView._viewedArrayBuffer;
        var data = buffer?.ArrayBufferData;
        if (data is null)
        {
            return default;
        }

        // A resizable buffer may have shrunk under the view since it was created; clamp rather than
        // trusting the recorded extent.
        var offset = (int) System.Math.Min(dataView._byteOffset, (uint) data.Length);
        var length = (int) System.Math.Min(dataView._byteLength, (uint) (data.Length - offset));
        return data.AsSpan(offset, length);
    }

    private static void Append(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (!bytes.IsEmpty)
        {
            writer.Write(bytes);
        }
    }

    /// <summary>
    /// UTF-8 encoding of a USVString. <see cref="System.Text.Encoding.UTF8"/> substitutes U+FFFD for an unpaired
    /// surrogate, which is exactly the scalar-value-string conversion the argument's USVString type asks
    /// for, so no separate pass is needed here.
    /// </summary>
    private static void AppendUtf8(ArrayBufferWriter<byte> writer, string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var byteCount = SystemEncoding.UTF8.GetByteCount(value);
        var destination = writer.GetSpan(byteCount);
        var written = SystemEncoding.UTF8.GetBytes(value.AsSpan(), destination);
        writer.Advance(written);
    }

    /// <summary>
    /// Reads a <c>BlobPropertyBag</c> and answers the normalized <c>type</c> it carries.
    /// <para>
    /// https://w3c.github.io/FileAPI/#dfn-BlobPropertyBag
    /// </para>
    /// </summary>
    internal static string ReadBlobPropertyBag(Realm realm, JsValue options)
    {
        var dictionary = ToDictionary(realm, options);
        if (dictionary is null)
        {
            return string.Empty;
        }

        // Dictionary members are converted in lexicographical order of their identifiers, so a bag whose
        // members are getters observes `endings` before `type`.
        ReadEndings(realm, dictionary);
        return ReadType(dictionary);
    }

    /// <summary>
    /// Reads a <c>FilePropertyBag</c>: the inherited <c>BlobPropertyBag</c> members and then
    /// <c>lastModified</c>, which is absent rather than defaulted when the member is not given.
    /// <para>
    /// https://w3c.github.io/FileAPI/#dfn-FilePropertyBag
    /// </para>
    /// </summary>
    internal static string ReadFilePropertyBag(Realm realm, JsValue options, out long? lastModified)
    {
        lastModified = null;

        var dictionary = ToDictionary(realm, options);
        if (dictionary is null)
        {
            return string.Empty;
        }

        // Inherited members come first, then the derived dictionary's own — so `endings`, `type`,
        // `lastModified`.
        ReadEndings(realm, dictionary);
        var type = ReadType(dictionary);

        var value = dictionary.Get("lastModified");
        if (!value.IsUndefined())
        {
            lastModified = ToLongLong(value);
        }

        return type;
    }

    /// <summary>
    /// WebIDL dictionary conversion: <see langword="null"/> and <c>undefined</c> mean "every member
    /// defaulted", and anything that is not an object is a <c>TypeError</c>.
    /// <para>
    /// https://webidl.spec.whatwg.org/#es-dictionary
    /// </para>
    /// </summary>
    private static ObjectInstance? ToDictionary(Realm realm, JsValue options)
    {
        if (options.IsUndefined() || options.IsNull())
        {
            return null;
        }

        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm, "The provided value is not of type 'BlobPropertyBag'");
            return null;
        }

        return dictionary;
    }

    /// <summary>
    /// Validates the <c>EndingType</c> enumeration member and then discards it.
    /// </summary>
    /// <remarks>
    /// An unknown string is a <c>TypeError</c>, which is what a WebIDL enumeration conversion does; but
    /// <c>"native"</c> is honoured as a synonym for <c>"transparent"</c> rather than rewriting line
    /// endings. Jint is embedded, not run on a desktop: the "underlying platform's conventions" a browser
    /// appeals to would here be the conventions of whatever machine happens to host the embedder, so
    /// honouring it would make the bytes of a blob — and therefore a hash, a signature, a request body —
    /// depend on the host operating system. A script that wants CRLF can write it.
    /// </remarks>
    private static void ReadEndings(Realm realm, ObjectInstance dictionary)
    {
        var value = dictionary.Get("endings");
        if (value.IsUndefined())
        {
            return;
        }

        var endings = TypeConverter.ToString(value);
        if (!string.Equals(endings, EndingsTransparent, StringComparison.Ordinal)
            && !string.Equals(endings, EndingsNative, StringComparison.Ordinal))
        {
            Throw.TypeError(realm, "The provided value '" + endings + "' is not a valid enum value of type EndingType");
        }
    }

    private static string ReadType(ObjectInstance dictionary)
    {
        var value = dictionary.Get("type");
        return value.IsUndefined() ? string.Empty : NormalizeMediaType(TypeConverter.ToString(value));
    }

    /// <summary>
    /// The media-type normalization the <c>Blob</c> constructor and <c>slice</c> both perform: a type
    /// carrying anything outside U+0020 to U+007E is replaced by the empty string, and what survives is
    /// ASCII-lowercased.
    /// <para>
    /// https://w3c.github.io/FileAPI/#constructorBlob
    /// </para>
    /// </summary>
    internal static string NormalizeMediaType(string type)
    {
        foreach (var c in type)
        {
            if (c is < ' ' or > '~')
            {
                return string.Empty;
            }
        }

        // Every code point is ASCII by the loop above, so the invariant lowercase is the ASCII lowercase
        // the specification asks for.
        return type.ToLowerInvariant();
    }

    /// <summary>
    /// The WebIDL scalar value string conversion — every unpaired surrogate becomes U+FFFD.
    /// <para>
    /// https://webidl.spec.whatwg.org/#idl-USVString
    /// </para>
    /// </summary>
    internal static string ToScalarValueString(string value)
    {
        var index = IndexOfSurrogate(value, 0);
        if (index < 0)
        {
            return value;
        }

        var buffer = value.ToCharArray();
        while (index >= 0)
        {
            var c = buffer[index];
            if (char.IsHighSurrogate(c) && index + 1 < buffer.Length && char.IsLowSurrogate(buffer[index + 1]))
            {
                // A well-formed pair is one scalar value; skip past both halves.
                index = IndexOfSurrogate(buffer, index + 2);
                continue;
            }

            buffer[index] = '\uFFFD';
            index = IndexOfSurrogate(buffer, index + 1);
        }

        return new string(buffer);
    }

    private static int IndexOfSurrogate(ReadOnlySpan<char> value, int start)
    {
        for (var i = start; i < value.Length; i++)
        {
            if (char.IsSurrogate(value[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The WebIDL <c>long long</c> conversion: truncate towards zero and wrap modulo 2^64 into the signed
    /// range, with every non-finite value becoming zero.
    /// <para>
    /// https://webidl.spec.whatwg.org/#idl-long-long
    /// </para>
    /// </summary>
    internal static long ToLongLong(JsValue value)
    {
        const double TwoPow64 = 18446744073709551616.0;
        const double TwoPow63 = 9223372036854775808.0;

        var number = TypeConverter.ToNumber(value);
        if (!double.IsFinite(number))
        {
            return 0;
        }

        // The remainder is exact, and so is each of the two corrections: at those magnitudes both operands
        // are multiples of the same power of two, and the result is smaller than either. Folding the
        // negative half through [0, 2^64) instead would not be — 2^64 − 1 is not a representable double, so
        // −1 would come back as 0.
        number = System.Math.Truncate(number) % TwoPow64;
        if (number >= TwoPow63)
        {
            number -= TwoPow64;
        }
        else if (number < -TwoPow63)
        {
            number += TwoPow64;
        }

        return (long) number;
    }

    /// <summary>
    /// The WebIDL <c>[Clamp] long long</c> conversion: NaN is zero, an out-of-range value saturates
    /// rather than wrapping, and a fractional value rounds to even.
    /// <para>
    /// https://webidl.spec.whatwg.org/#Clamp
    /// </para>
    /// </summary>
    internal static long ToClampedLongLong(JsValue value)
    {
        var number = TypeConverter.ToNumber(value);
        if (double.IsNaN(number))
        {
            return 0;
        }

        if (number <= long.MinValue)
        {
            return long.MinValue;
        }

        if (number >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return (long) System.Math.Round(number, MidpointRounding.ToEven);
    }
}
#endif

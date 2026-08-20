#if NET8_0_OR_GREATER
using System.Text;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using SystemEncoding = System.Text.Encoding;

namespace Jint.WebApi.Encoding;

/// <summary>
/// <c>TextEncoder.prototype</c> — the interface prototype object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textencoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>encoding</c> is the <c>TextEncoderCommon</c> attribute and answers <c>"utf-8"</c> for every
/// instance, since that is the only encoding <c>TextEncoder</c> has ever supported. It is an accessor
/// here rather than an own property of the instance, as WebIDL specifies attributes, and brand-checks its
/// receiver — including <c>TextEncoder.prototype</c> itself, which is not a <c>TextEncoder</c>.
/// </para>
/// <para>
/// One documented simplification against WebIDL, the same one <c>console</c> makes: <c>encode</c> and
/// <c>encodeInto</c> are non-enumerable, where https://webidl.spec.whatwg.org/#es-operations gives an
/// interface prototype object's operations <c>{ writable: true, enumerable: true, configurable: true }</c>.
/// The attributes themselves are enumerable, as WebIDL asks.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TextEncoderPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TextEncoderConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TextEncoderToStringTag = new("TextEncoder");

    private static readonly JsString _utf8 = new(EncodingLabels.Utf8);

    /// <summary>
    /// <c>TextEncoderEncodeIntoResult</c>, https://encoding.spec.whatwg.org/#dictdef-textencoderencodeintoresult —
    /// a WebIDL dictionary, so an ordinary object with two data properties in declaration order. Declaring
    /// it as a layout means every result object in an engine shares one hidden class, so a loop reading
    /// <c>.written</c> off them keeps a monomorphic inline cache.
    /// </summary>
    private static readonly JsObjectLayout _encodeIntoResultLayout = JsObjectLayout.CreateBuilder()
        .Add("read")
        .Add("written")
        .Build();

    internal TextEncoderPrototype(
        Engine engine,
        Realm realm,
        TextEncoderConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textencoder-encoding
    /// </summary>
    [JsAccessor("encoding", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString EncodingGet(JsValue thisObject)
    {
        Brand(thisObject);
        return _utf8;
    }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textencoder-encode
    /// </summary>
    /// <remarks>
    /// The argument is a <c>USVString</c>, so an unpaired surrogate is replaced by U+FFFD rather than
    /// encoded — which is exactly what .NET's UTF-8 encoder does with its replacement fallback, so the
    /// conversion and the encoding happen in one pass.
    /// </remarks>
    [JsFunction(Name = "encode", Length = 0)]
    private JsTypedArray Encode(JsValue thisObject, JsValue input)
    {
        Brand(thisObject);

        // `optional USVString input = ""`: an omitted argument and an explicitly passed undefined both
        // take the default.
        var text = input.IsUndefined() ? string.Empty : TypeConverter.ToString(input);
        return _realm.Intrinsics.Uint8Array.Construct(SystemEncoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textencoder-encodeinto
    /// </summary>
    /// <remarks>
    /// The loop stops as soon as the next scalar value's UTF-8 sequence would not fit, so a code point is
    /// never split across the end of the destination. <c>read</c> counts UTF-16 code units consumed, which
    /// is why a surrogate pair advances it by two while the U+FFFD an unpaired surrogate becomes advances
    /// it by one.
    /// </remarks>
    [JsFunction(Name = "encodeInto", Length = 2)]
    private JsObject EncodeInto(JsValue thisObject, JsValue source, JsValue destination)
    {
        Brand(thisObject);

        var text = TypeConverter.ToString(source);

        // `[AllowShared] Uint8Array destination` — a Uint8ClampedArray or any other view is a WebIDL
        // conversion failure, not a silently accepted destination.
        if (destination is not JsTypedArray { _arrayElementType: TypedArrayElementType.Uint8 } target)
        {
            Throw.TypeError(_realm, "TextEncoder.encodeInto: destination must be a Uint8Array");
            return null!;
        }

        var buffer = target._viewedArrayBuffer;
        buffer.AssertNotImmutable();

        var available = Writable(buffer.ArrayBufferData, target._byteOffset, (int) target.Length);

        var read = 0;
        var written = 0;
        while (read < text.Length)
        {
            // Rune.DecodeFromUtf16 is the specification's "convert code unit to scalar value" exactly:
            // an unpaired surrogate, leading or trailing, decodes to U+FFFD and consumes one code unit.
            Rune.DecodeFromUtf16(text.AsSpan(read), out var rune, out var charsConsumed);

            var byteCount = rune.Utf8SequenceLength;
            if (available.Length - written < byteCount)
            {
                break;
            }

            rune.EncodeToUtf8(available.Slice(written));
            written += byteCount;
            read += charsConsumed;
        }

        return JsObject.Create(_engine, _encodeIntoResultLayout, [JsNumber.Create(read), JsNumber.Create(written)]);
    }

    /// <summary>
    /// The destination's own window onto its buffer. A detached buffer, and a view left outside a resized
    /// one, both yield an empty span — which stops the loop before its first write, so the result is
    /// <c>{ read: 0, written: 0 }</c> rather than an exception.
    /// </summary>
    private static Span<byte> Writable(byte[]? data, int byteOffset, int length)
    {
        if (data is null || (uint) byteOffset >= (uint) data.Length || length <= 0)
        {
            return default;
        }

        return data.AsSpan(byteOffset, Math.Min(length, data.Length - byteOffset));
    }

    /// <summary>
    /// The WebIDL brand check every operation and attribute performs before it converts its arguments: a
    /// receiver that is not a platform object implementing the interface raises a <c>TypeError</c>.
    /// </summary>
    private void Brand(JsValue thisObject)
    {
        if (thisObject is not JsTextEncoder)
        {
            Throw.TypeError(_realm, "Illegal invocation: receiver is not a TextEncoder");
        }
    }
}
#endif

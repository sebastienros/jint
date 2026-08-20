#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Encoding;

/// <summary>
/// <c>TextDecoder.prototype</c> — the interface prototype object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoder
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>encoding</c>, <c>fatal</c> and <c>ignoreBOM</c> are the <c>TextDecoderCommon</c> attributes. They
/// are accessors here rather than own properties of the instance, as WebIDL specifies attributes, and each
/// brand-checks its receiver — including <c>TextDecoder.prototype</c> itself, which is not a
/// <c>TextDecoder</c>.
/// </para>
/// <para>
/// One documented simplification against WebIDL, the same one <c>console</c> makes: <c>decode</c> is
/// non-enumerable, where https://webidl.spec.whatwg.org/#es-operations gives an interface prototype
/// object's operations <c>{ writable: true, enumerable: true, configurable: true }</c>. The attributes
/// themselves are enumerable, as WebIDL asks.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TextDecoderPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TextDecoderConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TextDecoderToStringTag = new("TextDecoder");

    private static readonly JsString _stream = new("stream");

    internal TextDecoderPrototype(
        Engine engine,
        Realm realm,
        TextDecoderConstructor constructor,
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
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-encoding
    /// </summary>
    [JsAccessor("encoding", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString EncodingGet(JsValue thisObject)
    {
        return Brand(thisObject).Name;
    }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-fatal
    /// </summary>
    [JsAccessor("fatal", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean FatalGet(JsValue thisObject)
    {
        return Brand(thisObject).Fatal ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-ignorebom
    /// </summary>
    [JsAccessor("ignoreBOM", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean IgnoreBomGet(JsValue thisObject)
    {
        return Brand(thisObject).IgnoreBom ? JsBoolean.True : JsBoolean.False;
    }

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-decode
    /// </summary>
    /// <remarks>
    /// <c>input</c> is an <c>AllowSharedBufferSource</c>: an <c>ArrayBuffer</c>, a <c>SharedArrayBuffer</c>
    /// or any view over one. Omitting it decodes nothing, which is how a stream is flushed
    /// (<c>decode()</c> with no argument ends the stream and emits whatever a trailing incomplete sequence
    /// became). Passing <c>null</c> is not the same as omitting it — the IDL type is not nullable — so it
    /// is a <c>TypeError</c>.
    /// </remarks>
    [JsFunction(Name = "decode", Length = 0)]
    private JsString Decode(JsValue thisObject, JsValue input, JsValue options)
    {
        var decoder = Brand(thisObject);

        var bytes = ReadOnlySpan<byte>.Empty;
        if (!input.IsUndefined() && !BufferSource.TryGetBytes(input, out bytes))
        {
            Throw.TypeError(_realm, "TextDecoder.decode: input must be an ArrayBuffer or a view over one");
        }

        // The TextDecodeOptions dictionary, read the same way TextDecoderOptions is in the constructor.
        var stream = false;
        if (!options.IsUndefined() && !options.IsNull())
        {
            if (options is not ObjectInstance optionsObject)
            {
                Throw.TypeError(_realm, "TextDecoder.decode: options must be an object");
                return null!;
            }

            stream = TypeConverter.ToBoolean(optionsObject.Get(_stream));
        }

        return decoder.Decode(_realm, bytes, stream);
    }

    /// <summary>
    /// The WebIDL brand check every operation and attribute performs before it converts its arguments: a
    /// receiver that is not a platform object implementing the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsTextDecoder Brand(JsValue thisObject)
    {
        if (thisObject is JsTextDecoder decoder)
        {
            return decoder;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TextDecoder");
        return null!;
    }
}
#endif

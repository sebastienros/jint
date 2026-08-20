#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Encoding;

/// <summary>
/// <c>TextEncoderStream.prototype</c> — the interface prototype object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textencoderstream
/// </para>
/// </summary>
/// <remarks>
/// Three attributes and no operations: <c>encoding</c> from <c>TextEncoderCommon</c>, which answers
/// <c>"utf-8"</c> for every instance, and <c>readable</c> and <c>writable</c> from
/// <c>GenericTransformStream</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TextEncoderStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TextEncoderStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TextEncoderStreamToStringTag = new("TextEncoderStream");

    private static readonly JsString _utf8 = new(EncodingLabels.Utf8);

    internal TextEncoderStreamPrototype(
        Engine engine,
        Realm realm,
        TextEncoderStreamConstructor constructor,
        ObjectInstance objectPrototype) : base(engine, realm)
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
    /// https://streams.spec.whatwg.org/#dom-generictransformstream-readable
    /// </summary>
    [JsAccessor("readable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsReadableStream ReadableGet(JsValue thisObject) => Brand(thisObject).Transform.Readable;

    /// <summary>
    /// https://streams.spec.whatwg.org/#dom-generictransformstream-writable
    /// </summary>
    [JsAccessor("writable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsWritableStream WritableGet(JsValue thisObject) => Brand(thisObject).Transform.Writable;

    /// <summary>
    /// The WebIDL brand check every attribute performs: a receiver that is not a platform object
    /// implementing the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsTextEncoderStream Brand(JsValue thisObject)
    {
        if (thisObject is JsTextEncoderStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TextEncoderStream");
        return null!;
    }
}
#endif

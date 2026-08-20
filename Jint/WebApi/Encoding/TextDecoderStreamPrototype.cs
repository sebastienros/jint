#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Encoding;

/// <summary>
/// <c>TextDecoderStream.prototype</c> — the interface prototype object.
/// <para>
/// https://encoding.spec.whatwg.org/#interface-textdecoderstream
/// </para>
/// </summary>
/// <remarks>
/// Five attributes and no operations: <c>encoding</c>, <c>fatal</c> and <c>ignoreBOM</c> from
/// <c>TextDecoderCommon</c>, and <c>readable</c> and <c>writable</c> from <c>GenericTransformStream</c>.
/// Each brand-checks its receiver, so <c>TextDecoderStream.prototype</c> itself — which is not a
/// <c>TextDecoderStream</c> — and a <c>TextDecoder</c>, which includes the same mixin but is a different
/// interface, both raise a <c>TypeError</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TextDecoderStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TextDecoderStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TextDecoderStreamToStringTag = new("TextDecoderStream");

    internal TextDecoderStreamPrototype(
        Engine engine,
        Realm realm,
        TextDecoderStreamConstructor constructor,
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
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-encoding
    /// </summary>
    [JsAccessor("encoding", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString EncodingGet(JsValue thisObject) => Brand(thisObject).Common.Name;

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-fatal
    /// </summary>
    [JsAccessor("fatal", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean FatalGet(JsValue thisObject) => Brand(thisObject).Common.Fatal ? JsBoolean.True : JsBoolean.False;

    /// <summary>
    /// https://encoding.spec.whatwg.org/#dom-textdecoder-ignorebom
    /// </summary>
    [JsAccessor("ignoreBOM", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean IgnoreBomGet(JsValue thisObject) => Brand(thisObject).Common.IgnoreBom ? JsBoolean.True : JsBoolean.False;

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
    private JsTextDecoderStream Brand(JsValue thisObject)
    {
        if (thisObject is JsTextDecoderStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TextDecoderStream");
        return null!;
    }
}
#endif

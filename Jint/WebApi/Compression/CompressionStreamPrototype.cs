#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Compression;

/// <summary>
/// <c>CompressionStream.prototype</c> — the interface prototype object.
/// <para>
/// https://compression.spec.whatwg.org/#compression-stream
/// </para>
/// </summary>
/// <remarks>
/// The interface is nothing but the <c>GenericTransformStream</c> mixin, so the prototype carries its two
/// attributes and no operations at all. There is deliberately no <c>format</c> attribute: the standard
/// keeps the format as an internal value, and a browser exposes none either.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class CompressionStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly CompressionStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString CompressionStreamToStringTag = new("CompressionStream");

    internal CompressionStreamPrototype(
        Engine engine,
        Realm realm,
        CompressionStreamConstructor constructor,
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
    /// implementing the interface raises a <c>TypeError</c> — a <c>DecompressionStream</c> included, since
    /// the two share the mixin but are different interfaces.
    /// </summary>
    private JsCompressionStream Brand(JsValue thisObject)
    {
        if (thisObject is JsCompressionStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a CompressionStream");
        return null!;
    }
}
#endif

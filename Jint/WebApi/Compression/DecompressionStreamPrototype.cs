#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Streams;

namespace Jint.WebApi.Compression;

/// <summary>
/// <c>DecompressionStream.prototype</c> — the interface prototype object.
/// <para>
/// https://compression.spec.whatwg.org/#decompression-stream
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class DecompressionStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly DecompressionStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString DecompressionStreamToStringTag = new("DecompressionStream");

    internal DecompressionStreamPrototype(
        Engine engine,
        Realm realm,
        DecompressionStreamConstructor constructor,
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
    /// implementing the interface raises a <c>TypeError</c> — a <c>CompressionStream</c> included, since
    /// the two share the mixin but are different interfaces.
    /// </summary>
    private JsDecompressionStream Brand(JsValue thisObject)
    {
        if (thisObject is JsDecompressionStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a DecompressionStream");
        return null!;
    }
}
#endif

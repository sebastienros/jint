#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Streams;

/// <summary>
/// <c>TransformStream.prototype</c> — the interface prototype object.
/// <para>
/// https://streams.spec.whatwg.org/#ts-prototype
/// </para>
/// </summary>
[JsObject(UseShape = true)]
internal sealed partial class TransformStreamPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TransformStreamConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TransformStreamToStringTag = new("TransformStream");

    internal TransformStreamPrototype(
        Engine engine,
        Realm realm,
        TransformStreamConstructor constructor,
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
    /// https://streams.spec.whatwg.org/#ts-readable
    /// </summary>
    [JsAccessor("readable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsReadableStream ReadableGet(JsValue thisObject) => Brand(thisObject).Readable;

    /// <summary>
    /// https://streams.spec.whatwg.org/#ts-writable
    /// </summary>
    [JsAccessor("writable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsWritableStream WritableGet(JsValue thisObject) => Brand(thisObject).Writable;

    private JsTransformStream Brand(JsValue thisObject)
    {
        if (thisObject is JsTransformStream stream)
        {
            return stream;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TransformStream");
        return null!;
    }
}
#endif

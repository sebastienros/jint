#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Xhr;

/// <summary>
/// <c>XMLHttpRequestUpload.prototype</c> — the interface prototype object.
/// <para>
/// https://xhr.spec.whatwg.org/#xmlhttprequestupload
/// </para>
/// </summary>
/// <remarks>
/// <c>interface XMLHttpRequestUpload : XMLHttpRequestEventTarget { };</c> — the body really is empty, so this
/// prototype carries only <c>constructor</c> and <c>@@toStringTag</c> and inherits the seven handler
/// attributes from <c>XMLHttpRequestEventTarget.prototype</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class XmlHttpRequestUploadPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly XmlHttpRequestUploadConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString XmlHttpRequestUploadToStringTag = new("XMLHttpRequestUpload");

    internal XmlHttpRequestUploadPrototype(
        Engine engine,
        Realm realm,
        XmlHttpRequestUploadConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }
}
#endif

#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Xhr;

/// <summary>
/// The <c>XMLHttpRequestUpload</c> interface object.
/// <para>
/// https://xhr.spec.whatwg.org/#xmlhttprequestupload
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is the <c>XMLHttpRequestEventTarget</c> interface object, and the IDL declares no
/// constructor operation, so <c>new XMLHttpRequestUpload()</c> is a <c>TypeError</c>. It is exposed even
/// though only the engine creates one: <c>xhr.upload instanceof XMLHttpRequestUpload</c> is how a script tells
/// the two event targets apart, and one vendored web-platform-test does exactly that.
/// </remarks>
internal sealed class XmlHttpRequestUploadConstructor : Constructor
{
    private static readonly JsString _functionName = new("XMLHttpRequestUpload");

    internal XmlHttpRequestUploadConstructor(
        Engine engine,
        Realm realm,
        XmlHttpRequestEventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new XmlHttpRequestUploadPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal XmlHttpRequestUploadPrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif

#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Xhr;

/// <summary>
/// The <c>XMLHttpRequestEventTarget</c> interface object.
/// <para>
/// https://xhr.spec.whatwg.org/#xmlhttprequesteventtarget
/// </para>
/// </summary>
/// <remarks>
/// <c>XMLHttpRequestEventTarget</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the
/// <c>EventTarget</c> interface object and <c>Object.getPrototypeOf(XMLHttpRequestEventTarget) ===
/// EventTarget</c> holds. The IDL declares no constructor operation, so the object is a function that refuses
/// to construct — https://webidl.spec.whatwg.org/#es-interface-call.
/// </remarks>
internal sealed class XmlHttpRequestEventTargetConstructor : Constructor
{
    private static readonly JsString _functionName = new("XMLHttpRequestEventTarget");

    internal XmlHttpRequestEventTargetConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new XmlHttpRequestEventTargetPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal XmlHttpRequestEventTargetPrototype PrototypeObject { get; }

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

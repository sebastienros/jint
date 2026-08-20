#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Messaging;

/// <summary>
/// The <c>MessagePort</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#messageport
/// </para>
/// </summary>
/// <remarks>
/// <c>MessagePort</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the <c>EventTarget</c>
/// interface object. It declares no constructor operation, which in WebIDL means the interface object exists
/// and is a function but refuses to construct anything —
/// https://webidl.spec.whatwg.org/#es-interface-call — so a port can only come from a <c>MessageChannel</c> or
/// from the host, through <c>Engine.Advanced.CreateMessagePortPair</c>.
/// </remarks>
internal sealed class MessagePortConstructor : Constructor
{
    private static readonly JsString _functionName = new("MessagePort");

    internal MessagePortConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new MessagePortPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal MessagePortPrototype PrototypeObject { get; }

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

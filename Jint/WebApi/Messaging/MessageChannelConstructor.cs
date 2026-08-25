#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Messaging;

/// <summary>
/// The <c>MessageChannel</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#messagechannel
/// </para>
/// </summary>
internal sealed class MessageChannelConstructor : Constructor
{
    private static readonly JsString _functionName = new("MessageChannel");

    internal MessageChannelConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new MessageChannelPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal MessageChannelPrototype PrototypeObject { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messagechannel: create two
    /// <c>MessagePort</c> objects and entangle them.
    /// </summary>
    /// <remarks>
    /// Both ports belong to this engine, so the entangled pair is an ordinary in-engine channel: a message
    /// posted on one is serialized immediately and delivered to the other as an event-loop task. The
    /// cross-engine form of exactly the same pair is <c>Engine.WebApi.CreateMessagePortPair</c>.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var (port1, port2) = MessagePortBridge.CreatePair(_engine, _realm, _engine, _realm);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.MessageChannel.PrototypeObject,
            static (Engine engine, Realm realm, (JsMessagePort First, JsMessagePort Second) ports)
                => new JsMessageChannel(engine, realm, ports.First, ports.Second),
            (First: port1, Second: port2));
    }
}

/// <summary>
/// A <c>MessageChannel</c> instance: nothing but the two entangled ports it was created with.
/// </summary>
internal sealed class JsMessageChannel : ObjectInstance
{
    internal JsMessageChannel(Engine engine, Realm realm, JsMessagePort port1, JsMessagePort port2)
        : base(engine, ObjectClass.Object)
    {
        Realm = realm;
        Port1 = port1;
        Port2 = port2;
    }

    internal Realm Realm { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messagechannel-port1.</summary>
    internal JsMessagePort Port1 { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messagechannel-port2.</summary>
    internal JsMessagePort Port2 { get; }
}
#endif

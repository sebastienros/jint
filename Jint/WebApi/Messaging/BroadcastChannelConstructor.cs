#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Messaging;

/// <summary>
/// The <c>BroadcastChannel</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#broadcastchannel
/// </para>
/// </summary>
/// <remarks>
/// <c>BroadcastChannel</c> inherits from <c>EventTarget</c>, so its <c>[[Prototype]]</c> is the
/// <c>EventTarget</c> interface object and <c>channel instanceof EventTarget</c> holds. Unlike
/// <c>MessagePort</c> it declares a constructor operation, taking the channel name — the one argument, and a
/// required one.
/// </remarks>
internal sealed class BroadcastChannelConstructor : Constructor
{
    private static readonly JsString _functionName = new("BroadcastChannel");

    internal BroadcastChannelConstructor(Engine engine, Realm realm, EventTargetConstructor eventTargetConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventTargetConstructor;
        PrototypeObject = new BroadcastChannelPrototype(engine, realm, this, eventTargetConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal BroadcastChannelPrototype PrototypeObject { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-broadcastchannel — "Set
    /// this's channel name to name."
    /// </summary>
    /// <remarks>
    /// The name is a <c>DOMString</c> with no default, so a bare <c>new BroadcastChannel()</c> is WebIDL's
    /// arity <c>TypeError</c> rather than a channel named <c>"undefined"</c> — while
    /// <c>new BroadcastChannel(undefined)</c> is a channel named <c>"undefined"</c>, which is what converting a
    /// present argument to a <c>DOMString</c> gives.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var state = _engine._webApi;
        if (state is null)
        {
            // Unreachable: the global that reaches this is installed only where the state was created, in the
            // same block of WebApiRegistration.
            Throw.InvalidOperationException("The BroadcastChannel global was reached on an engine that has no web API state.");
        }

        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to construct 'BroadcastChannel': 1 argument required, but only 0 present.");
        }

        var name = TypeConverter.ToString(arguments[0]);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.BroadcastChannel.PrototypeObject,
            static (Engine engine, Realm realm, (string Name, BroadcastChannelBroker Broker) created)
                => new JsBroadcastChannel(engine, realm, created.Name, created.Broker),
            (Name: name, Broker: state.BroadcastChannels));
    }
}
#endif

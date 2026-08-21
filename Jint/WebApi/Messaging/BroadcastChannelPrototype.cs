#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Messaging;

/// <summary>
/// <c>BroadcastChannel.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#broadcastchannel
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so a channel has <c>addEventListener</c> and the
/// rest, and <c>channel instanceof EventTarget</c> holds.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class BroadcastChannelPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly BroadcastChannelConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString BroadcastChannelToStringTag = new("BroadcastChannel");

    internal BroadcastChannelPrototype(
        Engine engine,
        Realm realm,
        BroadcastChannelConstructor constructor,
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

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-name — the channel name,
    /// which is fixed at construction and has no setter.
    /// </summary>
    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject) => JsString.Create(Brand(thisObject).Name);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-postmessage
    /// </summary>
    /// <remarks>
    /// One argument, and no <c>transfer</c> overload: the specification calls StructuredSerialize rather than
    /// StructuredSerializeWithTransfer, so there is no second parameter to resolve — see
    /// <see cref="JsBroadcastChannel"/>.
    /// </remarks>
    [JsFunction(Name = "postMessage", Length = 1)]
    private JsValue PostMessage(JsValue thisObject, JsCallArguments arguments)
    {
        var channel = Brand(thisObject);

        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to execute 'postMessage' on 'BroadcastChannel': 1 argument required, but only 0 present.");
        }

        channel.PostMessage(arguments[0]);
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-close
    /// </summary>
    [JsFunction(Name = "close", Length = 0)]
    private JsValue Close(JsValue thisObject)
    {
        Brand(thisObject).Close();
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-broadcastchannel-onmessage — the
    /// <c>onmessage</c> event handler IDL attribute,
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes.
    /// </summary>
    /// <remarks>
    /// Unlike <c>MessagePort</c>'s, assigning this starts nothing: a <c>BroadcastChannel</c> has no port
    /// message queue to enable, so <c>addEventListener('message', …)</c> on its own is enough to receive. The
    /// handler is one entry of the channel's own event listener list, so it takes its turn in registration
    /// order among those listeners rather than running before or after all of them.
    /// </remarks>
    [JsAccessor("onmessage", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageGet(JsValue thisObject)
    {
        return Brand(thisObject).FindEventHandler(JsBroadcastChannel.MessageEventType)?.Callback ?? Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-broadcastchannel-onmessage, setter
    /// half.
    /// </summary>
    [JsAccessor("onmessage", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageSet(JsValue thisObject, JsValue value)
    {
        SetEventHandler(Brand(thisObject), JsBroadcastChannel.MessageEventType, value);
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-broadcastchannel-onmessageerror.
    /// </summary>
    /// <remarks>
    /// Nothing in Jint ever fires a <c>messageerror</c>; see <see cref="JsBroadcastChannel"/> for why.
    /// </remarks>
    [JsAccessor("onmessageerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageErrorGet(JsValue thisObject)
    {
        return Brand(thisObject).FindEventHandler(JsBroadcastChannel.MessageErrorEventType)?.Callback ?? Null;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#handler-broadcastchannel-onmessageerror,
    /// setter half.
    /// </summary>
    [JsAccessor("onmessageerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageErrorSet(JsValue thisObject, JsValue value)
    {
        SetEventHandler(Brand(thisObject), JsBroadcastChannel.MessageErrorEventType, value);
        return Undefined;
    }

    /// <summary>
    /// HTML's "set the current value of the event handler". <c>EventHandler</c> is a nullable callback
    /// function annotated <c>[LegacyTreatNonObjectAsNull]</c>, so assigning anything that is not an object
    /// clears the handler rather than raising a <c>TypeError</c>. Reassigning replaces the value in place, so
    /// the listener keeps the position it was first given.
    /// </summary>
    private static void SetEventHandler(JsBroadcastChannel channel, string type, JsValue value)
    {
        var existing = channel.FindEventHandler(type);

        if (value is not ObjectInstance)
        {
            // "Deactivate an event handler": the listener goes away entirely.
            if (existing is not null)
            {
                channel.RemoveListener(existing);
            }

            return;
        }

        if (existing is not null)
        {
            existing.Callback = value;
            return;
        }

        channel.AddListener(new EventListenerRegistration(type, value) { IsEventHandler = true });
    }

    private JsBroadcastChannel Brand(JsValue thisObject)
    {
        if (thisObject is JsBroadcastChannel channel)
        {
            return channel;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a BroadcastChannel");
        return null!;
    }
}
#endif

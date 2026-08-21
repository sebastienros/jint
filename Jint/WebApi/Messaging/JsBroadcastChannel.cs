#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Events;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Messaging;

/// <summary>
/// A <c>BroadcastChannel</c> instance: a name, a subscription to the engine's
/// <see cref="BroadcastChannelBroker"/>, and an <see cref="JsEventTarget"/> so a script can listen for the
/// <c>message</c> event.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#broadcasting-to-other-browsing-contexts
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a message port without the port.</b> Everything about the message itself is what
/// <see cref="JsMessagePort"/> does — serialization on the sender, deserialization on the receiver, the record
/// in between belonging to neither, delivery as an event-loop task — and the differences are all about
/// <i>who</i> a message reaches. There is no entanglement and no pair: a channel names a string, and every
/// other channel of that name in the same <see cref="BroadcastChannelBroker"/> hears it. The sender does not
/// hear itself, and neither does any other channel with a different name.
/// </para>
/// <para>
/// <b>There is no message queue and no <c>start()</c>.</b> A <c>MessagePort</c> holds messages until its queue
/// is enabled; a <c>BroadcastChannel</c> has no such flag at all, so <c>addEventListener('message', …)</c>
/// alone is enough to receive and a message that arrives with no listener is simply dispatched to nobody.
/// Delivery is still a task rather than a synchronous call — the specification queues one global task per
/// destination — so a same-engine broadcast is observed only after the current script has finished and the
/// event loop has been pumped.
/// </para>
/// <para>
/// <b>One job per destination, not two.</b> A <see cref="JsMessagePort"/> spends two event-loop jobs on a
/// message because its port message queue can be disabled and the head has to be taken off separately; a
/// channel has no queue, so its delivery is the single job step 8 describes — which is also what
/// <c>EventSource</c> and <c>WebSocket</c> do for the <c>MessageEvent</c>s they fire. Jint's event loop is one
/// queue rather than a task queue plus a microtask queue, so the observable consequence is that a job queued
/// <i>between</i> the <c>postMessage</c> and the delivery — a promise reaction, say — runs after the message
/// where a port's would have run before it. Everything queued before the post still runs before the message.
/// </para>
/// <para>
/// <b>There is no transfer list.</b> <c>postMessage(message)</c> takes one argument: the specification calls
/// StructuredSerialize, not StructuredSerializeWithTransfer, and there is nowhere for a transfer to move a
/// buffer <i>to</i> when the message has many destinations. A <c>DataCloneError</c> for an uncloneable value is
/// therefore still raised synchronously at the call, and no <c>ArrayBuffer</c> is ever detached by one.
/// </para>
/// <para>
/// <b>One record, several destinations.</b> The message is serialized once — step 2, before the destinations
/// are even collected — and each destination deserializes that one record in its own realm, which is exactly
/// what steps 2 and 8.3 say. That makes the record the one thing in the engine deserialized more than once, so
/// the deserializer is asked to copy the storage it would otherwise adopt; without it, two receivers would come
/// away with two <c>ArrayBuffer</c>s over one <c>byte[]</c> and each could see the other's writes. See
/// <see cref="StructuredDeserializer"/>'s <c>sharedRecord</c> parameter.
/// </para>
/// <para>
/// <b><c>messageerror</c> is never fired.</b> The event and the <c>onmessageerror</c> attribute exist because
/// the interface has them and a script may register for them, but the only thing that fires one is step 8.3's
/// deserialization failing, and a record built by this engine's own serializer always deserializes. An
/// execution-constraint failure during deserialization is not a candidate: a timeout or a cancellation must
/// erupt from the pump, never be flattened into an event. This is the same answer <see cref="JsMessagePort"/>
/// gives, for the same reason.
/// </para>
/// </remarks>
internal sealed class JsBroadcastChannel : JsEventTarget
{
    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-message.</summary>
    internal const string MessageEventType = "message";

    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-messageerror.</summary>
    internal const string MessageErrorEventType = "messageerror";

    private static readonly JsString _messageEventName = new(MessageEventType);

    private readonly BroadcastChannelBroker _broker;

    internal JsBroadcastChannel(Engine engine, Realm realm, string name, BroadcastChannelBroker broker)
        : base(engine, realm)
    {
        _prototype = realm.Intrinsics.BroadcastChannel.PrototypeObject;
        _broker = broker;
        Name = name;
        Subscription = new BroadcastChannelSubscription(engine, this, name);

        // The channel is live from the moment it is constructed — there is no start() — so it joins the
        // broker here, and the engine's own list here, which is what lets a restore end every channel it
        // created without the broker having to know which engine anything came from.
        broker.Subscribe(Subscription);
        engine._webApi!.RegisterBroadcastChannel(this);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-name — the name the
    /// constructor was given, as a CLR string because the broker keys its buckets on it.
    /// </summary>
    internal string Name { get; }

    /// <summary>This channel's entry in the broker; see <see cref="BroadcastChannelSubscription"/>.</summary>
    internal BroadcastChannelSubscription Subscription { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-postmessage
    /// </summary>
    internal void PostMessage(JsValue message)
    {
        // Step 1: "If this is closed flag is true, then throw an InvalidStateError DOMException." Unlike a
        // closed MessagePort, whose postMessage merely goes nowhere, a closed BroadcastChannel refuses.
        if (Subscription.Closed)
        {
            ThrowDomException(
                DomExceptionNames.InvalidState,
                "Failed to execute 'postMessage' on 'BroadcastChannel': Channel is closed.");
        }

        // Step 2: "Let serialized be StructuredSerialize(message). Rethrow any exceptions." Once, on this
        // thread, before any destination is looked at — so a DataCloneError reaches the caller synchronously
        // whether or not anybody was listening, and a later mutation of the graph cannot reach the message.
        var record = new StructuredSerializer(_engine, _realm).Serialize(message, transferList: null);

        // Steps 5 to 8.
        _broker.Post(Subscription, in record);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-close — "Set this's
    /// closed flag to true", plus the standard's note that closing is what lets the object be collected.
    /// Calling it again does nothing.
    /// </summary>
    internal void Close()
    {
        if (Subscription.Closed)
        {
            return;
        }

        Subscription.Close();
        _broker.Unsubscribe(Subscription);
        _engine._webApi!.UnregisterBroadcastChannel(this);
    }

    /// <summary>
    /// Step 8's task: the message, deserialized into this channel's realm and dispatched as a trusted
    /// <c>MessageEvent</c>. <b>Runs on this channel's own engine's thread</b>, inside a generation-fenced
    /// event-loop job, which is what makes touching this object safe however far away the sender was.
    /// </summary>
    internal void Receive(in SerializationRecord record)
    {
        // Step 8.1: "If destination's closed flag is true, then abort these steps." The channel may have been
        // closed between the post and this job running.
        if (Subscription.Closed)
        {
            return;
        }

        // Step 8.3. The record is shared with this broadcast's other destinations, so its storage is copied
        // rather than adopted — see the class remarks.
        var data = new StructuredDeserializer(_engine, _realm, sharedRecord: true).Deserialize(in record);

        // Step 8.4. `origin` is the empty string: it is the serialization of the destination's origin, and an
        // engine has none — the same answer a port-delivered event gives.
        var messageEvent = _realm.Intrinsics.MessageEvent.CreateTrustedMessageEvent(_messageEventName, data);
        DispatchEvent(messageEvent);
    }

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }
}
#endif

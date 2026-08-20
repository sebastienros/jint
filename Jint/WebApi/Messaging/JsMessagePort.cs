#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Messaging;

/// <summary>
/// A <c>MessagePort</c> instance: one end of a channel, and an <see cref="JsEventTarget"/> so that a script
/// can listen for the <c>message</c> event.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#messageport
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Serialization happens on the sender, deserialization on the receiver.</b> That is what the specification
/// says — <c>postMessage</c> runs StructuredSerializeWithTransfer synchronously and only then queues a task
/// whose first act is StructuredDeserialize in the <i>target's</i> realm — and it is also what makes a port
/// pair able to span two engines: the <see cref="SerializationRecord"/> in between belongs to neither.
/// A message therefore reflects the state of the graph at the instant it was posted, and a
/// <c>DataCloneError</c> for an uncloneable value is raised at the <c>postMessage</c> call, synchronously, on
/// the caller.
/// </para>
/// <para>
/// <b>The port message queue is a real queue, and it starts disabled.</b> Nothing is dispatched until
/// <c>start()</c> is called or <c>onmessage</c> is assigned; messages that arrive before then wait, in order,
/// and are delivered when the queue is enabled. Two event-loop jobs are involved per message — one to put it
/// on this port's queue on the receiving engine's thread, one to take the head off and dispatch it — which is
/// what keeps the order right no matter when the queue is enabled relative to the messages in flight. It also
/// means a message is delivered as a <i>task</i>: every promise reaction already queued runs first, exactly as
/// in a browser.
/// </para>
/// <para>
/// <b>Transferring a port is not supported in this version.</b> An <c>ArrayBuffer</c> is the only transferable
/// Jint has, so naming a port in a <c>transfer</c> list is a <c>DataCloneError</c>. That subsumes the
/// specification's two port-specific transfer rules — a port may not be posted through itself, and posting a
/// port through the port it is entangled with dooms the message — with a stricter answer: the browser dooms
/// the second case silently, and Jint refuses it outright. A delivered <c>MessageEvent</c>'s <c>ports</c> is
/// therefore always the empty frozen array.
/// </para>
/// <para>
/// <b><c>messageerror</c> is never fired.</b> The event and the <c>onmessageerror</c> attribute exist because
/// the interface has them and a script may register for them, but the only thing that fires one is a
/// deserialization that fails, and a record built by this engine's own serializer always deserializes. An
/// execution-constraint failure during deserialization is not a candidate: a timeout or a cancellation must
/// erupt from the pump, never be flattened into an event.
/// </para>
/// </remarks>
internal sealed class JsMessagePort : JsEventTarget
{
    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-message.</summary>
    internal const string MessageEventType = "message";

    /// <summary>https://html.spec.whatwg.org/multipage/indices.html#event-messageerror.</summary>
    internal const string MessageErrorEventType = "messageerror";

    private static readonly JsString _messageEventName = new(MessageEventType);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#port-message-queue. Only ever touched on this
    /// port's own engine's thread — a message arriving from elsewhere joins it from inside an event-loop job.
    /// </summary>
    private readonly Queue<SerializationRecord> _queue = new();

    /// <summary>Whether the port message queue is enabled, i.e. whether <c>start()</c> has happened.</summary>
    private bool _enabled;

    /// <summary>
    /// Whether a drain job is already queued. One at a time: the job takes exactly one message and re-arms
    /// itself if more remain, so a port never occupies more than one slot of the event loop.
    /// </summary>
    private bool _drainScheduled;

    internal JsMessagePort(Engine engine, Realm realm) : base(engine, realm)
    {
        _prototype = realm.Intrinsics.MessagePort.PrototypeObject;
        Endpoint = new MessagePortEndpoint(engine, this);
    }

    /// <summary>The thread-crossing half of this port; see <see cref="MessagePortEndpoint"/>.</summary>
    internal MessagePortEndpoint Endpoint { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#message-port-post-message-steps
    /// </summary>
    /// <param name="message">The value to send.</param>
    /// <param name="transferList">The already-converted <c>transfer</c> sequence, or <see langword="null"/>.</param>
    internal void PostMessage(JsValue message, List<JsValue>? transferList)
    {
        // Step 5 comes before step 6, and the order is observable: the message is serialized — and any
        // transfer performed — even when there is nothing to deliver it to. So `port.close();
        // port.postMessage(function(){})` still throws a DataCloneError, and a buffer in the transfer list is
        // still detached, whether or not anybody was listening.
        var record = new StructuredSerializer(_engine, _realm).Serialize(message, transferList);

        // Step 6: "If targetPort is null, or if doomed is true, then return." A closed port is disentangled,
        // which is the same thing as having no target.
        var endpoint = Endpoint;
        if (endpoint.Closed || endpoint.Peer.Closed)
        {
            return;
        }

        // Step 7: add a task to the target's port message queue. Cross-engine, this is where the record
        // leaves this thread; same-engine it is an ordinary generation-stamped event-loop job.
        endpoint.Peer.Post(record);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-start — "Enable this's port
    /// message queue." Calling it again does nothing.
    /// </summary>
    internal void Start()
    {
        if (_enabled)
        {
            return;
        }

        _enabled = true;
        ScheduleDrain();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-close — "Set this's
    /// [[Detached]] to true. If this is entangled, disentangle it."
    /// </summary>
    /// <remarks>
    /// The peer is <i>not</i> closed: it stays a perfectly usable object whose <c>postMessage</c> now goes
    /// nowhere, which is what disentangling means. Anything still waiting on this port's own queue is dropped,
    /// because a detached port can never dispatch again.
    /// </remarks>
    internal void Close()
    {
        Endpoint.Close();
        _queue.Clear();
    }

    /// <summary>
    /// Step 7's task, first half: the message joins this port's queue. <b>Runs on this port's own engine's
    /// thread</b>, inside a generation-fenced event-loop job, which is what makes touching the queue safe
    /// however far away the sender was.
    /// </summary>
    internal void Receive(SerializationRecord record)
    {
        if (Endpoint.Closed)
        {
            return;
        }

        _queue.Enqueue(record);
        ScheduleDrain();
    }

    /// <summary>
    /// Arms the job that takes one message off the queue, unless the queue is disabled, empty, or already has
    /// a job armed.
    /// </summary>
    /// <remarks>
    /// The job carries the port's own generation rather than the engine's current one, so a port left over
    /// from an evaluation cycle a <c>RestoreGlobalSnapshot</c> ended can never dispatch into the restored one.
    /// </remarks>
    private void ScheduleDrain()
    {
        if (_drainScheduled || !_enabled || _queue.Count == 0)
        {
            return;
        }

        _drainScheduled = true;
        _engine.AddToEventLoop(DrainOne, Endpoint.Generation);
    }

    /// <summary>
    /// Step 7's task, second half: one message, deserialized and dispatched.
    /// </summary>
    private void DrainOne()
    {
        _drainScheduled = false;

        // Any of the three may have changed since the job was armed: close(), or a listener of the previous
        // message emptying the queue.
        if (Endpoint.Closed || !_enabled || _queue.Count == 0)
        {
            return;
        }

        var record = _queue.Dequeue();

        // The next message is armed BEFORE this one is dispatched, so a listener that throws — which erupts
        // from the pump, per JsEventTarget's contract — does not strand everything behind it.
        ScheduleDrain();

        Dispatch(record);
    }

    /// <summary>
    /// Steps 7.3 to 7.6: deserialize into this port's realm, then fire a trusted <c>message</c> event.
    /// </summary>
    private void Dispatch(SerializationRecord record)
    {
        var data = new StructuredDeserializer(_engine, _realm).Deserialize(in record);
        var messageEvent = _realm.Intrinsics.MessageEvent.CreateTrustedMessageEvent(_messageEventName, data);
        DispatchEvent(messageEvent);
    }
}
#endif

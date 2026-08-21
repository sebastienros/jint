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
/// whose first act is StructuredDeserializeWithTransfer in the <i>target's</i> realm — and it is also what
/// makes a port pair able to span two engines: the <see cref="SerializationRecord"/> in between belongs to
/// neither. A message therefore reflects the state of the graph at the instant it was posted, and a
/// <c>DataCloneError</c> for an uncloneable value is raised at the <c>postMessage</c> call, synchronously, on
/// the caller.
/// </para>
/// <para>
/// <b>The port message queue is a real queue, it starts disabled, and it belongs to the <i>side</i> rather
/// than to this object.</b> Nothing is dispatched until <c>start()</c> is called or <c>onmessage</c> is
/// assigned; messages that arrive before then wait, in order, and are delivered when the queue is enabled.
/// Two event-loop jobs are involved per message — one to notice it on the receiving engine's thread, one to
/// take the head off and dispatch it — which is what keeps the order right no matter when the queue is enabled
/// relative to the messages in flight. It also means a message is delivered as a <i>task</i>: every promise
/// reaction already queued runs first, exactly as in a browser. The queue living on
/// <see cref="MessagePortEndpoint"/> is what lets it travel when the port is transferred, which is exactly how
/// HTML writes it.
/// </para>
/// <para>
/// <b>A port is transferable.</b> Naming one in a <c>transfer</c> list detaches it: this object becomes
/// permanently inert — <c>postMessage</c> is a silent no-op, no event will ever fire on it again — and its
/// side, carrying whatever its queue still held, is re-entangled with a fresh <c>MessagePort</c> created in
/// the receiving realm and handed to the listener as <c>event.ports</c>. The peer is untouched and never
/// learns that the far end moved. The specification's two port-specific refusals are implemented as written:
/// posting a port through <i>itself</i> is a <c>DataCloneError</c> (message port post message steps, step 2),
/// and posting a port through the port it is entangled with dooms the message — the transfer still happens,
/// nothing is delivered, and the channel is lost (steps 4 and 6). Naming one port twice in a single transfer
/// list, or naming an already-detached one, is a <c>DataCloneError</c> from
/// StructuredSerializeWithTransfer's own steps 2.3 and 5.2.
/// </para>
/// <para>
/// <b><c>messageerror</c> is never fired.</b> The event and the <c>onmessageerror</c> attribute exist because
/// the interface has them and a script may register for them, but the only thing that fires one is a
/// deserialization that fails, and a record built by this engine's own serializer always deserializes into an
/// engine that has the messaging feature — which a port's own existence proves it has. An execution-constraint
/// failure during deserialization is not a candidate: a timeout or a cancellation must erupt from the pump,
/// never be flattened into an event.
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
    /// The evaluation cycle this port belongs to — see <see cref="MessagePortEndpoint"/>'s remarks for why it
    /// is captured once, here, rather than read when a message is posted.
    /// </summary>
    private readonly int _generation;

    /// <summary>
    /// The side of the channel this port speaks for, or <see langword="null"/> once it is detached — HTML's
    /// <c>[[Detached]]</c>, expressed as "this object no longer owns a side". Both of the specification's two
    /// ways of setting that slot land here: <c>close()</c>, which ends the side as it lets go of it, and a
    /// transfer, which hands the side on intact.
    /// </summary>
    private MessagePortEndpoint? _endpoint;

    /// <summary>Whether the port message queue is enabled, i.e. whether <c>start()</c> has happened.</summary>
    private bool _enabled;

    /// <summary>
    /// Whether a drain job is already queued. One at a time: the job takes exactly one message and re-arms
    /// itself if more remain, so a port never occupies more than one slot of the event loop.
    /// </summary>
    private bool _drainScheduled;

    /// <summary>
    /// The two event-loop jobs this port ever queues, built once so that a sender posting in a loop allocates
    /// one delegate for the port rather than one per message.
    /// </summary>
    /// <remarks>
    /// Assigned in the constructor rather than lazily, because <see cref="RequestDelivery"/> runs on the
    /// <i>sender's</i> thread: a <c>??=</c> there would be a cross-thread publication of a delegate this
    /// engine's thread then invokes, which is a memory-model question not worth having for the price of two
    /// allocations per port.
    /// </remarks>
    private readonly Action _arrivalJob;

    private readonly Action _drainJob;

    /// <summary>
    /// Where a message goes when the <i>engine</i> owns this port rather than a script — which today means the
    /// cross-realm transform behind a transferred stream. Set, the deserialized message is handed straight to
    /// it and no <c>message</c> event is created or dispatched at all.
    /// </summary>
    /// <remarks>
    /// The Streams Standard writes its half as "add a handler for port's <c>message</c> event"
    /// (https://streams.spec.whatwg.org/#abstract-opdef-setupcrossrealmtransformreadable), and this is that
    /// handler. It is a delegate rather than an <see cref="EventListenerRegistration"/> for two reasons that
    /// both come from the port never being reachable by script: no listener can compete with it, so the
    /// dispatch algorithm has nothing to decide, and a listener would have to be a JavaScript function object
    /// — one per transferred stream, plus a <c>MessageEvent</c> per chunk — to say exactly what one delegate
    /// says. Everything else about the port is unchanged: the queue still starts disabled, <see cref="Start"/>
    /// is still what enables it, and delivery is still one event-loop task per message.
    /// </remarks>
    internal Action<JsValue>? InternalMessageHandler { get; set; }

    internal JsMessagePort(Engine engine, Realm realm) : this(engine, realm, endpoint: null)
    {
    }

    /// <param name="engine">The engine that owns this port.</param>
    /// <param name="realm">The realm whose <c>MessagePort.prototype</c> the port gets.</param>
    /// <param name="endpoint">
    /// The side to bind to — a transferred one, from StructuredDeserializeWithTransfer — or
    /// <see langword="null"/> to create a side of this port's own, which is what <c>new MessageChannel()</c>
    /// and <c>Engine.Advanced.CreateMessagePortPair</c> do.
    /// </param>
    internal JsMessagePort(Engine engine, Realm realm, MessagePortEndpoint? endpoint) : base(engine, realm)
    {
        _prototype = realm.Intrinsics.MessagePort.PrototypeObject;
        _generation = engine.EventLoopGeneration;
        _arrivalJob = OnMessageArrived;
        _drainJob = DrainOne;

        _endpoint = endpoint ?? new MessagePortEndpoint();
        _endpoint.Bind(this);

        // So a restore or a dispose can end this port rather than leave its side reachable from an engine that
        // has moved on. Nothing here wakes the queue: a freshly bound port is disabled, and start() is what
        // drains whatever a transfer brought with it.
        engine._webApi?.RegisterMessagePort(this);
    }

    /// <summary>
    /// The channel side this port speaks for — the thread-crossing half — or <see langword="null"/> once it is
    /// detached; see <see cref="MessagePortEndpoint"/>.
    /// </summary>
    internal MessagePortEndpoint? Endpoint => _endpoint;

    /// <summary>
    /// HTML's <c>[[Detached]]</c>: whether this port has let go of its side, by <c>close()</c> or by being
    /// transferred. A detached port can never post, receive or fire anything again, and — StructuredSerialize
    /// WithTransfer step 5.2 — can never be transferred either.
    /// </summary>
    internal bool Detached => _endpoint is null;

    /// <summary>
    /// Whether this port can no longer do anything — detached, or closed. What the engine's port registry
    /// prunes on.
    /// </summary>
    internal bool IsInert => _endpoint is not { Closed: false };

    /// <summary>
    /// Whether this port can never carry anything again: it has been detached or closed, or the side at the
    /// other end has been closed and nothing it posted is still waiting on this one's queue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing in the specifications asks this question, and only the cross-realm transform does.</b> HTML
    /// disentangles a port silently — <c>postMessage</c> to a port whose peer has gone is a no-op, and the
    /// Streams Standard's <c>PackAndPostMessage</c> inherits that — so a stream piping into a channel whose
    /// far end was ended would go on reading its source forever and writing into nothing. That is the one
    /// thing Jint's transferred streams add: the write and pull algorithms consult this and end the stream
    /// instead. See <c>CrossRealmTransform</c>.
    /// </para>
    /// <para>
    /// The queue test is what makes it safe rather than merely conservative, and it is not an optimization: a
    /// sender's last act is to post <c>close</c> and <i>then</i> disentangle, so a receiver that asked only
    /// "is the far side closed" would discard the very message that closes it cleanly. Both halves are read on
    /// this port's own engine's thread, and the peer's <see cref="MessagePortEndpoint.Closed"/> is written
    /// after its <see cref="MessagePortEndpoint.Post"/> released this side's lock, so a <see langword="true"/>
    /// here means every message that will ever arrive has already arrived.
    /// </para>
    /// </remarks>
    internal bool IsChannelExhausted
    {
        get
        {
            if (_endpoint is not { Closed: false } endpoint)
            {
                return true;
            }

            return endpoint.Peer is not { Closed: false } && !endpoint.HasQueuedMessages;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#message-port-post-message-steps
    /// </summary>
    /// <param name="message">The value to send.</param>
    /// <param name="transferList">The already-converted <c>transfer</c> sequence, or <see langword="null"/>.</param>
    internal void PostMessage(JsValue message, List<JsValue>? transferList)
    {
        var endpoint = _endpoint;
        var target = endpoint?.Peer;

        // Steps 2 and 4, both decided before anything is serialized. Step 2 is a refusal — a port cannot be
        // sent through itself — and step 4 only dooms: the transfer still happens and the message is simply
        // never delivered, which is the specification's own way of saying the channel is being thrown away.
        var doomed = false;
        if (transferList is not null)
        {
            foreach (var entry in transferList)
            {
                if (ReferenceEquals(entry, this))
                {
                    StructuredSerializer.ThrowDataCloneError(_realm, "A MessagePort cannot be transferred through itself");
                }

                // "transfer contains targetPort", asked of the SIDE rather than of the object, so it is a
                // reference comparison that needs nothing from the other engine's thread.
                if (target is not null && entry is JsMessagePort candidate && ReferenceEquals(candidate.Endpoint, target))
                {
                    doomed = true;
                }
            }
        }

        // Step 5 comes before step 6, and the order is observable: the message is serialized — and any
        // transfer performed — even when there is nothing to deliver it to. So `port.close();
        // port.postMessage(function(){})` still throws a DataCloneError, and a buffer in the transfer list is
        // still detached, whether or not anybody was listening.
        var record = new StructuredSerializer(_engine, _realm).Serialize(message, transferList);

        // Step 6: "If targetPort is null, or if doomed is true, then return." A closed or detached port is
        // disentangled, which is the same thing as having no target.
        if (doomed || endpoint is null || endpoint.Closed || target is null || target.Closed)
        {
            // Whatever the message was carrying is now unreachable, so a side transferred into it would sit
            // unbound forever while its own peer went on posting. Ending it here is what "causing the
            // communication channel to be lost" means when nobody can ever pick the record up.
            StructuredSerializer.StrandTransferredPorts(in record);
            return;
        }

        // Step 7: add a task to the target's port message queue. Cross-engine, this is where the record
        // leaves this thread; same-engine it is an ordinary generation-stamped event-loop job.
        target.Post(record);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-start — "Enable this's port
    /// message queue." Calling it again does nothing, and a detached port has no queue to enable.
    /// </summary>
    internal void Start()
    {
        if (_enabled || _endpoint is null)
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
    /// because a detached port can never dispatch again. A port that has been <i>transferred</i> away owns no
    /// side any more, so closing it is a no-op rather than a way to reach into somebody else's channel — and
    /// closing twice is the same no-op, since the first call is what let go.
    /// </remarks>
    internal void Close()
    {
        var endpoint = _endpoint;
        _endpoint = null;
        _enabled = false;

        endpoint?.Close();
    }

    /// <summary>
    /// HTML's transfer steps for this object: the port is detached and its side handed to the caller, which
    /// puts it in the serialization record. Runs on this port's own engine's thread, inside the
    /// <c>postMessage</c> or <c>structuredClone</c> that is transferring it.
    /// </summary>
    internal MessagePortEndpoint DetachForTransfer()
    {
        var endpoint = _endpoint!;
        _endpoint = null;

        // Not strictly needed — every path consults _endpoint first — but it keeps a detached port from
        // looking like a started one to anything that reads the flag later.
        _enabled = false;

        endpoint.Unbind();
        return endpoint;
    }

    /// <summary>
    /// The wake <see cref="MessagePortEndpoint.Post"/> asks for. <b>Runs on the sender's thread</b>, so it
    /// does nothing but enqueue: everything that touches this port's own state happens in the job.
    /// </summary>
    internal void RequestDelivery()
    {
        // A cheap early-out for a channel whose receiver has since restored a global snapshot, so a sender
        // that keeps posting into a dead port does not grow that engine's queue. It is not the fence: the
        // authoritative check is the one every job gets at dequeue, on the engine's own thread, which is the
        // only place the comparison is free of races.
        var engine = _engine;
        if (engine.EventLoopGeneration != _generation)
        {
            return;
        }

        engine.AddToEventLoop(_arrivalJob, _generation);
    }

    /// <summary>
    /// Step 7's task, first half. <b>Runs on this port's own engine's thread</b>, inside a generation-fenced
    /// event-loop job, which is what makes touching this port's state safe however far away the sender was.
    /// </summary>
    /// <remarks>
    /// A job queued for a port whose side has since been transferred away simply finds nothing to do: the
    /// message it was announcing is still on that side's queue, and travels with it.
    /// </remarks>
    private void OnMessageArrived() => ScheduleDrain();

    /// <summary>
    /// Arms the job that takes one message off the queue, unless the port is detached, the queue is disabled
    /// or empty, or a job is already armed.
    /// </summary>
    /// <remarks>
    /// The job carries the port's own generation rather than the engine's current one, so a port left over
    /// from an evaluation cycle a <c>RestoreGlobalSnapshot</c> ended can never dispatch into the restored one.
    /// </remarks>
    private void ScheduleDrain()
    {
        if (_drainScheduled || !_enabled || _endpoint is not { Closed: false } endpoint || !endpoint.HasQueuedMessages)
        {
            return;
        }

        _drainScheduled = true;
        _engine.AddToEventLoop(_drainJob, _generation);
    }

    /// <summary>
    /// Step 7's task, second half: one message, deserialized and dispatched.
    /// </summary>
    private void DrainOne()
    {
        _drainScheduled = false;

        // Any of these may have changed since the job was armed: close(), a transfer, or a listener of the
        // previous message emptying the queue.
        if (!_enabled || _endpoint is not { } endpoint || !endpoint.TryDequeue(this, out var record))
        {
            return;
        }

        // The next message is armed BEFORE this one is dispatched, so a listener that throws — which erupts
        // from the pump, per JsEventTarget's contract — does not strand everything behind it.
        ScheduleDrain();

        Dispatch(record);
    }

    /// <summary>
    /// Steps 7.2 to 7.5: StructuredDeserializeWithTransfer into this port's realm, then a trusted
    /// <c>message</c> event carrying the clone and the ports the transfer created.
    /// </summary>
    private void Dispatch(SerializationRecord record)
    {
        var message = new StructuredDeserializer(_engine, _realm).DeserializeWithTransfer(in record);

        // An engine-owned port has no listener list to dispatch to, and nothing that could have added one.
        if (InternalMessageHandler is { } handler)
        {
            handler(message.Value);
            return;
        }

        var messageEvent = _realm.Intrinsics.MessageEvent.CreateTrustedMessageEvent(_messageEventName, message.Value, message.Ports);
        DispatchEvent(messageEvent);
    }
}
#endif

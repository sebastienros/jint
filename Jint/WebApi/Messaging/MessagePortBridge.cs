#if NET8_0_OR_GREATER
using System.Threading;
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Messaging;

/// <summary>
/// Entangles two <see cref="JsMessagePort"/>s — the pair a <c>MessageChannel</c> is made of, and the pair
/// <c>Engine.WebApi.CreateMessagePortPair</c> hands a host to bridge two engines.
/// <para>
/// https://html.spec.whatwg.org/multipage/web-messaging.html#entangle
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only place in the web APIs where two engines meet, and the meeting point is deliberately
/// tiny.</b> A message crosses as a <see cref="SerializationRecord"/>, which belongs to no engine at all: the
/// sender serializes on its own thread, the receiver deserializes on its own, and neither ever touches a
/// <c>JsValue</c> belonging to the other. Everything a sender reads from the far side is on
/// <see cref="MessagePortEndpoint"/> and is either immutable, a single <see langword="volatile"/> flag, or
/// read under that endpoint's own lock.
/// </para>
/// <para>
/// The delivery job is enqueued onto the receiving engine's event loop, which is a
/// <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/> and is the one part of an engine any thread
/// may touch — the same door <c>ModuleLoadCompletion</c> and the promise settle closures come through. The
/// job then runs on whichever thread pumps that engine, and everything from the deserialization onwards
/// happens there.
/// </para>
/// </remarks>
internal static class MessagePortBridge
{
    /// <summary>
    /// Creates two entangled ports, one owned by each engine. Pass the same engine twice for a same-engine
    /// channel, which is what <c>new MessageChannel()</c> does.
    /// </summary>
    /// <remarks>
    /// Both ports are constructed here, on the calling thread, which for a cross-engine pair means one of them
    /// is built on a thread that is not its own engine's. That is why
    /// <c>Engine.WebApi.CreateMessagePortPair</c> requires both engines to be quiescent: constructing a port
    /// materializes its realm's <c>MessagePort</c> intrinsics, which is engine mutation like any other.
    /// </remarks>
    internal static (JsMessagePort First, JsMessagePort Second) CreatePair(
        Engine firstEngine,
        Realm firstRealm,
        Engine secondEngine,
        Realm secondRealm)
    {
        var first = new JsMessagePort(firstEngine, firstRealm);
        var second = new JsMessagePort(secondEngine, secondRealm);

        MessagePortEndpoint.Entangle(first.Endpoint!, second.Endpoint!);

        return (first, second);
    }
}

/// <summary>
/// One <i>side</i> of an entangled pair: the port message queue, whom that queue currently belongs to, and the
/// side at the other end. Every member here may be read by the sending engine's thread, so every member here
/// is immutable, <see langword="volatile"/>, or guarded by this object's own lock.
/// </summary>
/// <remarks>
/// <para>
/// <b>A side outlives the <c>MessagePort</c> object bound to it, and that is what makes a port transferable.</b>
/// HTML's transfer steps move a port's <i>port message queue</i> into the data holder and re-entangle the
/// remote port with whatever object the transfer-receiving steps create; the queue is passed by reference, so a
/// message posted while the port is in transit lands in the very queue that travels. This class is that queue
/// plus its bookkeeping. A transfer <see cref="Unbind"/>s it from the port that is being detached, the
/// serialization record carries <i>this object</i>, and the receiving engine <see cref="Bind"/>s it to a fresh
/// <see cref="JsMessagePort"/> of its own realm. <see cref="Peer"/> never changes, so re-pointing one side is
/// invisible to the other — which is what makes a three-engine relay work, and what makes both ends of one
/// channel transferable at the same time.
/// </para>
/// <para>
/// <b>The discipline that makes the swap race-free.</b> Everything mutable lives behind <c>_gate</c>, and there
/// are exactly three operations:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Post</b> — any thread. Under the lock it appends to the queue <i>and</i> reads the current binding, so a
/// message can never be enqueued against a binding that has already gone. Outside the lock it asks that binding
/// (if any) to pump. That wake is a <i>hint</i>: a stale one is a no-op, because the job it queues is the
/// port's own and a port whose side has been unbound refuses it.
/// </description></item>
/// <item><description>
/// <b>Unbind</b> — only ever the bound engine's own thread, because a transfer happens inside that engine's
/// <c>postMessage</c>. So no drain can be in progress when it runs, and no message can be lost: the queue is
/// not touched at all.
/// </description></item>
/// <item><description>
/// <b>Bind</b> — the receiving engine's thread, from the <see cref="JsMessagePort"/> constructor. It needs no
/// wake of its own, and that is not an omission: a port's message queue starts <b>disabled</b>, so the new port
/// cannot deliver anything until the script calls <c>start()</c> or assigns <c>onmessage</c> — and that call
/// drains whatever the queue already holds. A message enqueued while the side was unbound is therefore
/// delivered by the very act that makes delivery possible, and one enqueued afterwards sees the new binding and
/// gets its own wake.
/// </description></item>
/// </list>
/// <para>
/// So a message in flight at the instant of a swap is neither lost nor delivered to the port that is going
/// away: it is in the one queue, and the one queue travels.
/// </para>
/// <para>
/// The binding carries the receiving engine's <see cref="Engine.EventLoopGeneration"/> as it was when the
/// <b>port</b> was created, not as it is when a message is posted, and that is deliberate. A port's listeners
/// are closures over the evaluation cycle the port was made in, so delivering into it after a
/// <c>RestoreGlobalSnapshot</c> would run that dead cycle's code against the freshly restored globals — the
/// exact cross-cycle channel the generation fence exists to forbid. Reading the receiver's <i>current</i>
/// generation from the sender's thread would have permitted precisely that, as well as being a cross-thread
/// read of a value the receiver is free to change underneath it. Capturing once, per binding, gives the
/// stronger rule and needs no cross-thread read at all: <b>a port belongs to the evaluation cycle its engine
/// was in when it was created, and a restore on that engine closes it.</b> A transfer creates a new port, so
/// the new binding belongs to the receiving engine's <i>current</i> cycle rather than to the sender's.
/// </para>
/// </remarks>
internal sealed class MessagePortEndpoint
{
    /// <summary>
    /// Guards <see cref="_queue"/>, <see cref="_boundPort"/> and the authoritative write of
    /// <see cref="_closed"/>. Never held while script runs, and never held across a call into another
    /// endpoint — <see cref="Close"/>'s cascade is deliberately driven from a work list outside the lock.
    /// </summary>
    private readonly Lock _gate = new();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#port-message-queue. It belongs to the
    /// <i>side</i> rather than to the port, which is what lets it travel through a transfer with a message
    /// still in it.
    /// </summary>
    private readonly Queue<SerializationRecord> _queue = new();

    /// <summary>
    /// The <c>MessagePort</c> object this side currently delivers to, or <see langword="null"/> while it is in
    /// transit — between the transfer that detached the old port and the deserialization that creates the new
    /// one. Also <see langword="null"/> once closed.
    /// </summary>
    private JsMessagePort? _boundPort;

    private volatile bool _closed;

    /// <summary>
    /// Whether this side has been half-closed: it takes no further message, but what its queue already holds
    /// is still deliverable, and it closes for real once that queue runs dry. <b>Only a worker's
    /// <c>close()</c> produces this state</b> — HTML's <i>close a worker</i> discards the worker's own queued
    /// tasks and pointedly does not empty the queue of the port entangled with it, which <i>terminate a
    /// worker</i> does as its fourth step. So <c>postMessage(result); close();</c> — the commonest idiom there
    /// is — must still deliver, whether or not the parent had pumped in between.
    /// </summary>
    /// <remarks>
    /// It is deliberately <b>not</b> expressed as <see cref="Closed"/>. That flag is read by
    /// <c>JsMessagePort.IsChannelExhausted</c>, which a transferred stream consults to decide that its channel
    /// can never carry anything again — and a draining side whose queue still holds the stream's own
    /// <c>close</c> message is exactly the case where that answer would be wrong, and would error a stream
    /// that was about to end cleanly. Refusing new posts is <see cref="AcceptsPosts"/>'s job instead.
    /// </remarks>
    private volatile bool _draining;

    /// <summary>
    /// The side this one is entangled with, or <see langword="null"/> for a side that was never entangled.
    /// Assigned once, by <see cref="Entangle"/>, before either port can be reached by any script, and
    /// deliberately never reassigned: a transfer moves a <i>side</i>, so the other end goes on talking to the
    /// same object it always did.
    /// </summary>
    /// <remarks>
    /// The <see langword="null"/> case exists for one path that should be unreachable — a serialization record
    /// carrying a transferred side being deserialized twice, which the "a record is consumed once" rule
    /// forbids. Rather than hand two engines one side, the second deserialization gets a lone side, and a port
    /// bound to one is inert.
    /// </remarks>
    internal MessagePortEndpoint? Peer { get; private set; }

    /// <summary>
    /// Whether <c>close()</c> has been called on the port bound to this side —
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-close. Volatile because the
    /// peer's engine reads it from its own thread to decide whether it still has anywhere to post; that is a
    /// one-way flag, so a stale read can only cost one message that was in flight as the port closed, which is
    /// exactly what closing a port means. The authoritative test is the one <see cref="Post"/> makes under the
    /// lock.
    /// </summary>
    internal bool Closed => _closed;

    /// <summary>
    /// Whether a sender may still join this side's queue. False once it is closed, and once it is draining —
    /// see <see cref="_draining"/>. Read by the peer's engine from its own thread; the authoritative test is
    /// the one <see cref="Post"/> makes under the lock.
    /// </summary>
    internal bool AcceptsPosts => !_closed && !_draining;

    /// <summary>Whether anything is waiting to be taken off this side's queue. Bound engine's thread.</summary>
    internal bool HasQueuedMessages
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count > 0;
            }
        }
    }

    /// <summary>
    /// How many messages are waiting to be taken off this side's queue. What
    /// <c>Options.WebApi.Workers.MaxQueuedMessages</c> bounds, read by the sending engine's thread before it
    /// serializes anything.
    /// </summary>
    internal int QueuedMessageCount
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    internal static void Entangle(MessagePortEndpoint first, MessagePortEndpoint second)
    {
        first.Peer = second;
        second.Peer = first;
    }

    /// <summary>
    /// Makes <paramref name="port"/> the object this side delivers to. Called from the port's own constructor,
    /// on the engine that owns it: once at creation, and again for the port a transfer's receiving steps build.
    /// </summary>
    internal void Bind(JsMessagePort port)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _boundPort = port;
        }
    }

    /// <summary>
    /// HTML's transfer steps, from this side's point of view: the port that was bound here is detached, and the
    /// side — queue and all — is left waiting for the transfer-receiving steps to bind it somewhere else.
    /// </summary>
    /// <remarks>
    /// Runs on the bound engine's own thread, inside the <c>postMessage</c> or <c>structuredClone</c> that is
    /// transferring the port, which is why no drain can be in progress and the queue needs no protection beyond
    /// the lock's fence.
    /// </remarks>
    internal void Unbind()
    {
        lock (_gate)
        {
            _boundPort = null;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-close: detach this port,
    /// which is also what disentangles the pair — the peer's <c>postMessage</c> consults
    /// <see cref="Closed"/> and finds it has no target any more.
    /// </summary>
    /// <remarks>
    /// Anything still on the queue is dropped, because a detached port can never dispatch again — and a
    /// dropped message may itself be carrying a transferred side, which would otherwise sit unbound forever
    /// while its own peer went on posting into it. Those are closed too, transitively, from a work list rather
    /// than by recursion: the chain is as deep as a script cares to nest undelivered transfers.
    /// </remarks>
    internal void Close()
    {
        // Allocated only if a discarded message actually carried a transfer, which is what makes closing an
        // ordinary port — the case a restore runs for every port an engine has — allocation-free.
        Stack<MessagePortEndpoint>? stranded = null;
        var endpoint = this;

        while (true)
        {
            List<SerializationRecord>? discarded = null;

            lock (endpoint._gate)
            {
                if (!endpoint._closed)
                {
                    endpoint._closed = true;
                    endpoint._boundPort = null;

                    if (endpoint._queue.Count > 0)
                    {
                        discarded = new List<SerializationRecord>(endpoint._queue);
                        endpoint._queue.Clear();
                    }
                }
            }

            if (discarded is not null)
            {
                foreach (var record in discarded)
                {
                    if (record.TransferredPorts is not { } holders)
                    {
                        continue;
                    }

                    foreach (var holder in holders)
                    {
                        if (holder.Endpoint is { } carried)
                        {
                            (stranded ??= new Stack<MessagePortEndpoint>()).Push(carried);
                        }
                    }
                }
            }

            if (stranded is not { Count: > 0 })
            {
                return;
            }

            endpoint = stranded.Pop();
        }
    }

    /// <summary>
    /// Hands a serialized message to this side. <b>Runs on the sender's thread</b>, so it does exactly two
    /// things: join the queue, and ask whatever engine currently owns the side to pump.
    /// </summary>
    internal void Post(SerializationRecord record)
    {
        JsMessagePort? port;

        lock (_gate)
        {
            // The authoritative closed test. The volatile read callers make first is only an early-out.
            if (_closed || _draining)
            {
                return;
            }

            _queue.Enqueue(record);
            port = _boundPort;
        }

        // A hint, and deliberately outside the lock: if the side is rebound between here and there, the job
        // this queues is refused by a port that no longer owns the side, and the message is delivered by the
        // start() that the new port's script has to call anyway. See the class remarks.
        port?.RequestDelivery();
    }

    /// <summary>
    /// Takes the head of the queue, but only for the port the side is actually bound to. Bound engine's thread,
    /// from <c>JsMessagePort.DrainOne</c>.
    /// </summary>
    /// <remarks>
    /// The <paramref name="caller"/> test is an <b>invariant guard, not a behaviour</b>, and no test can make
    /// it fire: a transfer detaches the old port before the new one binds, so a port that does not own the
    /// side has already forgotten it and never gets this far. It is here because the invariant it asserts —
    /// one queue, one draining port — is the whole reason a transfer cannot split or reorder a channel, and a
    /// future rebind path that forgot to detach first would otherwise let two ports take alternate messages
    /// off one queue in silence.
    /// </remarks>
    internal bool TryDequeue(JsMessagePort caller, out SerializationRecord record)
    {
        bool drained;

        lock (_gate)
        {
            if (_closed || !ReferenceEquals(_boundPort, caller) || _queue.Count == 0)
            {
                record = default;
                return false;
            }

            record = _queue.Dequeue();

            // A draining side that has just handed over its last message has done what it was kept open for.
            drained = _draining && _queue.Count == 0;
        }

        if (drained)
        {
            // Outside the lock: Close walks a work list of stranded sides and takes each of their gates.
            Close();
        }

        return true;
    }

    /// <summary>
    /// Half-closes this side: it accepts nothing further, what it already holds stays deliverable, and it
    /// closes for real when that queue runs dry — or when a teardown closes it outright. See
    /// <see cref="_draining"/> for why this is not simply <see cref="Close"/>.
    /// </summary>
    /// <remarks>
    /// Any thread, like <see cref="Close"/>: a worker's <c>close()</c> runs on the worker's thread and this is
    /// the <i>parent's</i> side. An empty queue is closed on the spot rather than left in a state nothing
    /// would ever leave, since only a dequeue ends it and there is nothing left to dequeue.
    /// </remarks>
    internal void BeginDrainThenClose()
    {
        bool alreadyEmpty;

        lock (_gate)
        {
            if (_closed || _draining)
            {
                return;
            }

            _draining = true;
            alreadyEmpty = _queue.Count == 0;
        }

        if (alreadyEmpty)
        {
            Close();
        }
    }
}
#endif

#if NET8_0_OR_GREATER
using Jint.Runtime;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Messaging;

/// <summary>
/// Entangles two <see cref="JsMessagePort"/>s — the pair a <c>MessageChannel</c> is made of, and the pair
/// <c>Engine.Advanced.CreateMessagePortPair</c> hands a host to bridge two engines.
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
/// <see cref="MessagePortEndpoint"/> and is either immutable or a single <see langword="volatile"/> flag.
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
    /// <c>Engine.Advanced.CreateMessagePortPair</c> requires both engines to be quiescent: constructing a port
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

        MessagePortEndpoint.Entangle(first.Endpoint, second.Endpoint);

        return (first, second);
    }
}

/// <summary>
/// One end of an entangled pair, as seen from the <i>other</i> end. Every member here may be read by the
/// sending engine's thread, so every member here is immutable or volatile.
/// </summary>
/// <remarks>
/// The endpoint carries the receiving engine's <see cref="Engine.EventLoopGeneration"/> as it was when the
/// port was <b>created</b>, not as it is when a message is posted, and that is deliberate. A port's listeners
/// are closures over the evaluation cycle the port was made in, so delivering into it after a
/// <c>RestoreGlobalSnapshot</c> would run that dead cycle's code against the freshly restored globals — the
/// exact cross-cycle channel the generation fence exists to forbid. Reading the receiver's <i>current</i>
/// generation from the sender's thread would have permitted precisely that, as well as being a cross-thread
/// read of a value the receiver is free to change underneath it. Capturing once, at creation, gives the
/// stronger rule and needs no cross-thread read at all: <b>a port pair belongs to the evaluation cycle its
/// engines were in when it was created, and a restore on either engine ends the channel permanently.</b>
/// </remarks>
internal sealed class MessagePortEndpoint
{
    private volatile bool _closed;

    internal MessagePortEndpoint(Engine engine, JsMessagePort port)
    {
        Engine = engine;
        Port = port;
        Generation = engine.EventLoopGeneration;
    }

    /// <summary>The engine that owns <see cref="Port"/> and on whose pump a delivery job runs.</summary>
    internal Engine Engine { get; }

    /// <summary>The port itself. Only ever dereferenced on <see cref="Engine"/>'s own thread.</summary>
    internal JsMessagePort Port { get; }

    /// <summary>The evaluation cycle this port belongs to; see the class remarks.</summary>
    internal int Generation { get; }

    /// <summary>
    /// The endpoint this one is entangled with. Assigned once, by <see cref="Entangle"/>, before either port
    /// can be reached by any script.
    /// </summary>
    internal MessagePortEndpoint Peer { get; private set; } = null!;

    /// <summary>
    /// Whether <c>close()</c> has been called on this port —
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-close. Volatile because the
    /// peer's engine reads it from its own thread to decide whether it still has anywhere to post; that is a
    /// one-way flag, so a stale read can only cost one message that was in flight as the port closed, which is
    /// exactly what closing a port means.
    /// </summary>
    internal bool Closed => _closed;

    internal static void Entangle(MessagePortEndpoint first, MessagePortEndpoint second)
    {
        first.Peer = second;
        second.Peer = first;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-messageport-close: detach this port,
    /// which is also what disentangles the pair — the peer's <c>postMessage</c> consults this flag and finds
    /// it has no target any more.
    /// </summary>
    internal void Close() => _closed = true;

    /// <summary>
    /// Hands a serialized message to this endpoint's engine. <b>Runs on the sender's thread</b>, so it does
    /// exactly two things: check the fences, and enqueue.
    /// </summary>
    internal void Post(SerializationRecord record)
    {
        if (_closed)
        {
            return;
        }

        var engine = Engine;

        // A cheap early-out for a channel whose receiver has since restored a global snapshot, so a sender
        // that keeps posting into a dead port does not grow that engine's queue. It is not the fence: the
        // authoritative check is the one every job gets at dequeue, on the engine's own thread, which is the
        // only place the comparison is free of races.
        if (engine.EventLoopGeneration != Generation)
        {
            return;
        }

        var port = Port;
        engine.AddToEventLoop(() => port.Receive(record), Generation);
    }
}
#endif

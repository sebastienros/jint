#if NET8_0_OR_GREATER
using System.Threading;
using Jint.WebApi.Messaging;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi;

/// <summary>
/// The set of <c>BroadcastChannel</c> objects that can hear one another — what the HTML Standard reaches
/// through "the <see href="https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-postmessage">
/// BroadcastChannel objects whose relevant global object's storage key equals sourceStorageKey and whose
/// channel name equals source's channel name</see>". Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// <para>
/// A browser scopes broadcasting by two things Jint does not have: an <i>agent cluster</i> (which browsing
/// contexts share one event loop family) and a <i>storage key</i> (an origin). This object is Jint's answer to
/// both at once — <b>one broker is one agent cluster and one origin</b>. Channels created on engines that share
/// a broker hear each other; channels on engines that do not, cannot, however equal their names.
/// </para>
/// <para>
/// <b>You only ever construct one and hand it over.</b> It has no members of its own: everything it does
/// happens through the <c>BroadcastChannel</c> objects scripts create. Assign one to
/// <see cref="Options.MessagingOptions.Broker"/> on an <see cref="Options"/> instance several engines are built
/// from — or assign the same instance to several <see cref="Options"/> instances — and those engines broadcast
/// to each other. Say nothing and each engine gets a private broker of its own, so channels on one engine still
/// hear each other and nothing crosses an engine boundary.
/// </para>
/// <para>
/// <b>Threading.</b> This type is thread-safe, which is the whole reason it is a class rather than a field on
/// the engine: a broker shared by engines running on different threads is reached from all of them. What
/// crosses between them is never a <c>JsValue</c> — a message is serialized on the sending engine's thread into
/// a record that belongs to no engine, and enqueued onto the receiving engine's event loop, the one part of an
/// engine any thread may touch. The deserialization, the <c>MessageEvent</c> and the listeners all run on
/// whichever thread pumps the receiving engine, so an engine nobody pumps never takes delivery, exactly as for
/// timers and message ports.
/// </para>
/// <para>
/// <b>It holds its subscribers strongly, and that is deliberate.</b> A subscribed channel keeps its engine
/// reachable for as long as the broker lives, exactly as a browser keeps a <c>BroadcastChannel</c> alive until
/// it is closed or its context goes away — a channel nobody references is still a live receiver, so collecting
/// it would silently drop messages. Three things release a subscription: <c>close()</c> from script,
/// <c>Engine.Advanced.RestoreGlobalSnapshot</c> (which ends every channel that engine created, permanently, the
/// same rule a <c>MessagePort</c> follows), and <see cref="Engine.Dispose"/>. A host that shares one broker
/// between long-lived engines and short-lived ones therefore disposes or restores the short-lived ones;
/// there is no finalizer-driven cleanup and none is planned.
/// </para>
/// </remarks>
public sealed class BroadcastChannelBroker
{
    /// <summary>
    /// Guards <see cref="_channels"/> and every walk of a bucket. Held across the enqueue of a delivery job so
    /// that the set of destinations a message goes to is the set that existed when it was posted.
    /// </summary>
    /// <remarks>
    /// Enqueuing under a lock is only safe because <c>EventLoop.Enqueue</c> runs no user code: it appends to a
    /// concurrent queue, sets a <see cref="System.Threading.ManualResetEventSlim"/>, and completes its async
    /// waiters through task sources created <c>RunContinuationsAsynchronously</c>, so nothing it touches can
    /// re-enter this type. The delivery job itself runs later, on the receiving engine's own pump.
    /// </remarks>
    private readonly Lock _gate = new();

    /// <summary>
    /// The subscribers of each channel name, in subscription order, or <see langword="null"/> until the first
    /// channel is created — which is what keeps a broker an engine never broadcasts on to one empty object.
    /// A name whose last subscriber leaves is removed outright, so a script cycling through channel names
    /// cannot grow this map.
    /// </summary>
    private Dictionary<string, List<BroadcastChannelSubscription>>? _channels;

    /// <summary>
    /// Creates a broker: one agent cluster's worth of <c>BroadcastChannel</c> objects, empty to begin with.
    /// </summary>
    public BroadcastChannelBroker()
    {
    }

    /// <summary>
    /// How many channel names currently have at least one subscriber.
    /// </summary>
    /// <remarks>
    /// Deliberately <see langword="internal"/> and deliberately not a host-facing figure: it exists because the
    /// leak guard below — a name whose last subscriber leaves is removed from the map outright — is invisible
    /// from script by construction, so the in-repo tests have no other way to pin it. A host has no business
    /// branching on it.
    /// </remarks>
    internal int ActiveNameCount
    {
        get
        {
            lock (_gate)
            {
                return _channels?.Count ?? 0;
            }
        }
    }

    /// <summary>
    /// Adds a channel to its name's bucket, at the end — which is the order
    /// <see href="https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-postmessage">step
    /// 7</see> asks for, "sorted in creation order of their relevant global object", reduced to creation order
    /// because a broker is one agent cluster and the objects in it are therefore already sorted by nothing else.
    /// </summary>
    internal void Subscribe(BroadcastChannelSubscription subscription)
    {
        lock (_gate)
        {
            var channels = _channels ??= new Dictionary<string, List<BroadcastChannelSubscription>>(StringComparer.Ordinal);

            if (!channels.TryGetValue(subscription.Name, out var bucket))
            {
                bucket = new List<BroadcastChannelSubscription>();
                channels[subscription.Name] = bucket;
            }

            bucket.Add(subscription);
        }
    }

    /// <summary>
    /// Removes a channel from its bucket, and the bucket itself once it is empty.
    /// </summary>
    /// <remarks>
    /// Idempotent, because everything that can end a channel calls it: <c>close()</c>, the restore that ends
    /// the evaluation cycle the channel belongs to, and <see cref="Engine.Dispose"/>. A subscription that is
    /// already gone simply is not found.
    /// </remarks>
    internal void Unsubscribe(BroadcastChannelSubscription subscription)
    {
        lock (_gate)
        {
            if (_channels is not { } channels || !channels.TryGetValue(subscription.Name, out var bucket))
            {
                return;
            }

            bucket.Remove(subscription);

            if (bucket.Count == 0)
            {
                channels.Remove(subscription.Name);
            }
        }
    }

    /// <summary>
    /// <see href="https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-postmessage">Steps
    /// 5 to 8</see>: the destinations, minus the sender, each given a task of their own.
    /// </summary>
    /// <remarks>
    /// <b>Runs on the sending engine's thread</b>, and does exactly two things per destination: check the
    /// fences, and enqueue. The record was produced before this was called, so nothing here touches a
    /// <c>JsValue</c> — see <see cref="BroadcastChannelSubscription"/> for why that is what makes a shared
    /// broker safe at all.
    /// </remarks>
    /// <param name="source">The posting channel, which step 6 removes from the destinations.</param>
    /// <param name="record">The message, already serialized on the sender.</param>
    internal void Post(BroadcastChannelSubscription source, in SerializationRecord record)
    {
        lock (_gate)
        {
            if (_channels is not { } channels || !channels.TryGetValue(source.Name, out var bucket))
            {
                return;
            }

            foreach (var destination in bucket)
            {
                // Step 6: "Remove source from destinations." A channel never hears itself, however many
                // channels of that name the same engine has.
                if (ReferenceEquals(destination, source))
                {
                    continue;
                }

                destination.Deliver(in record);
            }
        }
    }
}

/// <summary>
/// One <c>BroadcastChannel</c>'s entry in a <see cref="BroadcastChannelBroker"/>, as seen from whichever engine
/// is posting. Every member here may be read by a sending engine's thread, so every member here is immutable or
/// <see langword="volatile"/> — the same discipline <c>MessagePortEndpoint</c> keeps, and for the same reason.
/// </summary>
/// <remarks>
/// The subscription carries the receiving engine's <see cref="Engine.EventLoopGeneration"/> as it was when the
/// channel was <b>created</b>, not as it is when a message is posted. A channel's listeners are closures over
/// the evaluation cycle it was created in, so delivering into it after a <c>RestoreGlobalSnapshot</c> would run
/// that dead cycle's code against the freshly restored globals — the exact cross-cycle channel the generation
/// fence exists to forbid. Reading the receiver's <i>current</i> generation from the sender's thread would have
/// permitted precisely that, as well as being a cross-thread read of a value the receiver is free to change
/// underneath it. So: <b>a channel belongs to the evaluation cycle its engine was in when it was created, and a
/// restore on that engine ends it permanently.</b>
/// </remarks>
internal sealed class BroadcastChannelSubscription
{
    private volatile bool _closed;

    internal BroadcastChannelSubscription(Engine engine, JsBroadcastChannel channel, string name)
    {
        Engine = engine;
        Channel = channel;
        Name = name;
        Generation = engine.EventLoopGeneration;
    }

    /// <summary>The engine that owns <see cref="Channel"/> and on whose pump a delivery job runs.</summary>
    internal Engine Engine { get; }

    /// <summary>The channel itself. Only ever dereferenced on <see cref="Engine"/>'s own thread.</summary>
    internal JsBroadcastChannel Channel { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-name — the channel name,
    /// which is what a bucket is keyed on. Compared with ordinal equality, since it is an arbitrary
    /// <c>DOMString</c> and the standard's "equals" is on the string and not on any collation.
    /// </summary>
    internal string Name { get; }

    /// <summary>The evaluation cycle this channel belongs to; see the class remarks.</summary>
    internal int Generation { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/web-messaging.html#dom-broadcastchannel-close — the channel's
    /// <i>closed flag</i>. Volatile because a sending engine reads it from its own thread to decide whether
    /// this destination is still worth a job; it is a one-way flag, so a stale read can only cost one job that
    /// the receiving engine then discards at step 8.1 anyway.
    /// </summary>
    internal bool Closed => _closed;

    internal void Close() => _closed = true;

    /// <summary>
    /// Step 8: "queue a global task … given destination's relevant global object". <b>Runs on the sender's
    /// thread</b>, so it checks the fences and enqueues, and does nothing else.
    /// </summary>
    internal void Deliver(in SerializationRecord record)
    {
        if (_closed)
        {
            return;
        }

        var engine = Engine;

        // A cheap early-out for a channel whose engine has since restored a global snapshot, so a sender that
        // keeps posting into a dead channel does not grow that engine's queue. It is not the fence: the
        // authoritative check is the one every job gets at dequeue, on the engine's own thread, which is the
        // only place the comparison is free of races.
        if (engine.EventLoopGeneration != Generation)
        {
            return;
        }

        var channel = Channel;
        var message = record;
        engine.AddToEventLoop(() => channel.Receive(in message), Generation);
    }
}
#endif

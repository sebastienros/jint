#if NET8_0_OR_GREATER
using System.Diagnostics;
using Jint.Native.Promise;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Abort;
using Jint.WebApi.Fetch;
using Jint.WebApi.GlobalEvents;
using Jint.WebApi.Idle;
using Jint.WebApi.Messaging;
using Jint.WebApi;
using Jint.WebApi.Scheduling;
using Jint.WebApi.ServerSentEvents;
using Jint.WebApi.Streams;
using Jint.WebApi.Timers;
using Jint.WebApi.WebSockets;
using Jint.WebApi.Workers;

namespace Jint;

public partial class Engine
{
    /// <summary>
    /// The bridges between a WHATWG stream and a host <see cref="System.IO.Stream"/> this engine has open,
    /// or <see langword="null"/> — which is what every engine carries until the first
    /// <c>Engine.Advanced.CreateReadableStream</c> and friends.
    /// </summary>
    /// <remarks>
    /// Deliberately its own field rather than a member of <see cref="WebApiEngineState"/>, which the streams
    /// feature does not otherwise need: putting it there would mean building that state — and reading a clock
    /// — for every engine that merely enables <see cref="WebApiFeatures.Streams"/>, and adding a live
    /// null check to the pump for a feature that never schedules anything. Here, an engine that never bridges
    /// a stream pays one null field.
    /// </remarks>
    private List<HostStreamBridge>? _hostStreamBridges;

    /// <summary>
    /// Adds a live bridge, so that a <c>RestoreGlobalSnapshot</c> can close the host stream behind it rather
    /// than leave the handle to a finalizer. Engine thread, from the bridge's own creation.
    /// </summary>
    /// <remarks>
    /// The list is pruned here rather than on release, because a release can happen on whichever thread the
    /// I/O completed on and this list is engine-thread state. Pruning on every registration bounds it at the
    /// number of bridges alive at the last one, so a host that opens one stream per request does not
    /// accumulate.
    /// </remarks>
    internal void RegisterHostStreamBridge(HostStreamBridge bridge)
    {
        var bridges = _hostStreamBridges ??= new List<HostStreamBridge>();
        bridges.RemoveAll(static candidate => candidate.IsReleased);
        bridges.Add(bridge);
    }

    /// <summary>
    /// Abandons every live bridge because the evaluation cycle they belong to has ended. Engine thread, from
    /// <see cref="ResetTransientEvaluationState"/>.
    /// </summary>
    internal void AbandonHostStreamBridges()
    {
        if (_hostStreamBridges is not { Count: > 0 } bridges)
        {
            return;
        }

        // Copied before the walk, exactly as the in-flight fetches are: abandoning cancels a token source,
        // and a continuation running synchronously on this very thread must not find the list being
        // enumerated.
        var pending = bridges.ToArray();
        bridges.Clear();

        foreach (var bridge in pending)
        {
            bridge.Abandon();
        }
    }

    /// <summary>
    /// The per-engine state behind the opt-in web APIs, or <see langword="null"/> — which is what a default
    /// engine, and an engine that enabled only stateless features such as <c>console</c>, carries. Every hot
    /// path that consults it starts with this field being null, so an engine that has no timers pays one
    /// predictable null check per event-loop drain and nothing else. The pump reaches it through
    /// <c>Engine.TryPromoteDueTimerJob</c> and <c>Engine.TimeUntilNextPumpScheduledWork</c>
    /// (<c>Jint/Engine.Pump.cs</c>), which are declared on every target framework so that neither the event
    /// loop nor the wait loops need a conditional-compilation directive of their own. It is created by
    /// <c>WebApiRegistration.Apply</c> for the features that keep state in it — the timers and the events, the
    /// latter for the time origin <c>Event.timeStamp</c> is measured against and for the queue
    /// <c>AbortSignal.timeout()</c> schedules on — or, for a feature enabled through
    /// <see cref="AdvancedOperations.EnableWebApis(WebApiFeatures, Action{Options.WebApiOptions})"/>, created
    /// or extended by that call.
    /// </summary>
    internal WebApiEngineState? _webApi;

    /// <summary>
    /// Which opt-in web APIs this engine carries, as <c>WebApiRegistration</c> recorded them after computing
    /// the feature closure, or <see cref="WebApiFeatures.None"/> for an engine that asked for nothing. Read by
    /// the host APIs that have to refuse an engine which never opted in —
    /// <see cref="AdvancedOperations.CreateMessagePortPair"/>,
    /// <see cref="AdvancedOperations.SetFetchHandler"/> and
    /// <see cref="AdvancedOperations.CreateAbortSignal"/> — by
    /// <see cref="AdvancedOperations.EnableWebApis(WebApiFeatures, Action{Options.WebApiOptions})"/>, which
    /// adds to it, and by nothing on any hot path. It lives here rather than being read back from
    /// <c>Options</c> because an <c>Options</c> instance is shareable and mutable, so the set an engine
    /// actually has is only knowable from the engine.
    /// </summary>
    internal WebApiFeatures _webApiFeatures;

    /// <summary>
    /// Whether the event loop still has jobs queued behind the one running now. Only the web-API scheduler
    /// reads it, to keep a task from overtaking the microtasks of the turn it was posted in — see
    /// <see cref="Runtime.EventLoop.HasPendingJobs"/>.
    /// </summary>
    internal bool HasPendingEventLoopJobs => _eventLoop.HasPendingJobs;
}

/// <summary>
/// Mutable per-engine state for the opt-in web APIs. Created by <c>WebApiRegistration</c> when a feature that
/// needs it is enabled, and engine-affine like everything else on <see cref="Engine"/> — two engines built
/// from one shared <see cref="Options"/> instance get one of these each, so their timers and their time
/// origin are independent.
/// </summary>
internal sealed class WebApiEngineState
{
    private readonly Engine _engine;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// The monotonic reading taken when this state was created, which is the instant
    /// <see cref="TimeOrigin"/> names and the one <see cref="CurrentHighResolutionTime"/> counts from.
    /// </summary>
    private readonly long _originTimestamp;

    /// <summary>
    /// The requests this engine has in flight, in registration order. Engine-thread-only, so no lock: a
    /// request is added by <c>fetch</c> and removed by its own settle job, both of which run on the engine's
    /// thread. A list rather than a set because it is bounded by
    /// <c>Options.FetchOptions.MaxConcurrentRequests</c> and never long.
    /// </summary>
    private List<FetchOperation>? _fetches;

    /// <summary>
    /// The quota a defaulted <see cref="InMemoryStorageProvider"/> is built with, captured when the engine
    /// was — or, for an engine that enabled storage through <c>Engine.Advanced.EnableWebApis</c>, when that
    /// call ran. Either way it is read once and never again, so mutating the options afterwards cannot change
    /// an engine that already has it.
    /// </summary>
    private long _storageQuotaBytes;

    private StorageProvider? _localStorageProvider;
    private StorageProvider? _sessionStorageProvider;

    /// <summary>
    /// The event streams this engine has open, in registration order. Engine-thread-only for the same reason
    /// <see cref="_fetches"/> is, and bounded by the same <c>Options.FetchOptions.MaxConcurrentRequests</c> —
    /// separately, because a stream holds its socket for as long as it lives rather than for one exchange.
    /// </summary>
    private List<EventSourceConnection>? _eventSources;

    /// <summary>
    /// The response bodies still arriving over the wire. Engine-thread-only for the same reason
    /// <see cref="_fetches"/> is: a body is registered when its stream is built and removed when the stream
    /// closes, errors or is cancelled, all of which happen on the engine's thread. Separate from
    /// <see cref="_fetches"/> because a request stops counting against
    /// <c>Options.FetchOptions.MaxConcurrentRequests</c> when its promise settles, which is before its body
    /// has finished.
    /// </summary>
    private List<FetchBodyStream>? _fetchBodies;

    /// <summary>
    /// The sockets this engine has open, in the order they were created. Engine-thread-only for the same
    /// reason the fetch list is: a socket is added by the <c>WebSocket</c> constructor and removed by its own
    /// close job, both of which run on the engine's thread.
    /// </summary>
    private List<JsWebSocket>? _webSockets;

    private List<HostAbortSignalBridge>? _hostAbortBridges;

    /// <summary>
    /// The synthetic target the global <c>addEventListener</c> registers on, or <see langword="null"/> — which
    /// is what every engine carries until the first of those calls, and forever on one that never enabled
    /// <see cref="WebApiFeatures.GlobalEvents"/>. That null is the whole cost of the four report sites: a
    /// failure nobody is listening for reaches one field read.
    /// </summary>
    private GlobalEventTarget? _globalEventTarget;

    /// <summary>
    /// The <c>BroadcastChannel</c> objects this engine has open, in creation order. Engine-thread-only for the
    /// same reason the fetch list is: a channel is added by its own constructor and removed by <c>close()</c>,
    /// both of which run on the engine's thread. It exists so a restore can end every channel this engine
    /// created — the broker itself is shareable and has no idea which engine anything came from.
    /// </summary>
    private List<JsBroadcastChannel>? _broadcastChannels;

    /// <summary>
    /// Where this engine's <c>BroadcastChannel</c> objects find each other, or <see langword="null"/> until one
    /// is needed. Seeded from <c>Options.WebApi.Messaging.Broker</c> when the engine was built, and defaulted
    /// per engine on first use — see <see cref="BroadcastChannels"/>.
    /// </summary>
    private BroadcastChannelBroker? _broadcastChannelBroker;

    /// <summary>
    /// The <c>MessagePort</c> objects this engine has created, held <b>weakly</b>. Engine-thread-only, and
    /// exists for one reason: a restore or a dispose has to be able to <i>close</i> this engine's ports, not
    /// merely stop delivering into them — a port's side may be entangled with one belonging to an engine that
    /// outlives this cycle, and it may be holding an undelivered message that itself carries a transferred
    /// side nothing else can ever reach.
    /// </summary>
    /// <remarks>
    /// Weak, because a strong list would turn <c>while (true) new MessageChannel();</c> into a leak that the
    /// engine itself created — a channel whose two ports no script can name is garbage in a browser too, and a
    /// port that has been collected cannot be holding anything that is not garbage with it. Pruned in
    /// amortized constant time rather than on every registration: the threshold doubles, so a script creating
    /// a great many channels does not turn each creation into a walk of everything before it.
    /// </remarks>
    private List<WeakReference<JsMessagePort>>? _messagePorts;

    private int _messagePortPruneThreshold = MessagePortPruneFloor;

    private const int MessagePortPruneFloor = 16;

    internal WebApiEngineState(Engine engine, TimeProvider timeProvider, TimerQueue? timers, Options.FetchOptions? fetchOptions, SchedulerQueue? scheduler, DiagnosticsSink? diagnostics, Options.StorageOptions? storage = null, CacheStorageProvider? cacheProvider = null, IdleCallbackQueue? idleCallbacks = null, Options.MessagingOptions? messaging = null)
    {
        _engine = engine;
        _timeProvider = timeProvider;
        Timers = timers;
        FetchOptions = fetchOptions;
        Scheduler = scheduler;
        Diagnostics = diagnostics;

        // Read once, here, exactly as the clock and the timer cap above are: a provider the host assigned is
        // the one this engine uses forever, and one it did not assign is defaulted per engine on first use.
        _localStorageProvider = storage?.LocalStorageProvider;
        _sessionStorageProvider = storage?.SessionStorageProvider;
        _storageQuotaBytes = storage?.MaxTotalBytes ?? Options.StorageOptions.DefaultMaxTotalBytes;
        CacheProvider = cacheProvider;
        IdleCallbacks = idleCallbacks;

        // Read once, here, exactly as the storage providers above are — and, like them, left null when the
        // host named none so that an engine which never creates a channel allocates no broker at all.
        _broadcastChannelBroker = messaging?.Broker;

        // Both halves of the time origin, read back to back: the monotonic reading every later now() is a
        // duration from, and the wall-clock moment that reading corresponds to.
        _originTimestamp = timeProvider.GetTimestamp();
        TimeOrigin = (timeProvider.GetUtcNow() - DateTimeOffset.UnixEpoch).TotalMilliseconds;
    }

    /// <summary>
    /// The script function inbound requests are routed to by <c>Engine.Advanced.InvokeFetchHandler</c>, or
    /// <see langword="null"/> when the host has registered none.
    /// </summary>
    /// <remarks>
    /// Host state rather than evaluation state, so — like <c>Engine.Advanced.HostDefined</c> — it is
    /// deliberately not cleared by <see cref="ResetTransientState"/>: a pooled engine that restores its
    /// globals between requests keeps the handler it was given, and the host replaces it when it wants to.
    /// An invocation that was in flight at the restore is fenced off by its own generation instead.
    /// </remarks>
    internal FetchHandler? FetchHandler { get; set; }

    /// <summary>
    /// The clock this state was built on, which every later piece attached to it has to share: the timers,
    /// <c>performance.now()</c> and the time origin all read it, so a feature enabled later must schedule
    /// against the same one rather than against whatever the options say by then.
    /// </summary>
    internal TimeProvider TimeProvider => _timeProvider;

    /// <summary>
    /// The engine's active timers, or <see langword="null"/> when nothing that schedules one is enabled.
    /// </summary>
    internal TimerQueue? Timers { get; private set; }

    /// <summary>
    /// This engine's worker provider, its two caps and its live connections, or <see langword="null"/> — which
    /// is what every engine carries unless it enabled <see cref="WebApiFeatures.Workers"/> <i>and</i> named a
    /// provider. That null is also what leaves the <c>Worker</c> global uninstalled, so
    /// <c>typeof Worker === 'undefined'</c>: the family's absent-rather-than-throwing convention.
    /// </summary>
    /// <remarks>
    /// The one member of this state whose contents are touched from more than one thread; see
    /// <see cref="WorkerRegistry"/> for why, and for the lock that makes it safe.
    /// </remarks>
    internal WorkerRegistry? Workers { get; private set; }

    /// <summary>
    /// The connection this engine is the <i>worker</i> of, or <see langword="null"/> for an engine that is
    /// nobody's worker. Written once, on the parent's thread, while this engine is owned and quiescent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is what makes "not already connected" a construction-time refusal rather than a second connection
    /// that would give one global two parents, and it is what a worker-side <c>RestoreGlobalSnapshot</c> or
    /// <c>Dispose</c> ends the connection through — see <see cref="EndWorkerConnections"/>.
    /// </para>
    /// <para>
    /// It is deliberately <b>not</b> cleared when the connection ends, so an engine that has been somebody's
    /// worker can never become somebody else's. Reusing one would be a trap rather than a saving: the worker
    /// global scope is installed non-clobbering, so a second connection's <c>postMessage</c> would be declined
    /// in favour of the first connection's dead one, and the difference is invisible from script.
    /// </para>
    /// </remarks>
    internal WorkerLink? OwningWorkerLink { get; set; }

    /// <summary>
    /// The host's fetch settings, or <see langword="null"/> when the feature is off. Read once — when the
    /// engine is built, or when <c>Engine.Advanced.EnableWebApis</c> turned the feature on — so that no
    /// background thread ever reaches into <see cref="Options"/>.
    /// </summary>
    internal Options.FetchOptions? FetchOptions { get; private set; }

    /// <summary>
    /// Where the <c>caches</c> object keeps what a script stored, or <see langword="null"/> when the Cache
    /// API is off. Resolved once, when the engine is built — either the host's provider or a private
    /// in-memory one — so two engines built from one shared <see cref="Options"/> do not share a cache
    /// unless the host deliberately gave them one provider.
    /// </summary>
    /// <remarks>
    /// Deliberately untouched by <see cref="ResetTransientState"/>: the provider is host storage, not engine
    /// state, so a pooled engine's next cycle finds what the previous one cached — the same answer the module
    /// registry gets from a restore.
    /// </remarks>
    internal CacheStorageProvider? CacheProvider { get; private set; }

    /// <summary>
    /// How many requests are in flight, which is what <c>Options.FetchOptions.MaxConcurrentRequests</c>
    /// bounds.
    /// </summary>
    internal int ActiveFetchCount => _fetches?.Count ?? 0;

    internal void RegisterFetch(FetchOperation operation) => (_fetches ??= new List<FetchOperation>()).Add(operation);

    internal void UnregisterFetch(FetchOperation operation) => _fetches?.Remove(operation);

    internal void RegisterBodyStream(FetchBodyStream body) => (_fetchBodies ??= new List<FetchBodyStream>()).Add(body);

    internal void UnregisterBodyStream(FetchBodyStream body) => _fetchBodies?.Remove(body);

    /// <summary>
    /// The engine's prioritized task queues, or <see langword="null"/> when the scheduler feature is off.
    /// Nothing else consults it: the scheduler drains itself through an ordinary event-loop job, so unlike the
    /// timers it needs no hook in the pump.
    /// </summary>
    internal SchedulerQueue? Scheduler { get; private set; }

    /// <summary>
    /// Where uncaught script errors are reported, or <see langword="null"/> when the host set no sink — which
    /// is also what says a <c>JavaScriptException</c> escaping an engine-invoked callback must erupt rather
    /// than be reported. Snapshotted when the engine was built, so the contract cannot change under a script
    /// that is already running.
    /// </summary>
    internal DiagnosticsSink? Diagnostics { get; }

    /// <summary>
    /// How many event streams are open, which is what <c>Options.FetchOptions.MaxConcurrentRequests</c>
    /// bounds for <c>EventSource</c>.
    /// </summary>
    internal int ActiveEventSourceCount => _eventSources?.Count ?? 0;

    internal void RegisterEventSource(EventSourceConnection connection)
        => (_eventSources ??= new List<EventSourceConnection>()).Add(connection);

    internal void UnregisterEventSource(EventSourceConnection connection) => _eventSources?.Remove(connection);

    /// <summary>
    /// How many sockets are open, which is what <c>Options.FetchOptions.MaxConcurrentRequests</c> bounds for
    /// this feature. Counted separately from the requests in flight: a socket is a long-lived thing and a
    /// fetch is not, so one kind must not exhaust the other's budget.
    /// </summary>
    internal int ActiveWebSocketCount => _webSockets?.Count ?? 0;

    /// <summary>
    /// Where a <c>WebSocket</c>'s transport comes from, or <see langword="null"/> for the in-box
    /// <c>ClientWebSocket</c> one.
    /// </summary>
    /// <remarks>
    /// The seam the test suite replaces so that the whole state machine — handshake, messages,
    /// <c>bufferedAmount</c>, the closing handshake, every event — can be driven with no network anywhere. It
    /// is deliberately not a host-facing option: a transport a host supplied would want headers, proxies,
    /// certificates and a keep-alive policy with it, which is a larger design than this field.
    /// </remarks>
    internal IWebSocketConnectionFactory? WebSocketConnections { get; set; }

    internal void RegisterWebSocket(JsWebSocket socket) => (_webSockets ??= new List<JsWebSocket>()).Add(socket);

    internal void UnregisterWebSocket(JsWebSocket socket) => _webSockets?.Remove(socket);

    /// <summary>
    /// The engine's <c>requestIdleCallback</c> queue, or <see langword="null"/> when that feature is off. It
    /// is drained from the same pump exhaustion point the timers are promoted at, one callback at a time and
    /// only once no timer is due — see <see cref="IdleCallbackQueue"/> for what "idle" means for an engine
    /// that has no frames.
    /// </summary>
    internal IdleCallbackQueue? IdleCallbacks { get; private set; }

    /// <summary>
    /// Whether an idle callback is waiting for a pump. Read only by
    /// <see cref="Engine.AdvancedOperations.TimeUntilNextScheduledWork"/>, which reports such a callback as
    /// work available now.
    /// </summary>
    internal bool HasPendingIdleWork => IdleCallbacks is { HasPendingWork: true };

    /// <summary>
    /// <c>performance.timeOrigin</c>: the moment this state was created, as milliseconds since the Unix
    /// epoch, https://w3c.github.io/hr-time/#dom-performance-timeorigin.
    /// </summary>
    /// <remarks>
    /// Per engine, and deliberately not reset by <see cref="ResetTransientState"/>: a pooled engine keeps the
    /// origin it was built with, so <c>performance.now()</c> can never go backwards across an evaluation
    /// cycle.
    /// </remarks>
    internal double TimeOrigin { get; }

    /// <summary>
    /// <c>performance.now()</c>: https://w3c.github.io/hr-time/#dfn-current-high-resolution-time, the
    /// duration from <see cref="TimeOrigin"/> to now in milliseconds, read from the same monotonic clock the
    /// timers are scheduled against and deliberately not coarsened.
    /// </summary>
    internal double CurrentHighResolutionTime =>
        _timeProvider.GetElapsedTime(_originTimestamp, _timeProvider.GetTimestamp()).TotalMilliseconds;

    /// <summary>
    /// The map behind <c>localStorage</c>, and behind <c>sessionStorage</c> — two separate stores, and two
    /// separate <see cref="InMemoryStorageProvider"/> instances when the host supplied neither.
    /// </summary>
    /// <remarks>
    /// Defaulted on first use rather than at construction, so an engine that enabled the feature and never
    /// touched the global has still allocated nothing. Deliberately <b>not</b> touched by
    /// <see cref="ResetTransientState"/>: what a storage holds is host state, like the module registry and
    /// like <c>Engine.Advanced.HostDefined</c>, and a restore reverts the global binding table rather than
    /// the world behind it. A host that wants a pooled engine to forget the previous cycle's storage swaps
    /// the provider, or clears it, itself.
    /// </remarks>
    internal StorageProvider LocalStorageProvider =>
        _localStorageProvider ??= new InMemoryStorageProvider(_storageQuotaBytes);

    /// <inheritdoc cref="LocalStorageProvider" />
    internal StorageProvider SessionStorageProvider =>
        _sessionStorageProvider ??= new InMemoryStorageProvider(_storageQuotaBytes);

    /// <summary>
    /// The <see cref="BroadcastChannelBroker"/> this engine's <c>BroadcastChannel</c> objects join: the host's,
    /// when it assigned one to <see cref="Options.MessagingOptions.Broker"/>, and otherwise a private one of
    /// this engine's own — so channels on one engine always hear each other and nothing crosses an engine
    /// boundary unless the host deliberately shared a broker.
    /// </summary>
    /// <remarks>
    /// Defaulted on first use rather than at construction, so an engine that enabled messaging and never
    /// created a channel has still allocated nothing. Deliberately <b>not</b> reset by
    /// <see cref="ResetTransientState"/>: a host's broker is host state like a storage provider, and the
    /// private default is the identity of this engine's own cluster, which a restore has no business
    /// replacing — what a restore ends is the channels, not the cluster they were in.
    /// </remarks>
    internal BroadcastChannelBroker BroadcastChannels =>
        _broadcastChannelBroker ??= new BroadcastChannelBroker();

    /// <summary>
    /// Records a live <c>BroadcastChannel</c>, so that a restore or a dispose can end it — which is also what
    /// takes it out of a broker the host may be sharing with engines that outlive this one.
    /// </summary>
    internal void RegisterBroadcastChannel(JsBroadcastChannel channel) =>
        (_broadcastChannels ??= new List<JsBroadcastChannel>()).Add(channel);

    /// <summary>Forgets one channel, which is what <c>close()</c> does to itself.</summary>
    internal void UnregisterBroadcastChannel(JsBroadcastChannel channel) => _broadcastChannels?.Remove(channel);

    /// <summary>
    /// Records a live <c>MessagePort</c>; see <see cref="_messagePorts"/>. There is deliberately no
    /// unregister: a port that closes or is transferred away becomes inert, and the next prune drops it.
    /// </summary>
    internal void RegisterMessagePort(JsMessagePort port)
    {
        var ports = _messagePorts ??= new List<WeakReference<JsMessagePort>>();

        if (ports.Count >= _messagePortPruneThreshold)
        {
            ports.RemoveAll(static entry => !entry.TryGetTarget(out var candidate) || candidate.IsInert);
            _messagePortPruneThreshold = Math.Max(MessagePortPruneFloor, ports.Count * 2);
        }

        ports.Add(new WeakReference<JsMessagePort>(port));
    }

    /// <summary>
    /// Closes every port this engine still has, which is what ends the channels the cycle being torn down
    /// created. Closing rather than forgetting, for the reason <see cref="ResetTransientState"/> gives.
    /// </summary>
    private void CloseMessagePorts()
    {
        if (_messagePorts is not { Count: > 0 } ports)
        {
            return;
        }

        // Copied before the walk, exactly as the broadcast channels are: closing a port can close the sides
        // stranded in its queue, and those can belong to ports of this engine too.
        var live = ports.ToArray();
        ports.Clear();
        _messagePortPruneThreshold = MessagePortPruneFloor;

        foreach (var entry in live)
        {
            if (entry.TryGetTarget(out var port))
            {
                port.Close();
            }
        }
    }

    /// <summary>
    /// Gives a state that was built without one the timer queue a feature enabled later needs. Called only by
    /// <c>WebApiRegistration.ExtendEngineState</c>, and only for a slot that is still empty — a queue the
    /// engine has already been scheduling on is never replaced.
    /// </summary>
    /// <remarks>
    /// The whole late-attachment family exists for <c>Engine.Advanced.EnableWebApis</c>: the state is created
    /// once, with exactly the queues the features named at that moment, so turning a feature on afterwards has
    /// to be able to add the one piece it brought with it. Each of these asserts the slot is empty rather than
    /// tolerating a second call, because a second call could only mean the caller lost track of which features
    /// were already on — which is precisely the question <c>Engine._webApiFeatures</c> answers.
    /// </remarks>
    internal void AttachTimers(TimerQueue timers)
    {
        Debug.Assert(Timers is null, "the timer queue must never be replaced on a live engine");
        Timers = timers;
    }

    /// <inheritdoc cref="AttachTimers" />
    internal void AttachWorkers(WorkerRegistry workers)
    {
        Debug.Assert(Workers is null, "the worker registry must never be replaced on a live engine");
        Workers = workers;
    }

    /// <inheritdoc cref="AttachTimers" />
    internal void AttachFetchOptions(Options.FetchOptions fetchOptions)
    {
        Debug.Assert(FetchOptions is null, "the network settings must never be replaced on a live engine");
        FetchOptions = fetchOptions;
    }

    /// <inheritdoc cref="AttachTimers" />
    internal void AttachScheduler(SchedulerQueue scheduler)
    {
        Debug.Assert(Scheduler is null, "the scheduler queues must never be replaced on a live engine");
        Scheduler = scheduler;
    }

    /// <inheritdoc cref="AttachTimers" />
    internal void AttachCacheProvider(CacheStorageProvider cacheProvider)
    {
        Debug.Assert(CacheProvider is null, "the cache provider must never be replaced on a live engine");
        CacheProvider = cacheProvider;
    }

    /// <inheritdoc cref="AttachTimers" />
    internal void AttachIdleCallbacks(IdleCallbackQueue idleCallbacks)
    {
        Debug.Assert(IdleCallbacks is null, "the idle-callback queue must never be replaced on a live engine");
        IdleCallbacks = idleCallbacks;
    }

    /// <summary>
    /// Reads the storage group into a state that was built without it. Unlike its siblings this one has no
    /// slot of its own to test: the two providers are defaulted lazily on first use, so what it must not do is
    /// overwrite a provider that has already been resolved — hence the <c>??=</c>. The quota is only ever
    /// consulted while defaulting a provider, so assigning it here reaches exactly the providers that do not
    /// exist yet, which is the same set the host is enabling storage for.
    /// </summary>
    internal void AttachStorage(Options.StorageOptions storage)
    {
        _localStorageProvider ??= storage.LocalStorageProvider;
        _sessionStorageProvider ??= storage.SessionStorageProvider;
        _storageQuotaBytes = storage.MaxTotalBytes;
    }

    /// <summary>
    /// Reads the messaging group into a state that was built without it. Like <see cref="AttachStorage"/> and
    /// unlike the <c>Debug.Assert</c>-ing siblings above, it has no slot of its own to test: the broker is
    /// defaulted lazily on first use, so what it must not do is overwrite one that has already been resolved —
    /// hence the <c>??=</c>. Nothing can have resolved one before the messaging feature was on, since only a
    /// <c>BroadcastChannel</c> constructor reads it, so the assignment reaches exactly the engines the host is
    /// enabling messaging for.
    /// </summary>
    internal void AttachMessaging(Options.MessagingOptions messaging) => _broadcastChannelBroker ??= messaging.Broker;

    /// <summary>
    /// Promotes at most one due timer into an event-loop job, and failing that runs at most one idle callback.
    /// One per call rather than all of them, so that the reactions a callback queues are run before the next
    /// one is even looked at.
    /// </summary>
    /// <remarks>
    /// The order is the priority: a due timer is a task, and idle callbacks are what a browser runs in the
    /// slack after the tasks are done, so nothing idle may overtake a timer that is already due.
    /// </remarks>
    internal bool TryPromoteDeferredWork()
    {
        var timers = Timers;
        if (timers is not null && timers.TryTakeDue(out var entry))
        {
            // Enqueued with the timer's own registration generation rather than the current one: a timer
            // registered before a RestoreGlobalSnapshot is already gone from the queue that restore cleared,
            // and this is the belt to that braces.
            _engine.AddToEventLoop(entry.Job, entry.Generation);
            return true;
        }

        return IdleCallbacks is { } idle && idle.TryRunIdleCallback();
    }

    /// <summary>
    /// Records a host <c>CancellationToken</c> bridged to an <c>AbortSignal</c> by
    /// <see cref="Engine.AdvancedOperations.CreateAbortSignal"/>, so the token registration can be released
    /// again when the cycle ends or the engine is disposed.
    /// </summary>
    internal void AddHostAbortBridge(HostAbortSignalBridge bridge) =>
        (_hostAbortBridges ??= new List<HostAbortSignalBridge>()).Add(bridge);

    /// <summary>Forgets one bridge, which is what a bridge does to itself once its abort has landed.</summary>
    internal void RemoveHostAbortBridge(HostAbortSignalBridge bridge) => _hostAbortBridges?.Remove(bridge);

    /// <summary>
    /// How many host token registrations this engine is currently holding. Nothing script-facing reads it: it
    /// is what an in-assembly test asserts on to prove that a pooled engine serving request after request from
    /// one long-lived host token accumulates no registrations.
    /// </summary>
    internal int HostAbortBridgeCount => _hostAbortBridges?.Count ?? 0;

    /// <summary>
    /// Releases every host token registration. A long-lived host token — a request's, an application
    /// lifetime's — would otherwise keep a finished engine reachable through its registration list.
    /// </summary>
    internal void ReleaseHostAbortBridges()
    {
        if (_hostAbortBridges is not { } bridges)
        {
            return;
        }

        foreach (var bridge in bridges)
        {
            // Detach deliberately does not touch this list, so walking it here is safe.
            bridge.Detach();
        }

        bridges.Clear();
    }

    /// <summary>
    /// How long the engine may idle before a timer needs the pump, or <see langword="null"/> when nothing is
    /// scheduled. Zero or negative means one is due right now.
    /// </summary>
    internal TimeSpan? TimeUntilNextDueTimer() => Timers?.TimeUntilNextDue();

    /// <summary>
    /// The engine's synthetic global event target, created on first use — which is the first global
    /// <c>addEventListener</c>, <c>removeEventListener</c> or <c>dispatchEvent</c>, all three of which exist
    /// only when <see cref="WebApiFeatures.GlobalEvents"/> is on.
    /// </summary>
    internal GlobalEventTarget GlobalEventTarget =>
        _globalEventTarget ??= new GlobalEventTarget(_engine, _engine._mainRealm);

    /// <summary>
    /// The same target, or <see langword="null"/> when nothing has built one yet. What
    /// <c>Engine.Advanced.InvokeFetchHandler</c> asks, because a host invoking a handler must not be the thing
    /// that creates a listener list: an engine whose script never called <c>addEventListener</c> answers the
    /// question with one field read and allocates nothing.
    /// </summary>
    internal GlobalEventTarget? GlobalEventTargetIfCreated => _globalEventTarget;

    /// <summary>
    /// <c>reportError(e)</c>: HTML's <i>report an exception</i> —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception. See
    /// <c>ReportErrorFunction</c>.
    /// </summary>
    /// <remarks>
    /// Step 5 fires an <c>error</c> event at the global scope, which now happens whenever a script registered
    /// a listener for it; step 6 hands the value to the sink. <b>The two are additive rather than
    /// alternative:</b> HTML lets a listener's <c>preventDefault()</c> set <i>notHandled</i> false and so
    /// suppress the console report, and Jint deliberately reports either way — a host's diagnostics channel is
    /// not something the script it is running may switch off. The <i>parent</i> of a worker is told only when
    /// <i>notHandled</i> is true, which is the half of the algorithm a script legitimately controls; see
    /// <see cref="FireErrorAndPropagate"/>.
    /// </remarks>
    internal void ReportError(JsValue value)
    {
        if (HasSomewhereToReportAnError)
        {
            // The location the engine last saw, which for a call from script is the reportError call site —
            // the same fallback every web API that has to place a failure uses.
            var location = _engine._lastSyntaxElement?.Location ?? default;
            FireErrorAndPropagate(ErrorEventDetails.FromReportedValue(value, in location));
        }

        Diagnostics?.Report(DiagnosticEvent.ForReportedError(value));
    }

    /// <summary>
    /// HTML's <i>report an exception</i> for a <see cref="JavaScriptException"/> that escaped a callback the
    /// engine invoked — a timer handler, an event listener. Step 5 only: the callers own step 6, because the
    /// sink report they make carries the exception itself and not merely its value.
    /// </summary>
    /// <remarks>
    /// A no-op on an engine that has no global listener and is nobody's worker, which is every engine until a
    /// script registers one. It declines to recurse: an exception thrown by a listener <i>while</i> a report is
    /// being dispatched reaches the sink alone — see <see cref="GlobalEventTarget"/>.
    /// </remarks>
    internal void FireGlobalErrorEvent(JavaScriptException exception)
    {
        if (HasSomewhereToReportAnError)
        {
            FireErrorAndPropagate(ErrorEventDetails.FromException(exception));
        }
    }

    /// <summary>
    /// Step 5.2.3 of <i>report an exception</i>: the <c>error</c> event this engine's own <c>Worker</c> object
    /// raised was not cancelled either, so the failure is reported one level further up — at <i>this</i>
    /// engine's global scope and to <i>this</i> engine's sink.
    /// </summary>
    /// <remarks>
    /// It is the recursive step, so it propagates again when this engine is itself a worker, which is what
    /// produces HTML's up-the-chain propagation for the nested workers a provider deliberately enabled.
    /// </remarks>
    internal void ReportWorkerError(in ErrorEventDetails details)
    {
        FireErrorAndPropagate(in details);
        Diagnostics?.Report(DiagnosticEvent.ForWorkerError(details.Message));
    }

    /// <summary>
    /// Whether a failure reported here could reach anything at all: a global <c>error</c> listener, or the
    /// parent of a worker. Two field reads, which is what every report site costs on an engine that has
    /// neither.
    /// </summary>
    private bool HasSomewhereToReportAnError => _globalEventTarget is not null || OwningWorkerLink is not null;

    /// <summary>
    /// <i>Report an exception</i> step 5: fire <c>error</c> at this global scope, and — <b>only</b> when the
    /// result is HTML's <i>notHandled</i> — hand the failure to the <c>Worker</c> object one engine up.
    /// <para>
    /// https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The <i>notHandled</i> gate is the whole reason this is not the <see cref="DiagnosticsSink"/>.</b> The
    /// sink is deliberately unsuppressible by script and is told whatever a listener did; HTML's propagation to
    /// the parent is gated on exactly the bool a worker-side <c>preventDefault()</c> — or a global
    /// <c>onerror</c> returning <see langword="true"/> — sets to false. So the relay sits beside the sink and
    /// reads that bool, and the host's own channel is untouched.
    /// </para>
    /// <para>
    /// Reading <see cref="GlobalEventTarget.IsReporting"/> <i>before</i> the dispatch is the in-error-reporting
    /// mode guard: a failure raised while a previous one was being reported fires nothing (the dispatch itself
    /// declines) and must not be propagated either, or the recursion the guard exists to stop would simply move
    /// one engine up. After the call the flag would answer for the outer report's frame instead, which is the
    /// same answer for the wrong reason.
    /// </para>
    /// </remarks>
    private void FireErrorAndPropagate(in ErrorEventDetails details)
    {
        var target = _globalEventTarget;
        var reporting = target is { IsReporting: true };
        var notHandled = target is null || target.FireError(in details);

        if (notHandled && !reporting)
        {
            OwningWorkerLink?.ReportErrorToParent(in details);
        }
    }

    /// <summary>
    /// The sink's half of <c>HostPromiseRejectionTracker</c>. Additive to
    /// <see cref="Engine.AdvancedOperations.PromiseRejectionTracker"/>, which has already been raised by the
    /// time this runs: a host with both channels wired sees the pre-existing event behave exactly as it did.
    /// </summary>
    /// <remarks>
    /// The <c>unhandledrejection</c> and <c>rejectionhandled</c> events of
    /// https://html.spec.whatwg.org/multipage/webappapis.html#unhandled-promise-rejections ride here too, and
    /// therefore at the <i>tracker's</i> cadence rather than HTML's microtask checkpoint — the divergence
    /// <see cref="DiagnosticEvent.RejectionHandled"/> documents, inherited whole. Cancelling
    /// <c>unhandledrejection</c> suppresses a browser's console report; the sink is told regardless, for the
    /// reason <see cref="ReportError"/> gives.
    /// </remarks>
    internal void ReportPromiseRejection(JsPromise promise, PromiseRejectionOperation operation)
    {
        if (_globalEventTarget is { } target)
        {
            var handled = operation == PromiseRejectionOperation.Handle;
            var reason = promise.State == PromiseState.Rejected ? promise.Value : JsValue.Undefined;
            target.FireRejection(handled, promise, reason);
        }

        Diagnostics?.Report(DiagnosticEvent.ForPromiseRejection(promise, operation));
    }

    /// <summary>
    /// Registered timers that have not fired and have not been cleared, for
    /// <see cref="Engine.AdvancedOperations.GetMemoryReport(int)"/>.
    /// </summary>
    internal int PendingTimerCount => Timers?.Count ?? 0;

    /// <summary>
    /// Drops the state that belongs to the evaluation cycle a <c>RestoreGlobalSnapshot</c> has just ended.
    /// </summary>
    /// <remarks>
    /// A request in flight is cancelled rather than merely forgotten: the generation fence already stops its
    /// response reaching the restored engine, but forgetting it would leave the socket open until the server
    /// answered. Its promise stays pending — settling it is exactly what the fence forbids — which is the
    /// same contract a promise registered before a restore has always had. A response body still streaming is
    /// the same story one step later: its promise has already settled, so what has to be ended is the
    /// <c>ReadableStream</c>. Its controller is <b>errored</b>, which is the honest answer to a host still
    /// holding the response — a stream that reports failure beats one that silently never produces another
    /// byte — and the reactions that erroring schedules carry the ending cycle's generation, so the fence
    /// discards them exactly as it discards a chunk still on its way. A <c>WebSocket</c> is abandoned the
    /// same way and for the same reason, and fires no further event: its connection is dropped, and its
    /// <c>close</c> event belongs to a cycle that has ended. The <b>global event listeners</b> go the same
    /// way: they are closures over the ended cycle, over globals the restore has just replaced, so an
    /// <c>error</c> fired afterwards must reach none of them. The sink is deliberately not reset: it is
    /// configuration the engine was built with, like the time origin beside it, and a pooled engine
    /// reporting the next cycle's errors nowhere would be a strange thing for a restore to arrange.
    /// A <c>BroadcastChannel</c> is <b>closed</b> rather than merely forgotten, which is the same rule a
    /// <c>MessagePort</c> follows for the same reason — its listeners are closures over the cycle that is
    /// ending — with one addition: the broker it joined may be the host's and may outlive this engine, so
    /// leaving the subscription there would keep this engine reachable and go on costing every future sender a
    /// job the generation fence then throws away.
    /// A <c>MessagePort</c> is closed for exactly those reasons and one more that is peculiar to it. The
    /// generation fence already stops delivery — the port belongs to the cycle it was created in — but the
    /// port message queue lives on the channel <i>side</i>, so a message posted before the restore is sitting
    /// in it whether or not anything ever pumps again, and that message may be carrying a <b>transferred</b>
    /// side, unbound and waiting for a deserialization that can now never happen while its own peer goes on
    /// posting into it. Closing the port drains the queue and ends every side stranded in it, transitively.
    /// The peer is deliberately <i>not</i> closed: disentangling is one-sided, and a peer on another engine is
    /// in a cycle of its own that this restore has no business ending.
    /// </remarks>
    internal List<Action>? ResetTransientState()
    {
        // First, and before the general port sweep below: a worker connection is two endpoints plus a token
        // plus a reason, and CloseMessagePorts would otherwise close this engine's half of it as an anonymous
        // port — stopping delivery while leaving the connection reading as live.
        var endedWorkers = EndWorkerConnections(WorkerEndReason.ParentRestored, WorkerEndReason.WorkerRestored);

        Timers?.Clear();
        Scheduler?.Clear();
        AbandonFetches();
        AbandonFetchBodies();
        AbandonEventSources();
        AbandonWebSockets();
        CloseBroadcastChannels();
        CloseMessagePorts();
        IdleCallbacks?.Clear();

        // Dropped whole rather than emptied: the next cycle's first addEventListener builds a fresh target,
        // and nothing outside this class holds a reference to the old one — the three global operations ask
        // for it by property on every call.
        _globalEventTarget = null;

        // A signal bridged to a host token belongs to the cycle it was created in: its abort job carries that
        // cycle's generation and would be dropped at dequeue anyway, so keeping the registration alive could
        // only accumulate one per cycle on a pooled engine.
        ReleaseHostAbortBridges();

        return endedWorkers;
    }

    /// <summary>
    /// Ends every worker connection this engine is a party to — the ones it created, and the one it <i>is</i>
    /// the worker of.
    /// </summary>
    /// <param name="asParent">The reason for a connection this engine created.</param>
    /// <param name="asWorker">The reason for the connection this engine is the worker of.</param>
    /// <returns>
    /// The host callbacks to run once the teardown is over, or <see langword="null"/> when there was nothing to
    /// end. See <see cref="NotifyWorkerHosts"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Both endpoints are closed, which is a worker-specific rule rather than the port rule.</b> A
    /// <c>MessagePort</c> deliberately does not close its peer — disentangling is one-sided, and a peer on
    /// another engine is in a cycle of its own. A worker connection is not two independent peers: it is one
    /// object the engine created spanning two engines, and a one-sided close would stop <i>delivery</i> while
    /// leaving the survivor paying a full structured clone per <c>postMessage</c>, detaching its own
    /// transfer-listed buffers and throwing <c>DataCloneError</c> for unserializable values, forever. Closing
    /// both is what stops the survivor's work.
    /// </para>
    /// <para>
    /// The <c>Worker</c> object itself stays alive and becomes <b>inert</b> — <c>postMessage</c> a no-op after
    /// the serialization the standard's step order still prescribes, <c>terminate()</c> idempotent, no further
    /// events, since every job this cycle queued on either loop carries a generation the restore has moved
    /// past. That is not a concession: the disentangled-port clause of HTML's own error propagation describes
    /// exactly this state.
    /// </para>
    /// <para>
    /// Everything here is the thread-safe part of ending — endpoints, a token, interlocked bookkeeping — which
    /// is what makes it callable from a teardown at all. The host callback is <i>collected</i> rather than
    /// invoked, so a provider that throws from <c>OnWorkerEnded</c> cannot erupt out of the middle of a
    /// half-finished restore.
    /// </para>
    /// </remarks>
    private List<Action>? EndWorkerConnections(WorkerEndReason asParent, WorkerEndReason asWorker)
    {
        var registry = Workers;
        if (registry is not { LiveCount: > 0 } && OwningWorkerLink is null)
        {
            return null;
        }

        var deferred = new List<Action>();

        if (registry is not null)
        {
            // Copied out under the registry's own lock before the walk, because ending one removes it from
            // that very list.
            foreach (var link in registry.Snapshot())
            {
                link.Connection.TryEnd(asParent, error: null, deferred);
            }
        }

        OwningWorkerLink?.Connection.TryEnd(asWorker, error: null, deferred);

        return deferred.Count == 0 ? null : deferred;
    }

    /// <summary>
    /// Runs the <see cref="WorkerProvider.OnWorkerEnded"/> callbacks a teardown deferred, once that teardown
    /// has finished and the engine is whole again.
    /// </summary>
    /// <remarks>
    /// A host exception propagates from here, which is the right place for it: the caller asked for the restore
    /// or the dispose, the engine has completed it, and swallowing a provider's failure would hide it. Ending
    /// the remaining connections first is deliberate — the engine's own work is done for all of them before any
    /// host code runs.
    /// </remarks>
    internal static void NotifyWorkerHosts(List<Action>? deferred)
    {
        if (deferred is null)
        {
            return;
        }

        foreach (var notification in deferred)
        {
            notification();
        }
    }

    private void AbandonFetches()
    {
        if (_fetches is not { Count: > 0 } fetches)
        {
            return;
        }

        // Copied before the walk: Abandon cancels a token source, and a synchronous continuation on this
        // very thread would otherwise reach UnregisterFetch and mutate the list under the enumerator.
        var pending = fetches.ToArray();
        fetches.Clear();

        foreach (var fetch in pending)
        {
            fetch.Abandon();
        }
    }

    private void AbandonWebSockets()
    {
        if (_webSockets is not { Count: > 0 } sockets)
        {
            return;
        }

        var open = sockets.ToArray();
        sockets.Clear();

        foreach (var socket in open)
        {
            socket.Operation?.Abandon();
        }
    }

    /// <summary>
    /// The same for the event streams, and for the same reasons â with one addition: an abandoned connection
    /// leaves its <c>EventSource</c> object <c>CLOSED</c>, and fires nothing, because the evaluation cycle
    /// those listeners belonged to has ended. The reconnect delay a stream may have been holding is on the
    /// timer queue <see cref="Timers"/> has just cleared.
    /// </summary>
    private void AbandonEventSources()
    {
        if (_eventSources is not { Count: > 0 } sources)
        {
            return;
        }

        var pending = sources.ToArray();
        sources.Clear();

        foreach (var source in pending)
        {
            source.Abandon();
        }
    }

    /// <summary>
    /// Closes every <c>BroadcastChannel</c> this engine created — which unsubscribes each from its broker, so
    /// a broker the host shares between engines does not keep a finished one reachable.
    /// </summary>
    private void CloseBroadcastChannels()
    {
        if (_broadcastChannels is not { Count: > 0 } channels)
        {
            return;
        }

        // Copied before the walk, exactly as the fetches are: Close unregisters the channel, which would
        // otherwise mutate the list under the enumerator.
        var open = channels.ToArray();
        channels.Clear();

        foreach (var channel in open)
        {
            channel.Close();
        }
    }

    private void AbandonFetchBodies()
    {
        if (_fetchBodies is not { Count: > 0 } bodies)
        {
            return;
        }

        var pendingBodies = bodies.ToArray();
        bodies.Clear();

        foreach (var body in pendingBodies)
        {
            body.Abandon();
        }
    }

    /// <summary>
    /// Called from <see cref="Engine.Dispose"/>: releases the state that reaches outside the engine, which is
    /// the host token registrations, the subscriptions in a <see cref="BroadcastChannelBroker"/> the host may
    /// share with engines that outlive this one, the worker connections spanning two engines, and the
    /// <c>MessagePort</c> sides entangled with ports of another engine — each of which is a live reference to
    /// this disposed one, and each of which would go on accepting messages into a queue nothing will ever
    /// drain. The queues need nothing — they hold no unmanaged resource and no timer, and die with the engine.
    /// </summary>
    /// <returns>The host callbacks for the connections this ended; see <see cref="NotifyWorkerHosts"/>.</returns>
    /// <remarks>
    /// A worker engine's own dispose ends its connection too, and that is the half worth naming: the far side
    /// would otherwise stay a <c>Worker</c> object that looks alive while every <c>postMessage</c> pays a full
    /// serialization into a queue nothing will ever drain. The host that built the engine is the host that
    /// disposes it, so <see cref="WorkerEndReason.WorkerDisposed"/> is a fact it can act on rather than a
    /// surprise.
    /// </remarks>
    internal List<Action>? Dispose()
    {
        var endedWorkers = EndWorkerConnections(WorkerEndReason.ParentDisposed, WorkerEndReason.WorkerDisposed);

        ReleaseHostAbortBridges();
        CloseBroadcastChannels();
        CloseMessagePorts();

        return endedWorkers;
    }
}
#endif

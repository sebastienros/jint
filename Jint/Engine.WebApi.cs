#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Promise;
using Jint.WebApi;
using Jint.WebApi.Fetch;
using Jint.WebApi.Scheduling;
using Jint.WebApi.ServerSentEvents;
using Jint.WebApi.Timers;

namespace Jint;

public partial class Engine
{
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
    /// <c>AbortSignal.timeout()</c> schedules on.
    /// </summary>
    internal WebApiEngineState? _webApi;

    /// <summary>
    /// Which opt-in web APIs this engine was built with, as <c>WebApiRegistration.Apply</c> recorded them
    /// after computing the feature closure, or <see cref="WebApiFeatures.None"/> for an engine that asked for
    /// nothing. Read by the host APIs that have to refuse an engine which never opted in —
    /// <see cref="AdvancedOperations.CreateMessagePortPair"/> and
    /// <see cref="AdvancedOperations.SetFetchHandler"/> — and by nothing on any hot path. It lives here rather
    /// than being read back from <c>Options</c> because an <c>Options</c> instance is shareable and mutable,
    /// so the set an engine was actually built with is only knowable at build time.
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
    /// was, so that mutating the options afterwards cannot change an engine that already exists.
    /// </summary>
    private readonly long _storageQuotaBytes;

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

    internal WebApiEngineState(Engine engine, TimeProvider timeProvider, TimerQueue? timers, Options.FetchOptions? fetchOptions, SchedulerQueue? scheduler, DiagnosticsSink? diagnostics, Options.StorageOptions? storage = null)
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
    /// The engine's active timers, or <see langword="null"/> when nothing that schedules one is enabled.
    /// </summary>
    internal TimerQueue? Timers { get; }

    /// <summary>
    /// The host's fetch settings, or <see langword="null"/> when the feature is off. Read once, when the
    /// engine is built, so that no background thread ever reaches into <see cref="Options"/>.
    /// </summary>
    internal Options.FetchOptions? FetchOptions { get; }

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
    internal SchedulerQueue? Scheduler { get; }

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
    /// Promotes at most one due timer into an event-loop job. One per call rather than all of them, so that
    /// the reactions a timer's callback queues are run before the next timer is even looked at.
    /// </summary>
    internal bool TryPromoteDueTimerJob()
    {
        var timers = Timers;
        if (timers is null || !timers.TryTakeDue(out var entry))
        {
            return false;
        }

        // Enqueued with the timer's own registration generation rather than the current one: a timer
        // registered before a RestoreGlobalSnapshot is already gone from the queue that restore cleared, and
        // this is the belt to that braces.
        _engine.AddToEventLoop(entry.Job, entry.Generation);
        return true;
    }

    /// <summary>
    /// How long the engine may idle before a timer needs the pump, or <see langword="null"/> when nothing is
    /// scheduled. Zero or negative means one is due right now.
    /// </summary>
    internal TimeSpan? TimeUntilNextDueTimer() => Timers?.TimeUntilNextDue();

    /// <summary>
    /// <c>reportError(e)</c>: HTML's <i>report an exception</i> reduced to its last step, since this engine's
    /// global object is not an <c>EventTarget</c> and so has no <c>error</c> event to fire first. See
    /// <c>ReportErrorFunction</c>.
    /// </summary>
    internal void ReportError(JsValue value) => Diagnostics?.Report(DiagnosticEvent.ForReportedError(value));

    /// <summary>
    /// The sink's half of <c>HostPromiseRejectionTracker</c>. Additive to
    /// <see cref="Engine.AdvancedOperations.PromiseRejectionTracker"/>, which has already been raised by the
    /// time this runs: a host with both channels wired sees the pre-existing event behave exactly as it did.
    /// </summary>
    internal void ReportPromiseRejection(JsPromise promise, PromiseRejectionOperation operation)
        => Diagnostics?.Report(DiagnosticEvent.ForPromiseRejection(promise, operation));

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
    /// discards them exactly as it discards a chunk still on its way. The sink is deliberately not reset: it
    /// is configuration the engine was built with, like the time origin beside it, and a pooled engine
    /// reporting the next cycle's errors nowhere would be a strange thing for a restore to arrange.
    /// </remarks>
    internal void ResetTransientState()
    {
        Timers?.Clear();
        Scheduler?.Clear();
        AbandonFetches();
        AbandonFetchBodies();
        AbandonEventSources();
    }

    private void AbandonFetches()
    {
        if (_fetches is not { Count: > 0 } fetches)
        {
            return;
        }

        // Copied before the walk: Abandon cancels a token source, and a synchronous continuation on this very
        // thread would otherwise reach UnregisterFetch and mutate the list under the enumerator.
        var pending = fetches.ToArray();
        fetches.Clear();

        foreach (var fetch in pending)
        {
            fetch.Abandon();
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
}
#endif

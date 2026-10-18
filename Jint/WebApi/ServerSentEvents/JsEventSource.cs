#if NET8_0_OR_GREATER
using System.Net.Http;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Timers;
using Jint.WebApi.Url.Parsing;

namespace Jint.WebApi.ServerSentEvents;

/// <summary>
/// An <c>EventSource</c> instance: the connection state machine of the server-sent events protocol, and an
/// <see cref="JsEventTarget"/> so that a script can listen for <c>open</c>, <c>message</c> and <c>error</c>.
/// <para>
/// https://html.spec.whatwg.org/multipage/server-sent-events.html#the-eventsource-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here runs on the engine's thread.</b> The readyState transitions, the event dispatches and
/// the decision to reconnect all happen inside the pump; the connection's own thread only ever hands over
/// plain CLR data through a generation-stamped job. That is what makes the state machine safe to read from
/// script without a lock, and it is the reason nothing at all happens in an engine nobody pumps.
/// </para>
/// <para>
/// <b>The reconnect delay rides the engine's timer queue</b> — the same queue <c>setTimeout</c> and
/// <c>AbortSignal.timeout()</c> use — so a reconnection, like everything else, happens only while the engine
/// is being pumped, and it counts against <c>Options.WebApi.Timers.MaxActiveTimers</c>. Each attempt re-runs
/// the URL policy from scratch: the scheme list and the host's <c>UrlFilter</c> are asked again, so revoking
/// a destination between two attempts really does stop the next one.
/// </para>
/// <para>
/// <b>A <c>data:</c> or <c>blob:</c> URL is the exception, and is answered by the engine itself</b> — the two
/// local arms of scheme fetch, as <c>fetch</c> and <c>XMLHttpRequest</c> take them — before the policy, with no
/// connection, no transport and nothing for an observer to see. See <see cref="ConnectDataUrl"/> and
/// <see cref="ConnectBlobUrl"/>.
/// </para>
/// </remarks>
internal sealed class JsEventSource : JsEventTarget
{
    /// <summary>https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-connecting.</summary>
    internal const int Connecting = 0;

    /// <summary>https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-open.</summary>
    internal const int Open = 1;

    /// <summary>https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-closed.</summary>
    internal const int Closed = 2;

    internal const string OpenEventType = "open";
    internal const string ErrorEventType = "error";

    /// <summary>
    /// The reconnection time an event source starts with. The standard leaves it implementation-defined,
    /// "probably in the region of a few seconds"; three seconds is what browsers use, and a <c>retry</c> field
    /// replaces it for the rest of the connection's life.
    /// </summary>
    private const long DefaultReconnectionTime = 3000;

    private static readonly JsString _openEventName = new(OpenEventType);
    private static readonly JsString _errorEventName = new(ErrorEventType);

    private readonly WebApiEngineState _state;
    private readonly Options.FetchOptions _options;

    /// <summary>
    /// The engine's cancellation as it stood when the script constructed this object — a registered
    /// constraint's token and the token of the <c>*Async</c> call it ran under. Every connection, a reconnect
    /// long after that call returned included, observes these, so the call's token ends the stream rather than
    /// only its first connection.
    /// </summary>
    private readonly EngineCancellation _cancellation;

    private EventSourceConnection? _connection;

    /// <summary>The id of the timer holding the reconnect delay, or zero when none is pending.</summary>
    private int _reconnectTimerId;

    internal JsEventSource(Engine engine, Realm realm, WebApiEngineState state, UrlRecord url, bool withCredentials)
        : base(engine, realm)
    {
        _state = state;
        _options = state.FetchOptions!;
        _cancellation = engine.CaptureCancellation();
        Url = url;
        Href = JsString.Create(url.Serialize());
        WithCredentials = withCredentials;
    }

    /// <summary>The event source's url, https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-url.</summary>
    internal UrlRecord Url { get; }

    /// <summary>
    /// The serialization of <see cref="Url"/>, which is what the <c>url</c> getter answers — built once,
    /// because the URL can never change.
    /// </summary>
    internal JsString Href { get; }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-withcredentials —
    /// "must return the value to which it was last initialized".
    /// </summary>
    /// <remarks>
    /// Accepted and remembered, and it changes nothing about the request. The member selects the CORS
    /// attribute state, which decides whether a browser attaches cookies and HTTP authentication to a
    /// cross-origin request; an embedded engine has no origin, no cookie jar and no credential store, so
    /// there is nothing for it to select. It is the same treatment <c>fetch</c> gives <c>credentials</c>, and
    /// the same one Node and workerd give it.
    /// </remarks>
    internal bool WithCredentials { get; }

    /// <summary>https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-readystate.</summary>
    internal int ReadyState { get; private set; } = Connecting;

    /// <summary>
    /// The event source's last event ID string, https://html.spec.whatwg.org/multipage/server-sent-events.html#last-event-id-string.
    /// Sent as <c>Last-Event-ID</c> on every reconnect once it is non-empty.
    /// </summary>
    internal string LastEventId { get; private set; } = string.Empty;

    /// <summary>
    /// The event source's reconnection time, in milliseconds — replaced by any <c>retry</c> field the stream
    /// sends.
    /// </summary>
    internal long ReconnectionTime { get; private set; } = DefaultReconnectionTime;

    /// <summary>
    /// Constructor step 15: "fetch request". Also the last step of reestablishing the connection, which is why
    /// the whole of the policy — the scheme list, the host's filter, the client and the concurrency limit — is
    /// resolved here rather than once at construction.
    /// </summary>
    internal void Connect()
    {
        // Scheme fetch's `data` and `blob` arms, before every network check and before an observation exists,
        // exactly as fetch takes them: the bytes are in the URL or in a Blob this engine's own script created,
        // so there is nothing for the policy to decide and no request for an observer to be told about, not
        // even as a refusal.
        if (DataUrl.Is(Url))
        {
            ConnectDataUrl();
            return;
        }

        if (string.Equals(Url.Scheme, "blob", StringComparison.Ordinal))
        {
            ConnectBlobUrl();
            return;
        }

        var urlFilter = _options.UrlFilter ?? (static _ => true);

        // One observation per connection, created before the three refusals below so that a stream the host
        // never let out is reported as a failure rather than as nothing at all — the same shape a fetch
        // refused before the transport has, and the same reason: fetch's synchronous half cannot await an
        // OnRequest either.
        var observation = FetchObservation.Create(_state.FetchNetwork.Observer, FetchInitiator.EventSource);

        var policy = new FetchPolicy
        {
            AllowedSchemes = [.. _options.AllowedSchemes],
            UrlFilter = urlFilter,
            MaxResponseBytes = _options.MaxResponseBytes,
            MaxRedirects = _options.MaxRedirects,
            UserAgent = _options.UserAgent,
        };

        // The first hop's check runs on the engine thread, so a refused URL never reaches a socket. Every
        // redirect hop is re-checked inside the transport, and every reconnect comes back through here.
        if (!policy.Allows(Url, out _))
        {
            observation?.Failed("The fetch policy refused '" + Url.Serialize() + "'.", null);
            FailConnectionLater();
            return;
        }

        // Counted per engine and per kind: a host's MaxConcurrentRequests bounds the fetches in flight and,
        // separately, the event streams open, because a stream holds its socket for as long as it lives.
        if (_state.ActiveEventSourceCount >= _options.MaxConcurrentRequests)
        {
            observation?.Failed(
                "The engine already has " + _options.MaxConcurrentRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " event streams open.",
                null);
            FailConnectionLater();
            return;
        }

        HttpClient? client;
        try
        {
            client = FetchTransport.ResolveClient(_engine, _options);
        }
        catch (Exception exception) when (!ConstraintFailure.MustPropagate(exception))
        {
            // A host HttpClientFactory that threw. Unlike fetch — where the failure becomes the rejection of
            // the promise the call returned — there is no caller to hand it to on a reconnect, so both the
            // first attempt and the later ones fail the connection.
            client = null;
        }

        if (client is null)
        {
            observation?.Failed("The host's HttpClient factory did not answer with a client.", null);
            FailConnectionLater();
            return;
        }

        // The new stream's last event ID buffer starts as this object's last event ID string, so an event
        // that carries no id of its own still reports the last one the server sent — see the parser.
        var connection = new EventSourceConnection(this, _engine, _cancellation, _options.MaxResponseBytes, LastEventId);
        _connection = connection;
        _state.RegisterEventSource(connection);
        connection.Start(client, BuildRequest(), policy, observation);
    }

    /// <summary>
    /// The request the constructor built, plus the <c>Last-Event-ID</c> header a reconnect adds — steps 8 to
    /// 13 of the constructor and step 5 of reestablishing the connection.
    /// </summary>
    /// <remarks>
    /// The cache mode the standard sets (<c>no-store</c>) has nothing to act on here — Jint has no HTTP cache
    /// — so it is expressed on the wire as <c>Cache-Control: no-cache</c>, which is what a browser sends for
    /// an event stream and what keeps an intermediary from replaying one. A <c>Last-Event-ID</c> whose value
    /// is not a valid header value is dropped rather than sent: it comes from the server, and the alternative
    /// is letting a server's own <c>id</c> field shape the next request's headers.
    /// </remarks>
    private FetchRequestSnapshot BuildRequest()
    {
        var headers = new List<HeaderEntry>
        {
            new("accept", "text/event-stream"),
            new("cache-control", "no-cache"),
        };

        if (LastEventId.Length != 0 && HeaderList.IsValue(LastEventId))
        {
            headers.Add(new HeaderEntry("last-event-id", LastEventId));
        }

        return new FetchRequestSnapshot
        {
            Method = "GET",
            Url = Url,
            Headers = headers,
            Body = null,
            Redirect = JsRequest.RedirectFollow,
        };
    }

    /// <summary>
    /// Constructor step 15 for a <c>data:</c> URL. https://fetch.spec.whatwg.org/#main-fetch sends one to
    /// https://fetch.spec.whatwg.org/#scheme-fetch whatever the request's mode — <c>cors</c>, here — and the
    /// <c>data</c> arm answers it from the URL itself; see <see cref="DataUrlFetch"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Before every network check, as <c>fetch</c> and <c>XMLHttpRequest</c> take it.</b> No request is made,
    /// so <c>AllowedSchemes</c>, the host's <c>UrlFilter</c>, an <c>HttpClient</c> and a <c>FetchObserver</c>
    /// have nothing to decide or to see, and nothing is counted against <c>MaxConcurrentRequests</c>, which
    /// bounds the sockets streams hold. What does bound it is <c>MaxResponseBytes</c>, on the decoded body:
    /// the whole body is in hand, so unlike a network stream it is bounded whole rather than one event at a
    /// time, and a script cannot escape the host's ceiling by inlining a stream.
    /// </para>
    /// <para>
    /// <b>The response is processed exactly as a network one is.</b> The arm's 200 carries the URL's own MIME
    /// type, so a <c>text/event-stream</c> one is announced and interpreted line by line and any other type
    /// fails the connection. A URL the processor refuses, or a body over the limit, is a network error every
    /// later attempt would meet again — the case step 15.ii lets a user agent fail rather than reestablish,
    /// "unless the user agent knows that to be futile". The end of a body is not a network error, so it
    /// reestablishes the connection: the same URL replays every reconnection time, honouring <c>retry</c>, and
    /// the delay is the timer-queue task every reconnection waits on, so an engine nobody pumps replays nothing.
    /// </para>
    /// <para>
    /// <b>The engine's cancellation ends it silently</b>, as it ends a network connection — here and at every
    /// task of the pass — so a host's token stops the replay rather than a listener throwing out of the pump
    /// every reconnection time.
    /// </para>
    /// </remarks>
    private void ConnectDataUrl()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        if (!DataUrlFetch.TryBuild(Url, "GET", _options.MaxResponseBytes, out var response, out _))
        {
            FailConnectionLater();
            return;
        }

        ProcessLocalResponse(in response);
    }

    /// <summary>
    /// Constructor step 15 for a <c>blob:</c> URL. A blob URL is same-origin with the environment that minted
    /// it, so https://fetch.spec.whatwg.org/#main-fetch sends the <c>cors</c>-mode request to
    /// https://fetch.spec.whatwg.org/#scheme-fetch, and the <c>blob</c> arm answers it from the blob URL store;
    /// see <see cref="BlobUrlFetch"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The store is asked on every connection, and the first one is inside the constructor.</b> A revoke on
    /// the line after <c>new EventSource(url)</c> therefore does not take the first pass away, as it does not for
    /// a <c>Request</c> built before the revoke; and a reconnection asks again, so a URL revoked since is the arm's
    /// network error. That error, like a URL that never named an entry, is one every later attempt would meet
    /// again — a store never mints the same URL twice — so it fails the connection rather than reestablishing
    /// it, which step 15.ii allows "if the user agent knows that to be futile". It also means the source holds
    /// no blob between passes: revoking the URL is what lets the bytes go, as it is for every other holder.
    /// </para>
    /// <para>
    /// <b>The store is the engine's own, and nothing else is consulted</b>, exactly as for <c>fetch</c> and
    /// <c>XMLHttpRequest</c>: every realm of this engine resolves against it, a URL another engine minted — a
    /// worker's or a parent's — names nothing here, and a URL naming another origin can never match an entry,
    /// since this engine mints only <c>blob:null/…</c>.
    /// </para>
    /// <para>
    /// <b>Bounded by <c>MaxResponseBytes</c> on the blob's bytes, which <c>fetch</c>'s arm is not.</b> A
    /// <c>fetch</c> hands the blob back as a body a script already held; here it is parsed whole on the engine
    /// thread, with no per-event cap, and replayed every reconnection time, so this is the bound a network
    /// stream's events have — the one a <c>data:</c> URL's decoded body already answers to. The request is
    /// always <c>GET</c> and carries no <c>Range</c>, so the arm's other two failures cannot arise.
    /// </para>
    /// </remarks>
    private void ConnectBlobUrl()
    {
        if (_cancellation.IsCancellationRequested)
        {
            return;
        }

        var blob = _state.BlobUrls.Resolve(Url);
        if (blob is null
            || blob.Data.Length > _options.MaxResponseBytes
            || !BlobUrlFetch.TryBuild(blob, "GET", rangeHeader: null, out var response))
        {
            FailConnectionLater();
            return;
        }

        ProcessLocalResponse(in response);
    }

    /// <summary>
    /// Processes the response one of the two local arms built, exactly as a network one is: a
    /// <c>text/event-stream</c> body is parsed and delivered as a <see cref="LocalSchemePass"/>, and any other
    /// type fails the connection.
    /// </summary>
    private void ProcessLocalResponse(in LocalSchemeResponse response)
    {
        if (!IsEventStream(response.Headers))
        {
            FailConnectionLater();
            return;
        }

        // The body is within MaxResponseBytes already, and UTF-8 never decodes to more characters than it has
        // bytes, so no event can outgrow the cap a network stream's parser is given: this one needs none.
        var parser = new EventStreamParser(long.MaxValue, LastEventId);
        var messages = new List<EventStreamMessage>();
        parser.Feed(response.Body.GetValueOrDefault().Span, messages);

        new LocalSchemePass(this, parser.TakeReconnectionTime(), messages).Post();
    }

    /// <summary>
    /// Whether the arm's <c>Content-Type</c> is <c>text/event-stream</c>, compared by essence as a network
    /// response's is — so a parameter changes nothing.
    /// </summary>
    private static bool IsEventStream(List<HeaderEntry> headers)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.LowerName, "content-type", StringComparison.Ordinal))
            {
                return MimeType.Parse(header.Value) is { Essence: EventSourceConnection.EventStreamMimeType };
            }
        }

        return false;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#announce-the-connection — "queue a task
    /// which, if the readyState attribute is set to a value other than CLOSED, sets the readyState attribute
    /// to OPEN and fires an event named open".
    /// </summary>
    internal void AnnounceConnection(EventSourceConnection connection)
    {
        if (!ReferenceEquals(_connection, connection))
        {
            return;
        }

        Announce();
    }

    /// <summary>The task <see cref="AnnounceConnection"/> describes, for whichever lane queued it.</summary>
    private void Announce()
    {
        if (ReadyState == Closed)
        {
            return;
        }

        ReadyState = Open;
        FireEvent(_openEventName);
    }

    /// <summary>
    /// Step 8 of https://html.spec.whatwg.org/multipage/server-sent-events.html#dispatchMessage, for every
    /// event one chunk of the stream produced: "queue a task which, if the readyState attribute is set to a
    /// value other than CLOSED, dispatches the newly created event at the EventSource object".
    /// </summary>
    /// <remarks>
    /// One job per chunk rather than one per event, which is what keeps the events of a chunk in order and
    /// contiguous. The readyState is re-read for each of them, because a listener may call <c>close()</c>
    /// halfway through — and then the rest are not dispatched, exactly as separate tasks would behave. Step 1
    /// still happens for those: the last event ID string is set as each event is reached, so a reconnect
    /// resumes from the last <c>id</c> the server sent rather than from the last one a listener saw.
    /// </remarks>
    internal void DeliverMessages(EventSourceConnection connection, long? reconnectionTime, List<EventStreamMessage> messages, string origin)
    {
        if (!ReferenceEquals(_connection, connection))
        {
            return;
        }

        Dispatch(reconnectionTime, messages, origin);
    }

    /// <summary>The tasks <see cref="DeliverMessages"/> describes, for whichever lane queued them.</summary>
    private void Dispatch(long? reconnectionTime, List<EventStreamMessage> messages, string origin)
    {
        if (reconnectionTime is { } milliseconds)
        {
            ReconnectionTime = milliseconds;
        }

        var originValue = JsString.Create(origin);

        foreach (var message in messages)
        {
            // Step 1: "set the last event ID string of the event source to the value of the last event ID
            // buffer".
            LastEventId = message.LastEventId;

            if (ReadyState == Closed)
            {
                continue;
            }

            var messageEvent = _realm.Intrinsics.MessageEvent.CreateTrustedMessageEvent(
                JsString.Create(message.Type),
                JsString.Create(message.Data),
                originValue,
                JsString.Create(message.LastEventId));

            DispatchEvent(messageEvent);
        }
    }

    /// <summary>
    /// What the engine thread does once a connection has ended, chosen by how it ended.
    /// </summary>
    internal void OnConnectionEnded(EventSourceConnection connection, EventSourceOutcome outcome)
    {
        if (!ReferenceEquals(_connection, connection))
        {
            // Superseded by a later connection, which can only happen if something ended this one first.
            return;
        }

        _connection = null;

        switch (outcome)
        {
            case EventSourceOutcome.Fail:
                FailConnection();
                break;
            case EventSourceOutcome.Reestablish:
                ReestablishConnection();
                break;
            default:
                // Abandoned: close(), a restore or the engine's own cancellation. Nothing is fired.
                break;
        }
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#fail-the-connection — "queue a task
    /// which, if the readyState attribute is set to a value other than CLOSED, sets the readyState attribute
    /// to CLOSED and fires an event named error at the EventSource object. Once the user agent has failed the
    /// connection, it does not attempt to reconnect."
    /// </summary>
    private void FailConnection()
    {
        if (ReadyState == Closed)
        {
            return;
        }

        ReadyState = Closed;
        CancelReconnectTimer();
        FireEvent(_errorEventName);
    }

    /// <summary>
    /// The same, queued: the failures the engine detects before anything is sent — a URL the policy refuses,
    /// too many streams already open, a client the host would not supply — happen inside the constructor,
    /// where the standard's failures are all in a task rather than in the caller's stack.
    /// </summary>
    private void FailConnectionLater() => _engine.AddToEventLoop(FailConnection, EventLoopJobKind.Task);

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#reestablish-the-connection: set
    /// CONNECTING and fire <c>error</c>, wait the reconnection time, then fetch again unless something closed
    /// the source in the meantime.
    /// </summary>
    /// <remarks>
    /// The delay is armed <i>before</i> the <c>error</c> event rather than after it, which is both what the
    /// standard describes — the wait runs in parallel with that task, not after it — and what keeps a
    /// listener that throws from cancelling a reconnection the object has already committed to.
    /// </remarks>
    private void ReestablishConnection()
    {
        if (ReadyState == Closed)
        {
            return;
        }

        ReadyState = Connecting;

        if (!TryScheduleReconnect())
        {
            // The engine's timer queue is full, so there is nothing to hold the delay. Failing is the honest
            // answer: the alternative is an object that says CONNECTING forever.
            FailConnection();
            return;
        }

        FireEvent(_errorEventName);
    }

    private bool TryScheduleReconnect()
    {
        var timers = _state.Timers;
        if (timers is null || timers.Count >= timers.MaxActiveTimers)
        {
            return false;
        }

        var entry = new TimerEntry(
            timers,
            _realm,
            new ReconnectAlgorithm(this),
            [],
            ReconnectionTime,
            repeat: false,
            _engine.CaptureEventLoopRegistration());

        _reconnectTimerId = timers.Schedule(entry);
        return true;
    }

    private void CancelReconnectTimer()
    {
        if (_reconnectTimerId == 0)
        {
            return;
        }

        _state.Timers?.Cancel(_reconnectTimerId);
        _reconnectTimerId = 0;
    }

    /// <summary>
    /// The last task of reestablishing the connection: "if the EventSource object's readyState attribute is
    /// not set to CONNECTING, then return … fetch request and process the response".
    /// </summary>
    private void Reconnect()
    {
        _reconnectTimerId = 0;

        if (ReadyState != Connecting)
        {
            return;
        }

        Connect();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#dom-eventsource-close — "must abort any
    /// instances of the fetch algorithm started for this EventSource object, and must set the readyState
    /// attribute to CLOSED". No event is fired, and nothing reconnects.
    /// </summary>
    internal void Close()
    {
        ReadyState = Closed;
        CancelReconnectTimer();

        var connection = _connection;
        if (connection is null)
        {
            return;
        }

        _connection = null;
        _state.UnregisterEventSource(connection);
        connection.Abandon();
    }

    /// <summary>
    /// The fence a <c>RestoreGlobalSnapshot</c> puts up, reached from the connection the engine state is
    /// abandoning. The socket is already being let go; this is the object's own half of it.
    /// </summary>
    /// <remarks>
    /// The source ends up <c>CLOSED</c> with no <c>error</c> event, which is the same shape <c>close()</c>
    /// has and the only honest one: the evaluation cycle those listeners belonged to has ended, so there is
    /// nobody left to tell.
    /// </remarks>
    internal void AbandonConnection(EventSourceConnection connection)
    {
        if (!ReferenceEquals(_connection, connection))
        {
            return;
        }

        _connection = null;
        ReadyState = Closed;

        // The queue this id belongs to has been cleared by the same restore; forgetting it keeps a later
        // close() from cancelling an id another timer has since been given.
        _reconnectTimerId = 0;
    }

    /// <summary>
    /// Runs an engine-thread job in the realm this object was created in.
    /// </summary>
    /// <remarks>
    /// Every task a connection or a local-scheme pass queues comes through here: one running on a later turn
    /// would otherwise run its listeners under whatever realm happened to be ambient. A listener that throws
    /// erupts from whatever is pumping the event loop, which is the contract every event Jint fires already
    /// has — see <c>JsEventTarget</c>. There is no promise here to reject it into.
    /// </remarks>
    internal void RunInRealm(Action job)
    {
        var entered = !ReferenceEquals(_engine.Realm, _realm);
        if (entered)
        {
            _engine.EnterExecutionContext(_realm.GlobalEnv, _realm.GlobalEnv, _realm, privateEnvironment: null, strict: _engine.Options.Strict);
        }

        try
        {
            job();
        }
        finally
        {
            if (entered)
            {
                _engine.LeaveExecutionContext();
            }
        }
    }

    /// <summary>
    /// One pass of a <c>data:</c> URL's or a <c>blob:</c> URL's body, delivered as the three tasks a network
    /// connection queues for one response: the announcement, the events, and the end of the body, which
    /// reestablishes the connection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each task is queued by the one before it, once it has run</b>, rather than all three at once. The
    /// engine's one queue is also its microtask queue, so a reaction a listener queues then runs before the next
    /// of these tasks — the checkpoint a task ends with — instead of after the whole pass.
    /// </para>
    /// <para>
    /// <b>Nothing holds a pass but the queue it is on.</b> Every task first asks whether the source is still
    /// open, so <c>close()</c> — from a listener or between two tasks — ends it without queueing another; and
    /// each carries the generation it was started in, so a <c>RestoreGlobalSnapshot</c> discards the one
    /// already queued, exactly as it discards a connection's.
    /// </para>
    /// </remarks>
    private sealed class LocalSchemePass
    {
        private const int AnnounceStep = 0;
        private const int DispatchStep = 1;
        private const int EndOfBodyStep = 2;

        private readonly JsEventSource _source;
        private readonly long? _reconnectionTime;
        private readonly List<EventStreamMessage> _messages;

        /// <summary>The evaluation cycle the pass was started in; every task it queues carries it.</summary>
        private readonly int _generation;

        private int _step = AnnounceStep;

        internal LocalSchemePass(JsEventSource source, long? reconnectionTime, List<EventStreamMessage> messages)
        {
            _source = source;
            _reconnectionTime = reconnectionTime;
            _messages = messages;
            _generation = source._engine.EventLoopGeneration;
        }

        /// <summary>
        /// Queues the next task, without an allocation budget — a source that replays for as long as the script
        /// leaves it open must not accumulate against one operation's, as a network stream's frames do not.
        /// </summary>
        internal void Post() => _source._engine.AddToEventLoop(() => _source.RunInRealm(Run), _generation, EventLoopJobKind.Task);

        private void Run()
        {
            if (_source.ReadyState == Closed || _source._cancellation.IsCancellationRequested)
            {
                return;
            }

            switch (_step)
            {
                case AnnounceStep:
                    _source.Announce();

                    // A body that completed no event and set no reconnection time — a comment, or half a line —
                    // has nothing to dispatch, as a network chunk that completed nothing queues nothing.
                    _step = _messages.Count != 0 || _reconnectionTime is not null ? DispatchStep : EndOfBodyStep;
                    break;

                case DispatchStep:
                    // The origin of a data: URL is opaque, which serializes as "null"; a blob: URL's is the one
                    // its path names, which for every URL this engine mints is "null" too.
                    _source.Dispatch(_reconnectionTime, _messages, _source.Url.SerializeOrigin());
                    _step = EndOfBodyStep;
                    break;

                default:
                    // processResponseEndOfBody: "if res is not a network error, then reestablish the connection".
                    _source.ReestablishConnection();
                    return;
            }

            if (_source.ReadyState != Closed)
            {
                Post();
            }
        }
    }

    /// <summary>
    /// What the reconnect delay's timer runs.
    /// </summary>
    /// <remarks>
    /// An <see cref="ICallable"/> rather than a <c>ClrFunction</c>, so that a reconnection creates no
    /// JavaScript function object for something no script can reach — the timer queue is the only caller.
    /// </remarks>
    private sealed class ReconnectAlgorithm : ICallable
    {
        private readonly JsEventSource _source;

        internal ReconnectAlgorithm(JsEventSource source)
        {
            _source = source;
        }

        public JsValue Call(JsValue thisObject, params JsCallArguments arguments)
        {
            _source.Reconnect();
            return JsValue.Undefined;
        }
    }
}
#endif

#if NET8_0_OR_GREATER
using System.Net.Http;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;
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

    private EventSourceConnection? _connection;

    /// <summary>The id of the timer holding the reconnect delay, or zero when none is pending.</summary>
    private int _reconnectTimerId;

    internal JsEventSource(Engine engine, Realm realm, WebApiEngineState state, UrlRecord url, bool withCredentials)
        : base(engine, realm)
    {
        _state = state;
        _options = state.FetchOptions!;
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
        var urlFilter = _options.UrlFilter ?? (static _ => true);

        var policy = new FetchPolicy
        {
            AllowedSchemes = [.. _options.AllowedSchemes],
            UrlFilter = urlFilter,
            MaxResponseBytes = _options.MaxResponseBytes,
            MaxRedirects = _options.MaxRedirects,
        };

        // The first hop's check runs on the engine thread, so a refused URL never reaches a socket. Every
        // redirect hop is re-checked inside the transport, and every reconnect comes back through here.
        if (!policy.Allows(Url, out _))
        {
            FailConnectionLater();
            return;
        }

        // Counted per engine and per kind: a host's MaxConcurrentRequests bounds the fetches in flight and,
        // separately, the event streams open, because a stream holds its socket for as long as it lives.
        if (_state.ActiveEventSourceCount >= _options.MaxConcurrentRequests)
        {
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
            FailConnectionLater();
            return;
        }

        // The new stream's last event ID buffer starts as this object's last event ID string, so an event
        // that carries no id of its own still reports the last one the server sent — see the parser.
        var connection = new EventSourceConnection(this, _engine, _realm, _options.MaxResponseBytes, LastEventId);
        _connection = connection;
        _state.RegisterEventSource(connection);
        connection.Start(client, BuildRequest(), policy);
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
    /// https://html.spec.whatwg.org/multipage/server-sent-events.html#announce-the-connection — "queue a task
    /// which, if the readyState attribute is set to a value other than CLOSED, sets the readyState attribute
    /// to OPEN and fires an event named open".
    /// </summary>
    internal void AnnounceConnection(EventSourceConnection connection)
    {
        if (!ReferenceEquals(_connection, connection) || ReadyState == Closed)
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
    private void FailConnectionLater() => _engine.AddToEventLoop(FailConnection);

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
            new ReconnectAlgorithm(this),
            [],
            ReconnectionTime,
            repeat: false,
            _engine.EventLoopGeneration);

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

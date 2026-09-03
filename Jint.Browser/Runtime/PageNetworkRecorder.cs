using System.Globalization;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Runtime;

#pragma warning disable JINT0002 // FetchObserver is the engine's own network seam; watching a page's requests is what it is for.

/// <summary>
/// The page's network log: one <see cref="PageRequest"/> per request the page makes, whether the page's own
/// script asked for it or a navigation did.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every callback here runs on a transport thread</b>, so nothing in it may touch the engine or the DOM.
/// It writes into a bounded list under a lock, and — when something is listening — tells that listener on the
/// same thread; the correlation from a hop to the request it belongs to is the observer's own
/// <c>FetchRequestId</c>, which is stable across a redirect chain.
/// </para>
/// <para>
/// <b>The log spans the page rather than the document.</b> A navigation replaces the engine, and with it
/// every request that engine had in flight, but the entries stay: a caller reading
/// <see cref="Page.Requests"/> after a redirect chain wants to see the chain. It is ring-bounded by
/// <c>BrowserOptions.MaxRecordedEvents</c> for the reason the error and console logs are — a page in a loop
/// can fetch without limit.
/// </para>
/// <para>
/// <b>One observer serves both initiators.</b> It is installed as <c>Options.WebApi.Fetch.Observer</c> on
/// every page engine, which covers <c>fetch</c>, <c>XMLHttpRequest</c> and a worker's loads; the document
/// fetch and every subresource report through the same instance, because a navigation and a parse both run
/// outside any engine.
/// </para>
/// <para>
/// <b>It watches by default and intercepts only for a listener.</b> With no
/// <see cref="Listener"/> registered it answers every hop with <see langword="null"/> and copies no bytes,
/// which is what a page with no protocol client attached costs. A listener is what turns on the request
/// rewriting, the refusals and the bounded body capture below, and there is exactly one — the page's target,
/// because a page has one target.
/// </para>
/// <para>
/// <b>The captured bodies are a ring, not a log.</b> <c>BrowserOptions.MaxCapturedResponseBytes</c> bounds
/// the total held for the whole page and the oldest capture is dropped to stay under it, so a client that
/// asks for a body it waited too long for is told there is none rather than the page being the memory it
/// would have taken to promise otherwise.
/// </para>
/// </remarks>
internal sealed class PageNetworkRecorder : FetchObserver
{
    /// <summary>The most bytes of a request body copied for a client to read back.</summary>
    /// <remarks>
    /// Chrome's own <c>maxPostDataSize</c> default, and the reason it is not the response bound: the copy is
    /// made while the request is being sent, on the transport thread, for every request the page makes.
    /// </remarks>
    private const int MaxPostDataBytes = 64 * 1024;

    private readonly Queue<PageRequest> _requests = new();
    private readonly Dictionary<long, PageRequest> _live = [];
    private readonly Dictionary<long, Declaration> _declared = [];
    private readonly Dictionary<long, Entry> _entries = [];
    private readonly Dictionary<string, Entry> _byRequestId = new(StringComparer.Ordinal);
    private readonly Queue<Entry> _captureOrder = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly int _max;
    private readonly long _maxCaptureBytes;
    private readonly Func<string> _loaderId;
    private readonly Func<string> _documentUrl;

    private long _syntheticId;
    private long _capturedBytes;
    private volatile IPageNetworkListener? _listener;
    private volatile bool _captureBodies;

    private long _lastChange = System.Diagnostics.Stopwatch.GetTimestamp();

    /// <summary>Builds the log for one page.</summary>
    /// <param name="max">How many entries the ring holds.</param>
    /// <param name="maxCaptureBytes">How many bytes of captured bodies the page may hold at once.</param>
    /// <param name="loaderId">The document a subresource belongs to, read when a request goes out.</param>
    /// <param name="documentUrl">The URL of that document, read at the same moment.</param>
    /// <remarks>
    /// The two delegates read <see cref="Page"/>'s own volatile fields rather than taking the page, so
    /// nothing here can reach a member that belongs to the page's thread.
    /// </remarks>
    internal PageNetworkRecorder(int max, long maxCaptureBytes, Func<string> loaderId, Func<string> documentUrl)
    {
        _max = max;
        _maxCaptureBytes = maxCaptureBytes;
        _loaderId = loaderId;
        _documentUrl = documentUrl;
    }

    /// <summary>The one thing that hears about this page's requests, or <see langword="null"/>.</summary>
    /// <remarks>
    /// One at a time, because a page has one target. Setting it does not by itself start the body capture;
    /// <see cref="CaptureBodies"/> is what does, and the <c>Network</c> domain turns that on when a client
    /// enables it — which is the protocol's own rule, and what keeps a page nobody is watching free of the
    /// copies.
    /// </remarks>
    internal IPageNetworkListener? Listener
    {
        get => _listener;
        set => _listener = value;
    }

    /// <summary>Whether response and request bodies are copied so a client can read them back.</summary>
    internal bool CaptureBodies
    {
        get => _captureBodies;
        set
        {
            _captureBodies = value;

            if (!value)
            {
                DropCaptures();
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Read once per request by the transport, which is why it can answer zero for a page with nothing
    /// watching and a bound for one being recorded.
    /// </remarks>
    public override int RequestBodyPreviewBytes => _captureBodies ? MaxPostDataBytes : 0;

    /// <summary>How many requests are still in flight, and when the last one stopped being.</summary>
    /// <remarks>
    /// What "the network is quiet" is computed from. Both are written from a transport thread under the same
    /// lock the log is, and read from wherever the quiet period is being timed; neither touches an engine.
    /// </remarks>
    internal (int InFlight, long LastChangeTicks) Activity
    {
        get
        {
            lock (_gate)
            {
                return (_live.Count, _lastChange);
            }
        }
    }

    /// <summary>Every request the page has made, oldest first.</summary>
    internal IReadOnlyList<PageRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return _requests.ToArray();
            }
        }
    }

    /// <summary>
    /// Starts an observation whose entries this log should file under <paramref name="initiator"/> and
    /// <paramref name="kind"/> rather than under what the transport can work out for itself.
    /// </summary>
    /// <param name="initiator">Who asked for the request.</param>
    /// <param name="kind">What it is for.</param>
    /// <param name="requestId">
    /// The identifier a client should address it by, or <see langword="null"/> to mint one. A navigation
    /// passes its <c>loaderId</c>, because that is what Chrome does and what every client reads as "this is
    /// the document's own request".
    /// </param>
    /// <remarks>
    /// The transport knows only three initiators — a script's <c>fetch</c>, its <c>XMLHttpRequest</c>, and
    /// one the host started — and every load the page makes for a document or a subresource is the third.
    /// Which <em>kind</em> of host load it is is something only the caller knows, so the caller says, once,
    /// when it creates the observation.
    /// </remarks>
    internal FetchObservation? Observe(RequestInitiator initiator, PageRequestKind kind, string? requestId = null)
    {
        var observation = FetchObservation.Create(this, initiator == RequestInitiator.Script ? FetchInitiator.Script : FetchInitiator.Host);

        if (observation is not null)
        {
            lock (_gate)
            {
                _declared[observation.Id.Value] = new Declaration(initiator, kind, requestId);
            }
        }

        return observation;
    }

    /// <summary>
    /// Records a reference the page saw and deliberately did not follow — an image, a frame's document, a
    /// URL a filter refused.
    /// </summary>
    internal void RecordNotFetched(string url, RequestInitiator initiator, PageRequestKind kind, string reason)
    {
        long id;
        lock (_gate)
        {
            id = -System.Threading.Interlocked.Increment(ref _syntheticId);
            _requests.Enqueue(new PageRequest(id, initiator, url, "GET", reason));
            Trim();
        }

        if (_listener is not { } listener)
        {
            return;
        }

        var request = new PageNetworkRequest(
            RequestId: Identify(id),
            Kind: kind,
            Initiator: initiator,
            LoaderId: _loaderId(),
            DocumentUrl: _documentUrl(),
            Url: url,
            Method: "GET",
            Headers: [],
            PostData: null,
            HasPostData: false,
            RedirectCount: 0,
            RedirectResponse: null);

        Safely(() => listener.NotFetched(request, reason));
    }

    /// <summary>The captured body of one request, or <see langword="null"/> when none is held.</summary>
    /// <param name="requestId">The identifier a client addressed the request by.</param>
    internal CapturedBody? Body(string requestId)
    {
        lock (_gate)
        {
            if (!_byRequestId.TryGetValue(requestId, out var entry) || entry.Body is null)
            {
                return null;
            }

            return new CapturedBody(entry.Body, entry.MimeType, entry.Charset);
        }
    }

    /// <summary>The captured request body of one request, or <see langword="null"/> when it carried none.</summary>
    /// <param name="requestId">The identifier a client addressed the request by.</param>
    internal string? PostData(string requestId)
    {
        lock (_gate)
        {
            return _byRequestId.TryGetValue(requestId, out var entry) ? entry.PostData : null;
        }
    }

    /// <summary>Whether the log has ever seen a request under <paramref name="requestId"/>.</summary>
    internal bool Knows(string requestId)
    {
        lock (_gate)
        {
            return _byRequestId.ContainsKey(requestId);
        }
    }

    /// <summary>
    /// Records a hop the transport is about to send, and asks the listener what should happen to it.
    /// </summary>
    /// <remarks>
    /// The one callback that may answer, and the one that may take time: a protocol client's
    /// <c>Fetch.requestPaused</c> is a wait inside this call. It holds the transport thread this request is
    /// being sent on and nothing else, and the fetch's own cancellation token is what bounds it.
    /// </remarks>
    public override async ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
    {
        var declaration = Declared(request.Id.Value);
        var initiator = declaration?.Initiator
            ?? (request.Initiator == FetchInitiator.Host ? RequestInitiator.Document : RequestInitiator.Script);

        var kind = declaration?.Kind ?? request.Initiator switch
        {
            FetchInitiator.XmlHttpRequest => PageRequestKind.Xhr,
            FetchInitiator.Host => PageRequestKind.Document,
            _ => PageRequestKind.Fetch,
        };

        var entry = Hop(request.Id.Value, initiator, kind, declaration?.RequestId, request.Url, request.Method, request.RedirectCount);

        if (_listener is not { } listener)
        {
            return null;
        }

        var headers = new PageHeader[request.Headers.Count];
        for (var i = 0; i < headers.Length; i++)
        {
            headers[i] = new PageHeader(request.Headers[i].Name, request.Headers[i].Value);
        }

        var postData = Capture(entry, request);

        var hop = new PageNetworkRequest(
            RequestId: entry.RequestId,
            Kind: kind,
            Initiator: initiator,
            LoaderId: entry.LoaderId,
            DocumentUrl: entry.DocumentUrl,
            Url: request.Url.AbsoluteUri,
            Method: request.Method,
            Headers: headers,
            PostData: postData,
            HasPostData: request.HasBody,
            RedirectCount: request.RedirectCount,
            RedirectResponse: Describe(entry.RequestId, request.RedirectResponse));

        entry.LastHop = hop;

        PageNetworkDecision decision;
        try
        {
            decision = await listener.RequestWillBeSentAsync(hop, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // a watcher that failed must not decide the fetch; see IPageNetworkListener
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }

        return Translate(entry, decision);
    }

    /// <inheritdoc />
    public override void OnResponse(ObservedFetchResponse response)
    {
        if (response.IsRedirect)
        {
            // The hop after it is what the log should show; a redirect's own status is visible as the
            // RedirectCount of the entry rather than as a status that will be overwritten in a moment.
            return;
        }

        var headers = new PageHeader[response.Headers.Count];
        for (var i = 0; i < headers.Length; i++)
        {
            headers[i] = new PageHeader(response.Headers[i].Name, response.Headers[i].Value);
        }

        Find(response.Id.Value)?.Responded(response.Status, response.StatusText, headers);

        Entry? entry;
        PageNetworkResponse described;

        lock (_gate)
        {
            if (!_entries.TryGetValue(response.Id.Value, out entry))
            {
                return;
            }

            described = Describe(entry.RequestId, response)!;
            entry.MimeType = described.MimeType;
            entry.Charset = described.Charset;
            entry.Status = described.Status;
        }

        if (_listener is { } listener && entry.LastHop is { } hop)
        {
            Safely(() => listener.ResponseReceived(hop, described));
        }
    }

    /// <inheritdoc />
    public override void OnData(FetchRequestId id, ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length == 0)
        {
            return;
        }

        string? requestId = null;

        lock (_gate)
        {
            if (_entries.TryGetValue(id.Value, out var entry))
            {
                requestId = entry.RequestId;

                if (_captureBodies)
                {
                    Append(entry, chunk);
                }
            }
        }

        if (requestId is not null && _listener is { } listener)
        {
            var length = chunk.Length;
            Safely(() => listener.DataReceived(requestId, length));
        }
    }

    /// <inheritdoc />
    public override void OnCompleted(FetchRequestId id, long bodyLength)
    {
        var request = Find(id.Value);
        request?.Completed(bodyLength);

        string? requestId;
        lock (_gate)
        {
            requestId = _entries.TryGetValue(id.Value, out var entry) ? entry.RequestId : null;
            Seal(id.Value);
        }

        Retire(id.Value);

        if (requestId is not null && _listener is { } listener)
        {
            Safely(() => listener.LoadingFinished(requestId, bodyLength));
        }
    }

    /// <inheritdoc />
    public override void OnFailed(FetchRequestId id, string reason, Exception? exception)
    {
        var request = Find(id.Value);
        request?.Failure(reason);

        string? requestId;
        PageRequestKind kind;
        string errorText;
        string? blockedReason;

        lock (_gate)
        {
            if (_entries.TryGetValue(id.Value, out var entry))
            {
                requestId = entry.RequestId;
                kind = entry.Kind;
                errorText = entry.ErrorText ?? NetworkError(reason, exception);
                blockedReason = entry.BlockedReason;
            }
            else
            {
                requestId = null;
                kind = PageRequestKind.Other;
                errorText = "net::ERR_FAILED";
                blockedReason = null;
            }

            Seal(id.Value);
        }

        Retire(id.Value);

        if (requestId is not null && _listener is { } listener)
        {
            var canceled = exception is OperationCanceledException || errorText == "net::ERR_ABORTED";
            Safely(() => listener.LoadingFailed(requestId, kind, errorText, canceled, blockedReason));
        }
    }

    /// <summary>
    /// The <c>net::ERR_*</c> string a client parses, for a failure nobody named one for.
    /// </summary>
    /// <remarks>
    /// A reason a listener chose carries its own code and never reaches here; this is the transport's own
    /// words, which are a sentence rather than a code. Four cases are told apart because a client acts on
    /// each differently, and everything else is the generic failure.
    /// </remarks>
    internal static string NetworkError(string reason, Exception? exception)
    {
        if (reason.Contains("net::ERR_", StringComparison.Ordinal))
        {
            var start = reason.IndexOf("net::ERR_", StringComparison.Ordinal);
            var end = start;
            while (end < reason.Length && (char.IsAsciiLetterOrDigit(reason[end]) || reason[end] == '_' || reason[end] == ':'))
            {
                end++;
            }

            return reason.Substring(start, end - start);
        }

        for (var inner = exception; inner is not null; inner = inner.InnerException)
        {
            if (inner is System.Net.Sockets.SocketException socket)
            {
                return socket.SocketErrorCode switch
                {
                    System.Net.Sockets.SocketError.HostNotFound
                        or System.Net.Sockets.SocketError.NoData
                        or System.Net.Sockets.SocketError.TryAgain => "net::ERR_NAME_NOT_RESOLVED",
                    System.Net.Sockets.SocketError.ConnectionRefused => "net::ERR_CONNECTION_REFUSED",
                    System.Net.Sockets.SocketError.TimedOut => "net::ERR_TIMED_OUT",
                    _ => "net::ERR_FAILED",
                };
            }
        }

        if (exception is OperationCanceledException || reason.Contains("cancel", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("abort", StringComparison.OrdinalIgnoreCase))
        {
            return "net::ERR_ABORTED";
        }

        if (reason.Contains("filter", StringComparison.OrdinalIgnoreCase))
        {
            return "net::ERR_BLOCKED_BY_CLIENT";
        }

        return "net::ERR_FAILED";
    }

    /// <summary>Turns a listener's decision into the engine's own interception, and remembers a failure.</summary>
    private FetchInterception? Translate(Entry entry, PageNetworkDecision decision)
    {
        switch (decision.Kind)
        {
            case PageNetworkDecisionKind.Fail:
                lock (_gate)
                {
                    // Kept so that OnFailed reports the code the client chose rather than deriving one from
                    // the sentence the transport will raise.
                    entry.ErrorText = decision.ErrorText;
                    entry.BlockedReason = decision.BlockedReason;
                }

                return FetchInterception.Fail(decision.ErrorText ?? "net::ERR_FAILED");

            case PageNetworkDecisionKind.Fulfill:
                return FetchInterception.Fulfill(
                    decision.Status,
                    ToFetchHeaders(decision.Headers),
                    decision.Body ?? [],
                    decision.StatusText);

            case PageNetworkDecisionKind.Continue:
                Uri? url = null;
                if (decision.Url is { Length: > 0 } rewritten && Uri.TryCreate(rewritten, UriKind.Absolute, out var parsed))
                {
                    url = parsed;
                }

                return FetchInterception.Continue(
                    url,
                    decision.Method,
                    ToFetchHeaders(decision.Headers),
                    decision.Body is { } body ? new ReadOnlyMemory<byte>(body) : null);

            default:
                return null;
        }
    }

    private static FetchHeader[]? ToFetchHeaders(IReadOnlyList<PageHeader>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        var converted = new FetchHeader[headers.Count];
        for (var i = 0; i < converted.Length; i++)
        {
            converted[i] = new FetchHeader(headers[i].Name, headers[i].Value);
        }

        return converted;
    }

    private static PageNetworkResponse? Describe(string requestId, ObservedFetchResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        var headers = new PageHeader[response.Headers.Count];
        string mime = "";
        string charset = "";

        for (var i = 0; i < headers.Length; i++)
        {
            headers[i] = new PageHeader(response.Headers[i].Name, response.Headers[i].Value);

            if (mime.Length == 0 && string.Equals(headers[i].Name, "content-type", StringComparison.OrdinalIgnoreCase))
            {
                var parsed = MimeType.Parse(headers[i].Value);
                mime = parsed?.Essence ?? "";
                charset = parsed?.GetParameter("charset") ?? "";
            }
        }

        return new PageNetworkResponse(
            requestId,
            response.Url.AbsoluteUri,
            response.Status,
            response.StatusText,
            headers,
            mime,
            charset,
            response.FromInterception);
    }

    /// <summary>Copies as much of the request body as the capture is allowed to keep.</summary>
    private string? Capture(Entry entry, ObservedFetchRequest request)
    {
        if (!_captureBodies || request.BodyPreview.Length == 0)
        {
            return entry.PostData;
        }

        // Text, because that is what the protocol's postData carries and what every client shows. Bytes that
        // are not text come back as the replacement character rather than as a refusal, which is what Chrome
        // does for a form posting binary too.
        var text = System.Text.Encoding.UTF8.GetString(request.BodyPreview.Span);

        lock (_gate)
        {
            entry.PostData = text;
        }

        return text;
    }

    private Entry Hop(long id, RequestInitiator initiator, PageRequestKind kind, string? requestId, Uri url, string method, int redirectCount)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(id, out var existing))
            {
                if (_live.TryGetValue(id, out var live))
                {
                    live.Hop(url.AbsoluteUri, method, redirectCount);
                }

                return existing;
            }

            var request = new PageRequest(id, initiator, url.AbsoluteUri, method);
            _live[id] = request;
            _lastChange = System.Diagnostics.Stopwatch.GetTimestamp();
            _requests.Enqueue(request);

            var entry = new Entry(requestId ?? Identify(id), kind)
            {
                // A navigation names its own identifier, and that identifier *is* its loaderId — the
                // document it is about to produce, not the one still showing, which is what the page's own
                // field still holds while the fetch is in flight.
                LoaderId = requestId ?? _loaderId(),
                DocumentUrl = requestId is null ? _documentUrl() : url.AbsoluteUri,
            };

            _entries[id] = entry;
            _byRequestId[entry.RequestId] = entry;
            _captureOrder.Enqueue(entry);

            Trim();
            return entry;
        }
    }

    /// <summary>The identifier a client addresses a request by when nothing else named it.</summary>
    /// <remarks>
    /// A navigation's is its <c>loaderId</c>, which is what makes a client recognise the document's own
    /// request; everything else gets the log's own number, which is unique for the process.
    /// </remarks>
    private static string Identify(long id) => id.ToString(CultureInfo.InvariantCulture);

    private Declaration? Declared(long id)
    {
        lock (_gate)
        {
            return _declared.TryGetValue(id, out var declaration) ? declaration : null;
        }
    }

    /// <summary>Drops the oldest entries past the ring's bound. Called with the gate held.</summary>
    private void Trim()
    {
        while (_requests.Count > _max)
        {
            var dropped = _requests.Dequeue();
            _live.Remove(dropped.Id);
            _declared.Remove(dropped.Id);

            if (_entries.Remove(dropped.Id, out var entry))
            {
                Forget(entry);
            }
        }

        while (_captureOrder.Count > _max)
        {
            Forget(_captureOrder.Dequeue());
        }

        while (_capturedBytes > _maxCaptureBytes && _captureOrder.Count > 0)
        {
            Forget(_captureOrder.Dequeue());
        }
    }

    /// <summary>Releases one entry's captured bytes and stops resolving its identifier. Gate held.</summary>
    private void Forget(Entry entry)
    {
        if (entry.Body is { } body)
        {
            _capturedBytes -= body.Length;
            entry.Body = null;
        }

        entry.Buffer = null;
        entry.PostData = null;
        _byRequestId.Remove(entry.RequestId);
    }

    /// <summary>Empties every capture, which disabling the domain does. Takes the gate.</summary>
    private void DropCaptures()
    {
        lock (_gate)
        {
            foreach (var entry in _byRequestId.Values.ToArray())
            {
                if (entry.Body is { } body)
                {
                    _capturedBytes -= body.Length;
                    entry.Body = null;
                }

                entry.Buffer = null;
            }
        }
    }

    /// <summary>Appends one chunk to a capture, refusing one that would pass the page's whole budget. Gate held.</summary>
    private void Append(Entry entry, ReadOnlySpan<byte> chunk)
    {
        if (entry.Truncated)
        {
            return;
        }

        var buffer = entry.Buffer ??= new MemoryStream();
        if (buffer.Length + chunk.Length > _maxCaptureBytes)
        {
            // A single body larger than the page's whole capture budget is not kept at all: keeping its first
            // half would answer getResponseBody with something that is not the response.
            entry.Truncated = true;
            entry.Buffer = null;
            return;
        }

        buffer.Write(chunk);
    }

    /// <summary>Turns a finished capture into the bytes a client may read back. Gate held.</summary>
    private void Seal(long id)
    {
        if (!_entries.TryGetValue(id, out var entry) || entry.Buffer is not { } buffer)
        {
            return;
        }

        entry.Body = buffer.ToArray();
        entry.Buffer = null;
        _capturedBytes += entry.Body.Length;

        while (_capturedBytes > _maxCaptureBytes && _captureOrder.Count > 0)
        {
            var oldest = _captureOrder.Peek();
            if (ReferenceEquals(oldest, entry))
            {
                break;
            }

            Forget(_captureOrder.Dequeue());
        }
    }

    private PageRequest? Find(long id)
    {
        lock (_gate)
        {
            return _live.TryGetValue(id, out var request) ? request : null;
        }
    }

    private void Retire(long id)
    {
        lock (_gate)
        {
            if (_live.Remove(id))
            {
                _lastChange = System.Diagnostics.Stopwatch.GetTimestamp();
            }

            _declared.Remove(id);
        }
    }

    /// <summary>Tells the listener, swallowing whatever it got wrong.</summary>
    private static void Safely(Action notify)
    {
        try
        {
            notify();
        }
#pragma warning disable CA1031 // see IPageNetworkListener: a transfer must not depend on a watcher
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>What a caller said about a request the transport could not have worked out for itself.</summary>
    private readonly record struct Declaration(RequestInitiator Initiator, PageRequestKind Kind, string? RequestId);

    /// <summary>One request's protocol-facing state, which outlives the request itself.</summary>
    /// <remarks>
    /// Separate from <see cref="PageRequest"/> because that type is the host's, published on
    /// <see cref="Page.Requests"/> and deliberately a summary; this is the identifier a client holds, the
    /// bytes it may ask for, and the failure it will be told about.
    /// </remarks>
    private sealed class Entry(string requestId, PageRequestKind kind)
    {
        internal string RequestId { get; } = requestId;

        internal PageRequestKind Kind { get; } = kind;

        internal string LoaderId { get; init; } = "";

        internal string DocumentUrl { get; init; } = "";

        internal string MimeType { get; set; } = "";

        internal string Charset { get; set; } = "";

        internal int Status { get; set; }

        internal string? PostData { get; set; }

        internal string? ErrorText { get; set; }

        internal string? BlockedReason { get; set; }

        internal bool Truncated { get; set; }

        internal MemoryStream? Buffer { get; set; }

        internal byte[]? Body { get; set; }

        /// <summary>The last hop that went out, which is what a response is reported against.</summary>
        internal PageNetworkRequest? LastHop { get; set; }
    }
}

/// <summary>One response body the log kept, and what it claims to be.</summary>
/// <param name="Bytes">The body exactly as it arrived.</param>
/// <param name="MimeType">The essence of the <c>Content-Type</c>, or the empty string.</param>
/// <param name="Charset">The <c>charset</c> parameter, or the empty string.</param>
internal readonly record struct CapturedBody(byte[] Bytes, string MimeType, string Charset);

#pragma warning restore JINT0002

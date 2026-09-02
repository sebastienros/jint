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
/// It writes into a bounded list under a lock and nothing else; the correlation from a hop to the request it
/// belongs to is the observer's own <c>FetchRequestId</c>, which is stable across a redirect chain.
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
/// every page engine, which covers <c>fetch</c>, <c>XMLHttpRequest</c>, <c>EventSource</c> and a worker's
/// loads; the document fetch reports through the same instance with
/// <see cref="RequestInitiator.Document"/>, because a navigation runs outside any engine.
/// </para>
/// </remarks>
internal sealed class PageNetworkRecorder : FetchObserver
{
    private readonly Queue<PageRequest> _requests = new();
    private readonly Dictionary<long, PageRequest> _live = [];
    private readonly Dictionary<long, RequestInitiator> _declared = [];
    private readonly System.Threading.Lock _gate = new();
    private readonly int _max;
    private long _syntheticId;

    private long _lastChange = System.Diagnostics.Stopwatch.GetTimestamp();

    internal PageNetworkRecorder(int max)
    {
        _max = max;
    }

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
    /// Starts an observation whose entries this log should file under <paramref name="initiator"/> rather
    /// than under what the transport can work out for itself.
    /// </summary>
    /// <remarks>
    /// The transport knows only two initiators — a script's own request and one the host started — and every
    /// load the page makes for a document is the second. Which <em>kind</em> of host load it is is something
    /// only the caller knows, so the caller says, once, when it creates the observation.
    /// </remarks>
    internal FetchObservation? Observe(RequestInitiator initiator)
    {
        var observation = FetchObservation.Create(this, initiator == RequestInitiator.Script ? FetchInitiator.Script : FetchInitiator.Host);

        if (observation is not null && initiator != RequestInitiator.Document)
        {
            lock (_gate)
            {
                _declared[observation.Id.Value] = initiator;
            }
        }

        return observation;
    }

    /// <summary>
    /// Records a reference the page saw and deliberately did not follow — an image, a frame's document, a
    /// URL a filter refused.
    /// </summary>
    internal void RecordNotFetched(string url, RequestInitiator initiator, string reason)
    {
        lock (_gate)
        {
            var id = -System.Threading.Interlocked.Increment(ref _syntheticId);
            _requests.Enqueue(new PageRequest(id, initiator, url, "GET", reason));
            Trim();
        }
    }

    /// <summary>
    /// Records a hop the transport is about to send, and answers nothing: this observer watches, it never
    /// intercepts. A host that wants to intercept installs its own through
    /// <c>BrowserOptions.ConfigureEngine</c>, which runs after this one is set and replaces it.
    /// </summary>
    public override ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
    {
        // The transport knows only that a document fetch is the host's and everything else is a script's; a
        // caller that said which kind of host load it was started is taken at its word.
        var initiator = Declared(request.Id.Value)
            ?? (request.Initiator == FetchInitiator.Host ? RequestInitiator.Document : RequestInitiator.Script);

        Hop(request.Id.Value, initiator, request.Url, request.Method, request.RedirectCount);
        return new ValueTask<FetchInterception?>((FetchInterception?) null);
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
    }

    /// <inheritdoc />
    public override void OnCompleted(FetchRequestId id, long bodyLength)
    {
        var request = Find(id.Value);
        request?.Completed(bodyLength);
        Retire(id.Value);
    }

    /// <inheritdoc />
    public override void OnFailed(FetchRequestId id, string reason, Exception? exception)
    {
        var request = Find(id.Value);
        request?.Failure(reason);
        Retire(id.Value);
    }

    private void Hop(long id, RequestInitiator initiator, Uri url, string method, int redirectCount)
    {
        lock (_gate)
        {
            if (_live.TryGetValue(id, out var existing))
            {
                existing.Hop(url.AbsoluteUri, method, redirectCount);
                return;
            }

            var request = new PageRequest(id, initiator, url.AbsoluteUri, method);
            _live[id] = request;
            _lastChange = System.Diagnostics.Stopwatch.GetTimestamp();
            _requests.Enqueue(request);
            Trim();
        }
    }

    private RequestInitiator? Declared(long id)
    {
        lock (_gate)
        {
            return _declared.TryGetValue(id, out var initiator) ? initiator : null;
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
}

#pragma warning restore JINT0002

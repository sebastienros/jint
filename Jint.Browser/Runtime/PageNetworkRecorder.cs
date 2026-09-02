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
    private readonly System.Threading.Lock _gate = new();
    private readonly int _max;

    internal PageNetworkRecorder(int max)
    {
        _max = max;
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
    /// Records a hop the transport is about to send, and answers nothing: this observer watches, it never
    /// intercepts. A host that wants to intercept installs its own through
    /// <c>BrowserOptions.ConfigureEngine</c>, which runs after this one is set and replaces it.
    /// </summary>
    public override ValueTask<FetchInterception?> OnRequestAsync(ObservedFetchRequest request, CancellationToken cancellationToken)
    {
        // The transport already knows which it is: a document fetch is started by the page and carries
        // FetchInitiator.Host, everything else is a script's own fetch, XMLHttpRequest or worker load.
        var initiator = request.Initiator == FetchInitiator.Host ? RequestInitiator.Document : RequestInitiator.Script;
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
            _requests.Enqueue(request);

            while (_requests.Count > _max)
            {
                var dropped = _requests.Dequeue();
                _live.Remove(dropped.Id);
            }
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
            _live.Remove(id);
        }
    }
}

#pragma warning restore JINT0002

namespace Jint.Browser.Runtime;

/// <summary>One registered route: which URLs it matches and who decides them.</summary>
/// <param name="Match">The host's predicate, run off the page's loop.</param>
/// <param name="Handler">The host's handler, run on the same thread as the predicate.</param>
internal sealed record PageRoute(Func<Uri, bool> Match, Func<RoutedRequest, Task> Handler);

/// <summary>The routes one page or one context registered, newest last.</summary>
/// <remarks>
/// Written from whichever thread calls <c>RouteAsync</c> and read by every request being routed, so it is a
/// copy-on-write array: a reader takes one snapshot and walks it without a lock, and a request already being
/// routed is decided by the routes that existed when it went out.
/// </remarks>
internal sealed class PageRouteTable
{
    private readonly System.Threading.Lock _gate = new();
    private PageRoute[] _routes = [];

    /// <summary>The routes as they stand, oldest first.</summary>
    internal PageRoute[] Routes => Volatile.Read(ref _routes);

    internal void Add(PageRoute route)
    {
        lock (_gate)
        {
            Volatile.Write(ref _routes, [.. _routes, route]);
        }
    }

    /// <summary>Removes every route registered with <paramref name="match"/>, and with <paramref name="handler"/> when one is named.</summary>
    internal void Remove(Func<Uri, bool> match, Func<RoutedRequest, Task>? handler)
    {
        lock (_gate)
        {
            Volatile.Write(ref _routes, [.. _routes.Where(route => !route.Match.Equals(match)
                || (handler is not null && !route.Handler.Equals(handler)))]);
        }
    }
}

/// <summary>
/// The host's in-process request routing for one page's network log: the page's routes first, then its
/// context's, newest first within each.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not a second interception.</b> A handler's answer is a <see cref="PageNetworkDecision"/>, the
/// type a protocol client's <c>Fetch</c> pause answers with, and <see cref="PageNetworkRecorder"/> is the one
/// place that turns either into the engine's own <c>FetchInterception</c> — so a fulfilled request is
/// reported, logged and delivered exactly as a fulfilled <c>Fetch.requestPaused</c> is.
/// </para>
/// <para>
/// <b>It runs after the context's <c>UrlFilter</c>, never instead of it.</b> The filter is the cheap
/// allow-list and has already refused the first hop before anything reaches the log; a continued request's
/// rewritten URL is re-checked by the transport exactly as a redirect target is.
/// </para>
/// </remarks>
internal sealed class PageRouter(PageRouteTable? page, PageRouteTable? context)
{
    private readonly PageRouteTable?[] _tables = [page, context];

    /// <summary>Whether anything is registered at all, which is what a request with no routes costs.</summary>
    internal bool HasRoutes
    {
        get
        {
            foreach (var table in _tables)
            {
                if (table?.Routes.Length > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Asks the matching routes about one hop; <see langword="null"/> when none decided it.</summary>
    /// <param name="hop">The hop, as the request log and any protocol client already settled it.</param>
    /// <param name="url">The hop's URL.</param>
    /// <param name="body">The request body preview the hop will carry.</param>
    /// <param name="cancellationToken">Cancelled when the fetch is abandoned or times out.</param>
    /// <remarks>
    /// <para>
    /// <b>Host code never runs on the page loop.</b> A script's <c>fetch</c> and a parser's subresource reach
    /// the observer synchronously on the loop thread, so the walk first moves to the thread pool when it is not
    /// already on it — which a transport continuation always is. A protocol pause needs no such hop because it
    /// only awaits; a host matcher or handler may block.
    /// </para>
    /// <para>
    /// Each handler gets its own <see cref="RoutedRequest"/>, sealed when its task completes, so a decision
    /// it takes too late throws rather than being silently ignored or deciding for the next route. A handler
    /// or a matcher that throws fails the request, because it is the callback that was asked to decide.
    /// </para>
    /// </remarks>
    internal async ValueTask<PageNetworkDecision?> RouteAsync(
        PageNetworkRequest hop,
        Uri url,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        if (!Thread.CurrentThread.IsThreadPoolThread)
        {
            return await Task.Run(() => WalkAsync(hop, url, body, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(false);
        }

        return await WalkAsync(hop, url, body, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<PageNetworkDecision?> WalkAsync(
        PageNetworkRequest hop,
        Uri url,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken)
    {
        foreach (var table in _tables)
        {
            if (table is null)
            {
                continue;
            }

            var routes = table.Routes;
            for (var i = routes.Length - 1; i >= 0; i--)
            {
                var route = routes[i];
                if (!route.Match(url))
                {
                    continue;
                }

                var routed = new RoutedRequest(url, hop.Method, hop.Headers, body, hop.HasPostData,
                    ResourceTypeOf(hop.Kind), hop.Initiator, hop.RedirectCount);

                try
                {
                    var handled = route.Handler(routed) ?? Task.CompletedTask;
                    await handled.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Sealed on the way out too, so nothing the handler left running can decide later.
                    routed.Seal();
                    throw;
                }

                if (routed.Seal() is { } decision)
                {
                    return decision;
                }
            }
        }

        return null;
    }

    internal static RequestResourceType ResourceTypeOf(PageRequestKind kind) => kind switch
    {
        PageRequestKind.Document => RequestResourceType.Document,
        PageRequestKind.Frame => RequestResourceType.Frame,
        PageRequestKind.Script => RequestResourceType.Script,
        PageRequestKind.Stylesheet => RequestResourceType.Stylesheet,
        PageRequestKind.Image => RequestResourceType.Image,
        PageRequestKind.Font => RequestResourceType.Font,
        PageRequestKind.Fetch => RequestResourceType.Fetch,
        PageRequestKind.Xhr => RequestResourceType.Xhr,
        PageRequestKind.EventSource => RequestResourceType.EventSource,
        _ => RequestResourceType.Other,
    };
}

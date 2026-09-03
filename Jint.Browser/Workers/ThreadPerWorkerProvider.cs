using System.Net.Http;
using Jint.Browser.Runtime;
using Jint.WebApi;

namespace Jint.Browser.Workers;

/// <summary>
/// The page's answer to <c>new Worker(...)</c>: one thread per worker, running the documented pump loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>The package is a host, so it may start a thread; the engine never does.</b> That is the whole reason
/// <c>WorkerProvider</c> exists — a worker needs a thread and a pump, and "Jint never starts a thread to run
/// script" is load-bearing across the web-API family. Everything specification-shaped (port entanglement,
/// the worker global, message and error plumbing, <c>terminate()</c>) is the engine's; the thread, the pump
/// and the worker engine's configuration are here.
/// </para>
/// <para>
/// <b>The worker's posture is the page's, and its grants are named one at a time.</b>
/// <c>WorkerRequest.CreateDefaultOptions</c> copies the parent's restrictive settings and withholds every
/// grant — network, storage, further workers — because a worker must never be a way around a hardened page.
/// What is added back here is exactly what a <c>DedicatedWorkerGlobalScope</c> is expected to have:
/// <c>fetch</c> over the same client, filter and jar the page uses, and a module loader against the page's
/// document URL. Storage is deliberately <b>not</b> given: <c>localStorage</c> is not available in a worker
/// in any browser either. A page with no document URL to resolve against — <c>about:blank</c>, a
/// <c>data:</c> URL — gets no module loader at all, which leaves the engine's own
/// <c>FailFastModuleLoader</c>: such a worker fails at its first import rather than reading the file system.
/// </para>
/// <para>
/// <b>Threads are counted and bounded by the engine, not here.</b> <c>Options.WebApi.Workers.MaxWorkers</c>
/// is the per-engine backstop; this provider refuses nothing of its own, and it stops every thread it
/// started when the page closes.
/// </para>
/// <para>
/// <b>A worker's turns are bounded the way a page's are.</b> <c>CreateDefaultOptions</c> replays the
/// parent's constraint <i>factories</i>, so a worker has an <c>OperationDeadlineConstraint</c> and — where
/// the page has one — a <c>MemoryLimitConstraint</c> of its own, and the pump below brackets each drain with
/// them exactly as <see cref="Runtime.PageLoop"/> does. Without that a worker would be the one engine here
/// with no wall-clock bound at all: an inherited <c>TimeoutInterval</c> never fires on an engine that is only
/// ever pumped. The three web-API limits are not constraints and do not travel with the posture, so they are
/// named again below.
/// </para>
/// </remarks>
internal sealed class ThreadPerWorkerProvider : WorkerProvider
{
    private readonly Page _page;
    private readonly PageNetwork _network;
    private readonly PageNetworkRecorder _requests;
    private readonly BrowserOptions _options;
    private readonly TimeSpan _pumpIdle;
    private readonly System.Threading.Lock _gate = new();
    private readonly List<WorkerConnection> _live = [];

    private volatile bool _closed;

    internal ThreadPerWorkerProvider(Page page, PageNetwork network, PageNetworkRecorder requests, BrowserOptions options)
    {
        _page = page;
        _network = network;
        _requests = requests;
        _options = options;
        _pumpIdle = options.PumpIdle;
    }

    /// <summary>How many workers of this page are running.</summary>
    internal int LiveCount
    {
        get
        {
            lock (_gate)
            {
                return _live.Count;
            }
        }
    }

    /// <inheritdoc />
    public override Engine? CreateWorkerEngine(WorkerRequest request)
    {
        if (_closed)
        {
            // A page that is closing starts no more threads. Null is a policy refusal, which reaches the
            // script as a SecurityError — the honest answer for "this page is going away".
            return null;
        }

        var options = request.CreateDefaultOptions();

        // The grants, one at a time and each with a reason. Fetch: a worker that cannot fetch is not much of
        // a worker, and it is bounded by the same filter as everything else the page does. Messaging and
        // GlobalEvents are already on — CreateDefaultOptions adds them, because the worker global is built
        // out of them.
        options.WebApi.Features |= WebApiFeatures.Fetch;

        // The three page-sized limits, again. CopySecurityPosture carries the engine's own bounds — the
        // constraint values, the parser bounds, the module-graph bounds, the result limits — but a web-API
        // setting is not one of them and a worker starts from a fresh Options, so a page held to five timers
        // and a small response ceiling would otherwise spawn a worker holding the engine defaults.
        options.WebApi.Timers.MaxActiveTimers = _options.MaxActiveTimers;
        options.WebApi.Fetch.MaxResponseBytes = _options.MaxResponseBytes;
        options.WebApi.Fetch.Timeout = _options.FetchTimeout;

        // A worker is the page making a request, so it says what the page says — the client's user agent
        // override included, which is why this is read from the page's emulation state rather than from
        // BrowserOptions. Read here, once per worker: a worker's engine options are frozen the moment it is
        // built, exactly as its parent document's are.
        options.WebApi.Fetch.UserAgent = _page.Emulation.EffectiveUserAgent;

        // On the parent's thread, inside the constructor, so the host's factory sees the engine that asked
        // for the worker — which is the engine whose HostDefined carries whatever varies per page.
        var client = _network.ClientFor(request.Parent);
        options.WebApi.Fetch.HttpClient = client;
        options.WebApi.Fetch.UrlFilter = _network.UrlFilter;
        options.WebApi.Fetch.CookieJar = _network.CookieJar;

        // The page's own network log, so what a worker fetches is in Page.Requests beside what the document
        // did. The observer is called from transport threads and is written for exactly that.
#pragma warning disable JINT0002 // FetchObserver is the engine's own network seam.
        options.WebApi.Fetch.Observer = _requests;
#pragma warning restore JINT0002

        if (Uri.TryCreate(_page.Url, UriKind.Absolute, out var baseUrl)
            && (baseUrl.Scheme == Uri.UriSchemeHttp || baseUrl.Scheme == Uri.UriSchemeHttps))
        {
            options.WebApi.Fetch.BaseUrl = baseUrl;
            options.WebApi.Fetch.Referrer = baseUrl;
            options.WebApi.Fetch.Origin = baseUrl.GetLeftPart(UriPartial.Authority);

            options.Modules.ModuleLoader = new PageModuleLoader(
                _network,
                _requests,
                client,
                baseUrl,
                options.WebApi.Fetch.MaxResponseBytes,
                options.WebApi.Fetch.Timeout,
                options.WebApi.Fetch.UserAgent);
        }

        return new Engine(options);
    }

    /// <inheritdoc />
    public override void OnWorkerStarted(WorkerConnection connection)
    {
        lock (_gate)
        {
            // Registered before the thread starts, which is the documented order: the moment a pump runs the
            // worker may load, evaluate, call close() and end, so OnWorkerEnded can be invoked before this
            // method returns — and a host that added afterwards would observe the remove before the add.
            _live.Add(connection);
        }

        var thread = new Thread(() => Pump(connection))
        {
            IsBackground = true,
            Name = "Jint.Browser worker",
        };

        // Thread.Start is the memory-ordering edge the hand-off needs: everything the engine wrote
        // happens-before OnWorkerStarted returns, and this publishes it to the first pump.
        thread.Start();
    }

    /// <inheritdoc />
    /// <remarks>
    /// A signal only, on whichever thread ended the connection — frequently the page's, because
    /// <c>terminate()</c> ends it from there while the worker thread sits inside <c>ProcessTasks</c>. It
    /// must not dispose or pump the worker engine; the loop below observes <c>IsEnded</c> and does both on
    /// the thread that was pumping.
    /// </remarks>
    public override void OnWorkerEnded(WorkerConnection connection, WorkerEndReason reason)
    {
        lock (_gate)
        {
            _live.Remove(connection);
        }
    }

    /// <summary>Ends every worker of this page, which is what closing the page does.</summary>
    /// <remarks>
    /// <c>End()</c> touches only endpoints, a cancellation source and interlocked bookkeeping, so it is safe
    /// from the page's own thread while a worker thread is inside <c>ProcessTasks</c>. Each worker's loop
    /// wakes on the token, leaves, and disposes its engine itself.
    /// </remarks>
    internal void CloseAll()
    {
        _closed = true;

        WorkerConnection[] live;
        lock (_gate)
        {
            live = _live.ToArray();
        }

        foreach (var connection in live)
        {
            try
            {
                connection.End();
            }
            catch (Exception)
            {
                // A connection that cannot be ended is not a reason to leave the rest running.
            }
        }
    }

    private void Pump(WorkerConnection connection)
    {
        var worker = connection.Worker;

        // The same bracket the page loop puts around its own turns, over the same two constraints: a worker
        // inherits its parent's constraint factories, so it has an OperationDeadlineConstraint and — where
        // the page has one — a MemoryLimitConstraint of its own. Without it a worker is the one engine in
        // the package with no wall-clock bound at all, because a pumped engine reaches ExecuteWithConstraints
        // never and its inherited TimeoutInterval therefore never fires.
        var budget = PageBudget.For(worker, _options);

        try
        {
            while (!connection.IsEnded)
            {
                try
                {
                    using (budget.BeginTurn())
                    {
                        worker.Tasks.ProcessTasks();
                    }
                }
                catch (Exception exception) when (PageBudget.IsBudgetFailure(exception))
                {
                    // Recorded on the page, and the worker goes on with a budget of its own — the same
                    // answer the page gives its own drains. Anything else still ends the worker below.
                    _page.RecordWorkerError(exception, connection.Name);
                }

                if (connection.IsEnded)
                {
                    break;
                }

                var next = worker.Tasks.TimeUntilNextScheduledWork;
                var wait = next is { } due && due < _pumpIdle ? due : _pumpIdle;
                if (wait <= TimeSpan.Zero)
                {
                    continue;
                }

                try
                {
                    worker.Tasks.WaitForScheduledWork(wait, connection.TerminationToken);
                }
                catch (OperationCanceledException)
                {
                    // terminate(), or the page closing. The loop leaves through IsEnded on the next turn.
                }
            }
        }
        catch (Exception exception)
        {
            _page.RecordWorkerError(exception, connection.Name);
        }
        finally
        {
            try
            {
                // On the pumping thread, after the loop — which is the one place a worker engine may be
                // disposed. Doing it from OnWorkerEnded would be the engine's concurrent-use exception,
                // thrown out of the middle of the page's own script.
                worker.Dispose();
            }
            catch (Exception)
            {
                // A worker that could not be torn down must not take the page's thread with it.
            }
        }
    }
}

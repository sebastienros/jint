using System.Diagnostics;
using System.Globalization;
using AngleSharp;
using Jint.Browser.Runtime;
using Jint.Browser.Workers;
using Jint.Native;
using Jint.WebApi;

namespace Jint.Browser;

/// <summary>
/// One document, one engine and one thread: a page loads content, runs its scripts, and answers questions
/// about the result.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member here is a request to the page's own thread.</b> Nothing a caller receives is a
/// <c>JsValue</c> or an AngleSharp node — those belong to the loop that made them — so a result is converted
/// before the returned task completes. That is what makes a <see cref="Page"/> usable from any thread while
/// its engine is used from exactly one.
/// </para>
/// <para>
/// <b>What runs today.</b> A document fetched over <c>http(s)</c>, from a <c>data:</c> URL,
/// <c>about:blank</c> or <see cref="SetContentAsync"/>, with classic inline scripts executed in document
/// order as the parse reaches them, then <c>readystatechange</c>, <c>DOMContentLoaded</c>, <c>load</c> and
/// <c>pageshow</c>. Forms submit, history travels, cookies and storage persist, and workers run. External
/// <c>&lt;script src&gt;</c> and module scripts still need the parser driver; what a parse skipped is listed
/// in <see cref="UnsupportedScripts"/>.
/// </para>
/// <para>
/// A page is closed with <see cref="CloseAsync"/> or by disposing it, and every call afterwards fails with
/// <see cref="ObjectDisposedException"/> rather than hanging.
/// </para>
/// </remarks>
public sealed partial class Page : IAsyncDisposable
{
    private readonly PageLoop _loop;
    private readonly PageRecorder _recorder;
    private readonly PageNetworkRecorder _requests;
    private readonly BrowserOptions _options;
    private readonly PageNetwork _network;
    private readonly ThreadPerWorkerProvider _workers;

    /// <summary>The page's session history. Touched on the loop thread only.</summary>
    private readonly SessionHistory _history = new();

    /// <summary>
    /// This page's <c>sessionStorage</c>, one store per origin. Touched on the loop thread only, and owned by
    /// the page rather than the context, which is exactly the lifetime the name promises.
    /// </summary>
    private readonly Dictionary<string, StorageProvider> _sessionStores = new(StringComparer.Ordinal);

    private volatile PageLoad? _load;
    private volatile string _url = "about:blank";
    private volatile string _referrer = "";
    private volatile PageResponse? _response;
    private volatile Frame _mainFrame;
    private volatile bool _closed;

    private Page(BrowserContext context, BrowserOptions options, PageRecorder recorder)
    {
        Context = context;
        _options = options;
        _recorder = recorder;
        _requests = new PageNetworkRecorder(options.MaxRecordedEvents);
        _network = context.Network;
        _workers = new ThreadPerWorkerProvider(this, _network, options.PumpIdle);
        _mainFrame = Frame.Detached(this);
        _loop = new PageLoop(
            "Jint.Browser page loop",
            options.PumpIdle,
            () => BuildEngine("about:blank", ""),
            exception => recorder.Add(new PageError(PageErrorKind.UncaughtCallbackError, exception.Message, "PageLoop")));
    }

    /// <summary>The context this page belongs to.</summary>
    public BrowserContext Context { get; }

    /// <summary>The page's only scripted frame; child frames are parsed and listed but do not run script.</summary>
    public Frame MainFrame => _mainFrame;

    /// <summary>The URL of the document currently loaded.</summary>
    /// <remarks>
    /// It follows <c>history.pushState</c> and a fragment navigation as well as a real one, which is what a
    /// page's own <c>location.href</c> does.
    /// </remarks>
    public string Url => _url;

    /// <summary>
    /// The response the current document came from, or <see langword="null"/> when it did not come from one.
    /// </summary>
    /// <remarks>
    /// <c>about:blank</c>, a <c>data:</c> URL and <see cref="SetContentAsync"/> reach no network, so they
    /// have no response. A <c>404</c> does: a status is not a failure, and the error page it carried is the
    /// document.
    /// </remarks>
    public PageResponse? Response => _response;

    /// <summary>Every request the page has made, oldest first — documents, <c>fetch</c>, <c>XMLHttpRequest</c>.</summary>
    /// <remarks>
    /// It spans the page rather than the document, so a navigation does not clear it, and it is ring-bounded
    /// by <see cref="BrowserOptions.MaxRecordedEvents"/>. An entry appears when the first hop goes out, so a
    /// request still in flight is in the list with a status of zero.
    /// </remarks>
    public IReadOnlyList<PageRequest> Requests => _requests.Requests;

    /// <summary>How many of this page's workers are running.</summary>
    public int Workers => _workers.LiveCount;

    /// <summary>Whether the page has been closed.</summary>
    public bool IsClosed => _closed;

    /// <summary>What the page's scripts got wrong, oldest first, rendered to text on the page's own thread.</summary>
    public IReadOnlyList<PageError> Errors => _recorder.Errors;

    /// <summary>What the page's scripts printed, oldest first, formatted the way a console would.</summary>
    public IReadOnlyList<string> ConsoleMessages => _recorder.ConsoleMessages;

    /// <summary>The <c>&lt;script&gt;</c> elements the last load could not run, each with the reason.</summary>
    /// <remarks>
    /// A page that does nothing is usually a page whose scripts were external or modules. This says so rather
    /// than leaving a host to work it out; it empties as the parser driver arrives.
    /// </remarks>
    public IReadOnlyList<string> UnsupportedScripts => _load?.UnsupportedScripts ?? [];

    /// <summary>Raised when the page calls <c>alert</c>, <c>confirm</c> or <c>prompt</c>.</summary>
    /// <remarks>
    /// The handler runs on the page's own thread, inside the script that opened the dialog, so it must return
    /// without calling back into the page. With no handler the dialog is dismissed.
    /// </remarks>
    public event EventHandler<DialogEventArgs>? DialogOpened;

    /// <summary>Replaces the document with <paramref name="html"/>, parsed as if fetched from a URL.</summary>
    /// <param name="html">The markup to parse.</param>
    /// <param name="baseUrl">The URL the document reports and resolves against; <c>about:blank</c> by default.</param>
    /// <returns>A task that completes when the document has loaded and its scripts have run.</returns>
    /// <remarks>
    /// The document's origin is the base URL's, so content given an <c>https://</c> base URL reaches that
    /// origin's <c>localStorage</c> and cookies exactly as a fetched document would. With no base URL the
    /// origin is opaque.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public async Task SetContentAsync(string html, string? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        ObjectDisposedException.ThrowIf(_closed, this);

        var url = baseUrl ?? "about:blank";

        // Through the same gate a navigation takes, because it is one: it unloads a document, swaps the
        // engine and adds a history entry, and doing that beside a navigation in flight would race the swap.
        await SetContentCoreAsync(url, html).ConfigureAwait(false);
    }

    /// <summary>Evaluates <paramref name="script"/> in the page and answers the result as a CLR value.</summary>
    /// <param name="script">The script to run; its completion value is what comes back.</param>
    /// <returns>The result converted on the page's thread — never a <c>JsValue</c>.</returns>
    /// <remarks>
    /// <para>
    /// The conversion is the engine's own <c>JsValue.ToObject</c>: a number is a <see cref="double"/>, an
    /// object is a property bag, an array is an <see cref="object"/> array. A JavaScript function converts to
    /// a delegate that would run script, so returning one from a page is not a way to leave its thread — call
    /// it only through another <see cref="EvaluateAsync(string)"/>.
    /// </para>
    /// <para>
    /// A value whose graph is cyclic cannot be converted, and <c>window</c> is the obvious one: it is its own
    /// <c>window</c>, <c>self</c>, <c>top</c> and <c>parent</c>, so converting it is the <c>TypeError</c> that
    /// <c>JSON.stringify(window)</c> is in a browser. Project what you need — <c>document.title</c>,
    /// <c>location.href</c> — rather than the object that holds it.
    /// </para>
    /// <para>
    /// The completion value is taken as it is: a script ending in a promise answers the promise, not what it
    /// settles to. Await it in the script and read the result on the next call, or use
    /// <see cref="WaitForIdleAsync"/> in between.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<object?> EvaluateAsync(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return _loop.PostAsync(engine => engine.Evaluate(script).ToObject());
    }

    /// <summary>Evaluates <paramref name="script"/> and converts its result to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="script">The script to run.</param>
    /// <returns>The converted result, or the default when the script answered <c>null</c> or <c>undefined</c>.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<T?> EvaluateAsync<T>(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return _loop.PostAsync(engine => Convert<T>(engine.Evaluate(script)));
    }

    /// <summary>The document's serialized markup, including the doctype.</summary>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string> ContentAsync()
        => _loop.PostAsync(engine => PageRuntime.Find(engine)?.Document?.ToHtml() ?? "");

    /// <summary>The document's title.</summary>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<string> TitleAsync()
        => _loop.PostAsync(engine => PageRuntime.Find(engine)?.Document?.Title ?? "");

    /// <summary>Runs the page until it has nothing left to do, or until <paramref name="timeout"/> runs out.</summary>
    /// <param name="timeout">The ceiling on how long to keep pumping.</param>
    /// <returns><see langword="true"/> when the page went idle, <see langword="false"/> when the timeout won.</returns>
    /// <remarks>
    /// <para>
    /// Idle means the engine has no queued job and nothing scheduled — no due timer, no pending animation
    /// frame, no promise waiting on a background completion. A page with a <c>setInterval</c> is never idle,
    /// so a timeout is the answer there and not a failure.
    /// </para>
    /// <para>
    /// <b>It is not a way to wait for a navigation.</b> The wait holds the page's thread for its whole
    /// duration, and a navigation commits by posting to that same thread — so a navigation a script started
    /// would be queued behind this call and never seen by it. <see cref="WaitForNavigationAsync"/> is the one
    /// that waits for that.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForIdleAsync(TimeSpan timeout) => _loop.PostAsync(engine => PumpUntilIdle(engine, timeout));

    /// <summary>Closes the page, disposing its engine and its document on the page's own thread.</summary>
    /// <returns>A task that completes when the page's thread has ended.</returns>
    /// <remarks>
    /// Everything the page had in flight ends with it: the page's cancellation token is cancelled, so a
    /// running <c>fetch</c> or <c>XMLHttpRequest</c> is abandoned at the socket and a navigation a caller was
    /// awaiting fails with <see cref="OperationCanceledException"/>; every worker thread is asked to stop and
    /// disposes its own engine.
    /// </remarks>
    public async Task CloseAsync()
    {
        _closed = true;
        Context.Remove(this);

        // Before the loop stops, because a worker's own thread is what disposes its engine and it needs to
        // observe the end. End() touches only endpoints and interlocked bookkeeping, so it is safe from here.
        _workers.CloseAll();

        // The document and the browsing context are the loop thread's, like the engine, so they are released
        // there — in the loop's own teardown rather than as a mailbox request, because a request would queue
        // behind whatever is running and what is running may be the very wait this call is ending.
        await _loop.CloseAsync(engine =>
        {
            if (engine is not null)
            {
                PageRuntime.Find(engine)?.Cancellation?.Cancel();
            }

            Release(_load);
            _load = null;
        }).ConfigureAwait(false);

        FailPendingNavigationWaiters();
        _loop.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    /// <summary>
    /// Runs <paramref name="work"/> on the page's own thread with its engine, for callers inside the
    /// assembly that need more than the public surface offers.
    /// </summary>
    /// <remarks>
    /// It is the one door onto the engine, and it is internal because what crosses back out is the caller's
    /// responsibility rather than the type's: a <c>JsValue</c> answered from here would be a value belonging
    /// to a thread the caller is not on. The protocol layer will hold it; today it is what lets the tests
    /// assert engine-level facts — that the global object still has its shared shape — without publishing a
    /// way for an embedder to reach around the page.
    /// </remarks>
    internal Task<T> RunOnLoopAsync<T>(Func<Engine, T> work) => _loop.PostAsync(work);

    /// <summary>The context's network position, which owns the cookie jar every document read shares.</summary>
    internal PageNetwork Network => _network;

    /// <summary>The page's session history. Reachable from the loop thread only.</summary>
    internal SessionHistory History => _history;

    internal static async Task<Page> CreateAsync(BrowserContext context, BrowserOptions options)
    {
        var page = new Page(context, options, new PageRecorder(options.MaxRecordedEvents));

        try
        {
            await page._loop.StartAsync().ConfigureAwait(false);

            // The loop built an engine on the way up, and it is already the about:blank engine this load
            // wants, so the first document reuses it rather than replacing a realm nothing has run in.
            await page._loop.PostAsync(engine => page.LoadInto(engine, "about:blank", "", response: null, referrer: "", onPhase: null)).ConfigureAwait(false);
            await page._loop.PostAsync(page.RecordFirstHistoryEntry).ConfigureAwait(false);
        }
        catch
        {
            // A page nobody received still started a thread and a cancellation source. Whichever of the two
            // exists is released here, because no caller has anything to close.
            page._closed = true;
            await page._loop.CloseAsync().ConfigureAwait(false);
            page._loop.Dispose();
            throw;
        }

        return page;
    }

    /// <summary>Raises <see cref="DialogOpened"/> from the page loop and answers what the handler decided.</summary>
    internal DialogEventArgs RaiseDialog(DialogKind kind, string message, string defaultPromptText)
    {
        var args = new DialogEventArgs(kind, message, defaultPromptText);
        DialogOpened?.Invoke(this, args);
        return args;
    }

    /// <summary>Records what a worker's pump got wrong, from the worker's own thread.</summary>
    internal void RecordWorkerError(Exception exception, string name)
        => _recorder.Add(new PageError(PageErrorKind.WorkerError, exception.Message, name.Length == 0 ? "Worker" : name));

    /// <summary>The engine one document runs in, built with that document's URL, origin and referrer.</summary>
    private Engine BuildEngine(string url, string referrer)
        => BrowserEngineFactory.Create(new PageEngineRequest(
            this,
            _options,
            _recorder,
            _requests,
            _network,
            _workers,
            _sessionStores,
            url,
            referrer,
            _loop.Closing));

    private static void Release(PageLoad? load)
    {
        if (load is null)
        {
            return;
        }

        try
        {
            load.Document.Dispose();
            (load.Context as IDisposable)?.Dispose();
        }
        catch (Exception)
        {
            // A document that could not be torn down is not a reason to fail the navigation that replaced it.
        }
    }

    /// <summary>
    /// Pumps inside one mailbox request until nothing is scheduled, the ceiling runs out, or the page closes.
    /// </summary>
    /// <remarks>
    /// The page's own token is what makes the last of those work: the wait holds the loop for its whole
    /// duration, so a close that could not end it would have to wait out the ceiling before the page's thread
    /// could stop.
    /// </remarks>
    private bool PumpUntilIdle(Engine engine, TimeSpan timeout)
    {
        var started = Stopwatch.GetTimestamp();
        var closing = _loop.Closing;

        while (!closing.IsCancellationRequested)
        {
            engine.Tasks.ProcessTasks();

            if (engine.Tasks.TimeUntilNextScheduledWork is not { } next)
            {
                return true;
            }

            var remaining = timeout - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            if (next > TimeSpan.Zero)
            {
                try
                {
                    engine.Tasks.WaitForScheduledWork(next < remaining ? next : remaining, closing);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return false;
    }

    private static T? Convert<T>(JsValue value)
    {
        var converted = value.ToObject();

        if (converted is null)
        {
            return default;
        }

        if (converted is T typed)
        {
            return typed;
        }

        // Nullable<T> has TypeCode.Object, so ChangeType would refuse every one of them — and a nullable is
        // exactly what a caller reaches for given that a script answering null or undefined gives a default.
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T?) System.Convert.ChangeType(converted, target, CultureInfo.InvariantCulture);
    }
}

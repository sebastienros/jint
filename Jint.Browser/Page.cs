using System.Diagnostics;
using System.Globalization;
using System.Text;
using AngleSharp;
using Jint.Browser.Runtime;
using Jint.Native;

namespace Jint.Browser;

/// <summary>
/// One document, one engine and one thread: a page loads static content, runs its scripts, and answers
/// questions about the result.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every member here is a request to the page's own thread.</b> Nothing a caller receives is a
/// <c>JsValue</c> or an AngleSharp node — those belong to the loop that made them — so a result is converted
/// before the returned task completes. That is what makes a <see cref="Page"/> usable from any thread while
/// its engine is used from exactly one.
/// </para>
/// <para>
/// <b>What runs today.</b> Static content from <see cref="SetContentAsync"/>, <c>about:blank</c> and
/// <c>data:text/html</c>, with classic inline scripts executed in document order as the parse reaches them,
/// then <c>readystatechange</c>, <c>DOMContentLoaded</c> and <c>load</c>. External scripts, modules, real
/// navigation and every kind of subresource need the network, which a page does not reach yet; what a parse
/// skipped is listed in <see cref="UnsupportedScripts"/>.
/// </para>
/// <para>
/// A page is closed with <see cref="CloseAsync"/> or by disposing it, and every call afterwards fails with
/// <see cref="ObjectDisposedException"/> rather than hanging.
/// </para>
/// </remarks>
public sealed class Page : IAsyncDisposable
{
    private readonly PageLoop _loop;
    private readonly PageRecorder _recorder;
    private readonly BrowserOptions _options;

    private volatile PageLoad? _load;
    private volatile string _url = "about:blank";
    private volatile Frame _mainFrame;
    private volatile bool _closed;

    private Page(BrowserContext context, BrowserOptions options, PageRecorder recorder)
    {
        Context = context;
        _options = options;
        _recorder = recorder;
        _mainFrame = Frame.Detached(this);
        _loop = new PageLoop(
            "Jint.Browser page loop",
            options.PumpIdle,
            () => BrowserEngineFactory.Create(this, options, recorder, "about:blank"),
            exception => recorder.Add(new PageError(PageErrorKind.UncaughtCallbackError, exception.Message, "PageLoop")));
    }

    /// <summary>The context this page belongs to.</summary>
    public BrowserContext Context { get; }

    /// <summary>The page's only scripted frame; child frames are parsed and listed but do not run script.</summary>
    public Frame MainFrame => _mainFrame;

    /// <summary>The URL of the document currently loaded.</summary>
    public string Url => _url;

    /// <summary>Whether the page has been closed.</summary>
    public bool IsClosed => _closed;

    /// <summary>What the page's scripts got wrong, oldest first, rendered to text on the page's own thread.</summary>
    public IReadOnlyList<PageError> Errors => _recorder.Errors;

    /// <summary>What the page's scripts printed, oldest first, formatted the way a console would.</summary>
    public IReadOnlyList<string> ConsoleMessages => _recorder.ConsoleMessages;

    /// <summary>The <c>&lt;script&gt;</c> elements the last load could not run, each with the reason.</summary>
    /// <remarks>
    /// A page that does nothing is usually a page whose scripts were external or modules. This says so rather
    /// than leaving a host to work it out; it empties as the parser driver and the network arrive.
    /// </remarks>
    public IReadOnlyList<string> UnsupportedScripts => _load?.UnsupportedScripts ?? [];

    /// <summary>Raised when the page calls <c>alert</c>, <c>confirm</c> or <c>prompt</c>.</summary>
    /// <remarks>
    /// The handler runs on the page's own thread, inside the script that opened the dialog, so it must return
    /// without calling back into the page. With no handler the dialog is dismissed.
    /// </remarks>
    public event EventHandler<DialogEventArgs>? DialogOpened;

    /// <summary>Loads <paramref name="url"/>, which must be <c>about:blank</c> or a <c>data:</c> URL.</summary>
    /// <param name="url">The URL to load.</param>
    /// <returns>A task that completes when the document has loaded and its scripts have run.</returns>
    /// <exception cref="NotSupportedException">The URL names a scheme a page cannot reach yet.</exception>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task NavigateAsync(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var html = ContentOf(url);
        return _loop.PostAsync(engine => Navigate(url, html));
    }

    /// <summary>Replaces the document with <paramref name="html"/>, parsed as if fetched from a URL.</summary>
    /// <param name="html">The markup to parse.</param>
    /// <param name="baseUrl">The URL the document reports and resolves against; <c>about:blank</c> by default.</param>
    /// <returns>A task that completes when the document has loaded and its scripts have run.</returns>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task SetContentAsync(string html, string? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(html);

        var url = baseUrl ?? "about:blank";
        return _loop.PostAsync(engine => Navigate(url, html));
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
    /// Idle means the engine has no queued job and nothing scheduled — no due timer, no pending animation
    /// frame, no promise waiting on a background completion. A page with a <c>setInterval</c> is never idle,
    /// so a timeout is the answer there and not a failure.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The page has been closed.</exception>
    public Task<bool> WaitForIdleAsync(TimeSpan timeout) => _loop.PostAsync(engine => PumpUntilIdle(engine, timeout));

    /// <summary>Closes the page, disposing its engine and its document on the page's own thread.</summary>
    /// <returns>A task that completes when the page's thread has ended.</returns>
    public async Task CloseAsync()
    {
        _closed = true;
        Context.Remove(this);

        // The document and the browsing context are the loop thread's, like the engine, so they are released
        // there — in the loop's own teardown rather than as a mailbox request, because a request would queue
        // behind whatever is running and what is running may be the very wait this call is ending.
        await _loop.CloseAsync(_ =>
        {
            Release(_load);
            _load = null;
        }).ConfigureAwait(false);

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

    internal static async Task<Page> CreateAsync(BrowserContext context, BrowserOptions options)
    {
        var page = new Page(context, options, new PageRecorder(options.MaxRecordedEvents));

        try
        {
            await page._loop.StartAsync().ConfigureAwait(false);

            // The loop built an engine on the way up, and it is already the about:blank engine this load
            // wants, so the first document reuses it rather than replacing a realm nothing has run in.
            await page._loop.PostAsync(engine => page.LoadInto(engine, "about:blank", "")).ConfigureAwait(false);
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

    /// <summary>
    /// The navigation seam a page's own <c>location</c> assignment reaches.
    /// </summary>
    /// <remarks>
    /// It is deliberately honest rather than complete: a target this version can load starts a real
    /// navigation on the next turn of the loop — never inline, because the caller is a script the current
    /// document is running — and anything else is recorded as a page error naming what it would have needed.
    /// A page is not silently left on the old document with no sign that it asked to leave.
    /// </remarks>
    internal void RequestNavigation(string url, bool replace)
    {
        if (_closed)
        {
            return;
        }

        if (!IsSupported(url))
        {
            _recorder.Add(new PageError(
                PageErrorKind.ReportedError,
                "Navigation to '" + url + "' was refused: a page reaches no network in this version, so only "
                + "about:blank and data:text/html URLs can be loaded.",
                "Location"));
            return;
        }

        string html;

        try
        {
            html = ContentOf(url);
        }
        catch (Exception exception)
        {
            // A malformed data: URL is the page's mistake, not the host's, and this call is inside the script
            // that made it — so it becomes a page error like any other rather than a CLR exception erupting
            // through the parse and faulting whatever the host was awaiting.
            _recorder.Add(new PageError(
                PageErrorKind.ReportedError,
                "Navigation to '" + url + "' was refused: " + exception.Message,
                "Location"));
            return;
        }

        // Deliberately not awaited: the caller is a script the current document is running, and the document
        // it asked for replaces the engine that script is in. The continuation is what keeps a navigation
        // posted into a closing page from becoming an unobserved faulted task.
        _ = _loop.PostAsync(engine => Navigate(url, html))
            .ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);
    }

    /// <summary>A navigation: a new engine, and therefore a new realm, for the document it loads.</summary>
    private object? Navigate(string url, string html)
        => LoadInto(_loop.ReplaceEngine(() => BrowserEngineFactory.Create(this, _options, _recorder, url)), url, html);

    private object? LoadInto(Engine engine, string url, string html)
    {
        // The previous document goes first, and the page describes nothing until the new one exists. The
        // engine that document belonged to has already been replaced, so nothing can reach it; and a parse
        // that throws leaves a page with no document rather than one describing a document that is gone.
        var previous = _load;
        _load = null;
        _url = url;
        _mainFrame = Frame.Detached(this);
        Release(previous);

        var runtime = PageRuntime.Find(engine)!;
        var load = PageDocument.Load(runtime, html, url, _loop.Thread.ManagedThreadId);

        _load = load;
        _mainFrame = Frame.Build(this, load.Document, url);
        return null;
    }

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

    private static bool IsSupported(string url)
        => url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The markup a URL this version can load carries: nothing for <c>about:</c>, and the payload of a
    /// <c>data:text/html</c> URL, percent-decoded or base64-decoded.
    /// </summary>
    private static string ContentOf(string url)
    {
        if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "Jint.Browser cannot load '" + url + "'. A page reaches no network in this version: use "
                + "SetContentAsync, about:blank, or a data:text/html URL.");
        }

        var comma = url.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
        {
            throw new NotSupportedException("'" + url + "' is not a valid data URL: it has no comma.");
        }

        var metadata = url[5..comma];
        var payload = url[(comma + 1)..];

        if (metadata.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetString(System.Convert.FromBase64String(payload));
        }

        return Uri.UnescapeDataString(payload);
    }
}

using Jint.Browser.Runtime;
using Jint.Browser.Workers;
using Jint.WebApi.Fetch;

namespace Jint.Browser;

/// <summary>
/// A set of pages that share cookies and storage and share nothing with any other context — a browser
/// profile, in one process.
/// </summary>
/// <remarks>
/// <para>
/// Contexts are how one <see cref="Browser"/> keeps unrelated work apart: two agents driving the same browser
/// take a context each and cannot see one another's cookies, one another's <c>localStorage</c> or one
/// another's pages. What makes that true is that the jar, the storage partition and the network position are
/// the context's rather than the page's — so two pages of one site in one context share a session, and the
/// same two pages in two contexts are two visitors.
/// </para>
/// <para>
/// Closing a context closes its pages.
/// </para>
/// </remarks>
public sealed class BrowserContext : IAsyncDisposable
{
    private readonly List<Page> _pages = [];
    private readonly List<BrowsingContextHandle> _openingPopups = [];
    private readonly List<TaskCompletionSource> _openingPages = [];
    private readonly int _maxPages;
    private readonly object _gate = new();
    private volatile bool _closed;

    internal BrowserContext(Browser browser, BrowserContextOptions options)
    {
        Browser = browser;
        var maxPages = options.MaxPages ?? browser.Options.MaxPages;
        _maxPages = browser.Options.UntrustedContent is not null && (maxPages == 0 || maxPages == int.MaxValue)
            ? browser.Options.MaxPages
            : maxPages;
        var maxStorage = options.MaxTotalStorageBytes ?? browser.Options.MaxTotalStorageBytes;
        if (browser.Options.UntrustedContent is not null && maxStorage == long.MaxValue)
            maxStorage = browser.Options.MaxTotalStorageBytes;
        Network = new PageNetwork(options, browser.Options.BlocksPrivateNetworkByDefault,
            browser.Options.MaxCacheStorageBytes, browser.Options.MaxIndexedDbBytes, maxStorage);
        Viewport = options.Viewport ?? browser.Options.Viewport;
        HasTouch = options.HasTouch ?? browser.Options.HasTouch;
        MediaEmulation = options.MediaEmulation;
    }

    /// <summary>
    /// The viewport this context's pages open with: <see cref="BrowserContextOptions.Viewport"/>, or the
    /// browser's when the context named none.
    /// </summary>
    internal Viewport Viewport { get; }

    /// <summary>
    /// Whether this context's pages open as touch devices: <see cref="BrowserContextOptions.HasTouch"/>, or the
    /// browser's when the context named none.
    /// </summary>
    internal bool HasTouch { get; }

    /// <summary>
    /// <see cref="BrowserContextOptions.MediaEmulation"/>, read once when the context was created and applied
    /// over the browser's own to every page the context opens.
    /// </summary>
    internal MediaEmulation? MediaEmulation { get; }

    /// <summary>The browser this context belongs to.</summary>
    public Browser Browser { get; }

    /// <summary>The pages currently open in this context.</summary>
    public IReadOnlyList<Page> Pages
    {
        get
        {
            lock (_gate)
            {
                return _pages.ToArray();
            }
        }
    }

    /// <summary>Where this context's cookies live — the one its options named, or a private jar.</summary>
    /// <remarks>
    /// The same jar answers every request's <c>Cookie</c> header, stores every response's <c>Set-Cookie</c>
    /// and backs <c>document.cookie</c>, so a host can seed a session before a page loads and read one back
    /// afterwards.
    /// </remarks>
    public CookieJar CookieJar => Network.CookieJar;

    /// <summary>Where this context's <c>localStorage</c> lives, one store per origin.</summary>
    public StoragePartitionProvider StoragePartition => Network.Storage;

    /// <summary>Whether the context has been closed.</summary>
    public bool IsClosed => _closed;

    /// <summary>
    /// The client, filter, jar and storage partition every page of this context loads through.
    /// </summary>
    internal PageNetwork Network { get; }

    internal SharedWorkerRegistry SharedWorkers { get; } = new();

    /// <summary>The routes this context registered, consulted after a page's own.</summary>
    internal PageRouteTable Routes { get; } = new();

    /// <summary>
    /// Routes every request of every page in this context — open now or opened later — whose URL
    /// <paramref name="match"/> accepts to <paramref name="handler"/>.
    /// </summary>
    /// <param name="match">Which URLs to route; called off the page's loop for every hop.</param>
    /// <param name="handler">What to do with a matching request; see <see cref="RoutedRequest"/>.</param>
    /// <returns>A task that completes once the route applies to every request sent after it.</returns>
    /// <remarks>
    /// A page's own routes (<see cref="Page.RouteAsync"/>) are asked first; a context route is asked when none
    /// of them decided the request. Shared workers' loads are routed here too, since they belong to no page.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> or <paramref name="handler"/> is null.</exception>
    public Task RouteAsync(Func<Uri, bool> match, Func<RoutedRequest, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(handler);

        if (_closed)
        {
            return Task.FromException(new ObjectDisposedException(nameof(BrowserContext)));
        }

        Routes.Add(new PageRoute(match, handler));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the routes this context registered with <paramref name="match"/>, or only the one with
    /// <paramref name="handler"/> as well when it is named.
    /// </summary>
    /// <param name="match">The predicate a route was registered with, compared as a delegate.</param>
    /// <param name="handler">The handler to remove, or <see langword="null"/> for every handler of <paramref name="match"/>.</param>
    /// <returns>A task that completes once no request sent afterwards reaches the removed routes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="match"/> is null.</exception>
    public Task UnrouteAsync(Func<Uri, bool> match, Func<RoutedRequest, Task>? handler = null)
    {
        ArgumentNullException.ThrowIfNull(match);
        Routes.Remove(match, handler);
        return Task.CompletedTask;
    }

    /// <summary>The init scripts every page of this context runs before its own, in each new document.</summary>
    internal NewDocumentScripts NewDocumentScripts { get; } = new();

    /// <summary>Adds a script that runs in every new document of every page of this context, before the document's own scripts.</summary>
    /// <param name="script">The script source.</param>
    /// <returns>A task that completes once the script is installed.</returns>
    /// <remarks>
    /// It reaches pages already open as well as later ones and popups, from their next document onwards,
    /// including the <c>about:blank</c> a new page starts on. A context's scripts run before a page's own
    /// <see cref="Page.AddInitScriptAsync(string)"/> scripts, with the same timing and error handling.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The context has been closed.</exception>
    public Task AddInitScriptAsync(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        ObjectDisposedException.ThrowIf(_closed, this);
        NewDocumentScripts.Add(this, "BrowserContext.AddInitScriptAsync", script);
        return Task.CompletedTask;
    }

    /// <summary>Adds a function invoked with one JSON-data argument in every new document of every page of this context.</summary>
    /// <param name="function">A JavaScript function expression.</param>
    /// <param name="argument">The argument, snapshotted as JSON data now and bound in each document.</param>
    /// <returns>A task that completes once the script is installed.</returns>
    /// <remarks>
    /// The argument is bound as <see cref="Page.EvaluateAsync(string, object?)"/> binds one. Otherwise this
    /// runs where and when <see cref="AddInitScriptAsync(string)"/> does.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The context has been closed.</exception>
    public Task AddInitScriptAsync(string function, object? argument)
    {
        ArgumentNullException.ThrowIfNull(function);
        ObjectDisposedException.ThrowIf(_closed, this);
        NewDocumentScripts.Add(this, "BrowserContext.AddInitScriptAsync", function, System.Text.Json.JsonSerializer.SerializeToElement(argument));
        return Task.CompletedTask;
    }

    /// <summary>Opens a new page on <c>about:blank</c>, with its own engine and its own thread.</summary>
    /// <returns>The page, once its thread is running and the blank document has loaded.</returns>
    /// <exception cref="ObjectDisposedException">The context or its browser has been closed.</exception>
    /// <exception cref="InvalidOperationException">The context page limit has been reached.</exception>
    public async Task<Page> NewPageAsync()
    {
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (AtPageLimit()) throw new InvalidOperationException("The context page limit has been reached.");
            _openingPages.Add(opening);
        }

        try
        {
            var page = await Page.CreateAsync(this, Browser.Options).ConfigureAwait(false);
            bool closed;
            lock (_gate)
            {
                closed = _closed;
                if (!closed)
                {
                    _pages.Add(page);
                    _openingPages.Remove(opening);
                }
            }

            if (closed)
            {
                await page.CloseAsync().ConfigureAwait(false);
                ObjectDisposedException.ThrowIf(true, this);
            }

            page.WindowHandle.Complete(page);
            Browser.OnPageOpened(this, page);
            return page;
        }
        finally
        {
            lock (_gate) _openingPages.Remove(opening);
            opening.SetResult();
        }
    }

    // Called only under _gate: reservations must count before a page allocates an engine or starts a thread.
    private bool AtPageLimit() => _maxPages != 0
        && (long) _pages.Count + _openingPopups.Count + _openingPages.Count >= _maxPages;

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/document-sequences.html#the-rules-for-choosing-a-navigable
    /// Reserve names under the context lock, including pages whose loops have not started yet.
    /// </summary>
    internal BrowsingContextHandle? ChoosePopup(Page source, string name, bool noopener, CrossPageNavigation navigation)
    {
        lock (_gate)
        {
            if (_closed) return null;
            var named = name.Length != 0 && !name.Equals("_blank", StringComparison.OrdinalIgnoreCase);
            if (named && !noopener)
            {
                foreach (var page in _pages)
                {
                    if (page.WindowHandle.Name == name && page.WindowHandle.CanBeNamedTarget(source, navigation.Origin))
                    {
                        if (navigation.Url.Length != 0) page.WindowHandle.Navigate(source, navigation);
                        return page.WindowHandle;
                    }
                }
                foreach (var pending in _openingPopups)
                {
                    if (pending.Name == name && pending.CanBeNamedTarget(source, navigation.Origin))
                    {
                        if (navigation.Url.Length != 0) pending.Navigate(source, navigation);
                        return pending;
                    }
                }
            }

            // HTML's choosing-a-navigable algorithm permits refusing a new auxiliary context.
            // Reusing an existing named target above needs no new resources.
            if (AtPageLimit()) return null;

            var handle = new BrowsingContextHandle(named ? name : "", noopener ? null : source, scriptClosable: true,
                origin: navigation.Origin);
            _openingPopups.Add(handle);
            if (navigation.Url.Length != 0 && navigation.Url != PageUrl.Blank) handle.Navigate(source, navigation);
            handle.Opening = Task.Run(() => OpenPopupAsync(source, handle, navigation));
            return handle;
        }
    }

    private async Task OpenPopupAsync(Page source, BrowsingContextHandle handle, CrossPageNavigation creator)
    {
        Page? page = null;
        try
        {
            page = await Page.CreateAsync(this, Browser.Options, handle, creator).ConfigureAwait(false);
            handle.Register(page);
            bool closed;
            lock (_gate)
            {
                closed = _closed;
                if (!closed)
                {
                    _pages.Add(page);
                    // From here context shutdown waits for the page, not its announcement callback.
                    _openingPopups.Remove(handle);
                }
            }
            if (closed)
            {
                await page.CloseAsync().ConfigureAwait(false);
                return;
            }
            Browser.OnPageOpened(this, page);
            source.AnnouncePopup(page);
        }
        catch (Exception exception)
        {
            source.RecordPopupError(exception);
        }
        finally
        {
            handle.Complete(page);
            lock (_gate) _openingPopups.Remove(handle);
        }
    }

    /// <summary>Closes the context and every page in it.</summary>
    /// <returns>A task that completes when every page's thread has ended.</returns>
    public async Task CloseAsync()
    {
        Page[] pages;
        Task[] opening;

        lock (_gate)
        {
            _closed = true;
            pages = _pages.ToArray();
            _pages.Clear();
            opening = _openingPopups.Select(handle => handle.Opening)
                .Concat(_openingPages.Select(pending => pending.Task)).ToArray();
        }

        try
        {
            foreach (var page in pages)
            {
                await page.CloseAsync().ConfigureAwait(false);
            }
            await Task.WhenAll(opening).ConfigureAwait(false);
        }
        finally
        {
            Network.HttpCache?.Dispose();
            Browser.Remove(this);
        }
    }

    /// <summary>Clears stored HTTP responses and prevents pending captures from repopulating them.</summary>
    public void ClearHttpCache() => Network.HttpCache?.Clear();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    internal void Remove(Page page)
    {
        lock (_gate)
        {
            _pages.Remove(page);
        }
    }
}

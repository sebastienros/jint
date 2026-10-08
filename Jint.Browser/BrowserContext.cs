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
    }

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

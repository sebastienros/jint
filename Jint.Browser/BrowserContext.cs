using Jint.Browser.Runtime;
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
    private readonly object _gate = new();
    private volatile bool _closed;

    internal BrowserContext(Browser browser, BrowserContextOptions options)
    {
        Browser = browser;
        Network = new PageNetwork(options, browser.Options.BlocksPrivateNetworkByDefault);
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

    /// <summary>Opens a new page on <c>about:blank</c>, with its own engine and its own thread.</summary>
    /// <returns>The page, once its thread is running and the blank document has loaded.</returns>
    /// <exception cref="ObjectDisposedException">The context or its browser has been closed.</exception>
    public async Task<Page> NewPageAsync()
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        var page = await Page.CreateAsync(this, Browser.Options).ConfigureAwait(false);
        var raced = false;

        lock (_gate)
        {
            if (_closed)
            {
                raced = true;
            }
            else
            {
                _pages.Add(page);
            }
        }

        if (raced)
        {
            // Closed while the page was starting, so it is nobody's page. Its thread is awaited here rather
            // than abandoned, because the browser promises that closing waits for every page thread to end
            // and this page is not in any list for it to wait on.
            await page.CloseAsync().ConfigureAwait(false);
            ObjectDisposedException.ThrowIf(true, this);
        }

        Browser.OnPageOpened(this, page);
        return page;
    }

    /// <summary>Closes the context and every page in it.</summary>
    /// <returns>A task that completes when every page's thread has ended.</returns>
    public async Task CloseAsync()
    {
        Page[] pages;

        lock (_gate)
        {
            _closed = true;
            pages = _pages.ToArray();
            _pages.Clear();
        }

        foreach (var page in pages)
        {
            await page.CloseAsync().ConfigureAwait(false);
        }

        Browser.Remove(this);
    }

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

namespace Jint.Browser;

/// <summary>
/// A headless browser: AngleSharp's parser and DOM, Jint's engine, and one thread per page.
/// </summary>
/// <remarks>
/// <para>
/// It is in-process and downloads nothing — there is no native binary and no browser to install — and it
/// renders nothing: no layout, no pixels, no screenshots. What it does is what automation and extraction
/// actually use: parse a document, run its scripts against a real DOM, and let a host read the result.
/// </para>
/// <para>
/// A browser owns contexts, a context owns pages, and a page owns an engine and the thread that drives it.
/// Disposing the browser closes all three, in that order, and waits for every page thread to end.
/// </para>
/// <para>
/// Everything on this type is safe to call from any thread. Nothing it returns is a value belonging to an
/// engine, which is what makes that true.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using var browser = new Browser();
/// var page = await browser.NewPageAsync();
/// await page.SetContentAsync("&lt;p id='greeting'&gt;&lt;/p&gt;&lt;script&gt;greeting.textContent = 'hello'&lt;/script&gt;");
/// var text = await page.EvaluateAsync&lt;string&gt;("document.querySelector('#greeting').textContent");
/// </code>
/// </example>
public sealed class Browser : IAsyncDisposable
{
    private readonly List<BrowserContext> _contexts = [];
    private readonly object _gate = new();
    private readonly BrowserContext _defaultContext;
    private volatile bool _closed;

    /// <summary>Creates a browser whose pages are built from <paramref name="options"/>.</summary>
    /// <param name="options">What every page is built from; the defaults if omitted.</param>
    public Browser(BrowserOptions? options = null)
    {
        Options = options ?? new BrowserOptions();
        _defaultContext = new BrowserContext(this, new BrowserContextOptions());
        _contexts.Add(_defaultContext);
    }

    /// <summary>What every page of this browser is built from.</summary>
    public BrowserOptions Options { get; }

    /// <summary>The context <see cref="NewPageAsync"/> opens pages in.</summary>
    public BrowserContext DefaultContext => _defaultContext;

    /// <summary>Every open context, the default one first.</summary>
    public IReadOnlyList<BrowserContext> Contexts
    {
        get
        {
            lock (_gate)
            {
                return _contexts.ToArray();
            }
        }
    }

    /// <summary>Whether the browser has been closed.</summary>
    public bool IsClosed => _closed;

    /// <summary>Opens a context that shares no cookies or storage with any other.</summary>
    /// <param name="options">What the context keeps to itself; the defaults if omitted.</param>
    /// <returns>The new context.</returns>
    /// <exception cref="ObjectDisposedException">The browser has been closed.</exception>
    public Task<BrowserContext> NewContextAsync(BrowserContextOptions? options = null)
    {
        ObjectDisposedException.ThrowIf(_closed, this);

        var context = new BrowserContext(this, options ?? new BrowserContextOptions());

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            _contexts.Add(context);
        }

        return Task.FromResult(context);
    }

    /// <summary>Opens a page in <see cref="DefaultContext"/>.</summary>
    /// <returns>The page, once its thread is running and the blank document has loaded.</returns>
    /// <exception cref="ObjectDisposedException">The browser has been closed.</exception>
    public Task<Page> NewPageAsync()
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        return _defaultContext.NewPageAsync();
    }

    /// <summary>Closes every context, every page, and every page thread.</summary>
    /// <returns>A task that completes when the last page thread has ended.</returns>
    public async Task CloseAsync()
    {
        BrowserContext[] contexts;

        lock (_gate)
        {
            _closed = true;
            contexts = _contexts.ToArray();
        }

        foreach (var context in contexts)
        {
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    internal void Remove(BrowserContext context)
    {
        lock (_gate)
        {
            _contexts.Remove(context);
        }
    }

    /// <summary>
    /// Raised on the thread that opened a page, once the page is in its context.
    /// </summary>
    /// <remarks>
    /// What lets the protocol layer publish a page a host opened as a target without the host having to say
    /// so. It is internal because a target is the only thing that wants it: the public shape of "a page
    /// appeared" is the protocol event a client receives.
    /// </remarks>
    internal event Action<BrowserContext, Page>? PageOpened;

    /// <summary>Tells whoever is listening that <paramref name="page"/> is open.</summary>
    internal void OnPageOpened(BrowserContext context, Page page) => PageOpened?.Invoke(context, page);
}

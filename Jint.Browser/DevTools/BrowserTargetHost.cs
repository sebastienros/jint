using System.Globalization;
using Jint.Browser.Runtime;
using Jint.DevTools;

namespace Jint.Browser.DevTools;

/// <summary>
/// What mints a browser context and a page when a client asks for one.
/// </summary>
/// <remarks>
/// <para>
/// The four commands every automation client sends before it can do anything — <c>createBrowserContext</c>,
/// <c>createTarget</c>, <c>closeTarget</c>, <c>disposeBrowserContext</c> — mapped onto what a
/// <see cref="Browser"/> already has: a context is a <see cref="BrowserContext"/>, a target is a
/// <see cref="Page"/>, and closing either closes what is in it.
/// </para>
/// <para>
/// <b>The default context has no identifier</b>, which is Chrome's own rule: <c>getBrowserContexts</c>
/// answers the ones a client created, and a page opened in the default one reports no
/// <c>browserContextId</c> at all. Identifiers for every other context are minted here and remembered, so
/// that a context a host created before the server was attached still has one the moment a client asks about
/// a page in it.
/// </para>
/// <para>
/// Every member runs on a transport thread. Nothing here touches an engine: creating a page starts its loop
/// and the target adopts its engine from inside one of its own requests.
/// </para>
/// </remarks>
internal sealed class BrowserTargetHost : ITargetHost
{
    private readonly Browser _browser;
    private readonly DevToolsServer _server;
    private readonly object _gate = new();

    /// <summary>
    /// The adoptions that have started and not finished, so that two paths to one page produce one target.
    /// </summary>
    /// <remarks>
    /// Building a target is asynchronous — it adopts the page's engine from inside one of the page's own loop
    /// requests — so a check-then-create would let two callers both pass the check. There are two callers by
    /// construction: the host creating a page for <c>Target.createTarget</c>, and the browser telling
    /// everyone a page was opened. Whichever arrives second waits on the first one's task rather than
    /// building a second target that would take the page's one observer slot from it.
    /// </remarks>
    private readonly Dictionary<Page, Task<PageTarget>> _adopting = [];

    /// <summary>
    /// Whether this flow is the host opening a page for <c>Target.createTarget</c>.
    /// </summary>
    /// <remarks>
    /// <c>BrowserContext.NewPageAsync</c> announces the page before it returns, so the announcement arrives
    /// while <see cref="CreateTargetAsync"/> is still inside it — and the target that call is about to build
    /// is the one carrying the <c>waitForDebuggerOnStart</c> the client asked for. An
    /// <see cref="AsyncLocal{T}"/> is what tells the two apart: it flows into the announcement and nowhere
    /// else, so a page a host opened on its own is still adopted by the announcement.
    /// </remarks>
    private readonly AsyncLocal<bool> _creating = new();
    private readonly Dictionary<string, BrowserContext> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<BrowserContext, string> _ids = [];
    private readonly Dictionary<Page, PageTarget> _targets = [];
    private readonly Dictionary<PageTarget, TabTarget> _tabs = [];

    private int _nextContext;

    /// <summary>The identifier the browser's own default context is named by.</summary>
    /// <remarks>
    /// <b>Chrome's default browser context has an identifier and every page in it reports one</b>, and a
    /// client is entitled to rely on that: Playwright asserts <c>targetInfo.browserContextId</c> on every
    /// target it attaches to and kills its driver process outright without it, which is how this was found.
    /// It is deliberately <i>not</i> in <see cref="BrowserContextIds"/> and cannot be disposed, because
    /// Chrome's <c>Target.getBrowserContexts</c> lists the contexts a client created rather than the one it
    /// was given, and Puppeteer's own bookkeeping depends on that distinction.
    /// </remarks>
    private const string DefaultContextId = "JINTBROWSERCONTEXTDEFAULT";

    internal BrowserTargetHost(Browser browser, DevToolsServer server)
    {
        _browser = browser;
        _server = server;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> BrowserContextIds
    {
        get
        {
            lock (_gate)
            {
                return [.. _ids.Values];
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<string> CreateBrowserContextAsync(CancellationToken cancellationToken)
    {
        var context = await _browser.NewContextAsync().ConfigureAwait(false);
        return IdOf(context)!;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeBrowserContextAsync(string browserContextId, CancellationToken cancellationToken)
    {
        BrowserContext? context;
        lock (_gate)
        {
            // The default context is not one a client created, so it is not one a client may dispose.
            _byId.TryGetValue(browserContextId, out context);
        }

        if (context is null)
        {
            // Chrome's wording for a context identifier that names nothing.
            Jint.DevTools.Throw.ServerError("Failed to find context with id " + browserContextId);
            return;
        }

        // Every page in it closes, and each of those reaches Closed() on its own target, which is what takes
        // the target off the server. Forgetting the context afterwards keeps the identifier from resolving
        // to something that is gone.
        await context.CloseAsync().ConfigureAwait(false);

        lock (_gate)
        {
            _byId.Remove(browserContextId);
            _ids.Remove(context);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DevToolsTarget> CreateTargetAsync(TargetCreationRequest request, CancellationToken cancellationToken)
    {
        var context = Resolve(request.BrowserContextId);

        Page page;
        _creating.Value = true;
        try
        {
            page = await context.NewPageAsync().ConfigureAwait(false);
        }
        finally
        {
            _creating.Value = false;
        }

        // Registered before the navigation, and holding it when the client asked to attach before anything
        // runs: a target that navigated first would have run the first document's scripts before any client
        // could see the frame start. The context is named from the context rather than from the request, so
        // that a createTarget with no browserContextId still produces a target that names the default one.
        var target = await AdoptAsync(page, IdOf(context), request.WaitForDebugger).ConfigureAwait(false);

        var url = string.IsNullOrEmpty(request.Url) ? "about:blank" : request.Url;
        if (!string.Equals(url, "about:blank", StringComparison.Ordinal))
        {
            try
            {
                await page.NavigateAsync(url, new NavigationOptions { WaitUntil = WaitUntilState.Commit }).ConfigureAwait(false);
            }
            catch (NavigationFailedException)
            {
                // Chrome answers createTarget with a target identifier whether or not the page loaded: the
                // target exists, and what went wrong is on the page rather than on the command.
            }
        }

        return target;
    }

    /// <inheritdoc/>
    public ValueTask CloseTargetAsync(DevToolsTarget target, CancellationToken cancellationToken)
        => target is PageTarget page ? new ValueTask(page.Page.CloseAsync()) : default;

    /// <inheritdoc/>
    public void RegisterBrowserDomains(Jint.DevTools.Session.DevToolsSession session)
        => session.Register(new BrowserStorageDomain(this));

    /// <summary>The network position -- the client, the filter and the jar -- one context loads through.</summary>
    /// <remarks>
    /// It is what the browser-session <c>Storage</c> commands read: they name a context rather than a page,
    /// and a context is exactly what owns a cookie jar.
    /// </remarks>
    internal PageNetwork NetworkOf(string? browserContextId) => Resolve(browserContextId).Network;

    /// <summary>Publishes every page the browser already has, and every one it opens from now on.</summary>
    internal async Task StartAsync()
    {
        _browser.PageOpened += OnPageOpened;

        foreach (var context in _browser.Contexts)
        {
            foreach (var page in context.Pages)
            {
                await AdoptAsync(page, IdOf(context), waitForDebugger: false).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Registers one page as a target and publishes it, once.</summary>
    private Task<PageTarget> AdoptAsync(Page page, string? browserContextId, bool waitForDebugger)
    {
        lock (_gate)
        {
            if (_targets.TryGetValue(page, out var existing))
            {
                return Task.FromResult(existing);
            }

            if (_adopting.TryGetValue(page, out var pending))
            {
                return pending;
            }

            // Started under the lock, which is safe because it runs synchronously only as far as its first
            // await -- a request on the page's own loop, which takes nothing this holds.
            var started = BuildAsync(page, browserContextId, waitForDebugger);
            _adopting[page] = started;
            return started;
        }
    }

    private async Task<PageTarget> BuildAsync(Page page, string? browserContextId, bool waitForDebugger)
    {
        try
        {
            var target = await PageTarget.CreateAsync(page, browserContextId, waitForDebugger, Forget).ConfigureAwait(false);
            var tab = new TabTarget(target);
            target.Tab = tab;

            lock (_gate)
            {
                _targets[page] = target;
                _tabs[target] = tab;
            }

            // The page first, so a client discovering targets is told about the page before it is handed the
            // tab session it will reach that page through.
            _server.AddTarget(target);
            _server.AddTarget(tab);
            return target;
        }
        finally
        {
            lock (_gate)
            {
                _adopting.Remove(page);
            }
        }
    }

    /// <summary>Stops publishing a page that has closed.</summary>
    private void Forget(PageTarget target)
    {
        TabTarget? tab;
        lock (_gate)
        {
            _targets.Remove(target.Page);
            _tabs.Remove(target, out tab);
        }

        _server.RemoveTarget(target);

        if (tab is not null)
        {
            _server.RemoveTarget(tab);
        }
    }

    private void OnPageOpened(BrowserContext context, Page page)
    {
        if (_creating.Value)
        {
            // Target.createTarget opened this one and is about to adopt it itself, with the
            // waitForDebuggerOnStart the asking session set.
            return;
        }

        // The page is already open and the browser is not waiting for this, so the adoption runs on its own
        // and a failure becomes a target nobody sees rather than an exception in the caller's NewPageAsync.
        _ = AdoptAsync(page, IdOf(context), waitForDebugger: false);
    }

    private BrowserContext Resolve(string? browserContextId)
    {
        if (string.IsNullOrEmpty(browserContextId) || string.Equals(browserContextId, DefaultContextId, StringComparison.Ordinal))
        {
            return _browser.DefaultContext;
        }

        lock (_gate)
        {
            if (_byId.TryGetValue(browserContextId, out var context))
            {
                return context;
            }
        }

        return Jint.DevTools.Throw.ServerError<BrowserContext>("Failed to find context with id " + browserContextId);
    }

    /// <summary>The identifier of a context, minted on first sight.</summary>
    private string? IdOf(BrowserContext context)
    {
        if (ReferenceEquals(context, _browser.DefaultContext))
        {
            return DefaultContextId;
        }

        lock (_gate)
        {
            if (_ids.TryGetValue(context, out var existing))
            {
                return existing;
            }

            var id = string.Create(CultureInfo.InvariantCulture, $"JINTBROWSERCONTEXT{++_nextContext:D8}");
            _ids[context] = id;
            _byId[id] = context;
            return id;
        }
    }
}

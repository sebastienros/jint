using System.Reflection;
using JintBrowser = Jint.Browser.Browser;
using JintBrowserContext = Jint.Browser.BrowserContext;
using JintBrowserContextOptions = Jint.Browser.BrowserContextOptions;
using JintBrowserOptions = Jint.Browser.BrowserOptions;
using Microsoft.Playwright;

namespace Jint.Browser.Playwright;

internal sealed class BrowserTypeTarget(Action<JintBrowserOptions>? configure) : ProxyTarget
{
    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        return method.Name switch
        {
            "get_Name" => "jint",
            "get_ExecutablePath" => string.Empty,
            nameof(IBrowserType.LaunchAsync) => LaunchAsync((BrowserTypeLaunchOptions?) arguments[0]),
            _ => Unsupported(method),
        };
    }

    private Task<IBrowser> LaunchAsync(BrowserTypeLaunchOptions? launchOptions)
    {
        OptionSupport.EnsureOnly(launchOptions, "IBrowserType.LaunchAsync", nameof(BrowserTypeLaunchOptions.Headless));
        if (launchOptions?.Headless is false)
        {
            throw new NotSupportedException("Jint.Browser.Playwright cannot launch a headed browser.");
        }

        var options = new JintBrowserOptions();
        configure?.Invoke(options);
        var browser = new JintBrowser(options);
        var target = new BrowserTarget(browser, (IBrowserType) Proxy);
        return Task.FromResult(ProxyFactory.Create<IBrowser>(target));
    }
}

internal sealed class BrowserTarget(JintBrowser browser, IBrowserType browserType) : ProxyTarget
{
    private readonly List<BrowserContextTarget> _contexts = [];
    private EventHandler<IBrowserContext>? _contextCreated;
    private EventHandler<IBrowser>? _disconnected;
    private bool _closed;

    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        switch (method.Name)
        {
            case "add_Context":
                _contextCreated += (EventHandler<IBrowserContext>) arguments[0]!;
                return null;
            case "remove_Context":
                _contextCreated -= (EventHandler<IBrowserContext>) arguments[0]!;
                return null;
            case "add_Disconnected":
                _disconnected += (EventHandler<IBrowser>) arguments[0]!;
                return null;
            case "remove_Disconnected":
                _disconnected -= (EventHandler<IBrowser>) arguments[0]!;
                return null;
            case "get_BrowserType":
                return browserType;
            case "get_Contexts":
                return _contexts.Where(x => !x.IsClosed).Select(x => x.Context).ToArray();
            case "get_IsConnected":
                return !_closed;
            case "get_Version":
                return typeof(JintBrowser).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            case nameof(IBrowser.NewContextAsync):
                OptionSupport.EnsureOnly(arguments[0], "IBrowser.NewContextAsync");
                return NewContextAsync();
            case nameof(IBrowser.NewPageAsync):
                OptionSupport.EnsureOnly(arguments[0], "IBrowser.NewPageAsync");
                return NewPageAsync();
            case nameof(IBrowser.CloseAsync):
                OptionSupport.EnsureOnly(arguments[0], "IBrowser.CloseAsync");
                return CloseAsync();
            case nameof(IAsyncDisposable.DisposeAsync):
                return new ValueTask(CloseAsync());
            default:
                return Unsupported(method);
        }
    }

    private async Task<IBrowserContext> NewContextAsync()
        => (await NewContextTargetAsync().ConfigureAwait(false)).Context;

    private async Task<BrowserContextTarget> NewContextTargetAsync()
    {
        ObjectDisposedException.ThrowIf(_closed, this);
        var inner = await browser.NewContextAsync(new JintBrowserContextOptions()).ConfigureAwait(false);
        var target = new BrowserContextTarget(this, inner);
        target.Context = ProxyFactory.Create<IBrowserContext>(target);
        _contexts.Add(target);

        var errors = new CleanupErrors();
        errors.Run(() => _contextCreated?.Invoke((IBrowser) Proxy, target.Context));
        if (errors.HasAny)
        {
            await errors.RunAsync(target.CloseAfterCreationFailureAsync).ConfigureAwait(false);
            errors.ThrowIfAny();
        }

        return target;
    }

    private async Task<IPage> NewPageAsync()
    {
        var context = await NewContextTargetAsync().ConfigureAwait(false);
        context.CloseWithPage = true;
        return await context.NewPageAsync().ConfigureAwait(false);
    }

    private async Task CloseAsync()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        var errors = new CleanupErrors();
        await errors.RunAsync(browser.CloseAsync).ConfigureAwait(false);
        foreach (var context in _contexts.ToArray())
        {
            errors.Run(context.BrowserClosed);
        }

        _contexts.Clear();
        errors.Run(() => _disconnected?.Invoke((IBrowser) Proxy, (IBrowser) Proxy));
        errors.ThrowIfAny();
    }

    internal void Remove(BrowserContextTarget context) => _contexts.Remove(context);
}

internal sealed class BrowserContextTarget(BrowserTarget owner, JintBrowserContext inner) : ProxyTarget
{
    private readonly List<PageTarget> _pages = [];
    private EventHandler<IPage>? _pageCreated;
    private EventHandler<IBrowserContext>? _closed;
    private bool _isClosed;

    internal IBrowserContext Context { get; set; } = null!;

    internal bool CloseWithPage { get; set; }

    internal bool IsClosed => _isClosed || inner.IsClosed;

    internal float DefaultTimeout { get; private set; } = 30_000;

    internal float DefaultNavigationTimeout { get; private set; } = 30_000;

    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        switch (method.Name)
        {
            case "add_Page":
                _pageCreated += (EventHandler<IPage>) arguments[0]!;
                return null;
            case "remove_Page":
                _pageCreated -= (EventHandler<IPage>) arguments[0]!;
                return null;
            case "add_Close":
                _closed += (EventHandler<IBrowserContext>) arguments[0]!;
                return null;
            case "remove_Close":
                _closed -= (EventHandler<IBrowserContext>) arguments[0]!;
                return null;
            case "get_Browser":
                return owner.Proxy;
            case "get_BackgroundPages":
                return Array.Empty<IPage>();
            case "get_IsClosed":
                return IsClosed;
            case "get_Pages":
                return _pages.Where(x => !x.IsClosed).Select(x => x.Page).ToArray();
            case nameof(IBrowserContext.NewPageAsync):
                return NewPageAsync();
            case nameof(IBrowserContext.CloseAsync):
                OptionSupport.EnsureOnly(arguments[0], "IBrowserContext.CloseAsync");
                return CloseAsync();
            case nameof(IBrowserContext.SetDefaultTimeout):
                DefaultTimeout = (float) arguments[0]!;
                return null;
            case nameof(IBrowserContext.SetDefaultNavigationTimeout):
                DefaultNavigationTimeout = (float) arguments[0]!;
                return null;
            case nameof(IAsyncDisposable.DisposeAsync):
                return new ValueTask(CloseAsync());
            default:
                return Unsupported(method);
        }
    }

    internal async Task<IPage> NewPageAsync()
    {
        ObjectDisposedException.ThrowIf(_isClosed, this);
        var page = await inner.NewPageAsync().ConfigureAwait(false);
        var target = new PageTarget(this, page);
        target.Page = ProxyFactory.Create<IPage>(target);
        _pages.Add(target);

        var errors = new CleanupErrors();
        errors.Run(() => _pageCreated?.Invoke(Context, target.Page));
        if (errors.HasAny)
        {
            await errors.RunAsync(target.CloseAfterCreationFailureAsync).ConfigureAwait(false);
            errors.ThrowIfAny();
        }

        return target.Page;
    }

    private async Task CloseAsync()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        var errors = new CleanupErrors();
        await errors.RunAsync(inner.CloseAsync).ConfigureAwait(false);
        foreach (var page in _pages.ToArray())
        {
            errors.Run(page.ContextClosed);
        }

        _pages.Clear();
        owner.Remove(this);
        errors.Run(() => _closed?.Invoke(Context, Context));
        errors.ThrowIfAny();
    }

    internal async Task PageClosedAsync(PageTarget page)
    {
        _pages.Remove(page);
        if (CloseWithPage && !_isClosed)
        {
            await CloseAsync().ConfigureAwait(false);
        }
    }

    internal Task CloseAfterCreationFailureAsync() => CloseAsync();

    internal void BrowserClosed()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        var errors = new CleanupErrors();
        foreach (var page in _pages)
        {
            errors.Run(page.ContextClosed);
        }

        _pages.Clear();
        errors.Run(() => _closed?.Invoke(Context, Context));
        errors.ThrowIfAny();
    }
}

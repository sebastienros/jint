using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using JintPage = Jint.Browser.Page;
using JintResponse = Jint.Browser.PageResponse;
using JintWaitUntil = Jint.Browser.WaitUntilState;
using Microsoft.Playwright;

namespace Jint.Browser.Playwright;

internal sealed class PageTarget(BrowserContextTarget context, JintPage inner) : ProxyTarget
{
    private static readonly TimeSpan DefaultWaitPoll = TimeSpan.FromMilliseconds(25);
    private static readonly MethodInfo EvaluateMethod = typeof(PageTarget)
        .GetMethod(nameof(EvaluateAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;
    private EventHandler<IPage>? _closed;
    private float? _defaultTimeout;
    private float? _defaultNavigationTimeout;
    private bool _isClosed;
    private IFrame? _mainFrame;

    internal IPage Page { get; set; } = null!;

    internal bool IsClosed => _isClosed || inner.IsClosed;

    internal float DefaultTimeout => _defaultTimeout ?? context.DefaultTimeout;

    internal float DefaultNavigationTimeout => _defaultNavigationTimeout ?? context.DefaultNavigationTimeout;

    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        switch (method.Name)
        {
            case "add_Close":
                _closed += (EventHandler<IPage>) arguments[0]!;
                return null;
            case "remove_Close":
                _closed -= (EventHandler<IPage>) arguments[0]!;
                return null;
            case "get_Context":
                return context.Context;
            case "get_Frames":
                return new[] { MainFrame };
            case "get_MainFrame":
                return MainFrame;
            case "get_IsClosed":
                return IsClosed;
            case "get_Url":
                return inner.Url;
            case nameof(IPage.Locator):
                OptionSupport.EnsureOnly(arguments[1], "IPage.Locator");
                return Locator((string) arguments[0]!);
            case nameof(IPage.GetByRole):
                return RoleLocator((AriaRole) arguments[0]!, (PageGetByRoleOptions?) arguments[1]);
            case nameof(IPage.GotoAsync):
                return GotoAsync((string) arguments[0]!, (PageGotoOptions?) arguments[1]);
            case nameof(IPage.ReloadAsync):
                return ReloadAsync((PageReloadOptions?) arguments[0]);
            case nameof(IPage.GoBackAsync):
                return GoBackAsync((PageGoBackOptions?) arguments[0]);
            case nameof(IPage.GoForwardAsync):
                return GoForwardAsync((PageGoForwardOptions?) arguments[0]);
            case nameof(IPage.SetContentAsync):
                return SetContentAsync((string) arguments[0]!, (PageSetContentOptions?) arguments[1]);
            case nameof(IPage.ContentAsync):
                return inner.ContentAsync();
            case nameof(IPage.TitleAsync):
                return inner.TitleAsync();
            case nameof(IPage.EvaluateAsync):
                return EvaluateMethod.MakeGenericMethod(method.ReturnType.GenericTypeArguments[0])
                    .Invoke(this, [(string) arguments[0]!, arguments[1]])!;
            case nameof(IPage.WaitForFunctionAsync):
                return WaitForFunctionAsync(
                    (string) arguments[0]!,
                    arguments[1],
                    (PageWaitForFunctionOptions?) arguments[2]);
            case nameof(IPage.CloseAsync):
                OptionSupport.EnsureOnly(arguments[0], "IPage.CloseAsync");
                return CloseAsync();
            case nameof(IPage.SetDefaultTimeout):
                _defaultTimeout = (float) arguments[0]!;
                return null;
            case nameof(IPage.SetDefaultNavigationTimeout):
                _defaultNavigationTimeout = (float) arguments[0]!;
                return null;
            case nameof(IPage.BringToFrontAsync):
                return Task.CompletedTask;
            case nameof(IAsyncDisposable.DisposeAsync):
                return new ValueTask(CloseAsync());
            default:
                return Unsupported(method);
        }
    }

    internal IFrame MainFrame => _mainFrame ??= ProxyFactory.Create<IFrame>(new FrameTarget(this));

    private ILocator Locator(string selector)
        => ProxyFactory.Create<ILocator>(new LocatorTarget(this, LocatorDescriptor.Css(selector)));

    private ILocator RoleLocator(AriaRole role, PageGetByRoleOptions? options)
    {
        OptionSupport.EnsureOnly(
            options,
            "IPage.GetByRole",
            nameof(PageGetByRoleOptions.Name),
            nameof(PageGetByRoleOptions.NameString),
            nameof(PageGetByRoleOptions.NameRegex),
            nameof(PageGetByRoleOptions.Exact),
            nameof(PageGetByRoleOptions.IncludeHidden));
        return ProxyFactory.Create<ILocator>(new LocatorTarget(this, LocatorDescriptor.Role(role, options)));
    }

    private async Task<IResponse?> GotoAsync(string url, PageGotoOptions? options)
    {
        var timeout = Timeout(options?.Timeout, DefaultNavigationTimeout);
        var started = Stopwatch.GetTimestamp();
        var response = await inner.NavigateAsync(url, NavigationOptions(options, timeout)).ConfigureAwait(false);
        await WaitForNetworkIdleAsync(options?.WaitUntil, timeout, started).ConfigureAwait(false);
        return response is null ? null : ProxyFactory.Create<IResponse>(new ResponseTarget(this, response));
    }

    private async Task<IResponse?> ReloadAsync(PageReloadOptions? options)
    {
        var timeout = Timeout(options?.Timeout, DefaultNavigationTimeout);
        var started = Stopwatch.GetTimestamp();
        var response = await inner.ReloadAsync(new Jint.Browser.NavigationOptions
        {
            Timeout = timeout,
            WaitUntil = Map(options?.WaitUntil),
        }).ConfigureAwait(false);
        await WaitForNetworkIdleAsync(options?.WaitUntil, timeout, started).ConfigureAwait(false);
        return response is null ? null : ProxyFactory.Create<IResponse>(new ResponseTarget(this, response));
    }

    private async Task<IResponse?> GoBackAsync(PageGoBackOptions? options)
    {
        var timeout = Timeout(options?.Timeout, DefaultNavigationTimeout);
        var started = Stopwatch.GetTimestamp();
        var moved = await inner.GoBackAsync(timeout).ConfigureAwait(false);
        await WaitForLoadStateAsync(options?.WaitUntil, timeout, started, moved).ConfigureAwait(false);
        return moved && inner.Response is { } response
            ? ProxyFactory.Create<IResponse>(new ResponseTarget(this, response))
            : null;
    }

    private async Task<IResponse?> GoForwardAsync(PageGoForwardOptions? options)
    {
        var timeout = Timeout(options?.Timeout, DefaultNavigationTimeout);
        var started = Stopwatch.GetTimestamp();
        var moved = await inner.GoForwardAsync(timeout).ConfigureAwait(false);
        await WaitForLoadStateAsync(options?.WaitUntil, timeout, started, moved).ConfigureAwait(false);
        return moved && inner.Response is { } response
            ? ProxyFactory.Create<IResponse>(new ResponseTarget(this, response))
            : null;
    }

    private async Task SetContentAsync(string html, PageSetContentOptions? options)
    {
        var timeout = Timeout(options?.Timeout, DefaultNavigationTimeout);
        var started = Stopwatch.GetTimestamp();

        await inner.SetContentAsync(html, inner.Url).WaitAsync(timeout).ConfigureAwait(false);
        await WaitForNetworkIdleAsync(options?.WaitUntil, timeout, started).ConfigureAwait(false);
    }

    private Task<T> EvaluateAsync<T>(string expression, object? argument)
        => EvaluateValueAsync<T>(Scripts.Invocation(expression, argument));

    internal async Task<T> EvaluateValueAsync<T>(string script)
    {
        var value = await inner.EvaluateAndAwaitAsync(script).ConfigureAwait(false);
        if (value is null)
        {
            return default!;
        }

        if (value is T typed)
        {
            return typed;
        }

        var json = JsonSerializer.Serialize(value, value.GetType());
        return JsonSerializer.Deserialize<T>(json)!;
    }

    private async Task<IJSHandle> WaitForFunctionAsync(
        string expression,
        object? argument,
        PageWaitForFunctionOptions? options)
    {
        OptionSupport.EnsureOnly(
            options,
            "IPage.WaitForFunctionAsync",
            nameof(PageWaitForFunctionOptions.PollingInterval),
            nameof(PageWaitForFunctionOptions.Timeout));
        var invocation = Scripts.WaitInvocation(expression, argument);
        var timeout = Timeout(options?.Timeout, DefaultTimeout);
        var poll = options?.PollingInterval is > 0
            ? TimeSpan.FromMilliseconds(options.PollingInterval.Value)
            : DefaultWaitPoll;
        using var timeoutCancellation = timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? null
            : new CancellationTokenSource(timeout);
        var cancellationToken = timeoutCancellation?.Token ?? CancellationToken.None;

        try
        {
            while (true)
            {
                var evaluation = inner.EvaluateAndAwaitAsync<IDictionary<string, object?>>(
                    invocation,
                    cancellationToken);
                var result = await evaluation.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (result is not null
                    && result.TryGetValue("truthy", out var truthy)
                    && truthy is true)
                {
                    result.TryGetValue("value", out var value);
                    return ProxyFactory.Create<IJSHandle>(new JsHandleTarget(value));
                }

                await Task.Delay(poll, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (timeoutCancellation?.IsCancellationRequested is true)
        {
            throw new TimeoutException($"Timeout {DisplayTimeout(timeout)} exceeded.");
        }
    }

    private async Task CloseAsync()
    {
        if (IsClosed)
        {
            return;
        }

        _isClosed = true;
        var errors = new CleanupErrors();
        await errors.RunAsync(inner.CloseAsync).ConfigureAwait(false);
        errors.Run(() => _closed?.Invoke(Page, Page));
        await errors.RunAsync(() => context.PageClosedAsync(this)).ConfigureAwait(false);
        errors.ThrowIfAny();
    }

    internal void ContextClosed()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;
        _closed?.Invoke(Page, Page);
    }

    internal Task CloseAfterCreationFailureAsync() => CloseAsync();

    private static Jint.Browser.NavigationOptions NavigationOptions(PageGotoOptions? options, TimeSpan timeout) => new()
    {
        Referrer = options?.Referer,
        Timeout = timeout,
        WaitUntil = Map(options?.WaitUntil),
    };

    internal static TimeSpan Timeout(float? milliseconds, float defaultMilliseconds)
    {
        var value = milliseconds ?? defaultMilliseconds;
        return value <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(value);
    }

    internal static bool HasTimedOut(TimeSpan timeout, long started)
        => timeout != System.Threading.Timeout.InfiniteTimeSpan
            && Stopwatch.GetElapsedTime(started) >= timeout;

    internal static TimeSpan Remaining(TimeSpan timeout, long started)
        => timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? timeout
            : timeout - Stopwatch.GetElapsedTime(started);

    internal static string DisplayTimeout(TimeSpan timeout)
        => timeout == System.Threading.Timeout.InfiniteTimeSpan
            ? "the configured timeout"
            : $"{timeout.TotalMilliseconds:0}ms";

    private static JintWaitUntil Map(Microsoft.Playwright.WaitUntilState? state) => state switch
    {
        Microsoft.Playwright.WaitUntilState.Commit => JintWaitUntil.Commit,
        Microsoft.Playwright.WaitUntilState.DOMContentLoaded => JintWaitUntil.DomContentLoaded,
        _ => JintWaitUntil.Load,
    };

    private async Task WaitForNetworkIdleAsync(
        Microsoft.Playwright.WaitUntilState? state,
        TimeSpan timeout,
        long started)
    {
        if (state != Microsoft.Playwright.WaitUntilState.NetworkIdle)
        {
            return;
        }

        var remaining = Remaining(timeout, started);
        if ((remaining != System.Threading.Timeout.InfiniteTimeSpan && remaining <= TimeSpan.Zero)
            || !await inner.WaitForNetworkIdleAsync(remaining).ConfigureAwait(false))
        {
            throw new TimeoutException($"Timeout {DisplayTimeout(timeout)} exceeded.");
        }
    }

    private async Task WaitForLoadStateAsync(
        Microsoft.Playwright.WaitUntilState? state,
        TimeSpan timeout,
        long started,
        bool navigationStarted)
    {
        if (!navigationStarted || state == Microsoft.Playwright.WaitUntilState.Commit)
        {
            return;
        }

        var waitUntil = state ?? Microsoft.Playwright.WaitUntilState.Load;
        var expression = waitUntil == Microsoft.Playwright.WaitUntilState.DOMContentLoaded
            ? "document.readyState !== 'loading'"
            : "document.readyState === 'complete'";
        var remaining = Remaining(timeout, started);
        if ((remaining != System.Threading.Timeout.InfiniteTimeSpan && remaining <= TimeSpan.Zero)
            || !await inner.WaitForAsync(expression, remaining).ConfigureAwait(false))
        {
            throw new TimeoutException($"Timeout {DisplayTimeout(timeout)} exceeded.");
        }

        await WaitForNetworkIdleAsync(waitUntil, timeout, started).ConfigureAwait(false);
    }

    internal JintPage Inner => inner;
}

internal sealed class FrameTarget(PageTarget page) : ProxyTarget
{
    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        return method.Name switch
        {
            "get_Page" => page.Page,
            "get_Url" => page.Inner.Url,
            "get_Name" => string.Empty,
            "get_ParentFrame" => null,
            "get_ChildFrames" => Array.Empty<IFrame>(),
            "get_IsDetached" => false,
            nameof(IFrame.Locator) => Locator(
                (string) arguments[0]!,
                (FrameLocatorOptions?) arguments[1]),
            nameof(IFrame.ContentAsync) => page.Inner.ContentAsync(),
            nameof(IFrame.TitleAsync) => page.Inner.TitleAsync(),
            _ => Unsupported(method),
        };
    }

    private ILocator Locator(string selector, FrameLocatorOptions? options)
    {
        OptionSupport.EnsureOnly(options, "IFrame.Locator");
        return ProxyFactory.Create<ILocator>(new LocatorTarget(page, LocatorDescriptor.Css(selector)));
    }
}

internal sealed class LocatorTarget(PageTarget page, LocatorDescriptor descriptor) : ProxyTarget
{
    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        return method.Name switch
        {
            "get_Page" => page.Page,
            "get_First" => New(descriptor with { Index = 0 }),
            "get_Last" => New(descriptor with { Index = -1 }),
            "get_Description" => null,
            nameof(ILocator.Nth) => New(descriptor with { Index = (int) arguments[0]! }),
            nameof(ILocator.CountAsync) => CountAsync(),
            nameof(ILocator.AllTextContentsAsync) => AllTextContentsAsync(),
            nameof(ILocator.TextContentAsync) => TextContentAsync((LocatorTextContentOptions?) arguments[0]),
            nameof(ILocator.InputValueAsync) => InputValueAsync((LocatorInputValueOptions?) arguments[0]),
            nameof(ILocator.IsVisibleAsync) => IsVisibleAsync(),
            nameof(ILocator.WaitForAsync) => WaitForAsync((LocatorWaitForOptions?) arguments[0]),
            nameof(ILocator.ClickAsync) => ClickAsync((LocatorClickOptions?) arguments[0]),
            nameof(ILocator.FillAsync) => FillAsync((string) arguments[0]!, (LocatorFillOptions?) arguments[1]),
            nameof(ILocator.PressAsync) => PressAsync((string) arguments[0]!, (LocatorPressOptions?) arguments[1]),
            _ => Unsupported(method),
        };
    }

    private ILocator New(LocatorDescriptor value)
        => ProxyFactory.Create<ILocator>(new LocatorTarget(page, value));

    private async Task<int> CountAsync()
        => await page.EvaluateValueAsync<int>(descriptor.CountScript()).ConfigureAwait(false);

    private async Task<IReadOnlyList<string>> AllTextContentsAsync()
        => await page.EvaluateValueAsync<string[]>(descriptor.TextContentsScript()).ConfigureAwait(false);

    private async Task<string?> TextContentAsync(LocatorTextContentOptions? options)
    {
        OptionSupport.EnsureOnly(options, "ILocator.TextContentAsync", nameof(LocatorTextContentOptions.Timeout));
        await WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = options?.Timeout,
        }).ConfigureAwait(false);
        return await page.EvaluateValueAsync<string?>(descriptor.PropertyScript("textContent")).ConfigureAwait(false);
    }

    private async Task<string> InputValueAsync(LocatorInputValueOptions? options)
    {
        OptionSupport.EnsureOnly(options, "ILocator.InputValueAsync", nameof(LocatorInputValueOptions.Timeout));
        await WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = options?.Timeout,
        }).ConfigureAwait(false);
        return await page.EvaluateValueAsync<string>(descriptor.PropertyScript("value")).ConfigureAwait(false)
            ?? throw new PlaywrightException("The locator did not resolve to an input element.");
    }

    private async Task<bool> IsVisibleAsync()
    {
        await EnsureStrictAsync().ConfigureAwait(false);
        return await page.EvaluateValueAsync<bool>(descriptor.VisibleScript()).ConfigureAwait(false);
    }

    private async Task WaitForAsync(LocatorWaitForOptions? options)
    {
        OptionSupport.EnsureOnly(
            options,
            "ILocator.WaitForAsync",
            nameof(LocatorWaitForOptions.State),
            nameof(LocatorWaitForOptions.Timeout));
        var state = options?.State ?? WaitForSelectorState.Visible;
        var expression = state switch
        {
            WaitForSelectorState.Attached => descriptor.ExistsScript(),
            WaitForSelectorState.Detached => $"!({descriptor.ExistsScript()})",
            WaitForSelectorState.Hidden => $"!({descriptor.VisibleScript()})",
            _ => descriptor.VisibleScript(),
        };
        var timeout = PageTarget.Timeout(options?.Timeout, page.DefaultTimeout);
        if (!await page.Inner.WaitForAsync(expression, timeout).ConfigureAwait(false))
        {
            throw new TimeoutException($"Timeout {PageTarget.DisplayTimeout(timeout)} exceeded.");
        }

        await EnsureStrictAsync().ConfigureAwait(false);
    }

    private async Task ClickAsync(LocatorClickOptions? options)
    {
        OptionSupport.EnsureOnly(options, "ILocator.ClickAsync", nameof(LocatorClickOptions.Timeout));
        var timeout = PageTarget.Timeout(options?.Timeout, page.DefaultTimeout);
        var started = Stopwatch.GetTimestamp();
        await WaitForAsync(new LocatorWaitForOptions { Timeout = options?.Timeout }).ConfigureAwait(false);
        var index = await descriptor.ResolveIndexAsync(page.Inner).ConfigureAwait(false);
        var remaining = RequireRemaining(timeout, started);
        var clicked = index is not null
            && await page.Inner.ClickAsync(
                    descriptor.Selector,
                    index.Value,
                    new Jint.Browser.NavigationOptions { Timeout = remaining })
                .WaitAsync(remaining).ConfigureAwait(false);
        if (!clicked)
        {
            throw new PlaywrightException("The locator did not resolve to a clickable element.");
        }
    }

    private async Task FillAsync(string value, LocatorFillOptions? options)
    {
        OptionSupport.EnsureOnly(options, "ILocator.FillAsync", nameof(LocatorFillOptions.Timeout));
        var timeout = PageTarget.Timeout(options?.Timeout, page.DefaultTimeout);
        var started = Stopwatch.GetTimestamp();
        await WaitForAsync(new LocatorWaitForOptions { Timeout = options?.Timeout }).ConfigureAwait(false);
        var index = await descriptor.ResolveIndexAsync(page.Inner).ConfigureAwait(false);
        var remaining = RequireRemaining(timeout, started);
        var filled = index is not null
            && await page.Inner.FillAsync(descriptor.Selector, index.Value, value)
                .WaitAsync(remaining).ConfigureAwait(false);
        if (!filled)
        {
            throw new PlaywrightException("The locator did not resolve to an editable element.");
        }
    }

    private async Task PressAsync(string key, LocatorPressOptions? options)
    {
        OptionSupport.EnsureOnly(options, "ILocator.PressAsync", nameof(LocatorPressOptions.Timeout));
        var timeout = PageTarget.Timeout(options?.Timeout, page.DefaultTimeout);
        var started = Stopwatch.GetTimestamp();
        await WaitForAsync(new LocatorWaitForOptions { Timeout = options?.Timeout }).ConfigureAwait(false);
        var index = await descriptor.ResolveIndexAsync(page.Inner).ConfigureAwait(false);
        var remaining = RequireRemaining(timeout, started);
        var focused = index is not null
            && await page.Inner.TypeAsync(descriptor.Selector, index.Value, string.Empty)
                .WaitAsync(remaining).ConfigureAwait(false);
        if (!focused)
        {
            throw new PlaywrightException("The locator did not resolve to a focusable element.");
        }

        remaining = RequireRemaining(timeout, started);
        await page.Inner.PressAsync(
                key,
                new Jint.Browser.NavigationOptions { Timeout = remaining })
            .WaitAsync(remaining).ConfigureAwait(false);
    }

    private static TimeSpan RequireRemaining(TimeSpan timeout, long started)
    {
        var remaining = PageTarget.Remaining(timeout, started);
        if (remaining != System.Threading.Timeout.InfiniteTimeSpan && remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException($"Timeout {PageTarget.DisplayTimeout(timeout)} exceeded.");
        }

        return remaining;
    }

    private async Task EnsureStrictAsync()
    {
        if (descriptor.Index is not null)
        {
            return;
        }

        var count = await CountAsync().ConfigureAwait(false);
        if (count > 1)
        {
            throw new PlaywrightException(
                $"Strict mode violation: locator '{descriptor.Selector}' resolved to {count} elements.");
        }
    }
}

internal sealed class ResponseTarget(PageTarget page, JintResponse response) : ProxyTarget
{
    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        return method.Name switch
        {
            "get_Frame" => page.MainFrame,
            "get_FromServiceWorker" => false,
            "get_Headers" => response.Headers
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key.ToLowerInvariant(), x => string.Join(", ", x.Select(v => v.Value))),
            "get_Ok" => response.Ok,
            "get_Status" => response.Status,
            "get_StatusText" => response.StatusText,
            "get_Url" => response.Url,
            nameof(IResponse.AllHeadersAsync) => Task.FromResult(response.Headers
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => string.Join(", ", x.Select(v => v.Value)))),
            nameof(IResponse.HeaderValueAsync) => Task.FromResult(response.Header((string) arguments[0]!)),
            nameof(IResponse.HeadersArrayAsync) => Task.FromResult<IReadOnlyList<Header>>(
                response.Headers.Select(x => new Header { Name = x.Name, Value = x.Value }).ToArray()),
            nameof(IResponse.FinishedAsync) => Task.FromResult<string?>(null),
            _ => Unsupported(method),
        };
    }
}

internal sealed class JsHandleTarget(object? value) : ProxyTarget
{
    private static readonly MethodInfo JsonValueMethod = typeof(JsHandleTarget)
        .GetMethod(nameof(JsonValue), BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal override object? Invoke(MethodInfo method, object?[] arguments)
    {
        return method.Name switch
        {
            nameof(IJSHandle.AsElement) => null,
            nameof(IJSHandle.JsonValueAsync) => JsonValueMethod
                .MakeGenericMethod(method.ReturnType.GenericTypeArguments[0])
                .Invoke(this, null)!,
            nameof(IJSHandle.DisposeAsync) => ValueTask.CompletedTask,
            _ => Unsupported(method),
        };
    }

    private Task<T> JsonValue<T>()
    {
        if (value is T typed)
        {
            return Task.FromResult(typed);
        }

        var json = JsonSerializer.Serialize(value);
        return Task.FromResult(JsonSerializer.Deserialize<T>(json)!);
    }
}

internal readonly record struct LocatorDescriptor(
    string Selector,
    int? Index,
    string? Name,
    Regex? NameRegex,
    bool Exact,
    bool IncludeHidden)
{
    internal static LocatorDescriptor Css(string selector)
        => new(selector, null, null, null, false, true);

    internal static LocatorDescriptor Role(AriaRole role, PageGetByRoleOptions? options)
    {
        var selector = role switch
        {
            AriaRole.Button => "button,input[type=button],input[type=submit],input[type=reset],[role=button]",
            AriaRole.Checkbox => "input[type=checkbox],[role=checkbox]",
            AriaRole.Heading => "h1,h2,h3,h4,h5,h6,[role=heading]",
            AriaRole.Link => "a[href],[role=link]",
            AriaRole.Listitem => "li,[role=listitem]",
            AriaRole.Option => "option,[role=option]",
            AriaRole.Radio => "input[type=radio],[role=radio]",
            AriaRole.Textbox => "input:not([type]),input[type=text],input[type=email],input[type=search],textarea,[role=textbox]",
            _ => $"[role={role.ToString().ToLowerInvariant()}]",
        };

        return new(
            selector,
            null,
            options?.Name ?? options?.NameString,
            options?.NameRegex,
            options?.Exact ?? false,
            options?.IncludeHidden ?? false);
    }

    internal string CountScript()
        => Index is null
            ? $"({MatchesScript()}).length"
            : $"({ElementScript()} === null ? 0 : 1)";

    internal string TextContentsScript()
        => Index is null
            ? $"Array.from({MatchesScript()}, element => element.textContent ?? '')"
            : $"(() => {{ const element = {ElementScript()}; return element === null ? [] : [element.textContent ?? '']; }})()";

    internal string PropertyScript(string property)
        => $"(() => {{ const element = {ElementScript()}; return element == null ? null : element.{property}; }})()";

    internal string ExistsScript() => $"{ElementScript()} !== null";

    internal string VisibleScript()
        => $"(() => {{ const element = {ElementScript()}; if (element == null) return false; "
            + "const box = element.getBoundingClientRect(); "
            + "return (box.width !== 0 || box.height !== 0) && getComputedStyle(element).visibility === 'visible'; })()";

    internal async Task<int?> ResolveIndexAsync(JintPage page)
    {
        if (Name is null && NameRegex is null)
        {
            return Index ?? 0;
        }

        var index = await page.EvaluateAsync<int>(
            $"(() => {{ const all = Array.from(document.querySelectorAll({JsonSerializer.Serialize(Selector)})); "
            + $"const matches = {MatchesScript()}; const element = matches[{ResolvedIndexScript("matches")}]; "
            + "return element == null ? -1 : all.indexOf(element); })()").ConfigureAwait(false);
        return index < 0 ? null : index;
    }

    private string ElementScript()
        => $"(() => {{ const matches = {MatchesScript()}; return matches[{ResolvedIndexScript("matches")}] ?? null; }})()";

    private string MatchesScript()
    {
        var selector = JsonSerializer.Serialize(Selector);
        if (Name is null && NameRegex is null && IncludeHidden)
        {
            return $"Array.from(document.querySelectorAll({selector}))";
        }

        var name = JsonSerializer.Serialize(Name);
        var regex = NameRegex is null
            ? "null"
            : $"new RegExp({JsonSerializer.Serialize(NameRegex.ToString())}, {JsonSerializer.Serialize(NameRegex.Options.HasFlag(RegexOptions.IgnoreCase) ? "i" : "")})";

        return $"Array.from(document.querySelectorAll({selector})).filter(element => {{ "
            + "if (!" + IncludeHidden.ToString().ToLowerInvariant() + " && (element.closest('[aria-hidden=true]') || getComputedStyle(element).visibility !== 'visible')) return false; "
            + "const accessibleName = (element.getAttribute('aria-label') "
            + "?? (element.tagName === 'INPUT' ? element.value : element.textContent) ?? '').trim().replace(/\\s+/g, ' '); "
            + $"const expected = {name}; const pattern = {regex}; "
            + "if (pattern !== null) return pattern.test(accessibleName); "
            + "if (expected === null) return true; "
            + (Exact
                ? "return accessibleName === expected;"
                : "return accessibleName.toLowerCase().includes(expected.toLowerCase());")
            + " })";
    }

    private string ResolvedIndexScript(string collection)
        => Index is >= 0 ? Index.Value.ToString(CultureInfo.InvariantCulture)
            : Index is < 0 ? $"{collection}.length + {Index.Value.ToString(CultureInfo.InvariantCulture)}"
            : "0";
}

internal static class Scripts
{
    internal static string Invocation(string expression, object? argument)
    {
        var serialized = JsonSerializer.Serialize(argument, argument?.GetType() ?? typeof(object));
        return $"(() => {{ const value = ({expression}); return typeof value === 'function' ? value({serialized}) : value; }})()";
    }

    internal static string WaitInvocation(string expression, object? argument)
    {
        var serialized = JsonSerializer.Serialize(argument, argument?.GetType() ?? typeof(object));
        return $"(async () => {{ const candidate = ({expression}); "
            + $"const value = typeof candidate === 'function' ? await candidate({serialized}) : await candidate; "
            + "return { truthy: Boolean(value), value }; })()";
    }
}

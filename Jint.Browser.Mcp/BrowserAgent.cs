using System.Globalization;
using System.Net;
using Jint.WebApi.Fetch;

namespace Jint.Browser.Mcp;

/// <summary>
/// One agent's browsing session: an isolated <see cref="BrowserContext"/> and the page it drives.
/// </summary>
/// <remarks>
/// <para>
/// <b>A context per session is the isolation, and a browser per process is the cost.</b> Two sessions of one
/// server share no cookies, no <c>localStorage</c> and no pages, because those belong to a context; what they
/// share is the process, the options and the transport, which is what makes a second session cost a thread
/// rather than a browser.
/// </para>
/// <para>
/// <b>The page is opened on the first tool that needs one</b>, so a client that connects, lists the tools and
/// disconnects starts no thread at all — which is most of what a client does.
/// </para>
/// <para>
/// <b>One tool at a time.</b> The gate is not about thread safety — every <see cref="Page"/> member is
/// already safe from any thread — but about meaning: two tools interleaving on one page would let a snapshot
/// answer about a document a click was in the middle of replacing.
/// </para>
/// </remarks>
public sealed class BrowserAgent : IAsyncDisposable
{
    private readonly Browser _browser;
    private readonly BrowserAgentOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private BrowserContext? _context;
    private Page? _page;
    private bool _disposed;

    /// <summary>Creates a session over <paramref name="browser"/>.</summary>
    /// <param name="browser">The server's one browser, whose contexts the sessions are.</param>
    /// <param name="options">What the pages are built from and how long they are given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="browser"/> or <paramref name="options"/> is null.</exception>
    public BrowserAgent(Browser browser, BrowserAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(options);

        _browser = browser;
        _options = options;
    }

    /// <summary>Loads <paramref name="url"/> and answers where the page ended up.</summary>
    /// <param name="url">An absolute <c>http</c>, <c>https</c>, <c>data:</c> or <c>about:</c> URL.</param>
    /// <param name="waitUntil">How far to wait: <c>load</c>, <c>domcontentloaded</c>, <c>commit</c> or <c>networkidle</c>.</param>
    /// <returns>The page's URL, title and status after the navigation.</returns>
    /// <exception cref="BrowserToolException">The URL was not one this can load, or there was no document to show.</exception>
    public Task<PageState> NavigateAsync(string url, string? waitUntil = null)
        => RunAsync(async page =>
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var target) || target.Scheme is not ("http" or "https" or "data" or "about"))
            {
                throw new BrowserToolException(
                    $"'{url}' is not a URL this browser loads. Give an absolute http: or https: URL.");
            }

            var wait = WaitUntilFor(waitUntil);

            try
            {
                await page.NavigateAsync(url, new NavigationOptions
                {
                    Timeout = _options.Timeout,
                    WaitUntil = wait == "networkidle" ? WaitUntilState.Load : ParseWaitUntil(wait),
                }).ConfigureAwait(false);
            }
            catch (NavigationFailedException failure)
            {
                throw new BrowserToolException(failure.Message, failure);
            }

            if (wait == "networkidle")
            {
                await page.WaitForNetworkIdleAsync(_options.Timeout).ConfigureAwait(false);
            }

            return await StateAsync(page).ConfigureAwait(false);
        });

    /// <summary>Goes back one entry in the session history.</summary>
    /// <returns>Whether a step was taken, and where the page is now.</returns>
    public Task<ActionOutcome> BackAsync()
        => RunAsync(async page =>
        {
            var moved = await page.GoBackAsync(_options.Timeout).ConfigureAwait(false);
            return new ActionOutcome(moved, page.Url, moved ? "Went back." : "There is nothing to go back to.");
        });

    /// <summary>Goes forward one entry in the session history.</summary>
    /// <returns>Whether a step was taken, and where the page is now.</returns>
    public Task<ActionOutcome> ForwardAsync()
        => RunAsync(async page =>
        {
            var moved = await page.GoForwardAsync(_options.Timeout).ConfigureAwait(false);
            return new ActionOutcome(moved, page.Url, moved ? "Went forward." : "There is nothing to go forward to.");
        });

    /// <summary>Loads the current URL again.</summary>
    /// <returns>The page's URL, title and status after the reload.</returns>
    /// <exception cref="BrowserToolException">There was no document to show.</exception>
    public Task<PageState> ReloadAsync()
        => RunAsync(async page =>
        {
            try
            {
                await page.ReloadAsync(new NavigationOptions { Timeout = _options.Timeout }).ConfigureAwait(false);
            }
            catch (NavigationFailedException failure)
            {
                throw new BrowserToolException(failure.Message, failure);
            }

            return await StateAsync(page).ConfigureAwait(false);
        });

    /// <summary>Reads the page as the agent should read it.</summary>
    /// <param name="mode"><c>markdown</c>, <c>text</c> or <c>ax</c>.</param>
    /// <param name="mainContentOnly">Whether to narrow to the document's main content.</param>
    /// <param name="maxLength">A ceiling of the caller's own, narrowing the server's.</param>
    /// <returns>The representation, with whether it was cut.</returns>
    /// <exception cref="BrowserToolException"><paramref name="mode"/> names no representation.</exception>
    public Task<PageSnapshot> SnapshotAsync(string? mode = null, bool mainContentOnly = false, int? maxLength = null)
        => RunAsync(async page =>
        {
            var chosen = string.IsNullOrWhiteSpace(mode) ? "ax" : mode.Trim().ToLowerInvariant();
            var limit = maxLength is { } asked && asked > 0
                ? Math.Min(asked, _options.MaxSnapshotLength)
                : _options.MaxSnapshotLength;

            var content = chosen switch
            {
                "markdown" => await page.MarkdownAsync(mainContentOnly, limit).ConfigureAwait(false),
                "text" => await page.TextAsync(mainContentOnly, limit).ConfigureAwait(false),

                // The references are what make the answer actionable: click, fill and type all take one in
                // place of a selector, and an agent reading roles and names has no selector to write.
                "ax" => await page.AccessibilitySnapshotAsync(mainContentOnly, limit, includeReferences: true).ConfigureAwait(false),
                _ => throw new BrowserToolException($"'{mode}' is not a snapshot mode; they are markdown, text and ax."),
            };

            return new PageSnapshot(
                page.Url,
                await page.TitleAsync().ConfigureAwait(false),
                chosen,
                content,
                content.EndsWith("[truncated]", StringComparison.Ordinal));
        });

    /// <summary>Clicks what <paramref name="target"/> names.</summary>
    /// <param name="target">A <c>ref=</c> from an <c>ax</c> snapshot, or a CSS selector.</param>
    /// <returns>Whether an element matched, and where the page is now.</returns>
    public Task<ActionOutcome> ClickAsync(string target)
        => ActAsync(target, (page, t) => page.ClickAsync(t), "Clicked");

    /// <summary>Moves the pointer over what <paramref name="target"/> names.</summary>
    /// <param name="target">A <c>ref=</c> from an <c>ax</c> snapshot, or a CSS selector.</param>
    /// <returns>Whether an element matched, and where the page is now.</returns>
    public Task<ActionOutcome> HoverAsync(string target)
        => ActAsync(target, (page, t) => page.HoverAsync(t), "Hovered over");

    /// <summary>Replaces the value of the control <paramref name="target"/> names.</summary>
    /// <param name="target">A <c>ref=</c> from an <c>ax</c> snapshot, or a CSS selector.</param>
    /// <param name="text">The value to leave in the control.</param>
    /// <returns>Whether an element matched, and where the page is now.</returns>
    public Task<ActionOutcome> FillAsync(string target, string text)
        => ActAsync(target, (page, t) => page.FillAsync(t, text), "Filled");

    /// <summary>Types into what <paramref name="target"/> names, one key at a time.</summary>
    /// <param name="target">A <c>ref=</c> from an <c>ax</c> snapshot, or a CSS selector.</param>
    /// <param name="text">The characters to type; the control is not cleared first.</param>
    /// <returns>Whether an element matched, and where the page is now.</returns>
    public Task<ActionOutcome> TypeAsync(string target, string text)
        => ActAsync(target, (page, t) => page.TypeAsync(t, text), "Typed into");

    /// <summary>Selects an option of the <c>&lt;select&gt;</c> <paramref name="target"/> names.</summary>
    /// <param name="target">A <c>ref=</c> from an <c>ax</c> snapshot, or a CSS selector.</param>
    /// <param name="value">The option's value, or its text.</param>
    /// <returns>Whether a matching option was found, and where the page is now.</returns>
    public Task<ActionOutcome> SelectAsync(string target, string value)
        => ActAsync(target, (page, t) => page.SelectAsync(t, value), "Selected an option in");

    /// <summary>Presses one key at whatever the page has focused.</summary>
    /// <param name="key">A <c>KeyboardEvent.key</c> value: a character, or <c>Enter</c>, <c>Tab</c>, <c>Escape</c>…</param>
    /// <returns>Where the page is now, which <c>Enter</c> in a form will have moved.</returns>
    public Task<ActionOutcome> PressAsync(string key)
        => RunAsync(async page =>
        {
            await page.PressAsync(key).ConfigureAwait(false);
            return new ActionOutcome(true, page.Url, $"Pressed {key}.");
        });

    /// <summary>Scrolls the page to an offset.</summary>
    /// <param name="y">The offset from the top of the document, in CSS pixels.</param>
    /// <returns>Where the page is, and what it was scrolled to.</returns>
    public Task<ActionOutcome> ScrollAsync(double y)
        => RunAsync(async page =>
        {
            await page.ScrollToAsync(y).ConfigureAwait(false);
            return new ActionOutcome(
                true,
                page.Url,
                "Scrolled to " + y.ToString("0.##", CultureInfo.InvariantCulture) + ".");
        });

    /// <summary>Evaluates <paramref name="expression"/> in the page and answers its JSON form.</summary>
    /// <param name="expression">A JavaScript expression.</param>
    /// <returns>What <c>JSON.stringify</c> made of the result, in the page's own realm.</returns>
    /// <exception cref="BrowserToolException">The expression threw.</exception>
    public Task<string> EvaluateAsync(string expression)
        => RunAsync(async page =>
        {
            try
            {
                // Serialized by the page rather than by this process, so a Date goes through its toJSON and
                // nothing belonging to the engine has to cross out of it. Its own README says the same of the
                // command line's `eval`, because it is the same expression.
                var json = await page.EvaluateAsync<string>(
                    "(() => { const value = (" + expression + "); const json = JSON.stringify(value); return json === undefined ? 'null' : json; })()")
                    .ConfigureAwait(false);

                return json ?? "null";
            }
            catch (global::Jint.Runtime.JavaScriptException failure)
            {
                throw new BrowserToolException("The expression threw: " + failure.Message, failure);
            }
        });

    /// <summary>Waits for a selector to match, or for text to appear.</summary>
    /// <param name="selector">A CSS selector to wait for, or <see langword="null"/>.</param>
    /// <param name="text">Text to wait for in the document's rendered text, or <see langword="null"/>.</param>
    /// <param name="timeoutSeconds">The ceiling on the wait; the server's own when omitted.</param>
    /// <returns>Whether what was asked for appeared.</returns>
    /// <exception cref="BrowserToolException">Neither a selector nor text was given.</exception>
    public Task<ActionOutcome> WaitForAsync(string? selector = null, string? text = null, double? timeoutSeconds = null)
        => RunAsync(async page =>
        {
            var timeout = timeoutSeconds is { } seconds && seconds > 0
                ? TimeSpan.FromSeconds(Math.Min(seconds, _options.Timeout.TotalSeconds))
                : _options.Timeout;

            if (!string.IsNullOrWhiteSpace(selector))
            {
                var appeared = await page.WaitForSelectorAsync(selector, timeout).ConfigureAwait(false);
                return new ActionOutcome(appeared, page.Url, appeared ? $"'{selector}' is in the document." : $"'{selector}' did not appear.");
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                var appeared = await page.WaitForTextAsync(text, timeout).ConfigureAwait(false);
                return new ActionOutcome(appeared, page.Url, appeared ? $"'{text}' is on the page." : $"'{text}' did not appear.");
            }

            throw new BrowserToolException("Give either a selector or some text to wait for.");
        });

    /// <summary>Every request the page has made, oldest first.</summary>
    /// <returns>The request log, which is what a client's network panel shows.</returns>
    public Task<IReadOnlyList<RequestLine>> RequestsAsync()
        => RunAsync(page => Task.FromResult<IReadOnlyList<RequestLine>>(
            [.. page.Requests.Select(request => new RequestLine(
                request.Url,
                request.Method,
                request.Status,
                request.Initiator.ToString(),
                request.Failed,
                request.FailureReason ?? request.NotFetchedReason))]));

    /// <summary>The cookies this session holds.</summary>
    /// <param name="url">The origin to read them for; the page's own URL when omitted.</param>
    /// <returns>The cookies that would be sent to that origin.</returns>
    /// <exception cref="BrowserToolException">The origin is not one cookies belong to.</exception>
    public Task<IReadOnlyList<CookieLine>> CookiesAsync(string? url = null)
        => RunAsync(page =>
        {
            var origin = OriginOf(url ?? page.Url);
            var jar = page.Context.CookieJar as CookieContainerCookieJar
                ?? throw new BrowserToolException("This session's cookie jar cannot be enumerated.");

            var cookies = new List<CookieLine>();
            foreach (Cookie cookie in jar.Container.GetCookies(origin))
            {
                cookies.Add(new CookieLine(cookie.Name, cookie.Value, cookie.Domain, cookie.Path, cookie.HttpOnly, cookie.Secure));
            }

            return Task.FromResult<IReadOnlyList<CookieLine>>(cookies);
        });

    /// <summary>Sets one cookie for an origin.</summary>
    /// <param name="name">The cookie's name.</param>
    /// <param name="value">Its value.</param>
    /// <param name="url">The origin it belongs to; the page's own URL when omitted.</param>
    /// <returns>What was set.</returns>
    /// <exception cref="BrowserToolException">The origin is not one cookies belong to.</exception>
    public Task<ActionOutcome> SetCookieAsync(string name, string value, string? url = null)
        => RunAsync(page =>
        {
            var origin = OriginOf(url ?? page.Url);

            // Through the jar's own store, so the pair is subject to the rules a Set-Cookie header is and the
            // page's document.cookie sees exactly what a server-set cookie would have left.
            page.Context.CookieJar.StoreResponseCookies(origin, [name + "=" + value]);

            return Task.FromResult(new ActionOutcome(true, page.Url, $"Set {name} for {origin.Host}."));
        });

    /// <summary>Closes the page and the context, so the next tool starts a fresh session.</summary>
    /// <returns>What was closed.</returns>
    /// <remarks>
    /// The cookies, the storage and the history go with the context. It is what an agent finishing a task
    /// calls, and it is what disposing this does — so a client that simply disconnects loses nothing it
    /// would have got by calling it.
    /// </remarks>
    public async Task<ActionOutcome> CloseAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            await ReleaseAsync().ConfigureAwait(false);
            return new ActionOutcome(true, "about:blank", "The page and its context are closed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await ReleaseAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    /// <summary>Runs one tool against this session's page, opening one if there is none.</summary>
    private async Task<T> RunAsync<T>(Func<Page, Task<T>> work)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            return await work(await PageAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (TimeoutException failure)
        {
            // A Page call that ran out of MaxTaskDuration. It is the page's fault rather than the caller's,
            // and an agent needs to be told that rather than to be handed a transport error.
            throw new BrowserToolException("The page ran out of its time budget: " + failure.Message, failure);
        }
        catch (OperationCanceledException failure)
        {
            throw new BrowserToolException("The page was closed while the call was running.", failure);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The shape every element-taking tool shares: locate, act, and say where the page is.</summary>
    private Task<ActionOutcome> ActAsync(string target, Func<Page, string, Task<bool>> act, string verb)
        => RunAsync(async page =>
        {
            var done = await act(page, target).ConfigureAwait(false);

            return new ActionOutcome(
                done,
                page.Url,
                done
                    ? $"{verb} {target}."
                    : $"Nothing matched {target}. Take an ax snapshot and use one of its ref= values.");
        });

    private async Task<Page> PageAsync()
    {
        if (_page is { IsClosed: false })
        {
            return _page;
        }

        _context = await _browser.NewContextAsync(new BrowserContextOptions { UrlFilter = _options.UrlFilter }).ConfigureAwait(false);
        _page = await _context.NewPageAsync().ConfigureAwait(false);
        return _page;
    }

    private async Task ReleaseAsync()
    {
        var context = _context;
        _context = null;
        _page = null;

        if (context is not null)
        {
            await context.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task<PageState> StateAsync(Page page)
        => new(page.Url, await page.TitleAsync().ConfigureAwait(false), page.Response?.Status ?? 0);

    private static Uri OriginOf(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : throw new BrowserToolException($"'{url}' is not an http: or https: URL, and a cookie belongs to one.");

    private static string WaitUntilFor(string? waitUntil)
    {
        var wait = string.IsNullOrWhiteSpace(waitUntil) ? "load" : waitUntil.Trim().ToLowerInvariant();

        return wait is "load" or "domcontentloaded" or "commit" or "networkidle"
            ? wait
            : throw new BrowserToolException($"'{waitUntil}' is not a wait; they are load, domcontentloaded, commit and networkidle.");
    }

    private static WaitUntilState ParseWaitUntil(string wait) => wait switch
    {
        "commit" => WaitUntilState.Commit,
        "domcontentloaded" => WaitUntilState.DomContentLoaded,
        _ => WaitUntilState.Load,
    };
}

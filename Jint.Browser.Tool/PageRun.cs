using System.Net.Http;

namespace Jint.Browser.Tool;

/// <summary>How far a load waits, how long it may take, and what it carries.</summary>
internal sealed class LoadSettings
{
    private LoadSettings()
    {
    }

    /// <summary>The signal to wait for; <see cref="WaitUntil.Load"/> by default.</summary>
    internal WaitUntil WaitUntil { get; private init; }

    /// <summary>The ceiling on the load, and on the quiet period a <c>networkidle</c> wait allows.</summary>
    internal TimeSpan Timeout { get; private init; }

    /// <summary>The headers every request carries.</summary>
    internal IReadOnlyList<(string Name, string Value)> Headers { get; private init; } = [];

    /// <summary>The <c>name=value</c> pairs seeded into the context's jar before the load.</summary>
    internal IReadOnlyList<string> Cookies { get; private init; } = [];

    /// <summary>Adds the load options to a command's syntax.</summary>
    internal static void Declare(Dictionary<string, OptionKind> syntax)
    {
        syntax["wait-until"] = OptionKind.Value;
        syntax["timeout"] = OptionKind.Value;
        syntax["header"] = OptionKind.Repeated;
        syntax["cookie"] = OptionKind.Repeated;
    }

    /// <summary>Reads the load options off a parsed command line.</summary>
    internal static LoadSettings Read(CommandLine line) => new()
    {
        WaitUntil = line.Value("wait-until") is { } wait
            ? ValueSyntax.Word(
                "wait-until",
                wait,
                ("commit", WaitUntil.Commit),
                ("domcontentloaded", WaitUntil.DomContentLoaded),
                ("load", WaitUntil.Load),
                ("networkidle", WaitUntil.NetworkIdle))
            : WaitUntil.Load,
        Timeout = line.Value("timeout") is { } timeout
            ? ValueSyntax.Duration("timeout", timeout)
            : TimeSpan.FromSeconds(30),
        Headers = [.. line.Values("header").Select(ValueSyntax.Header)],
        Cookies = [.. line.Values("cookie").Select(ValueSyntax.Cookie)],
    };
}

/// <summary>The signals a load can wait for, which are the package's three plus the network's quiet.</summary>
internal enum WaitUntil
{
    /// <summary>The document has been committed; nothing of it has run yet.</summary>
    Commit,

    /// <summary>The parse has finished and <c>DOMContentLoaded</c> has fired.</summary>
    DomContentLoaded,

    /// <summary>Everything the parse asked for has loaded and <c>load</c> has fired.</summary>
    Load,

    /// <summary><c>load</c>, and then half a second in which the page made no request.</summary>
    NetworkIdle,
}

/// <summary>
/// One browser, one context, one page, and the document a command was pointed at, torn down together.
/// </summary>
/// <remarks>
/// <c>fetch</c> and <c>eval</c> differ in what they do with the loaded page and in nothing else, so opening
/// one is this class and neither command repeats it.
/// </remarks>
internal sealed class PageRun : IAsyncDisposable
{
    private readonly Browser _browser;
    private readonly HttpClient _client;

    private PageRun(Browser browser, Page page, PageResponse? response, HttpClient client)
    {
        _browser = browser;
        _client = client;
        Page = page;
        Response = response;
    }

    /// <summary>The page the document was loaded into.</summary>
    internal Page Page { get; }

    /// <summary>The response the document came from, or <see langword="null"/> for one that reached no network.</summary>
    internal PageResponse? Response { get; }

    /// <summary>Opens a browser, loads <paramref name="source"/>, and answers once the wait is satisfied.</summary>
    /// <exception cref="ToolUsageException">The command line asked for something that cannot be done.</exception>
    /// <exception cref="NavigationFailedException">There was no document to show.</exception>
    internal static async Task<PageRun> OpenAsync(BrowserSettings browser, LoadSettings load, PageSource source)
    {
        var options = browser.ToBrowserOptions();

        // A --header 'User-Agent: …' names the page's user agent rather than a client default, because the
        // package puts the page's own on every request and a default header cannot replace one that is
        // already on the request. It wins over --user-agent, being the more specific of the two.
        if (BrowserSettings.UserAgentFrom(load.Headers) is { } named)
        {
            options.UserAgent = named;
        }

        var client = BrowserSettings.CreateRequestClient(load.Headers);
        var instance = new Browser(options);

        try
        {
            var context = await instance.NewContextAsync(new BrowserContextOptions { HttpClient = client }).ConfigureAwait(false);
            SeedCookies(context, load.Cookies, source);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            var response = await LoadAsync(page, source, load).ConfigureAwait(false);

            return new PageRun(instance, page, response, client);
        }
        catch
        {
            await instance.DisposeAsync().ConfigureAwait(false);
            client.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync().ConfigureAwait(false);
        _client.Dispose();
    }

    /// <summary>Writes every error the page recorded, and says whether one of them was a budget failure.</summary>
    internal bool ReportErrors(TextWriter error)
    {
        var budgetExceeded = false;

        foreach (var pageError in Page.Errors)
        {
            budgetExceeded |= pageError.Kind == PageErrorKind.BudgetExceeded;
            error.WriteLine(pageError.ToString());
        }

        return budgetExceeded;
    }

    private static async Task<PageResponse?> LoadAsync(Page page, PageSource source, LoadSettings load)
    {
        PageResponse? response = null;

        if (source.IsFile)
        {
            // A file is content rather than a fetch, so there is no response and no phase to wait for: the
            // call already returns once the document has loaded and its scripts have run.
            await page.SetContentAsync(source.FileContent!, source.Url).ConfigureAwait(false);
        }
        else
        {
            response = await page.NavigateAsync(source.Url, new NavigationOptions
            {
                Timeout = load.Timeout,

                // NetworkIdle is not one of the package's three signals and cannot be: the driver raises
                // Committed, DomContentLoaded and Loaded from the parse, and the network's quiet is timed off
                // the loop afterwards. So a networkidle wait is a Load wait plus the quiet period, which is
                // also exactly what a client library does with it.
                WaitUntil = load.WaitUntil switch
                {
                    WaitUntil.Commit => WaitUntilState.Commit,
                    WaitUntil.DomContentLoaded => WaitUntilState.DomContentLoaded,
                    _ => WaitUntilState.Load,
                },
            }).ConfigureAwait(false);
        }

        if (load.WaitUntil == WaitUntil.NetworkIdle)
        {
            await page.WaitForNetworkIdleAsync(load.Timeout).ConfigureAwait(false);
        }

        return response;
    }

    private static void SeedCookies(BrowserContext context, IReadOnlyList<string> cookies, PageSource source)
    {
        if (cookies.Count == 0)
        {
            return;
        }

        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ToolUsageException("'--cookie' needs an http: or https: URL, because a cookie belongs to an origin");
        }

        // Through the jar's own store, so the pairs are subject to the same rules a Set-Cookie header is and
        // the page's document.cookie sees exactly what a server-set cookie would have left.
        foreach (var cookie in cookies)
        {
            context.CookieJar.StoreResponseCookies(uri, [cookie]);
        }
    }
}

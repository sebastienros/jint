using Jint.Browser;

namespace Jint.Tests.Browser.Navigation;

using Browser = global::Jint.Browser.Browser;

/// <summary>
/// A loopback server, a browser and one page open on it, torn down together.
/// </summary>
/// <remarks>
/// Every navigation suite wants the same four lines, and every one of them wants the context's
/// <c>UrlFilter</c> pinned to the server: a test that could reach anything else would be a test that could
/// hang on somebody's DNS.
/// </remarks>
internal sealed class LoopbackPage : IAsyncDisposable
{
    private LoopbackPage(LoopbackServer server, Browser browser, BrowserContext context, Page page)
    {
        Server = server;
        Browser = browser;
        Context = context;
        Page = page;
    }

    internal LoopbackServer Server { get; }

    internal Browser Browser { get; }

    internal BrowserContext Context { get; }

    internal Page Page { get; }

    internal string Url(string path) => Server.Url(path);

    internal static async Task<LoopbackPage> CreateAsync(
        Action<LoopbackServer>? routes = null,
        Action<BrowserContextOptions>? configureContext = null,
        Action<BrowserOptions>? configureBrowser = null)
    {
        var server = new LoopbackServer();
        routes?.Invoke(server);

        var browserOptions = new BrowserOptions();
        configureBrowser?.Invoke(browserOptions);

        var browser = new Browser(browserOptions);
        var contextOptions = new BrowserContextOptions { UrlFilter = server.Owns };
        configureContext?.Invoke(contextOptions);

        var context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
        var page = await context.NewPageAsync().ConfigureAwait(false);

        return new LoopbackPage(server, browser, context, page);
    }

    /// <summary>
    /// Runs a script that starts a navigation, and waits for that navigation to commit.
    /// </summary>
    /// <remarks>
    /// <b>The wait is armed before the script, and that order is the whole point.</b> A navigation a script
    /// starts runs off the page's own thread, so one registered afterwards can miss a commit that already
    /// happened — which is a test that passes on a quiet machine and fails on a busy one. Every caller wants
    /// this order, so it is here rather than written out nine times.
    /// </remarks>
    internal async Task NavigateByScriptAsync(string script)
    {
        var navigated = Page.WaitForNavigationAsync(TimeSpan.FromSeconds(10));
        await Page.EvaluateAsync(script).ConfigureAwait(false);

        (await navigated.ConfigureAwait(false))
            .Should().BeTrue("'" + script + "' should have started a navigation that committed");
    }

    /// <summary>Opens a second page in the same context, which therefore shares its cookies and storage.</summary>
    internal Task<Page> NewPageAsync() => Context.NewPageAsync();

    /// <summary>Opens a page in a second context of the same browser, which shares nothing.</summary>
    internal async Task<Page> NewIsolatedPageAsync()
    {
        var context = await Browser.NewContextAsync(new BrowserContextOptions { UrlFilter = Server.Owns }).ConfigureAwait(false);
        return await context.NewPageAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await Browser.CloseAsync().ConfigureAwait(false);
        Server.Dispose();
    }
}

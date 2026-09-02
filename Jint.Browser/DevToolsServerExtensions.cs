using Jint.Browser.DevTools;
using Jint.DevTools;

namespace Jint.Browser;

/// <summary>
/// Publishes a <see cref="Browser"/> on a Chrome DevTools Protocol server.
/// </summary>
/// <remarks>
/// This is the whole of the public surface the protocol layer adds to this package: everything else a client
/// reaches is the protocol itself.
/// </remarks>
public static class DevToolsServerExtensions
{
    /// <summary>Publishes every page of <paramref name="browser"/> on <paramref name="server"/>.</summary>
    /// <param name="server">The server a client connects to.</param>
    /// <param name="browser">The browser whose pages become targets.</param>
    /// <returns>A task that completes once every page the browser already has is published.</returns>
    /// <remarks>
    /// <para>
    /// Every existing page becomes a <c>page</c> target carrying its browser context, every page opened
    /// afterwards is published as it opens, and a page that closes stops being published. The server's
    /// <c>Target</c> domain then mints contexts and pages through the browser: <c>createBrowserContext</c>
    /// opens a <see cref="BrowserContext"/>, <c>createTarget</c> opens a <see cref="Page"/> in it and
    /// navigates, <c>closeTarget</c> closes the page and <c>disposeBrowserContext</c> closes the context.
    /// </para>
    /// <para>
    /// That is what makes <c>Puppeteer.ConnectAsync</c>, Playwright's <c>connectOverCDP</c> and the rest work
    /// against a browser in the same process — no native binary, no download, and a page per thread rather
    /// than a page per process.
    /// </para>
    /// <para>
    /// <b>One browser per server.</b> A second call with a different browser is refused, because the target
    /// commands a client sends are answered by whichever browser is registered and a client cannot say which
    /// it meant.
    /// </para>
    /// <para>
    /// <b>The endpoint is unauthenticated</b>, which is the protocol's design: anything that can reach it can
    /// run script in this process and read whatever the pages can. <c>DevToolsServerOptions.Host</c> defaults
    /// to loopback for that reason; do not bind it to a routable address.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await using var browser = new Browser();
    /// await using var server = new DevToolsServer(new DevToolsServerOptions { Port = 9222 });
    ///
    /// await server.AddBrowser(browser);
    /// server.Start();
    ///
    /// // …and from anywhere that can reach the port:
    /// //   var puppeteer = await Puppeteer.ConnectAsync(
    /// //       new ConnectOptions { BrowserWSEndpoint = server.BrowserWebSocketUrl });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="server"/> or <paramref name="browser"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The server already publishes a different browser.</exception>
    public static Task AddBrowser(this DevToolsServer server, Browser browser)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(browser);

        var host = new BrowserTargetHost(browser, server);
        server.UseHost(host);
        return host.StartAsync();
    }
}

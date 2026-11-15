using System.Globalization;
using Jint.DevTools;

namespace Jint.Browser.Tool;

/// <summary>
/// <c>jint-browser serve</c>: publishes a browser on the Chrome DevTools Protocol until Ctrl+C.
/// </summary>
/// <remarks>
/// <para>
/// One browser, one server, and one blank page so that a client which lists targets before it opens one
/// finds what a real browser would. Everything else a client asks for — a context, a page, a navigation — is
/// minted through the protocol's own <c>Target</c> domain.
/// </para>
/// <para>
/// <b>The endpoint is unauthenticated, which is the protocol's design</b>: anything that can reach the port
/// can run script in this process and read whatever the pages can. That is why <c>--host</c> defaults to
/// loopback and why the banner says so out loud.
/// </para>
/// </remarks>
internal static class ServeCommand
{
    /// <summary>The frame interval <c>serve</c> gives its pages when <c>--animation-frame-interval</c> is not given.</summary>
    internal static readonly TimeSpan ServeAnimationFrameInterval = TimeSpan.FromMilliseconds(1);

    /// <summary>The turn ceiling <c>serve</c> gives trusted pages when <c>--max-task-duration</c> is not given: none.</summary>
    /// <remarks>
    /// <para>
    /// <b>A protocol client owns the timeout.</b> Playwright gives a test 30 seconds, Puppeteer a call 30 more,
    /// and both end a stuck page by closing its target — which ends a running turn whatever the ceiling,
    /// because the page's own cancellation is registered with its engine separately. Chromium has no per-task
    /// ceiling at all, so a five-second one here failed what Chromium runs: one synchronous <c>axe.run</c> over
    /// a large page is a single task (#4619).
    /// </para>
    /// <para>
    /// <c>fetch</c> and <c>eval</c> keep the package's five seconds, because nothing else bounds a page they
    /// drive; <c>--untrusted</c> keeps its profile's ceiling; and the banner says which applies.
    /// </para>
    /// </remarks>
    internal static readonly TimeSpan ServeMaxTaskDuration = Timeout.InfiniteTimeSpan;

    /// <summary>Every option <c>serve</c> accepts.</summary>
    internal static Dictionary<string, OptionKind> Syntax()
    {
        var syntax = new Dictionary<string, OptionKind>(StringComparer.Ordinal)
        {
            ["port"] = OptionKind.Value,
            ["host"] = OptionKind.Value,
        };

        BrowserSettings.Declare(syntax);
        return syntax;
    }

    /// <summary>Starts the server, prints where it is, and runs until <paramref name="stopping"/> is signalled.</summary>
    internal static async Task<int> RunAsync(CommandLine line, TextWriter output, TextWriter error, CancellationToken stopping)
    {
        if (line.Positional.Count != 0)
        {
            throw new ToolUsageException("usage: jint-browser serve [options]");
        }

        var host = line.Value("host") ?? "127.0.0.1";
        var port = line.Value("port") is { } text ? ValueSyntax.Integer("port", text, minimum: 0) : 9222;

        if (port > 65535)
        {
            throw new ToolUsageException($"'--port {port.ToString(CultureInfo.InvariantCulture)}' is not a port");
        }

        var settings = BrowserSettings.Read(line);

        // A protocol client is automation, and automation waits for frames: Playwright's actionability check
        // waits for two before every click. There is nothing to paint, so serve answers a frame in a
        // millisecond rather than the package's 16 unless the command line says otherwise.
        var browserOptions = settings.ToBrowserOptions(ServeAnimationFrameInterval, ServeMaxTaskDuration);
        settings.HttpCache.ConfigureBrowser(browserOptions);
        await using var browser = new Browser(browserOptions);
        await using var server = new DevToolsServer(new DevToolsServerOptions { Host = host, Port = port });

        // AddBrowser awaits publication of existing pages. A page opened afterwards is registered in
        // the background, so create the initial tab first to make the ready banner a discovery barrier.
        await browser.NewPageAsync().ConfigureAwait(false);
        await server.AddBrowser(browser).ConfigureAwait(false);

        try
        {
            server.Start();
        }
        catch (Exception exception)
        {
            // A port already in use and an address that is not one are the two ways this fails, and both are
            // the command line's fault rather than a page's. Jint.Repl's --inspect says so and stops; so does
            // this.
            error.WriteLine($"cannot listen on {host}:{port.ToString(CultureInfo.InvariantCulture)}: {exception.Message}");
            return ExitCode.Usage;
        }

        PrintBanner(output, host, server, settings, browserOptions.MaxTaskDuration);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C, which is how this command ends. The two `await using`s above are the shutdown: the
            // server stops answering first, then every page thread is asked to stop and joined.
        }

        output.WriteLine("Stopping.");
        return ExitCode.Ok;
    }

    private static void PrintBanner(TextWriter output, string host, DevToolsServer server, BrowserSettings settings, TimeSpan maxTaskDuration)
    {
        var authority = host + ":" + server.BoundPort.ToString(CultureInfo.InvariantCulture);

        output.WriteLine($"Jint browser listening on http://{authority}");
        output.WriteLine($"  version: http://{authority}/json/version");
        output.WriteLine($"  browser: {server.BrowserWebSocketUrl}");
        output.WriteLine($"  targets: http://{authority}/json/list");
        output.WriteLine();
        output.WriteLine("  Connect Puppeteer, Playwright or chrome-remote-interface to the browser endpoint,");
        output.WriteLine($"  or open chrome://inspect, click Configure... and add {authority}.");
        output.WriteLine();
        output.WriteLine(settings.Untrusted
            ? "  Pages run the hardened profile for content nobody vouches for."
            : "  Pages run with no hardened profile; pass --untrusted for content nobody vouches for.");
        output.WriteLine(maxTaskDuration > TimeSpan.Zero
            ? $"  One task may run for at most {ValueSyntax.FormatDuration(maxTaskDuration)} (--max-task-duration)."
            : "  Tasks have no time ceiling, as in Chromium; the client's own timeouts bound them (--max-task-duration sets one).");
        output.WriteLine("  The endpoint is unauthenticated: anything that reaches it can run script in this process.");
        output.WriteLine();
        output.WriteLine("  Ctrl+C to stop.");
    }
}

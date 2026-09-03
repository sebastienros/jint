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

        await using var browser = new Browser(settings.ToBrowserOptions());
        await using var server = new DevToolsServer(new DevToolsServerOptions { Host = host, Port = port });

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

        // A browser opens with a tab. A client that lists targets before it creates one — every recorded
        // client does — would otherwise be told this browser has no pages.
        await browser.NewPageAsync().ConfigureAwait(false);

        PrintBanner(output, host, server, settings);

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

    private static void PrintBanner(TextWriter output, string host, DevToolsServer server, BrowserSettings settings)
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
        output.WriteLine("  The endpoint is unauthenticated: anything that reaches it can run script in this process.");
        output.WriteLine();
        output.WriteLine("  Ctrl+C to stop.");
    }
}

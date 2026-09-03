using Jint.Browser.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Jint.Browser.Tool;

/// <summary>
/// <c>jint-browser mcp</c>: a Model Context Protocol server over one browsing session, on standard input
/// and standard output.
/// </summary>
/// <remarks>
/// <para>
/// <b>Standard output is the protocol and nothing else.</b> Every diagnostic goes to standard error, which
/// is what the transport requires and what makes a stray <c>Console.WriteLine</c> a bug rather than noise.
/// This command therefore prints no banner: a client is on the other end, not a person.
/// </para>
/// <para>
/// <b>The process is the session</b>, which is why the whole browser is a singleton here. A client starts
/// this program, drives it and ends it, so one process is one agent's browsing session and its context is
/// that session's alone — and closing the process is what disposes it.
/// </para>
/// </remarks>
internal static class McpCommand
{
    /// <summary>Every option <c>mcp</c> accepts.</summary>
    internal static Dictionary<string, OptionKind> Syntax() => new(StringComparer.Ordinal)
    {
        // Accepted rather than required, so a client configuration may say which transport it means. It is
        // the only one there is, and why there is no --http is argued in Jint.Browser.Mcp/AGENTS.md and in
        // that package's README.
        ["stdio"] = OptionKind.Flag,
        ["trusted"] = OptionKind.Flag,
        ["user-agent"] = OptionKind.Value,
        ["max-task-duration"] = OptionKind.Value,
        ["memory-limit"] = OptionKind.Value,
        ["timeout"] = OptionKind.Value,
        ["max-snapshot-length"] = OptionKind.Value,
        ["block-private-network"] = OptionKind.Flag,
        ["allow-private-network"] = OptionKind.Flag,
    };

    /// <summary>Serves the protocol on standard input and output until the client goes away.</summary>
    internal static async Task<int> RunAsync(CommandLine line, TextWriter error, CancellationToken stopping)
    {
        if (line.Positional.Count != 0)
        {
            throw new ToolUsageException("usage: jint-browser mcp [--stdio] [options]");
        }

        var block = line.Flag("block-private-network");
        var allow = line.Flag("allow-private-network");

        if (block && allow)
        {
            throw new ToolUsageException("'--block-private-network' and '--allow-private-network' say opposite things; give one of them");
        }

        // Read here rather than inside the callback, so a value the command line got wrong is a usage error
        // before anything is built rather than an exception out of the composition root.
        var trusted = line.Flag("trusted");
        var userAgent = line.Value("user-agent");
        var maxTaskDuration = line.Value("max-task-duration") is { } duration ? ValueSyntax.Duration("max-task-duration", duration) : (TimeSpan?) null;
        var memoryLimit = line.Value("memory-limit") is { } memory ? ValueSyntax.Size("memory-limit", memory) : (long?) null;
        var timeout = line.Value("timeout") is { } wait ? ValueSyntax.Duration("timeout", wait) : (TimeSpan?) null;
        var snapshotLength = line.Value("max-snapshot-length") is { } length ? ValueSyntax.Integer("max-snapshot-length", length, minimum: 1) : (int?) null;

        // Empty rather than default: a command line tool has no appsettings.json and no environment prefix,
        // and a configuration provider reading either would be a surprise a client cannot see.
        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services
            .AddMcpServer(server => server.ServerInfo = new Implementation
            {
                Name = "jint-browser",
                Title = "Jint headless browser",
                Version = ToolProgram.Version,
            })
            .WithStdioServerTransport()
            .AddJintBrowser(agent =>
            {
                agent.Trusted = trusted;
                agent.BlockPrivateNetwork = block ? true : allow ? false : null;
                agent.UserAgent = userAgent;
                agent.MaxTaskDuration = maxTaskDuration;
                agent.MemoryLimit = memoryLimit;

                // Left alone when the command line did not say, so the package's own defaults are the ones a
                // reader of its README will find rather than a second set stated here.
                if (timeout is { } ceiling)
                {
                    agent.Timeout = ceiling;
                }

                if (snapshotLength is { } characters)
                {
                    agent.MaxSnapshotLength = characters;
                }
            });

        var host = builder.Build();

        try
        {
            await host.RunAsync(stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C, or the client closing the pipe. Both are how this command ends.
        }
        finally
        {
            // Asynchronously and explicitly: the browser and the session are IAsyncDisposable and nothing
            // else, so the provider has to be torn down the async way or the page threads outlive the
            // process's last statement.
            if (host is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                host.Dispose();
            }
        }

        // Nothing was written to standard output that was not the protocol, and nothing is written now.
        error.Flush();
        return ExitCode.Ok;
    }
}

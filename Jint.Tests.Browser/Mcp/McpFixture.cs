using System.IO.Pipelines;
using Jint.Browser.Mcp;
using Jint.Tests.Browser.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Jint.Tests.Browser.Mcp;

/// <summary>
/// A loopback origin, a Model Context Protocol server over it, and a real client on the other end of a pipe.
/// </summary>
/// <remarks>
/// <para>
/// <b>A real client and a real server, over a real transport.</b> The two ends are joined by a pair of
/// <see cref="Pipe"/>s rather than a socket or a process, which is what the SDK's own
/// <c>WithStreamServerTransport</c> and <c>StreamClientTransport</c> are for — so every message is
/// serialized, framed, parsed and dispatched exactly as it would be over stdio, and a test that passes here
/// is a test about the protocol rather than about a method call.
/// </para>
/// <para>
/// <b>The server is pinned to the loopback origin.</b> Its <c>UrlFilter</c> answers only for this fixture's
/// own server, so a test can never reach the network and the refusal path has something real to refuse.
/// </para>
/// </remarks>
internal sealed class McpFixture : IAsyncDisposable
{
    private readonly IHost _host;

    private McpFixture(LoopbackServer server, IHost host, McpClient client)
    {
        Server = server;
        _host = host;
        Client = client;
    }

    /// <summary>The origin the pages load from.</summary>
    internal LoopbackServer Server { get; }

    /// <summary>The client, which is what every test drives.</summary>
    internal McpClient Client { get; }

    /// <summary>The absolute URL of a path on the fixture's server.</summary>
    internal string Url(string path) => Server.Url(path);

    /// <summary>Starts a server over <paramref name="routes"/> and connects a client to it.</summary>
    internal static async Task<McpFixture> CreateAsync(
        Action<LoopbackServer>? routes = null,
        Action<BrowserAgentOptions>? configure = null)
    {
        var server = new LoopbackServer();
        routes?.Invoke(server);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateEmptyApplicationBuilder(null);

        builder.Services
            .AddMcpServer(options => options.ServerInfo = new Implementation { Name = "jint-browser-test", Version = "1.0.0" })
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .AddJintBrowser(agent =>
            {
                // The hardened profile stays on, which is the server's own default and therefore what these
                // tests should be run under; only the private-network block is lifted, because the origin
                // under test is loopback by construction.
                agent.BlockPrivateNetwork = false;
                agent.UrlFilter = server.Owns;
                agent.Timeout = TimeSpan.FromSeconds(30);
                configure?.Invoke(agent);
            });

        var host = builder.Build();
        await host.StartAsync().ConfigureAwait(false);

        var transport = new StreamClientTransport(
            serverInput: clientToServer.Writer.AsStream(),
            serverOutput: serverToClient.Reader.AsStream());

        var client = await McpClient.CreateAsync(transport).ConfigureAwait(false);

        return new McpFixture(server, host, client);
    }

    /// <summary>Calls a tool and answers what came back.</summary>
    internal async Task<CallToolResult> CallAsync(string tool, params (string Name, object? Value)[] arguments)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (name, value) in arguments)
        {
            parameters[name] = value;
        }

        return await Client.CallToolAsync(tool, parameters).ConfigureAwait(false);
    }

    /// <summary>The text a tool answered with, which is the JSON of its result or the reason it has none.</summary>
    internal static string TextOf(CallToolResult result)
        => result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync().ConfigureAwait(false);
        await _host.StopAsync().ConfigureAwait(false);

        // Asynchronously: the browser and the session are IAsyncDisposable and nothing else, so a synchronous
        // teardown would leave the page threads running past the end of the test.
        if (_host is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _host.Dispose();
        }

        Server.Dispose();
    }
}

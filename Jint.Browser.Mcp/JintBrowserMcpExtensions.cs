using Jint.Browser;
using Jint.Browser.Mcp;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Adds a headless browser to a Model Context Protocol server.
/// </summary>
/// <remarks>
/// The whole public entry point of this package, in the namespace the SDK's own builder extensions live in,
/// so a host composes it the way it composes everything else.
/// </remarks>
public static class JintBrowserMcpExtensions
{
    /// <summary>Registers the browser tools and resources on <paramref name="builder"/>.</summary>
    /// <param name="builder">The server being built.</param>
    /// <param name="configure">What the pages are built from; the hardened defaults when omitted.</param>
    /// <returns><paramref name="builder"/>, so calls chain.</returns>
    /// <remarks>
    /// <para>
    /// It registers one <see cref="Browser"/> and one <see cref="BrowserAgent"/> for the process, and the
    /// tool and resource types over them. <b>That is a session per process</b>, which is exactly right for
    /// the stdio transport — a client starts the program, drives it and ends it, so the process is the
    /// session and its browsing context is that session's alone.
    /// </para>
    /// <para>
    /// <b>Over HTTP it is not.</b> The 2026-07-28 revision of the protocol removed the session header from
    /// streamable HTTP, so the SDK's default is stateless and one server serves every caller — which would
    /// make this one browsing session shared by all of them. A host that serves HTTP and needs a session each
    /// registers its own scoping through <c>HttpServerTransportOptions.ConfigureSessionOptions</c> and
    /// <c>RunSessionHandler</c>, binding a <see cref="BrowserAgent"/> of its own per session and disposing it
    /// in that handler's <c>finally</c>; it does not call this.
    /// </para>
    /// <para>
    /// <b>The defaults are hardened.</b> <see cref="BrowserAgentOptions.Trusted"/> is off, so every page runs
    /// <c>BrowserOptions.ForUntrustedContent()</c> and the private network is refused — a client is by
    /// definition pointing this at content nobody vouched for.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IMcpServerBuilder AddJintBrowser(this IMcpServerBuilder builder, Action<BrowserAgentOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new BrowserAgentOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddSingleton(_ => new Browser(options.ToBrowserOptions()));
        builder.Services.TryAddSingleton(services => new BrowserAgent(services.GetRequiredService<Browser>(), options));

        return builder
            .WithTools<BrowserTools>()
            .WithResources<BrowserResources>();
    }
}

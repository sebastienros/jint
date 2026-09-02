namespace Jint.DevTools;

/// <summary>
/// How a <see cref="DevToolsServer"/> listens, what it calls itself, and what it refuses.
/// </summary>
/// <remarks>
/// Read once, when the server is constructed. Changing an instance afterwards changes nothing about a
/// server already built from it.
/// </remarks>
public sealed class DevToolsServerOptions
{
    /// <summary>The command timeout a target uses until a server tells it otherwise.</summary>
    internal static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Creates a set of options, each at its default.</summary>
    public DevToolsServerOptions()
    {
    }

    /// <summary>Gets or sets the address to listen on. Defaults to <c>127.0.0.1</c>.</summary>
    /// <remarks>
    /// <b>The default is loopback, and that is a security decision rather than a convenience.</b> A DevTools
    /// endpoint is unauthenticated by design and evaluates arbitrary script in the host process; a host that
    /// binds it to a routable address has published a remote code execution endpoint.
    /// </remarks>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>Gets or sets the port to listen on, or <c>0</c> for an ephemeral one. Defaults to <c>0</c>.</summary>
    /// <remarks>
    /// Chrome's own default is 9222, which is what a client with no configuration looks for; the default
    /// here is an ephemeral port, so that starting a server never collides with one already running. Read
    /// <see cref="DevToolsServer.BoundPort"/> afterwards for what was chosen.
    /// </remarks>
    public int Port { get; set; }

    /// <summary>Gets or sets what the server calls itself. Defaults to <c>Jint/</c> and the engine version.</summary>
    /// <remarks>
    /// It is what <c>Browser.getVersion</c> and <c>/json/version</c> answer, and therefore what a client
    /// believes it is driving. Naming Jint rather than a Chrome build is deliberate: a client that branches
    /// on the product should take its "unknown browser" path.
    /// </remarks>
    public string? Product { get; set; }

    /// <summary>
    /// Gets or sets how long a client waits for one command before it is told the engine is not being
    /// pumped. Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    /// It bounds the client's wait, not the command: a command that times out still runs when the engine is
    /// next pumped. See <see cref="ThreadMode.HostOwned"/>.
    /// </remarks>
    public TimeSpan CommandTimeout { get; set; } = DefaultCommandTimeout;

    /// <summary>
    /// Gets or sets how long a client's command may wait while the engine is paused in the debugger.
    /// Defaults to 30 seconds.
    /// </summary>
    /// <remarks>
    /// Nothing pauses yet: the debugger domain and the pause-time message loop arrive separately. It is
    /// declared now because the pause loop's bound is a host decision and a host writing its configuration
    /// today should not have to revisit it.
    /// </remarks>
    public TimeSpan PauseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets the largest message the server will accept. Defaults to 16 MiB.</summary>
    /// <remarks>
    /// A frame over the limit closes the connection with the WebSocket <c>1009</c> status rather than being
    /// buffered, because the alternative is letting one client decide how much of the host's memory the
    /// protocol may hold.
    /// </remarks>
    public int MaxMessageBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Gets or sets whether <c>Browser.close</c> closes the client's connection rather than the host.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Every client sends <c>Browser.close</c> on the way out, and a browser would exit. Jint is embedded in
    /// somebody's process, so the default reads it as "this client is done": the connection closes and the
    /// host keeps running. Set it to <see langword="false"/> for a host whose whole purpose is the endpoint
    /// — a debugging tool — and stop the server yourself when the command arrives.
    /// </remarks>
    public bool CloseIsDisconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets what builds an engine for <c>Target.createTarget</c> and <c>/json/new</c>, or
    /// <see langword="null"/> to refuse both.
    /// </summary>
    /// <remarks>
    /// <b>The factory runs on a transport thread</b>, so it must build an engine and nothing else; the
    /// engine is not touched again until the target that owns it runs it. Without a factory both entry
    /// points answer that no engine factory is configured, which is a truthful refusal rather than a
    /// pretence that a client's <c>newPage</c> worked.
    /// </remarks>
    public Func<Engine>? EngineFactory { get; set; }
}

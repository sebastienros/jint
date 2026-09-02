using System.Globalization;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol.Browser;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.DevTools;

/// <summary>
/// A Chrome DevTools Protocol endpoint over the engines a host gives it.
/// </summary>
/// <remarks>
/// <para>
/// The host end of the package: it listens, answers the discovery documents a client reads before it
/// connects, and turns each accepted WebSocket into a conversation. What a client can then do is decided by
/// the domains, and where each command runs is decided by the target it addresses.
/// </para>
/// <para>
/// <b>The endpoint is unauthenticated, and that is the protocol's design rather than an omission.</b>
/// Anything that can reach it can evaluate arbitrary script in the host process, which is why
/// <see cref="DevToolsServerOptions.Host"/> defaults to loopback. Do not bind it to a routable address, and
/// do not run one in production without a reason.
/// </para>
/// <para>
/// A server may be built and used without ever being started: <see cref="Start"/> is what opens a socket,
/// and everything else works the same for a host embedding the protocol without a port.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var engine = new Engine(options => options.UseDevTools());
///
/// await using var server = new DevToolsServer(new DevToolsServerOptions { Port = 9222 });
/// server.AddTarget(new EngineTarget(engine));
/// server.Start();
///
/// // …the host's own loop, which is what runs the engine and answers the protocol:
/// while (running)
/// {
///     engine.Tasks.ProcessTasks();
///     engine.Tasks.WaitForScheduledWork(TimeSpan.FromMilliseconds(50));
/// }
/// </code>
/// </example>
public sealed class DevToolsServer : IAsyncDisposable
{
    private readonly List<EngineTarget> _targets = [];
    private readonly List<EngineTarget> _owned = [];
    private readonly List<BrowserSession> _browserSessions = [];
    private readonly object _lock = new();

    private WebSocketServerTransport? _transport;
    private int _disposed;

    /// <summary>Creates a server over <paramref name="options"/>.</summary>
    /// <param name="options">How to listen and what to call itself, or the defaults.</param>
    public DevToolsServer(DevToolsServerOptions? options = null)
    {
        Options = options ?? new DevToolsServerOptions();
        Version = BrowserDomain.Version(Options.Product);
        BrowserId = Identifiers.New();
    }

    /// <summary>Gets the identifier the browser endpoint's path carries.</summary>
    public string BrowserId { get; }

    /// <summary>Gets the port the server is listening on, or <c>0</c> when it has not been started.</summary>
    /// <remarks>
    /// The answer to <see cref="DevToolsServerOptions.Port"/> being <c>0</c>: read this after
    /// <see cref="Start"/> for the ephemeral port the operating system chose.
    /// </remarks>
    public int BoundPort => _transport?.BoundPort ?? 0;

    /// <summary>Gets the address a client connects to for the browser endpoint.</summary>
    /// <exception cref="InvalidOperationException">The server has not been started.</exception>
    public string BrowserWebSocketUrl
    {
        get
        {
            if (_transport is null)
            {
                Throw.InvalidOperation("The server has no address until it is started.");
            }

            return string.Create(CultureInfo.InvariantCulture, $"ws://{Authority}/devtools/browser/{BrowserId}");
        }
    }

    /// <summary>Gets the targets a client can list and attach to, oldest first.</summary>
    public IReadOnlyList<EngineTarget> Targets
    {
        get
        {
            lock (_lock)
            {
                return _targets.ToArray();
            }
        }
    }

    /// <summary>Gets how this server is configured.</summary>
    internal DevToolsServerOptions Options { get; }

    /// <summary>Gets what <c>Browser.getVersion</c> and <c>/json/version</c> answer.</summary>
    internal GetVersionResponse Version { get; }

    /// <summary>Gets the host and port a discovery document names, which is only valid once started.</summary>
    internal string Authority => string.Create(CultureInfo.InvariantCulture, $"{Options.Host}:{BoundPort}");

    /// <summary>Starts listening.</summary>
    /// <exception cref="InvalidOperationException">The server is already started.</exception>
    /// <exception cref="ObjectDisposedException">The server has been disposed.</exception>
    public void Start()
    {
        ThrowIfDisposed();

        if (_transport is not null)
        {
            Throw.InvalidOperation("The server is already started.");
        }

        var transport = new WebSocketServerTransport(this);
        transport.Start();
        _transport = transport;
    }

    /// <summary>Starts listening, for a host whose start-up path is asynchronous.</summary>
    /// <param name="cancellationToken">Cancelled before the listener is opened, nothing is opened.</param>
    /// <remarks>
    /// Binding a listener is synchronous, so this is <see cref="Start"/> with a task around it rather than a
    /// separate implementation; it exists so a host need not break its own <c>async</c> chain.
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Start();
        return Task.CompletedTask;
    }

    /// <summary>Adds a target, telling every connected client that it appeared.</summary>
    /// <param name="target">The engine to publish.</param>
    /// <remarks>
    /// A client that asked for discovery is told through <c>Target.targetCreated</c>, and one that asked for
    /// auto-attach is attached and told through <c>Target.attachedToTarget</c>, before this returns. Both
    /// events reach the connection's writer queue rather than a socket, so this does not block on the
    /// network.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The server has been disposed.</exception>
    public void AddTarget(EngineTarget target)
    {
        if (target is null)
        {
            Throw.ArgumentNull(nameof(target));
        }

        ThrowIfDisposed();

        BrowserSession[] sessions;
        lock (_lock)
        {
            if (_targets.Contains(target))
            {
                return;
            }

            _targets.Add(target);
            sessions = _browserSessions.ToArray();
        }

        // The bound is the server's rather than the target's: a target may well have been built before the
        // server it ends up on.
        target.Dispatcher.CommandTimeout = Options.CommandTimeout;
        target.PauseTimeout = Options.PauseTimeout;

        foreach (var session in sessions)
        {
            Complete(session.TargetAddedAsync(target, CancellationToken.None));
        }
    }

    /// <summary>Removes a target, telling every connected client that it went away.</summary>
    /// <param name="target">The engine to stop publishing.</param>
    /// <returns><see langword="true"/> when the target was published and now is not.</returns>
    /// <remarks>
    /// Every session attached to it is detached first, so a client is told the attachment ended before it is
    /// told the target did. The <see cref="EngineTarget"/> itself is not disposed: it is the host's.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public bool RemoveTarget(EngineTarget target)
    {
        if (target is null)
        {
            Throw.ArgumentNull(nameof(target));
        }

        BrowserSession[] sessions;
        lock (_lock)
        {
            if (!_targets.Remove(target))
            {
                return false;
            }

            sessions = _browserSessions.ToArray();
        }

        foreach (var session in sessions)
        {
            Complete(session.TargetRemovedAsync(target, CancellationToken.None));
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_transport is { } transport)
        {
            await transport.DisposeAsync().ConfigureAwait(false);
        }

        EngineTarget[] owned;
        lock (_lock)
        {
            owned = _owned.ToArray();
            _owned.Clear();
            _targets.Clear();
            _browserSessions.Clear();
        }

        // Only the targets this server made: an engine the host handed over is still the host's, and
        // stopping its thread would be this package deciding the lifetime of something it does not own.
        foreach (var target in owned)
        {
            await target.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Finds a published target by the identifier a client addresses it with.</summary>
    internal EngineTarget? FindTarget(string targetId)
    {
        lock (_lock)
        {
            foreach (var target in _targets)
            {
                if (string.Equals(target.TargetId, targetId, StringComparison.Ordinal))
                {
                    return target;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Builds a target from <see cref="DevToolsServerOptions.EngineFactory"/> and publishes it.
    /// </summary>
    /// <remarks>
    /// The target is <see cref="ThreadMode.LibraryOwned"/> and its lifetime is the server's, because there
    /// is no host thread that ever agreed to pump it: a client asked for it, so the package runs it.
    /// </remarks>
    internal EngineTarget CreateTarget()
    {
        if (Options.EngineFactory is not { } factory)
        {
            return Throw.ServerError<EngineTarget>(
                "No engine factory is configured",
                "set DevToolsServerOptions.EngineFactory for a client to be able to create targets");
        }

        var target = new EngineTarget(factory(), new EngineTargetOptions { ThreadMode = ThreadMode.LibraryOwned });

        lock (_lock)
        {
            _owned.Add(target);
        }

        AddTarget(target);
        return target;
    }

    /// <summary>Removes a target at a client's request, disposing it when the server made it.</summary>
    internal async ValueTask CloseTargetAsync(EngineTarget target)
    {
        RemoveTarget(target);

        bool owned;
        lock (_lock)
        {
            owned = _owned.Remove(target);
        }

        if (owned)
        {
            await target.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Opens a conversation with the browser endpoint over <paramref name="connection"/>.</summary>
    internal BrowserSession OpenBrowserSession(IDevToolsConnection connection, Action? closeRequested = null)
    {
        var session = new BrowserSession(this, connection, closeRequested);

        lock (_lock)
        {
            _browserSessions.Add(session);
        }

        // Nothing is announced here, deliberately. A client is told about a target when it asks to be --
        // through setDiscoverTargets or setAutoAttach -- and both replay what already exists, so announcing
        // on connect would double every event for the client that then asks.
        return session;
    }

    /// <summary>Opens a conversation with one target over <paramref name="connection"/>, with no sessions in it.</summary>
    internal static TargetSession OpenTargetSession(IDevToolsConnection connection, EngineTarget target)
    {
        return TargetSession.Direct(new DevToolsSession(connection), target);
    }

    /// <summary>Forgets a conversation whose connection has gone, detaching everything it held.</summary>
    internal void CloseBrowserSession(BrowserSession session)
    {
        lock (_lock)
        {
            _browserSessions.Remove(session);
        }

        session.DetachAll();
    }

    /// <summary>
    /// Waits for an event that both shipped transports finish synchronously, so publishing a target is not
    /// an asynchronous operation a host has to await.
    /// </summary>
    private static void Complete(ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return;
        }

        task.AsTask().GetAwaiter().GetResult();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}

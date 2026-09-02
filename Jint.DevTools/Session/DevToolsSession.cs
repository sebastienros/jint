using System.Text.Json;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Transport;

namespace Jint.DevTools.Session;

/// <summary>
/// One addressable set of domains on one connection: the connection's own, or one attachment's, told apart
/// by the <c>sessionId</c> a client puts on the message.
/// </summary>
/// <remarks>
/// <para>
/// A session is a node. The <b>root</b> owns the connection, parses every envelope and writes every reply;
/// a <b>child</b> is what one <c>Target.attachToTarget</c> minted, carries the <c>sessionId</c> that reaches
/// it, and writes through its root. A message with no <c>sessionId</c> is answered by the root, one naming a
/// child by that child, and one naming nothing by <c>-32001</c>.
/// </para>
/// <para>
/// A session answers every message with exactly one reply, whatever went wrong. A client waiting on an
/// <c>id</c> that never comes back is a hang rather than an error, so every path out of
/// <see cref="HandleMessageAsync"/> writes something.
/// </para>
/// <para>
/// <b>Where a command runs is the gateway's decision, not the session's.</b> A session whose domains hold
/// engine state is given an <see cref="ICommandGateway"/> — <see cref="EngineDispatcher"/> — and every
/// command addressed to it crosses to the engine thread through that gateway before a domain sees it. A
/// session with no gateway (the browser session: <c>Schema</c>, <c>Browser</c>, <c>Target</c>) answers on
/// the transport thread, because none of it touches an engine.
/// </para>
/// </remarks>
internal sealed class DevToolsSession
{
    private readonly IDevToolsConnection? _connection;
    private readonly DevToolsSession _root;
    private readonly CommandRouter _router = new();
    private readonly object _childLock;
    private Dictionary<string, DevToolsSession>? _children;
    private ICommandGateway? _gateway;

    /// <summary>Creates the root session of <paramref name="connection"/>.</summary>
    internal DevToolsSession(IDevToolsConnection connection)
    {
        if (connection is null)
        {
            Throw.ArgumentNull(nameof(connection));
        }

        _connection = connection;
        _root = this;
        _childLock = new object();
        _connection.MessageReceived = HandleMessageAsync;
    }

    private DevToolsSession(DevToolsSession root, string sessionId)
    {
        _root = root;
        _childLock = root._childLock;
        SessionId = sessionId;
    }

    /// <summary>
    /// Gets the identifier a client addresses this session by, or <see langword="null"/> for a root session
    /// — which is both the browser endpoint and a direct <c>/devtools/page/</c> connection.
    /// </summary>
    internal string? SessionId { get; }

    /// <summary>Gets the domains registered on this session.</summary>
    internal IReadOnlyCollection<DevToolsDomain> Domains => _router.Domains;

    /// <summary>Registers one domain and tells it which session its events go out on.</summary>
    internal DevToolsSession Register(DevToolsDomain domain)
    {
        _router.Add(domain);
        domain.Attach(this);
        return this;
    }

    /// <summary>
    /// Sets what brings a command addressed to this session to the thread that may answer it.
    /// </summary>
    internal void UseGateway(ICommandGateway gateway)
    {
        _gateway = gateway;
    }

    /// <summary>Mints a child session under <paramref name="sessionId"/>, which must be unused.</summary>
    internal DevToolsSession CreateChild(string sessionId)
    {
        var child = new DevToolsSession(_root, sessionId);

        lock (_childLock)
        {
            var children = _root._children ??= new Dictionary<string, DevToolsSession>(StringComparer.Ordinal);
            if (!children.TryAdd(sessionId, child))
            {
                Throw.InvalidOperation($"The session '{sessionId}' is already attached to this connection.");
            }
        }

        return child;
    }

    /// <summary>Answers the child session <paramref name="sessionId"/> names, or <see langword="null"/>.</summary>
    /// <remarks>
    /// What a test reaches an attachment's own domains through. The routing itself does not use it — a
    /// message is resolved on the way in — but a check that every command the manifest names is overridden
    /// has to be able to ask which domains one attachment actually registered.
    /// </remarks>
    internal DevToolsSession? Child(string sessionId)
    {
        lock (_childLock)
        {
            return _root._children?.GetValueOrDefault(sessionId);
        }
    }

    /// <summary>Removes a child session, answering whether there was one.</summary>
    internal bool RemoveChild(string sessionId)
    {
        lock (_childLock)
        {
            return _root._children?.Remove(sessionId) == true;
        }
    }

    /// <summary>
    /// Answers one command against this session's own domains, on whichever thread calls it.
    /// </summary>
    /// <remarks>
    /// A gateway calls this once it has reached the thread the domains may be touched from; a session with
    /// no gateway is called straight from <see cref="HandleMessageAsync"/>.
    /// </remarks>
    internal ValueTask<string> DispatchAsync(in ProtocolRequest request, CommandContext context)
    {
        return _router.DispatchAsync(in request, context);
    }

    /// <summary>
    /// Answers one incoming message, writing exactly one response or error to the connection.
    /// </summary>
    internal async ValueTask HandleMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(message);
        }
        catch (JsonException exception)
        {
            // The only failure that is genuinely a parse error. Everything further in is a request the
            // reader understood and refused, or a payload a command refused, and each has its own code.
            await FailAsync(id: null, ProtocolErrorCodes.ParseError, "Message must be a valid JSON", ProtocolMessage.ParseErrorDetails(exception), sessionId: null, cancellationToken).ConfigureAwait(false);
            return;
        }

        long? id = null;
        string? sessionId = null;

        try
        {
            id = ProtocolMessage.ReadId(document.RootElement);
            var request = ProtocolMessage.Read(document.RootElement, id.Value);
            sessionId = request.SessionId;

            var session = Resolve(sessionId);
            var context = new CommandContext(session, sessionId, cancellationToken);

            var result = session._gateway is { } gateway
                ? await gateway.DispatchAsync(session, request, context).ConfigureAwait(false)
                : await session.DispatchAsync(in request, context).ConfigureAwait(false);

            await SendAsync(ProtocolMessage.WriteResponse(id.Value, result, sessionId), cancellationToken).ConfigureAwait(false);
        }
        catch (ProtocolException exception)
        {
            await FailAsync(id, exception.Code, exception.Message, exception.Details, sessionId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A command that failed for a reason the protocol has no code for is still a command the client
            // is waiting on. It gets the server error rather than the exception, and the host keeps running.
            await FailAsync(id, ProtocolErrorCodes.ServerError, exception.Message, details: null, sessionId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            document.Dispose();
        }
    }

    /// <summary>Sends one event, which no client is waiting on and which carries no identifier.</summary>
    /// <remarks>
    /// The <c>sessionId</c> is this session's own rather than a caller's: an event belongs to the session
    /// that raised it, and a domain never has to remember which attachment it is part of.
    /// </remarks>
    internal ValueTask SendEventAsync(in ProtocolEvent @event, CancellationToken cancellationToken = default)
    {
        return SendAsync(ProtocolMessage.WriteEvent(in @event, SessionId), cancellationToken);
    }

    /// <summary>Writes one finished message to the connection this session belongs to.</summary>
    internal ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
    {
        return _root._connection!.SendAsync(message, cancellationToken);
    }

    private DevToolsSession Resolve(string? sessionId)
    {
        if (sessionId is null)
        {
            return this;
        }

        lock (_childLock)
        {
            if (_root._children?.TryGetValue(sessionId, out var child) == true)
            {
                return child;
            }
        }

        return Throw.SessionNotFound<DevToolsSession>();
    }

    private ValueTask FailAsync(long? id, int code, string message, string? details, string? sessionId, CancellationToken cancellationToken)
    {
        return SendAsync(ProtocolMessage.WriteError(id, code, message, details, sessionId), cancellationToken);
    }
}

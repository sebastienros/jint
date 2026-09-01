using System.Text.Json;
using Jint.DevTools.Domains;
using Jint.DevTools.Protocol;
using Jint.DevTools.Transport;

namespace Jint.DevTools.Session;

/// <summary>
/// One client's conversation: a connection, the domains it may address, and the envelope discipline between
/// them.
/// </summary>
/// <remarks>
/// <para>
/// A session reads one message, answers it, and writes one response — always exactly one, whatever went
/// wrong. A client waiting on an <c>id</c> that never comes back is a hang rather than an error, so every
/// path out of <see cref="HandleMessageAsync"/> writes something.
/// </para>
/// <para>
/// Nothing here touches an engine, and by design: the session is what a transport thread hands text to, and
/// bringing that text to the engine thread is the dispatcher's job, which arrives with the WebSocket
/// transport. Until then a session runs wherever it was pumped from.
/// </para>
/// </remarks>
internal sealed class DevToolsSession
{
    private readonly IDevToolsConnection _connection;
    private readonly CommandRouter _router = new();

    internal DevToolsSession(IDevToolsConnection connection)
    {
        if (connection is null)
        {
            Throw.ArgumentNull(nameof(connection));
        }

        _connection = connection;
        _connection.MessageReceived = HandleMessageAsync;
    }

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

            var context = new CommandContext(this, request.SessionId, cancellationToken);
            var result = await _router.DispatchAsync(in request, context).ConfigureAwait(false);

            await _connection.SendAsync(ProtocolMessage.WriteResponse(id.Value, result, sessionId), cancellationToken).ConfigureAwait(false);
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
    internal ValueTask SendEventAsync(in ProtocolEvent @event, string? sessionId, CancellationToken cancellationToken = default)
    {
        return _connection.SendAsync(ProtocolMessage.WriteEvent(in @event, sessionId), cancellationToken);
    }

    private ValueTask FailAsync(long? id, int code, string message, string? details, string? sessionId, CancellationToken cancellationToken)
    {
        return _connection.SendAsync(ProtocolMessage.WriteError(id, code, message, details, sessionId), cancellationToken);
    }
}

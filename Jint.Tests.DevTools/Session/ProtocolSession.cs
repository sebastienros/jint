using System.Text.Json;
using Jint.DevTools;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// A conversation with the browser endpoint over an in-process connection, plus the one operation every
/// protocol test performs: send a message, read what came back.
/// </summary>
/// <remarks>
/// The server is real and simply never started, which is what makes these tests worth writing: they drive
/// exactly the code a socket drives, minus the socket.
/// </remarks>
internal sealed class ProtocolSession : IAsyncDisposable
{
    private readonly InProcessConnection _connection = new();
    private readonly DevToolsServer _server;
    private readonly bool _ownsServer;
    private int _nextId = 1000;

    private ProtocolSession(DevToolsServerOptions options, Action? closeRequested)
    {
        _server = new DevToolsServer(options);
        _ownsServer = true;
        Browser = _server.OpenBrowserSession(_connection, closeRequested);
    }

    private ProtocolSession(DevToolsServer server)
    {
        _server = server;
        Browser = server.OpenBrowserSession(_connection);
    }

    /// <summary>The conversation under test.</summary>
    internal BrowserSession Browser { get; }

    /// <summary>The server whose targets the conversation sees.</summary>
    internal DevToolsServer Server => _server;

    /// <summary>Every message the session has sent, oldest first.</summary>
    internal IReadOnlyList<string> Sent => _connection.Sent;

    /// <summary>Builds a session carrying the domains a browser endpoint answers.</summary>
    internal static ProtocolSession Create(Action? closeRequested = null, DevToolsServerOptions? options = null)
        => new(options ?? new DevToolsServerOptions(), closeRequested);

    /// <summary>
    /// Opens a second client's conversation with the same server, over a connection of its own.
    /// </summary>
    /// <remarks>
    /// Not a second attachment: <c>Target.attachToTarget</c> on a conversation that is already attached to a
    /// target answers the session it already has, which is right and is also why anything about <i>two</i>
    /// clients needs two connections. Disposing this one leaves the server alone; the conversation that made
    /// it owns that.
    /// </remarks>
    internal ProtocolSession OpenSecondConversation() => new(_server);

    /// <summary>Publishes a host-pumped engine target on the server.</summary>
    internal EngineTarget AddTarget(EngineTargetOptions? options = null, Engine? engine = null)
    {
        var target = new EngineTarget(engine ?? new Engine(), options);
        _server.AddTarget(target);
        return target;
    }

    /// <summary>Sends one command, addressed to a session or to the conversation itself.</summary>
    internal Task<JsonElement> SendAsync(string method, string? parameters = null, string? sessionId = null)
    {
        var identifier = Interlocked.Increment(ref _nextId);
        var payload = parameters is null ? "" : ",\"params\":" + parameters;
        var session = sessionId is null ? "" : ",\"sessionId\":\"" + sessionId + "\"";

        return RoundTripAsync($$"""{"id":{{identifier}},"method":"{{method}}"{{payload}}{{session}}}""");
    }

    /// <summary>Attaches to <paramref name="target"/> the way a client does, and hands back the session identifier.</summary>
    internal async Task<string> AttachAsync(DevToolsTarget target)
    {
        var reply = await SendAsync(
            "Target.attachToTarget",
            $$"""{"targetId":"{{target.TargetId}}","flatten":true}""").ConfigureAwait(false);

        reply.TryGetProperty("error", out var error).Should().BeFalse("attaching was expected to succeed, and it answered {0}", error);
        return reply.GetProperty("result").GetProperty("sessionId").GetString()!;
    }

    /// <summary>Sends one message and hands back the reply, parsed.</summary>
    internal async Task<JsonElement> RoundTripAsync(string message)
    {
        var before = _connection.Sent.Count;
        await _connection.PostAsync(message).ConfigureAwait(false);

        var sent = _connection.Sent;
        sent.Count.Should().BeGreaterThan(before, "a session answers every message with exactly one reply, or the client hangs");

        var identifier = Identifier(message);
        for (var i = sent.Count - 1; i >= before; i--)
        {
            using var candidate = JsonDocument.Parse(sent[i]);
            if (candidate.RootElement.TryGetProperty("id", out var id) && id.GetInt64() == identifier)
            {
                return candidate.RootElement.Clone();
            }
        }

        // A message with no readable identifier is answered with an error notification, which carries none.
        using var document = JsonDocument.Parse(sent[^1]);
        return document.RootElement.Clone();
    }

    /// <summary>Sends one message and hands back the reply's <c>result</c>.</summary>
    internal async Task<JsonElement> ResultOfAsync(string message)
    {
        var reply = await RoundTripAsync(message).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeFalse("the command was expected to succeed, and it answered {0}", error);
        return reply.GetProperty("result");
    }

    /// <summary>Sends one message and hands back the reply's <c>error</c>.</summary>
    internal async Task<JsonElement> ErrorOfAsync(string message)
    {
        var reply = await RoundTripAsync(message).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeTrue("the command was expected to fail, and it answered {0}", reply);
        return error;
    }

    /// <summary>Sends one command on an attachment and hands back its <c>result</c>, asserting success.</summary>
    internal async Task<JsonElement> ResultAsync(string method, string? parameters, string sessionId)
    {
        var reply = await SendAsync(method, parameters, sessionId).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeFalse("'{0}' was expected to succeed, and it answered {1}", method, error);
        return reply.GetProperty("result");
    }

    /// <summary>Sends one command on an attachment and hands back its <c>error</c>, asserting failure.</summary>
    internal async Task<JsonElement> ErrorAsync(string method, string? parameters, string sessionId)
    {
        var reply = await SendAsync(method, parameters, sessionId).ConfigureAwait(false);
        reply.TryGetProperty("error", out var error).Should().BeTrue("'{0}' was expected to fail, and it answered {1}", method, reply);
        return error;
    }

    /// <summary>
    /// Waits for the event at <paramref name="index"/> of <paramref name="method"/>, failing rather than
    /// hanging.
    /// </summary>
    /// <remarks>
    /// <b>Every wait here is bounded.</b> A protocol test that can hang is a continuous-integration leg that
    /// can hang, and an event that never arrives is exactly the defect these tests are looking for.
    /// </remarks>
    internal async Task<JsonElement> EventAsync(string method, int index = 0, int timeoutSeconds = 60)
    {
        var deadline = Environment.TickCount64 + (timeoutSeconds * 1000L);

        while (Environment.TickCount64 < deadline)
        {
            var events = EventsOf(method);
            if (events.Count > index)
            {
                return events[index].GetProperty("params");
            }

            await Task.Delay(5).ConfigureAwait(false);
        }

        Assert.Fail($"'{method}' number {index} never arrived within {timeoutSeconds} seconds.");
        return default;
    }

    /// <summary>Every event of <paramref name="method"/> the session has sent, oldest first.</summary>
    internal IReadOnlyList<JsonElement> EventsOf(string method)
    {
        var events = new List<JsonElement>();
        foreach (var message in _connection.Sent)
        {
            using var document = JsonDocument.Parse(message);
            if (document.RootElement.TryGetProperty("method", out var name) && name.GetString() == method)
            {
                events.Add(document.RootElement.Clone());
            }
        }

        return events;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_ownsServer)
        {
            _server.CloseBrowserSession(Browser);
            return;
        }

        foreach (var target in _server.AllTargets)
        {
            await target.CloseAsync().ConfigureAwait(false);
        }

        await _server.DisposeAsync().ConfigureAwait(false);
    }

    private static long Identifier(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("id", out var id) &&
                   id.ValueKind == JsonValueKind.Number &&
                   id.TryGetInt64(out var value)
                ? value
                : long.MinValue;
        }
        catch (JsonException)
        {
            return long.MinValue;
        }
    }
}

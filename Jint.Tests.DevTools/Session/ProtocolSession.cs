using System.Text.Json;
using Jint.DevTools.Domains;
using Jint.DevTools.Session;
using Jint.DevTools.Transport;

namespace Jint.Tests.DevTools.Session;

/// <summary>
/// A session with the built-in domains on an in-process connection, plus the one operation every protocol
/// test performs: send a message, read what came back.
/// </summary>
internal sealed class ProtocolSession
{
    private readonly InProcessConnection _connection = new();

    private ProtocolSession(Action? closeRequested)
    {
        Session = BuiltInDomains.RegisterOn(new DevToolsSession(_connection), closeRequested);
    }

    /// <summary>The session under test.</summary>
    internal DevToolsSession Session { get; }

    /// <summary>Every message the session has sent, oldest first.</summary>
    internal IReadOnlyList<string> Sent => _connection.Sent;

    /// <summary>Builds a session carrying the domains this package answers.</summary>
    internal static ProtocolSession Create(Action? closeRequested = null) => new(closeRequested);

    /// <summary>Sends one message and hands back the reply, parsed.</summary>
    internal async Task<JsonElement> RoundTripAsync(string message)
    {
        var before = _connection.Sent.Count;
        await _connection.PostAsync(message).ConfigureAwait(false);

        var sent = _connection.Sent;
        sent.Count.Should().Be(before + 1, "a session answers every message with exactly one reply, or the client hangs");

        // Parsed into a JsonDocument that outlives this call: the whole document is cloned so the caller can
        // read it after the document is disposed.
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
}

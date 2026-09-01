using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Jint.DevTools.Protocol;

/// <summary>
/// One command a client sent: <c>{ "id": 1, "method": "Runtime.evaluate", "params": {…}, "sessionId": "…" }</c>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ProtocolRequest(long Id, string Method, JsonElement? Parameters, string? SessionId);

/// <summary>
/// Reads incoming protocol messages and writes outgoing ones.
/// </summary>
/// <remarks>
/// <para>
/// The envelope is written with a <see cref="Utf8JsonWriter"/> rather than by serializing an envelope
/// object, because a result and an event's parameters arrive already serialized: splicing the fragment in
/// with <see cref="Utf8JsonWriter.WriteRawValue(string, bool)"/> costs one validation pass, where an
/// envelope type would mean re-parsing it into a <see cref="JsonElement"/> first.
/// </para>
/// <para>
/// The wording of every failure comes from Chromium's <c>crdtp/dispatch.cc</c>, which is what clients were
/// written against. Only <c>-32601</c>'s is load-bearing enough for a test to pin verbatim.
/// </para>
/// </remarks>
internal static class ProtocolMessage
{
    /// <summary>
    /// Reads the request identifier, which has to come first: it is what an error response is addressed to,
    /// and a message that has none is answered with an error notification instead.
    /// </summary>
    internal static long ReadId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Throw.InvalidRequest("Message must be an object");
        }

        if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var value))
        {
            return value;
        }

        return Throw.InvalidRequest<long>("Message must have integer 'id' property");
    }

    /// <summary>
    /// Reads the rest of the message, once <see cref="ReadId"/> has said what to address a failure to.
    /// </summary>
    internal static ProtocolRequest Read(JsonElement root, long id)
    {
        if (!root.TryGetProperty("method", out var method) || method.ValueKind != JsonValueKind.String)
        {
            Throw.InvalidRequest("Message must have string 'method' property");
        }

        JsonElement? parameters = null;
        if (root.TryGetProperty("params", out var declared) && declared.ValueKind != JsonValueKind.Null)
        {
            if (declared.ValueKind != JsonValueKind.Object)
            {
                Throw.InvalidRequest("Message has property 'params' of type other than object");
            }

            parameters = declared;
        }

        string? sessionId = null;
        if (root.TryGetProperty("sessionId", out var session) && session.ValueKind != JsonValueKind.Null)
        {
            if (session.ValueKind != JsonValueKind.String)
            {
                Throw.InvalidRequest("Message must have string 'sessionId' property");
            }

            sessionId = session.GetString();
        }

        return new ProtocolRequest(id, method.GetString()!, parameters, sessionId);
    }

    /// <summary>Writes the response to a command that succeeded.</summary>
    internal static string WriteResponse(long id, string resultJson, string? sessionId)
    {
        var buffer = new ArrayBufferWriter<byte>(resultJson.Length + 64);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", id);
            writer.WritePropertyName("result");
            writer.WriteRawValue(resultJson);
            WriteSessionId(writer, sessionId);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Writes the response to a command that failed, or — when <paramref name="id"/> is
    /// <see langword="null"/> because the message never got as far as having one — the error notification
    /// Chrome sends in its place.
    /// </summary>
    internal static string WriteError(long? id, int code, string message, string? details, string? sessionId)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (id is { } value)
            {
                writer.WriteNumber("id", value);
            }

            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            if (details is not null)
            {
                writer.WriteString("data", details);
            }

            writer.WriteEndObject();
            WriteSessionId(writer, sessionId);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Writes an event, which carries no identifier because nothing is waiting for it.</summary>
    internal static string WriteEvent(in ProtocolEvent @event, string? sessionId)
    {
        var buffer = new ArrayBufferWriter<byte>(@event.ParametersJson.Length + 64);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("method", @event.Method);
            writer.WritePropertyName("params");
            writer.WriteRawValue(@event.ParametersJson);
            WriteSessionId(writer, sessionId);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Splits <c>Domain.method</c>, refusing anything else with the method-not-found error the protocol uses
    /// for a name no domain could own.
    /// </summary>
    internal static (string Domain, string Member) SplitMethod(string method)
    {
        var separator = method.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == method.Length - 1)
        {
            Throw.MethodNotFound(method);
        }

        return (method.Substring(0, separator), method.Substring(separator + 1));
    }

    private static void WriteSessionId(Utf8JsonWriter writer, string? sessionId)
    {
        if (sessionId is not null)
        {
            writer.WriteString("sessionId", sessionId);
        }
    }

    /// <summary>
    /// What a <see cref="JsonException"/> from the top-level parse becomes: the protocol's parse error,
    /// carrying the position the reader stopped at.
    /// </summary>
    internal static string ParseErrorDetails(JsonException exception)
    {
        return exception.LineNumber is { } line && exception.BytePositionInLine is { } position
            ? string.Create(CultureInfo.InvariantCulture, $"line {line}, position {position}")
            : exception.Message;
    }
}

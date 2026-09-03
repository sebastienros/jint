using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Protocol;

namespace Jint.Browser.Mcp;

/// <summary>
/// What a tool answers with, and how it is written.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every result is serialized through a source-generated context</b>, the discipline
/// <c>Jint.DevTools</c> keeps for the same reason: the first reflective serialization is the one that fails
/// in a published binary rather than in a test, and it fails where nothing was looking.
/// </para>
/// <para>
/// <b>Both halves of a result are filled.</b> <c>content</c> carries the JSON as text, because that is what
/// a model reads and what every client displays; <c>structuredContent</c> carries the same value, because
/// that is what a client that wants to bind to it reads. They are the same bytes, so the two can never
/// disagree.
/// </para>
/// </remarks>
internal static class ToolJson
{
    /// <summary>The one serializer context every tool answers through.</summary>
    internal static ToolJsonContext Default => ToolJsonContext.Default;

    /// <summary>A result, as JSON text and as structured content.</summary>
    internal static CallToolResult Ok<T>(T value, JsonTypeInfo<T> shape)
    {
        var json = JsonSerializer.Serialize(value, shape);

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            StructuredContent = JsonSerializer.Deserialize(json, ToolJsonContext.Default.JsonElement),
        };
    }

    /// <summary>A result that is already the text to answer with.</summary>
    internal static CallToolResult Text(string text)
        => new() { Content = [new TextContentBlock { Text = text }] };

    /// <summary>What could not be done, said in one sentence rather than thrown.</summary>
    internal static CallToolResult Failed(string message)
        => new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }],
        };
}

/// <summary>The shapes a tool answers with, generated rather than reflected over.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(PageState))]
[JsonSerializable(typeof(PageSnapshot))]
[JsonSerializable(typeof(ActionOutcome))]
[JsonSerializable(typeof(IReadOnlyList<RequestLine>))]
[JsonSerializable(typeof(IReadOnlyList<CookieLine>))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class ToolJsonContext : JsonSerializerContext;

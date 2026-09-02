using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jint.Browser.Accessibility;

/// <summary>
/// One computed accessibility value in the shape the Chrome DevTools Protocol's
/// <c>Accessibility.AXValue</c> has on the wire.
/// </summary>
/// <remarks>
/// The value is written as the JSON primitive its type calls for — a boolean for <c>boolean</c>, a number
/// for <c>integer</c> and <c>number</c>, a string for the rest — because the protocol's <c>value</c> is
/// <c>any</c> and a DevTools front end reads it as its type says.
/// </remarks>
[JsonConverter(typeof(AxProtocolValueConverter))]
internal sealed record AxProtocolValue(string Type, AxValue Value);

/// <summary>One name-and-value pair in the shape of the protocol's <c>Accessibility.AXProperty</c>.</summary>
internal sealed record AxProtocolProperty(string Name, AxProtocolValue Value);

/// <summary>
/// One node in the shape of the Chrome DevTools Protocol's <c>Accessibility.AXNode</c>.
/// </summary>
/// <remarks>
/// It exists here rather than in <c>Jint.DevTools</c> because this package does not reference that one:
/// the accessibility tree is computed over AngleSharp's DOM alone, and the protocol domain that serves it
/// maps this record onto its generated <c>AXNode</c> in one statement. <c>backendDOMNodeId</c> is therefore
/// always <see langword="null"/> here — only a session that owns a DOM node registry can fill it in.
/// </remarks>
internal sealed record AxProtocolNode
{
    /// <summary>The node's identifier, as the protocol's string-typed <c>AXNodeId</c>.</summary>
    [JsonPropertyName("nodeId")]
    public required string NodeId { get; init; }

    /// <summary>Whether an assistive technology skips this node.</summary>
    [JsonPropertyName("ignored")]
    public required bool Ignored { get; init; }

    /// <summary>Why the node is ignored, when it is.</summary>
    [JsonPropertyName("ignoredReasons")]
    public IReadOnlyList<AxProtocolProperty>? IgnoredReasons { get; init; }

    /// <summary>The computed role.</summary>
    [JsonPropertyName("role")]
    public AxProtocolValue? Role { get; init; }

    /// <summary>The computed accessible name.</summary>
    [JsonPropertyName("name")]
    public AxProtocolValue? Name { get; init; }

    /// <summary>The computed accessible description.</summary>
    [JsonPropertyName("description")]
    public AxProtocolValue? Description { get; init; }

    /// <summary>The widget's value.</summary>
    [JsonPropertyName("value")]
    public AxProtocolValue? Value { get; init; }

    /// <summary>Everything else the node states.</summary>
    [JsonPropertyName("properties")]
    public IReadOnlyList<AxProtocolProperty>? Properties { get; init; }

    /// <summary>The identifier of this node's parent, absent on the root.</summary>
    [JsonPropertyName("parentId")]
    public string? ParentId { get; init; }

    /// <summary>The identifiers of this node's children, in tree order.</summary>
    [JsonPropertyName("childIds")]
    public IReadOnlyList<string>? ChildIds { get; init; }

    /// <summary>The DOM node the protocol session knows this node by, which only a session can supply.</summary>
    [JsonPropertyName("backendDOMNodeId")]
    public int? BackendDomNodeId { get; init; }
}

/// <summary>Writes an <see cref="AxProtocolValue"/> as the protocol's <c>{ type, value }</c> object.</summary>
internal sealed class AxProtocolValueConverter : JsonConverter<AxProtocolValue>
{
    public override AxProtocolValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("Accessibility values are written, never read.");

    public override void Write(Utf8JsonWriter writer, AxProtocolValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.Type);

        var inner = value.Value;
        switch (inner.Type)
        {
            case AxValueType.Boolean:
                writer.WriteBoolean("value", inner.Flag ?? false);
                break;

            case AxValueType.Integer:
            case AxValueType.Number:
                writer.WriteNumber("value", inner.Numeric ?? 0);
                break;

            default:
                if (inner.Text is not null)
                {
                    writer.WriteString("value", inner.Text);
                }

                break;
        }

        writer.WriteEndObject();
    }
}

/// <summary>The source-generated serializer for the protocol shape of an accessibility tree.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AxProtocolNode))]
[JsonSerializable(typeof(IReadOnlyList<AxProtocolNode>))]
internal sealed partial class AxProtocolJsonContext : JsonSerializerContext;

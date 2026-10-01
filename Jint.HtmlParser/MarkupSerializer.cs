using Jint.HtmlParser.Serialization;

namespace Jint.HtmlParser;

/// <summary>Serializes native trees directly to HTML or XML, without mutating the tree or executing host code.</summary>
/// <remarks>
/// The selected method determines the format, not the node's document or namespace. Strings contain
/// no added encoding declaration or byte-order mark. Concurrent mutation is unsupported; detected
/// mutation throws <see cref="InvalidOperationException"/> and discards the unpublished output.
/// </remarks>
public static class MarkupSerializer
{
    /// <summary>Serializes a whole node as HTML. Attached shadow roots are omitted unless selected.</summary>
    public static string ToHtml(Node node, HtmlSerializationOptions? options = null,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default) =>
        HtmlMarkupSerializer.Serialize(node, options, limits, cancellationToken: cancellationToken);

    /// <summary>Serializes the children of an element, document or fragment as HTML; templates use their content.</summary>
    public static string ToHtmlChildren(Node parent, HtmlSerializationOptions? options = null,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default) =>
        HtmlMarkupSerializer.SerializeChildren(parent, options, limits, cancellationToken: cancellationToken);

    /// <summary>Serializes a whole node as XML, repairing namespace prefixes in the output only.</summary>
    /// <param name="node">The node to serialize.</param>
    /// <param name="requireWellFormed">Enables DOM Parsing's serialization checks, not document or DTD validation.</param>
    /// <param name="limits">Output limits, or null for unbounded output.</param>
    /// <param name="cancellationToken">Cancellation for this operation.</param>
    public static string ToXml(Node node, bool requireWellFormed = false,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default) =>
        XmlMarkupSerializer.Serialize(node, requireWellFormed, limits, cancellationToken: cancellationToken);

    /// <summary>Returns the standard empty XML serialization of an attached or detached attribute.</summary>
    public static string ToXml(Attr attribute, bool requireWellFormed = false,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default) =>
        XmlMarkupSerializer.Serialize(attribute, requireWellFormed, limits, cancellationToken: cancellationToken);

    /// <summary>Serializes children as XML with self-contained namespace declarations; templates use their content.</summary>
    public static string ToXmlChildren(Node parent, bool requireWellFormed = false,
        SerializationLimits? limits = null, CancellationToken cancellationToken = default) =>
        XmlMarkupSerializer.SerializeChildren(parent, requireWellFormed, limits, cancellationToken: cancellationToken);
}

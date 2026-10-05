namespace Jint.HtmlParser;

/// <summary>Entry points for native markup parsing.</summary>
public static partial class MarkupParser
{
    /// <summary>Parses one well-formed XML document into native nodes.</summary>
    public static Document ParseXml(string source, XmlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        return XmlDocumentParser.Parse(source, Document.CreateXml(), options, cancellationToken);
    }

    /// <summary>Parses XML children in the namespace context of an existing element.</summary>
    public static DocumentFragment ParseXmlFragment(string source, Element context, XmlParseOptions? options = null,
        CancellationToken cancellationToken = default)
        => XmlTreeParser.ParseFragment(source, context, options?.Limits ?? ParseLimits.Default, cancellationToken);

    /// <summary>Parses a document whose root is an SVG element in the SVG namespace.</summary>
    public static Document ParseSvg(string source, XmlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        return XmlTreeParser.ParseIntoDocument(source, Document.CreateXml("image/svg+xml"),
            options?.Limits ?? ParseLimits.Default, requireSvgRoot: true, cancellationToken);
    }
}

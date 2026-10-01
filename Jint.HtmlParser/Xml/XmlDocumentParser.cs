namespace Jint.HtmlParser;

// Browser selects the response MIME before tree construction; standalone XML
// uses the same core with the default application/xml document.
internal static class XmlDocumentParser
{
    internal static Document Parse(string source, Document document, XmlParseOptions? options,
        CancellationToken cancellationToken)
        => XmlTreeParser.ParseIntoDocument(source, document, options?.Limits ?? ParseLimits.Unbounded,
            requireSvgRoot: false, cancellationToken);
}

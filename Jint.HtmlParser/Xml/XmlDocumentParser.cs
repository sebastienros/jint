namespace Jint.HtmlParser;

// Browser selects the response MIME before tree construction; standalone XML
// uses the same core with the default application/xml document.
internal static class XmlDocumentParser
{
    internal static Document Parse(string source, Document document, XmlParseOptions? options,
        CancellationToken cancellationToken)
        => Parse(source, document, options, null, cancellationToken);

    // Browser observes its turn's constraints at the parser's bounded work polls.
    // Keep this host callback off the standalone public parsing surface.
    internal static Document Parse(string source, Document document, XmlParseOptions? options,
        Action? onCancellationPoll, CancellationToken cancellationToken)
    {
        onCancellationPoll?.Invoke();
        var result = XmlTreeParser.ParseIntoDocument(source, document, options?.Limits ?? ParseLimits.Default,
            requireSvgRoot: false, onCancellationPoll, cancellationToken);
        onCancellationPoll?.Invoke();
        return result;
    }

    internal static DocumentFragment ParseFragment(string source, Element context, XmlParseOptions? options,
        Action? onCancellationPoll, CancellationToken cancellationToken)
    {
        onCancellationPoll?.Invoke();
        var result = XmlTreeParser.ParseFragment(source, context, options?.Limits ?? ParseLimits.Default,
            onCancellationPoll, cancellationToken);
        onCancellationPoll?.Invoke();
        return result;
    }
}

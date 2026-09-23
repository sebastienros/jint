namespace Jint.HtmlParser;

/// <summary>A parser-validated, namespace-resolved initial attribute.</summary>
internal readonly struct ParserAttribute
{
    internal ParserAttribute(string? namespaceUri, string localName, string? prefix, string value)
    {
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Prefix = prefix;
        Value = value;
    }

    internal string? NamespaceUri { get; }
    internal string LocalName { get; }
    internal string? Prefix { get; }
    internal string Value { get; }
}

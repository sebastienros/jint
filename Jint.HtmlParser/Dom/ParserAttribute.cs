namespace Jint.HtmlParser;

/// <summary>A parser-validated, namespace-resolved initial attribute.</summary>
internal readonly struct ParserAttribute
{
    internal ParserAttribute(string? namespaceUri, string localName, string? prefix, string value,
        bool isDtdId = false)
        : this(namespaceUri, localName, prefix, new StringSlice(value), isDtdId) { }

    internal ParserAttribute(string? namespaceUri, string localName, string? prefix, StringSlice value,
        bool isDtdId = false)
    {
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Prefix = prefix;
        ValueSlice = value;
        IsDtdId = isDtdId;
    }

    internal string? NamespaceUri { get; }
    internal string LocalName { get; }
    internal string? Prefix { get; }
    internal StringSlice ValueSlice { get; }
    internal string Value => ValueSlice.ToString();
    internal bool IsDtdId { get; }
}

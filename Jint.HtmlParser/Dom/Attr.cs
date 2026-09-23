namespace Jint.HtmlParser;

/// <summary>A stable-identity attribute, attached to at most one element.</summary>
public sealed class Attr
{
    private string _value;

    internal Attr(Document ownerDocument, string? namespaceUri, string localName, string? prefix, string value)
    {
        OwnerDocument = ownerDocument;
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Prefix = prefix;
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public Document OwnerDocument { get; internal set; }
    public Element? OwnerElement { get; internal set; }
    public string? NamespaceUri { get; }
    public string LocalName { get; }
    public string? Prefix { get; internal set; }
    public string Name => Prefix is null ? LocalName : string.Concat(Prefix, ":", LocalName);
    public string Value
    {
        get => _value;
        set => _value = value ?? throw new ArgumentNullException(nameof(value));
    }
}

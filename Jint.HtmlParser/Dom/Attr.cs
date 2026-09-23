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
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var owner = OwnerElement;
            var oldValue = _value;
            var matches = owner is null ? null : MutationTracking.Match(owner, MutationRecordKind.Attributes,
                LocalName, NamespaceUri);
            _value = value;
            if (owner is not null)
            {
                owner.OwnerDocument!.MarkMutation();
                MutationTracking.QueueAttribute(owner, LocalName, NamespaceUri, oldValue, matches);
            }
        }
    }

    /// <summary>Creates a detached copy owned by the same document.</summary>
    public Attr Clone() => NodeCloner.CloneAttribute(this, OwnerDocument);
}

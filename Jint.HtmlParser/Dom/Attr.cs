namespace Jint.HtmlParser;

/// <summary>A stable-identity attribute, attached to at most one element.</summary>
public sealed class Attr
{
    private string _value;
    private string? _prefix;

    internal Attr(Document ownerDocument, string? namespaceUri, string localName, string? prefix, string value,
        bool isDtdId = false)
    {
        OwnerDocument = ownerDocument;
        NamespaceUri = namespaceUri;
        LocalName = localName;
        _prefix = prefix;
        _value = value ?? throw new ArgumentNullException(nameof(value));
        IsDtdId = isDtdId;
    }

    public Document OwnerDocument { get; private set; }
    public Element? OwnerElement { get; internal set; }
    internal bool IsDtdId { get; }
    public string? NamespaceUri { get; }
    public string LocalName { get; }
    public string? Prefix
    {
        get => _prefix;
        internal set
        {
            if (_prefix == value)
            {
                return;
            }

            _prefix = value;
            OwnerDocument.MarkMutation();
        }
    }
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
            OwnerDocument.MarkMutation();
            if (owner is not null)
            {
                HtmlFormAssociation.AttributeChanged(owner, NamespaceUri, LocalName, oldValue, value);
                SlotAssignment.AttributeChanged(owner, NamespaceUri, LocalName, oldValue, value);
                MutationTracking.QueueAttribute(owner, LocalName, NamespaceUri, oldValue, matches);
            }
        }
    }

    internal void Rehome(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (ReferenceEquals(OwnerDocument, document))
        {
            return;
        }

        var previous = OwnerDocument;
        OwnerDocument = document;
        previous.MarkMutation();
        document.MarkMutation();
    }

    /// <summary>Creates a detached copy owned by the same document.</summary>
    public Attr Clone() => NodeCloner.CloneAttribute(this, OwnerDocument);
}

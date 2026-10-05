namespace Jint.HtmlParser;

/// <summary>A stable-identity attribute, attached to at most one element.</summary>
public sealed class Attr
{
    // Live ranges and iterators rooted at an attribute are rare; keep their state out of line.
    private NodeRareData? _rare;

    internal EndpointBucket? RangeEndpoints
    {
        get => _rare?.RangeEndpoints;
        set { if (value is not null || _rare is not null) (_rare ??= new NodeRareData()).RangeEndpoints = value; }
    }

    internal List<WeakReference<DomNodeIterator>>? RootIterators
    {
        get => _rare?.RootIterators;
        set { if (value is not null || _rare is not null) (_rare ??= new NodeRareData()).RootIterators = value; }
    }

    internal int IteratorRootSweepCursor
    {
        get => _rare?.IteratorRootSweepCursor ?? 0;
        set { if (value != 0 || _rare is not null) (_rare ??= new NodeRareData()).IteratorRootSweepCursor = value; }
    }

    private StringSlice _value;
    private string? _prefix;

    internal Attr(Document ownerDocument, string? namespaceUri, string localName, string? prefix, string value,
        bool isDtdId = false)
        : this(ownerDocument, namespaceUri, localName, prefix,
            new StringSlice(value ?? throw new ArgumentNullException(nameof(value))), isDtdId)
    { }

    internal Attr(Document ownerDocument, string? namespaceUri, string localName, string? prefix, StringSlice value,
        bool isDtdId = false)
    {
        OwnerDocument = ownerDocument;
        NamespaceUri = namespaceUri;
        LocalName = localName;
        _prefix = prefix;
        _value = value;
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
        get => StringSlice.Materialize(ref _value);
        set => SetValue(value, null);
    }
    internal ReadOnlySpan<char> ValueSpan => _value.Span;
    internal void SetValue(string value, HtmlSelectWorkContext? context)
    {
        ArgumentNullException.ThrowIfNull(value);
        var owner = OwnerElement;
        if (owner is not null) HtmlInputStateChanges.BeforeAttributeChanged(owner, NamespaceUri, LocalName, value);
        var oldValue = Value;
        var matches = owner is null ? null : MutationTracking.Match(owner, MutationRecordKind.Attributes,
            LocalName, NamespaceUri);
        _value = new StringSlice(value);
        OwnerDocument.MarkMutation();
        if (owner is not null)
        {
            OwnerDocument.MarkIdAttributeMutation(NamespaceUri, LocalName);
            HtmlFormAssociation.AttributeChanged(owner, NamespaceUri, LocalName, oldValue, value);
            MutationTracking.QueueAttribute(owner, this, oldValue, matches);
            HtmlInputStateChanges.AttributeChanged(owner, NamespaceUri, LocalName, oldValue, value);
            HtmlSelectMutations.AttributeChanged(owner, NamespaceUri, LocalName, oldValue, value, context);
            SlotAssignment.AttributeChanged(owner, NamespaceUri, LocalName, oldValue, value);
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
        previous.AdoptionObserver?.Adopting(this);
        OwnerDocument = document;
        LiveTraversalTracking.Rehome(RangeEndpoints, document);
        IteratorTracking.Rehome(RootIterators, document);
        previous.MarkMutation();
        document.MarkMutation();
    }

    /// <summary>Creates a detached copy owned by the same document.</summary>
    public Attr Clone() => NodeCloner.CloneAttribute(this, OwnerDocument);
}

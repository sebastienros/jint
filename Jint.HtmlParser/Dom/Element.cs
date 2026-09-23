namespace Jint.HtmlParser;

/// <summary>A namespace-aware element with attributes kept in insertion order.</summary>
public sealed class Element : Node
{
    private List<Attr>? _attributes;

    internal Element(Document owner, string? namespaceUri, string localName, string? prefix) : base(owner)
    {
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Prefix = prefix;
    }

    public override NodeType NodeType => NodeType.Element;
    public string? NamespaceUri { get; }
    public string LocalName { get; }
    public string? Prefix { get; }
    public string TagName => Prefix is null ? LocalName : string.Concat(Prefix, ":", LocalName);
    public int AttributeCount => _attributes?.Count ?? 0;
    public IEnumerable<Attr> Attributes
    {
        get
        {
            if (_attributes is not null)
            {
                foreach (var attribute in _attributes)
                {
                    yield return attribute;
                }
            }
        }
    }

    public string? GetAttribute(string name) => GetAttributeNode(name)?.Value;

    public Attr? GetAttributeNode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        name = NormalizeAttributeName(name);
        if (_attributes is null)
        {
            return null;
        }

        foreach (var attribute in _attributes)
        {
            if (attribute.Name == name)
            {
                return attribute;
            }
        }

        return null;
    }

    public string? GetAttributeNS(string? namespaceUri, string localName) => GetAttributeNodeNS(namespaceUri, localName)?.Value;

    public Attr? GetAttributeNodeNS(string? namespaceUri, string localName)
    {
        ArgumentNullException.ThrowIfNull(localName);
        namespaceUri = namespaceUri is "" ? null : namespaceUri;
        if (_attributes is null)
        {
            return null;
        }

        foreach (var attribute in _attributes)
        {
            if (attribute.NamespaceUri == namespaceUri && attribute.LocalName == localName)
            {
                return attribute;
            }
        }

        return null;
    }

    public void SetAttribute(string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        QualifiedName.ValidateAttributeLocalName(name);
        name = NormalizeAttributeName(name);
        var attribute = GetAttributeNode(name);
        if (attribute is not null)
        {
            attribute.Value = value;
            return;
        }

        attribute = new Attr(OwnerDocument!, null, name, null, value);
        SetAttributeNode(attribute);
    }

    public void SetAttributeNS(string? namespaceUri, string qualifiedName, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var name = QualifiedName.Parse(namespaceUri, qualifiedName, attribute: true);
        var attribute = GetAttributeNodeNS(name.NamespaceUri, name.LocalName);
        if (attribute is not null)
        {
            attribute.Value = value;
            return;
        }

        attribute = OwnerDocument!.CreateAttributeNS(name.NamespaceUri, qualifiedName);
        attribute.Value = value;
        SetAttributeNode(attribute);
    }

    /// <summary>Attaches an attribute and returns the replaced attribute, if any.</summary>
    public Attr? SetAttributeNode(Attr attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (attribute.OwnerElement is not null && !ReferenceEquals(attribute.OwnerElement, this))
        {
            throw DomException.InUseAttribute();
        }

        var previous = GetAttributeNodeNS(attribute.NamespaceUri, attribute.LocalName);
        if (ReferenceEquals(previous, attribute))
        {
            return attribute;
        }

        _attributes ??= [];
        if (previous is null)
        {
            _attributes.Add(attribute);
        }
        else
        {
            _attributes[_attributes.IndexOf(previous)] = attribute;
            previous.OwnerElement = null;
        }

        attribute.OwnerElement = this;
        attribute.OwnerDocument = OwnerDocument!;
        return previous;
    }

    public void RemoveAttribute(string name)
    {
        var attribute = GetAttributeNode(name);
        if (attribute is not null)
        {
            RemoveAttributeNode(attribute);
        }
    }

    public void RemoveAttributeNS(string? namespaceUri, string localName)
    {
        var attribute = GetAttributeNodeNS(namespaceUri, localName);
        if (attribute is not null)
        {
            RemoveAttributeNode(attribute);
        }
    }

    public Attr RemoveAttributeNode(Attr attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (!ReferenceEquals(attribute.OwnerElement, this))
        {
            throw DomException.NotFound();
        }

        _attributes!.Remove(attribute);
        attribute.OwnerElement = null;
        return attribute;
    }

    internal void AdoptAttributes(Document document)
    {
        if (_attributes is null)
        {
            return;
        }

        foreach (var attribute in _attributes)
        {
            attribute.OwnerDocument = document;
        }
    }

    internal void CopyAttributesFrom(Element source, Document document)
    {
        if (source._attributes is null)
        {
            return;
        }

        _attributes = new List<Attr>(source._attributes.Count);
        foreach (var attribute in source._attributes)
        {
            var copy = NodeCloner.CloneAttribute(attribute, document);
            copy.OwnerElement = this;
            _attributes.Add(copy);
        }
    }

    // The parser supplies one duplicate-free, validated initial batch before the
    // element can be observed. Attach in source order without repeated lookups.
    internal void InitializeParsedAttributes(ReadOnlySpan<ParserAttribute> attributes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_attributes is not null || ParentNode is not null || ChildCount != 0)
        {
            throw new InvalidOperationException("Parsed attributes require a fresh, empty element.");
        }

        if (attributes.IsEmpty)
        {
            return;
        }

        var result = new List<Attr>(attributes.Length);
        cancellationToken.ThrowIfCancellationRequested();
        for (var i = 0; i < attributes.Length; i++)
        {
            if ((i & 63) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var parsed = attributes[i];
            var attribute = new Attr(OwnerDocument!, parsed.NamespaceUri, parsed.LocalName, parsed.Prefix, parsed.Value)
            {
                OwnerElement = this
            };
            result.Add(attribute);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _attributes = result;
    }

    private string NormalizeAttributeName(string name)
        => OwnerDocument!.Kind == DocumentKind.Html && NamespaceUri == Namespaces.Html
            ? QualifiedName.AsciiLower(name)
            : name;
}

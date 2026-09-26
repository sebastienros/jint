namespace Jint.HtmlParser;

internal enum ParsedAttributeMergeCheckpoint
{
    AfterIndex,
    AfterCommit
}

/// <summary>A namespace-aware element with attributes kept in insertion order.</summary>
public sealed class Element : Node
{
    private List<Attr>? _attributes;
    private HtmlElementState? _htmlState;
    internal HtmlFormAssociationState? FormAssociationState;
    internal SlotElementState? SlotState;
    internal bool WasInserted { get; set; }

    internal HtmlElementState? GetHtmlState()
        => NamespaceUri == Namespaces.Html ? _htmlState ??= new HtmlElementState(this) : null;
    internal HtmlTextAreaState? ExistingTextAreaState => _htmlState?.ExistingTextArea;
    internal ShadowRoot? AttachedShadowRoot { get; private set; }
    internal ShadowRoot? OpenShadowRoot => AttachedShadowRoot is { Mode: ShadowRootMode.Open } root ? root : null;
    internal CustomElementRegistryIdentity? CustomElementRegistry { get; private set; }

    internal void SetCustomElementRegistry(CustomElementRegistryIdentity? registry)
    {
        if (!ReferenceEquals(CustomElementRegistry, registry))
        {
            CustomElementRegistry = registry;
            OwnerDocument!.MarkMutation();
        }
    }

    internal void SetAttachedShadowRoot(ShadowRoot root)
    {
        if (AttachedShadowRoot is not null || !ReferenceEquals(root.Host, this))
        {
            throw new InvalidOperationException("A shadow root cannot be retargeted or replaced.");
        }

        AttachedShadowRoot = root;
    }
    internal void SetTemplateContent(ShadowRoot root) => TemplateContent = root;
    internal void InitializeCustomElementRegistry(CustomElementRegistryIdentity? registry) => CustomElementRegistry = registry;

    internal Element(Document owner, string? namespaceUri, string localName, string? prefix, string? isValue = null) : base(owner)
    {
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Prefix = prefix;
        IsValue = isValue;
        if (namespaceUri == Namespaces.Html && localName == "template")
        {
            TemplateContent = new DocumentFragment(owner.GetTemplateContentsOwnerDocument(), this);
        }
    }

    public override NodeType NodeType => NodeType.Element;
    public string? NamespaceUri { get; }
    public string LocalName { get; }
    public string? Prefix { get; }
    internal string? IsValue { get; }
    public string TagName => Prefix is null ? LocalName : string.Concat(Prefix, ":", LocalName);
    public DocumentFragment? TemplateContent { get; private set; }
    public int AttributeCount => _attributes?.Count ?? 0;
    internal Attr? GetAttributeAt(uint index)
        => _attributes is { } attributes && index < (uint) attributes.Count ? attributes[(int) index] : null;
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

        if (previous is null)
        {
            AppendNewAttribute(attribute);
        }
        else
        {
            var oldValue = previous.Value;
            var matches = MutationTracking.Match(this, MutationRecordKind.Attributes,
                attribute.LocalName, attribute.NamespaceUri);
            _attributes ??= [];
            _attributes[_attributes.IndexOf(previous)] = attribute;
            previous.OwnerElement = null;
            attribute.OwnerElement = this;
            attribute.Rehome(OwnerDocument!);
            OwnerDocument!.MarkMutation();
            HtmlFormAssociation.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
                oldValue, attribute.Value);
            SlotAssignment.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
                oldValue, attribute.Value);
            MutationTracking.QueueAttribute(this, attribute.LocalName, attribute.NamespaceUri, oldValue, matches);
        }

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
        OwnerDocument!.MarkMutation();
        HtmlFormAssociation.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            attribute.Value, null);
        SlotAssignment.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            attribute.Value, null);
        MutationTracking.QueueAttribute(this, attribute.LocalName, attribute.NamespaceUri, attribute.Value);
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
            attribute.Rehome(document);
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
            ScriptAttributeAdded(copy);
        }
    }

    // The parser supplies one duplicate-free, validated initial batch before the
    // element can be observed. Attach in source order without repeated lookups.
    internal void InitializeParsedAttributes(ReadOnlySpan<ParserAttribute> attributes, CancellationToken cancellationToken)
        => InitializeParsedAttributes(attributes, null, cancellationToken);

    // Per-invocation checkpoint lets tests deterministically cancel a partial
    // unpublished batch without adding shared state to parser construction.
    internal void InitializeParsedAttributes(ReadOnlySpan<ParserAttribute> attributes, Action<int>? workCheckpoint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_attributes is not null || ParentNode is not null || ChildCount != 0 ||
            MutationRegistrations is not null)
        {
            throw new InvalidOperationException("Parsed attributes require a fresh, empty element.");
        }

        if (attributes.IsEmpty)
        {
            return;
        }

        var result = new List<Attr>(attributes.Length);
        var scriptAsyncAdded = false;
        cancellationToken.ThrowIfCancellationRequested();
        for (var i = 0; i < attributes.Length; i++)
        {
            if ((i & 63) == 0)
            {
                workCheckpoint?.Invoke(i);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var parsed = attributes[i];
            if (NamespaceUri == Namespaces.Html && LocalName == "script" &&
                parsed.NamespaceUri is null && parsed.LocalName == "async") scriptAsyncAdded = true;
            var attribute = new Attr(OwnerDocument!, parsed.NamespaceUri, parsed.LocalName, parsed.Prefix,
                parsed.Value, parsed.IsDtdId)
            {
                OwnerElement = this
            };
            result.Add(attribute);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _attributes = result;
        if (scriptAsyncAdded) GetHtmlState()!.Script!.ForceAsync = false;
    }

    // HTML's repeated html/body start tags merge into an already published
    // element. The tokenizer supplies resolved, duplicate-free source attributes.
    internal void AddMissingParsedAttributes(ReadOnlySpan<ParserAttribute> attributes, CancellationToken cancellationToken)
        => AddMissingParsedAttributes(attributes, null, cancellationToken);

    internal void AddMissingParsedAttributes(ReadOnlySpan<ParserAttribute> attributes,
        Action<ParsedAttributeMergeCheckpoint, int>? workCheckpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = _attributes?.Count ?? 0;
        var keys = new HashSet<(string? NamespaceUri, string LocalName)>(count + attributes.Length);
        cancellationToken.ThrowIfCancellationRequested();
        if (_attributes is { } existing)
        {
            for (var i = 0; i < existing.Count; i++)
            {
                if ((i & 63) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var attribute = existing[i];
                keys.Add((attribute.NamespaceUri, attribute.LocalName));
            }
        }

        workCheckpoint?.Invoke(ParsedAttributeMergeCheckpoint.AfterIndex, count);
        cancellationToken.ThrowIfCancellationRequested();
        var committed = 0;
        for (var i = 0; i < attributes.Length; i++)
        {
            if ((i & 63) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var parsed = attributes[i];
            if (!keys.Add((parsed.NamespaceUri, parsed.LocalName)))
            {
                continue;
            }

            var attribute = new Attr(OwnerDocument!, parsed.NamespaceUri, parsed.LocalName, parsed.Prefix,
                parsed.Value, parsed.IsDtdId);
            AppendNewAttribute(attribute);
            committed++;
            workCheckpoint?.Invoke(ParsedAttributeMergeCheckpoint.AfterCommit, committed);
            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void AppendNewAttribute(Attr attribute)
    {
        _attributes ??= [];
        _attributes.Add(attribute);
        attribute.OwnerElement = this;
        attribute.Rehome(OwnerDocument!);
        ScriptAttributeAdded(attribute);
        OwnerDocument!.MarkMutation();
        HtmlFormAssociation.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            null, attribute.Value);
        SlotAssignment.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            null, attribute.Value);
        MutationTracking.QueueAttribute(this, attribute.LocalName, attribute.NamespaceUri, null);
    }

    private void ScriptAttributeAdded(Attr attribute)
    {
        if (NamespaceUri == Namespaces.Html && LocalName == "script" &&
            attribute.NamespaceUri is null && attribute.LocalName == "async")
            GetHtmlState()!.Script!.ForceAsync = false;
    }

    private string NormalizeAttributeName(string name)
        => OwnerDocument!.Kind == DocumentKind.Html && NamespaceUri == Namespaces.Html
            ? QualifiedName.AsciiLower(name)
            : name;
}

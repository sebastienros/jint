using System.Runtime.CompilerServices;

namespace Jint.HtmlParser;

internal enum ParsedAttributeMergeCheckpoint
{
    AfterIndex,
    AfterCommit
}

/// <summary>A namespace-aware element with attributes kept in insertion order.</summary>
public sealed class Element : Node
{
    // Attributes in order; slots past _attributeCount are null.
    private Attr[]? _attributes;
    private int _attributeCount;

    private protected override NodeRareData CreateRareData() => new ElementRareData();
    // Element only ever creates ElementRareData, so the rare slot needs no type check.
    private ElementRareData? ExistingRare => Unsafe.As<ElementRareData>(_rare);
    private ElementRareData ElementRare => Unsafe.As<ElementRareData>(Rare);

    // Demanded only for an attribute-absence proof; parsing and mutations allocate no token.
    internal object GetAttributeStructureIdentity() => ElementRare.AttributeStructureIdentity ??= new object();
    internal object? ExistingAttributeStructureIdentity => ExistingRare?.AttributeStructureIdentity;

    private void ResetAttributeStructureIdentity()
    {
        if (ExistingRare is { } rare) rare.AttributeStructureIdentity = null;
    }

    internal HtmlFormAssociationState? FormAssociationState
    {
        get => ExistingRare?.FormAssociationState;
        set { if (value is not null || _rare is not null) ElementRare.FormAssociationState = value; }
    }

    internal SlotElementState? SlotState
    {
        get => ExistingRare?.SlotState;
        set { if (value is not null || _rare is not null) ElementRare.SlotState = value; }
    }

    internal HtmlTemplatePatchState? TemplatePatchState
    {
        get => ExistingRare?.TemplatePatchState;
        set { if (value is not null || _rare is not null) ElementRare.TemplatePatchState = value; }
    }

    internal bool WasInserted { get; set; }

    internal HtmlElementState? GetHtmlState()
        => NamespaceUri == Namespaces.Html ? ElementRare.HtmlState ??= new HtmlElementState(this) : null;
    internal HtmlOptionCore GetOptionCore(CancellationToken token = default)
        => GetOptionCoreWithWork((HtmlSelectWorkContext?) null, token);
    internal HtmlOptionCore GetOptionCoreWithWork(HtmlSelectWorkContext? context, CancellationToken token = default)
    {
        var result = ElementRare.OptionCore ??= new HtmlOptionCore(this, context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal HtmlOptionCore InitializeOptionCore(bool selected)
        => ElementRare.OptionCore ??= new HtmlOptionCore(this, selected);
    internal HtmlSelectCore GetSelectCore(CancellationToken token = default)
        => GetSelectCoreWithWork((HtmlSelectWorkContext?) null, token);
    internal HtmlSelectCore GetSelectCoreWithWork(HtmlSelectWorkContext? context, CancellationToken token = default)
    {
        var result = ElementRare.SelectCore ??= new HtmlSelectCore(this, context, token);
        HtmlSelectWork.Check(context, token);
        return result;
    }
    internal HtmlOptionCore? ExistingOptionCore => ExistingRare?.OptionCore;
    internal HtmlSelectCore? ExistingSelectCore => ExistingRare?.SelectCore;
    internal HtmlOptionState? ExistingOptionState => ExistingRare?.HtmlState?.ExistingOption;
    internal HtmlSelectState? ExistingSelectState => ExistingRare?.HtmlState?.ExistingSelect;
    internal HtmlInputCheckedState? ExistingCheckedState => ExistingRare?.HtmlState?.ExistingCheckedState;
    internal bool HasHtmlState => ExistingRare?.HtmlState is not null;
    internal HtmlTextAreaState? ExistingTextAreaState => ExistingRare?.HtmlState?.ExistingTextArea;
    internal HtmlInputValueState? ExistingInputValueState => ExistingRare?.HtmlState?.ExistingInputValue;
    internal ShadowRoot? AttachedShadowRoot => ExistingRare?.AttachedShadowRoot;
    /// <summary>The attached open shadow root, or null for absent and closed roots.</summary>
    public ShadowRoot? OpenShadowRoot => AttachedShadowRoot is { Mode: ShadowRootMode.Open } root ? root : null;
    internal CustomElementRegistryIdentity? CustomElementRegistry
    {
        get => ExistingRare?.CustomElementRegistry;
        private set { if (value is not null || _rare is not null) ElementRare.CustomElementRegistry = value; }
    }

    /// <summary>Attaches a native shadow root and returns its identity, including for closed mode.</summary>
    /// <remarks>
    /// Uses native host-name and existing-root validation. This standalone entry does not execute
    /// Browser custom-element reactions or resolve Browser definitions.
    /// </remarks>
    public ShadowRoot AttachShadow(ShadowRootInit init, CancellationToken cancellationToken = default) =>
        ShadowTree.Attach(this, init, new ShadowAttachmentContext(CustomElementRegistry, false, false), null, cancellationToken);

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

        ElementRare.AttachedShadowRoot = root;
    }
    internal void SetTemplateContent(ShadowRoot root) => ElementRare.TemplateContent = root;
    internal void InitializeCustomElementRegistry(CustomElementRegistryIdentity? registry) => CustomElementRegistry = registry;

    internal Element(Document owner, string? namespaceUri, string localName, string? prefix, string? isValue = null) : base(owner)
    {
        NamespaceUri = namespaceUri;
        LocalName = localName;
        Prefix = prefix;
        if (isValue is not null) ElementRare.IsValue = isValue;
        if (namespaceUri == Namespaces.Html && localName == "template")
        {
            ElementRare.TemplateContent = new DocumentFragment(owner.GetTemplateContentsOwnerDocument(), this);
        }
        else if (namespaceUri == Namespaces.Html)
        {
            if (localName == "selectedcontent") owner.RecordCreatedElementKinds(DocumentElementKinds.SelectedContent);
            else if (localName == "base") owner.RecordCreatedElementKinds(DocumentElementKinds.Base);
        }
    }

    public override NodeType NodeType => NodeType.Element;
    public string? NamespaceUri { get; }
    public string LocalName { get; }
    public string? Prefix { get; }
    internal string? IsValue => ExistingRare?.IsValue;
    public string TagName => Prefix is null ? LocalName : string.Concat(Prefix, ":", LocalName);
    public DocumentFragment? TemplateContent => ExistingRare?.TemplateContent;
    public int AttributeCount => _attributeCount;
    internal Attr? GetAttributeAt(uint index) => index < (uint) _attributeCount ? _attributes![index] : null;
    // Internal readers iterate in place; a mutation during the read is detected by the caller's stamps.
    internal ReadOnlySpan<Attr> AttributeSpan => new(_attributes, 0, _attributeCount);

    public IEnumerable<Attr> Attributes
    {
        get
        {
            for (var i = 0; i < _attributeCount; i++)
            {
                yield return _attributes![i];
            }
        }
    }

    public string? GetAttribute(string name) => GetAttributeNode(name)?.Value;

    public Attr? GetAttributeNode(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        name = NormalizeAttributeName(name);
        foreach (var attribute in AttributeSpan)
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
        foreach (var attribute in AttributeSpan)
        {
            if (attribute.NamespaceUri == namespaceUri && attribute.LocalName == localName)
            {
                return attribute;
            }
        }

        return null;
    }

    // Select/option callers have already normalized the native HTML attribute name.
    internal void SetSelectAttribute(string name, string? value, HtmlSelectWorkContext? context, CancellationToken token)
    {
        var work = new HtmlSelectWork(OwnerDocument?.SelectWorkProbe, context, token);
        work.Check();
        Attr? existing = null;
        foreach (var attribute in Attributes)
        {
            work.Step();
            if (attribute.NamespaceUri is null && attribute.LocalName == name) { existing = attribute; break; }
        }
        work.Check();
        if (value is null)
        {
            if (existing is not null) RemoveAttributeNode(existing, context);
        }
        else if (existing is not null) existing.SetValue(value, context);
        else AppendNewAttribute(new Attr(OwnerDocument!, null, name, null, value), context);
        HtmlSelectWork.Check(context, token);
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
            HtmlInputStateChanges.BeforeAttributeChanged(this, attribute.NamespaceUri, attribute.LocalName, attribute.Value);
            var oldValue = previous.Value;
            var matches = MutationTracking.Match(this, MutationRecordKind.Attributes,
                attribute.LocalName, attribute.NamespaceUri);
            _attributes![Array.IndexOf(_attributes, previous, 0, _attributeCount)] = attribute;
            ResetAttributeStructureIdentity();
            previous.OwnerElement = null;
            attribute.OwnerElement = this;
            attribute.Rehome(OwnerDocument!);
            OwnerDocument!.MarkMutation();
            HtmlFormAssociation.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
                oldValue, attribute.Value);
            MutationTracking.QueueAttribute(this, attribute, oldValue, matches, previous);
            HtmlInputStateChanges.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
                oldValue, attribute.Value);
            HtmlSelectMutations.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
                oldValue, attribute.Value);
            SlotAssignment.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
                oldValue, attribute.Value);
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

    public Attr RemoveAttributeNode(Attr attribute) => RemoveAttributeNode(attribute, null);
    internal Attr RemoveAttributeNode(Attr attribute, HtmlSelectWorkContext? context)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (!ReferenceEquals(attribute.OwnerElement, this))
        {
            throw DomException.NotFound();
        }

        HtmlInputStateChanges.BeforeAttributeChanged(this, attribute.NamespaceUri, attribute.LocalName, null);
        RemoveAttributeSlot(attribute);
        ResetAttributeStructureIdentity();
        attribute.OwnerElement = null;
        OwnerDocument!.MarkMutation();
        HtmlFormAssociation.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            attribute.Value, null);
        MutationTracking.QueueAttribute(this, attribute, attribute.Value);
        HtmlInputStateChanges.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            attribute.Value, null);
        HtmlSelectMutations.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            attribute.Value, null, context);
        SlotAssignment.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            attribute.Value, null);
        return attribute;
    }

    internal void AdoptAttributes(Document document)
    {
        foreach (var attribute in AttributeSpan)
        {
            attribute.Rehome(document);
        }
    }

    internal void CopyAttributesFrom(Element source, Document document, CancellationToken cancellationToken = default)
        => CopyAttributesFromWithWork(source, document, (HtmlSelectWorkContext?) null, cancellationToken);
    internal void CopyAttributesFromWithWork(Element source, Document document, HtmlSelectWorkContext? context, CancellationToken cancellationToken = default)
    {
        if (source._attributes is null)
        {
            return;
        }

        var work = new HtmlSelectWork(document.SelectWorkProbe, context, cancellationToken);
        work.Check();
        _attributes = new Attr[source._attributeCount];
        _attributeCount = 0;
        ResetAttributeStructureIdentity();
        foreach (var attribute in source.AttributeSpan)
        {
            work.Step();
            var copy = NodeCloner.CloneAttribute(attribute, document);
            copy.OwnerElement = this;
            _attributes[_attributeCount++] = copy;
            ResetAttributeStructureIdentity();
            ScriptAttributeAdded(copy);
        }
        work.Check();
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
            HtmlInputStateChanges.Initialize(this);
            HtmlSelectMutations.Initialize(this, HtmlSelectMutations.PrepareInitialization(this, [], cancellationToken));
            return;
        }

        var result = new Attr[attributes.Length];
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
                parsed.ValueSlice, parsed.IsDtdId)
            {
                OwnerElement = this
            };
            result[i] = attribute;
        }

        var preparedInput = HtmlInputStateChanges.PrepareInitialization(this, result, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var selectInitialization = HtmlSelectMutations.PrepareInitialization(this, result, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _attributes = result;
        _attributeCount = result.Length;
        ResetAttributeStructureIdentity();
        HtmlInputStateChanges.Initialize(this, preparedInput);
        HtmlSelectMutations.Initialize(this, selectInitialization);
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
        var count = _attributeCount;
        var keys = new HashSet<(string? NamespaceUri, string LocalName)>(count + attributes.Length);
        cancellationToken.ThrowIfCancellationRequested();
        for (var i = 0; i < count; i++)
        {
            if ((i & 63) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var attribute = _attributes![i];
            keys.Add((attribute.NamespaceUri, attribute.LocalName));
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
                parsed.ValueSlice, parsed.IsDtdId);
            AppendNewAttribute(attribute);
            committed++;
            workCheckpoint?.Invoke(ParsedAttributeMergeCheckpoint.AfterCommit, committed);
            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private void AddAttributeSlot(Attr attribute)
    {
        var attributes = _attributes;
        if (attributes is null || _attributeCount == attributes.Length)
        {
            Array.Resize(ref _attributes, attributes is null || attributes.Length == 0 ? 4 : attributes.Length * 2);
        }

        _attributes![_attributeCount++] = attribute;
    }

    private void RemoveAttributeSlot(Attr attribute)
    {
        var index = Array.IndexOf(_attributes!, attribute, 0, _attributeCount);
        if (index < 0)
        {
            return;
        }

        _attributeCount--;
        Array.Copy(_attributes!, index + 1, _attributes!, index, _attributeCount - index);
        _attributes![_attributeCount] = null!;
    }

    private void AppendNewAttribute(Attr attribute, HtmlSelectWorkContext? context = null)
    {
        HtmlInputStateChanges.BeforeAttributeChanged(this, attribute.NamespaceUri, attribute.LocalName, attribute.Value);
        AddAttributeSlot(attribute);
        ResetAttributeStructureIdentity();
        attribute.OwnerElement = this;
        attribute.Rehome(OwnerDocument!);
        ScriptAttributeAdded(attribute);
        OwnerDocument!.MarkMutation();
        HtmlFormAssociation.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            null, attribute.Value);
        MutationTracking.QueueAttribute(this, attribute, null);
        HtmlInputStateChanges.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            null, attribute.Value);
        HtmlSelectMutations.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            null, attribute.Value, context);
        SlotAssignment.AttributeChanged(this, attribute.NamespaceUri, attribute.LocalName,
            null, attribute.Value);
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

namespace Jint.HtmlParser;

/// <summary>The kind of a native DOM mutation.</summary>
public enum MutationRecordKind
{
    ChildList,
    Attributes,
    CharacterData
}

/// <summary>An immutable snapshot of one native DOM mutation.</summary>
public sealed class MutationRecord
{
    internal static readonly IReadOnlyList<Node> EmptyNodes = Array.AsReadOnly(Array.Empty<Node>());

    internal MutationRecord(MutationRecordKind kind, Node target, IReadOnlyList<Node>? addedNodes = null,
        IReadOnlyList<Node>? removedNodes = null, Node? previousSibling = null, Node? nextSibling = null,
        string? attributeName = null, string? attributeNamespace = null, string? oldValue = null,
        bool targetWasConnected = false, string? attributeQualifiedName = null,
        string? attributePreviousQualifiedName = null, string? attributeNewValue = null, IReadOnlyList<HtmlMetaInsertion>? htmlMetaInsertions = null)
    {
        Kind = kind;
        Target = target;
        AddedNodes = addedNodes ?? EmptyNodes;
        RemovedNodes = removedNodes ?? EmptyNodes;
        PreviousSibling = previousSibling;
        NextSibling = nextSibling;
        AttributeName = attributeName;
        AttributeNamespace = attributeNamespace;
        AttributeQualifiedName = attributeQualifiedName;
        AttributePreviousQualifiedName = attributePreviousQualifiedName;
        AttributeNewValue = attributeNewValue;
        HtmlMetaInsertions = htmlMetaInsertions ?? Array.Empty<HtmlMetaInsertion>();
        OldValue = oldValue;
        TargetWasConnected = targetWasConnected;
    }

    public MutationRecordKind Kind { get; }
    public Node Target { get; }
    public IReadOnlyList<Node> AddedNodes { get; }
    public IReadOnlyList<Node> RemovedNodes { get; }
    public Node? PreviousSibling { get; }
    public Node? NextSibling { get; }
    public string? AttributeName { get; }
    public string? AttributeNamespace { get; }
    public string? OldValue { get; }

    // Trusted host lifecycle signal at the mutation's original match point.
    // It is not a public MutationObserver field and never reads today's tree.
    internal bool TargetWasConnected { get; }
    // Immutable host metadata for qualified-name protocols; DOM attributeName remains localName.
    internal string? AttributeQualifiedName { get; }
    internal string? AttributePreviousQualifiedName { get; }
    // The actual value at this transition, or null for removal; never today's attribute lookup.
    // Internal host history only, not a public MutationObserver field.
    internal string? AttributeNewValue { get; }
    internal IReadOnlyList<HtmlMetaInsertion> HtmlMetaInsertions { get; }
}

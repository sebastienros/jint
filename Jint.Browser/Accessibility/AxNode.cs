using AngleSharp.Dom;

namespace Jint.Browser.Accessibility;

/// <summary>
/// Why a node is in the tree but not exposed to an assistive technology.
/// </summary>
internal enum AxIgnoredReason
{
    /// <summary>The node is not ignored.</summary>
    None,

    /// <summary>The element or an ancestor carries the <c>hidden</c> content attribute.</summary>
    Hidden,

    /// <summary>The element or an ancestor computes to <c>display: none</c>.</summary>
    NotRendered,

    /// <summary>The element or an ancestor computes to <c>visibility: hidden</c>.</summary>
    NotVisible,

    /// <summary>The element carries <c>aria-hidden="true"</c>.</summary>
    AriaHiddenElement,

    /// <summary>An ancestor carries <c>aria-hidden="true"</c>.</summary>
    AriaHiddenSubtree,

    /// <summary>The element's role is <c>none</c> or <c>presentation</c>.</summary>
    PresentationalRole,

    /// <summary>An <c>img</c> whose <c>alt</c> is present and empty.</summary>
    EmptyAlt,

    /// <summary>A text node whose content collapses to nothing.</summary>
    EmptyText,

    /// <summary>A generic node carrying nothing an assistive technology would report.</summary>
    Uninteresting,
}

/// <summary>
/// One node of a computed accessibility tree.
/// </summary>
/// <remarks>
/// The shape is the Chrome DevTools Protocol's <c>Accessibility.AXNode</c> in tree rather than flattened form:
/// <see cref="AccessibilityTree.ToProtocol(AxNode)"/> converts one node and <see cref="AccessibilityTree.Flatten"/>
/// produces the list <c>getFullAXTree</c> answers with.
/// </remarks>
internal sealed class AxNode
{
    internal AxNode(int id, string role)
    {
        Id = id;
        Role = role;
    }

    /// <summary>The node's identifier, stable for as long as its document and this node's element live.</summary>
    internal int Id { get; }

    /// <summary>The computed role, explicit or implicit.</summary>
    internal string Role { get; }

    /// <summary>The element this node was computed from, or <see langword="null"/> for a text node.</summary>
    internal IElement? Element { get; init; }

    /// <summary>The DOM node this node was computed from.</summary>
    internal INode? Node { get; init; }

    /// <summary>The accessible name, or <see langword="null"/> when the computation produced nothing.</summary>
    internal string? Name { get; init; }

    /// <summary>The accessible description, or <see langword="null"/> when there is none.</summary>
    internal string? Description { get; init; }

    /// <summary>The value a widget carries, or <see langword="null"/> when the role has none.</summary>
    internal string? Value { get; init; }

    /// <summary>Everything else the node states, in a stable order.</summary>
    internal IReadOnlyList<AxProperty> Properties { get; init; } = [];

    /// <summary>The node's children, already pruned according to the options the tree was built with.</summary>
    internal IReadOnlyList<AxNode> Children { get; init; } = [];

    /// <summary>Whether an assistive technology would skip this node.</summary>
    internal bool Ignored => IgnoredReason != AxIgnoredReason.None;

    /// <summary>Why the node is ignored, or <see cref="AxIgnoredReason.None"/> when it is not.</summary>
    internal AxIgnoredReason IgnoredReason { get; init; }

    /// <summary>The node's parent, or <see langword="null"/> for the root.</summary>
    internal AxNode? Parent { get; private set; }

    internal void AdoptChildren()
    {
        foreach (var child in Children)
        {
            child.Parent = this;
        }
    }

    public override string ToString() => Name is null ? Role : $"{Role} \"{Name}\"";
}

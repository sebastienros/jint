namespace Jint.Browser.Accessibility;

/// <summary>
/// What a call to <see cref="AccessibilityTree.Build(AngleSharp.Dom.IDocument, AccessibilityOptions?)"/>
/// keeps and what it prunes.
/// </summary>
/// <remarks>
/// The defaults are the agent-facing tree: no generic wrappers, no text nodes, no ignored subtrees — the
/// smallest thing that still says what is on the page. <see cref="Full"/> is the other end, and is what a
/// Chrome DevTools Protocol <c>Accessibility.getFullAXTree</c> answers with.
/// </remarks>
internal sealed record AccessibilityOptions
{
    /// <summary>The agent-facing tree: interesting nodes only.</summary>
    internal static AccessibilityOptions Default { get; } = new();

    /// <summary>
    /// The agent-facing tree plus the text between its nodes, which is what a compact snapshot renders.
    /// </summary>
    /// <remarks>
    /// The text a node's own accessible name already carries is left out — a button's label, a
    /// <c>&lt;label&gt;</c>'s text, a <c>&lt;legend&gt;</c>, a <c>&lt;caption&gt;</c> — so a snapshot states
    /// each string once.
    /// </remarks>
    internal static AccessibilityOptions Snapshot { get; } = new() { IncludeText = true };

    /// <summary>Everything the walk saw, ignored nodes and text nodes included.</summary>
    internal static AccessibilityOptions Full { get; } = new()
    {
        IncludeGeneric = true,
        IncludeText = true,
        IncludeIgnored = true,
    };

    /// <summary>Whether nodes whose role is <c>generic</c>, <c>none</c> or <c>presentation</c> are kept.</summary>
    /// <remarks>
    /// A pruned node is replaced by its children rather than dropped with them, so the tree stays complete.
    /// A generic node that carries an accessible name or can take focus is kept either way.
    /// </remarks>
    internal bool IncludeGeneric { get; init; }

    /// <summary>Whether text nodes become <c>StaticText</c> nodes.</summary>
    internal bool IncludeText { get; init; }

    /// <summary>Whether hidden subtrees are kept as ignored nodes rather than dropped.</summary>
    internal bool IncludeIgnored { get; init; }

    /// <summary>Whether the CSS cascade is consulted for <c>display</c> and <c>visibility</c>.</summary>
    /// <remarks>
    /// It needs <c>AngleSharp.Css</c> registered on the browsing context. When it is not, the computation
    /// falls back to the <c>style</c> content attribute and the <c>hidden</c> attribute, once, and stays
    /// there for the life of the walk rather than throwing per element.
    /// </remarks>
    internal bool UseComputedStyle { get; init; } = true;
}

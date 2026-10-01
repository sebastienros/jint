namespace Jint.HtmlParser.Serialization;

/// <summary>What an HTML serialization does with one descendant node.</summary>
internal enum HtmlSerializationDecision
{
    /// <summary>Serialize the node as HTML §13.3 would.</summary>
    Include,
    /// <summary>Omit the node and its whole subtree, including template contents and shadow roots.</summary>
    Skip,
    /// <summary>Omit an element's own tags and attributes but serialize its children; other nodes are included.</summary>
    Unwrap,
}

/// <summary>
/// A consumer-supplied view over an HTML serialization, used for extraction dumps that strip scripts,
/// styles or page chrome without cloning the tree first.
/// </summary>
/// <remarks>
/// <para>
/// The operation root is always serialized; only its descendants are offered. A filter reads the tree
/// but must not mutate it: every callback is followed by the serializer's owner-stamp checks, so a
/// mutation invalidates the operation exactly as a concurrent mutation would.
/// </para>
/// <para>
/// A serialization without a filter pays one null check per node. The attribute and injection callbacks
/// are called only when <see cref="FiltersAttributes"/> or <see cref="InjectsContent"/> is set.
/// </para>
/// </remarks>
internal abstract class HtmlSerializationFilter
{
    /// <summary>Decides how a descendant of the operation root is serialized.</summary>
    internal abstract HtmlSerializationDecision Decide(Node node);

    /// <summary>When set, <see cref="IncludeAttribute"/> is consulted for every written attribute.</summary>
    internal virtual bool FiltersAttributes => false;

    /// <summary>Whether an included element's attribute is written. The synthesized <c>is</c> value is not offered.</summary>
    internal virtual bool IncludeAttribute(Element element, Attr attribute) => true;

    /// <summary>When set, <see cref="InjectAfterStartTag"/> is consulted for every written start tag.</summary>
    internal virtual bool InjectsContent => false;

    /// <summary>
    /// Markup written verbatim immediately after an included non-void element's start tag, or null for none.
    /// It is not escaped: the filter owns its well-formedness.
    /// </summary>
    internal virtual string? InjectAfterStartTag(Element element) => null;
}

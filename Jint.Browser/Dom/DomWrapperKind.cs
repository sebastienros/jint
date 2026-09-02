namespace Jint.Browser.Dom;

/// <summary>
/// Which wrapper class an instance of a DOM interface gets. The generator writes one of these beside every
/// interface it emits, from the <c>[DomAccessor]</c> metadata it found; <see cref="DomRealm"/> is the only
/// reader.
/// </summary>
/// <remarks>
/// The kind is a property of the <em>interface</em>, not of the value, because the binding never asks a
/// runtime object what it is: a member's declared return type already says which wrapper the value wants, and
/// the type map answers the same question for a value arriving from outside a generated member.
/// </remarks>
internal enum DomWrapperKind
{
    /// <summary>A plain platform object: <see cref="DomObject"/>, prototype from the interface.</summary>
    Object,

    /// <summary>
    /// An <c>INode</c>: <see cref="DomNodeObject"/>, cached one-per-node, on the engine's tree-dispatch lane.
    /// </summary>
    Node,

    /// <summary>
    /// An interface with an indexed getter: <c>Collections.DomCollectionObject</c> over the interface's
    /// generated <see cref="DomCollectionAccessor"/>.
    /// </summary>
    Collection,

    /// <summary>
    /// An interface with a named getter and nothing indexed — <c>DOMStringMap</c>:
    /// <c>Collections.DomNamedMapObject</c>.
    /// </summary>
    NamedMap,

    /// <summary>
    /// An <c>IHtmlCollection&lt;T&gt;</c> or one of its refinements, whose generic invariance keeps it off the
    /// accessor scheme: <c>Collections.DomHtmlCollectionObject&lt;T&gt;</c>.
    /// </summary>
    HtmlCollection,
}

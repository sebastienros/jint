using AngleSharp.Dom;
using Jint.Browser.Events;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom;

/// <summary>
/// The wrapper for an AngleSharp <see cref="INode"/>. It derives from Jint's <c>JsEventTarget</c> rather
/// than from <see cref="DomObject"/>, which is what puts it on the engine's tree-dispatch lane: capture,
/// target and bubble over a real event path, <c>composedPath()</c>, and retargeting all come from the engine
/// once the seams below answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one seam that must not be forgotten is <see cref="IsNode"/></b>, because it is what selects the
/// lane at all: a target that overrides <c>GetParent</c> without it dispatches to itself alone, in silence.
/// </para>
/// <para>
/// <b>What is deliberately not here yet.</b> Assigned slots answer <see langword="null"/>, so flat-tree
/// dispatch through <c>&lt;slot&gt;</c> is still absent; answering a wrong slot would be worse than answering
/// none. The activation behaviours below <i>are</i> here — <c>Events/ActivationBehaviors</c> is the table, and
/// it is what makes a checkbox toggle, a <c>&lt;summary&gt;</c> open its <c>&lt;details&gt;</c> and a submit
/// button reach its form.
/// </para>
/// </remarks>
internal class DomNodeObject : JsEventTarget, IDomWrapper
{
    internal DomNodeObject(DomRealm realm, DomInterfaceDefinition definition, INode node)
        : base(realm.Engine, realm.PrincipalRealm)
    {
        DomRealm = realm;
        Definition = definition;
        Node = node;
        Prototype = realm.PrototypeOf(definition);
    }

    /// <summary>The node this wrapper projects.</summary>
    internal INode Node { get; }

    /// <inheritdoc />
    public object DomTarget => Node;

    /// <inheritdoc />
    public DomRealm DomRealm { get; }

    /// <summary>The interface whose prototype this wrapper was given.</summary>
    internal DomInterfaceDefinition Definition { get; }

    /// <summary>
    /// Selects the engine's tree-dispatch lane. Everything else on this class is only consulted because this
    /// answers <see langword="true"/>.
    /// </summary>
    internal override bool IsNode => true;

    /// <summary>
    /// https://dom.spec.whatwg.org/#default-passive-value — the three node targets DOM names beside the
    /// <c>Window</c>: the document, its document element and its body element.
    /// </summary>
    /// <remarks>
    /// Read only for a <c>touchstart</c>, <c>touchmove</c>, <c>wheel</c> or <c>mousewheel</c> listener, which
    /// the engine tests for first, so every other <c>addEventListener</c> on a node costs nothing. The
    /// comparisons are against the node's <i>own</i> document rather than the page's, which is what makes a
    /// document a <c>DOMParser</c> produced answer about itself.
    /// </remarks>
    internal override bool IsDefaultPassiveTarget => Node switch
    {
        IDocument => true,
        IElement element => element.Owner is { } owner
            && (ReferenceEquals(element, owner.DocumentElement) || ReferenceEquals(element, owner.Body)),
        _ => false,
    };

    /// <summary>
    /// https://dom.spec.whatwg.org/#get-the-parent — the node tree parent, and for a shadow root the host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The shadow-root clause is stated as "the host, unless the event's composed flag is unset and the shadow
    /// root is the root of the event's path's first struct's invocation target". The second half is dropped
    /// here because it is always true when this method is reached: the dispatcher walks up from the dispatch
    /// target, so arriving at a shadow root means the target is inside it — and if a nested shadow root sat
    /// between them, a non-composed event would already have stopped at that one. A shadow root dispatched at
    /// directly is its own root, so the reduction holds there too.
    /// </para>
    /// <para>
    /// One clause is deliberately absent: a slottable's parent is its assigned slot, and
    /// <see cref="JsEventTarget.AssignedSlot"/> answers <see langword="null"/> until R2 has a flat tree to
    /// answer from.
    /// </para>
    /// <para>
    /// A document's parent is the window for every event but <c>load</c>, which is what puts a window listener
    /// on a bubbling event's path and keeps <c>load</c> off it. The window is
    /// <see cref="Dom.DomRealm.WindowTarget"/>, published by the runtime that installs one; with no runtime
    /// the document is the root of every path, which is what a document with no browsing context is.
    /// </para>
    /// </remarks>
    internal override JsEventTarget? GetParent(JsEvent ev)
    {
        // The dispatcher calls this exactly once per event path item, before any listener runs, which makes it
        // the one place a handler content attribute a script changed since the wrapper was built can be
        // noticed without an observer. It costs one GetAttribute, and only for a type that can be a handler.
        if (EventHandlerContentAttributes.IsHandlerType(ev.TypeName))
        {
            EventHandlerContentAttributes.Reconcile(this, ev.TypeName);
        }

        if (Node is IShadowRoot shadowRoot)
        {
            return !ev.Composed || shadowRoot.Host is not { } host ? null : DomRealm.WrapNode(host);
        }

        if (Node is IDocument)
        {
            return string.Equals(ev.EventType.ToString(), "load", StringComparison.Ordinal) ? null : DomRealm.WindowTarget;
        }

        return TreeParent;
    }

    /// <summary>
    /// The node tree parent, used by retargeting and by <c>composedPath()</c>. A shadow root's parent is its
    /// host, which <see cref="ShadowHost"/> answers separately; here it is the plain
    /// <see cref="INode.Parent"/>, which AngleSharp leaves <see langword="null"/> for a shadow root.
    /// </summary>
    internal override JsEventTarget? TreeParent
        => Node.Parent is { } parent ? DomRealm.WrapNode(parent) : null;

    /// <inheritdoc />
    internal override bool IsShadowRoot => Node is IShadowRoot;

    /// <summary>
    /// A closed shadow root hides its contents from <c>composedPath()</c>. AngleSharp models the mode on
    /// <see cref="IShadowRoot"/> and nowhere else, so this is the whole of it.
    /// </summary>
    internal override bool IsClosedShadowRoot => Node is IShadowRoot { Mode: ShadowRootMode.Closed };

    /// <inheritdoc />
    internal override JsEventTarget? ShadowHost
        => Node is IShadowRoot { Host: { } host } ? DomRealm.WrapNode(host) : null;

    /// <inheritdoc />
    internal override bool HasActivationBehavior => ActivationBehaviors.Has(Node);

    /// <inheritdoc />
    internal override void ActivationBehavior(JsEvent ev) => ActivationBehaviors.Run(this, ev);

    /// <inheritdoc />
    internal override void LegacyPreActivationBehavior() => ActivationBehaviors.LegacyPreActivationBehavior(this);

    /// <inheritdoc />
    internal override void LegacyCanceledActivationBehavior() => ActivationBehaviors.LegacyCanceledActivationBehavior(this);

    public override string ToString() => "[object " + Definition.Name + "]";
}

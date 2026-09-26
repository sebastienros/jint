using Jint.HtmlParser;
using Jint.Browser.Events;
using Jint.WebApi.Events;

namespace Jint.Browser.Dom;

/// <summary>A native node (or Attr) on Jint's DOM event dispatch lane.</summary>
/// <remarks>
/// Attr is a Node in Web IDL while the native tree deliberately models it outside its Node
/// hierarchy. Keeping both in this one wrapper family preserves the JavaScript prototype and
/// event-target contract without adding an attribute to the native child-link tree.
/// </remarks>
internal class DomNodeObject : JsEventTarget, IDomWrapper
{
    internal DomNodeObject(DomRealm realm, DomInterfaceDefinition definition, object target)
        : base(realm.Engine, realm.OwningRealm)
    {
        DomRealm = realm;
        Definition = definition;
        DomTarget = target;
        Prototype = realm.PrototypeOf(definition);
    }

    public object DomTarget { get; }

    internal Node? Node => DomTarget as Node;

    internal Attr? Attribute => DomTarget as Attr;

    public DomRealm DomRealm { get; }

    internal DomInterfaceDefinition Definition { get; }

    private static long _nextPositionOrder;
    internal long PositionOrder { get; } = Interlocked.Increment(ref _nextPositionOrder);

    /// <summary>Checks the immutable native Web IDL identity, independent of a script's prototype writes.</summary>
    internal bool Implements(string interfaceName)
    {
        for (var definition = Definition; definition is not null; definition = definition.Parent)
        {
            if (string.Equals(definition.Name, interfaceName, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    internal override bool IsNode => true;

    internal override bool IsDefaultPassiveTarget => DomTarget switch
    {
        Document => true,
        Element element when element.OwnerDocument is { } document =>
            ReferenceEquals(element, document.DocumentElement) ||
            ReferenceEquals(element, DomDocumentElements.Body(document)),
        _ => false,
    };

    internal override JsEventTarget? GetParent(JsEvent ev)
    {
        if (EventHandlerContentAttributes.IsHandlerType(ev.TypeName))
        {
            EventHandlerContentAttributes.Reconcile(this, ev.TypeName);
        }

        if (DomTarget is ShadowRoot shadow)
        {
            return ev.Composed ? DomRealm.WrapNode(shadow.Host) : null;
        }

        if (DomTarget is Document)
        {
            return string.Equals(ev.EventType.ToString(), "load", StringComparison.Ordinal)
                ? null
                : DomRealm.WindowTarget;
        }

        return TreeParent;
    }

    internal override JsEventTarget? TreeParent => DomTarget is Node { ParentNode: { } parent }
        ? DomRealm.WrapNode(parent)
        : null;

    internal override JsEventTarget GetRoot()
    {
        if (DomTarget is not Node node)
        {
            return this;
        }

        var root = node;
        while (root.ParentNode is { } parent)
        {
            root = parent;
        }

        return ReferenceEquals(root, node) ? this : DomRealm.WrapNode(root);
    }

    internal override bool IsShadowRoot => DomTarget is ShadowRoot;

    internal override bool IsClosedShadowRoot => DomTarget is ShadowRoot { Mode: ShadowRootMode.Closed };

    internal override JsEventTarget? ShadowHost => DomTarget is ShadowRoot shadow
        ? DomRealm.WrapNode(shadow.Host)
        : null;

    internal override bool HasActivationBehavior => Node is { } node && ActivationBehaviors.Has(node);

    internal override void ActivationBehavior(JsEvent ev) => ActivationBehaviors.Run(this, ev);

    internal override void LegacyPreActivationBehavior() => ActivationBehaviors.LegacyPreActivationBehavior(this);

    internal override void LegacyCanceledActivationBehavior() => ActivationBehaviors.LegacyCanceledActivationBehavior(this);

    public override string ToString() => "[object " + Definition.Name + "]";
}

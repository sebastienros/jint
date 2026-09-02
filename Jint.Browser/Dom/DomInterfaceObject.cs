using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom;

/// <summary>
/// An interface object — the global <c>HTMLDivElement</c>, <c>Node</c>, <c>NodeList</c> — carrying the
/// interface prototype object as its <c>prototype</c> and refusing construction.
/// </summary>
/// <remarks>
/// <para>
/// Every one of them throws <c>TypeError: Illegal constructor</c>, and that is not a gap: AngleSharp puts
/// <c>[DomConstructor]</c> on concrete classes (<c>Event</c>, <c>MouseEvent</c>, <c>MutationObserver</c>,
/// <c>DOMRect</c>) and on no <c>[DomName]</c> interface at all, so not one generated interface here is
/// constructible in WebIDL terms. It is also what a browser answers for <c>new HTMLDivElement()</c>.
/// </para>
/// <para>
/// It derives from <c>Constructor</c> rather than from a plain function so that <c>x instanceof Node</c>
/// works: <c>instanceof</c> reads <c>prototype</c> and walks the chain, and <c>Constructor</c> is what makes
/// the object a callable with a <c>prototype</c> the engine recognizes.
/// </para>
/// </remarks>
internal sealed class DomInterfaceObject : Constructor
{
    private readonly DomInterfaceDefinition _definition;

    internal DomInterfaceObject(DomRealm realm, DomInterfaceDefinition definition)
        : base(realm.Engine, realm.PrincipalRealm, new JsString(definition.Name))
    {
        _definition = definition;
        _prototype = realm.PrincipalRealm.Intrinsics.Function.PrototypeObject;
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);

        // https://webidl.spec.whatwg.org/#interface-object — { writable: false, enumerable: false,
        // configurable: false }, so a script cannot repoint an interface at another prototype.
        _prototypeDescriptor = new PropertyDescriptor(realm.PrototypeOf(definition), PropertyFlag.AllForbidden);

        // The interface object of an inheriting interface has the parent's interface object as its
        // [[Prototype]], which is what makes `Object.getPrototypeOf(HTMLElement) === Element` hold and what
        // lets `Node.ELEMENT_NODE` be read off `HTMLDivElement`.
        if (definition.Parent is { } parent)
        {
            _prototype = realm.InterfaceObjectOf(parent);
        }

        // https://webidl.spec.whatwg.org/#es-constants — a constant is an own property of BOTH the interface
        // object and the interface prototype object, so `Node.ELEMENT_NODE` and `node.ELEMENT_NODE` both
        // answer. The prototype's copy comes from the shape; this is the other one.
        foreach (var constant in definition.Constants)
        {
            DefineOwnPropertyUnchecked(
                constant.Name,
                new PropertyDescriptor(JsNumber.Create(constant.Value), PropertyFlag.OnlyEnumerable));
        }
    }

    /// <summary>https://webidl.spec.whatwg.org/#es-interface-call — an interface object is not callable.</summary>
    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return JsValue.Undefined;
    }

    /// <inheritdoc />
    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    public override string ToString() => "function " + _definition.Name + "() { [native code] }";
}

using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom;

/// <summary>
/// The script-visible constructor object and prototype for a DOM interface.
/// </summary>
/// <remarks>
/// Interfaces without a declared constructor throw TypeError for construction. Constructible interfaces
/// use the Browser constructor table rather than exposing arbitrary native CLR constructors.
/// </remarks>
internal sealed class DomInterfaceObject : Constructor
{
    private readonly DomRealm _domRealm;
    private readonly DomInterfaceDefinition _definition;

    internal DomInterfaceObject(DomRealm realm, DomInterfaceDefinition definition)
        : base(realm.Engine, realm.OwningRealm, new JsString(definition.Name))
    {
        _domRealm = realm;
        _definition = definition;
        _prototype = realm.OwningRealm.Intrinsics.Function.PrototypeObject;
        _length = new PropertyDescriptor(JsNumber.Create(DomConstructors.LengthOf(definition)), PropertyFlag.Configurable);

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

        // The contract has no static members; Document's two are HTML's and installed by hand.
        if (definition.Name == "Document")
        {
            DomMarkupApis.InstallDocumentStatics(realm, this);
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
        // https://html.spec.whatwg.org/multipage/custom-elements.html#html-element-constructors:
        // `super()` inside a registered custom element constructor is a `new` on this object, and it
        // answers the element being created. Every other `new`, `new HTMLElement()` included, falls
        // through to the refusal below.
        if (CustomElements.CustomElementCreation.TryConstruct(_domRealm, _definition, this, newTarget, out var element))
        {
            return element;
        }

        if (DomConstructors.TryConstruct(_domRealm, _definition, arguments, out var instance))
        {
            return instance;
        }

        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    public override string ToString() => "function " + _definition.Name + "() { [native code] }";
}

using Jint.Native.Object;

namespace Jint.Browser.Dom;

/// <summary>
/// The wrapper for a DOM platform object that is neither a node nor a collection — everything from
/// <c>DOMImplementation</c> and <c>ValidityState</c> to <c>CSSStyleRule</c> and <c>MediaQueryList</c>.
/// </summary>
/// <remarks>
/// <para>
/// It overrides nothing. That is the point: an object with no own properties, whose members all live on its
/// shaped prototype, is <c>PropertyAccessSemantics.Ordinary</c> by derivation and reaches the
/// prototype-method inline cache with a single probe per read. Every member reads its state through the
/// AngleSharp interface at call time, so the wrapper holds no projected value to keep in step with anything.
/// </para>
/// <para>
/// Script may still put expandos on it, exactly as on a browser platform object, and they live in the
/// ordinary property dictionary; the wrapper cache is what makes them survive a round trip through the DOM.
/// </para>
/// </remarks>
internal class DomObject : ObjectInstance, IDomWrapper
{
    internal DomObject(DomRealm realm, DomInterfaceDefinition definition, object target) : base(realm.Engine)
    {
        DomRealm = realm;
        Definition = definition;
        DomTarget = target;
        Prototype = realm.PrototypeOf(definition);
    }

    /// <inheritdoc />
    public object DomTarget { get; }

    /// <inheritdoc />
    public DomRealm DomRealm { get; }

    /// <summary>The interface whose prototype this wrapper was given.</summary>
    internal DomInterfaceDefinition Definition { get; }

    public override string ToString() => "[object " + Definition.Name + "]";
}

using Jint.Native.Object;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// What every DOM collection wrapper shares: the AngleSharp object, the realm, and the interface it was
/// given a prototype from.
/// </summary>
/// <remarks>
/// <para>
/// Deriving from <c>ArrayLikeObject</c> is what puts <c>list[i]</c>, <c>for..of</c>, spread,
/// <c>Array.from</c>, destructuring and the <c>Array.prototype</c> generics on the engine's
/// one-callback-per-element lane, with no <c>Reference</c>, no key object and no descriptor. The base class
/// derives <c>GetOwnProperty</c>, both key enumerations, the probe, <c>Set</c>, <c>Delete</c> and
/// <c>DefineOwnProperty</c> from <c>Length</c> and <c>TryGetIndex</c> and keeps them consistent.
/// </para>
/// <para>
/// <c>JSON.stringify</c> serializes the collection as a JSON array rather than as an object with numeric keys,
/// following <c>ArrayLikeObject</c>'s host convention. The WebIDL <c>length</c> attribute lives on the interface
/// prototype and is emitted with the other prototype accessors.
/// </para>
/// </remarks>
internal abstract class DomCollectionBase : ArrayLikeObject, IDomWrapper
{
    private protected DomCollectionBase(DomRealm realm, DomInterfaceDefinition definition, object target)
        : base(realm.Engine)
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

    /// <summary>The WebIDL <c>length</c> attribute is supplied by the interface prototype.</summary>
    protected override bool OwnsLength => false;

    /// <summary>
    /// <c>item(index)</c>, for the hand-written <c>HTMLCollection</c> shape, whose member bodies cannot name
    /// the receiver's element type. Every other collection's <c>item</c> is generated.
    /// </summary>
    internal Jint.Native.JsValue Item(uint index) => TryGetIndex(index, out var value) ? value : Jint.Native.JsValue.Null;

    /// <summary>
    /// <c>namedItem(name)</c>, for the same reason. <c>null</c> rather than <c>undefined</c> is what
    /// https://html.spec.whatwg.org/multipage/common-dom-interfaces.html#dom-htmlcollection-nameditem answers
    /// for a name the collection does not support.
    /// </summary>
    internal Jint.Native.JsValue NamedItem(string name)
        => TryGetNamedValue(name, out var value) ? value : Jint.Native.JsValue.Null;

    public override string ToString() => "[object " + Definition.Name + "]";
}

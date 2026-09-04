using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>
/// One WebIDL interface, as the generator found it in AngleSharp: its DOM name, the CLR interface it is
/// projected from, its member layout, and where its prototype sits in the chain.
/// </summary>
/// <remarks>
/// <para>
/// A definition is process-shared and immutable, which is what lets the <see cref="Shape"/> be: a
/// <see cref="JsObjectShape"/> describes members once for every engine in the process, and is built here at
/// most once however many engines ask for it. Everything per engine — the prototype object, the interface
/// object, the wrappers in front of them — lives in <see cref="DomRealm"/>.
/// </para>
/// <para>
/// The shape is built lazily because building all of them costs about two thousand delegate registrations,
/// and a page that touches ten interfaces should pay for ten. <see cref="Index"/> is what makes the per-engine
/// side an array rather than a dictionary.
/// </para>
/// </remarks>
internal sealed class DomInterfaceDefinition
{
    private readonly Func<JsObjectShape> _shapeFactory;
    private JsObjectShape? _shape;

    internal DomInterfaceDefinition(
        string name,
        Type clrInterface,
        Func<JsObjectShape> shapeFactory,
        DomInterfaceDefinition? parent,
        bool rootsAtEventTarget,
        bool hasInterfaceObject,
        DomWrapperKind wrapperKind,
        DomCollectionAccessor? collectionAccessor = null,
        DomConstant[]? constants = null,
        int constructorLength = 0)
    {
        Constants = constants ?? [];
        ConstructorLength = constructorLength;
        Name = name;
        ClrInterface = clrInterface;
        _shapeFactory = shapeFactory;
        Parent = parent;
        RootsAtEventTarget = rootsAtEventTarget;
        HasInterfaceObject = hasInterfaceObject;
        WrapperKind = wrapperKind;
        CollectionAccessor = collectionAccessor;
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-interface-call — the interface object's <c>length</c>, which is
    /// the number of arguments the constructor <em>requires</em>. Zero for every interface the generator
    /// produces: an interface with no constructor has no required arguments, and the handful
    /// <c>DomConstructors</c> gives one to all take optional arguments only. <c>StaticRange</c> is the
    /// exception, and the reason this is a value rather than a constant.
    /// </summary>
    internal int ConstructorLength { get; }

    /// <summary>The WebIDL interface name — <c>HTMLDivElement</c>, the value of <c>[DomName]</c>.</summary>
    internal string Name { get; }

    /// <summary>The AngleSharp interface the members are projected from.</summary>
    internal Type ClrInterface { get; }

    /// <summary>
    /// The interface's own members. Built at most once per process, on first use, behind the double-checked
    /// publication a <see langword="volatile"/>-free single assignment of an immutable object is allowed:
    /// two threads racing may both build one, and the loser's is simply dropped.
    /// </summary>
    internal JsObjectShape Shape => _shape ??= _shapeFactory();

    /// <summary>
    /// The interface this one inherits from, or <see langword="null"/> when the chain ends here — at
    /// <c>EventTarget.prototype</c> if <see cref="RootsAtEventTarget"/>, at <c>Object.prototype</c> otherwise.
    /// </summary>
    internal DomInterfaceDefinition? Parent { get; }

    /// <summary>
    /// Whether the root of this chain is Jint's own <c>EventTarget.prototype</c>. That is what makes
    /// <c>document instanceof EventTarget</c> hold, and it is why <c>EventTarget</c> itself is not generated:
    /// the engine already has it, and a second one would be a second brand.
    /// </summary>
    internal bool RootsAtEventTarget { get; }

    /// <summary>
    /// Whether a global constructor property is installed for this interface — WebIDL's
    /// <c>[LegacyNoInterfaceObject]</c>, spelled <c>[DomNoInterfaceObject]</c> by AngleSharp. An interface
    /// without one still has a prototype object in the chain; it just cannot be named from script.
    /// </summary>
    internal bool HasInterfaceObject { get; }

    /// <summary>Which wrapper class an instance of this interface gets.</summary>
    internal DomWrapperKind WrapperKind { get; }

    /// <summary>
    /// How this interface answers indexed and named property lookups, or <see langword="null"/> when it has
    /// neither. Non-null exactly for <see cref="DomWrapperKind.Collection"/> and
    /// <see cref="DomWrapperKind.NamedMap"/>.
    /// </summary>
    internal DomCollectionAccessor? CollectionAccessor { get; }

    /// <summary>
    /// The interface's own WebIDL constants. They are declared twice on purpose:
    /// <a href="https://webidl.spec.whatwg.org/#es-constants">the constants</a> are own properties of both the
    /// interface prototype object and the interface object, so <c>Node.ELEMENT_NODE</c> and
    /// <c>node.ELEMENT_NODE</c> both answer. The shape carries the prototype's copy;
    /// <see cref="DomInterfaceObject"/> installs the other from this list.
    /// </summary>
    internal DomConstant[] Constants { get; }

    /// <summary>Dense index into <see cref="DomRealm"/>'s per-engine arrays. Assigned by the registry.</summary>
    internal int Index { get; set; } = -1;

    public override string ToString() => Name;
}

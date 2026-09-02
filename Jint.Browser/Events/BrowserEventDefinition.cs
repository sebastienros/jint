using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.Browser.Events;

/// <summary>
/// One event interface the browser adds to the engine's <c>Event</c> — its name, where its prototype sits in
/// the chain, and how <c>new</c> on it builds an instance.
/// </summary>
/// <remarks>
/// The shape is process-shared and built at most once however many engines ask for it, exactly as
/// <see cref="Dom.DomInterfaceDefinition"/>'s is; everything per engine — the prototype object, the interface
/// object — lives in <see cref="BrowserEventRealm"/> and is indexed by <see cref="Index"/>.
/// </remarks>
internal sealed class BrowserEventDefinition
{
    private readonly Func<JsObjectShape> _shapeFactory;
    private JsObjectShape? _shape;

    internal BrowserEventDefinition(
        string name,
        BrowserEventDefinition? parent,
        int constructorLength,
        Func<JsObjectShape> shapeFactory,
        Func<BrowserEventRealm, JsValue[], JsEvent> construct,
        (string Name, int Value)[] constants)
    {
        Name = name;
        Parent = parent;
        ConstructorLength = constructorLength;
        _shapeFactory = shapeFactory;
        Construct = construct;
        Constants = constants;
    }

    /// <summary>The WebIDL interface name — <c>MouseEvent</c>.</summary>
    internal string Name { get; }

    /// <summary>
    /// The interface this one inherits from, or <see langword="null"/> when the chain ends at the engine's own
    /// <c>Event</c>.
    /// </summary>
    internal BrowserEventDefinition? Parent { get; }

    /// <summary>
    /// The interface object's <c>length</c>. One for every interface whose second constructor argument is an
    /// optional dictionary, which is all of them but <c>FormDataEvent</c>, whose <c>formData</c> member is
    /// required and so makes the dictionary required with it.
    /// </summary>
    internal int ConstructorLength { get; }

    /// <summary>The interface's own members. Built at most once per process, on first use.</summary>
    internal JsObjectShape Shape => _shape ??= _shapeFactory();

    /// <summary>
    /// The constructor's body once <c>newTarget</c> has been resolved: read the dictionary, build the
    /// instance. The prototype is assigned by the caller, so this never reads one.
    /// </summary>
    internal Func<BrowserEventRealm, JsValue[], JsEvent> Construct { get; }

    /// <summary>
    /// The interface's own WebIDL constants — <c>WheelEvent.DOM_DELTA_PIXEL</c> and its kind. They are own
    /// properties of both the interface prototype object and the interface object
    /// (https://webidl.spec.whatwg.org/#es-constants), so the shape carries the prototype's copy and
    /// <see cref="BrowserEventInterfaceObject"/> installs the other from this list.
    /// </summary>
    internal (string Name, int Value)[] Constants { get; }

    /// <summary>Dense index into <see cref="BrowserEventRealm"/>'s per-engine arrays.</summary>
    internal int Index { get; set; } = -1;

    public override string ToString() => Name;
}

/// <summary>
/// The interface object — the global <c>MouseEvent</c> — for one <see cref="BrowserEventDefinition"/> in one
/// engine.
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is the parent interface object rather than <c>%Function.prototype%</c>, which is
/// what https://webidl.spec.whatwg.org/#interface-object says for an interface that inherits and what makes
/// <c>Object.getPrototypeOf(PointerEvent) === MouseEvent</c> hold.
/// </remarks>
internal sealed class BrowserEventInterfaceObject : Constructor
{
    private static readonly JsString _prototypeKey = new("prototype");

    private readonly BrowserEventRealm _eventRealm;
    private readonly BrowserEventDefinition _definition;

    internal BrowserEventInterfaceObject(
        BrowserEventRealm eventRealm,
        BrowserEventDefinition definition,
        ObjectInstance prototype,
        JsValue parentInterface)
        : base(eventRealm.Engine, eventRealm.PrincipalRealm, new JsString(definition.Name))
    {
        _eventRealm = eventRealm;
        _definition = definition;
        _prototype = parentInterface as ObjectInstance;
        _length = new PropertyDescriptor(JsNumber.Create(definition.ConstructorLength), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(prototype, PropertyFlag.AllForbidden);

        foreach (var (name, value) in definition.Constants)
        {
            // https://webidl.spec.whatwg.org/#es-constants — { writable: false, enumerable: true,
            // configurable: false }, the same attributes the prototype's copy gets from JsObjectShape.Constant.
            SetProperty(name, new PropertyDescriptor(JsNumber.Create(value), PropertyFlag.OnlyEnumerable));
        }
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-interface-call — calling an interface object without <c>new</c> is a
    /// <c>TypeError</c>.
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        Throw.TypeError(_realm, "Failed to construct '" + _definition.Name + "': Please use the 'new' operator, this DOM object constructor cannot be called as a function.");
        return JsValue.Undefined;
    }

    public override ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
    {
        var instance = _definition.Construct(_eventRealm, arguments);

        // https://tc39.es/ecma262/#sec-ordinarycreatefromconstructor: the prototype comes from newTarget, so
        // `class Mine extends MouseEvent {}` gives an instance Mine.prototype is on. The fallback is this
        // interface's own prototype, which is reached only when newTarget carries no object `prototype`.
        instance._prototype = newTarget.Get(_prototypeKey) as ObjectInstance ?? _eventRealm.PrototypeOf(_definition);
        return instance;
    }

    public override string ToString() => "function " + _definition.Name + "() { [native code] }";
}

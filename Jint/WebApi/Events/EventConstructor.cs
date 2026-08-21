#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// The <c>Event</c> interface object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-event
/// </para>
/// </summary>
/// <remarks>
/// The four phase constants appear here as well as on the prototype, per
/// https://webidl.spec.whatwg.org/#es-constants, with the attributes constants are given there:
/// <c>{ writable: false, enumerable: true, configurable: false }</c>. That section defines them one
/// after another in the order the IDL declares them, and that order is observable, so they are declared
/// below in it and <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class EventConstructor : Constructor
{
    private static readonly JsString _functionName = new("Event");

    [JsProperty(Name = "NONE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber None = JsNumber.Create(JsEvent.PhaseNone);
    [JsProperty(Name = "CAPTURING_PHASE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber CapturingPhase = JsNumber.Create(JsEvent.PhaseCapturing);
    [JsProperty(Name = "AT_TARGET", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber AtTarget = JsNumber.Create(JsEvent.PhaseAtTarget);
    [JsProperty(Name = "BUBBLING_PHASE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber BubblingPhase = JsNumber.Create(JsEvent.PhaseBubbling);

    internal EventConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new EventPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal EventPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-event, which is the event constructing steps followed by the
    /// inner event creation steps.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = RequireType(_realm, arguments, "Event");
        var init = ReadEventInit(_realm, arguments.At(1), "Event");

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Event.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp) state)
                => new JsEvent(engine, state.Type, state.Init, state.TimeStamp),
            (Type: type, Init: init, TimeStamp: TimeStampNow(_engine)));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire step 2: an event the engine creates for itself, whose
    /// <c>isTrusted</c> is therefore true. Not reachable from script — the only trusted event Jint fires today
    /// is <c>abort</c>.
    /// </summary>
    internal JsEvent CreateTrustedEvent(JsString type)
    {
        return new JsEvent(_engine, type, default, TimeStampNow(_engine))
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }

    /// <summary>
    /// The <c>type</c> argument of an event constructor: required, and a <c>DOMString</c>, so anything at all
    /// is accepted and stringified but omitting it is a <c>TypeError</c>.
    /// </summary>
    internal static JsString RequireType(Realm realm, JsCallArguments arguments, string interfaceName)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(realm, $"Failed to construct '{interfaceName}': 1 argument required, but only 0 present.");
        }

        return TypeConverter.ToJsString(arguments[0]);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dictdef-eventinit converted per
    /// https://webidl.spec.whatwg.org/#es-dictionary: <see langword="undefined"/> and <see langword="null"/>
    /// give every member its default, anything that is not an object is a <c>TypeError</c>, and the members
    /// are read in the order the dictionary declares them.
    /// </summary>
    internal static EventInit ReadEventInit(Realm realm, JsValue init, string interfaceName)
    {
        if (init.IsUndefined() || init.IsNull())
        {
            return default;
        }

        if (init is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm, $"Failed to construct '{interfaceName}': the provided value is not of type '{interfaceName}Init'.");
            return default;
        }

        var bubbles = TypeConverter.ToBoolean(dictionary.Get(CommonEventProperties.Bubbles));
        var cancelable = TypeConverter.ToBoolean(dictionary.Get(CommonEventProperties.Cancelable));
        var composed = TypeConverter.ToBoolean(dictionary.Get(CommonEventProperties.Composed));

        return new EventInit(bubbles, cancelable, composed);
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#inner-event-creation-steps step 3, "the relative high resolution coarse
    /// time given time": milliseconds since this engine's time origin. Not coarsened — the coarsening in the
    /// specification is a browser's fingerprinting defence, and an embedded engine has no cross-origin script
    /// to defend against.
    /// </summary>
    internal static double TimeStampNow(Engine engine) => engine._webApi?.CurrentHighResolutionTime ?? 0;
}

/// <summary>
/// The dictionary member names the event constructors read, interned once.
/// </summary>
internal static class CommonEventProperties
{
    internal static readonly JsString Bubbles = new("bubbles");
    internal static readonly JsString Cancelable = new("cancelable");
    internal static readonly JsString Composed = new("composed");
    internal static readonly JsString Detail = new("detail");
    internal static readonly JsString Capture = new("capture");
    internal static readonly JsString Once = new("once");
    internal static readonly JsString Passive = new("passive");
    internal static readonly JsString Signal = new("signal");
}
#endif

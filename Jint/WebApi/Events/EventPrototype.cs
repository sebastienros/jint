#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.WebApi.Events;

/// <summary>
/// <c>Event.prototype</c> — the interface prototype object.
/// <para>
/// https://dom.spec.whatwg.org/#interface-event
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every attribute is an accessor here rather than an own property of the instance, as WebIDL specifies
/// attributes; each brand-checks its receiver and raises a <c>TypeError</c> for anything that is not an
/// <c>Event</c> — including <c>Event.prototype</c> itself, which is not one.
/// </para>
/// <para>
/// One documented simplification against WebIDL, shared with <c>console</c>: the operations are
/// non-enumerable, where a WebIDL interface prototype object's operations are enumerable. The attributes and
/// the constants <i>are</i> enumerable, as they should be. Nothing but code inspecting property attributes can
/// observe the difference.
/// </para>
/// <para>
/// https://webidl.spec.whatwg.org/#es-constants defines the constants one after another in the order the IDL
/// declares them, and that order is observable, so they are declared below in it and
/// <c>PreserveDeclarationOrder</c> keeps the generator from sorting them by name.
/// </para>
/// </remarks>
[JsObject(UseShape = true, PreserveDeclarationOrder = true)]
internal sealed partial class EventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly EventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString EventToStringTag = new("Event");

    [JsProperty(Name = "NONE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber None = JsNumber.Create(JsEvent.PhaseNone);
    [JsProperty(Name = "CAPTURING_PHASE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber CapturingPhase = JsNumber.Create(JsEvent.PhaseCapturing);
    [JsProperty(Name = "AT_TARGET", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber AtTarget = JsNumber.Create(JsEvent.PhaseAtTarget);
    [JsProperty(Name = "BUBBLING_PHASE", Flags = PropertyFlag.OnlyEnumerable)] private static readonly JsNumber BubblingPhase = JsNumber.Create(JsEvent.PhaseBubbling);

    internal EventPrototype(
        Engine engine,
        Realm realm,
        EventConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-type
    /// </summary>
    [JsAccessor("type", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString TypeGet(JsValue thisObject) => Brand(thisObject).EventType;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-target
    /// </summary>
    [JsAccessor("target", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue TargetGet(JsValue thisObject) => Brand(thisObject).Target;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-currenttarget
    /// </summary>
    [JsAccessor("currentTarget", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue CurrentTargetGet(JsValue thisObject) => Brand(thisObject).CurrentTarget;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-eventphase
    /// </summary>
    [JsAccessor("eventPhase", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber EventPhaseGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).EventPhase);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-bubbles
    /// </summary>
    [JsAccessor("bubbles", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean BubblesGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Bubbles);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-cancelable
    /// </summary>
    [JsAccessor("cancelable", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean CancelableGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Cancelable);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-defaultprevented
    /// </summary>
    [JsAccessor("defaultPrevented", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean DefaultPreventedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).CanceledFlag);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-composed
    /// </summary>
    [JsAccessor("composed", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean ComposedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).Composed);

    /// <summary>
    /// The attribute getter for <c>isTrusted</c> — the one member of the interface that does <b>not</b> live
    /// on this object. https://dom.spec.whatwg.org/#dom-event-istrusted declares it
    /// <c>[LegacyUnforgeable]</c>, so https://webidl.spec.whatwg.org/#es-attributes removes it from the
    /// prototype's attribute set ("Remove from <i>attributes</i> all the attributes that are unforgeable")
    /// and installs it on every instance instead — see <see cref="JsEvent.GetOwnProperty"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is nevertheless created here, once per realm, because that is what an attribute getter is: one
    /// function object for the interface rather than one per object that carries it.
    /// <c>dom/events/Event-isTrusted.any.js</c> reads the descriptor off two separate events and requires the
    /// same getter in both, and every event's descriptor is built from this one.
    /// </para>
    /// <para>
    /// Realm-pinned rather than built against whichever realm happens to be running when the first event asks
    /// for it, for the reason the internal <see cref="ClrFunction"/> constructor documents. Its <c>length</c>
    /// is a configurable, non-writable, non-enumerable <c>0</c>, which is what every other member of this
    /// prototype carries.
    /// </para>
    /// </remarks>
    internal Function IsTrustedGetter =>
        _isTrustedGetter ??= new ClrFunction(
            _engine,
            _realm,
            "get isTrusted",
            (thisObject, _) => JsBoolean.Create(Brand(thisObject).IsTrusted),
            length: 0,
            PropertyFlag.Configurable);

    private ClrFunction? _isTrustedGetter;

    /// <summary>
    /// The descriptor every event's own <c>isTrusted</c> is answered with:
    /// <c>{ [[Get]]: <see cref="IsTrustedGetter"/>, [[Set]]: undefined, [[Enumerable]]: true,
    /// [[Configurable]]: false }</c>, per https://webidl.spec.whatwg.org/#es-attributes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One instance for the realm, shared by every event, so carrying the property costs an event nothing and
    /// reading it allocates nothing either. An accessor descriptor is unwrapped by invoking its getter with
    /// the <i>receiver</i>, so one descriptor answering for every event is exactly right.
    /// </para>
    /// <para>
    /// Sharing is safe because the descriptor is non-configurable and has no setter, which puts it out of
    /// reach of every mutating branch of <c>ObjectInstance.ValidateAndApplyPropertyDescriptor</c>
    /// (https://tc39.es/ecma262/#sec-validateandapplypropertydescriptor): an incoming <c>[[Value]]</c> or
    /// <c>[[Writable]]</c> field makes that descriptor a data one and the data-versus-accessor mismatch is
    /// refused before any write; a differing <c>[[Enumerable]]</c>, or a <c>[[Configurable]]</c> of
    /// <see langword="true"/>, is refused for the same reason, so the only assignments that survive write back
    /// the values already there; and a <c>[[Get]]</c>/<c>[[Set]]</c> field is applied to a <i>copy</i>. It is
    /// the argument the engine already makes process-wide for
    /// <c>PropertyDescriptor.AllForbiddenDescriptor</c>, which every function's <c>length</c> shares.
    /// </para>
    /// </remarks>
    internal PropertyDescriptor IsTrustedDescriptor =>
        _isTrustedDescriptor ??= new GetSetPropertyDescriptor(IsTrustedGetter, set: null, enumerable: true, configurable: false);

    private GetSetPropertyDescriptor? _isTrustedDescriptor;

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-timestamp
    /// </summary>
    [JsAccessor("timeStamp", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsNumber TimeStampGet(JsValue thisObject) => JsNumber.Create(Brand(thisObject).TimeStamp);

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-composedpath. The path is the single-item «target» while a
    /// dispatch is running and empty otherwise, so this answers <c>[target]</c> or <c>[]</c> — which is what a
    /// browser answers for a target that is not in a tree.
    /// </summary>
    [JsFunction(Name = "composedPath", Length = 0)]
    private JsArray ComposedPath(JsValue thisObject)
    {
        var ev = Brand(thisObject);
        if (!ev.DispatchFlag || ev.CurrentTarget.IsNull())
        {
            return _realm.Intrinsics.Array.ArrayCreate(0);
        }

        return _realm.Intrinsics.Array.CreateArrayFromList(new[] { ev.CurrentTarget });
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-stoppropagation
    /// </summary>
    [JsFunction(Name = "stopPropagation", Length = 0)]
    private JsValue StopPropagation(JsValue thisObject)
    {
        Brand(thisObject).StopPropagationFlag = true;
        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-stopimmediatepropagation
    /// </summary>
    [JsFunction(Name = "stopImmediatePropagation", Length = 0)]
    private JsValue StopImmediatePropagation(JsValue thisObject)
    {
        var ev = Brand(thisObject);
        ev.StopPropagationFlag = true;
        ev.StopImmediatePropagationFlag = true;
        return Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-preventdefault
    /// </summary>
    [JsFunction(Name = "preventDefault", Length = 0)]
    private JsValue PreventDefault(JsValue thisObject)
    {
        Brand(thisObject).SetCanceledFlag();
        return Undefined;
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing the
    /// interface raises a <c>TypeError</c>.
    /// </summary>
    private JsEvent Brand(JsValue thisObject)
    {
        if (thisObject is JsEvent ev)
        {
            return ev;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not an Event");
        return null!;
    }
}
#endif

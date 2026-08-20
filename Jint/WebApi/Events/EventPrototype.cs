#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

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
/// </remarks>
[JsObject(UseShape = true)]
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
    /// https://dom.spec.whatwg.org/#dom-event-istrusted. <c>[LegacyUnforgeable]</c> in the IDL, which makes it
    /// a non-configurable own accessor of the instance in a browser; it is an ordinary prototype accessor here,
    /// so a script can shadow it. Nothing in the engine trusts the JavaScript-visible value — the flag the
    /// dispatch algorithm reads is the CLR one.
    /// </summary>
    [JsAccessor("isTrusted", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsBoolean IsTrustedGet(JsValue thisObject) => JsBoolean.Create(Brand(thisObject).IsTrusted);

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

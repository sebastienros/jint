#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Events;

/// <summary>
/// An <c>Event</c> instance — the object that signals "something has occurred".
/// <para>
/// https://dom.spec.whatwg.org/#interface-event
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The whole state lives in CLR fields here and <see cref="EventPrototype"/> reads it through a brand check,
/// exactly as <c>DOMException</c> does. One member is not on the prototype: <c>isTrusted</c> is
/// <c>[LegacyUnforgeable]</c>, so it is an own accessor of every instance — see
/// <see cref="GetOwnProperty"/>, which synthesizes it rather than storing it. It is the one name
/// <c>Object.getOwnPropertyNames(new Event('x'))</c> reports, in a browser and here.
/// </para>
/// <para>
/// The class is not sealed because <see cref="JsCustomEvent"/> derives from it, which is how
/// <c>CustomEvent</c>'s brand check can be "is a <c>JsEvent</c> that also carries a detail".
/// </para>
/// <para>
/// Deliberately absent: the touch target list, which belongs to <c>TouchEvent</c> — an interface that does
/// not exist here, so every path item's list is empty and nothing is modelled for it. The interface's legacy
/// members — <c>srcElement</c>, <c>returnValue</c>, <c>cancelBubble</c>, <c>initEvent()</c> and
/// <c>initCustomEvent()</c> — <i>are</i> implemented; see <see cref="EventPrototype"/> and
/// <see cref="CustomEventPrototype"/>. <c>relatedTarget</c> is carried here, because
/// https://dom.spec.whatwg.org/#concept-event-dispatch retargets it per path item, but it is exposed by no
/// interface this engine ships: it becomes visible only once a host declares the <c>MouseEvent</c> or
/// <c>FocusEvent</c> attribute that reads it.
/// </para>
/// </remarks>
internal class JsEvent : ObjectInstance
{
    /// <summary>
    /// The one own property every event carries — https://dom.spec.whatwg.org/#dom-event-istrusted.
    /// </summary>
    private static readonly JsString _isTrusted = new("isTrusted");

    private static readonly Key _isTrustedKey = "isTrusted";

    /// <summary>Not currently dispatched — https://dom.spec.whatwg.org/#dom-event-none.</summary>
    internal const int PhaseNone = 0;

    /// <summary>https://dom.spec.whatwg.org/#dom-event-capturing_phase.</summary>
    internal const int PhaseCapturing = 1;

    /// <summary>https://dom.spec.whatwg.org/#dom-event-at_target.</summary>
    internal const int PhaseAtTarget = 2;

    /// <summary>https://dom.spec.whatwg.org/#dom-event-bubbling_phase.</summary>
    internal const int PhaseBubbling = 3;

    internal JsEvent(Engine engine, JsString type, EventInit init, double timeStamp)
        : base(engine, ObjectClass.Object)
    {
        EventType = type;
        TypeName = type.ToString();
        Bubbles = init.Bubbles;
        Cancelable = init.Cancelable;
        Composed = init.Composed;
        TimeStamp = timeStamp;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-type. Spelled <c>EventType</c> rather than <c>Type</c> because
    /// <see cref="JsValue.Type"/> already means the JavaScript type of the value itself.
    /// </summary>
    /// <remarks>
    /// Settable only through <see cref="InitializeEvent"/>, which is the whole of what <c>initEvent()</c>
    /// does; nothing else on the interface can change an event's type once it exists.
    /// </remarks>
    internal JsString EventType { get; private set; }

    /// <summary>
    /// The same string as <see cref="EventType"/>, materialized once. Dispatch compares it against every
    /// listener's type, and a <see cref="JsString"/> can be a rope whose <c>ToString</c> is not free.
    /// </summary>
    internal string TypeName { get; private set; }

    /// <summary>https://dom.spec.whatwg.org/#dom-event-bubbles.</summary>
    internal bool Bubbles { get; private set; }

    /// <summary>https://dom.spec.whatwg.org/#dom-event-cancelable.</summary>
    internal bool Cancelable { get; private set; }

    /// <summary>https://dom.spec.whatwg.org/#dom-event-composed — the composed flag.</summary>
    internal bool Composed { get; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-timestamp — milliseconds since the engine's time origin.
    /// </summary>
    internal double TimeStamp { get; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-event-istrusted. False for anything a script constructed, true only
    /// for an event the engine itself creates and dispatches — today that is the <c>abort</c> event.
    /// </summary>
    internal bool IsTrusted { get; set; }

    /// <summary>https://dom.spec.whatwg.org/#dom-event-target, or <see cref="JsValue.Null"/>.</summary>
    internal JsValue Target { get; set; } = Null;

    /// <summary>https://dom.spec.whatwg.org/#dom-event-currenttarget, or <see cref="JsValue.Null"/>.</summary>
    internal JsValue CurrentTarget { get; set; } = Null;

    /// <summary>https://dom.spec.whatwg.org/#dom-event-eventphase.</summary>
    internal int EventPhase { get; set; } = PhaseNone;

    /// <summary>https://dom.spec.whatwg.org/#stop-propagation-flag.</summary>
    internal bool StopPropagationFlag { get; set; }

    /// <summary>https://dom.spec.whatwg.org/#stop-immediate-propagation-flag.</summary>
    internal bool StopImmediatePropagationFlag { get; set; }

    /// <summary>https://dom.spec.whatwg.org/#canceled-flag.</summary>
    internal bool CanceledFlag { get; set; }

    /// <summary>https://dom.spec.whatwg.org/#in-passive-listener-flag.</summary>
    internal bool InPassiveListenerFlag { get; set; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dispatch-flag. Set for the duration of one <c>dispatchEvent</c>, which is
    /// what makes a re-entrant dispatch of the same event an <c>InvalidStateError</c> and what
    /// <c>composedPath()</c> answers from.
    /// </summary>
    internal bool DispatchFlag { get; set; }

    /// <summary>
    /// https://dom.spec.whatwg.org/#event-relatedtarget — the event's related target, which
    /// https://dom.spec.whatwg.org/#concept-event-dispatch retargets against every invocation target on the
    /// path and which decides where a dispatch stops.
    /// </summary>
    /// <remarks>
    /// A slot on every event rather than a member of a derived interface, because <i>dispatch</i> reads and
    /// writes it for events of any type; the interfaces that expose it — <c>MouseEvent</c>,
    /// <c>FocusEvent</c> — do not exist in this engine, so nothing script can reach observes it until a host
    /// declares one. It is deliberately not restored after a dispatch: the specification leaves the last
    /// item's retargeted value in place unless <i>clearTargets</i> nulls it.
    /// </remarks>
    internal JsEventTarget? RelatedTarget { get; set; }

    /// <summary>
    /// Whether this is <i>isActivationEvent</i> — https://dom.spec.whatwg.org/#concept-event-dispatch step
    /// 6.4, "true if event is a <c>MouseEvent</c> object and event's <c>type</c> attribute is <c>click</c>".
    /// </summary>
    /// <remarks>
    /// False here and everywhere in this engine, which has no <c>MouseEvent</c>: activation behaviour is
    /// reachable only once a host declares that interface and overrides this on it.
    /// </remarks>
    internal virtual bool IsActivationEvent => false;

    /// <summary>
    /// https://dom.spec.whatwg.org/#event-path, or <see langword="null"/> when no dispatch has ever built one.
    /// </summary>
    /// <remarks>
    /// Empty outside a dispatch and empty throughout a tree-less one, which is what keeps an
    /// <c>AbortSignal</c>, a <c>MessagePort</c> or a global <c>error</c> event allocating exactly what it did
    /// before the path existed: only <see cref="EventDispatch"/> ever calls <see cref="EnsurePath"/>, and only
    /// a target that reports <see cref="JsEventTarget.IsNode"/> reaches it. The list is cleared rather than
    /// dropped at the end of a dispatch, so an event dispatched repeatedly through a tree reuses its capacity.
    /// </remarks>
    internal List<EventPathItem>? Path { get; private set; }

    /// <summary>The path list, created on the first tree dispatch this event takes part in.</summary>
    internal List<EventPathItem> EnsurePath() => Path ??= new List<EventPathItem>();

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-initialize — <i>initialize an event</i>, which is the whole
    /// body of <c>initEvent()</c> and the first two steps of <c>initCustomEvent()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Step 1, "set event's initialized flag", has nothing to set. The flag is unset by exactly one algorithm
    /// in the standard, <c>document.createEvent()</c>, and read by exactly one, <c>dispatchEvent</c>'s
    /// <c>InvalidStateError</c> guard. There is no <c>document</c> here, so every event Jint can build has the
    /// flag set from birth and no observation can tell a stored flag from an assumed one.
    /// </para>
    /// <para>
    /// What it deliberately does <b>not</b> touch is the composed flag, which is why the specification notes
    /// that <c>initEvent()</c> "is redundant with event constructors and incapable of setting composed".
    /// </para>
    /// </remarks>
    internal void InitializeEvent(JsString type, bool bubbles, bool cancelable)
    {
        // Step 2.
        StopPropagationFlag = false;
        StopImmediatePropagationFlag = false;
        CanceledFlag = false;

        // Steps 3 and 4.
        IsTrusted = false;
        Target = Null;

        // Steps 5 to 7.
        EventType = type;
        TypeName = type.ToString();
        Bubbles = bubbles;
        Cancelable = cancelable;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-initialize step "set the canceled flag": a non-cancelable
    /// event, and a listener that registered itself as passive, cannot cancel anything.
    /// </summary>
    internal void SetCanceledFlag()
    {
        if (Cancelable && !InPassiveListenerFlag)
        {
            CanceledFlag = true;
        }
    }

    /// <summary>
    /// The unforgeable <c>isTrusted</c>, synthesized on demand rather than stored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[LegacyUnforgeable]</c> (https://webidl.spec.whatwg.org/#LegacyUnforgeable) means the property is
    /// "non-configurable and … exist[s] as an own property on the object itself rather than on its
    /// prototype", and https://webidl.spec.whatwg.org/#es-attributes gives it
    /// <c>{ [[Get]]: getter, [[Set]]: undefined, [[Enumerable]]: true, [[Configurable]]: false }</c>. The
    /// getter is the interface's, one per realm, so two events answer the identical function object.
    /// </para>
    /// <para>
    /// Nothing is written into the property dictionary for it, and nothing is allocated to answer it: the
    /// descriptor is the realm's single <see cref="EventPrototype.IsTrustedDescriptor"/>. That matters
    /// because events are not rare — one per <c>dispatchEvent</c>, and more throughout the abort, message,
    /// worker and fetch paths — and a stored descriptor would cost a <c>PropertyDictionary</c>, a list node
    /// and the descriptor, measured at <b>+184 bytes on a 112-byte event</b>. As it is, an event allocates
    /// exactly what it did before this member existed, and so does a read of it. The shape is
    /// <c>Function</c>'s for <c>length</c>/<c>name</c>/<c>prototype</c> and <c>JsError</c>'s for
    /// <c>message</c>: an own property the type always has, answered from
    /// <see cref="ObjectInstance.GetOwnProperty"/> and listed by
    /// <see cref="ObjectInstance.GetInitialOwnStringPropertyKeys"/>.
    /// </para>
    /// <para>
    /// Because the property is non-configurable and has no setter, it can be neither deleted nor redefined
    /// into anything else, so it is a constant of the type: always present, never removable. That is what
    /// keeps the interpreter's version-gated prototype cache sound for an event receiver — no own property
    /// of an event ever joins or leaves its own-property set without <c>_propertiesVersion</c> moving. The
    /// one redefinition the specification does permit is an identical one
    /// (<c>{ get: theSameGetter, set: undefined }</c>), which the ordinary machinery stores in the dictionary;
    /// the two lookups below prefer that copy so the key is never reported twice.
    /// </para>
    /// </remarks>
    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (_isTrusted.Equals(property))
        {
            var stored = base.GetOwnProperty(property);
            return ReferenceEquals(stored, PropertyDescriptor.Undefined) ? IsTrustedDescriptor : stored;
        }

        return base.GetOwnProperty(property);
    }

    internal override IEnumerable<JsValue> GetInitialOwnStringPropertyKeys()
    {
        if (!HasStoredIsTrusted)
        {
            yield return _isTrusted;
        }
    }

    private bool HasStoredIsTrusted => _properties?.ContainsKey(_isTrustedKey) == true;

    private PropertyDescriptor IsTrustedDescriptor => _engine.Realm.Intrinsics.Event.PrototypeObject.IsTrustedDescriptor;
}

/// <summary>
/// A <c>CustomEvent</c> instance: an <see cref="JsEvent"/> that also carries the script's own payload.
/// <para>
/// https://dom.spec.whatwg.org/#interface-customevent
/// </para>
/// </summary>
internal sealed class JsCustomEvent : JsEvent
{
    internal JsCustomEvent(Engine engine, JsString type, EventInit init, double timeStamp, JsValue detail)
        : base(engine, type, init, timeStamp)
    {
        Detail = detail;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-customevent-detail. The IDL default is <c>null</c>, not
    /// <c>undefined</c>.
    /// </summary>
    /// <remarks>
    /// Settable only through <c>initCustomEvent()</c>, whose step 3 is "set this's detail attribute to
    /// detail" — https://dom.spec.whatwg.org/#dom-customevent-initcustomevent.
    /// </remarks>
    internal JsValue Detail { get; set; }
}

/// <summary>
/// The <c>EventInit</c> dictionary, https://dom.spec.whatwg.org/#dictdef-eventinit, after conversion.
/// </summary>
/// <param name="Bubbles">Whether the event would travel back up a tree; recorded, never acted on here.</param>
/// <param name="Cancelable">Whether <c>preventDefault()</c> can set the canceled flag.</param>
/// <param name="Composed">The composed flag, which only a shadow tree could observe.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct EventInit(bool Bubbles, bool Cancelable, bool Composed);
#endif

#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
using Jint.Native;
using Jint.Native.Object;

namespace Jint.WebApi.Events;

/// <summary>
/// An <c>Event</c> instance — the object that signals "something has occurred".
/// <para>
/// https://dom.spec.whatwg.org/#interface-event
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Every IDL attribute of <c>Event</c> is read-only, so the whole state lives in CLR fields here and
/// <see cref="EventPrototype"/> reads it through a brand check, exactly as <c>DOMException</c> does. An
/// instance therefore has no own property at all, which is what a browser reports for
/// <c>Object.getOwnPropertyNames(new Event('x'))</c>.
/// </para>
/// <para>
/// The class is not sealed because <see cref="JsCustomEvent"/> derives from it, which is how
/// <c>CustomEvent</c>'s brand check can be "is a <c>JsEvent</c> that also carries a detail".
/// </para>
/// <para>
/// Deliberately absent, all of them marked legacy by the specification and none of them reachable by a
/// script written for a non-browser runtime: <c>srcElement</c>, <c>cancelBubble</c>, <c>returnValue</c>,
/// <c>initEvent()</c> and <c>initCustomEvent()</c>. <c>relatedTarget</c> and the touch target list belong to
/// interfaces (<c>UIEvent</c>, <c>TouchEvent</c>) that do not exist here.
/// </para>
/// </remarks>
internal class JsEvent : ObjectInstance
{
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
    internal JsString EventType { get; }

    /// <summary>
    /// The same string as <see cref="EventType"/>, materialized once. Dispatch compares it against every
    /// listener's type, and a <see cref="JsString"/> can be a rope whose <c>ToString</c> is not free.
    /// </summary>
    internal string TypeName { get; }

    /// <summary>https://dom.spec.whatwg.org/#dom-event-bubbles.</summary>
    internal bool Bubbles { get; }

    /// <summary>https://dom.spec.whatwg.org/#dom-event-cancelable.</summary>
    internal bool Cancelable { get; }

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
    internal JsValue Detail { get; }
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
